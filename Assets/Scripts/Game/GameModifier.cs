using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Modifies faction-wide simulation outcomes without changing saved game state.
    /// </summary>
    [PersistableObject]
    public sealed class GameModifier
    {
        public static GameModifier Neutral { get; } = new GameModifier();

        public int MissionSuccessChancePoints { get; set; }

        public int MineOutputPercent { get; set; } = 100;

        public int RefineryOutputPercent { get; set; } = 100;

        public int ManufacturingSpeedPercent { get; set; } = 100;
    }
}
