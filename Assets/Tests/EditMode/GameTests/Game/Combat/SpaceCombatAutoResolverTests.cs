using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

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
        public void Resolve_WeaponsAcrossSeveralArcs_UsesStrongestArc()
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

            Assert.AreEqual(
                GetShipOutcome(first, firstTarget).HullAfter,
                GetShipOutcome(second, secondTarget).HullAfter
            );
        }

        [Test]
        public void Resolve_IonOnlyCapitalShipAgainstFighters_DoesNotDamageFighters()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 0);
            attacker.PrimaryWeapons[PrimaryWeaponType.IonCannon][0] = 100;
            Starfighter defender = CreateFighter("defender", squadronSize: 12, weaponStrength: 0);

            SpaceCombatAutoResult result = new SpaceCombatAutoResolver(CreateConfig()).Resolve(
                new[] { attacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { defender },
                attackerCanWithdraw: true,
                defenderCanWithdraw: true
            );

            Assert.AreEqual(12, GetFighterOutcome(result, defender).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_LargerFighterSquadron_DealsMoreDamage()
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

            SpaceCombatAutoResult first = Resolve(
                new List<CapitalShip>(),
                new[] { singleFighter },
                new[] { firstTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );
            SpaceCombatAutoResult second = Resolve(
                new List<CapitalShip>(),
                new[] { fullSquadron },
                new[] { secondTarget },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.Less(
                GetShipOutcome(second, secondTarget).HullAfter,
                GetShipOutcome(first, firstTarget).HullAfter
            );
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
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 1);

            SpaceCombatAutoResult result = Resolve(
                new[] { attacker },
                new[] { defender },
                defenderCanWithdraw: canWithdraw
            );

            Assert.AreEqual(
                expectsWithdrawal
                    ? SpaceCombatSideOutcome.Withdrawn
                    : SpaceCombatSideOutcome.Destroyed,
                result.DefenderOutcome
            );
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
            return new SpaceCombatAutoResolver(CreateConfig()).Resolve(
                attackerShips,
                attackerFighters,
                defenderShips,
                defenderFighters,
                attackerCanWithdraw,
                defenderCanWithdraw
            );
        }

        private static GameConfig.SpaceCombatConfig CreateConfig()
        {
            return new GameConfig.SpaceCombatConfig
            {
                AutoResolveRandomSeed = 1894809716,
                AutoResolveMaximumIterations = 4096,
                AutoResolveStagnationIterations = 1200,
                AutoResolveRetreatStrengthRatio = 0.33,
                AutoResolveMinimumManeuverRatio = 0.1,
            };
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
                ManufacturingStatus = ManufacturingStatus.Complete,
                WeaponRecharge = 1,
            };
            int armedArcCount = armEveryArc ? 4 : 1;
            for (int arc = 0; arc < armedArcCount; arc++)
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][arc] = weaponStrength;
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
                LaserCannon = weaponStrength,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static CapitalShip CreatePassiveTarget(string instanceId, int hull)
        {
            CapitalShip ship = CreateShip(instanceId, hull, weaponStrength: 1);
            ship.WeaponRecharge = 0;
            return ship;
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
    }
}
