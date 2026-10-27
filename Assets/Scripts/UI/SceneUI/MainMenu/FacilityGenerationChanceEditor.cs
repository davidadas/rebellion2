using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Units;

/// <summary>
/// Reads and edits player-facing chances in cumulative starting-facility tables.
/// </summary>
internal sealed class FacilityGenerationChanceEditor
{
    private readonly IReadOnlyDictionary<string, BuildingType> buildingTypesByID;

    /// <summary>
    /// Creates a facility chance editor for the active content's building definitions.
    /// </summary>
    /// <param name="buildings">Building definitions used by facility-generation tables.</param>
    internal FacilityGenerationChanceEditor(IEnumerable<Building> buildings)
    {
        buildingTypesByID = (buildings ?? throw new ArgumentNullException(nameof(buildings)))
            .Where(building => building != null && !string.IsNullOrEmpty(building.TypeID))
            .GroupBy(building => building.TypeID)
            .ToDictionary(group => group.Key, group => group.First().BuildingType);
    }

    /// <summary>
    /// Determines whether a regional table contains the requested facility category and an empty
    /// outcome that can absorb chance changes.
    /// </summary>
    /// <param name="config">Generation configuration containing the regional table.</param>
    /// <param name="core">Whether to inspect the core-region table.</param>
    /// <param name="buildingType">Facility category to find.</param>
    /// <returns>True when the category can be edited without changing unrelated outcomes.</returns>
    internal bool CanEdit(GameGenerationConfig config, bool core, BuildingType buildingType)
    {
        List<WeightedFacilityEntry> entries = SelectEntries(config, core);
        return entries.Any(IsEmpty) && entries.Any(entry => IsType(entry, buildingType));
    }

    /// <summary>
    /// Returns the number of regional table outcomes assigned to a facility category.
    /// </summary>
    /// <param name="config">Generation configuration containing the regional table.</param>
    /// <param name="core">Whether to inspect the core-region table.</param>
    /// <param name="buildingType">Facility category to total.</param>
    /// <returns>The configured chance units for the category.</returns>
    internal int GetChance(GameGenerationConfig config, bool core, BuildingType buildingType)
    {
        FacilityGenerationSection section = GetSection(config);
        List<WeightedFacilityEntry> entries = SelectEntries(section, core);
        int[] weights = ReadWeights(section, entries);
        int chance = 0;
        for (int index = 0; index < entries.Count; index++)
        {
            if (IsType(entries[index], buildingType))
                chance += weights[index];
        }

        return chance;
    }

    /// <summary>
    /// Determines whether a category chance can be changed using only that category and empty
    /// outcomes.
    /// </summary>
    /// <param name="config">Generation configuration containing the regional table.</param>
    /// <param name="core">Whether to inspect the core-region table.</param>
    /// <param name="buildingType">Facility category to change.</param>
    /// <param name="chance">Requested chance units.</param>
    /// <returns>True when the requested chance preserves every unrelated outcome.</returns>
    internal bool CanSetChance(
        GameGenerationConfig config,
        bool core,
        BuildingType buildingType,
        int chance
    )
    {
        if (chance < 0)
            return false;

        FacilityGenerationSection section = GetSection(config);
        List<WeightedFacilityEntry> entries = SelectEntries(section, core);
        int[] weights = ReadWeights(section, entries);
        int current = 0;
        int empty = 0;
        bool found = false;
        for (int index = 0; index < entries.Count; index++)
        {
            if (IsType(entries[index], buildingType))
            {
                current += weights[index];
                found = true;
            }
            else if (IsEmpty(entries[index]))
            {
                empty += weights[index];
            }
        }

        return found && chance <= current + empty;
    }

    /// <summary>
    /// Changes one category's chance while preserving all other facility chances and the table's
    /// total roll range.
    /// </summary>
    /// <param name="config">Generation configuration containing the regional table.</param>
    /// <param name="core">Whether to edit the core-region table.</param>
    /// <param name="buildingType">Facility category to change.</param>
    /// <param name="chance">Requested chance units.</param>
    /// <returns>True when the table was updated.</returns>
    internal bool TrySetChance(
        GameGenerationConfig config,
        bool core,
        BuildingType buildingType,
        int chance
    )
    {
        if (!CanSetChance(config, core, buildingType, chance))
            return false;

        FacilityGenerationSection section = GetSection(config);
        List<WeightedFacilityEntry> entries = SelectEntries(section, core);
        int[] weights = ReadWeights(section, entries);
        List<int> targetIndexes = new List<int>();
        List<int> emptyIndexes = new List<int>();
        for (int index = 0; index < entries.Count; index++)
        {
            if (IsType(entries[index], buildingType))
                targetIndexes.Add(index);
            else if (IsEmpty(entries[index]))
                emptyIndexes.Add(index);
        }

        int current = targetIndexes.Sum(index => weights[index]);
        int difference = chance - current;
        if (difference > 0)
        {
            RemoveWeight(weights, emptyIndexes, difference);
            weights[targetIndexes[0]] += difference;
        }
        else if (difference < 0)
        {
            int released = -difference;
            RemoveWeight(weights, targetIndexes, released);
            weights[emptyIndexes[0]] += released;
        }

        WriteWeights(section, entries, weights);
        return true;
    }

    /// <summary>
    /// Removes a requested number of outcomes from a set of table entries.
    /// </summary>
    /// <param name="weights">Mutable outcome weights.</param>
    /// <param name="indexes">Entries that may supply outcomes.</param>
    /// <param name="amount">Number of outcomes to remove.</param>
    private static void RemoveWeight(int[] weights, IReadOnlyList<int> indexes, int amount)
    {
        for (int position = indexes.Count - 1; position >= 0 && amount > 0; position--)
        {
            int index = indexes[position];
            int removed = Math.Min(weights[index], amount);
            weights[index] -= removed;
            amount -= removed;
        }

        if (amount != 0)
            throw new InvalidOperationException(
                "Facility chance table did not contain enough weight."
            );
    }

    /// <summary>
    /// Reconstructs cumulative table boundaries from discrete outcome weights.
    /// </summary>
    /// <param name="section">Facility generation settings defining the roll range.</param>
    /// <param name="entries">Ordered cumulative table entries.</param>
    /// <param name="weights">Outcome weights corresponding to the entries.</param>
    private static void WriteWeights(
        FacilityGenerationSection section,
        IReadOnlyList<WeightedFacilityEntry> entries,
        IReadOnlyList<int> weights
    )
    {
        int cumulative = section.FacilityTableRollMin;
        for (int index = 0; index < entries.Count; index++)
        {
            entries[index].CumulativeWeight = cumulative;
            cumulative += weights[index];
        }

        if (cumulative != section.FacilityTableRollMaxExclusive)
            throw new InvalidOperationException("Facility chance table changed its roll range.");
    }

    /// <summary>
    /// Expands cumulative table boundaries into one discrete weight per entry.
    /// </summary>
    /// <param name="section">Facility generation settings defining the roll range.</param>
    /// <param name="entries">Ordered cumulative table entries.</param>
    /// <returns>Discrete outcome weights corresponding to the entries.</returns>
    private static int[] ReadWeights(
        FacilityGenerationSection section,
        IReadOnlyList<WeightedFacilityEntry> entries
    )
    {
        if (entries == null || entries.Count == 0)
            return Array.Empty<int>();

        int[] weights = new int[entries.Count];
        for (int index = 0; index < entries.Count; index++)
        {
            int lower = index == 0 ? section.FacilityTableRollMin : entries[index].CumulativeWeight;
            int upper =
                index == entries.Count - 1
                    ? section.FacilityTableRollMaxExclusive
                    : entries[index + 1].CumulativeWeight;
            if (upper < lower)
                throw new InvalidOperationException("Facility chance table is not cumulative.");

            weights[index] = upper - lower;
        }

        return weights;
    }

    /// <summary>
    /// Selects one regional facility table from a complete generation configuration.
    /// </summary>
    /// <param name="config">Generation configuration to inspect.</param>
    /// <param name="core">Whether to select the core-region table.</param>
    /// <returns>The selected facility table.</returns>
    private static List<WeightedFacilityEntry> SelectEntries(
        GameGenerationConfig config,
        bool core
    ) => SelectEntries(GetSection(config), core);

    /// <summary>
    /// Selects one regional facility table from facility generation settings.
    /// </summary>
    /// <param name="section">Facility generation settings to inspect.</param>
    /// <param name="core">Whether to select the core-region table.</param>
    /// <returns>The selected facility table.</returns>
    private static List<WeightedFacilityEntry> SelectEntries(
        FacilityGenerationSection section,
        bool core
    ) =>
        (core ? section.CoreFacilityTable : section.RimFacilityTable)
        ?? new List<WeightedFacilityEntry>();

    /// <summary>
    /// Returns validated facility generation settings from a game generation configuration.
    /// </summary>
    /// <param name="config">Generation configuration to inspect.</param>
    /// <returns>The contained facility generation settings.</returns>
    private static FacilityGenerationSection GetSection(GameGenerationConfig config) =>
        config?.FacilityGeneration
        ?? throw new ArgumentException(
            "Facility generation settings are required.",
            nameof(config)
        );

    /// <summary>
    /// Determines whether a table entry represents an unoccupied outcome.
    /// </summary>
    /// <param name="entry">Table entry to inspect.</param>
    /// <returns>True when the entry does not create a facility.</returns>
    private static bool IsEmpty(WeightedFacilityEntry entry) =>
        entry != null && string.IsNullOrEmpty(entry.TypeID);

    /// <summary>
    /// Determines whether a table entry creates a requested facility category.
    /// </summary>
    /// <param name="entry">Table entry to inspect.</param>
    /// <param name="buildingType">Facility category to match.</param>
    /// <returns>True when the entry resolves to that facility category.</returns>
    private bool IsType(WeightedFacilityEntry entry, BuildingType buildingType) =>
        entry != null
        && !string.IsNullOrEmpty(entry.TypeID)
        && buildingTypesByID.TryGetValue(entry.TypeID, out BuildingType resolved)
        && resolved == buildingType;
}
