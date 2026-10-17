using System;
using System.Linq;

/// <summary>
/// Owns the pending content selection and the previous selection until loading succeeds or recovers.
/// </summary>
internal sealed class ContentReplacement
{
    private enum ReplacementState
    {
        Loading,
        Restoring,
        Complete,
    }

    private readonly ContentReloadPreparation _candidate;
    private readonly UserContentSettings _previousSettings;
    private ReplacementState _state;

    internal ContentPack PreviousPack { get; }
    internal bool IsRestoring => _state == ReplacementState.Restoring;

    /// <summary>
    /// Captures the running content and committed preferences before a replacement starts.
    /// </summary>
    /// <param name="previousPack">The running content that recovery must reconstruct.</param>
    /// <param name="previousSettings">The content preferences to retain on failure.</param>
    /// <param name="candidate">The verified pending content selection.</param>
    internal ContentReplacement(
        ContentPack previousPack,
        UserContentSettings previousSettings,
        ContentReloadPreparation candidate
    )
    {
        PreviousPack = previousPack ?? throw new ArgumentNullException(nameof(previousPack));
        _previousSettings = CopySettings(
            previousSettings ?? throw new ArgumentNullException(nameof(previousSettings))
        );
        _candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
    }

    /// <summary>
    /// Creates a replacement for a save while preserving unrelated packs' mod preferences.
    /// </summary>
    /// <param name="previousPack">The running content.</param>
    /// <param name="previousSettings">The committed content preferences.</param>
    /// <param name="selection">The exact content required by the saved game.</param>
    /// <returns>The pending saved-game content replacement.</returns>
    internal static ContentReplacement ForSavedGame(
        ContentPack previousPack,
        UserContentSettings previousSettings,
        SaveContentSelection selection
    )
    {
        if (selection == null)
            throw new ArgumentNullException(nameof(selection));

        UserContentSettings requested = CopySettings(previousSettings);
        requested.SetDisabledModsForPack(
            selection.Pack.FileResolver.AvailableMods.Select(mod => mod.ID),
            selection.DisabledModIDs
        );
        return new ContentReplacement(
            previousPack,
            previousSettings,
            new ContentReloadPreparation(selection.Pack, requested.DisabledModIDs)
        );
    }

    /// <summary>
    /// Admits one reconstruction of the previous content after replacement loading fails.
    /// </summary>
    /// <returns>True when recovery was admitted; false after recovery or completion has begun.</returns>
    internal bool BeginRecovery()
    {
        if (_state != ReplacementState.Loading)
            return false;

        _state = ReplacementState.Restoring;
        GameLaunchContext.PrepareContentRestart(PreviousPack);
        return true;
    }

    /// <summary>
    /// Commits a successfully initialized replacement or completes restoration without saving it.
    /// </summary>
    /// <param name="activePack">The content whose scene has finished initialization.</param>
    /// <param name="settings">The settings owned by that content's application services.</param>
    internal void Complete(ContentPack activePack, UserSettingsManager settings)
    {
        if (_state == ReplacementState.Complete)
            return;
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        ContentPack expected = IsRestoring ? PreviousPack : _candidate.Pack;
        if (!ReferenceEquals(activePack, expected))
            throw new InvalidOperationException("A different content selection finished loading.");

        UserContentSettings content = settings.Settings.Content;
        if (IsRestoring)
        {
            RestoreSettings(content);
        }
        else
        {
            content.ActivePackID = _candidate.Pack.Definition.ID;
            content.ActiveScenarioID = _candidate.Pack.Scenario.ID;
            content.DisabledModIDs = _candidate
                .DisabledModIDs.Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            try
            {
                settings.Save();
            }
            catch
            {
                RestoreSettings(content);
                throw;
            }
        }

        _state = ReplacementState.Complete;
    }

    /// <summary>
    /// Restores content fields without changing unrelated application preferences.
    /// </summary>
    /// <param name="content">The content settings to restore.</param>
    private void RestoreSettings(UserContentSettings content)
    {
        content.ActivePackID = _previousSettings.ActivePackID;
        content.ActiveScenarioID = _previousSettings.ActiveScenarioID;
        content.DisabledModIDs = _previousSettings.DisabledModIDs.ToArray();
    }

    /// <summary>
    /// Copies content preferences without sharing their mutable disabled-mod array.
    /// </summary>
    /// <param name="settings">The preferences to copy.</param>
    /// <returns>An independent content-preference snapshot.</returns>
    private static UserContentSettings CopySettings(UserContentSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        return new UserContentSettings
        {
            ActivePackID = settings.ActivePackID,
            ActiveScenarioID = settings.ActiveScenarioID,
            DisabledModIDs = (settings.DisabledModIDs ?? Array.Empty<string>()).ToArray(),
        };
    }
}
