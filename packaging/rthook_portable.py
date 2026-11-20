import os
import sys

base = getattr(sys, "_MEIPASS", None)
if base:
    os.environ["GI_TYPELIB_PATH"] = os.path.join(base, "gi_typelibs")
    os.environ["GSETTINGS_SCHEMA_DIR"] = os.path.join(base, "share", "glib-2.0", "schemas")
    os.environ["GDK_PIXBUF_MODULE_FILE"] = os.path.join(base, "lib", "gdk-pixbuf", "loaders.cache")
    share = os.path.join(base, "share")
    os.environ["XDG_DATA_DIRS"] = share + ":" + os.environ.get(
        "XDG_DATA_DIRS", "/usr/local/share:/usr/share")
