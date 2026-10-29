using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    public sealed class MultiplayerStateFingerprintTests
    {
        private GameSession _source;
        private GameSession _restored;

        /// <summary>
        /// Creates independent source and restored sessions for continuation comparisons.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Summary.Seed = 123;
            game.GetFactions().Add(new Faction { InstanceID = "owner" });
            game.SetFactionController("owner", "participant", PlayerControllerType.Human);
            _source = new GameSession(game, TestContent.Data);
            MultiplayerSnapshot snapshot = MultiplayerRecoverySnapshotCodec.Capture(_source);
            _restored = MultiplayerRecoverySnapshotCodec.Restore(
                snapshot.Payload,
                snapshot.Hash,
                TestContent.Data
            );
        }

        /// <summary>
        /// Releases both session-owned service scopes.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _source.Dispose();
            _restored.Dispose();
        }

        [Test]
        public void ComputeSimulation_EquivalentRecovery_ReturnsSameFingerprint()
        {
            Assert.AreEqual(
                MultiplayerStateFingerprint.ComputeSimulation(_source),
                MultiplayerStateFingerprint.ComputeSimulation(_restored)
            );
        }

        [Test]
        public void ComputeSimulation_LocalUIChange_IgnoresPresentation()
        {
            _restored.Game.GetFactionPlayer("owner").UIState.GetOrCreateSection("Selection").Values[
                "selected"
            ] = "local-selection";

            Assert.AreEqual(
                MultiplayerStateFingerprint.ComputeSimulation(_source),
                MultiplayerStateFingerprint.ComputeSimulation(_restored)
            );
        }

        [Test]
        public void ComputeSimulation_FactionRuntimeRuleChange_DetectsDifference()
        {
            _restored.Game.GetFactionByOwnerInstanceID("owner").Settings.BattleLossLoyaltyDivisor++;

            Assert.AreNotEqual(
                MultiplayerStateFingerprint.ComputeSimulation(_source),
                MultiplayerStateFingerprint.ComputeSimulation(_restored)
            );
        }

        [Test]
        public void ComputeSimulation_UnpersistedVictoryStateChange_DetectsDifference()
        {
            GameSessionRecoveryState state = _restored.CaptureRecoveryState();
            state.VictoryDeclared = !state.VictoryDeclared;
            _restored.ApplyRecoveryState(state);

            Assert.AreNotEqual(
                MultiplayerStateFingerprint.ComputeSimulation(_source),
                MultiplayerStateFingerprint.ComputeSimulation(_restored)
            );
        }

        [Test]
        public void ComputeSimulation_DifferentActualRandomSeed_DetectsDifference()
        {
            long position = _restored.Game.RandomIndex;
            GameSessionRecoveryState state = _restored.CaptureRecoveryState();
            state.RandomSeed = 456;
            state.RandomPosition = position;
            _restored.ApplyRecoveryState(state);
            Assert.AreEqual(_source.Game.Summary.Seed, _restored.Game.Summary.Seed);
            Assert.AreEqual(_source.Game.RandomIndex, _restored.Game.RandomIndex);

            Assert.AreNotEqual(
                MultiplayerStateFingerprint.ComputeSimulation(_source),
                MultiplayerStateFingerprint.ComputeSimulation(_restored)
            );
        }

        [Test]
        public void VerifyRecovery_EquivalentContinuation_AdmitsRestoredSession()
        {
            string expected = MultiplayerStateFingerprint.ComputeSimulation(_source);
            Assert.AreEqual(
                expected,
                MultiplayerStateFingerprint.VerifyRecovery(_restored, expected)
            );
        }

        [Test]
        public void VerifyRecovery_ChangedRestoredRule_RejectsBeforeGameplay()
        {
            string expected = MultiplayerStateFingerprint.ComputeSimulation(_source);
            _restored.Game.GetFactionByOwnerInstanceID("owner").Settings.BattleLossLoyaltyDivisor++;
            Assert.Throws<System.InvalidOperationException>(() =>
                MultiplayerStateFingerprint.VerifyRecovery(_restored, expected)
            );
        }

        [TestCase(null)]
        [TestCase("")]
        public void VerifyRecovery_MissingSourceFingerprint_RejectsBeforeGameplay(string expected)
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                MultiplayerStateFingerprint.VerifyRecovery(_restored, expected)
            );
        }

        [Test]
        public void VerifyRecovery_ActualSeedDiffersFromSummary_PreservesNextWideDraw()
        {
            _source.Dispose();
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Summary.Seed = 123;
            game.Random = new SystemRandomProvider(456);
            game.Random.NextInt(int.MinValue, int.MaxValue);
            _source = new GameSession(game, TestContent.Data);
            MultiplayerSnapshot snapshot = MultiplayerRecoverySnapshotCodec.Capture(_source);
            _restored.Dispose();
            _restored = MultiplayerRecoverySnapshotCodec.Restore(
                snapshot.Payload,
                snapshot.Hash,
                TestContent.Data
            );

            MultiplayerStateFingerprint.VerifyRecovery(
                _restored,
                MultiplayerStateFingerprint.ComputeSimulation(_source)
            );
            Assert.AreEqual(
                _source.Game.Random.NextInt(int.MinValue, int.MaxValue),
                _restored.Game.Random.NextInt(int.MinValue, int.MaxValue)
            );
        }

        [Test]
        public void VerifyRecovery_RepeatedRecoveryAndDraws_PreservesIndependentContinuation()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                MultiplayerSnapshot snapshot = MultiplayerRecoverySnapshotCodec.Capture(_source);
                _restored.Dispose();
                _restored = MultiplayerRecoverySnapshotCodec.Restore(
                    snapshot.Payload,
                    snapshot.Hash,
                    TestContent.Data
                );
                MultiplayerStateFingerprint.VerifyRecovery(
                    _restored,
                    MultiplayerStateFingerprint.ComputeSimulation(_source)
                );

                for (int draw = 0; draw < 8; draw++)
                    Assert.AreEqual(
                        _source.Game.Random.NextInt(int.MinValue, int.MaxValue),
                        _restored.Game.Random.NextInt(int.MinValue, int.MaxValue)
                    );
            }
        }

        [Test]
        public void ComputeSimulation_ReplacedModelRandomWithoutServiceRebind_RejectsSplitOwnership()
        {
            _restored.Game.Random = new SystemRandomProvider(456);

            Assert.Throws<System.InvalidOperationException>(() =>
                MultiplayerStateFingerprint.ComputeSimulation(_restored)
            );
        }

        [Test]
        public void Compute_UnassignedPrototypeIdentity_DoesNotAllocateIdentifier()
        {
            CapitalShip prototype = new() { TypeID = "prototype" };

            MultiplayerStateFingerprint.Compute(prototype);

            Assert.IsNull(prototype.GetRecoveryInstanceID());
        }
    }
}
