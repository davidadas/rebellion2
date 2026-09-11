using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.Editor.Simulation
{
    [TestFixture]
    public sealed class HeadlessSimulationRunnerTests
    {
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
            record.Invoke(tracker, new object[] { Array.Empty<GameResult>() });
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
            record.Invoke(tracker, new object[] { completionResults });
            record.Invoke(tracker, new object[] { completionResults });

            Assert.AreEqual(
                1,
                getManufacturedBuildings.Invoke(
                    tracker,
                    new object[] { empire.InstanceID, BuildingType.Shipyard }
                )
            );
        }
    }
}
