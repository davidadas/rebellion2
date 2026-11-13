using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class SpaceCombatAutoResolverTests
    {
        [Test]
        public void Resolve_EquivalentBattles_ReturnsDeterministicResults()
        {
            SpaceCombatResult first = ResolveSymmetricBattle();
            SpaceCombatResult second = ResolveSymmetricBattle();

            Assert.AreEqual(first.AttackerOutcome, second.AttackerOutcome);
            Assert.AreEqual(first.DefenderOutcome, second.DefenderOutcome);
            CollectionAssert.AreEqual(GetResolvedShipHulls(first), GetResolvedShipHulls(second));
        }

        [Test]
        public void Resolve_AdmiralLeadership_IncreasesCapitalShipManeuverability()
        {
            CapitalShip uncommandedShip = CreateShip("uncommanded", hull: 100, weaponStrength: 0);
            CapitalShip commandedShip = CreateShip("commanded", hull: 100, weaponStrength: 0);
            foreach (CapitalShip ship in new[] { uncommandedShip, commandedShip })
            {
                ship.SublightSpeed = 1;
                ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 20;
                ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            }
            Starfighter firstTarget = CreateFighter("first-target", 100, weaponStrength: 0);
            Starfighter secondTarget = CreateFighter("second-target", 100, weaponStrength: 0);
            firstTarget.ShieldStrength = 0;
            secondTarget.ShieldStrength = 0;
            firstTarget.SublightSpeed = 10;
            secondTarget.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveMinimumManeuverRatio = 0.01;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveFighterHullPerCraft = 1;

            SpaceCombatResult uncommanded = Resolve(
                config,
                new[] { uncommandedShip },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { firstTarget },
                defenderCanWithdraw: true
            );
            SpaceCombatResult commanded = Resolve(
                config,
                new[] { commandedShip },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { secondTarget },
                defenderCanWithdraw: true,
                attackerCommand: new SpaceCombatCommandModifiers(
                    admiralLeadership: 100,
                    commanderCombat: 0
                )
            );

            Assert.AreEqual(98, GetFighterOutcome(uncommanded, firstTarget).SquadronSizeAfter);
            Assert.AreEqual(80, GetFighterOutcome(commanded, secondTarget).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_AdmiralLeadership_ImprovesCapitalShipReactionRate()
        {
            CapitalShip uncommandedShip = CreateShip("uncommanded", hull: 100, weaponStrength: 2);
            CapitalShip commandedShip = CreateShip("commanded", hull: 100, weaponStrength: 2);
            uncommandedShip.WeaponRecharge = 0;
            commandedShip.WeaponRecharge = 0;
            CapitalShip firstTarget = CreatePassiveTarget("first-target", hull: 100);
            CapitalShip secondTarget = CreatePassiveTarget("second-target", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AdmiralLeadershipDivisor = 50;
            config.AutoResolveMaximumIterations = 2;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult uncommanded = Resolve(
                config,
                new[] { uncommandedShip },
                new List<Starfighter>(),
                new[] { firstTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );
            SpaceCombatResult commanded = Resolve(
                config,
                new[] { commandedShip },
                new List<Starfighter>(),
                new[] { secondTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                attackerCommand: new SpaceCombatCommandModifiers(
                    admiralLeadership: 100,
                    commanderCombat: 0
                )
            );

            Assert.AreEqual(98, GetShipOutcome(uncommanded, firstTarget).HullAfter);
            Assert.AreEqual(96, GetShipOutcome(commanded, secondTarget).HullAfter);
        }

        [Test]
        public void Resolve_CommanderCombat_IncreasesStarfighterEffectiveness()
        {
            Starfighter uncommandedFighter = CreateFighter("uncommanded", 1, weaponStrength: 5);
            Starfighter commandedFighter = CreateFighter("commanded", 1, weaponStrength: 5);
            CapitalShip firstTarget = CreatePassiveTarget("first-target", hull: 100);
            CapitalShip secondTarget = CreatePassiveTarget("second-target", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 1;
            config.CommanderCombatDivisor = 25;
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult uncommanded = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { uncommandedFighter },
                new[] { firstTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );
            SpaceCombatResult commanded = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { commandedFighter },
                new[] { secondTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                attackerCommand: new SpaceCombatCommandModifiers(
                    admiralLeadership: 0,
                    commanderCombat: 100
                )
            );

            Assert.AreEqual(95, GetShipOutcome(uncommanded, firstTarget).HullAfter);
            Assert.AreEqual(91, GetShipOutcome(commanded, secondTarget).HullAfter);
        }

        [Test]
        public void Resolve_CommanderCombat_IncreasesStarfighterManeuverability()
        {
            CapitalShip firstAttacker = CreateShip("first-attacker", hull: 100, weaponStrength: 0);
            CapitalShip secondAttacker = CreateShip(
                "second-attacker",
                hull: 100,
                weaponStrength: 0
            );
            foreach (CapitalShip ship in new[] { firstAttacker, secondAttacker })
            {
                ship.SublightSpeed = 1;
                ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 20;
                ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            }
            Starfighter uncommandedFighter = CreateFighter("uncommanded", 100, weaponStrength: 0);
            Starfighter commandedFighter = CreateFighter("commanded", 100, weaponStrength: 0);
            uncommandedFighter.ShieldStrength = 0;
            commandedFighter.ShieldStrength = 0;
            uncommandedFighter.SublightSpeed = 10;
            commandedFighter.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveMinimumManeuverRatio = 0.01;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveFighterHullPerCraft = 1;

            SpaceCombatResult uncommanded = Resolve(
                config,
                new[] { firstAttacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { uncommandedFighter },
                defenderCanWithdraw: true
            );
            SpaceCombatResult commanded = Resolve(
                config,
                new[] { secondAttacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { commandedFighter },
                defenderCanWithdraw: true,
                defenderCommand: new SpaceCombatCommandModifiers(
                    admiralLeadership: 0,
                    commanderCombat: 100
                )
            );

            Assert.AreEqual(
                98,
                GetFighterOutcome(uncommanded, uncommandedFighter).SquadronSizeAfter
            );
            Assert.AreEqual(99, GetFighterOutcome(commanded, commandedFighter).SquadronSizeAfter);
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

            SpaceCombatResult first = Resolve(
                new[] { singleArc },
                new[] { firstTarget },
                defenderCanWithdraw: true
            );
            SpaceCombatResult second = Resolve(
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
            config.LaserCannonCapitalDamageMultiplier = 0.25;
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatResult result = Resolve(
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
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 8;
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            Starfighter defender = CreateFighter("defender", squadronSize: 2, weaponStrength: 0);
            defender.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatResult result = Resolve(
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
        public void Resolve_CapitalShipTurbolaserAgainstStarfighters_UsesConfiguredMultiplier()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 0);
            attacker.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = 16;
            attacker.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = 100;
            Starfighter defender = CreateFighter("defender", squadronSize: 2, weaponStrength: 0);
            defender.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.TurbolaserFighterDamageMultiplier = 0.5;
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatResult result = Resolve(
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
            attacker.Maneuverability = 100;
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 48;
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            CapitalShip defenderShip = CreatePassiveTarget("defender-ship", hull: 100);
            Starfighter defenderFighter = CreateFighter(
                "defender-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            defenderFighter.Agility = 0;
            defenderFighter.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 0.01;
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult result = Resolve(
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
            config.LaserCannonCapitalDamageMultiplier = 0.25;
            config.AutoResolveMaximumIterations = 0;

            SpaceCombatResult result = Resolve(
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

            SpaceCombatResult result = Resolve(escorts, new[] { lineShip });

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

            SpaceCombatResult result = Resolve(
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

            SpaceCombatResult result = Resolve(
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

            SpaceCombatResult result = Resolve(
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

            SpaceCombatResult result = Resolve(
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
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 32);
            Starfighter defender = CreateFighter("defender", squadronSize: 10, weaponStrength: 0);
            defender.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 2;
            SequenceRNG random = new SequenceRNG(new[] { 0, 1 });

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { defender },
                defenderCanWithdraw: true,
                random: random
            );

            Assert.AreEqual(2, GetFighterOutcome(result, defender).SquadronSizeAfter);
        }

        /// <summary>
        /// Verifies capital-target laser scaling without reducing the charge consumed by a shot.
        /// </summary>
        /// <param name="multiplier">The configured capital-target laser damage multiplier.</param>
        /// <param name="iterations">The number of combat steps to resolve.</param>
        /// <param name="expectedHull">The target's remaining hull.</param>
        [TestCase(0.25, 1, 85)]
        [TestCase(0.25, 2, 85)]
        [TestCase(1, 1, 40)]
        [TestCase(0, 1, 100)]
        [TestCase(-1, 1, 100)]
        public void Resolve_FighterLaserAgainstCapitalShip_UsesConfiguredMultiplier(
            double multiplier,
            int iterations,
            int expectedHull
        )
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 1, weaponStrength: 60);
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = multiplier;
            config.AutoResolveMaximumIterations = iterations;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(expectedHull, GetShipOutcome(result, defender).HullAfter);
        }

        /// <summary>
        /// Verifies that target selection compares scaled capital damage with full fighter damage.
        /// </summary>
        [Test]
        public void Resolve_FighterLaserWithMixedTargets_SelectsHighestEffectiveDamageTarget()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 1, weaponStrength: 60);
            Starfighter defenderFighter = CreateFighter("defender-fighter", 12, weaponStrength: 0);
            defenderFighter.ShieldStrength = 0;
            CapitalShip defenderShip = CreatePassiveTarget("defender-ship", hull: 1000);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 0.25;
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defenderShip },
                new[] { defenderFighter },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(1000, GetShipOutcome(result, defenderShip).HullAfter);
            Assert.AreEqual(5, GetFighterOutcome(result, defenderFighter).SquadronSizeAfter);
        }

        /// <summary>
        /// Verifies that a capital-target laser multiplier does not reduce torpedo damage.
        /// </summary>
        [Test]
        public void Resolve_FighterTorpedoesAgainstCapitalShip_IgnoresLaserMultiplier()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 1, weaponStrength: 0);
            attacker.Torpedoes = 60;
            attacker.TorpedoRange = 100;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 0;
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(40, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_FighterTorpedoesAfterSamePhaseShieldDamage_DamageCapitalShipHull()
        {
            Starfighter torpedoAttacker = CreateFighter(
                "torpedo-attacker",
                squadronSize: 1,
                weaponStrength: 0
            );
            torpedoAttacker.Torpedoes = 20;
            torpedoAttacker.TorpedoRange = 100;
            Starfighter shieldAttacker = CreateFighter(
                "shield-attacker",
                squadronSize: 1,
                weaponStrength: 10
            );
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            defender.MaxShieldStrength = 10;
            defender.ShieldRechargeRate = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 1;
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { torpedoAttacker, shieldAttacker },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(80, GetShipOutcome(result, defender).HullAfter);
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
            config.LaserCannonCapitalDamageMultiplier = 1;
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatResult result = Resolve(
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
        public void Resolve_FighterWeaponCharge_MustRechargeBeforeFiringAgain()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 1, weaponStrength: 10);
            attacker.ShieldStrength = 1;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 1;
            config.AutoResolveMaximumIterations = 2;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(90, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_UnshieldedFighterWeaponCharge_RechargesAndFiresAgain()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 1, weaponStrength: 10);
            attacker.ShieldStrength = 0;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 1;
            config.AutoResolveMaximumIterations = 4;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(80, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_FighterWeaponCharge_WithDifferentShieldStrength_RechargesAtSameRate()
        {
            Starfighter unshieldedAttacker = CreateFighter(
                "unshielded-attacker",
                squadronSize: 1,
                weaponStrength: 10
            );
            unshieldedAttacker.ShieldStrength = 0;
            Starfighter shieldedAttacker = CreateFighter(
                "shielded-attacker",
                squadronSize: 1,
                weaponStrength: 10
            );
            shieldedAttacker.ShieldStrength = 5;
            CapitalShip firstDefender = CreatePassiveTarget("first-defender", hull: 100);
            CapitalShip secondDefender = CreatePassiveTarget("second-defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 4;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult unshieldedResult = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { unshieldedAttacker },
                new[] { firstDefender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );
            SpaceCombatResult shieldedResult = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { shieldedAttacker },
                new[] { secondDefender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(
                GetShipOutcome(unshieldedResult, firstDefender).HullAfter,
                GetShipOutcome(shieldedResult, secondDefender).HullAfter
            );
        }

        [Test]
        public void Resolve_FighterWeaponCharge_AfterSquadronLoss_RechargesAtReducedRate()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 12, weaponStrength: 10);
            attacker.ShieldStrength = 0;
            CapitalShip defender = CreateShip("defender", hull: 1000, weaponStrength: 6);
            defender.WeaponRecharge = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 1;
            config.AutoResolveMaximumIterations = 7;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.Greater(GetShipOutcome(result, defender).HullAfter, 640);
        }

        [Test]
        public void Resolve_FullFighterSquadronWeaponCharge_RechargesAggregateWeaponBank()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 12, weaponStrength: 10);
            CapitalShip defender = CreatePassiveTarget("defender", hull: 1000);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 1;
            config.AutoResolveMaximumIterations = 7;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(640, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_FighterSquadronShieldPool_AbsorbsDamageBeforeCraftHull()
        {
            CapitalShip firstAttacker = CreateShip("first-attacker", hull: 100, weaponStrength: 8);
            CapitalShip secondAttacker = CreateShip(
                "second-attacker",
                hull: 100,
                weaponStrength: 8
            );
            Starfighter shielded = CreateFighter("shielded", squadronSize: 1, weaponStrength: 0);
            Starfighter unshielded = CreateFighter(
                "unshielded",
                squadronSize: 1,
                weaponStrength: 0
            );
            shielded.ShieldStrength = 8;
            unshielded.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult shieldedResult = Resolve(
                config,
                new[] { firstAttacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { shielded },
                defenderCanWithdraw: true
            );
            SpaceCombatResult unshieldedResult = Resolve(
                config,
                new[] { secondAttacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { unshielded },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(1, GetFighterOutcome(shieldedResult, shielded).SquadronSizeAfter);
            Assert.AreEqual(0, GetFighterOutcome(unshieldedResult, unshielded).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_FighterHullPerCraft_ControlsSquadronDurability()
        {
            CapitalShip firstAttacker = CreateShip("first-attacker", hull: 100, weaponStrength: 8);
            CapitalShip secondAttacker = CreateShip(
                "second-attacker",
                hull: 100,
                weaponStrength: 8
            );
            Starfighter firstDefender = CreateFighter(
                "first-defender",
                squadronSize: 1,
                weaponStrength: 0
            );
            Starfighter secondDefender = CreateFighter(
                "second-defender",
                squadronSize: 1,
                weaponStrength: 0
            );
            firstDefender.ShieldStrength = 0;
            secondDefender.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig firstConfig = CreateConfig();
            firstConfig.AutoResolveFighterHullPerCraft = 8;
            firstConfig.AutoResolveMaximumIterations = 1;
            firstConfig.AutoResolveTargetScanDivisor = 1;
            firstConfig.AutoResolveStartingDistance = 0;
            GameConfig.SpaceCombatConfig secondConfig = firstConfig.CreateCopy();
            secondConfig.AutoResolveFighterHullPerCraft = 16;

            SpaceCombatResult firstResult = Resolve(
                firstConfig,
                new[] { firstAttacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { firstDefender },
                defenderCanWithdraw: true
            );
            SpaceCombatResult secondResult = Resolve(
                secondConfig,
                new[] { secondAttacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { secondDefender },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(0, GetFighterOutcome(firstResult, firstDefender).SquadronSizeAfter);
            Assert.AreEqual(1, GetFighterOutcome(secondResult, secondDefender).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_FighterShieldRechargePerCraft_ControlsShieldRecovery()
        {
            CapitalShip firstAttacker = CreateShip("first-attacker", hull: 100, weaponStrength: 3);
            CapitalShip secondAttacker = CreateShip(
                "second-attacker",
                hull: 100,
                weaponStrength: 3
            );
            Starfighter firstDefender = CreateFighter(
                "first-defender",
                squadronSize: 1,
                weaponStrength: 0
            );
            Starfighter secondDefender = CreateFighter(
                "second-defender",
                squadronSize: 1,
                weaponStrength: 0
            );
            firstDefender.ShieldStrength = 1;
            secondDefender.ShieldStrength = 1;
            GameConfig.SpaceCombatConfig firstConfig = CreateConfig();
            firstConfig.AutoResolveFighterShieldRechargePerCraft = 0;
            firstConfig.AutoResolveMaximumIterations = 3;
            firstConfig.AutoResolveTargetScanDivisor = 1;
            firstConfig.AutoResolveStartingDistance = 0;
            GameConfig.SpaceCombatConfig secondConfig = firstConfig.CreateCopy();
            secondConfig.AutoResolveFighterShieldRechargePerCraft = 1;

            SpaceCombatResult firstResult = Resolve(
                firstConfig,
                new[] { firstAttacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { firstDefender },
                defenderCanWithdraw: true
            );
            SpaceCombatResult secondResult = Resolve(
                secondConfig,
                new[] { secondAttacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { secondDefender },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(0, GetFighterOutcome(firstResult, firstDefender).SquadronSizeAfter);
            Assert.AreEqual(1, GetFighterOutcome(secondResult, secondDefender).SquadronSizeAfter);
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

            SpaceCombatResult result = Resolve(
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

            SpaceCombatResult conventionalFirst = Resolve(
                config,
                new[] { conventionalAttacker, ionAttacker },
                new List<Starfighter>(),
                new[] { conventionalFirstTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );
            SpaceCombatResult ionFirst = Resolve(
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

            SpaceCombatResult result = Resolve(
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

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                attackerCanWithdraw: true,
                random: new AttackDelayRNG()
            );

            Assert.AreEqual(2, GetFighterOutcome(result, attacker).SquadronSizeAfter);
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
            defenderFighter.ShieldStrength = 0;
            defenderFighter.SublightSpeed = 50;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 2;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveFighterHullPerCraft = 0.1;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defenderShip },
                new[] { defenderFighter },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(100, GetShipOutcome(result, defenderShip).HullAfter);
            Assert.AreEqual(0, GetFighterOutcome(result, defenderFighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_IonOnlyCapitalShipAgainstFighters_DoesNotDamageFighters()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 0);
            attacker.PrimaryWeapons[PrimaryWeaponType.IonCannon][0] = 100;
            attacker.PrimaryWeapons[PrimaryWeaponType.IonCannon][4] = 100;
            Starfighter defender = CreateFighter("defender", squadronSize: 12, weaponStrength: 0);

            SpaceCombatResult result = Resolve(
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

            SpaceCombatResult result = Resolve(
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
        public void Resolve_FighterWeaponStrength_ScalesFullSquadronWeaponBank()
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

            SpaceCombatResult first = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { singleFighter },
                new[] { firstTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );
            SpaceCombatResult second = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { fullSquadron },
                new[] { secondTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(99, GetShipOutcome(first, firstTarget).HullAfter);
            Assert.AreEqual(98, GetShipOutcome(second, secondTarget).HullAfter);
        }

        [Test]
        public void Resolve_DamagedFighterSquadron_DealsProportionalAggregateDamage()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 12, weaponStrength: 12);
            attacker.CurrentSquadronSize = 6;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 1;
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(28, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_DamagedFighterSquadron_StartsWithProportionalShields()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 12);
            Starfighter defender = CreateFighter("defender", squadronSize: 12, weaponStrength: 0);
            defender.CurrentSquadronSize = 6;
            defender.ShieldStrength = 8;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { defender },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(5, GetFighterOutcome(result, defender).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_DamagedCapitalShip_TakesLongerToDefeatEquivalentTarget()
        {
            CapitalShip undamaged = CreateShip("undamaged", hull: 100, weaponStrength: 10);
            CapitalShip damaged = CreateShip("damaged", hull: 100, weaponStrength: 10);
            damaged.CurrentHullStrength = 50;
            CapitalShip firstTarget = CreatePassiveTarget("first-target", hull: 1000);
            CapitalShip secondTarget = CreatePassiveTarget("second-target", hull: 1000);

            SpaceCombatResult first = Resolve(
                new[] { undamaged },
                new[] { firstTarget },
                defenderCanWithdraw: true
            );
            SpaceCombatResult second = Resolve(
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

            SpaceCombatResult result = Resolve(
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

            SpaceCombatResult result = Resolve(
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

            SpaceCombatResult result = Resolve(
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

            SpaceCombatResult result = Resolve(
                new[] { attacker },
                new[] { defender },
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(0, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_IntactForceBelowOpposingStrengthThreshold_WithdrawsAtOnce()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 100);
            CapitalShip defender = CreateShip("defender", hull: 1000, weaponStrength: 32);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 0.33;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new ArcDamageRNG()
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
        }

        [Test]
        public void Resolve_ForceAtOpposingStrengthThreshold_FiresBeforeStalemateResolution()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 100);
            CapitalShip defender = CreateShip("defender", hull: 1000, weaponStrength: 33);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 0.33;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new ArcDamageRNG()
            );

            Assert.Less(GetShipOutcome(result, attacker).HullAfter, 1000);
        }

        [Test]
        public void Resolve_ZeroStrengthForce_WithdrawsAtOnce()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 100);
            CapitalShip defender = CreatePassiveTarget("defender", hull: 1000);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 0.33;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
        }

        [Test]
        public void Resolve_PositiveStrengthForceAgainstZeroStrengthOpponent_DoesNotWithdraw()
        {
            CapitalShip attacker = CreatePassiveTarget("attacker", hull: 1);
            CapitalShip defender = CreateShip("defender", hull: 1000, weaponStrength: 1);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new ArcDamageRNG()
            );

            Assert.IsFalse(GetShipOutcome(result, defender).Withdrew);
            Assert.AreEqual(0, GetShipOutcome(result, attacker).HullAfter);
        }

        [Test]
        public void Resolve_MixedOpposingTargets_UsesCapitalTargetStrengthForWithdrawal()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 40);
            Starfighter attackerFighter = CreateFighter(
                "attacker-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            CapitalShip defender = CreateShip("defender", hull: 1000, weaponStrength: 0);
            defender.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 60;
            defender.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            defender.WeaponRecharge = 60;
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 0.33;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new[] { attackerFighter },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new ArcDamageRNG()
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void Resolve_ForceReachesOpposingStrengthThreshold_CompletesAccordingToWithdrawalAvailability(
            bool canWithdraw,
            bool expectsWithdrawal
        )
        {
            CapitalShip attacker = CreateShip("attacker", hull: 10000, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 1);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 0.33;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatResult result = Resolve(
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
        public void Resolve_FighterForceFallsBelowOpposingStrengthThreshold_WithdrawsForce()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 10000, weaponStrength: 10);
            Starfighter defender = CreateFighter("defender", squadronSize: 12, weaponStrength: 1);
            defender.ShieldStrength = 0;
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 1;
            config.AutoResolveRetreatStrengthRatio = 0.33;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { defender },
                attackerCanWithdraw: false,
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.That(GetFighterOutcome(result, defender).SquadronSizeAfter, Is.InRange(1, 11));
        }

        [Test]
        public void Resolve_TorpedoOnlyFighterAboveOpposingStrengthThreshold_DoesNotWithdraw()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 1, weaponStrength: 0);
            attacker.Torpedoes = 60;
            attacker.TorpedoRange = 100;
            CapitalShip defender = CreateShip("defender", hull: 40, weaponStrength: 1);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 0.33;
            config.AutoResolveTargetScanDivisor = 1;
            config.AutoResolveStartingDistance = 0;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                attackerCanWithdraw: true,
                defenderCanWithdraw: false
            );

            Assert.AreNotEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
        }

        [Test]
        public void Resolve_ThreeCapitalShipsWithdrawWithSlowNonHyperdriveShip_DoesNotDelayWithdrawal()
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
            strandedShip.SublightSpeed = 1;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 10;
            config.AutoResolveTargetScanDivisor = 1;
            SpaceCombatResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { firstWithdrawingShip, secondWithdrawingShip, strandedShip },
                    new List<Starfighter>(),
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.IsTrue(GetShipOutcome(result, firstWithdrawingShip).Withdrew);
            Assert.IsTrue(GetShipOutcome(result, secondWithdrawingShip).Withdrew);
            Assert.IsFalse(GetShipOutcome(result, strandedShip).Withdrew);
            Assert.AreEqual(0, GetShipOutcome(result, strandedShip).HullAfter);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(2, result.IterationsCompleted);
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
            SpaceCombatResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { fastFleetShip, slowFleetShip, coveringShip },
                    new[] { carriedFighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
            Assert.IsFalse(GetShipOutcome(result, fastFleetShip).Withdrew);
            Assert.IsFalse(GetShipOutcome(result, slowFleetShip).Withdrew);
            Assert.IsFalse(GetFighterOutcome(result, carriedFighter).Withdrew);
        }

        [Test]
        public void Resolve_WithdrawingForce_IsShotAtAndShootsBackUntilItIsGone()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 5);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 1);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 20;
            config.AutoResolveTargetScanDivisor = 1;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new ArcDamageRNG()
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.Less(GetShipOutcome(result, defender).HullAfter, 100);
            Assert.Less(GetShipOutcome(result, attacker).HullAfter, 100);
        }

        [Test]
        public void Resolve_WithdrawingShip_FliesOutPastItsOwnEdgeBeforeItIsGone()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 5);
            CapitalShip defender = CreateShip("defender", hull: 100000, weaponStrength: 1);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 100;
            config.AutoResolveWithdrawalDistance = 15;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            // Sixty back to its own edge (0.6 starting distances) and fifteen beyond it: eight
            // iterations at speed ten, and the battle ends on the next.
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(9, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_UnitsReachWithdrawalBoundaryAtDifferentTimes_CompletesEachUnitIndependently()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 5);
            CapitalShip fastDefender = CreateShip("fast-defender", hull: 100, weaponStrength: 1);
            fastDefender.SublightSpeed = 20;
            CapitalShip slowDefender = CreateShip("slow-defender", hull: 100, weaponStrength: 1);
            slowDefender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 20;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { fastDefender, slowDefender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.IsTrue(GetShipOutcome(result, fastDefender).Withdrew);
            Assert.IsFalse(GetShipOutcome(result, slowDefender).Withdrew);
        }

        [Test]
        public void Resolve_WithdrawalCannotAdvance_DoesNotWithdrawOpposingForce()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 1);
            defender.SublightSpeed = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveMaximumIterations = 2;
            config.AutoResolveStagnationIterations = 2;
            config.AutoResolveRetreatStrengthRatio = 0.33;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 20;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.AttackerOutcome);
            Assert.IsFalse(GetShipOutcome(result, attacker).Withdrew);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
        }

        [Test]
        public void Resolve_CarriedNonHyperdriveFighterWithdraws_PreservesFighter()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 1);
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
            SpaceCombatResult result = CreateResolver(config)
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { carrier },
                    new[] { fighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.IsTrue(GetShipOutcome(result, carrier).Withdrew);
            Assert.IsTrue(GetFighterOutcome(result, fighter).Withdrew);
            Assert.AreEqual(12, GetFighterOutcome(result, fighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_CarrierInOtherFleetHasRecoveryCapacity_PreservesNonHyperdriveFighter()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            CapitalShip destroyedCarrier = CreateShip(
                "destroyed-carrier",
                hull: 1,
                weaponStrength: 1
            );
            destroyedCarrier.StarfighterCapacity = 1;
            destroyedCarrier.SublightSpeed = 10;
            destroyedCarrier.CurrentHullStrength = 0;
            CapitalShip otherFleetCarrier = CreateShip(
                "other-fleet-carrier",
                hull: 1000,
                weaponStrength: 1
            );
            otherFleetCarrier.StarfighterCapacity = 1;
            otherFleetCarrier.SublightSpeed = 10;
            Starfighter fighter = CreateFighter("fighter", squadronSize: 12, weaponStrength: 0);
            fighter.Hyperdrive = 0;
            fighter.ShieldStrength = 100;
            fighter.SublightSpeed = 10;
            Fleet strandedFleet = new Fleet();
            Fleet recoveryFleet = new Fleet();
            strandedFleet.AddChild(destroyedCarrier);
            destroyedCarrier.SetParent(strandedFleet);
            destroyedCarrier.AddChild(fighter);
            fighter.SetParent(destroyedCarrier);
            recoveryFleet.AddChild(otherFleetCarrier);
            otherFleetCarrier.SetParent(recoveryFleet);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            config.AutoResolveStartingDistance = 0;
            config.AutoResolveWithdrawalDistance = 20;
            config.AutoResolveTargetScanDivisor = 1;
            SpaceCombatResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { destroyedCarrier, otherFleetCarrier },
                    new[] { fighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.AreEqual(0, GetShipOutcome(result, destroyedCarrier).HullAfter);
            Assert.IsTrue(GetShipOutcome(result, otherFleetCarrier).Withdrew);
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
            carrier.CurrentHullStrength = 0;
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
            SpaceCombatResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { carrier, escapeShip },
                    new[] { fighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.IsTrue(GetShipOutcome(result, escapeShip).Withdrew);
            Assert.IsFalse(GetFighterOutcome(result, fighter).Withdrew);
            Assert.AreEqual(0, GetFighterOutcome(result, fighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_TwoCarriersDestroyedWithNonHyperdriveFighters_DestroysFighters()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 100);
            attacker.WeaponRecharge = 100;
            CapitalShip firstCarrier = CreatePassiveTarget("first-carrier", hull: 1);
            firstCarrier.StarfighterCapacity = 1;
            firstCarrier.SublightSpeed = 1;
            CapitalShip secondCarrier = CreatePassiveTarget("second-carrier", hull: 1);
            secondCarrier.StarfighterCapacity = 1;
            secondCarrier.SublightSpeed = 1;
            firstCarrier.CurrentHullStrength = 0;
            secondCarrier.CurrentHullStrength = 0;
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
            SpaceCombatResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { firstCarrier, secondCarrier },
                    new[] { firstFighter, secondFighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.AreEqual(0, GetShipOutcome(result, firstCarrier).HullAfter);
            Assert.AreEqual(0, GetShipOutcome(result, secondCarrier).HullAfter);
            Assert.IsFalse(GetFighterOutcome(result, firstFighter).Withdrew);
            Assert.IsFalse(GetFighterOutcome(result, secondFighter).Withdrew);
            Assert.AreEqual(0, GetFighterOutcome(result, firstFighter).SquadronSizeAfter);
            Assert.AreEqual(0, GetFighterOutcome(result, secondFighter).SquadronSizeAfter);
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
            destroyedCarrier.CurrentHullStrength = 0;
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
            inTransitFighter.Movement = TestMovement.Create();
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
            SpaceCombatResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { destroyedCarrier, recoveryCarrier },
                    new[] { strandedFighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
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
            destroyedCarrier.CurrentHullStrength = 0;
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
            SpaceCombatResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { destroyedCarrier, recoveryCarrier },
                    new[] { fighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
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
            destroyedCarrier.CurrentHullStrength = 0;
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
            SpaceCombatResult result = CreateResolver(config, new ArcDamageRNG())
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { destroyedCarrier, recoveryCarrier },
                    new[] { hyperdriveFighter, nonHyperdriveFighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
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

            SpaceCombatResult result = Resolve(new[] { attacker }, new[] { defender });

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

            SpaceCombatResult result = Resolve(
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

        [Test]
        public void Resolve_PlanetDestroyer_StaysOutOfTheLineAndBurnsOneShipPerRecharge()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.BattleStationSuperlaserRechargeRounds = 20;
            CapitalShip station = CreateStation("station");
            CapitalShip[] targets =
            {
                CreatePassiveTarget("first", 100000),
                CreatePassiveTarget("second", 100000),
                CreatePassiveTarget("third", 100000),
            };

            SpaceCombatResult result = Resolve(
                config,
                new[] { station },
                new List<Starfighter>(),
                targets,
                new List<Starfighter>()
            );

            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.IsTrue(targets.All(target => GetShipOutcome(result, target).HullAfter == 0));
            Assert.IsFalse(result.ShipDamage.Any(damage => damage.Ship == station));
            Assert.Greater(result.IterationsCompleted, 50);
            Assert.Less(result.IterationsCompleted, 100);
        }

        [Test]
        public void Resolve_TorpedoSquadronsAgainstStation_FlyItsTrenchAndDestroyIt()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.BattleStationTrenchRunBaseChance = 1;
            CapitalShip station = CreateStation("station");
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);
            Starfighter escortFighter = CreateFighter("a-wing", 12, 0);

            SpaceCombatResult result = Resolve(
                config,
                new[] { CreatePassiveTarget("carrier", 100) },
                new[] { torpedoFighter, escortFighter },
                new[] { station },
                new List<Starfighter>()
            );

            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(
                0,
                result.ShipDamage.Single(damage => damage.Ship == station).HullAfter
            );
            Assert.IsFalse(result.FighterLosses.Any());
            // The run takes as long as the flight to the station: six starting distances.
            Assert.Greater(result.IterationsCompleted, 40);
            Assert.Less(result.IterationsCompleted, 70);
        }

        [Test]
        public void Resolve_FailedTrenchRun_CostsEverySquadronThatFlewIt()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.BattleStationTrenchRunBaseChance = 0;
            CapitalShip station = CreateStation("station");
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);
            Starfighter escortFighter = CreateFighter("a-wing", 12, 0);
            escortFighter.SublightSpeed = 8;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { torpedoFighter, escortFighter },
                new[] { station },
                new List<Starfighter>(),
                attackerCanWithdraw: true
            );

            FighterLossResult loss = result.FighterLosses.Single();
            Assert.AreSame(torpedoFighter, loss.Fighter);
            Assert.AreEqual(0, loss.SquadsAfter);
            Assert.IsFalse(result.ShipDamage.Any(damage => damage.Ship == station));
            Assert.AreEqual(CombatSide.Defender, result.Winner);
            // With its torpedoes gone the survivor has no way to hurt the station and leaves.
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            CollectionAssert.Contains(result.WithdrawnUnits, escortFighter);
        }

        [TestCase(0, true)]
        [TestCase(20, true)]
        [TestCase(120, false)]
        public void Resolve_TrenchRunAgainstDefendingFighters_SucceedsLessOftenTheMoreItIsOutnumbered(
            int defendingFighters,
            bool expectedDestroyed
        )
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.BattleStationTrenchRunBaseChance = 1;
            config.BattleStationTrenchRunMinimumChance = 0.2;
            CapitalShip station = CreateStation("station");
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);
            List<Starfighter> defenders = new List<Starfighter>();
            if (defendingFighters > 0)
                defenders.Add(CreateFighter("tie", defendingFighters, 0));

            // Twelve fighters fly the run and the roll is 0.5. The chance is 1 unopposed, 12/20
            // against twenty defenders, and the floor of 0.2 against a hundred and twenty.
            SpaceCombatResult result = Resolve(
                config,
                new[] { CreatePassiveTarget("carrier", 100) },
                new[] { torpedoFighter },
                new[] { station },
                defenders,
                random: new TrenchRunRNG()
            );

            bool destroyed = result.ShipDamage.Any(damage =>
                damage.Ship == station && damage.HullAfter == 0
            );
            Assert.AreEqual(expectedDestroyed, destroyed);
        }

        [Test]
        public void Resolve_ShieldedStation_IsNotAttackedAndItsEnemyLeaves()
        {
            CapitalShip station = CreateStation("station");
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);

            SpaceCombatResult result = CreateResolver()
                .Resolve(
                    new List<CapitalShip>(),
                    new[] { torpedoFighter },
                    new[] { station },
                    new List<Starfighter>(),
                    attackerCanWithdraw: true,
                    defenderCanWithdraw: false,
                    shieldedStations: new[] { station }
                );

            Assert.IsFalse(result.FighterLosses.Any());
            Assert.IsFalse(result.ShipDamage.Any());
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(CombatSide.Defender, result.Winner);
            Assert.Less(result.IterationsCompleted, 30);
        }

        [Test]
        public void Resolve_OutgunnedSideWithStation_HoldsInsteadOfRetreating()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 0.33;
            CapitalShip station = CreateStation("station");
            CapitalShip escort = CreateShip("escort", hull: 100000, weaponStrength: 1);
            CapitalShip raider = CreateShip("raider", hull: 100000, weaponStrength: 500);
            CapitalShip loneEscort = CreateShip("escort", hull: 100000, weaponStrength: 1);
            loneEscort.SublightSpeed = 4;

            SpaceCombatResult withStation = Resolve(
                config,
                new[] { raider },
                new List<Starfighter>(),
                new[] { station, escort },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );
            SpaceCombatResult withoutStation = Resolve(
                config,
                new[] { CreateShip("raider", hull: 100000, weaponStrength: 500) },
                new List<Starfighter>(),
                new[] { loneEscort },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, withoutStation.DefenderOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, withStation.DefenderOutcome);
            Assert.AreEqual(CombatSide.Defender, withStation.Winner);
            Assert.AreEqual(0, GetShipOutcome(withStation, raider).HullAfter);
        }

        [Test]
        public void Resolve_SquadronsOnATrenchRun_AreShotAtOnTheWayIn()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.BattleStationTrenchRunBaseChance = 1;
            config.AutoResolveTargetScanDivisor = 1;
            CapitalShip station = CreateStation("station");
            CapitalShip picket = CreateShip("picket", hull: 1000, weaponStrength: 0);
            picket.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 500;
            picket.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            picket.WeaponRecharge = 500;
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { torpedoFighter },
                new[] { station, picket },
                new List<Starfighter>(),
                random: new ArcDamageRNG()
            );

            // The picket's guns reach the squadron while it is still flying to the station, so
            // a run that could not fail is never rolled.
            Assert.AreEqual(0, GetFighterOutcome(result, torpedoFighter).SquadronSizeAfter);
            Assert.IsFalse(result.ShipDamage.Any(damage => damage.Ship == station));
            Assert.AreEqual(CombatSide.Defender, result.Winner);
        }

        [Test]
        public void Resolve_FleetFleeingALoneStation_LosesAShipToTheSuperlaser()
        {
            CapitalShip station = CreateStation("station");
            CapitalShip first = CreatePassiveTarget("first", 100000);
            first.SublightSpeed = 4;
            CapitalShip second = CreatePassiveTarget("second", 100000);
            second.SublightSpeed = 4;
            CapitalShip[] fleet = { first, second };

            SpaceCombatResult result = Resolve(
                CreateConfig(),
                fleet,
                new List<Starfighter>(),
                new[] { station },
                new List<Starfighter>(),
                attackerCanWithdraw: true
            );

            // The fleet cannot hurt the station and turns for home at once, but a ship running
            // for hyperspace is still a target: the superlaser's first shot catches one of them.
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(1, fleet.Count(ship => GetShipOutcome(result, ship).HullAfter == 0));
            Assert.AreEqual(1, fleet.Count(ship => GetShipOutcome(result, ship).Withdrew));
            Assert.IsFalse(result.ShipDamage.Any(damage => damage.Ship == station));
        }

        [Test]
        public void Resolve_StalemateAgainstAStation_IsNeverLostByTheStationSide()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.BattleStationSuperlaserRechargeRounds = 200000;
            CapitalShip station = CreateStation("station");
            CapitalShip target = CreatePassiveTarget("target", 100000);

            // Nothing can change once the one shot is spent, so the stalemate rule settles it:
            // the station holds the field although it could have left and its enemy could not.
            SpaceCombatResult result = Resolve(
                config,
                new[] { station },
                new List<Starfighter>(),
                new[] { target, CreatePassiveTarget("other", 100000) },
                new List<Starfighter>(),
                attackerCanWithdraw: true,
                defenderCanWithdraw: false
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.IsFalse(result.WithdrawnUnits.Contains(station));
        }

        /// <summary>
        /// Creates a planet-destroying capital ship, which fights as a battle station.
        /// </summary>
        /// <param name="instanceId">The instance identifier.</param>
        /// <returns>The created ship.</returns>
        private static CapitalShip CreateStation(string instanceId)
        {
            CapitalShip station = CreateShip(instanceId, hull: 25000, weaponStrength: 1400);
            station.TypeID = "station";
            station.CanDestroyPlanets = true;
            return station;
        }

        /// <summary>
        /// Creates a fighter squadron that carries torpedoes and flies at a typical speed.
        /// </summary>
        /// <param name="instanceId">The instance identifier.</param>
        /// <param name="squadronSize">The squadron size.</param>
        /// <returns>The created squadron.</returns>
        private static Starfighter CreateTorpedoFighter(string instanceId, int squadronSize)
        {
            Starfighter fighter = CreateFighter(instanceId, squadronSize, 0);
            fighter.SublightSpeed = 8;
            fighter.Torpedoes = 4;
            fighter.TorpedoRange = 7;
            return fighter;
        }

        /// <summary>
        /// Resolves symmetric battle.
        /// </summary>
        /// <returns>The resolved symmetric battle.</returns>
        private static SpaceCombatResult ResolveSymmetricBattle()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 10);
            return Resolve(new[] { attacker }, new[] { defender });
        }

        /// <summary>
        /// Resolves the requested operation.
        /// </summary>
        /// <param name="attackerShips">The attacker ships.</param>
        /// <param name="defenderShips">The defender ships.</param>
        /// <param name="attackerCanWithdraw">Whether attacker can withdraw.</param>
        /// <param name="defenderCanWithdraw">Whether defender can withdraw.</param>
        /// <returns>The resolved value.</returns>
        private static SpaceCombatResult Resolve(
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

        /// <summary>
        /// Resolves the requested operation.
        /// </summary>
        /// <param name="attackerShips">The attacker ships.</param>
        /// <param name="attackerFighters">The attacker fighters.</param>
        /// <param name="defenderShips">The defender ships.</param>
        /// <param name="defenderFighters">The defender fighters.</param>
        /// <param name="attackerCanWithdraw">Whether attacker can withdraw.</param>
        /// <param name="defenderCanWithdraw">Whether defender can withdraw.</param>
        /// <returns>The resolved value.</returns>
        private static SpaceCombatResult Resolve(
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

        /// <summary>
        /// Resolves the requested operation.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="attackerShips">The attacker ships.</param>
        /// <param name="attackerFighters">The attacker fighters.</param>
        /// <param name="defenderShips">The defender ships.</param>
        /// <param name="defenderFighters">The defender fighters.</param>
        /// <param name="attackerCanWithdraw">Whether attacker can withdraw.</param>
        /// <param name="defenderCanWithdraw">Whether defender can withdraw.</param>
        /// <param name="random">The random.</param>
        /// <param name="attackerCommand">The attacker's tactical command modifiers.</param>
        /// <param name="defenderCommand">The defender's tactical command modifiers.</param>
        /// <returns>The resolved value.</returns>
        private static SpaceCombatResult Resolve(
            GameConfig.SpaceCombatConfig config,
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<Starfighter> attackerFighters,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> defenderFighters,
            bool attackerCanWithdraw = false,
            bool defenderCanWithdraw = false,
            IRandomNumberProvider random = null,
            SpaceCombatCommandModifiers attackerCommand = default,
            SpaceCombatCommandModifiers defenderCommand = default
        )
        {
            return CreateResolver(config, random)
                .Resolve(
                    attackerShips,
                    attackerFighters,
                    defenderShips,
                    defenderFighters,
                    attackerCanWithdraw,
                    defenderCanWithdraw,
                    attackerCommand,
                    defenderCommand
                );
        }

        /// <summary>
        /// Creates config.
        /// </summary>
        /// <returns>The created config.</returns>
        private static GameConfig.SpaceCombatConfig CreateConfig()
        {
            return new GameConfig.SpaceCombatConfig
            {
                AdmiralLeadershipDivisor = 10,
                CommanderCombatDivisor = 20,
                LaserCannonCapitalDamageMultiplier = 1.0 / 6.0,
                TurbolaserFighterDamageMultiplier = 1,
                FighterIonCannonFighterDamageMultiplier = 0.5,
                AutoResolveWeaponDamagePerStrength = 1,
                AutoResolveTorpedoDamagePerStrength = 1,
                AutoResolveFighterWeaponRechargeMultiplier = 3.751,
                AutoResolveFighterShieldRechargePerCraft = 0.025,
                AutoResolveFighterHullPerCraft = 8,
                AutoResolveMaximumIterations = 4096,
                AutoResolveStagnationIterations = 1200,
                AutoResolveRetreatStrengthRatio = 0,
                AutoResolveMinimumManeuverRatio = 0.1,
                AutoResolveTargetScanDivisor = 3,
                AutoResolveStartingDistance = 75,
                AutoResolveWithdrawalDistance = 10,
                AutoResolveComponentDamageInterval = 1,
                AutoResolveComponentDamageRollMaximum = 10,
                AutoResolveComponentDelayMinimum = 30,
                AutoResolveComponentDelayMaximum = 50,
                AutoResolveComponentDelayRecovery = 1,
                BattleStationSuperlaserRechargeRounds = 360,
                BattleStationTrenchRunBaseChance = 0.9,
                BattleStationTrenchRunMinimumChance = 0.25,
            };
        }

        /// <summary>
        /// Creates resolver.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="random">The random.</param>
        /// <returns>The created resolver.</returns>
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

        /// <summary>
        /// Creates ship.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="hull">The hull.</param>
        /// <param name="weaponStrength">The weapon strength.</param>
        /// <param name="armEveryArc">Whether arm every arc.</param>
        /// <returns>The created ship.</returns>
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

        /// <summary>
        /// Creates laser escort.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The created laser escort.</returns>
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

        /// <summary>
        /// Creates heavy line ship.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The created heavy line ship.</returns>
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

        /// <summary>
        /// Creates fighter.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="squadronSize">The squadron size.</param>
        /// <param name="weaponStrength">The weapon strength.</param>
        /// <returns>The created fighter.</returns>
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

        /// <summary>
        /// Creates passive target.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="hull">The hull.</param>
        /// <returns>The created passive target.</returns>
        private static CapitalShip CreatePassiveTarget(string instanceId, int hull)
        {
            return CreateShip(instanceId, hull, weaponStrength: 0);
        }

        /// <summary>
        /// Gets ship outcome.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="ship">The ship.</param>
        /// <returns>The requested ship outcome.</returns>
        private static (int HullAfter, bool Withdrew) GetShipOutcome(
            SpaceCombatResult result,
            CapitalShip ship
        )
        {
            ShipDamageResult damage = result.ShipDamage.SingleOrDefault(outcome =>
                outcome.Ship == ship
            );
            return (
                damage?.HullAfter ?? ship.CurrentHullStrength,
                result.WithdrawnUnits.Contains(ship)
            );
        }

        /// <summary>
        /// Gets fighter outcome.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="fighter">The fighter.</param>
        /// <returns>The requested fighter outcome.</returns>
        private static (int SquadronSizeAfter, bool Withdrew) GetFighterOutcome(
            SpaceCombatResult result,
            Starfighter fighter
        )
        {
            FighterLossResult loss = result.FighterLosses.SingleOrDefault(outcome =>
                outcome.Fighter == fighter
            );
            return (
                loss?.SquadsAfter ?? fighter.CurrentSquadronSize,
                result.WithdrawnUnits.Contains(fighter)
            );
        }

        /// <summary>
        /// Returns committed hull values in stable damage-record order.
        /// </summary>
        /// <param name="result">The resolved combat result.</param>
        /// <returns>The committed post-combat hull values.</returns>
        private static IEnumerable<int> GetResolvedShipHulls(SpaceCombatResult result)
        {
            return result.ShipDamage.Select(damage => damage.HullAfter);
        }

        private sealed class ArcDamageRNG : IRandomNumberProvider
        {
            /// <summary>
            /// Executes next double.
            /// </summary>
            /// <returns>The result of next double.</returns>
            public double NextDouble()
            {
                return 0;
            }

            /// <summary>
            /// Executes next int.
            /// </summary>
            /// <param name="min">The min.</param>
            /// <param name="max">The max.</param>
            /// <returns>The result of next int.</returns>
            public int NextInt(int min, int max)
            {
                return min == 1 && max == 11 ? 3 : min;
            }
        }

        private sealed class TrenchRunRNG : IRandomNumberProvider
        {
            /// <summary>
            /// Executes next double.
            /// </summary>
            /// <returns>The result of next double.</returns>
            public double NextDouble()
            {
                return 0.5;
            }

            /// <summary>
            /// Executes next int.
            /// </summary>
            /// <param name="min">The min.</param>
            /// <param name="max">The max.</param>
            /// <returns>The result of next int.</returns>
            public int NextInt(int min, int max)
            {
                return min;
            }
        }

        private sealed class AttackDelayRNG : IRandomNumberProvider
        {
            /// <summary>
            /// Executes next double.
            /// </summary>
            /// <returns>The result of next double.</returns>
            public double NextDouble()
            {
                return 0;
            }

            /// <summary>
            /// Executes next int.
            /// </summary>
            /// <param name="min">The min.</param>
            /// <param name="max">The max.</param>
            /// <returns>The result of next int.</returns>
            public int NextInt(int min, int max)
            {
                return min == 1 && max == 11 ? 1 : min;
            }
        }
    }
}
