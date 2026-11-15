using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using UnityEngine;

/// <summary>
/// Records campaign creation and coarse faction balance at fixed tick intervals.
/// </summary>
internal sealed class CampaignAnalyticsTracker
{
    internal const int CheckpointIntervalTicks = 100;

    private readonly GameRoot _game;
    private readonly IAnalyticsEventSink _sink;
    private readonly string _playerFactionId;
    private readonly HashSet<string> _aiFactionIds;
    private readonly bool _newCampaign;
    private readonly bool _multiplayer;
    private readonly bool _reportsCampaign;
    private bool _started;
    private bool _finished;
    private int _lastCheckpointTick = -1;
    private bool _failed;

    /// <summary>
    /// Gets whether initial synchronization still owes this new campaign its start event.
    /// </summary>
    internal bool HasPendingMultiplayerStart => _multiplayer && _newCampaign && !_started;

    /// <summary>
    /// Creates analytics tracking for one active campaign.
    /// </summary>
    /// <param name="game">The active campaign state.</param>
    /// <param name="sink">The consent-aware analytics destination.</param>
    /// <param name="newCampaign">Whether this session created the campaign.</param>
    internal CampaignAnalyticsTracker(GameRoot game, IAnalyticsEventSink sink, bool newCampaign)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        EnsureCampaignId();
        _playerFactionId = ResolvePlayerFactionId(game);
        _newCampaign = newCampaign;
        _multiplayer = game.Metadata?.IsMultiplayer == true;
        _reportsCampaign =
            !_multiplayer || game.Metadata.MultiplayerCampaign.HostFactionID == _playerFactionId;
        _aiFactionIds = game.GetPlayers()
            .Where(player => player?.ControllerType == PlayerControllerType.AI)
            .Select(player => player.FactionID)
            .Where(factionId =>
                !string.IsNullOrWhiteSpace(factionId)
                && !string.Equals(factionId, _playerFactionId, StringComparison.Ordinal)
            )
            .ToHashSet(StringComparer.Ordinal);

        if (newCampaign && !_multiplayer)
            RecordCampaignStarted();
    }

    /// <summary>
    /// Records a new multiplayer campaign only after its shared state has synchronized.
    /// </summary>
    internal void RecordMultiplayerStarted()
    {
        if (_multiplayer && _newCampaign)
            RecordCampaignStarted();
    }

    /// <summary>
    /// Records one terminal faction outcome through the same consent-aware campaign sink.
    /// </summary>
    /// <param name="result">The victory published by the active simulation.</param>
    internal void RecordVictory(VictoryResult result)
    {
        if (_finished || result?.Winner == null || !CanRecord())
            return;
        try
        {
            Dictionary<string, object> parameters = BuildSnapshotParameters();
            parameters["event_id"] = _game.Metadata.CampaignID + ":finished";
            parameters["winner_faction_id"] = result.Winner.InstanceID;
            parameters["winner_role"] = ResolveRole(result.Winner.InstanceID);
            parameters["current_tick"] = result.Tick;
            _sink.RecordEvent("campaign_finished", parameters);
            _finished = true;
            _sink.Flush();
        }
        catch (Exception exception)
        {
            DisableAfterFailure(exception);
        }
    }

    /// <summary>
    /// Emits the current campaign balance after each completed 100-tick interval.
    /// </summary>
    internal void RecordTickCompleted()
    {
        if (!CanRecord())
            return;

        int tick = _game.CurrentTick;
        if (tick <= 0 || tick % CheckpointIntervalTicks != 0 || tick == _lastCheckpointTick)
            return;

        try
        {
            Dictionary<string, object> parameters = BuildSnapshotParameters();
            parameters["event_id"] = _game.Metadata.CampaignID + ":checkpoint:" + tick;
            _sink.RecordEvent("campaign_checkpoint", parameters);
            _lastCheckpointTick = tick;
        }
        catch (Exception exception)
        {
            DisableAfterFailure(exception);
        }
    }

    /// <summary>
    /// Records creation of a new campaign and its selected player faction.
    /// </summary>
    private void RecordCampaignStarted()
    {
        if (_started || !CanRecord())
            return;

        try
        {
            Dictionary<string, object> parameters = BuildSnapshotParameters();
            parameters["event_id"] = _game.Metadata.CampaignID + ":started";
            _sink.RecordEvent("campaign_started", parameters);
            _started = true;
        }
        catch (Exception exception)
        {
            DisableAfterFailure(exception);
        }
    }

    /// <summary>
    /// Returns whether collection is currently available and permitted.
    /// </summary>
    /// <returns>True when an analytics event may be recorded.</returns>
    private bool CanRecord()
    {
        return _reportsCampaign && !_failed && _sink.IsAvailable && _sink.IsCollectionEnabled;
    }

    /// <summary>
    /// Creates the campaign identity and current faction-balance parameters.
    /// </summary>
    /// <returns>The completed analytics payload.</returns>
    private Dictionary<string, object> BuildSnapshotParameters()
    {
        EnsureCampaignId();
        Dictionary<string, object> parameters = BuildGameSettings(_game.Summary);
        parameters["campaign_id"] = _game.Metadata?.CampaignID ?? string.Empty;
        parameters["player_faction_id"] = _playerFactionId ?? string.Empty;
        parameters["player_role"] = ResolveRole(_playerFactionId);
        parameters["multiplayer"] = _multiplayer;
        parameters["current_tick"] = _game.CurrentTick;
        AddBalanceParameters(parameters);
        return parameters;
    }

    /// <summary>
    /// Shares the established campaign settings fields with multiplayer lifecycle events.
    /// </summary>
    /// <param name="summary">The selected campaign settings, when available.</param>
    /// <returns>The common event fields without enumerating gameplay entities.</returns>
    internal static Dictionary<string, object> BuildGameSettings(GameSummary summary)
    {
        summary ??= new GameSummary();
        return new Dictionary<string, object>(24)
        {
            ["content_pack_id"] = summary.PackID ?? string.Empty,
            ["content_pack_version"] = summary.PackVersion ?? string.Empty,
            ["difficulty"] = summary.Difficulty.ToString(),
            ["galaxy_size"] = summary.GalaxySize.ToString(),
            ["victory_condition"] = summary.VictoryCondition.ToString(),
            ["resource_availability"] = summary.ResourceAvailability.ToString(),
            ["custom_game"] = summary.IsCustomGame,
        };
    }

    /// <summary>
    /// Identifies a result's participant role without transmitting another player's account identifier.
    /// </summary>
    /// <param name="factionID">The faction whose role is required.</param>
    /// <returns>The admitted multiplayer role, local player, AI, or an empty value.</returns>
    private string ResolveRole(string factionID)
    {
        MultiplayerCampaignIdentity identity = _game.Metadata?.MultiplayerCampaign;
        if (identity != null)
            return factionID == identity.HostFactionID ? "host"
                : factionID == identity.GuestFactionID ? "guest"
                : string.Empty;
        return IsPlayer(factionID) ? "player"
            : IsAI(factionID) ? "ai"
            : string.Empty;
    }

    /// <summary>
    /// Ensures the campaign has a stable identifier that persists with saves.
    /// </summary>
    private void EnsureCampaignId()
    {
        _game.Metadata ??= new GameMetadata();
        if (string.IsNullOrWhiteSpace(_game.Metadata.CampaignID))
            _game.Metadata.CampaignID = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Adds current player-versus-AI territorial and military totals.
    /// </summary>
    /// <param name="parameters">The event parameters to extend.</param>
    private void AddBalanceParameters(Dictionary<string, object> parameters)
    {
        FactionTotals player = new FactionTotals();
        FactionTotals ai = new FactionTotals();
        foreach (Faction faction in _game.GetFactions())
        {
            if (faction == null)
                continue;

            FactionTotals target =
                IsPlayer(faction.InstanceID) ? player
                : IsAI(faction.InstanceID) ? ai
                : null;
            if (target == null)
                continue;

            target.Planets += faction.GetOwnedUnitsByType<Planet>().Count;
            target.CapitalShips += faction.GetOwnedUnitsByType<CapitalShip>().Count;
            target.Starfighters += faction.GetOwnedUnitsByType<Starfighter>().Count;
            target.Regiments += faction.GetOwnedUnitsByType<Regiment>().Count;
        }

        parameters["ai_faction_count"] = _aiFactionIds.Count;
        parameters["player_planets"] = player.Planets;
        parameters["ai_planets"] = ai.Planets;
        parameters["player_capital_ships"] = player.CapitalShips;
        parameters["ai_capital_ships"] = ai.CapitalShips;
        parameters["player_starfighters"] = player.Starfighters;
        parameters["ai_starfighters"] = ai.Starfighters;
        parameters["player_regiments"] = player.Regiments;
        parameters["ai_regiments"] = ai.Regiments;
    }

    /// <summary>
    /// Disables campaign analytics after an unexpected analytics-only failure.
    /// </summary>
    /// <param name="exception">The failure that disabled collection.</param>
    private void DisableAfterFailure(Exception exception)
    {
        _failed = true;
        Debug.LogWarning($"Campaign analytics disabled for this session: {exception.Message}");
    }

    /// <summary>
    /// Resolves the local human faction without assuming a content-defined identifier.
    /// </summary>
    /// <param name="game">The active campaign.</param>
    /// <returns>The human or saved player-faction identifier, or an empty string.</returns>
    private static string ResolvePlayerFactionId(GameRoot game)
    {
        if (game.Metadata?.IsMultiplayer == true)
            return game.LocalPlayerFactionID;
        return game.GetPlayers()
                .FirstOrDefault(player => player?.ControllerType == PlayerControllerType.Human)
                ?.FactionID
            ?? game.Summary?.PlayerFactionID
            ?? string.Empty;
    }

    /// <summary>
    /// Returns whether an owner identifier belongs to the local player.
    /// </summary>
    /// <param name="factionId">The owner identifier.</param>
    /// <returns>True when the owner is the player faction.</returns>
    private bool IsPlayer(string factionId)
    {
        return !string.IsNullOrEmpty(factionId)
            && string.Equals(factionId, _playerFactionId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns whether an owner identifier belongs to an AI participant.
    /// </summary>
    /// <param name="factionId">The owner identifier.</param>
    /// <returns>True when the owner is controlled by AI.</returns>
    private bool IsAI(string factionId)
    {
        return !string.IsNullOrEmpty(factionId) && _aiFactionIds.Contains(factionId);
    }

    /// <summary>
    /// Holds current force totals for one analytics side.
    /// </summary>
    private sealed class FactionTotals
    {
        internal int Planets;
        internal int CapitalShips;
        internal int Starfighters;
        internal int Regiments;
    }
}
