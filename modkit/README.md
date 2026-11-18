# Modkit CLI

Small command-line fallbacks for the launcher. The GUI does all of this, but
these are handy after a Steam file verification or for scripting.

Paths can be overridden with `SBR_GAME` and `SBR_MODKIT`.

```sh
python3 modkit/apply-dual-audio.py      # JP voices + current text language
./modkit/restore-dual-audio.sh          # same as Steam verify for the DLL
./modkit/restore-intros.sh              # restore the 4-minute boot movies
./modkit/backup-save.sh                 # timestamped savedata.bin copy
./modkit/play-borderless.sh             # launch 1920x1080 borderless
```

Pack crypto (used by asset mods):

```sh
python3 tools/sbr_pack.py dec -i chara_pack_01 -o chara_pack_01.bundle
python3 tools/sbr_pack.py enc -i chara_pack_01.bundle -o chara_pack_01
```

The encrypted container is an AES-128-CBC UnityFS bundle. Key derivation is
PBKDF2-HMAC-SHA1 (1103 iterations) with password
`<filename-without-extension> + "c4GXA8FT"` and salt `"t4ChYLcH"`; key is the
first 16 derived bytes, IV the next 16. Re-encrypting an untouched bundle is
byte-identical to the original file.
