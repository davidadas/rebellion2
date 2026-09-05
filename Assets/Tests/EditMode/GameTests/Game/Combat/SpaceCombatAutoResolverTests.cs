using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Combat
{
    [TestFixture]
    public class SpaceCombatAutoResolverTests
    {
        [Test]
        public void Resolve_EquivalentBattles_ReturnsDeterministicResults()
        {
            SpaceCombatAutoResult first = ResolveSymmetricBattle();
            SpaceCombatAutoResult second = ResolveSymmetricBattle();

            Assert.AreEqual(first.AttackerOutcome, second.AttackerOutcome);
            Assert.AreEqual(first.DefenderOutcome, second.DefenderOutcome);
            CollectionAssert.AreEqual(
                first.Ships.Select(outcome => outcome.HullAfter),
                second.Ships.Select(outcome => outcome.HullAfter)
            );
        }

        [Test]
        public void Resolve_WeaponsAcrossSeveralArcs_UsesEveryReadyArc()
        {
            CapitalShip singleArc = CreateShip("single-arc", hull: 100, weaponStrength: 1);
            CapitalShip severalArcs = CreateShip(
                "several-arcs",
                hull: 100,
                weaponStrength: 1,
                armEveryArc: true
            );
            CapitalShip firstTarget = CreatePassiveTarget("first-target", hull: 100);
            CapitalShip secondTarget = CreatePassiveTarget("second-target", hull: 100);

            SpaceCombatAutoResult first = Resolve(
                new[] { singleArc },
                new[] { firstTarget },
                defenderCanWithdraw: true
            );
            SpaceCombatAutoResult second = Resolve(
                new[] { severalArcs },
                new[] { secondTarget },
                defenderCanWithdraw: true
            );

            Assert.Less(second.IterationsCompleted, first.IterationsCompleted);
        }

        [Test]
        public void Resolve_CapitalShipLaserCannonAgainstCapitalShip_UsesConfiguredMultiplier()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 0);
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 60;
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.CapitalShipLaserCannonDamageAgainstCapitalShipsMultiplier = 0.25;
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(85, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_CapitalShipLaserCannonAgainstStarfighters_DealsFullDamage()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 0);
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 120;
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            Starfighter defender = CreateFighter("defender", squadronSize: 2, weaponStrength: 0);
            defender.ShieldStrength = 100;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { defender },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(1, GetFighterOutcome(result, defender).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_CapitalShipLaserCannonWithMixedTargets_SelectsHighestEffectiveDamageTarget()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 0);
            attacker.Maneuverability = 1;
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 60;
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            CapitalShip defenderShip = CreatePassiveTarget("defender-ship", hull: 100);
            Starfighter defenderFighter = CreateFighter(
                "defender-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            defenderFighter.Agility = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.CapitalShipLaserCannonDamageAgainstCapitalShipsMultiplier = 0.01;
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defenderShip },
                new[] { defenderFighter },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(100, GetShipOutcome(result, defenderShip).HullAfter);
            Assert.AreEqual(6, GetFighterOutcome(result, defenderFighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_ZeroIterationStalemate_UsesConfiguredCapitalTargetLaserEffectiveness()
        {
            CapitalShip laserShip = CreateShip("laser-ship", hull: 100, weaponStrength: 0);
            laserShip.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 60;
            laserShip.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            CapitalShip turbolaserShip = CreateShip(
                "turbolaser-ship",
                hull: 100,
                weaponStrength: 20
            );
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.CapitalShipLaserCannonDamageAgainstCapitalShipsMultiplier = 0.25;
            config.AutoResolveMaximumIterations = 0;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { laserShip },
                new List<Starfighter>(),
                new[] { turbolaserShip },
                new List<Starfighter>(),
                attackerCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
        }

        [Test]
        public void Resolve_HeavyLineShipAgainstThreeLaserEscorts_DefeatsEscorts()
        {
            CapitalShip[] escorts =
            {
                CreateLaserEscort("escort-1"),
                CreateLaserEscort("escort-2"),
                CreateLaserEscort("escort-3"),
            };
            CapitalShip lineShip = CreateHeavyLineShip("line-ship");

            SpaceCombatAutoResult result = Resolve(escorts, new[] { lineShip });

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
            Assert.Greater(GetShipOutcome(result, lineShip).HullAfter, 0);
        }

        [Test]
        public void Resolve_WeaponOutsideItsConfiguredRange_DoesNotDamageTarget()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            attacker.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = 0;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(100, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_LongRangeWeapons_InflictDamageBeforeShortRangeWeapons()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 25);
            attacker.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = 75;
            attacker.SublightSpeed = 5;
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 25);
            defender.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = 25;
            defender.SublightSpeed = 5;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>()
            );

            Assert.Greater(
                GetShipOutcome(result, attacker).HullAfter,
                GetShipOutcome(result, defender).HullAfter
            );
        }

        [Test]
        public void Resolve_FastSquadronClosesRange_DoesNotMoveCapitalShipIntoRange()
        {
            CapitalShip attackerShip = CreateShip("attacker-ship", hull: 100, weaponStrength: 10);
            attackerShip.SublightSpeed = 0;
            attackerShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = 10;
            Starfighter attackerFighter = CreateFighter(
                "attacker-fighter",
                squadronSize: 1,
                weaponStrength: 0
            );
            attackerFighter.SublightSpeed = 100;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 2;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attackerShip },
                new[] { attackerFighter },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(100, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_ChargedCapitalShipArcs_ConsumeSharedRechargeBudget()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            attacker.WeaponRecharge = 1;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 1000);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 2;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(990, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_CapitalShipRetainsTargetBetweenScans_ContinuesFiring()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 60);
            Starfighter defender = CreateFighter("defender", squadronSize: 2, weaponStrength: 0);
            defender.ShieldStrength = 100;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 2;
            SequenceRNG random = new SequenceRNG(new[] { 0, 1 });

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { defender },
                defenderCanWithdraw: true,
                random: random
            );

            Assert.AreEqual(1, GetFighterOutcome(result, defender).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_FighterTargetsCapitalShipWithStrongerAvailableAttack_DamagesCapitalShip()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 1, weaponStrength: 1);
            attacker.IonCannon = 10;
            attacker.IonRange = 100;
            CapitalShip defenderShip = CreatePassiveTarget("defender-ship", hull: 1000);
            Starfighter defenderFighter = CreateFighter(
                "defender-fighter",
                squadronSize: 1,
                weaponStrength: 0
            );
            defenderFighter.ShieldStrength = 1000;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defenderShip },
                new[] { defenderFighter },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(999, GetShipOutcome(result, defenderShip).HullAfter);
            Assert.AreEqual(1, GetFighterOutcome(result, defenderFighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_IonDamageWithoutShields_DoesNotDamageCapitalShipHull()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 1, weaponStrength: 0);
            attacker.IonCannon = 10;
            attacker.IonRange = 100;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 1000);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new ArcDamageRNG()
            );

            Assert.AreEqual(1000, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_MixedDamageAgainstShieldedShips_PreservesFiringOrder()
        {
            CapitalShip conventionalAttacker = CreateShip(
                "conventional-attacker",
                hull: 100,
                weaponStrength: 100
            );
            CapitalShip ionAttacker = CreateShip("ion-attacker", hull: 100, weaponStrength: 0);
            ionAttacker.PrimaryWeapons[PrimaryWeaponType.IonCannon][0] = 100;
            ionAttacker.PrimaryWeapons[PrimaryWeaponType.IonCannon][4] = 100;
            CapitalShip conventionalFirstTarget = CreatePassiveTarget(
                "conventional-first-target",
                hull: 100
            );
            conventionalFirstTarget.MaxShieldStrength = 100;
            CapitalShip ionFirstTarget = CreatePassiveTarget("ion-first-target", hull: 100);
            ionFirstTarget.MaxShieldStrength = 100;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult conventionalFirst = Resolve(
                config,
                new[] { conventionalAttacker, ionAttacker },
                new List<Starfighter>(),
                new[] { conventionalFirstTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );
            SpaceCombatAutoResult ionFirst = Resolve(
                config,
                new[] { ionAttacker, conventionalAttacker },
                new List<Starfighter>(),
                new[] { ionFirstTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(
                100,
                GetShipOutcome(conventionalFirst, conventionalFirstTarget).HullAfter
            );
            Assert.AreEqual(0, GetShipOutcome(ionFirst, ionFirstTarget).HullAfter);
        }

        [Test]
        public void Resolve_ConventionalHullDamage_DoesNotDisableCapitalShipWeapons()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 2, weaponStrength: 1);
            attacker.ShieldStrength = 100;
            CapitalShip defender = CreateShip("defender", hull: 1000, weaponStrength: 110);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 3;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveComponentDamageInterval = 2;

            SpaceCombatAutoResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                attackerCanWithdraw: true,
                random: new AttackDelayRNG()
            );

            Assert.AreEqual(0, GetFighterOutcome(result, attacker).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_IonOverflowDamage_DelaysCapitalShipAttack()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 2, weaponStrength: 0);
            attacker.IonCannon = 1;
            attacker.IonRange = 100;
            attacker.ShieldStrength = 100;
            CapitalShip defender = CreateShip("defender", hull: 1000, weaponStrength: 110);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 3;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveComponentDamageInterval = 2;

            SpaceCombatAutoResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                attackerCanWithdraw: true,
                random: new AttackDelayRNG()
            );

            Assert.AreEqual(1, GetFighterOutcome(result, attacker).SquadronSizeAfter);
            Assert.AreEqual(1000, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_SeparateWeaponLanes_TargetUnitsAtDifferentRanges()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 1);
            attacker.SublightSpeed = 0;
            attacker.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = 30;
            attacker.PrimaryWeapons[PrimaryWeaponType.IonCannon][0] = 1;
            attacker.PrimaryWeapons[PrimaryWeaponType.IonCannon][4] = 100;
            CapitalShip defenderShip = CreatePassiveTarget("defender-ship", hull: 100);
            Starfighter defenderFighter = CreateFighter(
                "defender-fighter",
                squadronSize: 2,
                weaponStrength: 0
            );
            defenderFighter.SublightSpeed = 50;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 2;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defenderShip },
                new[] { defenderFighter },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(100, GetShipOutcome(result, defenderShip).HullAfter);
            Assert.AreEqual(1, GetFighterOutcome(result, defenderFighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_IonOnlyCapitalShipAgainstFighters_DoesNotDamageFighters()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 0);
            attacker.PrimaryWeapons[PrimaryWeaponType.IonCannon][0] = 100;
            attacker.PrimaryWeapons[PrimaryWeaponType.IonCannon][4] = 100;
            Starfighter defender = CreateFighter("defender", squadronSize: 12, weaponStrength: 0);

            SpaceCombatAutoResult result = Resolve(
                new[] { attacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { defender },
                attackerCanWithdraw: true,
                defenderCanWithdraw: true
            );

            Assert.AreEqual(12, GetFighterOutcome(result, defender).SquadronSizeAfter);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
        }

        [Test]
        public void Resolve_MixedTargetTypes_DestroysWeakerForce()
        {
            CapitalShip attackerShip = CreatePassiveTarget("attacker-ship", hull: 10000);
            Starfighter attackerFighter = CreateFighter(
                "attacker-fighter",
                squadronSize: 12,
                weaponStrength: 100
            );
            attackerFighter.ShieldStrength = 1000;
            CapitalShip defenderShip = CreateShip("defender-ship", hull: 100, weaponStrength: 0);
            defenderShip.PrimaryWeapons[PrimaryWeaponType.IonCannon][0] = 100;
            defenderShip.PrimaryWeapons[PrimaryWeaponType.IonCannon][4] = 100;
            Starfighter defenderFighter = CreateFighter(
                "defender-fighter",
                squadronSize: 12,
                weaponStrength: 10
            );

            SpaceCombatAutoResult result = Resolve(
                new[] { attackerShip },
                new[] { attackerFighter },
                new[] { defenderShip },
                new[] { defenderFighter },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(0, GetShipOutcome(result, defenderShip).HullAfter);
        }

        [Test]
        public void Resolve_FullFighterSquadronsWithEqualWeaponStrength_DealEqualDamage()
        {
            Starfighter singleFighter = CreateFighter(
                "single-fighter",
                squadronSize: 1,
                weaponStrength: 1
            );
            Starfighter fullSquadron = CreateFighter(
                "full-squadron",
                squadronSize: 12,
                weaponStrength: 1
            );
            CapitalShip firstTarget = CreatePassiveTarget("first-target", hull: 100);
            CapitalShip secondTarget = CreatePassiveTarget("second-target", hull: 100);

            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult first = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { singleFighter },
                new[] { firstTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );
            SpaceCombatAutoResult second = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { fullSquadron },
                new[] { secondTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(99, GetShipOutcome(first, firstTarget).HullAfter);
            Assert.AreEqual(99, GetShipOutcome(second, secondTarget).HullAfter);
        }

        [Test]
        public void Resolve_DamagedFighterSquadron_DealsProportionalDamage()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 12, weaponStrength: 12);
            attacker.CurrentSquadronSize = 6;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(94, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_DamagedCapitalShip_TakesLongerToDefeatEquivalentTarget()
        {
            CapitalShip undamaged = CreateShip("undamaged", hull: 100, weaponStrength: 10);
            CapitalShip damaged = CreateShip("damaged", hull: 100, weaponStrength: 10);
            damaged.CurrentHullStrength = 50;
            CapitalShip firstTarget = CreatePassiveTarget("first-target", hull: 1000);
            CapitalShip secondTarget = CreatePassiveTarget("second-target", hull: 1000);

            SpaceCombatAutoResult first = Resolve(
                new[] { undamaged },
                new[] { firstTarget },
                defenderCanWithdraw: true
            );
            SpaceCombatAutoResult second = Resolve(
                new[] { damaged },
                new[] { secondTarget },
                defenderCanWithdraw: true
            );

            Assert.Less(first.IterationsCompleted, second.IterationsCompleted);
        }

        [Test]
        public void Resolve_PositiveFractionalAttack_InflictsMinimumDamage()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 1);
            attacker.CurrentHullStrength = 1;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(99, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_ShieldsRemain_ProtectsCapitalShipHull()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 1);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 0);
            defender.MaxShieldStrength = 100;
            defender.ShieldRechargeRate = 1;

            SpaceCombatAutoResult result = Resolve(
                new[] { attacker },
                new[] { defender },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(100, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_ShieldRechargeWithoutShieldCapacity_DoesNotProtectHull()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 0);
            defender.MaxShieldStrength = 0;
            defender.ShieldRechargeRate = 100;

            SpaceCombatAutoResult result = Resolve(
                new[] { attacker },
                new[] { defender },
                defenderCanWithdraw: true
            );

            Assert.Less(GetShipOutcome(result, defender).HullAfter, 100);
        }

        [Test]
        public void Resolve_ShieldsDepleteBeyondStagnationWindow_ContinuesCombat()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 0);
            defender.MaxShieldStrength = 500;

            SpaceCombatAutoResult result = Resolve(
                new[] { attacker },
                new[] { defender },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(0, GetShipOutcome(result, defender).HullAfter);
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void Resolve_ForceReachesOneThirdStrength_CompletesAccordingToWithdrawalAvailability(
            bool canWithdraw,
            bool expectsWithdrawal
        )
        {
            CapitalShip attacker = CreateShip("attacker", hull: 10000, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 1);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: canWithdraw,
                random: new ArcDamageRNG()
            );

            Assert.AreEqual(
                expectsWithdrawal
                    ? SpaceCombatSideOutcome.Withdrawn
                    : SpaceCombatSideOutcome.Destroyed,
                result.DefenderOutcome
            );
        }

        [Test]
        public void Resolve_FighterForceFallsBelowOneThirdStrength_WithdrawsForce()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 10000, weaponStrength: 9);
            Starfighter defender = CreateFighter("defender", squadronSize: 12, weaponStrength: 1);
            defender.ShieldStrength = 10;
            defender.SublightSpeed = 10;

            SpaceCombatAutoResult result = Resolve(
                new[] { attacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { defender },
                attackerCanWithdraw: false,
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(4, GetFighterOutcome(result, defender).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_OnlyEligibleUnitsCanWithdraw_LeavesOtherUnitsInCombat()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 10000, weaponStrength: 100);
            CapitalShip withdrawingDefender = CreateShip(
                "withdrawing-defender",
                hull: 1000,
                weaponStrength: 1
            );
            withdrawingDefender.SublightSpeed = 10;
            CapitalShip trappedDefender = CreateShip(
                "trapped-defender",
                hull: 10,
                weaponStrength: 1
            );
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveTargetScanDivisor = 1;
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> defenderWithdrawalGroups =
                new IReadOnlyCollection<ISceneNode>[] { new ISceneNode[] { withdrawingDefender } };

            SpaceCombatAutoResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { withdrawingDefender, trappedDefender },
                    new List<Starfighter>(),
                    Array.Empty<IReadOnlyCollection<ISceneNode>>(),
                    defenderWithdrawalGroups
                );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.IsTrue(GetShipOutcome(result, withdrawingDefender).Withdrew);
            Assert.Greater(GetShipOutcome(result, withdrawingDefender).HullAfter, 0);
            Assert.IsFalse(GetShipOutcome(result, trappedDefender).Withdrew);
            Assert.AreEqual(0, GetShipOutcome(result, trappedDefender).HullAfter);
        }

        [Test]
        public void Resolve_WithdrawalRequiredWithoutHyperdrive_ContinuesFighting()
        {
            CapitalShip attacker = CreatePassiveTarget("attacker", hull: 1);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 100);
            defender.Hyperdrive = 0;
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 10;
            config.AutoResolveTargetScanDivisor = 1;
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> defenderWithdrawalGroups =
                new IReadOnlyCollection<ISceneNode>[] { new ISceneNode[] { defender } };

            SpaceCombatAutoResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { defender },
                    new List<Starfighter>(),
                    Array.Empty<IReadOnlyCollection<ISceneNode>>(),
                    defenderWithdrawalGroups
                );

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
            Assert.AreEqual(0, GetShipOutcome(result, attacker).HullAfter);
            Assert.IsFalse(GetShipOutcome(result, defender).Withdrew);
            Assert.Greater(GetShipOutcome(result, defender).HullAfter, 0);
        }

        [Test]
        public void Resolve_ThreeCapitalShipsWithdrawWithOneWithoutHyperdrive_DestroysStrandedShip()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            CapitalShip firstWithdrawingShip = CreateShip(
                "first-withdrawing-ship",
                hull: 1000,
                weaponStrength: 1
            );
            firstWithdrawingShip.SublightSpeed = 10;
            CapitalShip secondWithdrawingShip = CreateShip(
                "second-withdrawing-ship",
                hull: 1000,
                weaponStrength: 1
            );
            secondWithdrawingShip.SublightSpeed = 10;
            CapitalShip strandedShip = CreateShip("stranded-ship", hull: 100, weaponStrength: 1);
            strandedShip.Hyperdrive = 0;
            strandedShip.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 10;
            config.AutoResolveTargetScanDivisor = 1;
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> defenderWithdrawalGroups =
                new IReadOnlyCollection<ISceneNode>[]
                {
                    new ISceneNode[] { firstWithdrawingShip, secondWithdrawingShip, strandedShip },
                };

            SpaceCombatAutoResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { firstWithdrawingShip, secondWithdrawingShip, strandedShip },
                    new List<Starfighter>(),
                    Array.Empty<IReadOnlyCollection<ISceneNode>>(),
                    defenderWithdrawalGroups
                );

            Assert.IsTrue(GetShipOutcome(result, firstWithdrawingShip).Withdrew);
            Assert.IsTrue(GetShipOutcome(result, secondWithdrawingShip).Withdrew);
            Assert.IsFalse(GetShipOutcome(result, strandedShip).Withdrew);
            Assert.AreEqual(0, GetShipOutcome(result, strandedShip).HullAfter);
            Assert.Less(GetShipOutcome(result, attacker).HullAfter, 1000);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
        }

        [Test]
        public void Resolve_FleetWithdrawalInterruptedByVictory_DoesNotWithdrawPartialFleet()
        {
            CapitalShip attacker = CreatePassiveTarget("attacker", hull: 1);
            CapitalShip fastFleetShip = CreateShip("fast-fleet-ship", hull: 100, weaponStrength: 0);
            fastFleetShip.SublightSpeed = 20;
            fastFleetShip.StarfighterCapacity = 1;
            CapitalShip slowFleetShip = CreateShip("slow-fleet-ship", hull: 100, weaponStrength: 0);
            slowFleetShip.SublightSpeed = 10;
            Starfighter carriedFighter = CreateFighter(
                "carried-fighter",
                squadronSize: 1,
                weaponStrength: 0
            );
            carriedFighter.SublightSpeed = 30;
            CapitalShip coveringShip = CreateShip("covering-ship", hull: 100, weaponStrength: 1);
            Fleet fleet = new Fleet();
            fleet.AddChild(fastFleetShip);
            fleet.AddChild(slowFleetShip);
            fastFleetShip.AddChild(carriedFighter);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 20;
            config.AutoResolveTargetScanDivisor = 1;
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> defenderWithdrawalGroups =
                new IReadOnlyCollection<ISceneNode>[]
                {
                    new ISceneNode[] { fastFleetShip, slowFleetShip, carriedFighter },
                };

            SpaceCombatAutoResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { fastFleetShip, slowFleetShip, coveringShip },
                    new[] { carriedFighter },
                    Array.Empty<IReadOnlyCollection<ISceneNode>>(),
                    defenderWithdrawalGroups
                );

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
            Assert.IsFalse(GetShipOutcome(result, fastFleetShip).Withdrew);
            Assert.IsFalse(GetShipOutcome(result, slowFleetShip).Withdrew);
            Assert.IsFalse(GetFighterOutcome(result, carriedFighter).Withdrew);
        }

        [Test]
        public void Resolve_ForceBeginsWithdrawal_RemainsVulnerableUntilItEscapes()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 5);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 1);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 20;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new ArcDamageRNG()
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(90, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_CarriedNonHyperdriveFighterWithdraws_PreservesFighter()
        {
            CapitalShip attacker = CreatePassiveTarget("attacker", hull: 100);
            CapitalShip carrier = CreateShip("carrier", hull: 100, weaponStrength: 1);
            carrier.StarfighterCapacity = 1;
            carrier.SublightSpeed = 10;
            Starfighter fighter = CreateFighter("fighter", squadronSize: 12, weaponStrength: 0);
            fighter.Hyperdrive = 0;
            fighter.SublightSpeed = 10;
            Fleet fleet = new Fleet();
            fleet.AddChild(carrier);
            carrier.SetParent(fleet);
            carrier.AddChild(fighter);
            fighter.SetParent(carrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> withdrawalGroups =
                new IReadOnlyCollection<ISceneNode>[] { new ISceneNode[] { carrier, fighter } };

            SpaceCombatAutoResult result = CreateResolver(config)
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { carrier },
                    new[] { fighter },
                    Array.Empty<IReadOnlyCollection<ISceneNode>>(),
                    withdrawalGroups
                );

            Assert.IsTrue(GetShipOutcome(result, carrier).Withdrew);
            Assert.IsTrue(GetFighterOutcome(result, fighter).Withdrew);
            Assert.AreEqual(12, GetFighterOutcome(result, fighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_CarrierDestroyedWithoutRecoveryCapacity_DestroysNonHyperdriveFighter()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            CapitalShip carrier = CreateShip("carrier", hull: 1, weaponStrength: 1);
            carrier.StarfighterCapacity = 1;
            carrier.SublightSpeed = 10;
            CapitalShip escapeShip = CreateShip("escape-ship", hull: 1000, weaponStrength: 1);
            escapeShip.StarfighterCapacity = 0;
            escapeShip.SublightSpeed = 10;
            Starfighter fighter = CreateFighter("fighter", squadronSize: 12, weaponStrength: 0);
            fighter.Hyperdrive = 0;
            fighter.ShieldStrength = 100;
            fighter.SublightSpeed = 10;
            Fleet fleet = new Fleet();
            fleet.AddChild(carrier);
            carrier.SetParent(fleet);
            fleet.AddChild(escapeShip);
            escapeShip.SetParent(fleet);
            carrier.AddChild(fighter);
            fighter.SetParent(carrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 20;
            config.AutoResolveTargetScanDivisor = 1;
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> withdrawalGroups =
                new IReadOnlyCollection<ISceneNode>[]
                {
                    new ISceneNode[] { carrier, escapeShip, fighter },
                };

            SpaceCombatAutoResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { carrier, escapeShip },
                    new[] { fighter },
                    Array.Empty<IReadOnlyCollection<ISceneNode>>(),
                    withdrawalGroups
                );

            Assert.IsTrue(GetShipOutcome(result, escapeShip).Withdrew);
            Assert.IsFalse(GetFighterOutcome(result, fighter).Withdrew);
            Assert.AreEqual(0, GetFighterOutcome(result, fighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_TwoCarriersDestroyedWithNonHyperdriveFighters_DestroysFightersAfterTheyFight()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 100);
            attacker.WeaponRecharge = 100;
            CapitalShip firstCarrier = CreatePassiveTarget("first-carrier", hull: 1);
            firstCarrier.StarfighterCapacity = 1;
            firstCarrier.SublightSpeed = 1;
            CapitalShip secondCarrier = CreatePassiveTarget("second-carrier", hull: 1);
            secondCarrier.StarfighterCapacity = 1;
            secondCarrier.SublightSpeed = 1;
            Starfighter firstFighter = CreateFighter(
                "first-fighter",
                squadronSize: 12,
                weaponStrength: 10
            );
            firstFighter.Hyperdrive = 0;
            firstFighter.SublightSpeed = 10;
            Starfighter secondFighter = CreateFighter(
                "second-fighter",
                squadronSize: 12,
                weaponStrength: 10
            );
            secondFighter.Hyperdrive = 0;
            secondFighter.SublightSpeed = 10;
            Fleet fleet = new Fleet();
            fleet.AddChild(firstCarrier);
            firstCarrier.SetParent(fleet);
            fleet.AddChild(secondCarrier);
            secondCarrier.SetParent(fleet);
            firstCarrier.AddChild(firstFighter);
            firstFighter.SetParent(firstCarrier);
            secondCarrier.AddChild(secondFighter);
            secondFighter.SetParent(secondCarrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 10;
            config.AutoResolveTargetScanDivisor = 1;
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> defenderWithdrawalGroups =
                new IReadOnlyCollection<ISceneNode>[]
                {
                    new ISceneNode[] { firstCarrier, secondCarrier, firstFighter, secondFighter },
                };

            SpaceCombatAutoResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { firstCarrier, secondCarrier },
                    new[] { firstFighter, secondFighter },
                    Array.Empty<IReadOnlyCollection<ISceneNode>>(),
                    defenderWithdrawalGroups
                );

            Assert.AreEqual(0, GetShipOutcome(result, firstCarrier).HullAfter);
            Assert.AreEqual(0, GetShipOutcome(result, secondCarrier).HullAfter);
            Assert.IsFalse(GetFighterOutcome(result, firstFighter).Withdrew);
            Assert.IsFalse(GetFighterOutcome(result, secondFighter).Withdrew);
            Assert.AreEqual(0, GetFighterOutcome(result, firstFighter).SquadronSizeAfter);
            Assert.AreEqual(0, GetFighterOutcome(result, secondFighter).SquadronSizeAfter);
            Assert.Less(GetShipOutcome(result, attacker).HullAfter, 1000);
        }

        [Test]
        public void Resolve_RecoveryCarrierBayOccupiedByInTransitFighter_DestroysNonHyperdriveFighter()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            CapitalShip destroyedCarrier = CreateShip(
                "destroyed-carrier",
                hull: 1,
                weaponStrength: 1
            );
            destroyedCarrier.StarfighterCapacity = 1;
            destroyedCarrier.SublightSpeed = 10;
            CapitalShip recoveryCarrier = CreateShip(
                "recovery-carrier",
                hull: 1000,
                weaponStrength: 1
            );
            recoveryCarrier.StarfighterCapacity = 1;
            recoveryCarrier.SublightSpeed = 10;
            Starfighter strandedFighter = CreateFighter(
                "stranded-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            strandedFighter.Hyperdrive = 0;
            strandedFighter.ShieldStrength = 100;
            strandedFighter.SublightSpeed = 10;
            Starfighter inTransitFighter = CreateFighter(
                "in-transit-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            inTransitFighter.Movement = new MovementState();
            Fleet fleet = new Fleet();
            fleet.AddChild(destroyedCarrier);
            destroyedCarrier.SetParent(fleet);
            fleet.AddChild(recoveryCarrier);
            recoveryCarrier.SetParent(fleet);
            destroyedCarrier.AddChild(strandedFighter);
            strandedFighter.SetParent(destroyedCarrier);
            recoveryCarrier.AddChild(inTransitFighter);
            inTransitFighter.SetParent(recoveryCarrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 20;
            config.AutoResolveTargetScanDivisor = 1;
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> withdrawalGroups =
                new IReadOnlyCollection<ISceneNode>[]
                {
                    new ISceneNode[] { destroyedCarrier, recoveryCarrier, strandedFighter },
                };

            SpaceCombatAutoResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { destroyedCarrier, recoveryCarrier },
                    new[] { strandedFighter },
                    Array.Empty<IReadOnlyCollection<ISceneNode>>(),
                    withdrawalGroups
                );

            Assert.AreEqual(0, GetShipOutcome(result, destroyedCarrier).HullAfter);
            Assert.IsTrue(GetShipOutcome(result, recoveryCarrier).Withdrew);
            Assert.IsFalse(GetFighterOutcome(result, strandedFighter).Withdrew);
            Assert.AreEqual(0, GetFighterOutcome(result, strandedFighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_CarrierDestroyedWithSpareRecoveryCapacity_WithdrawsNonHyperdriveFighter()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            CapitalShip destroyedCarrier = CreateShip(
                "destroyed-carrier",
                hull: 1,
                weaponStrength: 1
            );
            destroyedCarrier.StarfighterCapacity = 1;
            destroyedCarrier.SublightSpeed = 10;
            CapitalShip recoveryCarrier = CreateShip(
                "recovery-carrier",
                hull: 1000,
                weaponStrength: 1
            );
            recoveryCarrier.StarfighterCapacity = 1;
            recoveryCarrier.SublightSpeed = 10;
            Starfighter fighter = CreateFighter("fighter", squadronSize: 12, weaponStrength: 0);
            fighter.Hyperdrive = 0;
            fighter.ShieldStrength = 100;
            fighter.SublightSpeed = 10;
            Fleet fleet = new Fleet();
            fleet.AddChild(destroyedCarrier);
            destroyedCarrier.SetParent(fleet);
            fleet.AddChild(recoveryCarrier);
            recoveryCarrier.SetParent(fleet);
            destroyedCarrier.AddChild(fighter);
            fighter.SetParent(destroyedCarrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 20;
            config.AutoResolveTargetScanDivisor = 1;
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> withdrawalGroups =
                new IReadOnlyCollection<ISceneNode>[]
                {
                    new ISceneNode[] { destroyedCarrier, recoveryCarrier, fighter },
                };

            SpaceCombatAutoResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { destroyedCarrier, recoveryCarrier },
                    new[] { fighter },
                    Array.Empty<IReadOnlyCollection<ISceneNode>>(),
                    withdrawalGroups
                );

            Assert.AreEqual(0, GetShipOutcome(result, destroyedCarrier).HullAfter);
            Assert.IsTrue(GetShipOutcome(result, recoveryCarrier).Withdrew);
            Assert.IsTrue(GetFighterOutcome(result, fighter).Withdrew);
            Assert.AreEqual(12, GetFighterOutcome(result, fighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_HyperdriveFighterOccupiesRecoveryCarrier_WithdrawsBothFighters()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            CapitalShip destroyedCarrier = CreateShip(
                "destroyed-carrier",
                hull: 1,
                weaponStrength: 1
            );
            destroyedCarrier.StarfighterCapacity = 1;
            destroyedCarrier.SublightSpeed = 10;
            CapitalShip recoveryCarrier = CreateShip(
                "recovery-carrier",
                hull: 1000,
                weaponStrength: 1
            );
            recoveryCarrier.StarfighterCapacity = 1;
            recoveryCarrier.SublightSpeed = 10;
            Starfighter hyperdriveFighter = CreateFighter(
                "hyperdrive-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            hyperdriveFighter.Hyperdrive = 1;
            hyperdriveFighter.ShieldStrength = 100;
            hyperdriveFighter.SublightSpeed = 10;
            Starfighter nonHyperdriveFighter = CreateFighter(
                "non-hyperdrive-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            nonHyperdriveFighter.Hyperdrive = 0;
            nonHyperdriveFighter.ShieldStrength = 100;
            nonHyperdriveFighter.SublightSpeed = 10;
            Fleet fleet = new Fleet();
            fleet.AddChild(destroyedCarrier);
            destroyedCarrier.SetParent(fleet);
            fleet.AddChild(recoveryCarrier);
            recoveryCarrier.SetParent(fleet);
            destroyedCarrier.AddChild(nonHyperdriveFighter);
            nonHyperdriveFighter.SetParent(destroyedCarrier);
            recoveryCarrier.AddChild(hyperdriveFighter);
            hyperdriveFighter.SetParent(recoveryCarrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 20;
            config.AutoResolveTargetScanDivisor = 1;
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> withdrawalGroups =
                new IReadOnlyCollection<ISceneNode>[]
                {
                    new ISceneNode[]
                    {
                        destroyedCarrier,
                        recoveryCarrier,
                        hyperdriveFighter,
                        nonHyperdriveFighter,
                    },
                };

            SpaceCombatAutoResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { destroyedCarrier, recoveryCarrier },
                    new[] { hyperdriveFighter, nonHyperdriveFighter },
                    Array.Empty<IReadOnlyCollection<ISceneNode>>(),
                    withdrawalGroups
                );

            Assert.AreEqual(0, GetShipOutcome(result, destroyedCarrier).HullAfter);
            Assert.IsTrue(GetShipOutcome(result, recoveryCarrier).Withdrew);
            Assert.IsTrue(GetFighterOutcome(result, hyperdriveFighter).Withdrew);
            Assert.IsTrue(GetFighterOutcome(result, nonHyperdriveFighter).Withdrew);
        }

        [Test]
        public void Resolve_UnarmedForcesWithoutWithdrawal_DestroysBothForces()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 0);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 0);

            SpaceCombatAutoResult result = Resolve(new[] { attacker }, new[] { defender });

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(0, GetShipOutcome(result, attacker).HullAfter);
            Assert.AreEqual(0, GetShipOutcome(result, defender).HullAfter);
            Assert.AreEqual(1200, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_DamagedShipsCannotRechargeWeaponsOrWithdraw_DestroysBothSides()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            attacker.CurrentHullStrength = 10;
            attacker.Hyperdrive = 0;
            attacker.WeaponRecharge = 0;
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 10);
            defender.CurrentHullStrength = 10;
            defender.Hyperdrive = 0;
            defender.WeaponRecharge = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveStagnationIterations = 2;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatAutoResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>()
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(0, GetShipOutcome(result, attacker).HullAfter);
            Assert.AreEqual(0, GetShipOutcome(result, defender).HullAfter);
            Assert.AreEqual(3, result.IterationsCompleted);
        }

        private static SpaceCombatAutoResult ResolveSymmetricBattle()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 10);
            return Resolve(new[] { attacker }, new[] { defender });
        }

        private static SpaceCombatAutoResult Resolve(
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<CapitalShip> defenderShips,
            bool attackerCanWithdraw = false,
            bool defenderCanWithdraw = false
        )
        {
            return Resolve(
                attackerShips,
                new List<Starfighter>(),
                defenderShips,
                new List<Starfighter>(),
                attackerCanWithdraw,
                defenderCanWithdraw
            );
        }

        private static SpaceCombatAutoResult Resolve(
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<Starfighter> attackerFighters,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> defenderFighters,
            bool attackerCanWithdraw = false,
            bool defenderCanWithdraw = false
        )
        {
            return Resolve(
                CreateConfig(),
                attackerShips,
                attackerFighters,
                defenderShips,
                defenderFighters,
                attackerCanWithdraw,
                defenderCanWithdraw
            );
        }

        private static SpaceCombatAutoResult Resolve(
            GameConfig.SpaceCombatConfig config,
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<Starfighter> attackerFighters,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> defenderFighters,
            bool attackerCanWithdraw = false,
            bool defenderCanWithdraw = false,
            IRandomNumberProvider random = null
        )
        {
            return CreateResolver(config, random)
                .Resolve(
                    attackerShips,
                    attackerFighters,
                    defenderShips,
                    defenderFighters,
                    CreateWithdrawalGroups(attackerCanWithdraw, attackerShips, attackerFighters),
                    CreateWithdrawalGroups(defenderCanWithdraw, defenderShips, defenderFighters)
                );
        }

        private static GameConfig.SpaceCombatConfig CreateConfig()
        {
            return new GameConfig.SpaceCombatConfig
            {
                CapitalShipLaserCannonDamageAgainstCapitalShipsMultiplier = 1.0 / 6.0,
                AutoResolveMaximumIterations = 4096,
                AutoResolveStagnationIterations = 1200,
                AutoResolveRetreatStrengthRatio = 0.33,
                AutoResolveMinimumManeuverRatio = 0.1,
                AutoResolveTargetScanDivisor = 3,
                AutoResolveStartingDistance = 75,
                AutoResolveWithdrawalDistance = 10,
                AutoResolveComponentDamageInterval = 1,
                AutoResolveComponentDamageRollMaximum = 10,
                AutoResolveComponentDelayMinimum = 30,
                AutoResolveComponentDelayMaximum = 50,
                AutoResolveComponentDelayRecovery = 1,
            };
        }

        private static IReadOnlyList<IReadOnlyCollection<ISceneNode>> CreateWithdrawalGroups(
            bool canWithdraw,
            IReadOnlyList<CapitalShip> ships,
            IReadOnlyList<Starfighter> fighters
        )
        {
            if (!canWithdraw)
                return Array.Empty<IReadOnlyCollection<ISceneNode>>();

            return ships
                .Cast<ISceneNode>()
                .Concat(fighters.Cast<ISceneNode>())
                .Select(unit => (IReadOnlyCollection<ISceneNode>)new ISceneNode[] { unit })
                .ToList();
        }

        private static SpaceCombatAutoResolver CreateResolver(
            GameConfig.SpaceCombatConfig config = null,
            IRandomNumberProvider random = null
        )
        {
            return new SpaceCombatAutoResolver(
                config ?? CreateConfig(),
                random ?? new SystemRandomProvider(1894809716)
            );
        }

        private static CapitalShip CreateShip(
            string instanceId,
            int hull,
            int weaponStrength,
            bool armEveryArc = false
        )
        {
            CapitalShip ship = new CapitalShip
            {
                InstanceID = instanceId,
                MaxHullStrength = hull,
                CurrentHullStrength = hull,
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
                WeaponRecharge = Math.Max(weaponStrength, 0),
            };
            int armedArcCount = armEveryArc ? 4 : 1;
            for (int arc = 0; arc < armedArcCount; arc++)
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][arc] = weaponStrength;
            if (weaponStrength > 0)
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = 100;
            return ship;
        }

        private static CapitalShip CreateLaserEscort(string instanceId)
        {
            CapitalShip ship = CreateShip(instanceId, hull: 500, weaponStrength: 0);
            ship.MaxShieldStrength = 200;
            ship.ShieldRechargeRate = 10;
            ship.SublightSpeed = 6;
            ship.Maneuverability = 3;
            ship.WeaponRecharge = 8;
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 120, 90, 120, 120, 17 };
            return ship;
        }

        private static CapitalShip CreateHeavyLineShip(string instanceId)
        {
            CapitalShip ship = CreateShip(instanceId, hull: 2750, weaponStrength: 0);
            ship.MaxShieldStrength = 300;
            ship.ShieldRechargeRate = 15;
            ship.SublightSpeed = 4;
            ship.Maneuverability = 1;
            ship.WeaponRecharge = 20;
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100, 40, 60, 60, 50 };
            ship.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new[] { 100, 40, 40, 40, 35 };
            return ship;
        }

        private static Starfighter CreateFighter(
            string instanceId,
            int squadronSize,
            int weaponStrength
        )
        {
            return new Starfighter
            {
                InstanceID = instanceId,
                MaxSquadronSize = squadronSize,
                CurrentSquadronSize = squadronSize,
                ShieldStrength = 1,
                Hyperdrive = 1,
                LaserCannon = weaponStrength,
                LaserRange = weaponStrength > 0 ? 100 : 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static CapitalShip CreatePassiveTarget(string instanceId, int hull)
        {
            return CreateShip(instanceId, hull, weaponStrength: 0);
        }

        private static SpaceCombatAutoShipOutcome GetShipOutcome(
            SpaceCombatAutoResult result,
            CapitalShip ship
        )
        {
            return result.Ships.Single(outcome => outcome.Ship == ship);
        }

        private static SpaceCombatAutoFighterOutcome GetFighterOutcome(
            SpaceCombatAutoResult result,
            Starfighter fighter
        )
        {
            return result.Fighters.Single(outcome => outcome.Fighter == fighter);
        }

        private sealed class ArcDamageRNG : IRandomNumberProvider
        {
            public double NextDouble()
            {
                return 0;
            }

            public int NextInt(int min, int max)
            {
                return min == 1 && max == 11 ? 3 : min;
            }
        }

        private sealed class AttackDelayRNG : IRandomNumberProvider
        {
            public double NextDouble()
            {
                return 0;
            }

            public int NextInt(int min, int max)
            {
                return min == 1 && max == 11 ? 1 : min;
            }
        }
    }
}
