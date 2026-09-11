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
        [Test]
        public void Execute_WithOfficerReplacement_ReservesSpecialForcesAsDecoy()
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
                SpecialForcesIntent.Decoy,
                context.GetSpecialForcesIntent(specialForces)
            );
        }

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

        [Test]
        public void Execute_WithMultipleReplaceableUnits_AssignsAllAsDecoys()
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

            Assert.AreEqual(SpecialForcesIntent.Decoy, context.GetSpecialForcesIntent(first));
            Assert.AreEqual(SpecialForcesIntent.Decoy, context.GetSpecialForcesIntent(second));
            Assert.AreEqual(SpecialForcesIntent.Decoy, context.GetSpecialForcesIntent(third));
        }

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
