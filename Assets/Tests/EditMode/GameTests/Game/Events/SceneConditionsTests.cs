using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class SceneConditionsTests
    {
        [Test]
        public void ShareParent_DifferentImmediateParents_DoesNotMatch()
        {
            GameRoot game = BuildHierarchy(
                out Planet planet,
                out Fleet fleet,
                out CapitalShip ship
            );
            Officer planetOfficer = EntityFactory.CreateOfficer("planet-officer", "faction");
            Officer shipOfficer = EntityFactory.CreateOfficer("ship-officer", "faction");
            game.AttachNode(planetOfficer, planet);
            game.AttachNode(shipOfficer, ship);
            ShareParentConditional condition = new ShareParentConditional
            {
                Units = References(planetOfficer, shipOfficer),
            };

            bool isMet = condition.IsMet(game);

            Assert.IsFalse(isMet);
            Assert.AreSame(planet, fleet.GetParent());
        }

        [Test]
        public void ShareAncestor_SamePlanetWithDifferentImmediateParents_Matches()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out CapitalShip ship);
            Officer planetOfficer = EntityFactory.CreateOfficer("planet-officer", "faction");
            Officer shipOfficer = EntityFactory.CreateOfficer("ship-officer", "faction");
            game.AttachNode(planetOfficer, planet);
            game.AttachNode(shipOfficer, ship);
            ShareAncestorConditional condition = new ShareAncestorConditional
            {
                Type = SceneAncestorType.Planet,
                Units = References(planetOfficer, shipOfficer),
            };

            bool isMet = condition.IsMet(game);

            Assert.IsTrue(isMet);
        }

        [Test]
        public void IsCapturedBy_UncapturedOfficerWithStaleCaptor_DoesNotMatch()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            officer.IsCaptured = false;
            officer.CaptorInstanceID = "captor";
            game.AttachNode(officer, planet);
            IsCapturedByConditional condition = new IsCapturedByConditional
            {
                OfficerInstanceID = officer.InstanceID,
                CaptorFactionInstanceID = "captor",
            };

            bool isMet = condition.IsMet(game);

            Assert.IsFalse(isMet);
        }

        [Test]
        public void IsKilled_InactiveKilledOfficer_MatchesByRegisteredIdentity()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, planet);
            new PersonnelSystem(game).KillOfficer(officer);
            IsKilledConditional condition = new IsKilledConditional
            {
                OfficerInstanceID = officer.InstanceID,
            };

            bool isMet = condition.IsMet(game);

            Assert.IsTrue(isMet);
        }

        [Test]
        public void HasForceRank_ConfiguredSemanticRank_UsesConfiguredMinimum()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            officer.ForceValue = game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight);
            game.AttachNode(officer, planet);
            HasForceRankConditional condition = new HasForceRankConditional
            {
                OfficerInstanceID = officer.InstanceID,
                Comparison = ComparisonOperator.GreaterThanOrEqual,
                Rank = ForceRankLabel.ForceKnight,
            };

            bool isMet = condition.IsMet(game);

            Assert.IsTrue(isMet);
        }

        private static GameRoot BuildHierarchy(
            out Planet planet,
            out Fleet fleet,
            out CapitalShip ship
        )
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "faction" });
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "faction",
                IsColonized = true,
            };
            fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "faction" };
            ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "faction",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return game;
        }

        private static List<EventUnitReference> References(params ISceneNode[] nodes) =>
            new List<EventUnitReference>(
                System.Array.ConvertAll(
                    nodes,
                    node => new EventUnitReference { UnitInstanceID = node.InstanceID }
                )
            );
    }
}
