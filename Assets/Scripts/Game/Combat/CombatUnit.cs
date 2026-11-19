using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    public enum FighterFlightState
    {
        Deployed,
        Docked,
        Queued,
        Launching,
        Returning,
        Docking,
    }

    [PersistableObject]
    public sealed class BattleVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    /// <summary>
    /// Owns the independently mutable copy of one strategic battle participant.
    /// </summary>
    [PersistableObject]
    public sealed class CombatUnit : BaseGameEntity
    {
        [PersistableMember(Name = "Unit")]
        [PersistableInclude(typeof(CapitalShip))]
        [PersistableInclude(typeof(Starfighter))]
        private BaseSceneNode _unit;

        public string SourceUnitInstanceID { get; set; }
        public string CarrierSourceUnitInstanceID { get; set; }
        public bool HasRetreated { get; set; }
        public bool HasPlanetaryProtection { get; set; }
        public int PayloadCount { get; set; }
        public int FighterCapacity { get; set; }
        public int? CurrentShieldStrength { get; set; }
        public BattleVector3 Position { get; set; } = new BattleVector3();
        public BattleVector3 Forward { get; set; } = new BattleVector3 { Z = 1f };
        public BattleVector3 MoveDestination { get; set; }
        public FighterFlightState FlightState { get; set; }
        public int FlightBayIndex { get; set; }
        public int FlightWaypointIndex { get; set; }
        public float FlightSegmentDistance { get; set; }
        public float FlightDelay { get; set; }
        public bool RecallRequested { get; set; }
        public BattleVector3 LaunchFormationOffset { get; set; }
        public List<string> LaunchQueue { get; set; } = new List<string>();
        public float LaunchCountdown { get; set; }

        public BaseSceneNode GetUnit() => _unit;

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
                FlightState =
                    sourceUnit is Starfighter && sourceUnit.ParentNode is CapitalShip
                        ? FighterFlightState.Docked
                        : FighterFlightState.Deployed,
                CurrentShieldStrength = battleCopy is CapitalShip capitalShip
                    ? capitalShip.MaxShieldStrength
                    : ((Starfighter)battleCopy).ShieldStrength,
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

        public void ApplyCapitalShipState(
            int hull,
            int shields,
            CapitalShipSystemDamage systemDamage
        )
        {
            if (_unit is not CapitalShip ship)
                throw new InvalidOperationException("The battle unit is not a capital ship.");
            ship.CurrentHullStrength = hull;
            ship.SystemDamage = systemDamage?.CreateCopy() ?? new CapitalShipSystemDamage();
            CurrentShieldStrength = shields;
        }

        public void ApplyStarfighterState(int squadronSize, string carrierSourceUnitInstanceID)
        {
            if (_unit is not Starfighter fighter)
                throw new InvalidOperationException("The battle unit is not a starfighter.");
            fighter.CurrentSquadronSize = squadronSize;
            CarrierSourceUnitInstanceID = carrierSourceUnitInstanceID;
        }
    }
}
