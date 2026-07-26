using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rebellion.Game;
using UnityEngine;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class ResourceManagerTests
    {
        private string _contentRoot;

        [SetUp]
        public void SetUp()
        {
            _contentRoot = Path.Combine(
                Path.GetTempPath(),
                nameof(ResourceManagerTests),
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(Path.Combine(_contentRoot, "Configs"));
            Directory.CreateDirectory(Path.Combine(_contentRoot, "Data"));
            ResourceManager.SetContentRootPathForTests(_contentRoot);
        }

        [TearDown]
        public void TearDown()
        {
            ResourceManager.SetContentRootPathForTests(null);
            if (Directory.Exists(_contentRoot))
                Directory.Delete(_contentRoot, true);
        }

        [Test]
        public void GetTexture_RawPng_LoadsAndCachesDecodedTexture()
        {
            string artDirectory = Path.Combine(_contentRoot, "Art", "HD", "UI");
            Directory.CreateDirectory(artDirectory);
            string imagePath = Path.Combine(artDirectory, "mod_image.png");
            WritePng(imagePath, 3, 2);

            Texture2D first = ResourceManager.GetTexture("Art/HD/UI/mod_image");
            Texture2D second = ResourceManager.GetTexture("Art/HD/UI/mod_image");

            Assert.IsNotNull(first);
            Assert.AreEqual(3, first.width);
            Assert.AreEqual(2, first.height);
            Assert.AreEqual("mod_image", first.name);
            Assert.AreEqual(TextureWrapMode.Clamp, first.wrapMode);
            Assert.AreSame(first, second);
        }

        [Test]
        public void GetTexture_MissingPath_ReturnsNull()
        {
            Assert.IsNull(ResourceManager.GetTexture("Art/HD/UI/missing"));
            Assert.IsNull(ResourceManager.GetTexture("Art/HD/UI/missing"));
        }

        [Test]
        public void GetTexture_PathLeavesContentRoot_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ResourceManager.GetTexture("../outside"));
        }

        [Test]
        public void GetConfig_ExternalContentRoot_ReadsModifiedConfig()
        {
            File.WriteAllText(
                Path.Combine(_contentRoot, "Configs", "GameConfig.xml"),
                "<?xml version=\"1.0\"?><GameConfig><AI><TickInterval>123</TickInterval></AI></GameConfig>"
            );

            GameConfig config = ResourceManager.GetConfig<GameConfig>();

            Assert.AreEqual(123, config.AI.TickInterval);
        }

        [Test]
        public void GetVideoUrl_ExternalContentRoot_ReturnsLocalFileUrl()
        {
            string videoDirectory = Path.Combine(_contentRoot, "Videos");
            Directory.CreateDirectory(videoDirectory);
            string videoPath = Path.Combine(videoDirectory, "intro.mp4");
            File.WriteAllBytes(videoPath, Array.Empty<byte>());

            string url = ResourceManager.GetVideoUrl("Videos/intro");

            Assert.AreEqual(new Uri(videoPath).AbsoluteUri, url);
        }

        [Test]
        public void GetVideoUrl_PathLeavesContentRoot_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ResourceManager.GetVideoUrl("../outside"));
        }

        [Test]
        public void GetVideoUrl_AbsolutePath_ThrowsArgumentException()
        {
            string videoPath = Path.Combine(_contentRoot, "Videos", "intro.mp4");

            Assert.Throws<ArgumentException>(() => ResourceManager.GetVideoUrl(videoPath));
        }

        [Test]
        public void TryGetExternalArtPath_HdArtFile_ResolvesExtensionlessContentPath()
        {
            string artDirectory = Path.Combine(_contentRoot, "Art", "HD", "UI");
            Directory.CreateDirectory(artDirectory);
            File.WriteAllBytes(Path.Combine(artDirectory, "mod_image.png"), Array.Empty<byte>());
            Texture2D texture = new Texture2D(1, 1) { name = "mod_image" };
            try
            {
                bool found = ResourceManager.TryGetExternalArtPath(texture, out string path);

                Assert.IsTrue(found);
                Assert.AreEqual("Art/HD/UI/mod_image", path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void GetExternalAnimationFramePaths_NumberedFiles_ReturnsContiguousSequence()
        {
            string artDirectory = Path.Combine(_contentRoot, "Art", "HD", "UI");
            Directory.CreateDirectory(artDirectory);
            for (int frame = 1; frame <= 3; frame++)
            {
                File.WriteAllBytes(
                    Path.Combine(artDirectory, $"sequence_{frame:D2}.png"),
                    Array.Empty<byte>()
                );
            }

            Texture2D texture = new Texture2D(1, 1) { name = "sequence_01" };
            try
            {
                IReadOnlyList<string> paths = ResourceManager.GetExternalAnimationFramePaths(
                    texture
                );

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "Art/HD/UI/sequence_01",
                        "Art/HD/UI/sequence_02",
                        "Art/HD/UI/sequence_03",
                    },
                    paths
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WritePng(string path, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
