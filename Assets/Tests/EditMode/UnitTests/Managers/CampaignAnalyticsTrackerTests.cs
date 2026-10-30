using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;

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
        }

        [Test]
        public void Constructor_LoadedCampaign_DoesNotRecordCreation()
        {
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();

            new CampaignAnalyticsTracker(CreateGame(), sink, false);

            Assert.IsEmpty(sink.Events);
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

        private sealed class RecordingAnalyticsSink : IAnalyticsEventSink
        {
            internal bool Enabled { get; set; } = true;
            internal List<RecordedEvent> Events { get; } = new List<RecordedEvent>();

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
            public void Flush() { }
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
