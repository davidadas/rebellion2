using Rebellion.Game.Factions;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Messages
{
    [PersistableObject]
    public class MessageImageMap
    {
        public string Default { get; set; }
        public string FNALL1 { get; set; }
        public string FNEMP1 { get; set; }

        public string GetForFaction(Faction faction)
        {
            return faction?.InstanceID switch
            {
                "FNALL1" when !string.IsNullOrEmpty(FNALL1) => FNALL1,
                "FNEMP1" when !string.IsNullOrEmpty(FNEMP1) => FNEMP1,
                _ => GetDefaultPath(),
            };
        }

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
