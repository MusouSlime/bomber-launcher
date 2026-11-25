#!/usr/bin/env python3
"""OG Mode stage generator.

The NES stage is a long horizontal field: 29x11 playable tiles inside a
one-tile hard border (31x13 total). The hard skeleton is a 14x5 pillar
lattice; soft blocks, enemies, the exit and the pickup are randomized.

This tool is the layout/manifest side of OG Mode. It does not yet write SBR2
MapData bundles; it produces a deterministic layout and a JSON manifest that
the SBR2 side can consume.

Power-up table provenance:
    Bomberman (USA).nes, PRG offset 0xC04 (CPU $CC03,X, 1-indexed by stage).
    The 50 bytes are the per-stage NES power-up IDs.
"""

from __future__ import annotations

import argparse
import json
import random
from dataclasses import dataclass, asdict
from pathlib import Path

# Field: 29x11 interior + 1-tile border
W, H = 31, 13
X0, X1 = 1, 29  # inclusive interior
Z0, Z1 = 1, 11
START = (1, 10)

# NES power-up IDs, 1-indexed by stage. Extracted from the USA ROM.
# 1=Bomb Up, 2=Fire Up, 3=Speed Up, 4=Remote, 5=Wall Pass,
# 6=Bomb Pass, 7=Flame Pass, 8=Invincible.
NES_POWERUPS = [
    2, 1, 5, 3, 1, 1, 2, 5, 6, 4,
    1, 1, 5, 6, 2, 4, 1, 6, 1, 5,
    6, 5, 1, 5, 6, 8, 2, 1, 5, 7,
    4, 1, 5, 8, 6, 7, 5, 2, 4, 8,
    5, 4, 6, 5, 8, 4, 6, 5, 7, 8,
]

POWERUP_NAMES = {
    1: "bomb_up",
    2: "fire_up",
    3: "speed_up",
    4: "remote",
    5: "wall_pass",
    6: "bomb_pass",
    7: "flame_pass",
    8: "invincible",
}

ENEMY_ORDER = ["ballom", "onil", "dahl", "minvo", "doria", "ovape", "pass", "pontan"]


class NesRng:
    """The NES Bomberman RNG from TASVideos (PermutateRandomCtrlVars)."""

    def __init__(self, seed: int):
        # Four bytes of state; carry starts clear.
        self.r = [
            (seed >> 24) & 0xFF,
            (seed >> 16) & 0xFF,
            (seed >> 8) & 0xFF,
            seed & 0xFF,
        ]
        self.carry = 0

    @staticmethod
    def _rol(a: int, carry: int):
        c = (a >> 7) & 1
        return ((a << 1) | carry) & 0xFF, c

    def next(self) -> int:
        r = self.r
        a, c = self._rol(r[0], self.carry)
        a, c = self._rol(a, c)
        a ^= 0x41
        a, c = self._rol(a, c)
        a, c = self._rol(a, c)
        a ^= 0x93

        t = a + r[1] + c
        a, c = t & 0xFF, (t >> 8) & 1
        r[0] = a

        a, c = self._rol(a, c)
        a, c = self._rol(a, c)
        a ^= 0x12
        a, c = self._rol(a, c)
        a, c = self._rol(a, c)

        t = a + r[2] + c
        a, c = t & 0xFF, (t >> 8) & 1
        r[1] = a

        t = a + r[0] + c
        a, c = t & 0xFF, (t >> 8) & 1

        r[2] = (r[2] + 1) & 0xFF
        if r[2] == 0:
            r[3] = (r[3] + 0x1D) & 0xFF
            # ADC #$1D carry is the carry into the final EOR.
            c = 1 if (r[3] + 0x1D) > 0xFF else 0

        self.carry = c
        return a ^ r[3]

    def below(self, n: int) -> int:
        """Rejection-sample a value in [0, n)."""
        if n <= 0:
            raise ValueError(n)
        # Avoid modulo bias; n is small.
        limit = 256 - (256 % n)
        while True:
            v = self.next()
            if v < limit:
                return v % n


@dataclass
class Stage:
    id: int
    seed: int
    width: int
    height: int
    item: str
    time_limit: int
    soft_blocks: int
    exit_cell: list[int]
    pickup_cell: list[int]
    enemies: list[dict]
    grid: list[str]

    def to_json(self) -> dict:
        d = asdict(self)
        d.pop("grid")
        return d


def enemy_count(stage: int) -> int:
    if stage <= 5:
        return 4
    if stage <= 10:
        return 5
    if stage <= 20:
        return 6
    if stage <= 30:
        return 7
    if stage <= 40:
        return 8
    if stage <= 48:
        return 9
    return 10


def enemy_pool(stage: int) -> list[str]:
    pool = ["ballom"]
    if stage >= 3:
        pool.append("onil")
    if stage >= 6:
        pool.append("dahl")
    if stage >= 11:
        pool.append("minvo")
    if stage >= 21:
        pool.append("doria")
    if stage >= 26:
        pool.append("ovape")
    if stage >= 31:
        pool.append("pass")
    if stage >= 49:
        pool.append("pontan")
    return pool


def soft_count(stage: int) -> int:
    # 55 at stage 1, ~128 at stage 50.
    return min(140, 55 + round((stage - 1) * 1.5))


def time_limit(stage: int) -> int:
    if stage >= 49:
        return 120
    if stage >= 41:
        return 135
    if stage >= 21:
        return 150
    return 180


def base_grid() -> list[list[str]]:
    g = [["." for _ in range(W)] for _ in range(H)]
    for x in range(W):
        g[0][x] = "#"
        g[H - 1][x] = "#"
    for z in range(H):
        g[z][0] = "#"
        g[z][W - 1] = "#"

    # 14x5 hard pillar lattice at even coordinates.
    for z in range(Z0 + 1, Z1, 2):
        for x in range(X0 + 1, X1, 2):
            g[z][x] = "#"

    # Clear the 3x3 start pocket (bottom-left).
    sx, sz = START
    for z in range(sz - 1, sz + 2):
        for x in range(sx - 1, sx + 2):
            if X0 <= x <= X1 and Z0 <= z <= Z1:
                g[z][x] = "."
    g[sz][sx] = "S"
    return g


def manhattan(a: tuple[int, int], b: tuple[int, int]) -> int:
    return abs(a[0] - b[0]) + abs(a[1] - b[1])


def generate(stage: int, run_seed: int = 0) -> Stage:
    # Stable per stage; change run_seed to reroll.
    seed = (run_seed * 0x9E3779B1 + stage * 0x85EBCA6B + 0xC2B2AE35) & 0xFFFFFFFF
    rng = NesRng(seed)
    g = base_grid()

    free = [
        (x, z)
        for z in range(Z0, Z1 + 1)
        for x in range(X0, X1 + 1)
        if g[z][x] == "." and manhattan((x, z), START) >= 2
    ]

    # Shuffle-free random selection using the NES RNG.
    def pick(cells: list[tuple[int, int]]) -> tuple[int, int]:
        return cells[rng.below(len(cells))]

    # Soft blocks.
    n_soft = min(soft_count(stage), len(free) - 12)
    softs: set[tuple[int, int]] = set()
    while len(softs) < n_soft:
        c = pick(free)
        if c not in softs:
            softs.add(c)
    for x, z in softs:
        g[z][x] = "%"

    # Enemy spawn cells: open floor, away from the start.
    floor = [
        (x, z)
        for z in range(Z0, Z1 + 1)
        for x in range(X0, X1 + 1)
        if g[z][x] == "." and manhattan((x, z), START) >= 7
    ]
    if len(floor) < enemy_count(stage):
        # Relax the distance, then open soft blocks as a last resort.
        floor = [
            (x, z)
            for z in range(Z0, Z1 + 1)
            for x in range(X0, X1 + 1)
            if g[z][x] == "." and (x, z) != START
        ]
    rng2 = NesRng((seed ^ 0xA5A5A5A5) & 0xFFFFFFFF)
    rng2.r, rng2.carry = rng.r[:], rng.carry
    available = floor[:]
    enemies: list[dict] = []
    pool = enemy_pool(stage)
    n_enemies = enemy_count(stage)
    # One main type per stage (NES-style), plus occasional variety later.
    # Pontan is a punctuation enemy, not the stage's main type.
    main_pool = [p for p in pool if p != "pontan"]
    main_type = main_pool[min(len(main_pool) - 1, (stage - 1) // 7)]
    pontans = 1 if stage == 49 else (2 if stage >= 50 else 0)
    for i in range(n_enemies):
        if not available:
            break
        c = available[rng2.below(len(available))]
        available.remove(c)
        if i < pontans:
            etype = "pontan"
        else:
            etype = main_type
            if stage >= 6 and i > pontans and rng2.below(100) < 25:
                etype = main_pool[rng2.below(len(main_pool))]
        enemies.append({"type": etype, "x": c[0], "z": c[1]})
        g[c[1]][c[0]] = "E"

    # Hidden exit: under a soft block, far from start.
    far_softs = sorted(
        (c for c in softs if manhattan(c, START) >= 9),
        key=lambda c: (-manhattan(c, START), c),
    )
    if not far_softs:
        far_softs = sorted(softs, key=lambda c: -manhattan(c, START))
    exit_cell = far_softs[0]
    g[exit_cell[1]][exit_cell[0]] = "x"

    # Hidden pickup: another soft block, separated from the exit.
    remaining = [c for c in softs if c != exit_cell]
    separated = [c for c in remaining if manhattan(c, exit_cell) >= 6]
    pool2 = separated or [c for c in remaining if manhattan(c, exit_cell) >= 3] or remaining
    pool2.sort(key=lambda c: (-manhattan(c, START), c))
    pickup_cell = pool2[0]
    g[pickup_cell[1]][pickup_cell[0]] = "p"

    item = POWERUP_NAMES[NES_POWERUPS[stage - 1]]

    return Stage(
        id=stage,
        seed=seed,
        width=W,
        height=H,
        item=item,
        time_limit=time_limit(stage),
        soft_blocks=n_soft,
        exit_cell=[exit_cell[0], exit_cell[1]],
        pickup_cell=[pickup_cell[0], pickup_cell[1]],
        enemies=enemies,
        grid=["".join(row) for row in g],
    )


def show(stage: Stage) -> None:
    print(f"OG Mode stage {stage.id}  seed={stage.seed:08x}  item={stage.item}  "
          f"soft={stage.soft_blocks}  enemies={len(stage.enemies)}")
    print("\n".join(stage.grid))
    print("legend: # hard  % soft  . floor  S start  E enemy  x exit  p pickup")


def main() -> None:
    ap = argparse.ArgumentParser(description="Generate OG Mode layouts")
    ap.add_argument("--stage", type=int, default=1)
    ap.add_argument("--all", action="store_true", help="emit all 50 manifests")
    ap.add_argument("--json", type=Path, help="write JSON manifest")
    ap.add_argument("--seed", type=int, default=0, help="reroll seed")
    args = ap.parse_args()

    if args.all:
        stages = [generate(i, args.seed) for i in range(1, 51)]
        data = {"mode": "og", "version": 1, "stages": [s.to_json() for s in stages]}
        text = json.dumps(data, indent=2)
        if args.json:
            args.json.write_text(text + "\n")
            print(f"wrote {args.json} ({len(stages)} stages)")
        else:
            print(text)
        return

    if not 1 <= args.stage <= 50:
        raise SystemExit("stage must be 1..50")
    show(generate(args.stage, args.seed))


if __name__ == "__main__":
    main()
