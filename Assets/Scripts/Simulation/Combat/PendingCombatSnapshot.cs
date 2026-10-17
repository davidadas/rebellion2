using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Carries detached, faction-visible state for one pending fleet encounter.
    /// </summary>
    public sealed class PendingCombatSnapshot
    {
        public Fleet AttackerFleet { get; set; }
        public Fleet DefenderFleet { get; set; }
        public string AttackerOwnerInstanceID { get; set; }
        public string DefenderOwnerInstanceID { get; set; }
        public Planet Planet { get; set; }
        public bool AttackerCanRetreat { get; set; }
        public bool DefenderCanRetreat { get; set; }
        public int Tick { get; set; }
    }
}
