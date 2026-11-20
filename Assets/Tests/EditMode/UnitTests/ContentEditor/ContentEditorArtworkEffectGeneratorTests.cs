using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorArtworkEffectGeneratorTests
    {
        private const int _width = 120;
        private const int _height = 48;
        private string sourcePath;

        [SetUp]
        public void SetUp()
        {
            sourcePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
            Texture2D source = new(_width, _height, TextureFormat.RGBA32, false);
            try
            {
                Color32[] pixels = new Color32[_width * _height];
                for (int y = 12; y < 36; y++)
                for (int x = 12; x < 105; x++)
                    pixels[y * _width + x] = new Color32(31, 72, 81, 255);
                source.SetPixels32(pixels);
                File.WriteAllBytes(sourcePath, source.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [TearDown]
        public void TearDown() => File.Delete(sourcePath);

        [TestCase("ShipTravel")]
        [TestCase("PersonnelTravel")]
        [TestCase("FacilityTravel")]
        [TestCase("Damage")]
        public void Generate_SameSourceAndSeed_EncodesIdenticalPixels(string effectStyle)
        {
            var style = Enum.Parse<ContentEditorArtworkEffectStyle>(effectStyle);
            var first = ContentEditorArtworkEffectGenerator.Generate(
                sourcePath,
                null,
                style,
                148,
                false
            );
            var second = ContentEditorArtworkEffectGenerator.Generate(
                sourcePath,
                null,
                style,
                148,
                false
            );
            CollectionAssert.AreEqual(first.Png, second.Png);
        }

        [Test]
        public void Generate_ChangedDamageSeed_ProducesDifferentDamage()
        {
            var first = ContentEditorArtworkEffectGenerator.Generate(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.Damage,
                148,
                false
            );
            var second = ContentEditorArtworkEffectGenerator.Generate(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.Damage,
                149,
                false
            );
            Assert.IsFalse(first.Png.SequenceEqual(second.Png));
        }

        [Test]
        public void Generate_PersonnelTravel_KeepsTheSubjectTransparentInTheBackingLayer()
        {
            Color32[] pixels = ReadPixels(ContentEditorArtworkEffectStyle.PersonnelTravel);
            Assert.AreEqual(0, pixels[24 * _width + 56].a);
            Assert.AreEqual(255, pixels[24 * _width].a);
        }

        [Test]
        public void Generate_FacilityTravel_PreservesOpaqueSubjectPixelsInPlace()
        {
            Color32[] pixels = ReadPixels(ContentEditorArtworkEffectStyle.FacilityTravel);
            Assert.AreEqual(new Color32(31, 72, 81, 255), pixels[24 * _width + 56]);
            Assert.AreEqual(255, pixels[24 * _width].a);
        }

        [Test]
        public void Generate_Damage_ProducesALocalizedOverlayWithNoSubjectPixels()
        {
            Color32[] pixels = ReadPixels(ContentEditorArtworkEffectStyle.Damage);
            Assert.AreEqual(0, pixels[0].a);
            Assert.That(pixels.Count(pixel => pixel.a > 0), Is.InRange(150, _width * _height / 2));
            Assert.IsTrue(pixels.Where(pixel => pixel.a > 0).All(pixel => pixel.r > pixel.b));
        }

        private Color32[] ReadPixels(ContentEditorArtworkEffectStyle style)
        {
            var image = ContentEditorArtworkEffectGenerator.Generate(
                sourcePath,
                null,
                style,
                148,
                false
            );
            Texture2D output = new(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(output.LoadImage(image.Png));
                Assert.AreEqual(_width, output.width);
                Assert.AreEqual(_height, output.height);
                return output.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(output);
            }
        }
    }
}
