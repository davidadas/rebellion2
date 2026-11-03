using System;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerSnapshotAdmissionTests
    {
        [Test]
        public void Accepts_RestartedHostEstablishesNewInitialization_AcceptsInitialWorld()
        {
            MultiplayerSnapshotAdmission admission = new();
            admission.Begin(Guid.NewGuid().ToString("N"));
            string returning = Guid.NewGuid().ToString("N");
            admission.Begin(returning);

            Assert.That(admission.Accepts(returning), Is.True);
        }

        [Test]
        public void Accepts_CurrentInitialization_AcceptsInitialWorld()
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);

            Assert.That(admission.Accepts(initialization), Is.True);
        }

        [Test]
        public void Accepts_PreviousInitialization_RejectsInitialWorld()
        {
            MultiplayerSnapshotAdmission admission = new();
            string previous = Guid.NewGuid().ToString("N");
            admission.Begin(previous);
            admission.Begin(Guid.NewGuid().ToString("N"));

            Assert.That(admission.Accepts(previous), Is.False);
        }

        [Test]
        public void Begin_RepeatedIdentity_RetainsInitialization()
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);
            bool changed = admission.Begin(initialization);

            Assert.That(changed, Is.False);
            Assert.That(admission.InitializationID, Is.EqualTo(initialization));
        }

        [Test]
        public void Begin_NewInitialization_ReplacesPreviousOwnership()
        {
            MultiplayerSnapshotAdmission admission = new();
            admission.Begin(Guid.NewGuid().ToString("N"));
            string replacement = Guid.NewGuid().ToString("N");

            Assert.That(admission.Begin(replacement), Is.True);
            Assert.That(admission.InitializationID, Is.EqualTo(replacement));
        }

        [Test]
        public void Clear_ConnectionReleased_RejectsItsOutstandingSnapshot()
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);
            admission.Clear();

            Assert.That(admission.Accepts(initialization), Is.False);
        }

        [Test]
        public void AcceptsReceipt_MatchingLoadedWorld_ConfirmsInitialState()
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);
            MultiplayerSnapshotMessage receipt = new()
            {
                initializationID = initialization,
                hash = "payload",
                simulationFingerprint = "loaded-world",
            };

            Assert.That(admission.AcceptsReceipt(receipt, "payload", "loaded-world"), Is.True);
        }

        [TestCase("initialization")]
        [TestCase("payload")]
        [TestCase("state")]
        [TestCase("missing")]
        public void AcceptsReceipt_DifferentInitialState_RejectsReceipt(string difference)
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);
            MultiplayerSnapshotMessage receipt = new()
            {
                initializationID = initialization,
                hash = "payload",
                simulationFingerprint = "loaded-world",
            };
            switch (difference)
            {
                case "initialization":
                    receipt.initializationID = Guid.NewGuid().ToString("N");
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

        [Test]
        public void AcceptsPreparedSnapshot_VerifiedWorldInCurrentInitialization_AcceptsStateCheck()
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization, "verified-world");
            MultiplayerSnapshotMessage snapshot = new()
            {
                initializationID = initialization,
                hash = "verified-world",
            };

            Assert.That(admission.AcceptsPreparedSnapshot(snapshot), Is.True);
        }

        [TestCase("unprepared")]
        [TestCase("world")]
        [TestCase("initialization")]
        [TestCase("missing")]
        public void AcceptsPreparedSnapshot_UnverifiedWorld_RejectsStateCheck(string difference)
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization, difference == "unprepared" ? null : "verified-world");
            MultiplayerSnapshotMessage snapshot = new()
            {
                initializationID =
                    difference == "initialization" ? Guid.NewGuid().ToString("N") : initialization,
                hash = difference == "world" ? "different-world" : "verified-world",
            };

            Assert.That(
                admission.AcceptsPreparedSnapshot(difference == "missing" ? null : snapshot),
                Is.False
            );
        }

        [Test]
        public void Begin_NewInitialization_DiscardsPreviousPreflight()
        {
            MultiplayerSnapshotAdmission admission = new();
            admission.Begin(Guid.NewGuid().ToString("N"), "verified-world");
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);

            Assert.That(
                admission.AcceptsPreparedSnapshot(
                    new MultiplayerSnapshotMessage
                    {
                        initializationID = initialization,
                        hash = "verified-world",
                    }
                ),
                Is.False
            );
        }

        [Test]
        public void Begin_RepeatedPreparedInitialization_RetainsVerifiedWorld()
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization, "verified-world");

            Assert.That(admission.Begin(initialization, "verified-world"), Is.False);
            Assert.That(admission.PreparedSnapshotHash, Is.EqualTo("verified-world"));
        }

        [Test]
        public void Begin_ChangingWorldWithinInitialization_RejectsReplacement()
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization, "verified-world");

            Assert.Throws<InvalidOperationException>(() =>
                admission.Begin(initialization, "different-world")
            );
            Assert.That(admission.PreparedSnapshotHash, Is.EqualTo("verified-world"));
        }

        [Test]
        public void Clear_PreparedInitialization_DiscardsVerifiedWorld()
        {
            MultiplayerSnapshotAdmission admission = new();
            admission.Begin(Guid.NewGuid().ToString("N"), "verified-world");
            admission.Clear();

            Assert.That(admission.PreparedSnapshotHash, Is.Null);
        }

        [TestCase("")]
        [TestCase(" ")]
        public void Begin_EmptyPreparedHash_PreservesCurrentInitialization(string hash)
        {
            MultiplayerSnapshotAdmission admission = new();
            string initialization = Guid.NewGuid().ToString("N");
            admission.Begin(initialization);

            Assert.Throws<ArgumentException>(() =>
                admission.Begin(Guid.NewGuid().ToString("N"), hash)
            );
            Assert.That(admission.InitializationID, Is.EqualTo(initialization));
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
