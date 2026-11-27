using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Orders enabled mods and reports requirements that prevent a mod from loading.
/// </summary>
internal static class ContentModLoadOrder
{
    /// <summary>
    /// Resolves the enabled set using author rules, then the applied order and stable mod IDs.
    /// </summary>
    /// <param name="enabled">The requested mods in legacy discovery order.</param>
    /// <param name="issues">Receives explanations for excluded mods.</param>
    /// <param name="preferredOrder">The applied or staged order, or null for legacy discovery.</param>
    /// <returns>The loadable mods in application order.</returns>
    internal static IReadOnlyList<ContentModDefinition> Resolve(
        IReadOnlyList<ContentModDefinition> enabled,
        ICollection<ContentModIssue> issues,
        IReadOnlyList<string> preferredOrder
    )
    {
        var selected = enabled.ToDictionary(mod => mod.ID, StringComparer.Ordinal);
        RemoveMissingRequirements(selected, issues);
        var conflicts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ContentModDefinition mod in selected.Values)
        foreach (string id in mod.IncompatibleWith)
            if (selected.TryGetValue(id, out ContentModDefinition other))
            {
                conflicts.TryAdd(mod.ID, other.DisplayName);
                conflicts.TryAdd(other.ID, mod.DisplayName);
            }
        foreach (var conflict in conflicts)
        {
            ContentModDefinition mod = selected[conflict.Key];
            issues.Add(
                new ContentModIssue(
                    mod.ID,
                    mod.DisplayName,
                    $"Cannot load with {conflict.Value}. Disable one of these mods.",
                    blocksSelection: true
                )
            );
            selected.Remove(mod.ID);
        }
        RemoveMissingRequirements(selected, issues);

        ContentModDefinition[] candidates =
            preferredOrder == null
                ? enabled.Where(mod => selected.ContainsKey(mod.ID)).ToArray()
                : preferredOrder
                    .Concat(selected.Keys.OrderBy(id => id, StringComparer.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .Where(id => id != null && selected.ContainsKey(id))
                    .Select(id => selected[id])
                    .ToArray();
        var indices = candidates
            .Select((mod, index) => (mod.ID, index))
            .ToDictionary(item => item.ID, item => item.index, StringComparer.Ordinal);
        HashSet<int>[] followers = candidates.Select(_ => new HashSet<int>()).ToArray();
        int[] incoming = new int[candidates.Length];
        for (int index = 0; index < candidates.Length; index++)
        {
            ContentModDefinition mod = candidates[index];
            foreach (
                string after in mod.LoadAfter.Concat(
                    mod.Requires.Select(requirement => requirement.ID)
                )
            )
                if (
                    indices.TryGetValue(after, out int predecessor)
                    && followers[predecessor].Add(index)
                )
                    incoming[index]++;
            foreach (string before in mod.LoadBefore)
                if (
                    indices.TryGetValue(before, out int successor)
                    && followers[index].Add(successor)
                )
                    incoming[successor]++;
        }

        List<int>[] predecessors = candidates.Select(_ => new List<int>()).ToArray();
        for (int index = 0; index < followers.Length; index++)
            foreach (int follower in followers[index])
                predecessors[follower].Add(index);

        var ready = new SortedSet<int>(
            Enumerable.Range(0, candidates.Length).Where(index => incoming[index] == 0)
        );
        var ordered = new List<ContentModDefinition>(candidates.Length);
        while (ready.Count > 0)
        {
            int index = ready.Min;
            ready.Remove(index);
            ordered.Add(candidates[index]);
            foreach (int follower in followers[index])
                if (--incoming[follower] == 0)
                    ready.Add(follower);
        }
        for (int index = 0; index < candidates.Length; index++)
            if (incoming[index] > 0)
            {
                ContentModDefinition mod = candidates[index];
                issues.Add(
                    new ContentModIssue(
                        mod.ID,
                        mod.DisplayName,
                        DescribeCycle(index, candidates, predecessors, incoming)
                            + " is a load-order cycle. Disable a mod in the cycle or correct its ordering rules.",
                        blocksSelection: true
                    )
                );
            }
        if (preferredOrder == null)
            return ordered;

        PreserveAppliedNeighbors(candidates, followers, predecessors, incoming, preferredOrder);
        return ApplyPreference(candidates, predecessors, incoming);
    }

    /// <summary>
    /// Checks the author rules for every mod crossed by a proposed move.
    /// </summary>
    /// <param name="mods">The current active mods in resolved order.</param>
    /// <param name="from">The current zero-based position.</param>
    /// <param name="to">The proposed zero-based position after moving the mod.</param>
    /// <returns>The first crossed rule that blocks the move, or an empty string.</returns>
    internal static string GetMoveRestriction(
        IReadOnlyList<ContentModDefinition> mods,
        int from,
        int to
    )
    {
        int step = Math.Sign(to - from);
        for (int index = from; index != to; )
        {
            index += step;
            string restriction = GetSwapRestriction(
                mods[Math.Min(from, index)],
                mods[Math.Max(from, index)]
            );
            if (restriction.Length != 0)
                return restriction;
        }
        return string.Empty;
    }

    /// <summary>
    /// Explains the direct author rule preventing two adjacent active mods from swapping.
    /// </summary>
    /// <param name="earlier">The first mod in the resolved order.</param>
    /// <param name="later">The next mod in the resolved order.</param>
    /// <returns>The blocking rule, or an empty string when the swap is legal.</returns>
    private static string GetSwapRestriction(
        ContentModDefinition earlier,
        ContentModDefinition later
    )
    {
        if (later.Requires.Any(requirement => requirement.ID == earlier.ID))
            return $"{later.DisplayName} requires {earlier.DisplayName}.";
        if (later.LoadAfter.Contains(earlier.ID) || earlier.LoadBefore.Contains(later.ID))
            return $"{earlier.DisplayName} must load before {later.DisplayName}.";
        return string.Empty;
    }

    /// <summary>
    /// Preserves applied neighbors where valid, including when a new rule moves both ahead of an earlier mod.
    /// </summary>
    /// <param name="candidates">The mods in preference order.</param>
    /// <param name="followers">The successor rules to check before adding each preference.</param>
    /// <param name="predecessors">The prerequisites to augment with legal preferences.</param>
    /// <param name="incoming">Remaining edges after resolving the author rules.</param>
    /// <param name="preferredOrder">The previously applied or staged order.</param>
    private static void PreserveAppliedNeighbors(
        ContentModDefinition[] candidates,
        HashSet<int>[] followers,
        List<int>[] predecessors,
        int[] incoming,
        IReadOnlyList<string> preferredOrder
    )
    {
        var applied = preferredOrder.ToHashSet(StringComparer.Ordinal);
        var visited = new HashSet<int>();
        var pending = new Stack<int>();
        int previous = -1;
        for (int index = 0; index < candidates.Length; index++)
        {
            if (incoming[index] != 0 || !applied.Contains(candidates[index].ID))
                continue;
            if (previous >= 0)
            {
                visited.Clear();
                pending.Clear();
                pending.Push(index);
                bool wouldCycle = false;
                while (pending.TryPop(out int follower))
                {
                    if (follower == previous)
                    {
                        wouldCycle = true;
                        break;
                    }
                    if (visited.Add(follower))
                        foreach (int next in followers[follower])
                            pending.Push(next);
                }

                if (!wouldCycle && followers[previous].Add(index))
                    predecessors[index].Add(previous);
            }
            previous = index;
        }
    }

    /// <summary>
    /// Visits each preferred mod after its own prerequisites, leaving unrelated mods in preference order.
    /// </summary>
    /// <param name="candidates">The candidates in preference order.</param>
    /// <param name="predecessors">Each candidate's direct prerequisites in preference order.</param>
    /// <param name="incoming">Remaining edges after resolving the graph; nonzero marks a blocked mod.</param>
    /// <returns>The active mods in application order.</returns>
    private static List<ContentModDefinition> ApplyPreference(
        ContentModDefinition[] candidates,
        List<int>[] predecessors,
        int[] incoming
    )
    {
        var result = new List<ContentModDefinition>(candidates.Length);
        bool[] added = new bool[candidates.Length];
        var pending = new Stack<(int Index, bool Add)>();
        for (int index = 0; index < candidates.Length; index++)
        {
            if (incoming[index] != 0 || added[index])
                continue;

            pending.Push((index, false));
            while (pending.TryPop(out var step))
            {
                if (added[step.Index])
                    continue;
                if (step.Add)
                {
                    result.Add(candidates[step.Index]);
                    added[step.Index] = true;
                    continue;
                }

                pending.Push((step.Index, true));
                List<int> requirements = predecessors[step.Index];
                for (int dependency = requirements.Count - 1; dependency >= 0; dependency--)
                    if (!added[requirements[dependency]])
                        pending.Push((requirements[dependency], false));
            }
        }
        return result;
    }

    /// <summary>
    /// Walks unresolved prerequisites to identify a concrete cycle blocking a mod.
    /// </summary>
    /// <param name="index">The blocked candidate.</param>
    /// <param name="candidates">The candidate definitions.</param>
    /// <param name="predecessors">Each candidate's incoming author rules.</param>
    /// <param name="incoming">Remaining edges after resolving the graph.</param>
    /// <returns>The cycle in application direction.</returns>
    private static string DescribeCycle(
        int index,
        ContentModDefinition[] candidates,
        List<int>[] predecessors,
        int[] incoming
    )
    {
        var path = new List<int>();
        var positions = new Dictionary<int, int>();
        while (positions.TryAdd(index, path.Count))
        {
            path.Add(index);
            index = predecessors[index].First(predecessor => incoming[predecessor] > 0);
        }
        return string.Join(
            " → ",
            path.Skip(positions[index])
                .Append(index)
                .Reverse()
                .Select(i => candidates[i].DisplayName)
        );
    }

    /// <summary>
    /// Excludes mods with missing, disabled, or mismatched requirements, including indirect requirements.
    /// </summary>
    /// <param name="selected">The remaining candidates indexed by ID.</param>
    /// <param name="issues">Receives one reason per excluded mod.</param>
    private static void RemoveMissingRequirements(
        Dictionary<string, ContentModDefinition> selected,
        ICollection<ContentModIssue> issues
    )
    {
        bool removed;
        do
        {
            removed = false;
            foreach (ContentModDefinition mod in selected.Values.ToArray())
            {
                ContentModRequirement missing = mod.Requires.FirstOrDefault(requirement =>
                    !selected.TryGetValue(requirement.ID, out ContentModDefinition dependency)
                    || !string.IsNullOrEmpty(requirement.Version)
                        && dependency.Version != requirement.Version
                );
                if (missing == null)
                    continue;

                string required = string.IsNullOrEmpty(missing.Version)
                    ? missing.ID
                    : $"{missing.ID} {missing.Version}";
                issues.Add(
                    new ContentModIssue(
                        mod.ID,
                        mod.DisplayName,
                        $"Requires {required}. Install and enable that mod.",
                        blocksSelection: true
                    )
                );
                selected.Remove(mod.ID);
                removed = true;
            }
        } while (removed);
    }
}
