#!/usr/bin/env python3
import os
"""Rebuild Bomber Launcher art from Super Bomberman R's own assets.

Requires: UnityPy, Pillow (use a venv). Usage:
    /tmp/opencode/venv/bin/python tools/extract_ui_assets.py [game_dir]

Everything written to assets/ is derived from files shipped with the game.
"""
import hashlib
import shutil
import sys
import tempfile
from pathlib import Path

import UnityPy
from PIL import Image

try:
    from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes

    def aes_cbc_decrypt(key, iv, data):
        c = Cipher(algorithms.AES(key), modes.CBC(iv)).decryptor()
        return c.update(data) + c.finalize()
except ImportError:
    from Cryptodome.Cipher import AES

    def aes_cbc_decrypt(key, iv, data):
        return AES.new(key, AES.MODE_CBC, iv).decrypt(data)

PW, SALT, ITER = "c4GXA8FT", b"t4ChYLcH", 1103
HERE = Path(__file__).resolve().parent.parent
OUT = Path(os.environ.get("BOMBER_ASSETS", str(HERE / "assets")))
DEFAULT_GAME = (Path.home() / ".local/share/Steam/steamapps/common/"
                "Super Bomberman R")


def decrypt(path, dst):
    stem = Path(path).name.rsplit(".", 1)[0]
    d = hashlib.pbkdf2_hmac("sha1", (stem + PW).encode(), SALT, ITER, 32)
    data = Path(path).read_bytes()
    out = aes_cbc_decrypt(d[:16], d[16:32], data)
    Path(dst).write_bytes(out)
    return dst


def sprites(path):
    env = UnityPy.load(str(path))
    found = {}
    for o in env.objects:
        if o.type.name in ("Sprite", "Texture2D"):
            try:
                found.setdefault(o.read().m_Name, o)
            except Exception:
                pass
    return found


def export(path, name, dst, size=None):
    objs = sprites(path)
    o = objs.get(name)
    if o is None:
        print("missing:", name, "in", path)
        return None
    img = o.read().image.convert("RGBA")
    if size:
        img = img.resize(size, Image.LANCZOS)
    img.save(dst, optimize=True)
    return img


def main(game_dir=None, out_dir=None):
    global OUT
    if out_dir is not None:
        OUT = Path(out_dir)
    if game_dir is not None:
        game = Path(game_dir)
    elif len(sys.argv) > 1:
        game = Path(sys.argv[1])
    else:
        game = DEFAULT_GAME
    if not (game / "SuperBombermanR.exe").exists():
        raise RuntimeError(f"game not found: {game}")
    OUT.mkdir(parents=True, exist_ok=True)
    tmp = Path(tempfile.mkdtemp(prefix="bomber-art-"))
    steam = game / "SuperBombermanR_Data/StreamingAssets/InAppAssets/steam"

    res = game / "SuperBombermanR_Data/resources.assets"
    locale = decrypt(steam / "locale_pack.en-us", tmp / "locale.bin")
    chara = decrypt(steam / "chara/chara_pack_01", tmp / "chara.bin")

    export(res, "app_icon", OUT / "logo_square.png", (256, 256))
    export(res, "BG_Title_mountain", OUT / "mountain.png")
    for src, dst in (("DecorationWord_Other_VariousSettings", "header_basic.png"),
                     ("DecorationWord_Other_ChangeLanguage", "header_language.png"),
                     ("DecorationWord_Other", "header_other.png"),
                     ("DecorationWord_Other_EnvironmentSettings", "header_environment.png")):
        export(locale, src, tmp / dst)
    export(res, "Select_Bar_Long_default", tmp / "bar.png")

    for c in ("white", "red", "blue", "green", "yellow", "pink"):
        export(chara, f"large_icon_{c}", OUT / f"avatar_{c}.png", (96, 96))

    # hero crop + composited section bars
    engage = export(res, "EngageBack", tmp / "engage.png")
    engage.crop((300, 290, 1620, 900)).save(OUT / "hero.png", optimize=True)

    bar = Image.open(tmp / "bar.png").convert("RGBA")
    W, H = 1328, 76
    s = H / bar.height
    left = bar.crop((0, 0, 110, bar.height)).resize((int(110 * s), H),
                                                    Image.LANCZOS)
    right = bar.crop((646, 0, 676, bar.height)).resize((int(30 * s), H),
                                                       Image.LANCZOS)
    wide = Image.new("RGBA", (W, H), (60, 70, 71, 255))
    wide.paste(left, (0, 0))
    wide.paste(right, (W - right.width, 0))

    for name in ("basic", "language", "other", "environment"):
        wm = Image.open(tmp / f"header_{name}.png").convert("RGBA")
        h = 44
        wm = wm.resize((int(wm.width * h / wm.height), h), Image.LANCZOS)
        canvas = wide.copy()
        canvas.alpha_composite(wm, (120, (H - h) // 2))
        canvas.save(OUT / f"section_{name}.png", optimize=True)

    shutil.rmtree(tmp, ignore_errors=True)
    print("assets rebuilt in", OUT)


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(exc, file=sys.stderr)
        raise SystemExit(1)
