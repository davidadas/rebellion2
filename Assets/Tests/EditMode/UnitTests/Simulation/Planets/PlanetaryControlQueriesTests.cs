using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class PlanetaryControlQueriesTests
    {
        private GameRoot _game;
        private Faction _empire;
        private Faction _rebels;
        private Planet _targetPlanet;
        private PlanetaryControlQueries _queries;

        /// <summary>Creates a planet and two factions for read-only control calculations.</summary>
        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _rebels = new Faction { InstanceID = "rebels" };
            _empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(_rebels);
            _game.GetFactions().Add(_empire);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _targetPlanet = new Planet { InstanceID = "planet", IsColonized = true };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(_targetPlanet, sector);
            _queries = new PlanetaryControlQueries(_game);
        }

        /// <summary>Verifies support ties retain faction-list ordering.</summary>
        [Test]
        public void GetPlanetController_EqualQualifyingSupport_ReturnsFirstFaction()
        {
            _game.Config.SupportShift.OwnershipTransferThreshold = 50;
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _empire.InstanceID, 50 },
                { _rebels.InstanceID, 50 },
            };

            Assert.AreSame(_rebels, _queries.GetPlanetController(_targetPlanet));
        }

        /// <summary>Verifies support-based control can be queried without transferring ownership.</summary>
        [Test]
        public void GetPlanetController_OtherFactionHasControllingSupport_DoesNotChangeOwner()
        {
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.SetPopularSupport(_rebels.InstanceID, 100);

            Assert.AreSame(_rebels, _queries.GetPlanetController(_targetPlanet));

            Assert.AreEqual(_empire.InstanceID, _targetPlanet.OwnerInstanceID);
        }

        /// <summary>Verifies an active garrison takes precedence over controlling support.</summary>
        [Test]
        public void GetPlanetController_ActiveGarrisonOpposesSupport_ReturnsGarrisonOwner()
        {
            AddRegiment("garrison", _empire);
            _targetPlanet.SetPopularSupport(_rebels.InstanceID, 100);

            Assert.AreSame(_empire, _queries.GetPlanetController(_targetPlanet));
        }

        /// <summary>Verifies unfinished and traveling regiments do not determine control.</summary>
        [Test]
        public void GetPlanetController_OnlyInactiveRegiments_ReturnsSupportController()
        {
            AddRegiment("unfinished", _empire).ManufacturingStatus = ManufacturingStatus.Building;
            AddRegiment("traveling", _empire).Movement = new MovementState { TransitTicks = 2 };
            _targetPlanet.SetPopularSupport(_rebels.InstanceID, 100);

            Assert.AreSame(_rebels, _queries.GetPlanetController(_targetPlanet));
        }

        /// <summary>Verifies opposing active garrisons prevent either faction controlling the planet.</summary>
        [Test]
        public void GetPlanetController_OpposingActiveGarrisons_ReturnsNoController()
        {
            AddRegiment("first", _empire);
            AddRegiment("second", _rebels);
            _targetPlanet.SetPopularSupport(_rebels.InstanceID, 100);

            Assert.IsNull(_queries.GetPlanetController(_targetPlanet));
        }

        /// <summary>Verifies outer-rim shifts bypass core-sector resistance.</summary>
        [Test]
        public void ApplyCoreSupportResistance_OuterRimPlanet_ReturnsUnadjustedShift()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _empire.Settings.SupportResistance = SupportChange.Increase;

            Assert.AreEqual(
                5,
                PlanetaryControlQueries.ApplyCoreSupportResistance(_targetPlanet, _empire, 5, 2)
            );
        }

        /// <summary>Verifies configured support resistance preserves signed integer division.</summary>
        /// <param name="resistance">The direction resisted by the faction.</param>
        /// <param name="shift">The requested support shift.</param>
        /// <param name="divisor">The configured divisor.</param>
        /// <param name="expected">The adjusted shift.</param>
        [TestCase(SupportChange.Increase, 5, 2, 2)]
        [TestCase(SupportChange.Decrease, -5, 2, -2)]
        [TestCase(SupportChange.Increase, -5, 2, -5)]
        [TestCase(SupportChange.Decrease, 5, 2, 5)]
        [TestCase(SupportChange.Increase, 5, 0, 5)]
        [TestCase(SupportChange.Increase, 0, 2, 0)]
        public void ApplyCoreSupportResistance_CorePlanet_ReturnsConfiguredAdjustment(
            SupportChange resistance,
            int shift,
            int divisor,
            int expected
        )
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.Core;
            _empire.Settings.SupportResistance = resistance;

            int adjusted = PlanetaryControlQueries.ApplyCoreSupportResistance(
                _targetPlanet,
                _empire,
                shift,
                divisor
            );

            Assert.AreEqual(expected, adjusted);
        }

        /// <summary>Places a completed regiment after setting its planet's ownership.</summary>
        /// <param name="id">The regiment identifier.</param>
        /// <param name="owner">The regiment's faction.</param>
        /// <returns>The attached regiment.</returns>
        private Regiment AddRegiment(string id, Faction owner)
        {
            _game.ChangeOwnership(_targetPlanet, owner.InstanceID);
            Regiment regiment = EntityFactory.CreateRegiment(id, owner.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, _targetPlanet);
            return regiment;
        }
    }
}
