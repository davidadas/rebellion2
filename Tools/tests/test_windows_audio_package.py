import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile


SPEC = importlib.util.spec_from_file_location(
    "windows_audio_package", Path(__file__).parents[1] / "WindowsAudio" / "Package.py"
)
packager = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(packager)


class WindowsAudioPackageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / "checkout with spaces"
        self.source.mkdir()
        self.files = {
            "Run.cmd": b"@echo off\npause\n",
            "Run.ps1": b"Write-Host 'Audio check'\n",
            "WindowsAudioProbe.cs": b"public static class Probe {}\n",
            "Read me.txt": b"Double-click Run.cmd.\n",
        }
        for name, content in self.files.items():
            (self.source / name).write_bytes(content)

    def test_package_excludes_reports_and_unrelated_files(self):
        (self.source / "audio-format-report.txt").write_text("Private diagnostic data")
        (self.source / "Player.log").write_text("Private game log")
        (self.source / "unrelated.asset").write_text("Unrelated game content")
        destination = self.root / "output with spaces" / "audio-check.zip"

        packager.package(self.source, destination)

        with zipfile.ZipFile(destination) as archive:
            self.assertEqual(set(archive.namelist()), set(self.files))

    def test_package_preserves_collector_and_normalizes_batch_line_endings(self):
        destination = self.root / "audio-check.zip"

        packager.package(self.source, destination)

        with zipfile.ZipFile(destination) as archive:
            self.assertEqual(
                archive.read("WindowsAudioProbe.cs"), self.files["WindowsAudioProbe.cs"]
            )
            self.assertEqual(archive.read("Run.ps1"), self.files["Run.ps1"])
            self.assertEqual(archive.read("Run.cmd"), b"@echo off\r\npause\r\n")

    def test_package_missing_input_does_not_replace_existing_archive(self):
        destination = self.root / "audio-check.zip"
        destination.write_bytes(b"Existing archive")
        (self.source / "WindowsAudioProbe.cs").unlink()

        with self.assertRaises(FileNotFoundError):
            packager.package(self.source, destination)

        self.assertEqual(destination.read_bytes(), b"Existing archive")


if __name__ == "__main__":
    unittest.main()
