using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Targeting
{
    [TestFixture]
    public class StrategyMissionTargetTests
    {
        /// <summary>
        /// Verifies get move destination missing planet returns null.
        /// </summary>
        [Test]
        public void GetMoveDestination_MissingPlanet_ReturnsNull()
        {
            StrategyMissionTarget target = new StrategyMissionTarget(null, new Officer());

            ISceneNode destination = target.GetMoveDestination();

            Assert.IsNull(destination);
        }

        /// <summary>
        /// Verifies get move destination planet without item returns planet.
        /// </summary>
        [Test]
        public void GetMoveDestination_PlanetWithoutItem_ReturnsPlanet()
        {
            GalaxyMapPlanet mapPlanet = CreateMapPlanet("planet", "player");
            StrategyMissionTarget target = new StrategyMissionTarget(mapPlanet, null);

            ISceneNode destination = target.GetMoveDestination();

            Assert.AreSame(mapPlanet.Planet, destination);
        }

        /// <summary>
        /// Verifies get move destination fleet or capital ship returns item.
        /// </summary>
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

        /// <summary>
        /// Verifies get move destination item inside fleet or ship returns parent.
        /// </summary>
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

        /// <summary>
        /// Verifies get move destination other item returns planet.
        /// </summary>
        [Test]
        public void GetMoveDestination_OtherItem_ReturnsPlanet()
        {
            GalaxyMapPlanet mapPlanet = CreateMapPlanet("planet", "player");
            Officer officer = new Officer();
            StrategyMissionTarget target = new StrategyMissionTarget(mapPlanet, officer);

            ISceneNode destination = target.GetMoveDestination();

            Assert.AreSame(mapPlanet.Planet, destination);
        }

        /// <summary>
        /// Verifies get mission target officer target returns officer.
        /// </summary>
        [Test]
        public void GetMissionTarget_OfficerTarget_ReturnsOfficer()
        {
            Officer officer = new Officer();
            StrategyMissionTarget target = new StrategyMissionTarget(null, officer);

            ISceneNode specificTarget = target.GetMissionTarget(MissionTargetKind.Officer);

            Assert.AreSame(officer, specificTarget);
        }

        /// <summary>
        /// Verifies get mission target location mission returns planet.
        /// </summary>
        [Test]
        public void GetMissionTarget_LocationMission_ReturnsPlanet()
        {
            GalaxyMapPlanet mapPlanet = CreateMapPlanet("planet", "player");
            StrategyMissionTarget locationMission = new StrategyMissionTarget(
                mapPlanet,
                mapPlanet.Planet
            );

            ISceneNode locationTarget = locationMission.GetMissionTarget(MissionTargetKind.Planet);

            Assert.AreSame(mapPlanet.Planet, locationTarget);
        }

        /// <summary>
        /// Verifies get mission target planet mission from capital ship returns null.
        /// </summary>
        [Test]
        public void GetMissionTarget_PlanetMissionFromCapitalShip_ReturnsNull()
        {
            GalaxyMapPlanet mapPlanet = CreateMapPlanet("planet", "player");
            StrategyMissionTarget target = new StrategyMissionTarget(mapPlanet, new CapitalShip());

            ISceneNode missionTarget = target.GetMissionTarget(MissionTargetKind.Planet);

            Assert.IsNull(missionTarget);
        }

        /// <summary>
        /// Verifies get mission target targeted mission without item returns null.
        /// </summary>
        [Test]
        public void GetMissionTarget_TargetedMissionWithoutItem_ReturnsNull()
        {
            GalaxyMapPlanet mapPlanet = CreateMapPlanet("planet", "player");
            StrategyMissionTarget target = new StrategyMissionTarget(mapPlanet, null);

            ISceneNode missionTarget = target.GetMissionTarget(MissionTargetKind.Manufacturable);

            Assert.IsNull(missionTarget);
        }

        /// <summary>
        /// Creates map planet.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <returns>The created map planet.</returns>
        private static GalaxyMapPlanet CreateMapPlanet(string instanceId, string ownerId)
        {
            Planet planet = new Planet { InstanceID = instanceId, OwnerInstanceID = ownerId };
            return new GalaxyMapPlanet(new GalaxyPlanetSector(), planet, string.Empty);
        }
    }
}
