using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class RecoveryTickProcessorTests
    {
        [TestCase(79, 9)]
        [TestCase(80, 8)]
        public void ProcessTick_AuthoredHealingThreshold_UsesConfiguredForceBoundary(
            int rank,
            int expectedInjury
        )
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.SetConfig(TestContent.Data.GameConfig.CreateCopy());
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            officer.ForceValue = rank;
            officer.InjuryPoints = 10;
            game.AttachNode(officer, planet);

            new RecoveryTickProcessor().ProcessTick(game);

            Assert.AreEqual(expectedInjury, officer.InjuryPoints);
        }

        [Test]
        public void ProcessTick_InjuredOfficerAtFriendlyPlanet_ReducesInjury()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            game.AttachNode(officer, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(9, officer.InjuryPoints, "Officer should heal 1 point per tick");
        }

        [Test]
        public void ProcessTick_InjuredHighRankForceSensitiveOfficer_HealsMorePerTick()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            officer.IsForceSensitive = true;
            officer.ForceValue = game.Config.Jedi.FastHealThreshold;
            game.AttachNode(officer, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(
                7,
                officer.InjuryPoints,
                "High-rank Force-sensitive officer should heal 3 points per tick"
            );
        }

        [Test]
        public void ProcessTick_InjuredLowRankForceSensitiveOfficer_HealsNormally()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            officer.IsForceSensitive = true;
            officer.ForceValue = game.Config.Jedi.FastHealThreshold - 1;
            game.AttachNode(officer, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(9, officer.InjuryPoints);
        }

        [Test]
        public void ProcessTick_InjuredOfficerAtEnemyPlanet_DoesNotHeal()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            game.AttachNode(officer, planet);
            planet.OwnerInstanceID = "rebels";

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(10, officer.InjuryPoints, "Officer at enemy planet should not heal");
        }

        [Test]
        public void ProcessTick_InjuredOfficerAboardFriendlyFleet_ReducesInjury()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(officer, ship);
            planet.OwnerInstanceID = "rebels";

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(9, officer.InjuryPoints, "Officer aboard friendly fleet should heal");
        }

        [Test]
        public void ProcessTick_CapturedOfficer_DoesNotHeal()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            officer.IsCaptured = true;
            game.AttachNode(officer, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(10, officer.InjuryPoints, "Captured officer should not heal");
        }

        [Test]
        public void ProcessTick_OfficerInTransit_DoesNotHeal()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            officer.Movement = TestMovement.Create(transitTicks: 10);
            game.AttachNode(officer, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(10, officer.InjuryPoints, "Officer in transit should not heal");
        }

        [Test]
        public void ProcessTick_OfficerAboardFleetInTransit_DoesNotHeal()
        {
            (GameRoot game, Planet planet) = BuildScene();
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            fleet.Movement = TestMovement.Create(transitTicks: 10);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(officer, ship);

            new RecoveryTickProcessor().ProcessTick(game);

            Assert.AreEqual(10, officer.InjuryPoints);
        }

        [Test]
        public void ProcessTick_OfficerOnMission_DoesNotHeal()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            game.AttachNode(officer, mission);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(10, officer.InjuryPoints, "Officer on mission should not heal");
        }

        [Test]
        public void ProcessTick_OfficerFullyHealed_EmitsResult()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 1;
            game.AttachNode(officer, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            IReadOnlyList<GameResult> results = system.ProcessTick(game);

            Assert.IsTrue(
                results.Any(r => r is OfficerInjuredResult),
                "Fully healed officer should produce result"
            );
        }

        [Test]
        public void ProcessTick_OfficerPartiallyHealed_NoResult()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            game.AttachNode(officer, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            IReadOnlyList<GameResult> results = system.ProcessTick(game);

            Assert.IsFalse(
                results.Any(r => r is OfficerInjuredResult),
                "Partially healed officer should not produce result"
            );
        }

        [Test]
        public void ProcessTick_HealingClampsToZero_DoesNotGoNegative()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 1;
            officer.IsForceSensitive = true;
            game.AttachNode(officer, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(0, officer.InjuryPoints, "Injury should clamp to 0, not go negative");
        }

        [Test]
        public void ProcessTick_DamagedShipAtFriendlyShipyard_RepairsFast()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Fleet fleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "s1",
                OwnerInstanceID = "empire",
                MaxHullStrength = 100,
                CurrentHullStrength = 80,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(
                new Building
                {
                    InstanceID = "shipyard",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Shipyard,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(
                85,
                ship.CurrentHullStrength,
                "Ship at a friendly orbital shipyard should repair 5 per tick"
            );
        }

        [Test]
        public void ProcessTick_DamagedShipAtFriendlyPlanetWithoutShipyard_RepairsSlowly()
        {
            (GameRoot game, Planet planet) = BuildScene();
            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "s1",
                OwnerInstanceID = "empire",
                MaxHullStrength = 100,
                CurrentHullStrength = 80,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            new RecoveryTickProcessor().ProcessTick(game);

            Assert.AreEqual(81, ship.CurrentHullStrength);
        }

        [Test]
        public void ProcessTick_DamagedShipAtIncompleteFriendlyShipyard_RepairsSlowly()
        {
            (GameRoot game, Planet planet) = BuildScene();
            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "s1",
                OwnerInstanceID = "empire",
                MaxHullStrength = 100,
                CurrentHullStrength = 80,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(
                new Building
                {
                    InstanceID = "shipyard",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Shipyard,
                    ManufacturingStatus = ManufacturingStatus.Building,
                },
                planet
            );

            new RecoveryTickProcessor().ProcessTick(game);

            Assert.AreEqual(81, ship.CurrentHullStrength);
        }

        [Test]
        public void ProcessTick_DamagedShipInTransit_DoesNotRepair()
        {
            (GameRoot game, Planet planet) = BuildScene();
            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            fleet.Movement = TestMovement.Create(transitTicks: 10);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "s1",
                OwnerInstanceID = "empire",
                MaxHullStrength = 100,
                CurrentHullStrength = 80,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            new RecoveryTickProcessor().ProcessTick(game);

            Assert.AreEqual(80, ship.CurrentHullStrength);
        }

        [Test]
        public void ProcessTick_DamagedShipWithDirectTransit_DoesNotRepair()
        {
            (GameRoot game, Planet planet) = BuildScene();
            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "s1",
                OwnerInstanceID = "empire",
                MaxHullStrength = 100,
                CurrentHullStrength = 80,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = TestMovement.Create(transitTicks: 10),
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            new RecoveryTickProcessor().ProcessTick(game);

            Assert.AreEqual(80, ship.CurrentHullStrength);
        }

        [Test]
        public void ProcessTick_DamagedShipAtEnemyPlanet_RepairsSlowly()
        {
            (GameRoot game, Planet planet) = BuildScene();
            planet.OwnerInstanceID = "rebels";

            Fleet fleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "s1",
                OwnerInstanceID = "empire",
                MaxHullStrength = 100,
                CurrentHullStrength = 80,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(
                81,
                ship.CurrentHullStrength,
                "Ship at enemy planet should repair 1 per tick"
            );
        }

        [Test]
        public void ProcessTick_ShipAtMaxHull_NoChange()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Fleet fleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "s1",
                OwnerInstanceID = "empire",
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(100, ship.CurrentHullStrength, "Undamaged ship should not change");
        }

        [Test]
        public void ProcessTick_RepairClampsToMax_DoesNotExceedHull()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Fleet fleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "s1",
                OwnerInstanceID = "empire",
                MaxHullStrength = 100,
                CurrentHullStrength = 98,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(
                new Building
                {
                    InstanceID = "shipyard",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Shipyard,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(
                100,
                ship.CurrentHullStrength,
                "Repair should clamp to MaxHullStrength"
            );
        }

        [Test]
        public void ProcessTick_ShipFullyRepaired_EmitsResult()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Fleet fleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "s1",
                OwnerInstanceID = "empire",
                MaxHullStrength = 100,
                CurrentHullStrength = 99,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            IReadOnlyList<GameResult> results = system.ProcessTick(game);

            Assert.IsTrue(
                results.Any(r => r is ShipHullDamageResult),
                "Fully repaired ship should produce result"
            );
        }

        [Test]
        public void ProcessTick_ShipPartiallyRepaired_NoResult()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Fleet fleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "s1",
                OwnerInstanceID = "empire",
                MaxHullStrength = 100,
                CurrentHullStrength = 80,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            IReadOnlyList<GameResult> results = system.ProcessTick(game);

            Assert.IsFalse(
                results.Any(r => r is ShipHullDamageResult),
                "Partially repaired ship should not produce result"
            );
        }

        [Test]
        public void ProcessTick_DepletedSquadronWithoutShipyard_WaitsForNormalInterval()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.CurrentTick = 10;

            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 8,
            };
            game.AttachNode(squadron, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);
            game.CurrentTick = 39;
            system.ProcessTick(game);

            Assert.AreEqual(8, squadron.CurrentSquadronSize);

            game.CurrentTick = 40;
            system.ProcessTick(game);

            Assert.AreEqual(
                9,
                squadron.CurrentSquadronSize,
                "A friendly planet without a shipyard should use the normal interval"
            );
        }

        [Test]
        public void ProcessTick_DepletedSquadronAtFriendlyShipyard_UsesFastInterval()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.CurrentTick = 10;
            game.AttachNode(
                new Building
                {
                    InstanceID = "shipyard",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Shipyard,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 8,
            };
            game.AttachNode(squadron, planet);
            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);
            game.CurrentTick = 11;
            system.ProcessTick(game);

            Assert.AreEqual(8, squadron.CurrentSquadronSize);

            game.CurrentTick = 12;
            system.ProcessTick(game);

            Assert.AreEqual(9, squadron.CurrentSquadronSize);
        }

        [Test]
        public void ProcessTick_SquadronInTransitToFriendlyShipyard_UsesNormalInterval()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.CurrentTick = 10;
            game.AttachNode(
                new Building
                {
                    InstanceID = "shipyard",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Shipyard,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            Fleet fleet = new Fleet
            {
                InstanceID = "f1",
                OwnerInstanceID = "empire",
                Movement = TestMovement.Create(transitTicks: 10),
            };
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "carrier1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 20,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 8,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(carrier, fleet);
            game.AttachNode(squadron, carrier);

            new RecoveryTickProcessor().ProcessTick(game);

            Assert.AreEqual(30, squadron.ReplacementIntervalTicks);
            Assert.AreEqual(40, squadron.NextReplacementTick);
        }

        [Test]
        public void ProcessTick_DepletedSquadronAtEnemyPlanet_UsesNormalInterval()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.CurrentTick = 10;
            planet.OwnerInstanceID = "rebels";

            Fleet fleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "empire" };
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "carrier1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 20,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 8,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(carrier, fleet);
            game.AttachNode(squadron, carrier);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);
            game.CurrentTick = 40;
            system.ProcessTick(game);

            Assert.AreEqual(
                9,
                squadron.CurrentSquadronSize,
                "Squadron at enemy planet should replace one fighter after the normal interval"
            );
        }

        [Test]
        public void ProcessTick_SquadronReachesFriendlyShipyard_ReschedulesForFastInterval()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.CurrentTick = 10;
            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 8,
            };
            game.AttachNode(squadron, planet);
            RecoveryTickProcessor system = new RecoveryTickProcessor();
            system.ProcessTick(game);

            game.AttachNode(
                new Building
                {
                    InstanceID = "shipyard",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Shipyard,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.CurrentTick = 11;
            system.ProcessTick(game);

            Assert.AreEqual(2, squadron.ReplacementIntervalTicks);
            Assert.AreEqual(13, squadron.NextReplacementTick);
        }

        [Test]
        public void ProcessTick_FullSquadron_ClearsReplacementSchedule()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                NextReplacementTick = 20,
                ReplacementIntervalTicks = 30,
            };
            game.AttachNode(squadron, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(0, squadron.NextReplacementTick);
            Assert.AreEqual(0, squadron.ReplacementIntervalTicks);
        }

        [Test]
        public void ProcessTick_ReplacementClampsToMax_DoesNotExceedSquadronSize()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.CurrentTick = 10;

            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 11,
                NextReplacementTick = 10,
                ReplacementIntervalTicks = 30,
            };
            game.AttachNode(squadron, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(
                12,
                squadron.CurrentSquadronSize,
                "Replacement should clamp to MaxSquadronSize"
            );
        }

        [Test]
        public void ProcessTick_SquadronFullyReplaced_EmitsResult()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.CurrentTick = 10;

            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 11,
                NextReplacementTick = 10,
                ReplacementIntervalTicks = 30,
            };
            game.AttachNode(squadron, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            IReadOnlyList<GameResult> results = system.ProcessTick(game);

            Assert.IsTrue(
                results.Any(r => r is FighterDamageResult),
                "Fully replaced squadron should produce result"
            );
        }

        [Test]
        public void ProcessTick_SquadronPartiallyReplaced_NoResult()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.CurrentTick = 10;

            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 8,
                NextReplacementTick = 10,
                ReplacementIntervalTicks = 30,
            };
            game.AttachNode(squadron, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            IReadOnlyList<GameResult> results = system.ProcessTick(game);

            Assert.IsFalse(
                results.Any(r => r is FighterDamageResult),
                "Partially replaced squadron should not produce result"
            );
        }

        [Test]
        public void ProcessTick_ShipUnderConstruction_NotRepaired()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Fleet fleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "s1",
                OwnerInstanceID = "empire",
                MaxHullStrength = 100,
                CurrentHullStrength = 50,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(
                50,
                ship.CurrentHullStrength,
                "Ship still under construction should not be repaired"
            );
        }

        [Test]
        public void ProcessTick_SquadronUnderConstruction_NotReplaced()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 8,
            };
            game.AttachNode(squadron, planet);

            RecoveryTickProcessor system = new RecoveryTickProcessor();

            system.ProcessTick(game);

            Assert.AreEqual(
                8,
                squadron.CurrentSquadronSize,
                "Squadron still under construction should not have fighters replaced"
            );
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void ProcessTick_SystemRepairDue_RepairsWeightedComponent(int selected)
        {
            (GameRoot game, Planet planet) = BuildScene();
            CapitalShip ship = CreateSystemDamagedShip(game, planet);
            ship.SystemDamage = new CapitalShipSystemDamage
            {
                Shields = 1,
                Weapons = 1,
                TractorBeam = 1,
                Engines = 1,
                PrimaryHyperdrive = 1,
                BackupHyperdrive = 1,
            };
            game.Random = new FixedRandomProvider(new[] { 0.0, (selected + 0.5) / 6 });
            game.CurrentTick = 10;
            RecoveryTickProcessor processor = new();
            processor.ProcessTick(game);
            game.CurrentTick = 40;
            processor.ProcessTick(game);
            int[] damage =
            {
                ship.SystemDamage.Shields,
                ship.SystemDamage.Weapons,
                ship.SystemDamage.TractorBeam,
                ship.SystemDamage.Engines,
                ship.SystemDamage.PrimaryHyperdrive,
                ship.SystemDamage.BackupHyperdrive,
            };
            Assert.AreEqual(0, damage[selected]);
            Assert.AreEqual(5, ship.SystemDamage.Total);
        }

        [Test]
        public void ProcessTick_SystemRepairNotDue_LeavesComponentsDamaged()
        {
            (GameRoot game, Planet planet) = BuildScene();
            CapitalShip ship = CreateSystemDamagedShip(game, planet);
            game.CurrentTick = 10;
            RecoveryTickProcessor processor = new();
            processor.ProcessTick(game);
            game.CurrentTick = 39;
            processor.ProcessTick(game);
            Assert.AreEqual(1, ship.SystemDamage.Engines);
        }

        [Test]
        public void ProcessTick_ShipyardAvailable_RearmsSystemRepairAtFastInterval()
        {
            (GameRoot game, Planet planet) = BuildScene();
            CapitalShip ship = CreateSystemDamagedShip(game, planet);
            RecoveryTickProcessor processor = new();
            game.CurrentTick = 10;
            processor.ProcessTick(game);
            Building shipyard = new()
            {
                InstanceID = "shipyard",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Shipyard,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(shipyard, planet);
            game.CurrentTick = 11;
            processor.ProcessTick(game);
            Assert.AreEqual(13, ship.SystemDamage.RepairDueTick);
        }

        [Test]
        public void ProcessTick_FailedSystemRepairRoll_RetainsComponentDamage()
        {
            (GameRoot game, Planet planet) = BuildScene();
            CapitalShip ship = CreateSystemDamagedShip(game, planet);
            ship.DamageControl = 50;
            game.Random = new FixedRandomProvider(new[] { 0.99 });
            game.CurrentTick = 10;
            RecoveryTickProcessor processor = new();
            processor.ProcessTick(game);
            game.CurrentTick = 40;
            processor.ProcessTick(game);
            Assert.AreEqual(1, ship.SystemDamage.Engines);
        }

        /// <summary>
        /// Creates a fully built ship with shared repair definitions and one damaged system.
        /// </summary>
        /// <param name="game">The fixture's campaign graph.</param>
        /// <param name="planet">The ship's repair location.</param>
        /// <returns>The attached damaged ship.</returns>
        private static CapitalShip CreateSystemDamagedShip(GameRoot game, Planet planet)
        {
            GameDataCatalog content = TestContent.Data;
            UnitFactory factory = new(
                content.Buildings,
                content.CapitalShips,
                content.Starfighters,
                content.Regiments,
                content.SpecialForces,
                content.TacticalCombat
            );
            CapitalShip ship = factory.Create<CapitalShip>(
                content.CapitalShips[0].TypeID,
                "empire"
            );
            ship.InstanceID = "repair-ship";
            ship.OwnerInstanceID = "empire";
            ship.ManufacturingStatus = ManufacturingStatus.Complete;
            ship.CurrentHullStrength = ship.MaxHullStrength;
            ship.DamageControl = 100;
            ship.SystemDamage.Engines = 1;
            Fleet fleet = EntityFactory.CreateFleet("repair-fleet", "empire");
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return ship;
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <returns>The constructed scene.</returns>
        private (GameRoot game, Planet planet) BuildScene()
        {
            GameConfig config = TestConfig.Create();
            config.Jedi.FastHealThreshold = 80;
            config.Recovery = new GameConfig.RecoveryConfig
            {
                NormalHealAmount = 1,
                FastHealAmount = 3,
                NormalRepairAmount = 1,
                FastRepairAmount = 5,
                NormalReplacementIntervalTicks = 30,
                FastReplacementIntervalTicks = 2,
            };
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 1,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, planetSector);

            return (game, planet);
        }
    }
}
