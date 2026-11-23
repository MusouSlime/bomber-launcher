#!/usr/bin/env python3
"""Create a palette-swapped character skin from a game character atlas.

The game's character skins are 128x128 flat-colour atlases (body, face,
gloves, boots, belt, buckle). This tool remaps those palette colours to a new
character's palette without touching the mesh, skeleton or animations.

Example:
    ffmpeg/UnityPy extract tex_pc001_white_AL.png from chara_pack_01
    python3 tools/make_palette_skin.py --base tex_pc001_white_AL.png \
        --out viewtifuljoe.png --preset joe

The output replaces the target character's albedo at runtime:
    BepInEx/plugins/BomberQoL/skins/<name>/texture.png

Only derived colours are produced; no game artwork is stored in this repo.
"""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def map_joe(r: int, g: int, b: int) -> tuple[int, int, int]:
    # Face/skin patch first.
    if r > 235 and g > 200 and 140 < b < 205:
        return (248, 180, 120)
    # Whites: body/helmet -> red suit.
    if r > 235 and g > 235 and b > 235:
        return (248, 28, 8)
    # Pale blue highlights -> red.
    if b > 200 and g > 200 and r < 245:
        return (232, 24, 8)
    # Blue overalls -> dark red.
    if b > 140 and b > r * 1.25 and b > g * 1.25:
        return (120, 16, 0)
    # Pink gloves/boots -> white.
    if r > 195 and b > 55 and g < 160:
        return (248, 248, 248)
    # Yellow buckle -> gold.
    if r > 200 and g > 175 and b < 140:
        return (248, 204, 88)
    # Black eyes/belt.
    if max(r, g, b) < 70:
        return (0, 0, 0)
    # Neutral shadows -> dark red-brown.
    if abs(r - g) < 22 and abs(g - b) < 22 and abs(r - b) < 22:
        return (70, 20, 14)
    return (248, 28, 8)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True, help="base atlas PNG")
    ap.add_argument("--out", required=True, help="output skin PNG")
    ap.add_argument("--preset", default="joe", choices=["joe"])
    args = ap.parse_args()

    base = Image.open(args.base).convert("RGB")
    out = Image.new("RGB", base.size)
    src = base.load()
    dst = out.load()
    w, h = base.size
    for y in range(h):
        for x in range(w):
            dst[x, y] = map_joe(*src[x, y])
    out.save(args.out)
    print(f"wrote {args.out} ({w}x{h})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
