using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class HeadquartersQueriesTests
    {
        [Test]
        public void CanRelocate_FriendlyDestination_LeavesHeadquartersUnchanged()
        {
            (GameRoot game, Faction faction, Planet origin, Planet destination, Building hq) =
                CreateGame(isMobile: true);

            bool allowed = new HeadquartersQueries(game).CanRelocate(hq, destination);

            Assert.IsTrue(allowed);
            Assert.IsNull(hq.Movement);
            Assert.AreSame(origin, hq.GetParent());
            Assert.IsTrue(origin.IsHeadquarters);
            Assert.AreEqual(origin.InstanceID, faction.HQInstanceID);
        }

        [TestCase("origin")]
        [TestCase("enemy")]
        [TestCase("destroyed")]
        [TestCase("uncolonized")]
        [TestCase("full")]
        public void CanRelocate_IneligibleDestination_ReturnsFalse(string reason)
        {
            (GameRoot game, _, Planet origin, Planet destination, Building hq) = CreateGame(
                isMobile: true
            );
            switch (reason)
            {
                case "origin":
                    destination = origin;
                    break;
                case "enemy":
                    destination.OwnerInstanceID = "enemy";
                    break;
                case "destroyed":
                    destination.IsDestroyed = true;
                    break;
                case "uncolonized":
                    destination.IsColonized = false;
                    break;
                case "full":
                    destination.EnergyCapacity = 0;
                    break;
            }

            bool allowed = new HeadquartersQueries(game).CanRelocate(hq, destination);

            Assert.IsFalse(allowed);
            Assert.IsNull(hq.Movement);
        }

        [Test]
        public void CanRelocate_HeadquartersInTransit_ReturnsFalse()
        {
            (GameRoot game, _, _, Planet destination, Building hq) = CreateGame(isMobile: true);
            hq.Movement = new MovementState();

            Assert.IsFalse(new HeadquartersQueries(game).CanRelocate(hq, destination));
        }

        /// <summary>Creates a faction with headquarters and two registered planets.</summary>
        /// <param name="isMobile">Whether the headquarters may relocate.</param>
        /// <returns>The game, faction, origin, destination and headquarters building.</returns>
        private static (GameRoot, Faction, Planet, Planet, Building) CreateGame(bool isMobile)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction
            {
                InstanceID = "alliance",
                HQInstanceID = "origin",
                Settings = new FactionSettings
                {
                    Headquarters = new HeadquartersSettings
                    {
                        FacilityTypeID = "BDHQ01",
                        IsMobile = isMobile,
                    },
                },
            };
            game.GetFactions().Add(faction);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(planetSector, game.GetGalaxyMap());
            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                IsHeadquarters = true,
                EnergyCapacity = 1,
            };
            Planet destination = new Planet
            {
                InstanceID = "destination",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                EnergyCapacity = 2,
                PositionX = 100,
            };
            game.AttachNode(origin, planetSector);
            game.AttachNode(destination, planetSector);

            Building headquarters = new Building
            {
                InstanceID = "headquarters",
                TypeID = "BDHQ01",
                OwnerInstanceID = faction.InstanceID,
                BuildingType = BuildingType.Headquarters,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(headquarters, origin);
            return (game, faction, origin, destination, headquarters);
        }
    }
}
