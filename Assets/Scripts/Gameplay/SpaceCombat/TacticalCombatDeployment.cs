namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures a capital ship's installed-weapon class and supported movement ordering at battle entry.
    /// </summary>
    internal readonly struct TacticalCombatDeployment
    {
        public TacticalCombatUnit Unit { get; }
        public int Category { get; }
        public float Score { get; }

        /// <summary>
        /// Captures the immutable deployment assessment used by initial placement and automatic regrouping.
        /// </summary>
        /// <param name="unit">The capital ship being deployed.</param>
        public TacticalCombatDeployment(TacticalCombatUnit unit)
        {
            Unit = unit;
            float movement = unit.GetSpeed();
            bool armed = false;
            bool laser = false;
            float maximum = 0;
            for (int arc = 0; arc < 4; arc++)
            for (int profile = 0; profile < 3; profile++)
            {
                TacticalWeaponType weapon = profile switch
                {
                    0 => TacticalWeaponType.IonCannon,
                    1 => TacticalWeaponType.LaserCannon,
                    _ => TacticalWeaponType.Turbolaser,
                };
                if (unit.Weapons.GetStrength((TacticalWeaponArc)arc, weapon) == 0)
                    continue;
                armed = true;
                laser |= weapon == TacticalWeaponType.LaserCannon;
                float range = unit.Weapons.GetRange(weapon);
                if (maximum < range)
                    maximum = range;
            }
            Category =
                laser ? 1
                : armed ? 0
                : 2;
            Score = -(float)((double)movement - (double)maximum * -10000f);
        }

        /// <summary>
        /// Orders captured deployment scores and resolves equal scores by unsigned battle registration.
        /// </summary>
        /// <param name="first">The first capital deployment.</param>
        /// <param name="second">The second capital deployment.</param>
        /// <returns>The deployment ordering.</returns>
        public static int Compare(TacticalCombatDeployment first, TacticalCombatDeployment second)
        {
            if (first.Score < second.Score)
                return -1;
            if (first.Score > second.Score)
                return 1;
            return ((uint)first.Unit.Index).CompareTo((uint)second.Unit.Index);
        }
    }
}
