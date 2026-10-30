import hashlib
import importlib.util
import json
import shutil
from pathlib import Path
import tarfile
import tempfile
import unittest
import zipfile

SPEC = importlib.util.spec_from_file_location(
    "package_desktop", Path(__file__).parents[1] / "package_desktop.py"
)
packager = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(packager)


class PackageDesktopTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / "build"
        self.root.mkdir()

    def write(self, relative, value=b"runtime"):
        target = self.root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(value)
        return target

    def create_build(self, platform="StandaloneWindows64", game_name="rebellion2"):
        suffix, native = packager.PLATFORMS[platform]
        for name in (game_name, "rebellion2-editor"):
            if suffix == ".app":
                app = packager.player_path(self.root, name, platform, game_name)
                self.write(
                    (app / "Contents/MacOS" / name).relative_to(self.root)
                ).chmod(0o755)
            else:
                self.write(name + suffix).chmod(0o755)
            data = packager.player_data(self.root, name, platform, game_name)
            self.write((data / "Managed/GameAssembly.dll").relative_to(self.root))
        editor = packager.player_data(
            self.root, "rebellion2-editor", platform, game_name
        )
        self.write(
            (editor / "Managed" / packager.EDITOR_ASSEMBLY).relative_to(self.root)
        )
        notice_root = editor.parent if suffix == ".app" else editor
        self.write(
            (notice_root / "ThirdPartyNotices/NativeFileDialog.txt").relative_to(
                self.root
            )
        )
        if platform == "StandaloneWindows64":
            binary = bytearray(70)
            binary[:2], binary[60:64], binary[64:70] = (
                b"MZ",
                (64).to_bytes(4, "little"),
                b"PE\0\0\x64\x86",
            )
        elif platform == "StandaloneLinux64":
            binary = bytearray(64)
            binary[:6], binary[18:20] = b"\x7fELF\x02\x01", b"\x3e\0"
        else:
            binary = bytearray(48)
            binary[:8] = b"\xca\xfe\xba\xbe\0\0\0\x02"
            binary[8:12], binary[28:32] = (0x01000007).to_bytes(4, "big"), (
                0x0100000C
            ).to_bytes(4, "big")
        self.write((editor / "Plugins" / native).relative_to(self.root), binary)
        if platform != "StandaloneOSX":
            self.write("MonoBleedingEdge/EmbedRuntime/mono-runtime")
            self.write(
                "UnityPlayer.dll"
                if platform == "StandaloneWindows64"
                else "UnityPlayer.so"
            )
        return editor

    def test_package_all_desktop_platforms_contain_both_players_and_verifiable_manifest(
        self,
    ):
        for platform in packager.PLATFORMS:
            with (
                self.subTest(platform=platform),
                tempfile.TemporaryDirectory() as temporary,
            ):
                self.root = Path(temporary) / "build"
                self.root.mkdir()
                self.create_build(platform)
                output = Path(temporary) / (
                    "bundle.zip"
                    if platform == "StandaloneWindows64"
                    else "bundle.tar.gz"
                )
                packager.package(self.root, platform, output)
                if platform == "StandaloneWindows64":
                    with zipfile.ZipFile(output) as archive:
                        payload = {
                            name: archive.read(name) for name in archive.namelist()
                        }
                else:
                    with tarfile.open(output) as archive:
                        payload = {
                            item.name: archive.extractfile(item).read()
                            for item in archive.getmembers()
                        }
                        player = archive.getmember(
                            "rebellion2.x86_64"
                            if platform == "StandaloneLinux64"
                            else "rebellion2.app/Contents/MacOS/rebellion2"
                        )
                        self.assertTrue(player.mode & 0o111)
                manifest = json.loads(payload.pop("bundle.json"))
                self.assertEqual(
                    set(payload), {item["path"] for item in manifest["files"]}
                )
                for item in manifest["files"]:
                    self.assertEqual(
                        item["sha256"],
                        hashlib.sha256(payload[item["path"]]).hexdigest(),
                    )
                self.assertTrue(
                    output.with_name(output.name + ".sha256")
                    .read_text()
                    .startswith(hashlib.sha256(output.read_bytes()).hexdigest())
                )

    def test_package_ignores_user_content_and_debug_output(self):
        self.create_build()
        self.write("Mods/private-pack/pack.xml", b"private")
        self.write("Content/catalog.xml", b"private")
        self.write("anything-private.txt", b"private")
        self.write("Rebellion 2_BurstDebugInformation_DoNotShip/secret.txt", b"private")
        self.write("rebellion2_Data/Managed/GameAssembly.pdb", b"private")
        files = packager.collect_files(self.root, "StandaloneWindows64")
        self.assertTrue(files)
        self.assertFalse(any(path.read_bytes() == b"private" for path in files))

    def test_mac_single_app_installer_keeps_the_nested_companion(self):
        self.create_build("StandaloneOSX")
        nested = (
            self.root
            / "rebellion2.app/Contents/Helpers/rebellion2-editor.app/Contents/MacOS/rebellion2-editor"
        )
        self.assertTrue(nested.is_file())
        copied = Path(self.temporary.name) / "installed"
        shutil.copytree(self.root / "rebellion2.app", copied / "rebellion2.app")

        files = packager.collect_files(copied, "StandaloneOSX")

        self.assertIn(copied / nested.relative_to(self.root), files)
        self.assertFalse((copied / "rebellion2-editor.app").exists())

    def test_collect_files_supports_the_release_pipeline_game_name(self):
        for platform in packager.PLATFORMS:
            with (
                self.subTest(platform=platform),
                tempfile.TemporaryDirectory() as temporary,
            ):
                self.root = Path(temporary) / "build"
                self.root.mkdir()
                self.create_build(platform, "Rebellion2")

                files = packager.collect_files(self.root, platform, "Rebellion2")

                self.assertTrue(files)

    def test_package_missing_companion_preserves_existing_archive(self):
        self.create_build()
        (self.root / "rebellion2-editor.exe").unlink()
        output = Path(self.temporary.name) / "bundle.zip"
        output.write_bytes(b"existing")
        with self.assertRaisesRegex(ValueError, "Missing player"):
            packager.package(self.root, "StandaloneWindows64", output)
        self.assertEqual(b"existing", output.read_bytes())

    def test_collect_files_rejects_editor_code_in_game(self):
        self.create_build()
        self.write("rebellion2_Data/Managed/ContentPackEditorAssembly.dll")
        with self.assertRaisesRegex(ValueError, "assembly inclusion"):
            packager.collect_files(self.root, "StandaloneWindows64")

    def test_collect_files_rejects_missing_editor_native_library(self):
        editor = self.create_build()
        (editor / "Plugins/nfd.dll").unlink()
        with self.assertRaisesRegex(ValueError, "library inclusion"):
            packager.collect_files(self.root, "StandaloneWindows64")

    def test_collect_files_rejects_wrong_native_architecture(self):
        editor = self.create_build()
        (editor / "Plugins/nfd.dll").write_bytes(b"wrong architecture")
        with self.assertRaisesRegex(ValueError, "dialog library"):
            packager.collect_files(self.root, "StandaloneWindows64")

    def test_collect_files_rejects_missing_license(self):
        editor = self.create_build()
        (editor / "ThirdPartyNotices/NativeFileDialog.txt").unlink()
        with self.assertRaisesRegex(ValueError, "license notice"):
            packager.collect_files(self.root, "StandaloneWindows64")

    def test_collect_files_rejects_user_content_inside_player_data(self):
        self.create_build()
        self.write("rebellion2-editor_Data/Mods/private-pack/pack.xml")
        with self.assertRaisesRegex(ValueError, "user content"):
            packager.collect_files(self.root, "StandaloneWindows64")

    def test_collect_files_rejects_user_content_regardless_of_case(self):
        self.create_build()
        self.write("rebellion2-editor_Data/mOdS/private-pack/pack.xml")
        with self.assertRaisesRegex(ValueError, "user content"):
            packager.collect_files(self.root, "StandaloneWindows64")

    def test_collect_files_rejects_missing_shared_runtime(self):
        self.create_build()
        (self.root / "MonoBleedingEdge/EmbedRuntime/mono-runtime").unlink()
        with self.assertRaisesRegex(ValueError, "Mono runtime"):
            packager.collect_files(self.root, "StandaloneWindows64")

    def test_package_output_inside_build_is_rejected(self):
        self.create_build()
        with self.assertRaisesRegex(ValueError, "outside the build"):
            packager.package(self.root, "StandaloneWindows64", self.root / "bundle.zip")

    def test_stage_installer_payload_excludes_debug_and_user_files(self):
        self.create_build()
        self.write("Mods/user/pack.xml", b"user")
        self.write("Rebellion 2_BurstDebugInformation_DoNotShip/source.txt", b"debug")
        self.write("rebellion2_Data/Managed/GameAssembly.pdb", b"debug")
        destination = Path(self.temporary.name) / "staged"

        packager.stage(self.root, "StandaloneWindows64", destination)

        staged = {
            path.relative_to(destination)
            for path in destination.rglob("*")
            if path.is_file()
        }
        expected = {
            path.relative_to(self.root)
            for path in packager.collect_files(self.root, "StandaloneWindows64")
        }
        self.assertEqual(expected, staged)
        self.assertTrue((destination / "rebellion2-editor.exe").is_file())

    def test_stage_existing_destination_is_preserved(self):
        self.create_build()
        destination = Path(self.temporary.name) / "staged"
        destination.mkdir()
        (destination / "existing.txt").write_bytes(b"keep")

        with self.assertRaisesRegex(ValueError, "already exists"):
            packager.stage(self.root, "StandaloneWindows64", destination)

        self.assertEqual(b"keep", (destination / "existing.txt").read_bytes())


if __name__ == "__main__":
    unittest.main()
