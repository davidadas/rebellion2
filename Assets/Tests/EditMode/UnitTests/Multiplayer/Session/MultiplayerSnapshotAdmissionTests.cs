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

        [Test]
        public void AcceptsReceipt_MatchingLoadedWorld_ConfirmsInitialState()
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);
            long revision = admission.NextRevision();
            MultiplayerSnapshotMessage receipt = new()
            {
                initializationID = initialization,
                revision = revision,
                hash = "payload",
                simulationFingerprint = "loaded-world",
            };

            Assert.That(admission.AcceptsReceipt(receipt, "payload", "loaded-world"), Is.True);
        }

        [TestCase("initialization")]
        [TestCase("revision")]
        [TestCase("payload")]
        [TestCase("state")]
        [TestCase("missing")]
        public void AcceptsReceipt_DifferentInitialState_RejectsReceipt(string difference)
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);
            long revision = admission.NextRevision();
            MultiplayerSnapshotMessage receipt = new()
            {
                initializationID = initialization,
                revision = revision,
                hash = "payload",
                simulationFingerprint = "loaded-world",
            };
            switch (difference)
            {
                case "initialization":
                    receipt.initializationID = Guid.NewGuid().ToString("N");
                    break;
                case "revision":
                    receipt.revision++;
                    break;
                case "payload":
                    receipt.hash = "another-payload";
                    break;
                case "state":
                    receipt.simulationFingerprint = "another-world";
                    break;
                case "missing":
                    receipt = null;
                    break;
            }

            Assert.That(admission.AcceptsReceipt(receipt, "payload", "loaded-world"), Is.False);
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
