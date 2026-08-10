using System;
using Rebellion.Game.Units;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Describes one capital ship weapon type across its four firing arcs.
    /// </summary>
    public sealed class TacticalWeaponBattery
    {
        private const int ForeIndex = 0;
        private const int AftIndex = 1;
        private const int PortIndex = 2;
        private const int StarboardIndex = 3;
        private const int RangeIndex = 4;
        private readonly int[] arcCounts;

        /// <summary>
        /// Gets the weapon type represented by this battery.
        /// </summary>
        public PrimaryWeaponType WeaponType { get; }

        /// <summary>
        /// Gets the weapon range.
        /// </summary>
        public int Range { get; }

        private TacticalWeaponBattery(
            PrimaryWeaponType weaponType,
            int fore,
            int aft,
            int port,
            int starboard,
            int range
        )
        {
            WeaponType = weaponType;
            arcCounts = new[]
            {
                Math.Max(0, fore),
                Math.Max(0, aft),
                Math.Max(0, port),
                Math.Max(0, starboard),
            };
            Range = Math.Max(0, range);
        }

        /// <summary>
        /// Creates a tactical battery from a capital ship weapon definition.
        /// </summary>
        /// <param name="weaponType">The primary weapon type.</param>
        /// <param name="values">The fore, aft, port, starboard, and range values.</param>
        /// <returns>The initialized tactical battery.</returns>
        public static TacticalWeaponBattery Create(PrimaryWeaponType weaponType, int[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Length != 5)
                throw new ArgumentException(
                    "Capital ship weapon definitions require four firing arcs and a range.",
                    nameof(values)
                );

            return new TacticalWeaponBattery(
                weaponType,
                values[ForeIndex],
                values[AftIndex],
                values[PortIndex],
                values[StarboardIndex],
                values[RangeIndex]
            );
        }

        /// <summary>
        /// Returns the weapon count available in one firing arc.
        /// </summary>
        /// <param name="arc">The firing arc to inspect.</param>
        /// <returns>The weapon count in the requested arc.</returns>
        public int GetCount(TacticalWeaponArc arc)
        {
            return arc switch
            {
                TacticalWeaponArc.Fore => arcCounts[ForeIndex],
                TacticalWeaponArc.Aft => arcCounts[AftIndex],
                TacticalWeaponArc.Port => arcCounts[PortIndex],
                TacticalWeaponArc.Starboard => arcCounts[StarboardIndex],
                _ => throw new ArgumentOutOfRangeException(nameof(arc)),
            };
        }
    }
}
