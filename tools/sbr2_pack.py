#!/usr/bin/env python3
"""Decrypt/encrypt Super Bomberman R 2 UnityFS bundles.

SBR 2 stores its payload bundles under:

    SuperBombermanR2_Data/StreamingAssets/InAppAssets/Win64

Every payload bundle is a normal UnityFS bundle whose **first 0x100 bytes**
are AES-256-ECB encrypted.  Everything after 0x100 is plain (often
uncompressed) Unity data.  The plain `Win64` file in that directory is the
AssetBundleManifest and is *not* encrypted.

The AES key is compiled into the IL2CPP class `ResourceCryptoKey` and is
specific to the game build.  This is the key for the current Steam build
(app 1913740, content Version 35199):

    cb bc a0 47 71 15 dc 4d 5b 9e 93 bb 57 b9 37 b9
    2c b9 5f c9 11 4a f3 06 ce b4 08 5b 03 df 51 85

The game decrypts exactly 0x100 bytes (`ResourceDecrypter.decrypt` ->
`NonAllocAes.decryptByECB`).  A few bundles have block-info tables that run
past 0x100; those tail bytes are plain and are left alone.

Examples:
    python3 tools/sbr2_pack.py info common_battle
    python3 tools/sbr2_pack.py decrypt common_battle /tmp/common_battle.unity3d
    python3 tools/sbr2_pack.py decrypt-all /tmp/sbr2-bundles
    python3 tools/sbr2_pack.py encrypt /tmp/edited/common_battle common_battle

Requires: cryptography (or pycryptodome).
"""

from __future__ import annotations

import argparse
import os
import shutil
import struct
import sys
from pathlib import Path

HEADER_SIZE = 0x100
KEY = bytes.fromhex(
    "cbbca0477115dc4d5b9e93bb57b937b9"
    "2cb95fc9114af306ceb4085b03df5185"
)

DEFAULT_GAME = (
    Path.home() / ".local/share/Steam/steamapps/common/SUPER BOMBERMAN R2"
)
BUNDLE_SUBDIR = Path("SuperBombermanR2_Data/StreamingAssets/InAppAssets/Win64")

try:  # the launcher venv already depends on cryptography
    from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes

    def _aes_ecb(data: bytes, encrypt: bool) -> bytes:
        ctx = Cipher(algorithms.AES(KEY), modes.ECB())
        op = ctx.encryptor() if encrypt else ctx.decryptor()
        return op.update(data) + op.finalize()

except ImportError:  # pragma: no cover - fallback
    from Cryptodome.Cipher import AES

    def _aes_ecb(data: bytes, encrypt: bool) -> bytes:
        cipher = AES.new(KEY, AES.MODE_ECB)
        return cipher.encrypt(data) if encrypt else cipher.decrypt(data)


def is_plain_bundle(data: bytes) -> bool:
    """True for a normal UnityFS bundle (the manifest, or a decrypted file)."""
    return data[:7] == b"UnityFS"


def looks_encrypted(data: bytes) -> bool:
    """True if the first 0x100 bytes decrypt to a UnityFS header."""
    if len(data) < HEADER_SIZE:
        return False
    return is_plain_bundle(_aes_ecb(bytes(data[:HEADER_SIZE]), encrypt=False))


def decrypt_bytes(data: bytes) -> bytes:
    """Return a copy with the encrypted 0x100-byte header decrypted."""
    if len(data) < HEADER_SIZE or is_plain_bundle(data):
        return bytes(data)
    return _aes_ecb(bytes(data[:HEADER_SIZE]), encrypt=False) + bytes(data[HEADER_SIZE:])


def encrypt_bytes(data: bytes) -> bytes:
    """Return a copy with the first 0x100 bytes encrypted.

    This is what the game expects on disk.  It is deliberately unconditional
    (except for short files): if you edited a bundle, its first 0x100 bytes
    must be encrypted even if they now contain serialized metadata.
    """
    if len(data) < HEADER_SIZE:
        return bytes(data)
    return _aes_ecb(bytes(data[:HEADER_SIZE]), encrypt=True) + bytes(data[HEADER_SIZE:])


def parse_header(data: bytes) -> dict | None:
    """Parse the plain UnityFS header.  Returns None for non-bundles."""
    if len(data) < 0x30 or not is_plain_bundle(data):
        return None

    def cstr(pos: int) -> tuple[str, int]:
        end = data.index(0, pos)
        return data[pos:end].decode("utf-8", "replace"), end + 1

    pos = 8
    version = struct.unpack_from(">I", data, pos)[0]
    pos += 4
    unity_version, pos = cstr(pos)
    unity_revision, pos = cstr(pos)
    # The 8-byte size starts immediately after the revision string; it is not
    # 4-byte aligned (the two leading zero bytes are the high half of the size).
    total_size = struct.unpack_from(">q", data, pos)[0]
    pos += 8
    csize, usize, flags = struct.unpack_from(">III", data, pos)
    return {
        "version": version,
        "unity_version": unity_version,
        "unity_revision": unity_revision,
        "size": total_size,
        "compressed_blocks_info_size": csize,
        "uncompressed_blocks_info_size": usize,
        "flags": flags,
        "encrypted_header_bytes": HEADER_SIZE,
    }


def _iter_files(src: Path):
    if src.is_file():
        yield src
        return
    for root, _dirs, files in os.walk(src):
        for name in sorted(files):
            yield Path(root) / name


def _convert(src: Path, dst: Path, encrypt: bool) -> str:
    data = src.read_bytes()
    if encrypt:
        if is_plain_bundle(data):
            out = encrypt_bytes(data)
            action = "encrypted"
        else:
            action = "skipped (already encrypted?)"
            out = data
    else:
        if is_plain_bundle(data):
            action = "skipped (plain)"
            out = data
        else:
            out = decrypt_bytes(data)
            action = "decrypted"

    dst.parent.mkdir(parents=True, exist_ok=True)
    dst.write_bytes(out)
    return action


def cmd_info(args: argparse.Namespace) -> int:
    data = Path(args.file).read_bytes()
    if not is_plain_bundle(data) and looks_encrypted(data):
        data = decrypt_bytes(data)
    try:
        header = parse_header(data)
    except Exception as exc:  # malformed header
        print(f"{args.file}: cannot parse header: {type(exc).__name__}: {exc}")
        return 1
    if header is None:
        print(f"{args.file}: not a UnityFS bundle")
        return 1
    for key, value in header.items():
        if isinstance(value, int) and key != "flags":
            print(f"{key:28} {value} (0x{value:x})")
        elif key == "flags":
            print(f"{key:28} 0x{value:x}")
        else:
            print(f"{key:28} {value}")
    return 0


def cmd_convert(args: argparse.Namespace) -> int:
    src = Path(args.src)
    dst = Path(args.dst)

    if src.is_file():
        action = _convert(src, dst, args.encrypt)
        print(f"{action}: {src} -> {dst}")
        return 0

    count = 0
    for path in _iter_files(src):
        rel = path.relative_to(src)
        if args.encrypt and path.name == "Win64":
            # The manifest bundle is intentionally plain.  Encrypting a whole
            # decrypted tree must not touch it.
            continue
        target = dst / rel
        action = _convert(path, target, args.encrypt)
        if action.startswith("skipped"):
            continue
        count += 1
    print(f"{'encrypted' if args.encrypt else 'decrypted'} {count} file(s) -> {dst}")
    return 0


def cmd_encrypt_in_place(args: argparse.Namespace) -> int:
    path = Path(args.file)
    data = path.read_bytes()
    if not is_plain_bundle(data):
        print(f"{path}: already encrypted / not a plain bundle")
        return 1
    backup = path.with_suffix(path.suffix + ".orig")
    if not backup.exists():
        shutil.copy2(path, backup)
        print(f"backup: {backup}")
    path.write_bytes(encrypt_bytes(data))
    print(f"encrypted: {path}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--game", type=Path, default=DEFAULT_GAME, help="SBR2 install directory")
    sub = parser.add_subparsers(dest="cmd", required=True)

    p_info = sub.add_parser("info", help="show a bundle header (decrypts in memory)")
    p_info.add_argument("file")
    p_info.set_defaults(func=cmd_info)

    p_dec = sub.add_parser("decrypt", help="decrypt a file or a tree of bundles")
    p_dec.add_argument("src")
    p_dec.add_argument("dst")
    p_dec.set_defaults(func=cmd_convert, encrypt=False)

    p_enc = sub.add_parser("encrypt", help="encrypt a file or a tree of plain bundles")
    p_enc.add_argument("src")
    p_enc.add_argument("dst")
    p_enc.set_defaults(func=cmd_convert, encrypt=True)

    p_inplace = sub.add_parser("encrypt-in-place", help="encrypt one edited bundle, keeping a .orig backup")
    p_inplace.add_argument("file")
    p_inplace.set_defaults(func=cmd_encrypt_in_place)

    p_all = sub.add_parser("decrypt-all", help="decrypt the whole Win64 payload tree")
    p_all.add_argument("dst")
    p_all.add_argument("--game", type=Path, default=argparse.SUPPRESS)
    p_all.set_defaults(func=cmd_decrypt_all)
    return parser


def cmd_decrypt_all(args: argparse.Namespace) -> int:
    src = Path(args.game) / BUNDLE_SUBDIR
    if not src.is_dir():
        print(f"bundle directory not found: {src}", file=sys.stderr)
        return 1
    return cmd_convert(argparse.Namespace(src=str(src), dst=args.dst, encrypt=False))


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
