#!/usr/bin/env python3
"""Bomber Launcher — QoL front-end for Super Bomberman R (Steam 702700).

All artwork is extracted from the game itself (see tools/extract_ui_assets.py).
"""

import hashlib
import json
import os
import shutil
import struct
import subprocess
import sys
import time
from collections import OrderedDict
from pathlib import Path

import gi
gi.require_version("Gtk", "4.0")
gi.require_version("Adw", "1")
from gi.repository import Adw, Gdk, Gio, GLib, Gtk  # noqa: E402

APP_ID = "io.github.bomberlauncher.BomberLauncher"
APP_NAME = "Bomber Launcher"
STEAM_APPID = "702700"
GAME_DIR_NAME = "Super Bomberman R"

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
ART = Path(os.environ.get("BOMBER_ASSETS", str(ASSETS)))
if not (ART / "hero.png").exists() and not os.environ.get("BOMBER_ASSETS"):
    ART = (PORTABLE / "art") if PORTABLE else (HOME / ".local/share/bomber-launcher/assets")
HAVE_ART = (ART / "hero.png").exists()
CONFIG_PATH = (PORTABLE / "config.json") if PORTABLE else (HOME / ".config" / "bomber-launcher" / "config.json")

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
        ART.mkdir(parents=True, exist_ok=True)
        os.environ["BOMBER_ASSETS"] = str(ART)
        spec = importlib.util.spec_from_file_location(
            "bomber_extract_art", script)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        mod.main(str(self.game))
        return "Artwork extracted — restart the launcher"

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
        self.game = find_game_dir()
        self.mods = Mods(self.game) if self.game else None
        self._loading = False

        self.toasts = Adw.ToastOverlay()
        self.set_content(self.toasts)

        root = Gtk.Box(orientation=Gtk.Orientation.VERTICAL)
        root.add_css_class("root")
        self.toasts.set_child(root)

        header = Adw.HeaderBar()
        title = Gtk.Label(label=APP_NAME)
        title.add_css_class("header-title")
        header.set_title_widget(title)
        root.append(header)

        scroller = Gtk.ScrolledWindow(vexpand=True)
        scroller.set_policy(Gtk.PolicyType.NEVER, Gtk.PolicyType.AUTOMATIC)
        root.append(scroller)

        page = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        page.set_margin_top(12)
        page.set_margin_bottom(14)
        page.set_margin_start(18)
        page.set_margin_end(18)
        scroller.set_child(page)

        page.append(self._build_hero())

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
        page.append(basic)

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

        self.refresh()
        GLib.timeout_add_seconds(3, self._poll)

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
            "language": "CHANGE LANGUAGE",
            "basic": "BASIC SETTINGS",
            "environment": "ENVIRONMENT",
            "other": "OTHER",
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

        for w in (self.dual_sw, self.intro_sw, self.lang_dd, self.border_sw):
            w.set_sensitive(self.mods is not None and not running)
        plugin = self.mods is not None and self.mods.plugin_installed()
        for w in (self.skip_movies_sw, self.pad_dd, self.logos_sw,
                  self.autoskip_sw, self.mblur_sw, self.dof_sw,
                  self.bloom_sw, self.fast_results_sw):
            w.set_sensitive(plugin and not running)
        if not plugin:
            for w in (self.skip_movies_sw, self.logos_sw, self.autoskip_sw,
                      self.mblur_sw, self.dof_sw, self.bloom_sw,
                      self.fast_results_sw):
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
                    self.mblur_sw.set_active(
                        cfg["Graphics"].get("DisableMotionBlur", "true").lower() != "true")
                    self.dof_sw.set_active(
                        cfg["Graphics"].get("DisableDepthOfField", "true").lower() != "true")
                    self.bloom_sw.set_active(
                        cfg["Graphics"].get("DisableBloom", "false").lower() != "true")
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
            w, h = self.screen_size()
            args = ["-popupwindow", "-screen-width", str(w),
                    "-screen-height", str(h)]
        try:
            subprocess.Popen(["steam", "-applaunch", STEAM_APPID, *args],
                             start_new_session=True,
                             stdout=subprocess.DEVNULL,
                             stderr=subprocess.DEVNULL)
            self.toast("Launching Super Bomberman R…")
        except Exception as e:
            self.toast(f"Launch failed: {e}")

    def screen_size(self):
        try:
            display = Gdk.Display.get_default()
            mon = display.get_monitors().get_item(0)
            geo = mon.get_geometry()
            return geo.width, geo.height
        except Exception:
            return 1920, 1080

    def on_borderless(self, row, _pspec):
        if self._loading:
            return
        self.cfg["borderless"] = row.get_active()
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
            self.mods.set_plugin_option(
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

    def on_fast_results(self, row, _pspec):
        self._plugin_write("Results", "FastResults", row.get_active())

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
        try:
            msg = self.mods.extract_art()
            self.toast(msg)
        except Exception as e:
            self.toast(str(e))
        self.refresh()

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


class BomberApp(Adw.Application):
    def __init__(self):
        super().__init__(application_id=APP_ID,
                         flags=Gio.ApplicationFlags.DEFAULT_FLAGS)

    def do_activate(self):
        Adw.StyleManager.get_default().set_color_scheme(
            Adw.ColorScheme.FORCE_LIGHT)
        css = Gtk.CssProvider()
        css.load_from_data(
            CSS.replace("__ASSETS__", ASSETS.as_uri()).encode("utf-8"))
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


def main():
    if "--check" in sys.argv:
        raise SystemExit(check())
    BomberApp().run(None)


if __name__ == "__main__":
    main()
