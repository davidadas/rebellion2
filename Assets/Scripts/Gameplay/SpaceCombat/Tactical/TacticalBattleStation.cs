using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Places a planet-destroying battle station in a tactical battle: beyond its own side's edge
    /// of the battlefield, where squadrons sent against it fly to reach its trench.
    /// </summary>
    public sealed class TacticalBattleStation : BattleStationState<TacticalBattleUnit>
    {
        public CombatSide Side { get; }

        public double X { get; internal set; }

        public double Y { get; internal set; }

        public double Radius { get; internal set; }

        /// <summary>
        /// Creates tactical state for one battle station.
        /// </summary>
        /// <param name="ship">The station entering combat.</param>
        /// <param name="side">The side the station fights for.</param>
        /// <param name="isShielded">Whether a planetary shield generator protects the station.</param>
        internal TacticalBattleStation(CapitalShip ship, CombatSide side, bool isShielded)
            : base(ship, isShielded)
        {
            Side = side;
        }
    }
}
