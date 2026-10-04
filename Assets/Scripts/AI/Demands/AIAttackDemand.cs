using Rebellion.Game.Galaxy;

namespace Rebellion.AI.Demands
{
    /// <summary>
    /// Captures the force capabilities required to attack one known planet.
    /// </summary>
    public sealed class AIAttackDemand
    {
        public Planet TargetPlanet { get; }
        public int CombatStrength { get; }
        public int OrbitalStrength { get; }
        public int RegimentStrength { get; }
        public int RegimentCount { get; }
        public int OccupationRegimentCount { get; }
        public int BombardmentStrength { get; }
        public bool IsAssaultBlockedByShields { get; }

        /// <summary>
        /// Creates the measured attack demand for one known planet.
        /// </summary>
        /// <param name="targetPlanet">The known planet represented by the demand.</param>
        /// <param name="combatStrength">The system-level combat strength required to launch.</param>
        /// <param name="orbitalStrength">The orbital strength required at the planet.</param>
        /// <param name="regimentStrength">The ground attack strength required.</param>
        /// <param name="regimentCount">The regiment count required for combat and occupation.</param>
        /// <param name="occupationRegimentCount">The regiment count required after defenders are removed.</param>
        /// <param name="bombardmentStrength">The bombardment strength required to penetrate shields.</param>
        /// <param name="isAssaultBlockedByShields">Whether shields currently block a ground assault.</param>
        public AIAttackDemand(
            Planet targetPlanet,
            int combatStrength,
            int orbitalStrength,
            int regimentStrength,
            int regimentCount,
            int occupationRegimentCount,
            int bombardmentStrength,
            bool isAssaultBlockedByShields
        )
        {
            TargetPlanet = targetPlanet;
            CombatStrength = combatStrength;
            OrbitalStrength = orbitalStrength;
            RegimentStrength = regimentStrength;
            RegimentCount = regimentCount;
            OccupationRegimentCount = occupationRegimentCount;
            BombardmentStrength = bombardmentStrength;
            IsAssaultBlockedByShields = isAssaultBlockedByShields;
        }
    }
}
