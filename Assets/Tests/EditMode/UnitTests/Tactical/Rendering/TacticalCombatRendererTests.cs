using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using Rebellion.Multiplayer.Tactical;
using Rebellion.Tactical;
using Rebellion.Tactical.Rendering;
using Rebellion.Tactical.Synchronization;
using Rebellion.UI.SceneUI.TacticalCombat;
using Rebellion.UI.SceneUI.TacticalCombat.Audio;
using Rebellion.Util.Random;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Array = global::System.Array;
using GraphicsDeviceType = global::UnityEngine.Rendering.GraphicsDeviceType;
using Object = global::UnityEngine.Object;
using TextMeshProUGUI = global::TMPro.TextMeshProUGUI;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tests.Tactical.Rendering
{
    [TestFixture]
    [Category("Graphics")]
    public sealed class TacticalCombatRendererTests
    {
        private TacticalCombatController controller;
        private TacticalCombatView view;
        private ContentAssets assets;
        private ContentModelCache models;
        private readonly Dictionary<
            TacticalCombatSession,
            TacticalCombatReplicaPlayback
        > playbacks = new();
        private float _audioVolume;
        private bool _audioVolumeCaptured;

        /// <summary>
        /// Enters an empty play-mode scene for the isolated render target.
        /// </summary>
        /// <returns>The setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Tactical render targets require a graphics device.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _audioVolume = AudioListener.volume;
            _audioVolumeCaptured = true;
            AudioListener.volume = 0;
        }

        /// <summary>
        /// Releases owned instances before their cached meshes and textures.
        /// </summary>
        /// <returns>The cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            controller?.Dispose();
            foreach (TacticalCombatReplicaPlayback playback in playbacks.Values)
                playback.Dispose();
            playbacks.Clear();
            if (controller == null && view != null)
                Object.Destroy(view.gameObject);
            yield return null;
            models?.Dispose();
            assets?.Dispose();
            controller = null;
            view = null;
            models = null;
            assets = null;
            if (_audioVolumeCaptured)
                AudioListener.volume = _audioVolume;
            _audioVolumeCaptured = false;
            if (Application.isPlaying)
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

        [UnityTest]
        public IEnumerator PrepareAsync_SharedDriver_DelegatesEntryAndLeavesClockOwnershipWithDriver()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            PresentationDriver driver = new(encounter);

            yield return PrepareController(encounter, pack, driver);

            Assert.AreEqual(1, driver.PreparationCount);
            Assert.AreSame(view.Renderer, driver.Presentation);
            Assert.IsFalse(encounter.IsArriving);
            Assert.DoesNotThrow(() => encounter.ConfigureClockedAdvance(() => 0));
            int before = encounter.SystemTime;
            controller.Advance(.25f);
            CollectionAssert.AreEqual(new[] { .25f }, driver.Intervals);
            Assert.AreEqual(before, encounter.SystemTime);
            controller.Dispose();
            controller.Dispose();
            Assert.AreEqual(1, driver.DisposeCount);
        }

        [UnityTest]
        public IEnumerator Advance_SharedCompletion_WaitsForAgreementAndCompletesOnce()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            PresentationDriver driver = new(encounter);
            int completions = 0;
            yield return PrepareController(encounter, pack, driver, _ => completions++);
            Assert.IsTrue(encounter.RequestImmediateResult());
            encounter.Advance(0);
            Assert.IsTrue(encounter.IsComplete);

            controller.Advance(0);

            Assert.AreEqual(0, completions);
            driver.CompletionReady = true;
            controller.Advance(0);
            controller.Advance(0);
            Assert.AreEqual(1, completions);
        }

        [UnityTest]
        public IEnumerator HandleKeyboardEvent_ReplicaView_KeepsCameraLocalAndSelectsAuthenticatedFaction()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession host = CreateEncounter(pack.GameData);
            TacticalCombatSession guest = CreateEncounter(
                pack.GameData,
                playerSide: CombatSide.Defender
            );
            host.ConfigureSynchronizedAuthority();
            guest.ConfigureSynchronizedReplica();
            List<TacticalCombatCommand> commands = new();
            guest.SynchronizedCommandRequested += commands.Add;
            PresentationDriver driver = new(guest);
            yield return PrepareController(guest, pack, driver);
            string framePath = guest
                .Definition.HudImages.Concat(
                    guest.Definition.Factions.Single(faction => faction.FactionIndex == 1).HudImages
                )
                .Last(image => image.Key == "frame")
                .Path;
            RawImage frame = view.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "Frame");
            Assert.AreSame(assets.GetTexture(framePath), frame.texture);
            float field = view.Renderer.Camera.fieldOfView;
            float unzoomedHorizontalScale = view.Renderer.Camera.projectionMatrix.m00;

            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.PageUp }
                )
            );
            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyUp, keyCode = KeyCode.PageUp }
                )
            );

            Assert.Less(view.Renderer.Camera.fieldOfView, field);
            float zoomedHorizontalScale = view.Renderer.Camera.projectionMatrix.m00;
            Assert.Greater(zoomedHorizontalScale, unzoomedHorizontalScale);
            Assert.IsEmpty(commands);
            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.Alpha1 }
                )
            );
            Assert.AreEqual(1, commands.Count);
            Assert.AreEqual(TacticalCombatCommandKind.ObjectCategory, commands[0].Kind);
            Assert.AreEqual(TacticalCombatSelectionKind.Empty, guest.Selection.Kind);
            Assert.IsTrue(host.ApplySynchronizedCommand(CombatSide.Defender, commands[0]));

            ApplyCapturedProjection(guest, host.CaptureSnapshot(1));
            controller.Advance(0);

            Assert.AreEqual(TacticalCombatSelectionKind.ObjectGroup, guest.Selection.Kind);
            Assert.AreEqual(CombatSide.Defender, guest.Selection.Group.Side);
            Assert.AreEqual(CombatSide.Defender, guest.Selection.PrimaryUnit.Side);
            Assert.AreEqual(3, guest.SystemTime);
            Assert.That(
                view.Renderer.Camera.projectionMatrix.m00,
                Is.EqualTo(zoomedHorizontalScale).Within(.0001f)
            );
            Assert.AreEqual(1, commands.Count);
            host.Selection.Dispose();
            host.Audio.Dispose();
        }

        [UnityTest]
        public IEnumerator RenderUnits_ReplicaMotion_AlignsModelPickingAndWeaponEndpointsToDelayedPose()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession host = CreateEncounter(pack.GameData);
            TacticalCombatSession guest = CreateEncounter(pack.GameData);
            host.ConfigureSynchronizedAuthority();
            guest.ConfigureSynchronizedReplica();
            try
            {
                TacticalCombatUnit target = host.GetUnit("ALCS008");
                Vector3N start = target.Position;
                TacticalCombatSnapshot first = host.CaptureSnapshot(1);
                first.TimestampMilliseconds = 0;
                target.Position += Vector3N.UnitX * 4;
                target.Heading = Vector3N.Normalize(Vector3N.UnitZ + Vector3N.UnitX);
                TacticalCombatSnapshot second = host.CaptureSnapshot(2);
                second.TimestampMilliseconds = 100;
                ApplyCapturedProjection(guest, first);
                ApplyCapturedProjection(guest, second);
                yield return PrepareRenderer(guest, pack);
                GetReplicaPlayback(guest).Advance(50);

                view.Renderer.RenderUnits();

                TacticalCombatUnit rendered = guest.GetUnit(target.InstanceID);
                Transform root = view
                    .Renderer.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == target.InstanceID);
                Vector3N expected = start + Vector3N.UnitX * 2;
                Assert.AreEqual(
                    new Vector3(expected.X, expected.Y, expected.Z),
                    root.localPosition
                );
                Assert.AreEqual(target.Position, rendered.Position);
                Assert.AreNotEqual(
                    new Vector3(rendered.Position.X, rendered.Position.Y, rendered.Position.Z),
                    root.localPosition
                );
                Assert.IsTrue(
                    GetReplicaPlayback(guest)
                        .TryGetPose(target.InstanceID, out _, out Vector3N heading)
                );
                Assert.That(
                    Vector3.Dot(root.forward, new Vector3(-heading.X, -heading.Y, -heading.Z)),
                    Is.EqualTo(1).Within(.0001f)
                );

                MeshFilter mesh = root.GetComponentsInChildren<MeshFilter>().Single();
                Vector3[] vertices = mesh.sharedMesh.vertices;
                int[] triangles = mesh.sharedMesh.triangles;
                Vector3 face =
                    (vertices[triangles[0]] + vertices[triangles[1]] + vertices[triangles[2]]) / 3;
                Vector3 point = view.Renderer.Camera.WorldToViewportPoint(
                    mesh.transform.TransformPoint(face)
                );
                Assert.IsTrue(view.Renderer.TryPick(point, out TacticalCombatUnit picked, out _));
                Assert.AreSame(rendered, picked);

                TacticalCombatUnit source = guest.GetUnit("CSEM006");
                view.Renderer.ShowShot(
                    new TacticalCombatShot(source, rendered, TacticalWeaponType.LaserCannon, 50),
                    1
                );
                view.Renderer.AdvanceEffects(.5f);
                Transform[] objects = view.Renderer.GetComponentsInChildren<Transform>(true);
                Transform sourceRoot = objects.Single(item => item.name == source.InstanceID);
                Transform beam = objects.Single(item => item.name == "Weapon beam");
                Assert.That(
                    Vector3.Distance(
                        (sourceRoot.localPosition + root.localPosition) * .5f,
                        beam.localPosition
                    ),
                    Is.LessThan(.0001f)
                );
            }
            finally
            {
                host.Selection.Dispose();
                host.Audio.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator RenderUnits_ReplicaDeparture_UsesInterpolatedTimerWithoutChangingRawDeparture()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession host = CreateEncounter(pack.GameData);
            TacticalCombatSession guest = CreateEncounter(pack.GameData);
            host.ConfigureSynchronizedAuthority();
            guest.ConfigureSynchronizedReplica();
            try
            {
                TacticalCombatUnit departing = host.GetUnit("ALCS008");
                departing.Status = TacticalCombatUnitStatus.EnteringHyperspace;
                departing.HyperspaceMilliseconds = 1000;
                TacticalCombatSnapshot first = host.CaptureSnapshot(1);
                first.TimestampMilliseconds = 0;
                departing.HyperspaceMilliseconds = 1200;
                TacticalCombatSnapshot second = host.CaptureSnapshot(2);
                second.TimestampMilliseconds = 100;
                ApplyCapturedProjection(guest, first);
                ApplyCapturedProjection(guest, second);
                yield return PrepareRenderer(guest, pack);
                GetReplicaPlayback(guest).Advance(50);

                view.Renderer.RenderUnits();

                TacticalCombatUnit unit = guest.GetUnit(departing.InstanceID);
                TacticalCombatDeparturePose expected = TacticalCombatDeparturePose.Evaluate(
                    unit.Index,
                    unit.Definition.PresentationIndex,
                    unit.Position,
                    guest.GetSide(unit.Side).Forward,
                    1100
                );
                Transform root = view
                    .Renderer.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == unit.InstanceID);
                Assert.AreEqual(
                    new Vector3(expected.Position.X, expected.Position.Y, expected.Position.Z),
                    root.localPosition
                );
                Assert.AreEqual(1200, unit.HyperspaceMilliseconds);
                Assert.AreEqual(departing.Position, unit.Position);
            }
            finally
            {
                host.Selection.Dispose();
                host.Audio.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator RenderUnits_ReplicaDestruction_RetainsModelsUntilDelayedDeathThenExplosionOwnsVisibility()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession host = CreateEncounter(pack.GameData);
            TacticalCombatSession guest = CreateEncounter(pack.GameData);
            host.ConfigureSynchronizedAuthority();
            guest.ConfigureSynchronizedReplica();
            Action<TacticalCombatUnit> destruction = null;
            try
            {
                TacticalCombatSnapshot first = host.CaptureSnapshot(1);
                first.TimestampMilliseconds = 0;
                TacticalCombatSnapshot death = host.CaptureSnapshot(2);
                death.TimestampMilliseconds = 100;
                foreach (
                    TacticalCombatUnitSnapshot state in death.Units.Where(unit =>
                        unit.UnitID is "ALCS008" or "fighter"
                    )
                )
                {
                    state.Status = TacticalCombatUnitStatus.Destroyed;
                    state.Hull = 0;
                }
                TacticalCombatUnitSnapshot departure = death.Units.Single(unit =>
                    unit.UnitID == "CSEM006"
                );
                departure.Status = TacticalCombatUnitStatus.EnteringHyperspace;
                departure.HyperspaceMilliseconds = 1200;
                GetReplicaPlayback(guest);
                guest.ApplySnapshot(first);
                guest.ApplySnapshot(death);
                yield return PrepareRenderer(guest, pack);
                destruction = view.Renderer.ShowDestruction;
                guest.DestructionEffectRequested += destruction;
                Transform[] objects = view.Renderer.GetComponentsInChildren<Transform>(true);
                Transform capital = objects.Single(item => item.name == "ALCS008");
                Transform fighter = objects.Single(item => item.name == "fighter");
                Transform departing = objects.Single(item => item.name == "CSEM006");
                Assert.IsFalse(guest.GetUnit("ALCS008").Systems.IsAlive);
                Assert.IsFalse(guest.GetUnit("fighter").Systems.IsAlive);

                GetReplicaPlayback(guest).Advance(99);
                view.Renderer.RenderUnits();

                Assert.IsTrue(capital.gameObject.activeSelf);
                Assert.IsTrue(fighter.gameObject.activeSelf);
                Vector3N canonical = guest.GetUnit("CSEM006").Position;
                Assert.AreEqual(
                    new Vector3(canonical.X, canonical.Y, canonical.Z),
                    departing.localPosition
                );
                Assert.IsFalse(
                    view.Renderer.GetComponentsInChildren<MeshRenderer>(true)
                        .Any(item => item.name == "Impact" && item.enabled)
                );

                GetReplicaPlayback(guest).Advance(1);
                guest.ApplySynchronizedEvents(
                    new[]
                    {
                        new TacticalCombatEvent
                        {
                            Kind = TacticalCombatEventKind.Destruction,
                            TargetID = "ALCS008",
                        },
                    }
                );
                view.Renderer.RenderUnits();

                Assert.IsTrue(
                    capital.gameObject.activeSelf,
                    "The destruction sequence retains its capital model after the death edge."
                );
                Assert.IsFalse(fighter.gameObject.activeSelf);
                TacticalCombatUnit leaving = guest.GetUnit("CSEM006");
                TacticalCombatDeparturePose expectedDeparture =
                    TacticalCombatDeparturePose.Evaluate(
                        leaving.Index,
                        leaving.Definition.PresentationIndex,
                        leaving.Position,
                        guest.GetSide(leaving.Side).Forward,
                        1200
                    );
                Assert.AreEqual(
                    new Vector3(
                        expectedDeparture.Position.X,
                        expectedDeparture.Position.Y,
                        expectedDeparture.Position.Z
                    ),
                    departing.localPosition
                );
                Assert.IsTrue(
                    view.Renderer.GetComponentsInChildren<MeshRenderer>(true)
                        .Any(item => item.name == "Impact" && item.enabled)
                );
                for (int frame = 0; frame < 12; frame++)
                    view.Renderer.AdvanceEffects(0);
                view.Renderer.RenderUnits();
                Assert.IsFalse(capital.gameObject.activeSelf);
                GetReplicaPlayback(guest).Advance(100);
                view.Renderer.RenderUnits();
                Assert.IsFalse(
                    capital.gameObject.activeSelf,
                    "An older alive snapshot cannot revive the detached model."
                );
            }
            finally
            {
                if (destruction != null)
                    guest.DestructionEffectRequested -= destruction;
                host.Selection.Dispose();
                host.Audio.Dispose();
                guest.Selection.Dispose();
                guest.Audio.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SetTransform_ArrivalFrame_RetainsPoseWithoutChangingSimulation()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit unit = encounter.Units.First(item =>
                !item.IsFighter && !item.IsBattleStation
            );
            Vector3N position = unit.Position;
            Vector3N heading = unit.Heading;
            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == unit.InstanceID);
            ITacticalCombatArrivalPresentation presentation = view.Renderer;
            presentation.SetArrivalActive(true);
            presentation.ResetArrival(unit.Index);
            presentation.SetTransform(
                unit.Index,
                new System.Numerics.Vector4(14, 7, -350, -1),
                new System.Numerics.Vector4(1, 0, 0, -1)
            );
            presentation.Refresh(unit.Index);

            view.Renderer.RenderUnits();

            Assert.AreEqual(new Vector3(14, 7, -350), root.localPosition);
            Assert.That(Vector3.Dot(root.forward, Vector3.left), Is.EqualTo(1).Within(0.0001f));
            Assert.AreEqual(position, unit.Position);
            Assert.AreEqual(heading, unit.Heading);
        }

        [UnityTest]
        public IEnumerator SetArrivalActive_CompletedArrival_ReleasesTransformsToSimulation()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit unit = encounter.Units.First(item =>
                !item.IsFighter && !item.IsBattleStation
            );
            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == unit.InstanceID);
            List<bool> strobes = new();
            view.Renderer.ArrivalPresentationChanged += strobes.Add;
            ITacticalCombatArrivalPresentation presentation = view.Renderer;
            presentation.SetArrivalActive(true);
            presentation.ResetArrival(unit.Index);
            presentation.SetTransform(
                unit.Index,
                new System.Numerics.Vector4(14, 7, -350, -1),
                new System.Numerics.Vector4(1, 0, 0, -1)
            );

            presentation.SetArrivalActive(false);
            unit.Position = new Vector3N(9, 2, 18);
            view.Renderer.RenderUnits();

            CollectionAssert.AreEqual(new[] { true, false }, strobes);
            Assert.IsFalse(view.Renderer.IsPresentingArrival);
            Assert.AreEqual(new Vector3(9, 2, 18), root.localPosition);
            Assert.That(
                Vector3.Dot(
                    root.forward,
                    new Vector3(-unit.Heading.X, -unit.Heading.Y, -unit.Heading.Z)
                ),
                Is.EqualTo(1).Within(0.0001f)
            );
        }

        [UnityTest]
        public IEnumerator Reset_HiddenCapital_RestoresBaseModelBeforeRefresh()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit unit = encounter.Units.First(item =>
                !item.IsFighter && !item.IsBattleStation
            );
            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == unit.InstanceID);
            root.gameObject.SetActive(false);

            view.Renderer.ResetArrival(unit.Index);

            Assert.IsTrue(root.gameObject.activeSelf);
            Assert.AreEqual(1, root.GetComponentsInChildren<MeshRenderer>().Length);
        }

        [UnityTest]
        public IEnumerator Refresh_UnknownRegistration_DoesNotCreatePresentation()
        {
            GameObject root = new("Empty renderer", typeof(TacticalCombatRenderer));
            try
            {
                ITacticalCombatArrivalPresentation presentation =
                    root.GetComponent<TacticalCombatRenderer>();
                presentation.ResetArrival(42);
                presentation.SetTransform(
                    42,
                    System.Numerics.Vector4.Zero,
                    System.Numerics.Vector4.UnitZ
                );
                presentation.Refresh(42);

                Assert.AreEqual(0, root.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrepareAsync_PlanetDisabled_SkipsImageAndItsRandomDraws()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            TacticalCombatSceneryRandom expectedRandom = new(
                new LinearCongruentialRandomProvider(42),
                () => 1
            );
            Vector3N expectedStation = expectedRandom.NextPosition(100, -2, 0.2f);

            yield return PrepareRenderer(
                encounter,
                pack,
                new UserVideoSettings { ShowPlanet = false }
            );

            Transform[] objects = view.Renderer.GetComponentsInChildren<Transform>(true);
            Assert.IsFalse(objects.Any(item => item.name == "Planet"));
            Assert.AreEqual(
                new Vector3(expectedStation.X, expectedStation.Y, expectedStation.Z),
                objects.Single(item => item.name == "CSEM015").localPosition
            );
        }

        [UnityTest]
        public IEnumerator ShowImpact_PyrotechnicsDisabled_DoesNotCreateImpactOrRetainDestroyedModel()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(
                encounter,
                pack,
                new UserVideoSettings { ShowPyro = false }
            );
            TacticalCombatUnit unit = encounter.Units.First(item =>
                !item.IsFighter && !item.IsBattleStation
            );
            int childCount = view.Renderer.GetComponentsInChildren<Transform>(true).Length;

            view.Renderer.ShowImpact(
                unit,
                TacticalWeaponType.Turbolaser,
                TacticalCombatImpactKind.Shield
            );
            view.Renderer.ShowDestruction(unit);
            unit.Status = TacticalCombatUnitStatus.Destroyed;
            view.Renderer.RenderUnits();

            Transform[] objects = view.Renderer.GetComponentsInChildren<Transform>(true);
            Assert.AreEqual(childCount, objects.Length);
            Assert.IsFalse(
                objects.Single(item => item.name == unit.InstanceID).gameObject.activeSelf
            );
        }

        [UnityTest]
        public IEnumerator PrepareAsync_ReducedDetail_NeverDisplaysNearFighterSprite()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(
                encounter,
                pack,
                new UserVideoSettings { HighDetail = false }
            );
            TacticalCombatUnit fighter = encounter.Units.Single(item => item.IsFighter);
            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == fighter.InstanceID);
            fighter.Position = new Vector3N(
                view.Renderer.Camera.transform.localPosition.x,
                view.Renderer.Camera.transform.localPosition.y,
                view.Renderer.Camera.transform.localPosition.z
            );

            view.Renderer.RenderUnits();

            Assert.AreSame(
                assets.GetTexture(
                    fighter.Definition.FighterSprites.MiddleImages[
                        encounter.GetRequestedGroup(fighter).FighterCategory
                    ]
                ),
                root.GetComponentInChildren<MeshRenderer>().sharedMaterial.mainTexture
            );
        }

        [UnityTest]
        public IEnumerator Advance_CapturedArrival_HoldsBattleClockAndReleasesPresentation()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            assets = new ContentAssets(pack.FileResolver);
            models = new ContentModelCache(assets);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/StrategyView/TacticalCombat.prefab"
            );
            view = Object.Instantiate(prefab).GetComponent<TacticalCombatView>();
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
            TacticalCombatUnit unit = encounter.Units.First(item =>
                item.Side == encounter.EnteringSide && !item.IsFighter
            );
            Vector3N originalPosition = unit.Position;
            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == unit.InstanceID);

            controller.Advance(0.5f);
            controller.Advance(0.5f);

            Assert.IsTrue(encounter.IsArriving);
            Assert.IsTrue(view.Renderer.IsPresentingArrival);
            Assert.IsFalse(encounter.IsDirectControlEnabled);
            Assert.AreEqual(3, encounter.SystemTime);
            Assert.AreEqual(originalPosition, unit.Position);
            Assert.AreNotEqual(
                new Vector3(unit.Position.X, unit.Position.Y, unit.Position.Z),
                root.localPosition
            );
            Image pause = view.transform.Find("Canvas/Frame/Pause").GetComponent<Image>();
            Assert.AreSame(
                assets.GetSprite(
                    encounter.Definition.HudImages.Single(item => item.Key == "pause-down").Path
                ),
                pause.sprite
            );

            for (int frame = 0; frame < 7; frame++)
                controller.Advance(0.5f);

            Assert.IsFalse(encounter.IsArriving);
            Assert.IsFalse(view.Renderer.IsPresentingArrival);
            Assert.IsTrue(encounter.IsDirectControlEnabled);
            Assert.AreEqual(
                new Vector3(unit.Position.X, unit.Position.Y, unit.Position.Z),
                root.localPosition
            );
        }

        [UnityTest]
        public IEnumerator PrepareAsync_HolocubeDisabled_RetainsEncounterOptionSnapshot()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            UserVideoSettings settings = new() { ShowHolocube = false };
            yield return PrepareRenderer(encounter, pack, settings);
            Transform cube = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "Holocube");

            settings.ShowHolocube = true;
            view.Renderer.RenderUnits();

            MeshRenderer[] boundaries = cube.GetComponentsInChildren<MeshRenderer>(true);
            Assert.AreEqual(2, boundaries.Length);
            Assert.IsTrue(boundaries.All(renderer => !renderer.enabled));
        }

        [UnityTest]
        public IEnumerator Focus_StationFrame_AimsAtBattleOriginWithoutMovingItsBillboard()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit station = encounter.Units.Single(item => item.IsBattleStation);
            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == station.InstanceID);
            Vector3 billboardPosition = root.position;
            Vector3 cameraPosition = view.Renderer.Camera.transform.position;

            view.Renderer.Focus(station);

            Vector3 direction = (root.parent.position - cameraPosition).normalized;
            Assert.That(
                Vector3.Dot(direction, view.Renderer.Camera.transform.forward),
                Is.EqualTo(1).Within(0.0001f)
            );
            Assert.AreEqual(cameraPosition, view.Renderer.Camera.transform.position);
            Assert.AreEqual(billboardPosition, root.position);
        }

        [UnityTest]
        public IEnumerator ShowShot_StationTarget_UsesLogicalFrameWithoutMovingBackgroundBillboard()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit station = encounter.Units.Single(item => item.IsBattleStation);
            TacticalCombatUnit source = encounter.Units.First(item =>
                !item.IsBattleStation && !item.IsFighter
            );
            Transform stationRoot = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == station.InstanceID);
            Transform sourceRoot = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == source.InstanceID);
            Vector3 billboardPosition = stationRoot.localPosition;
            Assert.That(billboardPosition.sqrMagnitude, Is.GreaterThan(1));

            view.Renderer.ShowShot(
                new TacticalCombatShot(source, station, TacticalWeaponType.LaserCannon, 1),
                0
            );
            view.Renderer.AdvanceEffects(.5f);

            Transform beam = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "Weapon beam");
            Assert.That(
                Vector3.Distance(sourceRoot.localPosition * .5f, beam.localPosition),
                Is.LessThan(.0001f)
            );
            Assert.AreEqual(billboardPosition, stationRoot.localPosition);
        }

        [UnityTest]
        public IEnumerator Focus_ArrivalTransform_AimsAtLiveRenderPoseWithoutMovingSimulation()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit unit = encounter.Units.First(item =>
                !item.IsFighter && !item.IsBattleStation
            );
            Vector3N before = unit.Position;
            view.Renderer.SetArrivalActive(true);
            view.Renderer.ResetArrival(unit.Index);
            view.Renderer.SetTransform(
                unit.Index,
                new System.Numerics.Vector4(14, 7, -350, -1),
                new System.Numerics.Vector4(unit.Heading, -1)
            );

            view.Renderer.Focus(unit);

            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == unit.InstanceID);
            Vector3 direction = (
                root.position - view.Renderer.Camera.transform.position
            ).normalized;
            Assert.That(
                Vector3.Dot(direction, view.Renderer.Camera.transform.forward),
                Is.EqualTo(1).Within(0.0001f)
            );
            Assert.AreEqual(before, unit.Position);
        }

        [UnityTest]
        public IEnumerator ShowCategory_CapitalFrame_UsesCachedMiddleBoundsAndLivePresentationPose()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit unit = encounter
                .GetObjectGroup(encounter.PlayerSide, 0)
                .PrimaryMember;
            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == unit.InstanceID);
            Vector3 bounds = root.Find("Selection box")
                .GetComponent<MeshFilter>()
                .sharedMesh.bounds.size;
            float span = Mathf.Max(bounds.x, bounds.y);

            Assert.IsTrue(view.Renderer.ShowCategory(0));

            Assert.That(
                Vector3.Distance(
                    root.TransformPoint(new Vector3(-span, span, span + span)),
                    view.Renderer.Camera.transform.position
                ),
                Is.LessThan(0.003f)
            );
            Assert.That(
                Vector3.Dot(view.Renderer.Camera.transform.forward, -root.forward),
                Is.EqualTo(1).Within(0.0001f)
            );
            Assert.AreEqual(0.1f, view.Renderer.Camera.nearClipPlane);
            Assert.AreEqual(TacticalCombatSelectionKind.Empty, encounter.Selection.Kind);
            unit.Position = new Vector3N(18, 4, -12);
            unit.Heading = Vector3N.UnitX;

            view.Renderer.RenderUnits();

            Assert.That(
                Vector3.Distance(
                    root.TransformPoint(new Vector3(-span, span, span + span)),
                    view.Renderer.Camera.transform.position
                ),
                Is.LessThan(0.003f)
            );
        }

        [UnityTest]
        public IEnumerator ShowImpact_CategoryCloseUp_SuppressesPrimaryWithoutSuppressingOrdinaryFocus()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit unit = encounter
                .GetObjectGroup(encounter.PlayerSide, 0)
                .PrimaryMember;
            view.Renderer.Focus(unit);
            view.Renderer.ShowImpact(
                unit,
                TacticalWeaponType.LaserCannon,
                TacticalCombatImpactKind.Shield
            );
            view.Renderer.AdvanceEffects(0);
            MeshRenderer impact = view
                .Renderer.GetComponentsInChildren<MeshRenderer>(true)
                .Single(item => item.name == "Impact");
            Assert.IsTrue(impact.enabled);

            Assert.IsTrue(view.Renderer.ShowCategory(0));
            view.Renderer.AdvanceEffects(0);

            Assert.IsFalse(impact.enabled);
            view.Renderer.ShowImpact(
                unit,
                TacticalWeaponType.LaserCannon,
                TacticalCombatImpactKind.Shield
            );
            Assert.IsFalse(impact.enabled);
            view.Renderer.Focus(unit);
            view.Renderer.ShowImpact(
                unit,
                TacticalWeaponType.LaserCannon,
                TacticalCombatImpactKind.Hull
            );
            view.Renderer.AdvanceEffects(0);
            Assert.IsTrue(impact.enabled);
        }

        [UnityTest]
        public IEnumerator ShowImpact_InspectedPrimaryChangesBeforeCameraRefresh_ResolvesLiveCategoryOwner()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(
                pack.GameData,
                allCapitalsPlayer: true
            );
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit previous = encounter
                .GetObjectGroup(encounter.PlayerSide, 0)
                .PrimaryMember;
            TacticalCombatUnit current = encounter
                .GetObjectGroup(encounter.PlayerSide, 1)
                .PrimaryMember;
            Assert.IsTrue(view.Renderer.ShowCategory(0));
            Vector3 cameraPosition = view.Renderer.Camera.transform.position;
            view.Renderer.ShowImpact(
                previous,
                TacticalWeaponType.LaserCannon,
                TacticalCombatImpactKind.Shield
            );
            encounter.Selection.QueueObjectCategory(1);
            encounter.Selection.ProcessPendingCommands();
            Assert.AreSame(current, encounter.Selection.PrimaryUnit);

            view.Renderer.ShowImpact(
                current,
                TacticalWeaponType.LaserCannon,
                TacticalCombatImpactKind.Shield
            );

            Assert.IsFalse(
                view.Renderer.GetComponentsInChildren<MeshRenderer>(true)
                    .Any(item => item.name == "Impact" && item.enabled)
            );
            view.Renderer.ShowDestruction(previous);
            Assert.AreEqual(
                1,
                view.Renderer.GetComponentsInChildren<MeshRenderer>(true)
                    .Count(item => item.name == "Impact" && item.enabled)
            );
            Assert.AreEqual(cameraPosition, view.Renderer.Camera.transform.position);
        }

        [UnityTest]
        public IEnumerator ShowCategory_AppliedRouteAndTarget_PrefersAppliedPointOverRequestedChanges()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatGroup group = encounter.GetObjectGroup(encounter.PlayerSide, 0);
            group.SetTarget(
                encounter.Units.First(item =>
                    item.Side != encounter.PlayerSide && !item.IsBattleStation
                )
            );
            Vector3N point = encounter.Navigation.Waypoints[0];
            group.SetWaypoint(point);
            encounter.SetWaypoint(group, encounter.Navigation.Waypoints[1]);

            Assert.IsTrue(view.Renderer.ShowCategory(0));

            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == group.PrimaryMember.InstanceID);
            Vector3 destination = root.parent.TransformPoint(
                new Vector3(point.X, point.Y, point.Z)
            );
            Vector3 direction = (destination - view.Renderer.Camera.transform.position).normalized;
            Assert.That(
                Vector3.Dot(view.Renderer.Camera.transform.forward, direction),
                Is.EqualTo(1).Within(0.0001f)
            );
            Assert.AreEqual(point, group.PrimaryWaypoint);
        }

        [UnityTest]
        public IEnumerator ShowCategory_MembershipTransferPending_UsesAppliedLeader()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(
                pack.GameData,
                allCapitalsPlayer: true
            );
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatGroup group = encounter.GetObjectGroup(encounter.PlayerSide, 0);
            TacticalCombatUnit applied = group.PrimaryMember;
            TacticalCombatUnit requested = encounter
                .GetObjectGroup(encounter.PlayerSide, 1)
                .PrimaryMember;
            encounter.AddGroupMember(group, requested);
            encounter.RemoveGroupMember(group, applied);

            Assert.IsTrue(view.Renderer.ShowCategory(0));

            Assert.AreSame(applied, group.PrimaryMember);
            Assert.AreSame(requested, group.RequestedLeader);
            AssertCategoryOffset(applied);
        }

        [UnityTest]
        public IEnumerator ShowCategory_OtherObjectCategoryInspected_UsesGlobalInspectedObject()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(
                pack.GameData,
                allCapitalsPlayer: true
            );
            yield return PrepareRenderer(encounter, pack);
            encounter.Selection.QueueObjectCategory(0);
            encounter.Selection.ProcessPendingCommands();
            TacticalCombatUnit inspected = encounter.Selection.PrimaryUnit;

            Assert.IsTrue(view.Renderer.ShowCategory(1));

            Assert.AreSame(
                encounter.GetObjectGroup(encounter.PlayerSide, 0).PrimaryMember,
                inspected
            );
            Assert.AreNotSame(
                encounter.GetObjectGroup(encounter.PlayerSide, 1).PrimaryMember,
                inspected
            );
            AssertCategoryOffset(inspected);
        }

        [UnityTest]
        public IEnumerator ShowCategory_CategoryCleared_ResumesPrimaryViewWithoutChangingSelection()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            view.Renderer.ApplyCameraCommand(TacticalCombatCameraCommand.ZoomIn);
            Vector3 position = view.Renderer.Camera.transform.position;
            Quaternion rotation = view.Renderer.Camera.transform.rotation;
            float fieldOfView = view.Renderer.Camera.fieldOfView;
            Assert.IsFalse(view.Renderer.ShowCategory(7));
            Assert.IsTrue(view.Renderer.ShowCategory(0));

            encounter.SetObjectGroup(encounter.PlayerSide, 0, null);
            view.Renderer.RenderUnits();

            Assert.AreEqual(position, view.Renderer.Camera.transform.position);
            Assert.That(
                Quaternion.Angle(rotation, view.Renderer.Camera.transform.rotation),
                Is.LessThan(0.0001f)
            );
            Assert.AreEqual(fieldOfView, view.Renderer.Camera.fieldOfView);
            Assert.AreEqual(1, view.Renderer.Camera.nearClipPlane);
            Assert.AreEqual(TacticalCombatSelectionKind.Empty, encounter.Selection.Kind);
        }

        [UnityTest]
        public IEnumerator ShowCategory_FighterWithoutModelBounds_UsesExactPresentationOrigin()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData, fighterCategory: 0);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit fighter = encounter.Units.Single(item => item.IsFighter);
            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == fighter.InstanceID);

            Assert.IsTrue(view.Renderer.ShowCategory(8));

            Assert.AreEqual(root.position, view.Renderer.Camera.transform.position);
            Assert.That(
                Vector3.Dot(view.Renderer.Camera.transform.forward, -root.forward),
                Is.EqualTo(1).Within(0.0001f)
            );
            Assert.AreEqual(0.1f, view.Renderer.Camera.nearClipPlane);
        }

        [UnityTest]
        public IEnumerator SetInspection_CapitalPreview_EnlargesVisibleHullWithoutOverlappingStatus()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareController(encounter, pack);
            TacticalCombatUnit unit = encounter.Units.First(item =>
                !item.IsFighter && !item.IsBattleStation
            );
            view.SetPanel(TacticalCombatHudPanel.SingleObject);
            view.SetInspection(unit, new TacticalCombatInspection(encounter).ReadUnit(unit));
            Transform panel = view.transform.Find("Canvas/Frame/Right panel");
            RawImage preview = panel.Find("Object preview").GetComponent<RawImage>();
            RectInt bounds = assets.GetTextureContentBounds((Texture2D)preview.texture);
            RectInt displayed = UILayout.GetSourceRect(preview.rectTransform);
            RectInt assignment = UILayout.GetSourceRect((RectTransform)panel.Find("Assignment"));
            RectInt hull = UILayout.GetSourceRect((RectTransform)panel.Find("Hull"));

            Assert.Greater(displayed.width, bounds.width);
            Assert.Greater(displayed.height, bounds.height);
            Assert.LessOrEqual(displayed.yMax, assignment.yMin);
            Assert.LessOrEqual(assignment.yMax, hull.yMin);
            Assert.That(
                (float)displayed.width / displayed.height,
                Is.EqualTo((float)bounds.width / bounds.height).Within(0.05f)
            );
            Assert.AreEqual((float)bounds.width / preview.texture.width, preview.uvRect.width);
            Assert.AreEqual((float)bounds.height / preview.texture.height, preview.uvRect.height);
            Assert.IsTrue(panel.Find("Object preview backdrop").gameObject.activeInHierarchy);
            view.SetPanel(TacticalCombatHudPanel.ObjectGroup);
            RectInt name = UILayout.GetSourceRect((RectTransform)panel.Find("Name"));
            Assert.GreaterOrEqual(displayed.yMin, name.yMax);
            foreach (TacticalCombatHudControl control in TacticalCombatHudLayout.Controls)
                if (
                    control.Panel
                        is TacticalCombatHudPanel.SingleObject
                            or TacticalCombatHudPanel.ObjectGroup
                    && control.Control
                        is TacticalCombatControl.PreviousObject
                            or TacticalCombatControl.NextObject
                )
                    Assert.IsFalse(displayed.Overlaps(control.Bounds), control.Control.ToString());
            view.SetPanel(TacticalCombatHudPanel.FighterGroup);
            Assert.IsFalse(panel.Find("Object preview backdrop").gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator HandleKeyboardEvent_Wasd_RotatesAndTiltsBattleCamera()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            yield return PrepareController(CreateEncounter(pack.GameData), pack);
            foreach (KeyCode key in new[] { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D })
            {
                Quaternion before = view.Renderer.Camera.transform.rotation;
                Assert.IsTrue(
                    view.HandleKeyboardEvent(new Event { type = EventType.KeyDown, keyCode = key })
                );
                Assert.IsTrue(
                    view.HandleKeyboardEvent(new Event { type = EventType.KeyUp, keyCode = key })
                );
                Assert.Greater(
                    Quaternion.Angle(before, view.Renderer.Camera.transform.rotation),
                    1,
                    key.ToString()
                );
            }
        }

        [UnityTest]
        public IEnumerator HandleKeyboardEvent_EscapeAndShiftEscape_RequestsMenuOncePerPress()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            yield return PrepareController(CreateEncounter(pack.GameData), pack);
            int requests = 0;
            controller.MenuControlRequested += control =>
            {
                Assert.AreEqual(TacticalCombatControl.OpenOptions, control);
                requests++;
            };
            foreach (
                EventModifiers modifiers in new[] { EventModifiers.None, EventModifiers.Shift }
            )
            {
                int before = requests;
                for (int repeat = 0; repeat < 3; repeat++)
                    Assert.IsTrue(
                        view.HandleKeyboardEvent(
                            new Event
                            {
                                type = EventType.KeyDown,
                                keyCode = KeyCode.Escape,
                                modifiers = modifiers,
                            }
                        )
                    );
                Assert.AreEqual(before + 1, requests);
                Assert.IsTrue(
                    view.HandleKeyboardEvent(
                        new Event
                        {
                            type = EventType.KeyUp,
                            keyCode = KeyCode.Escape,
                            modifiers = modifiers,
                        }
                    )
                );
            }
        }

        [UnityTest]
        public IEnumerator HandleKeyboardEvent_EscapeHeldAcrossMenuClose_WaitsForRelease()
        {
            InputTestFixture input = new();
            input.Setup();
            try
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                ContentPack pack = ContentPackLoader.OpenActive();
                yield return PrepareController(CreateEncounter(pack.GameData), pack);
                int requests = 0;
                controller.MenuControlRequested += _ => requests++;
                controller.SetApplicationMenuOpen(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                controller.SetApplicationMenuOpen(false);

                Assert.IsTrue(
                    view.HandleKeyboardEvent(
                        new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }
                    )
                );
                Assert.Zero(requests);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                Assert.IsTrue(
                    view.HandleKeyboardEvent(
                        new Event { type = EventType.KeyUp, keyCode = KeyCode.Escape }
                    )
                );
                Assert.IsTrue(
                    view.HandleKeyboardEvent(
                        new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }
                    )
                );
                Assert.AreEqual(1, requests);
            }
            finally
            {
                input.TearDown();
            }
        }

        [UnityTest]
        public IEnumerator HandleKeyboardEvent_ApplicationMenuOpen_BlocksNativeCameraUntilInputOwnershipReturns()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareController(encounter, pack);
            for (int frame = 0; frame < 9; frame++)
                controller.Advance(0.5f);
            float before = view.Renderer.Camera.fieldOfView;
            bool pausedBefore = encounter.IsPaused;

            controller.SetApplicationMenuOpen(true);
            Assert.IsFalse(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.PageUp }
                )
            );
            Assert.IsFalse(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.F1 }
                )
            );
            Assert.AreEqual(before, view.Renderer.Camera.fieldOfView);
            Assert.AreEqual(pausedBefore, encounter.IsPaused);
            controller.SetApplicationMenuOpen(false);

            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.PageUp }
                )
            );
            Assert.Less(view.Renderer.Camera.fieldOfView, before);
            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyUp, keyCode = KeyCode.PageUp }
                )
            );
        }

        [UnityTest]
        public IEnumerator RenderUnits_DepartingParticipant_UsesTemporaryPoseWithoutMovingSimulation()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateWithdrawalEncounter(pack.GameData);
            TacticalCombatUnit unit = encounter.GetUnit("departing");
            yield return PrepareRenderer(encounter, pack);
            Assert.IsTrue(encounter.BeginWithdrawal(encounter.PlayerSide));
            for (
                int frame = 0;
                frame < 1000 && unit.Status != TacticalCombatUnitStatus.EnteringHyperspace;
                frame++
            )
                encounter.Advance(100);
            Assert.AreEqual(TacticalCombatUnitStatus.EnteringHyperspace, unit.Status);
            Assert.Less(unit.HyperspaceMilliseconds, 4000);
            Vector3N position = unit.Position;
            TacticalCombatDeparturePose expected = TacticalCombatDeparturePose.Evaluate(
                unit.Index,
                unit.Definition.PresentationIndex,
                position,
                encounter.GetSide(unit.Side).Forward,
                unit.HyperspaceMilliseconds
            );

            view.Renderer.RenderUnits();

            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == unit.InstanceID);
            Assert.AreEqual(
                new Vector3(expected.Position.X, expected.Position.Y, expected.Position.Z),
                root.localPosition
            );
            Assert.AreEqual(position, unit.Position);
            Assert.AreNotEqual(new Vector3(position.X, position.Y, position.Z), root.localPosition);
        }

        [UnityTest]
        public IEnumerator ShowShotAndImpact_DepartingTarget_FollowsPresentationWithoutMovingSimulation()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit source = encounter.GetUnit("CSEM006");
            TacticalCombatUnit target = encounter.GetUnit("ALCS008");
            Vector3N canonical = target.Position;
            target.Status = TacticalCombatUnitStatus.EnteringHyperspace;
            target.HyperspaceMilliseconds = 1500;

            view.Renderer.ShowShot(
                new TacticalCombatShot(source, target, TacticalWeaponType.LaserCannon, 50),
                1
            );
            view.Renderer.ShowImpact(
                target,
                TacticalWeaponType.LaserCannon,
                TacticalCombatImpactKind.Shield
            );
            view.Renderer.AdvanceEffects(.5f);

            Transform[] objects = view.Renderer.GetComponentsInChildren<Transform>(true);
            Transform sourceRoot = objects.Single(item => item.name == source.InstanceID);
            Transform targetRoot = objects.Single(item => item.name == target.InstanceID);
            Transform beam = objects.Single(item => item.name == "Weapon beam");
            Assert.AreNotEqual(
                new Vector3(canonical.X, canonical.Y, canonical.Z),
                targetRoot.localPosition
            );
            Assert.AreEqual(
                (sourceRoot.localPosition + targetRoot.localPosition) * .5f,
                beam.localPosition
            );
            Vector3 cameraPosition = targetRoot.parent.InverseTransformPoint(
                view.Renderer.Camera.transform.position
            );
            Vector3 towardCamera = (cameraPosition - targetRoot.localPosition).normalized;
            Vector3 expectedImpact = targetRoot.parent.TransformPoint(
                targetRoot.localPosition + towardCamera
            );
            Assert.Less(
                Vector3.Distance(
                    expectedImpact,
                    objects.Single(item => item.name == "Impact").position
                ),
                .0001f
            );
            Assert.AreEqual(canonical, target.Position);
        }

        [UnityTest]
        public IEnumerator HandleKeyboardEvent_CategoryRepeats_PostsEveryPressAndIgnoresRelease()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareController(encounter, pack);
            for (int frame = 0; frame < 9; frame++)
                controller.Advance(0.5f);
            TacticalCombatGroup group = encounter.GetGroup(
                encounter.Units.First(unit => unit.Side == encounter.PlayerSide && !unit.IsFighter)
            );
            encounter.SetObjectGroup(encounter.PlayerSide, 0, group);
            TacticalCombatHudButton button = view.GetComponentsInChildren<TacticalCombatHudButton>(
                    true
                )
                .Single(item => item.name == nameof(TacticalCombatControl.ObjectGroup1));
            int presses = 0;
            button.onClick.AddListener(() => presses++);
            view.SetControlEnabled(TacticalCombatControl.ObjectGroup1, false);

            for (int index = 0; index < 3; index++)
                Assert.IsTrue(
                    view.HandleKeyboardEvent(
                        new Event { type = EventType.KeyDown, keyCode = KeyCode.Alpha1 }
                    )
                );
            Sprite pressed = ((Image)button.targetGraphic).overrideSprite;
            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyUp, keyCode = KeyCode.Alpha1 }
                )
            );

            Assert.AreEqual(3, presses);
            Assert.AreSame(pressed, ((Image)button.targetGraphic).overrideSprite);
            controller.Advance(0);
            Assert.AreEqual(TacticalCombatSelectionKind.ObjectGroup, encounter.Selection.Kind);
            Assert.AreSame(group, encounter.Selection.Group);
        }

        [UnityTest]
        public IEnumerator HandleKeyboardEvent_Arrival_PreservesCameraWithoutAdmittingDirectCommands()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareController(encounter, pack);
            controller.Advance(0);
            Assert.IsTrue(encounter.IsArriving);
            float before = view.Renderer.Camera.fieldOfView;

            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.PageUp }
                )
            );
            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyUp, keyCode = KeyCode.PageUp }
                )
            );
            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.Space }
                )
            );
            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.Alpha1 }
                )
            );
            controller.Advance(0);

            Assert.Less(view.Renderer.Camera.fieldOfView, before);
            Assert.AreEqual(TacticalCombatSelectionKind.Empty, encounter.Selection.Kind);
            Assert.IsFalse(encounter.IsDirectControlEnabled);
        }

        [UnityTest]
        public IEnumerator HandleKeyboardEvent_HeldTab_TraversesOnceUntilReleased()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareController(encounter, pack);
            for (int frame = 0; frame < 9; frame++)
                controller.Advance(0.5f);
            TacticalCombatUnit unit = encounter.Units.First(item =>
                item.Side == encounter.PlayerSide && !item.IsFighter
            );
            encounter.Selection.QueueObject(unit, false);
            controller.Advance(0);
            TacticalCombatHudButton button = view.GetComponentsInChildren<TacticalCombatHudButton>(
                    true
                )
                .Single(item =>
                    item.name == nameof(TacticalCombatControl.NextObject)
                    && item.gameObject.activeInHierarchy
                );
            Sprite before = ((Image)button.targetGraphic).overrideSprite;
            int presses = 0;
            button.onClick.AddListener(() => presses++);

            for (int index = 0; index < 3; index++)
            {
                Assert.IsTrue(
                    view.HandleKeyboardEvent(
                        new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab }
                    )
                );
                Assert.AreSame(before, ((Image)button.targetGraphic).overrideSprite);
            }
            Assert.AreEqual(1, presses);
            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyUp, keyCode = KeyCode.Tab }
                )
            );

            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab }
                )
            );
            Assert.AreEqual(2, presses);
        }

        [UnityTest]
        public IEnumerator HandleKeyboardEvent_TabHeldAcrossApplicationMenu_WaitsForRelease()
        {
            InputTestFixture input = new();
            input.Setup();
            try
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                ContentPack pack = ContentPackLoader.OpenActive();
                TacticalCombatSession encounter = CreateEncounter(pack.GameData);
                yield return PrepareController(encounter, pack);
                TacticalCombatHudButton button = SelectInspectionButton(encounter);
                int presses = 0;
                button.onClick.AddListener(() => presses++);
                controller.SetApplicationMenuOpen(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                InputSystem.Update();
                controller.SetApplicationMenuOpen(false);

                Assert.IsTrue(
                    view.HandleKeyboardEvent(
                        new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab }
                    )
                );
                Assert.AreEqual(0, presses);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                Assert.IsTrue(
                    view.HandleKeyboardEvent(
                        new Event { type = EventType.KeyUp, keyCode = KeyCode.Tab }
                    )
                );
                Assert.IsTrue(
                    view.HandleKeyboardEvent(
                        new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab }
                    )
                );
                Assert.AreEqual(1, presses);
            }
            finally
            {
                input.TearDown();
            }
        }

        [UnityTest]
        public IEnumerator HandleKeyboardEvent_TabReleasedDuringApplicationMenu_AdmitsNextPress()
        {
            InputTestFixture input = new();
            input.Setup();
            try
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                ContentPack pack = ContentPackLoader.OpenActive();
                TacticalCombatSession encounter = CreateEncounter(pack.GameData);
                yield return PrepareController(encounter, pack);
                TacticalCombatHudButton button = SelectInspectionButton(encounter);
                int presses = 0;
                button.onClick.AddListener(() => presses++);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                InputSystem.Update();
                Assert.IsTrue(
                    view.HandleKeyboardEvent(
                        new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab }
                    )
                );
                Assert.AreEqual(1, presses);
                controller.SetApplicationMenuOpen(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                controller.SetApplicationMenuOpen(false);

                Assert.IsTrue(
                    view.HandleKeyboardEvent(
                        new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab }
                    )
                );
                Assert.AreEqual(2, presses);
            }
            finally
            {
                input.TearDown();
            }
        }

        [UnityTest]
        public IEnumerator ClearKeyboardArtwork_HeldRepeatingPointer_RetainsRepeatOwnership()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            yield return PrepareRenderer(CreateEncounter(pack.GameData), pack);
            TacticalCombatHudButton button = view.GetComponentsInChildren<TacticalCombatHudButton>(
                    true
                )
                .Single(item => item.name == nameof(TacticalCombatControl.CameraZoomIn));
            button.SetRepeatInterval(10);
            int repeats = 0;
            button.onClick.AddListener(() => repeats++);
            PointerEventData pointer = new(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };
            button.OnPointerDown(pointer);

            button.ClearKeyboardArtwork();
            yield return new WaitForSecondsRealtime(0.4f);

            Assert.Greater(repeats, 0);
            button.OnPointerUp(pointer);
        }

        [UnityTest]
        public IEnumerator PointerClick_ChangingEmpireObjectGroups_UpdatesSelectionAndInspection()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(
                pack.GameData,
                playerSide: CombatSide.Defender
            );
            encounter.IsPaused = true;
            yield return PrepareController(encounter, pack);
            for (int frame = 0; frame < 9; frame++)
                controller.Advance(0.5f);
            Assert.IsFalse(encounter.IsArriving);
            TacticalCombatGroup[] groups = encounter
                .Units.Where(unit => unit.Side == encounter.PlayerSide && !unit.IsFighter)
                .Select(encounter.GetGroup)
                .Where(group => group != null)
                .Take(2)
                .ToArray();
            Assert.AreEqual(2, groups.Length);
            for (int slot = 0; slot < groups.Length; slot++)
                encounter.SetObjectGroup(encounter.PlayerSide, slot, groups[slot]);
            controller.Advance(0);
            GameObject events = new("Group selection events", typeof(EventSystem));
            try
            {
                Canvas.ForceUpdateCanvases();
                foreach (bool paused in new[] { true, false })
                {
                    encounter.IsPaused = paused;
                    foreach (int slot in new[] { 0, 1, 0, 1 })
                    {
                        TacticalCombatControl control = TacticalCombatControl.ObjectGroup1 + slot;
                        TacticalCombatHudButton button =
                            view.GetComponentsInChildren<TacticalCombatHudButton>(true)
                                .Single(item => item.name == control.ToString());

                        ClickGroupButton(button, events.GetComponent<EventSystem>());
                        controller.Advance(0);

                        Assert.AreEqual(
                            TacticalCombatSelectionKind.ObjectGroup,
                            encounter.Selection.Kind
                        );
                        Assert.AreEqual(slot, encounter.Selection.Category);
                        Assert.AreSame(groups[slot], encounter.Selection.Group);
                        string name = groups[slot].RequestedMembers[0].DisplayName;
                        Assert.IsTrue(
                            view.GetComponentsInChildren<TextMeshProUGUI>(true)
                                .Any(label =>
                                    label.gameObject.activeInHierarchy && label.text == name
                                )
                        );
                    }
                }
            }
            finally
            {
                Object.Destroy(events);
            }
        }

        [UnityTest]
        public IEnumerator PointerClick_EmptyEmpireObjectSlot_PreservesOccupiedGroup() =>
            VerifyEmptyEmpireSlot(false);

        [UnityTest]
        public IEnumerator HandleKeyboardEvent_EmptyEmpireObjectSlot_PreservesOccupiedGroup() =>
            VerifyEmptyEmpireSlot(true);

        [UnityTest]
        public IEnumerator PointerClick_EmptyEmpireSlot_AssignsSelectedCapital()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(
                pack.GameData,
                playerSide: CombatSide.Defender
            );
            yield return PrepareEmpireGroupSelection(encounter, pack);
            TacticalCombatUnit unit = encounter.GetUnit("CSEM006");
            encounter.SetObjectGroup(encounter.PlayerSide, 7, null);
            encounter.Selection.QueueObject(unit);
            controller.Advance(0);
            Assert.AreEqual(TacticalCombatSelectionKind.SingleObject, encounter.Selection.Kind);
            GameObject events = new("Single assignment events", typeof(EventSystem));
            try
            {
                Canvas.ForceUpdateCanvases();
                TacticalCombatHudButton button =
                    view.GetComponentsInChildren<TacticalCombatHudButton>(true)
                        .Single(item => item.name == nameof(TacticalCombatControl.ObjectGroup8));

                ClickGroupButton(button, events.GetComponent<EventSystem>());
                controller.Advance(0);

                Assert.AreEqual(TacticalCombatSelectionKind.ObjectGroup, encounter.Selection.Kind);
                Assert.AreEqual(7, encounter.Selection.Category);
                CollectionAssert.AreEqual(
                    new[] { unit },
                    encounter.Selection.Group.RequestedMembers
                );
            }
            finally
            {
                Object.Destroy(events);
            }
        }

        [UnityTest]
        public IEnumerator PointerClick_MultipleEmpireSelection_AdmitsAssignmentBeforeAuthorityReturns()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(
                pack.GameData,
                playerSide: CombatSide.Defender
            );
            yield return PrepareEmpireGroupSelection(encounter, pack);
            TacticalCombatUnit first = encounter.GetUnit("CSEM006");
            TacticalCombatUnit second = encounter.GetUnit("CSAL009");
            encounter.SetObjectGroup(encounter.PlayerSide, 7, null);
            encounter.Selection.QueueObject(first);
            encounter.Selection.QueueObject(second, true);
            controller.Advance(0);
            Assert.AreEqual(TacticalCombatSelectionKind.MultipleObjects, encounter.Selection.Kind);
            encounter.SetAutomated(encounter.PlayerSide, true);
            controller.Advance(0);
            GameObject events = new("Multiple assignment events", typeof(EventSystem));
            try
            {
                Canvas.ForceUpdateCanvases();
                TacticalCombatHudButton button =
                    view.GetComponentsInChildren<TacticalCombatHudButton>(true)
                        .Single(item => item.name == nameof(TacticalCombatControl.ObjectGroup8));

                ClickGroupButton(button, events.GetComponent<EventSystem>());
                encounter.SetAutomated(encounter.PlayerSide, false);
                controller.Advance(0);

                Assert.AreEqual(TacticalCombatSelectionKind.ObjectGroup, encounter.Selection.Kind);
                Assert.AreEqual(7, encounter.Selection.Category);
                CollectionAssert.AreEqual(
                    new[] { first, second },
                    encounter.Selection.Group.RequestedMembers
                );
            }
            finally
            {
                Object.Destroy(events);
            }
        }

        [UnityTest]
        public IEnumerator PrepareAsync_TractorMembership_ShowsLockWithZeroRestraint()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData, gravityWell: false);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit unit = encounter.GetUnit("ALCS008");
            Assert.AreEqual(0, unit.TractorRestraint);

            unit.SetTractorSources(true);

            MeshRenderer loop = view
                .Renderer.GetComponentsInChildren<MeshRenderer>(true)
                .Single(renderer => renderer.name == "Lock");
            Assert.IsTrue(loop.enabled);
            TacticalCombatEffectDefinition definition = pack.GameData.TacticalCombat.Effects.Single(
                effect => effect.Kind == TacticalCombatEffectKind.TractorRestraint
            );
            Assert.AreSame(
                assets.GetTexture(definition.FramePaths[0]),
                loop.sharedMaterial.mainTexture
            );
            unit.SetTractorSources(false);
            Assert.IsFalse(loop.enabled);
        }

        [UnityTest]
        public IEnumerator PrepareAsync_GravityGenerator_IgnoresTractorAndShowsMovementLoop()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData, gravityWell: true);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit unit = encounter.GetUnit("ALCS008");
            int gravityRequests = 0;
            unit.GravityWellPresentationRequested += () => gravityRequests++;

            unit.SetTractorSources(true);
            Assert.IsFalse(
                view.Renderer.GetComponentsInChildren<Transform>(true)
                    .Any(item => item.name == "Lock")
            );
            for (int frame = 0; frame < 120; frame++)
                encounter.Advance(50);

            Assert.Greater(gravityRequests, 0);
            MeshRenderer[] loops = view
                .Renderer.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name == "Lock")
                .ToArray();
            Assert.IsNotEmpty(loops);
            TacticalCombatEffectDefinition definition = pack.GameData.TacticalCombat.Effects.Single(
                effect => effect.Kind == TacticalCombatEffectKind.GravityLock
            );
            Assert.IsTrue(
                loops.All(renderer =>
                    renderer.enabled
                    && ReferenceEquals(
                        assets.GetTexture(definition.FramePaths[0]),
                        renderer.sharedMaterial.mainTexture
                    )
                )
            );
        }

        [UnityTest]
        public IEnumerator PrepareAsync_PyrotechnicsDisabled_DoesNotCreateLockBillboards()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData, gravityWell: true);
            yield return PrepareRenderer(
                encounter,
                pack,
                new UserVideoSettings { ShowPyro = false }
            );
            TacticalCombatUnit unit = encounter.GetUnit("ALCS008");
            int gravityRequests = 0;
            unit.GravityWellPresentationRequested += () => gravityRequests++;
            unit.SetTractorSources(true);

            for (int frame = 0; frame < 120; frame++)
                encounter.Advance(50);

            Assert.Greater(gravityRequests, 0);
            Assert.IsFalse(
                view.Renderer.GetComponentsInChildren<Transform>(true)
                    .Any(item => item.name == "Lock")
            );
        }

        [UnityTest]
        public IEnumerator ConfirmPanel_UnchangedGroupEditors_PreservesMissionAndTarget()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareEmpireGroupSelection(encounter, pack);
            TacticalCombatGroup group = encounter.Groups.First(item =>
                item.Side == encounter.PlayerSide && !item.IsFighterGroup
            );
            TacticalCombatUnit target = encounter.Units.First(item =>
                item.Side != encounter.PlayerSide && !item.IsFighter && !item.IsBattleStation
            );
            encounter.SetObjectGroup(encounter.PlayerSide, 0, group);
            encounter.SetOrder(group, TacticalCombatOrder.AttackFighters);
            encounter.SetTarget(group, target);
            encounter.Selection.QueueObjectCategory(0);
            controller.Advance(0);
            Assert.AreSame(group, encounter.Selection.Group);

            foreach (
                TacticalCombatControl editor in new[]
                {
                    TacticalCombatControl.OpenOrders,
                    TacticalCombatControl.OpenTactics,
                }
            )
            {
                PressControl(editor);
                PressControl(TacticalCombatControl.ConfirmPanel);
                controller.Advance(0);

                Assert.AreEqual(TacticalCombatOrder.AttackFighters, group.Order);
                Assert.AreSame(target, group.RequestedTargets.Single());
                Assert.AreSame(group, encounter.Selection.Group);
            }
        }

        [UnityTest]
        public IEnumerator ConfirmPanel_AttackModeOnly_PreservesMissionAndTarget()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareEmpireGroupSelection(encounter, pack);
            TacticalCombatGroup group = encounter.Groups.First(item =>
                item.Side == encounter.PlayerSide && !item.IsFighterGroup
            );
            TacticalCombatUnit target = encounter.Units.First(item =>
                item.Side != encounter.PlayerSide && !item.IsFighter && !item.IsBattleStation
            );
            encounter.SetObjectGroup(encounter.PlayerSide, 0, group);
            encounter.SetOrder(group, TacticalCombatOrder.AttackFighters);
            encounter.SetTarget(group, target);
            encounter.Selection.QueueObjectCategory(0);
            controller.Advance(0);
            bool closeAttack = group.CloseAttack;

            PressControl(TacticalCombatControl.OpenTactics);
            PressControl(TacticalCombatControl.CloseAttack);
            PressControl(TacticalCombatControl.ConfirmPanel);
            controller.Advance(0);

            Assert.AreEqual(!closeAttack, group.CloseAttack);
            Assert.AreEqual(TacticalCombatOrder.AttackFighters, group.Order);
            Assert.AreSame(target, group.RequestedTargets.Single());
        }

        [UnityTest]
        public IEnumerator HandleControl_ImmediateResultWhilePaused_QueuesOneCompletion()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareEmpireGroupSelection(encounter, pack);
            int completed = 0;
            encounter.Completed += () => completed++;

            PressControl(TacticalCombatControl.OpenMenu);
            PressControl(TacticalCombatControl.ResolveImmediately);
            Assert.IsFalse(encounter.IsComplete);
            controller.Advance(0);

            Assert.IsTrue(encounter.IsComplete);
            Assert.IsTrue(encounter.ImmediateResultRequested);
            Assert.AreEqual(1, completed);
        }

        /// <summary>
        /// Activates one currently presented HUD control through its click event.
        /// </summary>
        /// <param name="control">The visible semantic control to press.</param>
        private void PressControl(TacticalCombatControl control)
        {
            TacticalCombatHudButton button = view.GetComponentsInChildren<TacticalCombatHudButton>(
                    true
                )
                .Single(item =>
                    item.name == control.ToString() && item.gameObject.activeInHierarchy
                );
            Assert.IsTrue(button.interactable);
            button.onClick.Invoke();
        }

        /// <summary>
        /// Selects a controllable capital and returns its active forward inspection control.
        /// </summary>
        /// <param name="encounter">The prepared encounter to inspect.</param>
        /// <returns>The active forward inspection control.</returns>
        private TacticalCombatHudButton SelectInspectionButton(TacticalCombatSession encounter)
        {
            for (int frame = 0; frame < 9; frame++)
                controller.Advance(0.5f);
            TacticalCombatUnit unit = encounter.Units.First(item =>
                item.Side == encounter.PlayerSide && !item.IsFighter
            );
            encounter.Selection.QueueObject(unit, false);
            controller.Advance(0);
            return view.GetComponentsInChildren<TacticalCombatHudButton>(true)
                .Single(item =>
                    item.name == nameof(TacticalCombatControl.NextObject)
                    && item.gameObject.activeInHierarchy
                );
        }

        /// <summary>
        /// Prepares the Empire controls while keeping deterministic input checks paused.
        /// </summary>
        /// <param name="encounter">The encounter whose controls are being tested.</param>
        /// <param name="pack">The content supplying its presentation.</param>
        /// <returns>The presentation preparation sequence.</returns>
        private IEnumerator PrepareEmpireGroupSelection(
            TacticalCombatSession encounter,
            ContentPack pack
        )
        {
            encounter.IsPaused = true;
            yield return PrepareController(encounter, pack);
            for (int frame = 0; frame < 9; frame++)
                controller.Advance(0.5f);
            Assert.IsFalse(encounter.IsArriving);
        }

        /// <summary>
        /// Checks empty-slot rejection through the presented Empire controls.
        /// </summary>
        /// <param name="keyboard">Whether to activate the slot with a native key instead of a mouse click.</param>
        /// <returns>The preparation, input, and verification sequence.</returns>
        private IEnumerator VerifyEmptyEmpireSlot(bool keyboard)
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(
                pack.GameData,
                playerSide: CombatSide.Defender
            );
            encounter.IsPaused = true;
            yield return PrepareController(encounter, pack);
            for (int frame = 0; frame < 9; frame++)
                controller.Advance(0.5f);
            TacticalCombatGroup group = encounter.GetGroup(encounter.GetUnit("CSEM006"));
            encounter.SetObjectGroup(encounter.PlayerSide, 0, group);
            encounter.SetObjectGroup(encounter.PlayerSide, 1, null);
            controller.Advance(0);
            Assert.IsNull(encounter.GetObjectGroup(encounter.PlayerSide, 1));
            GameObject events = new("Empty group selection events", typeof(EventSystem));
            try
            {
                Canvas.ForceUpdateCanvases();
                TacticalCombatHudButton occupied =
                    view.GetComponentsInChildren<TacticalCombatHudButton>(true)
                        .Single(item => item.name == nameof(TacticalCombatControl.ObjectGroup1));
                TacticalCombatHudButton empty =
                    view.GetComponentsInChildren<TacticalCombatHudButton>(true)
                        .Single(item => item.name == nameof(TacticalCombatControl.ObjectGroup2));
                ClickGroupButton(occupied, events.GetComponent<EventSystem>());
                controller.Advance(0);
                Assert.AreSame(group, encounter.Selection.Group);

                if (keyboard)
                    Assert.IsTrue(
                        view.HandleKeyboardEvent(
                            new Event { type = EventType.KeyDown, keyCode = KeyCode.Alpha2 }
                        )
                    );
                else
                    ClickGroupButton(empty, events.GetComponent<EventSystem>());
                controller.Advance(0);

                Assert.AreEqual(TacticalCombatSelectionKind.ObjectGroup, encounter.Selection.Kind);
                Assert.AreEqual(0, encounter.Selection.Category);
                Assert.AreSame(group, encounter.Selection.Group);
                Assert.IsNull(encounter.GetObjectGroup(encounter.PlayerSide, 1));
            }
            finally
            {
                Object.Destroy(events);
            }
        }

        /// <summary>
        /// Sends a completed mouse click through the visible control's raycast and pointer handlers.
        /// </summary>
        /// <param name="button">The numbered category button.</param>
        /// <param name="events">The event system resolving the visible pointer target.</param>
        private static void ClickGroupButton(TacticalCombatHudButton button, EventSystem events)
        {
            RectTransform rect = (RectTransform)button.transform;
            PointerEventData pointer = new(events)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    rect.TransformPoint(rect.rect.center)
                ),
            };
            List<RaycastResult> hits = new();
            events.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits);
            pointer.pointerPressRaycast = hits[0];
            GameObject pressed = ExecuteEvents.ExecuteHierarchy(
                hits[0].gameObject,
                pointer,
                ExecuteEvents.pointerDownHandler
            );
            Assert.AreSame(button.gameObject, pressed);
            pointer.pointerPress = pressed;
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
        }

        /// <summary>
        /// Checks the camera against a presented unit's independently observable middle-model boundary.
        /// </summary>
        /// <param name="unit">The expected camera primary.</param>
        private void AssertCategoryOffset(TacticalCombatUnit unit)
        {
            Transform root = view
                .Renderer.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == unit.InstanceID);
            Vector3 bounds = root.Find("Selection box")
                .GetComponent<MeshFilter>()
                .sharedMesh.bounds.size;
            float span = Mathf.Max(bounds.x, bounds.y);
            Assert.That(
                Vector3.Distance(
                    root.TransformPoint(new Vector3(-span, span, span + span)),
                    view.Renderer.Camera.transform.position
                ),
                Is.LessThan(0.003f)
            );
        }

        /// <summary>
        /// Prepares the real presentation controller with captured arrival metadata.
        /// </summary>
        /// <param name="encounter">The encounter to present.</param>
        /// <param name="pack">The active content pack.</param>
        /// <param name="driver">The shared playback owner, or null for a local encounter.</param>
        /// <param name="complete">Observes completion, or null when no completion observation is needed.</param>
        /// <returns>The asynchronous preparation sequence.</returns>
        private IEnumerator PrepareController(
            TacticalCombatSession encounter,
            ContentPack pack,
            ITacticalCombatPresentationDriver driver = null,
            Action<TacticalCombatSession> complete = null
        )
        {
            assets = new ContentAssets(pack.FileResolver);
            models = new ContentModelCache(assets);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/StrategyView/TacticalCombat.prefab"
            );
            view = Object.Instantiate(prefab).GetComponent<TacticalCombatView>();
            controller = new TacticalCombatController(
                encounter,
                view,
                complete ?? (_ => { }),
                CancellationToken.None,
                driver: driver
            );
            Task preparation = controller.PrepareAsync(models, assets);
            while (!preparation.IsCompleted)
                yield return null;
            preparation.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Records presentation ownership without advancing the encounter's simulation.
        /// </summary>
        private sealed class PresentationDriver : ITacticalCombatPresentationDriver
        {
            public TacticalCombatSession Session { get; }
            public TacticalCombatReplicaPlayback ReplicaPlayback { get; }
            ITacticalCombatPresentationTimeline ITacticalCombatPresentationDriver.ReplicaPlayback =>
                ReplicaPlayback;
            public bool IsAuthoritative => false;
            public bool CompletionReady { get; set; }
            public SpaceCombatResult CommittedResult => null;
            public int PreparationCount { get; private set; }
            public int DisposeCount { get; private set; }
            public ITacticalCombatArrivalPresentation Presentation { get; private set; }
            public List<float> Intervals { get; } = new();

            /// <summary>
            /// Binds the encounter whose presentation calls are recorded.
            /// </summary>
            /// <param name="session">The controller's encounter.</param>
            public PresentationDriver(TacticalCombatSession session)
            {
                Session = session;
                if (session.IsReplica)
                    ReplicaPlayback = new TacticalCombatReplicaPlayback(session);
            }

            /// <summary>
            /// Records the loaded arrival presentation supplied by the controller.
            /// </summary>
            /// <param name="presentation">The local owner of arrival transforms.</param>
            public void Prepare(ITacticalCombatArrivalPresentation presentation)
            {
                Presentation = presentation;
                PreparationCount++;
            }

            /// <summary>
            /// Records an unscaled presentation interval without ticking combat.
            /// </summary>
            /// <param name="elapsedSeconds">The interval delegated by the controller.</param>
            public void Advance(float elapsedSeconds) => Intervals.Add(elapsedSeconds);

            /// <summary>
            /// Accepts a movie acknowledgement without releasing simulation state.
            /// </summary>
            /// <param name="request">The movie ownership token supplied by the view.</param>
            public void AcknowledgeMovie(TacticalCombatMovieRequest request) { }

            /// <summary>
            /// Counts releases of the controller-owned driver.
            /// </summary>
            public void Dispose()
            {
                DisposeCount++;
                ReplicaPlayback?.Dispose();
            }
        }

        /// <summary>
        /// Applies a captured projection through the same separated state and reliable-effect boundaries as live playback.
        /// </summary>
        /// <param name="session">The passive encounter receiving the projection.</param>
        /// <param name="frame">The captured state and its separately delivered effects.</param>
        private void ApplyCapturedProjection(
            TacticalCombatSession session,
            TacticalCombatSnapshot frame
        )
        {
            GetReplicaPlayback(session);
            TacticalCombatEvent[] effects = frame.Effects;
            frame.Effects = Array.Empty<TacticalCombatEvent>();
            session.ApplySnapshot(frame);
            session.ApplySynchronizedEvents(effects);
        }

        /// <summary>
        /// Prepares a renderer directly so its presentation contract can be exercised independently of session ticking.
        /// </summary>
        /// <param name="encounter">The encounter to present.</param>
        /// <param name="pack">The active content pack.</param>
        /// <param name="videoSettings">The explicitly configured presentation options, or null for their authored defaults.</param>
        /// <returns>The asynchronous preparation sequence.</returns>
        private IEnumerator PrepareRenderer(
            TacticalCombatSession encounter,
            ContentPack pack,
            UserVideoSettings videoSettings = null
        )
        {
            assets = new ContentAssets(pack.FileResolver);
            models = new ContentModelCache(assets);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/StrategyView/TacticalCombat.prefab"
            );
            view = Object.Instantiate(prefab).GetComponent<TacticalCombatView>();
            Task preparation = view.Renderer.PrepareAsync(
                encounter,
                models,
                assets,
                CancellationToken.None,
                videoSettings,
                GetReplicaPlayback(encounter)
            );
            while (!preparation.IsCompleted)
                yield return null;
            preparation.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Retains one playback owner for each passive encounter used by the rendering fixture.
        /// </summary>
        /// <param name="session">The encounter whose validated projections are observed.</param>
        /// <returns>The fixture-owned timeline, or null for an authoritative encounter.</returns>
        private TacticalCombatReplicaPlayback GetReplicaPlayback(TacticalCombatSession session)
        {
            if (!session.IsReplica)
                return null;
            if (!playbacks.TryGetValue(session, out TacticalCombatReplicaPlayback playback))
            {
                playback = new TacticalCombatReplicaPlayback(session);
                playbacks.Add(session, playback);
            }
            return playback;
        }

        /// <summary>
        /// Creates two unarmed content-backed capitals at their separated deployment boundaries.
        /// </summary>
        /// <param name="catalog">The selected pack's typed data.</param>
        /// <returns>The encounter with a mobile departing participant and a surviving opponent.</returns>
        private static TacticalCombatSession CreateWithdrawalEncounter(GameDataCatalog catalog)
        {
            List<TacticalCombatUnit> units = new();
            foreach (CombatSide side in new[] { CombatSide.Attacker, CombatSide.Defender })
            {
                CapitalShip ship = (CapitalShip)
                    catalog.CapitalShips.Single(item => item.TypeID == "ALCS008").CreateCopy();
                ship.InstanceID = side == CombatSide.Attacker ? "departing" : "opponent";
                ship.CurrentHullStrength = ship.MaxHullStrength;
                ship.ManufacturingStatus = ManufacturingStatus.Complete;
                ship.Hyperdrive = 1;
                ship.HasGravityWell = false;
                ship.PrimaryWeapons = ship.PrimaryWeapons.ToDictionary(
                    pair => pair.Key,
                    pair => new int[pair.Value.Length]
                );
                units.Add(
                    new TacticalCombatUnit(
                        CombatUnit.Create(ship),
                        side,
                        units.Count + 1,
                        catalog.TacticalCombat.Units.Single(item => item.UnitTypeID == ship.TypeID)
                    )
                    {
                        Position = CreateSide(side).Forward * -50,
                        Heading = CreateSide(side).Forward,
                        DesiredHeading = CreateSide(side).Forward,
                    }
                );
            }
            LinearCongruentialRandomProvider random = new(42);
            return new TacticalCombatSession(
                units,
                units.ConvertAll(unit => new TacticalCombatGroup(
                    unit.Index + 100,
                    new[] { unit },
                    CreateSide(unit.Side).Forward
                )),
                CreateSide(CombatSide.Attacker),
                CreateSide(CombatSide.Defender),
                CombatSide.Attacker,
                100,
                random,
                TacticalCombatAudio.Create,
                Rebellion.AI.Tactical.TacticalCombatPlanner.Attach,
                catalog.TacticalCombat,
                catalog.TacticalCombat.Environments[1],
                new TacticalCombatSceneryRandom(random, () => 1),
                enteringSide: CombatSide.Attacker
            );
        }

        /// <summary>
        /// Creates a representative content-backed roster without starting a campaign.
        /// </summary>
        /// <param name="catalog">The selected pack's typed data.</param>
        /// <param name="fighterCategory">The fighter group's authored category, or four for an uncategorized group.</param>
        /// <param name="allCapitalsPlayer">Whether all capitals belong to the player for multiple-category ownership tests.</param>
        /// <param name="playerSide">The side whose faction artwork and controls are presented.</param>
        /// <param name="gravityWell">The generator flag override, or null to keep each roster entry's configured value.</param>
        /// <returns>The isolated encounter.</returns>
        private static TacticalCombatSession CreateEncounter(
            GameDataCatalog catalog,
            int fighterCategory = 4,
            bool allCapitalsPlayer = false,
            CombatSide playerSide = CombatSide.Attacker,
            bool? gravityWell = null
        )
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
                if (gravityWell.HasValue)
                    ship.HasGravityWell = gravityWell.Value;
                CombatSide side =
                    type == "ALCS008" || allCapitalsPlayer
                        ? CombatSide.Attacker
                        : CombatSide.Defender;
                units.Add(
                    new TacticalCombatUnit(
                        CombatUnit.Create(ship),
                        side,
                        units.Count + 1,
                        definitions[type]
                    )
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
                    CombatUnit.Create(fighter),
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
            foreach (TacticalCombatGroup group in groups)
                if (group.IsFighterGroup)
                    group.FighterCategory = fighterCategory;
            LinearCongruentialRandomProvider random = new(42);
            return new TacticalCombatSession(
                units,
                groups,
                CreateSide(CombatSide.Attacker),
                CreateSide(CombatSide.Defender),
                playerSide,
                100,
                random,
                TacticalCombatAudio.Create,
                Rebellion.AI.Tactical.TacticalCombatPlanner.Attach,
                catalog.TacticalCombat,
                catalog.TacticalCombat.Environments[1],
                new TacticalCombatSceneryRandom(random, () => 1),
                enteringSide: CombatSide.Attacker
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
