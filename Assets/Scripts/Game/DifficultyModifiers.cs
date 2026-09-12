using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Configures faction-wide AI bonuses for one game difficulty.
    /// </summary>
    [PersistableObject]
    public sealed class DifficultyModifiers
    {
        public int MissionSuccessChancePoints { get; set; }

        public int MineOutputPercent { get; set; } = 100;

        public int RefineryOutputPercent { get; set; } = 100;

        public int ManufacturingSpeedPercent { get; set; } = 100;
    }
}
