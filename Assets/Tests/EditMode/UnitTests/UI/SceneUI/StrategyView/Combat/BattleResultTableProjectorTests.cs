using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Combat
{
    [TestFixture]
    public class BattleResultTableProjectorTests
    {
        [Test]
        public void Project_ReportCategory_ReturnsMatchingUnitsOnly()
        {
            CombatReport report = CreateReport();
            report.AttackingUnits.Add(CreateUnit("ship", CombatReportUnitCategory.CapitalShip));
            report.AttackingUnits.Add(CreateUnit("fighter", CombatReportUnitCategory.Starfighter));

            BattleResultTableRenderData table = new BattleResultTableProjector().Project(
                null,
                BattleResultPresentation.Create(report),
                "attacker",
                BattleResultCategory.CapitalShips
            );

            CollectionAssert.AreEqual(
                new[] { "ship" },
                table.Operational.Select(item => item.Text)
            );
            Assert.AreEqual("No Casualties", table.Destroyed.Single().Text);
        }

        [Test]
        public void Project_DestroyedUnit_PlacesUnitInDestroyedColumn()
        {
            CombatReport report = CreateReport();
            CombatReportUnit destroyed = CreateUnit(
                "destroyed",
                CombatReportUnitCategory.CapitalShip
            );
            destroyed.Destroyed = true;
            report.AttackingUnits.Add(destroyed);

            BattleResultTableRenderData table = new BattleResultTableProjector().Project(
                null,
                BattleResultPresentation.Create(report),
                "attacker",
                BattleResultCategory.CapitalShips
            );

            Assert.AreEqual("None", table.Operational.Single().Text);
            Assert.AreEqual("destroyed", table.Destroyed.Single().Text);
        }

        [Test]
        public void Project_DuplicateInstanceId_ReturnsOneRow()
        {
            CombatReport report = CreateReport();
            report.AttackingUnits.Add(CreateUnit("first", CombatReportUnitCategory.Troops, "same"));
            report.AttackingUnits.Add(
                CreateUnit("duplicate", CombatReportUnitCategory.Troops, "same")
            );

            BattleResultTableRenderData table = new BattleResultTableProjector().Project(
                null,
                BattleResultPresentation.Create(report),
                "attacker",
                BattleResultCategory.Troops
            );

            Assert.AreEqual(1, table.Operational.Count);
            Assert.AreEqual("first", table.Operational[0].Text);
        }

        [Test]
        public void Project_DefenderOwner_ReturnsDefendingUnits()
        {
            CombatReport report = CreateReport();
            report.DefendingUnits.Add(CreateUnit("defender", CombatReportUnitCategory.Personnel));

            BattleResultTableRenderData table = new BattleResultTableProjector().Project(
                null,
                BattleResultPresentation.Create(report),
                "defender",
                BattleResultCategory.Personnel
            );

            Assert.AreEqual("defender", table.Operational.Single().Text);
        }

        [Test]
        public void Project_UnrepresentedOwner_ReturnsEmptyColumns()
        {
            BattleResultTableRenderData table = new BattleResultTableProjector().Project(
                null,
                BattleResultPresentation.Create(CreateReport()),
                "unknown",
                BattleResultCategory.CapitalShips
            );

            Assert.AreEqual("None", table.Operational.Single().Text);
            Assert.AreEqual("No Casualties", table.Destroyed.Single().Text);
        }

        /// <summary>
        /// Creates a detached combat report.
        /// </summary>
        /// <returns>The configured report.</returns>
        private static CombatReport CreateReport()
        {
            return new CombatReport
            {
                CombatType = CombatReportType.SpaceBattle,
                AttackerOwnerInstanceID = "attacker",
                DefenderOwnerInstanceID = "defender",
                AttackerOutcome = SpaceCombatSideOutcome.Active,
                DefenderOutcome = SpaceCombatSideOutcome.Active,
            };
        }

        /// <summary>
        /// Creates one detached report unit.
        /// </summary>
        /// <param name="name">The display name.</param>
        /// <param name="category">The result category.</param>
        /// <param name="instanceId">The optional stable identifier.</param>
        /// <returns>The configured report unit.</returns>
        private static CombatReportUnit CreateUnit(
            string name,
            CombatReportUnitCategory category,
            string instanceId = null
        )
        {
            return new CombatReportUnit
            {
                InstanceID = instanceId ?? name,
                DisplayName = name,
                Category = category,
                WasOperational = true,
            };
        }
    }
}
