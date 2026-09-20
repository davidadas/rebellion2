using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Director
{
    [TestFixture]
    public sealed class AIAttackRequirementsTests
    {
        [Test]
        public void GetCombatStrength_WithoutOrbitalDefenders_ReturnsMinimumStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1400;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            int required = context.AttackRequirements.GetCombatStrength(
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            Assert.AreEqual(1400, required);
        }

        [Test]
        public void GetCombatStrength_WithStaleIntelligence_IncreasesRequirement()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1000;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick =
                game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks * 3;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            int required = context.AttackRequirements.GetCombatStrength(
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            Assert.Greater(required, 1000);
        }

        [Test]
        public void GetCombatStrength_WithExtremelyStaleIntelligence_CapsRequirement()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1000;
            game.Config.AI.FleetDeployment.StaleIntelMaximumAttackStrengthPercent = 250;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = 10000;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            int required = context.AttackRequirements.GetCombatStrength(
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            Assert.AreEqual(2500, required);
        }
    }
}
