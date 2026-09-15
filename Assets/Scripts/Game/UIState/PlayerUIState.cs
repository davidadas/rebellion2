using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.UIState
{
    /// <summary>
    /// Stores durable, save-specific interface choices for one player.
    /// </summary>
    [PersistableObject]
    public sealed class PlayerUIState
    {
        public List<UIStateSection> UIStateSections { get; set; } = new List<UIStateSection>();

        /// <summary>
        /// Gets the state section with the supplied identifier, creating it when necessary.
        /// </summary>
        /// <param name="sectionID">The stable identifier owned by the interface area.</param>
        /// <returns>The matching state section.</returns>
        public UIStateSection GetOrCreateSection(string sectionID)
        {
            if (string.IsNullOrWhiteSpace(sectionID))
                throw new ArgumentException(
                    "A UI state section identifier is required.",
                    nameof(sectionID)
                );

            UIStateSection section = UIStateSections.FirstOrDefault(item =>
                item.SectionID == sectionID
            );
            if (section != null)
                return section;

            section = new UIStateSection { SectionID = sectionID };
            UIStateSections.Add(section);
            return section;
        }
    }
}
