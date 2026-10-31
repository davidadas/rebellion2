using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
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
    private int _lastCheckpointTick = -1;
    private bool _failed;

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
        _aiFactionIds = game.GetPlayers()
            .Where(player => player?.ControllerType == PlayerControllerType.AI)
            .Select(player => player.FactionID)
            .Where(factionId =>
                !string.IsNullOrWhiteSpace(factionId)
                && !string.Equals(factionId, _playerFactionId, StringComparison.Ordinal)
            )
            .ToHashSet(StringComparer.Ordinal);

        if (newCampaign)
            RecordCampaignStarted();
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
            _sink.RecordEvent("campaign_checkpoint", BuildSnapshotParameters());
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
        if (!CanRecord())
            return;

        try
        {
            _sink.RecordEvent("campaign_started", BuildSnapshotParameters());
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
        return !_failed && _sink.IsAvailable && _sink.IsCollectionEnabled;
    }

    /// <summary>
    /// Creates the campaign identity and current faction-balance parameters.
    /// </summary>
    /// <returns>The completed analytics payload.</returns>
    private Dictionary<string, object> BuildSnapshotParameters()
    {
        EnsureCampaignId();
        GameSummary summary = _game.Summary ?? new GameSummary();
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            ["campaign_id"] = _game.Metadata?.CampaignID ?? string.Empty,
            ["content_pack_id"] = summary.PackID ?? string.Empty,
            ["content_pack_version"] = summary.PackVersion ?? string.Empty,
            ["player_faction_id"] = _playerFactionId ?? string.Empty,
            ["difficulty"] = summary.Difficulty.ToString(),
            ["galaxy_size"] = summary.GalaxySize.ToString(),
            ["victory_condition"] = summary.VictoryCondition.ToString(),
            ["resource_availability"] = summary.ResourceAvailability.ToString(),
            ["custom_game"] = summary.IsCustomGame,
            ["current_tick"] = _game.CurrentTick,
        };
        AddBalanceParameters(parameters);
        return parameters;
    }

    /// <summary>
    /// Ensures the campaign has an anonymous identifier that persists with saves.
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

            target.Planets += faction.GetOwnedUnitsByType<Rebellion.Game.Galaxy.Planet>().Count;
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
