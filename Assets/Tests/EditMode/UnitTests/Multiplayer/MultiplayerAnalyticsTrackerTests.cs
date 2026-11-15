using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerAnalyticsTrackerTests
    {
        private Sink _sink;
        private MultiplayerAnalyticsTracker _tracker;

        /// <summary>
        /// Creates an isolated analytics destination and tracker for each case.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _sink = new Sink();
            _tracker = new MultiplayerAnalyticsTracker(_sink);
        }

        [Test]
        public void RecordLobbyCreated_PublishedLobby_CountsCreationOnce()
        {
            _tracker.RecordLobbyCreated("lobby", "host-account", Summary());
            _tracker.RecordLobbyCreated("lobby", "host-account", Summary());

            Entry entry = _sink.Events.Single();
            Assert.AreEqual("multiplayer_lobby_created", entry.Name);
            Assert.AreEqual("lobby:created", entry.Parameters["event_id"]);
            Assert.AreEqual("pack", entry.Parameters["content_pack_id"]);
            Assert.AreEqual(true, entry.Parameters["multiplayer"]);
            Assert.AreEqual("host", entry.Parameters["player_role"]);
        }

        [Test]
        public void RecordLobbyCreated_ConsentDeclined_RecordsNothing()
        {
            _sink.Enabled = false;

            _tracker.RecordLobbyCreated("lobby", "host-account", Summary());

            Assert.IsEmpty(_sink.Events);
        }

        [Test]
        public void RecordLobbyConnected_AdmittedPeer_ReportsOneConnection()
        {
            _tracker.RecordLobbyCreated("lobby", "host-account", Summary());

            _tracker.RecordLobbyConnected();
            _tracker.RecordLobbyConnected();

            Assert.AreEqual(2, _sink.Events.Count);
            Assert.AreEqual("connected", _sink.Events[1].Parameters["connection_state"]);
            Assert.AreEqual("lobby", _sink.Events[1].Parameters["connection_phase"]);
        }

        [Test]
        public void RecordLobbyJoined_AdmittedGuest_RecordsGuestRole()
        {
            _tracker.RecordLobbyJoined("lobby", "guest-account", Summary());

            Entry entry = _sink.Events.Single();
            Assert.AreEqual("guest", entry.Parameters["player_role"]);
            Assert.AreEqual("connected", entry.Parameters["connection_state"]);
        }

        [Test]
        public void RecordGameConnected_RepeatedReadiness_DoesNotDuplicateConnection()
        {
            GameRoot game = Game(MultiplayerRole.Host);

            _tracker.RecordGameConnected(game, MultiplayerRole.Host, "initialization", false);
            _tracker.RecordGameConnected(game, MultiplayerRole.Host, "initialization", false);

            Entry entry = _sink.Events.Single();
            Assert.AreEqual("game", entry.Parameters["connection_phase"]);
            Assert.AreEqual("campaign", entry.Parameters["campaign_id"]);
            Assert.AreEqual("initialization", entry.Parameters["initialization_id"]);
            Assert.AreEqual(100, entry.Parameters["current_tick"]);
        }

        [Test]
        public void RecordGameConnected_Guest_ReportsOnlyItsOwnPseudonymousIdentity()
        {
            _tracker.RecordGameConnected(
                Game(MultiplayerRole.Guest),
                MultiplayerRole.Guest,
                "id",
                false
            );

            Entry entry = _sink.Events.Single();
            Assert.AreEqual("guest", entry.Parameters["player_role"]);
            Assert.AreEqual("FNEMP1", entry.Parameters["player_faction_id"]);
            Assert.AreEqual(64, ((string)entry.Parameters["player_id"]).Length);
            CollectionAssert.DoesNotContain(entry.Parameters.Values, "guest-account");
            CollectionAssert.DoesNotContain(entry.Parameters.Values, "host-account");
        }

        [Test]
        public void RecordDisconnected_RepeatedTransportCallbacks_RecordsOneDeparture()
        {
            _tracker.RecordGameConnected(
                Game(MultiplayerRole.Host),
                MultiplayerRole.Host,
                "id",
                false
            );

            _tracker.RecordDisconnected(150, "transport_closed");
            _tracker.RecordDisconnected(151, "transport_closed");

            Assert.AreEqual(2, _sink.Events.Count);
            Assert.AreEqual("disconnected", _sink.Events[1].Parameters["connection_state"]);
            Assert.AreEqual(150, _sink.Events[1].Parameters["current_tick"]);
            Assert.AreEqual("transport_closed", _sink.Events[1].Parameters["connection_reason"]);
        }

        [Test]
        public void RecordDisconnected_NoAdmittedPeer_DoesNotReportRejectedGuests()
        {
            _tracker.RecordLobbyCreated("lobby", "host-account", Summary());

            _tracker.RecordDisconnected(null, "transport_closed");

            Assert.AreEqual(1, _sink.Events.Count);
        }

        [Test]
        public void RecordGameConnected_Recovery_ReportsReconnectionWithSameLocalIdentity()
        {
            GameRoot game = Game(MultiplayerRole.Host);
            _tracker.RecordGameConnected(game, MultiplayerRole.Host, "before", false);
            _tracker.RecordDisconnected(150, "transport_closed");

            _tracker.RecordGameConnected(game, MultiplayerRole.Host, "after", true);

            CollectionAssert.AreEqual(
                new[] { "connected", "disconnected", "reconnected" },
                _sink.Events.Select(entry => entry.Parameters["connection_state"])
            );
            Assert.AreEqual(
                _sink.Events[0].Parameters["player_id"],
                _sink.Events[2].Parameters["player_id"]
            );
        }

        [Test]
        public void BeginGameLoad_InterruptedLaunch_ReportsLoadingPhase()
        {
            _tracker.RecordLobbyJoined("lobby", "guest-account", Summary());
            _tracker.BeginGameLoad("initialization");

            _tracker.RecordDisconnected(null, "transport_closed");

            Entry entry = _sink.Events.Last();
            Assert.AreEqual("loading", entry.Parameters["connection_phase"]);
            Assert.AreEqual("initialization", entry.Parameters["initialization_id"]);
            Assert.IsFalse(entry.Parameters.ContainsKey("current_tick"));
        }

        [Test]
        public void RecordClosed_AfterDeparture_DoesNotReportAnotherLoss()
        {
            _tracker.RecordGameConnected(
                Game(MultiplayerRole.Host),
                MultiplayerRole.Host,
                "id",
                false
            );
            _tracker.RecordDisconnected(150, "transport_closed");

            _tracker.RecordClosed(150);

            Assert.AreEqual(2, _sink.Events.Count);
        }

        [Test]
        public void RecordClosed_LocalClosure_DistinguishesItFromPeerLoss()
        {
            _tracker.RecordGameConnected(
                Game(MultiplayerRole.Host),
                MultiplayerRole.Host,
                "id",
                false
            );

            _tracker.RecordClosed(150);

            Assert.AreEqual("closed", _sink.Events.Last().Parameters["connection_state"]);
        }

        [Test]
        public void RecordStopped_TransportLaterCloses_ReportsDistinctTransitions()
        {
            _tracker.RecordGameConnected(
                Game(MultiplayerRole.Host),
                MultiplayerRole.Host,
                "id",
                false
            );

            _tracker.RecordStopped(120);
            _tracker.RecordStopped(120);
            _tracker.RecordDisconnected(121, "transport_closed");

            CollectionAssert.AreEqual(
                new[] { "connected", "stopped", "disconnected" },
                _sink.Events.Select(entry => entry.Parameters["connection_state"])
            );
        }

        [Test]
        public void Reset_PreviousCampaign_DropsStaleConnectionContext()
        {
            _tracker.RecordGameConnected(
                Game(MultiplayerRole.Host),
                MultiplayerRole.Host,
                "id",
                false
            );
            _tracker.Reset();

            _tracker.RecordDisconnected(121, "transport_closed");

            Assert.AreEqual(1, _sink.Events.Count);
        }

        [Test]
        public void RecordDisconnected_ConsentWithdrawn_DoesNotRecordLaterEvents()
        {
            _tracker.RecordGameConnected(
                Game(MultiplayerRole.Host),
                MultiplayerRole.Host,
                "id",
                false
            );
            _sink.Enabled = false;

            _tracker.RecordDisconnected(120, "transport_closed");

            Assert.AreEqual(1, _sink.Events.Count);
        }

        [Test]
        public void RecordLobbyCreated_SinkFailure_DoesNotInterruptNetworking()
        {
            _sink.Fail = true;
            LogAssert.Expect(
                LogType.Warning,
                "Multiplayer analytics disabled for this session: sink unavailable"
            );

            Assert.DoesNotThrow(() =>
                _tracker.RecordLobbyCreated("lobby", "host-account", Summary())
            );
        }

        /// <summary>
        /// Creates the settings advertised by the test lobby.
        /// </summary>
        /// <returns>The lobby settings.</returns>
        private static GameSummary Summary() =>
            new GameSummary { PackID = "pack", PackVersion = "1" };

        /// <summary>
        /// Creates synchronized campaign identity for the requested participant.
        /// </summary>
        /// <param name="role">The local participant's role.</param>
        /// <returns>The initialized multiplayer world.</returns>
        private static GameRoot Game(MultiplayerRole role) =>
            new GameRoot
            {
                Summary = Summary(),
                CurrentTick = 100,
                LocalPlayerFactionID = role == MultiplayerRole.Host ? "FNALL1" : "FNEMP1",
                Metadata = new GameMetadata
                {
                    CampaignID = "campaign",
                    MultiplayerCampaign = new MultiplayerCampaignIdentity
                    {
                        HostUserID = "host-account",
                        GuestUserID = "guest-account",
                        HostFactionID = "FNALL1",
                        GuestFactionID = "FNEMP1",
                    },
                },
            };

        private sealed class Sink : IAnalyticsEventSink
        {
            internal readonly List<Entry> Events = new();
            internal bool Enabled = true;
            internal bool Fail;
            public bool IsAvailable => true;
            public bool IsCollectionEnabled => Enabled;

            /// <summary>
            /// Captures an independent event payload or simulates a destination failure.
            /// </summary>
            /// <param name="eventName">The event schema name.</param>
            /// <param name="parameters">The recorded event values.</param>
            public void RecordEvent(
                string eventName,
                IReadOnlyDictionary<string, object> parameters
            )
            {
                if (Fail)
                    throw new InvalidOperationException("sink unavailable");
                Events.Add(new Entry(eventName, new Dictionary<string, object>(parameters)));
            }

            /// <summary>
            /// Completes a flush without contacting an external service.
            /// </summary>
            public void Flush() { }
        }

        private sealed class Entry
        {
            internal readonly string Name;
            internal readonly Dictionary<string, object> Parameters;

            /// <summary>
            /// Retains the event name and detached parameters for assertions.
            /// </summary>
            /// <param name="name">The recorded event name.</param>
            /// <param name="parameters">The copied event parameters.</param>
            internal Entry(string name, Dictionary<string, object> parameters)
            {
                Name = name;
                Parameters = parameters;
            }
        }
    }
}
