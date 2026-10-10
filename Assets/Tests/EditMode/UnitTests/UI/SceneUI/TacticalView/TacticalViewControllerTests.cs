using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEditor;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalViewControllerTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab";

        private GameObject instance;
        private TacticalViewController controller;

        /// <summary>
        /// Loads an isolated generated tactical root for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            Assert.IsNotNull(prefab, $"Missing generated tactical prefab: {_prefabPath}");
            instance = UnityEngine.Object.Instantiate(prefab);
            controller = instance.GetComponent<TacticalViewController>();
        }

        /// <summary>
        /// Releases the generated tactical root after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(instance);
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
        }

        [Test]
        public void Initialize_MatchingBattleAndMap_BindsPresentationState()
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "SPACE_MAP",
                Kind = BattleKind.Space,
            };
            BattleMap map = new BattleMap { InstanceID = "SPACE_MAP", Kind = BattleKind.Space };

            controller.Initialize(battle, map);

            Assert.AreSame(battle, controller.ActiveBattle);
            Assert.AreSame(map, controller.BattleMap);
        }

        [Test]
        public void Initialize_BattleCombatant_CreatesModelPresentation()
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

            controller.Initialize(battle, map, "FACTION");

            Transform participantRoot = controller.BattleRoot.GetChild(0);
            Transform unitRoot = participantRoot.GetChild(0);
            ContentModelBinding modelBinding = unitRoot.GetComponent<ContentModelBinding>();
            Assert.AreEqual(new Vector3(25f, 10f, -100f), unitRoot.localPosition);
            Assert.That(
                Quaternion.Angle(Quaternion.LookRotation(Vector3.right), unitRoot.localRotation),
                Is.LessThan(0.01f)
            );
            Assert.IsNotNull(modelBinding);
            Assert.AreEqual(
                "Pack/Factions/Test/Units/CapitalShips/TEST/Models/model",
                modelBinding.Address
            );
        }

        [Test]
        public void Initialize_ViewerFaction_AppliesParticipantCameraStart()
        {
            ActiveBattle battle = CreateBattleWithCombatant(out _);
            BattleMap map = CreateBattleMap();

            controller.Initialize(battle, map, "FACTION");

            Assert.AreEqual(
                new Vector3(0f, 120f, -2250f),
                controller.BattleCamera.transform.position
            );
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
        public void Initialize_AuthoredMapObjects_CreatesMapPresentation()
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

            controller.Initialize(battle, map, "FACTION");

            Transform stars = controller.MapRoot.Find("Distant Stars");
            Light starlight = controller.MapRoot.Find("Starlight Key")?.GetComponent<Light>();
            Assert.IsNotNull(stars?.GetComponent<ParticleSystem>());
            Assert.AreEqual(4, stars.GetComponent<ParticleSystem>().particleCount);
            Assert.IsNotNull(starlight);
            Assert.AreEqual(1.15f, starlight.intensity);
            Assert.AreSame(starlight, RenderSettings.sun);
        }

        [Test]
        public void Initialize_MismatchedMapIdentifier_ThrowsArgumentException()
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "SPACE_MAP",
                Kind = BattleKind.Space,
            };
            BattleMap map = new BattleMap { InstanceID = "OTHER_MAP", Kind = BattleKind.Space };

            Assert.Throws<ArgumentException>(() => controller.Initialize(battle, map));
        }

        [Test]
        public void Initialize_MismatchedBattleKind_ThrowsArgumentException()
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "MAP",
                Kind = BattleKind.Space,
            };
            BattleMap map = new BattleMap { InstanceID = "MAP", Kind = BattleKind.Ground };

            Assert.Throws<ArgumentException>(() => controller.Initialize(battle, map));
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
                ModelPath = "Pack/Factions/Test/Units/CapitalShips/TEST/Models/model",
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
