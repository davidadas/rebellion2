using System;
using Rebellion.Game.Results;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Matches one concrete simulation result and publishes its authored bindings.
    /// </summary>
    [PersistableObject]
    public abstract class GameEventTrigger
    {
        internal abstract Type ResultType { get; }

        internal abstract bool Matches(GameResult result);

        internal abstract void Bind(GameEventExecutionContext context, GameResult result);

        protected static void BindValue(
            GameEventExecutionContext context,
            string bindingName,
            object value
        )
        {
            if (!string.IsNullOrWhiteSpace(bindingName))
                context.Bind(bindingName, value);
        }
    }

    [PersistableObject(Name = "UnitArrived")]
    public sealed class UnitArrivedTrigger : GameEventTrigger
    {
        internal override Type ResultType => typeof(UnitArrivedResult);

        [PersistableAttribute]
        public string Unit { get; set; }

        [PersistableAttribute]
        public string Destination { get; set; }

        [PersistableAttribute]
        public string SourceEvent { get; set; }

        internal override bool Matches(GameResult result) => result is UnitArrivedResult;

        internal override void Bind(GameEventExecutionContext context, GameResult result)
        {
            UnitArrivedResult arrival = (UnitArrivedResult)result;
            BindValue(context, Unit, arrival.Unit);
            BindValue(context, Destination, arrival.Destination);
            BindValue(context, SourceEvent, arrival.SourceEventInstanceID);
        }
    }

    [PersistableObject(Name = "DuelCompleted")]
    public sealed class DuelCompletedTrigger : GameEventTrigger
    {
        internal override Type ResultType => typeof(DuelResult);

        [PersistableAttribute]
        public string Officer { get; set; }

        [PersistableAttribute]
        public string Opponent { get; set; }

        [PersistableAttribute]
        public string Location { get; set; }

        [PersistableAttribute]
        public string OfficerCaptured { get; set; }

        [PersistableAttribute]
        public string OfficerInjury { get; set; }

        [PersistableAttribute]
        public string OpponentInjury { get; set; }

        [PersistableAttribute]
        public string ImagePath { get; set; }

        [PersistableAttribute]
        public string AudioPath { get; set; }

        [PersistableAttribute]
        public string SourceEvent { get; set; }

        internal override bool Matches(GameResult result) => result is DuelResult;

        internal override void Bind(GameEventExecutionContext context, GameResult result)
        {
            DuelResult duel = (DuelResult)result;
            BindValue(context, Officer, duel.EncounteredOfficer);
            BindValue(context, Opponent, duel.OpposingOfficer);
            BindValue(context, Location, duel.Location);
            BindValue(context, OfficerCaptured, duel.EncounteredOfficerCaptured);
            BindValue(context, OfficerInjury, duel.EncounteredOfficerInjury);
            BindValue(context, OpponentInjury, duel.OpposingOfficerInjury);
            BindValue(context, ImagePath, duel.ImagePath);
            BindValue(context, AudioPath, duel.AudioPath);
            BindValue(context, SourceEvent, duel.SourceEventInstanceID);
        }
    }

    [PersistableObject(Name = "OfficerCaptureChanged")]
    public sealed class OfficerCaptureChangedTrigger : GameEventTrigger
    {
        internal override Type ResultType => typeof(OfficerCaptureStateResult);

        [PersistableAttribute]
        public string Officer { get; set; }

        [PersistableAttribute]
        public string LinkedOfficer { get; set; }

        [PersistableAttribute]
        public string Context { get; set; }

        [PersistableAttribute]
        public string IsCaptured { get; set; }

        [PersistableAttribute]
        public string SourceEvent { get; set; }

        internal override bool Matches(GameResult result) => result is OfficerCaptureStateResult;

        internal override void Bind(GameEventExecutionContext context, GameResult result)
        {
            OfficerCaptureStateResult capture = (OfficerCaptureStateResult)result;
            BindValue(context, Officer, capture.TargetOfficer ?? capture.CapturedOfficer);
            BindValue(context, LinkedOfficer, capture.LinkedOfficer);
            BindValue(context, Context, capture.Context);
            BindValue(context, IsCaptured, capture.IsCaptured);
            BindValue(context, SourceEvent, capture.SourceEventInstanceID);
        }
    }

    [PersistableObject(Name = "MissionCompleted")]
    public sealed class MissionCompletedTrigger : GameEventTrigger
    {
        internal override Type ResultType => typeof(MissionCompletedResult);

        [PersistableAttribute]
        public string Mission { get; set; }

        [PersistableAttribute]
        public string Outcome { get; set; }

        [PersistableAttribute]
        public string CompletionReason { get; set; }

        [PersistableAttribute]
        public string Participants { get; set; }

        [PersistableAttribute]
        public string Location { get; set; }

        [PersistableAttribute]
        public string ReturnDestination { get; set; }

        [PersistableAttribute]
        public string SourceEvent { get; set; }

        internal override bool Matches(GameResult result) => result is MissionCompletedResult;

        internal override void Bind(GameEventExecutionContext context, GameResult result)
        {
            MissionCompletedResult mission = (MissionCompletedResult)result;
            BindValue(context, Mission, mission.Mission);
            BindValue(context, Outcome, mission.Outcome);
            BindValue(context, CompletionReason, mission.CompletionReason);
            BindValue(context, Participants, mission.Participants);
            BindValue(context, Location, mission.Location);
            BindValue(context, ReturnDestination, mission.ReturnDestination);
            BindValue(context, SourceEvent, mission.SourceEventInstanceID);
        }
    }

    [PersistableObject(Name = "ForceDiscovered")]
    public sealed class ForceDiscoveredTrigger : GameEventTrigger
    {
        internal override Type ResultType => typeof(ForceDiscoveryResult);

        [PersistableAttribute]
        public string Officer { get; set; }

        [PersistableAttribute]
        public string Discoverer { get; set; }

        [PersistableAttribute]
        public string ForceRank { get; set; }

        [PersistableAttribute]
        public string SourceEvent { get; set; }

        internal override bool Matches(GameResult result) => result is ForceDiscoveryResult;

        internal override void Bind(GameEventExecutionContext context, GameResult result)
        {
            ForceDiscoveryResult discovery = (ForceDiscoveryResult)result;
            BindValue(context, Officer, discovery.Officer);
            BindValue(context, Discoverer, discovery.Discoverer);
            BindValue(context, ForceRank, discovery.ForceRank);
            BindValue(context, SourceEvent, discovery.SourceEventInstanceID);
        }
    }
}
