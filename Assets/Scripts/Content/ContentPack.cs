using System;
using System.Collections.Generic;
using System.IO;
using Rebellion.Game;

/// <summary>
/// Represents one selected content pack, scenario, and composed game-data catalog.
/// </summary>
public sealed class ContentPack
{
    private readonly IReadOnlyDictionary<string, ContentPreloadManifest> preloadManifests;

    /// <summary>
    /// Gets the absolute root of the external content directory.
    /// </summary>
    public string ContentRootPath { get; }

    /// <summary>
    /// Gets the absolute root of this pack.
    /// </summary>
    public string PackRootPath { get; }

    /// <summary>
    /// Gets this pack's definition.
    /// </summary>
    public ContentPackDefinition Definition { get; }

    /// <summary>
    /// Gets the selected scenario definition.
    /// </summary>
    public ContentScenarioDefinition Scenario { get; }

    /// <summary>
    /// Gets the factions declared by this pack.
    /// </summary>
    public IReadOnlyList<ContentFactionDefinition> Factions { get; }

    /// <summary>
    /// Gets the typed game data composed from this pack and scenario.
    /// </summary>
    public GameDataCatalog GameData { get; }

    /// <summary>
    /// Creates one fully loaded content pack.
    /// </summary>
    /// <param name="contentRootPath">The absolute external content root.</param>
    /// <param name="packRootPath">The absolute pack root.</param>
    /// <param name="definition">The pack definition.</param>
    /// <param name="scenario">The selected scenario definition.</param>
    /// <param name="factions">The pack's faction definitions.</param>
    /// <param name="gameData">The composed typed game data.</param>
    /// <param name="preloadManifests">The pack's preload manifests by identifier.</param>
    internal ContentPack(
        string contentRootPath,
        string packRootPath,
        ContentPackDefinition definition,
        ContentScenarioDefinition scenario,
        IReadOnlyList<ContentFactionDefinition> factions,
        GameDataCatalog gameData,
        IReadOnlyDictionary<string, ContentPreloadManifest> preloadManifests
    )
    {
        ContentRootPath =
            contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath));
        PackRootPath = packRootPath ?? throw new ArgumentNullException(nameof(packRootPath));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        Factions = factions ?? throw new ArgumentNullException(nameof(factions));
        GameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
        this.preloadManifests =
            preloadManifests ?? throw new ArgumentNullException(nameof(preloadManifests));
    }

    /// <summary>
    /// Gets a required preload manifest declared by this pack.
    /// </summary>
    /// <param name="preloadID">The preload group identifier.</param>
    /// <returns>The configured preload manifest.</returns>
    internal ContentPreloadManifest GetPreloadManifest(string preloadID)
    {
        if (
            !preloadManifests.TryGetValue(
                preloadID ?? string.Empty,
                out ContentPreloadManifest manifest
            )
        )
            throw new InvalidDataException($"Content preload group '{preloadID}' is missing.");

        return manifest;
    }

    /// <summary>
    /// Checks whether a game summary belongs to this pack, version, and scenario.
    /// </summary>
    /// <param name="summary">The game summary to compare.</param>
    /// <returns>True when every content identity field matches.</returns>
    public bool MatchesContentIdentity(GameSummary summary)
    {
        return summary != null
            && MatchesContentIdentity(summary.PackID, summary.PackVersion, summary.ScenarioID);
    }

    /// <summary>
    /// Checks three serialized identity values against this pack.
    /// </summary>
    /// <param name="packID">The serialized pack identifier.</param>
    /// <param name="packVersion">The serialized pack version.</param>
    /// <param name="scenarioID">The serialized scenario identifier.</param>
    /// <returns>True when the serialized identity exactly matches this pack.</returns>
    private bool MatchesContentIdentity(string packID, string packVersion, string scenarioID)
    {
        return string.Equals(packID, Definition.ID, StringComparison.Ordinal)
            && string.Equals(packVersion, Definition.Version, StringComparison.Ordinal)
            && string.Equals(scenarioID, Scenario.ID, StringComparison.Ordinal);
    }
}
