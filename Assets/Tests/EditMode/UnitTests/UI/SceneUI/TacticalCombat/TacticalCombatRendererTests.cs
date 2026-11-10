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
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    [Category("Graphics")]
    public sealed class TacticalCombatRendererTests
    {
        private TacticalCombatController controller;
        private TacticalCombatView view;
        private ContentAssets assets;
        private ContentModelCache models;
        private float _audioVolume;
        private bool _audioVolumeCaptured;

        /// <summary>
        /// Enters an empty play-mode scene for the isolated render target.
        /// </summary>
        /// <returns>The setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
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
            presentation.SetPauseStrobe(true);
            presentation.Reset(unit.Index);
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
        public IEnumerator SetPauseStrobe_CompletedArrival_ReleasesTransformsToSimulation()
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
            view.Renderer.ArrivalStrobeChanged += strobes.Add;
            ITacticalCombatArrivalPresentation presentation = view.Renderer;
            presentation.SetPauseStrobe(true);
            presentation.Reset(unit.Index);
            presentation.SetTransform(
                unit.Index,
                new System.Numerics.Vector4(14, 7, -350, -1),
                new System.Numerics.Vector4(1, 0, 0, -1)
            );

            presentation.SetPauseStrobe(false);
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

            view.Renderer.Reset(unit.Index);

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
                presentation.Reset(42);
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
            TacticalCombatSceneryRandom expectedRandom = new(new TacticalCombatRandom(42), () => 1);
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
            UnityEngine.UI.Image pause = view
                .transform.Find("Canvas/Frame/Pause")
                .GetComponent<UnityEngine.UI.Image>();
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
        public IEnumerator Focus_ArrivalTransform_AimsAtLiveRenderPoseWithoutMovingSimulation()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            TacticalCombatSession encounter = CreateEncounter(pack.GameData);
            yield return PrepareRenderer(encounter, pack);
            TacticalCombatUnit unit = encounter.Units.First(item =>
                !item.IsFighter && !item.IsBattleStation
            );
            Vector3N before = unit.Position;
            view.Renderer.SetPauseStrobe(true);
            view.Renderer.Reset(unit.Index);
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
        public IEnumerator HandleKeyboardEvent_RepeatedTab_ReleasesSingleInspectionArtworkAfterEachPress()
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
            Assert.IsTrue(
                view.HandleKeyboardEvent(
                    new Event { type = EventType.KeyUp, keyCode = KeyCode.Tab }
                )
            );

            Assert.AreEqual(3, presses);
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
                            view.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)
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
        /// <returns>The asynchronous preparation sequence.</returns>
        private IEnumerator PrepareController(TacticalCombatSession encounter, ContentPack pack)
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
                _ => { },
                CancellationToken.None
            );
            Task preparation = controller.PrepareAsync(models, assets);
            while (!preparation.IsCompleted)
                yield return null;
            preparation.GetAwaiter().GetResult();
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
                videoSettings
            );
            while (!preparation.IsCompleted)
                yield return null;
            preparation.GetAwaiter().GetResult();
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
                        ship,
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
            TacticalCombatRandom random = new(42);
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
        /// <returns>The isolated encounter.</returns>
        private static TacticalCombatSession CreateEncounter(
            GameDataCatalog catalog,
            int fighterCategory = 4,
            bool allCapitalsPlayer = false,
            CombatSide playerSide = CombatSide.Attacker
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
                CombatSide side =
                    type == "ALCS008" || allCapitalsPlayer
                        ? CombatSide.Attacker
                        : CombatSide.Defender;
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
            foreach (TacticalCombatGroup group in groups)
                if (group.IsFighterGroup)
                    group.FighterCategory = fighterCategory;
            TacticalCombatRandom random = new(42);
            return new TacticalCombatSession(
                units,
                groups,
                CreateSide(CombatSide.Attacker),
                CreateSide(CombatSide.Defender),
                playerSide,
                100,
                random,
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
