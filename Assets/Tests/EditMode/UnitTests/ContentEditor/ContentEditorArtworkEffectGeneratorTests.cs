using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Creates a source image for artwork effects.
        /// </summary>
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

        /// <summary>
        /// Removes the source image and its effect fixtures.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            File.Delete(sourcePath);
            File.Delete(Path.ChangeExtension(sourcePath, ".effect.png"));
        }

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
        public void Bake_ImportedLayer_PreservesItsPositionAndTransparency()
        {
            Texture2D image = new(4, 4, TextureFormat.RGBA32, false);
            Texture2D output = new(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Color32[] pixels = new Color32[16];
                pixels[5] = new Color32(215, 32, 17, 128);
                image.SetPixels32(pixels);
                image.Apply(false);
                byte[] png = image.EncodeToPNG();
                using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                    sourcePath,
                    null,
                    ContentEditorArtworkEffectStyle.ShipTravel,
                    148,
                    false,
                    null,
                    loadLayerSource: _ => png
                );
                var layer = new ContentEditorArtworkLayer
                {
                    Source = -1,
                    ImageSource = "Pack/Assets/ArtworkSources/layer.png",
                    Offset = new Vector2(3f / _width, 0f),
                };

                Assert.IsTrue(output.LoadImage(frame.Bake(new[] { layer })));
                Color32[] visible = output.GetPixels32();
                int x = (_width - 4) / 2 + 1 + 3;
                int y = (_height - 4) / 2 + 1;
                Assert.AreEqual(128, visible[y * _width + x].a);
                Assert.AreEqual(1, visible.Count(pixel => pixel.a != 0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        [Test]
        public void CreateEditableFrame_AngledShip_PlacesTheWakePastTheDiagonalHull()
        {
            Texture2D angled = new(_width, _height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[_width * _height];
            for (int x = 12; x < 83; x++)
            for (int y = 5 + x / 3; y < 11 + x / 3; y++)
                pixels[y * _width + x] = Color.gray;
            angled.SetPixels(pixels);
            angled.Apply(false);
            File.WriteAllBytes(sourcePath, angled.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(angled);

            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.ShipTravel,
                148,
                false,
                null
            );
            var wake = frame.GetLayer(new ContentEditorArtworkLayer { Source = 0 });
            Assert.IsNotNull(wake.Texture);
            Assert.Greater(wake.Bounds.xMax, 89);
            Assert.Greater(wake.Bounds.center.y, 30);
        }

        [Test]
        public void GetLayer_ExtraShipWake_StartsBesideTheDefaultWake()
        {
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.ShipTravel,
                148,
                false,
                null
            );
            var existing = frame.GetLayer(new ContentEditorArtworkLayer { Source = 0 });
            var added = frame.GetLayer(new ContentEditorArtworkLayer { Source = -1, Seed = 149 });

            Assert.IsNotNull(added.Texture);
            Assert.Less(Mathf.Abs(existing.Bounds.center.x - added.Bounds.center.x), 12);
            Assert.Greater(Mathf.Abs(existing.Bounds.center.y - added.Bounds.center.y), 5);
        }

        [Test]
        public void Bake_ExistingShipOverlay_PreservesTheArtworkAndExposesSeparateLayers()
        {
            string imported = WriteEffectImage(
                (x, y) =>
                    (x >= 78 && x < 92 && y >= 12 && y < 17)
                    || (x >= 78 && x < 92 && y >= 33 && y < 38)
                        ? new Color32(3, 251, 251, 255)
                        : new Color32(0, 0, 0, 0)
            );
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.ShipTravel,
                148,
                false,
                null,
                imported
            );
            Assert.AreEqual(2, frame.DefaultLayerCount);
            var first = new ContentEditorArtworkLayer { Source = 0 };
            var second = new ContentEditorArtworkLayer { Source = 1 };
            CollectionAssert.AreEqual(
                DecodePixels(File.ReadAllBytes(imported)),
                DecodePixels(frame.Bake(new[] { first, second }))
            );
        }

        [Test]
        public void Bake_ResizedShipWake_MatchesItsPreviewBounds()
        {
            string imported = WriteEffectImage(
                (x, y) =>
                    x >= 78 && x < 92 && y >= 12 && y < 18
                        ? new Color32(3, 251, 251, 255)
                        : new Color32(0, 0, 0, 0)
            );
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.ShipTravel,
                148,
                false,
                null,
                imported
            );
            var layer = new ContentEditorArtworkLayer { Source = 0, ScalePercent = 50 };
            RectInt bounds = frame.GetLayerBounds(layer, frame.GetLayer(layer).Bounds);
            Color32[] baked = DecodePixels(frame.Bake(new[] { layer }));

            Assert.That(bounds.size, Is.EqualTo(new Vector2Int(7, 3)));
            for (int y = 0; y < frame.Size.y; y++)
            for (int x = 0; x < frame.Size.x; x++)
                Assert.That(
                    baked[y * frame.Size.x + x].a != 0,
                    Is.EqualTo(bounds.Contains(new Vector2Int(x, y)))
                );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Bake_ExcludedShipOverlayLayer_KeepsTheOtherLayer(bool hideRatherThanRemove)
        {
            string imported = WriteEffectImage(
                (x, y) =>
                    x >= 78 && x < 92 && (y >= 12 && y < 17 || y >= 33 && y < 38)
                        ? new Color32(3, 251, 251, 255)
                        : new Color32(0, 0, 0, 0)
            );
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.ShipTravel,
                148,
                false,
                null,
                imported
            );

            var visible = new ContentEditorArtworkLayer { Source = 1 };
            var layers = hideRatherThanRemove
                ? new[]
                {
                    new ContentEditorArtworkLayer { Source = 0, Hidden = true },
                    visible,
                }
                : new[] { visible };
            Color32[] pixels = DecodePixels(frame.Bake(layers));
            Assert.AreEqual(0, pixels[14 * _width + 82].a);
            Assert.AreEqual(255, pixels[34 * _width + 82].a);
        }

        [Test]
        public void Bake_ExistingPersonnelCard_RetainsTheCardAndItsTransparentCutout()
        {
            string imported = WriteEffectImage(
                (x, y) =>
                    x >= 12 && x < 105 && y >= 12 && y < 36 ? new Color32(0, 0, 0, 0)
                    : y == 4 || y == 43 ? new Color32(223, 227, 247, 255)
                    : new Color32(0, 0, 0, 255)
            );
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.PersonnelTravel,
                148,
                false,
                null,
                imported
            );
            var layers = Enumerable
                .Range(0, frame.DefaultLayerCount)
                .Select(index => new ContentEditorArtworkLayer { Source = index })
                .ToArray();

            CollectionAssert.AreEqual(
                DecodePixels(File.ReadAllBytes(imported)),
                DecodePixels(frame.Bake(layers))
            );
        }

        [Test]
        public void Bake_MovedPersonnelStreakOntoTransparentArea_PreservesTheVisibleEdit()
        {
            Color32 streak = new(80, 120, 240, 255);
            string imported = WriteEffectImage(
                (x, y) => y == 4 && x is >= 1 and <= 6 ? streak : default
            );
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.PersonnelTravel,
                148,
                false,
                null,
                imported
            );
            var layer = new ContentEditorArtworkLayer
            {
                Source = 0,
                Offset = new Vector2(0, 3f / _height),
            };

            Assert.That(
                DecodePixels(frame.Bake(new[] { layer }))[7 * _width + 3],
                Is.EqualTo(streak)
            );
        }

        [Test]
        public void Bake_GeneratedPersonnelCard_ProducesBrightBlueBeyondTheSilhouette()
        {
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.PersonnelTravel,
                148,
                false,
                null
            );
            var layers = Enumerable
                .Range(0, frame.DefaultLayerCount)
                .Select(index => new ContentEditorArtworkLayer { Source = index })
                .ToArray();

            Color32[] pixels = DecodePixels(frame.Bake(layers));

            Assert.AreEqual(0, pixels[24 * _width + 40].a);
            Assert.IsTrue(pixels.Any(pixel => pixel.a == 255 && pixel.r >= 220 && pixel.g >= 220));
            Assert.IsFalse(
                pixels.Any(pixel => pixel.g > pixel.r * 1.5f && pixel.b <= pixel.g * 1.1f)
            );
        }

        [Test]
        public void Bake_NonShipTransitVariantAndAddition_RenderIndependently()
        {
            using var regular = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.RegimentTravel,
                148,
                false,
                null
            );
            using var variant = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.RegimentTravel,
                148,
                false,
                null,
                variant: 2
            );
            var added = new ContentEditorArtworkLayer { Source = -1, Seed = 149 };
            var layers = Enumerable
                .Range(0, regular.DefaultLayerCount)
                .Select(index => new ContentEditorArtworkLayer { Source = index })
                .ToArray();

            Assert.IsNotNull(regular.GetLayer(added).Texture);
            Assert.IsFalse(regular.Bake(layers).SequenceEqual(variant.Bake(layers)));
        }

        [TestCase("PersonnelTravel", 275, 113, 0, 50, 9)]
        [TestCase("PersonnelTravel", 275, 113, 1, 50, 9)]
        [TestCase("PersonnelTravel", 275, 113, 2, 50, 9)]
        [TestCase("PersonnelTravel", 549, 225, 0, 50, 9)]
        [TestCase("PersonnelTravel", 549, 225, 1, 50, 9)]
        [TestCase("PersonnelTravel", 549, 225, 2, 50, 9)]
        [TestCase("FacilityTravel", 302, 158, 0, 50, 5)]
        [TestCase("FacilityTravel", 302, 158, 1, 50, 5)]
        [TestCase("RegimentTravel", 275, 113, 0, 62, 5)]
        [TestCase("RegimentTravel", 275, 113, 1, 62, 5)]
        public void Bake_TransitStreaks_KeepTheirCardScaleAndRemainSeparate(
            string style,
            int width,
            int height,
            int variant,
            int longest,
            int thickest
        )
        {
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                new Vector2Int(width, height),
                Enum.Parse<ContentEditorArtworkEffectStyle>(style),
                148,
                false,
                null,
                variant: variant
            );
            var layers = Enumerable
                .Range(0, frame.DefaultLayerCount)
                .Select(index => new ContentEditorArtworkLayer { Source = index })
                .ToArray();

            Color32[] pixels = DecodePixels(frame.Bake(layers));
            Assert.That(
                LongestBlueRun(pixels, width, height, vertical: false),
                Is.InRange(1, longest)
            );
            Assert.That(
                LongestBlueRun(pixels, width, height, vertical: true),
                Is.InRange(1, thickest)
            );
        }

        [TestCase(275, 113)]
        [TestCase(549, 225)]
        public void Bake_DefaultPersonnelStreaks_IncludeShortAndLongStrokes(int width, int height)
        {
            string transparent = WriteEffectImage(
                (x, y) =>
                    x >= 50 && x < 70 && y >= 18 && y < 30
                        ? new Color32(128, 128, 128, 255)
                        : new Color32(0, 0, 0, 0)
            );
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                transparent,
                new Vector2Int(width, height),
                ContentEditorArtworkEffectStyle.PersonnelTravel,
                148,
                false,
                null
            );
            var layers = Enumerable
                .Range(0, frame.DefaultLayerCount)
                .Select(index => new ContentEditorArtworkLayer { Source = index })
                .ToArray();

            int[] runs = GetBlueRuns(
                DecodePixels(frame.Bake(layers)),
                width,
                height,
                vertical: false
            );
            double mean = runs.Average();
            double deviation = Math.Sqrt(runs.Average(length => Math.Pow(length - mean, 2)));
            Assert.That(deviation, Is.InRange(13.0, 22.0));
        }

        [Test]
        public void GetLayer_AddedStreaksInEarlierDraft_UseTheMeasuredScale()
        {
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                new Vector2Int(549, 225),
                ContentEditorArtworkEffectStyle.PersonnelTravel,
                148,
                false,
                null,
                variant: 1,
                legacyStreaks: true
            );

            var added = frame.GetLayer(
                new ContentEditorArtworkLayer
                {
                    Source = -1,
                    Seed = 1024,
                    MeasuredStreaks = true,
                }
            );

            Assert.That(
                LongestBlueRun(
                    added.Texture.GetPixels32(),
                    added.Bounds.width,
                    added.Bounds.height,
                    vertical: false
                ),
                Is.InRange(1, 50)
            );
        }

        [TestCase("PersonnelTravel", 275, 113)]
        [TestCase("PersonnelTravel", 549, 225)]
        [TestCase("RegimentTravel", 275, 113)]
        [TestCase("FacilityTravel", 302, 158)]
        public void GetLayer_AddedStreakGroup_UsesSeveralVisibleRows(
            string effect,
            int width,
            int height
        )
        {
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                new Vector2Int(width, height),
                Enum.Parse<ContentEditorArtworkEffectStyle>(effect),
                148,
                false,
                null
            );
            var addition = frame.GetLayer(
                new ContentEditorArtworkLayer
                {
                    Source = -1,
                    Seed = 149,
                    MeasuredStreaks = true,
                    VisibleStreakGroup = true,
                }
            );

            Assert.IsNotNull(addition.Texture);
            Assert.Greater(addition.Bounds.xMin, 0);
            Assert.Less(addition.Bounds.xMax, width);
            Assert.Greater(addition.Bounds.yMin, 0);
            Assert.Less(addition.Bounds.yMax, height);
            Color32[] pixels = addition.Texture.GetPixels32();
            int visibleRows = 0;
            bool previous = false;
            for (int y = 0; y < addition.Bounds.height; y++)
            {
                bool hasStreak = pixels
                    .Skip(y * addition.Bounds.width)
                    .Take(addition.Bounds.width)
                    .Any(pixel => pixel.a > 0);
                if (hasStreak && !previous)
                    visibleRows++;
                previous = hasStreak;
            }
            Assert.That(visibleRows, Is.InRange(3, 5));
            Assert.That(
                GetBlueRuns(pixels, addition.Bounds.width, addition.Bounds.height, vertical: false)
                    .Distinct()
                    .Count(),
                Is.GreaterThan(1)
            );
        }

        [TestCase(0, false)]
        [TestCase(2, false)]
        [TestCase(3, true)]
        public void CreateEditableFrame_RegisteredFacilityCard_UsesItsOwnClearance(
            int margin,
            bool edgeReflections
        )
        {
            string reference = WriteEffectImage(
                (x, y) =>
                {
                    int horizontal = Math.Max(12 - x, x - 104);
                    int vertical = Math.Max(12 - y, y - 35);
                    int ring = Math.Max(horizontal, vertical);
                    bool reflection = edgeReflections && x >= 40 && x < 45 && ring is 1 or 2;
                    return ring > margin || reflection
                        ? new Color32(90, 129, 188, 255)
                        : new Color32(0, 0, 0, 255);
                }
            );

            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.FacilityTravel,
                148,
                false,
                null,
                facilityReferencePath: reference
            );

            Assert.AreEqual(margin, frame.FacilityClearance);
        }

        [Test]
        public void Bake_MovedFacility_KeepsTheClearanceWithTheSubject()
        {
            const int margin = 4;
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.FacilityTravel,
                148,
                false,
                null,
                facilityClearance: margin
            );

            var layers = Enumerable
                .Range(0, frame.DefaultLayerCount)
                .Select(index => new ContentEditorArtworkLayer
                {
                    Source = index,
                    Offset =
                        index == frame.FacilityLayerIndex
                            ? new Vector2(0, 4f / _height)
                            : Vector2.zero,
                })
                .ToArray();
            Color32[] pixels = DecodePixels(frame.Bake(layers));

            Assert.AreEqual(new Color32(0, 0, 0, 255), pixels[42 * _width + 40]);
            Assert.AreEqual(new Color32(31, 72, 81, 255), pixels[39 * _width + 40]);
        }

        [TestCase("ShipTravel")]
        [TestCase("Damage")]
        [TestCase("FacilityTravel")]
        [TestCase("PersonnelTravel")]
        [TestCase("RegimentTravel")]
        public void Bake_OpaqueUnregisteredArtwork_GeneratesAVisibleEffect(string format)
        {
            Color32 sourceColor = new(143, 31, 45, 255);
            string image = WriteEffectImage((_, _) => sourceColor);
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                image,
                new Vector2Int(_width, _height),
                Enum.Parse<ContentEditorArtworkEffectStyle>(format),
                seed: 17,
                stretchSource: false,
                backdrop: null,
                completeTravel: true
            );
            var layers = Enumerable
                .Range(0, frame.DefaultLayerCount)
                .Select(index => new ContentEditorArtworkLayer { Source = index })
                .ToArray();

            Color32[] pixels = DecodePixels(frame.Bake(layers));

            Assert.IsTrue(pixels.Any(pixel => pixel.a > 0 && !pixel.Equals(sourceColor)));
        }

        [Test]
        public void Bake_RegisteredShipTravel_PreservesTheCompleteWake()
        {
            var cyan = new Color32(3, 251, 251, 255);
            string overlay = WriteEffectImage(
                (x, y) =>
                    x >= 78 && x < 99 && ((y >= 9 && y < 16) || (y >= 33 && y < 38))
                        ? cyan
                        : new Color32(0, 0, 0, 0)
            );
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.ShipTravel,
                148,
                false,
                null,
                completeTravel: true,
                travelPattern: new ContentEditorTravelPattern(overlay, sourcePath)
            );
            var layers = Enumerable
                .Range(0, frame.DefaultLayerCount)
                .Select(i => new ContentEditorArtworkLayer { Source = i })
                .ToArray();

            CollectionAssert.AreEqual(
                DecodePixels(File.ReadAllBytes(overlay)),
                DecodePixels(frame.Bake(layers))
            );
        }

        [TestCase("PersonnelTravel")]
        [TestCase("RegimentTravel")]
        public void Bake_CompleteTravel_KeepsThePatternAndDropsSlivers(string format)
        {
            Color32 blue = new(90, 129, 188, 255);
            Color32 black = new(0, 0, 0, 255);
            string pattern = WriteEffectImage(
                (x, y) =>
                    (y == 6 && (x == 9 || x >= 39 && x <= 49)) || (y == 20 && x >= 10 && x <= 14)
                        ? blue
                        : black
            );
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                Enum.Parse<ContentEditorArtworkEffectStyle>(format),
                148,
                false,
                null,
                completeTravel: true,
                travelPattern: new ContentEditorTravelPattern(pattern, null)
            );
            Color32[] pixels = DecodePixels(
                frame.Bake(new[] { new ContentEditorArtworkLayer { Source = 0 } })
            );

            Assert.AreEqual(1, frame.DefaultLayerCount);
            Assert.AreEqual(blue, pixels[6 * _width + 42]);
            Assert.AreEqual(black, pixels[6 * _width + 9]);
            Assert.AreEqual(black, pixels[20 * _width + 10]);
            Assert.AreEqual(0, pixels[20 * _width + 12].a);
        }

        [Test]
        public void Bake_VariedShipWakesWithDifferentSeeds_ChangesTrailLength()
        {
            string overlay = WriteShipWakeReference();
            Color32[] shortWake = DecodePixels(BakeWakeVariation(overlay, 14));
            Color32[] longWake = DecodePixels(BakeWakeVariation(overlay, 42));

            int Length(Color32[] pixels) =>
                Enumerable.Range(99, 21).Count(x => pixels[18 * _width + x].a > 0);

            Assert.That(Length(shortWake), Is.Not.EqualTo(Length(longWake)));
        }

        [Test]
        public void Bake_VariedShipWakesWithTheSameSeed_RecreatesTheDraft()
        {
            string overlay = WriteShipWakeReference();

            CollectionAssert.AreEqual(
                BakeWakeVariation(overlay, 42),
                BakeWakeVariation(overlay, 42)
            );
        }

        [Test]
        public void Bake_VariedShipWakes_PreservesHullContactAndDetachedWakeOrigin()
        {
            string overlay = WriteShipWakeReference();
            Color32[] pixels = DecodePixels(BakeWakeVariation(overlay, 42));

            Assert.That(pixels[18 * _width + 104], Is.EqualTo(new Color32(205, 246, 255, 255)));
            Assert.That(pixels[31 * _width + 107], Is.EqualTo(new Color32(87, 160, 230, 255)));
        }

        [Test]
        public void Bake_MissingTravelWithRegisteredFacilityCard_RebuildsTheCompleteAuthoredImage()
        {
            Color32 white = new(240, 240, 240, 255);
            Color32 blue = new(90, 129, 188, 255);
            Color32 black = new(0, 0, 0, 255);
            Color32 facility = new(31, 72, 81, 255);
            string reference = WriteEffectImage(
                (x, y) =>
                {
                    if (x >= 12 && x < 105 && y >= 12 && y < 36)
                        return facility;
                    if (y % 4 == 1 && x >= 20 && x <= 86)
                        return x % 12 < 4 ? white : blue;
                    if (y < 3)
                        return new Color32(5, 25, 51, 128);
                    return black;
                }
            );
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.FacilityTravel,
                148,
                false,
                null,
                completeTravel: true,
                travelPattern: new ContentEditorTravelPattern(reference, sourcePath),
                preserveRegisteredTravel: true
            );
            var layers = Enumerable
                .Range(0, frame.DefaultLayerCount)
                .Select(i => new ContentEditorArtworkLayer { Source = i })
                .ToArray();

            Assert.That(layers.Length, Is.EqualTo(2));
            CollectionAssert.AreEqual(
                DecodePixels(File.ReadAllBytes(reference)),
                DecodePixels(frame.Bake(layers))
            );
        }

        [Test]
        public void Bake_RegisteredOpaqueFacilityCards_KeepsTheCompactSubjectPlacement()
        {
            Color32 facility = new(188, 134, 99, 255);
            Color32 backdrop = new(75, 105, 30, 255);
            Color32 blue = new(90, 129, 188, 255);
            Color32 black = new(0, 0, 0, 255);
            bool IsFacility(int x, int y) => x >= 27 && x < 86 && y >= 12 && y < 28;
            string normal = WriteEffectImage((x, y) => IsFacility(x, y) ? facility : backdrop);
            File.Copy(normal, sourcePath, overwrite: true);
            string travel = WriteEffectImage(
                (x, y) =>
                    IsFacility(x, y) ? facility
                    : y < 7 && x >= 3 && x < 44 ? blue
                    : black
            );
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                new Vector2Int(_width, _height),
                ContentEditorArtworkEffectStyle.FacilityTravel,
                148,
                false,
                null,
                completeTravel: true,
                travelPattern: new ContentEditorTravelPattern(travel, sourcePath)
            );
            var layers = Enumerable
                .Range(0, frame.DefaultLayerCount)
                .Select(i => new ContentEditorArtworkLayer { Source = i })
                .ToArray();
            Color32[] pixels = DecodePixels(frame.Bake(layers));

            Assert.That(pixels.Count(pixel => pixel.Equals(facility)), Is.EqualTo(59 * 16));
            Assert.That(pixels[12 * _width + 27], Is.EqualTo(facility));
            Assert.That(pixels[4 * _width + 6], Is.EqualTo(blue));
            CollectionAssert.DoesNotContain(pixels, backdrop);
        }

        [Test]
        public void Bake_ShiftedTravelField_KeepsJoinedStreaksWithinTheReferenceLength()
        {
            Color32 blue = new(90, 129, 188, 255);
            Color32 black = new(0, 0, 0, 255);
            string pattern = WriteEffectImage(
                (x, y) =>
                {
                    if (y == 6 && x >= 25 && x <= 56)
                        return x >= 35 && x <= 46 ? default : blue;
                    return y == 30 && x >= 35 && x <= 46 ? blue : black;
                }
            );
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.PersonnelTravel,
                148,
                false,
                null,
                variant: 1,
                completeTravel: true,
                travelPattern: new ContentEditorTravelPattern(pattern, null)
            );
            var row = DecodePixels(
                    frame.Bake(new[] { new ContentEditorArtworkLayer { Source = 0 } })
                )
                .Skip(6 * _width)
                .Take(_width)
                .Select(pixel => pixel.Equals(blue))
                .ToArray();
            var lengths = new List<int>();
            int length = 0;
            foreach (bool hasBlue in row)
            {
                if (hasBlue)
                    length++;
                else if (length > 0)
                {
                    lengths.Add(length);
                    length = 0;
                }
            }
            Assert.That(lengths.Sum(), Is.GreaterThanOrEqualTo(24));
            Assert.That(lengths, Is.All.InRange(4, 12));
        }

        [Test]
        public void Bake_SeparateExplosions_RemovingOnePreservesAnother()
        {
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.Damage,
                148,
                false,
                null
            );
            Assert.AreEqual(3, frame.DefaultLayerCount);
            var first = new ContentEditorArtworkLayer { Source = 0 };
            var second = new ContentEditorArtworkLayer { Source = 1 };
            byte[] both = frame.Bake(new[] { first, second });
            byte[] onlySecond = frame.Bake(new[] { second });
            byte[] empty = frame.Bake(Array.Empty<ContentEditorArtworkLayer>());
            Assert.IsFalse(both.SequenceEqual(onlySecond));
            Assert.IsFalse(onlySecond.SequenceEqual(empty));
        }

        [Test]
        public void CreateEditableFrame_PersonnelTravel_UsesBlueStreaksWithoutShipExhaust()
        {
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.PersonnelTravel,
                148,
                false,
                null
            );
            Assert.IsNotNull(frame.SubjectOverlay);
            Assert.AreEqual(3, frame.DefaultLayerCount);
            var layer = frame.GetLayer(new ContentEditorArtworkLayer { Source = 0 });
            Assert.IsNotNull(layer.Texture);
            Assert.IsTrue(
                layer
                    .Texture.GetPixels32()
                    .Where(pixel => pixel.a > 0)
                    .All(pixel => pixel.b > pixel.g)
            );
        }

        [Test]
        public void Generate_ExistingShipLayer_UsesTheAuthoredPixelsBeforeVariation()
        {
            var result = ContentEditorArtworkEffectGenerator.Generate(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.Damage,
                148,
                false,
                existingEffectPath: sourcePath
            );
            Texture2D original = new(2, 2);
            Texture2D generated = new(2, 2);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(original, File.ReadAllBytes(sourcePath)));
                Assert.IsTrue(ImageConversion.LoadImage(generated, result.Png));
                CollectionAssert.AreEqual(original.GetPixels32(), generated.GetPixels32());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(original);
                UnityEngine.Object.DestroyImmediate(generated);
            }
        }

        [Test]
        public void Generate_RegimentTravel_PreservesTransparentSpaceForTheSharedBackdrop()
        {
            Color32[] pixels = ReadPixels(ContentEditorArtworkEffectStyle.RegimentTravel);
            Assert.That(pixels.Count(pixel => pixel.a > 0), Is.InRange(1, _width * _height - 1));
            Assert.IsTrue(pixels.All(pixel => pixel.a < 255));
        }

        [Test]
        public void Generate_AdditionalDamageLayer_ChangesTheEncodedImageWhenMoved()
        {
            var first = ContentEditorArtworkEffectGenerator.Generate(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.Damage,
                148,
                false,
                placements: new[] { Vector2.zero, new Vector2(0.15f, 0.32f) }
            );
            var second = ContentEditorArtworkEffectGenerator.Generate(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.Damage,
                148,
                false,
                placements: new[] { Vector2.zero, new Vector2(0.82f, 0.67f) }
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

        [TestCase("ShipTravel")]
        [TestCase("Damage")]
        public void Generate_ShipOverlay_LeavesTheCanvasBorderClear(string effect)
        {
            Color32[] pixels = ReadPixels(Enum.Parse<ContentEditorArtworkEffectStyle>(effect));

            Assert.IsTrue(pixels.Any(pixel => pixel.a > 0));
            for (int x = 0; x < _width; x++)
            {
                Assert.AreEqual(0, pixels[x].a);
                Assert.AreEqual(0, pixels[(_height - 1) * _width + x].a);
            }
            for (int y = 0; y < _height; y++)
            {
                Assert.AreEqual(0, pixels[y * _width].a);
                Assert.AreEqual(0, pixels[y * _width + _width - 1].a);
            }
        }

        [Test]
        public void Generate_ShipWithSeparatedAftSections_DrawsAnExhaustTrailForEach()
        {
            Texture2D source = new(_width, _height, TextureFormat.RGBA32, false);
            try
            {
                Color32[] pixels = new Color32[_width * _height];
                Color32 shipColor = new(31, 72, 81, 255);
                for (int y = 6; y < 42; y++)
                for (int x = 12; x < 113; x++)
                {
                    if (x < 50 || y < 16 || y >= 30)
                        pixels[y * _width + x] = shipColor;
                }
                source.SetPixels32(pixels);
                File.WriteAllBytes(sourcePath, source.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            Color32[] result = ReadPixels(ContentEditorArtworkEffectStyle.ShipTravel);

            Assert.IsTrue(result.Skip(8 * _width + 94).Take(24).Any(pixel => pixel.a > 0));
            Assert.IsTrue(result.Skip(34 * _width + 94).Take(24).Any(pixel => pixel.a > 0));
            Assert.IsTrue(result.Skip(23 * _width + 94).Take(24).All(pixel => pixel.a == 0));
        }

        /// <summary>
        /// Measures the longest continuous blue streak.
        /// </summary>
        /// <param name="pixels">The source pixel buffer.</param>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="vertical">Whether to measure vertical runs.</param>
        /// <returns>The length of the longest run.</returns>
        private static int LongestBlueRun(Color32[] pixels, int width, int height, bool vertical) =>
            GetBlueRuns(pixels, width, height, vertical).DefaultIfEmpty().Max();

        /// <summary>
        /// Measures continuous blue streaks in the selected direction.
        /// </summary>
        /// <param name="pixels">The source pixel buffer.</param>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="vertical">Whether to measure vertical runs.</param>
        /// <returns>The lengths of the detected runs.</returns>
        private static int[] GetBlueRuns(Color32[] pixels, int width, int height, bool vertical)
        {
            List<int> runs = new();
            int lines = vertical ? width : height;
            int length = vertical ? height : width;
            for (int line = 0; line < lines; line++)
            {
                int run = 0;
                for (int index = 0; index < length; index++)
                {
                    Color32 pixel = pixels[vertical ? index * width + line : line * width + index];
                    if (pixel.a == 255 && pixel.b >= 139 && pixel.b > pixel.g + 7)
                        run++;
                    else if (run > 0)
                    {
                        runs.Add(run);
                        run = 0;
                    }
                }
                if (run > 0)
                    runs.Add(run);
            }
            return runs.ToArray();
        }

        /// <summary>
        /// Writes the reference ship wake for variation tests.
        /// </summary>
        /// <returns>The reference PNG path.</returns>
        private string WriteShipWakeReference() =>
            WriteEffectImage(
                (x, y) =>
                {
                    if (x == 104 && y >= 16 && y <= 20)
                        return new Color32(205, 246, 255, 255);
                    if (x >= 99 && x < 117 && y >= 16 && y <= 20)
                        return new Color32(143, 221, 255, 255);
                    if (x >= 107 && x < 115 && y >= 29 && y <= 33)
                        return new Color32(87, 160, 230, 255);
                    return new Color32(0, 0, 0, 0);
                }
            );

        /// <summary>
        /// Renders a seeded wake from the reference overlay.
        /// </summary>
        /// <param name="overlay">The reference overlay path.</param>
        /// <param name="seed">The variation seed.</param>
        /// <returns>The rendered wake PNG.</returns>
        private byte[] BakeWakeVariation(string overlay, int seed)
        {
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                sourcePath,
                null,
                ContentEditorArtworkEffectStyle.ShipTravel,
                seed,
                false,
                null,
                completeTravel: true,
                travelPattern: new ContentEditorTravelPattern(overlay, sourcePath),
                varyShipWakes: true
            );
            var layers = Enumerable
                .Range(0, frame.DefaultLayerCount)
                .Select(index => new ContentEditorArtworkLayer { Source = index })
                .ToArray();
            return frame.Bake(layers);
        }

        /// <summary>
        /// Writes the effect image supplied by a pixel function.
        /// </summary>
        /// <param name="getPixel">The color provider for each pixel.</param>
        /// <returns>The fixture PNG path.</returns>
        private string WriteEffectImage(Func<int, int, Color32> getPixel)
        {
            string path = Path.ChangeExtension(sourcePath, ".effect.png");
            Texture2D texture = new(_width, _height, TextureFormat.RGBA32, false);
            try
            {
                Color32[] pixels = new Color32[_width * _height];
                for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    pixels[y * _width + x] = getPixel(x, y);
                texture.SetPixels32(pixels);
                texture.Apply(false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return path;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Decodes the pixels of a rendered effect.
        /// </summary>
        /// <param name="png">The encoded effect image.</param>
        /// <returns>The decoded pixels.</returns>
        private static Color32[] DecodePixels(byte[] png)
        {
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(texture, png));
                return texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Renders an artwork effect and returns its pixels.
        /// </summary>
        /// <param name="style">The effect style to render.</param>
        /// <returns>The rendered pixels.</returns>
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
