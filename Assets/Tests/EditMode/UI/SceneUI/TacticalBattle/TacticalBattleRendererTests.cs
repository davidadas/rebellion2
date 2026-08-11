using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.TacticalBattle
{
    [TestFixture]
    public sealed class TacticalBattleRendererTests
    {
        private GameObject root;
        private TacticalBattleRenderer renderer;
        private Texture2D texture;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TacticalBattleRendererTests");
            renderer = root.AddComponent<TacticalBattleRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void CreatePlanetDecoration_ConfiguredPlanet_CreatesTacticalPlanetSprite()
        {
            const string address = "Pack/Shared/Tactical/Environment/Planets/temperate";
            texture = new Texture2D(256, 256);
            FakeContentAssetSource contentAssets = new FakeContentAssetSource(address, texture);
            Planet planet = new Planet { TacticalTexturePath = address };

            typeof(TacticalBattleRenderer)
                .GetMethod("CreatePlanetDecoration", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(renderer, new object[] { planet, contentAssets, 150f });

            SpriteRenderer spriteRenderer = root.GetComponentInChildren<SpriteRenderer>();
            Assert.AreEqual("Tactical Planet", spriteRenderer.gameObject.name);
            Assert.AreSame(texture, spriteRenderer.sprite.texture);
            Assert.AreEqual(address, contentAssets.RequestedAddress);
        }

        [Test]
        public void CreateHolocube_Enabled_CreatesBattlefieldBoundary()
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);

            InvokeCreateHolocube(grid, true);

            MeshFilter meshFilter = root.GetComponentInChildren<MeshFilter>();
            Assert.AreEqual("Tactical Holocube", meshFilter.gameObject.name);
            Assert.AreEqual(8, meshFilter.sharedMesh.vertexCount);
            Assert.AreEqual(24, meshFilter.sharedMesh.GetIndices(0).Length);
            Assert.That(meshFilter.sharedMesh.bounds.extents, Is.EqualTo(Vector3.one * 100f));
        }

        [Test]
        public void CreateHolocube_Disabled_DoesNotCreateBattlefieldBoundary()
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);

            InvokeCreateHolocube(grid, false);

            Assert.IsNull(root.GetComponentInChildren<MeshFilter>());
        }

        [Test]
        public void SetNavigationRoute_OrderedRoute_HighlightsOnlyLaterWaypoints()
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);
            InvokeCreateNavigationGrid(grid);
            TacticalNavPoint first = grid.GetPoints(0)[0];
            TacticalNavPoint second = grid.GetPoints(0)[1];

            renderer.SetNavigationRoute(new[] { first, second });

            TacticalNavigationMarker[] markers =
                root.GetComponentsInChildren<TacticalNavigationMarker>(true);
            Assert.That(
                FindMarker(markers, first).GetComponent<MeshRenderer>().sharedMaterial.color,
                Is.Not.EqualTo(Color.magenta)
            );
            Assert.That(
                FindMarker(markers, second).GetComponent<MeshRenderer>().sharedMaterial.color,
                Is.EqualTo(Color.magenta)
            );
        }

        [Test]
        public void SetNavigationRoute_ReplacedRoute_RestoresPriorWaypointLayerColor()
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);
            InvokeCreateNavigationGrid(grid);
            TacticalNavPoint first = grid.GetPoints(0)[0];
            TacticalNavPoint second = grid.GetPoints(0)[1];
            renderer.SetNavigationRoute(new[] { first, second });

            renderer.SetNavigationRoute(new[] { first });

            TacticalNavigationMarker marker = FindMarker(
                root.GetComponentsInChildren<TacticalNavigationMarker>(true),
                second
            );
            Assert.That(
                marker.GetComponent<MeshRenderer>().sharedMaterial,
                Is.SameAs(marker.NormalMaterial)
            );
        }

        [Test]
        public void HasActiveCombatEffects_TransientEffectExists_ReturnsTrue()
        {
            GameObject effect = new GameObject("Effect");
            effect.transform.SetParent(root.transform, false);
            effect.AddComponent<TacticalCombatEffectView>();

            bool hasActiveEffects = renderer.HasActiveCombatEffects;

            Assert.IsTrue(hasActiveEffects);
        }

        [TestCase(TacticalWeaponType.LaserCannon, 0.5f)]
        [TestCase(TacticalWeaponType.Turbolaser, 0.65f)]
        [TestCase(TacticalWeaponType.IonCannon, 0.4f)]
        [TestCase(TacticalWeaponType.Torpedo, 0.2f)]
        public void PresentEvents_WeaponImpact_UsesWeaponFamilyBeamWidth(
            TacticalWeaponType weaponType,
            float expectedWidth
        )
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);

            renderer.PresentEvents(
                new[] { TacticalCombatEvent.WeaponImpact(source, target, weaponType) }
            );

            LineRenderer line = root.GetComponentInChildren<LineRenderer>();
            Assert.AreEqual(expectedWidth, line.startWidth);
            Assert.AreEqual(expectedWidth, line.endWidth);
        }

        [TestCase(TacticalBattleSide.Attacker, TacticalWeaponType.LaserCannon, 1f, 0f, 0f)]
        [TestCase(TacticalBattleSide.Defender, TacticalWeaponType.Turbolaser, 0f, 1f, 0f)]
        [TestCase(TacticalBattleSide.Attacker, TacticalWeaponType.IonCannon, 1f, 0f, 0f)]
        public void PresentEvents_CapitalShipWeaponImpact_UsesWeaponBeamColor(
            TacticalBattleSide side,
            TacticalWeaponType weaponType,
            float red,
            float green,
            float blue
        )
        {
            TacticalUnitState source = CreateCapitalShip(side);
            TacticalUnitState target = CreateCapitalShip(
                side == TacticalBattleSide.Attacker
                    ? TacticalBattleSide.Defender
                    : TacticalBattleSide.Attacker
            );

            renderer.PresentEvents(
                new[] { TacticalCombatEvent.WeaponImpact(source, target, weaponType) }
            );

            Color color = root.GetComponentInChildren<LineRenderer>().sharedMaterial.color;
            Assert.AreEqual(new Color(red, green, blue), color);
        }

        [Test]
        public void PresentEvents_WeaponImpactWithMappedTarget_CreatesImpactAnimation()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);
            RegisterUnitView(target);
            typeof(TacticalBattleRenderer)
                .GetField("blueSpreadImpactFrames", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(renderer, new Sprite[1]);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        source,
                        target,
                        TacticalWeaponType.LaserCannon
                    ),
                }
            );

            Assert.IsNotNull(root.GetComponentInChildren<TacticalOneShotEffectView>());
        }

        [TestCase(TacticalImpactState.Shield, "blueSpreadImpactFrames", 2.5f)]
        [TestCase(TacticalImpactState.Hull, "orangeBlastImpactFrames", 5f)]
        public void PresentEvents_WeaponImpact_UsesImpactStateEffectDiameter(
            TacticalImpactState impactState,
            string frameFieldName,
            float expectedDiameter
        )
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);
            RegisterUnitView(target);
            typeof(TacticalBattleRenderer)
                .GetField(frameFieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(renderer, new Sprite[1]);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        source,
                        target,
                        TacticalWeaponType.LaserCannon,
                        impactState
                    ),
                }
            );

            TacticalOneShotEffectView effect =
                root.GetComponentInChildren<TacticalOneShotEffectView>();
            Assert.AreEqual(expectedDiameter, effect.transform.lossyScale.x);
        }

        [Test]
        public void PresentEvents_CapitalShipDestroyed_UsesDestructionEffectDiameter()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            typeof(TacticalBattleRenderer)
                .GetField("destructionEffectFrames", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(renderer, new Sprite[1]);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.UnitLifecycle(
                        TacticalCombatEventKind.UnitDestroyed,
                        source
                    ),
                }
            );

            TacticalOneShotEffectView effect =
                root.GetComponentInChildren<TacticalOneShotEffectView>();
            Assert.AreEqual(7.5f, effect.transform.lossyScale.x);
        }

        [Test]
        public void PresentEvents_FighterTorpedoImpact_UsesWhiteBeam()
        {
            TacticalUnitState source = TacticalUnitState.FromFighters(
                new Starfighter { CurrentSquadronSize = 1 },
                TacticalBattleSide.Attacker
            );
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        source,
                        target,
                        TacticalWeaponType.Torpedo,
                        attackStrength: 13
                    ),
                }
            );

            Assert.AreEqual(
                Color.white,
                root.GetComponentInChildren<LineRenderer>().sharedMaterial.color
            );
        }

        [TestCase(TacticalWeaponType.LaserCannon, 29, 0.5f)]
        [TestCase(TacticalWeaponType.Turbolaser, 35, 1f)]
        [TestCase(TacticalWeaponType.IonCannon, 32, 1f)]
        [TestCase(TacticalWeaponType.Torpedo, 13, 0.2f)]
        public void PresentEvents_HeavyWeaponImpact_UsesTierBeamWidth(
            TacticalWeaponType weaponType,
            int attackStrength,
            float expectedWidth
        )
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        source,
                        target,
                        weaponType,
                        attackStrength: attackStrength
                    ),
                }
            );

            Assert.AreEqual(expectedWidth, root.GetComponentInChildren<LineRenderer>().startWidth);
        }

        [TestCase(TacticalWeaponType.LaserCannon, 29, 0f, 0f, 1f)]
        [TestCase(TacticalWeaponType.Turbolaser, 35, 1f, 1f, 1f)]
        [TestCase(TacticalWeaponType.IonCannon, 32, 1f, 1f, 1f)]
        [TestCase(TacticalWeaponType.Torpedo, 13, 1f, 1f, 1f)]
        public void PresentEvents_HeavyWeaponImpact_UsesTierBeamColor(
            TacticalWeaponType weaponType,
            int attackStrength,
            float red,
            float green,
            float blue
        )
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        source,
                        target,
                        weaponType,
                        attackStrength: attackStrength
                    ),
                }
            );

            Assert.AreEqual(
                new Color(red, green, blue),
                root.GetComponentInChildren<LineRenderer>().sharedMaterial.color
            );
        }

        [TestCase(TacticalWeaponType.LaserCannon, 29, 1f)]
        [TestCase(TacticalWeaponType.Turbolaser, 35, 2f)]
        [TestCase(TacticalWeaponType.IonCannon, 32, 2f)]
        [TestCase(TacticalWeaponType.Torpedo, 13, 2f)]
        public void PresentEvents_HeavyWeaponImpact_UsesTierBeamLifetime(
            TacticalWeaponType weaponType,
            int attackStrength,
            float expectedLifetime
        )
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        source,
                        target,
                        weaponType,
                        attackStrength: attackStrength
                    ),
                }
            );

            TacticalCombatEffectView effect =
                root.GetComponentInChildren<TacticalCombatEffectView>();
            float lifetime = (float)
                typeof(TacticalCombatEffectView)
                    .GetField("lifetime", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(effect);
            Assert.AreEqual(expectedLifetime, lifetime);
        }

        [Test]
        public void PresentEvents_HeavyFighterTurbolaserImpact_UsesMediumBeamWidth()
        {
            TacticalUnitState source = TacticalUnitState.FromFighters(
                new Starfighter { CurrentSquadronSize = 1 },
                TacticalBattleSide.Attacker
            );
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        source,
                        target,
                        TacticalWeaponType.Turbolaser,
                        attackStrength: 35
                    ),
                }
            );

            Assert.AreEqual(0.75f, root.GetComponentInChildren<LineRenderer>().startWidth);
        }

        [Test]
        public void PresentEvents_WeaponImpact_StartsBeamAtSource()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);
            source.Position = new System.Numerics.Vector3(1f, 2f, 3f);
            target.Position = new System.Numerics.Vector3(4f, 5f, 6f);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        source,
                        target,
                        TacticalWeaponType.LaserCannon
                    ),
                }
            );

            LineRenderer line = root.GetComponentInChildren<LineRenderer>();
            Assert.AreEqual(line.GetPosition(0), line.GetPosition(1));
        }

        [Test]
        public void PresentEvents_SuperlaserFired_CreatesDedicatedBeamEffect()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);

            renderer.PresentEvents(new[] { TacticalCombatEvent.SuperlaserFired(source, target) });

            LineRenderer line = root.GetComponentInChildren<LineRenderer>();
            Assert.IsNotNull(line);
            Assert.AreEqual(1f, line.startWidth);
            Assert.AreEqual(1f, line.endWidth);
            Assert.IsTrue(renderer.HasActiveCombatEffects);
        }

        [Test]
        public void PresentEvents_SuperlaserFired_UsesSourceFactionBeamColor()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Defender);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Attacker);

            renderer.PresentEvents(new[] { TacticalCombatEvent.SuperlaserFired(source, target) });

            Assert.AreEqual(
                Color.green,
                root.GetComponentInChildren<LineRenderer>().sharedMaterial.color
            );
        }

        [Test]
        public void PresentEvents_UnitDestroyed_CreatesPyrotechnicEffect()
        {
            TacticalUnitState unit = CreateCapitalShip(TacticalBattleSide.Attacker);
            typeof(TacticalBattleRenderer)
                .GetField("destructionEffectFrames", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(renderer, new Sprite[1]);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.UnitLifecycle(TacticalCombatEventKind.UnitDestroyed, unit),
                }
            );

            Assert.IsTrue(renderer.HasActiveCombatEffects);
            Assert.IsNotNull(root.GetComponentInChildren<TacticalOneShotEffectView>());
        }

        [Test]
        public void PresentEvents_UnitDestroyedWithPyrotechnicsDisabled_DoesNotCreateEffect()
        {
            TacticalUnitState unit = CreateCapitalShip(TacticalBattleSide.Attacker);
            typeof(TacticalBattleRenderer)
                .GetField("showPyrotechnics", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(renderer, false);
            typeof(TacticalBattleRenderer)
                .GetField("destructionEffectFrames", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(renderer, new Sprite[1]);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.UnitLifecycle(TacticalCombatEventKind.UnitDestroyed, unit),
                }
            );

            Assert.IsNull(root.GetComponentInChildren<TacticalOneShotEffectView>());
        }

        [Test]
        public void PresentEvents_WeaponImpactWithPyrotechnicsDisabled_DoesNotCreateImpactEffect()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);
            RegisterUnitView(target);
            typeof(TacticalBattleRenderer)
                .GetField("showPyrotechnics", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(renderer, false);
            typeof(TacticalBattleRenderer)
                .GetField("blueSpreadImpactFrames", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(renderer, new Sprite[1]);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        source,
                        target,
                        TacticalWeaponType.LaserCannon
                    ),
                }
            );

            Assert.IsNull(root.GetComponentInChildren<TacticalOneShotEffectView>());
        }

        [Test]
        public void PresentEvents_WeaponImpactWithPyrotechnicsDisabled_CreatesWeaponBeam()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);
            typeof(TacticalBattleRenderer)
                .GetField("showPyrotechnics", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(renderer, false);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        source,
                        target,
                        TacticalWeaponType.LaserCannon
                    ),
                }
            );

            Assert.IsNotNull(root.GetComponentInChildren<LineRenderer>());
        }

        [TestCase(
            TacticalWeaponType.LaserCannon,
            TacticalImpactState.Shield,
            TacticalUnitKind.CapitalShip,
            "blueSpreadImpactFrames"
        )]
        [TestCase(
            TacticalWeaponType.Turbolaser,
            TacticalImpactState.Shield,
            TacticalUnitKind.CapitalShip,
            "blueSpreadImpactFrames"
        )]
        [TestCase(
            TacticalWeaponType.Turbolaser,
            TacticalImpactState.Hull,
            TacticalUnitKind.CapitalShip,
            "orangeBlastImpactFrames"
        )]
        [TestCase(
            TacticalWeaponType.IonCannon,
            TacticalImpactState.Shield,
            TacticalUnitKind.CapitalShip,
            "orangeSplitImpactFrames"
        )]
        [TestCase(
            TacticalWeaponType.IonCannon,
            TacticalImpactState.Hull,
            TacticalUnitKind.CapitalShip,
            "blueNetImpactFrames"
        )]
        [TestCase(
            TacticalWeaponType.Torpedo,
            TacticalImpactState.Hull,
            TacticalUnitKind.CapitalShip,
            "blueBlastImpactFrames"
        )]
        public void GetWeaponImpactFrames_ResolvedImpact_ReturnsMappedAnimation(
            TacticalWeaponType weaponType,
            TacticalImpactState impactState,
            TacticalUnitKind targetKind,
            string expectedFieldName
        )
        {
            Sprite[] expectedFrames = new Sprite[1];
            typeof(TacticalBattleRenderer)
                .GetField(expectedFieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(renderer, expectedFrames);
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target =
                targetKind == TacticalUnitKind.Fighters
                    ? TacticalUnitState.FromFighters(
                        new Starfighter { CurrentSquadronSize = 1 },
                        TacticalBattleSide.Defender
                    )
                    : CreateCapitalShip(TacticalBattleSide.Defender);
            TacticalCombatEvent combatEvent = TacticalCombatEvent.WeaponImpact(
                source,
                target,
                weaponType,
                impactState
            );

            Sprite[] actualFrames = (Sprite[])
                typeof(TacticalBattleRenderer)
                    .GetMethod(
                        "GetWeaponImpactFrames",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )
                    ?.Invoke(renderer, new object[] { combatEvent });

            Assert.AreSame(expectedFrames, actualFrames);
        }

        [TestCase(TacticalImpactState.Shield, TacticalUnitKind.Fighters)]
        [TestCase(TacticalImpactState.Hull, TacticalUnitKind.Fighters)]
        [TestCase(TacticalImpactState.Destroyed, TacticalUnitKind.CapitalShip)]
        public void GetWeaponImpactFrames_UnrenderedImpact_ReturnsNoAnimation(
            TacticalImpactState impactState,
            TacticalUnitKind targetKind
        )
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target =
                targetKind == TacticalUnitKind.Fighters
                    ? TacticalUnitState.FromFighters(
                        new Starfighter { CurrentSquadronSize = 1 },
                        TacticalBattleSide.Defender
                    )
                    : CreateCapitalShip(TacticalBattleSide.Defender);
            TacticalCombatEvent combatEvent = TacticalCombatEvent.WeaponImpact(
                source,
                target,
                TacticalWeaponType.LaserCannon,
                impactState
            );

            Sprite[] actualFrames = (Sprite[])
                typeof(TacticalBattleRenderer)
                    .GetMethod(
                        "GetWeaponImpactFrames",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )
                    ?.Invoke(renderer, new object[] { combatEvent });

            Assert.IsEmpty(actualFrames);
        }

        [Test]
        public void PresentEvents_FighterDestroyed_DoesNotCreatePyrotechnicAnimation()
        {
            TacticalUnitState fighter = TacticalUnitState.FromFighters(
                new Starfighter { CurrentSquadronSize = 1 },
                TacticalBattleSide.Attacker
            );

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.UnitLifecycle(
                        TacticalCombatEventKind.UnitDestroyed,
                        fighter
                    ),
                }
            );

            Assert.IsNull(root.GetComponentInChildren<TacticalOneShotEffectView>());
        }

        /// <summary>
        /// Builds the runtime-only marker hierarchy through the renderer's initialization helper.
        /// </summary>
        /// <param name="grid">The navigation grid to render.</param>
        private void InvokeCreateNavigationGrid(TacticalNavigationGrid grid)
        {
            typeof(TacticalBattleRenderer)
                .GetMethod("CreateNavigationGrid", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(renderer, new object[] { grid });
        }

        /// <summary>
        /// Builds the optional tactical boundary through the renderer's initialization helper.
        /// </summary>
        /// <param name="grid">The navigation grid that defines the boundary.</param>
        /// <param name="visible">Whether the boundary should be created.</param>
        private void InvokeCreateHolocube(TacticalNavigationGrid grid, bool visible)
        {
            typeof(TacticalBattleRenderer)
                .GetMethod("CreateHolocube", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(renderer, new object[] { grid, visible });
        }

        /// <summary>
        /// Creates and registers one target presentation for event-projection tests.
        /// </summary>
        /// <param name="state">The tactical unit represented by the view.</param>
        private void RegisterUnitView(TacticalUnitState state)
        {
            GameObject unitObject = new GameObject("Tactical Unit");
            unitObject.transform.SetParent(root.transform, false);
            TacticalUnitView unitView = unitObject.AddComponent<TacticalUnitView>();
            unitView.Initialize(state);
            typeof(TacticalUnitView)
                .GetField("presentationBounds", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(unitView, new Bounds(Vector3.zero, Vector3.one));
            Dictionary<TacticalUnitState, TacticalUnitView> views =
                (Dictionary<TacticalUnitState, TacticalUnitView>)
                    typeof(TacticalBattleRenderer)
                        .GetField(
                            "unitViewsByState",
                            BindingFlags.Instance | BindingFlags.NonPublic
                        )
                        ?.GetValue(renderer);
            views.Add(state, unitView);
        }

        /// <summary>
        /// Creates one minimal capital-ship state for renderer tests.
        /// </summary>
        /// <param name="side">The tactical side assigned to the state.</param>
        /// <returns>The initialized tactical state.</returns>
        private static TacticalUnitState CreateCapitalShip(TacticalBattleSide side)
        {
            return TacticalUnitState.FromCapitalShip(
                new CapitalShip { CurrentHullStrength = 1, MaxHullStrength = 1 },
                side
            );
        }

        /// <summary>
        /// Finds the marker associated with one navigation-point instance.
        /// </summary>
        /// <param name="markers">The rendered markers to search.</param>
        /// <param name="point">The represented navigation point.</param>
        /// <returns>The matching marker.</returns>
        private static TacticalNavigationMarker FindMarker(
            TacticalNavigationMarker[] markers,
            TacticalNavPoint point
        )
        {
            return System.Array.Find(markers, marker => ReferenceEquals(marker.Point, point));
        }

        private sealed class FakeContentAssetSource : IContentAssetSource
        {
            private readonly string address;
            private readonly Texture2D texture;

            public FakeContentAssetSource(string address, Texture2D texture)
            {
                this.address = address;
                this.texture = texture;
            }

            public string RequestedAddress { get; private set; }

            public Texture2D GetTexture(string requestedAddress)
            {
                RequestedAddress = requestedAddress;
                return requestedAddress == address ? texture : null;
            }

            public Sprite GetSprite(string requestedAddress)
            {
                return null;
            }
        }
    }
}
