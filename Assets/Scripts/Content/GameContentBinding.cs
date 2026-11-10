using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;

/// <summary>
/// Validates saved content identity and restores authored settings before a world becomes active.
/// </summary>
internal sealed class GameContentBinding
{
    private readonly ContentPack _contentPack;

    /// <summary>
    /// Binds game preparation to one loaded pack and its authored data.
    /// </summary>
    /// <param name="contentPack">The active content pack.</param>
    internal GameContentBinding(ContentPack contentPack)
    {
        _contentPack = contentPack ?? throw new ArgumentNullException(nameof(contentPack));
    }

    /// <summary>
    /// Prepares a new or restored world without publishing it into the active session.
    /// </summary>
    /// <param name="game">The incoming world to validate and reconcile.</param>
    internal void Prepare(GameRoot game)
    {
        ValidateGameContent(game);
        ReconcileStaticContent(game);
    }

    /// <summary>
    /// Verifies that a game was created by the active pack, version, and scenario.
    /// </summary>
    /// <param name="game">The game whose content identity is being validated.</param>
    private void ValidateGameContent(GameRoot game)
    {
        if (game?.Summary == null)
            throw new InvalidOperationException("Game content identity is missing.");

        GameSummary summary = game.Summary;
        if (!_contentPack.MatchesContentIdentity(summary))
        {
            string requiredMods = FormatModIdentity(summary.ModIDs, summary.ModVersions);
            string activeMods = FormatModIdentity(
                _contentPack.FileResolver.Mods.Select(mod => mod.ID).ToArray(),
                _contentPack.FileResolver.Mods.Select(mod => mod.Version).ToArray()
            );
            throw new InvalidOperationException(
                $"Save requires content pack '{summary.PackID}' version '{summary.PackVersion}' "
                    + $"scenario '{summary.ScenarioID}' with mods [{requiredMods}], but "
                    + $"'{_contentPack.Definition.ID}' version '{_contentPack.Definition.Version}' "
                    + $"scenario '{_contentPack.Scenario.ID}' with mods [{activeMods}] is active."
            );
        }
    }

    /// <summary>
    /// Restores immutable authored state from the active content pack.
    /// </summary>
    /// <param name="game">The game whose authored state will be restored.</param>
    private void ReconcileStaticContent(GameRoot game)
    {
        ReconcileFactionSettings(game);
        ReconcileGalaxyLayout(game);
    }

    /// <summary>
    /// Restores faction configuration from saved launch rules or the active content pack.
    /// </summary>
    /// <param name="game">The game whose faction settings will be restored.</param>
    private void ReconcileFactionSettings(GameRoot game)
    {
        Dictionary<string, FactionSettings> overrides = GetFactionSettingOverrides(game);
        foreach (Faction faction in game.GetFactions())
        {
            if (faction == null || string.IsNullOrWhiteSpace(faction.InstanceID))
                throw new InvalidOperationException(
                    "Loaded game contains an unidentified faction."
                );

            Faction template = _contentPack.GameData.Factions.FirstOrDefault(candidate =>
                candidate?.InstanceID == faction.InstanceID
            );
            if (template == null)
                throw new InvalidOperationException(
                    $"Loaded faction '{faction.InstanceID}' is missing from the active content pack."
                );

            FactionSettings settings = overrides.TryGetValue(
                faction.InstanceID,
                out FactionSettings factionOverride
            )
                ? factionOverride
                : template.Settings;
            faction.Settings = settings.CreateCopy();
        }
    }

    /// <summary>
    /// Reads and validates launch-specific faction settings stored with the game.
    /// </summary>
    /// <param name="game">The game whose launch configuration will be read.</param>
    /// <returns>Faction settings indexed by faction instance ID.</returns>
    private Dictionary<string, FactionSettings> GetFactionSettingOverrides(GameRoot game)
    {
        Dictionary<string, FactionSettings> overrides = new Dictionary<string, FactionSettings>(
            StringComparer.Ordinal
        );
        GameGenerationConfig config = game.Summary.CustomSettings?.GenerationConfig;
        if (config == null)
            return overrides;

        foreach (
            FactionModifierConfig modifier in config.FactionModifiers
                ?? Enumerable.Empty<FactionModifierConfig>()
        )
        {
            if (modifier == null || string.IsNullOrWhiteSpace(modifier.FactionID))
                throw new InvalidOperationException(
                    "Game generation config contains an unidentified faction modifier."
                );
            if (modifier.Settings == null)
                throw new InvalidOperationException(
                    $"Faction modifier '{modifier.FactionID}' has no settings."
                );
            if (
                !_contentPack.GameData.Factions.Any(faction =>
                    faction?.InstanceID == modifier.FactionID
                )
            )
                throw new InvalidOperationException(
                    $"Faction modifier '{modifier.FactionID}' is missing from the active content pack."
                );
            if (overrides.ContainsKey(modifier.FactionID))
                throw new InvalidOperationException(
                    $"Game generation config contains duplicate faction modifier '{modifier.FactionID}'."
                );

            overrides.Add(modifier.FactionID, modifier.Settings);
        }

        return overrides;
    }

    /// <summary>
    /// Restores authored sector and planet coordinates from the active content pack.
    /// </summary>
    /// <param name="game">The game whose galaxy layout will be restored.</param>
    private void ReconcileGalaxyLayout(GameRoot game)
    {
        foreach (PlanetSector sector in game.GetGalaxyMap().GetChildren<PlanetSector>())
        {
            PlanetSector template = _contentPack.GameData.PlanetSectors.FirstOrDefault(candidate =>
                candidate?.TypeID == sector.TypeID
            );
            if (template == null)
                continue;

            sector.PositionX = template.PositionX;
            sector.PositionY = template.PositionY;
            foreach (Planet planet in sector.GetChildren<Planet>())
            {
                Planet planetTemplate = template
                    .GetChildren<Planet>()
                    .FirstOrDefault(candidate => candidate?.InstanceID == planet.InstanceID);
                if (planetTemplate == null)
                    continue;

                planet.PositionX = planetTemplate.PositionX;
                planet.PositionY = planetTemplate.PositionY;
            }
        }
    }

    /// <summary>
    /// Formats ordered mod identifiers and versions for a compatibility error.
    /// </summary>
    /// <param name="modIDs">The ordered mod identifiers.</param>
    /// <param name="modVersions">The versions corresponding to the identifiers.</param>
    /// <returns>A comma-separated ordered mod identity.</returns>
    private static string FormatModIdentity(string[] modIDs, string[] modVersions)
    {
        string[] ids = modIDs ?? Array.Empty<string>();
        string[] versions = modVersions ?? Array.Empty<string>();
        return string.Join(
            ", ",
            ids.Select((id, index) => $"{id}@{(index < versions.Length ? versions[index] : "?")}")
        );
    }
}
