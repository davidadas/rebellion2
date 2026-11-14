using NUnit.Framework;

namespace Rebellion.Tests.UI.Components
{
    public sealed class SaveGameFailureNoticeTests
    {
        [TestCase(SaveGameOperation.Save)]
        [TestCase(SaveGameOperation.Load)]
        public void Show_StorageFailure_PublishesPendingAcknowledgment(SaveGameOperation operation)
        {
            SaveGameFailureNotice notice = new();
            int changes = 0;
            notice.Changed += () => changes++;
            notice.Show(operation);

            Assert.That(notice.IsPending, Is.True);
            Assert.That(notice.Operation, Is.EqualTo(operation));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Acknowledge_PendingFailure_DismissesNotice()
        {
            SaveGameFailureNotice notice = new();
            notice.Show(SaveGameOperation.Save);
            notice.Acknowledge();

            Assert.That(notice.IsPending, Is.False);
            Assert.That(notice.Operation, Is.Null);
        }

        [Test]
        public void Acknowledge_NoPendingFailure_DoesNotPublishAChange()
        {
            SaveGameFailureNotice notice = new();
            int changes = 0;
            notice.Changed += () => changes++;
            notice.Acknowledge();

            Assert.That(changes, Is.Zero);
        }
    }
}
