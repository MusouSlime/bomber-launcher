# Assets

The game's artwork is **not** committed here (© Konami). Rebuild it from your
own copy of Super Bomberman R:

```sh
python3 -m venv .venv
.venv/bin/pip install UnityPy pillow cryptography
.venv/bin/python tools/extract_ui_assets.py
```

`intro_blank.mp4` is the only committed media file: a 1-second black 1080p30
clip generated for the intro-skip mod.

Without the extracted files the launcher still runs, but the banner, avatars
and section bars will be blank.
