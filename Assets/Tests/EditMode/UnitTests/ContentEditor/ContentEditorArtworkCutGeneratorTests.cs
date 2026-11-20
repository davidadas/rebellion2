using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorArtworkCutGeneratorTests
    {
        private string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(
                Path.GetTempPath(),
                "rebellion-artwork-cuts-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Test]
        public void GeneratePlacedPngCuts_SameSizeWithDifferentOffsets_RendersEachPlacement()
        {
            string source = WriteSolidPng(Color.green, 8, 4);
            Vector2Int size = new(8, 4);
            Vector2 shifted = new(0.25f, 0);
            var outputs = ContentEditorArtworkCutGenerator.GeneratePlacedPngCuts(
                source,
                new[] { (size, Vector2.zero), (size, shifted) },
                fitEntireSource: true,
                preserveVisibleSubject: true
            );
            Texture2D centered = Decode(outputs[(size, Vector2.zero)]);
            Texture2D moved = Decode(outputs[(size, shifted)]);
            try
            {
                Assert.AreEqual(1f, centered.GetPixel(0, 1).a);
                Assert.AreEqual(0f, moved.GetPixel(0, 1).a);
                Assert.AreEqual(1f, moved.GetPixel(2, 1).a);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(centered);
                UnityEngine.Object.DestroyImmediate(moved);
            }
        }

        [Test]
        public void GeneratePngCuts_MultipleOutputs_CreatesExactSizesAndPreservesAlpha()
        {
            string source = WriteSolidPng(new Color(0.2f, 0.4f, 0.6f, 0.25f), 8, 4);

            IReadOnlyDictionary<Vector2Int, byte[]> outputs =
                ContentEditorArtworkCutGenerator.GeneratePngCuts(
                    source,
                    new[] { new Vector2Int(6, 2), new Vector2Int(3, 3) }
                );

            AssertCut(outputs[new Vector2Int(6, 2)], 6, 2, 0.25f);
            AssertCut(outputs[new Vector2Int(3, 3)], 3, 3, 0.25f);
        }

        [Test]
        public void GeneratePngCuts_WideSource_CentersCoverCropWithoutEdgeBands()
        {
            Texture2D source = new Texture2D(6, 2, TextureFormat.RGBA32, false);
            try
            {
                for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                    source.SetPixel(
                        x,
                        y,
                        x == 0 ? Color.red
                            : x == source.width - 1 ? Color.blue
                            : Color.green
                    );
                source.Apply(false, false);
                string path = Path.Combine(folder, "bands.png");
                File.WriteAllBytes(path, source.EncodeToPNG());

                byte[] bytes = ContentEditorArtworkCutGenerator.GeneratePngCuts(
                    path,
                    new[] { new Vector2Int(2, 2) }
                )[new Vector2Int(2, 2)];
                Texture2D output = Decode(bytes);
                try
                {
                    foreach (Color pixel in output.GetPixels())
                    {
                        Assert.Greater(pixel.g, 0.9f);
                        Assert.Less(pixel.r, 0.1f);
                        Assert.Less(pixel.b, 0.1f);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(output);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void GeneratePngCuts_OffCenterTransparentSubject_CentersTheVisibleSubject()
        {
            Texture2D source = new Texture2D(6, 2, TextureFormat.RGBA32, false);
            try
            {
                Color[] pixels = new Color[source.width * source.height];
                Array.Fill(pixels, Color.clear);
                source.SetPixels(pixels);
                for (int y = 0; y < source.height; y++)
                for (int x = 4; x < source.width; x++)
                    source.SetPixel(x, y, Color.green);
                source.Apply(false, false);
                string path = Path.Combine(folder, "off-center.png");
                File.WriteAllBytes(path, source.EncodeToPNG());

                byte[] bytes = ContentEditorArtworkCutGenerator.GeneratePngCuts(
                    path,
                    new[] { new Vector2Int(2, 2) }
                )[new Vector2Int(2, 2)];
                Texture2D output = Decode(bytes);
                try
                {
                    foreach (Color pixel in output.GetPixels())
                    {
                        Assert.Greater(pixel.g, 0.45f);
                        Assert.Greater(pixel.a, 0.45f);
                        Assert.Less(pixel.r, 0.01f);
                        Assert.Less(pixel.b, 0.01f);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(output);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void GeneratePngCuts_PortraitInWideFrame_PreservesBothEndsOfTheSubject()
        {
            Texture2D source = new(8, 8, TextureFormat.RGBA32, false);
            string path = Path.Combine(folder, "portrait.png");
            try
            {
                source.SetPixels(new Color[64]);
                for (int y = 0; y < 8; y++)
                for (int x = 2; x < 6; x++)
                    source.SetPixel(x, y, y < 4 ? Color.red : Color.green);
                source.Apply();
                File.WriteAllBytes(path, source.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            Vector2Int size = new(20, 8);
            Texture2D output = Decode(
                ContentEditorArtworkCutGenerator.GeneratePngCuts(
                    path,
                    new[] { size },
                    preserveVisibleSubject: true
                )[size]
            );
            try
            {
                Assert.Greater(output.GetPixel(10, 0).r, 0.95f);
                Assert.Greater(output.GetPixel(10, 7).g, 0.95f);
                Assert.AreEqual(0, output.GetPixel(2, 4).a);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        [Test]
        public void GeneratePngCuts_FitEntireSource_AddsTransparentBarsWithoutCropping()
        {
            string source = WriteSolidPng(Color.green, 4, 2);

            byte[] bytes = ContentEditorArtworkCutGenerator.GeneratePngCuts(
                source,
                new[] { new Vector2Int(4, 4) },
                true
            )[new Vector2Int(4, 4)];
            Texture2D output = Decode(bytes);
            try
            {
                Assert.AreEqual(0, output.GetPixel(1, 0).a);
                Assert.AreEqual(0, output.GetPixel(1, 3).a);
                Assert.Greater(output.GetPixel(1, 1).g, 0.9f);
                Assert.Greater(output.GetPixel(1, 2).g, 0.9f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        private string WriteSolidPng(Color color, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Color[] pixels = new Color[width * height];
                Array.Fill(pixels, color);
                texture.SetPixels(pixels);
                texture.Apply(false, false);
                string path = Path.Combine(folder, "source.png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return path;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void AssertCut(
            byte[] bytes,
            int expectedWidth,
            int expectedHeight,
            float expectedAlpha
        )
        {
            Texture2D texture = Decode(bytes);
            try
            {
                Assert.AreEqual(expectedWidth, texture.width);
                Assert.AreEqual(expectedHeight, texture.height);
                Assert.That(texture.GetPixel(0, 0).a, Is.EqualTo(expectedAlpha).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Texture2D Decode(byte[] bytes)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(texture, bytes, false));
            return texture;
        }
    }
}
