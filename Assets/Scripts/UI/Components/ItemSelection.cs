using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Holds a draft selection and quantities without changing its source collection.
/// </summary>
public sealed class ItemSelection
{
    private readonly Dictionary<string, int> counts;
    public IReadOnlyDictionary<string, int> Counts => counts;

    /// <summary>
    /// Copies the currently selected item quantities.
    /// </summary>
    /// <param name="values">The selected keys and their quantities.</param>
    public ItemSelection(IReadOnlyDictionary<string, int> values) =>
        counts = new Dictionary<string, int>(values, StringComparer.Ordinal);

    /// <summary>
    /// Selects an item, updates its quantity, or removes it with zero.
    /// </summary>
    /// <param name="key">The stable item identity.</param>
    /// <param name="count">The nonnegative quantity.</param>
    public void SetCount(string key, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0)
            counts.Remove(key);
        else
            counts[key] = count;
    }

    /// <summary>
    /// Retains existing order and duplicate entries while applying selected quantities.
    /// </summary>
    /// <param name="source">The original ordered values.</param>
    /// <returns>The updated values, with newly selected entries appended.</returns>
    public IReadOnlyList<string> ApplyTo(IReadOnlyList<string> source)
    {
        Dictionary<string, int> remaining = new(counts, StringComparer.Ordinal);
        List<string> result = new();
        foreach (string value in source)
            if (remaining.TryGetValue(value, out int count) && count > 0)
            {
                result.Add(value);
                remaining[value] = count - 1;
            }
        foreach (var item in remaining)
            for (int index = 0; index < item.Value; index++)
                result.Add(item.Key);
        return result;
    }

    /// <summary>
    /// Counts repeated source values without removing their ordering information.
    /// </summary>
    /// <param name="values">The original values.</param>
    /// <returns>The quantity of every distinct value.</returns>
    public static IReadOnlyDictionary<string, int> Count(IEnumerable<string> values) =>
        values
            .GroupBy(value => value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
}
