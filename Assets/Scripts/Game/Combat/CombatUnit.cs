using System;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Owns the independently mutable copy of one strategic battle participant.
    /// </summary>
    [PersistableObject]
    public sealed class CombatUnit : BaseGameEntity
    {
        [PersistableMember(Name = nameof(Unit))]
        [PersistableInclude(typeof(CapitalShip))]
        [PersistableInclude(typeof(Starfighter))]
        private BaseSceneNode _unit;

        public string SourceUnitInstanceID { get; set; }
        public string CarrierSourceUnitInstanceID { get; set; }
        public bool HasPlanetaryProtection { get; set; }
        public int PayloadCount { get; set; }
        public int FighterCapacity { get; set; }

        [PersistableIgnore]
        public BaseSceneNode Unit => _unit;

        /// <summary>
        /// Captures the campaign unit state used for the duration of one battle.
        /// </summary>
        /// <param name="sourceUnit">The strategic capital ship or starfighter.</param>
        /// <returns>An independently mutable combat copy.</returns>
        public static CombatUnit Create(BaseSceneNode sourceUnit)
        {
            if (sourceUnit == null)
                throw new ArgumentNullException(nameof(sourceUnit));
            if (sourceUnit is not CapitalShip && sourceUnit is not Starfighter)
                throw new ArgumentException(
                    "Only capital ships and starfighters can enter a space battle.",
                    nameof(sourceUnit)
                );

            BaseSceneNode battleCopy = (BaseSceneNode)sourceUnit.CreateCopy();
            CombatUnit combatUnit = new CombatUnit
            {
                SourceUnitInstanceID = sourceUnit.InstanceID,
                CarrierSourceUnitInstanceID =
                    sourceUnit is Starfighter
                        ? (sourceUnit.ParentNode as CapitalShip)?.InstanceID
                        : null,
                _unit = battleCopy,
            };

            if (sourceUnit is CapitalShip ship)
            {
                combatUnit.PayloadCount =
                    ship.GetChildren<Officer>().Count
                    + ship.GetChildren<Regiment>().Count
                    + ship.GetChildren<SpecialForces>().Count;
                int fighterCount = ship.GetChildren<Starfighter>(includeDisabled: true).Count;
                int activeFighterCount = ship.GetChildren<Starfighter>()
                    .Count(fighter =>
                        fighter.ManufacturingStatus == ManufacturingStatus.Complete
                        && fighter.Movement == null
                        && fighter.CurrentSquadronSize > 0
                    );
                combatUnit.FighterCapacity =
                    ship.StarfighterCapacity - fighterCount + activeFighterCount;
            }

            return combatUnit;
        }

        /// <summary>
        /// Replaces the battle copy's mutable capital-ship result state.
        /// </summary>
        /// <param name="hull">The surviving hull strength.</param>
        /// <param name="systemDamage">The surviving component damage.</param>
        public void ApplyCapitalShipState(int hull, CapitalShipSystemDamage systemDamage)
        {
            if (_unit is not CapitalShip ship)
                throw new InvalidOperationException("The battle unit is not a capital ship.");
            ship.CurrentHullStrength = hull;
            ship.SystemDamage = systemDamage?.CreateCopy() ?? new CapitalShipSystemDamage();
        }

        /// <summary>
        /// Replaces the battle copy's mutable starfighter result state.
        /// </summary>
        /// <param name="squadronSize">The surviving fighter count.</param>
        /// <param name="carrierSourceUnitInstanceID">The campaign carrier identity after recovery.</param>
        public void ApplyStarfighterState(int squadronSize, string carrierSourceUnitInstanceID)
        {
            if (_unit is not Starfighter fighter)
                throw new InvalidOperationException("The battle unit is not a starfighter.");
            fighter.CurrentSquadronSize = squadronSize;
            CarrierSourceUnitInstanceID = carrierSourceUnitInstanceID;
        }
    }
}
