#!/usr/bin/env python3
"""Apply JSON data-table edits to a Super Bomberman R 2 bundle.

This is a small, deliberately conservative patcher for the ScriptableObject /
MonoBehaviour data tables in `defines/common` (story EXP, enemy stats, NPC AI,
shop prices, stage lists, ...).  It decrypts the bundle, applies the edits with
UnityPy, saves a new bundle, and re-encrypts the 0x100-byte SBR2 header.

Patch format:

    {
      "bundle": "defines/common",
      "edits": [
        {"table": "StoryCampExpTable_World1",
         "path": "_datas[*]._nextLevelNeedExp",
         "op": "scale", "value": 0.6},

        {"table": "StoryCharacterStatusDataTable",
         "path": "_datas[*]._initFirePower",
         "op": "add", "value": 1},

        {"table": "NpcStrongDefData",
         "path": "_moveRateOverDanger",
         "op": "set", "value": 0.35},

        {"table": "BattleModeUIDataTable",
         "path": "_datas",
         "op": "copy", "from": 13,
         "set": {"_mapId": 20001}}
      ]
    }

Supported ops:
  set       replace the value
  add       add a number
  scale     multiply a number
  append    append a value to a list (path must point at the list)
  copy      copy list[index] and append it, with optional field overrides

Path syntax: dotted keys plus [index] or [*] wildcards, e.g.
  _datas[*]._nextLevelNeedExp
  _datas[0]._price

Examples:
  # dry run, show old -> new
  python3 tools/sbr2_data_patch.py --patch patches/sbr2-qol-v1.json --dry-run

  # write a patched bundle next to a folder
  python3 tools/sbr2_data_patch.py --patch patches/sbr2-qol-v1.json \
      --out /var/home/user/.cache/sbr2-patched

  # install into the game (keeps defines/common.orig)
  python3 tools/sbr2_data_patch.py --patch patches/sbr2-qol-v1.json --in-place

Requires: UnityPy, cryptography (or pycryptodome).
"""

from __future__ import annotations

import argparse
import copy
import json
import os
import re
import shutil
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import sbr2_pack  # noqa: E402

import UnityPy  # noqa: E402

TOKEN_RE = re.compile(r"([^.\[\]]+)|\[(\d+|\*)\]")


def parse_path(path: str):
    tokens = []
    for match in TOKEN_RE.finditer(path):
        key, index = match.groups()
        if key is not None:
            tokens.append(key)
        elif index == "*":
            tokens.append("*")
        else:
            tokens.append(int(index))
    return tokens


def _apply_leaf(container, key, op, value, context):
    old = container.get(key) if isinstance(container, dict) else container[key]
    if op == "set":
        new = value
    elif op == "add":
        new = old + value
    elif op == "scale":
        new = old * value
        if isinstance(old, int) and not isinstance(old, bool):
            new = int(round(new))
    else:
        raise ValueError(f"unsupported leaf op: {op}")
    if isinstance(container, dict):
        container[key] = new
    else:
        container[key] = new
    context.append((old, new))


def _walk(container, tokens, op, value, context, full_path):
    token = tokens[0]
    rest = tokens[1:]
    if token == "*":
        if not isinstance(container, list):
            raise TypeError(f"'*' applied to non-list at {full_path}")
        for item in container:
            if rest:
                _walk(item, rest, op, value, context, full_path)
            else:
                raise ValueError("wildcard cannot be the final token for set/add/scale")
        return

    if rest:
        _walk(container[token], rest, op, value, context, full_path)
        return

    if op in ("set", "add", "scale"):
        _apply_leaf(container, token, op, value, context)
        return

    if op == "append":
        target = container[token] if isinstance(container, dict) else container[token]
        if not isinstance(target, list):
            raise TypeError(f"append target is not a list at {full_path}")
        target.append(copy.deepcopy(value))
        context.append((None, value))
        return

    if op == "copy":
        target = container[token] if isinstance(container, dict) else container[token]
        if not isinstance(target, list):
            raise TypeError(f"copy target is not a list at {full_path}")
        item = copy.deepcopy(target[value["from"]])
        item.update(value.get("set", {}))
        target.append(item)
        context.append((None, item))
        return

    raise ValueError(f"unsupported op: {op}")


def find_tables(env):
    tables = {}
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            tree = obj.read_typetree()
        except Exception:
            continue
        name = tree.get("m_Name")
        if name:
            tables[name] = (obj, tree)
    return tables


def apply_patch(patch: dict, bundle_path: Path, out_path: Path, dry_run: bool) -> int:
    data = sbr2_pack.decrypt_bytes(bundle_path.read_bytes())
    with tempfile.TemporaryDirectory(prefix="sbr2-data-") as tmp:
        tmp_bundle = Path(tmp) / "bundle.unity3d"
        tmp_bundle.write_bytes(data)
        env = UnityPy.load(str(tmp_bundle))
        tables = find_tables(env)
        print(f"{bundle_path}: {len(tables)} data tables")

        changed = 0
        for edit in patch.get("edits", []):
            table = edit["table"]
            if table not in tables:
                print(f"  !! missing table: {table}")
                continue
            obj, tree = tables[table]
            context = []
            try:
                _walk(tree, parse_path(edit["path"]), edit["op"], edit.get("value", edit), context, edit["path"])
            except Exception as exc:  # noqa: BLE001
                print(f"  !! {table} {edit['path']}: {type(exc).__name__}: {exc}")
                continue
            for old, new in context[:3]:
                print(f"  {table} {edit['path']}: {old!r} -> {new!r}")
            if len(context) > 3:
                print(f"  ... {len(context) - 3} more change(s)")
            obj.save_typetree(tree)
            changed += 1

        if dry_run:
            print(f"dry run: {changed} edit(s), no file written")
            return 0

        out_dir = Path(tmp) / "out"
        out_dir.mkdir()
        env.save(out_path=str(out_dir))
        saved = [p for p in out_dir.iterdir() if p.is_file() and p.stat().st_size > 0]
        if not saved:
            raise RuntimeError("UnityPy wrote no bundle")
        saved_bundle = max(saved, key=lambda p: p.stat().st_size)
        patched = sbr2_pack.encrypt_bytes(saved_bundle.read_bytes())
        out_path.parent.mkdir(parents=True, exist_ok=True)
        out_path.write_bytes(patched)
        print(f"wrote {out_path} ({len(patched)} bytes, {changed} edit(s))")
        return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--patch", required=True, type=Path)
    parser.add_argument("--game", type=Path, default=sbr2_pack.DEFAULT_GAME)
    parser.add_argument("--bundle", default=None, help="bundle relative path (default: patch['bundle'])")
    parser.add_argument("--out", type=Path, help="output directory (default: dry run)")
    parser.add_argument("--in-place", action="store_true", help="replace the game bundle, keeping .orig")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    patch = json.loads(args.patch.read_text(encoding="utf-8"))
    rel = args.bundle or patch.get("bundle", "defines/common")
    source = args.game / sbr2_pack.BUNDLE_SUBDIR / rel
    if not source.exists():
        print(f"bundle not found: {source}", file=sys.stderr)
        return 1

    if args.in_place and not args.dry_run:
        target = source
        backup = source.with_suffix(source.suffix + ".orig")
        if not backup.exists():
            shutil.copy2(source, backup)
            print(f"backup: {backup}")
    elif args.out:
        target = args.out / Path(rel).name
    else:
        target = source  # only used for dry-run
        args.dry_run = True

    return apply_patch(patch, source, target, args.dry_run)


if __name__ == "__main__":
    raise SystemExit(main())
