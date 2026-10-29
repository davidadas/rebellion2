using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Multiplayer;
using Rebellion.Simulation;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerSnapshotSerializerTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SerializeAndDeserialize_RegistrationAcrossPlanetsWithVacancy_PreservesFutureOrder(
            bool recovery
        )
        {
            GameRoot game = CreateGame();
            using GameSession session = new(game, TestContent.Data);
            Planet first = game.GetSceneNodeByInstanceID<Planet>("test-planet");
            Planet second = new() { InstanceID = "second-planet" };
            game.AttachNode(second, first.ParentNode);
            game.AttachNode(new Regiment { InstanceID = "retained-first" }, second);
            game.AttachNode(new Regiment { InstanceID = "removed" }, first);
            game.AttachNode(new Regiment { InstanceID = "retained-second" }, first);
            game.DeleteNode(game.GetSceneNodeByInstanceID<Regiment>("removed"));

            GameRoot restored;
            if (recovery)
            {
                MultiplayerSnapshot snapshot = MultiplayerRecoverySnapshotCodec.Capture(session);
                using GameSession decoded = MultiplayerRecoverySnapshotCodec.Restore(
                    snapshot.Payload,
                    snapshot.Hash,
                    TestContent.Data
                );
                restored = decoded.Game;
            }
            else
                restored = MultiplayerSnapshotSerializer.Deserialize(
                    MultiplayerSnapshotSerializer.Serialize(game)
                );

            CollectionAssert.AreEqual(game.NodesByInstanceID.Keys, restored.NodesByInstanceID.Keys);
            restored.RebuildSceneState();
            game.AttachNode(new Regiment { InstanceID = "added" }, first);
            restored.AttachNode(
                new Regiment { InstanceID = "added" },
                restored.GetSceneNodeByInstanceID<Planet>(first.InstanceID)
            );
            CollectionAssert.AreEqual(
                game.GetRegisteredSceneNodesByType<Regiment>().Select(node => node.InstanceID),
                restored.GetRegisteredSceneNodesByType<Regiment>().Select(node => node.InstanceID)
            );
            CollectionAssert.AreEqual(
                new[] { "retained-first", "added", "retained-second" },
                restored.GetRegisteredSceneNodesByType<Regiment>().Select(node => node.InstanceID)
            );
        }

        [Test]
        public void SerializeAndDeserialize_NestedGraph_PreservesPersistedValues()
        {
            GameRoot game = CreateGame();

            GameRoot restored = MultiplayerSnapshotSerializer.Deserialize(
                MultiplayerSnapshotSerializer.Serialize(game)
            );

            Assert.That(CanonicalXml(restored), Is.EqualTo(CanonicalXml(game)));
        }

        [Test]
        public void SerializeAndDeserialize_NestedGraph_PreservesFingerprint()
        {
            GameRoot game = CreateGame();

            GameRoot restored = MultiplayerSnapshotSerializer.Deserialize(
                MultiplayerSnapshotSerializer.Serialize(game)
            );

            Assert.That(
                MultiplayerSnapshotCodec.ComputeStateFingerprint(restored),
                Is.EqualTo(MultiplayerSnapshotCodec.ComputeStateFingerprint(game))
            );
        }

        [TestCase(null)]
        [TestCase("test-sector")]
        public void SerializeAndDeserialize_PreviousParent_PreservesIdentityAndRestoresAvailableReference(
            string previousParentID
        )
        {
            GameRoot game = CreateGame();
            Planet planet = game.GetSceneNodeByInstanceID<Planet>("test-planet");
            planet.LastParentInstanceID = previousParentID;

            GameRoot restored = MultiplayerSnapshotSerializer.Deserialize(
                MultiplayerSnapshotSerializer.Serialize(game)
            );

            Planet restoredPlanet = restored.GetSceneNodeByInstanceID<Planet>("test-planet");
            Assert.That(restoredPlanet.LastParentInstanceID, Is.EqualTo(previousParentID));
            Assert.That(restoredPlanet.ParentInstanceID, Is.EqualTo(planet.ParentInstanceID));
            Assert.That(
                restoredPlanet.LastParentNode,
                Is.SameAs(
                    previousParentID == null
                        ? null
                        : restored.GetSceneNodeByInstanceID<PlanetSector>(previousParentID)
                )
            );
        }

        [Test]
        public void SerializeAndDeserialize_DiskLoadedGraph_PreservesLoadedState()
        {
            GameRoot game = DiskRoundTrip(CreateGame());

            GameRoot restored = MultiplayerSnapshotSerializer.Deserialize(
                MultiplayerSnapshotSerializer.Serialize(game)
            );

            Assert.That(CanonicalXml(restored), Is.EqualTo(CanonicalXml(game)));
        }

        [Test]
        public void SerializeAndDeserialize_NestedGraph_RebuildsNodeRegistry()
        {
            GameRoot game = CreateGame();

            GameRoot restored = MultiplayerSnapshotSerializer.Deserialize(
                MultiplayerSnapshotSerializer.Serialize(game)
            );

            Assert.That(restored.NodesByInstanceID.ContainsKey("test-planet"), Is.True);
            Assert.That(restored.NodesByInstanceID["test-planet"], Is.TypeOf<Planet>());
        }

        [Test]
        public void Serialize_NestedGraph_DoesNotMutateSource()
        {
            GameRoot game = CreateGame();
            string before = CanonicalXml(game);

            MultiplayerSnapshotSerializer.Serialize(game);

            Assert.That(CanonicalXml(game), Is.EqualTo(before));
        }

        [Test]
        public void SerializeAndDeserialize_RuntimeLocalFaction_IsNotReplicated()
        {
            GameRoot game = CreateGame();
            game.LocalPlayerFactionID = "guest-faction";

            GameRoot restored = MultiplayerSnapshotSerializer.Deserialize(
                MultiplayerSnapshotSerializer.Serialize(game)
            );

            Assert.That(restored.LocalPlayerFactionID, Is.Null);
        }

        [Test]
        public void SerializeAndDeserialize_DateLookingText_PreservesString()
        {
            GameRoot game = CreateGame();
            game.Summary.PackID = "2026-10-27T00:00:00.0000000Z";

            GameRoot restored = MultiplayerSnapshotSerializer.Deserialize(
                MultiplayerSnapshotSerializer.Serialize(game)
            );

            Assert.That(restored.Summary.PackID, Is.EqualTo(game.Summary.PackID));
        }

        [TestCase(DateTimeKind.Unspecified)]
        [TestCase(DateTimeKind.Utc)]
        [TestCase(DateTimeKind.Local)]
        public void SerializeAndDeserialize_SaveTimestamp_PreservesTicksAndKind(DateTimeKind kind)
        {
            GameRoot game = CreateGame();
            game.Metadata.LastSavedUtc = new DateTime(2026, 10, 27, 12, 34, 56, kind).AddTicks(
                1234
            );

            GameRoot restored = MultiplayerSnapshotSerializer.Deserialize(
                MultiplayerSnapshotSerializer.Serialize(game)
            );

            Assert.That(
                restored.Metadata.LastSavedUtc.Ticks,
                Is.EqualTo(game.Metadata.LastSavedUtc.Ticks)
            );
            Assert.That(restored.Metadata.LastSavedUtc.Kind, Is.EqualTo(kind));
        }

        [TestCase("System.IO.FileInfo, mscorlib")]
        [TestCase("System.IO.FileInfo")]
        [TestCase("Rebellion.Game.GameRoot, GameAssembly")]
        public void Deserialize_UnadmittedType_RejectsPayload(string typeName)
        {
            byte[] payload = Encoding.UTF8.GetBytes("{\"$type\":\"" + typeName + "\"}");

            Assert.Catch(() => MultiplayerSnapshotSerializer.Deserialize(payload));
        }

        [Test]
        public void Deserialize_UnknownMember_RejectsPayload()
        {
            Assert.Catch(() =>
                MultiplayerSnapshotSerializer.Deserialize(
                    Encoding.UTF8.GetBytes("{\"unexpected\":true}")
                )
            );
        }

        [Test]
        public void Deserialize_TrailingContent_RejectsPayload()
        {
            Assert.Catch(() =>
                MultiplayerSnapshotSerializer.Deserialize(Encoding.UTF8.GetBytes("{}{}"))
            );
        }

        [Test]
        public void Deserialize_NullRoot_RejectsPayload()
        {
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerSnapshotSerializer.Deserialize(Encoding.UTF8.GetBytes("null"))
            );
        }

        /// <summary>
        /// Creates a graph containing inherited private state, mixed node types, and shared ownership.
        /// </summary>
        /// <returns>The persisted test state.</returns>
        private static GameRoot CreateGame()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary
                {
                    PackID = "test",
                    Seed = 314159,
                    ModIDs = new[] { "first", "second" },
                },
                CurrentTick = 120,
                RandomIndex = 42,
                GameSpeed = TickSpeed.Fast,
            };
            game.SetFactionController("host-faction", "host", PlayerControllerType.Human);
            game.SetFactionController("guest-faction", "guest", PlayerControllerType.Human);
            PlanetSector sector = new PlanetSector
            {
                InstanceID = "test-sector",
                PositionX = -123,
                PositionY = 456,
            };
            Planet planet = new Planet
            {
                InstanceID = "test-planet",
                DisplayName = "Example",
                PositionX = 27,
                PositionY = -31,
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            return game;
        }

        /// <summary>
        /// Uses the independent disk serializer as a complete persisted-value oracle.
        /// </summary>
        /// <param name="game">The state to inspect.</param>
        /// <returns>The canonical disk-save XML.</returns>
        private static string CanonicalXml(GameRoot game)
        {
            using MemoryStream stream = new MemoryStream();
            new GameSerializer(typeof(GameRoot)).Serialize(stream, game);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Uses the disk reader's established hierarchy restoration as the behavioral reference.
        /// </summary>
        /// <param name="game">The state whose runtime parent references will be restored.</param>
        /// <returns>The state restored through the disk serializer.</returns>
        private static GameRoot DiskRoundTrip(GameRoot game)
        {
            using MemoryStream stream = new MemoryStream(
                Encoding.UTF8.GetBytes(CanonicalXml(game))
            );
            return (GameRoot)new GameSerializer(typeof(GameRoot)).Deserialize(stream);
        }
    }
}
