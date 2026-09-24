using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Helpers
{
    /// <summary>
    /// Builds scenes shared by faction visibility and snapshot tests.
    /// </summary>
    public abstract class FogOfWarTestBase
    {
        protected GameRoot _game;
        protected Faction _alliance;
        protected Faction _empire;
        protected PlanetSector _coreSector;
        protected PlanetSector _outerRim;
        protected Planet _coruscant;
        protected Planet _tatooine;
        protected Planet _hoth;

        /// <summary>
        /// Creates the shared scene for visibility and intelligence tests.
        /// </summary>
        [SetUp]
        public void SetUpScene()
        {
            GameConfig config = new GameConfig();
            _game = new GameRoot(config);

            _alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            _empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            _game.GetFactions().Add(_alliance);
            _game.GetFactions().Add(_empire);

            _coreSector = new PlanetSector
            {
                InstanceID = "CORE_SECTOR",
                DisplayName = "Core Sector",
                SectorType = PlanetSectorType.Core,
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(_coreSector, _game.GetGalaxyMap());

            _outerRim = new PlanetSector
            {
                InstanceID = "OUTERRIM",
                DisplayName = "Outer Rim Sector",
                SectorType = PlanetSectorType.OuterRim,
                PositionX = 100,
                PositionY = 100,
            };
            _game.AttachNode(_outerRim, _game.GetGalaxyMap());

            _coruscant = new Planet
            {
                InstanceID = "CORUSCANT",
                DisplayName = "Coruscant",
                OwnerInstanceID = "FNEMP1",
                IsColonized = true,
            };
            _game.AttachNode(_coruscant, _coreSector);

            _tatooine = new Planet
            {
                InstanceID = "TATOOINE",
                DisplayName = "Tatooine",
                OwnerInstanceID = null,
            };
            _game.AttachNode(_tatooine, _outerRim);

            _hoth = new Planet
            {
                InstanceID = "HOTH",
                DisplayName = "Hoth",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            _game.AttachNode(_hoth, _outerRim);
        }

        /// <summary>
        /// Creates officer.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="faction">The faction.</param>
        /// <returns>The created officer.</returns>
        protected Officer CreateOfficer(string id, Faction faction) =>
            EntityFactory.CreateOfficer(id, faction.InstanceID);

        /// <summary>
        /// Adds queued building.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="faction">The faction.</param>
        /// <param name="id">The id.</param>
        /// <param name="progress">The progress.</param>
        /// <returns>The result of add queued building.</returns>
        protected Building AddQueuedBuilding(
            Planet planet,
            Faction faction,
            string id,
            int progress
        )
        {
            Building building = CreateBuilding(id, faction, ManufacturingStatus.Building);
            building.ProducerOwnerID = faction.InstanceID;
            building.ProducerPlanetID = planet.InstanceID;
            building.ManufacturingProgress = progress;
            planet.EnergyCapacity = planet.GetChildren<Building>().Count + 1;
            _game.AttachNode(building, planet);
            planet.AddToManufacturingQueue(building);
            return building;
        }

        /// <summary>
        /// Executes make tatooine imperial.
        /// </summary>
        protected void MakeTatooineImperial()
        {
            _tatooine.OwnerInstanceID = _empire.InstanceID;
            _tatooine.IsColonized = true;
        }

        /// <summary>
        /// Creates fleet.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="faction">The faction.</param>
        /// <returns>The created fleet.</returns>
        protected Fleet CreateFleet(string id, Faction faction) =>
            EntityFactory.CreateFleet(id, faction.InstanceID);

        /// <summary>
        /// Adds capital ship.
        /// </summary>
        /// <param name="fleet">The fleet.</param>
        /// <param name="faction">The faction.</param>
        /// <param name="id">The id.</param>
        /// <returns>The result of add capital ship.</returns>
        protected CapitalShip AddCapitalShip(Fleet fleet, Faction faction, string id)
        {
            CapitalShip ship = new CapitalShip
            {
                InstanceID = id,
                OwnerInstanceID = faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(ship, fleet);
            return ship;
        }

        /// <summary>
        /// Creates regiment.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="faction">The faction.</param>
        /// <returns>The created regiment.</returns>
        protected Regiment CreateRegiment(string id, Faction faction)
        {
            Regiment regiment = EntityFactory.CreateRegiment(id, faction.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            return regiment;
        }

        /// <summary>
        /// Creates building.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="faction">The faction.</param>
        /// <param name="status">The status.</param>
        /// <returns>The created building.</returns>
        protected Building CreateBuilding(
            string id,
            Faction faction,
            ManufacturingStatus status = ManufacturingStatus.Complete
        )
        {
            Building building = EntityFactory.CreateBuilding(id, faction.InstanceID);
            building.ManufacturingStatus = status;
            return building;
        }

        /// <summary>
        /// Creates starfighter.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="faction">The faction.</param>
        /// <returns>The created starfighter.</returns>
        protected Starfighter CreateStarfighter(string id, Faction faction)
        {
            Starfighter starfighter = EntityFactory.CreateStarfighter(id, faction.InstanceID);
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            return starfighter;
        }

        /// <summary>
        /// Creates mission.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="owner">The owner.</param>
        /// <param name="target">The target.</param>
        /// <returns>The created mission.</returns>
        protected StubMission CreateMission(string id, Faction owner, Planet target) =>
            EntityFactory.CreateMission(id, owner.InstanceID, target.InstanceID);
    }
}
