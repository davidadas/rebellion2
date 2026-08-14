using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using GameFleet = Rebellion.Game.Units.Fleet;

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
            faction.AddOwnedUnit(
                new Building
                {
                    TypeID = "building",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    MaintenanceCost = 40,
                }
            );
            faction.AddOwnedUnit(
                new SpecialForces
                {
                    TypeID = "special-forces",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    MaintenanceCost = 20,
                }
            );

            IReadOnlyList<AdvisorReportRow> rows = AdvisorReportBuilder.BuildGalaxyOverview(
                faction
            );

            Assert.AreEqual(5, rows.Count);
            CollectionAssert.AreEqual(
                new[] { "regiment", "capital", "fighter", "building", "special-forces" },
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
                    null,
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

        [Test]
        public void BuildGalaxyOverview_NullFaction_ReturnsEmpty()
        {
            IReadOnlyList<AdvisorReportRow> rows = AdvisorReportBuilder.BuildGalaxyOverview(null);

            Assert.IsEmpty(rows);
        }

        [Test]
        public void BuildGalaxyOverview_UnitCarriedByMovingFleet_ExcludesUnit()
        {
            Faction faction = new Faction { InstanceID = "faction" };
            Regiment regiment = new Regiment
            {
                TypeID = "regiment",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            CapitalShip ship = new CapitalShip();
            GameFleet fleet = new GameFleet { Movement = new MovementState() };
            ship.SetParent(fleet);
            regiment.SetParent(ship);
            faction.AddOwnedUnit(regiment);

            IReadOnlyList<AdvisorReportRow> rows = AdvisorReportBuilder.BuildGalaxyOverview(
                faction
            );

            Assert.IsEmpty(rows);
        }

        [Test]
        public void BuildObjectives_MissingGameOrObjectives_ReturnsEmpty()
        {
            IReadOnlyList<AdvisorReportRow> missingGame = AdvisorReportBuilder.BuildObjectives(
                null,
                new AdvisorReportWindowTheme()
            );
            IReadOnlyList<AdvisorReportRow> missingObjectives =
                AdvisorReportBuilder.BuildObjectives(
                    new GameRoot(),
                    new AdvisorReportWindowTheme()
                );

            Assert.IsEmpty(missingGame);
            Assert.IsEmpty(missingObjectives);
        }

        [Test]
        public void BuildObjectives_HeadquartersOfficerAndUnknownConditions_ReturnsEvaluatedRows()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary { VictoryCondition = GameVictoryCondition.Conquest },
            };
            Faction faction = new Faction { InstanceID = "faction", HQInstanceID = "hq" };
            Planet headquarters = new Planet
            {
                InstanceID = "hq",
                OwnerInstanceID = faction.InstanceID,
            };
            Officer officer = new Officer { InstanceID = "officer", IsCaptured = true };
            game.Factions.Add(faction);
            game.NodesByInstanceID[headquarters.InstanceID] = headquarters;
            game.NodesByInstanceID[officer.InstanceID] = officer;
            AdvisorReportWindowTheme theme = new AdvisorReportWindowTheme
            {
                Objectives = new List<AdvisorObjectiveTheme>
                {
                    new AdvisorObjectiveTheme
                    {
                        Condition = AdvisorObjectiveCondition.HeadquartersOwnedByFaction,
                        TargetFactionInstanceID = faction.InstanceID,
                        TrueText = "Headquarters Held",
                        FalseText = "Headquarters Lost",
                    },
                    new AdvisorObjectiveTheme
                    {
                        Condition = AdvisorObjectiveCondition.OfficerCaptured,
                        TargetInstanceID = officer.InstanceID,
                        TrueText = "Officer Captured",
                        FalseText = "Officer Free",
                    },
                    new AdvisorObjectiveTheme
                    {
                        Condition = (AdvisorObjectiveCondition)99,
                        TrueText = "Unexpected",
                        FalseText = "Unsupported",
                    },
                },
            };

            IReadOnlyList<AdvisorReportRow> rows = AdvisorReportBuilder.BuildObjectives(
                game,
                theme
            );

            CollectionAssert.AreEqual(
                new[] { "Headquarters Held", "Officer Captured", "Unsupported" },
                rows.Select(row => row.PrimaryText)
            );
        }
    }
}
