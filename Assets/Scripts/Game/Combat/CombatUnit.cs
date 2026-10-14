using System;
using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Describes a fighter's carrier deployment phase independently of its rendered model.
    /// </summary>
    public enum FighterFlightState
    {
        Deployed,
        Docked,
        Queued,
        Launching,
        Returning,
        Docking,
    }

    /// <summary>
    /// Stores a three-dimensional value in battle coordinates.
    /// </summary>
    [PersistableObject]
    public sealed class BattleVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    /// <summary>
    /// An independent copy of one strategic unit participating in an active battle.
    /// </summary>
    [PersistableObject]
    public sealed class CombatUnit : BaseGameEntity
    {
        [PersistableMember(Name = "Unit")]
        [PersistableInclude(typeof(CapitalShip))]
        [PersistableInclude(typeof(Starfighter))]
        private BaseSceneNode _unit;

        public string SourceUnitInstanceID { get; set; }
        public bool HasRetreated { get; set; }

        /// <summary>
        /// Gets or sets remaining battle shields, or null when no shield state was recorded.
        /// </summary>
        public int? CurrentShieldStrength { get; set; }
        public BattleVector3 Position { get; set; } = new BattleVector3();
        public BattleVector3 Forward { get; set; } = new BattleVector3 { Z = 1f };

        /// <summary>
        /// Gets or sets the pending move destination in battle coordinates, or null when stopped.
        /// </summary>
        public BattleVector3 MoveDestination { get; set; }

        /// <summary>
        /// Gets or sets the strategic identity of the fighter's assigned carrier.
        /// </summary>
        public string CarrierSourceUnitInstanceID { get; set; }
        public FighterFlightState FlightState { get; set; }
        public int FlightBayIndex { get; set; }
        public int FlightWaypointIndex { get; set; }
        public float FlightSegmentDistance { get; set; }
        public float FlightDelay { get; set; }
        public bool RecallRequested { get; set; }
        public BattleVector3 LaunchFormationOffset { get; set; }

        /// <summary>
        /// Gets the carrier's ordered squadron launch requests, preserved across scene reloads.
        /// </summary>
        public List<string> LaunchQueue { get; set; } = new List<string>();
        public float LaunchCountdown { get; set; }

        /// <summary>
        /// Returns the independently mutable battle copy of the strategic unit.
        /// </summary>
        /// <returns>The battle unit.</returns>
        public BaseSceneNode GetUnit()
        {
            return _unit;
        }

        /// <summary>
        /// Creates an independently mutable battle copy of a strategic ship.
        /// </summary>
        /// <param name="sourceUnit">The strategic unit entering battle.</param>
        /// <returns>The battle unit.</returns>
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
            return new CombatUnit
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
        }
    }
}
