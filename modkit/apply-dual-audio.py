#!/usr/bin/env python3
"""Apply the dual-audio patch (JP voices, current text language).

Portable CLI fallback for the launcher. Override paths with:
    SBR_GAME=/path/to/game SBR_MODKIT=/path/to/backups
"""
import os
import shutil
from pathlib import Path

HOME = Path.home()
GAME = Path(os.environ.get(
    "SBR_GAME", HOME / ".local/share/Steam/steamapps/common/"
    "Super Bomberman R"))
MODKIT = Path(os.environ.get("SBR_MODKIT", HOME / "Projects" / "sbr-modkit"))
DLL = GAME / "SuperBombermanR_Data/Managed/Assembly-CSharp.dll"
ORIG = MODKIT / "backups/Assembly-CSharp.dll.orig"
PAT_E = bytes.fromhex("05 5f 00 45 00 00")
PAT_J = bytes.fromhex("05 5f 00 4a 00 00")


def main():
    if not DLL.exists():
        raise SystemExit(f"game DLL not found: {DLL}")
    data = bytearray(DLL.read_bytes())
    if data.count(PAT_E) == 0 and data.count(PAT_J) == 2:
        print("already patched")
        return
    if data.count(PAT_E) != 1:
        raise SystemExit("unrecognized Assembly-CSharp.dll")
    ORIG.parent.mkdir(parents=True, exist_ok=True)
    if not ORIG.exists():
        shutil.copy2(DLL, ORIG)
    i = data.find(PAT_E)
    data[i + 3] = 0x4A
    DLL.write_bytes(data)
    print("dual audio enabled (original backed up)")


if __name__ == "__main__":
    main()
