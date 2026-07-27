using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
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
        public void GetTexture_MissingApplicationAddress_ReturnsNull()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);

            Assert.IsNull(assets.GetTexture("application/common/ui/missing"));
            Assert.IsNull(assets.GetTexture("application/common/ui/missing"));
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
            string textureDirectory = Path.Combine(contentRoot, "application", "common", "ui");
            Directory.CreateDirectory(textureDirectory);
            string sourcePath = Path.Combine(
                pack.ContentRootPath,
                "application",
                "common",
                "ui",
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
                    TextureDirectories = { "application/common/ui" },
                };

                await assets.PreloadAsync(manifest);
                File.Delete(destinationPath);

                Assert.IsNotNull(
                    assets.GetTexture("application/common/ui/ui_common_confirmation_dialog")
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

            Assert.Throws<ArgumentException>(() => assets.GetTexture("ui/outside"));
        }

        [Test]
        public void GetTexture_AddressLeavesPackRoot_ThrowsArgumentException()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);

            Assert.Throws<ArgumentException>(() => assets.GetTexture("pack/../../outside"));
        }

        [Test]
        public void GetVideoUrl_ConfiguredSharedAddress_ReturnsExistingLocalFile()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);

            string url = assets.GetVideoUrl("application/boot/videos/intro");

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
                assets.GetTexture("application/common/ui/missing")
            );
        }

        private static ContentAssets CreateAssets(ContentPack pack)
        {
            return new ContentAssets(pack.ContentRootPath, pack.PackRootPath);
        }
    }
}
