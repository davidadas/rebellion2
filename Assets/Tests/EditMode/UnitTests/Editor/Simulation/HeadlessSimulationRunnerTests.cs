using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.Editor.Simulation
{
    [TestFixture]
    public sealed class HeadlessSimulationRunnerTests
    {
        [Test]
        public void SimulationOptions_ParseDifficulty_UsesRequestedValue()
        {
            object options = ParseSimulationOptions("-simDifficulty", "Hard");

            Assert.AreEqual(
                GameDifficulty.Hard,
                options.GetType().GetProperty("Difficulty").GetValue(options)
            );
        }

        [Test]
        public void SimulationOptions_ParseDifficulty_DefaultsToMedium()
        {
            object options = ParseSimulationOptions();

            Assert.AreEqual(
                GameDifficulty.Medium,
                options.GetType().GetProperty("Difficulty").GetValue(options)
            );
        }

        [Test]
        public void ManufacturedUnitTracker_RecordCompletion_CountsFacilityOnce()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "construction-world",
                empire.InstanceID,
                energyCapacity: 4
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard-template",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            Type trackerType = AppDomain
                .CurrentDomain.GetAssemblies()
                .Select(assembly =>
                    assembly.GetType("HeadlessSimulationRunner+ManufacturedUnitTracker")
                )
                .Single(type => type != null);
            object tracker = Activator.CreateInstance(trackerType, nonPublic: true);
            MethodInfo recordInitialState = trackerType.GetMethod("RecordInitialState");
            MethodInfo record = trackerType.GetMethod("Record");
            MethodInfo getManufacturedBuildings = trackerType.GetMethod(
                "GetManufacturedBuildings",
                new[] { typeof(string), typeof(BuildingType) }
            );
            List<SpecialForces> specialForces = game.GetSceneNodesByType<SpecialForces>();
            recordInitialState.Invoke(tracker, new object[] { game, specialForces });

            Assert.IsTrue(
                context.Manufacturing.StartManufacturing(
                    planet,
                    shipyard,
                    planet,
                    1,
                    empire.InstanceID
                )
            );
            record.Invoke(tracker, new object[] { game, Array.Empty<GameResult>() });
            Assert.AreEqual(
                0,
                getManufacturedBuildings.Invoke(
                    tracker,
                    new object[] { empire.InstanceID, BuildingType.Shipyard }
                )
            );

            Building queuedShipyard = planet
                .GetManufacturingQueue()[ManufacturingType.Building]
                .OfType<Building>()
                .Single();
            queuedShipyard.ManufacturingStatus = ManufacturingStatus.Complete;
            GameResult[] completionResults =
            {
                new GameObjectDeployedResult { GameObject = queuedShipyard },
            };
            record.Invoke(tracker, new object[] { game, completionResults });
            record.Invoke(tracker, new object[] { game, completionResults });

            Assert.AreEqual(
                1,
                getManufacturedBuildings.Invoke(
                    tracker,
                    new object[] { empire.InstanceID, BuildingType.Shipyard }
                )
            );
        }

        [Test]
        public void MissionOutcomeTracker_RecordChangedSideDiplomacy_RecordsOwnershipAndRefresh()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction alliance);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "target",
                alliance.InstanceID
            );
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "mission-1",
                OwnerInstanceID = empire.InstanceID,
                TypeID = MissionTypeIDs.Diplomacy,
            };
            MissionCompletedResult completion = new MissionCompletedResult
            {
                Mission = mission,
                MissionInstanceID = mission.InstanceID,
                MissionTypeID = MissionTypeIDs.Diplomacy,
                Location = planet,
                Outcome = MissionOutcome.Failed,
                CompletionReason = MissionCompletionReason.TargetChangedSides,
                Tick = 42,
            };
            IntelligenceRevealedResult intelligence = new IntelligenceRevealedResult
            {
                MissionInstanceID = mission.InstanceID,
                Recipient = empire,
                Observations = new List<Rebellion.SceneGraph.ISceneNode> { planet },
                Tick = 42,
            };
            object tracker = CreateRunnerNestedType("MissionOutcomeTracker");

            tracker
                .GetType()
                .GetMethod("Record")
                .Invoke(tracker, new object[] { new GameResult[] { intelligence, completion } });
            object summary = tracker
                .GetType()
                .GetMethod("BuildSummary")
                .Invoke(tracker, new object[] { empire.InstanceID });
            Array records = (Array)
                summary.GetType().GetField("DiplomacyOwnershipChanges").GetValue(summary);
            object record = records.GetValue(0);

            Assert.AreEqual(1, records.Length);
            Assert.AreEqual(42, record.GetType().GetField("Tick").GetValue(record));
            Assert.AreEqual(
                planet.InstanceID,
                record.GetType().GetField("PlanetId").GetValue(record)
            );
            Assert.AreEqual(
                alliance.InstanceID,
                record.GetType().GetField("CurrentOwnerFactionId").GetValue(record)
            );
            Assert.IsTrue(
                (bool)record.GetType().GetField("IntelligenceRefreshed").GetValue(record)
            );
        }

        /// <summary>
        /// Parses simulation options through the headless runner's private option type.
        /// </summary>
        /// <param name="args">The command-line arguments to parse.</param>
        /// <returns>The parsed simulation options.</returns>
        private static object ParseSimulationOptions(params string[] args)
        {
            Type runnerType = GetRunnerType();
            Type optionsType = runnerType.GetNestedType(
                "SimulationOptions",
                BindingFlags.NonPublic
            );
            return optionsType
                .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { args });
        }

        /// <summary>
        /// Creates a private nested headless-runner collaborator for focused instrumentation tests.
        /// </summary>
        /// <param name="typeName">The nested type name.</param>
        /// <returns>A new collaborator instance.</returns>
        private static object CreateRunnerNestedType(string typeName)
        {
            Type type = GetRunnerType().GetNestedType(typeName, BindingFlags.NonPublic);
            return Activator.CreateInstance(type, nonPublic: true);
        }

        /// <summary>
        /// Finds the headless simulation runner type in the loaded editor assemblies.
        /// </summary>
        /// <returns>The headless simulation runner type.</returns>
        private static Type GetRunnerType()
        {
            return AppDomain
                .CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("HeadlessSimulationRunner"))
                .Single(type => type != null);
        }
    }
}
