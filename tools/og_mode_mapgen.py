#!/usr/bin/env python3
"""Build OG Mode SBR2 MapData bundles from og_mode_gen manifests.

This writes *new* bundles to a build directory. It never touches the game
install. The bundle is a modified SBR2 versus map (vs001) with a 31x13 OG Mode
grid, the repeated NES hard skeleton, and the per-stage soft blocks.

Usage:
    python3 tools/og_mode_mapgen.py --all
    python3 tools/og_mode_mapgen.py --stage 1 --verify
"""

from __future__ import annotations

import argparse
import copy
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import og_mode_gen  # noqa: E402
import sbr2_pack  # noqa: E402
import UnityPy  # noqa: E402

DEFAULT_GAME = Path.home() / ".local/share/Steam/steamapps/common/SUPER BOMBERMAN R2"
TEMPLATE = Path(
    "SuperBombermanR2_Data/StreamingAssets/InAppAssets/Win64/map/data/vs/map_data_vs001"
)
OUT_DIR = Path("build/og_mode/map/data/og")

# EnemyInfoDataTable ids (see docs/SBR2_NES_MODE.md).
ENEMY_IDS = {
    "ballom": 0,   # WANDERING1
    "onil": 3,     # RUSH1
    "dahl": 4,     # RUSH2
    "minvo": 5,    # RUSH3
    "doria": 2,    # WANDERING3
    "ovape": 1,    # WANDERING2
    "pass": 5,     # RUSH3 placeholder
    "pontan": 12,  # BOMB_EAT2 placeholder
}


def empty_item() -> dict:
    return {
        "itemAppearRate": 0.0,
        "itemType": -1,
        "randomAssignableItemTypes": {"_bits": 0},
        "randomAssignableItemTypesHard": {"_bits": 0},
        "randomAssignableItemTypesModerately": {"_bits": 0},
        "randomAssignableItemTypesEasy": {"_bits": 0},
    }


def load_template() -> bytes:
    p = DEFAULT_GAME / TEMPLATE
    if not p.exists():
        raise SystemExit(f"template map not found: {p}")
    return sbr2_pack.decrypt_bytes(p.read_bytes())


def templates_by_stats(tt: dict) -> dict[int, dict]:
    out: dict[int, dict] = {}
    for b in tt["_blockDeployParams"]:
        out.setdefault(b["blockStatisticsIndex"], b)
    return out


def make_block(base: dict, stats_index: int, x: int, z: int, y: int) -> dict:
    b = copy.deepcopy(base)
    b["blockStatisticsIndex"] = stats_index
    b["mapIdx"] = {"x": x, "y": y, "z": z}
    b["direction"] = 0
    b["appearRate"] = 1.0
    b["supplyRate"] = 0.0
    b["itemParamWhenDeploy"] = empty_item()
    b["itemParamWhenSupply"] = empty_item()
    return b


def build_stage(stage: dict, decrypted: bytes) -> bytes:
    env = UnityPy.load(decrypted)
    obj = None
    for o in env.objects:
        if o.type.name == "MonoBehaviour":
            t = o.read_typetree()
            if t.get("m_Name", "").startswith("MapData_"):
                obj = o
                tt = t
                break
    if obj is None:
        raise RuntimeError("MapData not found in template")

    w, h = stage["width"], stage["height"]
    grid = stage["grid"]
    blocks = templates_by_stats(tt)
    base_ground = blocks[0]
    base_border = blocks[1]
    base_soft = blocks[3]
    base_pillar = blocks[4]

    deploy: list[dict] = []
    # Ground at y=0 for every cell.
    for z in range(h):
        for x in range(w):
            deploy.append(make_block(base_ground, 0, x, z, 0))

    n_soft = 0
    n_pillar = 0
    n_border = 0
    for z in range(h):
        for x in range(w):
            ch = grid[z][x]
            if x in (0, w - 1) or z in (0, h - 1):
                deploy.append(make_block(base_border, 1, x, z, 1))
                n_border += 1
            elif ch == "#":
                deploy.append(make_block(base_pillar, 4, x, z, 1))
                n_pillar += 1
            elif ch in "%xp":
                deploy.append(make_block(base_soft, 3, x, z, 1))
                n_soft += 1
            # '.', 'S', 'E' are open floor: no y=1 block.

    tt["m_Name"] = f"MapData_OG{stage['id']:03d}"
    tt["_mapSize"] = {"x": w, "y": 40, "z": h}
    tt["_blockDeployParams"] = deploy

    # Player start is the S cell.
    player = {"mapIdx": {"x": 1, "y": 1, "z": 10}, "direction": 2}
    for z in range(h):
        for x in range(w):
            if grid[z][x] == "S":
                player = {"mapIdx": {"x": x, "y": 1, "z": z}, "direction": 2}
    tt["_playerStartPointParams"] = [player]

    tt["_enemyStartPointParams"] = [
        {
            "mapIdx": {"x": e["x"], "y": 1, "z": e["z"]},
            "direction": 2,
            "id": ENEMY_IDS.get(e["type"], 0),
        }
        for e in stage["enemies"]
    ]

    tt["_itemDeployParams"] = []
    tt["_randomItemParams"] = []
    tt["_areaGateParams"] = []
    tt["_eventDetectionParams"] = []
    tt["_puzzleParams"] = []
    tt["_shutterParams"] = []

    # Keep the template resource id (100) so the versus environment loads.
    for s in tt["_blockStatisticsByBlockTypes"]:
        name = s["blockTypeName"]
        if name == "HARD_05_A":
            s["deployedCount"] = w * h
        elif name == "END_10_A":
            s["deployedCount"] = n_border
        elif name == "HARD_05_C":
            s["deployedCount"] = 0
        elif name == "SOFT_10_A":
            s["deployedCount"] = n_soft
        elif name == "PILLAR_05_A":
            s["deployedCount"] = n_pillar
        elif name == "PRESSURE":
            s["deployedCount"] = 0
    tt["_blockStatistics"] = {
        "dynamicBlockCount": n_soft,
        "itemAppearableOnTopCount": n_soft + (w - 2) * (h - 2) - n_pillar,
        "gimmickCount": 0,
    }
    tt["_miniMapParam"]["fireAppearablePlaceCount"] = n_soft

    obj.save_typetree(tt)
    obj.assets_file.mark_changed()

    # Keep the bundle's internal identity consistent with the OG path.
    sid = stage["id"]
    asset_name = f"map/data/og/map_data_og{sid:03d}"
    container_key = f"assets/externals/datas/map/mapdata/og/mapdata_og{sid:03d}.asset"
    for o in env.objects:
        if o.type.name != "AssetBundle":
            continue
        ab = o.read_typetree()
        ab["m_Name"] = asset_name
        ab["m_AssetBundleName"] = asset_name
        if ab.get("m_Container"):
            key, info = ab["m_Container"][0]
            ab["m_Container"][0] = (container_key, info)
        o.save_typetree(ab)
        o.assets_file.mark_changed()

    bundle = list(env.files.values())[0]
    return bundle.save(packer="none")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--stage", type=int)
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--manifest", type=Path, help="og_mode_stages.json")
    ap.add_argument("--out", type=Path, default=OUT_DIR)
    ap.add_argument("--verify", action="store_true")
    args = ap.parse_args()

    if not args.stage and not args.all:
        raise SystemExit("use --stage N or --all")

    if args.manifest:
        data = json.loads(args.manifest.read_text())
        stages = {s["id"]: s for s in data["stages"]}
    else:
        stages = {i: og_mode_gen.generate(i).to_json() for i in range(1, 51)}

    ids = range(1, 51) if args.all else [args.stage]
    decrypted = load_template()
    args.out.mkdir(parents=True, exist_ok=True)

    for sid in ids:
        stage = stages[sid]
        # og_mode_gen's JSON omits the grid; regenerate if needed.
        if "grid" not in stage:
            stage = og_mode_gen.generate(sid).__dict__
            stage["grid"] = og_mode_gen.generate(sid).grid
        plain = build_stage(stage, decrypted)
        enc = sbr2_pack.encrypt_bytes(plain)
        path = args.out / f"map_data_og{sid:03d}"
        path.write_bytes(enc)
        print(f"{path}  {len(enc)} bytes  soft={stage['soft_blocks']} enemies={len(stage['enemies'])}")

        if args.verify:
            env = UnityPy.load(sbr2_pack.decrypt_bytes(enc))
            for o in env.objects:
                if o.type.name == "MonoBehaviour":
                    tt = o.read_typetree()
                    print("  verify", tt["m_Name"], tt["_mapSize"],
                          "blocks", len(tt["_blockDeployParams"]),
                          "player", tt["_playerStartPointParams"],
                          "enemies", len(tt["_enemyStartPointParams"]))


if __name__ == "__main__":
    main()
