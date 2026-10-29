using NUnit.Framework;
using Rebellion.App;

namespace Rebellion.Tests.App
{
    public sealed class PersistenceFailureNoticeTests
    {
        [TestCase(PersistenceOperation.Save)]
        [TestCase(PersistenceOperation.Load)]
        public void Show_StorageFailure_PublishesPendingAcknowledgment(
            PersistenceOperation operation
        )
        {
            PersistenceFailureNotice notice = new();
            int changes = 0;
            notice.Changed += () => changes++;
            notice.Show(operation);

            Assert.That(notice.IsPending, Is.True);
            Assert.That(notice.Message, Is.Not.Empty);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Acknowledge_PendingFailure_DismissesNotice()
        {
            PersistenceFailureNotice notice = new();
            notice.Show(PersistenceOperation.Save);
            notice.Acknowledge();

            Assert.That(notice.IsPending, Is.False);
            Assert.That(notice.Message, Is.Null);
        }

        [Test]
        public void Acknowledge_NoPendingFailure_DoesNotPublishAChange()
        {
            PersistenceFailureNotice notice = new();
            int changes = 0;
            notice.Changed += () => changes++;
            notice.Acknowledge();

            Assert.That(changes, Is.Zero);
        }
    }
}
