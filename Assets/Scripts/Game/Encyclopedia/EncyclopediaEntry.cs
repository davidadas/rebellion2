using System;
using System.Collections.Generic;
using System.Text;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Encyclopedia
{
    [PersistableObject]
    public sealed class EncyclopediaEntry
    {
        public string TypeID { get; set; }
        public string DisplayName { get; set; }
        public EncyclopediaEntryCategory Category { get; set; }
        public string VisibleFactionInstanceID { get; set; }
        public string OwnerInstanceID { get; set; }
        public string ImagePath { get; set; }
        public List<EncyclopediaEntryStat> Stats { get; set; } = new List<EncyclopediaEntryStat>();
        public string Description { get; set; }

        /// <summary>
        /// Builds the rendered encyclopedia detail text from this entry's stat rows and description.
        /// </summary>
        /// <returns>The formatted detail text for the encyclopedia detail panel.</returns>
        public string GetInfoText()
        {
            StringBuilder builder = new StringBuilder();
            if (Stats != null)
            {
                for (int i = 0; i < Stats.Count; i++)
                {
                    string text = Stats[i]?.GetText();
                    if (string.IsNullOrEmpty(text))
                        continue;

                    if (builder.Length > 0)
                        builder.AppendLine();
                    builder.Append(text);
                }
            }

            if (!string.IsNullOrEmpty(Description))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }
                builder.Append(Description);
            }

            return builder.ToString();
        }
    }

    [PersistableObject]
    public sealed class EncyclopediaEntryStat
    {
        public string Label { get; set; }
        public string Value { get; set; }

        /// <summary>
        /// Builds the rendered text for one encyclopedia stat row.
        /// </summary>
        /// <returns>The formatted stat row text.</returns>
        public string GetText()
        {
            if (string.IsNullOrEmpty(Label))
                return Value;
            if (string.IsNullOrEmpty(Value))
                return Label + ":";

            return Label + ":\t" + Value;
        }
    }

    public enum EncyclopediaEntryCategory
    {
        Concept,
        System,
        Ship,
        Facility,
        Mission,
        Troop,
        Personnel,
    }

    [PersistableObject]
    public sealed class EncyclopediaEntries : List<EncyclopediaEntry>
    {
        /// <summary>
        /// Gets all entries visible in the requested encyclopedia tab.
        /// </summary>
        /// <param name="tab">The encyclopedia tab index.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entries.</param>
        /// <returns>The visible entries sorted by display name.</returns>
        public List<EncyclopediaEntry> GetRows(int tab, string factionInstanceId)
        {
            List<EncyclopediaEntry> rows = new List<EncyclopediaEntry>();
            for (int i = 0; i < Count; i++)
            {
                EncyclopediaEntry entry = this[i];
                if (
                    entry != null
                    && IsInTab(entry, tab)
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
        /// Finds an entry by type ID.
        /// </summary>
        /// <param name="typeId">The entry type ID to match.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entry.</param>
        /// <returns>The matching entry, or null when no entry exists.</returns>
        public EncyclopediaEntry FindEntry(string typeId, string factionInstanceId)
        {
            if (string.IsNullOrEmpty(typeId))
                return null;

            for (int i = 0; i < Count; i++)
            {
                EncyclopediaEntry entry = this[i];
                if (
                    string.Equals(entry?.TypeID, typeId, StringComparison.Ordinal)
                    && IsVisibleToFaction(entry, factionInstanceId)
                )
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Checks whether an entry belongs in a tab.
        /// </summary>
        /// <param name="entry">The entry to check.</param>
        /// <param name="tab">The encyclopedia tab index.</param>
        /// <returns>True when the entry is visible in the tab.</returns>
        private static bool IsInTab(EncyclopediaEntry entry, int tab)
        {
            return tab == 0
                || tab == 1 && entry.Category == EncyclopediaEntryCategory.System
                || tab == 2 && entry.Category == EncyclopediaEntryCategory.Ship
                || tab == 3 && entry.Category == EncyclopediaEntryCategory.Facility
                || tab == 4 && entry.Category == EncyclopediaEntryCategory.Mission
                || tab == 5 && entry.Category == EncyclopediaEntryCategory.Troop
                || tab == 6 && entry.Category == EncyclopediaEntryCategory.Personnel;
        }

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
