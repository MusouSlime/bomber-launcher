#!/usr/bin/env python3
"""Bomber Launcher — QoL front-end for Super Bomberman R (Steam 702700).

All artwork is extracted from the game itself (see tools/extract_ui_assets.py).
"""

import hashlib
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import threading
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from collections import OrderedDict
from pathlib import Path

import gi
gi.require_version("Gtk", "4.0")
gi.require_version("Adw", "1")
from gi.repository import Adw, Gdk, Gio, GLib, GObject, Gtk  # noqa: E402

APP_ID = "io.github.bomberlauncher.BomberLauncher"
APP_NAME = "Bomber Launcher"
STEAM_APPID = "702700"
GAME_DIR_NAME = "Super Bomberman R"
SBR2_APPID = "1913740"
SBR2_DIR_NAME = "SUPER BOMBERMAN R2"
SBR2_CFG_REL = Path("BepInEx/config/sbr2.qol.cfg")
SBR2_PLUGIN_REL = Path("BepInEx/plugins/SBR2QoL.dll")

HOME = Path.home()


def _portable_dir():
    """Writable data folder next to a portable build (AppImage / unpacked)."""
    candidates = []
    data = os.environ.get("BOMBER_DATA")
    if data:
        candidates.append(Path(data))
    appimage = os.environ.get("APPIMAGE")
    if appimage:
        candidates.append(Path(appimage).resolve().parent / "BomberLauncher.data")
    appdir = os.environ.get("APPDIR")
    if appdir:
        candidates.append(Path(appdir).resolve().parent / "BomberLauncher.data")
    for c in candidates:
        try:
            c.mkdir(parents=True, exist_ok=True)
            probe = c / ".write-test"
            probe.write_text("ok")
            probe.unlink()
            return c
        except Exception:
            continue
    return None


PORTABLE = _portable_dir()
MODKIT = Path(os.environ.get("SBR_MODKIT",
    (PORTABLE / "modkit") if PORTABLE else (HOME / "Projects" / "sbr-modkit")))
BACKUPS = MODKIT / "backups"
VIDEO_BACKUPS = BACKUPS / "videos"
SAVE_BACKUPS = BACKUPS / "saves"
ASSETS = Path(__file__).resolve().parent / "assets"


def _writable_dir(path):
    try:
        path.mkdir(parents=True, exist_ok=True)
        probe = path / ".write-test"
        probe.write_text("ok")
        probe.unlink()
        return True
    except Exception:
        return False


def resolve_art_dir():
    env = os.environ.get("BOMBER_ASSETS")
    if env:
        return Path(env)
    if (ASSETS / "hero.png").exists():
        return ASSETS
    candidates = []
    if PORTABLE:
        candidates.append(PORTABLE / "art")
    candidates.append(HOME / ".local/share/bomber-launcher/assets")
    for candidate in candidates:
        if _writable_dir(candidate):
            return candidate
    return Path(tempfile.mkdtemp(prefix="bomber-art-"))


ART = resolve_art_dir()
HAVE_ART = (ART / "hero.png").exists()
CONFIG_PATH = (PORTABLE / "config.json") if PORTABLE else (HOME / ".config" / "bomber-launcher" / "config.json")
AUDIO_CACHE_NAME = "bgm_cache"


def _bgm_cache_dir():
    base = PORTABLE if PORTABLE else (HOME / ".local/share/bomber-launcher")
    return base / AUDIO_CACHE_NAME


def find_sbr2_dir():
    env = os.environ.get("SBR2_GAME")
    if env:
        p = Path(env)
        if p.is_dir():
            return p
    p = (HOME / ".local/share/Steam/steamapps/common/SUPER BOMBERMAN R2")
    return p if p.is_dir() else None


def set_sbr2_plugin_value(section, key, value):
    game = find_sbr2_dir()
    if not game:
        raise RuntimeError("SBR2 game folder not found")
    path = game / "BepInEx/config/sbr2.qol.cfg"
    text = path.read_text(errors="replace") if path.exists() else ""
    out = text.splitlines()
    in_sec = False
    found = False
    for i, line in enumerate(out):
        s = line.strip()
        if s.startswith("[") and s.endswith("]"):
            in_sec = s[1:-1].strip() == section
            found = found or in_sec
        elif in_sec and "=" in s and s.split("=", 1)[0].strip() == key:
            out[i] = f"{key} = {value}"
    if not found:
        out.extend(["", f"[{section}]", f"{key} = {value}"])
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(out).rstrip() + "\n")
    return path


def get_sbr2_plugin_value(section, key, default=None):
    game = find_sbr2_dir()
    if not game:
        return default
    path = game / SBR2_CFG_REL
    if not path.exists():
        return default
    current = None
    try:
        for line in path.read_text(errors="replace").splitlines():
            s = line.strip()
            if s.startswith("[") and s.endswith("]"):
                current = s[1:-1].strip()
            elif current == section and "=" in s and not s.startswith("#"):
                k, v = s.split("=", 1)
                if k.strip() == key:
                    return v.strip()
    except Exception:
        pass
    return default


def sbr2_interop_ready():
    game = find_sbr2_dir()
    return bool(game and (game / "BepInEx/interop/GameAssembly.dll").exists())


def sbr2_plugin_installed():
    game = find_sbr2_dir()
    return bool(game and (game / SBR2_PLUGIN_REL).exists())


def install_sbr2_plugin():
    game = find_sbr2_dir()
    if not game:
        raise RuntimeError("SBR2 game folder not found")
    if not (game / "BepInEx/core").is_dir():
        raise RuntimeError("Install BepInEx 6 IL2CPP x64 into SBR2 first")
    src = Path(__file__).resolve().parent / "plugin-sbr2" / "bin"
    dll = src / "SBR2QoL.dll"
    if not dll.exists():
        raise RuntimeError("Build plugin-sbr2 first (plugin-sbr2/build.sh)")
    dest = game / "BepInEx/plugins"
    dest.mkdir(parents=True, exist_ok=True)
    shutil.copy2(dll, dest / dll.name)
    nvorbis = src / "NVorbis.dll"
    if nvorbis.exists():
        shutil.copy2(nvorbis, dest / nvorbis.name)
    return dest


def apply_sbr2_bgm(source=None):
    """Point SBR2 at the launcher's OGG music cache."""
    game = find_sbr2_dir()
    if not game:
        print("SBR2 game folder not found")
        return 1
    if source is not None:
        source = Path(source)
        if not source.is_dir():
            print("music folder not found:", source)
            return 1
        print("converting", source, "to OGG for SBR2...")
        dest, done, skipped = convert_bgm_folder(source)
        if skipped:
            print(f"skipped {skipped} track(s)")
    else:
        dest = _bgm_cache_dir()
    dest = Path(dest)
    count = len([p for p in dest.rglob("*")
                 if p.is_file() and p.suffix.lower() in (".ogg", ".wav")])
    if count == 0:
        print("no music cache found; pass a music folder")
        return 1
    set_sbr2_plugin_value("Audio", "CustomBgmFolder", str(dest))
    set_sbr2_plugin_value("Audio", "CustomBgmShuffle", "true")
    print(f"SBR2 custom BGM: {count} track(s) in {dest}")
    return 0


def _safe_track_name(stem):
    cleaned = "".join(
        c if c not in '/\\:*?"<>|' else "_" for c in stem).strip()
    return cleaned or "track"


def convert_bgm_folder(source, progress=None):
    """Flatten a music folder into OGG/WAV files the plugin can decode."""
    source = Path(source).resolve()
    dest = _bgm_cache_dir().resolve()
    if source == dest:
        files = [p for p in dest.rglob("*")
                 if p.is_file() and p.suffix.lower() in (".ogg", ".wav")]
        if progress:
            progress(len(files), len(files))
        return dest, len(files), 0
    if dest.exists():
        shutil.rmtree(dest, ignore_errors=True)
    dest.mkdir(parents=True, exist_ok=True)
    exts = (".ogg", ".wav", ".mp3", ".flac", ".m4a", ".aac", ".opus",
            ".wma")
    files = []
    for path in source.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in exts:
            continue
        try:
            if dest == path or dest in path.parents:
                continue
        except Exception:
            pass
        files.append(path)
    if not files:
        raise RuntimeError("No audio files found in that folder")
    files.sort(key=lambda p: str(p).lower())
    used = set()
    jobs = []
    for src in files:
        ext = src.suffix.lower()
        out_ext = ext if ext in (".ogg", ".wav") else ".ogg"
        stem = _safe_track_name(src.stem)
        name = stem + out_ext
        n = 2
        while name.lower() in used:
            name = f"{stem}_{n}{out_ext}"
            n += 1
        used.add(name.lower())
        jobs.append((src, out_ext, dest / name,
                     ext not in (".ogg", ".wav")))

    ffmpeg = shutil.which("ffmpeg")
    total = len(jobs)
    errors = []

    def work(job):
        src, _ext, out, needs_convert = job
        if not needs_convert:
            shutil.copy2(src, out)
            return
        if not ffmpeg:
            raise RuntimeError("ffmpeg not found")
        result = subprocess.run(
            [ffmpeg, "-y", "-loglevel", "error", "-i", str(src), "-vn",
             "-c:a", "libvorbis", "-q:a", "4", str(out)],
            capture_output=True, text=True)
        if result.returncode != 0:
            lines = (result.stderr or result.stdout).strip().splitlines()
            raise RuntimeError(lines[-1] if lines else "ffmpeg failed")

    workers = max(1, min(8, os.cpu_count() or 2))
    done = 0
    with ThreadPoolExecutor(max_workers=workers) as pool:
        futures = [pool.submit(work, job) for job in jobs]
        for future in as_completed(futures):
            done += 1
            try:
                future.result()
            except Exception as e:
                errors.append(f"{len(errors) + 1}: {e}")
            if progress:
                try:
                    progress(done, total)
                except Exception:
                    pass
    converted = total - len(errors)
    if converted == 0:
        raise RuntimeError(errors[0] if errors else "No tracks converted")
    return dest, converted, len(errors)



DLL_REL = Path("SuperBombermanR_Data/Managed/Assembly-CSharp.dll")
BIN_REL = Path("SuperBombermanR_Data/StreamingAssets/bin")
SAVE_REL = Path("savedata.bin")
PLUGIN_REL = Path("BepInEx/plugins/BomberQoL.dll")
PLUGIN_CFG_REL = Path("BepInEx/config/bomber.qol.cfg")
INTROS = ("bom_00_intro_a", "bom_00_intro_b")

PAT_E = bytes.fromhex("05 5f 00 45 00 00")  # #US literal "_E"
PAT_J = bytes.fromhex("05 5f 00 4a 00 00")  # #US literal "_J"

LANG_OFFSET = 1132
LANG_AUTO = 0xFF
ASPECTS = [
    ("Native / display", 0.0),
    ("16:9", 16 / 9),
    ("16:10", 16 / 10),
    ("21:9", 21 / 9),
    ("4:3", 4 / 3),
]

PAD_BUTTONS = [
    ("Disabled", -1),
    ("A / Cross", 0),
    ("B / Circle", 1),
    ("X / Square", 2),
    ("Y / Triangle", 3),
    ("LB / L1", 4),
    ("RB / R1", 5),
    ("Back / Share", 6),
    ("Start / Options", 7),
]

LANGUAGES = [
    ("Auto (Steam)", LANG_AUTO),
    ("English", 1),
    ("Japanese", 0),
    ("French", 2),
    ("German", 3),
    ("Italian", 4),
    ("Spanish", 5),
    ("Dutch", 8),
    ("Portuguese", 9),
    ("Russian", 10),
    ("Korean", 7),
    ("Chinese (Simplified)", 6),
    ("Chinese (Traditional)", 11),
]

CSS = """
.root {
  background: linear-gradient(165deg, #e8f2ff 0%, #f2efe9 55%, #faf7f1 100%);
}
.hero-frame {
  border-radius: 20px;
  background: #ffffff;
  box-shadow: 0 10px 24px rgba(20, 40, 80, 0.20);
}
.play {
  background: #ffcc00;
  color: #141414;
  border-radius: 999px;
  border: none;
  padding: 8px 26px;
  box-shadow: 0 5px 0 #c79d00, 0 8px 18px rgba(0,0,0,.25);
  transition: 120ms ease;
}
.play:hover { background: #ffd633; }
.play:active { box-shadow: 0 2px 0 #c79d00; margin-top: 3px; }
.play-text { font-weight: 800; font-size: 15pt; letter-spacing: 2px; }
.status-pill {
  color: #ffffff;
  background: rgba(20, 28, 44, 0.78);
  border-radius: 999px;
  padding: 5px 14px;
  font-size: 9pt;
  font-weight: 700;
  letter-spacing: 1px;
}
.status-ok { background: #1f9d55; }
.status-run { background: #c26a00; }
.status-err { background: #b91c1c; }
.section {
  min-height: 38px;
}
.card {
  background: #f8f5ef;
  border-radius: 16px;
  box-shadow: 0 3px 10px rgba(30, 40, 60, 0.12);
}
.avatar { border-radius: 11px; }
.row-title { font-weight: 700; font-size: 10.5pt; color: #16202e; }
.row-sub { color: #5c6674; font-size: 9pt; }
.footer { color: #7a8391; font-size: 8.5pt; }
.header-title { font-weight: 800; font-size: 13pt; letter-spacing: .5px; color: #16202e; }
.lang-drop { min-width: 190px; }
.lang-drop button { background: #ffffff; border-radius: 10px; }
.fallback-hero {
  background: linear-gradient(135deg, #16233d 0%, #0d1526 100%);
  min-height: 170px;
}
.fallback-title {
  color: #ffcc00; font-weight: 900; font-size: 22pt; letter-spacing: 1px;
}
.fallback-sub {
  color: #e8f2ff; font-size: 11pt; letter-spacing: 5px;
}
.section-fallback {
  background: #3c4647; color: #ffffff; padding: 8px 16px;
  font-weight: 800; font-size: 10pt; letter-spacing: 2px; border-radius: 3px;
}
.avatar-fallback { color: #46555a; }
"""


# ---------------------------------------------------------------- discovery
def find_game_dir():
    env = os.environ.get("SBR_GAME")
    if env and (Path(env) / "SuperBombermanR.exe").exists():
        return Path(env)
    roots = [
        HOME / ".local/share/Steam",
        HOME / ".steam/steam",
        HOME / ".var/app/com.valvesoftware.Steam/.local/share/Steam",
    ]
    libs = []
    for root in roots:
        if not root.exists():
            continue
        libs.append(root)
        vdf = root / "steamapps/libraryfolders.vdf"
        if vdf.exists():
            text = vdf.read_text(errors="replace")
            for line in text.splitlines():
                if '"path"' in line:
                    parts = line.split('"')
                    if len(parts) >= 4:
                        libs.append(Path(parts[3]))
    seen = set()
    for lib in libs:
        if lib in seen:
            continue
        seen.add(lib)
        p = lib / "steamapps/common" / GAME_DIR_NAME
        if (p / "SuperBombermanR.exe").exists():
            return p
    return None


def game_running():
    try:
        r = subprocess.run(["pgrep", "-f", "SuperBombermanR.exe"],
                           capture_output=True, timeout=3)
        return r.returncode == 0
    except Exception:
        return False


def sbr2_running():
    try:
        r = subprocess.run(["pgrep", "-f", "SuperBombermanR2.exe"],
                           capture_output=True, timeout=3)
        return r.returncode == 0
    except Exception:
        return False


# --------------------------------------------------------------------- config
def load_config():
    try:
        return json.loads(CONFIG_PATH.read_text())
    except Exception:
        return {}


def save_config(cfg):
    CONFIG_PATH.parent.mkdir(parents=True, exist_ok=True)
    CONFIG_PATH.write_text(json.dumps(cfg, indent=2))


# ----------------------------------------------------------------- save file
class SaveFile:
    SEED = 1962962729

    @staticmethod
    def _crypt(data: bytearray) -> bytearray:
        s = SaveFile.SEED
        for i in range(len(data)):
            s ^= (s << 13) & 0xFFFFFFFF
            s ^= s >> 17
            s ^= (s << 5) & 0xFFFFFFFF
            s &= 0xFFFFFFFF
            data[i] ^= s & 0xFF
        return data

    @staticmethod
    def _read7(data, pos):
        v = 0
        shift = 0
        while True:
            b = data[pos]
            pos += 1
            v |= (b & 0x7F) << shift
            if not b & 0x80:
                return v, pos
            shift += 7

    @staticmethod
    def load(path):
        raw = bytearray(Path(path).read_bytes())
        data = SaveFile._crypt(raw)
        pos = 0
        entries = OrderedDict()
        while pos < len(data):
            n, pos = SaveFile._read7(data, pos)
            key = data[pos:pos + n].decode("utf-8")
            pos += n
            size = struct.unpack_from("<i", data, pos)[0]
            pos += 4
            blob = bytes(data[pos:pos + size])
            pos += size
            hsize = struct.unpack_from("<i", data, pos)[0]
            pos += 4
            pos += hsize
            entries[key] = blob
        return entries

    @staticmethod
    def save(path, entries):
        out = bytearray()
        for key, blob in entries.items():
            kb = key.encode("utf-8")
            n = len(kb)
            while True:
                b = n & 0x7F
                n >>= 7
                out.append(b | (0x80 if n else 0))
                if not n:
                    break
            out += kb
            out += struct.pack("<i", len(blob))
            out += blob
            digest = hashlib.md5(blob).digest()
            out += struct.pack("<i", len(digest))
            out += digest
        SaveFile._crypt(out)
        tmp = Path(str(path) + ".bomber-tmp")
        tmp.write_bytes(out)
        os.replace(tmp, path)

    @staticmethod
    def get_language(path):
        entries = SaveFile.load(path)
        blob = entries.get("/systemData")
        if blob is None or len(blob) != 1352:
            raise ValueError("unsupported save layout")
        return blob[LANG_OFFSET]

    @staticmethod
    def set_language(path, value):
        path = Path(path)
        entries = SaveFile.load(path)
        blob = bytearray(entries.get("/systemData", b""))
        if len(blob) != 1352:
            raise ValueError("unsupported save layout")
        SAVE_BACKUPS.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, SAVE_BACKUPS /
                     f"savedata_{time.strftime('%Y%m%d_%H%M%S')}.bin")
        blob[LANG_OFFSET] = value & 0xFF
        entries["/systemData"] = bytes(blob)
        SaveFile.save(path, entries)


# ----------------------------------------------------------------------- mods
class Mods:
    def __init__(self, game_dir):
        self.game = Path(game_dir)
        self.dll = self.game / DLL_REL
        self.bin = self.game / BIN_REL
        self.save = self.game / SAVE_REL

    def dual_audio_state(self):
        data = self.dll.read_bytes()
        if data.count(PAT_E) == 1 and data.count(PAT_J) == 1:
            return False
        if data.count(PAT_E) == 0 and data.count(PAT_J) == 2:
            return True
        return None

    def set_dual_audio(self, enabled):
        if game_running():
            raise RuntimeError("Close the game first.")
        orig = BACKUPS / "Assembly-CSharp.dll.orig"
        data = bytearray(self.dll.read_bytes())
        if enabled:
            if data.count(PAT_E) == 1:
                BACKUPS.mkdir(parents=True, exist_ok=True)
                if not orig.exists():
                    shutil.copy2(self.dll, orig)
                i = data.find(PAT_E)
                data[i + 3] = 0x4A
                self.dll.write_bytes(data)
            elif data.count(PAT_J) != 2:
                raise RuntimeError("Unrecognized Assembly-CSharp.dll")
        else:
            if orig.exists():
                shutil.copy2(orig, self.dll)
            elif data.count(PAT_J) == 2 and data.count(PAT_E) == 0:
                i = data.find(PAT_J)
                data[i + 3] = 0x45
                self.dll.write_bytes(data)
            else:
                raise RuntimeError(
                    "No backup found. Re-verify game files in Steam.")

    def intro_files(self):
        return [self.bin / name for name in INTROS]

    def intro_state(self):
        sizes = [p.stat().st_size for p in self.intro_files() if p.exists()]
        if len(sizes) != 2:
            return None
        if all(s < 1024 * 1024 for s in sizes):
            return True
        if all(s > 10 * 1024 * 1024 for s in sizes):
            return False
        return None

    def set_intro_skip(self, enabled):
        if game_running():
            raise RuntimeError("Close the game first.")
        blank = ASSETS / "intro_blank.mp4"
        if enabled:
            if not blank.exists():
                raise RuntimeError("Missing assets/intro_blank.mp4")
            VIDEO_BACKUPS.mkdir(parents=True, exist_ok=True)
            for p in self.intro_files():
                backup = VIDEO_BACKUPS / p.name
                if p.exists() and p.stat().st_size > 10 * 1024 * 1024:
                    if not backup.exists():
                        shutil.move(str(p), backup)
                if not p.exists() or p.stat().st_size > 1024 * 1024:
                    shutil.copy2(blank, p)
        else:
            restored = 0
            for p in self.intro_files():
                backup = VIDEO_BACKUPS / p.name
                if backup.exists():
                    shutil.copy2(backup, p)
                    restored += 1
            if restored != 2:
                raise RuntimeError(
                    "Original movies missing. Use Steam → Verify files.")

    def language(self):
        return SaveFile.get_language(self.save)

    def set_language(self, value):
        if game_running():
            raise RuntimeError("Close the game first.")
        SaveFile.set_language(self.save, value)

    # -- BepInEx plugin ---------------------------------------------------
    def plugin_installed(self):
        return (self.game / PLUGIN_REL).exists()

    def plugin_config(self):
        cfg = {
            "Movies": {"SkipEnabled": "true", "SkipKey": "Space",
                       "SkipJoystickButton": "0", "AutoSkipLogos": "true",
                       "AutoSkipMovies": "0"},
            "Gameplay": {
                "UnlockMaster": "false",
                "SoftResetEnabled": "true",
                "SoftResetPadCombo": "4,5,7",
                "SoftResetPadHold": "0.75",
                "SoftResetKeyEnabled": "true",
                "SoftResetKey": "F10",
                "SoftResetRequireCtrl": "true",
                "SoftResetSave": "false",
            },
            "Results": {"FastResults": "false", "ResultSpeed": "0.5"},
            "Graphics": {
                "DisableMotionBlur": "true",
                "DisableDepthOfField": "true",
                "DisableBloom": "false",
                "DisableVignette": "false",
                "DisableGrain": "false",
            },
        }
        path = self.game / PLUGIN_CFG_REL
        if path.exists():
            section = None
            for line in path.read_text(errors="replace").splitlines():
                s = line.strip()
                if s.startswith("[") and s.endswith("]"):
                    section = s[1:-1].strip()
                    cfg.setdefault(section, {})
                elif section and "=" in s and not s.startswith("#"):
                    k, v = s.split("=", 1)
                    cfg[section][k.strip()] = v.strip()
        return cfg

    def set_plugin_option(self, section, key, value):
        if isinstance(value, str):
            self.set_plugin_value(section, key, value)
        else:
            self.set_plugin_value(section, key,
                                  "true" if value else "false")

    def set_plugin_value(self, section, key, value):
        path = self.game / PLUGIN_CFG_REL
        text = path.read_text(errors="replace") if path.exists() else ""
        out = text.splitlines()
        in_sec = False
        found_sec = False
        written = False
        for i, line in enumerate(list(out)):
            s = line.strip()
            if s.startswith("[") and s.endswith("]"):
                in_sec = s[1:-1].strip() == section
                found_sec = found_sec or in_sec
            elif in_sec and "=" in s and s.split("=", 1)[0].strip() == key:
                out[i] = f"{key} = {value}"
                written = True
        if not found_sec:
            if out and out[-1].strip():
                out.append("")
            out.extend([f"[{section}]", f"{key} = {value}"])
        elif not written:
            for i, line in enumerate(out):
                s = line.strip()
                if s.startswith("[") and s[1:-1].strip() == section:
                    out.insert(i + 1, f"{key} = {value}")
                    break
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("\n".join(out) + "\n")

    def restore_all(self):
        if game_running():
            raise RuntimeError("Close the game first.")
        if (BACKUPS / "Assembly-CSharp.dll.orig").exists():
            shutil.copy2(BACKUPS / "Assembly-CSharp.dll.orig", self.dll)
        for p in self.intro_files():
            backup = VIDEO_BACKUPS / p.name
            if backup.exists():
                shutil.copy2(backup, p)

    # -- BepInEx bootstrap -------------------------------------------------
    BEPINEX_URL = ("https://github.com/BepInEx/BepInEx/releases/download/"
                   "v5.4.23.2/BepInEx_win_x86_5.4.23.2.zip")

    def install_bepinex(self):
        if game_running():
            raise RuntimeError("Close the game first.")
        import io
        import urllib.request
        import zipfile
        with urllib.request.urlopen(self.BEPINEX_URL, timeout=60) as r:
            data = r.read()
        with zipfile.ZipFile(io.BytesIO(data)) as z:
            z.extractall(self.game)
        plugins = self.game / "BepInEx" / "plugins"
        plugins.mkdir(parents=True, exist_ok=True)
        src = Path(__file__).resolve().parent / "plugin" / "BomberQoL.dll"
        if not src.exists():
            src = ASSETS.parent / "plugin" / "BomberQoL.dll"
        if not src.exists():
            raise RuntimeError("Bundled BomberQoL.dll not found")
        shutil.copy2(src, plugins / "BomberQoL.dll")
        self._ensure_winhttp_override()
        return "BepInEx + BomberQoL installed"

    def extract_art(self):
        import importlib.util
        script = Path(__file__).resolve().parent / "tools" / "extract_ui_assets.py"
        if not script.exists():
            raise RuntimeError("Artwork extractor not bundled")
        if not self.game or not (
                Path(self.game) / "SuperBombermanR.exe").exists():
            raise RuntimeError(
                "Game folder is missing or invalid — use Game Folder → "
                "Choose…")
        ART.mkdir(parents=True, exist_ok=True)
        os.environ["BOMBER_ASSETS"] = str(ART)
        spec = importlib.util.spec_from_file_location(
            "bomber_extract_art", script)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        try:
            mod.main(str(self.game), str(ART))
        except SystemExit as exc:
            raise RuntimeError("Artwork extractor stopped: " + str(exc))
        return f"Artwork extracted to {ART} — restart the launcher"

    def _ensure_winhttp_override(self):
        reg = Path.home() / ".local/share/Steam/steamapps/compatdata/702700/pfx/user.reg"
        if not reg.exists():
            return False
        shutil.copy2(reg, reg.with_suffix(".bomber-backup"))
        text = reg.read_text(errors="replace")
        header = "[Software\\\\Wine\\\\DllOverrides]"
        if header not in text:
            import time
            text += (f"\n{header} {int(time.time())}\n"
                     "#time=1dd000000000000\n"
                     '"winhttp"="native,builtin"\n')
        else:
            idx = text.index(header)
            end = text.find("\n[", idx + 1)
            if end < 0:
                end = len(text)
            if '"winhttp"' not in text[idx:end]:
                text = (text[:end] + '\n"winhttp"="native,builtin"\n'
                        + text[end:])
        reg.write_text(text)
        return True


# ------------------------------------------------------------------------ GUI
class BomberWindow(Adw.ApplicationWindow):
    def __init__(self, app):
        super().__init__(application=app, title=APP_NAME)
        self.set_default_size(700, 980)
        self.cfg = load_config()
        self.game = self._configured_game()
        self.mods = Mods(self.game) if self.game else None
        self._loading = False
        self._game_chooser = None
        self._extracting = False

        self.toasts = Adw.ToastOverlay()
        self.set_content(self.toasts)

        root = Gtk.Box(orientation=Gtk.Orientation.VERTICAL)
        root.add_css_class("root")
        self.toasts.set_child(root)

        self.view_stack = Adw.ViewStack()
        self.view_switcher = Adw.ViewSwitcherTitle()
        self.view_switcher.set_stack(self.view_stack)
        self.view_switcher.set_title(APP_NAME)

        header = Adw.HeaderBar()
        header.set_title_widget(self.view_switcher)
        root.append(header)

        scroller = Gtk.ScrolledWindow(vexpand=True)
        scroller.set_policy(Gtk.PolicyType.NEVER, Gtk.PolicyType.AUTOMATIC)
        scroller.set_child(self.view_stack)
        self.scroller = scroller
        root.append(scroller)

        switcher_bar = Adw.ViewSwitcherBar()
        switcher_bar.set_stack(self.view_stack)
        self.view_switcher.bind_property(
            "title-visible", switcher_bar, "reveal",
            GObject.BindingFlags.SYNC_CREATE)
        root.append(switcher_bar)

        page = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        page.set_margin_top(12)
        page.set_margin_bottom(14)
        page.set_margin_start(18)
        page.set_margin_end(18)

        page.append(self._build_hero())

        page.append(self._section("game"))
        gamecard = self._card()
        gamebox = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=6)
        self.game_label = Gtk.Label(label="")
        self.game_label.set_xalign(0)
        self.game_label.set_hexpand(True)
        self.game_label.set_wrap(True)
        self.game_label.set_max_width_chars(46)
        game_choose = Gtk.Button(label="Choose…")
        game_choose.set_valign(Gtk.Align.CENTER)
        game_choose.connect("clicked", self.on_choose_game)
        game_auto = Gtk.Button(label="Auto")
        game_auto.set_valign(Gtk.Align.CENTER)
        game_auto.connect("clicked", self.on_auto_game)
        gamebox.append(self.game_label)
        gamebox.append(game_choose)
        gamebox.append(game_auto)
        gamecard.append(self._row(
            "avatar_blue.png", "Game Folder",
            "Folder that contains SuperBombermanR.exe; Auto restores Steam detection",
            gamebox))
        page.append(gamecard)
        page.append(Gtk.Separator())

        # ----- language
        page.append(self._section("language"))
        lang_card = self._card()
        self.lang_dd = Gtk.DropDown.new_from_strings(
            [name for name, _ in LANGUAGES])
        self.lang_dd.add_css_class("lang-drop")
        self.lang_dd.set_valign(Gtk.Align.CENTER)
        self.lang_dd.connect("notify::selected", self.on_language)
        lang_card.append(self._row("avatar_white.png", "Text Language",
                                   "Writes the language byte in savedata.bin",
                                   self.lang_dd))
        page.append(lang_card)

        # ----- settings
        page.append(self._section("basic"))
        basic = self._card()

        self.border_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.border_sw.set_active(bool(self.cfg.get("borderless", True)))
        self.border_sw.connect("notify::active", self.on_borderless)
        basic.append(self._row("avatar_blue.png", "Borderless Fullscreen",
                               "Launch with -popupwindow at desktop resolution",
                               self.border_sw))
        basic.append(self._separator())

        self.aspect_dd = Gtk.DropDown.new_from_strings(
            [name for name, _ in ASPECTS])
        self.aspect_dd.set_valign(Gtk.Align.CENTER)
        try:
            self.aspect_dd.set_selected(
                int(self.cfg.get("aspect", 0)) % len(ASPECTS))
        except Exception:
            self.aspect_dd.set_selected(0)
        self.aspect_dd.connect("notify::selected", self.on_aspect)
        basic.append(self._row(
            "avatar_pink.png", "Aspect Ratio",
            "Window shape; the game fits inside the desktop with bars",
            self.aspect_dd))
        basic.append(self._separator())

        self.dual_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.dual_sw.connect("notify::active", self.on_dual)
        basic.append(self._row("avatar_red.png", "Japanese Voices",
                               "JP voice acting with the selected text language",
                               self.dual_sw))
        basic.append(self._separator())

        self.intro_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.intro_sw.connect("notify::active", self.on_intro)
        basic.append(self._row("avatar_green.png", "Skip Boot Intro",
                               "Replaces the ~4 minute opening movie",
                               self.intro_sw))
        basic.append(self._separator())

        self.skip_movies_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.skip_movies_sw.connect("notify::active", self.on_skip_movies)
        basic.append(self._row(
            "avatar_yellow.png", "Skip Cutscenes",
            "Press Space during any movie (BepInEx, next launch)",
            self.skip_movies_sw))
        basic.append(self._separator())

        self.pad_dd = Gtk.DropDown.new_from_strings(
            [name for name, _ in PAD_BUTTONS])
        self.pad_dd.set_valign(Gtk.Align.CENTER)
        self.pad_dd.connect("notify::selected", self.on_pad_button)
        basic.append(self._row(
            "avatar_pink.png", "Controller Skip Button",
            "A / Cross by default (B is Cancel); next launch", self.pad_dd))
        basic.append(self._separator())

        self.logos_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.logos_sw.connect("notify::active", self.on_auto_logos)
        basic.append(self._row(
            "avatar_blue.png", "Remove Publisher Logos",
            "Removes Konami / 33rd / HexaDrive logo states", self.logos_sw))
        basic.append(self._separator())

        self.autoskip_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.autoskip_sw.connect("notify::active", self.on_auto_skip)
        basic.append(self._row(
            "avatar_white.png", "Auto-skip Cutscenes",
            "Skips every movie after 5 seconds (next launch)",
            self.autoskip_sw))
        basic.append(self._separator())

        self.master_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.master_sw.connect("notify::active", self.on_master)
        basic.append(self._row(
            "avatar_yellow.png", "Master Difficulty",
            "Adds the hidden Master option to the difficulty menu",
            self.master_sw))
        basic.append(self._separator())

        self.softreset_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.softreset_sw.connect("notify::active", self.on_soft_reset)
        basic.append(self._row(
            "avatar_pink.png", "Soft Reset Combo",
            "Hold LB + RB + Start for 0.75s to save and return to title",
            self.softreset_sw))
        page.append(basic)

        # ----- audio
        page.append(self._section("audio"))
        audio = self._card()
        self._bgm_chooser = None
        self._sfx_chooser = None
        self._converting = False
        self.bgm_btn = Gtk.Button(label="Choose…")
        self.bgm_btn.set_valign(Gtk.Align.CENTER)
        self.bgm_btn.connect("clicked", self.on_bgm_folder)
        self.bgm_clear = Gtk.Button(label="Clear")
        self.bgm_clear.add_css_class("flat")
        self.bgm_clear.set_valign(Gtk.Align.CENTER)
        self.bgm_clear.connect("clicked", self.on_bgm_clear)
        bgm_box = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=4)
        bgm_box.append(self.bgm_btn)
        bgm_box.append(self.bgm_clear)
        audio.append(self._row(
            "avatar_white.png", "Custom BGM Folder",
            "Music folder (MP3/OGG/FLAC/WAV); converted to an OGG cache",
            bgm_box))
        audio.append(self._separator())

        self.bgm_shuffle_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.bgm_shuffle_sw.connect("notify::active", self.on_bgm_shuffle)
        audio.append(self._row(
            "avatar_yellow.png", "Shuffle BGM",
            "Randomize custom BGM instead of sequential order",
            self.bgm_shuffle_sw))
        audio.append(self._separator())

        self.sfx_btn = Gtk.Button(label="Choose…")
        self.sfx_btn.set_valign(Gtk.Align.CENTER)
        self.sfx_btn.connect("clicked", self.on_sfx_folder)
        self.sfx_clear = Gtk.Button(label="Clear")
        self.sfx_clear.add_css_class("flat")
        self.sfx_clear.set_valign(Gtk.Align.CENTER)
        self.sfx_clear.connect("clicked", self.on_sfx_clear)
        sfx_box = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=4)
        sfx_box.append(self.sfx_btn)
        sfx_box.append(self.sfx_clear)
        audio.append(self._row(
            "avatar_pink.png", "Custom SFX Folder",
            "Files named <label>.ogg or <label>_N.ogg; empty keeps original SFX",
            sfx_box))
        audio.append(self._separator())

        self.sfx_shuffle_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.sfx_shuffle_sw.connect("notify::active", self.on_sfx_shuffle)
        audio.append(self._row(
            "avatar_blue.png", "Shuffle SFX",
            "Randomize among variants with the same sound label",
            self.sfx_shuffle_sw))
        page.append(audio)

        # ----- randomizer
        page.append(self._section("randomizer"))
        rnd = self._card()
        self.rand_chars_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.rand_chars_sw.connect("notify::active", self.on_random_chars)
        rnd.append(self._row(
            "avatar_yellow.png", "Random Story Characters",
            "Random character selection at each StageSetup",
            self.rand_chars_sw))
        rnd.append(self._separator())
        self.rand_enemies_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.rand_enemies_sw.connect("notify::active", self.on_random_enemies)
        rnd.append(self._row(
            "avatar_green.png", "Random Enemies",
            "Shuffle the enemy types that spawn on each story map",
            self.rand_enemies_sw))
        rnd.append(self._separator())
        self.skip_chars_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.skip_chars_sw.connect("notify::active", self.on_skip_chars)
        rnd.append(self._row(
            "avatar_blue.png", "Skip Character Select",
            "Jump straight into battle when random characters or a random run is active",
            self.skip_chars_sw))
        rnd.append(self._separator())
        page.append(rnd)

        # ----- multiplayer
        page.append(self._section("multiplayer"))
        mp = self._card()
        self.smart_bots_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.smart_bots_sw.connect("notify::active", self.on_smart_bots)
        mp.append(self._row(
            "avatar_pink.png", "Smarter Bots",
            "Longer NPC search range and faster target re-evaluation",
            self.smart_bots_sw))
        mp.append(self._separator())
        self.random_cpu_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.random_cpu_sw.connect("notify::active", self.on_random_cpu)
        mp.append(self._row(
            "avatar_yellow.png", "Random CPU Opponent",
            "Randomize character selection for CPU slots in local battle",
            self.random_cpu_sw))
        mp.append(self._separator())
        self.cpu_skills_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.cpu_skills_sw.connect("notify::active", self.on_cpu_skills)
        mp.append(self._row(
            "avatar_blue.png", "CPU Special Abilities",
            "Local battle CPUs use their A-button special ability when they have one",
            self.cpu_skills_sw))
        mp.append(self._separator())
        self.platform_excl_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.platform_excl_sw.connect("notify::active", self.on_platform_exclusives)
        mp.append(self._row(
            "avatar_red.png", "Platform Exclusives",
            "Enable Master Chief/Ratchet/etc. IDs; models and voices are present, no NPC AI",
            self.platform_excl_sw))
        page.append(mp)

        # ----- achievements
        page.append(self._section("achievements"))
        ach = self._card()
        self.room_ach_btn = Gtk.Button(label="Unlock")
        self.room_ach_btn.set_valign(Gtk.Align.CENTER)
        self.room_ach_btn.connect("clicked", self.on_room_achievements)
        ach.append(self._row(
            "avatar_white.png", "Online Room Achievements",
            "One-shot shortcut for Room Creator / Master / Champ; applies next launch",
            self.room_ach_btn))
        page.append(ach)

        # ----- environment / post processing
        page.append(self._section("environment"))
        env = self._card()
        self.mblur_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.mblur_sw.connect("notify::active", self.on_motion_blur)
        env.append(self._row("avatar_pink.png", "Motion Blur",
                             "Post-processing effect (BepInEx, next launch)",
                             self.mblur_sw))
        env.append(self._separator())
        self.dof_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.dof_sw.connect("notify::active", self.on_dof)
        env.append(self._row("avatar_blue.png", "Depth of Field",
                             "Post-processing effect (BepInEx, next launch)",
                             self.dof_sw))
        env.append(self._separator())
        self.bloom_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.bloom_sw.connect("notify::active", self.on_bloom)
        env.append(self._row("avatar_green.png", "Bloom",
                             "Post-processing effect (BepInEx, next launch)",
                             self.bloom_sw))
        env.append(self._separator())

        self.vignette_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.vignette_sw.connect("notify::active", self.on_vignette)
        env.append(self._row("avatar_pink.png", "Vignette",
                             "Darkened screen edges (BepInEx, next launch)",
                             self.vignette_sw))
        env.append(self._separator())

        self.grain_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.grain_sw.connect("notify::active", self.on_grain)
        env.append(self._row("avatar_white.png", "Film Grain",
                             "Grain overlay (BepInEx, next launch)",
                             self.grain_sw))
        env.append(self._separator())

        self.low_end_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.low_end_sw.connect("notify::active", self.on_low_end)
        env.append(self._row(
            "avatar_blue.png", "Low-End Mode",
            "60 FPS cap (30 optional), no post-FX, half textures",
            self.low_end_sw))
        env.append(self._separator())

        self.ultra_low_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.ultra_low_sw.connect("notify::active", self.on_ultra_low)
        env.append(self._row(
            "avatar_pink.png", "Ultra-Low Mode",
            "960x540, quarter textures, no pixel lights, LOD 2",
            self.ultra_low_sw))
        env.append(self._separator())

        self.high_end_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.high_end_sw.connect("notify::active", self.on_high_end)
        env.append(self._row(
            "avatar_green.png", "High-End Mode (ceiling)",
            "Native res, full textures, 8x AA, max lights/shadows/LOD",
            self.high_end_sw))
        env.append(self._separator())

        self.fast_results_sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        self.fast_results_sw.connect("notify::active", self.on_fast_results)
        env.append(self._row(
            "avatar_yellow.png", "Faster Result Screens",
            "Halves post-battle result delays (BepInEx, next launch)",
            self.fast_results_sw))
        page.append(env)

        # ----- maintenance
        page.append(self._section("other"))
        maint = self._card()
        maint.append(self._action(
            "avatar_pink.png", "Install BepInEx + Plugin",
            "Downloads BepInEx x86 and installs BomberQoL",
            self.on_install_bepinex))
        maint.append(self._separator())
        maint.append(self._action(
            "avatar_white.png", "Extract Game Artwork",
            "Rebuilds the interface from your game files", self.on_extract_art))
        maint.append(self._separator())
        maint.append(self._action("avatar_yellow.png",
                                  "Restore Original Files",
                                  "Reverts dual audio and intro changes",
                                  self.on_restore))
        maint.append(self._separator())
        maint.append(self._action("avatar_pink.png", "Back Up Save",
                                  "Timestamped copy in sbr-modkit/backups",
                                  self.on_backup_save))
        maint.append(self._separator())
        maint.append(self._action("avatar_white.png", "Open Modkit Folder",
                                  str(MODKIT), self.on_open_modkit))
        page.append(maint)

        foot = Gtk.Label(
            label=("Artwork and interface elements are from Super Bomberman R "
                   "© Konami. Unofficial QoL launcher."))
        foot.add_css_class("footer")
        foot.set_wrap(True)
        foot.set_margin_top(6)
        page.append(foot)

        if (ART / "mountain.png").exists():
            strip = Gtk.Picture.new_for_filename(str(ART / "mountain.png"))
            strip.set_content_fit(Gtk.ContentFit.COVER)
            strip.set_size_request(-1, 28)
            strip.set_margin_top(4)
            page.append(strip)

        self.view_stack.add_titled_with_icon(
            page, "sbr1", "SBR1", "applications-games-symbolic")
        self.view_stack.add_titled_with_icon(
            self._build_sbr2_page(), "sbr2", "SBR2 QoL",
            "applications-games-symbolic")
        self.view_stack.set_visible_child_name(
            "sbr2" if "--sbr2" in sys.argv else "sbr1")

        self.refresh()
        GLib.timeout_add_seconds(3, self._poll)

        fraction = os.environ.get("BOMBER_SCROLL")
        if fraction:
            try:
                GLib.timeout_add(500, self._scroll_fraction,
                                 float(fraction))
            except Exception:
                pass

    # -- game folder ------------------------------------------------------
    def _configured_game(self):
        path = (self.cfg.get("game_dir") or "").strip()
        if path and (Path(path) / "SuperBombermanR.exe").exists():
            return Path(path)
        if path:
            self.cfg.pop("game_dir", None)
        found = find_game_dir()
        if found:
            self.cfg["game_dir"] = str(found)
            save_config(self.cfg)
        return found

    def _update_game_label(self):
        if not hasattr(self, "game_label"):
            return
        if self.game:
            self.game_label.set_text(str(self.game))
            self.game_label.set_tooltip_text(str(self.game))
        else:
            self.game_label.set_text(
                "Not found — choose the folder containing "
                "SuperBombermanR.exe")
            self.game_label.set_tooltip_text("")

    def _set_game_dir(self, path):
        path = Path(path)
        if not (path / "SuperBombermanR.exe").exists():
            self.toast("That folder has no SuperBombermanR.exe")
            return False
        self.cfg["game_dir"] = str(path)
        save_config(self.cfg)
        self.game = path
        self.mods = Mods(path)
        self._update_game_label()
        self.refresh()
        self.toast("Game folder: " + str(path))
        return True

    def on_choose_game(self, _btn):
        current = str(self.game) if self.game else str(HOME)
        self._game_chooser = self._open_folder_chooser(
            "Select the Super Bomberman R folder", current,
            self._game_folder_response)

    def _game_folder_response(self, chooser, response):
        self._game_chooser = None
        if response == Gtk.ResponseType.ACCEPT:
            f = chooser.get_file()
            path = f.get_path() if f else None
            if path:
                self._set_game_dir(path)
        chooser.destroy()

    def on_auto_game(self, _btn):
        self.cfg.pop("game_dir", None)
        save_config(self.cfg)
        self.game = find_game_dir()
        self.mods = Mods(self.game) if self.game else None
        self._update_game_label()
        self.refresh()
        if self.game:
            self.toast("Game folder: " + str(self.game))
        else:
            self.toast("Game not found — choose the folder manually")

    # -- widget helpers ---------------------------------------------------
    def _build_hero(self):
        frame = Gtk.Box()
        frame.add_css_class("hero-frame")
        frame.set_overflow(Gtk.Overflow.HIDDEN)

        if not HAVE_ART:
            frame.add_css_class("fallback-hero")
            box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=4)
            box.set_valign(Gtk.Align.CENTER)
            box.set_halign(Gtk.Align.CENTER)
            title = Gtk.Label(label="SUPER BOMBERMAN R")
            title.add_css_class("fallback-title")
            sub = Gtk.Label(label="BOMBER LAUNCHER")
            sub.add_css_class("fallback-sub")
            play = self._play_button()
            self.status_pill = Gtk.Label(label="READY")
            self.status_pill.add_css_class("status-pill")
            bar = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=12)
            bar.set_halign(Gtk.Align.CENTER)
            bar.set_margin_top(12)
            bar.append(play)
            bar.append(self.status_pill)
            box.append(title)
            box.append(sub)
            box.append(bar)
            frame.append(box)
            return frame

        overlay = Gtk.Overlay()
        pic = Gtk.Picture.new_for_filename(str(ART / "hero.png"))
        pic.set_content_fit(Gtk.ContentFit.COVER)
        pic.set_can_shrink(True)
        pic.set_size_request(-1, 240)
        overlay.set_child(pic)

        bar = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=12)
        bar.set_valign(Gtk.Align.END)
        for side in ("top", "bottom", "start", "end"):
            getattr(bar, f"set_margin_{side}")(14)

        play = self._play_button()

        self.status_pill = Gtk.Label(label="READY")
        self.status_pill.add_css_class("status-pill")
        self.status_pill.set_valign(Gtk.Align.CENTER)

        bar.append(play)
        bar.append(self.status_pill)
        overlay.add_overlay(bar)
        frame.append(overlay)
        return frame

    def _play_button(self):
        play = Gtk.Button()
        play.add_css_class("play")
        label = Gtk.Label(label="PLAY")
        label.add_css_class("play-text")
        play.set_child(label)
        play.connect("clicked", self.on_launch)
        return play

    def _section(self, name):
        labels = {
            "game": "GAME FOLDER",
            "language": "CHANGE LANGUAGE",
            "basic": "BASIC SETTINGS",
            "environment": "ENVIRONMENT",
            "other": "OTHER",
            "sbr2_status": "SBR2 STATUS",
            "sbr2_movies": "SBR2 MOVIES",
            "sbr2_graphics": "SBR2 GRAPHICS",
            "sbr2_audio": "SBR2 AUDIO",
            "sbr2_story": "SBR2 STORY",
            "sbr2_interface": "SBR2 INTERFACE",
            "sbr2_debug": "SBR2 DEBUG",
        }
        path = ART / f"section_{name}.png"
        if not path.exists():
            label = Gtk.Label(label=labels.get(name, name.upper()))
            label.add_css_class("section-fallback")
            label.set_xalign(0)
            return label
        box = Gtk.Box()
        box.add_css_class("section")
        pic = Gtk.Picture.new_for_filename(str(path))
        pic.set_content_fit(Gtk.ContentFit.FILL)
        pic.set_can_shrink(True)
        pic.set_size_request(-1, 36)
        pic.set_hexpand(True)
        box.append(pic)
        return box

    def _card(self):
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL)
        box.add_css_class("card")
        box.set_overflow(Gtk.Overflow.HIDDEN)
        return box

    def _separator(self):
        sep = Gtk.Separator()
        sep.set_margin_start(64)
        return sep

    def _row(self, avatar, title, subtitle, control):
        row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=12)
        for side in ("top", "bottom", "start", "end"):
            getattr(row, f"set_margin_{side}")(6)
        row.set_margin_start(12)

        apath = ART / avatar
        if apath.exists():
            pic = Gtk.Picture.new_for_filename(str(apath))
            pic.set_size_request(40, 40)
            pic.set_content_fit(Gtk.ContentFit.COVER)
            pic.set_overflow(Gtk.Overflow.HIDDEN)
            pic.add_css_class("avatar")
        else:
            pic = Gtk.Image.new_from_icon_name("applications-games-symbolic")
            pic.set_pixel_size(26)
            pic.set_size_request(40, 40)
            pic.add_css_class("avatar-fallback")
        row.append(pic)

        texts = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=1)
        texts.set_hexpand(True)
        texts.set_valign(Gtk.Align.CENTER)
        t = Gtk.Label(label=title)
        t.set_xalign(0)
        t.add_css_class("row-title")
        s = Gtk.Label(label=subtitle)
        s.set_xalign(0)
        s.set_wrap(True)
        s.add_css_class("row-sub")
        texts.append(t)
        texts.append(s)
        row.append(texts)
        row.append(control)
        return row

    def _action(self, avatar, title, subtitle, callback):
        row = self._row(avatar, title, subtitle,
                        Gtk.Image.new_from_icon_name("go-next-symbolic"))
        row.set_cursor(Gdk.Cursor.new_from_name("pointer"))
        click = Gtk.GestureClick()
        click.connect("released", lambda *_: callback(row))
        row.add_controller(click)
        return row

    # -- helpers ----------------------------------------------------------
    def toast(self, text):
        self.toasts.add_toast(Adw.Toast(title=text, timeout=3))

    def _guard(self):
        if not self.mods:
            self.toast("Game not found")
            return False
        if game_running():
            self.toast("Close the game first")
            self.refresh()
            return False
        return True

    def _poll(self):
        self.refresh()
        return True

    def refresh(self):
        if hasattr(self, "sbr2_status"):
            self.refresh_sbr2()
        self._loading = True
        running = game_running()
        if not self.mods:
            state, label = "status-err", "NOT FOUND"
        elif running:
            state, label = "status-run", "RUNNING"
        else:
            state, label = "status-ok", "READY"
        for cls in ("status-ok", "status-run", "status-err"):
            self.status_pill.remove_css_class(cls)
        self.status_pill.add_css_class(state)
        self.status_pill.set_label(label)
        self._update_game_label()

        for w in (self.dual_sw, self.intro_sw, self.lang_dd, self.border_sw):
            w.set_sensitive(self.mods is not None and not running)
        plugin = self.mods is not None and self.mods.plugin_installed()
        for w in (self.skip_movies_sw, self.pad_dd, self.logos_sw,
                  self.autoskip_sw, self.master_sw, self.mblur_sw,
                  self.dof_sw, self.bloom_sw, self.vignette_sw,
                  self.grain_sw, self.low_end_sw,
                  self.ultra_low_sw, self.high_end_sw,
                  self.fast_results_sw):
            w.set_sensitive(plugin and not running)
        if not plugin:
            for w in (self.skip_movies_sw, self.logos_sw, self.autoskip_sw,
                      self.master_sw, self.mblur_sw, self.dof_sw,
                      self.bloom_sw, self.vignette_sw, self.grain_sw,
                      self.low_end_sw, self.ultra_low_sw,
                      self.high_end_sw, self.fast_results_sw):
                w.set_active(False)

        if self.mods:
            try:
                self.dual_sw.set_active(bool(self.mods.dual_audio_state()))
            except Exception:
                pass
            try:
                self.intro_sw.set_active(bool(self.mods.intro_state()))
            except Exception:
                pass
            try:
                lang = self.mods.language()
                for idx, (_, val) in enumerate(LANGUAGES):
                    if val == lang:
                        self.lang_dd.set_selected(idx)
                        break
                else:
                    self.lang_dd.set_selected(0)
            except Exception:
                pass
            if plugin:
                try:
                    cfg = self.mods.plugin_config()
                    self.skip_movies_sw.set_active(
                        cfg["Movies"].get("SkipEnabled", "true").lower() == "true")
                    pad = int(cfg["Movies"].get("SkipJoystickButton", "0"))
                    for idx, (_, val) in enumerate(PAD_BUTTONS):
                        if val == pad:
                            self.pad_dd.set_selected(idx)
                            break
                    self.logos_sw.set_active(
                        cfg["Movies"].get("AutoSkipLogos", "true").lower() == "true")
                    self.autoskip_sw.set_active(
                        float(cfg["Movies"].get("AutoSkipMovies", "0")) > 0)
                    self.fast_results_sw.set_active(
                        cfg.get("Results", {}).get("FastResults", "false").lower() == "true")
                    self.master_sw.set_active(
                        cfg.get("Gameplay", {}).get("UnlockMaster", "false").lower() == "true")
                    self.softreset_sw.set_active(
                        cfg.get("Gameplay", {}).get("SoftResetEnabled", "true").lower() == "true")
                    self._update_audio_labels()
                    self.bgm_shuffle_sw.set_active(
                        cfg.get("Audio", {}).get("CustomBgmShuffle", "true").lower() == "true")
                    self.sfx_shuffle_sw.set_active(
                        cfg.get("Audio", {}).get("CustomSfxShuffle", "true").lower() == "true")
                    self.rand_chars_sw.set_active(
                        cfg.get("Randomizer", {}).get("RandomStoryCharacters", "false").lower() == "true")
                    self.rand_enemies_sw.set_active(
                        cfg.get("Randomizer", {}).get("RandomEnemies", "false").lower() == "true")
                    self.skip_chars_sw.set_active(
                        cfg.get("Randomizer", {}).get("SkipCharacterSelect", "true").lower() == "true")
                    self.smart_bots_sw.set_active(
                        cfg.get("Bots", {}).get("SmarterBots", "false").lower() == "true")
                    self.random_cpu_sw.set_active(
                        cfg.get("Multiplayer", {}).get("RandomCpuOpponent", "false").lower() == "true")
                    self.cpu_skills_sw.set_active(
                        cfg.get("Multiplayer", {}).get("CpuSpecialAbilities", "true").lower() == "true")
                    self.platform_excl_sw.set_active(
                        cfg.get("Multiplayer", {}).get("PlatformExclusives", "false").lower() == "true")
                    self.mblur_sw.set_active(
                        cfg["Graphics"].get("DisableMotionBlur", "true").lower() != "true")
                    self.dof_sw.set_active(
                        cfg["Graphics"].get("DisableDepthOfField", "true").lower() != "true")
                    self.bloom_sw.set_active(
                        cfg["Graphics"].get("DisableBloom", "false").lower() != "true")
                    self.vignette_sw.set_active(
                        cfg["Graphics"].get("DisableVignette", "false").lower() != "true")
                    self.grain_sw.set_active(
                        cfg["Graphics"].get("DisableGrain", "false").lower() != "true")
                    self.low_end_sw.set_active(
                        cfg.get("Performance", {}).get("LowEnd", "false").lower() == "true")
                    self.ultra_low_sw.set_active(
                        cfg.get("Performance", {}).get("UltraLow", "false").lower() == "true")
                    self.high_end_sw.set_active(
                        cfg.get("Performance", {}).get("HighEnd", "false").lower() == "true")
                except Exception:
                    pass
        self._loading = False

    # -- handlers ---------------------------------------------------------
    def on_launch(self, *_):
        if not self.game:
            self.toast("Game not found")
            return
        args = []
        if self.border_sw.get_active():
            ratio = self.aspect_ratio()
            w, h = self.screen_size(ratio)
            args = ["-popupwindow", "-screen-fullscreen", "0",
                    "-screen-width", str(w), "-screen-height", str(h)]
            if ratio > 0:
                dw, dh = self.desktop_size()
                args += ["-screen-x", str(max(0, (dw - w) // 2)),
                         "-screen-y", str(max(0, (dh - h) // 2))]
        try:
            subprocess.Popen(["steam", "-applaunch", STEAM_APPID, *args],
                             start_new_session=True,
                             stdout=subprocess.DEVNULL,
                             stderr=subprocess.DEVNULL)
            self.toast("Launching Super Bomberman R…")
        except Exception as e:
            self.toast(f"Launch failed: {e}")

    def desktop_size(self):
        # Proton runs through Xwayland. GDK on Wayland reports the logical
        # monitor size (for example 1280x720 at scale 2), but Unity treats the
        # -screen-width/height values as physical pixels and creates a small
        # window. Ask the X server for the real Xwayland screen size instead.
        try:
            r = subprocess.run(["xdotool", "getdisplaygeometry"],
                               capture_output=True, text=True, timeout=2)
            if r.returncode == 0:
                parts = r.stdout.split()
                if len(parts) == 2:
                    return int(parts[0]), int(parts[1])
        except Exception:
            pass
        try:
            r = subprocess.run(["xrandr", "--current"],
                               capture_output=True, text=True, timeout=2)
            if r.returncode == 0:
                m = re.search(r"current\s+(\d+)\s*x\s*(\d+)", r.stdout)
                if m:
                    return int(m.group(1)), int(m.group(2))
        except Exception:
            pass
        try:
            display = Gdk.Display.get_default()
            mon = display.get_monitors().get_item(0)
            geo = mon.get_geometry()
            return geo.width, geo.height
        except Exception:
            return 1920, 1080

    def aspect_ratio(self):
        idx = self.aspect_dd.get_selected()
        if idx < 0 or idx >= len(ASPECTS):
            return 0.0
        return ASPECTS[idx][1]

    def screen_size(self, ratio=0.0):
        dw, dh = self.desktop_size()
        if ratio <= 0:
            return dw, dh
        if dw / dh > ratio:
            h = dh
            w = int(dh * ratio)
        else:
            w = dw
            h = int(dw / ratio)
        w = max(2, w - (w % 2))
        h = max(2, h - (h % 2))
        return w, h

    def on_borderless(self, row, _pspec):
        if self._loading:
            return
        self.cfg["borderless"] = row.get_active()
        save_config(self.cfg)

    def _audio_folder(self, key):
        if not self.mods:
            return ""
        try:
            return (self.mods.plugin_config().get("Audio", {})
                    .get(key, "") or "").strip()
        except Exception:
            return ""

    @staticmethod
    def _set_folder_button(btn, clear, path):
        if path:
            name = Path(path).name or path
            btn.set_label(name[:28] + ("…" if len(name) > 28 else ""))
            btn.set_tooltip_text(path)
            clear.set_sensitive(True)
        else:
            btn.set_label("Choose…")
            btn.set_tooltip_text("No folder selected")
            clear.set_sensitive(False)

    def _audio_source_folder(self, key, source_key):
        if not self.mods:
            return ""
        try:
            cfg = self.mods.plugin_config().get("Audio", {})
            return (cfg.get(source_key) or cfg.get(key) or "").strip()
        except Exception:
            return ""

    def _update_audio_labels(self):
        self._set_folder_button(
            self.bgm_btn, self.bgm_clear,
            self._audio_source_folder("CustomBgmFolder", "CustomBgmSource"))
        self._set_folder_button(
            self.sfx_btn, self.sfx_clear,
            self._audio_folder("CustomSfxFolder"))

    def _open_folder_chooser(self, title, current, callback):
        chooser = Gtk.FileChooserNative(
            title=title, transient_for=self,
            action=Gtk.FileChooserAction.SELECT_FOLDER,
            accept_label="Choose", cancel_label="Cancel")
        if current and Path(current).is_dir():
            try:
                chooser.set_current_folder(Gio.File.new_for_path(current))
            except Exception:
                pass
        chooser.connect("response", callback)
        chooser.show()
        return chooser

    def on_bgm_folder(self, _btn):
        if not self.mods or not self.mods.plugin_installed():
            self.toast("BomberQoL plugin not installed")
            return
        if game_running():
            self.toast("Close the game first")
            return
        if self._converting:
            self.toast("BGM conversion already running")
            return
        current = self._audio_source_folder(
            "CustomBgmFolder", "CustomBgmSource")
        self._bgm_chooser = self._open_folder_chooser(
            "Custom BGM folder", current, self._bgm_folder_response)

    def _bgm_folder_response(self, chooser, response):
        self._bgm_chooser = None
        if response == Gtk.ResponseType.ACCEPT:
            f = chooser.get_file()
            path = f.get_path() if f else None
            if path:
                self._start_bgm_conversion(path)
        chooser.destroy()

    def _start_bgm_conversion(self, source):
        if self._converting:
            return
        if game_running():
            self.toast("Close the game first")
            return
        self._converting = True
        self.bgm_btn.set_sensitive(False)
        self.toast("Converting BGM to OGG — this can take a while")

        def report(done, total):
            GLib.idle_add(self.bgm_btn.set_label,
                          f"Converting… {done}/{total}")

        def work():
            try:
                dest, converted, skipped = convert_bgm_folder(source, report)
                GLib.idle_add(self._bgm_conversion_done, str(dest),
                              str(source), converted, skipped, None)
            except Exception as e:
                GLib.idle_add(self._bgm_conversion_done, None,
                              str(source), 0, 0, str(e))

        threading.Thread(target=work, daemon=True).start()

    def _bgm_conversion_done(self, dest, source, converted, skipped, error):
        self._converting = False
        self.bgm_btn.set_sensitive(True)
        self._update_audio_labels()
        if error:
            self.toast("BGM conversion failed: " + error)
            return False
        try:
            self.mods.set_plugin_value("Audio", "CustomBgmFolder", dest)
            self.mods.set_plugin_value("Audio", "CustomBgmSource", source)
            self._update_audio_labels()
            message = f"BGM ready: {converted} tracks"
            if skipped:
                message += f", {skipped} skipped"
            self.toast(message + " — restart the game")
        except Exception as e:
            self.toast(str(e))
        return False

    def on_bgm_clear(self, _btn):
        if not self.mods or not self.mods.plugin_installed():
            self.toast("BomberQoL plugin not installed")
            return
        if game_running():
            self.toast("Close the game first")
            return
        try:
            self.mods.set_plugin_value("Audio", "CustomBgmFolder", "")
            self.mods.set_plugin_value("Audio", "CustomBgmSource", "")
            shutil.rmtree(_bgm_cache_dir(), ignore_errors=True)
            self._update_audio_labels()
            self.toast("Custom BGM folder cleared")
        except Exception as e:
            self.toast(str(e))

    def on_sfx_folder(self, _btn):
        if not self.mods or not self.mods.plugin_installed():
            self.toast("BomberQoL plugin not installed")
            return
        self._sfx_chooser = self._open_folder_chooser(
            "Custom SFX folder", self._audio_folder("CustomSfxFolder"),
            self._sfx_folder_response)

    def _sfx_folder_response(self, chooser, response):
        self._sfx_chooser = None
        if response == Gtk.ResponseType.ACCEPT:
            f = chooser.get_file()
            path = f.get_path() if f else None
            if path:
                try:
                    self.mods.set_plugin_value(
                        "Audio", "CustomSfxFolder", path)
                    self._update_audio_labels()
                    self.toast(f"Custom SFX folder: {path}")
                except Exception as e:
                    self.toast(str(e))
        chooser.destroy()

    def on_sfx_clear(self, _btn):
        if not self.mods or not self.mods.plugin_installed():
            self.toast("BomberQoL plugin not installed")
            return
        try:
            self.mods.set_plugin_value("Audio", "CustomSfxFolder", "")
            self._update_audio_labels()
            self.toast("Custom SFX folder cleared")
        except Exception as e:
            self.toast(str(e))

    def on_random_chars(self, row, _pspec):
        self._plugin_write("Randomizer", "RandomStoryCharacters", row.get_active())

    def on_random_enemies(self, row, _pspec):
        self._plugin_write("Randomizer", "RandomEnemies", row.get_active())

    def on_skip_chars(self, row, _pspec):
        self._plugin_write("Randomizer", "SkipCharacterSelect", row.get_active())

    def on_smart_bots(self, row, _pspec):
        self._plugin_write("Bots", "SmarterBots", row.get_active())

    def on_random_cpu(self, row, _pspec):
        self._plugin_write("Multiplayer", "RandomCpuOpponent", row.get_active())

    def on_cpu_skills(self, row, _pspec):
        self._plugin_write("Multiplayer", "CpuSpecialAbilities", row.get_active())

    def on_platform_exclusives(self, row, _pspec):
        self._plugin_write("Multiplayer", "PlatformExclusives", row.get_active())

    def on_room_achievements(self, _btn):
        if not self.mods or not self.mods.plugin_installed():
            self.toast("BomberQoL plugin not installed")
            return
        try:
            self.mods.set_plugin_option(
                "Achievements", "UnlockRoomAchievements", True)
            self.toast("Room achievements queued — launch the game once")
        except Exception as e:
            self.toast(str(e))

    def on_bgm_shuffle(self, row, _pspec):
        self._plugin_write("Audio", "CustomBgmShuffle", row.get_active())

    def on_sfx_shuffle(self, row, _pspec):
        self._plugin_write("Audio", "CustomSfxShuffle", row.get_active())

    def on_aspect(self, row, _pspec):
        if self._loading:
            return
        self.cfg["aspect"] = row.get_selected()
        save_config(self.cfg)

    def on_dual(self, row, _pspec):
        if self._loading or not self._guard():
            return
        want = row.get_active()
        try:
            self.mods.set_dual_audio(want)
            self.toast("Dual audio enabled" if want else "Voices restored")
        except Exception as e:
            self.toast(str(e))
        self.refresh()

    def on_intro(self, row, _pspec):
        if self._loading or not self._guard():
            return
        want = row.get_active()
        try:
            self.mods.set_intro_skip(want)
            self.toast("Intro skipped" if want else "Intro restored")
        except Exception as e:
            self.toast(str(e))
        self.refresh()

    def _plugin_write(self, section, key, value):
        if self._loading:
            return
        if not self.mods or not self.mods.plugin_installed():
            self.toast("BomberQoL plugin not installed")
            self.refresh()
            return
        try:
            self.mods.set_plugin_value(
                section, key, "true" if value else "false")
            self.toast("Saved — applies next launch")
        except Exception as e:
            self.toast(str(e))
        self.refresh()

    def on_skip_movies(self, row, _pspec):
        self._plugin_write("Movies", "SkipEnabled", row.get_active())

    def on_auto_logos(self, row, _pspec):
        self._plugin_write("Movies", "AutoSkipLogos", row.get_active())

    def on_auto_skip(self, row, _pspec):
        if self._loading or not self.mods or not self.mods.plugin_installed():
            return
        try:
            self.mods.set_plugin_value(
                "Movies", "AutoSkipMovies", "5" if row.get_active() else "0")
            self.toast("Auto-skip saved (next launch)")
        except Exception as e:
            self.toast(str(e))
        self.refresh()

    def on_low_end(self, row, _pspec):
        if self._loading or not self.mods or not self.mods.plugin_installed():
            return
        active = row.get_active()
        try:
            self.mods.set_plugin_option("Performance", "LowEnd", active)
            if active:
                self.mods.set_plugin_option("Performance", "HighEnd", False)
                self.mods.set_plugin_value("Performance", "TargetFps", "60")
            self.toast("Low-end mode " + ("on" if active else "off") +
                       " — applies next launch")
        except Exception as e:
            self.toast(str(e))
        self.refresh()

    def on_ultra_low(self, row, _pspec):
        if self._loading or not self.mods or not self.mods.plugin_installed():
            return
        active = row.get_active()
        try:
            self.mods.set_plugin_option("Performance", "UltraLow", active)
            if active:
                self.mods.set_plugin_option("Performance", "LowEnd", True)
                self.mods.set_plugin_option("Performance", "HighEnd", False)
                self.mods.set_plugin_value("Performance", "TargetFps", "60")
            self.toast("Ultra-low mode " + ("on" if active else "off") +
                       " — applies next launch")
        except Exception as e:
            self.toast(str(e))
        self.refresh()

    def on_high_end(self, row, _pspec):
        if self._loading or not self.mods or not self.mods.plugin_installed():
            return
        active = row.get_active()
        try:
            self.mods.set_plugin_option("Performance", "HighEnd", active)
            if active:
                self.mods.set_plugin_option("Performance", "LowEnd", False)
                self.mods.set_plugin_option("Performance", "UltraLow", False)
            self.toast("High-end mode " + ("on" if active else "off") +
                       " — applies next launch")
        except Exception as e:
            self.toast(str(e))
        self.refresh()

    def on_fast_results(self, row, _pspec):
        self._plugin_write("Results", "FastResults", row.get_active())

    def on_master(self, row, _pspec):
        self._plugin_write("Gameplay", "UnlockMaster", row.get_active())

    def on_soft_reset(self, row, _pspec):
        self._plugin_write("Gameplay", "SoftResetEnabled", row.get_active())

    def on_pad_button(self, row, _pspec):
        if self._loading or not self.mods or not self.mods.plugin_installed():
            return
        if game_running():
            self.toast("Close the game first")
            self.refresh()
            return
        name, value = PAD_BUTTONS[row.get_selected()]
        try:
            self.mods.set_plugin_value(
                "Movies", "SkipJoystickButton", str(value))
            self.toast(f"Controller skip: {name} (next launch)")
        except Exception as e:
            self.toast(str(e))
        self.refresh()

    def on_motion_blur(self, row, _pspec):
        self._plugin_write("Graphics", "DisableMotionBlur",
                           not row.get_active())

    def on_dof(self, row, _pspec):
        self._plugin_write("Graphics", "DisableDepthOfField",
                           not row.get_active())

    def on_bloom(self, row, _pspec):
        self._plugin_write("Graphics", "DisableBloom",
                           not row.get_active())

    def on_vignette(self, row, _pspec):
        self._plugin_write("Graphics", "DisableVignette",
                           not row.get_active())

    def on_grain(self, row, _pspec):
        self._plugin_write("Graphics", "DisableGrain",
                           not row.get_active())

    def on_language(self, row, _pspec):
        if self._loading or not self._guard():
            return
        name, value = LANGUAGES[row.get_selected()]
        try:
            self.mods.set_language(value)
            self.toast(f"Text language set to {name}")
        except Exception as e:
            self.toast(str(e))
        self.refresh()

    def on_install_bepinex(self, _row):
        if not self._guard():
            return
        try:
            msg = self.mods.install_bepinex()
            self.toast(msg + " — restart the game")
        except Exception as e:
            self.toast(str(e))
        self.refresh()

    def on_extract_art(self, _row):
        if not self._guard():
            return
        if self._extracting:
            self.toast("Artwork extraction already running")
            return
        mods = self.mods
        if not self.game or not (
                Path(self.game) / "SuperBombermanR.exe").exists():
            self.toast("Choose the game folder first")
            return
        self._extracting = True
        self.toast("Extracting artwork — this can take a minute")
        self.refresh()

        def work():
            try:
                msg = mods.extract_art()
                GLib.idle_add(self._extract_done, msg, None)
            except BaseException as exc:
                GLib.idle_add(self._extract_done, None, str(exc))

        threading.Thread(target=work, daemon=True).start()

    def _extract_done(self, message, error):
        self._extracting = False
        if error:
            self.toast("Artwork extraction failed: " + error)
        else:
            self.toast(message)
        self.refresh()
        return False

    def on_backup_save(self, _row):
        if not self.mods:
            return
        try:
            SAVE_BACKUPS.mkdir(parents=True, exist_ok=True)
            dst = SAVE_BACKUPS / \
                f"savedata_{time.strftime('%Y%m%d_%H%M%S')}.bin"
            shutil.copy2(self.mods.save, dst)
            self.toast(f"Saved to {dst.name}")
        except Exception as e:
            self.toast(str(e))

    def on_open_modkit(self, _row):
        MODKIT.mkdir(parents=True, exist_ok=True)
        Gio.AppInfo.launch_default_for_uri(MODKIT.as_uri(), None)

    def on_restore(self, _row):
        if not self._guard():
            return
        dialog = Adw.AlertDialog(
            heading="Restore original files?",
            body="Dual audio and intro-skip changes will be reverted.",
            close_response="cancel")
        dialog.add_response("cancel", "Cancel")
        dialog.add_response("restore", "Restore")
        dialog.set_response_appearance("restore",
                                       Adw.ResponseAppearance.DESTRUCTIVE)
        dialog.connect("response", self._restore_response)
        dialog.present(self)

    def _restore_response(self, dialog, response):
        if response != "restore":
            return
        try:
            self.mods.restore_all()
            self.toast("Original files restored")
        except Exception as e:
            self.toast(str(e))
        self.refresh()



    def _scroll_fraction(self, fraction):
        adjustment = self.scroller.get_vadjustment()
        adjustment.set_value(
            adjustment.get_upper() * max(0.0, min(1.0, fraction)))
        return False

    # -- SBR2 tab ---------------------------------------------------------
    def _sbr2_bool(self, section, key, default=False):
        value = get_sbr2_plugin_value(
            section, key, "true" if default else "false")
        return str(value).strip().lower() in ("true", "1", "yes", "on")

    def _sbr2_write(self, section, key, value, toast=True):
        if self._sbr2_loading:
            return
        try:
            set_sbr2_plugin_value(section, key, value)
            if toast:
                self.toast("SBR2 setting saved — restart the game")
        except Exception as e:
            self.toast(str(e))

    def on_sbr2_switch(self, row, _pspec, section, key):
        self._sbr2_write(section, key,
                         "true" if row.get_active() else "false")

    def on_sbr2_dropdown(self, row, _pspec, section, key, values):
        idx = row.get_selected()
        if 0 <= idx < len(values):
            self._sbr2_write(section, key, values[idx])

    def on_sbr2_volume_change(self, _scale, _scroll, value):
        if self._sbr2_loading:
            return False
        self._sbr2_write("Audio", "CustomBgmVolume",
                         f"{value / 100.0:.2f}", toast=False)
        return False

    def _sbr2_switch_row(self, card, title, subtitle, section, key,
                         default=False):
        sw = Gtk.Switch(valign=Gtk.Align.CENTER)
        sw.set_active(self._sbr2_bool(section, key, default))
        sw.connect("notify::active", self.on_sbr2_switch, section, key)
        card.append(self._row("avatar_blue.png", title, subtitle, sw))
        card.append(self._separator())
        self._sbr2_widgets.append(sw)
        return sw

    def _sbr2_dropdown_row(self, card, title, subtitle, section, key,
                           display, values, avatar="avatar_yellow.png"):
        dd = Gtk.DropDown.new_from_strings(display)
        dd.set_valign(Gtk.Align.CENTER)
        current = str(get_sbr2_plugin_value(section, key, "")).strip().lower()
        index = 0
        for i, value in enumerate(values):
            if str(value).strip().lower() == current:
                index = i
                break
        dd.set_selected(index)
        dd.connect("notify::selected", self.on_sbr2_dropdown,
                   section, key, values)
        card.append(self._row(avatar, title, subtitle, dd))
        card.append(self._separator())
        self._sbr2_widgets.append(dd)
        return dd

    def _build_sbr2_page(self):
        self._sbr2_loading = True
        self._sbr2_converting = False
        self._sbr2_chooser = None
        self._sbr2_widgets = []

        page = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        page.set_margin_top(12)
        page.set_margin_bottom(14)
        page.set_margin_start(18)
        page.set_margin_end(18)

        banner = Gtk.Label(label="SUPER BOMBERMAN R 2 — QoL")
        banner.add_css_class("section-fallback")
        banner.set_xalign(0)
        page.append(banner)

        page.append(self._section("sbr2_status"))
        status = self._card()
        status_box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=4)
        self.sbr2_status = Gtk.Label(label="")
        self.sbr2_status.set_xalign(0)
        self.sbr2_status.set_wrap(True)
        self.sbr2_status.add_css_class("row-sub")
        open_btn = Gtk.Button(label="Open Folder")
        open_btn.add_css_class("flat")
        open_btn.set_halign(Gtk.Align.START)
        open_btn.connect("clicked", self.on_sbr2_open_folder)
        status_box.append(self.sbr2_status)
        status_box.append(open_btn)
        status.append(self._row(
            "avatar_blue.png", "Game Folder",
            "Steam install with BepInEx 6 IL2CPP x64", status_box))
        status.append(self._separator())

        actions = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=6)
        install = Gtk.Button(label="Install / Update")
        install.connect("clicked", self.on_sbr2_install)
        launch = Gtk.Button(label="Launch SBR2")
        launch.add_css_class("suggested-action")
        launch.connect("clicked", self.on_sbr2_launch)
        actions.append(install)
        actions.append(launch)
        status.append(self._row(
            "avatar_yellow.png", "Plugin & Game",
            "Copy the built plugin, then launch through Steam", actions))
        page.append(status)
        page.append(Gtk.Separator())

        page.append(self._section("sbr2_movies"))
        movies = self._card()
        self._sbr2_switch_row(movies, "Skip Movies",
                              "Press the skip key during any movie",
                              "Movies", "SkipEnabled", True)
        self._sbr2_dropdown_row(
            movies, "Skip Key", "Key used to skip a movie",
            "Movies", "SkipKey",
            ["Space", "Escape", "Return", "F8"],
            ["Space", "Escape", "Return", "F8"])
        self._sbr2_dropdown_row(
            movies, "Auto-skip Movies", "Seconds before a movie is skipped",
            "Movies", "AutoSkipMovies",
            ["Off", "3 s", "5 s", "10 s", "15 s", "30 s"],
            ["0", "3", "5", "10", "15", "30"])
        note = Gtk.Label(
            label=("Logo skip stays off: the old hook left SBR2 on a "
                   "black screen."))
        note.set_xalign(0)
        note.set_wrap(True)
        note.add_css_class("row-sub")
        note.set_margin_start(12)
        note.set_margin_end(12)
        note.set_margin_bottom(8)
        movies.append(note)
        page.append(movies)
        page.append(Gtk.Separator())

        page.append(self._section("sbr2_graphics"))
        gfx = self._card()
        self._sbr2_switch_row(gfx, "Disable Motion Blur",
                              "HDRP volume stack", "Graphics",
                              "DisableMotionBlur", True)
        self._sbr2_switch_row(gfx, "Disable Depth of Field",
                              "HDRP volume stack", "Graphics",
                              "DisableDepthOfField", True)
        self._sbr2_switch_row(gfx, "Disable Bloom", "HDRP volume stack",
                              "Graphics", "DisableBloom", False)
        self._sbr2_switch_row(gfx, "Disable Vignette", "HDRP volume stack",
                              "Graphics", "DisableVignette", False)
        self._sbr2_switch_row(gfx, "Disable Film Grain",
                              "HDRP volume stack", "Graphics",
                              "DisableFilmGrain", False)
        self._sbr2_switch_row(gfx, "Disable Chromatic Aberration",
                              "HDRP volume stack", "Graphics",
                              "DisableChromaticAberration", False)
        self._sbr2_switch_row(gfx, "Disable Lens Distortion",
                              "HDRP volume stack", "Graphics",
                              "DisableLensDistortion", False)
        self._sbr2_dropdown_row(
            gfx, "Field of View", "Gameplay camera override",
            "Graphics", "FieldOfView",
            ["Vanilla", "60", "70", "75", "80", "85", "90", "100", "110"],
            ["0", "60", "70", "75", "80", "85", "90", "100", "110"])
        page.append(gfx)
        page.append(Gtk.Separator())

        page.append(self._section("sbr2_audio"))
        audio = self._card()
        self.sbr2_bgm_btn = Gtk.Button(label="Choose…")
        self.sbr2_bgm_btn.set_valign(Gtk.Align.CENTER)
        self.sbr2_bgm_btn.connect("clicked", self.on_sbr2_bgm_folder)
        self.sbr2_bgm_clear = Gtk.Button(label="Clear")
        self.sbr2_bgm_clear.add_css_class("flat")
        self.sbr2_bgm_clear.set_valign(Gtk.Align.CENTER)
        self.sbr2_bgm_clear.connect("clicked", self.on_sbr2_bgm_clear)
        bgm_box = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=4)
        bgm_box.append(self.sbr2_bgm_btn)
        bgm_box.append(self.sbr2_bgm_clear)
        audio.append(self._row(
            "avatar_white.png", "Custom BGM Folder",
            "Music folder converted to the shared OGG cache", bgm_box))
        audio.append(self._separator())

        volume = Gtk.Scale.new_with_range(
            Gtk.Orientation.HORIZONTAL, 0, 100, 5)
        volume.set_size_request(170, -1)
        volume.set_draw_value(True)
        try:
            volume.set_value(round(float(get_sbr2_plugin_value(
                "Audio", "CustomBgmVolume", "0.8")) * 100))
        except Exception:
            volume.set_value(80)
        volume.connect("change-value", self.on_sbr2_volume_change)
        audio.append(self._row("avatar_yellow.png", "BGM Volume",
                               "Custom track volume", volume))
        audio.append(self._separator())
        self._sbr2_widgets.append(volume)

        self._sbr2_switch_row(audio, "Shuffle BGM",
                              "Random track when a label has no exact file",
                              "Audio", "CustomBgmShuffle", True)
        self._sbr2_switch_row(audio, "Playlist BGM",
                              "Cycle tracks when a label has no exact file",
                              "Audio", "CustomBgmPlaylist", False)
        page.append(audio)
        page.append(Gtk.Separator())

        page.append(self._section("sbr2_story"))
        story = self._card()
        story_keys = [
            "white", "black", "pink", "blue", "red", "yellow", "green",
            "aqua", "simon", "pyramid", "richter", "lance", "bill", "mimi",
            "nyami", "alucard", "vicviper", "nurse", "lobby", "tomato",
            "nakedSnake", "raiden", "oldsnake", "vicviperzero", "somaCruz",
            "fujisaki", "genso2p", "goemon", "magnet", "tinyGolem",
            "phantom", "sexy", "plasma", "fiter", "Massive", "wing", "bean",
            "lucaBlight",
        ]
        self._sbr2_dropdown_row(
            story, "Story Character",
            "Story-mode player; Default keeps White", "Story",
            "StoryCharacter",
            ["Default (White)"] + [k.capitalize() for k in story_keys],
            [""] + story_keys)
        page.append(story)
        page.append(Gtk.Separator())

        page.append(self._section("sbr2_interface"))
        interface = self._card()
        self._sbr2_switch_row(interface, "F9 Overlay",
                              "Show the in-game settings overlay",
                              "Interface", "OverlayEnabled", True)
        self._sbr2_switch_row(
            interface, "Replace Settings Gear",
            "Gear / Basic Settings open the overlay; hold Shift for vanilla",
            "Interface", "ReplaceOptionsMenu", True)
        self._sbr2_dropdown_row(
            interface, "Overlay Key", "Keyboard toggle",
            "Interface", "OverlayKey",
            ["F9", "F8", "F10", "F11", "Insert"],
            ["F9", "F8", "F10", "F11", "Insert"])
        pad_note = Gtk.Label(
            label=("Controller: left stick / D-pad moves, A changes, "
                   "B or Start closes."))
        pad_note.set_xalign(0)
        pad_note.set_wrap(True)
        pad_note.add_css_class("row-sub")
        pad_note.set_margin_start(12)
        pad_note.set_margin_end(12)
        pad_note.set_margin_bottom(8)
        interface.append(pad_note)
        page.append(interface)
        page.append(Gtk.Separator())

        page.append(self._section("sbr2_debug"))
        debug = self._card()
        self._sbr2_switch_row(debug, "Self Test",
                              "Play one custom track at startup and log it",
                              "Debug", "SelfTest", False)
        page.append(debug)

        tip = Gtk.Label(
            label="Press F9 in-game for the Harmony-only settings overlay.")
        tip.add_css_class("footer")
        tip.set_wrap(True)
        tip.set_margin_top(6)
        page.append(tip)

        self._sbr2_loading = False
        self._update_sbr2_bgm_label()
        return page

    def refresh_sbr2(self):
        if not hasattr(self, "sbr2_status"):
            return
        game = find_sbr2_dir()
        running = sbr2_running()
        if not game:
            self.sbr2_status.set_text(
                "Not found — install SUPER BOMBERMAN R2 from Steam")
            for w in self._sbr2_widgets:
                w.set_sensitive(False)
            return
        core = (game / "BepInEx/core").is_dir()
        plugin = (game / SBR2_PLUGIN_REL).exists()
        interop = (game / "BepInEx/interop/GameAssembly.dll").exists()
        if not core:
            state = "BepInEx 6 IL2CPP x64 is not installed"
        elif not plugin:
            state = "BepInEx ready · plugin not installed"
        elif not interop:
            state = "Plugin installed · run once to generate interop"
        else:
            state = "Plugin installed · interop ready"
        if running:
            state += " · game running"
        self.sbr2_status.set_text(f"{game}\n{state}")
        for w in self._sbr2_widgets:
            w.set_sensitive(not running)

    def _update_sbr2_bgm_label(self):
        if not hasattr(self, "sbr2_bgm_btn"):
            return
        path = (get_sbr2_plugin_value(
            "Audio", "CustomBgmFolder", "") or "").strip()
        self._set_folder_button(
            self.sbr2_bgm_btn, self.sbr2_bgm_clear, path)

    def on_sbr2_install(self, _btn):
        try:
            dest = install_sbr2_plugin()
            self.toast(f"SBR2 plugin installed in {dest}")
            self.refresh_sbr2()
        except Exception as e:
            self.toast(str(e))

    def on_sbr2_launch(self, _btn):
        try:
            subprocess.Popen(["steam", "-applaunch", SBR2_APPID],
                             start_new_session=True,
                             stdout=subprocess.DEVNULL,
                             stderr=subprocess.DEVNULL)
            self.toast("Launching SUPER BOMBERMAN R2…")
        except Exception as e:
            self.toast(f"Launch failed: {e}")

    def on_sbr2_open_folder(self, _btn):
        game = find_sbr2_dir()
        if not game:
            self.toast("SBR2 folder not found")
            return
        try:
            Gio.AppInfo.launch_default_for_uri(game.as_uri(), None)
        except Exception as e:
            self.toast(str(e))

    def on_sbr2_bgm_folder(self, _btn):
        if sbr2_running():
            self.toast("Close SBR2 first")
            return
        if self._sbr2_converting:
            self.toast("BGM conversion already running")
            return
        current = (get_sbr2_plugin_value(
            "Audio", "CustomBgmFolder", "") or "").strip()
        self._sbr2_chooser = self._open_folder_chooser(
            "SBR2 custom BGM folder", current, self._sbr2_bgm_response)

    def _sbr2_bgm_response(self, chooser, response):
        self._sbr2_chooser = None
        if response == Gtk.ResponseType.ACCEPT:
            f = chooser.get_file()
            path = f.get_path() if f else None
            if path:
                self._sbr2_start_conversion(path)
        chooser.destroy()

    def _sbr2_start_conversion(self, source):
        if self._sbr2_converting:
            return
        self._sbr2_converting = True
        self.sbr2_bgm_btn.set_sensitive(False)
        self.toast("Converting BGM to OGG — this can take a while")

        def report(done, total):
            GLib.idle_add(self.sbr2_bgm_btn.set_label,
                          f"Converting… {done}/{total}")

        def work():
            try:
                dest, converted, skipped = convert_bgm_folder(source, report)
                GLib.idle_add(self._sbr2_conversion_done, str(dest),
                              converted, skipped, None)
            except Exception as e:
                GLib.idle_add(self._sbr2_conversion_done, None, 0, 0, str(e))

        threading.Thread(target=work, daemon=True).start()

    def _sbr2_conversion_done(self, dest, converted, skipped, error):
        self._sbr2_converting = False
        self.sbr2_bgm_btn.set_sensitive(True)
        if error:
            self.toast("BGM conversion failed: " + error)
            return False
        try:
            set_sbr2_plugin_value("Audio", "CustomBgmFolder", dest)
            set_sbr2_plugin_value("Audio", "CustomBgmShuffle", "true")
            self._update_sbr2_bgm_label()
            message = f"SBR2 BGM ready: {converted} tracks"
            if skipped:
                message += f", {skipped} skipped"
            self.toast(message + " — restart SBR2")
        except Exception as e:
            self.toast(str(e))
        return False

    def on_sbr2_bgm_clear(self, _btn):
        if sbr2_running():
            self.toast("Close SBR2 first")
            return
        try:
            set_sbr2_plugin_value("Audio", "CustomBgmFolder", "")
            self._update_sbr2_bgm_label()
            self.toast("SBR2 custom BGM disabled")
        except Exception as e:
            self.toast(str(e))


class BomberApp(Adw.Application):
    def __init__(self):
        super().__init__(application_id=APP_ID,
                         flags=Gio.ApplicationFlags.DEFAULT_FLAGS)

    def do_activate(self):
        Adw.StyleManager.get_default().set_color_scheme(
            Adw.ColorScheme.FORCE_LIGHT)
        css = Gtk.CssProvider()
        css.load_from_string(
            CSS.replace("__ASSETS__", ASSETS.as_uri()))
        Gtk.StyleContext.add_provider_for_display(
            Gdk.Display.get_default(), css,
            Gtk.STYLE_PROVIDER_PRIORITY_APPLICATION)
        win = self.get_active_window() or BomberWindow(self)
        win.present()


def check():
    game = find_game_dir()
    print("game:", game)
    if not game:
        return 1
    m = Mods(game)
    print("dual audio:", m.dual_audio_state())
    print("intro skip:", m.intro_state())
    print("save language:", m.language())
    print("running:", game_running())
    print("bepinex plugin:", m.plugin_installed())
    return 0


def apply_preset(name):
    game = find_game_dir()
    if not game:
        print("game not found")
        return 1
    mods = Mods(game)
    if name in ("ultra-low", "ultralow", "ultra"):
        mods.set_plugin_option("Performance", "UltraLow", True)
        mods.set_plugin_option("Performance", "LowEnd", True)
        mods.set_plugin_option("Performance", "HighEnd", False)
        mods.set_plugin_value("Performance", "TargetFps", "60")
        print("Ultra-low mode enabled for", game)
        return 0
    if name in ("low-end", "lowend", "low"):
        mods.set_plugin_option("Performance", "LowEnd", True)
        mods.set_plugin_option("Performance", "UltraLow", False)
        mods.set_plugin_option("Performance", "HighEnd", False)
        mods.set_plugin_value("Performance", "TargetFps", "60")
        print("Low-end mode enabled for", game)
        return 0
    if name in ("high-end", "highend", "high", "quality"):
        mods.set_plugin_option("Performance", "HighEnd", True)
        mods.set_plugin_option("Performance", "LowEnd", False)
        mods.set_plugin_option("Performance", "UltraLow", False)
        print("High-end mode enabled for", game)
        return 0
    if name in ("balanced", "default"):
        mods.set_plugin_option("Performance", "LowEnd", False)
        mods.set_plugin_option("Performance", "UltraLow", False)
        mods.set_plugin_option("Performance", "HighEnd", False)
        print("Performance modes disabled for", game)
        return 0
    print("unknown preset:", name)
    return 1


def main():
    if "--check" in sys.argv:
        raise SystemExit(check())
    if "--sbr2-bgm" in sys.argv:
        src = None
        for i, arg in enumerate(sys.argv):
            if arg == "--sbr2-bgm" and i + 1 < len(sys.argv):
                nxt = sys.argv[i + 1]
                if not nxt.startswith("-"):
                    src = nxt
            elif arg.startswith("--sbr2-bgm="):
                src = arg.split("=", 1)[1]
        raise SystemExit(apply_sbr2_bgm(src))
    if "--high-end" in sys.argv or "--highend" in sys.argv:
        raise SystemExit(apply_preset("high-end"))
    if "--ultra-low" in sys.argv or "--ultralow" in sys.argv:
        raise SystemExit(apply_preset("ultra-low"))
    if "--low-end" in sys.argv or "--lowend" in sys.argv:
        raise SystemExit(apply_preset("low-end"))
    for arg in sys.argv[1:]:
        if arg.startswith("--preset="):
            raise SystemExit(apply_preset(arg.split("=", 1)[1]))
    if "--extract-art" in sys.argv:
        import traceback
        global ART
        idx = sys.argv.index("--extract-art")
        game = sys.argv[idx + 1] if len(sys.argv) > idx + 1 else None
        out = sys.argv[idx + 2] if len(sys.argv) > idx + 2 else None
        if out:
            ART = Path(out)
        try:
            mods = Mods(game or find_game_dir())
            print(mods.extract_art())
        except BaseException:
            traceback.print_exc()
            raise SystemExit(1)
        return
    BomberApp().run(None)


if __name__ == "__main__":
    main()
