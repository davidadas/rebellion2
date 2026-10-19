using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Configures faction-wide gameplay modifiers for one game difficulty.
    /// </summary>
    [PersistableObject]
    public sealed class DifficultyModifiers
    {
        public int MissionSuccessChancePoints { get; set; }

        public int MissionFoilChancePoints { get; set; }

        public int MissionExecutionSpeedIncreasePercent { get; set; }

        public double DetectionRatingMultiplier { get; set; } = 1;

        public int MineOutputPercent { get; set; } = 100;

        public int RefineryOutputPercent { get; set; } = 100;

        public int MaintenanceCapacityPercent { get; set; } = 100;

        public int ManufacturingSpeedPercent { get; set; } = 100;

        /// <summary>
        /// Creates an independent copy of these modifiers.
        /// </summary>
        /// <returns>A copy containing the same modifier values.</returns>
        public DifficultyModifiers CreateCopy()
        {
            return new DifficultyModifiers
            {
                MissionSuccessChancePoints = MissionSuccessChancePoints,
                MissionFoilChancePoints = MissionFoilChancePoints,
                MissionExecutionSpeedIncreasePercent = MissionExecutionSpeedIncreasePercent,
                DetectionRatingMultiplier = DetectionRatingMultiplier,
                MineOutputPercent = MineOutputPercent,
                RefineryOutputPercent = RefineryOutputPercent,
                MaintenanceCapacityPercent = MaintenanceCapacityPercent,
                ManufacturingSpeedPercent = ManufacturingSpeedPercent,
            };
        }
    }
}
