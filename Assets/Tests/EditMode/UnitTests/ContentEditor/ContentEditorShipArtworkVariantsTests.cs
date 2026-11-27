using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorShipArtworkVariantsTests : ContentPackEditorTestBase
    {
        private ContentPackEditorProject project;
        private ContentPackEditorRecord[] ships;
        private ContentPackEditorRecord selected;
        private string normal;
        private string effect;

        /// <summary>
        /// Creates two factions containing both ship kinds and independent normal artwork.
        /// </summary>
        [SetUp]
        public void SetUpShips()
        {
            foreach (string faction in new[] { "Fixture", "Other" })
            foreach (string kind in new[] { "CapitalShip", "Starfighter" })
                WriteFixtureFile(
                    $"Factions/{faction}/{kind}.xml",
                    new XElement(
                        kind + "s",
                        new XElement(
                            kind,
                            new XElement("TypeID", faction + kind),
                            new[]
                            {
                                "DisplayImagePath",
                                "SmallDisplayImagePath",
                                "BattleResultImagePath",
                                "BackgroundImagePath",
                            }.Select(member => new XElement(
                                member,
                                $"Pack/{faction}/{kind}/{member}"
                            ))
                        )
                    ).ToString()
                );
            UpdateBaseFactionDefinition(faction =>
            {
                faction.CapitalShipsPath = "Factions/Fixture/CapitalShip.xml";
                faction.StarfightersPath = "Factions/Fixture/Starfighter.xml";
            });
            WriteFixtureFile(
                "Factions/Other/faction.xml",
                "<Faction><ID>other</ID><CapitalShipsPath>Factions/Other/CapitalShip.xml</CapitalShipsPath><StarfightersPath>Factions/Other/Starfighter.xml</StarfightersPath></Faction>"
            );
            UpdateBasePackDefinition(pack => pack.FactionPaths.Add("Factions/Other/faction.xml"));
            project = CreateWorkingProject();
            ships = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind is "CapitalShip" or "Starfighter")
                .ToArray();
            selected = ships.First(ship => ship.Kind == "CapitalShip");
            normal = Path.Combine(contentRoot, "normal.png");
            effect = Path.Combine(contentRoot, "effect.png");
            WriteImage(
                normal,
                new Vector2Int(297, 113),
                (x, _) => x < 148 ? Color.red : Color.clear
            );
            WriteImage(effect, new Vector2Int(297, 113), (_, _) => Color.blue);
        }

        /// <summary>
        /// Releases the isolated pack and its undo file ownership.
        /// </summary>
        [TearDown]
        public void ReleaseProject() => project?.Dispose();

        [TestCase("CapitalShip")]
        [TestCase("Starfighter")]
        public async Task ApplyAsync_SelectedShipVariants_PreservesOtherShipTypesAndNormalArtworkAsync(
            string kind
        )
        {
            selected = ships.First(ship => ship.Kind == kind);
            var before = ships.ToDictionary(ship => ship, ship => ship.Element.ToString());
            var normalArtwork = selected
                .GetFields()
                .Where(field => field.IsMedia && !IsEffect(field.MemberName))
                .ToDictionary(field => field.MemberName, field => field.Value);
            int count = await ApplyAsync(Templates());
            Assert.AreEqual(6, count);
            foreach (var ship in ships.Where(ship => ship != selected))
                Assert.AreEqual(before[ship], ship.Element.ToString(), ship.ID);
            foreach (var field in normalArtwork)
                Assert.AreEqual(field.Value, selected.GetField(field.Key).Value);
            foreach (string member in Members())
            {
                var field = selected.GetField(member);
                Assert.IsTrue(File.Exists(ImagePath(project, field)));
                var recipe = ContentEditorArtworkRecipe.ReadForOutput(
                    ContentPackEditorStorage.ReadGeneratedLayers(project, field),
                    member,
                    0,
                    3,
                    true
                );
                Assert.IsNotNull(recipe);
                Assert.IsTrue(recipe.Layers.Any(layer => layer.ImageSource != null));
            }
        }

        [Test]
        public async Task ApplyAsync_EffectBelowSubject_MasksAgainstEachCardsOwnArtworkAsync()
        {
            string other = Path.Combine(contentRoot, "other.png");
            WriteImage(
                other,
                new Vector2Int(297, 113),
                (x, _) => x >= 148 ? Color.green : Color.clear
            );
            var templates = Members()
                .Where(member => member.Contains("Damaged"))
                .Select(member => Template(member, below: true))
                .ToArray();
            await ContentEditorShipArtworkVariants.ApplyAsync(
                project,
                selected,
                templates,
                field => field.MemberName == "SmallDisplayImagePath" ? normal : other,
                _ => default
            );
            foreach (var member in Members().Where(member => member.Contains("Damaged")))
            {
                Texture2D image = new(2, 2);
                try
                {
                    ImageConversion.LoadImage(
                        image,
                        File.ReadAllBytes(ImagePath(project, selected.GetField(member)))
                    );
                    bool small = member == "DamagedSmallImagePath";
                    Assert.AreEqual(
                        small ? 0 : 1,
                        image.GetPixel(image.width / 5, image.height / 2).a,
                        0.01
                    );
                    Assert.AreEqual(
                        small ? 1 : 0,
                        image.GetPixel(image.width * 4 / 5, image.height / 2).a,
                        0.01
                    );
                    Assert.AreEqual(
                        0,
                        image.GetPixel(image.width * 4 / 5, image.height / 2).r,
                        0.01
                    );
                    Assert.AreEqual(0, image.GetPixel(image.width / 5, image.height / 2).g, 0.01);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }
        }

        [Test]
        public async Task ApplyAsync_UndoAndRedo_RestoresAllImagesRecipesAndDocumentsInOneStepAsync()
        {
            string[] before = project.Documents.Select(document => document.Text).ToArray();
            await ApplyAsync(Templates());
            var after = project.Documents.Select(document => document.Text).ToArray();
            var files = Directory
                .GetFiles(project.PackRootPath, "*.png", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes);

            Assert.IsTrue(project.Undo());
            CollectionAssert.AreEqual(before, project.Documents.Select(document => document.Text));
            Assert.IsFalse(project.CanUndo);
            foreach (string file in files.Keys)
                Assert.IsFalse(File.Exists(file), file);
            Assert.IsTrue(project.Redo());
            CollectionAssert.AreEqual(after, project.Documents.Select(document => document.Text));
            foreach (var file in files)
                CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key));
        }

        [Test]
        public async Task ApplyAsync_SaveAndReopen_RetainsEditablePortableLayersAsync()
        {
            await ApplyAsync(Templates());
            ContentPackEditorStorage.Save(project);
            using var reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            foreach (
                var ship in reopened
                    .GetCatalogs()
                    .SelectMany(catalog => catalog.Records)
                    .Where(record => record.Kind == selected.Kind && record.ID == selected.ID)
            )
            foreach (string member in Members())
            {
                var field = ship.GetField(member);
                var recipe = ContentEditorArtworkRecipe.ReadForOutput(
                    ContentPackEditorStorage.ReadGeneratedLayers(reopened, field),
                    member,
                    0,
                    3,
                    true
                );
                ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(field, out var size);
                using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                    normal,
                    size,
                    member.Contains("InTransit")
                        ? ContentEditorArtworkEffectStyle.ShipTravel
                        : ContentEditorArtworkEffectStyle.Damage,
                    0,
                    ship.Kind == "CapitalShip"
                        && member is "DamagedImagePath" or "InTransitImagePath",
                    null,
                    loadLayerSource: address =>
                        File.ReadAllBytes(Path.Combine(reopened.PackRootPath, address.Substring(5)))
                );
                CollectionAssert.AreEqual(
                    File.ReadAllBytes(ImagePath(reopened, field)),
                    frame.Bake(recipe.Layers)
                );
            }
        }

        [Test]
        public async Task ApplyAsync_ActiveSmallCardLayers_ScalesPositionsAndSizesAcrossCardVariantsAsync()
        {
            WriteImage(
                effect,
                new Vector2Int(297, 113),
                (x, y) => x >= 24 && x < 54 && y >= 30 && y < 50 ? Color.blue : Color.clear
            );
            using var source = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                normal,
                new Vector2Int(297, 113),
                ContentEditorArtworkEffectStyle.Damage,
                5,
                false,
                null,
                effect
            );
            var recipe = new ContentEditorArtworkRecipe { Layers = source.CreateDefaultLayers() };
            int index = recipe.Layers.FindIndex(layer =>
                layer.Role == ContentEditorArtworkLayerRole.Effect
            );
            var layer = recipe.Layers[index];
            layer.Name = "Custom fire";
            layer.Offset = new Vector2(0.1f, 0);
            layer.ScalePercent = 50;
            recipe.Layers[index] = layer;
            string[] members = Members().Where(member => member.Contains("Damaged")).ToArray();
            await ApplyAsync(
                members
                    .Select(member => new ContentEditorShipArtworkVariants(
                        selected.GetField(member),
                        source,
                        recipe
                    ))
                    .ToArray()
            );
            foreach (string member in members)
            {
                Texture2D image = new(2, 2);
                try
                {
                    ImageConversion.LoadImage(
                        image,
                        File.ReadAllBytes(ImagePath(project, selected.GetField(member)))
                    );
                    Color pixel = image.GetPixel(
                        Mathf.RoundToInt(image.width * (39f / 297 + 0.1f)),
                        Mathf.RoundToInt(image.height * 40f / 113)
                    );
                    Assert.Greater(pixel.a, 0.9f, member);
                    Assert.Greater(pixel.b, 0.9f, member);
                    var saved = ContentEditorArtworkRecipe.ReadForOutput(
                        ContentPackEditorStorage.ReadGeneratedLayers(
                            project,
                            selected.GetField(member)
                        ),
                        member,
                        0,
                        3,
                        true
                    );
                    Assert.IsTrue(
                        saved.Layers.Any(item =>
                            item.Name == "Custom fire" && item.ScalePercent == 50
                        )
                    );
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }
        }

        [Test]
        public async Task ApplyAsync_RepeatedBatch_ReusesFilesAndAddsNoExtraUndoStepAsync()
        {
            var templates = Templates();
            await ApplyAsync(templates);
            var files = Directory.GetFiles(project.PackRootPath, "*", SearchOption.AllDirectories);
            await ApplyAsync(templates);
            CollectionAssert.AreEquivalent(
                files,
                Directory.GetFiles(project.PackRootPath, "*", SearchOption.AllDirectories)
            );
            Assert.IsTrue(project.Undo());
            Assert.IsFalse(project.CanUndo);
        }

        [Test]
        public async Task ApplyAsync_DamageOnly_PreservesHyperspaceAndCombinedOrderAsync()
        {
            var before = ships.ToDictionary(ship => ship.ID, ship => ship.Element.ToString());
            await ApplyAsync(
                new[]
                {
                    Template("DamagedSmallImagePath"),
                    Template("DamagedImagePath"),
                    Template("BattleResultDamagedImagePath"),
                }
            );
            foreach (var ship in ships)
            {
                var after = new XElement(ship.Element);
                var original = XElement.Parse(before[ship.ID]);
                foreach (
                    var element in after
                        .Elements()
                        .Where(element => element.Name.LocalName.Contains("Damaged"))
                        .ToArray()
                )
                    element.Remove();
                Assert.AreEqual(original.ToString(), after.ToString());
            }
        }

        [Test]
        public async Task ApplyAsync_BackgroundAboveEffects_UsesEachRecipientsBackdropAsync()
        {
            Texture2D donor = new(4, 4);
            Texture2D recipient = new(4, 4);
            try
            {
                donor.SetPixels(Enumerable.Repeat(Color.red, 16).ToArray());
                donor.Apply();
                recipient.SetPixels(Enumerable.Repeat(Color.green, 16).ToArray());
                recipient.Apply();
                var field = selected.GetField("DamagedSmallImagePath");
                using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                    normal,
                    new Vector2Int(297, 113),
                    ContentEditorArtworkEffectStyle.Damage,
                    1,
                    false,
                    donor,
                    effect
                );
                var layers = frame.CreateDefaultLayers();
                var background = layers[0];
                layers.RemoveAt(0);
                layers.Add(background);
                var template = new ContentEditorShipArtworkVariants(
                    field,
                    frame,
                    new ContentEditorArtworkRecipe { Layers = layers }
                );
                await ContentEditorShipArtworkVariants.ApplyAsync(
                    project,
                    selected,
                    new[] { template },
                    _ => normal,
                    _ => (recipient, new Rect(0, 0, 1, 1))
                );
                Texture2D output = new(2, 2);
                try
                {
                    ImageConversion.LoadImage(
                        output,
                        File.ReadAllBytes(
                            ImagePath(project, selected.GetField("DamagedSmallImagePath"))
                        )
                    );
                    Assert.AreEqual(Color.green, output.GetPixel(10, 10));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(output);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(donor);
                UnityEngine.Object.DestroyImmediate(recipient);
            }
        }

        [Test]
        public async Task ApplyAsync_LateRecipientFailsToDecode_LeavesFilesAndAssignmentsUnchangedAsync()
        {
            string[] before = project.Documents.Select(document => document.Text).ToArray();
            string[] files = Directory.GetFiles(
                project.PackRootPath,
                "*",
                SearchOption.AllDirectories
            );
            string invalid = Path.Combine(contentRoot, "broken.png");
            File.WriteAllText(invalid, "not an image");
            await ExpectFailureAsync<InvalidDataException>(() =>
                ContentEditorShipArtworkVariants.ApplyAsync(
                    project,
                    selected,
                    Templates(),
                    field => field.MemberName == "BattleResultImagePath" ? invalid : normal,
                    _ => default
                )
            );

            CollectionAssert.AreEqual(before, project.Documents.Select(document => document.Text));
            CollectionAssert.AreEquivalent(
                files,
                Directory.GetFiles(project.PackRootPath, "*", SearchOption.AllDirectories)
            );
            Assert.IsFalse(project.CanUndo);
        }

        [Test]
        public async Task ApplyAsync_CancelledDuringPreparation_LeavesPackAndHistoryUnchangedAsync()
        {
            string[] before = project.Documents.Select(document => document.Text).ToArray();
            string[] files = Directory.GetFiles(
                project.PackRootPath,
                "*",
                SearchOption.AllDirectories
            );
            using var cancellation = new CancellationTokenSource();
            int prepared = 0;
            await ExpectFailureAsync<OperationCanceledException>(() =>
                ContentEditorShipArtworkVariants.ApplyAsync(
                    project,
                    selected,
                    Templates(),
                    _ =>
                    {
                        if (++prepared == 2)
                            cancellation.Cancel();
                        return normal;
                    },
                    _ => default,
                    cancellationToken: cancellation.Token
                )
            );
            CollectionAssert.AreEqual(before, project.Documents.Select(document => document.Text));
            CollectionAssert.AreEquivalent(
                files,
                Directory.GetFiles(project.PackRootPath, "*", SearchOption.AllDirectories)
            );
            Assert.IsFalse(project.CanUndo);
        }

        /// <summary>
        /// Locates a canonical PNG written into the working pack.
        /// </summary>
        /// <param name="owner">The pack owning the image.</param>
        /// <param name="field">The assigned image field.</param>
        /// <returns>The physical PNG path.</returns>
        private static string ImagePath(
            ContentPackEditorProject owner,
            ContentPackEditorField field
        ) =>
            Path.Combine(
                owner.PackRootPath,
                field.GetMediaAddress(field.Value).Substring(5) + ".png"
            );

        /// <summary>
        /// Recognizes the ship effect slots being replaced by the batch.
        /// </summary>
        /// <param name="member">The artwork member name.</param>
        /// <returns>Whether this is a damage or hyperspace image.</returns>
        private static bool IsEffect(string member) => Members().Contains(member);

        /// <summary>
        /// Lists both effects at every supported gameplay size.
        /// </summary>
        /// <returns>The six ship effect fields.</returns>
        private static string[] Members() =>
            new[]
            {
                "DamagedSmallImagePath",
                "DamagedImagePath",
                "BattleResultDamagedImagePath",
                "InTransitSmallImagePath",
                "InTransitImagePath",
                "BattleResultInTransitImagePath",
            };

        /// <summary>
        /// Captures the complete fixture effect set.
        /// </summary>
        /// <returns>The independent portable templates.</returns>
        private ContentEditorShipArtworkVariants[] Templates() =>
            Members().Select(member => Template(member)).ToArray();

        /// <summary>
        /// Captures one effect using the actual layer export and ship mask implementation.
        /// </summary>
        /// <param name="member">The source artwork slot.</param>
        /// <param name="below">Whether the effect is placed behind the ship.</param>
        /// <returns>The captured template.</returns>
        private ContentEditorShipArtworkVariants Template(string member, bool below = false)
        {
            var field = selected.GetField(member);
            ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(field, out var size);
            using var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                normal,
                new Vector2Int(297, 113),
                member.Contains("InTransit")
                    ? ContentEditorArtworkEffectStyle.ShipTravel
                    : ContentEditorArtworkEffectStyle.Damage,
                5,
                false,
                null,
                effect
            );
            var layers = frame.CreateDefaultLayers();
            if (below)
                layers.Reverse();
            return new ContentEditorShipArtworkVariants(
                field,
                frame,
                new ContentEditorArtworkRecipe { Layers = layers }
            );
        }

        /// <summary>
        /// Applies prepared effects through the same pack operation used by the UI.
        /// </summary>
        /// <param name="templates">The captured effects.</param>
        /// <returns>The number of updated card images.</returns>
        private Task<int> ApplyAsync(IReadOnlyList<ContentEditorShipArtworkVariants> templates) =>
            ContentEditorShipArtworkVariants.ApplyAsync(
                project,
                selected,
                templates,
                _ => normal,
                _ => default
            );

        /// <summary>
        /// Writes a transparent fixture image with controlled pixels.
        /// </summary>
        /// <param name="path">The destination file.</param>
        /// <param name="size">The image dimensions.</param>
        /// <param name="pixel">Selects each pixel's color.</param>
        private static void WriteImage(string path, Vector2Int size, Func<int, int, Color> pixel)
        {
            Texture2D image = new(size.x, size.y, TextureFormat.RGBA32, false);
            try
            {
                image.SetPixels(
                    Enumerable
                        .Range(0, size.x * size.y)
                        .Select(index => pixel(index % size.x, index / size.x))
                        .ToArray()
                );
                image.Apply();
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(image));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
