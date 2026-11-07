using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatRendererTests
    {
        private TacticalCombatController controller;
        private TacticalCombatView view;
        private ContentAssets assets;
        private ContentModelCache models;

        /// <summary>
        /// Enters an empty play-mode scene for the isolated render target.
        /// </summary>
        /// <returns>The setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
        }

        /// <summary>
        /// Releases owned instances before their cached meshes and textures.
        /// </summary>
        /// <returns>The cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            controller?.Dispose();
            yield return null;
            models?.Dispose();
            assets?.Dispose();
            controller = null;
            view = null;
            models = null;
            assets = null;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PrepareAsync_AuthoredRoster_InstantiatesCapitalModelsFightersAndStation()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            assets = new ContentAssets(pack.FileResolver);
            models = new ContentModelCache(assets);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/StrategyView/TacticalCombat.prefab"
            );
            Assert.IsNotNull(prefab);
            view = Object.Instantiate(prefab).GetComponent<TacticalCombatView>();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            controller = new TacticalCombatController(
                encounter,
                view,
                _ => { },
                CancellationToken.None
            );
            Task preparation = controller.PrepareAsync(models, assets);
            while (!preparation.IsCompleted)
                yield return null;
            preparation.GetAwaiter().GetResult();
            yield return null;

            Assert.AreEqual(5, encounter.Units.Count);
            Assert.GreaterOrEqual(
                view.Renderer.GetComponentsInChildren<MeshRenderer>(true).Length,
                8
            );
            Assert.AreEqual(
                new Vector3(200, -7, -5),
                view.Renderer.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "Planet")
                    .localPosition
            );
            Assert.AreEqual(
                new Vector3(-200, 9, -16),
                view.Renderer.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "CSEM015")
                    .localPosition
            );
            Texture2D expectedPaletteTexture = assets.GetTexture(
                encounter
                    .Environment.TextureOverrides.Single(item => item.TextureName == "GUNSHIP.BMP")
                    .ImagePath
            );
            Assert.IsTrue(
                view.Renderer.GetComponentsInChildren<MeshRenderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Any(material => ReferenceEquals(material.mainTexture, expectedPaletteTexture))
            );
            foreach (
                TacticalCombatUnit unit in encounter.Units.Where(item =>
                    !item.IsFighter && !item.IsBattleStation
                )
            )
            {
                Transform model = view
                    .Renderer.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == unit.InstanceID);
                Assert.AreEqual(
                    1,
                    model.GetComponentsInChildren<MeshRenderer>().Length,
                    "Each deployed capital ship must have one visible detail level."
                );
                Vector3 heading = new(unit.Heading.X, unit.Heading.Y, unit.Heading.Z);
                Assert.That(Vector3.Dot(model.forward, heading), Is.EqualTo(-1).Within(0.0001f));
            }
            TacticalCombatUnit pickable = encounter.Units.First(unit =>
                !unit.IsFighter && !unit.IsBattleStation && unit.Side == encounter.PlayerSide
            );
            Transform pickRoot = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == pickable.InstanceID);
            MeshFilter pickMesh = pickRoot.GetComponentsInChildren<MeshFilter>().Single();
            Vector3[] pickVertices = pickMesh.sharedMesh.vertices;
            int[] pickTriangles = pickMesh.sharedMesh.triangles;
            Vector3 localFaceCenter =
                (
                    pickVertices[pickTriangles[0]]
                    + pickVertices[pickTriangles[1]]
                    + pickVertices[pickTriangles[2]]
                ) / 3;
            Vector3 facePoint = view.Renderer.Camera.WorldToViewportPoint(
                pickMesh.transform.TransformPoint(localFaceCenter)
            );
            Assert.IsTrue(view.Renderer.TryPick(facePoint, out TacticalCombatUnit picked, out _));
            Assert.AreSame(pickable, picked);
            Assert.IsNull(view.Renderer.Target);
            Assert.IsNull(view.Renderer.Camera.targetTexture);
            foreach (
                MeshRenderer renderer in view.Renderer.GetComponentsInChildren<MeshRenderer>(true)
            )
            foreach (Material material in renderer.sharedMaterials)
                Assert.AreNotEqual("Hidden/InternalErrorShader", material.shader.name);
        }

        /// <summary>
        /// Creates a representative content-backed roster without starting a campaign.
        /// </summary>
        /// <param name="catalog">The selected pack's typed data.</param>
        /// <returns>The isolated encounter.</returns>
        private static TacticalCombatSession CreateEncounter(GameDataCatalog catalog)
        {
            Dictionary<string, TacticalCombatUnitDefinition> definitions =
                catalog.TacticalCombat.Units.ToDictionary(item => item.UnitTypeID);
            List<TacticalCombatUnit> units = new();
            foreach (string type in new[] { "ALCS008", "CSEM006", "CSEM015", "CSAL009" })
            {
                CapitalShip ship = (CapitalShip)
                    catalog.CapitalShips.Single(item => item.TypeID == type).CreateCopy();
                ship.InstanceID = type;
                ship.CurrentHullStrength = ship.MaxHullStrength;
                ship.ManufacturingStatus = ManufacturingStatus.Complete;
                CombatSide side = type == "ALCS008" ? CombatSide.Attacker : CombatSide.Defender;
                units.Add(
                    new TacticalCombatUnit(ship, side, units.Count + 1, definitions[type])
                    {
                        Position = new Vector3N(
                            type == "ALCS008" ? -12 : 12,
                            0,
                            type == "ALCS008" ? -20 : 20
                        ),
                        Heading = side == CombatSide.Attacker ? Vector3N.UnitZ : -Vector3N.UnitZ,
                    }
                );
            }
            Starfighter fighter = (Starfighter)
                catalog.Starfighters.Single(item => item.TypeID == "SFAL01").CreateCopy();
            fighter.InstanceID = "fighter";
            fighter.CurrentSquadronSize = fighter.MaxSquadronSize;
            units.Add(
                new TacticalCombatUnit(
                    fighter,
                    CombatSide.Attacker,
                    units.Count + 1,
                    definitions[fighter.TypeID]
                )
                {
                    Position = new Vector3N(0, 0, -15),
                }
            );
            List<TacticalCombatGroup> groups = units
                .Where(unit => !unit.IsBattleStation)
                .Select(unit => new TacticalCombatGroup(
                    unit.Index + 100,
                    new[] { unit },
                    unit.Side == CombatSide.Attacker ? Vector3N.UnitZ : -Vector3N.UnitZ
                ))
                .ToList();
            TacticalCombatRandom random = new(42);
            return new TacticalCombatSession(
                units,
                groups,
                CreateSide(CombatSide.Attacker),
                CreateSide(CombatSide.Defender),
                CombatSide.Attacker,
                100,
                random,
                catalog.TacticalCombat,
                catalog.TacticalCombat.Environments[1],
                new TacticalCombatSceneryRandom(random, () => 1)
            );
        }

        /// <summary>
        /// Captures a player-controlled side for presentation-only verification.
        /// </summary>
        /// <param name="side">The encounter side.</param>
        /// <returns>The captured command state.</returns>
        private static TacticalCombatSide CreateSide(CombatSide side) =>
            new(
                side,
                (int)side,
                side == CombatSide.Attacker ? Vector3N.UnitZ : -Vector3N.UnitZ,
                true,
                0,
                0,
                null,
                null,
                0,
                0,
                false
            );
    }
}
