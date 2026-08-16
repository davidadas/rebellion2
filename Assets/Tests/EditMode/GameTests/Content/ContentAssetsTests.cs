using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentAssetsTests
    {
        private const string _textureAddress = "Application/Textures/test";
        private const string _videoAddress = "Application/Videos/test";
        private string _contentRoot;
        private string _packRoot;

        [SetUp]
        public void SetUp()
        {
            _contentRoot = Path.Combine(
                Path.GetTempPath(),
                "rebellion2-content-assets",
                Guid.NewGuid().ToString("N")
            );
            _packRoot = Path.Combine(_contentRoot, "Pack");
            Directory.CreateDirectory(Path.Combine(_contentRoot, "Application", "Textures"));
            Directory.CreateDirectory(Path.Combine(_contentRoot, "Application", "Videos"));
            Directory.CreateDirectory(_packRoot);
            File.WriteAllBytes(
                Path.Combine(_contentRoot, "Application", "Textures", "test.png"),
                Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
                )
            );
            File.WriteAllBytes(
                Path.Combine(_contentRoot, "Application", "Videos", "test.mp4"),
                Array.Empty<byte>()
            );
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_contentRoot))
                Directory.Delete(_contentRoot, true);
        }

        [Test]
        public void GetTexture_ExistingAddress_LoadsAndCachesTexture()
        {
            using ContentAssets assets = CreateAssets();

            Texture2D first = assets.GetTexture(_textureAddress);
            Texture2D second = assets.GetTexture(_textureAddress);

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
            Assert.AreEqual(FilterMode.Point, first.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, first.wrapMode);
        }

        [Test]
        public void GetTexture_MissingAddress_ReturnsNull()
        {
            using ContentAssets assets = CreateAssets();

            Assert.IsNull(assets.GetTexture("Application/Textures/missing"));
            Assert.IsNull(assets.GetTexture("Application/Textures/missing"));
        }

        [Test]
        public void GetCursor_ApplicationCursor_MeetsRuntimeRequirementsAndRemainsCached()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            using ContentAssets assets = CreateAssets(pack);
            const string address = "Application/Common/UI/ui_common_cursor_default_outlined";

            Texture2D first = assets.GetCursor(address);
            Texture2D second = assets.GetCursor(address);

            Assert.IsNotNull(first);
            Assert.IsTrue(first.isReadable);
            Assert.AreEqual(TextureFormat.RGBA32, first.format);
            Assert.AreEqual(1, first.mipmapCount);
            Assert.AreSame(first, second);
            Assert.AreNotSame(first, assets.GetTexture(address));
        }

        /// <summary>
        /// Verifies sprite caching distinguishes explicit nine-slice borders.
        /// </summary>
        [Test]
        public void GetSprite_DifferentBorders_CachesDistinctVariants()
        {
            using ContentAssets assets = CreateAssets();
            Vector4 border = new Vector4(7f, 7f, 7f, 7f);

            Sprite unbordered = assets.GetSprite(_textureAddress);
            Sprite bordered = assets.GetSprite(_textureAddress, border);
            Sprite borderedAgain = assets.GetSprite(_textureAddress, border);

            Assert.IsNotNull(unbordered);
            Assert.IsNotNull(bordered);
            Assert.AreNotSame(unbordered, bordered);
            Assert.AreSame(bordered, borderedAgain);
            Assert.AreEqual(border, bordered.border);
        }

        [Test]
        public async Task PreloadAsync_TextureDirectory_CachesExtensionlessAddressAsync()
        {
            using ContentAssets assets = CreateAssets();
            ContentPreloadManifest manifest = new ContentPreloadManifest
            {
                TexturesPerFrame = 100,
                TextureDirectories = { "Application/Textures" },
            };

            await assets.PreloadAsync(manifest);
            File.Delete(Path.Combine(_contentRoot, "Application", "Textures", "test.png"));

            Assert.IsNotNull(assets.GetTexture(_textureAddress));
        }

        [Test]
        public void GetTexture_UnscopedAddress_ThrowsArgumentException()
        {
            using ContentAssets assets = CreateAssets();

            Assert.Throws<ArgumentException>(() => assets.GetTexture("Textures/outside"));
        }

        [Test]
        public void GetTexture_AddressLeavesPackRoot_ThrowsArgumentException()
        {
            using ContentAssets assets = CreateAssets();

            Assert.Throws<ArgumentException>(() => assets.GetTexture("Pack/../../outside"));
        }

        [Test]
        public void GetVideoUrl_ExistingAddress_ReturnsExistingLocalFile()
        {
            using ContentAssets assets = CreateAssets();

            string url = assets.GetVideoUrl(_videoAddress);

            Assert.AreEqual(Uri.UriSchemeFile, new Uri(url).Scheme);
            Assert.IsTrue(File.Exists(new Uri(url).LocalPath));
        }

        [Test]
        public void GetVideoUrl_AbsoluteAddress_ThrowsArgumentException()
        {
            using ContentAssets assets = CreateAssets();

            Assert.Throws<ArgumentException>(() => assets.GetVideoUrl(_packRoot));
        }

        [Test]
        public void Dispose_SubsequentAssetRequest_ThrowsObjectDisposedException()
        {
            ContentAssets assets = CreateAssets();

            assets.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                assets.GetTexture("Application/Textures/missing")
            );
        }

        private ContentAssets CreateAssets()
        {
            return new ContentAssets(_contentRoot, _packRoot);
        }

        private static ContentAssets CreateAssets(ContentPack pack)
        {
            return new ContentAssets(pack.ContentRootPath, pack.PackRootPath);
        }
    }
}
