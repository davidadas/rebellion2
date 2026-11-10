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

            // Generator repair runs several times slower than other component recovery.
            unit.AdvanceState(25, config);
            int daysAfterComponentDelay = unit.ShieldGeneratorRepairDays;
            unit.AdvanceState(25 * (config.TacticalShieldGeneratorRepairMultiplier - 1), config);

            Assert.IsTrue(offlineAtStart);
            Assert.AreEqual(3, daysAtStart);
            Assert.AreEqual(3, daysAfterComponentDelay);
            Assert.IsTrue(unit.ShieldGeneratorOffline);
            Assert.AreEqual(2, unit.ShieldGeneratorRepairDays);
        }

        [Test]
        public void AdvanceState_ShieldRecharge_RunsAtAFractionOfTheStrategicRate()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.TacticalShieldGeneratorHitChance = 0;
            CapitalShip ship = CreateShip("ship", 400, 10);
            ship.MaxShieldStrength = 1000;
            ship.ShieldRechargeRate = 10;
            TacticalSpaceBattle battle = CreateBattle(
                new[] { ship },
                new[] { CreateShip("defender", 100000, 0) },
                config: config
            );
            TacticalCapitalShipUnit unit = GetShip(battle, ship);
            unit.ApplyDamage(500, 0, config, new SystemRandomProvider(_seed));

            unit.AdvanceState(10, config);

            Assert.Less(config.TacticalShieldRechargeMultiplier, 1);
            Assert.AreEqual(
                500 + 100 * config.TacticalShieldRechargeMultiplier,
                unit.CurrentShields,
                0.0001
            );
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
        public void Advance_ShieldsRegenerateEveryVolley_EndsAsAStalemateInsteadOfRunningOn()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 40;
            config.TacticalShieldGeneratorHitChance = 0;
            CapitalShip attacker = CreateShip("attacker", 100, 5);
            CapitalShip defender = CreateShip("defender", 100, 10);
            foreach (CapitalShip ship in new[] { attacker, defender })
            {
                ship.MaxShieldStrength = 1000;
                ship.ShieldRechargeRate = 100;
            }

            TacticalSpaceBattle battle = CreateBattle(
                new[] { attacker },
                new[] { defender },
                attackerCanWithdraw: true,
                config: config
            );

            // Both ships hit shields every round and the shields heal straight back: durability
            // keeps moving, but nothing lasting is lost, so the weaker side breaks off.
            battle.Advance(120);

            Assert.IsTrue(battle.IsComplete);
            // 240 rounds were on offer; the stalemate is called soon after the stagnation window.
            Assert.Less(battle.RoundsCompleted, 120);
            SpaceCombatResult result = battle.CreateResult();
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
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
            Assert.AreEqual(
                TacticalWithdrawalStatus.NoEscapeRoute,
                battle.GetWithdrawalStatus(CombatSide.Attacker)
            );
        }

        [Test]
        public void GetWithdrawalStatus_NoHyperdriveLeft_ReportsNoHyperdrive()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10);
            attacker.Hyperdrive = 0;
            TacticalSpaceBattle battle = CreateBattle(
                new[] { attacker },
                new[] { CreateShip("defender", 100, 10) },
                attackerCanWithdraw: true
            );

            Assert.AreEqual(
                TacticalWithdrawalStatus.NoHyperdrive,
                battle.GetWithdrawalStatus(CombatSide.Attacker)
            );
            Assert.IsFalse(battle.CanWithdraw(CombatSide.Attacker));
            Assert.IsFalse(battle.OrderWithdrawal(CombatSide.Attacker));
        }

        [Test]
        public void GetWithdrawalStatus_EnemyGravityWell_HoldsTheSideUntilTheInterdictorIsGone()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            CapitalShip interdictorShip = CreateShip("interdictor", 100, 0);
            interdictorShip.HasGravityWell = true;
            TacticalSpaceBattle battle = CreateBattle(
                new[] { CreateShip("attacker", 100, 0) },
                new[] { interdictorShip, CreateShip("escort", 100, 0) },
                attackerCanWithdraw: true,
                defenderCanWithdraw: true,
                config: config
            );
            TacticalCapitalShipUnit interdictor = GetShip(battle, interdictorShip);

            TacticalWithdrawalStatus statusWhileHeld = battle.GetWithdrawalStatus(
                CombatSide.Attacker
            );
            bool orderedWhileHeld = battle.OrderWithdrawal(CombatSide.Attacker);
            interdictor.ApplyDamage(1000, 0, config, new SystemRandomProvider(_seed));
            TacticalWithdrawalStatus statusAfterKill = battle.GetWithdrawalStatus(
                CombatSide.Attacker
            );
            bool orderedAfterKill = battle.OrderWithdrawal(CombatSide.Attacker);

            Assert.AreEqual(TacticalWithdrawalStatus.Interdicted, statusWhileHeld);
            Assert.IsFalse(orderedWhileHeld);
            Assert.AreEqual(TacticalWithdrawalStatus.Available, statusAfterKill);
            Assert.IsTrue(orderedAfterKill);
            Assert.IsTrue(battle.HasOrderedWithdrawal(CombatSide.Attacker));
            // The defender's own gravity well never holds the defender.
            Assert.AreEqual(
                TacticalWithdrawalStatus.Available,
                battle.GetWithdrawalStatus(CombatSide.Defender)
            );
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
        public void IssueAttackOrder_PlayerSquadron_HoldsTheTargetUntilItIsGone()
        {
            CapitalShip carrier = CreateShip("carrier", 500, 0);
            carrier.StarfighterCapacity = 2;
            Starfighter fighter = CreateFighter("fighter", 12, 5);
            carrier.AddChild(fighter);
            fighter.SetParent(carrier);
            CapitalShip defender = CreateShip("defender", 400, 0);
            Starfighter enemyFighter = CreateFighter("enemy", 12, 5);
            TacticalSpaceBattle battle = CreateBattle(
                new[] { carrier },
                new[] { defender },
                attackerFighters: new[] { fighter },
                defenderFighters: new[] { enemyFighter }
            );
            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Fighter == fighter);
            TacticalStarfighterUnit enemySquadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Fighter == enemyFighter);
            TacticalCapitalShipUnit target = GetShip(battle, defender);

            battle.Advance(1);
            TacticalBattleUnit automaticTarget = squadron.Target;
            battle.IssueAttackOrder(new[] { squadron }, target);
            battle.Advance(20);
            TacticalBattleUnit heldTarget = squadron.Target;
            bool directedWhileHeld = squadron.IsDirected;
            target.Destroy();
            battle.Advance(1);

            Assert.IsTrue(battle.CanCommand(squadron));
            Assert.AreSame(enemySquadron, automaticTarget);
            Assert.AreSame(target, heldTarget);
            Assert.IsTrue(directedWhileHeld);
            Assert.IsFalse(squadron.IsDirected);
            Assert.AreSame(enemySquadron, squadron.Target);
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

        [Test]
        public void Constructor_PlanetDestroyer_StandsOffBeyondTheArenaInsteadOfJoiningTheLine()
        {
            CapitalShip station = CreateStation("station");
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { CreateShip("attacker", 100, 0) },
                new[] { station, CreateShip("escort", 100, 0) },
                playerSide: CombatSide.Defender
            );

            TacticalBattleStation unit = battle.Stations.Single();
            Assert.AreSame(station, unit.Ship);
            Assert.AreEqual(CombatSide.Defender, unit.Side);
            Assert.IsFalse(
                battle.Units.OfType<TacticalCapitalShipUnit>().Any(ship => ship.Ship == station)
            );
            Assert.Greater(unit.X - unit.Radius, battle.HalfWidth);
            Assert.AreEqual(1, unit.SuperlaserCharge);
            Assert.IsTrue(unit.IsSuperlaserReady);
            Assert.AreSame(unit, battle.GetStation(CombatSide.Defender));
            Assert.IsNull(battle.GetStation(CombatSide.Attacker));
            Assert.AreEqual(1, battle.CountShips(CombatSide.Defender));
        }

        [Test]
        public void FireSuperlaser_ChargedStation_DestroysTheShipAndRecharges()
        {
            CapitalShip victim = CreateShip("victim", 100000, 0);
            CapitalShip other = CreateShip("other", 100000, 0);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.TacticalSuperlaserRechargeSeconds = 20;
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { CreateStation("station"), CreateShip("escort", 100, 0) },
                new[] { victim, other },
                playerSide: CombatSide.Attacker,
                config: config
            );
            TacticalBattleStation station = battle.Stations.Single();
            TacticalCapitalShipUnit victimUnit = GetShip(battle, victim);

            bool fired = battle.FireSuperlaser(station, victimUnit);
            bool firedAgain = battle.FireSuperlaser(station, GetShip(battle, other));
            TacticalBattleEvent shot = battle
                .TakeEvents()
                .Single(battleEvent => battleEvent.Kind == TacticalBattleEventKind.SuperlaserFired);
            bool aliveWhileTheBeamBurns = victimUnit.IsAlive;
            battle.Advance(2);
            bool destroyed = !victimUnit.IsAlive;
            double chargeAfterTheShot = station.SuperlaserCharge;
            battle.Advance(22);

            Assert.IsTrue(fired);
            Assert.IsFalse(firedAgain);
            Assert.AreSame(station, shot.Station);
            Assert.AreSame(victimUnit, shot.Unit);
            Assert.IsTrue(aliveWhileTheBeamBurns);
            Assert.IsTrue(destroyed);
            Assert.Less(chargeAfterTheShot, 0.2);
            Assert.IsTrue(station.IsSuperlaserReady);
            Assert.IsTrue(GetShip(battle, other).IsAlive);
            Assert.IsFalse(battle.IsComplete);
        }

        [Test]
        public void FireSuperlaser_SquadronOrFriendlyShip_IsRefused()
        {
            CapitalShip escort = CreateShip("escort", 100, 0);
            Starfighter enemySquadron = CreateFighter("enemy-squadron", 12, 5);
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { CreateStation("station"), escort },
                new[] { CreateShip("defender", 100000, 0) },
                defenderFighters: new[] { enemySquadron },
                playerSide: CombatSide.Attacker
            );
            TacticalBattleStation station = battle.Stations.Single();
            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single();

            Assert.IsFalse(battle.FireSuperlaser(station, squadron));
            Assert.IsFalse(battle.FireSuperlaser(station, GetShip(battle, escort)));
            Assert.IsFalse(battle.FireSuperlaser(station, null));
            Assert.IsTrue(station.IsSuperlaserReady);
        }

        [Test]
        public void Advance_AutomaticStation_BurnsTheToughestEnemyShipAfterTheOpening()
        {
            CapitalShip small = CreateShip("small", 100, 0);
            CapitalShip big = CreateShip("big", 5000, 0);
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { CreateStation("station"), CreateShip("escort", 100, 0) },
                new[] { small, big },
                playerSide: CombatSide.Defender
            );

            battle.Advance(4);
            bool heldFire = GetShip(battle, big).IsAlive && battle.Stations[0].IsSuperlaserReady;
            battle.Advance(4);

            Assert.IsTrue(heldFire);
            Assert.IsFalse(GetShip(battle, big).IsAlive);
            Assert.IsTrue(GetShip(battle, small).IsAlive);
            Assert.Less(battle.Stations[0].SuperlaserCharge, 1);
        }

        [Test]
        public void IssueTrenchRunOrder_TorpedoSquadron_DestroysTheStationAndComesBack()
        {
            CapitalShip station = CreateStation("station");
            Starfighter fighter = CreateTorpedoFighter("x-wing", 12);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.TacticalTrenchRunBaseChance = 1;
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { CreateShip("attacker", 100, 0) },
                new[] { station },
                attackerFighters: new[] { fighter },
                playerSide: CombatSide.Attacker,
                config: config
            );
            TacticalBattleStation stationUnit = battle.Stations.Single();
            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single();

            bool ordered = battle.IssueTrenchRunOrder(new[] { squadron }, stationUnit);
            bool onItsWay = squadron.TrenchRunTarget == stationUnit;
            battle.Advance(15);
            List<TacticalBattleEventKind> kinds = battle
                .TakeEvents()
                .Select(battleEvent => battleEvent.Kind)
                .ToList();

            Assert.IsTrue(ordered);
            Assert.IsTrue(onItsWay);
            Assert.IsFalse(stationUnit.IsAlive);
            Assert.IsTrue(squadron.IsAlive);
            Assert.IsFalse(squadron.IsOffField);
            Assert.IsNull(squadron.TrenchRunTarget);
            CollectionAssert.Contains(kinds, TacticalBattleEventKind.TrenchRunSucceeded);
            CollectionAssert.Contains(kinds, TacticalBattleEventKind.StationDestroyed);
            Assert.IsTrue(battle.IsComplete);
            SpaceCombatResult result = battle.CreateResult();
            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            ShipDamageResult damage = result.ShipDamage.Single(entry => entry.Ship == station);
            Assert.AreEqual(0, damage.HullAfter);
        }

        [Test]
        public void IssueTrenchRunOrder_SquadronWithoutTorpedoes_FailsAndIsLost()
        {
            Starfighter fighter = CreateFighter("a-wing", 12, 5);
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { CreateShip("attacker", 100, 0) },
                new[] { CreateStation("station"), CreateShip("escort", 100000, 0) },
                attackerFighters: new[] { fighter },
                playerSide: CombatSide.Attacker
            );
            TacticalBattleStation station = battle.Stations.Single();
            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single();

            double chance = battle.EstimateTrenchRunChance(new[] { squadron }, station);
            battle.IssueTrenchRunOrder(new[] { squadron }, station);
            battle.Advance(15);

            Assert.AreEqual(0, chance);
            Assert.IsFalse(squadron.IsAlive);
            Assert.IsTrue(station.IsAlive);
            CollectionAssert.Contains(
                battle.TakeEvents().Select(battleEvent => battleEvent.Kind).ToList(),
                TacticalBattleEventKind.TrenchRunFailed
            );
        }

        [Test]
        public void IssueTrenchRunOrder_ShieldedStation_IsRefused()
        {
            CapitalShip station = CreateStation("station");
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { CreateShip("attacker", 100, 0) },
                new[] { station },
                attackerFighters: new[] { CreateTorpedoFighter("x-wing", 12) },
                playerSide: CombatSide.Attacker,
                shieldedStations: new[] { station }
            );
            TacticalBattleStation stationUnit = battle.Stations.Single();
            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single();

            bool ordered = battle.IssueTrenchRunOrder(new[] { squadron }, stationUnit);

            Assert.IsTrue(stationUnit.IsShielded);
            Assert.IsFalse(battle.CanOrderTrenchRun(stationUnit));
            Assert.IsFalse(ordered);
            Assert.IsNull(squadron.TrenchRunTarget);
            Assert.AreEqual(0, battle.EstimateTrenchRunChance(new[] { squadron }, stationUnit));
        }

        [TestCase(0, 0.9)]
        [TestCase(1, 0.9)]
        [TestCase(2, 0.45)]
        [TestCase(6, 0.25)]
        public void EstimateTrenchRunChance_DefendingFighters_LowerTheOddsOnlyWhenTheyOutnumberTheRun(
            int defendingSquadrons,
            double expected
        )
        {
            List<Starfighter> defenders = Enumerable
                .Range(0, defendingSquadrons)
                .Select(index => CreateFighter($"tie-{index}", 12, 5))
                .ToList();
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { CreateShip("attacker", 100, 0) },
                new[] { CreateStation("station") },
                attackerFighters: new[] { CreateTorpedoFighter("x-wing", 12) },
                defenderFighters: defenders,
                playerSide: CombatSide.Attacker
            );
            TacticalStarfighterUnit squadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Side == CombatSide.Attacker);

            double chance = battle.EstimateTrenchRunChance(new[] { squadron }, battle.Stations[0]);

            Assert.AreEqual(expected, chance, 0.0001);
        }

        [Test]
        public void Advance_AutomaticAttackers_SendOnlyTheirTorpedoSquadronsAtTheStation()
        {
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);
            Starfighter escortFighter = CreateFighter("a-wing", 12, 5);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.TacticalTrenchRunBaseChance = 1;
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { CreateShip("attacker", 100, 0) },
                new[] { CreateStation("station"), CreateShip("escort", 100000, 0) },
                attackerFighters: new[] { torpedoFighter, escortFighter },
                playerSide: CombatSide.Defender,
                config: config
            );
            TacticalBattleStation station = battle.Stations.Single();
            TacticalStarfighterUnit torpedoSquadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Fighter == torpedoFighter);
            TacticalStarfighterUnit escortSquadron = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Fighter == escortFighter);

            battle.Advance(1);
            bool torpedoesSent = torpedoSquadron.TrenchRunTarget == station;
            bool escortSent = escortSquadron.TrenchRunTarget != null;
            battle.Advance(15);

            Assert.IsTrue(torpedoesSent);
            Assert.IsFalse(escortSent);
            Assert.IsFalse(station.IsAlive);
            Assert.IsTrue(torpedoSquadron.IsAlive);
        }

        [Test]
        public void Advance_OutgunnedAutomaticAttackers_FlyTheTrenchRunBeforeTheyThinkOfLeaving()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 0.33;
            Starfighter torpedoFighter = CreateTorpedoFighter("x-wing", 12);
            TacticalSpaceBattle outgunned = CreateStationBattle(
                new[] { CreateShip("attacker", 100, 1) },
                new[] { CreateShip("defender", 100000, 500) },
                playerSide: CombatSide.Defender,
                config: config,
                canWithdraw: true
            );
            TacticalSpaceBattle committed = CreateStationBattle(
                new[] { CreateShip("attacker", 100, 1) },
                new[] { CreateStation("station"), CreateShip("defender", 100000, 500) },
                attackerFighters: new[] { torpedoFighter },
                playerSide: CombatSide.Defender,
                config: config,
                canWithdraw: true
            );

            outgunned.Advance(0.5);
            committed.Advance(0.5);

            Assert.IsTrue(outgunned.HasOrderedWithdrawal(CombatSide.Attacker));
            Assert.IsFalse(committed.HasOrderedWithdrawal(CombatSide.Attacker));
            Assert.AreSame(
                committed.Stations[0],
                committed.Units.OfType<TacticalStarfighterUnit>().Single().TrenchRunTarget
            );
        }

        [Test]
        public void OrderWithdrawal_SideWithAStation_TheStationJumpsOutWithTheFleet()
        {
            CapitalShip station = CreateStation("station");
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { station, CreateShip("escort", 100, 0) },
                new[] { CreateShip("defender", 100, 0) },
                playerSide: CombatSide.Attacker,
                canWithdraw: true
            );
            TacticalBattleStation stationUnit = battle.Stations.Single();

            bool ordered = battle.OrderWithdrawal(CombatSide.Attacker);
            bool spoolingUp = stationUnit.IsWithdrawing && !stationUnit.HasWithdrawn;
            bool lockedOut = battle.FireSuperlaser(
                stationUnit,
                battle.Units.First(unit => unit.Side == CombatSide.Defender)
            );
            battle.Advance(120);

            Assert.IsTrue(ordered);
            Assert.IsTrue(spoolingUp);
            Assert.IsFalse(lockedOut);
            Assert.IsTrue(battle.IsComplete);
            Assert.IsTrue(stationUnit.HasWithdrawn);
            SpaceCombatResult result = battle.CreateResult();
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            CollectionAssert.Contains(result.WithdrawnUnits, station);
            Assert.IsFalse(result.ShipDamage.Any(entry => entry.Ship == station));
        }

        [Test]
        public void Advance_StationOutlastsItsFleet_TheBattleGoesOn()
        {
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { CreateShip("attacker", 1000, 50) },
                new[] { CreateStation("station"), CreateShip("escort", 10, 0) },
                playerSide: CombatSide.Defender
            );

            battle.Advance(30);

            Assert.AreEqual(0, battle.CountShips(CombatSide.Defender));
            Assert.IsFalse(battle.IsComplete);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, battle.GetOutcome(CombatSide.Defender));
        }

        [Test]
        public void Advance_AutomaticSideFacingOnlyAStationItCannotAttack_Withdraws()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.TacticalSuperlaserRechargeSeconds = 100000;
            TacticalSpaceBattle battle = CreateStationBattle(
                new[] { CreateShip("first", 1000, 50), CreateShip("second", 1000, 50) },
                new[] { CreateStation("station"), CreateShip("escort", 10, 0) },
                playerSide: CombatSide.Defender,
                config: config,
                canWithdraw: true
            );

            battle.Advance(120);

            Assert.IsTrue(battle.IsComplete);
            SpaceCombatResult result = battle.CreateResult();
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(CombatSide.Defender, result.Winner);
            Assert.IsTrue(battle.Stations[0].IsAlive);
        }

        /// <summary>
        /// Creates a battle that may include planet destroyers and a chosen player side.
        /// </summary>
        /// <param name="attackerShips">The attacking capital ships.</param>
        /// <param name="defenderShips">The defending capital ships.</param>
        /// <param name="attackerFighters">The attacking fighter squadrons.</param>
        /// <param name="defenderFighters">The defending fighter squadrons.</param>
        /// <param name="playerSide">The side the player commands.</param>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="shieldedStations">The planet destroyers under a planetary shield.</param>
        /// <param name="canWithdraw">Whether both sides have a route out.</param>
        /// <returns>The created battle.</returns>
        private static TacticalSpaceBattle CreateStationBattle(
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> attackerFighters = null,
            IReadOnlyList<Starfighter> defenderFighters = null,
            CombatSide? playerSide = CombatSide.Attacker,
            GameConfig.SpaceCombatConfig config = null,
            IReadOnlyCollection<CapitalShip> shieldedStations = null,
            bool canWithdraw = false
        )
        {
            return new TacticalSpaceBattle(
                config ?? CreateConfig(),
                new SystemRandomProvider(_seed),
                attackerShips,
                attackerFighters ?? new List<Starfighter>(),
                defenderShips,
                defenderFighters ?? new List<Starfighter>(),
                canWithdraw,
                canWithdraw,
                default,
                default,
                playerSide,
                shieldedStations
            );
        }

        /// <summary>
        /// Creates a planet-destroying capital ship, which fights as a battle station.
        /// </summary>
        /// <param name="instanceId">The instance identifier.</param>
        /// <returns>The created ship.</returns>
        private static CapitalShip CreateStation(string instanceId)
        {
            CapitalShip station = CreateShip(instanceId, 25000, 0);
            station.TypeID = "station";
            station.CanDestroyPlanets = true;
            return station;
        }

        /// <summary>
        /// Creates a hyperdrive-equipped fighter squadron that carries torpedoes.
        /// </summary>
        /// <param name="instanceId">The instance identifier.</param>
        /// <param name="squadronSize">The squadron size.</param>
        /// <returns>The created squadron.</returns>
        private static Starfighter CreateTorpedoFighter(string instanceId, int squadronSize)
        {
            Starfighter fighter = CreateFighter(instanceId, squadronSize, 5);
            fighter.Hyperdrive = 1;
            fighter.Torpedoes = 4;
            fighter.TorpedoRange = 7;
            return fighter;
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
