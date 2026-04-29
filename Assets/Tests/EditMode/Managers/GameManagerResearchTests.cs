using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class GameManagerResearchTests
    {
        private static GameManager CreateManager(GameRoot game)
        {
            return new GameManager(game);
        }

        private static ResearchOrderedResult CreateResearchOrderedResult(
            Faction faction,
            ManufacturingType facilityType,
            int researchOrder
        )
        {
            return new ResearchOrderedResult
            {
                Faction = faction,
                FacilityType = facilityType,
                ResearchOrder = researchOrder,
            };
        }

        private static void InvokeProcessResults(GameManager manager, List<GameResult> results)
        {
            MethodInfo processResults = typeof(GameManager).GetMethod(
                "ProcessResults",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.IsNotNull(processResults);
            processResults.Invoke(manager, new object[] { results });
        }

        [Test]
        public void ProcessResults_ResearchOrderAdvance_AddsResolvedMissionMessage()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.Factions.Add(faction);

            CapitalShip frigate = new CapitalShip
            {
                DisplayName = "Frigate",
                ResearchOrder = 1,
                ResearchDifficulty = 12,
                AllowedOwnerInstanceIDs = new List<string> { "FNALL1" },
            };

            GameManager manager = CreateManager(game);
            faction.RebuildResearchQueues(new IManufacturable[] { frigate });
            InvokeProcessResults(
                manager,
                new List<GameResult>
                {
                    CreateResearchOrderedResult(faction, ManufacturingType.Ship, 1),
                }
            );

            Message message = faction.Messages[MessageType.Mission][0];
            Assert.AreEqual(
                "R&D Reports that the Frigate is now available for manufacture.",
                message.Text
            );
        }

        [Test]
        public void ProcessResults_ResearchExhausted_AddsDisciplineMessage()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.Factions.Add(faction);

            GameManager manager = CreateManager(game);
            InvokeProcessResults(
                manager,
                new List<GameResult>
                {
                    new ResearchExhaustedResult
                    {
                        Faction = faction,
                        FacilityType = ManufacturingType.Building,
                        PreviousState = 0,
                        NewState = 1,
                    },
                }
            );

            Message message = faction.Messages[MessageType.Mission][0];
            Assert.AreEqual(
                "There are no further advances expected in facility construction.",
                message.Text
            );
        }
    }
}
