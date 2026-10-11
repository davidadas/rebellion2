using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentModelMetadataTests
    {
        private const string _address = "Pack/Units/Test/Models/model";
        private string directory;
        private string modelPath;
        private ContentModelMetadata metadata;

        /// <summary>Creates an isolated content root with generated static model dimensions.</summary>
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "model-metadata-" + Guid.NewGuid());
            modelPath = Path.Combine(directory, "Units", "Test", "Models", "model.glb");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath));
            File.WriteAllText(modelPath, "model fixture");
            WriteMetadata("2");
            metadata = new ContentModelMetadata(new ContentFileResolver(directory, directory));
        }

        /// <summary>Removes only this test's isolated temporary content tree.</summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(directory, true);
        }

        [Test]
        public void GetDimensions_IntentionalScale_ReturnsScaledModelBounds()
        {
            ModelDimensions result = metadata.GetDimensions(_address, 3f);

            Assert.AreEqual(6f, result.Width);
            Assert.AreEqual(12f, result.Height);
            Assert.AreEqual(18f, result.Depth);
        }

        [Test]
        public void GetDimensions_RepeatedAddress_ReusesMetadataWithoutSharingMutableResults()
        {
            ModelDimensions first = metadata.GetDimensions(_address, 3f);
            first.Width = 1000f;
            File.Delete(Path.ChangeExtension(modelPath, ".bounds.xml"));

            ModelDimensions second = metadata.GetDimensions(_address, 2f);

            Assert.AreEqual(4f, second.Width);
            Assert.AreNotSame(first, second);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void GetDimensions_InvalidScale_ThrowsArgumentOutOfRangeException(float scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                metadata.GetDimensions(_address, scale)
            );
        }

        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("invalid")]
        public void GetDimensions_InvalidMetadataDimension_ThrowsInvalidDataException(string width)
        {
            WriteMetadata(width);

            Assert.Throws<InvalidDataException>(() => metadata.GetDimensions(_address, 1f));
        }

        [Test]
        public void GetDimensions_MissingMetadata_RejectsRatherThanInventingDimensions()
        {
            File.Delete(Path.ChangeExtension(modelPath, ".bounds.xml"));

            Assert.Throws<FileNotFoundException>(() => metadata.GetDimensions(_address, 1f));
        }

        [Test]
        public void GetDimensions_ModModelWithoutSidecar_DoesNotUseBaseModelMetadata()
        {
            string modRoot = Path.Combine(directory, "Mod");
            string modModel = Path.Combine(modRoot, "Pack", "Units", "Test", "Models", "model.glb");
            Directory.CreateDirectory(Path.GetDirectoryName(modModel));
            File.WriteAllText(modModel, "mod fixture");
            ContentModelMetadata layered = new ContentModelMetadata(
                new ContentFileResolver(directory, directory, new[] { modRoot })
            );

            Assert.Throws<FileNotFoundException>(() => layered.GetDimensions(_address, 1f));
        }

        [Test]
        public void ResolveSize_ModelWithoutScale_UsesImportedDimensions()
        {
            CapitalShip ship = new CapitalShip { ModelPath = _address };

            metadata.ResolveSize(ship);

            Assert.AreEqual(1f, ship.ModelScale);
            Assert.AreEqual(2f, ship.ModelSize.Width);
            Assert.AreEqual(4f, ship.ModelSize.Height);
            Assert.AreEqual(6f, ship.ModelSize.Depth);
        }

        [Test]
        public void ResolveSize_RestoredBattle_RefreshesDimensionsWithoutChangingPose()
        {
            ActiveBattle battle = new ActiveBattle { Kind = BattleKind.Space };
            CapitalShip ship = new CapitalShip
            {
                ModelPath = _address,
                ModelScale = 3f,
                OwnerInstanceID = "FACTION",
            };
            metadata.ResolveSize(ship);
            CombatUnit combatant = battle.AddCombatants(ship).Single();
            combatant.Position = new BattleVector3 { X = 42f };
            combatant.Forward = new BattleVector3 { X = 1f };
            string xml = SerializationHelper.Serialize(battle);
            ActiveBattle restored = SerializationHelper.Deserialize<ActiveBattle>(xml);
            CombatUnit restoredUnit = restored.GetParticipants().Single().GetCombatants().Single();
            restoredUnit.GetUnit().ModelSize.Width = 1000f;

            metadata.ResolveSize(restoredUnit.GetUnit());

            Assert.AreEqual(3f, restoredUnit.GetUnit().ModelScale);
            Assert.AreEqual(6f, restoredUnit.GetUnit().ModelSize.Width);
            Assert.AreEqual(42f, restoredUnit.Position.X);
            Assert.AreEqual(1f, restoredUnit.Forward.X);
        }

        [Test]
        public void ResolveSize_NoModel_ClearsDerivedDimensions()
        {
            CapitalShip ship = new CapitalShip { ModelSize = new ModelDimensions { Width = 10f } };

            metadata.ResolveSize(ship);

            Assert.IsNull(ship.ModelSize);
        }

        [TestCaseSource(nameof(GetModeledShipTypeIDs))]
        public async Task GetDimensions_ImportedShip_MatchesActualScaledRendererBoundsAsync(
            string typeID
        )
        {
            BaseSceneNode ship = TestContent
                .Data.CapitalShips.Cast<BaseSceneNode>()
                .Concat(TestContent.Data.Starfighters)
                .Single(unit => unit.TypeID == typeID);
            ModelDimensions expected = TestContent.Pack.ModelMetadata.GetDimensions(
                ship.ModelPath,
                ship.ModelScale
            );
            string file = TestContent.Pack.FileResolver.ResolveFile(ship.ModelPath, ".glb");
            using ContentModelResource resource = await ContentModelLoader.LoadResourceAsync(
                file,
                CancellationToken.None
            );
            GameObject pivot = new GameObject("Metadata verification");
            try
            {
                using ContentModelInstance instance = await resource.InstantiateAsync(
                    pivot.transform,
                    CancellationToken.None
                );
                instance.ApplyPose(
                    pivot.transform,
                    ship.ModelScale,
                    Vector3.zero,
                    false,
                    false,
                    true,
                    -1
                );
                Renderer[] renderers = pivot.GetComponentsInChildren<Renderer>();
                Assert.IsNotEmpty(renderers);
                Bounds actual = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1))
                    actual.Encapsulate(renderer.bounds);

                Assert.That(actual.size.x, Is.EqualTo(expected.Width).Within(0.0001f));
                Assert.That(actual.size.y, Is.EqualTo(expected.Height).Within(0.0001f));
                Assert.That(actual.size.z, Is.EqualTo(expected.Depth).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pivot);
            }
        }

        /// <summary>Enumerates every modeled ship type for independent renderer-bound verification.</summary>
        /// <returns>The modeled capital-ship and fighter type identifiers.</returns>
        private static IEnumerable<string> GetModeledShipTypeIDs() =>
            TestContent
                .Data.CapitalShips.Cast<BaseSceneNode>()
                .Concat(TestContent.Data.Starfighters)
                .Where(unit => !string.IsNullOrWhiteSpace(unit.ModelPath))
                .Select(unit => unit.TypeID);

        /// <summary>Writes one generated-metadata fixture with an optionally invalid width.</summary>
        /// <param name="width">The width text to place in the fixture.</param>
        private void WriteMetadata(string width)
        {
            File.WriteAllText(
                Path.ChangeExtension(modelPath, ".bounds.xml"),
                "<ModelBounds><SourceHash>"
                    + new string('a', 64)
                    + "</SourceHash>"
                    + $"<Width>{width}</Width><Height>4</Height><Depth>6</Depth></ModelBounds>"
            );
        }
    }
}
