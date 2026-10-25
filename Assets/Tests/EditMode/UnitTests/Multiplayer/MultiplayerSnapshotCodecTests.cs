using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerSnapshotCodecTests
    {
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
