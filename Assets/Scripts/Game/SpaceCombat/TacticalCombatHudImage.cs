using Rebellion.Util.Serialization;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Associates one semantic tactical image with a private content address.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatHudImage
    {
        public string Key { get; set; }
        public string Path { get; set; }
    }
}
