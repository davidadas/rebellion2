using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Holds mutable tactical state for one strategic combat unit.
    /// </summary>
    public sealed class TacticalUnitState
    {
        private readonly ReadOnlyCollection<TacticalWeaponBattery> weaponBatteries;

        /// <summary>
        /// Gets the strategic unit represented by this tactical unit.
        /// </summary>
        public IGameEntity Unit { get; }

        /// <summary>
        /// Gets the tactical side controlling this unit.
        /// </summary>
        public TacticalBattleSide Side { get; }

        /// <summary>
        /// Gets the simulation behavior used by this unit.
        /// </summary>
        public TacticalUnitKind Kind { get; }

        /// <summary>
        /// Gets the unit's hull strength when the battle began.
        /// </summary>
        public int InitialHull { get; }

        /// <summary>
        /// Gets or sets the unit's current tactical hull strength.
        /// </summary>
        public int Hull { get; set; }

        /// <summary>
        /// Gets the unit's shield strength when the battle began.
        /// </summary>
        public int InitialShields { get; }

        /// <summary>
        /// Gets or sets the unit's current tactical shield strength.
        /// </summary>
        public int Shields { get; set; }

        /// <summary>
        /// Gets whether the unit can continue participating in combat.
        /// </summary>
        public bool IsActive => Hull > 0;

        /// <summary>
        /// Gets the capital ship's tactical weapon batteries.
        /// </summary>
        public IReadOnlyList<TacticalWeaponBattery> WeaponBatteries => weaponBatteries;

        private TacticalUnitState(
            IGameEntity unit,
            TacticalBattleSide side,
            TacticalUnitKind kind,
            int hull,
            int shields,
            IList<TacticalWeaponBattery> weaponBatteries
        )
        {
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
            Side = side;
            Kind = kind;
            InitialHull = Math.Max(0, hull);
            Hull = InitialHull;
            InitialShields = Math.Max(0, shields);
            Shields = InitialShields;
            this.weaponBatteries = new ReadOnlyCollection<TacticalWeaponBattery>(
                weaponBatteries ?? Array.Empty<TacticalWeaponBattery>()
            );
        }

        /// <summary>
        /// Creates tactical state for a capital ship.
        /// </summary>
        /// <param name="ship">The strategic capital ship.</param>
        /// <param name="side">The side controlling the ship.</param>
        /// <returns>The initialized tactical state.</returns>
        public static TacticalUnitState FromCapitalShip(CapitalShip ship, TacticalBattleSide side)
        {
            if (ship == null)
                throw new ArgumentNullException(nameof(ship));

            return new TacticalUnitState(
                ship,
                side,
                TacticalUnitKind.CapitalShip,
                ship.CurrentHullStrength,
                ship.MaxShieldStrength,
                ship.PrimaryWeapons.OrderBy(entry => entry.Key)
                    .Select(entry => TacticalWeaponBattery.Create(entry.Key, entry.Value))
                    .ToList()
            );
        }

        /// <summary>
        /// Creates tactical state for a fighter squadron.
        /// </summary>
        /// <param name="fighters">The strategic fighter squadron.</param>
        /// <param name="side">The side controlling the squadron.</param>
        /// <returns>The initialized tactical state.</returns>
        public static TacticalUnitState FromFighters(Starfighter fighters, TacticalBattleSide side)
        {
            if (fighters == null)
                throw new ArgumentNullException(nameof(fighters));

            return new TacticalUnitState(
                fighters,
                side,
                TacticalUnitKind.Fighters,
                fighters.CurrentSquadronSize,
                fighters.CurrentSquadronSize * fighters.ShieldStrength,
                null
            );
        }
    }
}
