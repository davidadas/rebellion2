using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Multiplayer;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerSnapshotCodecTests
    {
        [Test]
        public void ComputeStateFingerprint_UnchangedState_ReturnsSameFingerprint()
        {
            GameRoot game = new GameRoot();
            string fingerprint = MultiplayerSnapshotCodec.ComputeStateFingerprint(game);
            Assert.That(
                MultiplayerSnapshotCodec.ComputeStateFingerprint(game),
                Is.EqualTo(fingerprint)
            );
        }

        [Test]
        public void ComputeStateFingerprint_PersistedTickChanges_DetectsChange()
        {
            GameRoot game = new GameRoot();
            string fingerprint = MultiplayerSnapshotCodec.ComputeStateFingerprint(game);
            game.CurrentTick++;
            Assert.That(
                MultiplayerSnapshotCodec.ComputeStateFingerprint(game),
                Is.Not.EqualTo(fingerprint)
            );
        }

        [Test]
        public void ComputeStateFingerprint_LocalViewChanges_IgnoresRuntimeOnlyFaction()
        {
            GameRoot game = new GameRoot();
            string fingerprint = MultiplayerSnapshotCodec.ComputeStateFingerprint(game);
            game.LocalPlayerFactionID = "other-faction";
            Assert.That(
                MultiplayerSnapshotCodec.ComputeStateFingerprint(game),
                Is.EqualTo(fingerprint)
            );
        }

        [Test]
        public void ComputeStateFingerprint_NestedSummaryArrayChanges_DetectsChange()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary { ModIDs = new[] { "first", "second" } },
            };
            string fingerprint = MultiplayerSnapshotCodec.ComputeStateFingerprint(game);
            game.Summary.ModIDs[1] = "changed";
            Assert.That(
                MultiplayerSnapshotCodec.ComputeStateFingerprint(game),
                Is.Not.EqualTo(fingerprint)
            );
        }

        [Test]
        public void ComputeStateFingerprint_PlayerOwnershipChanges_DetectsChange()
        {
            GameRoot game = new GameRoot();
            game.SetFactionController("faction", "host", PlayerControllerType.Human);
            string fingerprint = MultiplayerSnapshotCodec.ComputeStateFingerprint(game);
            game.SetFactionController("faction", "guest", PlayerControllerType.Human);
            Assert.That(
                MultiplayerSnapshotCodec.ComputeStateFingerprint(game),
                Is.Not.EqualTo(fingerprint)
            );
        }

        [Test]
        public void ComputeStateFingerprint_NestedSceneNodeChanges_DetectsChange()
        {
            GameRoot game = new GameRoot();
            Rebellion.Game.Galaxy.Planet planet = new Rebellion.Game.Galaxy.Planet
            {
                InstanceID = "planet",
                DisplayName = "Before",
            };
            Rebellion.Game.Galaxy.PlanetSector sector = new Rebellion.Game.Galaxy.PlanetSector
            {
                InstanceID = "sector",
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            string snapshotHash = MultiplayerSnapshotCodec.Capture(game).Hash;
            string fingerprint = MultiplayerSnapshotCodec.ComputeStateFingerprint(game);
            planet.DisplayName = "After";
            Assert.That(MultiplayerSnapshotCodec.Capture(game).Hash, Is.Not.EqualTo(snapshotHash));
            Assert.That(
                MultiplayerSnapshotCodec.ComputeStateFingerprint(game),
                Is.Not.EqualTo(fingerprint)
            );
        }

        [Test]
        public void ComputeStateFingerprint_RestoredSnapshot_MatchesPersistedState()
        {
            GameRoot game = new GameRoot { CurrentTick = 42 };
            game.SetFactionController("faction", "host", PlayerControllerType.Human);
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(game);
            GameRoot restored = MultiplayerSnapshotCodec.Restore(snapshot.Payload, snapshot.Hash);
            Assert.That(
                MultiplayerSnapshotCodec.ComputeStateFingerprint(restored),
                Is.EqualTo(MultiplayerSnapshotCodec.ComputeStateFingerprint(game))
            );
        }

        [Test]
        public void ComputeStateFingerprint_DateMetadataChanges_DetectsChange()
        {
            GameRoot game = new GameRoot();
            string fingerprint = MultiplayerSnapshotCodec.ComputeStateFingerprint(game);
            game.Metadata.LastSavedUtc = new System.DateTime(
                2026,
                10,
                26,
                12,
                0,
                0,
                System.DateTimeKind.Utc
            );
            Assert.That(
                MultiplayerSnapshotCodec.ComputeStateFingerprint(game),
                Is.Not.EqualTo(fingerprint)
            );
        }

        [Test]
        public void CaptureAndRestore_GameState_RoundTripsAndValidatesHash()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary
                {
                    PackID = "base",
                    PackVersion = "1",
                    ScenarioID = "default",
                    PlayerFactionID = "faction-a",
                    StartingFactionIDs = new[] { "faction-a", "faction-b" },
                    Seed = 12345,
                },
                CurrentTick = 42,
            };
            game.SetFactionController("faction-a", "host", PlayerControllerType.Human);
            game.SetFactionController("faction-b", "guest", PlayerControllerType.Human);

            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(game);
            GameRoot restored = MultiplayerSnapshotCodec.Restore(snapshot.Payload, snapshot.Hash);

            Assert.That(restored.CurrentTick, Is.EqualTo(42));
            Assert.That(restored.Summary.Seed, Is.EqualTo(12345));
            Assert.That(restored.GetPlayers(), Has.Count.EqualTo(2));
        }

        [Test]
        public void Restore_WrongHash_RejectsSnapshot()
        {
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(new GameRoot());

            Assert.Throws<System.IO.InvalidDataException>(() =>
                MultiplayerSnapshotCodec.Restore(snapshot.Payload, new string('0', 64))
            );
        }

        [Test]
        public void Summary_ValidContent_RoundTripsContentAndSeed()
        {
            GameSummary summary = new GameSummary
            {
                PackID = "pack",
                PackVersion = "2",
                ScenarioID = "scenario",
                ModIDs = new[] { "mod" },
                ModVersions = new[] { "3" },
                Seed = -812,
            };

            GameSummary restored = MultiplayerSnapshotCodec.DeserializeSummary(
                MultiplayerSnapshotCodec.SerializeSummary(summary)
            );

            Assert.That(restored.PackID, Is.EqualTo("pack"));
            Assert.That(restored.ModIDs, Is.EqualTo(new[] { "mod" }));
            Assert.That(restored.Seed, Is.EqualTo(-812));
        }

        [Test]
        public void GetPlayerFaction_TwoHumanPlayers_UsesRuntimeLocalFactionWithoutPersistingIt()
        {
            GameRoot game = new GameRoot();
            game.GetFactions().Add(new Rebellion.Game.Factions.Faction { InstanceID = "host" });
            game.GetFactions().Add(new Rebellion.Game.Factions.Faction { InstanceID = "guest" });
            game.SetFactionController("host", "player-1", PlayerControllerType.Human);
            game.SetFactionController("guest", "player-2", PlayerControllerType.Human);
            game.LocalPlayerFactionID = "guest";

            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(game);
            GameRoot restored = MultiplayerSnapshotCodec.Restore(snapshot.Payload, snapshot.Hash);

            Assert.That(game.GetPlayerFaction().InstanceID, Is.EqualTo("guest"));
            Assert.That(restored.LocalPlayerFactionID, Is.Null);
            Assert.That(restored.GetPlayerFaction().InstanceID, Is.EqualTo("host"));
            restored.LocalPlayerFactionID = "guest";
            Assert.That(restored.GetPlayerFaction().InstanceID, Is.EqualTo("guest"));
        }

        [Test]
        public void Capture_GameState_CanBeReadByOrdinaryXmlSerializer()
        {
            GameRoot game = new() { CurrentTick = 123 };
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(game);
            using MemoryStream compressed = new(snapshot.Payload);
            using GZipStream xml = new(compressed, CompressionMode.Decompress);

            GameRoot restored = (GameRoot)new GameSerializer(typeof(GameRoot)).Deserialize(xml);

            Assert.That(restored.CurrentTick, Is.EqualTo(game.CurrentTick));
        }

        [TestCase("<WrongRoot />")]
        [TestCase("<GameSummary><Unexpected /></GameSummary>")]
        [TestCase("<GameSummary /><GameSummary />")]
        [TestCase(
            "<!DOCTYPE GameSummary [<!ENTITY value 'text'>]><GameSummary><PackID>&value;</PackID></GameSummary>"
        )]
        public void DeserializeSummary_XmlOutsideSchema_RejectsPayload(string xml)
        {
            Assert.That(
                () => MultiplayerSnapshotCodec.DeserializeSummary(Encoding.UTF8.GetBytes(xml)),
                Throws.Exception
            );
        }

        [Test]
        public void CaptureAndRestore_NestedGraph_PreservesPersistedValues()
        {
            GameRoot game = CreateGame();

            GameRoot restored = RoundTrip(game);

            Assert.That(CanonicalXml(restored), Is.EqualTo(CanonicalXml(game)));
        }

        [Test]
        public void CaptureAndRestore_NestedGraph_PreservesFingerprint()
        {
            GameRoot game = CreateGame();

            GameRoot restored = RoundTrip(game);

            Assert.That(
                MultiplayerSnapshotCodec.ComputeStateFingerprint(restored),
                Is.EqualTo(MultiplayerSnapshotCodec.ComputeStateFingerprint(game))
            );
        }

        [TestCase(null)]
        [TestCase("test-sector")]
        public void CaptureAndRestore_PreviousParent_PreservesIdentityAndRestoresAvailableReference(
            string previousParentID
        )
        {
            GameRoot game = CreateGame();
            Planet planet = game.GetSceneNodeByInstanceID<Planet>("test-planet");
            planet.LastParentInstanceID = previousParentID;

            GameRoot restored = RoundTrip(game);

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
        public void CaptureAndRestore_DiskLoadedGraph_PreservesLoadedState()
        {
            GameRoot game = DiskRoundTrip(CreateGame());

            GameRoot restored = RoundTrip(game);

            Assert.That(CanonicalXml(restored), Is.EqualTo(CanonicalXml(game)));
        }

        [Test]
        public void CaptureAndRestore_NestedGraph_RebuildsNodeRegistry()
        {
            GameRoot game = CreateGame();

            GameRoot restored = RoundTrip(game);

            Assert.That(restored.NodesByInstanceID.ContainsKey("test-planet"), Is.True);
            Assert.That(restored.NodesByInstanceID["test-planet"], Is.TypeOf<Planet>());
        }

        [Test]
        public void Capture_NestedGraph_DoesNotMutateSource()
        {
            GameRoot game = CreateGame();
            string before = CanonicalXml(game);

            MultiplayerSnapshotCodec.Capture(game);

            Assert.That(CanonicalXml(game), Is.EqualTo(before));
        }

        [Test]
        public void CaptureAndRestore_RuntimeLocalFaction_IsNotReplicated()
        {
            GameRoot game = CreateGame();
            game.LocalPlayerFactionID = "guest-faction";

            GameRoot restored = RoundTrip(game);

            Assert.That(restored.LocalPlayerFactionID, Is.Null);
        }

        [Test]
        public void CaptureAndRestore_DateLookingText_PreservesString()
        {
            GameRoot game = CreateGame();
            game.Summary.PackID = "2026-10-27T00:00:00.0000000Z";

            GameRoot restored = RoundTrip(game);

            Assert.That(restored.Summary.PackID, Is.EqualTo(game.Summary.PackID));
        }

        [TestCase(DateTimeKind.Unspecified)]
        [TestCase(DateTimeKind.Utc)]
        [TestCase(DateTimeKind.Local)]
        public void CaptureAndRestore_SaveTimestamp_PreservesTicksAndKind(DateTimeKind kind)
        {
            GameRoot game = CreateGame();
            game.Metadata.LastSavedUtc = new DateTime(2026, 10, 27, 12, 34, 56, kind).AddTicks(
                1234
            );

            GameRoot restored = RoundTrip(game);

            Assert.That(
                restored.Metadata.LastSavedUtc.Ticks,
                Is.EqualTo(game.Metadata.LastSavedUtc.Ticks)
            );
            Assert.That(restored.Metadata.LastSavedUtc.Kind, Is.EqualTo(kind));
        }

        /// <summary>
        /// Restores a graph through the compressed XML snapshot boundary.
        /// </summary>
        /// <param name="game">The settled source graph.</param>
        /// <returns>The independently restored graph.</returns>
        private static GameRoot RoundTrip(GameRoot game)
        {
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(game);
            return MultiplayerSnapshotCodec.Restore(snapshot.Payload, snapshot.Hash);
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
