using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Util.Reflection;

/// <summary>
/// Owns one custom-game configuration draft and its default/override relationships.
/// </summary>
internal sealed class CustomGameSettingsSession
{
    private const int _maximumResourceOutputPercent = 1000;
    private const int _maximumResourceProcessingPointsPerFacility = 500;

    internal sealed class GenerationEntry
    {
        internal NewGameSettingsCategory Category;
        internal string Section;
        internal string RowKey;
        internal string Key;
        internal string Label;
        internal string Description;
        internal string Column;
        internal int Minimum;
        internal int Maximum;
        internal Func<GameGenerationConfig, int> Read;
        internal Action<GameGenerationConfig, int> Write;
        internal Func<GameGenerationConfig, int, bool> IsValid;
    }

    private readonly List<(
        string Key,
        string Section,
        string Label,
        string Description,
        bool AllowsFraction,
        double Minimum,
        bool MinimumExclusive,
        double? Maximum,
        double PlayerDefault,
        double AIDefault
    )> difficultyEntries = new();
    private readonly Dictionary<string, double> playerDraft = new();
    private readonly Dictionary<string, double> aiDraft = new();
    private readonly HashSet<string> aiCustomized = new();
    private readonly List<GenerationEntry> generationEntries = new();
    private readonly GameEventCategory[] eventCategories;
    private readonly GameSummary summaryDefaults;
    private readonly GameSummary summaryDraft;
    private readonly ContentPack activePack;
    private readonly GameGenerationConfig generationDefaults;
    private readonly GameGenerationConfig generationDraft;
    private string currentDifficultySection;
    private string currentGenerationSection;

    internal IReadOnlyList<(
        string Key,
        string Section,
        string Label,
        string Description,
        bool AllowsFraction,
        double Minimum,
        bool MinimumExclusive,
        double? Maximum,
        double PlayerDefault,
        double AIDefault
    )> DifficultyEntries => difficultyEntries;

    internal IReadOnlyList<GenerationEntry> GenerationEntries => generationEntries;

    internal IReadOnlyList<GameEventCategory> EventCategories => eventCategories;

    internal ContentPack Pack => activePack;

    internal GameSummary Draft => summaryDraft;

    /// <summary>
    /// Creates an isolated editing session from active content and a launch summary.
    /// </summary>
    /// <param name="pack">The active content pack.</param>
    /// <param name="summary">The launch configuration to edit.</param>
    internal CustomGameSettingsSession(ContentPack pack, GameSummary summary)
    {
        activePack = pack ?? throw new ArgumentNullException(nameof(pack));
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));

        summaryDefaults = GameLaunchContext.CreateDefaultSummary(pack);
        summaryDraft = summary.GetDeepCopy();
        summaryDefaults.Seed = summaryDraft.Seed;
        summaryDraft.StartingFactionIDs = summaryDefaults.StartingFactionIDs.ToArray();
        summaryDraft.PackID = summaryDefaults.PackID;
        summaryDraft.PackVersion = summaryDefaults.PackVersion;
        summaryDraft.ScenarioID = summaryDefaults.ScenarioID;
        summaryDraft.ModIDs = summaryDefaults.ModIDs.ToArray();
        summaryDraft.ModVersions = summaryDefaults.ModVersions.ToArray();
        if (!summaryDraft.StartingFactionIDs.Contains(summaryDraft.PlayerFactionID))
            summaryDraft.PlayerFactionID = summaryDefaults.PlayerFactionID;
        generationDefaults = pack.GameData.GenerationConfig.GetDeepCopy();
        generationDraft =
            summaryDraft.GenerationConfigOverride?.GetDeepCopy()
            ?? pack.GameData.GenerationConfig.GetDeepCopy();
        ReconcileFactionModifiers(generationDefaults, pack.GameData.Factions);
        ReconcileFactionModifiers(generationDraft, pack.GameData.Factions);
        eventCategories = pack
            .GameData.GameEvents.Where(gameEvent =>
                gameEvent != null && gameEvent.Category != GameEventCategory.None
            )
            .Select(gameEvent => gameEvent.Category)
            .Distinct()
            .OrderBy(category => category)
            .ToArray();

        DifficultyModifiers playerDefaults = new DifficultyModifiers();
        DifficultyModifiers aiDefaults = ResolveAIDefaults(pack, summaryDraft.Difficulty);
        DifficultyModifiers playerValues = summaryDraft.PlayerDifficultyModifiers ?? playerDefaults;
        DifficultyModifiers aiValues = summaryDraft.AIDifficultyModifiers ?? aiDefaults;
        AddDifficultyEntries(playerDefaults, aiDefaults, playerValues, aiValues);
        if (summaryDraft.AIDifficultyModifiers != null)
        {
            foreach (var entry in difficultyEntries)
            {
                if (aiDraft[entry.Key] != entry.AIDefault)
                    aiCustomized.Add(entry.Key);
            }
        }

        BuildGenerationEntries(pack, summaryDraft);
    }

    /// <summary>
    /// Returns one current player or AI difficulty value.
    /// </summary>
    /// <param name="key">The stable modifier key.</param>
    /// <param name="ai">Whether to read the AI column.</param>
    /// <returns>The current draft value.</returns>
    internal double GetDifficultyValue(string key, bool ai) => (ai ? aiDraft : playerDraft)[key];

    /// <summary>
    /// Returns whether one difficulty value matches its active profile default.
    /// </summary>
    /// <param name="key">The stable modifier key.</param>
    /// <param name="ai">Whether to inspect the AI column.</param>
    /// <returns>True when the current value matches its default.</returns>
    internal bool IsDifficultyValueDefault(string key, bool ai)
    {
        var entry = difficultyEntries.Single(candidate => candidate.Key == key);
        return GetDifficultyValue(key, ai) == (ai ? entry.AIDefault : entry.PlayerDefault);
    }

    /// <summary>
    /// Applies a validated difficulty value from menu text.
    /// </summary>
    /// <param name="key">The stable modifier key.</param>
    /// <param name="ai">Whether to edit the AI column.</param>
    /// <param name="text">The entered value.</param>
    /// <returns>True when the value was accepted.</returns>
    internal bool TrySetDifficultyValue(string key, bool ai, string text)
    {
        var entry = difficultyEntries.Single(candidate => candidate.Key == key);
        if (
            !double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value
            )
            || !double.IsFinite(value)
            || (!entry.AllowsFraction && value != Math.Truncate(value))
            || (entry.MinimumExclusive ? value <= entry.Minimum : value < entry.Minimum)
            || (entry.Maximum.HasValue && value > entry.Maximum.Value)
            || (!entry.AllowsFraction && value is < int.MinValue or > int.MaxValue)
        )
            return false;

        Dictionary<string, double> draft = ai ? aiDraft : playerDraft;
        draft[key] = value;
        if (ai)
        {
            if (value == entry.AIDefault)
                aiCustomized.Remove(key);
            else
                aiCustomized.Add(key);
        }
        return true;
    }

    /// <summary>
    /// Restores one difficulty value and its profile-link state.
    /// </summary>
    /// <param name="key">The stable modifier key.</param>
    /// <param name="ai">Whether to reset the AI column.</param>
    internal void ResetDifficultyValue(string key, bool ai)
    {
        var entry = difficultyEntries.Single(candidate => candidate.Key == key);
        (ai ? aiDraft : playerDraft)[key] = ai ? entry.AIDefault : entry.PlayerDefault;
        if (ai)
            aiCustomized.Remove(key);
    }

    /// <summary>
    /// Applies a validated generation value from menu text.
    /// </summary>
    /// <param name="entry">The setting to edit.</param>
    /// <param name="text">The entered value.</param>
    /// <returns>True when the value was accepted.</returns>
    internal bool TrySetGenerationValue(GenerationEntry entry, string text)
    {
        if (
            !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            || value < entry.Minimum
            || value > entry.Maximum
            || (entry.IsValid != null && !entry.IsValid(generationDraft, value))
        )
            return false;

        entry.Write(generationDraft, value);
        return true;
    }

    /// <summary>
    /// Reads one current generation value.
    /// </summary>
    /// <param name="entry">The setting to read.</param>
    /// <returns>The current value.</returns>
    internal int GetGenerationValue(GenerationEntry entry) => entry.Read(generationDraft);

    /// <summary>
    /// Returns whether one generation value matches the active content default.
    /// </summary>
    /// <param name="entry">The setting to inspect.</param>
    /// <returns>True when the current value matches its default.</returns>
    internal bool IsGenerationValueDefault(GenerationEntry entry) =>
        entry.Read(generationDraft) == entry.Read(generationDefaults);

    /// <summary>
    /// Restores one generation value from active content.
    /// </summary>
    /// <param name="entry">The setting to restore.</param>
    internal void ResetGenerationValue(GenerationEntry entry) =>
        entry.Write(generationDraft, entry.Read(generationDefaults));

    /// <summary>
    /// Applies a manually entered deterministic game seed.
    /// </summary>
    /// <param name="text">The entered signed integer.</param>
    /// <returns>True when the seed was accepted.</returns>
    internal bool TrySetSeed(string text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
            return false;

        summaryDraft.Seed = seed;
        return true;
    }

    /// <summary>
    /// Generates and selects a new game seed.
    /// </summary>
    internal void RandomizeSeed()
    {
        int current = summaryDraft.Seed;
        int next;
        do next = Guid.NewGuid().GetHashCode();
        while (next == current);
        summaryDraft.Seed = next;
    }

    /// <summary>
    /// Selects a player faction and rebuilds dependent generation settings.
    /// </summary>
    /// <param name="factionID">The selected faction instance ID.</param>
    internal void SetPlayerFaction(string factionID)
    {
        summaryDraft.PlayerFactionID = factionID;
        BuildGenerationEntries(activePack, summaryDraft);
    }

    /// <summary>
    /// Selects a difficulty and refreshes every profile-linked setting.
    /// </summary>
    /// <param name="difficulty">The selected difficulty.</param>
    internal void SetDifficulty(GameDifficulty difficulty)
    {
        summaryDraft.Difficulty = difficulty;
        UpdateAIDifficultyDefaults(difficulty);
        BuildGenerationEntries(activePack, summaryDraft);
    }

    /// <summary>
    /// Selects a galaxy size and rebuilds applicable generation settings.
    /// </summary>
    /// <param name="size">The selected galaxy size.</param>
    internal void SetGalaxySize(GameSize size)
    {
        summaryDraft.GalaxySize = size;
        BuildGenerationEntries(activePack, summaryDraft);
    }

    /// <summary>
    /// Selects a victory condition.
    /// </summary>
    /// <param name="condition">The selected victory condition.</param>
    internal void SetVictoryCondition(GameVictoryCondition condition) =>
        summaryDraft.VictoryCondition = condition;

    /// <summary>
    /// Returns whether a classified event category is enabled in the draft campaign.
    /// </summary>
    /// <param name="category">The category to inspect.</param>
    /// <returns>True when the category is enabled.</returns>
    internal bool IsEventCategoryEnabled(GameEventCategory category) =>
        summaryDraft.IsEventCategoryEnabled(category);

    /// <summary>
    /// Enables or disables one classified event category in the draft campaign.
    /// </summary>
    /// <param name="category">The category to update.</param>
    /// <param name="enabled">Whether the category should be enabled.</param>
    internal void SetEventCategoryEnabled(GameEventCategory category, bool enabled)
    {
        if (category == GameEventCategory.None)
            return;

        HashSet<GameEventCategory> disabled = new HashSet<GameEventCategory>(
            summaryDraft.DisabledEventCategories.Where(candidate =>
                candidate != GameEventCategory.None
            )
        );
        if (enabled)
            disabled.Remove(category);
        else
            disabled.Add(category);

        summaryDraft.DisabledEventCategories = disabled.OrderBy(candidate => candidate).ToArray();
    }

    /// <summary>
    /// Restores defaults for one visible settings category.
    /// </summary>
    /// <param name="category">The category to restore.</param>
    internal void RestoreDefaults(NewGameSettingsCategory category)
    {
        if (category == NewGameSettingsCategory.GameSetup)
        {
            summaryDraft.PlayerFactionID = summaryDefaults.PlayerFactionID;
            summaryDraft.Difficulty = summaryDefaults.Difficulty;
            summaryDraft.GalaxySize = summaryDefaults.GalaxySize;
            summaryDraft.VictoryCondition = summaryDefaults.VictoryCondition;
            summaryDraft.ResourceAvailability = summaryDefaults.ResourceAvailability;
            summaryDraft.DisabledEventCategories =
                summaryDefaults.DisabledEventCategories.ToArray();
            summaryDraft.Seed = summaryDefaults.Seed;
            UpdateAIDifficultyDefaults(summaryDraft.Difficulty);
            BuildGenerationEntries(activePack, summaryDraft);
            return;
        }

        if (category == NewGameSettingsCategory.DifficultyModifiers)
        {
            foreach (var entry in difficultyEntries)
            {
                playerDraft[entry.Key] = entry.PlayerDefault;
                aiDraft[entry.Key] = entry.AIDefault;
            }
            aiCustomized.Clear();
            return;
        }

        foreach (
            GenerationEntry entry in generationEntries.Where(entry => entry.Category == category)
        )
            entry.Write(generationDraft, entry.Read(generationDefaults));
    }

    /// <summary>
    /// Captures the complete launch configuration represented by this session.
    /// </summary>
    /// <returns>A detached launch summary.</returns>
    internal GameSummary CreateSummarySnapshot()
    {
        GameSummary snapshot = summaryDraft.GetDeepCopy();
        snapshot.PlayerDifficultyModifiers = CreateModifiers(playerDraft);
        snapshot.AIDifficultyModifiers = aiCustomized.Count == 0 ? null : CreateModifiers(aiDraft);
        snapshot.GenerationConfigOverride = generationDraft.GetDeepCopy();
        return snapshot;
    }

    /// <summary>
    /// Defines the supported difficulty fields and their starting values.
    /// </summary>
    /// <param name="playerDefaults">The player defaults.</param>
    /// <param name="aiDefaults">The selected AI profile.</param>
    /// <param name="playerValues">The initial player values.</param>
    /// <param name="aiValues">The initial AI values.</param>
    private void AddDifficultyEntries(
        DifficultyModifiers playerDefaults,
        DifficultyModifiers aiDefaults,
        DifficultyModifiers playerValues,
        DifficultyModifiers aiValues
    )
    {
        currentDifficultySection = "ECONOMY";
        Add(
            "mine-output",
            "Mine Output Modifier",
            "How fast mines produce raw materials. Higher is faster. Range: 0% to 1,000%.",
            false,
            0,
            false,
            _maximumResourceOutputPercent,
            playerDefaults.MineOutputPercent,
            aiDefaults.MineOutputPercent,
            playerValues.MineOutputPercent,
            aiValues.MineOutputPercent
        );
        Add(
            "refinery-output",
            "Refinery Output Modifier",
            "How fast refineries produce refined materials. Higher is faster. Range: 0% to 1,000%.",
            false,
            0,
            false,
            _maximumResourceOutputPercent,
            playerDefaults.RefineryOutputPercent,
            aiDefaults.RefineryOutputPercent,
            playerValues.RefineryOutputPercent,
            aiValues.RefineryOutputPercent
        );
        Add(
            "maintenance",
            "Maintenance Modifier",
            "Maintenance from each working mine and refinery pair. Higher supports more units. Range: 0% or higher.",
            false,
            0,
            false,
            null,
            playerDefaults.MaintenanceCapacityPercent,
            aiDefaults.MaintenanceCapacityPercent,
            playerValues.MaintenanceCapacityPercent,
            aiValues.MaintenanceCapacityPercent
        );
        Add(
            "manufacturing-speed",
            "Manufacturing Speed Modifier",
            "How fast ships, fighters, troops, and facilities are built. Higher is faster. Range: 0% or higher.",
            false,
            0,
            false,
            null,
            playerDefaults.ManufacturingSpeedPercent,
            aiDefaults.ManufacturingSpeedPercent,
            playerValues.ManufacturingSpeedPercent,
            aiValues.ManufacturingSpeedPercent
        );

        currentDifficultySection = "MISSIONS AND INTELLIGENCE";
        Add(
            "mission-success",
            "Mission Success Bonus",
            "Changes this faction's chance to complete missions successfully. Range: -100 to 100.",
            false,
            -100,
            false,
            100,
            playerDefaults.MissionSuccessChancePoints,
            aiDefaults.MissionSuccessChancePoints,
            playerValues.MissionSuccessChancePoints,
            aiValues.MissionSuccessChancePoints
        );
        Add(
            "mission-foil",
            "Mission Foil Bonus",
            "Changes the chance that this faction's missions are detected and stopped. Range: -100 to 100.",
            false,
            -100,
            false,
            100,
            playerDefaults.MissionFoilChancePoints,
            aiDefaults.MissionFoilChancePoints,
            playerValues.MissionFoilChancePoints,
            aiValues.MissionFoilChancePoints
        );
        Add(
            "mission-speed",
            "Mission Speed Bonus",
            "How long this faction's missions take. Higher finishes them sooner. Range: 0% or higher.",
            false,
            0,
            false,
            null,
            playerDefaults.MissionExecutionSpeedIncreasePercent,
            aiDefaults.MissionExecutionSpeedIncreasePercent,
            playerValues.MissionExecutionSpeedIncreasePercent,
            aiValues.MissionExecutionSpeedIncreasePercent
        );
        Add(
            "detection-rating",
            "Detection Rating Multiplier",
            "How easily this faction detects enemy missions. Higher improves detection. Range: greater than 0.",
            true,
            0,
            true,
            null,
            playerDefaults.DetectionRatingMultiplier,
            aiDefaults.DetectionRatingMultiplier,
            playerValues.DetectionRatingMultiplier,
            aiValues.DetectionRatingMultiplier
        );
    }

    /// <summary>
    /// Resolves the authored AI defaults for the selected difficulty.
    /// </summary>
    /// <param name="pack">The active content pack.</param>
    /// <param name="difficulty">The selected new-game difficulty.</param>
    /// <returns>The configured modifiers, or neutral modifiers when the difficulty is absent.</returns>
    private static DifficultyModifiers ResolveAIDefaults(
        ContentPack pack,
        GameDifficulty difficulty
    )
    {
        if (
            pack.GameData.GameConfig.DifficultyModifiers.TryGetValue(
                difficulty,
                out DifficultyModifiers modifiers
            )
        )
            return modifiers;

        return new DifficultyModifiers();
    }

    /// <summary>
    /// Replaces unchanged AI values with the defaults for a newly selected difficulty.
    /// </summary>
    /// <param name="current">The newly selected difficulty.</param>
    private void UpdateAIDifficultyDefaults(GameDifficulty current)
    {
        DifficultyModifiers currentDefaults = ResolveAIDefaults(activePack, current);
        for (int index = 0; index < difficultyEntries.Count; index++)
        {
            var entry = difficultyEntries[index];
            double currentDefault = ReadDifficultyModifier(currentDefaults, entry.Key);
            if (!aiCustomized.Contains(entry.Key))
                aiDraft[entry.Key] = currentDefault;

            difficultyEntries[index] = (
                entry.Key,
                entry.Section,
                entry.Label,
                entry.Description,
                entry.AllowsFraction,
                entry.Minimum,
                entry.MinimumExclusive,
                entry.Maximum,
                entry.PlayerDefault,
                currentDefault
            );
        }
    }

    /// <summary>
    /// Reads one named value from a difficulty-modifier object.
    /// </summary>
    /// <param name="modifiers">The modifier object to inspect.</param>
    /// <param name="key">The stable modifier identity.</param>
    /// <returns>The selected modifier value.</returns>
    private static double ReadDifficultyModifier(DifficultyModifiers modifiers, string key)
    {
        return key switch
        {
            "mine-output" => modifiers.MineOutputPercent,
            "refinery-output" => modifiers.RefineryOutputPercent,
            "maintenance" => modifiers.MaintenanceCapacityPercent,
            "manufacturing-speed" => modifiers.ManufacturingSpeedPercent,
            "mission-success" => modifiers.MissionSuccessChancePoints,
            "mission-foil" => modifiers.MissionFoilChancePoints,
            "mission-speed" => modifiers.MissionExecutionSpeedIncreasePercent,
            "detection-rating" => modifiers.DetectionRatingMultiplier,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
        };
    }

    /// <summary>
    /// Rebuilds faction modifier entries in active content order while retaining existing values.
    /// </summary>
    /// <param name="config">The launch configuration to reconcile.</param>
    /// <param name="factions">The active faction templates.</param>
    private static void ReconcileFactionModifiers(
        GameGenerationConfig config,
        IReadOnlyList<Faction> factions
    )
    {
        IReadOnlyList<FactionModifierConfig> existing =
            config.FactionModifiers ?? new List<FactionModifierConfig>();
        config.FactionModifiers = factions
            .Select(faction =>
            {
                FactionModifierConfig current = existing.FirstOrDefault(candidate =>
                    candidate.FactionID == faction.InstanceID
                );
                return new FactionModifierConfig
                {
                    FactionID = faction.InstanceID,
                    Settings = (current?.Settings ?? faction.Settings).GetDeepCopy(),
                };
            })
            .ToList();
    }

    /// <summary>
    /// Builds numeric launch settings from the selected scenario and faction configuration.
    /// </summary>
    /// <param name="pack">The active content and scenario.</param>
    /// <param name="summary">The selected launch settings that determine applicable values.</param>
    private void BuildGenerationEntries(ContentPack pack, GameSummary summary)
    {
        generationEntries.Clear();
        currentGenerationSection = "FACTION STARTING CONDITIONS";
        AddStartingResearchEntries(pack.GameData.Factions);
        AddOfficerEntries(pack.GameData.Factions, summary.GalaxySize);
        AddStartingForceEntries(pack.GameData.Factions, summary.GalaxySize);
        AddGalaxyClassificationEntries(pack, summary);
        AddResourceEntries(summary.ResourceAvailability);
        currentGenerationSection = "STARTING PLANET SUPPORT";
        AddPlanetSupportEntries();
        currentGenerationSection = "MINE GENERATION";
        AddFacilityEntries();
        currentGenerationSection = "GARRISONS AND SUPPORT";
        AddUnitAndBalanceEntries();
        AddFactionModifierEntries(pack.GameData.Factions);
    }

    /// <summary>
    /// Adds consumed numeric faction settings as tables with one column per faction.
    /// </summary>
    /// <param name="factions">The active faction templates in display order.</param>
    private void AddFactionModifierEntries(IReadOnlyList<Faction> factions)
    {
        currentGenerationSection = "ECONOMY";
        AddFactionModifierRow(
            factions,
            "refinement-multiplier",
            "Refinement Multiplier",
            "Usable supply gained from raw and refined materials. Higher provides more supply. Range: 1 or higher.",
            1,
            int.MaxValue,
            settings => settings.RefinementMultiplier,
            (settings, value) => settings.RefinementMultiplier = value
        );
        AddFactionModifierRow(
            factions,
            "resource-processing",
            "Maintenance per Pair",
            "Maintenance from each working mine and refinery pair. Higher supports more units. Range: 1 to 500.",
            1,
            _maximumResourceProcessingPointsPerFacility,
            settings => settings.ResourceProcessingPointsPerFacility,
            (settings, value) => settings.ResourceProcessingPointsPerFacility = value
        );

        currentGenerationSection = "UPRISINGS";
        AddFactionModifierRow(
            factions,
            "uprising-resistance",
            "Uprising Resistance",
            "Garrison strength during core-world uprisings. Higher makes each troop stronger. Range: 1 or higher.",
            1,
            int.MaxValue,
            settings => settings.UprisingResistance,
            (settings, value) => settings.UprisingResistance = value
        );

        currentGenerationSection = "POPULAR SUPPORT";
        AddFactionModifierRow(
            factions,
            "core-bombardment-penalty",
            "Core Bombardment Penalty",
            "Sets the sector-wide support shift after civilian bombardment in a core sector.",
            int.MinValue,
            int.MaxValue,
            settings => settings.CivilianBombardmentCoreSupportPenalty,
            (settings, value) => settings.CivilianBombardmentCoreSupportPenalty = value
        );
        AddFactionModifierRow(
            factions,
            "rim-bombardment-penalty",
            "Rim Bombardment Penalty",
            "Sets the sector-wide support shift after civilian bombardment in a rim sector.",
            int.MinValue,
            int.MaxValue,
            settings => settings.CivilianBombardmentOuterRimSupportPenalty,
            (settings, value) => settings.CivilianBombardmentOuterRimSupportPenalty = value
        );
        AddFactionModifierRow(
            factions,
            "battle-loss-loyalty-divisor",
            "Battle-Loss Loyalty Divisor",
            "How strongly battle losses affect officer loyalty. Higher reduces the effect. Range: 1 or higher.",
            1,
            int.MaxValue,
            settings => settings.BattleLossLoyaltyDivisor,
            (settings, value) => settings.BattleLossLoyaltyDivisor = value
        );
    }

    /// <summary>
    /// Adds one faction-settings row with a cell for every active faction.
    /// </summary>
    /// <param name="factions">The active faction templates in display order.</param>
    /// <param name="rowKey">The stable row identity.</param>
    /// <param name="label">The visible row label.</param>
    /// <param name="description">The visible behavior description.</param>
    /// <param name="minimum">The inclusive minimum value.</param>
    /// <param name="maximum">The inclusive maximum value.</param>
    /// <param name="read">Reads the value from one faction's settings.</param>
    /// <param name="write">Writes the value to one faction's settings.</param>
    private void AddFactionModifierRow(
        IReadOnlyList<Faction> factions,
        string rowKey,
        string label,
        string description,
        int minimum,
        int maximum,
        Func<FactionSettings, int> read,
        Action<FactionSettings, int> write
    )
    {
        for (int factionIndex = 0; factionIndex < factions.Count; factionIndex++)
        {
            int index = factionIndex;
            Faction faction = factions[index];
            AddFactionModifierTableCell(
                rowKey,
                faction.InstanceID.ToLowerInvariant() + "-" + rowKey,
                label,
                description,
                faction.GetDisplayName(),
                minimum,
                maximum,
                config => read(config.FactionModifiers[index].Settings),
                (config, value) => write(config.FactionModifiers[index].Settings, value)
            );
        }
    }

    /// <summary>
    /// Adds each faction's starting research level as a shared table row.
    /// </summary>
    /// <param name="factions">The active faction templates in display order.</param>
    private void AddStartingResearchEntries(IReadOnlyList<Faction> factions)
    {
        for (int factionIndex = 0; factionIndex < factions.Count; factionIndex++)
        {
            Faction faction = factions[factionIndex];
            string factionID = faction.InstanceID;
            AddGenerationTableCell(
                "starting-research",
                factionID.ToLowerInvariant() + "-starting-research",
                "Starting Research Level",
                "Research level available in every field when the game begins. Range: 0 or higher.",
                faction.GetDisplayName(),
                0,
                int.MaxValue,
                config => GetFactionSetup(config, factionID).StartingResearchLevel,
                (config, value) => GetFactionSetup(config, factionID).StartingResearchLevel = value
            );
        }
    }

    /// <summary>
    /// Adds each faction's recruitable-officer limit for the selected galaxy size.
    /// </summary>
    /// <param name="factions">The active faction templates in display order.</param>
    /// <param name="galaxySize">The selected galaxy size.</param>
    private void AddOfficerEntries(IReadOnlyList<Faction> factions, GameSize galaxySize)
    {
        foreach (Faction faction in factions)
        {
            string factionID = faction.InstanceID;
            AddGenerationTableCell(
                "starting-officers",
                factionID.ToLowerInvariant() + "-starting-officers",
                "Starting Officers",
                "Most officers this faction can start with. Range: 0 or higher.",
                faction.GetDisplayName(),
                0,
                int.MaxValue,
                config => ReadOfficerCount(GetFactionSetup(config, factionID), galaxySize),
                (config, value) =>
                    WriteOfficerCount(GetFactionSetup(config, factionID), galaxySize, value)
            );
        }
    }

    /// <summary>
    /// Resolves one faction's generation setup by its stable instance ID.
    /// </summary>
    /// <param name="config">The generation configuration to inspect.</param>
    /// <param name="factionID">The faction instance ID.</param>
    /// <returns>The matching faction setup.</returns>
    private static FactionSetup GetFactionSetup(GameGenerationConfig config, string factionID) =>
        config.GalaxyClassification.FactionSetups.Single(setup => setup.FactionID == factionID);

    /// <summary>
    /// Reads one faction's officer count for a galaxy size.
    /// </summary>
    /// <param name="setup">The faction setup to inspect.</param>
    /// <param name="galaxySize">The selected galaxy size.</param>
    /// <returns>The configured officer count.</returns>
    private static int ReadOfficerCount(FactionSetup setup, GameSize galaxySize)
    {
        return galaxySize switch
        {
            GameSize.Small => setup.NumStartingOfficers.Small,
            GameSize.Medium => setup.NumStartingOfficers.Medium,
            GameSize.Large => setup.NumStartingOfficers.Large,
            _ => throw new ArgumentOutOfRangeException(nameof(galaxySize), galaxySize, null),
        };
    }

    /// <summary>
    /// Writes one faction's officer count for a galaxy size.
    /// </summary>
    /// <param name="setup">The faction setup to update.</param>
    /// <param name="galaxySize">The selected galaxy size.</param>
    /// <param name="value">The officer count.</param>
    private static void WriteOfficerCount(FactionSetup setup, GameSize galaxySize, int value)
    {
        switch (galaxySize)
        {
            case GameSize.Small:
                setup.NumStartingOfficers.Small = value;
                break;
            case GameSize.Medium:
                setup.NumStartingOfficers.Medium = value;
                break;
            case GameSize.Large:
                setup.NumStartingOfficers.Large = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(galaxySize), galaxySize, null);
        }
    }

    /// <summary>
    /// Adds each faction's variable starting-unit budget for the selected galaxy size.
    /// </summary>
    /// <param name="factions">The active faction templates in display order.</param>
    /// <param name="galaxySize">The selected galaxy size.</param>
    private void AddStartingForceEntries(IReadOnlyList<Faction> factions, GameSize galaxySize)
    {
        foreach (Faction faction in factions)
        {
            string factionID = faction.InstanceID;
            int budgetIndex = generationDefaults.UnitDeployment.FactionBudgets.FindIndex(budget =>
                budget.FactionID == factionID
            );
            if (budgetIndex < 0)
                continue;
            List<BudgetLevel> levels = generationDefaults
                .UnitDeployment
                .FactionBudgets[budgetIndex]
                .BudgetLevels;
            int levelIndex = levels.FindIndex(level => level.GalaxySize == (int)galaxySize);
            if (levelIndex < 0)
                levelIndex = 0;

            AddGenerationTableCell(
                "starting-forces",
                factionID.ToLowerInvariant() + "-starting-forces",
                "Extra Starting Forces %",
                "Share of spare maintenance capacity used for extra starting ships, fighters, troops, and special forces. Range: 0% to 100%.",
                faction.GetDisplayName(),
                0,
                100,
                config =>
                    config
                        .UnitDeployment
                        .FactionBudgets[budgetIndex]
                        .BudgetLevels[levelIndex]
                        .Percentage,
                (config, value) =>
                    config
                        .UnitDeployment
                        .FactionBudgets[budgetIndex]
                        .BudgetLevels[levelIndex]
                        .Percentage = value
            );
        }
    }

    /// <summary>
    /// Adds base and conditional core-planet ownership percentages for every faction.
    /// </summary>
    /// <param name="pack">The active content and scenario.</param>
    /// <param name="summary">The selected difficulty and player faction.</param>
    private void AddGalaxyClassificationEntries(ContentPack pack, GameSummary summary)
    {
        for (
            int bucketIndex = 0;
            bucketIndex < generationDefaults.GalaxyClassification.FactionBuckets.Count;
            bucketIndex++
        )
        {
            int index = bucketIndex;
            FactionBucketConfig bucket = generationDefaults.GalaxyClassification.FactionBuckets[
                index
            ];
            string factionLabel =
                pack.GameData.Factions.FirstOrDefault(faction =>
                        faction.InstanceID == bucket.FactionID
                    )
                    ?.GetDisplayName()
                ?? bucket.FactionID;
            string factionKey = bucket.FactionID.ToLowerInvariant();
            PlayerControllerType controller =
                bucket.FactionID == summary.PlayerFactionID
                    ? PlayerControllerType.Human
                    : PlayerControllerType.AI;
            int overrideIndex = bucket.Overrides.FindIndex(candidate =>
                candidate.Difficulty == summary.Difficulty && candidate.ControllerType == controller
            );
            Func<GameGenerationConfig, int> readStrong =
                overrideIndex < 0
                    ? config => config.GalaxyClassification.FactionBuckets[index].StrongPct
                    : config =>
                        config
                            .GalaxyClassification
                            .FactionBuckets[index]
                            .Overrides[overrideIndex]
                            .StrongPct;
            Action<GameGenerationConfig, int> writeStrong =
                overrideIndex < 0
                    ? (config, value) =>
                        config.GalaxyClassification.FactionBuckets[index].StrongPct = value
                    : (config, value) =>
                        config
                            .GalaxyClassification
                            .FactionBuckets[index]
                            .Overrides[overrideIndex]
                            .StrongPct = value;
            Func<GameGenerationConfig, int> readWeak =
                overrideIndex < 0
                    ? config => config.GalaxyClassification.FactionBuckets[index].WeakPct
                    : config =>
                        config
                            .GalaxyClassification
                            .FactionBuckets[index]
                            .Overrides[overrideIndex]
                            .WeakPct;
            Action<GameGenerationConfig, int> writeWeak =
                overrideIndex < 0
                    ? (config, value) =>
                        config.GalaxyClassification.FactionBuckets[index].WeakPct = value
                    : (config, value) =>
                        config
                            .GalaxyClassification
                            .FactionBuckets[index]
                            .Overrides[overrideIndex]
                            .WeakPct = value;
            AddGenerationTableCell(
                "strong-core-worlds",
                factionKey + "-strong",
                "Strong Core Worlds %",
                "Core worlds this faction starts owning with strong popular support. Range: 0% to 100%.",
                factionLabel,
                0,
                100,
                readStrong,
                writeStrong
            );
            AddGenerationTableCell(
                "weak-core-worlds",
                factionKey + "-weak",
                "Weak Core Worlds %",
                "Core worlds this faction starts owning with weak popular support. Range: 0% to 100%.",
                factionLabel,
                0,
                100,
                readWeak,
                writeWeak
            );
        }
    }

    /// <summary>
    /// Adds consumed numeric fields from the selected planet-resource profile.
    /// </summary>
    /// <param name="availability">The selected resource-availability level.</param>
    private void AddResourceEntries(GameResourceAvailability availability)
    {
        int index = generationDefaults.PlanetResources.Profiles.FindIndex(profile =>
            profile.Availability == availability
        );
        if (index < 0)
        {
            index = generationDefaults.PlanetResources.Profiles.FindIndex(profile =>
                profile.Availability == GameResourceAvailability.Normal
            );
        }
        if (index < 0)
            index = 0;

        PlanetResourceProfile profile = generationDefaults.PlanetResources.Profiles[index];
        string key = profile.Availability.ToString().ToLowerInvariant();
        string availabilityLabel = profile.Availability.ToString().ToUpperInvariant();
        currentGenerationSection = "PLANET ENERGY (" + availabilityLabel + ")";
        AddResourceDiceColumn(
            index,
            key,
            "energy",
            "core-energy",
            "energy",
            "Core",
            candidate => candidate.CoreEnergy
        );
        AddResourceDiceColumn(
            index,
            key,
            "energy",
            "rim-energy",
            "energy",
            "Rim",
            candidate => candidate.RimEnergy
        );
        currentGenerationSection = "RAW MATERIALS (" + availabilityLabel + ")";
        AddResourceDiceColumn(
            index,
            key,
            "materials",
            "core-materials",
            "raw materials",
            "Core",
            candidate => candidate.CoreRawMaterials
        );
        AddResourceDiceColumn(
            index,
            key,
            "materials",
            "rim-materials",
            "raw materials",
            "Rim",
            candidate => candidate.RimRawMaterials
        );
        currentGenerationSection = "PLANET RESOURCE LIMITS (" + availabilityLabel + ")";
        AddGenerationTableCell(
            key + "-resource-minimum",
            key + "-energy-min",
            "Minimum",
            "Fewest resource points a planet can start with. Range: 0 or higher.",
            "Energy",
            0,
            int.MaxValue,
            config => config.PlanetResources.Profiles[index].EnergyMin,
            (config, value) => config.PlanetResources.Profiles[index].EnergyMin = value,
            (config, value) => value <= config.PlanetResources.Profiles[index].EnergyMax
        );
        AddGenerationTableCell(
            key + "-resource-minimum",
            key + "-materials-min",
            "Minimum",
            "Fewest resource points a planet can start with. Range: 0 or higher.",
            "Raw Materials",
            0,
            int.MaxValue,
            config => config.PlanetResources.Profiles[index].RawMaterialsMin,
            (config, value) => config.PlanetResources.Profiles[index].RawMaterialsMin = value,
            (config, value) => value <= config.PlanetResources.Profiles[index].RawMaterialsMax
        );
        AddGenerationTableCell(
            key + "-resource-maximum",
            key + "-energy-max",
            "Maximum",
            "Most resource points a planet can start with. Range: 0 or higher.",
            "Energy",
            0,
            int.MaxValue,
            config => config.PlanetResources.Profiles[index].EnergyMax,
            (config, value) => config.PlanetResources.Profiles[index].EnergyMax = value,
            (config, value) => value >= config.PlanetResources.Profiles[index].EnergyMin
        );
        AddGenerationTableCell(
            key + "-resource-maximum",
            key + "-materials-max",
            "Maximum",
            "Most resource points a planet can start with. Range: 0 or higher.",
            "Raw Materials",
            0,
            int.MaxValue,
            config => config.PlanetResources.Profiles[index].RawMaterialsMax,
            (config, value) => config.PlanetResources.Profiles[index].RawMaterialsMax = value,
            (config, value) => value >= config.PlanetResources.Profiles[index].RawMaterialsMin
        );
        currentGenerationSection = "RIM COLONIZATION";
        AddGeneration(
            NewGameSettingsCategory.WorldGeneration,
            key + "-rim-colonization",
            "Rim Colonization Chance %",
            "Chance that an unclaimed rim world starts colonized. Range: 0% to 100%.",
            0,
            100,
            config => config.PlanetResources.Profiles[index].RimColonizationPct,
            (config, value) => config.PlanetResources.Profiles[index].RimColonizationPct = value
        );
    }

    /// <summary>
    /// Adds one Core or Rim column for a resource-roll table.
    /// </summary>
    /// <param name="profileIndex">The owning resource-profile index.</param>
    /// <param name="profileKey">The resource-profile key.</param>
    /// <param name="resourceKey">The shared resource row-key prefix.</param>
    /// <param name="formulaKey">The formula key.</param>
    /// <param name="resourceLabel">The resource name used in help text.</param>
    /// <param name="column">The Core or Rim column label.</param>
    /// <param name="select">Selects the formula from a profile.</param>
    private void AddResourceDiceColumn(
        int profileIndex,
        string profileKey,
        string resourceKey,
        string formulaKey,
        string resourceLabel,
        string column,
        Func<PlanetResourceProfile, DiceFormula> select
    )
    {
        string prefix = profileKey + "-" + formulaKey;
        AddGenerationTableCell(
            profileKey + "-" + resourceKey + "-base",
            prefix + "-base",
            "Starting Amount",
            $"Amount of {resourceLabel} each planet starts with before random bonuses. Range: 0 or higher.",
            column,
            0,
            int.MaxValue,
            config => select(config.PlanetResources.Profiles[profileIndex]).Base,
            (config, value) => select(config.PlanetResources.Profiles[profileIndex]).Base = value
        );
        AddGenerationTableCell(
            profileKey + "-" + resourceKey + "-random-1",
            prefix + "-random-1",
            "Random Bonus",
            $"Largest first random {resourceLabel} bonus a planet can receive. Range: 0 or higher.",
            column,
            0,
            int.MaxValue,
            config => select(config.PlanetResources.Profiles[profileIndex]).Random1,
            (config, value) => select(config.PlanetResources.Profiles[profileIndex]).Random1 = value
        );
        AddGenerationTableCell(
            profileKey + "-" + resourceKey + "-random-2",
            prefix + "-random-2",
            "Secondary Bonus",
            $"Largest second random {resourceLabel} bonus a planet can receive. Range: 0 or higher.",
            column,
            0,
            int.MaxValue,
            config => select(config.PlanetResources.Profiles[profileIndex]).Random2,
            (config, value) => select(config.PlanetResources.Profiles[profileIndex]).Random2 = value
        );
    }

    /// <summary>
    /// Adds the consumed starting-support formulas.
    /// </summary>
    private void AddPlanetSupportEntries()
    {
        AddSupportFormulaEntries("strong", "Strong", config => config.PlanetSupport.Strong);
        AddSupportFormulaEntries("weak", "Weak", config => config.PlanetSupport.Weak);
        AddSupportFormulaEntries("neutral", "Neutral", config => config.PlanetSupport.Neutral);
    }

    /// <summary>
    /// Adds one support formula's base and inclusive random range.
    /// </summary>
    /// <param name="key">The support bucket key.</param>
    /// <param name="label">The support bucket label.</param>
    /// <param name="select">Selects the formula from a generation configuration.</param>
    private void AddSupportFormulaEntries(
        string key,
        string label,
        Func<GameGenerationConfig, SupportFormula> select
    )
    {
        AddGenerationTableCell(
            "support-base",
            key + "-support-base",
            "Base Support",
            "Popular support before the random bonus. Range: 0% to 100%.",
            label,
            0,
            100,
            config => select(config).Base,
            (config, value) => select(config).Base = value
        );
        AddGenerationTableCell(
            "support-random",
            key + "-support-random",
            "Random Bonus",
            "Largest random popular-support bonus at game start. Range: 0% to 100%.",
            label,
            0,
            100,
            config => select(config).Random,
            (config, value) => select(config).Random = value
        );
    }

    /// <summary>
    /// Adds player-meaningful facility-generation controls.
    /// </summary>
    private void AddFacilityEntries()
    {
        AddGenerationTableCell(
            "mine-chance-per-resource",
            "core-mine-multiplier",
            "Mine Chance per Unused Resource %",
            "Extra chance to place a mine for each raw material without one. Range: 0% or higher.",
            "Core",
            0,
            int.MaxValue,
            config => config.FacilityGeneration.CoreMineMultiplier,
            (config, value) => config.FacilityGeneration.CoreMineMultiplier = value
        );
        AddGenerationTableCell(
            "mine-chance-per-resource",
            "rim-mine-multiplier",
            "Mine Chance per Unused Resource %",
            "Extra chance to place a mine for each raw material without one. Range: 0% or higher.",
            "Rim",
            0,
            int.MaxValue,
            config => config.FacilityGeneration.RimMineMultiplier,
            (config, value) => config.FacilityGeneration.RimMineMultiplier = value
        );
    }

    /// <summary>
    /// Adds low-support garrison and post-generation balance controls.
    /// </summary>
    private void AddUnitAndBalanceEntries()
    {
        AddGeneration(
            NewGameSettingsCategory.WorldGeneration,
            "uprising-prevention-threshold",
            "Garrison Support Threshold %",
            "Planets below this owner support start with garrison troops. Range: 0% to 100%.",
            0,
            100,
            config => config.UnitDeployment.UprisingPreventionThreshold,
            (config, value) => config.UnitDeployment.UprisingPreventionThreshold = value
        );
        AddGeneration(
            NewGameSettingsCategory.WorldGeneration,
            "support-deficit-per-garrison",
            "Support Deficit per Garrison Unit",
            "One starting garrison troop is added for each amount of missing owner support. Range: 1 or higher.",
            1,
            int.MaxValue,
            config => config.UnitDeployment.SupportDeficitPerGarrisonTroop,
            (config, value) => config.UnitDeployment.SupportDeficitPerGarrisonTroop = value
        );
        AddGeneration(
            NewGameSettingsCategory.WorldGeneration,
            "support-boost-per-unit",
            "Support per Starting Force",
            "Each starting regiment, fighter squadron, or fleet adds this many owner-support points to its planet. Range: 0 or higher.",
            0,
            int.MaxValue,
            config => config.Balance.SupportBoostPerUnit,
            (config, value) => config.Balance.SupportBoostPerUnit = value
        );
        AddGeneration(
            NewGameSettingsCategory.WorldGeneration,
            "maximum-military-support",
            "Maximum Starting Force Support",
            "Most owner-support points a planet can gain from its starting regiments, fighter squadrons, and fleets. Range: 0 or higher.",
            0,
            int.MaxValue,
            config => config.Balance.MaxMilitaryPresenceBoost,
            (config, value) => config.Balance.MaxMilitaryPresenceBoost = value
        );
    }

    /// <summary>
    /// Registers one validated integer setting from the launch-specific generation config.
    /// </summary>
    /// <param name="category">The settings page containing the value.</param>
    /// <param name="key">The stable row identity.</param>
    /// <param name="label">The visible value name.</param>
    /// <param name="description">The visible behavior description.</param>
    /// <param name="minimum">The inclusive minimum value.</param>
    /// <param name="maximum">The inclusive maximum value.</param>
    /// <param name="read">Reads the value from a generation config.</param>
    /// <param name="write">Writes the value to a generation config.</param>
    /// <param name="isValid">Optional validation involving another config value.</param>
    private void AddGeneration(
        NewGameSettingsCategory category,
        string key,
        string label,
        string description,
        int minimum,
        int maximum,
        Func<GameGenerationConfig, int> read,
        Action<GameGenerationConfig, int> write,
        Func<GameGenerationConfig, int, bool> isValid = null
    )
    {
        generationEntries.Add(
            new GenerationEntry
            {
                Category = category,
                Section = currentGenerationSection,
                RowKey = key,
                Key = key,
                Label = label,
                Description = description,
                Minimum = minimum,
                Maximum = maximum,
                Read = read,
                Write = write,
                IsValid = isValid,
            }
        );
    }

    /// <summary>
    /// Registers one validated integer cell in a world-generation table.
    /// </summary>
    /// <param name="rowKey">The stable identity shared by cells in one row.</param>
    /// <param name="key">The stable cell identity.</param>
    /// <param name="label">The visible row label.</param>
    /// <param name="description">The visible row description.</param>
    /// <param name="column">The visible column label.</param>
    /// <param name="minimum">The inclusive minimum value.</param>
    /// <param name="maximum">The inclusive maximum value.</param>
    /// <param name="read">Reads the value from a generation config.</param>
    /// <param name="write">Writes the value to a generation config.</param>
    /// <param name="isValid">Optional validation involving another config value.</param>
    private void AddGenerationTableCell(
        string rowKey,
        string key,
        string label,
        string description,
        string column,
        int minimum,
        int maximum,
        Func<GameGenerationConfig, int> read,
        Action<GameGenerationConfig, int> write,
        Func<GameGenerationConfig, int, bool> isValid = null
    )
    {
        generationEntries.Add(
            new GenerationEntry
            {
                Category = NewGameSettingsCategory.WorldGeneration,
                Section = currentGenerationSection,
                RowKey = rowKey,
                Key = key,
                Label = label,
                Description = description,
                Column = column,
                Minimum = minimum,
                Maximum = maximum,
                Read = read,
                Write = write,
                IsValid = isValid,
            }
        );
    }

    /// <summary>
    /// Registers one validated integer cell in a faction-modifier table.
    /// </summary>
    /// <param name="rowKey">The stable identity shared by cells in one row.</param>
    /// <param name="key">The stable cell identity.</param>
    /// <param name="label">The visible row label.</param>
    /// <param name="description">The visible behavior description.</param>
    /// <param name="column">The visible faction label.</param>
    /// <param name="minimum">The inclusive minimum value.</param>
    /// <param name="maximum">The inclusive maximum value.</param>
    /// <param name="read">Reads the value from a launch configuration.</param>
    /// <param name="write">Writes the value to a launch configuration.</param>
    private void AddFactionModifierTableCell(
        string rowKey,
        string key,
        string label,
        string description,
        string column,
        int minimum,
        int maximum,
        Func<GameGenerationConfig, int> read,
        Action<GameGenerationConfig, int> write
    )
    {
        generationEntries.Add(
            new GenerationEntry
            {
                Category = NewGameSettingsCategory.FactionModifiers,
                Section = currentGenerationSection,
                RowKey = rowKey,
                Key = key,
                Label = label,
                Description = description,
                Column = column,
                Minimum = minimum,
                Maximum = maximum,
                Read = read,
                Write = write,
            }
        );
    }

    /// <summary>
    /// Adds one modifier and its defaults and initial values to the editable draft.
    /// </summary>
    /// <param name="key">The modifier identity.</param>
    /// <param name="label">The visible modifier name.</param>
    /// <param name="description">The help text.</param>
    /// <param name="allowsFraction">Whether fractional input is accepted.</param>
    /// <param name="minimum">The minimum accepted value.</param>
    /// <param name="minimumExclusive">Whether the minimum itself is rejected.</param>
    /// <param name="maximum">The optional maximum accepted value.</param>
    /// <param name="playerDefault">The authored player default.</param>
    /// <param name="aiDefault">The authored AI default.</param>
    /// <param name="playerValue">The initial player value.</param>
    /// <param name="aiValue">The initial AI value.</param>
    private void Add(
        string key,
        string label,
        string description,
        bool allowsFraction,
        double minimum,
        bool minimumExclusive,
        double? maximum,
        double playerDefault,
        double aiDefault,
        double playerValue,
        double aiValue
    )
    {
        difficultyEntries.Add(
            (
                key,
                currentDifficultySection,
                label,
                description,
                allowsFraction,
                minimum,
                minimumExclusive,
                maximum,
                playerDefault,
                aiDefault
            )
        );
        playerDraft.Add(key, playerValue);
        aiDraft.Add(key, aiValue);
    }

    /// <summary>
    /// Creates a modifier object from one column's draft values.
    /// </summary>
    /// <param name="values">The values keyed by modifier identity.</param>
    /// <returns>The populated difficulty modifiers.</returns>
    private static DifficultyModifiers CreateModifiers(IReadOnlyDictionary<string, double> values)
    {
        return new DifficultyModifiers
        {
            MineOutputPercent = (int)values["mine-output"],
            RefineryOutputPercent = (int)values["refinery-output"],
            MaintenanceCapacityPercent = (int)values["maintenance"],
            ManufacturingSpeedPercent = (int)values["manufacturing-speed"],
            MissionSuccessChancePoints = (int)values["mission-success"],
            MissionFoilChancePoints = (int)values["mission-foil"],
            MissionExecutionSpeedIncreasePercent = (int)values["mission-speed"],
            DetectionRatingMultiplier = values["detection-rating"],
        };
    }
}
