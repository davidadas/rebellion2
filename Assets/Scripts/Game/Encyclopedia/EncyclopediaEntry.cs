using System.Collections.Generic;
using System.Text;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Encyclopedia
{
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
    public sealed class EncyclopediaEntries : List<EncyclopediaEntry> { }
}
