using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Director
{
    [TestFixture]
    public class AIAssessmentTests
    {
        [Test]
        public void Constructor_WithMixedPlanetOwnership_BuildsOwnershipLists()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "owned",
                empire.InstanceID
            );
            Planet enemy = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "enemy",
                rebels.InstanceID
            );
            Planet neutral = AITestSceneBuilder.AddPlanet(game, planetSector, "neutral", null);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            CollectionAssert.Contains(assessment.OwnedPlanets, owned);
            CollectionAssert.Contains(assessment.EnemyPlanets, enemy);
            CollectionAssert.Contains(assessment.NeutralPlanets, neutral);
        }

        [Test]
        public void Constructor_WithEnemyOfficer_BuildsTargetableEnemyOfficerTargets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            AITestSceneBuilder.AddPlanet(game, planetSector, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "enemy",
                rebels.InstanceID
            );
            Officer target = EntityFactory.CreateOfficer("target", rebels.InstanceID);
            game.AttachNode(target, enemy);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1, assessment.TargetableEnemyOfficerMissionTargets.Count);
            Assert.AreSame(enemy, assessment.TargetableEnemyOfficerMissionTargets[0].Planet);
            Assert.AreSame(
                target,
                assessment.TargetableEnemyOfficerMissionTargets[0].TargetOfficer
            );
        }

        [Test]
        public void GetAttackTargetPlanet_EnemyAttackOrder_ReturnsTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "owned",
                empire.InstanceID
            );
            Planet enemy = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "enemy",
                rebels.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = enemy.InstanceID,
            };
            game.AttachNode(fleet, owned);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Planet target = assessment.GetAttackTargetPlanet(fleet);

            Assert.AreSame(enemy, target);
        }

        [Test]
        public void GetAttackTargetPlanet_NonEnemyAttackOrder_ReturnsNull()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "owned",
                empire.InstanceID
            );
            Planet enemy = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "enemy",
                rebels.InstanceID
            );
            Planet neutral = AITestSceneBuilder.AddPlanet(game, planetSector, "neutral", null);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, owned);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                TargetPlanetId = enemy.InstanceID,
            };
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));

            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = owned.InstanceID,
            };
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));

            fleet.Order.TargetPlanetId = neutral.InstanceID;
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));

            fleet.Order.TargetPlanetId = "missing";
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));
        }

        [Test]
        public void GetFleetBombardmentStrength_GeneralAboardCapitalShip_AppliesLeadership()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out _);
            game.Config.Combat.Bombardment.AttackerLeadershipDivisor = 10;
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "owned",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip capitalShip = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID
            );
            capitalShip.Bombardment = 10;
            Officer general = EntityFactory.CreateOfficer("general", empire.InstanceID);
            general.CurrentRank = OfficerRank.General;
            general.Ratings[OfficerRating.Leadership] = 20;
            game.AttachNode(fleet, planet);
            game.AttachNode(capitalShip, fleet);
            game.AttachNode(general, capitalShip);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            int strength = assessment.GetFleetBombardmentStrength(fleet);

            Assert.AreEqual(30, strength);
        }
    }
}
