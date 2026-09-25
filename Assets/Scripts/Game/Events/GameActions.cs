using System.Collections.Generic;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    #region RandomActions
    /// <summary>
    /// Defines an inclusive integer roll that may supply an action value or event binding.
    /// </summary>
    [PersistableObject(Name = "RollInteger")]
    public sealed class RollInteger
    {
        // Range.
        [PersistableAttribute]
        public int Minimum { get; set; }

        [PersistableAttribute]
        public int Maximum { get; set; }
    }

    /// <summary>
    /// Defines a double roll whose minimum is inclusive and maximum is exclusive.
    /// </summary>
    [PersistableObject(Name = "RollDouble")]
    public sealed class RollDouble
    {
        // Range.
        [PersistableAttribute]
        public double Minimum { get; set; }

        [PersistableAttribute]
        public double Maximum { get; set; }
    }

    /// <summary>
    /// Defines one conditionally eligible weighted outcome.
    /// </summary>
    [PersistableObject(Name = "Outcome")]
    public sealed class RandomOutcome
    {
        [PersistableAttribute]
        public int Weight { get; set; } = 1;

        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();

        public List<GameAction> Actions { get; set; } = new List<GameAction>();
    }

    /// <summary>
    /// Selects and executes exactly one eligible outcome using relative authored weights.
    /// </summary>
    [PersistableObject(Name = "RollOutcome")]
    public sealed class RollOutcomeAction : GameAction
    {
        public List<RandomOutcome> Outcomes { get; set; } = new List<RandomOutcome>();
    }

    /// <summary>
    /// Executes authored actions when one normalized probability roll succeeds.
    /// </summary>
    [PersistableObject(Name = "RollChance")]
    public sealed class RollChanceAction : GameAction
    {
        // Probability.
        [PersistableAttribute]
        public double? Probability { get; set; }

        [PersistableAttribute]
        public string ProbabilityBinding { get; set; }

        public RollDouble RollDouble { get; set; }

        public List<GameAction> Actions { get; set; } = new List<GameAction>();
    }

    #endregion

    #region CompositeActions
    [PersistableObject(Name = "If")]
    public sealed class IfAction : GameAction
    {
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
        public List<GameAction> Else { get; set; } = new List<GameAction>();
    }
    #endregion

    #region EventStateActions
    public enum EventVariableOperation
    {
        Set,
        Add,
        Minimum,
        Maximum,
    }

    [PersistableObject(Name = "SetEventVariable")]
    public sealed class SetEventVariableAction : GameAction
    {
        public string Key { get; set; }
        public EventVariableOperation Operation { get; set; }
        public int? Operand { get; set; }

        public string OperandBinding { get; set; }

        public RollInteger RollInteger { get; set; }
    }
    #endregion

    #region FogOfWarActions
    /// <summary>
    /// Supplies current observations about selected objects to one faction.
    /// </summary>
    [PersistableObject(Name = "RevealToFaction")]
    public sealed class RevealToFactionAction : GameAction
    {
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Targets { get; set; } = new List<GameEventSelector>();
    }
    #endregion

    #region MessageActions
    /// <summary>
    /// Emits a normal faction message from presentation data authored with a game event.
    /// </summary>
    [PersistableObject(Name = "SendMessage")]
    public sealed class SendMessageAction : GameAction
    {
        [PersistableAttribute]
        public string RecipientFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string SubjectInstanceID { get; set; }

        [PersistableAttribute]
        public string SubjectBinding { get; set; }

        [PersistableAttribute]
        public string RelatedSubjectInstanceID { get; set; }

        [PersistableAttribute]
        public bool ShowSubjectImage { get; set; }

        [PersistableAttribute]
        public string LocationInstanceID { get; set; }

        [PersistableAttribute]
        public string LocationBinding { get; set; }

        [PersistableAttribute(Name = "Type")]
        public MessageType MessageType { get; set; } = MessageType.Advice;
        public string Subject { get; set; }
        public string Body { get; set; }
        public MessageBackgroundImage BackgroundImage { get; set; }
        public MessageImage OverlayImage { get; set; }
        public MessageAudio BackgroundAudio { get; set; }
        public MessageOfficerVoice OfficerVoice { get; set; }
        public AdvisorNotification AdvisorNotification { get; set; }
    }

    #endregion

    #region OfficerActions
    /// <summary>
    /// Sets one officer's captivity state and emits the standard state-change result.
    /// </summary>
    [PersistableObject(Name = "SetCaptureStatus")]
    public sealed class SetCaptureStatusAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public bool IsCaptured { get; set; }

        [PersistableAttribute]
        public string CaptorFactionInstanceID { get; set; }

        [PersistableAttribute]
        public bool CanEscape { get; set; } = true;

        [PersistableMember(Name = "Officers")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Adjusts selected officers using one authored calculation.
    /// </summary>
    [PersistableObject(Name = "ChangeOfficerRating")]
    public sealed class ChangeOfficerRatingAction : GameAction
    {
        // Officer Targets.
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        // Rating.
        [PersistableAttribute]
        public SkillRating Rating { get; set; }

        // Adjustment.
        public int? Amount { get; set; }
        public string AmountBinding { get; set; }
        public RollInteger RollInteger { get; set; }
        public int? PercentOfStored { get; set; }
        public int? PercentOfEffective { get; set; }
        public int? PercentOfPositiveGap { get; set; }

        [PersistableAttribute]
        public string ReferenceOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public int MinimumAmount { get; set; }

        // Additional Officer Targets.
        [PersistableMember(Name = "Officers")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Increases selected officers' stored Force progression using one authored calculation.
    /// </summary>
    [PersistableObject(Name = "IncreaseForceRank")]
    public sealed class IncreaseForceRankAction : GameAction
    {
        // Officer Targets.
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        // Adjustment.
        public int? Amount { get; set; }
        public string AmountBinding { get; set; }
        public RollInteger RollInteger { get; set; }
        public int? PercentOfStored { get; set; }
        public int? PercentOfEffective { get; set; }
        public int? PercentOfPositiveGap { get; set; }

        [PersistableAttribute]
        public string ReferenceOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public int MinimumAmount { get; set; }

        // Additional Officer Targets.
        [PersistableMember(Name = "Officers")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Resolves one officer's effective rating through an authored probability table.
    /// </summary>
    [PersistableObject(Name = "PerformSkillCheck")]
    public sealed class PerformSkillCheckAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public SkillRating Rating { get; set; }

        [PersistableAttribute]
        public string ProbabilityTable { get; set; }

        [PersistableAttribute]
        public int RatingMultiplier { get; set; } = 1;

        public List<GameAction> OnSuccess { get; set; } = new List<GameAction>();
        public List<GameAction> OnFailure { get; set; } = new List<GameAction>();
    }

    /// <summary>
    /// Marks one officer as having latent Force potential.
    /// </summary>
    [PersistableObject(Name = "SetForceSensitive")]
    public sealed class SetForceSensitiveAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }
    }

    /// <summary>
    /// Reveals one Force-sensitive officer's potential and initializes usable Force progression.
    /// </summary>
    [PersistableObject(Name = "SetForceEligible")]
    public sealed class SetForceEligibleAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }
    }

    /// <summary>
    /// Applies a data-authored inclusive random injury range to one officer.
    /// </summary>
    [PersistableObject(Name = "ApplyOfficerInjury")]
    public sealed class ApplyOfficerInjuryAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }
        public int MinimumInjury { get; set; }
        public int MaximumInjury { get; set; }
    }

    /// <summary>
    /// Replaces the authored image paths used for an officer.
    /// </summary>
    [PersistableObject(Name = "SetOfficerImages")]
    public sealed class SetOfficerImagesAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }
        public string DisplayImagePath { get; set; }
        public string SmallDisplayImagePath { get; set; }
        public string MessageImagePath { get; set; }
        public string EncyclopediaImagePath { get; set; }
    }

    /// <summary>
    /// Replaces selected officer voice-line collections with authored asset paths.
    /// </summary>
    [PersistableObject(Name = "SetOfficerVoiceSet")]
    public sealed class SetOfficerVoiceSetAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableCollectionItem(Name = "Path")]
        public List<string> Order { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> PersonnelArrived { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionSuccess { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionFailure { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionAbort { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> Released { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> Recovered { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> EnemyDetected { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> ForceGrowth { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> ForceUserDiscovered { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> TraitorDiscovered { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> RescueAttempt { get; set; } = new List<string>();
    }

    /// <summary>
    /// Requests resolution of a duel between two officers.
    /// </summary>
    [PersistableObject(Name = "TriggerDuel")]
    public sealed class TriggerDuelAction : GameAction
    {
        [PersistableAttribute]
        public string FirstOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string SecondOfficerInstanceID { get; set; }

        public string ImagePath { get; set; }
        public string AudioPath { get; set; }
    }
    #endregion

    #region PresentationActions

    /// <summary>
    /// Replaces the display name of every explicitly named or selected entity.
    /// </summary>
    [PersistableObject(Name = "SetDisplayName")]
    public sealed class SetDisplayNameAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableAttribute]
        public string Name { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Replaces the optional status text of every explicitly named or selected entity.
    /// </summary>
    [PersistableObject(Name = "SetDisplayStatus")]
    public sealed class SetDisplayStatusAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableAttribute]
        public string Status { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Clears the optional status text of every explicitly named or selected entity.
    /// </summary>
    [PersistableObject(Name = "ClearDisplayStatus")]
    public sealed class ClearDisplayStatusAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }
    #endregion

    #region PlanetActions
    /// <summary>
    /// Defines shared targeting and adjustment data for one concrete planet value.
    /// </summary>
    public abstract class ChangePlanetValueAction : GameAction
    {
        // Planet Targets.
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        // Adjustment.
        public int? Amount { get; set; }
        public string AmountBinding { get; set; }
        public RollInteger RollInteger { get; set; }
        public int? PercentOfCurrent { get; set; }

        // Additional Planet Targets.
        [PersistableMember(Name = "Planets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Changes the number of raw-resource nodes available on selected planets.
    /// </summary>
    [PersistableObject(Name = "ChangeRawResourceNodes")]
    public sealed class ChangeRawResourceNodesAction : ChangePlanetValueAction { }

    /// <summary>
    /// Changes the energy capacity available on selected planets.
    /// </summary>
    [PersistableObject(Name = "ChangeEnergyCapacity")]
    public sealed class ChangeEnergyCapacityAction : ChangePlanetValueAction { }

    /// <summary>
    /// Changes one faction's popular support on selected planets by a signed amount.
    /// </summary>
    [PersistableObject(Name = "ChangePopularSupport")]
    public sealed class ChangePopularSupportAction : GameAction
    {
        // Faction.
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        // Planet Targets.
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        // Adjustment.
        public int? Amount { get; set; }
        public string AmountBinding { get; set; }
        public RollInteger RollInteger { get; set; }
        public int? PercentOfCurrent { get; set; }

        // Additional Planet Targets.
        [PersistableMember(Name = "Planets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Sets one faction's popular support on selected planets to an absolute value.
    /// </summary>
    [PersistableObject(Name = "SetPopularSupport")]
    public sealed class SetPopularSupportAction : GameAction
    {
        // Faction.
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        // Planet Targets.
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        // Support Value.
        public int? Support { get; set; }
        public string SupportBinding { get; set; }
        public RollInteger RollInteger { get; set; }

        // Additional Planet Targets.
        [PersistableMember(Name = "Planets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Reduces raw-resource nodes and energy capacity using independent per-point rolls.
    /// </summary>
    [PersistableObject(Name = "DamagePlanetResources")]
    public sealed class DamagePlanetResourcesAction : GameAction
    {
        // Planet Target.
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        // Damage Probability.
        [PersistableAttribute(Name = "LossProbabilityPerResource")]
        public double? LossProbabilityPerResource { get; set; }

        [PersistableAttribute]
        public string ProbabilityBinding { get; set; }

        public RollDouble RollDouble { get; set; }

        [PersistableAttribute(Name = "MinimumTotalLoss")]
        public int MinimumTotalLoss { get; set; } = 1;
    }

    #endregion

    #region UnitActions
    [PersistableObject(Name = "DestroyUnits")]
    public sealed class DestroyUnitsAction : GameAction
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        [PersistableMember(Name = "Units")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// A data-defined request to change ownership of selected planets or units.
    /// </summary>
    [PersistableObject(Name = "ChangeOwner")]
    public sealed class ChangeOwnerAction : GameAction
    {
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        public List<GameEventSelector> Planets { get; set; } = new List<GameEventSelector>();

        public List<GameEventSelector> Units { get; set; } = new List<GameEventSelector>();
    }

    public abstract class UnitTransferAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string DestinationInstanceID { get; set; }

        public List<GameEventSelector> Units { get; set; } = new List<GameEventSelector>();

        public List<GameEventSelector> Destination { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Places one or more units at a destination without transit time.
    /// </summary>
    [PersistableObject(Name = "PlaceUnits")]
    public sealed class PlaceUnitsAction : UnitTransferAction { }

    /// <summary>
    /// Supplies newly instantiated units from one registered content definition.
    /// </summary>
    [PersistableObject]
    public sealed class SpawnUnits : GameEventSelector
    {
        [PersistableAttribute]
        public string TypeID { get; set; }

        [PersistableAttribute]
        public int Count { get; set; } = 1;

        [PersistableAttribute]
        public string OwnerFactionInstanceID { get; set; }
    }

    /// <summary>
    /// Sends one or more units through normal movement and transit.
    /// </summary>
    [PersistableObject(Name = "SendUnits")]
    public sealed class SendUnitsAction : UnitTransferAction { }

    /// <summary>
    /// Defines whether selected scene nodes participate in active gameplay.
    /// </summary>
    public enum SceneNodeState
    {
        Active,

        Inactive,
    }

    /// <summary>
    /// Sets the gameplay state of one or more retained scene nodes.
    /// </summary>
    [PersistableObject(Name = "SetNodeState")]
    public sealed class SetNodeStateAction : GameAction
    {
        [PersistableAttribute]
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public SceneNodeState State { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }
    #endregion
}
