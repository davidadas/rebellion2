using System;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerSnapshotAdmissionTests
    {
        [Test]
        public void Accepts_RestartedHostIssuesLowerRevisionInNewInitialization_AcceptsSnapshot()
        {
            MultiplayerSnapshotAdmission admission = new();
            admission.Begin(Guid.NewGuid().ToString("N"));
            admission.RecordRevision(2);
            string returning = Guid.NewGuid().ToString("N");
            admission.Begin(returning);

            Assert.That(admission.Accepts(returning, 1), Is.True);
        }

        [Test]
        public void Accepts_OlderRevisionInSameInitialization_RejectsSnapshot()
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);
            admission.RecordRevision(2);

            Assert.That(admission.Accepts(initialization, 1), Is.False);
        }

        [Test]
        public void Accepts_PreviousInitializationHasHigherRevision_RejectsSnapshot()
        {
            MultiplayerSnapshotAdmission admission = new();
            string previous = Guid.NewGuid().ToString("N");
            admission.Begin(previous);
            admission.RecordRevision(2);
            admission.Begin(Guid.NewGuid().ToString("N"));

            Assert.That(admission.Accepts(previous, 3), Is.False);
        }

        [Test]
        public void Begin_RepeatedIdentity_RetainsAcceptedRevision()
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);
            admission.RecordRevision(2);
            admission.Begin(initialization);

            Assert.That(admission.Revision, Is.EqualTo(2));
        }

        [Test]
        public void NextRevision_NewInitialization_RestartsHostSequence()
        {
            MultiplayerSnapshotAdmission admission = new();
            admission.Begin(Guid.NewGuid().ToString("N"));
            admission.NextRevision();
            admission.NextRevision();
            admission.Begin(Guid.NewGuid().ToString("N"));

            Assert.That(admission.NextRevision(), Is.EqualTo(1));
        }

        [Test]
        public void Clear_ConnectionReleased_RejectsItsOutstandingSnapshot()
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);
            admission.Clear();

            Assert.That(admission.Accepts(initialization, 1), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("invalid")]
        public void Begin_InvalidIdentity_RejectsInitialization(string identity)
        {
            Assert.Throws<ArgumentException>(() =>
                new MultiplayerSnapshotAdmission().Begin(identity)
            );
        }
    }
}
