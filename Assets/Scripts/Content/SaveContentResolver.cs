using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;

/// <summary>
/// Represents an exact content selection reconstructed from saved-game metadata.
/// </summary>
internal sealed class SaveContentSelection
{
    /// <summary>
    /// Gets the verified content pack.
    /// </summary>
    internal ContentPack Pack { get; }

    /// <summary>
    /// Gets compatible mods excluded by the saved game.
    /// </summary>
    internal string[] DisabledModIDs { get; }

    /// <summary>
    /// Creates a verified saved-game content selection.
    /// </summary>
    /// <param name="pack">The verified pack.</param>
    /// <param name="disabledModIDs">Compatible mods excluded by the save.</param>
    internal SaveContentSelection(ContentPack pack, string[] disabledModIDs)
    {
        Pack = pack ?? throw new ArgumentNullException(nameof(pack));
        DisabledModIDs = disabledModIDs ?? Array.Empty<string>();
    }
}

/// <summary>
/// Describes the content work required before a saved game can enter the strategy scene.
/// </summary>
internal sealed class SaveLoadPreparation
{
    /// <summary>
    /// Gets the save identifier that will be loaded.
    /// </summary>
    internal string FileName { get; }

    /// <summary>
    /// Gets a replacement content selection when the active content does not match the save.
    /// </summary>
    internal SaveContentSelection ContentSelection { get; }

    /// <summary>
    /// Gets whether application content must be reconstructed before loading the save.
    /// </summary>
    internal bool RequiresContentRestart => ContentSelection != null;

    /// <summary>
    /// Creates a verified saved-game loading preparation.
    /// </summary>
    /// <param name="fileName">The save identifier to load.</param>
    /// <param name="contentSelection">The optional replacement content selection.</param>
    internal SaveLoadPreparation(string fileName, SaveContentSelection contentSelection)
    {
        FileName = string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("A save file name is required.", nameof(fileName))
            : fileName;
        ContentSelection = contentSelection;
    }
}

/// <summary>
/// Resolves the exact installed content identity required by saved-game metadata.
/// </summary>
internal static class SaveContentResolver
{
    /// <summary>
    /// Opens and verifies the exact pack, scenario, and mods required by a save.
    /// </summary>
    /// <param name="activePack">The active pack that identifies the shared content root.</param>
    /// <param name="metadata">The save metadata declaring the required content.</param>
    /// <returns>The verified content selection.</returns>
    internal static SaveContentSelection Resolve(ContentPack activePack, GameMetadata metadata)
    {
        if (activePack == null)
            throw new ArgumentNullException(nameof(activePack));
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        string[] requiredModIDs = metadata.ModIDs ?? Array.Empty<string>();
        string[] requiredModVersions = metadata.ModVersions ?? Array.Empty<string>();
        if (requiredModIDs.Length != requiredModVersions.Length)
        {
            throw new InvalidOperationException(
                "The save's content-mod identifiers and versions do not match."
            );
        }

        ContentFileResolver discovered = ContentPackLoader.DiscoverModResolver(
            activePack.ContentRootPath,
            metadata.PackID,
            Array.Empty<string>()
        );
        Dictionary<string, ContentModDefinition> availableMods =
            discovered.AvailableMods.ToDictionary(mod => mod.ID, StringComparer.Ordinal);
        HashSet<string> requiredModIDSet = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < requiredModIDs.Length; index++)
        {
            string requiredModID = requiredModIDs[index];
            if (!requiredModIDSet.Add(requiredModID))
            {
                throw new InvalidOperationException(
                    $"Save content lists mod '{requiredModID}' more than once."
                );
            }

            if (!availableMods.TryGetValue(requiredModID, out ContentModDefinition availableMod))
            {
                throw new InvalidOperationException(
                    $"Save requires content mod '{requiredModID}', but it is not installed."
                );
            }

            if (
                !string.Equals(
                    availableMod.Version,
                    requiredModVersions[index],
                    StringComparison.Ordinal
                )
            )
            {
                throw new InvalidOperationException(
                    $"Save requires content mod '{requiredModID}' version "
                        + $"'{requiredModVersions[index]}', but version '{availableMod.Version}' is installed."
                );
            }
        }

        string[] disabledModIDs = discovered
            .AvailableMods.Where(mod => !requiredModIDSet.Contains(mod.ID))
            .Select(mod => mod.ID)
            .ToArray();
        ContentPack requiredPack = ContentPackLoader.OpenExact(
            activePack.ContentRootPath,
            metadata.PackID,
            metadata.ScenarioID,
            disabledModIDs
        );
        if (!requiredPack.MatchesContentIdentity(metadata))
        {
            throw new InvalidOperationException(
                $"Save requires content pack '{metadata.PackID}' version "
                    + $"'{metadata.PackVersion}' and scenario '{metadata.ScenarioID}', but the installed "
                    + $"pack is version '{requiredPack.Definition.Version}' with scenario "
                    + $"'{requiredPack.Scenario.ID}'."
            );
        }

        return new SaveContentSelection(requiredPack, disabledModIDs);
    }
}
