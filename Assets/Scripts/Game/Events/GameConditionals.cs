using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    #region CompositeConditions
    /// <summary>
    /// A <see cref="GameConditional"/> that is met when all child conditions are met.
    /// </summary>
    [PersistableObject(Name = "All")]
    public sealed class AllConditional : GameConditional
    {
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        /// <summary>
        /// Initializes a new instance of the AllConditional class.
        /// </summary>
        public AllConditional()
            : base() { }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when any child condition is met.
    /// </summary>
    [PersistableObject(Name = "Any")]
    public sealed class AnyConditional : GameConditional
    {
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        /// <summary>
        /// Initializes a new instance of the AnyConditional class.
        /// </summary>
        public AnyConditional()
            : base() { }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when none of the child conditions are met.
    /// </summary>
    [PersistableObject(Name = "Not")]
    public sealed class NotConditional : GameConditional
    {
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        /// <summary>
        /// Initializes a new instance of the NotConditional class.
        /// </summary>
        public NotConditional()
            : base() { }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when exactly one child condition is met.
    /// </summary>
    [PersistableObject(Name = "Xor")]
    public sealed class XorConditional : GameConditional
    {
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        /// <summary>
        /// Initializes a new instance of the XorConditional class.
        /// </summary>
        public XorConditional()
            : base() { }
    }
    #endregion

    #region EventStateConditions
    /// <summary>
    /// Selects the comparison applied to two authored scalar values.
    /// </summary>
    public enum ComparisonOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when the current tick count satisfies a comparison against a target value.
    /// </summary>
    [PersistableObject(Name = "TickCount")]
    public sealed class TickCountConditional : GameConditional
    {
        [PersistableAttribute]
        public ComparisonOperator Comparison { get; set; }

        [PersistableAttribute]
        public int Ticks { get; set; }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met after the specified event has activated.
    /// </summary>
    [PersistableObject(Name = "HasEventActivated")]
    public sealed class HasEventActivatedConditional : GameConditional
    {
        [PersistableAttribute]
        public string EventInstanceID { get; set; }
    }

    /// <summary>
    /// Tests whether the specified event can no longer activate.
    /// </summary>
    [PersistableObject(Name = "IsEventComplete")]
    public sealed class IsEventCompleteConditional : GameConditional
    {
        [PersistableAttribute]
        public string EventInstanceID { get; set; }
    }

    /// <summary>
    /// Compares a persistent, data-defined event variable with an authored value.
    /// </summary>
    [PersistableObject(Name = "EvaluateEventVariable")]
    public sealed class EvaluateEventVariableConditional : GameConditional
    {
        [PersistableAttribute]
        public string Key { get; set; }

        [PersistableAttribute]
        public ComparisonOperator Comparison { get; set; }

        [PersistableAttribute]
        public int CompareTo { get; set; }
    }

    /// <summary>
    /// Compares one scalar binding with an authored scalar or another scalar binding.
    /// </summary>
    [PersistableObject(Name = "EvaluateBinding")]
    public sealed class EvaluateBindingConditional : GameConditional
    {
        [PersistableAttribute]
        public string Binding { get; set; }

        [PersistableAttribute]
        public ComparisonOperator Comparison { get; set; }

        [PersistableAttribute]
        public string CompareTo { get; set; }

        [PersistableAttribute]
        public string CompareToBinding { get; set; }
    }

    /// <summary>
    /// Tests whether a bound scene-node collection contains one canonical unit.
    /// </summary>
    [PersistableObject(Name = "BindingIncludesUnit")]
    public sealed class BindingIncludesUnitConditional : GameConditional
    {
        [PersistableAttribute]
        public string Binding { get; set; }

        [PersistableAttribute]
        public string UnitInstanceID { get; set; }
    }
    #endregion

    #region OfficerConditions
    /// <summary>
    /// Selects one boolean officer state for a data-defined condition.
    /// </summary>
    public abstract class OfficerBooleanConditional : GameConditional
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }
    }

    [PersistableObject(Name = "IsCaptured")]
    public sealed class IsCapturedConditional : OfficerBooleanConditional
    {
        [PersistableAttribute]
        public string CaptorFactionInstanceID { get; set; }
    }

    [PersistableObject(Name = "IsKilled")]
    public sealed class IsKilledConditional : OfficerBooleanConditional { }

    [PersistableObject(Name = "IsInjured")]
    public sealed class IsInjuredConditional : OfficerBooleanConditional { }

    [PersistableObject(Name = "IsForceEligible")]
    public sealed class IsForceEligibleConditional : OfficerBooleanConditional { }

    /// <summary>
    /// Compares one officer's effective Force rank with an authored threshold.
    /// </summary>
    [PersistableObject(Name = "HasForceRank")]
    public sealed class HasForceRankConditional : GameConditional
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public ComparisonOperator Comparison { get; set; }

        [PersistableAttribute]
        public ForceRankLabel Rank { get; set; }
    }

    #endregion

    #region SceneConditions
    [PersistableObject(Name = "HasBuildingType")]
    public sealed class HasBuildingTypeConditional : GameConditional
    {
        [PersistableAttribute]
        public BuildingType Type { get; set; }

        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }
    }

    /// <summary>
    /// Tests whether an authored planet has any owner or one specific faction owner.
    /// </summary>
    [PersistableObject(Name = "IsOwned")]
    public sealed class IsOwnedConditional : GameConditional
    {
        [PersistableAttribute(Name = "PlanetInstanceID")]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        [PersistableAttribute(Name = "FactionInstanceID")]
        public string FactionInstanceID { get; set; }
    }

    /// <summary>
    /// Rolls against one faction's current popular support at a planet.
    /// </summary>
    [PersistableObject(Name = "RollAgainstPopularSupport")]
    public sealed class RollAgainstPopularSupportConditional : GameConditional
    {
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }
    }

    [PersistableObject(Name = "Unit")]
    public sealed class EventUnitReference
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }
    }

    public enum SceneAncestorType
    {
        Galaxy,
        PlanetSector,
        Planet,
        Fleet,
        Mission,
        CapitalShip,
    }

    [PersistableObject(Name = "ShareParent")]
    public sealed class ShareParentConditional : GameConditional
    {
        public List<EventUnitReference> Units { get; set; } = new List<EventUnitReference>();
    }

    [PersistableObject(Name = "ShareAncestor")]
    public sealed class ShareAncestorConditional : GameConditional
    {
        [PersistableAttribute]
        public SceneAncestorType Type { get; set; }

        public List<EventUnitReference> Units { get; set; } = new List<EventUnitReference>();
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when exactly two units belong to different factions.
    /// </summary>
    [PersistableObject(Name = "AreOnOpposingFactions")]
    public sealed class AreOnOpposingFactionsConditional : GameConditional
    {
        public List<string> UnitInstanceIDs { get; set; } = new List<string>();

        /// <summary>
        /// Initializes a new instance of the AreOnOpposingFactionsConditional class.
        /// </summary>
        public AreOnOpposingFactionsConditional()
            : base() { }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when the specified unit is currently assigned to a mission.
    /// </summary>
    [PersistableObject(Name = "IsOnMission")]
    public sealed class IsOnMissionConditional : GameConditional
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }
    }

    /// <summary>
    /// Tests whether a retained scene node participates in normal gameplay queries.
    /// </summary>
    [PersistableObject(Name = "IsActive")]
    public sealed class IsActiveConditional : GameConditional
    {
        [PersistableAttribute]
        public string NodeInstanceID { get; set; }
    }

    /// <summary>
    /// Tests whether a movable unit currently has an active movement state.
    /// </summary>
    [PersistableObject(Name = "IsInTransit")]
    public sealed class IsInTransitConditional : GameConditional
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }
    }

    /// <summary>
    /// Tests whether a scene node is contained by a specific location node.
    /// </summary>
    [PersistableObject(Name = "IsAtLocation")]
    public sealed class IsAtLocationConditional : GameConditional
    {
        public string UnitInstanceID { get; set; }
        public string LocationInstanceID { get; set; }
    }
    #endregion
}
