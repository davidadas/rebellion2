using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentAssetsTests
    {
        [Test]
        public void GetTexture_ConfiguredPackAddress_LoadsAndCachesTexture()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);
            FactionTheme theme = pack.GameData.FactionThemes.First(candidate =>
                candidate.FactionInstanceID == pack.Scenario.PlayableFactionIDs[0]
            );
            string address = theme.ConfirmDialogTheme.BackgroundImagePath;

            Texture2D first = assets.GetTexture(address);
            Texture2D second = assets.GetTexture(address);

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
            Assert.AreEqual(FilterMode.Point, first.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, first.wrapMode);
        }

        [Test]
        public void GetTexture_ConfiguredTacticalFighterSprites_LoadsEveryGroupColorAndLod()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);
            string[] colors = { "red", "blue", "green", "gold", "gray" };
            string[] lods = { "close", "far" };

            foreach (Starfighter fighters in pack.GameData.Starfighters)
            {
                Assert.IsNotEmpty(fighters.TacticalSpritePath, fighters.TypeID);
                foreach (string color in colors)
                {
                    foreach (string lod in lods)
                    {
                        string address = $"{fighters.TacticalSpritePath}/{color}-{lod}";

                        Assert.IsNotNull(assets.GetTexture(address), address);
                    }
                }
            }
        }

        [Test]
        public void GetTexture_MissingApplicationAddress_ReturnsNull()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);

            Assert.IsNull(assets.GetTexture("Application/Common/UI/missing"));
            Assert.IsNull(assets.GetTexture("Application/Common/UI/missing"));
        }

        [Test]
        public void GetReadableTexture_ApplicationCursor_RemainsReadableAndCached()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);
            const string address = "Application/Common/UI/ui_common_cursor_default_outlined";

            Texture2D first = assets.GetReadableTexture(address);
            Texture2D second = assets.GetReadableTexture(address);

            Assert.IsNotNull(first);
            Assert.IsTrue(first.isReadable);
            Assert.AreSame(first, second);
            Assert.AreNotSame(first, assets.GetTexture(address));
        }

        [Test]
        public async System.Threading.Tasks.Task PreloadAsync_TextureDirectory_CachesExtensionlessAddressAsync()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            string contentRoot = Path.Combine(
                Path.GetTempPath(),
                "rebellion2-content-assets",
                Guid.NewGuid().ToString("N")
            );
            string textureDirectory = Path.Combine(contentRoot, "Application", "Common", "UI");
            Directory.CreateDirectory(textureDirectory);
            string sourcePath = Path.Combine(
                pack.ContentRootPath,
                "Application",
                "Common",
                "UI",
                "ui_common_confirmation_dialog.png"
            );
            string destinationPath = Path.Combine(
                textureDirectory,
                "ui_common_confirmation_dialog.png"
            );
            File.Copy(sourcePath, destinationPath);

            try
            {
                using ContentAssets assets = new ContentAssets(
                    contentRoot,
                    Path.Combine(contentRoot, "pack")
                );
                ContentPreloadManifest manifest = new ContentPreloadManifest
                {
                    TexturesPerFrame = 100,
                    TextureDirectories = { "Application/Common/UI" },
                };

                await assets.PreloadAsync(manifest);
                File.Delete(destinationPath);

                Assert.IsNotNull(
                    assets.GetTexture("Application/Common/UI/ui_common_confirmation_dialog")
                );
            }
            finally
            {
                Directory.Delete(contentRoot, true);
            }
        }

        [Test]
        public void GetTexture_UnscopedAddress_ThrowsArgumentException()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);

            Assert.Throws<ArgumentException>(() => assets.GetTexture("UI/outside"));
        }

        [Test]
        public void GetTexture_AddressLeavesPackRoot_ThrowsArgumentException()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);

            Assert.Throws<ArgumentException>(() => assets.GetTexture("Pack/../../outside"));
        }

        [Test]
        public void GetVideoUrl_ConfiguredSharedAddress_ReturnsExistingLocalFile()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);

            string url = assets.GetVideoUrl("Application/Boot/Videos/intro");

            Assert.AreEqual(Uri.UriSchemeFile, new Uri(url).Scheme);
            Assert.IsTrue(File.Exists(new Uri(url).LocalPath));
        }

        [Test]
        public void GetVideoUrl_AbsoluteAddress_ThrowsArgumentException()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);

            Assert.Throws<ArgumentException>(() => assets.GetVideoUrl(pack.PackRootPath));
        }

        [Test]
        public void Dispose_SubsequentAssetRequest_ThrowsObjectDisposedException()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            ContentAssets assets = CreateAssets(pack);

            assets.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                assets.GetTexture("Application/Common/UI/missing")
            );
        }

        private static ContentAssets CreateAssets(ContentPack pack)
        {
            return new ContentAssets(pack.ContentRootPath, pack.PackRootPath);
        }
    }
}
