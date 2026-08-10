using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Matches the two officers in the duel that triggered an event.
    /// </summary>
    [PersistableObject(Name = "DuelIncludes")]
    public class DuelIncludesConditional : GameResultConditional
    {
        [PersistableAttribute]
        public string FirstOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string SecondOfficerInstanceID { get; set; }

        /// <inheritdoc />
        protected override bool IsMatch(GameRoot game, GameResult triggerResult)
        {
            if (triggerResult is not OfficerEncounterResult encounter)
                return false;

            return (
                    encounter.EncounteredOfficer?.InstanceID == FirstOfficerInstanceID
                    && encounter.OpposingOfficer?.InstanceID == SecondOfficerInstanceID
                )
                || (
                    encounter.EncounteredOfficer?.InstanceID == SecondOfficerInstanceID
                    && encounter.OpposingOfficer?.InstanceID == FirstOfficerInstanceID
                );
        }
    }

    /// <summary>
    /// Matches an authored unit contained by the arrival that triggered an event.
    /// </summary>
    [PersistableObject(Name = "UnitArrived")]
    public sealed class UnitArrivedConditional : GameResultConditional
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string DestinationInstanceID { get; set; }

        protected override bool IsMatch(GameRoot game, GameResult triggerResult)
        {
            if (
                triggerResult is not UnitArrivedResult arrival
                || arrival.Unit is not ISceneNode arrivingUnit
                || (
                    !string.IsNullOrWhiteSpace(DestinationInstanceID)
                    && arrival.Destination?.InstanceID != DestinationInstanceID
                )
            )
                return false;

            ISceneNode expectedUnit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            return arrivingUnit == expectedUnit
                || arrivingUnit.GetChildren<ISceneNode>(node => node == expectedUnit).Any();
        }
    }

    /// <summary>
    /// Matches a unit that participated in the completed mission which triggered an event.
    /// </summary>
    [PersistableObject(Name = "MissionIncludes")]
    public sealed class MissionIncludesConditional : GameResultConditional
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        protected override bool IsMatch(GameRoot game, GameResult triggerResult)
        {
            return triggerResult is MissionCompletedResult completion
                && completion
                    .Participants.OfType<ISceneNode>()
                    .Any(participant => participant.InstanceID == UnitInstanceID);
        }
    }

    /// <summary>
    /// Matches an authored officer and capture state on the result that triggered an event.
    /// </summary>
    [PersistableObject(Name = "OfficerCaptured")]
    public sealed class OfficerCapturedConditional : GameResultConditional
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        /// <inheritdoc />
        protected override bool IsMatch(GameRoot game, GameResult triggerResult)
        {
            return triggerResult is OfficerCaptureStateResult capture
                && capture.TargetOfficer?.InstanceID == OfficerInstanceID
                && capture.IsCaptured;
        }
    }

    /// <summary>
    /// Matches the authored event that produced the result currently triggering an event.
    /// </summary>
    [PersistableObject(Name = "TriggeredBy")]
    public sealed class TriggeredByConditional : GameResultConditional
    {
        [PersistableAttribute]
        public string EventInstanceID { get; set; }

        /// <inheritdoc />
        protected override bool IsMatch(GameRoot game, GameResult triggerResult)
        {
            return triggerResult?.SourceEventInstanceID == EventInstanceID;
        }
    }

    /// <summary>
    /// Matches the target and outcome of a content-authored capture attempt.
    /// </summary>
    [PersistableObject(Name = "CaptureFailed")]
    public sealed class CaptureFailedConditional : GameResultConditional
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        /// <inheritdoc />
        protected override bool IsMatch(GameRoot game, GameResult triggerResult)
        {
            return triggerResult is OfficerCaptureAttemptResult capture
                && capture.Target?.InstanceID == OfficerInstanceID
                && !capture.WasCaptured;
        }
    }

    /// <summary>
    /// Matches the collector that completed a story prisoner pickup.
    /// </summary>
    [PersistableObject(Name = "PrisonerPickupCollector")]
    public sealed class PrisonerPickupCollectorConditional : GameResultConditional
    {
        public string CollectorOfficerInstanceID { get; set; }

        /// <inheritdoc />
        protected override bool IsMatch(GameRoot game, GameResult triggerResult)
        {
            return triggerResult is PrisonerPickupCompletedResult pickup
                && pickup.Collector?.InstanceID == CollectorOfficerInstanceID;
        }
    }

    /// <summary>
    /// Matches the authored outcome of the scripted final battle.
    /// </summary>
    [PersistableObject(Name = "ForceConfrontationOutcome")]
    public sealed class ForceConfrontationOutcomeConditional : GameResultConditional
    {
        public bool LukeVictorious { get; set; }

        /// <inheritdoc />
        protected override bool IsMatch(GameRoot game, GameResult triggerResult) =>
            triggerResult is ForceConfrontationCompletedResult finalBattle
            && finalBattle.LukeVictorious == LukeVictorious;
    }
}
