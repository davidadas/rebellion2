using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
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
        [TestCase(TacticalBattleSide.Attacker, TacticalWeaponType.IonCannon, 0f, 0f, 1f)]
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
                .GetField("orangeSplitImpactFrames", BindingFlags.Instance | BindingFlags.NonPublic)
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
                    TacticalCombatEvent.WeaponImpact(source, target, TacticalWeaponType.Torpedo),
                }
            );

            Assert.AreEqual(
                Color.white,
                root.GetComponentInChildren<LineRenderer>().sharedMaterial.color
            );
        }

        [Test]
        public void PresentEvents_SuperlaserFired_CreatesDedicatedBeamEffect()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender);

            renderer.PresentEvents(new[] { TacticalCombatEvent.SuperlaserFired(source, target) });

            LineRenderer line = root.GetComponentInChildren<LineRenderer>();
            Assert.IsNotNull(line);
            Assert.AreEqual(0.9f, line.startWidth);
            Assert.AreEqual(0.5f, line.endWidth);
            Assert.IsTrue(renderer.HasActiveCombatEffects);
        }

        [Test]
        public void PresentEvents_UnitDestroyed_CreatesPyrotechnicEffect()
        {
            TacticalUnitState unit = CreateCapitalShip(TacticalBattleSide.Attacker);

            renderer.PresentEvents(
                new[]
                {
                    TacticalCombatEvent.UnitLifecycle(TacticalCombatEventKind.UnitDestroyed, unit),
                }
            );

            Assert.IsTrue(renderer.HasActiveCombatEffects);
            Assert.IsNotNull(root.GetComponentInChildren<TacticalDestructionEffectView>());
        }

        [TestCase(
            TacticalWeaponType.LaserCannon,
            TacticalImpactState.Shield,
            TacticalUnitKind.CapitalShip,
            "orangeSplitImpactFrames"
        )]
        [TestCase(
            TacticalWeaponType.LaserCannon,
            TacticalImpactState.Destroyed,
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
            TacticalWeaponType.Turbolaser,
            TacticalImpactState.Destroyed,
            TacticalUnitKind.CapitalShip,
            "blueNetImpactFrames"
        )]
        [TestCase(
            TacticalWeaponType.Turbolaser,
            TacticalImpactState.Shield,
            TacticalUnitKind.Fighters,
            "blueBlastImpactFrames"
        )]
        [TestCase(
            TacticalWeaponType.IonCannon,
            TacticalImpactState.Shield,
            TacticalUnitKind.CapitalShip,
            "orangeDoubleBlastImpactFrames"
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
    }
}
