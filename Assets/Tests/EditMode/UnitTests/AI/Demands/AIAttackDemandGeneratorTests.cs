using NUnit.Framework;
using Rebellion.AI;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Demands
{
    [TestFixture]
    public sealed class AIAttackDemandGeneratorTests
    {
        [Test]
        public void BuildDemands_WithoutOrbitalDefenders_UsesMinimumCombatStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1400;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(1400, context.GetAttackDemand(target).CombatStrength);
        }

        [Test]
        public void BuildDemands_WithStaleIntelligence_IncreasesCombatStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1000;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick =
                game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks * 3;

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.Greater(context.GetAttackDemand(target).CombatStrength, 1000);
        }

        [Test]
        public void BuildDemands_WithExtremelyStaleIntelligence_CapsCombatStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1000;
            game.Config.AI.FleetDeployment.StaleIntelMaximumAttackStrengthPercent = 250;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = 10000;

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(2500, context.GetAttackDemand(target).CombatStrength);
        }

        [Test]
        public void BuildDemands_WithHostileFleetElsewhereInSystem_UsesTargetDefense()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 150;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Planet defended = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defended",
                rebels.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", rebels.InstanceID);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                rebels.InstanceID,
                combatStrength: 1000
            );
            game.AttachNode(fleet, defended);
            game.AttachNode(ship, fleet);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITestSceneBuilder.RevealPlanet(game, empire, defended);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(100, context.GetAttackDemand(target).CombatStrength);
        }

        [Test]
        public void BuildDemands_WithHostileFleetAtTarget_UsesLocalDefense()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 150;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", rebels.InstanceID);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                rebels.InstanceID,
                combatStrength: 1000
            );
            game.AttachNode(fleet, target);
            game.AttachNode(ship, fleet);
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(1500, context.GetAttackDemand(target).CombatStrength);
        }
    }
}
