using System;
using Rebellion.Game.Units;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Identifies a weapon type used by the tactical simulation.
    /// </summary>
    public enum TacticalWeaponType
    {
        /// <summary>Heavy anti-ship energy weapons.</summary>
        Turbolaser,

        /// <summary>Ion weapons that primarily attack shields and systems.</summary>
        IonCannon,

        /// <summary>Rapid energy weapons used by ships and fighters.</summary>
        LaserCannon,

        /// <summary>Fighter-launched proton torpedoes.</summary>
        Torpedo,
    }

    /// <summary>
    /// Describes one weapon-family attack resolved by the tactical simulation.
    /// </summary>
    public readonly struct TacticalAttack
    {
        /// <summary>
        /// Gets the weapon family that produced the attack.
        /// </summary>
        public TacticalWeaponType WeaponType { get; }

        /// <summary>
        /// Gets the attack strength.
        /// </summary>
        public int Strength { get; }

        /// <summary>
        /// Initializes a tactical attack.
        /// </summary>
        /// <param name="weaponType">The weapon family that produced the attack.</param>
        /// <param name="strength">The nonnegative attack strength.</param>
        public TacticalAttack(TacticalWeaponType weaponType, int strength)
        {
            if (strength < 0)
                throw new ArgumentOutOfRangeException(nameof(strength));

            WeaponType = weaponType;
            Strength = strength;
        }
    }

    /// <summary>
    /// Describes one capital ship weapon type across its four firing arcs.
    /// </summary>
    public sealed class TacticalWeaponBattery
    {
        private const int _foreIndex = 0;
        private const int _aftIndex = 1;
        private const int _portIndex = 2;
        private const int _starboardIndex = 3;
        private const int _rangeIndex = 4;
        private readonly int[] arcCounts;

        /// <summary>
        /// Gets the weapon type represented by this battery.
        /// </summary>
        public TacticalWeaponType WeaponType { get; }

        /// <summary>
        /// Gets the weapon range.
        /// </summary>
        public int Range { get; }

        /// <summary>
        /// Initializes one weapon family across the four tactical firing arcs.
        /// </summary>
        /// <param name="weaponType">The tactical weapon family.</param>
        /// <param name="fore">The forward-arc weapon count.</param>
        /// <param name="aft">The aft-arc weapon count.</param>
        /// <param name="port">The port-arc weapon count.</param>
        /// <param name="starboard">The starboard-arc weapon count.</param>
        /// <param name="range">The weapon range.</param>
        private TacticalWeaponBattery(
            TacticalWeaponType weaponType,
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
                (TacticalWeaponType)weaponType,
                values[_foreIndex],
                values[_aftIndex],
                values[_portIndex],
                values[_starboardIndex],
                values[_rangeIndex]
            );
        }

        /// <summary>
        /// Creates a forward-firing tactical battery for a fighter weapon.
        /// </summary>
        /// <param name="weaponType">The tactical weapon type.</param>
        /// <param name="strength">The weapon strength.</param>
        /// <param name="range">The weapon range.</param>
        /// <returns>The initialized tactical battery.</returns>
        public static TacticalWeaponBattery CreateFighter(
            TacticalWeaponType weaponType,
            int strength,
            int range
        )
        {
            return new TacticalWeaponBattery(weaponType, strength, 0, 0, 0, range);
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
                TacticalWeaponArc.Fore => arcCounts[_foreIndex],
                TacticalWeaponArc.Aft => arcCounts[_aftIndex],
                TacticalWeaponArc.Port => arcCounts[_portIndex],
                TacticalWeaponArc.Starboard => arcCounts[_starboardIndex],
                _ => throw new ArgumentOutOfRangeException(nameof(arc)),
            };
        }
    }
}
