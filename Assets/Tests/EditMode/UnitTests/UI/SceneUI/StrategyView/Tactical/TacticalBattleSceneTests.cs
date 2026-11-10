using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Tactical
{
    [TestFixture]
    public class TacticalBattleSceneTests
    {
        private const string _imagePath =
            "Pack/Factions/Alliance/Units/CapitalShips/ALCS003/display";
        private static readonly RectInt _arena = new RectInt(0, 0, 640, 480);
        private GameObject _root;
        private TacticalBattleView _view;
        private TacticalBattleScene _scene;

        /// <summary>
        /// Builds the view whose scene is under test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _view = TacticalBattleViewTestFactory.Create(out _root);
            _scene = _view.Scene;
        }

        /// <summary>
        /// Destroys the test hierarchy.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void GetModelAddress_DisplayImagePath_PointsAtSiblingModelsFolder()
        {
            CreateBattle(out TacticalCapitalShipUnit friendly, out _);

            string address = TacticalBattleScene.GetModelAddress(friendly);

            Assert.AreEqual(
                "Pack/Factions/Alliance/Units/CapitalShips/ALCS003/Models/model",
                address
            );
        }

        [Test]
        public void GetModelAddress_MissingDisplayImagePath_ReturnsNull()
        {
            CreateBattle(out _, out TacticalCapitalShipUnit enemy);

            Assert.IsNull(TacticalBattleScene.GetModelAddress(enemy));
            Assert.IsNull(TacticalBattleScene.GetModelAddress(null));
        }

        [Test]
        public void GetModelDiameter_CapitalShip_ScalesWellAboveItsSimulationRadius()
        {
            CreateBattle(out TacticalCapitalShipUnit friendly, out _);

            float diameter = TacticalBattleScene.GetModelDiameter(friendly);

            Assert.AreEqual(
                (float)friendly.Radius * 2f * TacticalBattleScene.ShipModelDiameterScale,
                diameter,
                0.001f
            );
            Assert.Greater(
                TacticalBattleScene.ShipModelDiameterScale,
                TacticalBattleScene.FighterModelDiameterScale
            );
        }

        [Test]
        public void Sync_LiveBattle_PlacesPlaceholdersOnTheGroundPlaneFacingTheirHeadings()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            _scene.Begin(null);

            SyncSettled(battle);

            Assert.AreEqual(battle.Units.Count, _scene.UnitCount);
            Assert.IsTrue(_scene.gameObject.activeSelf);
            Assert.IsTrue(_scene.TryGetUnitTransform(friendly, out Transform holder));
            Assert.IsTrue(holder.gameObject.activeSelf);
            Assert.AreEqual(PresentationLayers.TacticalBattle, holder.gameObject.layer);
            Assert.IsNotNull(holder.GetComponentInChildren<Renderer>());
            Assert.AreEqual((float)friendly.X, holder.localPosition.x, 0.001f);
            Assert.AreEqual(0f, holder.localPosition.y, 0.001f);
            Assert.AreEqual((float)friendly.Y, holder.localPosition.z, 0.001f);
            Vector3 forward = holder.localRotation * Vector3.forward;
            Assert.AreEqual(Math.Cos(friendly.Heading), forward.x, 0.001);
            Assert.AreEqual(0f, forward.y, 0.001f);
            Assert.AreEqual(Math.Sin(friendly.Heading), forward.z, 0.001);
        }

        [Test]
        public void Sync_FirstFrame_FramesBothFleetsFromBehindThePlayer()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            _scene.Begin(null);

            SyncSettled(battle);

            Assert.IsFalse(_scene.SceneCamera.orthographic);
            Assert.AreEqual((float)(friendly.X + enemy.X) / 2f, _scene.Focus.x, 0.01f);
            Assert.AreEqual((float)(friendly.Y + enemy.Y) / 2f, _scene.Focus.y, 0.01f);
            Assert.AreEqual(
                90f - (float)(friendly.Heading * Mathf.Rad2Deg),
                _scene.YawDegrees,
                0.01f
            );
            Assert.AreEqual(TacticalBattleScene.DefaultPitchDegrees, _scene.PitchDegrees, 0.001f);
            Assert.GreaterOrEqual(_scene.Distance, TacticalBattleScene.MinimumDistance);
            Assert.LessOrEqual(_scene.Distance, _scene.DistanceLimit);
            Assert.IsTrue(_scene.TryProjectUnit(friendly, _arena, out Vector2 near, out _));
            Assert.IsTrue(_scene.TryProjectUnit(enemy, _arena, out Vector2 far, out _));
            Assert.Greater(near.y, far.y);
        }

        [Test]
        public void TryProject_MatchesTheCameraProjection()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            _scene.Begin(null);
            SyncSettled(battle);
            Assert.IsTrue(_scene.TryGetUnitTransform(friendly, out Transform holder));

            Assert.IsTrue(_scene.TryProject(friendly.X, friendly.Y, _arena, out Vector2 source));

            Vector3 viewport = _scene.SceneCamera.WorldToViewportPoint(holder.position);
            Assert.AreEqual(_arena.x + viewport.x * _arena.width, source.x, 0.01f);
            Assert.AreEqual(_arena.y + (1f - viewport.y) * _arena.height, source.y, 0.01f);
        }

        [Test]
        public void TryPickGround_AfterTryProject_RoundTrips()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            _scene.Begin(null);
            _scene.Sync(battle, _arena);

            foreach ((double x, double y) in new[] { (0.0, 0.0), (12.5, -7.0), (-30.0, 15.0) })
            {
                Assert.IsTrue(_scene.TryProject(x, y, _arena, out Vector2 source));
                Assert.IsTrue(_scene.TryPickGround(_arena, source, out Vector2 restored));
                Assert.AreEqual(x, restored.x, 0.2);
                Assert.AreEqual(y, restored.y, 0.2);
            }
        }

        [Test]
        public void Zoom_Steps_StayWithinThePlaySpaceLimits()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            _scene.Begin(null);
            _scene.Sync(battle, _arena);
            float initial = _scene.Distance;

            _scene.Zoom(2f);
            float closer = _scene.Distance;
            _scene.Zoom(-200f);
            float farthest = _scene.Distance;
            _scene.Zoom(200f);

            Assert.Less(closer, initial);
            Assert.AreEqual(_scene.DistanceLimit, farthest, 0.001f);
            Assert.LessOrEqual(_scene.DistanceLimit, TacticalBattleScene.MaximumDistance);
            Assert.AreEqual(TacticalBattleScene.MinimumDistance, _scene.Distance, 0.001f);
        }

        [Test]
        public void Zoom_OutToTheLimit_KeepsBothFleetsOnScreen()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            _scene.Begin(null);
            SyncSettled(battle);

            _scene.Zoom(-200f);

            Assert.IsTrue(_scene.TryProjectUnit(friendly, _arena, out Vector2 near, out _));
            Assert.IsTrue(_scene.TryProjectUnit(enemy, _arena, out Vector2 far, out _));
            Assert.Greater(near.y, far.y);
        }

        [Test]
        public void Rotate_Degrees_OrbitsTheFocusAndClampsTheTilt()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            _scene.Begin(null);
            SyncSettled(battle);
            _scene.FocusOn(friendly);
            float yaw = _scene.YawDegrees;

            _scene.Rotate(90f, 10f);
            float tilted = _scene.PitchDegrees;
            _scene.Rotate(0f, -1000f);

            Assert.AreEqual(Mathf.Repeat(yaw + 90f, 360f), _scene.YawDegrees, 0.001f);
            Assert.AreEqual(TacticalBattleScene.DefaultPitchDegrees + 10f, tilted, 0.001f);
            Assert.AreEqual(TacticalBattleScene.MinimumPitchDegrees, _scene.PitchDegrees, 0.001f);
            Assert.IsTrue(_scene.TryProjectUnit(friendly, _arena, out Vector2 point, out _));
            Assert.AreEqual(_arena.width / 2f, point.x, 1f);
            Assert.AreEqual(_arena.height / 2f, point.y, 1f);
        }

        [Test]
        public void Pan_Drag_KeepsTheGroundPointUnderThePointer()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            _scene.Begin(null);
            _scene.Sync(battle, _arena);
            Vector2 from = new Vector2(320f, 300f);
            Vector2 to = new Vector2(380f, 260f);
            Assert.IsTrue(_scene.TryPickGround(_arena, from, out Vector2 groundBefore));

            _scene.Pan(_arena, from, to);

            Assert.IsTrue(_scene.TryPickGround(_arena, to, out Vector2 groundAfter));
            Assert.AreEqual(groundBefore.x, groundAfter.x, 0.2f);
            Assert.AreEqual(groundBefore.y, groundAfter.y, 0.2f);
        }

        [Test]
        public void PanBy_PastTheArenaEdge_ClampsTheFocusToThePlaySpace()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            _scene.Begin(null);
            _scene.Sync(battle, _arena);

            for (int step = 0; step < 50; step++)
                _scene.PanBy(new Vector2(1f, 1f));

            Assert.LessOrEqual(Math.Abs(_scene.Focus.x), (float)battle.HalfWidth + 0.001f);
            Assert.LessOrEqual(Math.Abs(_scene.Focus.y), (float)battle.HalfHeight + 0.001f);
            Assert.Greater(Math.Abs(_scene.Focus.x), (float)battle.HalfWidth * 0.5f);
        }

        [Test]
        public void FocusOn_Unit_CentersItAndClosesIn()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            _scene.Begin(null);
            SyncSettled(battle);
            _scene.Zoom(-200f);
            float overview = _scene.Distance;

            _scene.FocusOn(friendly);

            Assert.AreEqual((float)friendly.X, _scene.Focus.x, 0.001f);
            Assert.AreEqual((float)friendly.Y, _scene.Focus.y, 0.001f);
            Assert.Less(_scene.Distance, overview);
            Assert.IsTrue(_scene.TryProjectUnit(friendly, _arena, out Vector2 point, out _));
            Assert.AreEqual(_arena.width / 2f, point.x, 1f);
            Assert.AreEqual(_arena.height / 2f, point.y, 1f);
        }

        [Test]
        public void SetVisible_False_DisablesSceneAndCamera()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            _scene.Begin(null);
            _scene.Sync(battle, _arena);

            _scene.SetVisible(false);

            Assert.IsFalse(_scene.gameObject.activeSelf);
            Assert.IsFalse(_scene.SceneCamera.enabled);
            Assert.AreEqual(battle.Units.Count, _scene.UnitCount);
        }

        [Test]
        public void End_AfterSync_RemovesUnitsAndReleasesTheRenderTarget()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            _scene.Begin(null);
            _scene.Sync(battle, _arena);

            _scene.End();

            Assert.AreEqual(0, _scene.UnitCount);
            Assert.IsFalse(_scene.gameObject.activeSelf);
            Assert.IsNull(_scene.SceneCamera.targetTexture);
            Assert.IsFalse(_scene.SceneCamera.enabled);
            Assert.AreEqual(
                0,
                _scene
                    .GetComponentsInChildren<Renderer>(true)
                    .Count(renderer => renderer.name == "Placeholder")
            );
        }

        [Test]
        public void Handle_UnitDestroyed_PlaysAnExplosionWhereTheUnitWas()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out TacticalCapitalShipUnit enemy);
            _scene.Begin(null);
            _scene.Sync(battle, _arena);

            _scene.Handle(
                new[] { new TacticalBattleEvent(TacticalBattleEventKind.UnitDestroyed, enemy) }
            );

            List<Vector3> explosions = _scene.GetActiveExplosionPositions().ToList();
            Assert.AreEqual(1, explosions.Count);
            Assert.AreEqual(TacticalBattleScene.ToScenePosition(enemy.X, enemy.Y), explosions[0]);
            Assert.AreEqual(
                3,
                _scene
                    .GetComponentsInChildren<ParticleSystem>(true)
                    .Count(system => system.particleCount > 0)
            );
        }

        [Test]
        public void Sync_TractoredShip_ShowsTheTractorEffectUntilReleased()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            _scene.Begin(null);
            enemy.TractoredBy = friendly;

            _scene.Sync(battle, _arena);
            bool shownWhileHeld = _scene.HasTractorEffect(enemy);
            ParticleSystem effect = _scene
                .GetComponentsInChildren<ParticleSystem>(true)
                .Single(system => system.name == "TractorBeam");
            bool emittingWhileHeld = effect.isEmitting;
            enemy.TractoredBy = null;
            _scene.Sync(battle, _arena);

            Assert.IsTrue(shownWhileHeld);
            Assert.IsTrue(emittingWhileHeld);
            Assert.AreEqual("Corvette", effect.transform.parent.name);
            Assert.IsFalse(_scene.HasTractorEffect(enemy));
            Assert.IsFalse(_scene.HasTractorEffect(friendly));
            Assert.IsFalse(effect.isEmitting);
        }

        [Test]
        public void FrameArena_AfterSync_BacksOutOverTheCenterOfThePlaySpace()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            _scene.Begin(null);
            SyncSettled(battle);

            _scene.FrameArena();

            Assert.AreEqual(Vector2.zero, _scene.Focus);
            Assert.AreEqual(_scene.DistanceLimit, _scene.Distance, 0.001f);
            Assert.IsTrue(_scene.TryProjectUnit(friendly, _arena, out _, out _));
            Assert.IsTrue(_scene.TryProjectUnit(enemy, _arena, out _, out _));
        }

        [Test]
        public void Sync_OpeningFrame_DropsTheAttackersOutOfHyperspace()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            _scene.Begin(null);
            Vector3 place = TacticalBattleScene.ToScenePosition(friendly.X, friendly.Y);
            Vector3 forward =
                TacticalBattleScene.ToSceneRotation(friendly.Heading) * Vector3.forward;

            _scene.Sync(battle, _arena);
            Assert.IsTrue(_scene.TryGetUnitTransform(friendly, out Transform arriving));
            Assert.IsTrue(_scene.TryGetUnitTransform(enemy, out Transform waiting));
            bool jumpingAtStart = _scene.IsJumping(friendly);
            bool enemyJumpingAtStart = _scene.IsJumping(enemy);
            Vector3 start = arriving.localPosition;
            float startStretch = arriving.localScale.z;
            _scene.Advance(0.5f);
            _scene.Sync(battle, _arena);
            Vector3 midway = arriving.localPosition;
            _scene.Advance(10f);
            _scene.Sync(battle, _arena);

            Assert.IsTrue(jumpingAtStart);
            Assert.IsFalse(enemyJumpingAtStart);
            Assert.IsFalse(_scene.IsJumping(friendly));
            Assert.Greater(Vector3.Distance(start, place), (float)battle.HalfWidth);
            Assert.Greater(Vector3.Dot(place - start, forward), 0f);
            Assert.Greater(startStretch, 1f);
            Assert.Less(Vector3.Distance(midway, place), Vector3.Distance(start, place));
            Assert.Greater(Vector3.Distance(midway, place), 0.01f);
            Assert.AreEqual(place.x, arriving.localPosition.x, 0.001f);
            Assert.AreEqual(place.z, arriving.localPosition.z, 0.001f);
            Assert.AreEqual(1f, arriving.localScale.z, 0.001f);
            Assert.AreEqual(
                TacticalBattleScene.ToScenePosition(enemy.X, enemy.Y),
                waiting.localPosition
            );
        }

        [Test]
        public void Handle_UnitWithdrew_JumpsTheShipOutAlongItsHeadingAndHidesIt()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out TacticalCapitalShipUnit enemy);
            _scene.Begin(null);
            SyncSettled(battle);
            Assert.IsTrue(_scene.TryGetUnitTransform(enemy, out Transform holder));
            Vector3 place = holder.localPosition;
            Vector3 forward = TacticalBattleScene.ToSceneRotation(enemy.Heading) * Vector3.forward;
            enemy.FinishWithdrawal();

            _scene.Handle(
                new[] { new TacticalBattleEvent(TacticalBattleEventKind.UnitWithdrew, enemy) }
            );
            _scene.Advance(0.6f);
            _scene.Sync(battle, _arena);
            bool shownWhileLeaving = holder.gameObject.activeSelf;
            Vector3 leaving = holder.localPosition;
            float stretch = holder.localScale.z;
            _scene.Advance(10f);
            _scene.Sync(battle, _arena);

            Assert.IsTrue(shownWhileLeaving);
            Assert.Greater(Vector3.Dot(leaving - place, forward), 1f);
            Assert.Greater(stretch, 1f);
            Assert.IsFalse(holder.gameObject.activeSelf);
            Assert.IsFalse(_scene.IsJumping(enemy));
        }

        [Test]
        public void Sync_TorpedoLands_PlaysASmallExplosionWhereItHit()
        {
            Starfighter fighter = new Starfighter
            {
                InstanceID = "squadron",
                DisplayName = "squadron",
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                ShieldStrength = 1,
                SublightSpeed = 8,
                Agility = 4,
                LaserCannon = 5,
                LaserRange = 12,
                Torpedoes = 4,
                TorpedoRange = 7,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            CapitalShip carrier = CreateShip("Carrier", 500);
            CapitalShip target = CreateShip("Target", 100000);
            TacticalSpaceBattle battle = new TacticalSpaceBattle(
                CreateConfig(),
                new SystemRandomProvider(5),
                new[] { carrier },
                new[] { fighter },
                new[] { target },
                new List<Starfighter>(),
                false,
                false,
                default,
                default,
                CombatSide.Attacker
            );
            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single();
            TacticalCapitalShipUnit targetUnit = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Ship == target);
            squadron.X = targetUnit.X - targetUnit.HullFootprint - 2;
            squadron.Y = targetUnit.Y;
            _scene.Begin(null);
            _scene.Sync(battle, _arena);

            battle.Advance(TacticalSpaceBattle.RoundSeconds / 4);
            bool torpedoFired = battle.Shots.Any(shot => shot.IsTorpedo);
            _scene.Sync(battle, _arena);
            bool explodedInFlight = _scene.HasActiveExplosions;
            battle.Advance(4);
            _scene.Sync(battle, _arena);

            Assert.IsTrue(torpedoFired);
            Assert.IsFalse(explodedInFlight);
            Assert.IsTrue(_scene.HasActiveExplosions);
            Assert.IsTrue(targetUnit.IsAlive);
        }

        [Test]
        public void Sync_NullBattle_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _scene.Sync(null, _arena));
        }

        /// <summary>
        /// Syncs a battle and lets the opening hyperspace arrival finish, so units sit at their
        /// battle positions.
        /// </summary>
        /// <param name="battle">The battle to present.</param>
        private void SyncSettled(TacticalSpaceBattle battle)
        {
            _scene.Sync(battle, _arena);
            _scene.Advance(10f);
            _scene.Sync(battle, _arena);
        }

        /// <summary>
        /// Creates a two-ship battle whose attacker has a display image and whose defender has none.
        /// </summary>
        /// <param name="friendly">Receives the attacking ship unit.</param>
        /// <param name="enemy">Receives the defending ship unit.</param>
        /// <returns>The battle.</returns>
        private static TacticalSpaceBattle CreateBattle(
            out TacticalCapitalShipUnit friendly,
            out TacticalCapitalShipUnit enemy
        )
        {
            CapitalShip attackerShip = CreateShip("Victory Destroyer", 400);
            attackerShip.DisplayImagePath = _imagePath;
            CapitalShip defenderShip = CreateShip("Corvette", 100);
            TacticalSpaceBattle battle = new TacticalSpaceBattle(
                CreateConfig(),
                new SystemRandomProvider(5),
                new[] { attackerShip },
                new List<Starfighter>(),
                new[] { defenderShip },
                new List<Starfighter>(),
                false,
                false,
                default,
                default,
                CombatSide.Attacker
            );
            friendly = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Ship == attackerShip);
            enemy = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Ship == defenderShip);
            return battle;
        }

        /// <summary>
        /// Creates an unarmed capital ship.
        /// </summary>
        /// <param name="displayName">The display name and instance identifier.</param>
        /// <param name="hull">The hull strength.</param>
        /// <returns>The created ship.</returns>
        private static CapitalShip CreateShip(string displayName, int hull)
        {
            return new CapitalShip
            {
                InstanceID = displayName,
                DisplayName = displayName,
                MaxHullStrength = hull,
                CurrentHullStrength = hull,
                Hyperdrive = 1,
                SublightSpeed = 3,
                Maneuverability = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        /// <summary>
        /// Creates the combat configuration the scene tests battle with.
        /// </summary>
        /// <returns>The combat configuration.</returns>
        private static GameConfig.SpaceCombatConfig CreateConfig()
        {
            return new GameConfig.SpaceCombatConfig
            {
                AdmiralLeadershipDivisor = 10,
                CommanderCombatDivisor = 20,
                LaserCannonCapitalDamageMultiplier = 1.0 / 6.0,
                AutoResolveFighterWeaponRechargeMultiplier = 3.751,
                AutoResolveMaximumIterations = 4096,
                AutoResolveStagnationIterations = 1200,
                AutoResolveRetreatStrengthRatio = 0,
                AutoResolveMinimumManeuverRatio = 0.1,
                AutoResolveTargetScanDivisor = 3,
                AutoResolveStartingDistance = 24,
                AutoResolveWithdrawalDistance = 10,
                AutoResolveComponentDamageInterval = 1,
                AutoResolveComponentDamageRollMaximum = 10,
                AutoResolveComponentDelayMinimum = 30,
                AutoResolveComponentDelayMaximum = 50,
                AutoResolveComponentDelayRecovery = 1,
            };
        }
    }
}
