import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location(
    "install_build_media", Path(__file__).parents[1] / "install_build_media.py"
)
stager = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(stager)


class InstallBuildMediaTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.media = self.root / "media checkout"
        self.assets = self.root / "game checkout/Assets"
        self.write(self.media, "Content/Application/Schemas/game-config.xsd", "schema")
        (self.media / "Models/MainMenu").mkdir(parents=True)
        (self.media / "UnityAssets").mkdir(parents=True)

    def test_install_build_media_mirrors_content_and_models(self):
        self.write(self.media, "Content/Packs/pack.xml", "current content")
        self.write(self.media, "Models/MainMenu/planet.obj", "current model")
        self.write(self.assets, "Content/Packs/old.xml", "stale content")
        self.write(self.assets, "Art/Models/MainMenu/old/model.obj", "stale model")

        stager.install_build_media(self.media, self.assets)

        self.assertEqual(
            "current content", (self.assets / "Content/Packs/pack.xml").read_text()
        )
        self.assertEqual(
            "current model", (self.assets / "Art/Models/MainMenu/planet.obj").read_text()
        )
        self.assertFalse((self.assets / "Content/Packs/old.xml").exists())
        self.assertFalse((self.assets / "Art/Models/MainMenu/old").exists())

    def test_install_build_media_preserves_existing_metadata(self):
        self.write(self.media, "Content/pack.xml", "content")
        self.write(self.media, "Content/pack.xml.meta", "incoming metadata")
        self.write(self.assets, "Content/pack.xml.meta", "existing metadata")
        self.write(self.assets, "Content/removed/stale.xml", "stale content")
        self.write(self.assets, "Content/removed/stale.xml.meta", "retained metadata")

        stager.install_build_media(self.media, self.assets)

        self.assertEqual(
            "existing metadata", (self.assets / "Content/pack.xml.meta").read_text()
        )
        self.assertEqual(
            "retained metadata",
            (self.assets / "Content/removed/stale.xml.meta").read_text(),
        )
        self.assertFalse((self.assets / "Content/removed/stale.xml").exists())

    def test_install_build_media_overlays_unity_assets_with_metadata(self):
        self.write(self.media, "UnityAssets/Art/planet.asset", "new asset")
        self.write(self.media, "UnityAssets/Art/planet.asset.meta", "asset metadata")
        self.write(self.assets, "Art/planet.asset", "old asset")
        self.write(self.assets, "Art/unrelated.asset", "keep")

        stager.install_build_media(self.media, self.assets)

        self.assertEqual("new asset", (self.assets / "Art/planet.asset").read_text())
        self.assertEqual(
            "asset metadata", (self.assets / "Art/planet.asset.meta").read_text()
        )
        self.assertEqual("keep", (self.assets / "Art/unrelated.asset").read_text())

    def test_install_build_media_excludes_finder_files(self):
        for directory in ("Content", "Models/MainMenu", "UnityAssets/Art"):
            self.write(self.media, directory + "/.DS_Store", "finder")

        stager.install_build_media(self.media, self.assets)

        self.assertEqual([], list(self.assets.rglob(".DS_Store")))

    def test_install_build_media_rejects_missing_schema_before_changing_assets(self):
        (self.media / "Content/Application/Schemas/game-config.xsd").unlink()
        self.write(self.assets, "Content/keep.xml", "existing")

        with self.assertRaises(FileNotFoundError):
            stager.install_build_media(self.media, self.assets)

        self.assertEqual("existing", (self.assets / "Content/keep.xml").read_text())

    @staticmethod
    def write(root, relative, contents):
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(contents)
