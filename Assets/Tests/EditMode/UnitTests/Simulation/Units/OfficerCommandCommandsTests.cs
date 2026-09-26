using System.Collections.Generic;
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
    public class OfficerCommandCommandsTests
    {
        private const string _ownerId = "alliance";

        [Test]
        public void TrySetRank_EligibleOfficer_AssignsPostAndPublishesCommandKind()
        {
            GameRoot game = BuildScene(out Planet planet);
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = _ownerId };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            Officer officer = CreateOfficer(game, ship, "candidate", OfficerRank.Admiral);
            OfficerCommandCommands commands = new OfficerCommandCommands(game);
            List<GameResult> published = null;
            commands.ResultsProduced += results => published = results.ToList();

            bool changed = commands.TrySetRank(officer.InstanceID, OfficerRank.Admiral, _ownerId);

            Assert.IsTrue(changed);
            Assert.AreEqual(OfficerRank.Admiral, officer.CurrentRank);
            Assert.AreEqual("Admiral candidate", officer.GetDisplayName());
            Assert.AreEqual(2, (int)OfficerRank.Admiral);
            CommandKindChangedResult rankResult = published
                .OfType<CommandKindChangedResult>()
                .Single();
            Assert.AreEqual(2, rankResult.CommandKind);
            Assert.AreEqual((int)OfficerRank.None, rankResult.Detail);
            Assert.AreSame(
                fleet,
                published.OfType<OfficerCommandingResult>().Single().CommandTarget
            );
        }

        [Test]
        public void TrySetRank_None_RemovesCommandTitleFromDisplayName()
        {
            GameRoot game = BuildScene(out Planet planet);
            Officer officer = CreateOfficer(game, planet, "candidate", OfficerRank.Admiral);
            officer.CurrentRank = OfficerRank.Admiral;

            bool changed = new OfficerCommandCommands(game).TrySetRank(
                officer.InstanceID,
                OfficerRank.None,
                _ownerId
            );

            Assert.IsTrue(changed);
            Assert.AreEqual("candidate", officer.GetDisplayName());
        }

        [Test]
        public void TrySetRank_ActiveRank_ResignsFromPost()
        {
            GameRoot game = BuildScene(out Planet planet);
            Officer officer = CreateOfficer(game, planet, "candidate", OfficerRank.Commander);
            officer.CurrentRank = OfficerRank.Commander;

            bool changed = new OfficerCommandCommands(game).TrySetRank(
                officer.InstanceID,
                OfficerRank.Commander,
                _ownerId
            );

            Assert.IsTrue(changed);
            Assert.AreEqual(OfficerRank.None, officer.CurrentRank);
        }

        [Test]
        public void TrySetRank_PostAlreadyFilled_ReplacesIncumbentInSamePlanetaryCommand()
        {
            GameRoot game = BuildScene(out Planet planet);
            Officer incumbent = CreateOfficer(game, planet, "incumbent", OfficerRank.General);
            incumbent.CurrentRank = OfficerRank.General;
            Officer replacement = CreateOfficer(game, planet, "replacement", OfficerRank.General);

            bool changed = new OfficerCommandCommands(game).TrySetRank(
                replacement.InstanceID,
                OfficerRank.General,
                _ownerId
            );

            Assert.IsTrue(changed);
            Assert.AreEqual(OfficerRank.None, incumbent.CurrentRank);
            Assert.AreEqual(OfficerRank.General, replacement.CurrentRank);
        }

        [Test]
        public void TrySetRank_PlanetAndOrbitingFleet_HaveIndependentCommandPosts()
        {
            GameRoot game = BuildScene(out Planet planet);
            Officer systemGeneral = CreateOfficer(
                game,
                planet,
                "system-general",
                OfficerRank.General
            );
            systemGeneral.CurrentRank = OfficerRank.General;
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = _ownerId };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            Officer fleetGeneral = CreateOfficer(game, ship, "fleet-general", OfficerRank.General);

            bool changed = new OfficerCommandCommands(game).TrySetRank(
                fleetGeneral.InstanceID,
                OfficerRank.General,
                _ownerId
            );

            Assert.IsTrue(changed);
            Assert.AreEqual(OfficerRank.General, systemGeneral.CurrentRank);
            Assert.AreEqual(OfficerRank.General, fleetGeneral.CurrentRank);
        }

        [Test]
        public void CanSetRank_AdmiralAtPlanet_IsRejected()
        {
            GameRoot game = BuildScene(out Planet planet);
            Officer officer = CreateOfficer(game, planet, "candidate", OfficerRank.Admiral);

            bool allowed = new OfficerCommandCommands(game).CanSetRank(
                officer,
                OfficerRank.Admiral,
                _ownerId
            );

            Assert.IsFalse(allowed);
        }

        [Test]
        public void TrySetRank_IneligibleRank_IsRejectedWithoutChangingCurrentPost()
        {
            GameRoot game = BuildScene(out Planet planet);
            Officer officer = CreateOfficer(game, planet, "candidate", OfficerRank.General);
            officer.CurrentRank = OfficerRank.General;

            bool changed = new OfficerCommandCommands(game).TrySetRank(
                officer.InstanceID,
                OfficerRank.Admiral,
                _ownerId
            );

            Assert.IsFalse(changed);
            Assert.AreEqual(OfficerRank.General, officer.CurrentRank);
        }

        [TestCase(true, false, false, 0)]
        [TestCase(false, true, false, 0)]
        [TestCase(false, false, true, 0)]
        [TestCase(false, false, false, 1)]
        public void CanSetRank_UnavailableOfficer_IsRejected(
            bool captured,
            bool killed,
            bool retired,
            int injuryPoints
        )
        {
            GameRoot game = BuildScene(out Planet planet);
            Officer officer = CreateOfficer(game, planet, "candidate", OfficerRank.Commander);
            officer.IsCaptured = captured;
            officer.IsKilled = killed;
            officer.IsRetired = retired;
            officer.InjuryPoints = injuryPoints;

            bool allowed = new OfficerCommandCommands(game).CanSetRank(
                officer,
                OfficerRank.Commander,
                _ownerId
            );

            Assert.IsFalse(allowed);
        }

        /// <summary>
        /// Builds a minimal deployed game graph.
        /// </summary>
        /// <param name="planet">Receives the command planet.</param>
        /// <returns>The game graph.</returns>
        private static GameRoot BuildScene(out Planet planet)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _ownerId });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _ownerId,
                IsColonized = true,
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            return game;
        }

        /// <summary>
        /// Creates an officer with one allowed command post and attaches it to a live container.
        /// </summary>
        /// <param name="game">The game graph.</param>
        /// <param name="parent">The deployment container.</param>
        /// <param name="instanceId">The officer identifier.</param>
        /// <param name="allowedRank">The allowed post.</param>
        /// <returns>The attached officer.</returns>
        private static Officer CreateOfficer(
            GameRoot game,
            Rebellion.SceneGraph.ContainerNode parent,
            string instanceId,
            OfficerRank allowedRank
        )
        {
            Officer officer = EntityFactory.CreateOfficer(instanceId, _ownerId);
            officer.AllowedRanks = new[] { allowedRank };
            game.AttachNode(officer, parent);
            return officer;
        }
    }
}
