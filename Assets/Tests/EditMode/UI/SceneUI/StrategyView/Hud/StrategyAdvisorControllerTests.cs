using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Hud
{
    [TestFixture]
    public class StrategyAdvisorControllerTests
    {
        [Test]
        public void BuildCommandMenu_PlayerFaction_ReturnsAuthoredOrderAndDefaultChecks()
        {
            Faction faction = new Faction();

            IReadOnlyList<StrategyMenuCommand> commands =
                StrategyAdvisorController.BuildCommandMenu(faction);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Build Ships",
                    "Build Troops",
                    "Build Facilities",
                    "Galaxy Overview",
                    "Objectives",
                    "Translate Counterpart",
                    "Agent Advice",
                },
                commands.Select(command => command.Text)
            );
            Assert.AreEqual(
                StrategyContextMenuIconKeys.CheckMark,
                commands.Single(command => command.Text == "Translate Counterpart").IconKey
            );
            Assert.AreEqual(
                StrategyContextMenuIconKeys.CheckMark,
                commands.Single(command => command.Text == "Agent Advice").IconKey
            );
        }

        [Test]
        public void BuildNotificationMenu_SavedCategorySetting_ReturnsAuthoredOrderAndChecks()
        {
            Faction faction = new Faction();
            faction.ToggleAdvisorMessageNotification(MessageType.Fleet);

            IReadOnlyList<StrategyMenuCommand> commands =
                StrategyAdvisorController.BuildNotificationMenu(faction);
            IReadOnlyList<StrategyMenuCommand> alerts = commands
                .Single(command => command.Text == "Message Alerts")
                .SubmenuCommands;

            CollectionAssert.AreEqual(
                new[] { "Messages", "Message Alerts" },
                commands.Select(command => command.Text)
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    "Loyalty",
                    "Fleets",
                    "Mission",
                    "Resources",
                    "Manufacturing",
                    "Defense",
                    "Conflict",
                    "Advice",
                    "Chat",
                },
                alerts.Select(command => command.Text)
            );
            Assert.AreEqual(
                StrategyContextMenuIconKeys.None,
                alerts.Single(command => command.Text == "Fleets").IconKey
            );
            Assert.AreEqual(
                StrategyContextMenuIconKeys.CheckMark,
                alerts.Single(command => command.Text == "Mission").IconKey
            );
        }

        [Test]
        public void BuildCommandMenu_WithoutPlayerFaction_DisablesAllCommands()
        {
            IReadOnlyList<StrategyMenuCommand> commandMenu =
                StrategyAdvisorController.BuildCommandMenu(null);

            Assert.IsTrue(commandMenu.All(command => !command.Enabled));
        }

        [Test]
        public void BuildNotificationMenu_WithoutPlayerFaction_DisablesAllCommands()
        {
            IReadOnlyList<StrategyMenuCommand> notificationMenu =
                StrategyAdvisorController.BuildNotificationMenu(null);

            Assert.IsTrue(
                notificationMenu.All(command =>
                    !command.Enabled
                    && command.SubmenuCommands.All(submenuCommand => !submenuCommand.Enabled)
                )
            );
        }
    }
}
