using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Util.IO;
using Rebellion.Util.Logging;
using UnityEngine;

/// <summary>
/// Loads, applies, captures, and saves user settings.
/// </summary>
public sealed class UserSettingsManager
{
    private const string _settingsFileName = "user-settings.json";

    private readonly AudioManager _audioManager;
    private readonly DisplayManager _displayManager;
    private readonly GraphicsManager _graphicsManager;
    private readonly InputManager _inputManager;
    private readonly IAnalyticsConsentRuntime _analyticsConsentRuntime;
    private readonly string _settingsFilePathOverride;
    private UserContentSettings _savedContent = new();

    /// <summary>
    /// Gets the active user settings.
    /// </summary>
    public UserSettings Settings { get; private set; }

    /// <summary>
    /// Creates a user settings manager for the supplied runtime systems.
    /// </summary>
    /// <param name="audioManager">The audio manager that receives audio settings.</param>
    /// <param name="displayManager">The display manager that receives video settings.</param>
    /// <param name="inputManager">The input manager that receives binding overrides.</param>
    public UserSettingsManager(
        AudioManager audioManager,
        DisplayManager displayManager,
        InputManager inputManager
    )
        : this(audioManager, displayManager, null, inputManager, null, null) { }

    /// <summary>
    /// Creates a user settings manager for the supplied runtime systems.
    /// </summary>
    /// <param name="audioManager">The audio manager that receives audio settings.</param>
    /// <param name="displayManager">The display manager that receives display settings.</param>
    /// <param name="graphicsManager">The graphics manager that receives URP settings.</param>
    /// <param name="inputManager">The input manager that receives binding overrides.</param>
    public UserSettingsManager(
        AudioManager audioManager,
        DisplayManager displayManager,
        GraphicsManager graphicsManager,
        InputManager inputManager
    )
        : this(audioManager, displayManager, graphicsManager, inputManager, null, null) { }

    /// <summary>
    /// Creates a user settings manager for every supplied runtime system.
    /// </summary>
    /// <param name="audioManager">The audio manager that receives audio settings.</param>
    /// <param name="displayManager">The display manager that receives display settings.</param>
    /// <param name="graphicsManager">The graphics manager that receives URP settings.</param>
    /// <param name="inputManager">The input manager that receives binding overrides.</param>
    /// <param name="analyticsConsentRuntime">The analytics consent runtime.</param>
    internal UserSettingsManager(
        AudioManager audioManager,
        DisplayManager displayManager,
        GraphicsManager graphicsManager,
        InputManager inputManager,
        IAnalyticsConsentRuntime analyticsConsentRuntime
    )
        : this(
            audioManager,
            displayManager,
            graphicsManager,
            inputManager,
            analyticsConsentRuntime,
            null
        ) { }

    /// <summary>
    /// Creates a user settings manager that uses the given file path.
    /// </summary>
    /// <param name="audioManager">The audio manager that receives audio settings.</param>
    /// <param name="displayManager">The display manager that receives video settings.</param>
    /// <param name="inputManager">The input manager that receives binding overrides.</param>
    /// <param name="settingsFilePath">The exact settings file path.</param>
    internal UserSettingsManager(
        AudioManager audioManager,
        DisplayManager displayManager,
        InputManager inputManager,
        string settingsFilePath
    )
        : this(audioManager, displayManager, null, inputManager, null, settingsFilePath) { }

    /// <summary>
    /// Creates a user settings manager that uses the given file path and graphics owner.
    /// </summary>
    /// <param name="audioManager">The audio manager that receives audio settings.</param>
    /// <param name="displayManager">The display manager that receives display settings.</param>
    /// <param name="graphicsManager">The graphics manager that receives URP settings.</param>
    /// <param name="inputManager">The input manager that receives binding overrides.</param>
    /// <param name="settingsFilePath">The exact settings file path.</param>
    internal UserSettingsManager(
        AudioManager audioManager,
        DisplayManager displayManager,
        GraphicsManager graphicsManager,
        InputManager inputManager,
        string settingsFilePath
    )
        : this(audioManager, displayManager, graphicsManager, inputManager, null, settingsFilePath)
    { }

    /// <summary>
    /// Creates a user settings manager with explicit runtime systems and persistence path.
    /// </summary>
    /// <param name="audioManager">The audio manager that receives audio settings.</param>
    /// <param name="displayManager">The display manager that receives display settings.</param>
    /// <param name="graphicsManager">The graphics manager that receives URP settings.</param>
    /// <param name="inputManager">The input manager that receives binding overrides.</param>
    /// <param name="analyticsConsentRuntime">The analytics consent runtime.</param>
    /// <param name="settingsFilePath">The exact settings file path.</param>
    internal UserSettingsManager(
        AudioManager audioManager,
        DisplayManager displayManager,
        GraphicsManager graphicsManager,
        InputManager inputManager,
        IAnalyticsConsentRuntime analyticsConsentRuntime,
        string settingsFilePath
    )
    {
        _audioManager = audioManager;
        _displayManager = displayManager ?? throw new ArgumentNullException(nameof(displayManager));
        _graphicsManager = graphicsManager;
        _inputManager = inputManager;
        _analyticsConsentRuntime = analyticsConsentRuntime;
        _settingsFilePathOverride = settingsFilePath;
    }

    /// <summary>
    /// Reads the saved content selection from the settings file before the settings
    /// system is initialized.
    /// </summary>
    /// <returns>The persisted preferences, or new-installation defaults.</returns>
    public static UserContentSettings ReadContentSelection()
    {
        try
        {
            string path = Path.Combine(UserDataPaths.GameRoot, _settingsFileName);
            if (File.Exists(path))
            {
                UserSettings settings = JsonUtility.FromJson<UserSettings>(
                    AtomicTextFile.ReadAllText(path)
                );
                if (settings?.Content != null)
                {
                    settings.Content.Normalize();
                    return settings.Content;
                }
            }
        }
        catch (Exception exception)
        {
            GameLogger.Warning($"Failed to read content settings: {exception.Message}");
        }
        return new UserContentSettings { HasModLoadOrderPreferences = true };
    }

    /// <summary>
    /// Updates only content selection from the companion toolkit without capturing runtime settings.
    /// </summary>
    /// <param name="contentRoot">The installed content used to resolve compatible add-ons.</param>
    /// <param name="packID">The selected pack, or null to retain the saved selection.</param>
    /// <param name="scenarioID">The selected scenario when changing packs.</param>
    /// <param name="mods">The add-ons whose enabled state is being changed.</param>
    /// <param name="enabled">Whether those add-ons should be enabled.</param>
    public static void UpdateContentSelection(
        string contentRoot,
        string packID,
        string scenarioID,
        IReadOnlyList<InstalledModContent> mods,
        bool enabled
    )
    {
        string path = Path.Combine(UserDataPaths.GameRoot, _settingsFileName);
        AtomicTextFile.UpdateAllText(
            path,
            () =>
            {
                UserSettings settings = ReadSettingsForUpdate(path);
                if (!File.Exists(path))
                    settings.Content.HasModLoadOrderPreferences = true;
                if (packID != null)
                {
                    settings.Content.ActivePackID = packID;
                    settings.Content.ActiveScenarioID = scenarioID ?? string.Empty;
                }
                foreach (var group in mods.GroupBy(mod => mod.BasePackID, StringComparer.Ordinal))
                    UpdateModSelection(contentRoot, settings.Content, group.Key, group, enabled);
                return JsonUtility.ToJson(settings, true);
            }
        );
    }

    /// <summary>
    /// Gets the enabled mod releases in the order used by the selected game pack.
    /// </summary>
    /// <param name="contentRoot">The installation containing the game pack and mods.</param>
    /// <param name="packID">The game pack whose order will be shown.</param>
    /// <returns>The saved stack or the resolved legacy selection.</returns>
    public static IReadOnlyList<ContentModDefinition> ReadModLoadOrder(
        string contentRoot,
        string packID
    ) => ResolveSavedModOrder(contentRoot, packID, ReadContentSelection());

    /// <summary>
    /// Applies a new order to the same enabled mod releases and validates their combined campaign.
    /// </summary>
    /// <param name="contentRoot">The installation containing the game pack and mods.</param>
    /// <param name="packID">The game pack whose order will be changed.</param>
    /// <param name="original">The applied releases when editing began.</param>
    /// <param name="requested">The same mod IDs in the player's proposed order.</param>
    public static void UpdateModLoadOrder(
        string contentRoot,
        string packID,
        IReadOnlyList<ContentModDefinition> original,
        IReadOnlyList<string> requested
    )
    {
        string path = Path.Combine(UserDataPaths.GameRoot, _settingsFileName);
        AtomicTextFile.UpdateAllText(
            path,
            () =>
            {
                UserSettings settings = ReadSettingsForUpdate(path);
                IReadOnlyList<ContentModDefinition> current = ResolveSavedModOrder(
                    contentRoot,
                    packID,
                    settings.Content
                );
                if (
                    !current
                        .Select(mod => (mod.ID, mod.Version))
                        .SequenceEqual(original.Select(mod => (mod.ID, mod.Version)))
                )
                    throw new InvalidOperationException(
                        "The enabled mods changed. Reopen Load order to see the current list."
                    );
                if (
                    requested.Count != current.Count
                    || !requested
                        .ToHashSet(StringComparer.Ordinal)
                        .SetEquals(current.Select(mod => mod.ID))
                )
                    throw new ArgumentException(
                        "The new order must contain the same enabled mods.",
                        nameof(requested)
                    );

                var issues = new List<ContentModIssue>();
                IReadOnlyList<ContentModDefinition> ordered = ContentModLoadOrder.Resolve(
                    current,
                    issues,
                    requested
                );
                if (!ordered.Select(mod => mod.ID).SequenceEqual(requested))
                    throw new InvalidOperationException(
                        issues.FirstOrDefault()?.Message
                            ?? "The requested order conflicts with a mod's load rules."
                    );

                string[] versions = ordered.Select(mod => mod.Version).ToArray();
                ContentPack candidate = ContentPackLoader.OpenDefaultScenario(
                    contentRoot,
                    packID,
                    settings.Content.DisabledModIDs,
                    requested,
                    appliedModVersions: versions
                );
                CampaignContentValidator.ValidateCampaigns(candidate);
                settings.Content.SetModLoadOrder(packID, requested, versions);
                return JsonUtility.ToJson(settings, true);
            }
        );
    }

    /// <summary>
    /// Resolves the applied membership and release versions using the active game's discovery rules.
    /// </summary>
    /// <param name="contentRoot">The installation containing the game pack and mods.</param>
    /// <param name="packID">The game pack whose stack is requested.</param>
    /// <param name="preferences">The player settings currently being read or modified.</param>
    /// <returns>The resolved mods in application order.</returns>
    private static IReadOnlyList<ContentModDefinition> ResolveSavedModOrder(
        string contentRoot,
        string packID,
        UserContentSettings preferences
    )
    {
        IReadOnlyList<string> order = preferences.GetModLoadOrder(packID);
        return ContentPackLoader
            .DiscoverModResolver(
                contentRoot,
                packID,
                preferences.DisabledModIDs,
                order,
                order,
                preferences.GetModVersions(packID)
            )
            .Mods;
    }

    /// <summary>
    /// Applies a package selection to the matching game pack using the same native load rules as the game.
    /// </summary>
    /// <param name="contentRoot">The installation containing the game pack and add-ons.</param>
    /// <param name="selection">The preferences read under the publication lock.</param>
    /// <param name="packID">The game pack targeted by the selected add-ons.</param>
    /// <param name="mods">The selected add-ons for this game pack.</param>
    /// <param name="enabled">Whether the selected add-ons should load.</param>
    private static void UpdateModSelection(
        string contentRoot,
        UserContentSettings selection,
        string packID,
        IEnumerable<InstalledModContent> mods,
        bool enabled
    )
    {
        string[] ids = mods.Select(mod => mod.ID).Distinct(StringComparer.Ordinal).ToArray();
        IReadOnlyList<string> previous = selection.GetModLoadOrder(packID);
        selection.SetDisabledModsForPack(ids, enabled ? Array.Empty<string>() : ids);
        if (previous == null)
            return;

        string[] requested = enabled
            ? previous.Union(ids, StringComparer.Ordinal).ToArray()
            : previous.Except(ids, StringComparer.Ordinal).ToArray();
        if (!enabled && requested.Length == previous.Count)
            return;
        ContentFileResolver installed = ContentPackLoader.DiscoverModResolver(
            contentRoot,
            packID,
            selection.DisabledModIDs
        );
        var requestedIDs = requested.ToHashSet(StringComparer.Ordinal);
        string[] excluded = selection
            .DisabledModIDs.Concat(
                installed
                    .AvailableMods.Select(mod => mod.ID)
                    .Concat(
                        installed
                            .ModIssues.Where(issue => issue.IsDuplicate)
                            .Select(issue => issue.ID)
                    )
                    .Where(id => !requestedIDs.Contains(id))
            )
            .ToArray();
        ContentPack candidate = ContentPackLoader.OpenDefaultScenario(
            contentRoot,
            packID,
            excluded,
            requested,
            requireValidSelection: true
        );
        string unavailable = requested.FirstOrDefault(id =>
            !candidate.FileResolver.Mods.Any(mod => mod.ID == id)
        );
        if (unavailable != null)
            throw new InvalidOperationException(
                $"Mod '{unavailable}' is unavailable for the selected game pack."
            );
        CampaignContentValidator.ValidateCampaigns(candidate);
        selection.SetModLoadOrder(
            packID,
            candidate.FileResolver.Mods.Select(mod => mod.ID),
            candidate.FileResolver.Mods.Select(mod => mod.Version)
        );
    }

    /// <summary>
    /// Returns the path used for user settings persistence.
    /// </summary>
    /// <returns>The absolute user settings file path.</returns>
    public string GetSettingsFilePath()
    {
        return _settingsFilePathOverride ?? Path.Combine(UserDataPaths.GameRoot, _settingsFileName);
    }

    /// <summary>
    /// Loads user settings from disk and applies them to runtime systems.
    /// </summary>
    /// <returns>The loaded user settings.</returns>
    public UserSettings Load()
    {
        Settings = LoadFromDisk();
        RememberContentSelection(Settings.Content);
        Apply();
        return Settings;
    }

    /// <summary>
    /// Applies the active user settings to runtime systems.
    /// </summary>
    public void Apply()
    {
        Settings ??= CreateDefaults();
        Settings.Normalize();

        _audioManager?.ApplySettings(Settings.Audio);
        _inputManager?.LoadBindingOverrides(Settings.Input.BindingOverridesJson);
        _displayManager.Apply(Settings.Video);
        _graphicsManager?.Apply(Settings.Video);
        _analyticsConsentRuntime?.ApplyConsent(Settings.Analytics);
    }

    /// <summary>
    /// Captures current runtime settings and writes them to disk.
    /// </summary>
    public void Save()
    {
        Settings ??= CreateDefaults();
        CaptureRuntimeSettings();
        SaveToDisk(Settings);
    }

    /// <summary>
    /// Saves an explicit acceptance without retaining it in memory if persistence fails.
    /// </summary>
    /// <param name="agreement">The agreement the player explicitly accepted.</param>
    public void AcceptAgreement(PlayerAgreement agreement)
    {
        if (agreement == null)
            throw new ArgumentNullException(nameof(agreement));
        Settings ??= CreateDefaults();
        string previousVersion = Settings.AcceptedAgreementVersion;
        try
        {
            Settings.AcceptedAgreementVersion = agreement.Version;
            CaptureRuntimeSettings();
            SaveToDisk(Settings, agreement.Version);
        }
        catch
        {
            Settings.AcceptedAgreementVersion = previousVersion;
            throw;
        }
    }

    /// <summary>
    /// Loads user settings from the settings file.
    /// </summary>
    /// <returns>The loaded settings, or default settings when no usable file exists.</returns>
    private UserSettings LoadFromDisk()
    {
        string path = GetSettingsFilePath();
        if (!File.Exists(path))
            return CreateDefaults();

        try
        {
            UserSettings settings = DeserializeSettings(AtomicTextFile.ReadAllText(path));
            if (settings == null)
                return CreateDefaults();

            settings.Normalize();
            return settings;
        }
        catch (Exception ex)
        {
            GameLogger.Warning($"Failed to load user settings: {ex.Message}");
            return CreateDefaults();
        }
    }

    /// <summary>
    /// Deserializes current settings and migrates the former interface-owned launch speed.
    /// </summary>
    /// <param name="json">The persisted settings document.</param>
    /// <returns>The deserialized settings, or null when the document has no settings object.</returns>
    private static UserSettings DeserializeSettings(string json)
    {
        UserSettings settings = JsonUtility.FromJson<UserSettings>(json);
        if (settings == null)
            return null;

        settings.Gameplay ??= new UserGameplaySettings();
        LegacyUserSettings legacy = JsonUtility.FromJson<LegacyUserSettings>(json);
        GameLaunchSpeed legacySpeed =
            legacy?.UserInterface?.GameLaunchSpeed ?? GameLaunchSpeed.Unchanged;
        if (
            settings.Gameplay.GameLaunchSpeed == GameLaunchSpeed.Unchanged
            && legacySpeed != GameLaunchSpeed.Unchanged
            && Enum.IsDefined(typeof(GameLaunchSpeed), legacySpeed)
        )
        {
            settings.Gameplay.GameLaunchSpeed = legacySpeed;
        }

        return settings;
    }

    /// <summary>
    /// Saves runtime settings without reverting another application's content selection.
    /// </summary>
    /// <param name="settings">The runtime settings.</param>
    /// <param name="acceptedAgreementVersion">An explicitly accepted agreement, if any.</param>
    private void SaveToDisk(UserSettings settings, string acceptedAgreementVersion = null)
    {
        settings ??= CreateDefaults();
        settings.Normalize();
        string path = GetSettingsFilePath();
        UserSettings persisted = null;
        AtomicTextFile.UpdateAllText(
            path,
            () =>
            {
                UserSettings latest = ReadSettingsForUpdate(path, settings);
                persisted = JsonUtility.FromJson<UserSettings>(JsonUtility.ToJson(settings));
                persisted.AcceptedAgreementVersion =
                    acceptedAgreementVersion ?? latest.AcceptedAgreementVersion;
                persisted.Content = MergeContentSelection(settings.Content, latest.Content);
                return JsonUtility.ToJson(persisted, true);
            }
        );
        settings.AcceptedAgreementVersion = persisted.AcceptedAgreementVersion;
        settings.Content = persisted.Content;
        RememberContentSelection(settings.Content);
    }

    /// <summary>
    /// Reads authoritative settings under the publication lock, preserving invalid files before runtime recovery.
    /// </summary>
    /// <param name="path">The shared settings file.</param>
    /// <param name="fallback">The runtime snapshot for a missing or invalid file, or null to reject invalid updates.</param>
    /// <returns>The normalized settings.</returns>
    private static UserSettings ReadSettingsForUpdate(string path, UserSettings fallback = null)
    {
        if (!File.Exists(path))
            return fallback ?? CreateDefaults();
        UserSettings settings;
        try
        {
            settings =
                DeserializeSettings(AtomicTextFile.ReadAllText(path))
                ?? throw new InvalidDataException("The user settings could not be read.");
        }
        catch (Exception exception)
            when (fallback != null && exception is ArgumentException or InvalidDataException)
        {
            string backup = path + ".corrupt-" + Guid.NewGuid().ToString("N");
            File.Copy(path, backup);
            GameLogger.Warning("Invalid user settings were backed up to " + backup + ".");
            return fallback;
        }
        settings.Normalize();
        return settings;
    }

    /// <summary>
    /// Applies local selection changes over the latest saved content preferences.
    /// </summary>
    /// <param name="current">This instance's pending selection.</param>
    /// <param name="latest">The selection currently on disk.</param>
    /// <returns>The merged next-startup selection.</returns>
    private UserContentSettings MergeContentSelection(
        UserContentSettings current,
        UserContentSettings latest
    )
    {
        bool changedPack =
            current.ActivePackID != _savedContent.ActivePackID
            || current.ActiveScenarioID != _savedContent.ActiveScenarioID;
        HashSet<string> disabled = new(latest.DisabledModIDs, StringComparer.Ordinal);
        disabled.ExceptWith(
            _savedContent.DisabledModIDs.Except(current.DisabledModIDs, StringComparer.Ordinal)
        );
        disabled.UnionWith(
            current.DisabledModIDs.Except(_savedContent.DisabledModIDs, StringComparer.Ordinal)
        );
        var currentOrders = current.ModLoadOrders.ToLookup(order => order?.PackID);
        var savedOrders = _savedContent.ModLoadOrders.ToLookup(order => order?.PackID);
        var changedOrderIDs = currentOrders
            .Select(order => order.Key)
            .Union(savedOrders.Select(order => order.Key))
            .Where(id => !string.IsNullOrEmpty(id))
            .Where(id =>
                !SameModOrder(currentOrders[id].FirstOrDefault(), savedOrders[id].FirstOrDefault())
            )
            .ToHashSet(StringComparer.Ordinal);
        return new UserContentSettings
        {
            ActivePackID = changedPack ? current.ActivePackID : latest.ActivePackID,
            ActiveScenarioID = changedPack ? current.ActiveScenarioID : latest.ActiveScenarioID,
            DisabledModIDs = disabled.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            HasModLoadOrderPreferences =
                current.HasModLoadOrderPreferences != _savedContent.HasModLoadOrderPreferences
                    ? current.HasModLoadOrderPreferences
                    : latest.HasModLoadOrderPreferences,
            ModLoadOrders = latest
                .ModLoadOrders.Where(order =>
                    order != null && !changedOrderIDs.Contains(order.PackID)
                )
                .Concat(
                    current.ModLoadOrders.Where(order =>
                        order != null && changedOrderIDs.Contains(order.PackID)
                    )
                )
                .ToArray(),
        };
    }

    /// <summary>
    /// Records the last observed selection independently of subsequent UI edits.
    /// </summary>
    /// <param name="content">The successfully loaded or saved selection.</param>
    private void RememberContentSelection(UserContentSettings content) =>
        _savedContent = JsonUtility.FromJson<UserContentSettings>(JsonUtility.ToJson(content));

    /// <summary>
    /// Compares nullable per-pack preferences by their recorded IDs and versions.
    /// </summary>
    /// <param name="left">The order to compare.</param>
    /// <param name="right">The prior order.</param>
    /// <returns>Whether both describe the same persisted selection.</returns>
    private static bool SameModOrder(
        ContentModOrderPreference left,
        ContentModOrderPreference right
    ) =>
        ReferenceEquals(left, right)
        || left != null
            && right != null
            && (left.ModIDs ?? Array.Empty<string>()).SequenceEqual(
                right.ModIDs ?? Array.Empty<string>()
            )
            && (left.ModVersions ?? Array.Empty<string>()).SequenceEqual(
                right.ModVersions ?? Array.Empty<string>()
            );

    /// <summary>
    /// Captures runtime settings into the active user settings object.
    /// </summary>
    private void CaptureRuntimeSettings()
    {
        Settings ??= CreateDefaults();
        if (_audioManager != null)
            Settings.Audio = _audioManager.CreateSettingsSnapshot();
        if (
            _inputManager != null
            && _inputManager.TrySaveBindingOverrides(out string bindingOverrides)
        )
            Settings.Input.BindingOverridesJson = bindingOverrides;

        Settings.Normalize();
    }

    /// <summary>
    /// Creates normalized default user settings.
    /// </summary>
    /// <returns>The normalized default user settings.</returns>
    private static UserSettings CreateDefaults()
    {
        UserSettings settings = new UserSettings();
        settings.Normalize();
        return settings;
    }

    /// <summary>
    /// Describes the portion of the former settings shape needed during migration.
    /// </summary>
    [Serializable]
    private sealed class LegacyUserSettings
    {
        public LegacyUserInterfaceSettings UserInterface;
    }

    /// <summary>
    /// Describes the former interface-owned launch-speed setting.
    /// </summary>
    [Serializable]
    private sealed class LegacyUserInterfaceSettings
    {
        public GameLaunchSpeed GameLaunchSpeed = GameLaunchSpeed.Unchanged;
    }
}
