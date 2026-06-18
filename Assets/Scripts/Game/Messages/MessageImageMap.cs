using Rebellion.Game.Factions;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Maps message artwork to faction-specific or default image paths.
    /// </summary>
    [PersistableObject]
    public class MessageImageMap
    {
        public string Default { get; set; }
        public string FNALL1 { get; set; }
        public string FNEMP1 { get; set; }

        /// <summary>
        /// Gets the configured image path for a faction.
        /// </summary>
        /// <param name="faction">The faction whose image path should be selected.</param>
        /// <returns>The faction-specific image path, or the fallback path when no faction-specific path is configured.</returns>
        public string GetForFaction(Faction faction)
        {
            return faction?.InstanceID switch
            {
                "FNALL1" when !string.IsNullOrEmpty(FNALL1) => FNALL1,
                "FNEMP1" when !string.IsNullOrEmpty(FNEMP1) => FNEMP1,
                _ => GetDefaultPath(),
            };
        }

        /// <summary>
        /// Gets the fallback image path.
        /// </summary>
        /// <returns>The default image path, or the first faction image path available.</returns>
        private string GetDefaultPath()
        {
            if (!string.IsNullOrEmpty(Default))
                return Default;

            if (!string.IsNullOrEmpty(FNALL1))
                return FNALL1;

            return FNEMP1;
        }
    }
}
