using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Rebellion.Game;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Records consented lobby creation and local connection transitions without touching simulation state.
    /// </summary>
    internal sealed class MultiplayerAnalyticsTracker
    {
        private readonly IAnalyticsEventSink _sink;
        private Dictionary<string, object> _context;
        private string _lobbyID;
        private string _campaignID;
        private string _initializationID;
        private string _localUserID;
        private string _playerID;
        private string _phase;
        private bool _connected;
        private bool _stopped;
        private bool _failed;

        /// <summary>
        /// Uses the application's existing consent-aware analytics destination.
        /// </summary>
        /// <param name="sink">The shared analytics destination.</param>
        internal MultiplayerAnalyticsTracker(IAnalyticsEventSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>
        /// Counts one successfully published host lobby using its service-issued identity.
        /// </summary>
        /// <param name="lobbyID">The published lobby identifier.</param>
        /// <param name="localUserID">The local participant's authenticated product user identifier.</param>
        /// <param name="summary">The advertised game settings.</param>
        internal void RecordLobbyCreated(string lobbyID, string localUserID, GameSummary summary)
        {
            if (_context != null && _lobbyID == lobbyID)
                return;
            BeginLobby(lobbyID, localUserID, summary, "host");
            Emit("multiplayer_lobby_created", null, null, null);
        }

        /// <summary>
        /// Records the guest's connection only after the host accepts admission.
        /// </summary>
        /// <param name="lobbyID">The admitted lobby identifier.</param>
        /// <param name="localUserID">The local participant's authenticated product user identifier.</param>
        /// <param name="summary">The accepted game settings.</param>
        internal void RecordLobbyJoined(string lobbyID, string localUserID, GameSummary summary)
        {
            if (_context == null || _lobbyID != lobbyID)
                BeginLobby(lobbyID, localUserID, summary, "guest");
            RecordLobbyConnected();
        }

        /// <summary>
        /// Records an admitted peer without counting repeated roster callbacks as new connections.
        /// </summary>
        internal void RecordLobbyConnected()
        {
            if (_context == null || _connected)
                return;
            _connected = true;
            Emit("multiplayer_connection_changed", "connected", null, null);
        }

        /// <summary>
        /// Associates loading failures with the launch identity without counting an unready game as started.
        /// </summary>
        /// <param name="initializationID">The agreed identity of the game being loaded.</param>
        internal void BeginGameLoad(string initializationID)
        {
            if (_context == null)
                return;
            _initializationID = initializationID;
            _context["initialization_id"] = initializationID;
            _phase = "loading";
        }

        /// <summary>
        /// Records shared gameplay readiness after the state handshake completes.
        /// </summary>
        /// <param name="game">The synchronized campaign.</param>
        /// <param name="role">The local admitted role.</param>
        /// <param name="initializationID">The agreed identity of this initialized game.</param>
        /// <param name="returning">Whether the coordinator completed its recovery flow.</param>
        internal void RecordGameConnected(
            GameRoot game,
            MultiplayerRole role,
            string initializationID,
            bool returning
        )
        {
            MultiplayerCampaignIdentity identity = game?.Metadata?.MultiplayerCampaign;
            if (identity == null || role == MultiplayerRole.None)
                return;
            if (
                _connected
                && _phase == "game"
                && _campaignID == game.Metadata.CampaignID
                && _initializationID == initializationID
            )
                return;
            _campaignID = game.Metadata.CampaignID;
            _initializationID = initializationID;
            _localUserID =
                role == MultiplayerRole.Host ? identity.HostUserID : identity.GuestUserID;
            _playerID = null;
            _phase = "game";
            _context = CampaignAnalyticsTracker.BuildGameSettings(game.Summary);
            _context["multiplayer"] = true;
            _context["lobby_id"] = _lobbyID ?? string.Empty;
            _context["campaign_id"] = _campaignID ?? string.Empty;
            _context["initialization_id"] = initializationID ?? string.Empty;
            _context["player_role"] = role == MultiplayerRole.Host ? "host" : "guest";
            _context["player_faction_id"] = game.LocalPlayerFactionID ?? string.Empty;
            _connected = true;
            _stopped = false;
            Emit(
                "multiplayer_connection_changed",
                returning ? "reconnected" : "connected",
                null,
                game.CurrentTick
            );
        }

        /// <summary>
        /// Records one observed peer departure until another connection is admitted.
        /// </summary>
        /// <param name="currentTick">The observed campaign tick, or null before a game exists.</param>
        /// <param name="reason">A fixed reason category without exception text or peer data.</param>
        internal void RecordDisconnected(int? currentTick, string reason)
        {
            if (!_connected)
                return;
            _connected = false;
            Emit("multiplayer_connection_changed", "disconnected", reason, currentTick);
        }

        /// <summary>
        /// Distinguishes a local session closure from an observed loss of the peer connection.
        /// </summary>
        /// <param name="currentTick">The observed campaign tick, when a game exists.</param>
        internal void RecordClosed(int? currentTick)
        {
            if (!_connected)
                return;
            _connected = false;
            Emit("multiplayer_connection_changed", "closed", "local_session_closed", currentTick);
        }

        /// <summary>
        /// Reports a stopped simulation separately from a closed transport.
        /// </summary>
        /// <param name="currentTick">The observed campaign tick, when available.</param>
        internal void RecordStopped(int? currentTick)
        {
            if (_context == null || _stopped)
                return;
            _stopped = true;
            Emit(
                "multiplayer_connection_changed",
                "stopped",
                "synchronization_stopped",
                currentTick
            );
        }

        /// <summary>
        /// Releases session context when the coordinator resets, without publishing another departure.
        /// </summary>
        internal void Reset()
        {
            _context = null;
            _lobbyID = _campaignID = _initializationID = _localUserID = _playerID = _phase = null;
            _connected = _stopped = _failed = false;
        }

        /// <summary>
        /// Captures lobby settings without reading the world or retaining mutable game objects.
        /// </summary>
        /// <param name="lobbyID">The service-issued lobby identifier.</param>
        /// <param name="localUserID">The reporting participant's product user identifier.</param>
        /// <param name="summary">The admitted settings.</param>
        /// <param name="role">The reporting participant's role.</param>
        private void BeginLobby(
            string lobbyID,
            string localUserID,
            GameSummary summary,
            string role
        )
        {
            Reset();
            _lobbyID = lobbyID;
            _localUserID = localUserID;
            _phase = "lobby";
            _context = CampaignAnalyticsTracker.BuildGameSettings(summary);
            _context["multiplayer"] = true;
            _context["lobby_id"] = lobbyID ?? string.Empty;
            _context["player_role"] = role;
        }

        /// <summary>
        /// Publishes a detached event only with consent, isolating analytics failures from networking.
        /// </summary>
        /// <param name="eventName">The established schema name.</param>
        /// <param name="state">The observed connection state, when applicable.</param>
        /// <param name="reason">The fixed reason category, when applicable.</param>
        /// <param name="currentTick">The observed campaign tick, when available.</param>
        private void Emit(string eventName, string state, string reason, int? currentTick)
        {
            if (_context == null || _failed)
                return;
            try
            {
                if (!_sink.IsAvailable || !_sink.IsCollectionEnabled)
                    return;
                if (_playerID == null)
                {
                    using SHA256 hash = SHA256.Create();
                    _playerID = string.IsNullOrWhiteSpace(_localUserID)
                        ? string.Empty
                        : BitConverter
                            .ToString(
                                hash.ComputeHash(
                                    Encoding.UTF8.GetBytes("rebellion2-analytics\0" + _localUserID)
                                )
                            )
                            .Replace("-", string.Empty);
                }
                Dictionary<string, object> parameters = new Dictionary<string, object>(_context)
                {
                    ["player_id"] = _playerID,
                    ["connection_phase"] = _phase,
                    ["event_id"] =
                        state == null ? _lobbyID + ":created" : Guid.NewGuid().ToString("N"),
                };
                if (state != null)
                    parameters["connection_state"] = state;
                if (reason != null)
                    parameters["connection_reason"] = reason;
                if (currentTick.HasValue)
                    parameters["current_tick"] = currentTick.Value;
                _sink.RecordEvent(eventName, parameters);
                _sink.Flush();
            }
            catch (Exception exception)
            {
                _failed = true;
                Debug.LogWarning(
                    $"Multiplayer analytics disabled for this session: {exception.Message}"
                );
            }
        }
    }
}
