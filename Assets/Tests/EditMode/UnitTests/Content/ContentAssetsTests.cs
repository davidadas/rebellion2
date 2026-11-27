using System;
using System.IO;
using System.Linq;
using System.Threading;
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
        public void GetReadableTexture_ExistingAddress_RetainsPixelsAndCachesIndependently()
        {
            using ContentAssets assets = CreateAssets();
            Texture2D rendered = assets.GetTexture(_textureAddress);

            Texture2D first = assets.GetReadableTexture(_textureAddress);
            Texture2D second = assets.GetReadableTexture(_textureAddress);

            Assert.IsTrue(first.isReadable);
            Assert.IsFalse(rendered.isReadable);
            Assert.AreSame(first, second);
            Assert.AreNotSame(rendered, first);
            Assert.DoesNotThrow(() => first.GetPixel(0, 0));
        }

        [Test]
        public void GetReadableTexture_RgbImageThenCursor_PreservesSharedTextureOwnership()
        {
            const string address = "Application/Textures/rgb-mask";
            Texture2D source = new(1, 1, TextureFormat.RGB24, false);
            source.SetPixel(0, 0, Color.red);
            source.Apply();
            File.WriteAllBytes(
                Path.Combine(_contentRoot, "Application", "Textures", "rgb-mask.png"),
                source.EncodeToPNG()
            );
            UnityEngine.Object.DestroyImmediate(source);
            using ContentAssets assets = CreateAssets();
            Texture2D first = assets.GetReadableTexture(address);

            Texture2D cursor = assets.GetCursor(address);

            Assert.AreSame(first, cursor);
            Assert.IsTrue(first != null);
            Assert.AreEqual(TextureFormat.RGBA32, first.format);
            Assert.AreEqual(Color.red, first.GetPixel(0, 0));
            Assert.AreSame(first, assets.GetReadableTexture(address));
        }

        [Test]
        public void GetReadableTexture_MissingAddress_ReturnsNull()
        {
            using ContentAssets assets = CreateAssets();

            Assert.IsNull(assets.GetReadableTexture("Application/Textures/missing"));
        }

        [Test]
        public void GetSmoothTexture_ExistingAddress_LoadsDistinctMipmappedTexture()
        {
            const string address = "Application/Textures/smooth";
            string path = Path.Combine(_contentRoot, "Application", "Textures", "smooth.png");
            Texture2D source = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            source.SetPixels(Enumerable.Repeat(Color.white, 64).ToArray());
            source.Apply();
            File.WriteAllBytes(path, source.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            using ContentAssets assets = CreateAssets();

            Texture2D point = assets.GetTexture(address);
            Texture2D first = assets.GetSmoothTexture(address);
            Texture2D second = assets.GetSmoothTexture(address);

            Assert.IsNotNull(first);
            Assert.AreNotSame(point, first);
            Assert.AreSame(first, second);
            Assert.Greater(first.mipmapCount, 1);
            Assert.AreEqual(FilterMode.Trilinear, first.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, first.wrapMode);
            Assert.IsFalse(first.isReadable);
        }

        [Test]
        public void TryGetTextureAddress_LoadedPointAndSmoothTextures_ReturnsNormalizedAddress()
        {
            using ContentAssets assets = CreateAssets();

            Texture2D point = assets.GetTexture(" Application\\Textures\\test ");
            Texture2D smooth = assets.GetSmoothTexture(_textureAddress);

            Assert.IsTrue(assets.TryGetTextureAddress(point, out string pointAddress));
            Assert.IsTrue(assets.TryGetTextureAddress(smooth, out string smoothAddress));
            Assert.AreEqual(_textureAddress, pointAddress);
            Assert.AreEqual(_textureAddress, smoothAddress);
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
        public void GetTextureContentBounds_TintedMask_ReturnsVisiblePixelBounds()
        {
            const string address = "Application/Textures/padded-mask";
            string path = Path.Combine(_contentRoot, "Application", "Textures", "padded-mask.png");
            Texture2D source = new(5, 4, TextureFormat.RGBA32, false);
            try
            {
                source.SetPixels32(new Color32[20]);
                source.SetPixel(3, 2, Color.white);
                source.Apply();
                File.WriteAllBytes(path, source.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
            using ContentAssets assets = CreateAssets();
            Texture2D tinted = assets.GetAlphaTintedTexture(address, Color.cyan);

            Assert.AreEqual(new RectInt(3, 2, 1, 1), assets.GetTextureContentBounds(tinted));
        }

        [Test]
        public void GetAlphaTintedTexture_SharedMask_AppliesColorAndCachesColorVariant()
        {
            const string address = "Application/Textures/mask";
            string path = Path.Combine(_contentRoot, "Application", "Textures", "mask.png");
            Texture2D source = new Texture2D(2, 1, TextureFormat.RGBA32, false);
            source.SetPixels32(new[] { new Color32(4, 5, 6, 128), new Color32(7, 8, 9, 0) });
            source.Apply();
            File.WriteAllBytes(path, source.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            using ContentAssets assets = CreateAssets();

            Texture2D first = assets.GetAlphaTintedTexture(address, new Color32(20, 40, 60, 128));
            Texture2D second = assets.GetAlphaTintedTexture(address, new Color32(20, 40, 60, 128));
            Texture2D alternate = assets.GetAlphaTintedTexture(
                address,
                new Color32(60, 40, 20, 128)
            );
            Color32[] pixels = first.GetPixels32();

            Assert.AreSame(first, second);
            Assert.AreNotSame(first, alternate);
            Assert.AreEqual(new Color32(20, 40, 60, 64), pixels[0]);
            Assert.AreEqual(new Color32(20, 40, 60, 0), pixels[1]);
            Assert.IsTrue(first.isReadable);
        }

        [Test]
        public void GetAlphaTintedTexture_SharedSourceAndColor_KeepsSeparateSettingIdentities()
        {
            const string address = "Application/Textures/mask";
            string path = Path.Combine(_contentRoot, "Application", "Textures", "mask.png");
            Texture2D source = new(1, 1, TextureFormat.RGBA32, false);
            try
            {
                source.SetPixel(0, 0, Color.white);
                source.Apply();
                File.WriteAllBytes(path, source.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
            using ContentAssets assets = CreateAssets();
            var active = assets.GetAlphaTintedTexture(
                address,
                Color.red,
                FactionMaskColor.ActiveWindowTitleColorHex
            );
            var inactive = assets.GetAlphaTintedTexture(
                address,
                Color.red,
                FactionMaskColor.InactiveWindowTitleColorHex
            );

            Assert.That(
                assets.TryGetTintRole(active, out var activeRole)
                    && assets.TryGetTintRole(inactive, out var inactiveRole)
                    && activeRole == FactionMaskColor.ActiveWindowTitleColorHex
                    && inactiveRole == FactionMaskColor.InactiveWindowTitleColorHex,
                Is.True
            );
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
        public async Task PreloadAsync_CanceledBetweenFrames_CancelsBeforeDisposedAssetAccessAsync()
        {
            using ContentAssets assets = CreateAssets();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            ContentPreloadManifest manifest = new ContentPreloadManifest
            {
                TexturesPerFrame = 1,
                Textures = { _textureAddress, "Application/Textures/missing" },
            };

            Task preload = assets.PreloadAsync(manifest, cancellation.Token);
            cancellation.Cancel();
            assets.Dispose();

            try
            {
                await preload;
                Assert.Fail("The abandoned preload should have been canceled.");
            }
            catch (OperationCanceledException)
            {
                Assert.IsTrue(preload.IsCanceled);
            }
        }

        [Test]
        public async Task PreloadAsync_CanceledDuringAudioLoad_CancelsBeforeDisposedAssetAccessAsync()
        {
            ContentAssets assets = CreateAssets();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            ContentPreloadManifest manifest = new ContentPreloadManifest
            {
                TexturesPerFrame = 1,
                Audio = { "Application/Audio/pending" },
            };

            Task preload = assets.PreloadAsync(manifest, cancellation.Token);
            cancellation.Cancel();
            assets.Dispose();

            try
            {
                await preload;
                Assert.Fail("The abandoned audio preload should have been canceled.");
            }
            catch (OperationCanceledException exception)
            {
                Assert.AreEqual(cancellation.Token, exception.CancellationToken);
                Assert.IsTrue(preload.IsCanceled);
            }

            await Task.Yield();
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
        public void GetVideoUrl_ExtensionlessWebMAddress_ResolvesTheVideo()
        {
            string video = Path.Combine(_packRoot, "crawl.webm");
            File.WriteAllBytes(video, Array.Empty<byte>());
            using ContentAssets assets = CreateAssets();

            Assert.AreEqual(video, new Uri(assets.GetVideoUrl("Pack/crawl")).LocalPath);
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

        [Test]
        public async Task LoadAudioAsync_DisposedDuringLoad_RejectsPendingLoadAsync()
        {
            ContentAssets assets = CreateAssets();
            Task<AudioClip> pending = assets.LoadAudioAsync("Application/Audio/pending");

            assets.Dispose();

            try
            {
                await pending;
                Assert.Fail("Disposed assets must not finish loading audio.");
            }
            catch (ObjectDisposedException)
            {
                Assert.Throws<ObjectDisposedException>(() =>
                    assets.GetPreloadedAudio("Application/Audio/pending")
                );
            }
        }

        [TestCase(".wav")]
        [TestCase(".ogg")]
        [TestCase(".mp3")]
        public async Task LoadAudioAsync_SupportedCodec_DecodesNonSilentSamplesAsync(
            string extension
        )
        {
            string directory = Path.Combine(_packRoot, "Audio");
            Directory.CreateDirectory(directory);
            File.Copy(
                Path.Combine(
                    "Assets",
                    "Tests",
                    "EditMode",
                    "UnitTests",
                    "Content",
                    "AudioFixtures",
                    "tone" + extension
                ),
                Path.Combine(directory, "tone" + extension)
            );
            using ContentAssets assets = CreateAssets();
            AudioClip clip = await assets.LoadAudioAsync("Pack/Audio/tone");
            Assert.IsNotNull(clip);
            Assert.Greater(clip.samples, 0);
            float[] samples = new float[clip.samples * clip.channels];
            Assert.IsTrue(clip.GetData(samples, 0));
            Assert.IsTrue(Array.Exists(samples, sample => Mathf.Abs(sample) > 0.01f));
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
