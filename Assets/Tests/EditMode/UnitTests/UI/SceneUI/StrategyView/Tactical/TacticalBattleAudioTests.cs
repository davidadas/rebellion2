using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Tactical
{
    [TestFixture]
    public class TacticalBattleAudioTests
    {
        private readonly List<string> _played = new List<string>();
        private TacticalSpaceBattle _battle;
        private TacticalBattleUnit _ship;
        private TacticalBattleUnit _fighter;

        /// <summary>
        /// Creates a battle supplying units for synthetic events.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _played.Clear();
            CapitalShip carrier = CreateShip("carrier", 500, 10);
            carrier.StarfighterCapacity = 1;
            Starfighter squadron = new Starfighter
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
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            carrier.AddChild(squadron);
            squadron.SetParent(carrier);
            _battle = new TacticalSpaceBattle(
                CreateConfig(),
                new SystemRandomProvider(3),
                new[] { carrier },
                new[] { squadron },
                new[] { CreateShip("target", 1000, 0) },
                new List<Starfighter>(),
                false,
                false,
                default,
                default,
                CombatSide.Attacker
            );
            _ship = _battle.Units.First(unit => !unit.IsStarfighter);
            _fighter = _battle.Units.First(unit => unit.IsStarfighter);
        }

        [Test]
        public void Constructor_NullPlayback_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TacticalBattleAudio(null));
        }

        [Test]
        public void Begin_WithFighters_PlaysLaunchCueOnce()
        {
            TacticalBattleAudio audio = CreateAudio();

            audio.Begin(hasFighters: true);

            CollectionAssert.AreEqual(new[] { TacticalBattleAudio.FighterLaunchPath }, _played);
        }

        [Test]
        public void Begin_WithoutFighters_PlaysNothing()
        {
            TacticalBattleAudio audio = CreateAudio();

            audio.Begin(hasFighters: false);

            Assert.IsEmpty(_played);
        }

        [Test]
        public void Handle_ManyWeaponFires_PlaysAtMostTwoPerUpdateAndPrefersCapitalShips()
        {
            TacticalBattleAudio audio = CreateAudio();
            audio.Begin(hasFighters: false);
            List<TacticalBattleEvent> events = new List<TacticalBattleEvent>
            {
                Fired(_fighter),
                Fired(_fighter),
                Fired(_ship),
                Fired(_fighter),
            };

            audio.Handle(events);
            audio.Handle(events);

            Assert.AreEqual(2, _played.Count);
            Assert.IsTrue(_played.All(path => TacticalBattleAudio.WeaponFirePaths.Contains(path)));
            Assert.AreNotEqual(_played[0], _played[1]);
        }

        [Test]
        public void Handle_WeaponFireAfterCooldown_PlaysAgain()
        {
            TacticalBattleAudio audio = CreateAudio();
            audio.Begin(hasFighters: false);
            List<TacticalBattleEvent> events = new List<TacticalBattleEvent> { Fired(_ship) };

            audio.Handle(events);
            audio.Advance(0.5f);
            audio.Handle(events);

            Assert.AreEqual(2, _played.Count);
        }

        [Test]
        public void Handle_UnitHit_PlaysOneImpactPerCooldown()
        {
            TacticalBattleAudio audio = CreateAudio();
            audio.Begin(hasFighters: false);
            List<TacticalBattleEvent> events = new List<TacticalBattleEvent>
            {
                new TacticalBattleEvent(TacticalBattleEventKind.UnitHit, _ship),
                new TacticalBattleEvent(TacticalBattleEventKind.UnitHit, _ship),
            };

            audio.Handle(events);
            audio.Handle(events);
            audio.Advance(1f);
            audio.Handle(events);

            Assert.AreEqual(2, _played.Count);
            Assert.IsTrue(
                _played.All(path => TacticalBattleAudio.WeaponImpactPaths.Contains(path))
            );
        }

        [Test]
        public void Handle_DestroyedAndWithdrawnUnits_PlaysExplosionAndHyperspace()
        {
            TacticalBattleAudio audio = CreateAudio();
            audio.Begin(hasFighters: false);

            audio.Handle(
                new List<TacticalBattleEvent>
                {
                    new TacticalBattleEvent(TacticalBattleEventKind.UnitDestroyed, _ship),
                    new TacticalBattleEvent(TacticalBattleEventKind.UnitWithdrew, _fighter),
                    new TacticalBattleEvent(TacticalBattleEventKind.UnitWithdrew, _fighter),
                }
            );

            CollectionAssert.AreEqual(
                new[] { TacticalBattleAudio.ExplosionPath, TacticalBattleAudio.HyperspacePath },
                _played
            );
        }

        [Test]
        public void Handle_UnavailableSounds_SkipsPlaybackAndChecksEachPathOnce()
        {
            int checks = 0;
            TacticalBattleAudio audio = new TacticalBattleAudio(
                path => _played.Add(path),
                path =>
                {
                    checks++;
                    return false;
                }
            );
            audio.Begin(hasFighters: true);
            List<TacticalBattleEvent> events = new List<TacticalBattleEvent>
            {
                Fired(_ship),
                new TacticalBattleEvent(TacticalBattleEventKind.UnitDestroyed, _ship),
            };

            audio.Handle(events);
            audio.Advance(1f);
            audio.Handle(events);

            Assert.IsEmpty(_played);
            Assert.AreEqual(
                1 + TacticalBattleAudio.WeaponFirePaths.Count + 1,
                checks,
                "Each distinct path should be probed once."
            );
        }

        [Test]
        public void Handle_NoEvents_PlaysNothing()
        {
            TacticalBattleAudio audio = CreateAudio();

            audio.Handle(Array.Empty<TacticalBattleEvent>());
            audio.Handle(null);

            Assert.IsEmpty(_played);
        }

        /// <summary>
        /// Creates audio that records every played path.
        /// </summary>
        /// <returns>The audio under test.</returns>
        private TacticalBattleAudio CreateAudio()
        {
            return new TacticalBattleAudio(path => _played.Add(path));
        }

        /// <summary>
        /// Creates a weapon-fired event for a unit.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <returns>The event.</returns>
        private static TacticalBattleEvent Fired(TacticalBattleUnit unit)
        {
            return new TacticalBattleEvent(TacticalBattleEventKind.WeaponFired, unit);
        }

        /// <summary>
        /// Creates combat parameters without automatic retreat.
        /// </summary>
        /// <returns>The combat parameters.</returns>
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

        /// <summary>
        /// Creates a capital ship with one forward turbolaser lane.
        /// </summary>
        /// <param name="instanceId">The instance identifier.</param>
        /// <param name="hull">The hull strength.</param>
        /// <param name="weaponStrength">The forward turbolaser strength, or zero for unarmed.</param>
        /// <returns>The created ship.</returns>
        private static CapitalShip CreateShip(string instanceId, int hull, int weaponStrength)
        {
            CapitalShip ship = new CapitalShip
            {
                InstanceID = instanceId,
                DisplayName = instanceId,
                MaxHullStrength = hull,
                CurrentHullStrength = hull,
                Hyperdrive = 1,
                SublightSpeed = 3,
                Maneuverability = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
                WeaponRecharge = Math.Max(weaponStrength, 0),
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = weaponStrength;
            if (weaponStrength > 0)
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = 150;
            return ship;
        }
    }
}
