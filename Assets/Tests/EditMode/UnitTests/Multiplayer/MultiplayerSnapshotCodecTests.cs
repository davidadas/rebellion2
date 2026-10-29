using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;

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
    }
}
