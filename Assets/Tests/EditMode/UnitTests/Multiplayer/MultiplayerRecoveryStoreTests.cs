using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using UnityEngine;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerRecoveryStoreTests
    {
        private string _directory;
        private MultiplayerRecoveryStore _store;
        private MultiplayerRecoveryRecord _record;
        private GameSession _session;

        /// <summary>
        /// Creates isolated storage and a deterministic two-player simulation.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "rebellion-recovery-test-" + Guid.NewGuid().ToString("N")
            );
            _store = new MultiplayerRecoveryStore(_directory);
            _record = MultiplayerRecoveryRecordTests.CreateRecord();
            GameRoot game = TestGame.Create(
                new GameSummary { Seed = 12345, PlayerFactionID = "FNALL1" },
                TestConfig.Create()
            );
            game.GetFactions()
                .AddRange(
                    new[]
                    {
                        new Faction { InstanceID = "FNALL1" },
                        new Faction { InstanceID = "FNEMP1" },
                    }
                );
            game.SetFactionController("FNALL1", "host-player", PlayerControllerType.Human);
            game.SetFactionController("FNEMP1", "guest-player", PlayerControllerType.Human);
            game.UseDeterministicRuntimeIdentifiers = true;
            MultiplayerCampaignAdmission.InitializeCampaign(
                game,
                new MultiplayerCampaignIdentity
                {
                    HostUserID = "host",
                    GuestUserID = "guest",
                    HostFactionID = "FNALL1",
                    GuestFactionID = "FNEMP1",
                },
                true
            );
            game.Metadata.CampaignID = _record.campaignID;
            _session = new GameSession(game, TestGameData.Create(game.Config));
        }

        /// <summary>
        /// Releases the simulation and deletes only this fixture's generated files.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _session.Dispose();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void ReadRecord_NoCheckpoint_ReturnsNull()
        {
            Assert.IsNull(_store.ReadRecord("host"));
        }

        [Test]
        public async Task WriteAsync_CompletedPublication_RestoresExactSimulationAndRandomPositionAsync()
        {
            _session.Game.Random.NextInt(int.MinValue, int.MaxValue);
            _session.Game.Random.NextDouble();
            MultiplayerSnapshot snapshot = MultiplayerRecoverySnapshotCodec.Capture(_session);
            _record.snapshotHash = snapshot.Hash;
            await _store.WriteAsync("host", _record, snapshot);
            GameSessionRecoveryState state = _store.ReadState("host");
            using GameSession restored = GameSession.RestoreRecoveryState(
                state,
                TestGameData.Create(_session.Game.Config)
            );
            Assert.AreEqual(
                MultiplayerStateFingerprint.ComputeSimulation(_session),
                MultiplayerStateFingerprint.ComputeSimulation(restored)
            );
            Assert.AreEqual(_session.Game.Random.NextDouble(), restored.Game.Random.NextDouble());
        }

        [Test]
        public async Task WriteAsync_NewStoreInstance_RetainsRoomAndOriginalRoleAsync()
        {
            await WriteCheckpointAsync("guest");
            MultiplayerRecoveryStore restarted = new(_directory);
            MultiplayerRecoveryRecord record = restarted.ReadRecord("guest");
            Assert.AreEqual(_record.lobbyID, record.lobbyID);
            Assert.AreEqual(MultiplayerRole.Guest, record.GetRole("guest"));
            Assert.AreEqual(
                _record.campaignID,
                restarted.ReadState("guest").Game.Metadata.CampaignID
            );
        }

        [Test]
        public async Task ReadRecord_DifferentAccount_DoesNotExposeAnotherPlayersRecoveryAsync()
        {
            await WriteCheckpointAsync("host");
            Assert.IsNull(_store.ReadRecord("other"));
        }

        [Test]
        public async Task ReadState_CorruptPayload_RejectsBeforeRestoringWorldAsync()
        {
            await WriteCheckpointAsync("host");
            string path = Directory.GetFiles(_directory).Single();
            byte[] bytes = File.ReadAllBytes(path);
            bytes[^1] ^= 127;
            File.WriteAllBytes(path, bytes);
            Assert.Catch<IOException>(() => _store.ReadState("host"));
        }

        [Test]
        public async Task ReadState_TruncatedPublication_RejectsCheckpointAsync()
        {
            await WriteCheckpointAsync("host");
            using (FileStream stream = File.OpenWrite(Directory.GetFiles(_directory).Single()))
                stream.SetLength(stream.Length - 1);
            Assert.Throws<InvalidDataException>(() => _store.ReadState("host"));
        }

        [Test]
        public async Task WriteAsync_InvalidReplacement_PreservesPreviouslyPublishedCheckpointAsync()
        {
            await WriteCheckpointAsync("host");
            string previousHash = _store.ReadRecord("host").snapshotHash;
            Assert.Throws<InvalidDataException>(() =>
                _store.WriteAsync(
                    "host",
                    _record,
                    new MultiplayerSnapshot(new byte[] { 1 }, "wrong")
                )
            );
            Assert.AreEqual(previousHash, _store.ReadRecord("host").snapshotHash);
            Assert.DoesNotThrow(() => _store.ReadState("host"));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(int.MaxValue)]
        public async Task ReadState_InvalidDeclaredPayloadLength_RejectsCheckpointAsync(
            int declaredLength
        )
        {
            await WriteCheckpointAsync("host");
            string path = Directory.GetFiles(_directory).Single();
            long lengthOffset;
            using (BinaryReader reader = new(File.OpenRead(path), Encoding.UTF8))
            {
                reader.ReadString();
                reader.ReadString();
                lengthOffset = reader.BaseStream.Position;
            }
            using (BinaryWriter writer = new(File.OpenWrite(path), Encoding.UTF8))
            {
                writer.BaseStream.Position = lengthOffset;
                writer.Write(declaredLength);
            }

            Assert.Throws<InvalidDataException>(() => _store.ReadState("host"));
        }

        [Test]
        public async Task ReadState_TrailingBytes_RejectsCheckpointAsync()
        {
            await WriteCheckpointAsync("host");
            using (
                FileStream stream = File.Open(
                    Directory.GetFiles(_directory).Single(),
                    FileMode.Append
                )
            )
                stream.WriteByte(0);

            Assert.Throws<InvalidDataException>(() => _store.ReadState("host"));
        }

        [TestCase("campaignID")]
        [TestCase("hostUserID")]
        [TestCase("guestUserID")]
        [TestCase("hostFactionID")]
        [TestCase("guestFactionID")]
        [TestCase("snapshotHash")]
        public async Task ReadState_HeaderDisagreesWithRetainedWorld_RejectsCheckpointAsync(
            string field
        )
        {
            await WriteCheckpointAsync("host");
            string path = Directory.GetFiles(_directory).Single();
            string signature;
            MultiplayerRecoveryRecord changed;
            byte[] body;
            using (BinaryReader reader = new(File.OpenRead(path), Encoding.UTF8))
            {
                signature = reader.ReadString();
                changed = JsonUtility.FromJson<MultiplayerRecoveryRecord>(reader.ReadString());
                body = reader.ReadBytes(
                    (int)(reader.BaseStream.Length - reader.BaseStream.Position)
                );
            }
            switch (field)
            {
                case "campaignID":
                    changed.campaignID = "00000000000000000000000000000001";
                    break;
                case "hostUserID":
                    changed.hostUserID = "other-host";
                    break;
                case "guestUserID":
                    changed.guestUserID = "other-guest";
                    break;
                case "hostFactionID":
                    changed.hostFactionID = "other-host-faction";
                    break;
                case "guestFactionID":
                    changed.guestFactionID = "other-guest-faction";
                    break;
                case "snapshotHash":
                    changed.snapshotHash = new string('0', 64);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field));
            }
            using (BinaryWriter writer = new(File.Create(path), Encoding.UTF8))
            {
                writer.Write(signature);
                writer.Write(JsonUtility.ToJson(changed));
                writer.Write(body);
            }

            Assert.Throws<InvalidDataException>(() => _store.ReadState("host"));
        }

        [Test]
        public async Task WriteAsync_ConcurrentAccountsAndManualSaves_PreservesEveryOwnedFileAsync()
        {
            MultiplayerSnapshot snapshot = MultiplayerRecoverySnapshotCodec.Capture(_session);
            _record.snapshotHash = snapshot.Hash;
            SaveGameManager manual = new(_directory);

            await Task.WhenAll(
                _store.WriteAsync("host", _record, snapshot),
                _store.WriteAsync("guest", _record, snapshot),
                manual.SaveGameDataAsync(_session.Game, "manual-one", "Manual one"),
                manual.SaveGameDataAsync(_session.Game, "manual-two", "Manual two")
            );

            Assert.AreEqual(snapshot.Hash, _store.ReadRecord("host").snapshotHash);
            Assert.AreEqual(snapshot.Hash, _store.ReadRecord("guest").snapshotHash);
            Assert.AreEqual(_record.campaignID, _store.ReadState("host").Game.Metadata.CampaignID);
            Assert.AreEqual(_record.campaignID, _store.ReadState("guest").Game.Metadata.CampaignID);
            Assert.AreEqual(
                "Manual one",
                manual.LoadGameData("manual-one").Metadata.SaveDisplayName
            );
            Assert.AreEqual(
                "Manual two",
                manual.LoadGameData("manual-two").Metadata.SaveDisplayName
            );
            Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
        }

        [Test]
        public async Task WriteAsync_RecoveryPublication_LeavesManualSavesUntouchedAsync()
        {
            Directory.CreateDirectory(_directory);
            string manualPath = Path.Combine(_directory, "save_slot_1.sav");
            File.WriteAllText(manualPath, "manual checkpoint");
            await WriteCheckpointAsync("host");
            Assert.AreEqual("manual checkpoint", File.ReadAllText(manualPath));
            Assert.AreEqual(1, Directory.GetFiles(_directory, "*.sav").Length);
            Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
        }

        /// <summary>
        /// Captures and publishes the fixture's current complete runtime boundary.
        /// </summary>
        /// <param name="userID">The participant owning the local recovery copy.</param>
        /// <returns>The durable publication.</returns>
        private Task WriteCheckpointAsync(string userID)
        {
            MultiplayerSnapshot snapshot = MultiplayerRecoverySnapshotCodec.Capture(_session);
            _record.snapshotHash = snapshot.Hash;
            return _store.WriteAsync(userID, _record, snapshot);
        }
    }
}
