using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalViewControllerTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab";

        private GameObject instance;
        private TacticalViewController controller;
        private ContentModelCache models;

        /// <summary>
        /// Loads an isolated generated tactical root for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
            models?.Dispose();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            Assert.IsNotNull(prefab, $"Missing generated tactical prefab: {_prefabPath}");
            instance = UnityEngine.Object.Instantiate(prefab);
            controller = instance.GetComponent<TacticalViewController>();
            controller.enabled = false;
            models = new ContentModelCache(TestContent.Assets);
        }

        /// <summary>
        /// Releases the generated tactical root after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(instance);
            models.Dispose();
        }

        /// <summary>
        /// Checks the authored Move and Stop buttons drive saved positions and their rendered model roots.
        /// </summary>
        /// <returns>The model loading, runtime input, and mode transitions.</returns>
        [UnityTest]
        public IEnumerator InitializeAsync_MoveAndStopButtons_MovePresentationWithBattleState()
        {
            UnityEngine.Object.DestroyImmediate(instance);
            models.Dispose();
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single
            );
            EditorSceneManager.MarkSceneDirty(scene);
            yield return new EnterPlayMode();
            SetUp();
            InputTestFixture input = new InputTestFixture();
            input.Setup();
            try
            {
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                ActiveBattle battle = CreateBattleWithCombatant(out CombatUnit combatant);
                combatant.Data.SublightSpeed = 4;
                Task initialization = controller.InitializeAsync(
                    battle,
                    CreateBattleMap(),
                    models,
                    "FACTION"
                );
                while (!initialization.IsCompleted)
                    yield return null;
                initialization.GetAwaiter().GetResult();
                controller.enabled = true;
                TacticalSelectionController selection =
                    instance.GetComponent<TacticalSelectionController>();
                selection.SelectAt(controller.BattleCamera.WorldToScreenPoint(Vector3.zero));
                Assert.AreSame(combatant, selection.SelectedCombatant);
                TacticalSelectionView view =
                    instance.GetComponentInChildren<TacticalSelectionView>();
                view.MoveButton.onClick.Invoke();
                Vector2 target = controller.BattleCamera.WorldToScreenPoint(
                    new Vector3(100f, 0f, 0f)
                );

                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = target }.WithButton(MouseButton.Left)
                );
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = target });
                yield return null;
                for (int frame = 0; frame < 10 && combatant.Position.X <= 0f; frame++)
                    yield return null;

                Assert.That(combatant.Position.X, Is.GreaterThan(0f));
                Assert.That(combatant.Position.Z, Is.GreaterThan(0f));
                Assert.That(combatant.Forward.X, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.That(combatant.MoveOrder.Destination.X, Is.EqualTo(100f).Within(0.05f));
                Transform presentation = controller.BattleRoot.GetChild(0).GetChild(0);
                Assert.That(
                    presentation.localPosition.x,
                    Is.EqualTo(combatant.Position.X).Within(0.0001f)
                );
                Assert.That(
                    Quaternion.Angle(
                        presentation.localRotation,
                        Quaternion.LookRotation(
                            new Vector3(
                                combatant.Forward.X,
                                combatant.Forward.Y,
                                combatant.Forward.Z
                            )
                        )
                    ),
                    Is.LessThan(0.01f)
                );
                view.StopButton.onClick.Invoke();
                float stopped = combatant.Position.X;
                yield return null;
                Assert.AreEqual(stopped, combatant.Position.X);
                Assert.IsNull(combatant.MoveOrder.Destination);
            }
            finally
            {
                input.TearDown();
                UnityEngine.Object.DestroyImmediate(instance);
                models.Dispose();
            }
            yield return new ExitPlayMode();
        }

        [Test]
        public void GeneratedPrefab_RequiredSceneReferences_AreAssigned()
        {
            Assert.IsNotNull(controller);
            Assert.IsNotNull(controller.BattleCamera);
            Assert.IsNotNull(controller.BattleRoot);
            Assert.IsNotNull(controller.MapRoot);
            Assert.IsNotNull(controller.MapRoot.GetComponent<TacticalMapRenderer>());
            Assert.IsNotNull(controller.BattleCamera.GetComponent<TacticalViewCameraController>());
            Assert.IsNotNull(instance.GetComponent<TacticalSelectionController>());
            Assert.IsNotNull(instance.GetComponentInChildren<TacticalSelectionView>());
            Assert.IsNotNull(instance.GetComponentInChildren<TacticalTimeControlsView>());
        }

        /// <summary>
        /// Checks every generated speed button updates the tactical-only battle clock.
        /// </summary>
        /// <returns>The play-mode lifecycle and speed-selection checks.</returns>
        [UnityTest]
        public IEnumerator TimeControl_SpeedClicked_UpdatesBattleClock()
        {
            UnityEngine.Object.DestroyImmediate(instance);
            models.Dispose();
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single
            );
            EditorSceneManager.MarkSceneDirty(scene);
            yield return new EnterPlayMode();
            SetUp();
            try
            {
                (string Name, TacticalBattleSpeed Speed, float Scale)[] cases =
                {
                    ("TimeControl0", TacticalBattleSpeed.Paused, 0f),
                    ("TimeControl1", TacticalBattleSpeed.Half, 0.5f),
                    ("TimeControl2", TacticalBattleSpeed.Normal, 1f),
                    ("TimeControl3", TacticalBattleSpeed.Double, 2f),
                    ("TimeControl4", TacticalBattleSpeed.Quadruple, 4f),
                };
                foreach ((string name, TacticalBattleSpeed speed, float scale) in cases)
                {
                    Button button = instance
                        .GetComponentsInChildren<Button>(true)
                        .Single(candidate => candidate.name == name);

                    button.onClick.Invoke();

                    Assert.AreEqual(speed, controller.BattleSpeed);
                    Assert.AreEqual(scale, controller.BattleTimeScale);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                models.Dispose();
            }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks loaded geometry participates in world selection.
        /// </summary>
        /// <returns>The asynchronous model-loading and selection check.</returns>
        [Test]
        public async Task InitializeAsync_LoadedHull_CanBeSelectedAsync()
        {
            ActiveBattle battle = CreateBattleWithCombatant(out CombatUnit combatant);
            await controller.InitializeAsync(battle, CreateBattleMap(), models, "FACTION");
            TacticalSelectionController selection =
                instance.GetComponent<TacticalSelectionController>();

            selection.SelectAt(controller.BattleCamera.WorldToScreenPoint(Vector3.zero));

            Assert.AreSame(combatant, selection.SelectedCombatant);
        }

        /// <summary>
        /// Checks real content connects the dock to multiple deployed ships.
        /// </summary>
        /// <returns>The asynchronous integration check.</returns>
        [Test]
        public async Task InitializeAsync_ContentFleet_ConnectsRosterAndRadarAsync()
        {
            string mapId = TestContent.Data.DefaultSpaceBattleMapInstanceIDs.First();
            Assert.IsTrue(TestContent.Data.TryGetBattleMap(mapId, out BattleMap map));
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = mapId,
                Kind = BattleKind.Space,
            };
            string viewer = TestContent.Pack.Scenario.PlayableFactionIDs.First();
            BattleMapDeploymentRegion[] regions = map.GetDeploymentRegions().ToArray();
            int slot = 0;
            foreach (string faction in TestContent.Pack.Scenario.PlayableFactionIDs)
            {
                foreach (
                    CapitalShip template in TestContent
                        .Data.CapitalShips.Where(ship =>
                            !ship.CanDestroyPlanets
                            && !string.IsNullOrWhiteSpace(ship.ModelPath)
                            && ship.ManufacturingFactionInstanceIDs.Contains(faction)
                        )
                        .Take(3)
                )
                {
                    CapitalShip ship = (CapitalShip)template.CreateCopy();
                    ship.OwnerInstanceID = faction;
                    battle.AddCombatants(ship);
                }
                battle.GetParticipants().Last().BattleMapSlotID = regions[slot++].ParticipantSlotID;
            }
            await controller.InitializeAsync(battle, map, models, viewer, true);
            ContentBindings.Apply(controller.MapRoot.gameObject, TestContent.Assets);
            TacticalSelectionView dock = instance.GetComponentInChildren<TacticalSelectionView>();
            dock.ApplyTheme(
                TestContent.CreateThemeLibrary().GetTheme(viewer).BattleHud,
                TestContent.Assets
            );
            CombatUnit[] selected = battle.GetParticipants().First().GetCombatants().ToArray();
            System.Collections.Generic.Dictionary<CombatUnit, Transform> presentations =
                new System.Collections.Generic.Dictionary<CombatUnit, Transform>();
            for (int i = 0; i < selected.Length; i++)
                presentations.Add(selected[i], controller.BattleRoot.GetChild(0).GetChild(i));
            dock.RenderSelection(
                selected.Take(2).ToArray(),
                presentations,
                controller.BattleCamera
            );
            Assert.AreEqual(2, dock.GetComponentsInChildren<TacticalUnitCardView>().Length);

            dock.RenderSelection(selected, presentations, controller.BattleCamera);

            Assert.AreEqual(3, dock.GetComponentsInChildren<TacticalUnitCardView>().Length);
        }

        [Test]
        public async Task InitializeAsync_MatchingBattleAndMap_BindsPresentationStateAsync()
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "SPACE_MAP",
                Kind = BattleKind.Space,
            };
            BattleMap map = new BattleMap { InstanceID = "SPACE_MAP", Kind = BattleKind.Space };

            await controller.InitializeAsync(battle, map, models);

            Assert.AreSame(battle, controller.ActiveBattle);
            Assert.AreSame(map, controller.BattleMap);
        }

        [Test]
        public async Task InitializeAsync_BattleCombatant_CreatesModelPresentationAsync()
        {
            ActiveBattle battle = CreateBattleWithCombatant(out CombatUnit combatant);
            combatant.Position = new BattleVector3
            {
                X = 25f,
                Y = 10f,
                Z = -100f,
            };
            combatant.Forward = new BattleVector3 { X = 1f };
            BattleMap map = CreateBattleMap();

            await controller.InitializeAsync(battle, map, models, "FACTION");

            Transform participantRoot = controller.BattleRoot.GetChild(0);
            Transform unitRoot = participantRoot.GetChild(0);
            Assert.AreEqual(new Vector3(25f, 10f, -100f), unitRoot.localPosition);
            Assert.That(
                Quaternion.Angle(Quaternion.LookRotation(Vector3.right), unitRoot.localRotation),
                Is.LessThan(0.01f)
            );
            Assert.That(unitRoot.GetComponentsInChildren<Renderer>(), Is.Not.Empty);
        }

        [Test]
        public async Task InitializeAsync_ViewerFaction_FramesFleetUsingParticipantViewingAngleAsync()
        {
            ActiveBattle battle = CreateBattleWithCombatant(out _);
            BattleMap map = CreateBattleMap();

            await controller.InitializeAsync(battle, map, models, "FACTION");

            Vector3 projected = controller.BattleCamera.WorldToViewportPoint(Vector3.zero);
            Assert.That(projected.x, Is.InRange(0.05f, 0.95f));
            Assert.That(projected.y, Is.InRange(0.05f, 0.95f));
            Assert.That(projected.z, Is.GreaterThan(controller.BattleCamera.nearClipPlane));
            Assert.That(
                Quaternion.Angle(
                    Quaternion.Euler(12f, 0f, 0f),
                    controller.BattleCamera.transform.rotation
                ),
                Is.LessThan(0.01f)
            );
            Assert.AreEqual(48f, controller.BattleCamera.fieldOfView);
            Assert.That(controller.BattleCamera.farClipPlane, Is.GreaterThan(1000f));
            Assert.IsTrue(
                controller.BattleCamera.GetComponent<TacticalViewCameraController>().IsConfigured
            );
        }

        [Test]
        public async Task InitializeAsync_HeavyDefaultFleet_LoadsAndDeploysEveryCombatantAsync()
        {
            TacticalBattleLaunchContext.Reset(TestContent.Pack);
            ActiveBattle battle = TacticalBattleLaunchContext.ActiveBattle;
            BattleMap map = TestContent.Data.BattleMaps.Single(candidate =>
                candidate.InstanceID == battle.BattleMapInstanceID
            );

            await controller.InitializeAsync(battle, map, models, deployFleet: true);

            Assert.That(
                controller.BattleRoot.childCount,
                Is.EqualTo(battle.GetParticipants().Count)
            );
            for (
                int participantIndex = 0;
                participantIndex < battle.GetParticipants().Count;
                participantIndex++
            )
            {
                BattleParticipant participant = battle.GetParticipants()[participantIndex];
                Transform participantRoot = controller.BattleRoot.GetChild(participantIndex);
                Assert.That(
                    participantRoot.childCount,
                    Is.EqualTo(participant.GetCombatants().Count)
                );
                for (int unitIndex = 0; unitIndex < participant.GetCombatants().Count; unitIndex++)
                {
                    CombatUnit combatant = participant.GetCombatants()[unitIndex];
                    Transform presentation = participantRoot.GetChild(unitIndex);
                    Assert.That(presentation.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
                    Assert.That(
                        presentation.gameObject.activeSelf,
                        Is.EqualTo(combatant.Flight.Phase != FighterFlightState.Docked)
                    );
                    Assert.That(
                        presentation.localPosition,
                        Is.EqualTo(
                            new Vector3(
                                combatant.Position.X,
                                combatant.Position.Y,
                                combatant.Position.Z
                            )
                        )
                    );
                }
            }
        }

        [Test]
        public async Task InitializeAsync_AuthoredMapObjects_CreatesMapPresentationAsync()
        {
            ActiveBattle battle = CreateBattleWithCombatant(out _);
            BattleMap map = CreateBattleMap();
            map.GetStarfields()
                .Add(
                    new BattleMapStarfield
                    {
                        InstanceID = "Distant Stars",
                        Count = 4,
                        Seed = 72491,
                        InnerRadius = 325f,
                        OuterRadius = 475f,
                        MinimumSize = 0.035f,
                        MaximumSize = 0.19f,
                        MinimumBrightness = 0.05f,
                        MaximumBrightness = 0.26f,
                        SecondaryColorProbability = 0.16f,
                        PrimaryColor = new BattleMapColor
                        {
                            Red = 0.68f,
                            Green = 0.78f,
                            Blue = 0.92f,
                        },
                        SecondaryColor = new BattleMapColor
                        {
                            Red = 0.38f,
                            Green = 0.58f,
                            Blue = 0.86f,
                        },
                    }
                );
            map.GetDirectionalLights()
                .Add(
                    new BattleMapDirectionalLight
                    {
                        InstanceID = "Starlight Key",
                        Color = new BattleMapColor
                        {
                            Red = 1f,
                            Green = 0.94f,
                            Blue = 0.84f,
                        },
                        Intensity = 1.15f,
                        CastShadows = true,
                        IsSun = true,
                    }
                );

            await controller.InitializeAsync(battle, map, models, "FACTION");

            Transform stars = controller.MapRoot.Find("Distant Stars");
            Light starlight = controller.MapRoot.Find("Starlight Key")?.GetComponent<Light>();
            Assert.IsNotNull(stars?.GetComponent<ParticleSystem>());
            Assert.AreEqual(4, stars.GetComponent<ParticleSystem>().particleCount);
            Assert.IsNotNull(starlight);
            Assert.That(starlight.intensity, Is.EqualTo(1.15f * Mathf.PI).Within(0.0001f));
            Assert.AreEqual(LightUnit.Lux, starlight.lightUnit);
            Assert.IsNotNull(starlight.GetComponent<HDAdditionalLightData>());
            Assert.AreSame(starlight, RenderSettings.sun);
            Volume environmentVolume = controller
                .MapRoot.Find("Tactical Environment")
                ?.GetComponent<Volume>();
            Assert.IsNotNull(environmentVolume);
            Assert.IsTrue(environmentVolume.sharedProfile.TryGet(out GradientSky gradientSky));
            Assert.AreEqual(0.05f, gradientSky.top.value.r);
            Assert.AreEqual(0.065f, gradientSky.top.value.g);
            Assert.AreEqual(0.11f, gradientSky.top.value.b);
            HDAdditionalCameraData cameraData =
                controller.BattleCamera.GetComponent<HDAdditionalCameraData>();
            Assert.IsNotNull(cameraData);
            Assert.AreEqual(HDAdditionalCameraData.ClearColorMode.Color, cameraData.clearColorMode);
            Assert.AreEqual(
                controller.BattleCamera.backgroundColor.linear,
                cameraData.backgroundColorHDR
            );
        }

        [Test]
        public void InitializeAsync_MismatchedMapIdentifier_ThrowsArgumentException()
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "SPACE_MAP",
                Kind = BattleKind.Space,
            };
            BattleMap map = new BattleMap { InstanceID = "OTHER_MAP", Kind = BattleKind.Space };

            Assert.ThrowsAsync<ArgumentException>(() =>
                controller.InitializeAsync(battle, map, models)
            );
        }

        [Test]
        public void InitializeAsync_MismatchedBattleKind_ThrowsArgumentException()
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "MAP",
                Kind = BattleKind.Space,
            };
            BattleMap map = new BattleMap { InstanceID = "MAP", Kind = BattleKind.Ground };

            Assert.ThrowsAsync<ArgumentException>(() =>
                controller.InitializeAsync(battle, map, models)
            );
        }

        [Test]
        public async Task InitializeAsync_StaleDimensions_ReplacesSnapshotWithRenderedSizeAsync()
        {
            ActiveBattle battle = CreateBattleWithCombatant(out CombatUnit combatant);

            await controller.InitializeAsync(battle, CreateBattleMap(), models, "FACTION");

            Transform root = controller.BattleRoot.GetChild(0).GetChild(0);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds expected = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
                expected.Encapsulate(renderer.bounds);
            ModelDimensions measured = combatant.Data.ModelSize;
            Assert.That(measured.Width, Is.EqualTo(expected.size.x).Within(0.0001f));
            Assert.That(measured.Height, Is.EqualTo(expected.size.y).Within(0.0001f));
            Assert.That(measured.Depth, Is.EqualTo(expected.size.z).Within(0.0001f));
            Assert.That(measured.Depth, Is.Not.EqualTo(60f));
        }

        [TestCase(0.5f)]
        [TestCase(2f)]
        public async Task InitializeAsync_ModelScale_ScalesRenderedGeometryAndMeasuredSizeAsync(
            float scale
        )
        {
            ActiveBattle battle = CreateBattleWithCombatant(out CombatUnit combatant);
            BattleMap map = CreateBattleMap();
            Assert.AreEqual(1f, combatant.Data.ModelScale);
            await controller.InitializeAsync(battle, map, models);
            ModelDimensions unscaled = combatant.Data.ModelSize;
            combatant.Data.ModelScale = scale;

            await controller.InitializeAsync(battle, map, models);

            ModelDimensions measured = combatant.Data.ModelSize;
            Assert.That(measured.Width, Is.EqualTo(unscaled.Width * scale).Within(0.0001f));
            Assert.That(measured.Height, Is.EqualTo(unscaled.Height * scale).Within(0.0001f));
            Assert.That(measured.Depth, Is.EqualTo(unscaled.Depth * scale).Within(0.0001f));
            Renderer[] renderers = controller.BattleRoot.GetComponentsInChildren<Renderer>();
            Bounds actual = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
                actual.Encapsulate(renderer.bounds);
            Assert.That(actual.size.x, Is.EqualTo(measured.Width).Within(0.0001f));
            Assert.That(actual.size.y, Is.EqualTo(measured.Height).Within(0.0001f));
            Assert.That(actual.size.z, Is.EqualTo(measured.Depth).Within(0.0001f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InitializeAsync_InvalidModelScale_ThrowsInvalidOperationException(float scale)
        {
            ActiveBattle battle = CreateBattleWithCombatant(out CombatUnit combatant);
            combatant.Data.ModelScale = scale;

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                controller.InitializeAsync(battle, CreateBattleMap(), models)
            );
        }

        [Test]
        public async Task InitializeAsync_RestoredBattle_PreservesSavedPoseAsync()
        {
            ActiveBattle battle = CreateBattleWithCombatant(out CombatUnit combatant);
            combatant.Position = new BattleVector3
            {
                X = 42f,
                Y = 6f,
                Z = -80f,
            };
            combatant.Forward = new BattleVector3 { X = 1f };
            combatant.Data.ModelScale = 2.5f;
            ActiveBattle restored = SerializationHelper.Deserialize<ActiveBattle>(
                SerializationHelper.Serialize(battle)
            );

            await controller.InitializeAsync(restored, CreateBattleMap(), models, "FACTION");

            CombatUnit restoredUnit = restored.GetParticipants().Single().GetCombatants().Single();
            Assert.AreEqual(42f, restoredUnit.Position.X);
            Assert.AreEqual(6f, restoredUnit.Position.Y);
            Assert.AreEqual(-80f, restoredUnit.Position.Z);
            Assert.AreEqual(1f, restoredUnit.Forward.X);
            Assert.AreEqual(0f, restoredUnit.Forward.Y);
            Assert.AreEqual(0f, restoredUnit.Forward.Z);
            Assert.AreEqual(2.5f, restoredUnit.Data.ModelScale);
        }

        [Test]
        public async Task InitializeAsync_ReboundBattle_ReleasesPreviousModelInstancesAsync()
        {
            ActiveBattle battle = CreateBattleWithCombatant(out _);
            BattleMap map = CreateBattleMap();
            await controller.InitializeAsync(battle, map, models);
            Renderer previous = controller.BattleRoot.GetComponentsInChildren<Renderer>().First();

            await controller.InitializeAsync(battle, map, models);

            Assert.IsTrue(previous == null);
            Assert.That(controller.BattleRoot.GetComponentsInChildren<Renderer>(), Is.Not.Empty);
        }

        [Test]
        public async Task InitializeAsync_ModeledShipCatalog_MeasuresAndDeploysEveryShipAsync()
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "SPACE_MAP",
                Kind = BattleKind.Space,
            };
            BaseSceneNode[] templates = TestContent
                .Data.CapitalShips.Cast<BaseSceneNode>()
                .Concat(TestContent.Data.Starfighters)
                .Where(unit => !string.IsNullOrWhiteSpace(unit.ModelPath))
                .ToArray();
            foreach (BaseSceneNode template in templates)
            {
                BaseSceneNode ship = (BaseSceneNode)template.CreateCopy();
                ship.InstanceID = template.TypeID;
                ship.OwnerInstanceID = "FACTION";
                battle.AddCombatants(ship);
            }
            battle.GetParticipants().Single().BattleMapSlotID = "attacker";
            BattleMap map = CreateBattleMap();
            map.GetDeploymentRegions()
                .Add(
                    new BattleMapDeploymentRegion
                    {
                        ParticipantSlotID = "attacker",
                        Bounds = map.PlayableBounds,
                        Facing = new BattleMapVector3(),
                    }
                );

            await controller.InitializeAsync(battle, map, models, "FACTION", deployFleet: true);

            CombatUnit[] combatants = battle.GetParticipants().Single().GetCombatants().ToArray();
            CollectionAssert.AreEquivalent(
                templates.Select(unit => unit.TypeID),
                combatants.Select(unit => unit.Data.TypeID).Distinct()
            );
            for (int index = 0; index < combatants.Length; index++)
            {
                Transform root = controller.BattleRoot.GetChild(0).GetChild(index);
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
                Assert.That(renderers, Is.Not.Empty);
                Bounds actual = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1))
                    actual.Encapsulate(renderer.bounds);
                ModelDimensions measured = combatants[index].Data.ModelSize;
                Assert.That(actual.size.x, Is.EqualTo(measured.Width).Within(0.0001f));
                Assert.That(actual.size.y, Is.EqualTo(measured.Height).Within(0.0001f));
                Assert.That(actual.size.z, Is.EqualTo(measured.Depth).Within(0.0001f));
                Assert.That(actual.min.x, Is.GreaterThanOrEqualTo(map.PlayableBounds.MinimumX));
                Assert.That(actual.max.x, Is.LessThanOrEqualTo(map.PlayableBounds.MaximumX));
                Assert.That(actual.min.y, Is.GreaterThanOrEqualTo(map.PlayableBounds.MinimumY));
                Assert.That(actual.max.y, Is.LessThanOrEqualTo(map.PlayableBounds.MaximumY));
                Assert.That(actual.min.z, Is.GreaterThanOrEqualTo(map.PlayableBounds.MinimumZ));
                Assert.That(actual.max.z, Is.LessThanOrEqualTo(map.PlayableBounds.MaximumZ));
            }
        }

        /// <summary>
        /// Creates a space battle containing one modeled capital ship.
        /// </summary>
        /// <param name="combatant">The independently mutable combatant created for the ship.</param>
        /// <returns>The active battle.</returns>
        private static ActiveBattle CreateBattleWithCombatant(out CombatUnit combatant)
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "SPACE_MAP",
                Kind = BattleKind.Space,
            };
            CapitalShip ship = new CapitalShip
            {
                OwnerInstanceID = "FACTION",
                TypeID = "TEST",
                DisplayName = "Test Ship",
                ModelPath = TestContent
                    .Data.CapitalShips.First(ship =>
                        !string.IsNullOrWhiteSpace(ship.ModelPath) && !ship.CanDestroyPlanets
                    )
                    .ModelPath,
                ModelSize = new ModelDimensions
                {
                    Width = 20f,
                    Height = 10f,
                    Depth = 60f,
                },
            };
            combatant = battle.AddCombatants(ship).Single();
            battle.GetParticipants().Single().BattleMapSlotID = "attacker";
            return battle;
        }

        /// <summary>
        /// Creates an authored space map with a participant camera and playable bounds.
        /// </summary>
        /// <returns>The battle map.</returns>
        private static BattleMap CreateBattleMap()
        {
            BattleMap map = new BattleMap
            {
                InstanceID = "SPACE_MAP",
                Kind = BattleKind.Space,
                PlayableBounds = new BattleMapBounds
                {
                    MinimumX = -2500f,
                    MaximumX = 2500f,
                    MinimumY = -1000f,
                    MaximumY = 1000f,
                    MinimumZ = -3000f,
                    MaximumZ = 3000f,
                },
                Environment = new BattleMapEnvironment
                {
                    BackgroundColor = new BattleMapColor
                    {
                        Red = 0.001f,
                        Green = 0.002f,
                        Blue = 0.006f,
                    },
                    AmbientSkyColor = new BattleMapColor
                    {
                        Red = 0.05f,
                        Green = 0.065f,
                        Blue = 0.11f,
                    },
                    AmbientEquatorColor = new BattleMapColor
                    {
                        Red = 0.025f,
                        Green = 0.035f,
                        Blue = 0.065f,
                    },
                    AmbientGroundColor = new BattleMapColor
                    {
                        Red = 0.015f,
                        Green = 0.022f,
                        Blue = 0.045f,
                    },
                },
            };
            map.GetCameraStarts()
                .Add(
                    new BattleMapCameraStart
                    {
                        ParticipantSlotID = "attacker",
                        Position = new BattleMapVector3
                        {
                            X = 0f,
                            Y = 120f,
                            Z = -2250f,
                        },
                        Rotation = new BattleMapVector3 { X = 12f },
                        FieldOfView = 48f,
                    }
                );
            return map;
        }
    }
}
