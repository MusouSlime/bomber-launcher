#!/usr/bin/env python3
"""Extract textures, sprites, audio and text from Super Bomberman R 2 bundles.

The SBR2 payload bundles are header-encrypted UnityFS files; this script uses
`sbr2_pack.decrypt_bytes` to make them readable, then UnityPy to export assets.

Examples:
    # First 200 textures/sprites from the shared battle bundle
    python3 tools/extract_sbr2_assets.py --kind texture --bundle common_battle

    # All audio clips from the battle SE bundle
    python3 tools/extract_sbr2_assets.py --kind audio --bundle sounds/se/btl_common --limit 0

    # Localisation text/MonoBehaviours (small)
    python3 tools/extract_sbr2_assets.py --kind text --bundle locale.en-us

Requires: UnityPy, Pillow, cryptography (or pycryptodome).
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import tempfile
from pathlib import Path

import UnityPy
from PIL import Image

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import sbr2_pack  # noqa: E402

DEFAULT_OUT = Path.home() / "sbr2-assets"


def safe(name: str) -> str:
    name = re.sub(r"[^A-Za-z0-9._-]+", "_", name).strip("._")
    return name or "unnamed"


def export_texture(obj, out_dir: Path) -> bool:
    data = obj.read()
    image = data.image
    if image is None:
        return False
    out_dir.mkdir(parents=True, exist_ok=True)
    dest = out_dir / f"{safe(data.m_Name)}.png"
    image.save(dest)
    return True


def export_audio(obj, out_dir: Path) -> int:
    data = obj.read()
    samples = data.samples
    if not isinstance(samples, dict):
        return 0
    out_dir.mkdir(parents=True, exist_ok=True)
    count = 0
    for name, wav in samples.items():
        dest = out_dir / safe(name)
        if dest.suffix.lower() != ".wav":
            dest = dest.with_suffix(".wav")
        dest.write_bytes(wav)
        count += 1
    return count


def export_text(obj, out_dir: Path) -> bool:
    data = obj.read()
    typetree = obj.read_typetree()
    out_dir.mkdir(parents=True, exist_ok=True)
    if obj.type.name == "TextAsset":
        script = getattr(data, "m_Script", b"")
        if isinstance(script, str):
            script = script.encode("utf-8", "replace")
        dest = out_dir / f"{safe(data.m_Name)}.txt"
        dest.write_bytes(script)
        return True
    # MonoBehaviour: dump the serialized fields as JSON.  This is how the
    # localisation/subtitle tables are stored.
    dest = out_dir / f"{safe(data.m_Name)}.json"
    dest.write_text(json.dumps(typetree, indent=2, ensure_ascii=False), encoding="utf-8")
    return True


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--game", type=Path, default=sbr2_pack.DEFAULT_GAME)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--kind", choices=["texture", "sprite", "audio", "text", "all"], default="texture")
    parser.add_argument("--bundle", default="", help="only bundles whose relative path contains this")
    parser.add_argument("--limit", type=int, default=200, help="max assets per kind (0 = unlimited)")
    parser.add_argument("--manifest", action="store_true", help="include the plain Win64 manifest bundle")
    args = parser.parse_args()

    root = args.game / sbr2_pack.BUNDLE_SUBDIR
    if not root.is_dir():
        print(f"bundle directory not found: {root}", file=sys.stderr)
        return 1

    wanted = {"texture", "sprite", "audio", "text"} if args.kind == "all" else {args.kind}
    counts = {k: 0 for k in wanted}
    bundles = 0

    with tempfile.TemporaryDirectory(prefix="sbr2-extract-") as tmp:
        for dirpath, _dirs, files in os.walk(root):
            for name in sorted(files):
                path = Path(dirpath) / name
                rel = str(path.relative_to(root))
                if rel == "Win64" and not args.manifest:
                    continue
                if args.bundle and args.bundle not in rel:
                    continue
                if args.limit and all(counts[k] >= args.limit for k in wanted):
                    break
                bundles += 1
                try:
                    data = sbr2_pack.decrypt_bytes(path.read_bytes())
                    tmp_file = Path(tmp) / "bundle.unity3d"
                    tmp_file.write_bytes(data)
                    env = UnityPy.load(str(tmp_file))
                except Exception as exc:  # noqa: BLE001
                    print(f"skip {rel}: {type(exc).__name__}: {exc}", file=sys.stderr)
                    continue

                for obj in env.objects:
                    typ = obj.type.name
                    kind = None
                    if typ in ("Texture2D", "Sprite"):
                        kind = "texture" if typ == "Texture2D" else "sprite"
                    elif typ == "AudioClip":
                        kind = "audio"
                    elif typ in ("TextAsset", "MonoBehaviour"):
                        kind = "text"
                    if kind not in wanted:
                        continue
                    if args.limit and counts[kind] >= args.limit:
                        continue

                    out_dir = args.out / kind / Path(rel).parent
                    try:
                        if kind in ("texture", "sprite"):
                            ok = export_texture(obj, out_dir)
                            counts[kind] += int(ok)
                        elif kind == "audio":
                            counts[kind] += export_audio(obj, out_dir)
                        else:
                            ok = export_text(obj, out_dir)
                            counts[kind] += int(ok)
                    except Exception as exc:  # noqa: BLE001
                        print(f"  asset fail {rel}/{typ}: {type(exc).__name__}: {exc}", file=sys.stderr)

                print(f"{rel}: {', '.join(f'{k}={counts[k]}' for k in sorted(counts))}")

    print(f"done: bundles={bundles} " + ", ".join(f"{k}={counts[k]}" for k in sorted(counts)))
    print(f"output: {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
