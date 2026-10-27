using System;
using System.Collections.Generic;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Identifies the combat capabilities represented by a battle member.
    /// </summary>
    public enum CombatUnitKind
    {
        None,
        CapitalShip,
        Starfighter,
    }

    /// <summary>
    /// Captures combat-relevant configuration independently of strategic production and travel state.
    /// </summary>
    [PersistableObject]
    public sealed class CombatUnitData
    {
        public CombatUnitKind Kind { get; set; }
        public string TypeID { get; set; }
        public string OwnerInstanceID { get; set; }
        public string DisplayName { get; set; }
        public string DisplayImagePath { get; set; }
        public string ModelPath { get; set; }
        public float ModelScale { get; set; } = 1f;
        public int MaxHullStrength { get; set; }
        public int MaxShieldStrength { get; set; }
        public int ShieldRechargeRate { get; set; }
        public int SublightSpeed { get; set; }
        public int Hyperdrive { get; set; }
        public int StarfighterCapacity { get; set; }
        public int MaxSquadronSize { get; set; }

        /// <summary>
        /// Gets or sets geometry measured from the loaded model and rebuilt when a battle is loaded.
        /// </summary>
        [PersistableIgnore]
        public ModelDimensions ModelSize { get; set; }

        [PersistableMember(Name = "Components")]
        private List<ShipComponent> _components = new List<ShipComponent>();

        [PersistableMember(Name = "HardpointGroups")]
        private List<HardpointGroup> _hardpointGroups = new List<HardpointGroup>();

        /// <summary>
        /// Captures one ship's effective combat values and independently owned installed components.
        /// </summary>
        /// <param name="source">The strategic ship entering battle.</param>
        /// <returns>The ship's combat configuration.</returns>
        public static CombatUnitData Create(BaseSceneNode source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source is not CapitalShip && source is not Starfighter)
                throw new ArgumentException(
                    "Only capital ships and starfighters can enter a space battle.",
                    nameof(source)
                );
            CapitalShip ship = source as CapitalShip;
            Starfighter fighter = source as Starfighter;
            return new CombatUnitData
            {
                Kind = ship != null ? CombatUnitKind.CapitalShip : CombatUnitKind.Starfighter,
                TypeID = source.TypeID,
                OwnerInstanceID = source.OwnerInstanceID,
                DisplayName = source.DisplayName,
                DisplayImagePath = source.DisplayImagePath,
                ModelPath = source.ModelPath,
                ModelScale = source.ModelScale,
                ModelSize = source.ModelSize?.CreateCopy(),
                MaxHullStrength = ship != null ? ship.MaxHullStrength : fighter.MaxHullStrength,
                MaxShieldStrength = ship != null ? ship.MaxShieldStrength : fighter.ShieldStrength,
                ShieldRechargeRate = ship != null ? ship.ShieldRechargeRate : 0,
                SublightSpeed = ship != null ? ship.SublightSpeed : fighter.SublightSpeed,
                Hyperdrive = ship != null ? ship.Hyperdrive : fighter.Hyperdrive,
                StarfighterCapacity = ship != null ? ship.StarfighterCapacity : 0,
                MaxSquadronSize = fighter != null ? fighter.MaxSquadronSize : 0,
                _components = (
                    ship != null ? ship.GetComponents() : fighter.GetComponents()
                ).ConvertAll(component => component?.CreateCopy()),
                _hardpointGroups = (
                    ship != null ? ship.GetHardpointGroups() : fighter.GetHardpointGroups()
                ).ConvertAll(group => group?.CreateCopy()),
            };
        }

        /// <summary>
        /// Returns the non-negative configured sublight speed.
        /// </summary>
        /// <returns>The ship's sublight speed.</returns>
        public int GetSublightSpeed() => Math.Max(0, SublightSpeed);

        /// <summary>
        /// Returns the configured carrier capacity.
        /// </summary>
        /// <returns>The maximum number of carried squadrons.</returns>
        public int GetStarfighterCapacity() => StarfighterCapacity;

        /// <summary>
        /// Returns independently mutable battle components.
        /// </summary>
        /// <returns>The installed components.</returns>
        public List<ShipComponent> GetComponents() => _components;

        /// <summary>
        /// Returns independently owned battle weapon mounts.
        /// </summary>
        /// <returns>The installed hardpoint groups.</returns>
        public List<HardpointGroup> GetHardpointGroups() => _hardpointGroups;

        /// <summary>
        /// Returns the display image captured for the entering unit.
        /// </summary>
        /// <returns>The unit's display image path.</returns>
        public string GetDisplayImagePath() => DisplayImagePath;
    }
}
