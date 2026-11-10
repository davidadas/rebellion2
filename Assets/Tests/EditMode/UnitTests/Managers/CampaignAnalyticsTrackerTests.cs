using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class CampaignAnalyticsTrackerTests
    {
        [Test]
        public void Constructor_NewCampaign_RecordsCreationAndSelectedFaction()
        {
            GameRoot game = CreateGame();
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();

            new CampaignAnalyticsTracker(game, sink, true);

            RecordedEvent started = sink.Events.Single();
            Assert.AreEqual("campaign_started", started.Name);
            Assert.AreEqual("FNALL1", started.Parameters["player_faction_id"]);
            Assert.AreEqual("base-pack", started.Parameters["content_pack_id"]);
            Assert.AreEqual(1, started.Parameters["player_planets"]);
            Assert.AreEqual(1, started.Parameters["ai_planets"]);
            Assert.IsNotEmpty(game.Metadata.CampaignID);
            Assert.AreEqual(false, started.Parameters["multiplayer"]);
            Assert.AreEqual("player", started.Parameters["player_role"]);
        }

        [Test]
        public void Constructor_LoadedCampaign_DoesNotRecordCreation()
        {
            GameRoot game = CreateGame();
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();

            new CampaignAnalyticsTracker(game, sink, false);

            Assert.IsEmpty(sink.Events);
            Assert.IsNotEmpty(game.Metadata.CampaignID);
        }

        [Test]
        public void RecordTickCompleted_AtHundredTicks_RecordsCurrentBalanceOnce()
        {
            GameRoot game = CreateGame();
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            CampaignAnalyticsTracker tracker = new CampaignAnalyticsTracker(game, sink, false);

            game.CurrentTick = 99;
            tracker.RecordTickCompleted();
            game.CurrentTick = CampaignAnalyticsTracker.CheckpointIntervalTicks;
            tracker.RecordTickCompleted();
            tracker.RecordTickCompleted();

            RecordedEvent checkpoint = sink.Events.Single();
            Assert.AreEqual("campaign_checkpoint", checkpoint.Name);
            Assert.AreEqual(100, checkpoint.Parameters["current_tick"]);
            Assert.AreEqual("FNALL1", checkpoint.Parameters["player_faction_id"]);
            Assert.AreEqual(1, checkpoint.Parameters["player_planets"]);
            Assert.AreEqual(1, checkpoint.Parameters["ai_planets"]);
        }

        [Test]
        public void RecordTickCompleted_CollectionDisabled_DoesNotRecordCheckpoint()
        {
            GameRoot game = CreateGame();
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink { Enabled = false };
            CampaignAnalyticsTracker tracker = new CampaignAnalyticsTracker(game, sink, true);
            game.CurrentTick = CampaignAnalyticsTracker.CheckpointIntervalTicks;

            tracker.RecordTickCompleted();

            Assert.IsEmpty(sink.Events);
        }

        [Test]
        public void RecordMultiplayerStarted_HostWaitsForSynchronization_RecordsOnce()
        {
            GameRoot game = CreateMultiplayerGame("FNALL1");
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            CampaignAnalyticsTracker tracker = new CampaignAnalyticsTracker(game, sink, true);
            Assert.IsEmpty(sink.Events);

            tracker.RecordMultiplayerStarted();
            tracker.RecordMultiplayerStarted();

            RecordedEvent started = sink.Events.Single();
            Assert.AreEqual("campaign_started", started.Name);
            Assert.AreEqual(true, started.Parameters["multiplayer"]);
            Assert.AreEqual("host", started.Parameters["player_role"]);
            Assert.AreEqual(game.Metadata.CampaignID + ":started", started.Parameters["event_id"]);
        }

        [Test]
        public void RecordMultiplayerStarted_Guest_DoesNotDuplicateCampaignMetrics()
        {
            GameRoot game = CreateMultiplayerGame("FNEMP1");
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            CampaignAnalyticsTracker tracker = new CampaignAnalyticsTracker(game, sink, true);

            tracker.RecordMultiplayerStarted();
            game.CurrentTick = 100;
            tracker.RecordTickCompleted();
            tracker.RecordVictory(new VictoryResult { Winner = game.GetFactions()[1], Tick = 100 });

            Assert.IsEmpty(sink.Events);
        }

        [Test]
        public void RecordMultiplayerStarted_LoadedCampaign_DoesNotCountAnotherCreation()
        {
            GameRoot game = CreateMultiplayerGame("FNALL1");
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            CampaignAnalyticsTracker tracker = new CampaignAnalyticsTracker(game, sink, false);

            tracker.RecordMultiplayerStarted();

            Assert.IsEmpty(sink.Events);
        }

        [Test]
        public void RecordTickCompleted_LoadedMultiplayer_UsesExistingCampaignAndCheckpointFields()
        {
            GameRoot game = CreateMultiplayerGame("FNALL1");
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            CampaignAnalyticsTracker tracker = new CampaignAnalyticsTracker(game, sink, false);
            game.CurrentTick = 100;

            tracker.RecordTickCompleted();

            RecordedEvent checkpoint = sink.Events.Single();
            Assert.AreEqual("campaign_checkpoint", checkpoint.Name);
            Assert.AreEqual(game.Metadata.CampaignID, checkpoint.Parameters["campaign_id"]);
            Assert.AreEqual(true, checkpoint.Parameters["multiplayer"]);
            Assert.AreEqual("host", checkpoint.Parameters["player_role"]);
        }

        [Test]
        public void RecordVictory_RepeatedPublication_RecordsOneFinishedCampaign()
        {
            GameRoot game = CreateGame();
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            CampaignAnalyticsTracker tracker = new CampaignAnalyticsTracker(game, sink, false);
            VictoryResult result = new VictoryResult { Winner = game.GetFactions()[1], Tick = 123 };

            tracker.RecordVictory(result);
            tracker.RecordVictory(result);

            RecordedEvent finished = sink.Events.Single();
            Assert.AreEqual("campaign_finished", finished.Name);
            Assert.AreEqual("FNEMP1", finished.Parameters["winner_faction_id"]);
            Assert.AreEqual("ai", finished.Parameters["winner_role"]);
            Assert.AreEqual(123, finished.Parameters["current_tick"]);
            Assert.AreEqual(1, sink.FlushCount);
        }

        [Test]
        public void RecordVictory_GuestWins_HostReportsGuestFactionWithoutPeerAccount()
        {
            GameRoot game = CreateMultiplayerGame("FNALL1");
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            CampaignAnalyticsTracker tracker = new CampaignAnalyticsTracker(game, sink, false);

            tracker.RecordVictory(new VictoryResult { Winner = game.GetFactions()[1], Tick = 123 });

            RecordedEvent finished = sink.Events.Single();
            Assert.AreEqual("guest", finished.Parameters["winner_role"]);
            Assert.AreEqual("FNEMP1", finished.Parameters["winner_faction_id"]);
            CollectionAssert.DoesNotContain(finished.Parameters.Values, "guest-account");
        }

        [Test]
        public void RecordVictory_CollectionDeclined_DoesNotRecordResult()
        {
            GameRoot game = CreateGame();
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink { Enabled = false };
            CampaignAnalyticsTracker tracker = new CampaignAnalyticsTracker(game, sink, false);

            tracker.RecordVictory(new VictoryResult { Winner = game.GetFactions()[0], Tick = 123 });

            Assert.IsEmpty(sink.Events);
        }

        [Test]
        public void RecordVictory_ReloadedSameCampaign_PreservesOutcomeDeduplicationKey()
        {
            GameRoot game = CreateGame();
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            VictoryResult result = new VictoryResult { Winner = game.GetFactions()[0], Tick = 123 };

            new CampaignAnalyticsTracker(game, sink, false).RecordVictory(result);
            new CampaignAnalyticsTracker(game, sink, false).RecordVictory(result);

            Assert.AreEqual(
                sink.Events[0].Parameters["event_id"],
                sink.Events[1].Parameters["event_id"]
            );
        }

        /// <summary>
        /// Creates a minimal game with one human faction and one AI faction.
        /// </summary>
        /// <returns>The configured game root.</returns>
        private static GameRoot CreateGame()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary
                {
                    PackID = "base-pack",
                    PackVersion = "1.2.3",
                    PlayerFactionID = "FNALL1",
                    Difficulty = GameDifficulty.Hard,
                    GalaxySize = GameSize.Medium,
                    VictoryCondition = GameVictoryCondition.Conquest,
                    ResourceAvailability = GameResourceAvailability.Normal,
                },
                Metadata = new GameMetadata(),
            };
            Faction player = new Faction { InstanceID = "FNALL1" };
            Faction ai = new Faction { InstanceID = "FNEMP1" };
            game.GetFactions().Add(player);
            game.GetFactions().Add(ai);
            game.SetFactionController("FNALL1", "human", PlayerControllerType.Human);
            game.SetFactionController("FNEMP1", "computer", PlayerControllerType.AI);
            player.AddOwnedUnit(
                new Planet { InstanceID = "player-planet", OwnerInstanceID = "FNALL1" }
            );
            ai.AddOwnedUnit(new Planet { InstanceID = "ai-planet", OwnerInstanceID = "FNEMP1" });
            return game;
        }

        /// <summary>
        /// Creates a two-human campaign with the requested local participant.
        /// </summary>
        /// <param name="localFactionID">The reporting participant's faction identifier.</param>
        /// <returns>The admitted multiplayer campaign.</returns>
        private static GameRoot CreateMultiplayerGame(string localFactionID)
        {
            GameRoot game = CreateGame();
            game.Metadata.MultiplayerCampaign = new MultiplayerCampaignIdentity
            {
                HostUserID = "host-account",
                GuestUserID = "guest-account",
                HostFactionID = "FNALL1",
                GuestFactionID = "FNEMP1",
            };
            game.SetFactionController("FNEMP1", "guest", PlayerControllerType.Human);
            game.LocalPlayerFactionID = localFactionID;
            return game;
        }

        private sealed class RecordingAnalyticsSink : IAnalyticsEventSink
        {
            internal bool Enabled { get; set; } = true;
            internal List<RecordedEvent> Events { get; } = new List<RecordedEvent>();
            internal int FlushCount;

            public bool IsAvailable => true;
            public bool IsCollectionEnabled => Enabled;

            /// <summary>
            /// Records an analytics event for later assertions.
            /// </summary>
            /// <param name="eventName">The event name.</param>
            /// <param name="parameters">The event parameters.</param>
            public void RecordEvent(
                string eventName,
                IReadOnlyDictionary<string, object> parameters
            )
            {
                Events.Add(
                    new RecordedEvent(eventName, new Dictionary<string, object>(parameters))
                );
            }

            /// <summary>
            /// Completes a no-op flush for the in-memory sink.
            /// </summary>
            public void Flush()
            {
                FlushCount++;
            }
        }

        private sealed class RecordedEvent
        {
            internal string Name { get; }
            internal IReadOnlyDictionary<string, object> Parameters { get; }

            /// <summary>
            /// Initializes a recorded analytics event.
            /// </summary>
            /// <param name="name">The event name.</param>
            /// <param name="parameters">The event parameters.</param>
            internal RecordedEvent(string name, IReadOnlyDictionary<string, object> parameters)
            {
                Name = name;
                Parameters = parameters;
            }
        }
    }
}
