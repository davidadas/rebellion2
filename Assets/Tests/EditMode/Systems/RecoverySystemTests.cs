using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class RecoverySystemTests
    {
        private (GameRoot game, Planet planet) BuildScene()
        {
            GameConfig config = TestConfig.Create();
            config.Recovery = new GameConfig.RecoveryConfig
            {
                NormalHealAmount = 1,
                FastHealAmount = 3,
                NormalRepairAmount = 1,
                FastRepairAmount = 5,
                NormalReplacementAmount = 1,
                FastReplacementAmount = 2,
            };
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            return (game, planet);
        }

        [Test]
        public void ProcessTick_InjuredOfficerCanHeal_ReducesInjury()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            officer.CanHeal = true;
            game.AttachNode(officer, planet);

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

            Assert.AreEqual(9, officer.InjuryPoints, "Officer should heal 1 point per tick");
        }

        [Test]
        public void ProcessTick_InjuredOfficerFastHeal_HealsMorePerTick()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            officer.CanHeal = true;
            officer.FastHeal = true;
            game.AttachNode(officer, planet);

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

            Assert.AreEqual(
                7,
                officer.InjuryPoints,
                "FastHeal officer should heal 3 points per tick"
            );
        }

        [Test]
        public void ProcessTick_InjuredOfficerCannotHeal_NoChange()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            officer.CanHeal = false;
            game.AttachNode(officer, planet);

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

            Assert.AreEqual(10, officer.InjuryPoints, "Officer with CanHeal=false should not heal");
        }

        [Test]
        public void ProcessTick_CapturedOfficer_DoesNotHeal()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 10;
            officer.CanHeal = true;
            officer.IsCaptured = true;
            game.AttachNode(officer, planet);

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

            Assert.AreEqual(10, officer.InjuryPoints, "Captured officer should not heal");
        }

        [Test]
        public void ProcessTick_OfficerFullyHealed_EmitsResult()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.InjuryPoints = 1;
            officer.CanHeal = true;
            game.AttachNode(officer, planet);

            RecoverySystem system = new RecoverySystem(game);

            List<GameResult> results = system.ProcessTick();

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
            officer.CanHeal = true;
            game.AttachNode(officer, planet);

            RecoverySystem system = new RecoverySystem(game);

            List<GameResult> results = system.ProcessTick();

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
            officer.CanHeal = true;
            officer.FastHeal = true;
            game.AttachNode(officer, planet);

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

            Assert.AreEqual(0, officer.InjuryPoints, "Injury should clamp to 0, not go negative");
        }

        [Test]
        public void ProcessTick_DamagedShipAtFriendlyPlanet_RepairsFast()
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

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

            Assert.AreEqual(
                85,
                ship.CurrentHullStrength,
                "Ship at friendly planet should repair 5 per tick"
            );
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

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

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

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

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

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

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

            RecoverySystem system = new RecoverySystem(game);

            List<GameResult> results = system.ProcessTick();

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

            RecoverySystem system = new RecoverySystem(game);

            List<GameResult> results = system.ProcessTick();

            Assert.IsFalse(
                results.Any(r => r is ShipHullDamageResult),
                "Partially repaired ship should not produce result"
            );
        }

        [Test]
        public void ProcessTick_DepletedSquadronAtFriendlyPlanet_ReplacesFast()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 8,
            };
            game.AttachNode(squadron, planet);

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

            Assert.AreEqual(
                10,
                squadron.CurrentSquadronSize,
                "Squadron at friendly planet should replace 2 fighters per tick"
            );
        }

        [Test]
        public void ProcessTick_DepletedSquadronAtEnemyPlanet_ReplacesSlowly()
        {
            (GameRoot game, Planet planet) = BuildScene();
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

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

            Assert.AreEqual(
                9,
                squadron.CurrentSquadronSize,
                "Squadron at enemy planet should replace 1 fighter per tick"
            );
        }

        [Test]
        public void ProcessTick_FullSquadron_NoChange()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
            };
            game.AttachNode(squadron, planet);

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

            Assert.AreEqual(12, squadron.CurrentSquadronSize, "Full squadron should not change");
        }

        [Test]
        public void ProcessTick_ReplacementClampsToMax_DoesNotExceedSquadronSize()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 11,
            };
            game.AttachNode(squadron, planet);

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

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

            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 11,
            };
            game.AttachNode(squadron, planet);

            RecoverySystem system = new RecoverySystem(game);

            List<GameResult> results = system.ProcessTick();

            Assert.IsTrue(
                results.Any(r => r is FighterDamageResult),
                "Fully replaced squadron should produce result"
            );
        }

        [Test]
        public void ProcessTick_SquadronPartiallyReplaced_NoResult()
        {
            (GameRoot game, Planet planet) = BuildScene();

            Starfighter squadron = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 8,
            };
            game.AttachNode(squadron, planet);

            RecoverySystem system = new RecoverySystem(game);

            List<GameResult> results = system.ProcessTick();

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

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

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

            RecoverySystem system = new RecoverySystem(game);

            system.ProcessTick();

            Assert.AreEqual(
                8,
                squadron.CurrentSquadronSize,
                "Squadron still under construction should not have fighters replaced"
            );
        }
    }
}
