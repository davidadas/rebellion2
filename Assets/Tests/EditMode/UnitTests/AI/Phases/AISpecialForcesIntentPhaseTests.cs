using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AISpecialForcesIntentPhaseTests
    {
        /// <summary>
        /// Verifies execute with one replaceable unit retains it as a primary agent.
        /// </summary>
        [Test]
        public void Execute_WithOneOfficerReplaceableUnit_RetainsPrimaryAgent()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            SpecialForces specialForces = CreateSpecialForces(
                "special-forces",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            game.AttachNode(officer, planet);
            game.AttachNode(specialForces, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            new AISpecialForcesIntentPhase().Execute(context);

            Assert.AreEqual(
                SpecialForcesIntent.PrimaryAgent,
                context.GetSpecialForcesIntent(specialForces)
            );
        }

        /// <summary>
        /// Verifies execute without officer replacement keeps special forces as primary agent.
        /// </summary>
        [Test]
        public void Execute_WithoutOfficerReplacement_KeepsSpecialForcesAsPrimaryAgent()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            SpecialForces specialForces = CreateSpecialForces(
                "special-forces",
                empire.InstanceID,
                MissionTypeIDs.Reconnaissance
            );
            game.AttachNode(officer, planet);
            game.AttachNode(specialForces, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            new AISpecialForcesIntentPhase().Execute(context);

            Assert.AreEqual(
                SpecialForcesIntent.PrimaryAgent,
                context.GetSpecialForcesIntent(specialForces)
            );
        }

        /// <summary>
        /// Verifies execute with multiple replaceable units retains one primary agent.
        /// </summary>
        [Test]
        public void Execute_WithMultipleReplaceableUnits_RetainsOnePrimaryAgent()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            SpecialForces first = CreateSpecialForces(
                "special-forces-1",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            SpecialForces second = CreateSpecialForces(
                "special-forces-2",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            SpecialForces third = CreateSpecialForces(
                "special-forces-3",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            game.AttachNode(officer, planet);
            game.AttachNode(first, planet);
            game.AttachNode(second, planet);
            game.AttachNode(third, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            new AISpecialForcesIntentPhase().Execute(context);

            Assert.AreEqual(
                SpecialForcesIntent.PrimaryAgent,
                context.GetSpecialForcesIntent(first)
            );
            Assert.AreEqual(SpecialForcesIntent.Decoy, context.GetSpecialForcesIntent(second));
            Assert.AreEqual(SpecialForcesIntent.Decoy, context.GetSpecialForcesIntent(third));
        }

        /// <summary>
        /// Verifies execute with partially replaceable role keeps special forces as primary agent.
        /// </summary>
        [Test]
        public void Execute_WithPartiallyReplaceableRole_KeepsSpecialForcesAsPrimaryAgent()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            SpecialForces specialForces = CreateSpecialForces(
                "special-forces",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            specialForces.AllowedMissionTypeIDs.Add(MissionTypeIDs.Reconnaissance);
            game.AttachNode(officer, planet);
            game.AttachNode(specialForces, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            new AISpecialForcesIntentPhase().Execute(context);

            Assert.AreEqual(
                SpecialForcesIntent.PrimaryAgent,
                context.GetSpecialForcesIntent(specialForces)
            );
        }

        /// <summary>
        /// Creates special forces.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="missionTypeId">The mission type id.</param>
        /// <returns>The created special forces.</returns>
        private static SpecialForces CreateSpecialForces(
            string instanceId,
            string ownerInstanceId,
            string missionTypeId
        )
        {
            return new SpecialForces
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { missionTypeId },
            };
        }
    }
}
