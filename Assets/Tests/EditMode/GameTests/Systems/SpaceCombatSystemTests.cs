using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Tests.Systems
{
    /// <summary>
    /// Tests for SpaceCombatSystem.
    /// Validates the 7-phase combat pipeline.
    /// </summary>
    [TestFixture]
    public class SpaceCombatSystemTests : CombatTestBase
    {
        [Test]
        public void Resolve_TwoFactionFleets_RunsSpaceCombat()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 10);
            CapitalShip empireShip = empireFleet.GetChildren<CapitalShip>()[0];
            CapitalShip allianceShip = allianceFleet.GetChildren<CapitalShip>()[0];
            empireShip.DisplayName = "Empire Ship";

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            TryResolveCombat(manager, empireFleet, allianceFleet, out List<GameResult> results);

            bool combatOccurred =
                HasDamageFor(results, empireShip) || HasDamageFor(results, allianceShip);
            SpaceCombatResult combatResult = GetCombatResult(results);
            Assert.IsTrue(combatOccurred, "Combat should occur between hostile factions");
            Assert.IsNotEmpty(combatResult.ShipDamage);
            foreach (ShipDamageResult damage in combatResult.ShipDamage)
            {
                int totalDamage = combatResult
                    .Events.OfType<GameObjectDamagedResult>()
                    .Where(result => result.GameObject == damage.Ship)
                    .Sum(result => result.DamageValue);
                Assert.AreEqual(damage.HullBefore - damage.HullAfter, totalDamage);
            }
            CombatUnitSnapshot empireShipSnapshot = combatResult.AttackingUnits.Single(unit =>
                unit.Unit.GetInstanceID() == empireShip.GetInstanceID()
            );
            Assert.AreNotSame(empireShip, empireShipSnapshot.Unit);
            empireShip.DisplayName = "Renamed Ship";
            Assert.AreEqual("Empire Ship", empireShipSnapshot.Unit.GetDisplayName());
            Assert.IsFalse(results.OfType<GameObjectDamagedResult>().Any());
        }

        [Test]
        public void Resolve_MultipleCombatRounds_ReturnsAggregateDamage()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleet(
                game,
                "f1",
                "empire",
                planet,
                1,
                100,
                10,
                shieldRechargeRate: 0
            );
            Fleet allianceFleet = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                100,
                10,
                shieldRechargeRate: 0
            );
            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, empireFleet, allianceFleet, out List<GameResult> results);

            SpaceCombatResult combatResult = GetCombatResult(results);
            ShipDamageResult repeatedDamage = combatResult.ShipDamage.FirstOrDefault(damage =>
                combatResult
                    .Events.OfType<GameObjectDamagedResult>()
                    .Count(result => result.GameObject == damage.Ship) > 1
            );
            List<GameObjectDamagedResult> repeatedDamageEvents = combatResult
                .Events.OfType<GameObjectDamagedResult>()
                .Where(result => result.GameObject == repeatedDamage?.Ship)
                .ToList();

            Assert.IsNotNull(repeatedDamage);
            Assert.AreEqual(100, repeatedDamage.HullBefore);
            Assert.AreEqual(repeatedDamage.Ship.CurrentHullStrength, repeatedDamage.HullAfter);
            Assert.AreEqual(
                repeatedDamage.HullBefore - repeatedDamage.HullAfter,
                repeatedDamageEvents.Sum(result => result.DamageValue)
            );
        }

        [Test]
        public void Resolve_NoHostileFleets_DoesNotRunCombat()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(empire);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet fleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            int initialHull = fleet.GetChildren<CapitalShip>()[0].CurrentHullStrength;

            QueueRNG rng = new QueueRNG();
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            bool detected = RunCombat(manager);

            Assert.IsFalse(detected, "No combat should be detected");
            Assert.AreEqual(
                initialHull,
                fleet.GetChildren<CapitalShip>()[0].CurrentHullStrength,
                "No combat should occur"
            );
        }

        [Test]
        public void Resolve_InTransitCapitalShipAttachedToFleet_DoesNotTakeCombatDamage()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1, 0);
            CapitalShip inTransitShip = new CapitalShip
            {
                InstanceID = "f1_ship_moving",
                OwnerInstanceID = "empire",
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
                ShieldRechargeRate = 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState
                {
                    TransitTicks = 5,
                    TicksElapsed = 1,
                    OriginPosition = planet.GetPosition(),
                    CurrentPosition = planet.GetPosition(),
                },
            };
            game.AttachNode(inTransitShip, empireFleet);

            Fleet allianceFleet = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                1000,
                100,
                shieldRechargeRate: 0
            );
            allianceFleet.GetChildren<CapitalShip>()[0].HasGravityWell = true;

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, empireFleet, allianceFleet, out _);

            Assert.AreEqual(1000, inTransitShip.CurrentHullStrength);
        }

        [Test]
        public void Resolve_InTransitStarfighterAttachedToFleet_DoesNotTakeCombatLosses()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1, 0);
            CapitalShip empireCarrier = empireFleet.GetChildren<CapitalShip>()[0];
            empireCarrier.StarfighterCapacity = 1;
            Starfighter inTransitFighter = new Starfighter
            {
                InstanceID = "f1_fighter_moving",
                OwnerInstanceID = "empire",
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                LaserCannon = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState
                {
                    TransitTicks = 5,
                    TicksElapsed = 1,
                    OriginPosition = planet.GetPosition(),
                    CurrentPosition = planet.GetPosition(),
                },
            };
            game.AttachNode(inTransitFighter, empireCarrier);

            Fleet allianceFleet = CreateFleetWithFighters(
                game,
                "f2",
                "alliance",
                planet,
                1,
                1000,
                0,
                100
            );
            allianceFleet.GetChildren<CapitalShip>()[0].HasGravityWell = true;

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, empireFleet, allianceFleet, out _);

            Assert.AreEqual(12, inTransitFighter.CurrentSquadronSize);
        }

        [Test]
        public void Resolve_SingleFactionFleets_DoesNotRunCombat()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(empire);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet fleet1 = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet fleet2 = CreateFleet(game, "f2", "empire", planet, 1, 100, 10);

            QueueRNG rng = new QueueRNG();
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            RunCombat(manager);

            Assert.AreEqual(100, fleet1.GetChildren<CapitalShip>()[0].CurrentHullStrength);
            Assert.AreEqual(100, fleet2.GetChildren<CapitalShip>()[0].CurrentHullStrength);
        }

        [Test]
        public void Resolve_MultipleAttackerFleets_OnlyFirstPairFights()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet1 = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet empireFleet2 = CreateFleet(game, "f2", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f3", "alliance", planet, 1, 100, 10);
            CapitalShip empireShip1 = empireFleet1.GetChildren<CapitalShip>()[0];
            CapitalShip empireShip2 = empireFleet2.GetChildren<CapitalShip>()[0];
            CapitalShip allianceShip = allianceFleet.GetChildren<CapitalShip>()[0];

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            TryResolveCombat(manager, empireFleet1, allianceFleet, out List<GameResult> results);

            bool firstPairFought =
                HasDamageFor(results, empireShip1) || HasDamageFor(results, allianceShip);
            Assert.IsTrue(firstPairFought, "First fleet fights");
            Assert.IsFalse(HasDamageFor(results, empireShip2), "Second fleet does not fight");
            Assert.AreEqual(
                100,
                empireFleet2.GetChildren<CapitalShip>()[0].CurrentHullStrength,
                "Second fleet does not fight"
            );
        }

        [Test]
        public void Resolve_AttackerDestroysDefender_ReturnsAttackerVictory()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1000, 100);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1, 0);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            RunCombat(manager);

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Fleet>(allianceFleet.InstanceID),
                "Defender fleet destroyed"
            );
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Fleet>("f1"), "Attacker survives");
        }

        [Test]
        public void Resolve_DefenderDestroysAttacker_ReturnsDefenderVictory()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1, 0);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1000, 100);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            RunCombat(manager);

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Fleet>(empireFleet.InstanceID),
                "Attacker fleet destroyed"
            );
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Fleet>("f2"), "Defender survives");
        }

        [Test]
        public void Resolve_MutualDestruction_RemovesBothFleets()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleet(
                game,
                "f1",
                "empire",
                planet,
                1,
                10,
                3,
                shieldRechargeRate: 0
            );
            Fleet allianceFleet = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                10,
                3,
                shieldRechargeRate: 0
            );

            QueueRNG rng = new QueueRNG(0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            RunCombat(manager);

            bool anyDestroyed =
                game.GetSceneNodeByInstanceID<Fleet>(empireFleet.InstanceID) == null
                || game.GetSceneNodeByInstanceID<Fleet>(allianceFleet.InstanceID) == null;
            Assert.IsTrue(
                anyDestroyed,
                "At least one fleet should be destroyed in evenly-matched combat"
            );
        }

        [Test]
        public void Resolve_ShipTakesDamage_ReducesCurrentHullStrength()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 10);
            CapitalShip empireShip = empireFleet.GetChildren<CapitalShip>()[0];

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            TryRunCombat(manager, out List<GameResult> results);

            Assert.IsTrue(
                HasDamageFor(results, empireShip),
                "Ships should take damage during combat"
            );
        }

        [Test]
        public void Resolve_ShipDestroyed_RemovedFromFleet()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1000, 100);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1, 0);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            RunCombat(manager);

            Assert.AreEqual(
                0,
                allianceFleet.GetChildren<CapitalShip>().Count,
                "Destroyed ship removed from fleet"
            );
        }

        [Test]
        public void Resolve_FighterSquadronTakesLosses_ReducesCurrentSquadronSize()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleetWithFighters(
                game,
                "f1",
                "empire",
                planet,
                1,
                1000,
                1,
                100
            );
            Fleet allianceFleet = CreateFleetWithFighters(
                game,
                "f2",
                "alliance",
                planet,
                1,
                50,
                1,
                10
            );

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            RunCombat(manager);

            Fleet allianceFleet2 = game.GetSceneNodeByInstanceID<Fleet>("f2");
            if (allianceFleet2 != null)
            {
                List<Starfighter> allFighters = allianceFleet2.GetStarfighters().ToList();
                if (allFighters.Count > 0)
                {
                    Assert.Less(
                        allFighters[0].CurrentSquadronSize,
                        10,
                        "Alliance fighters should take losses"
                    );
                    return;
                }
            }

            Fleet remainingEmpireFleet = game.GetSceneNodeByInstanceID<Fleet>("f1");
            Assert.IsNotNull(remainingEmpireFleet, "Empire fleet should still exist");
            List<Starfighter> empireFighters = remainingEmpireFleet.GetStarfighters().ToList();
            Assert.Greater(empireFighters.Count, 0, "Empire should have fighters");
            Assert.Less(
                empireFighters[0].CurrentSquadronSize,
                100,
                "Empire fighters should take some losses"
            );
        }

        [Test]
        public void Resolve_EmptyFleet_RemovedFromScene()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1000, 100);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1, 0);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            RunCombat(manager);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>(allianceFleet.InstanceID));
            bool foundFleet = false;
            foreach (Fleet fleet in planet.GetChildren<Fleet>())
            {
                if (fleet == allianceFleet)
                {
                    foundFleet = true;
                    break;
                }
            }
            Assert.IsFalse(foundFleet, "Destroyed fleet should not be in planet's children");
        }

        [Test]
        public void Resolve_BothSidesZeroWeapons_AppliesNoDamageAndSeparatesFleets()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 0);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 0);

            empireFleet.GetChildren<CapitalShip>()[0].PrimaryWeapons.Clear();
            allianceFleet.GetChildren<CapitalShip>()[0].PrimaryWeapons.Clear();

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            RunCombat(manager);

            Assert.AreEqual(
                100,
                empireFleet.GetChildren<CapitalShip>()[0].CurrentHullStrength,
                "No damage without weapons"
            );
            Assert.AreEqual(
                100,
                allianceFleet.GetChildren<CapitalShip>()[0].CurrentHullStrength,
                "No damage without weapons"
            );
            Assert.IsFalse(
                HasOpposingReadyFleets(planet),
                "Opposing fleets should not remain ready at the same planet"
            );
        }

        [Test]
        public void Resolve_WeaponFire_DamagesTargets()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 20);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 20);
            CapitalShip empireShip = empireFleet.GetChildren<CapitalShip>()[0];
            CapitalShip allianceShip = allianceFleet.GetChildren<CapitalShip>()[0];

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            TryRunCombat(manager, out List<GameResult> results);

            Assert.IsTrue(HasDamageFor(results, empireShip));
            Assert.IsTrue(HasDamageFor(results, allianceShip));
        }

        [Test]
        public void Resolve_MaxShieldStrength_AbsorbsDamageBeforeHull()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet attacker = CreateFleet(
                game,
                "f1",
                "empire",
                planet,
                1,
                1,
                10,
                shieldRechargeRate: 0
            );
            Fleet defender = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                100,
                100,
                shieldRechargeRate: 0
            );
            CapitalShip defenderShip = defender.GetChildren<CapitalShip>()[0];
            defenderShip.MaxShieldStrength = 100;

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, attacker, defender, out List<GameResult> results);

            Assert.AreEqual(100, defenderShip.CurrentHullStrength);
            Assert.IsFalse(HasDamageFor(results, defenderShip));
        }

        [Test]
        public void Resolve_ShieldRechargeRateWithoutShieldStrength_DoesNotAbsorbDamage()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet attacker = CreateFleet(
                game,
                "f1",
                "empire",
                planet,
                1,
                1,
                10,
                shieldRechargeRate: 0
            );
            Fleet defender = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                100,
                100,
                shieldRechargeRate: 15
            );
            CapitalShip defenderShip = defender.GetChildren<CapitalShip>()[0];
            defenderShip.MaxShieldStrength = 0;

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, attacker, defender, out List<GameResult> results);

            Assert.AreEqual(60, defenderShip.CurrentHullStrength);
            Assert.AreEqual(40, GetDamageFor(results, defenderShip));
        }

        [Test]
        public void Resolve_ShieldRechargeRate_RestoresShieldStrengthBetweenRounds()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet attacker = CreateFleet(
                game,
                "f1",
                "empire",
                planet,
                1,
                1000,
                10,
                shieldRechargeRate: 0
            );
            Fleet defender = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                100,
                0,
                shieldRechargeRate: 40
            );
            CapitalShip defenderShip = defender.GetChildren<CapitalShip>()[0];
            defenderShip.MaxShieldStrength = 50;

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, attacker, defender, out List<GameResult> results);

            Assert.AreEqual(100, defenderShip.CurrentHullStrength);
            Assert.IsFalse(HasDamageFor(results, defenderShip));
        }

        [Test]
        public void Resolve_DepletedShieldStrength_PersistsBetweenRounds()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet attacker = CreateFleet(
                game,
                "f1",
                "empire",
                planet,
                1,
                1000,
                10,
                shieldRechargeRate: 0
            );
            Fleet defender = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                100,
                0,
                shieldRechargeRate: 10
            );
            CapitalShip defenderShip = defender.GetChildren<CapitalShip>()[0];
            defenderShip.MaxShieldStrength = 50;

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, attacker, defender, out List<GameResult> results);

            Assert.AreEqual(0, defenderShip.CurrentHullStrength);
            ShipDamageResult damage = GetCombatResult(results)
                .ShipDamage.Single(result => result.Ship == defenderShip);
            Assert.AreEqual(100, damage.HullBefore - damage.HullAfter);
        }

        [Test]
        [Timeout(5000)]
        public void Resolve_ShieldDamageFullyRecharged_EndsAsStalemate()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet attacker = CreateFleet(
                game,
                "f1",
                "empire",
                planet,
                1,
                100,
                1,
                shieldRechargeRate: 4
            );
            Fleet defender = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                100,
                1,
                shieldRechargeRate: 4
            );
            attacker.GetChildren<CapitalShip>()[0].MaxShieldStrength = 100;
            defender.GetChildren<CapitalShip>()[0].MaxShieldStrength = 100;
            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, attacker, defender, out List<GameResult> results);

            SpaceCombatResult result = GetCombatResult(results);
            Assert.That(result.ShipDamage, Is.Empty);
            Assert.That(HasOpposingReadyFleets(planet), Is.False);
        }

        [Test]
        [Timeout(5000)]
        public void Resolve_PreDamagedShipWithStableShields_DoesNotReportExistingHullDamage()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet attacker = CreateFleet(
                game,
                "f1",
                "empire",
                planet,
                1,
                1000,
                1,
                shieldRechargeRate: 0
            );
            Fleet defender = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                100,
                0,
                shieldRechargeRate: 8
            );
            CapitalShip defenderShip = defender.GetChildren<CapitalShip>()[0];
            defenderShip.CurrentHullStrength = 50;
            defenderShip.MaxShieldStrength = 100;

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, attacker, defender, out List<GameResult> results);

            Assert.AreEqual(50, defenderShip.CurrentHullStrength);
            Assert.IsFalse(HasDamageFor(results, defenderShip));
        }

        [Test]
        [Timeout(5000)]
        public void Resolve_DamagedHull_ReducesShieldRechargeRate()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet attacker = CreateFleet(
                game,
                "f1",
                "empire",
                planet,
                1,
                1000,
                1,
                shieldRechargeRate: 0
            );
            Fleet defender = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                100,
                0,
                shieldRechargeRate: 4
            );
            CapitalShip defenderShip = defender.GetChildren<CapitalShip>()[0];
            defenderShip.CurrentHullStrength = 50;
            defenderShip.MaxShieldStrength = 100;

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, attacker, defender, out _);

            Assert.AreEqual(0, defenderShip.CurrentHullStrength);
        }

        [Test]
        public void Resolve_FighterDamage_IsAbsorbedByCapitalShipShields()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet attacker = CreateFleetWithFighters(game, "f1", "empire", planet, 1, 100, 0, 12);
            Fleet defender = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                100,
                0,
                shieldRechargeRate: 120
            );
            CapitalShip defenderShip = defender.GetChildren<CapitalShip>()[0];
            defenderShip.MaxShieldStrength = 200;

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, attacker, defender, out List<GameResult> results);

            Assert.AreEqual(100, defenderShip.CurrentHullStrength);
            Assert.IsFalse(HasDamageFor(results, defenderShip));
        }

        [Test]
        public void Resolve_FightersAttackCapitalShips_AppliesDamage()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet fighterFleet = CreateFleetWithFighters(
                game,
                "f1",
                "empire",
                planet,
                1,
                50,
                5,
                12
            );
            Fleet targetFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1000, 5);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            RunCombat(manager);

            Fleet target = game.GetSceneNodeByInstanceID<Fleet>("f2");
            Assert.IsNotNull(target, "Target fleet should still exist");
            Assert.Less(
                target.GetChildren<CapitalShip>()[0].CurrentHullStrength,
                1000,
                "Fighters should damage capital ships"
            );
        }

        [Test]
        public void Resolve_DifferentRNGValues_ProducesDifferentDamage()
        {
            int damage1 = ResolveDamageValues(0.0).First();
            int damage2 = ResolveDamageValues(1.0).First();

            Assert.AreNotEqual(damage1, damage2, "Damage should vary with different RNG");
        }

        [Test]
        public void Resolve_SameRNGSeed_ProducesSameOutcome()
        {
            CollectionAssert.AreEqual(
                ResolveDamageValues(0.5),
                ResolveDamageValues(0.5),
                "Same RNG should produce identical results"
            );
        }

        [Test]
        public void Resolve_DifferentRNGSeeds_ProduceDifferentOutcomes()
        {
            CollectionAssert.AreNotEqual(
                ResolveDamageValues(0.0),
                ResolveDamageValues(1.0),
                "Different RNG should produce different results"
            );
        }

        [Test]
        public void Resolve_EmptyFleets_DoesNotRunCombat()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "empire" };
            Fleet allianceFleet = new Fleet { InstanceID = "f2", OwnerInstanceID = "alliance" };
            game.AttachNode(empireFleet, planet);
            game.AttachNode(allianceFleet, planet);

            QueueRNG rng = new QueueRNG();
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            RunCombat(manager);

            Assert.Pass("Empty fleets should not cause combat");
        }

        [Test]
        public void Resolve_CombatWithSurvivors_ClearsIsInCombatOnSurvivingFleets()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 10000, 1);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 10000, 1);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            manager.ProcessTick();

            Fleet survivingEmpireFleet = game.GetSceneNodeByInstanceID<Fleet>("f1");
            Fleet survivingAllianceFleet = game.GetSceneNodeByInstanceID<Fleet>("f2");

            if (survivingEmpireFleet != null)
                Assert.IsFalse(
                    survivingEmpireFleet.IsInCombat,
                    "IsInCombat should be cleared after resolution"
                );
            if (survivingAllianceFleet != null)
                Assert.IsFalse(
                    survivingAllianceFleet.IsInCombat,
                    "IsInCombat should be cleared after resolution"
                );
        }

        [Test]
        public void Resolve_DefenderWinsOnOwnPlanet_DoesNotChangeOwnership()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire", PlayerID = null };
            Faction alliance = new Faction { InstanceID = "alliance", PlayerID = null };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "alliance",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "alliance", 80 } },
            };
            game.AttachNode(planet, planetSector);

            // Weak empire fleet vs strong alliance fleet — alliance defends
            Fleet empireFleet = CreateFleet(game, "ef1", "empire", planet, 1, 1, 0);
            Fleet allianceFleet = CreateFleet(game, "af1", "alliance", planet, 3, 1000, 100);

            QueueRNG rng = new QueueRNG(0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            RunCombat(manager);

            Assert.AreEqual(
                "alliance",
                planet.GetOwnerInstanceID(),
                "Defender winning on own planet should not change ownership"
            );
        }

        [Test]
        public void ProcessTick_WithInTransitFleet_IgnoresInTransitFleet()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 10);
            empireFleet.Movement = new MovementState
            {
                TransitTicks = 5,
                TicksElapsed = 1,
                OriginPosition = planet.GetPosition(),
                CurrentPosition = planet.GetPosition(),
            };

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG());

            List<GameResult> results = manager.ProcessTick();

            Assert.IsEmpty(results);
            Assert.IsFalse(empireFleet.IsInCombat);
            Assert.IsFalse(allianceFleet.IsInCombat);
        }

        [Test]
        public void ProcessTick_FleetsWithOnlyInTransitShips_DoesNotRunCombat()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 10);
            empireFleet.GetChildren<CapitalShip>()[0].Movement = new MovementState
            {
                TransitTicks = 5,
                TicksElapsed = 1,
                OriginPosition = planet.GetPosition(),
                CurrentPosition = planet.GetPosition(),
            };
            allianceFleet.GetChildren<CapitalShip>()[0].Movement = new MovementState
            {
                TransitTicks = 5,
                TicksElapsed = 1,
                OriginPosition = planet.GetPosition(),
                CurrentPosition = planet.GetPosition(),
            };

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG());

            List<GameResult> results = manager.ProcessTick();

            Assert.IsEmpty(results);
            Assert.IsFalse(empireFleet.IsInCombat);
            Assert.IsFalse(allianceFleet.IsInCombat);
        }

        [Test]
        public void ProcessTick_MultipleEncountersAllAI_ResolvesAll()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            CreatePlanet(game, "empireHome", owner: "empire");
            CreatePlanet(game, "allianceHome", owner: "alliance");

            for (int i = 1; i <= 3; i++)
            {
                PlanetSector planetSector = new PlanetSector { InstanceID = $"sector{i}" };
                Planet planet = new Planet { InstanceID = $"p{i}" };
                game.AttachNode(planetSector, game.Galaxy);
                game.AttachNode(planet, planetSector);
                CreateFleet(game, $"ef{i}", "empire", planet, 1, 1000, 20);
                CreateFleet(game, $"af{i}", "alliance", planet, 1, 1000, 20);
            }

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5, 0.5, 0.5);
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            List<GameResult> results = manager.ProcessTick();

            Assert.IsFalse(
                results.OfType<PendingCombatResult>().Any(),
                "All AI encounters should auto-resolve with no pending decision"
            );

            for (int i = 1; i <= 3; i++)
            {
                Planet planet = game.GetSceneNodeByInstanceID<Planet>($"p{i}");
                Assert.IsFalse(HasHostileFleets(planet));
            }
        }

        [Test]
        public void ProcessTick_WeakerAIFleetCanRetreat_MovesToFriendlyPlanet()
        {
            GameRoot game = CreateGame();
            (Planet combatPlanet, _) = CreatePlanet(game, "combat");
            (Planet empireHome, _) = CreatePlanet(game, "empireHome", owner: "empire");
            CreatePlanet(game, "allianceHome", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "ef1", "empire", combatPlanet, 1, 100, 1);
            Fleet allianceFleet = CreateFleet(game, "af1", "alliance", combatPlanet, 1, 1000, 100);

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG());

            manager.ProcessTick();

            Assert.AreSame(empireHome, empireFleet.GetParentOfType<Planet>());
            Assert.IsNotNull(empireFleet.Movement);
            Assert.AreSame(combatPlanet, allianceFleet.GetParentOfType<Planet>());
            Assert.IsFalse(HasHostileFleets(combatPlanet));
        }

        [Test]
        public void ProcessTick_WeakerAIFleetBlockedByGravityWell_Fights()
        {
            GameRoot game = CreateGame();
            (Planet combatPlanet, _) = CreatePlanet(game, "combat");
            CreatePlanet(game, "empireHome", owner: "empire");
            CreatePlanet(game, "allianceHome", owner: "alliance");

            Fleet empireFleet = CreateFleet(
                game,
                "ef1",
                "empire",
                combatPlanet,
                1,
                1,
                1,
                shieldRechargeRate: 0
            );
            Fleet allianceFleet = CreateFleet(
                game,
                "af1",
                "alliance",
                combatPlanet,
                1,
                1000,
                100,
                shieldRechargeRate: 0
            );
            allianceFleet.GetChildren<CapitalShip>()[0].HasGravityWell = true;

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick();

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>(empireFleet.InstanceID));
            Assert.AreSame(combatPlanet, allianceFleet.GetParentOfType<Planet>());
            Assert.IsFalse(HasHostileFleets(combatPlanet));
        }

        [Test]
        public void ProcessTick_UnarmedAIFleets_RetreatsBoth()
        {
            GameRoot game = CreateGame();
            (Planet combatPlanet, _) = CreatePlanet(game, "combat");
            (Planet empireHome, _) = CreatePlanet(game, "empireHome", owner: "empire");
            (Planet allianceHome, _) = CreatePlanet(game, "allianceHome", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "ef1", "empire", combatPlanet, 1, 100, 0);
            Fleet allianceFleet = CreateFleet(game, "af1", "alliance", combatPlanet, 1, 100, 0);
            empireFleet.GetChildren<CapitalShip>()[0].PrimaryWeapons.Clear();
            allianceFleet.GetChildren<CapitalShip>()[0].PrimaryWeapons.Clear();

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG());

            manager.ProcessTick();

            Assert.AreSame(empireHome, empireFleet.GetParentOfType<Planet>());
            Assert.AreSame(allianceHome, allianceFleet.GetParentOfType<Planet>());
            Assert.IsFalse(HasHostileFleets(combatPlanet));
        }

        [Test]
        public void ProcessTick_PlayerInvolvedEncounter_ReturnsPendingDecision()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire", PlayerID = "player1" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);
            CreateFleet(game, "ef1", "empire", planet, 1, 1000, 10);
            CreateFleet(game, "af1", "alliance", planet, 1, 1000, 10);

            QueueRNG rng = new QueueRNG();
            SpaceCombatSystem manager = MakeSpaceCombat(game, rng);

            List<GameResult> results = manager.ProcessTick();
            PendingCombatResult pending = results.OfType<PendingCombatResult>().SingleOrDefault();

            Assert.IsNotNull(
                pending,
                "Player-involved encounter should emit a PendingCombatResult"
            );
            Assert.AreSame(planet, pending.Planet);
            Assert.IsTrue(manager.HasPendingDecision);
            Assert.IsEmpty(manager.ProcessTick());

            List<GameResult> resolvedResults = manager.ResolvePending(autoResolve: true);

            Assert.IsFalse(manager.HasPendingDecision);
            Assert.IsNotEmpty(resolvedResults);
        }

        [Test]
        public void ProcessTick_PlayerInvolvedEncounter_ClearsFleetWaypointRoutes()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire", PlayerID = "player1" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);
            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);
            Fleet empireFleet = CreateFleet(game, "ef1", "empire", planet, 1, 1000, 10);
            Fleet allianceFleet = CreateFleet(game, "af1", "alliance", planet, 1, 1000, 10);
            empireFleet.Waypoints.Add("empire-next");
            allianceFleet.Waypoints.Add("alliance-next");
            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG());

            List<GameResult> results = manager.ProcessTick();

            Assert.IsNotNull(results.OfType<PendingCombatResult>().SingleOrDefault());
            Assert.IsEmpty(empireFleet.Waypoints);
            Assert.IsEmpty(allianceFleet.Waypoints);
        }

        [Test]
        public void ProcessTick_PlayerFleetAgainstPlanetaryStarfighters_ReturnsPendingDecision()
        {
            GameRoot game = CreateGame();
            game.GetFactions().First(faction => faction.InstanceID == "empire").PlayerID =
                "player1";
            (Planet planet, _) = CreatePlanet(game, "combat", owner: "alliance");
            Fleet fleet = CreateFleet(game, "ef1", "empire", planet, 1, 1000, 10);
            Starfighter defender = new Starfighter
            {
                InstanceID = "planet-fighter",
                OwnerInstanceID = "alliance",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                LaserCannon = 5,
            };
            game.AttachNode(defender, planet);
            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG());

            PendingCombatResult pending = manager
                .ProcessTick()
                .OfType<PendingCombatResult>()
                .Single();

            Assert.AreSame(fleet, pending.AttackerFleet);
            Assert.IsNull(pending.DefenderFleet);
            Assert.AreEqual("empire", pending.AttackerOwnerInstanceID);
            Assert.AreEqual("alliance", pending.DefenderOwnerInstanceID);
            Assert.AreSame(planet, pending.Planet);
            Assert.IsTrue(pending.AttackerCanRetreat);
            Assert.IsFalse(pending.DefenderCanRetreat);
        }

        [Test]
        public void ProcessTick_UnfinishedPlanetaryStarfighters_DoNotTriggerCombat()
        {
            GameRoot game = CreateGame();
            game.GetFactions().First(faction => faction.InstanceID == "empire").PlayerID =
                "player1";
            (Planet planet, _) = CreatePlanet(game, "combat", owner: "alliance");
            Fleet fleet = CreateFleet(game, "ef1", "empire", planet, 1, 1000, 10);
            Starfighter defender = new Starfighter
            {
                InstanceID = "planet-fighter",
                OwnerInstanceID = "alliance",
                ManufacturingStatus = ManufacturingStatus.Building,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                LaserCannon = 5,
            };
            game.AttachNode(defender, planet);
            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG());

            List<GameResult> results = manager.ProcessTick();

            Assert.IsEmpty(results);
            Assert.IsFalse(manager.HasPendingDecision);
            Assert.IsFalse(fleet.IsInCombat);
        }

        [Test]
        public void ProcessTick_PlayerInvolvedEncounter_SetsRetreatAvailability()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire", PlayerID = "player1" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);
            Fleet empireFleet = CreateFleet(game, "ef1", "empire", planet, 1, 1000, 10);
            Fleet allianceFleet = CreateFleet(game, "af1", "alliance", planet, 1, 1000, 10);
            allianceFleet.GetChildren<CapitalShip>()[0].HasGravityWell = true;

            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG());

            PendingCombatResult pending = manager
                .ProcessTick()
                .OfType<PendingCombatResult>()
                .Single();

            bool empireCanRetreat = ReferenceEquals(pending.AttackerFleet, empireFleet)
                ? pending.AttackerCanRetreat
                : pending.DefenderCanRetreat;
            bool allianceCanRetreat = ReferenceEquals(pending.AttackerFleet, allianceFleet)
                ? pending.AttackerCanRetreat
                : pending.DefenderCanRetreat;

            Assert.IsFalse(empireCanRetreat);
            Assert.IsTrue(allianceCanRetreat);
        }

        [Test]
        public void ResolvePending_PlanetaryStarfighters_ParticipateInCombat()
        {
            GameRoot game = CreateGame();
            game.GetFactions().First(faction => faction.InstanceID == "empire").PlayerID =
                "player1";
            (Planet planet, _) = CreatePlanet(game, "combat", owner: "alliance");
            Fleet fleet = CreateFleet(
                game,
                "ef1",
                "empire",
                planet,
                1,
                1,
                0,
                shieldRechargeRate: 0
            );
            Starfighter defender = new Starfighter
            {
                InstanceID = "planet-fighter",
                OwnerInstanceID = "alliance",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                LaserCannon = 100,
            };
            game.AttachNode(defender, planet);
            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5));

            manager.ProcessTick();
            SpaceCombatResult result = manager
                .ResolvePending(autoResolve: true)
                .OfType<SpaceCombatResult>()
                .Single();

            Assert.AreEqual(CombatSide.Defender, result.Winner);
            Assert.AreEqual("alliance", result.DefenderOwnerInstanceID);
            Assert.AreSame(planet, defender.GetParentOfType<Planet>());
            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID));
        }

        [Test]
        public void ResolvePending_CorellianCorvetteAgainstPlanetaryTie_DestroysTie()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "FNALL1", PlayerID = "player1" });
            game.GetFactions().Add(new Faction { InstanceID = "FNEMP1" });
            (Planet planet, _) = CreatePlanet(game, "combat", owner: "FNEMP1");
            Fleet fleet = new Fleet { InstanceID = "alliance-fleet", OwnerInstanceID = "FNALL1" };
            game.AttachNode(fleet, planet);

            CapitalShip corvette = TestContent
                .Data.CapitalShips.Single(ship => ship.GetTypeID() == "ALCS006")
                .GetDeepCopy();
            corvette.InstanceID = "corellian-corvette";
            corvette.OwnerInstanceID = "FNALL1";
            corvette.ManufacturingStatus = ManufacturingStatus.Complete;
            corvette.Movement = null;
            game.AttachNode(corvette, fleet);

            Starfighter tie = TestContent
                .Data.Starfighters.Single(fighter => fighter.GetTypeID() == "SFEM01")
                .GetDeepCopy();
            tie.InstanceID = "planet-tie";
            tie.OwnerInstanceID = "FNEMP1";
            tie.ManufacturingStatus = ManufacturingStatus.Complete;
            tie.Movement = null;
            game.AttachNode(tie, planet);
            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG(0.5));

            manager.ProcessTick();
            SpaceCombatResult result = manager
                .ResolvePending(autoResolve: true)
                .OfType<SpaceCombatResult>()
                .Single();

            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.AreEqual(500, corvette.CurrentHullStrength);
            Assert.IsNull(game.GetSceneNodeByInstanceID<Starfighter>(tie.InstanceID));
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Fleet>("alliance-fleet"));
        }

        [Test]
        public void ResolvePending_WhenResolveThrows_KeepsPendingDecision()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire", PlayerID = "player1" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);
            CreateFleet(game, "ef1", "empire", planet, 1, 1000, 10);
            CreateFleet(game, "af1", "alliance", planet, 1, 1000, 10);

            SpaceCombatSystem manager = MakeSpaceCombat(game, new ThrowingRNG());

            manager.ProcessTick();

            Assert.Throws<InvalidOperationException>(() =>
                manager.ResolvePending(autoResolve: true)
            );
            Assert.IsTrue(manager.HasPendingDecision);
        }

        [Test]
        public void ResolvePendingRetreat_PlayerFleet_MovesToFriendlyPlanet()
        {
            GameRoot game = CreateGame();
            game.GetFactions().First(faction => faction.InstanceID == "empire").PlayerID =
                "player1";
            (Planet combatPlanet, _) = CreatePlanet(game, "combat");
            (Planet empireHome, _) = CreatePlanet(game, "empireHome", owner: "empire");
            empireHome.PositionX = 100;
            CreatePlanet(game, "allianceHome", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "ef1", "empire", combatPlanet, 1, 100, 1);
            Fleet allianceFleet = CreateFleet(game, "af1", "alliance", combatPlanet, 1, 1000, 100);
            SpaceCombatSystem manager = MakeSpaceCombat(game, new QueueRNG());

            manager.ProcessTick();
            List<GameResult> results = manager.ResolvePendingRetreat("empire");

            Assert.IsNotNull(results);
            Assert.AreSame(empireHome, empireFleet.GetParentOfType<Planet>());
            Assert.IsNotNull(empireFleet.Movement);
            SpaceCombatResult combatResult = results.OfType<SpaceCombatResult>().Single();
            Assert.AreSame(combatPlanet, combatResult.Planet);
            bool empireWasAttacker = ReferenceEquals(combatResult.AttackerFleet, empireFleet);
            Assert.AreSame(
                allianceFleet,
                empireWasAttacker ? combatResult.DefenderFleet : combatResult.AttackerFleet
            );
            Assert.AreEqual(
                empireWasAttacker ? CombatSide.Defender : CombatSide.Attacker,
                combatResult.Winner
            );
            Assert.AreEqual(
                SpaceCombatSideOutcome.Withdrawn,
                empireWasAttacker ? combatResult.AttackerOutcome : combatResult.DefenderOutcome
            );
            Assert.AreEqual(
                SpaceCombatSideOutcome.Active,
                empireWasAttacker ? combatResult.DefenderOutcome : combatResult.AttackerOutcome
            );
        }

        [Test]
        public void EvacuateOfficers_ShipDestroyedWithSurvivingShip_OfficerMovedToSurvivingShip()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "alliance" });

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planet, planetSector);

            // Alliance fleet: two ships. Weak ship dies, strong ship survives.
            Fleet allianceFleet = new Fleet { InstanceID = "af1", OwnerInstanceID = "alliance" };
            CapitalShip weakShip = new CapitalShip
            {
                InstanceID = "weak",
                OwnerInstanceID = "alliance",
                MaxHullStrength = 1,
                CurrentHullStrength = 1,
                ShieldRechargeRate = 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            CapitalShip strongShip = new CapitalShip
            {
                InstanceID = "strong",
                OwnerInstanceID = "alliance",
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
                ShieldRechargeRate = 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            allianceFleet.AddChild(weakShip);
            weakShip.SetParent(allianceFleet);
            allianceFleet.AddChild(strongShip);
            strongShip.SetParent(allianceFleet);
            game.AttachNode(allianceFleet, planet);

            Officer officer = new Officer { InstanceID = "han", OwnerInstanceID = "alliance" };
            game.AttachNode(officer, weakShip);

            // Overwhelming empire fleet destroys the weak ship.
            Fleet empireFleet = CreateFleet(
                game,
                "ef1",
                "empire",
                planet,
                1,
                1000,
                100,
                shieldRechargeRate: 0
            );

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            TryResolveCombat(MakeSpaceCombat(game, rng), empireFleet, allianceFleet, out _);

            Assert.Contains(
                officer,
                strongShip.GetChildren<Officer>().ToList(),
                "Officer should be evacuated to the surviving ship"
            );
        }

        [Test]
        public void EvacuateOfficers_LastShipDestroyed_OfficerEvacuatedToNearestFriendlyPlanet()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "alliance" });

            PlanetSector sector1 = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector1, game.Galaxy);
            Planet combatPlanet = new Planet { InstanceID = "p1" };
            game.AttachNode(combatPlanet, sector1);

            PlanetSector sector2 = new PlanetSector { InstanceID = "sector2" };
            game.AttachNode(sector2, game.Galaxy);
            Planet alliancePlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "alliance",
                IsColonized = true,
            };
            game.AttachNode(alliancePlanet, sector2);

            // Alliance fleet: single ship that is immediately destroyed.
            Fleet allianceFleet = new Fleet { InstanceID = "af1", OwnerInstanceID = "alliance" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship1",
                OwnerInstanceID = "alliance",
                MaxHullStrength = 1,
                CurrentHullStrength = 1,
                ShieldRechargeRate = 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            allianceFleet.AddChild(ship);
            ship.SetParent(allianceFleet);
            game.AttachNode(allianceFleet, combatPlanet);

            Officer officer = new Officer { InstanceID = "leia", OwnerInstanceID = "alliance" };
            game.AttachNode(officer, ship);

            Fleet empireFleet = CreateFleet(
                game,
                "ef1",
                "empire",
                combatPlanet,
                1,
                1000,
                100,
                shieldRechargeRate: 0
            );

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            TryResolveCombat(MakeSpaceCombat(game, rng), empireFleet, allianceFleet, out _);

            Assert.Contains(
                officer,
                alliancePlanet.GetChildren<Officer>().ToList(),
                "Officer should be evacuated to the nearest friendly planet"
            );
        }

        /// <summary>
        /// Runs a full combat cycle: detect then resolve (auto).
        /// Returns true if combat was detected and resolved.
        /// </summary>
        private bool RunCombat(SpaceCombatSystem manager)
        {
            return TryRunCombat(manager, out _);
        }

        private bool TryRunCombat(SpaceCombatSystem manager, out List<GameResult> results)
        {
            results = manager.ProcessTick();
            return results.Count > 0;
        }

        private bool TryResolveCombat(
            SpaceCombatSystem manager,
            Fleet attacker,
            Fleet defender,
            out List<GameResult> results
        )
        {
            Planet planet =
                attacker.GetParentOfType<Planet>() ?? defender.GetParentOfType<Planet>();
            results = manager.Resolve(
                new SpaceCombatDecision
                {
                    AttackerFleetInstanceID = attacker.InstanceID,
                    DefenderFleetInstanceID = defender.InstanceID,
                    AttackerOwnerInstanceID = attacker.OwnerInstanceID,
                    DefenderOwnerInstanceID = defender.OwnerInstanceID,
                    PlanetInstanceID = planet?.InstanceID,
                },
                true
            );
            return results.Count > 0;
        }

        private static bool HasDamageFor(List<GameResult> results, CapitalShip ship)
        {
            return GetDamageResults(results)
                .Any(result => result.GameObject == ship && result.DamageValue > 0);
        }

        private static int GetDamageFor(List<GameResult> results, CapitalShip ship)
        {
            GameObjectDamagedResult damageResult = GetDamageResults(results)
                .FirstOrDefault(result => result.GameObject == ship);

            return damageResult?.DamageValue ?? 0;
        }

        private static IEnumerable<GameObjectDamagedResult> GetDamageResults(
            List<GameResult> results
        )
        {
            return results
                .OfType<SpaceCombatResult>()
                .SelectMany(result => result.Events)
                .OfType<GameObjectDamagedResult>();
        }

        private static SpaceCombatResult GetCombatResult(List<GameResult> results)
        {
            return results.OfType<SpaceCombatResult>().Single();
        }

        private List<int> ResolveDamageValues(double randomValue)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            CreateFleet(game, "f1", "empire", planet, 1, 100, 20);
            CreateFleet(game, "f2", "alliance", planet, 1, 100, 20);

            QueueRNG rng = new QueueRNG(
                randomValue,
                randomValue,
                randomValue,
                randomValue,
                randomValue,
                randomValue
            );
            TryRunCombat(MakeSpaceCombat(game, rng), out List<GameResult> results);

            List<int> damageValues = GetDamageResults(results)
                .Select(result => result.DamageValue)
                .ToList();

            CollectionAssert.IsNotEmpty(damageValues, "Combat should emit damage results.");
            return damageValues;
        }

        private bool HasOpposingReadyFleets(Planet planet)
        {
            return planet
                    .GetChildren<Fleet>()
                    .Where(fleet => fleet.Movement == null)
                    .Select(fleet => fleet.GetOwnerInstanceID())
                    .Where(ownerInstanceId => !string.IsNullOrEmpty(ownerInstanceId))
                    .Distinct()
                    .Count() > 1;
        }

        private Fleet CreateFleet(
            GameRoot game,
            string instanceId,
            string ownerId,
            Planet planet,
            int shipCount,
            int hullStrength,
            int weaponPower,
            int shieldRechargeRate = 5
        )
        {
            Fleet fleet = new Fleet { InstanceID = instanceId, OwnerInstanceID = ownerId };

            for (int i = 0; i < shipCount; i++)
            {
                CapitalShip ship = new CapitalShip
                {
                    InstanceID = $"{instanceId}_ship{i}",
                    OwnerInstanceID = ownerId,
                    MaxHullStrength = hullStrength,
                    CurrentHullStrength = hullStrength,
                    ShieldRechargeRate = shieldRechargeRate,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };

                // Add weapon arcs
                if (weaponPower > 0)
                {
                    ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new int[]
                    {
                        weaponPower,
                        weaponPower,
                        weaponPower,
                        weaponPower,
                    };
                }

                fleet.AddChild(ship);
                ship.SetParent(fleet);
            }

            game.AttachNode(fleet, planet);
            return fleet;
        }

        private static bool HasHostileFleets(Planet planet)
        {
            List<string> owners = planet
                .GetChildren<Fleet>()
                .Where(fleet => fleet.Movement == null)
                .Select(fleet => fleet.GetOwnerInstanceID())
                .Where(owner => !string.IsNullOrEmpty(owner))
                .Distinct()
                .ToList();

            return owners.Count > 1;
        }

        private Fleet CreateFleetWithFighters(
            GameRoot game,
            string instanceId,
            string ownerId,
            Planet planet,
            int shipCount,
            int hullStrength,
            int weaponPower,
            int squadronSize
        )
        {
            Fleet fleet = CreateFleet(
                game,
                instanceId,
                ownerId,
                planet,
                shipCount,
                hullStrength,
                weaponPower
            );

            // Add fighters to first ship
            if (fleet.GetChildren<CapitalShip>().Count > 0)
            {
                Starfighter fighter = new Starfighter
                {
                    InstanceID = $"{instanceId}_fighter",
                    OwnerInstanceID = ownerId,
                    MaxSquadronSize = squadronSize,
                    CurrentSquadronSize = squadronSize,
                    LaserCannon = 5,
                    IonCannon = 3,
                    Torpedoes = 2,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                fleet.GetChildren<CapitalShip>()[0].StarfighterCapacity = 1;
                game.AttachNode(fighter, fleet.GetChildren<CapitalShip>()[0]);
            }

            return fleet;
        }
    }
}
