using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Game;

/// <summary>
/// Owns preparation, commitment, and recovery for one game or content-selection load.
/// The operation survives reconstruction of the application services that consume its content.
/// </summary>
internal sealed class ContentLoadOperation
{
    private enum LoadState
    {
        Created,
        Preparing,
        Loading,
        Restoring,
        Ready,
        Restored,
        Failed,
    }

    private readonly ContentPack _previousPack;
    private readonly ContentPack _previousMenuPack;
    private readonly UserContentSettings _previousSettings;
    private readonly string[] _applicationPreloadIDs;
    private readonly object _stateLock = new object();
    private ContentPack _candidate;
    private string[] _disabledModIDs;
    private string _saveFileName;
    private GameSummary _customGameSummary;
    private bool _isContentReload;
    private bool _rememberModOrder;
    private LoadState _state;

    /// <summary>
    /// Gets the content that the current loading or recovery attempt must initialize.
    /// </summary>
    internal ContentPack Pack =>
        IsRecovering ? _previousMenuPack ?? _previousPack : _candidate ?? _previousPack;

    /// <summary>
    /// Gets the menu content to keep available while a saved campaign is active.
    /// </summary>
    internal ContentPack MenuPack =>
        SaveFileName == null ? null : _previousMenuPack ?? _previousPack;

    /// <summary>
    /// Gets whether this operation replaces application content and therefore requires reconstruction.
    /// </summary>
    internal bool RequiresContentRestart =>
        _candidate != null || (IsRecovering && _previousMenuPack != null);

    /// <summary>
    /// Gets whether replacement content and its preferences have already committed successfully.
    /// </summary>
    internal bool HasCommittedContent => _state == LoadState.Ready && _candidate != null;

    /// <summary>
    /// Gets whether this operation is recovering, has recovered, or cannot recover further.
    /// </summary>
    internal bool IsRecovering =>
        _state is LoadState.Restoring or LoadState.Restored or LoadState.Failed;

    /// <summary>
    /// Gets the destination for the current attempt, including recovery navigation.
    /// </summary>
    internal string DestinationScene =>
        IsRecovering || _isContentReload ? "MainMenu" : "StrategyView";

    /// <summary>
    /// Gets the requested save, or null for a content selection or recovery to the main menu.
    /// </summary>
    internal string SaveFileName => IsRecovering ? null : _saveFileName;

    /// <summary>
    /// Gets an independent custom-game configuration, or null during other loads and recovery.
    /// </summary>
    internal GameSummary CustomGameSummary =>
        IsRecovering ? null : _customGameSummary?.CreateCopy();

    /// <summary>
    /// Captures the active content and committed preferences before any preparation starts.
    /// </summary>
    /// <param name="previousPack">The content to retain if loading fails.</param>
    /// <param name="previousSettings">The preferences to retain if loading fails.</param>
    /// <param name="applicationPreloadIDs">The application manifests required by replacement content.</param>
    /// <param name="previousMenuPack">The new-game content retained while playing a saved campaign.</param>
    internal ContentLoadOperation(
        ContentPack previousPack,
        UserContentSettings previousSettings,
        IEnumerable<string> applicationPreloadIDs,
        ContentPack previousMenuPack = null
    )
    {
        _previousPack = previousPack ?? throw new ArgumentNullException(nameof(previousPack));
        _previousSettings = CopySettings(previousSettings);
        _previousMenuPack = previousMenuPack;
        _applicationPreloadIDs = (
            applicationPreloadIDs ?? throw new ArgumentNullException(nameof(applicationPreloadIDs))
        ).ToArray();
    }

    /// <summary>
    /// Opens and validates a requested content selection without changing the running application.
    /// </summary>
    /// <param name="packID">The installed pack to select.</param>
    /// <param name="disabledModIDs">The complete pending disabled-mod preference list.</param>
    /// <param name="preferredModOrder">The staged load order for the selected pack.</param>
    /// <returns>A task that completes when replacement content is ready for reconstruction.</returns>
    internal async Task PrepareContentReloadAsync(
        string packID,
        IEnumerable<string> disabledModIDs,
        IReadOnlyList<string> preferredModOrder = null
    )
    {
        if (string.IsNullOrWhiteSpace(packID))
            throw new ArgumentException("A game pack identifier is required.", nameof(packID));

        BeginPreparation();
        _isContentReload = true;
        _rememberModOrder = true;
        string[] disabled = (disabledModIDs ?? Array.Empty<string>()).ToArray();
        string[] order = (
            preferredModOrder ?? _previousSettings.GetModLoadOrder(packID)
        )?.ToArray();
        ContentPack candidate = await Task.Run(() =>
            {
                ContentPackDefinition definition = ContentPackLoader
                    .DiscoverPacks(_previousPack.ContentRootPath)
                    .FirstOrDefault(pack =>
                        string.Equals(pack.ID, packID, StringComparison.Ordinal)
                    );
                if (definition == null)
                    throw new InvalidOperationException($"Game pack '{packID}' is not installed.");

                return OpenValidatedPack(packID, disabled, order);
            })
            .ConfigureAwait(false);
        AcceptPreparation(candidate, disabled);
    }

    /// <summary>
    /// Resolves the exact content required by a save while preserving new-game preferences.
    /// </summary>
    /// <param name="saves">The persistence service that owns the requested save.</param>
    /// <param name="fileName">The save identifier to load.</param>
    /// <returns>A task that completes when the save's content has been verified.</returns>
    internal async Task PrepareSavedGameAsync(SaveGameManager saves, string fileName)
    {
        if (saves == null)
            throw new ArgumentNullException(nameof(saves));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A save file name is required.", nameof(fileName));

        BeginPreparation();
        _saveFileName = fileName;
        SaveContentSelection selection = await Task.Run(() =>
            {
                GameMetadata metadata = saves.GetSaveMetadata(fileName);
                if (_previousPack.MatchesContentIdentity(metadata))
                    return null;

                SaveContentSelection required = SaveContentResolver.Resolve(
                    _previousPack,
                    metadata
                );
                ValidateApplicationPreloads(required.Pack);
                return required;
            })
            .ConfigureAwait(false);
        AcceptPreparation(selection?.Pack, null);
    }

    /// <summary>
    /// Restores the content retained from the main menu before loading a saved campaign.
    /// </summary>
    /// <param name="menuPack">The verified content from the previous main menu.</param>
    internal void PrepareMenuReturn(ContentPack menuPack)
    {
        BeginPreparation();
        _isContentReload = true;
        AcceptPreparation(menuPack, null);
    }

    /// <summary>
    /// Validates a custom-game launch and stages its content without committing preferences.
    /// </summary>
    /// <param name="contentPack">The selected content already opened by the custom-game menu.</param>
    /// <param name="summary">The complete custom-game configuration.</param>
    /// <param name="rememberModOrder">Whether the launch uses the player's new-game order.</param>
    internal void PrepareCustomGame(
        ContentPack contentPack,
        GameSummary summary,
        bool rememberModOrder = true
    )
    {
        if (contentPack == null)
            throw new ArgumentNullException(nameof(contentPack));
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));
        if (!contentPack.MatchesContentIdentity(summary))
            throw new InvalidOperationException(
                "The custom-game configuration does not match its selected game pack."
            );

        BeginPreparation();
        ValidateApplicationPreloads(contentPack);
        _customGameSummary = summary.CreateCopy();
        _rememberModOrder =
            rememberModOrder
            && _previousSettings.GetModLoadOrder(contentPack.Definition.ID) == null
            && !contentPack.FileResolver.ModIssues.Any(issue => issue.BlocksSelection);
        UserContentSettings requested = CopySettings(_previousSettings);
        if (
            rememberModOrder
            && _previousSettings.GetModLoadOrder(contentPack.Definition.ID) == null
        )
            requested.SetDisabledModsForPack(
                contentPack.FileResolver.AvailableMods.Select(mod => mod.ID),
                contentPack
                    .FileResolver.AvailableMods.Select(mod => mod.ID)
                    .Except(
                        contentPack.FileResolver.Mods.Select(mod => mod.ID),
                        StringComparer.Ordinal
                    )
            );
        AcceptPreparation(contentPack, requested.DisabledModIDs);
    }

    /// <summary>
    /// Admits one recovery attempt and selects the previous content without retaining the failed launch.
    /// </summary>
    /// <returns>True when recovery may proceed; false after recovery or successful completion.</returns>
    internal bool BeginRecovery()
    {
        lock (_stateLock)
        {
            bool canRecover =
                _state is LoadState.Created or LoadState.Preparing or LoadState.Loading
                || (_state == LoadState.Ready && _candidate == null);
            if (!canRecover)
                return false;

            _state = LoadState.Restoring;
            return true;
        }
    }

    /// <summary>
    /// Verifies initialization and commits replacement preferences or finishes recovery.
    /// </summary>
    /// <param name="activePack">The exact content whose destination finished initialization.</param>
    /// <param name="settings">The preferences owned by the initialized application services.</param>
    internal void Complete(ContentPack activePack, UserSettingsManager settings)
    {
        lock (_stateLock)
        {
            if (_state is LoadState.Ready or LoadState.Restored)
                return;
            if (_state is not (LoadState.Loading or LoadState.Restoring))
                throw new InvalidOperationException("Content is not ready to finish loading.");
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (!ReferenceEquals(activePack, Pack))
                throw new InvalidOperationException(
                    "A different content selection finished loading."
                );

            if (IsRecovering)
            {
                RestoreSettings(settings.Settings.Content);
                _state = LoadState.Restored;
                return;
            }

            if (_candidate != null && _disabledModIDs != null)
            {
                UserContentSettings content = settings.Settings.Content;
                content.ActivePackID = _candidate.Definition.ID;
                content.ActiveScenarioID = _candidate.Scenario.ID;
                content.DisabledModIDs = _disabledModIDs
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                if (_rememberModOrder)
                    content.SetModLoadOrder(
                        _candidate.Definition.ID,
                        _candidate.FileResolver.Mods.Select(mod => mod.ID),
                        _candidate.FileResolver.Mods.Select(mod => mod.Version)
                    );
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
            _state = LoadState.Ready;
        }
    }

    /// <summary>
    /// Ends recovery after an unrecoverable service or scene initialization failure.
    /// </summary>
    internal void Fail()
    {
        lock (_stateLock)
            _state = LoadState.Failed;
    }

    /// <summary>
    /// Admits preparation exactly once for this operation.
    /// </summary>
    private void BeginPreparation()
    {
        lock (_stateLock)
        {
            if (_state != LoadState.Created)
                throw new InvalidOperationException(
                    "A content load operation can only be prepared once."
                );
            _state = LoadState.Preparing;
        }
    }

    /// <summary>
    /// Publishes completed preparation unless recovery has already superseded the request.
    /// </summary>
    /// <param name="candidate">The verified replacement, or null when existing content is sufficient.</param>
    /// <param name="disabledModIDs">The complete preference selection for a replacement.</param>
    private void AcceptPreparation(ContentPack candidate, string[] disabledModIDs)
    {
        lock (_stateLock)
        {
            if (_state != LoadState.Preparing)
                return;
            _candidate = candidate;
            _disabledModIDs = disabledModIDs;
            _state = LoadState.Loading;
        }
    }

    /// <summary>
    /// Opens one exact content selection and verifies its application preload manifests.
    /// </summary>
    /// <param name="packID">The required installed pack.</param>
    /// <param name="disabledModIDs">The mods excluded from this selection.</param>
    /// <param name="order">The requested priority of mod IDs.</param>
    /// <returns>The verified pack.</returns>
    private ContentPack OpenValidatedPack(string packID, string[] disabledModIDs, string[] order)
    {
        ContentPack pack = ContentPackLoader.OpenDefaultScenario(
            _previousPack.ContentRootPath,
            packID,
            disabledModIDs,
            order,
            requireValidSelection: true
        );
        ValidateApplicationPreloads(pack);
        CampaignContentValidator.ValidateCampaigns(pack);
        return pack;
    }

    /// <summary>
    /// Verifies the application manifests before the outgoing services are released.
    /// </summary>
    /// <param name="pack">The candidate content whose manifests must be valid.</param>
    private void ValidateApplicationPreloads(ContentPack pack)
    {
        foreach (string preloadID in _applicationPreloadIDs)
            ContentPackLoader.LoadApplicationPreloadManifest(pack.FileResolver, preloadID);
    }

    /// <summary>
    /// Restores content fields without changing unrelated application preferences.
    /// </summary>
    /// <param name="content">The preferences to restore.</param>
    private void RestoreSettings(UserContentSettings content)
    {
        content.ActivePackID = _previousSettings.ActivePackID;
        content.ActiveScenarioID = _previousSettings.ActiveScenarioID;
        content.DisabledModIDs = _previousSettings.DisabledModIDs.ToArray();
        content.HasModLoadOrderPreferences = _previousSettings.HasModLoadOrderPreferences;
        content.ModLoadOrders = _previousSettings.ModLoadOrders.ToArray();
    }

    /// <summary>
    /// Copies content preferences for independent staging and recovery.
    /// </summary>
    /// <param name="settings">The preferences to copy.</param>
    /// <returns>An independent preference snapshot.</returns>
    private static UserContentSettings CopySettings(UserContentSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        return new UserContentSettings
        {
            ActivePackID = settings.ActivePackID,
            ActiveScenarioID = settings.ActiveScenarioID,
            DisabledModIDs = (settings.DisabledModIDs ?? Array.Empty<string>()).ToArray(),
            HasModLoadOrderPreferences = settings.HasModLoadOrderPreferences,
            ModLoadOrders = (settings.ModLoadOrders ?? Array.Empty<ContentModOrderPreference>())
                .Where(order => order != null)
                .Select(order => new ContentModOrderPreference
                {
                    PackID = order.PackID,
                    ModIDs = order.ModIDs?.ToArray() ?? Array.Empty<string>(),
                    ModVersions = order.ModVersions?.ToArray(),
                })
                .ToArray(),
        };
    }
}
