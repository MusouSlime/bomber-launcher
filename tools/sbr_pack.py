#!/usr/bin/env python3
"""Super Bomberman R pack decrypt/encrypt.

The files under StreamingAssets/InAppAssets/steam are AES-128-CBC encrypted
UnityFS asset bundles. The key is derived from the file name, so keep the
original stem when repacking.

    python3 tools/sbr_pack.py dec -i <pack> -o <bundle>
    python3 tools/sbr_pack.py enc -i <bundle> -o <pack>
"""
import argparse
import hashlib
import os
import sys

PW, SALT, ITER = "c4GXA8FT", b"t4ChYLcH", 1103

try:
    from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes

    def _aes(data, key, iv, encrypt):
        c = Cipher(algorithms.AES(key), modes.CBC(iv))
        t = c.encryptor() if encrypt else c.decryptor()
        return t.update(data) + t.finalize()
except ImportError:
    try:
        from Cryptodome.Cipher import AES

        def _aes(data, key, iv, encrypt):
            c = AES.new(key, AES.MODE_CBC, iv)
            return c.encrypt(data) if encrypt else c.decrypt(data)
    except ImportError:
        sys.exit("install cryptography or pycryptodome")


def transform(data, name, encrypt):
    stem = os.path.basename(name).rsplit(".", 1)[0]
    d = hashlib.pbkdf2_hmac("sha1", (stem + PW).encode(), SALT, ITER, 32)
    return _aes(data, d[:16], d[16:32], encrypt)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=("dec", "enc"))
    ap.add_argument("-i", "--input", required=True)
    ap.add_argument("-o", "--output", required=True)
    a = ap.parse_args()
    out = transform(open(a.input, "rb").read(), a.input, a.mode == "enc")
    open(a.output, "wb").write(out)
    print(f"{a.mode}: {a.input} -> {a.output} ({len(out)} bytes)")


if __name__ == "__main__":
    main()
