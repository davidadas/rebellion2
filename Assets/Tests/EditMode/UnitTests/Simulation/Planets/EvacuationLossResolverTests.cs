using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class EvacuationLossResolverTests
    {
        [Test]
        public void Resolve_HostileBlockade_RemovesRegimentAndReportsLoss()
        {
            (GameRoot game, Planet planet) = BuildScene();
            Regiment regiment = EntityFactory.CreateRegiment("evacuating", "empire");
            game.AttachNode(regiment, planet);
            game.Config.Blockade.CapitalShipProductionPenaltyPercent = 100;
            game.CurrentTick = 42;
            EvacuationLossResolver system = new EvacuationLossResolver(game, new FixedRNG());

            EvacuationLossesResult result = system.Resolve(regiment, planet);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
            Assert.AreSame(regiment, result.LostRegiments.Single());
            Assert.AreSame(regiment, result.DestroyedObject);
            Assert.AreSame(planet, result.Context);
            Assert.AreSame(planet, result.Location);
            Assert.AreEqual("empire", result.Faction.InstanceID);
            Assert.AreEqual(UnitDestructionReason.Blockade, result.Reason);
            Assert.AreEqual(42, result.Tick);
        }

        [Test]
        public void Resolve_OfficerUnderBlockade_DoesNotConsumeRandomValues()
        {
            (GameRoot game, Planet planet) = BuildScene();
            planet.IsColonized = true;
            Officer officer = new Officer { InstanceID = "officer", OwnerInstanceID = "empire" };
            game.AttachNode(officer, planet);
            EvacuationLossResolver system = new EvacuationLossResolver(game, new ThrowingRNG());

            EvacuationLossesResult result = system.Resolve(officer, planet);

            Assert.IsNull(result);
            Assert.AreSame(planet, officer.GetParent());
        }

        [Test]
        public void Resolve_RollBelowSurvivalThreshold_PreservesRegiment()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.Config.Blockade.CapitalShipProductionPenaltyPercent = 25;
            Regiment regiment = EntityFactory.CreateRegiment("evacuating", "empire");
            game.AttachNode(regiment, planet);
            EvacuationLossResolver system = new EvacuationLossResolver(game, new FixedRNG());

            EvacuationLossesResult result = system.Resolve(regiment, planet);

            Assert.IsNull(result);
            Assert.AreSame(regiment, game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        [Test]
        public void Resolve_RollAboveSurvivalThreshold_RemovesRegiment()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.Config.Blockade.CapitalShipProductionPenaltyPercent = 25;
            Regiment regiment = EntityFactory.CreateRegiment("evacuating", "empire");
            game.AttachNode(regiment, planet);
            EvacuationLossResolver system = new EvacuationLossResolver(game, new MaximumRNG());

            EvacuationLossesResult result = system.Resolve(regiment, planet);

            Assert.IsNotNull(result);
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        [Test]
        public void Resolve_NoOpposingBlockade_PreservesRegimentWithoutRolling()
        {
            (GameRoot game, Planet planet) = BuildScene();
            Regiment regiment = EntityFactory.CreateRegiment("evacuating", "empire");
            game.AttachNode(regiment, planet);
            planet.OwnerInstanceID = "alliance";
            EvacuationLossResolver system = new EvacuationLossResolver(game, new ThrowingRNG());

            EvacuationLossesResult result = system.Resolve(regiment, planet);

            Assert.IsNull(result);
            Assert.AreSame(regiment, game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        [Test]
        public void Resolve_OverwhelmingBlockade_RemovesRegiment()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.Config.Blockade.CapitalShipProductionPenaltyPercent = 100;
            Regiment regiment = EntityFactory.CreateRegiment("evacuating", "empire");
            game.AttachNode(regiment, planet);
            EvacuationLossResolver system = new EvacuationLossResolver(game, new FixedRNG());

            EvacuationLossesResult result = system.Resolve(regiment, planet);

            Assert.IsNotNull(result);
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        [Test]
        public void Resolve_NeutralPlanetBlockadingFaction_ReturnsNoLoss()
        {
            (GameRoot game, Planet planet) = BuildScene();
            planet.OwnerInstanceID = null;
            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "alliance",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, planet);
            EvacuationLossResolver system = new EvacuationLossResolver(game, new FixedRNG());

            EvacuationLossesResult result = system.Resolve(regiment, planet);

            Assert.IsNull(result);
            Assert.AreEqual(regiment, game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        [Test]
        public void Resolve_OperationalIonCannon_PreventsLoss()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.Config.Blockade.CapitalShipProductionPenaltyPercent = 100;
            planet.IsColonized = true;
            planet.EnergyCapacity = 1;
            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Building ionCannon = new Building
            {
                InstanceID = "ion-cannon",
                BuildingType = BuildingType.Weapon,
                DefenseWeaponEffect = DefenseWeaponEffect.ShieldDamage,
                ManufacturingStatus = ManufacturingStatus.Complete,
                OwnerInstanceID = "empire",
            };
            game.AttachNode(ionCannon, planet);
            game.AttachNode(regiment, planet);
            EvacuationLossResolver system = new EvacuationLossResolver(game, new FixedRNG());

            EvacuationLossesResult result = system.Resolve(regiment, planet);

            Assert.IsNull(result);
            Assert.AreSame(regiment, game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        [Test]
        public void Resolve_PreOwnershipChangeBlockade_UsesObservedBlockadeStrength()
        {
            (GameRoot game, Planet planet) = BuildScene();
            game.Config.Blockade.CapitalShipProductionPenaltyPercent = 100;
            Regiment regiment = EntityFactory.CreateRegiment("evacuating", "empire");
            game.AttachNode(regiment, planet);
            planet.OwnerInstanceID = "alliance";
            Assert.IsFalse(planet.IsBlockaded());
            EvacuationLossResolver system = new EvacuationLossResolver(game, new FixedRNG());

            EvacuationLossesResult result = system.Resolve(
                regiment,
                planet,
                opposingBlockadeAtDeparture: true
            );

            Assert.IsNotNull(result);
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        /// <summary>Builds a planet under an opposing blockade.</summary>
        /// <returns>The game and blockaded planet.</returns>
        private static (GameRoot game, Planet planet) BuildScene()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSector sector = new PlanetSector { InstanceID = "s1" };
            Planet planet = new Planet { InstanceID = "p1", OwnerInstanceID = "empire" };
            Fleet hostileFleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "alliance" };
            CapitalShip hostileShip = new CapitalShip
            {
                InstanceID = "hostile-ship",
                OwnerInstanceID = "alliance",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            game.AttachNode(hostileFleet, planet);
            game.AttachNode(hostileShip, hostileFleet);
            return (game, planet);
        }
    }
}
