using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Targeting
{
    [TestFixture]
    public class StrategyMissionTargetTests
    {
        [Test]
        public void Constructor_PlanetAndItem_StoresTargetState()
        {
            GalaxyMapPlanet planet = CreateMapPlanet("planet", "player");
            Officer officer = new Officer();

            StrategyMissionTarget target = new StrategyMissionTarget(planet, officer);

            Assert.AreSame(planet, target.Planet);
            Assert.AreSame(officer, target.Item);
            Assert.AreSame(target, target.Target);
        }

        [Test]
        public void GetMoveDestination_MissingPlanet_ReturnsNull()
        {
            StrategyMissionTarget target = new StrategyMissionTarget(null, new Officer());

            ISceneNode destination = target.GetMoveDestination();

            Assert.IsNull(destination);
        }

        [Test]
        public void GetMoveDestination_PlanetWithoutItem_ReturnsPlanet()
        {
            GalaxyMapPlanet mapPlanet = CreateMapPlanet("planet", "player");
            StrategyMissionTarget target = new StrategyMissionTarget(mapPlanet, null);

            ISceneNode destination = target.GetMoveDestination();

            Assert.AreSame(mapPlanet.Planet, destination);
        }

        [Test]
        public void GetMoveDestination_FleetOrCapitalShip_ReturnsItem()
        {
            GalaxyMapPlanet mapPlanet = CreateMapPlanet("planet", "player");
            GameFleet fleet = new GameFleet("player", "fleet");
            CapitalShip ship = new CapitalShip();

            ISceneNode fleetDestination = new StrategyMissionTarget(
                mapPlanet,
                fleet
            ).GetMoveDestination();
            ISceneNode shipDestination = new StrategyMissionTarget(
                mapPlanet,
                ship
            ).GetMoveDestination();

            Assert.AreSame(fleet, fleetDestination);
            Assert.AreSame(ship, shipDestination);
        }

        [Test]
        public void GetMoveDestination_ItemInsideFleetOrShip_ReturnsParent()
        {
            GalaxyMapPlanet mapPlanet = CreateMapPlanet("planet", "player");
            GameFleet fleet = new GameFleet("player", "fleet");
            CapitalShip ship = new CapitalShip();
            Officer fleetOfficer = new Officer();
            Officer shipOfficer = new Officer();
            fleetOfficer.SetParent(fleet);
            shipOfficer.SetParent(ship);

            ISceneNode fleetDestination = new StrategyMissionTarget(
                mapPlanet,
                fleetOfficer
            ).GetMoveDestination();
            ISceneNode shipDestination = new StrategyMissionTarget(
                mapPlanet,
                shipOfficer
            ).GetMoveDestination();

            Assert.AreSame(fleet, fleetDestination);
            Assert.AreSame(ship, shipDestination);
        }

        [Test]
        public void GetMoveDestination_OtherItem_ReturnsPlanet()
        {
            GalaxyMapPlanet mapPlanet = CreateMapPlanet("planet", "player");
            Officer officer = new Officer();
            StrategyMissionTarget target = new StrategyMissionTarget(mapPlanet, officer);

            ISceneNode destination = target.GetMoveDestination();

            Assert.AreSame(mapPlanet.Planet, destination);
        }

        [TestCase(MissionTypeIDs.Sabotage)]
        [TestCase(MissionTypeIDs.Abduction)]
        [TestCase(MissionTypeIDs.Assassination)]
        [TestCase(MissionTypeIDs.Rescue)]
        public void GetSpecificMissionTarget_TargetedMission_ReturnsItem(string missionTypeId)
        {
            Officer officer = new Officer();
            StrategyMissionTarget target = new StrategyMissionTarget(null, officer);

            ISceneNode specificTarget = target.GetSpecificMissionTarget(missionTypeId);

            Assert.AreSame(officer, specificTarget);
        }

        [Test]
        public void GetSpecificMissionTarget_MissingItemOrLocationMission_ReturnsNull()
        {
            StrategyMissionTarget missingItem = new StrategyMissionTarget(null, null);
            StrategyMissionTarget locationMission = new StrategyMissionTarget(null, new Officer());

            ISceneNode missingItemTarget = missingItem.GetSpecificMissionTarget(
                MissionTypeIDs.Sabotage
            );
            ISceneNode locationTarget = locationMission.GetSpecificMissionTarget(
                MissionTypeIDs.Diplomacy
            );

            Assert.IsNull(missingItemTarget);
            Assert.IsNull(locationTarget);
        }

        [TestCase(MissionTypeIDs.Abduction)]
        [TestCase(MissionTypeIDs.Assassination)]
        [TestCase(MissionTypeIDs.Rescue)]
        public void GetMissionTargetOfficer_OfficerDirectedMission_ReturnsOfficer(
            string missionTypeId
        )
        {
            Officer officer = new Officer();
            StrategyMissionTarget target = new StrategyMissionTarget(null, officer);

            Officer missionOfficer = target.GetMissionTargetOfficer(missionTypeId);

            Assert.AreSame(officer, missionOfficer);
        }

        [Test]
        public void GetMissionTargetOfficer_NonOfficerItemOrMission_ReturnsNull()
        {
            StrategyMissionTarget nonOfficer = new StrategyMissionTarget(null, new SpecialForces());
            StrategyMissionTarget nonOfficerMission = new StrategyMissionTarget(
                null,
                new Officer()
            );

            Officer wrongItem = nonOfficer.GetMissionTargetOfficer(MissionTypeIDs.Abduction);
            Officer wrongMission = nonOfficerMission.GetMissionTargetOfficer(
                MissionTypeIDs.Sabotage
            );

            Assert.IsNull(wrongItem);
            Assert.IsNull(wrongMission);
        }

        private static GalaxyMapPlanet CreateMapPlanet(string instanceId, string ownerId)
        {
            Planet planet = new Planet { InstanceID = instanceId, OwnerInstanceID = ownerId };
            return new GalaxyMapPlanet(new GamePlanetSystem(), planet, string.Empty);
        }
    }
}
