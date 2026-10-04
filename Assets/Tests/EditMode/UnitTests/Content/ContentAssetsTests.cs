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

        /// <summary>
        /// Sets up.
        /// </summary>
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

        /// <summary>
        /// Executes tear down.
        /// </summary>
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
        public void GetTextureContentBounds_TransparentCanvas_ReturnsVisiblePixelBounds()
        {
            const string address = "Application/Textures/trimmed";
            string path = Path.Combine(_contentRoot, "Application", "Textures", "trimmed.png");
            Texture2D source = new Texture2D(4, 3, TextureFormat.RGBA32, false);
            source.SetPixels32(new Color32[12]);
            source.SetPixel(2, 1, Color.white);
            source.Apply();
            File.WriteAllBytes(path, source.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            using ContentAssets assets = CreateAssets();

            Texture2D texture = assets.GetTexture(address);
            RectInt bounds = assets.GetTextureContentBounds(texture);

            Assert.AreEqual(new RectInt(2, 1, 1, 1), bounds);
            Assert.IsFalse(texture.isReadable);
        }

        [Test]
        public void GetTexture_MissingAddress_ReturnsNull()
        {
            using ContentAssets assets = CreateAssets();

            Assert.IsNull(assets.GetTexture("Application/Textures/missing"));
            Assert.IsNull(assets.GetTexture("Application/Textures/missing"));
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

        /// <summary>
        /// Creates assets.
        /// </summary>
        /// <returns>The created assets.</returns>
        private ContentAssets CreateAssets()
        {
            return new ContentAssets(new ContentFileResolver(_contentRoot, _packRoot));
        }
    }
}
