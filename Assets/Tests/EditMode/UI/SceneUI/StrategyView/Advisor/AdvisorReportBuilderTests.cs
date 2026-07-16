using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Advisor
{
    [TestFixture]
    public class AdvisorReportBuilderTests
    {
        [Test]
        public void BuildGalaxyOverview_CompletedStationaryUnits_GroupsAndFormatsTotals()
        {
            Faction faction = new Faction { InstanceID = "faction" };
            faction.AddOwnedUnit(
                new Regiment
                {
                    TypeID = "regiment",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    MaintenanceCost = 12,
                }
            );
            faction.AddOwnedUnit(
                new Regiment
                {
                    TypeID = "regiment",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    MaintenanceCost = 12,
                }
            );
            faction.AddOwnedUnit(
                new Starfighter
                {
                    TypeID = "fighter",
                    ManufacturingStatus = ManufacturingStatus.Building,
                    MaintenanceCost = 30,
                }
            );
            faction.AddOwnedUnit(
                new Starfighter
                {
                    TypeID = "fighter",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    MaintenanceCost = 30,
                }
            );
            faction.AddOwnedUnit(
                new CapitalShip
                {
                    TypeID = "capital",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    MaintenanceCost = 50,
                }
            );

            IReadOnlyList<AdvisorReportRow> rows = AdvisorReportBuilder.BuildGalaxyOverview(
                faction
            );

            Assert.AreEqual(3, rows.Count);
            CollectionAssert.AreEqual(
                new[] { "regiment", "capital", "fighter" },
                rows.Select(row => row.Item.GetTypeID())
            );
            Assert.AreEqual("002", rows[0].PrimaryText);
            Assert.AreEqual("0024", rows[0].SecondaryText);
        }

        [Test]
        public void BuildObjectives_ConfiguredConditionsAndVictoryMode_ReturnsVisibleResultsInOrder()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary { VictoryCondition = GameVictoryCondition.Headquarters },
            };
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "faction" };
            game.NodesByInstanceID[planet.InstanceID] = planet;
            AdvisorReportWindowTheme theme = new AdvisorReportWindowTheme
            {
                Objectives = new List<AdvisorObjectiveTheme>
                {
                    new AdvisorObjectiveTheme
                    {
                        Condition = AdvisorObjectiveCondition.PlanetOwnedByFaction,
                        TargetInstanceID = planet.InstanceID,
                        TargetFactionInstanceID = "faction",
                        TrueText = "Held",
                        FalseText = "Lost",
                    },
                    new AdvisorObjectiveTheme
                    {
                        Condition = AdvisorObjectiveCondition.PlanetOwnedByFaction,
                        TargetInstanceID = planet.InstanceID,
                        TargetFactionInstanceID = "other",
                        TrueText = "Wrong",
                        FalseText = "Not Held",
                    },
                    new AdvisorObjectiveTheme
                    {
                        ConquestOnly = true,
                        Condition = AdvisorObjectiveCondition.PlanetOwnedByFaction,
                        TargetInstanceID = planet.InstanceID,
                        TargetFactionInstanceID = "faction",
                        TrueText = "Conquest",
                    },
                },
            };

            IReadOnlyList<AdvisorReportRow> rows = AdvisorReportBuilder.BuildObjectives(
                game,
                theme
            );

            CollectionAssert.AreEqual(
                new[] { "Held", "Not Held" },
                rows.Select(row => row.PrimaryText)
            );
        }
    }
}
