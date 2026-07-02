using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Rebellion.Game.Encyclopedia
{
    /// <summary>
    /// Provides the merged encyclopedia entries used by UI consumers.
    /// </summary>
    public sealed class EncyclopediaCatalog : IReadOnlyList<EncyclopediaEntry>
    {
        private readonly List<EncyclopediaEntry> _entries;

        /// <summary>
        /// Creates a catalog from encyclopedia entries.
        /// </summary>
        /// <param name="entries">The entries to include in the catalog.</param>
        public EncyclopediaCatalog(IEnumerable<EncyclopediaEntry> entries)
        {
            _entries =
                entries?.Where(entry => entry != null).ToList() ?? new List<EncyclopediaEntry>();
        }

        public int Count => _entries.Count;

        public EncyclopediaEntry this[int index] => _entries[index];

        /// <summary>
        /// Gets the entries visible in an encyclopedia category for a faction.
        /// </summary>
        /// <param name="category">The encyclopedia category, or null for all categories.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entries.</param>
        /// <returns>The visible entries sorted by display name.</returns>
        public List<EncyclopediaEntry> GetRows(
            EncyclopediaEntryCategory? category,
            string factionInstanceId
        )
        {
            return GetRows(_entries, category, factionInstanceId);
        }

        /// <summary>
        /// Finds an entry visible to a faction by type ID.
        /// </summary>
        /// <param name="typeId">The entry type ID.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entry.</param>
        /// <returns>The matching entry, or null when no visible entry exists.</returns>
        public EncyclopediaEntry FindEntry(string typeId, string factionInstanceId)
        {
            return FindEntry(_entries, typeId, factionInstanceId);
        }

        /// <summary>
        /// Gets an enumerator over the catalog entries.
        /// </summary>
        /// <returns>An enumerator over the catalog entries.</returns>
        public IEnumerator<EncyclopediaEntry> GetEnumerator()
        {
            return _entries.GetEnumerator();
        }

        /// <summary>
        /// Gets a non-generic enumerator over the catalog entries.
        /// </summary>
        /// <returns>A non-generic enumerator over the catalog entries.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Gets entries visible in an encyclopedia category for a faction.
        /// </summary>
        /// <param name="entries">The entries to filter.</param>
        /// <param name="category">The encyclopedia category, or null for all categories.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entries.</param>
        /// <returns>The visible entries sorted by display name.</returns>
        public static List<EncyclopediaEntry> GetRows(
            IEnumerable<EncyclopediaEntry> entries,
            EncyclopediaEntryCategory? category,
            string factionInstanceId
        )
        {
            List<EncyclopediaEntry> rows = new List<EncyclopediaEntry>();
            if (entries == null)
                return rows;

            foreach (EncyclopediaEntry entry in entries)
            {
                if (
                    entry != null
                    && IsInCategory(entry, category)
                    && IsVisibleToFaction(entry, factionInstanceId)
                )
                    rows.Add(entry);
            }

            rows.Sort(
                (left, right) =>
                    string.Compare(
                        left?.DisplayName,
                        right?.DisplayName,
                        StringComparison.OrdinalIgnoreCase
                    )
            );
            return rows;
        }

        /// <summary>
        /// Finds an entry visible to a faction by type ID.
        /// </summary>
        /// <param name="entries">The entries to search.</param>
        /// <param name="typeId">The entry type ID.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entry.</param>
        /// <returns>The matching entry, or null when no visible entry exists.</returns>
        public static EncyclopediaEntry FindEntry(
            IEnumerable<EncyclopediaEntry> entries,
            string typeId,
            string factionInstanceId
        )
        {
            if (entries == null || string.IsNullOrEmpty(typeId))
                return null;

            foreach (EncyclopediaEntry entry in entries)
            {
                if (
                    string.Equals(entry?.TypeID, typeId, StringComparison.Ordinal)
                    && IsVisibleToFaction(entry, factionInstanceId)
                )
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Checks whether an entry belongs to the requested encyclopedia category.
        /// </summary>
        /// <param name="entry">The entry to check.</param>
        /// <param name="category">The encyclopedia category, or null for all categories.</param>
        /// <returns>True when the entry belongs in the category.</returns>
        private static bool IsInCategory(
            EncyclopediaEntry entry,
            EncyclopediaEntryCategory? category
        )
        {
            return !category.HasValue || entry.Category == category.Value;
        }

        /// <summary>
        /// Checks whether an entry can be viewed by a faction.
        /// </summary>
        /// <param name="entry">The entry to check.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entry.</param>
        /// <returns>True when the entry can be viewed by the faction.</returns>
        private static bool IsVisibleToFaction(EncyclopediaEntry entry, string factionInstanceId)
        {
            return string.IsNullOrEmpty(entry.VisibleFactionInstanceID)
                || string.Equals(
                    entry.VisibleFactionInstanceID,
                    factionInstanceId,
                    StringComparison.Ordinal
                );
        }
    }
}
