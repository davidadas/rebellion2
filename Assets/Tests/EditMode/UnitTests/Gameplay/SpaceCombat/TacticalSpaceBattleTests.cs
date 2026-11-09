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
    public class TacticalSpaceBattleTests
    {
        private const int _seed = 1894809716;

        [Test]
        public void Constructor_TwoForces_PlacesSidesInOppositeCornersFacingEachOther()
        {
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("attacker", 100, 10) },
                new[] { CreateShip("defender", 100, 10) }
            );

            TacticalBattleUnit attacker = battle.Units.Single(unit =>
                unit.Side == CombatSide.Attacker
            );
            TacticalBattleUnit defender = battle.Units.Single(unit =>
                unit.Side == CombatSide.Defender
            );
            Assert.Less(attacker.X, 0);
            Assert.Less(attacker.Y, 0);
            Assert.Greater(defender.X, 0);
            Assert.Greater(defender.Y, 0);
            Assert.Greater(attacker.GetDistanceTo(defender), battle.HalfWidth);
            Assert.AreEqual(attacker.GetAngleTo(defender), attacker.Heading, 0.0001);
            Assert.AreEqual(defender.GetAngleTo(attacker), defender.Heading, 0.0001);
            Assert.AreEqual(CombatSide.Attacker, battle.PlayerSide);
            Assert.IsFalse(battle.IsComplete);
        }

        [Test]
        public void Constructor_CarrierFighters_LaunchBesideTheirCarrier()
        {
            CapitalShip carrier = CreateShip("carrier", 500, 10);
            carrier.StarfighterCapacity = 2;
            Starfighter fighter = CreateFighter("fighter", 12, 5);
            carrier.AddChild(fighter);
            fighter.SetParent(carrier);

            TacticalSpaceBattle battle = CreateBattle(
                new[] { carrier },
                new[] { CreateShip("defender", 100, 0) },
                attackerFighters: new[] { fighter }
            );

            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single();
            TacticalCapitalShipUnit carrierUnit = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Ship == carrier);
            Assert.AreSame(carrier, squadron.Carrier);
            Assert.Greater(squadron.X, carrierUnit.X);
            Assert.Greater(squadron.GetDistanceTo(carrierUnit), carrierUnit.HullFootprint);
            Assert.Less(squadron.GetDistanceTo(carrierUnit), carrierUnit.HullFootprint + 10);
        }

        [Test]
        public void Constructor_SeveralShips_SpawnsThemClearOfEachOthersHulls()
        {
            CapitalShip first = CreateShip("first", 400, 10);
            CapitalShip second = CreateShip("second", 400, 10);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { first, second },
                new[] { CreateShip("defender", 100, 0) }
            );

            TacticalCapitalShipUnit firstUnit = GetShip(battle, first);
            TacticalCapitalShipUnit secondUnit = GetShip(battle, second);

            Assert.GreaterOrEqual(
                firstUnit.GetDistanceTo(secondUnit),
                firstUnit.HullFootprint + secondUnit.HullFootprint
            );
        }

        [Test]
        public void Advance_ShipsOrderedOntoEachOther_AreKeptApart()
        {
            CapitalShip first = CreateShip("first", 400, 0);
            CapitalShip second = CreateShip("second", 400, 0);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { first, second },
                new[] { CreateShip("defender", 100000, 0) }
            );
            TacticalCapitalShipUnit firstUnit = GetShip(battle, first);
            TacticalCapitalShipUnit secondUnit = GetShip(battle, second);
            battle.IssueMoveOrder(new[] { firstUnit }, secondUnit.X, secondUnit.Y);

            battle.Advance(30);

            Assert.GreaterOrEqual(
                firstUnit.GetDistanceTo(secondUnit),
                (firstUnit.HullFootprint + secondUnit.HullFootprint) * 0.99
            );
        }

        [Test]
        public void Advance_FighterInRange_OrbitsItsTarget()
        {
            CapitalShip carrier = CreateShip("carrier", 500, 0);
            carrier.StarfighterCapacity = 2;
            Starfighter fighter = CreateFighter("fighter", 12, 5);
            carrier.AddChild(fighter);
            fighter.SetParent(carrier);
            CapitalShip defender = CreateShip("defender", 400, 0);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { carrier },
                new[] { defender },
                attackerFighters: new[] { fighter }
            );
            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single();
            TacticalCapitalShipUnit target = GetShip(battle, defender);

            battle.Advance(30);
            double engagementRange = squadron.GetEngagementRange(targetsFighters: false);
            double firstBearing = Math.Atan2(squadron.Y - target.Y, squadron.X - target.X);
            double firstX = squadron.X;
            double firstY = squadron.Y;
            battle.Advance(1);
            double secondBearing = Math.Atan2(squadron.Y - target.Y, squadron.X - target.X);

            Assert.AreSame(target, squadron.Target);
            Assert.LessOrEqual(squadron.GetEngagementDistanceTo(target), engagementRange);
            Assert.Greater(squadron.GetDistanceTo(target), target.HullFootprint);
            Assert.AreNotEqual(firstX, squadron.X);
            Assert.AreNotEqual(firstY, squadron.Y);
            Assert.Greater(
                Math.Abs(TacticalBattleGeometry.NormalizeAngle(secondBearing - firstBearing)),
                0.1
            );
            Assert.Greater(
                Math.Abs(
                    TacticalBattleGeometry.NormalizeAngle(
                        squadron.Heading - squadron.GetAngleTo(target)
                    )
                ),
                0.5
            );
        }

        [Test]
        public void Advance_TwoSquadronsOnOneTarget_CircleOnDifferentRings()
        {
            CapitalShip carrier = CreateShip("carrier", 500, 0);
            carrier.StarfighterCapacity = 2;
            Starfighter first = CreateFighter("first", 12, 5);
            Starfighter second = CreateFighter("second", 12, 5);
            foreach (Starfighter fighter in new[] { first, second })
            {
                carrier.AddChild(fighter);
                fighter.SetParent(carrier);
            }
            CapitalShip defender = CreateShip("defender", 400, 0);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { carrier },
                new[] { defender },
                attackerFighters: new[] { first, second }
            );
            TacticalStarfighterUnit firstUnit = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Fighter == first);
            TacticalStarfighterUnit secondUnit = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Fighter == second);
            TacticalCapitalShipUnit target = GetShip(battle, defender);
            // Pull the target off the arena edge so both rings fit without sliding along it.
            target.X = 0;
            target.Y = 0;

            battle.Advance(40);

            Assert.AreNotEqual(firstUnit.OrbitRadiusScale, secondUnit.OrbitRadiusScale);
            Assert.Greater(
                Math.Abs(firstUnit.GetDistanceTo(target) - secondUnit.GetDistanceTo(target)),
                1
            );
        }

        [Test]
        public void Advance_FighterWithoutAnEngageableTarget_PatrolsAroundItsCarrier()
        {
            CapitalShip carrier = CreateShip("carrier", 500, 10);
            carrier.StarfighterCapacity = 2;
            Starfighter fighter = CreateFighter("fighter", 12, 0);
            carrier.AddChild(fighter);
            fighter.SetParent(carrier);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { carrier },
                new[] { CreateShip("defender", 100000, 0) },
                attackerFighters: new[] { fighter }
            );
            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single();
            TacticalCapitalShipUnit carrierUnit = GetShip(battle, carrier);

            battle.Advance(30);
            double firstX = squadron.X;
            double firstY = squadron.Y;
            double firstBearing = Math.Atan2(
                squadron.Y - carrierUnit.Y,
                squadron.X - carrierUnit.X
            );
            battle.Advance(1);
            double secondBearing = Math.Atan2(
                squadron.Y - carrierUnit.Y,
                squadron.X - carrierUnit.X
            );

            Assert.AreEqual(0, squadron.GetEngagementRange(targetsFighters: false));
            Assert.IsFalse(squadron.IsWithdrawing);
            Assert.Greater(squadron.GetDistanceTo(carrierUnit), carrierUnit.HullFootprint / 2);
            Assert.Less(squadron.GetDistanceTo(carrierUnit), carrierUnit.HullFootprint + 20);
            Assert.AreNotEqual(firstX, squadron.X);
            Assert.AreNotEqual(firstY, squadron.Y);
            Assert.Greater(
                Math.Abs(TacticalBattleGeometry.NormalizeAngle(secondBearing - firstBearing)),
                0.1
            );
        }

        [Test]
        public void Advance_TractorBeamInRange_HoldsTheTargetAndDelaysItsJump()
        {
            CapitalShip prey = CreateShip("prey", 100000, 0);
            CapitalShip hunter = CreateShip("hunter", 400, 10, range: 150);
            hunter.TractorBeamPower = 3;
            hunter.TractorBeamnRange = 24;
            TacticalSpaceBattle battle = CreateBattle(
                new[] { prey },
                new[] { hunter },
                attackerCanWithdraw: true
            );
            TacticalCapitalShipUnit preyUnit = GetShip(battle, prey);
            TacticalCapitalShipUnit hunterUnit = GetShip(battle, hunter);

            battle.Advance(30);
            bool heldFromWeaponRange = preyUnit.IsTractored;
            // The hunter fights from weapon range; bring it alongside so the beam can reach.
            hunterUnit.X = preyUnit.X + 70;
            hunterUnit.Y = preyUnit.Y;
            battle.Advance(TacticalSpaceBattle.RoundSeconds);

            Assert.IsFalse(heldFromWeaponRange);
            Assert.AreSame(hunterUnit, preyUnit.TractoredBy);
            Assert.LessOrEqual(
                hunterUnit.GetEngagementDistanceTo(preyUnit),
                hunterUnit.TractorBeamRange
            );
            Assert.Less(preyUnit.Speed, prey.SublightSpeed);
            Assert.IsFalse(hunterUnit.IsTractored);

            Assert.IsTrue(battle.OrderWithdrawal(CombatSide.Attacker));
            battle.Advance(60);

            // The hunter follows its prey to the arena edge and the beam never lets go.
            Assert.IsTrue(preyUnit.IsWithdrawing);
            Assert.IsTrue(preyUnit.IsTractored);
            Assert.IsFalse(preyUnit.HasWithdrawn);
            Assert.LessOrEqual(
                hunterUnit.GetEngagementDistanceTo(preyUnit),
                hunterUnit.TractorBeamRange
            );

            hunterUnit.Destroy();
            battle.Advance(60);

            Assert.IsFalse(preyUnit.IsTractored);
            Assert.IsTrue(preyUnit.HasWithdrawn);
        }

        [Test]
        public void Advance_WithdrawingShip_StaysUnderFireUntilItIsGone()
        {
            CapitalShip runner = CreateShip("runner", 1000, 0);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { runner },
                new[] { CreateShip("hunter", 400, 10, range: 150) },
                attackerCanWithdraw: true
            );
            TacticalCapitalShipUnit runnerUnit = GetShip(battle, runner);

            Assert.IsTrue(battle.OrderWithdrawal(CombatSide.Attacker));
            battle.Advance(2);

            Assert.IsTrue(runnerUnit.IsWithdrawing);
            Assert.IsTrue(runnerUnit.IsTargetable);
            Assert.Less(runnerUnit.CurrentHull, 1000);
            Assert.IsFalse(runnerUnit.HasWithdrawn);
        }

        [Test]
        public void Constructor_FighterDurability_AddsAnAirframeToTheShields()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            Starfighter shielded = CreateFighter("shielded", 12, 5);
            shielded.ShieldStrength = 5;
            Starfighter bare = CreateFighter("bare", 12, 5);
            bare.ShieldStrength = 0;
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("carrier", 500, 10) },
                new[] { CreateShip("defender", 100000, 0) },
                attackerFighters: new[] { shielded, bare },
                config: config
            );
            TacticalStarfighterUnit shieldedUnit = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Fighter == shielded);
            TacticalStarfighterUnit bareUnit = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Fighter == bare);
            double airframe = config.TacticalFighterHullStrength;
            SystemRandomProvider random = new SystemRandomProvider(_seed);

            shieldedUnit.ApplyDamage((airframe + 5) * 5, 0, config, random);
            bareUnit.ApplyDamage(airframe * 5, 0, config, random);

            Assert.AreEqual(7, shieldedUnit.CurrentSquadronSize);
            Assert.AreEqual(7, bareUnit.CurrentSquadronSize);
            Assert.IsTrue(bareUnit.IsAlive);
        }

        [Test]
        public void GetEffectiveness_TurbolasersAgainstFighters_CountOnlyTheExpectedHits()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("battery", 400, 40) },
                new[] { CreateShip("defender", 100, 0) },
                config: config
            );
            TacticalCapitalShipUnit battery = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Side == CombatSide.Attacker);

            double againstShips = battery.GetEffectiveness(targetsFighters: false);
            double againstFighters = battery.GetEffectiveness(targetsFighters: true);

            Assert.Greater(againstShips, 0);
            Assert.AreEqual(
                againstShips * config.TacticalTurbolaserFighterHitChance,
                againstFighters,
                0.0001
            );
        }

        [Test]
        public void QueueAttacks_TurbolasersAgainstFighters_HitOnTheRollAndOtherwiseFlyWide()
        {
            double hitDamage = FireTurbolaserAtSquadron(
                hitChance: 1,
                out TacticalShot hitShot,
                out TacticalStarfighterUnit hitSquadron
            );
            double missDamage = FireTurbolaserAtSquadron(
                hitChance: 0.001,
                out TacticalShot missShot,
                out TacticalStarfighterUnit missSquadron
            );

            Assert.Greater(hitDamage, 0);
            Assert.AreEqual(hitSquadron.X, hitShot.EndX, 0.0001);
            Assert.AreEqual(hitSquadron.Y, hitShot.EndY, 0.0001);
            Assert.AreEqual(0, missDamage);
            Assert.Greater(
                TacticalBattleGeometry.GetDistance(
                    missShot.EndX,
                    missShot.EndY,
                    missSquadron.X,
                    missSquadron.Y
                ),
                missSquadron.Radius
            );
        }

        [Test]
        public void QueueAttacks_TorpedoSquadron_HoldsTorpedoesUntilTheTargetsShieldsAreDown()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.TacticalShieldGeneratorHitChance = 0;
            Starfighter fighter = CreateFighter("fighter", 12, 5);
            fighter.Torpedoes = 4;
            fighter.TorpedoRange = 7;
            CapitalShip ship = CreateShip("ship", 400, 0);
            ship.MaxShieldStrength = 100;
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("carrier", 500, 0) },
                new[] { ship },
                attackerFighters: new[] { fighter },
                config: config
            );
            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single();
            TacticalCapitalShipUnit target = GetShip(battle, ship);
            squadron.X = target.X + target.HullFootprint + 2;
            squadron.Y = target.Y;
            SystemRandomProvider random = new SystemRandomProvider(_seed);
            List<TacticalShot> shieldedShots = new List<TacticalShot>();
            List<TacticalShot> openShots = new List<TacticalShot>();

            squadron.QueueAttacks(
                new[] { target },
                (unit, amount, isIon) => { },
                shieldedShots,
                0.01,
                random
            );
            target.ApplyDamage(target.MaximumShields, 0, config, random);
            squadron.AdvanceState(1000, config);
            squadron.QueueAttacks(
                new[] { target },
                (unit, amount, isIon) => { },
                openShots,
                0.01,
                random
            );

            Assert.AreEqual(1, shieldedShots.Count);
            Assert.IsFalse(shieldedShots[0].IsTorpedo);
            Assert.IsTrue(target.ShieldsDown);
            Assert.AreEqual(2, openShots.Count);
            TacticalShot torpedo = openShots.Single(shot => shot.IsTorpedo);
            TacticalShot laser = openShots.Single(shot => !shot.IsTorpedo);
            Assert.Greater(torpedo.TotalSeconds, laser.TotalSeconds);
        }

        [Test]
        public void GetDesiredFacing_AfterTheBowFires_KeepsTheStrongestBatteryOnTheTarget()
        {
            CapitalShip ship = CreateShip("ship", 400, 10, range: 150);
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][(int)PrimaryWeaponArc.Port] = 4;
            CapitalShip defender = CreateShip("defender", 100000, 0);
            TacticalSpaceBattle battle = CreateBattle(new[] { ship }, new[] { defender });
            TacticalCapitalShipUnit shipUnit = GetShip(battle, ship);
            TacticalCapitalShipUnit targetUnit = GetShip(battle, defender);

            // One step: the bow has fired and is recharging while the port arc stays full.
            battle.Advance(TacticalSpaceBattle.RoundSeconds / 4);

            Assert.Less(shipUnit.GetArcChargeFraction(PrimaryWeaponArc.Fore), 1);
            Assert.AreEqual(1, shipUnit.GetArcChargeFraction(PrimaryWeaponArc.Port), 0.0001);
            Assert.AreEqual(
                0,
                Math.Abs(
                    TacticalBattleGeometry.NormalizeAngle(
                        shipUnit.GetDesiredFacing(targetUnit) - shipUnit.GetAngleTo(targetUnit)
                    )
                ),
                0.0001
            );
        }

        [Test]
        public void Constructor_CapitalWeaponRange_IsScaledForTheTacticalArena()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("attacker", 100, 10, range: 50) },
                new[] { CreateShip("defender", 100, 0) },
                config: config
            );
            TacticalCapitalShipUnit attacker = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Side == CombatSide.Attacker);

            Assert.AreEqual(
                50 * config.TacticalCapitalWeaponRangeMultiplier,
                attacker.MaximumWeaponRange,
                0.0001
            );
            Assert.Greater(attacker.MaximumWeaponRange, 50);
        }

        [Test]
        public void ApplyDamage_ManyHits_KnockTheShieldGeneratorOfflineAndStopRecharge()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            GameConfig.SpaceCombatConfig safeConfig = CreateConfig();
            safeConfig.TacticalShieldGeneratorHitChance = 0;
            CapitalShip ship = CreateShip("ship", 400, 10);
            ship.MaxShieldStrength = 1000;
            ship.ShieldRechargeRate = 10;
            CapitalShip intact = CreateShip("intact", 400, 10);
            intact.MaxShieldStrength = 1000;
            intact.ShieldRechargeRate = 10;
            TacticalSpaceBattle battle = CreateBattle(
                new[] { ship, intact },
                new[] { CreateShip("defender", 100000, 0) },
                config: config
            );
            TacticalCapitalShipUnit struck = GetShip(battle, ship);
            TacticalCapitalShipUnit control = GetShip(battle, intact);
            SystemRandomProvider random = new SystemRandomProvider(_seed);

            for (int hit = 0; hit < 200; hit++)
            {
                struck.ApplyDamage(1, 0, config, random);
                control.ApplyDamage(1, 0, safeConfig, random);
            }

            struck.AdvanceState(1, config);
            control.AdvanceState(1, safeConfig);

            Assert.IsTrue(struck.ShieldGeneratorOffline);
            Assert.Greater(struck.ShieldGeneratorRepairDays, 0);
            Assert.AreEqual(800, struck.CurrentShields, 0.0001);
            Assert.IsFalse(control.ShieldGeneratorOffline);
            Assert.Greater(control.CurrentShields, 800);
        }

        [Test]
        public void Constructor_DamagedShieldGenerator_EntersBattleOfflineAndRepairsOverTime()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            CapitalShip ship = CreateShip("ship", 400, 10);
            ship.MaxShieldStrength = 100;
            ship.ShieldGeneratorRepairDays = 3;
            TacticalSpaceBattle battle = CreateBattle(
                new[] { ship },
                new[] { CreateShip("defender", 100000, 0) },
                config: config
            );
            TacticalCapitalShipUnit unit = GetShip(battle, ship);
            bool offlineAtStart = unit.ShieldGeneratorOffline;
            int daysAtStart = unit.ShieldGeneratorRepairDays;

            unit.AdvanceState(25, config);

            Assert.IsTrue(offlineAtStart);
            Assert.AreEqual(3, daysAtStart);
            Assert.IsTrue(unit.ShieldGeneratorOffline);
            Assert.AreEqual(2, unit.ShieldGeneratorRepairDays);
        }

        [Test]
        public void CreateResult_DamagedShieldGenerator_CarriesTheRepairOutOfTheBattle()
        {
            CapitalShip ship = CreateShip("ship", 400, 0);
            ship.MaxShieldStrength = 100;
            ship.ShieldGeneratorRepairDays = 2;
            TacticalSpaceBattle battle = CreateBattle(
                new[] { ship },
                new[] { CreateShip("defender", 100, 10, range: 20) },
                attackerCanWithdraw: true
            );

            battle.OrderWithdrawal(CombatSide.Attacker);
            battle.Advance(30);
            SpaceCombatResult result = battle.CreateResult();

            Assert.IsTrue(battle.IsComplete);
            ShipDamageResult damage = result.ShipDamage.Single(entry => entry.Ship == ship);
            Assert.AreEqual(400, damage.HullAfter);
            Assert.IsTrue(damage.ShieldGeneratorRepairDays.HasValue);
            Assert.LessOrEqual(damage.ShieldGeneratorRepairDays.Value, 2);
        }

        [Test]
        public void Constructor_WeaponMix_SetsDefaultTargetPriorities()
        {
            CapitalShip heavy = CreateShip("heavy", 400, 10);
            CapitalShip escort = CreateShip("escort", 200, 2);
            escort.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 30;
            escort.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 40;
            escort.StarfighterCapacity = 1;
            Starfighter fighter = CreateFighter("fighter", 12, 5);
            escort.AddChild(fighter);
            fighter.SetParent(escort);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { heavy, escort },
                new[] { CreateShip("defender", 100, 0) },
                attackerFighters: new[] { fighter }
            );

            Assert.AreEqual(
                TacticalTargetPriority.CapitalShips,
                GetShip(battle, heavy).TargetPriority
            );
            Assert.AreEqual(
                TacticalTargetPriority.Starfighters,
                GetShip(battle, escort).TargetPriority
            );
            Assert.AreEqual(
                TacticalTargetPriority.Starfighters,
                battle.Units.OfType<TacticalStarfighterUnit>().Single().TargetPriority
            );
        }

        [Test]
        public void Advance_TargetPriority_PicksThePreferredClassBeforeNearerTargets()
        {
            CapitalShip escort = CreateShip("escort", 400, 2, range: 150);
            escort.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 30;
            escort.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 150;
            CapitalShip carrier = CreateShip("carrier", 100000, 0);
            carrier.StarfighterCapacity = 1;
            Starfighter fighter = CreateFighter("fighter", 12, 5);
            carrier.AddChild(fighter);
            fighter.SetParent(carrier);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { escort },
                new[] { carrier },
                defenderFighters: new[] { fighter }
            );
            TacticalCapitalShipUnit escortUnit = GetShip(battle, escort);
            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single();

            battle.Advance(TacticalSpaceBattle.RoundSeconds);
            TacticalBattleUnit screening = escortUnit.Target;
            battle.SetTargetPriority(new[] { escortUnit }, TacticalTargetPriority.CapitalShips);
            battle.Advance(TacticalSpaceBattle.RoundSeconds);

            Assert.AreSame(squadron, screening);
            Assert.AreSame(GetShip(battle, carrier), escortUnit.Target);
        }

        [Test]
        public void Advance_AutomaticTarget_TakesTheNearestPreferredUnitAndSticksToIt()
        {
            CapitalShip attacker = CreateShip("attacker", 400, 10, range: 150);
            CapitalShip near = CreateShip("near", 100000, 0);
            CapitalShip far = CreateShip("far", 100000, 0);
            TacticalSpaceBattle battle = CreateBattle(new[] { attacker }, new[] { near, far });
            TacticalCapitalShipUnit attackerUnit = GetShip(battle, attacker);
            TacticalCapitalShipUnit nearUnit = GetShip(battle, near);
            TacticalCapitalShipUnit farUnit = GetShip(battle, far);
            nearUnit.Y = attackerUnit.Y + 10;
            farUnit.Y = attackerUnit.Y - 30;

            battle.Advance(TacticalSpaceBattle.RoundSeconds);
            TacticalBattleUnit first = attackerUnit.Target;
            // The other ship closes in; a sticky target ignores it until the current one is gone.
            farUnit.X = attackerUnit.X + 5;
            farUnit.Y = attackerUnit.Y;
            battle.Advance(60);

            Assert.AreSame(nearUnit, first);
            Assert.AreSame(nearUnit, attackerUnit.Target);
        }

        [Test]
        public void SetTargetPriority_EnemyUnit_IsIgnored()
        {
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("attacker", 100, 10) },
                new[] { CreateShip("defender", 100, 10) }
            );
            TacticalCapitalShipUnit defender = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Side == CombatSide.Defender);
            TacticalTargetPriority initial = defender.TargetPriority;

            battle.SetTargetPriority(
                new[] { defender },
                initial == TacticalTargetPriority.Starfighters
                    ? TacticalTargetPriority.CapitalShips
                    : TacticalTargetPriority.Starfighters
            );

            Assert.IsFalse(battle.CanSetTargetPriority(defender));
            Assert.AreEqual(initial, defender.TargetPriority);
        }

        [Test]
        public void Advance_ArmedAttackerAgainstPassiveDefender_DestroysDefender()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10);
            CapitalShip defender = CreateShip("defender", 50, 0);
            TacticalSpaceBattle battle = CreateBattle(new[] { attacker }, new[] { defender });

            battle.Advance(20);

            Assert.IsTrue(battle.IsComplete);
            SpaceCombatResult result = battle.CreateResult();
            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            ShipDamageResult damage = result.ShipDamage.Single(entry => entry.Ship == defender);
            Assert.AreEqual(50, damage.HullBefore);
            Assert.AreEqual(0, damage.HullAfter);
            Assert.IsTrue(
                result
                    .DefendingUnits.Single(unit => unit.Unit.GetInstanceID() == "defender")
                    .Destroyed
            );
            Assert.IsEmpty(result.WithdrawnUnits);
        }

        [Test]
        public void Advance_EquivalentBattles_ReturnsDeterministicResults()
        {
            SpaceCombatResult first = RunSymmetricBattle();
            SpaceCombatResult second = RunSymmetricBattle();

            Assert.AreEqual(first.AttackerOutcome, second.AttackerOutcome);
            Assert.AreEqual(first.DefenderOutcome, second.DefenderOutcome);
            CollectionAssert.AreEqual(
                first.ShipDamage.Select(damage => damage.HullAfter),
                second.ShipDamage.Select(damage => damage.HullAfter)
            );
        }

        [Test]
        public void Advance_UnarmedForces_ResolvesStalemateAfterOneRound()
        {
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("attacker", 100, 0) },
                new[] { CreateShip("defender", 100, 0) }
            );

            battle.Advance(TacticalSpaceBattle.RoundSeconds);

            Assert.IsTrue(battle.IsComplete);
            Assert.AreEqual(CombatSide.Draw, battle.CreateResult().Winner);
        }

        [Test]
        public void Advance_AutomaticSideBelowRetreatRatio_WithdrawsInsteadOfFighting()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 0.33;
            CapitalShip attacker = CreateShip("attacker", 100, 100, range: 20);
            CapitalShip defender = CreateShip("defender", 100, 1);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { attacker },
                new[] { defender },
                config: config,
                defenderCanWithdraw: true
            );

            battle.Advance(30);

            Assert.IsTrue(battle.IsComplete);
            SpaceCombatResult result = battle.CreateResult();
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            CollectionAssert.AreEqual(new[] { defender }, result.WithdrawnUnits);
            Assert.AreEqual(100, defender.CurrentHullStrength);
        }

        [Test]
        public void OrderWithdrawal_PlayerSideCanWithdraw_WithdrawsAllShipsAndEndsBattle()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 0);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { attacker },
                new[] { CreateShip("defender", 100, 10, range: 20) },
                attackerCanWithdraw: true
            );

            bool ordered = battle.OrderWithdrawal(CombatSide.Attacker);
            battle.Advance(30);

            Assert.IsTrue(ordered);
            Assert.IsTrue(battle.HasOrderedWithdrawal(CombatSide.Attacker));
            Assert.IsFalse(battle.OrderWithdrawal(CombatSide.Attacker));
            Assert.IsTrue(battle.IsComplete);
            SpaceCombatResult result = battle.CreateResult();
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
            Assert.AreEqual(CombatSide.Defender, result.Winner);
            CollectionAssert.AreEqual(new[] { attacker }, result.WithdrawnUnits);
            Assert.IsEmpty(result.ShipDamage);
        }

        [Test]
        public void OrderWithdrawal_SideCannotWithdraw_ReturnsFalse()
        {
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("attacker", 100, 10) },
                new[] { CreateShip("defender", 100, 10) }
            );

            Assert.IsFalse(battle.OrderWithdrawal(CombatSide.Attacker));
            Assert.IsFalse(battle.HasOrderedWithdrawal(CombatSide.Attacker));
        }

        [Test]
        public void IssueMoveOrder_PlayerShip_MovesTowardDestination()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10, range: 20);
            attacker.SublightSpeed = 3;
            TacticalSpaceBattle battle = CreateBattle(
                new[] { attacker },
                new[] { CreateShip("defender", 1000, 0) }
            );
            TacticalCapitalShipUnit ship = GetShip(battle, attacker);
            double startX = ship.X;
            double startY = ship.Y;

            battle.IssueMoveOrder(new[] { ship }, startX, startY + 20);
            battle.Advance(2);

            Assert.AreEqual(TacticalShipOrder.Move, ship.Order);
            Assert.IsTrue(ship.HasDestination);
            Assert.AreEqual(startY + 20, ship.DestinationY, 0.0001);
            Assert.Greater(ship.Y, startY + 10);
            Assert.AreEqual(startX, ship.X, 0.0001);
        }

        [Test]
        public void IssueAttackOrder_EnemyTarget_PursuesTarget()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10, range: 20);
            attacker.SublightSpeed = 3;
            CapitalShip defender = CreateShip("defender", 1000, 0);
            TacticalSpaceBattle battle = CreateBattle(new[] { attacker }, new[] { defender });
            TacticalCapitalShipUnit ship = GetShip(battle, attacker);
            TacticalCapitalShipUnit target = GetShip(battle, defender);
            double startDistance = ship.GetDistanceTo(target);

            battle.IssueAttackOrder(new[] { ship }, target);
            battle.Advance(2);

            Assert.AreEqual(TacticalShipOrder.Attack, ship.Order);
            Assert.AreSame(target, ship.Target);
            Assert.Less(ship.GetDistanceTo(target), startDistance - 5);
        }

        [Test]
        public void IssueMoveOrder_AutomaticSide_IgnoresOrder()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { attacker },
                new[] { CreateShip("defender", 100, 10) }
            );
            TacticalCapitalShipUnit ship = GetShip(battle, attacker);

            battle.SetAutomatic(CombatSide.Attacker, true);
            battle.IssueMoveOrder(new[] { ship }, 0, 0);

            Assert.IsTrue(battle.IsAutomatic(CombatSide.Attacker));
            Assert.IsFalse(battle.CanCommand(ship));
            Assert.AreEqual(TacticalShipOrder.None, ship.Order);
        }

        [Test]
        public void SetAutomatic_NonPlayerSide_ThrowsInvalidOperationException()
        {
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("attacker", 100, 10) },
                new[] { CreateShip("defender", 100, 10) }
            );

            Assert.Throws<InvalidOperationException>(() =>
                battle.SetAutomatic(CombatSide.Defender, true)
            );
        }

        [Test]
        public void CreateResult_BeforeCompletion_ThrowsInvalidOperationException()
        {
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("attacker", 100, 10) },
                new[] { CreateShip("defender", 100, 10) }
            );

            Assert.Throws<InvalidOperationException>(() => battle.CreateResult());
        }

        [Test]
        public void FindUnitAt_NearAndFarPoints_ReturnsOnlyNearbyUnit()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { attacker },
                new[] { CreateShip("defender", 100, 10) }
            );
            TacticalCapitalShipUnit ship = GetShip(battle, attacker);

            Assert.AreSame(ship, battle.FindUnitAt(ship.X + 1, ship.Y + 1, 1));
            Assert.IsNull(battle.FindUnitAt(0, 0, 1));
        }

        [Test]
        public void TakeEvents_WeaponFire_ReportsFiringShipAndHitTarget()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10, range: 150);
            CapitalShip defender = CreateShip("defender", 1000, 0);
            TacticalSpaceBattle battle = CreateBattle(new[] { attacker }, new[] { defender });

            battle.Advance(TacticalSpaceBattle.RoundSeconds / 4);
            IReadOnlyList<TacticalBattleEvent> events = battle.TakeEvents();

            Assert.AreEqual(
                1,
                events.Count(battleEvent =>
                    battleEvent.Kind == TacticalBattleEventKind.WeaponFired
                    && battleEvent.Unit == GetShip(battle, attacker)
                )
            );
            Assert.AreEqual(
                1,
                events.Count(battleEvent =>
                    battleEvent.Kind == TacticalBattleEventKind.UnitHit
                    && battleEvent.Unit == GetShip(battle, defender)
                )
            );
            Assert.AreEqual(CombatSide.Attacker, battle.Shots.Single().Side);
            Assert.IsEmpty(battle.TakeEvents());
        }

        [Test]
        public void TakeEvents_DefenderDestroyed_ReportsDestructionOnce()
        {
            CapitalShip defender = CreateShip("defender", 50, 0);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("attacker", 100, 10) },
                new[] { defender }
            );

            battle.Advance(30);
            IReadOnlyList<TacticalBattleEvent> events = battle.TakeEvents();

            Assert.AreEqual(
                1,
                events.Count(battleEvent =>
                    battleEvent.Kind == TacticalBattleEventKind.UnitDestroyed
                )
            );
            Assert.AreSame(
                GetShip(battle, defender),
                events
                    .Single(battleEvent =>
                        battleEvent.Kind == TacticalBattleEventKind.UnitDestroyed
                    )
                    .Unit
            );
            Assert.IsFalse(
                events.Any(battleEvent => battleEvent.Kind == TacticalBattleEventKind.UnitWithdrew)
            );
        }

        [Test]
        public void TakeEvents_PlayerWithdraws_ReportsWithdrawnShip()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 0);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { attacker },
                new[] { CreateShip("defender", 100, 10, range: 20) },
                attackerCanWithdraw: true
            );

            battle.OrderWithdrawal(CombatSide.Attacker);
            battle.Advance(30);
            IReadOnlyList<TacticalBattleEvent> events = battle.TakeEvents();

            TacticalBattleEvent withdrew = events.Single(battleEvent =>
                battleEvent.Kind == TacticalBattleEventKind.UnitWithdrew
            );
            Assert.AreSame(GetShip(battle, attacker), withdrew.Unit);
            Assert.IsFalse(
                events.Any(battleEvent => battleEvent.Kind == TacticalBattleEventKind.UnitDestroyed)
            );
        }

        [Test]
        public void Advance_WeaponFire_RecordsShotsThatFade()
        {
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("attacker", 100, 10, range: 150) },
                new[] { CreateShip("defender", 1000, 0) }
            );

            battle.Advance(TacticalSpaceBattle.RoundSeconds / 4);
            int shotsAfterFiring = battle.Shots.Count;
            battle.Advance(5);

            Assert.AreEqual(1, shotsAfterFiring);
            Assert.IsFalse(battle.Shots.Any(shot => shot.RemainingSeconds <= 0));
        }

        /// <summary>
        /// Fires a charged turbolaser battery once at an enemy squadron inside its forward arc.
        /// </summary>
        /// <param name="hitChance">The turbolaser hit chance against fighters.</param>
        /// <param name="shot">Receives the recorded shot.</param>
        /// <param name="squadron">Receives the squadron fired at.</param>
        /// <returns>The damage queued against the squadron.</returns>
        private static double FireTurbolaserAtSquadron(
            double hitChance,
            out TacticalShot shot,
            out TacticalStarfighterUnit squadron
        )
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.TacticalTurbolaserFighterHitChance = hitChance;
            CapitalShip carrier = CreateShip("carrier", 500, 0);
            carrier.StarfighterCapacity = 2;
            Starfighter fighter = CreateFighter("fighter", 12, 5);
            carrier.AddChild(fighter);
            fighter.SetParent(carrier);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("battery", 400, 40, range: 150) },
                new[] { carrier },
                defenderFighters: new[] { fighter },
                config: config
            );
            TacticalCapitalShipUnit battery = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Side == CombatSide.Attacker);
            squadron = battle.Units.OfType<TacticalStarfighterUnit>().Single();
            List<TacticalShot> shots = new List<TacticalShot>();
            double damage = 0;

            battery.QueueAttacks(
                new[] { squadron },
                (target, amount, isIon) => damage += amount,
                shots,
                1,
                new SystemRandomProvider(_seed)
            );

            Assert.AreEqual(1, shots.Count);
            shot = shots[0];
            return damage;
        }

        /// <summary>
        /// Runs one mirrored battle to completion from a fixed seed.
        /// </summary>
        /// <returns>The resolved battle result.</returns>
        private static SpaceCombatResult RunSymmetricBattle()
        {
            CapitalShip attacker = CreateShip("attacker", 300, 20);
            attacker.MaxShieldStrength = 50;
            attacker.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new[] { 10, 0, 0, 0, 100 };
            CapitalShip defender = CreateShip("defender", 250, 25);
            defender.MaxShieldStrength = 40;
            defender.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new[] { 10, 0, 0, 0, 100 };
            TacticalSpaceBattle battle = CreateBattle(new[] { attacker }, new[] { defender });

            battle.Advance(600);

            Assert.IsTrue(battle.IsComplete);
            return battle.CreateResult();
        }

        /// <summary>
        /// Creates a battle with the attacker under player command.
        /// </summary>
        /// <param name="attackerShips">The attacking capital ships.</param>
        /// <param name="defenderShips">The defending capital ships.</param>
        /// <param name="attackerFighters">The attacking fighter squadrons.</param>
        /// <param name="defenderFighters">The defending fighter squadrons.</param>
        /// <param name="attackerCanWithdraw">Whether the attacker can withdraw.</param>
        /// <param name="defenderCanWithdraw">Whether the defender can withdraw.</param>
        /// <param name="config">The space-combat parameters.</param>
        /// <returns>The created battle.</returns>
        private static TacticalSpaceBattle CreateBattle(
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> attackerFighters = null,
            IReadOnlyList<Starfighter> defenderFighters = null,
            bool attackerCanWithdraw = false,
            bool defenderCanWithdraw = false,
            GameConfig.SpaceCombatConfig config = null
        )
        {
            return new TacticalSpaceBattle(
                config ?? CreateConfig(),
                new SystemRandomProvider(_seed),
                attackerShips,
                attackerFighters ?? new List<Starfighter>(),
                defenderShips,
                defenderFighters ?? new List<Starfighter>(),
                attackerCanWithdraw,
                defenderCanWithdraw,
                default,
                default,
                CombatSide.Attacker
            );
        }

        /// <summary>
        /// Creates combat parameters matching the shipped defaults without automatic retreat.
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
        /// <param name="range">The turbolaser range.</param>
        /// <returns>The created ship.</returns>
        private static CapitalShip CreateShip(
            string instanceId,
            int hull,
            int weaponStrength,
            int range = 100
        )
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
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = range;
            return ship;
        }

        /// <summary>
        /// Creates a fighter squadron with a laser cannon.
        /// </summary>
        /// <param name="instanceId">The instance identifier.</param>
        /// <param name="squadronSize">The squadron size.</param>
        /// <param name="weaponStrength">The laser cannon strength.</param>
        /// <returns>The created squadron.</returns>
        private static Starfighter CreateFighter(
            string instanceId,
            int squadronSize,
            int weaponStrength
        )
        {
            return new Starfighter
            {
                InstanceID = instanceId,
                DisplayName = instanceId,
                MaxSquadronSize = squadronSize,
                CurrentSquadronSize = squadronSize,
                ShieldStrength = 1,
                Hyperdrive = 0,
                SublightSpeed = 8,
                Agility = 4,
                LaserCannon = weaponStrength,
                LaserRange = weaponStrength > 0 ? 12 : 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        /// <summary>
        /// Returns the tactical unit wrapping a capital ship.
        /// </summary>
        /// <param name="battle">The battle to search.</param>
        /// <param name="ship">The capital ship.</param>
        /// <returns>The matching tactical unit.</returns>
        private static TacticalCapitalShipUnit GetShip(TacticalSpaceBattle battle, CapitalShip ship)
        {
            return battle.Units.OfType<TacticalCapitalShipUnit>().Single(unit => unit.Ship == ship);
        }
    }
}
