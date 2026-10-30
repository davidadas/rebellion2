using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.App;
using Rebellion.App.PlayerRequests;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.PlayerRequests;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesPlayerActionsTests
    {
        [Test]
        public async Task MarkReadAsync_UnreadMessage_AppliesReadWithoutPersistingAcknowledgmentAsync()
        {
            var fixture = CreateFixture();
            Message message = AddMessage(fixture.faction, MessageType.Fleet, "fleet");

            bool accepted = await fixture.actions.MarkReadAsync(message);

            Assert.IsTrue(accepted);
            Assert.IsTrue(message.Read);
            Assert.IsTrue(message.NotificationAcknowledged);
            message.Read = false;
            Assert.IsFalse(message.NotificationAcknowledged);
        }

        [Test]
        public async Task MarkReadAsync_RejectedRequest_PreservesUnreadStateAsync()
        {
            Faction faction = new Faction();
            Message message = AddMessage(faction, MessageType.Fleet, "fleet");
            RecordingPlayerRequestDispatcher dispatcher = TestPlayerRequests.RejectAll();
            MessagesPlayerActions actions = new MessagesPlayerActions(dispatcher, () => faction);

            bool accepted = await actions.MarkReadAsync(message);

            Assert.IsFalse(accepted);
            Assert.IsFalse(message.Read);
            Assert.IsInstanceOf<ReadMessagesRequest>(dispatcher.Requests.Single());
        }

        [Test]
        public async Task MarkReadAsync_RepeatedPendingRead_WaitsForAcceptanceAndSubmitsOnceAsync()
        {
            var fixture = CreateFixture();
            Message message = AddMessage(fixture.faction, MessageType.Fleet, "fleet");
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>();
            RecordingPlayerRequestDispatcher dispatcher = new RecordingPlayerRequestDispatcher(
                async action =>
                {
                    await release.Task;
                    return await fixture.dispatcher.ExecuteAsync(action);
                }
            );
            MessagesPlayerActions actions = new MessagesPlayerActions(
                dispatcher,
                () => fixture.faction
            );

            Task<bool> first = actions.MarkReadAsync(message);
            Task<bool> second = actions.MarkReadAsync(message);

            Assert.IsFalse(first.IsCompleted);
            Assert.IsFalse(second.IsCompleted);
            Assert.IsFalse(message.Read);
            Assert.AreEqual(1, dispatcher.Requests.Count);
            release.SetResult(true);
            Assert.IsTrue(await first);
            Assert.IsTrue(await second);
            Assert.IsTrue(message.Read);
            Assert.AreEqual(1, dispatcher.Requests.Count);
        }

        [Test]
        public async Task AcknowledgeAsync_PresentedMessages_AcknowledgesWithoutMarkingReadAsync()
        {
            var fixture = CreateFixture();
            Message fleet = AddMessage(fixture.faction, MessageType.Fleet, "fleet");
            Message mission = AddMessage(fixture.faction, MessageType.Mission, "mission");

            bool accepted = await fixture.actions.AcknowledgeAsync(new[] { fleet, mission });

            Assert.IsTrue(accepted);
            Assert.IsTrue(fleet.NotificationAcknowledged);
            Assert.IsTrue(mission.NotificationAcknowledged);
            Assert.IsFalse(fleet.Read);
            Assert.IsFalse(mission.Read);
        }

        [Test]
        public async Task AcknowledgeAsync_AlreadyAcknowledged_DoesNotSubmitAnotherRequestAsync()
        {
            Faction faction = new Faction();
            Message message = AddMessage(faction, MessageType.Fleet, "fleet");
            message.NotificationAcknowledged = true;
            RecordingPlayerRequestDispatcher dispatcher = TestPlayerRequests.RejectAll();
            MessagesPlayerActions actions = new MessagesPlayerActions(dispatcher, () => faction);

            bool accepted = await actions.AcknowledgeAsync(new[] { message });

            Assert.IsTrue(accepted);
            Assert.IsEmpty(dispatcher.Requests);
        }

        [Test]
        public async Task RemoveAsync_SelectedIDs_RemovesMatchingMessagesAcrossBucketsAsync()
        {
            var fixture = CreateFixture();
            Message fleet = AddMessage(fixture.faction, MessageType.Fleet, "fleet");
            Message mission = AddMessage(fixture.faction, MessageType.Mission, "mission");
            Message retained = AddMessage(fixture.faction, MessageType.Mission, "retained");

            bool accepted = await fixture.actions.RemoveAsync(
                new[] { fleet.InstanceID, mission.InstanceID }
            );

            Assert.IsTrue(accepted);
            Assert.IsEmpty(fixture.faction.Messages[MessageType.Fleet]);
            CollectionAssert.AreEqual(
                new[] { retained },
                fixture.faction.Messages[MessageType.Mission]
            );
        }

        [Test]
        public async Task RemoveAsync_EmptySelection_DoesNotSubmitRequestAsync()
        {
            RecordingPlayerRequestDispatcher dispatcher = TestPlayerRequests.RejectAll();
            MessagesPlayerActions actions = new MessagesPlayerActions(
                dispatcher,
                () => new Faction()
            );

            bool accepted = await actions.RemoveAsync(new[] { null, string.Empty });

            Assert.IsFalse(accepted);
            Assert.IsEmpty(dispatcher.Requests);
        }

        [Test]
        public async Task ConsumeContinuationAsync_PendingChoice_ConsumesWithoutMarkingReadAsync()
        {
            var fixture = CreateFixture();
            Message message = AddMessage(fixture.faction, MessageType.Mission, "mission");
            message.MissionContinuationPending = true;

            bool accepted = await fixture.actions.ConsumeContinuationAsync(message);

            Assert.IsTrue(accepted);
            Assert.IsFalse(message.MissionContinuationPending);
            Assert.IsFalse(message.Read);
        }

        [Test]
        public async Task ConsumeContinuationAsync_AlreadyConsumed_DoesNotSubmitAnotherRequestAsync()
        {
            Faction faction = new Faction();
            Message message = AddMessage(faction, MessageType.Mission, "mission");
            RecordingPlayerRequestDispatcher dispatcher = TestPlayerRequests.RejectAll();
            MessagesPlayerActions actions = new MessagesPlayerActions(dispatcher, () => faction);

            bool accepted = await actions.ConsumeContinuationAsync(message);

            Assert.IsTrue(accepted);
            Assert.IsEmpty(dispatcher.Requests);
        }

        [Test]
        public async Task ToggleNotificationAsync_AllTab_TogglesGlobalSettingAsync()
        {
            var fixture = CreateFixture();

            bool accepted = await fixture.actions.ToggleNotificationAsync(MessagesTab.All);

            Assert.IsTrue(accepted);
            Assert.IsFalse(fixture.faction.AdvisorMessageNotificationsEnabled);
        }

        [Test]
        public async Task ToggleNotificationAsync_CategoryTab_TogglesOnlyCategorySettingAsync()
        {
            var fixture = CreateFixture();

            bool accepted = await fixture.actions.ToggleNotificationAsync(MessagesTab.Fleet);

            Assert.IsTrue(accepted);
            Assert.IsTrue(fixture.faction.AdvisorMessageNotificationsEnabled);
            Assert.IsFalse(fixture.faction.IsAdvisorMessageNotificationEnabled(MessageType.Fleet));
            Assert.IsTrue(fixture.faction.IsAdvisorMessageNotificationEnabled(MessageType.Mission));
        }

        /// <summary>
        /// Creates message actions backed by an owned game session and its dispatcher.
        /// </summary>
        /// <returns>The receiving faction, action dispatcher, and message action collaborator.</returns>
        private static (
            Faction faction,
            IPlayerRequestDispatcher dispatcher,
            MessagesPlayerActions actions
        ) CreateFixture()
        {
            Faction faction = new Faction { InstanceID = "FNALL1" };
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(faction);
            IPlayerRequestDispatcher dispatcher = TestPlayerRequests.Create(
                game,
                () => faction.InstanceID
            );
            return (faction, dispatcher, new MessagesPlayerActions(dispatcher, () => faction));
        }

        /// <summary>
        /// Adds one identified message to the selected faction category.
        /// </summary>
        /// <param name="faction">The faction receiving the message.</param>
        /// <param name="type">The message category.</param>
        /// <param name="id">The message identifier and fixture text.</param>
        /// <returns>The inserted message.</returns>
        private static Message AddMessage(Faction faction, MessageType type, string id)
        {
            Message message = new StatusMessage(type, id, id) { InstanceID = id };
            if (!faction.Messages.TryGetValue(type, out List<Message> bucket))
                faction.Messages[type] = bucket = new List<Message>();
            bucket.Add(message);
            return message;
        }
    }
}
