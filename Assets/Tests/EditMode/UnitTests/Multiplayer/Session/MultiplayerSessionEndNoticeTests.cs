using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerSessionEndNoticeTests
    {
        [Test]
        public void ShowOpponentDeparture_FirstDeparture_PublishesPendingNotice()
        {
            MultiplayerSessionEndNotice notice = new();
            bool observedPending = false;
            notice.Changed += () => observedPending = notice.IsPending;

            notice.ShowOpponentDeparture();

            Assert.IsTrue(observedPending);
        }

        [Test]
        public void ShowOpponentDeparture_RepeatedDeparture_PublishesOnlyOnce()
        {
            MultiplayerSessionEndNotice notice = new();
            int notifications = 0;
            notice.Changed += () => notifications++;

            notice.ShowOpponentDeparture();
            notice.ShowOpponentDeparture();

            Assert.AreEqual(1, notifications);
        }

        [Test]
        public void Acknowledge_PendingNotice_ClearsPendingStateBeforePublishing()
        {
            MultiplayerSessionEndNotice notice = new();
            notice.ShowOpponentDeparture();
            bool observedPending = true;
            notice.Changed += () => observedPending = notice.IsPending;

            notice.Acknowledge();

            Assert.IsFalse(observedPending);
        }

        [Test]
        public void Acknowledge_AlreadyAcknowledged_DoesNotPublishAgain()
        {
            MultiplayerSessionEndNotice notice = new();
            notice.ShowOpponentDeparture();
            notice.Acknowledge();
            int notifications = 0;
            notice.Changed += () => notifications++;

            notice.Acknowledge();

            Assert.Zero(notifications);
        }

        [Test]
        public void ShowOpponentDeparture_AfterAcknowledgment_PublishesNewNotice()
        {
            MultiplayerSessionEndNotice notice = new();
            notice.ShowOpponentDeparture();
            notice.Acknowledge();

            notice.ShowOpponentDeparture();

            Assert.IsTrue(notice.IsPending);
        }
    }
}
