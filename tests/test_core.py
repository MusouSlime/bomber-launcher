import tempfile
import unittest
from pathlib import Path
from unittest import mock

import bomber_launcher as bl


class SaveTests(unittest.TestCase):
    def test_roundtrip(self):
        entries = {"alpha": b"hello" * 7, "/systemData": bytes(1352)}
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "savedata.bin"
            bl.SaveFile.save(p, entries)
            loaded = bl.SaveFile.load(p)
            self.assertEqual(list(loaded), list(entries))
            self.assertEqual(loaded["alpha"], entries["alpha"])
            self.assertEqual(len(loaded["/systemData"]), 1352)

    def test_language(self):
        blob = bytearray(1352)
        blob[1132] = 3
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "savedata.bin"
            bl.SaveFile.save(p, {"/systemData": bytes(blob)})
            self.assertEqual(bl.SaveFile.get_language(p), 3)
            with mock.patch.object(bl, "SAVE_BACKUPS", Path(d)):
                bl.SaveFile.set_language(p, 9)
            self.assertEqual(bl.SaveFile.get_language(p), 9)


class DllPatchTests(unittest.TestCase):
    def test_patch_and_restore(self):
        pat_e = bytes.fromhex("05 5f 00 45 00 00")
        pat_j = bytes.fromhex("05 5f 00 4a 00 00")
        with tempfile.TemporaryDirectory() as d:
            game = Path(d) / "game"
            dll = game / bl.DLL_REL
            dll.parent.mkdir(parents=True)
            dll.write_bytes(b"prefix" + pat_e + b"mid" + pat_j + b"suffix")
            mods = bl.Mods(game)
            with mock.patch.object(bl, "BACKUPS", Path(d) / "backup"), \
                 mock.patch.object(bl, "game_running", return_value=False):
                self.assertFalse(mods.dual_audio_state())
                mods.set_dual_audio(True)
                self.assertTrue(mods.dual_audio_state())
                mods.set_dual_audio(False)
                self.assertFalse(mods.dual_audio_state())
                self.assertEqual(dll.read_bytes(),
                                 b"prefix" + pat_e + b"mid" + pat_j + b"suffix")


class PluginConfigTests(unittest.TestCase):
    def test_read_write(self):
        with tempfile.TemporaryDirectory() as d:
            game = Path(d)
            cfg = game / bl.PLUGIN_CFG_REL
            cfg.parent.mkdir(parents=True)
            mods = bl.Mods(game)
            mods.set_plugin_value("Movies", "SkipJoystickButton", "7")
            self.assertEqual(
                mods.plugin_config()["Movies"]["SkipJoystickButton"], "7")
            mods.set_plugin_value("Results", "FastResults", "true")
            self.assertEqual(
                mods.plugin_config()["Results"]["FastResults"], "true")

    def test_defaults(self):
        with tempfile.TemporaryDirectory() as d:
            mods = bl.Mods(Path(d))
            cfg = mods.plugin_config()
            self.assertEqual(cfg["Movies"]["SkipJoystickButton"], "0")
            self.assertEqual(cfg["Movies"]["AutoSkipLogos"], "true")


class IntroStateTests(unittest.TestCase):
    def test_states(self):
        with tempfile.TemporaryDirectory() as d:
            game = Path(d)
            bin_dir = game / bl.BIN_REL
            bin_dir.mkdir(parents=True)
            mods = bl.Mods(game)
            for name, size in (("bom_00_intro_a", 100),
                               ("bom_00_intro_b", 100)):
                (bin_dir / name).write_bytes(b"0" * size)
            self.assertTrue(mods.intro_state())
            for name in ("bom_00_intro_a", "bom_00_intro_b"):
                (bin_dir / name).write_bytes(b"0" * (11 * 1024 * 1024))
            self.assertFalse(mods.intro_state())


if __name__ == "__main__":
    unittest.main()
