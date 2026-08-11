using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// A persisted mission instance whose lifecycle is supplied by a content definition.
    /// </summary>
    [PersistableObject(Name = "CustomMission")]
    public sealed class CustomMission : Mission
    {
        [PersistableIgnore]
        private readonly List<string> _returnPassengerInstanceIDs = new List<string>();

        [PersistableIgnore]
        private ISceneNode _target;

        [PersistableIgnore]
        public CustomMissionDefinition Definition { get; private set; }

        public string MissionDefinitionID { get; set; }
        public string TargetInstanceID { get; set; }

        public CustomMission()
        {
            ConfigKey = "CustomMission";
            ParticipantRating = OfficerRating.None;
        }

        public CustomMission(
            CustomMissionDefinition definition,
            string targetInstanceId,
            IEnumerable<string> mainParticipantInstanceIds,
            IEnumerable<string> decoyParticipantInstanceIds,
            string sourceEventInstanceId,
            GameRoot game
        )
            : base(
                definition?.InstanceID ?? throw new ArgumentNullException(nameof(definition)),
                ResolveOwner(definition, targetInstanceId, mainParticipantInstanceIds, game),
                ResolveLocation(definition, targetInstanceId, game).InstanceID,
                ResolveParticipants(mainParticipantInstanceIds, game),
                ResolveParticipants(decoyParticipantInstanceIds, game),
                OfficerRating.None,
                definition.DisplayName
            )
        {
            Definition = definition;
            MissionDefinitionID = definition.InstanceID;
            TargetInstanceID = targetInstanceId;
            _target = game.GetSceneNodeByInstanceID<ISceneNode>(targetInstanceId);
            CanAbort = definition.CanAbort;
            SourceEventInstanceID = sourceEventInstanceId;
        }

        public void SetDefinition(CustomMissionDefinition definition, GameRoot game)
        {
            if (definition?.InstanceID != MissionDefinitionID)
                throw new InvalidOperationException(
                    $"Mission definition '{MissionDefinitionID}' is unavailable."
                );
            Definition = definition;
            _target = game.GetSceneNodeByInstanceID<ISceneNode>(TargetInstanceID);
        }

        public int RollDuration(IRandomNumberProvider provider)
        {
            EnsureDefinition();
            return checked(
                Definition.DurationTicks
                + (
                    Definition.DurationRandomTicks > 0
                        ? provider.NextInt(0, Definition.DurationRandomTicks)
                        : 0
                )
            );
        }

        public void RefreshTrackedLocation(GameRoot game)
        {
            EnsureDefinition();
            if (Definition.Phase != CustomMissionPhase.GatherTarget)
                return;
            Planet tracked = GetTarget<ISceneNode>(game)?.GetParentOfType<Planet>();
            if (tracked != null && tracked != GetParent())
            {
                game.MoveNode(this, tracked);
                LocationInstanceID = tracked.InstanceID;
            }
        }

        public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;

        internal override bool AppliesFoiledParticipantConsequences => false;

        internal override bool SuccessfulParticipantsRemainAtLocation =>
            Definition?.Resolution == CustomMissionResolution.ForceConfrontation;

        /// <summary>
        /// Returns mission children, including an escorted target that is not a participant.
        /// </summary>
        public override IEnumerable<ISceneNode> GetChildren()
        {
            IEnumerable<ISceneNode> participants = base.GetChildren();
            return _target?.GetParent() == this ? participants.Append(_target) : participants;
        }

        public override void AddChild(ISceneNode child)
        {
            if (child?.InstanceID == TargetInstanceID)
                _target = child;
            else
                base.AddChild(child);
        }

        public override void RemoveChild(ISceneNode child)
        {
            if (ReferenceEquals(child, _target))
                _target = null;
            base.RemoveChild(child);
        }

        internal IMovable GetEscortedTarget(GameRoot game) =>
            Definition?.Phase == CustomMissionPhase.EscortToDestination
                ? GetTarget<IMovable>(game)
                : null;

        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

        public override MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            MissionCompletionReason? baseReason = base.GetAbortReason(game);
            if (baseReason.HasValue)
                return baseReason;
            EnsureDefinition();

            return Definition.Resolution switch
            {
                CustomMissionResolution.OfficerRescue => IsCaptiveAtLocation(game)
                    ? null
                    : MissionCompletionReason.TargetUnavailable,
                CustomMissionResolution.PrisonerPickup => GetEligiblePrisoners().Any()
                    ? null
                    : MissionCompletionReason.TargetUnavailable,
                CustomMissionResolution.ForceConfrontation => GetForceAbortReason(game),
                _ => null,
            };
        }

        internal override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            EnsureDefinition();
            return Definition.Resolution switch
            {
                CustomMissionResolution.OfficerCapture => ResolveCapture(game, provider),
                CustomMissionResolution.OfficerRescue => ResolveRescue(game, provider),
                CustomMissionResolution.PrisonerPickup => ResolvePickup(game),
                CustomMissionResolution.ForceConfrontation => ResolveForceConfrontation(
                    game,
                    provider
                ),
                _ => throw new InvalidOperationException(
                    $"Unsupported custom mission resolution '{Definition.Resolution}'."
                ),
            };
        }

        internal override IEnumerable<IMovable> GetSuccessfulReturnPassengers(GameRoot game)
        {
            foreach (string instanceId in _returnPassengerInstanceIDs)
            {
                IMovable passenger = game.GetSceneNodeByInstanceID<IMovable>(instanceId);
                if (passenger != null)
                    yield return passenger;
            }
        }

        private List<GameResult> ResolveCapture(GameRoot game, IRandomNumberProvider provider)
        {
            Officer target = GetTarget<Officer>(game);
            Planet location = GetParent() as Planet;
            if (target?.IsKilled != false || target?.IsCaptured != false)
                return CaptureResults(
                    game,
                    target,
                    location,
                    false,
                    MissionCompletionReason.TargetUnavailable
                );

            int resistance = target.GetEffectiveRating(Definition.ResistanceRating);
            double probability = LookupSuccessProbability(
                game,
                Definition.AttackRating - resistance,
                Definition.ProbabilityTableKey
            );
            if (!IsSuccessfulProbabilityRoll(provider.NextDouble() * 100, probability))
                return CaptureResults(game, target, location, false);

            target.IsCaptured = true;
            target.CaptorInstanceID = Definition.CaptorFactionInstanceID;
            target.CanEscape = Definition.TargetCanEscape;
            List<GameResult> results = new List<GameResult>
            {
                Stamp(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = target,
                        IsCaptured = true,
                        Context = location,
                        Tick = game.CurrentTick,
                    }
                ),
            };
            results.AddRange(CaptureResults(game, target, location, true));
            return results;
        }

        private List<GameResult> CaptureResults(
            GameRoot game,
            Officer target,
            Planet location,
            bool captured,
            MissionCompletionReason reason = MissionCompletionReason.None
        )
        {
            MissionOutcome outcome = captured ? MissionOutcome.Success : MissionOutcome.Failed;
            return new List<GameResult>
            {
                Stamp(
                    new OfficerCaptureAttemptResult
                    {
                        Target = target,
                        Location = location,
                        WasCaptured = captured,
                        Tick = game.CurrentTick,
                    }
                ),
                Stamp(
                    reason == MissionCompletionReason.None
                        ? BuildCompletedResult(outcome, game)
                        : BuildCompletedResult(outcome, reason, game)
                ),
            };
        }

        private List<GameResult> ResolveRescue(GameRoot game, IRandomNumberProvider provider)
        {
            Officer rescuer = GetMainParticipant<Officer>(0);
            Officer captive = GetTarget<Officer>(game);
            if (rescuer == null || captive?.IsCaptured != true)
                return new List<GameResult>
                {
                    Stamp(
                        BuildCompletedResult(
                            MissionOutcome.Failed,
                            MissionCompletionReason.TargetUnavailable,
                            game
                        )
                    ),
                };

            int successPercent =
                rescuer.GetEffectiveRating(OfficerRating.Combat) / Definition.RatingDivisor
                + rescuer.GetEffectiveRating(OfficerRating.Espionage) / Definition.RatingDivisor;
            bool success = provider.NextDouble() * 100 < successPercent;
            List<GameResult> results = success
                ? ResolveRescueSuccess(game, rescuer)
                : ResolveRescueFailure(game, rescuer);
            results.Add(
                Stamp(
                    BuildCompletedResult(
                        success ? MissionOutcome.Success : MissionOutcome.Failed,
                        game
                    )
                )
            );
            return results;
        }

        private List<GameResult> ResolveRescueSuccess(GameRoot game, Officer rescuer)
        {
            rescuer.IncrementBaseRating(OfficerRating.Combat, Definition.SuccessCombatBonus);
            rescuer.IncrementBaseRating(OfficerRating.Espionage, Definition.SuccessEspionageBonus);
            Planet location = GetParent() as Planet;
            List<GameResult> results = new List<GameResult>();
            foreach (
                Officer officer in location
                    ?.GetAllOfficers()
                    .Where(candidate =>
                        candidate.IsCaptured && candidate.OwnerInstanceID == OwnerInstanceID
                    )
                    .ToList()
                    ?? new List<Officer>()
            )
            {
                officer.IsCaptured = false;
                officer.CaptorInstanceID = null;
                officer.CanEscape = false;
                _returnPassengerInstanceIDs.Add(officer.InstanceID);
                results.Add(
                    Stamp(
                        new OfficerCaptureStateResult
                        {
                            TargetOfficer = officer,
                            IsCaptured = false,
                            Context = location,
                            Tick = game.CurrentTick,
                        }
                    )
                );
                results.Add(
                    Stamp(
                        new OfficerRescuedResult
                        {
                            Officer = officer,
                            RescuingFaction = game.GetFactionByOwnerInstanceID(OwnerInstanceID),
                            Location = location,
                            Tick = game.CurrentTick,
                        }
                    )
                );
            }
            return results;
        }

        private List<GameResult> ResolveRescueFailure(GameRoot game, Officer rescuer)
        {
            if (!Definition.CaptureRescuerOnFailure)
                return new List<GameResult>();
            rescuer.IsCaptured = true;
            rescuer.CaptorInstanceID = null;
            rescuer.CanEscape = Definition.FailedRescuerCanEscape;
            return new List<GameResult>
            {
                Stamp(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = rescuer,
                        IsCaptured = true,
                        Context = GetParent() as Planet,
                        Tick = game.CurrentTick,
                    }
                ),
            };
        }

        private List<GameResult> ResolvePickup(GameRoot game)
        {
            Officer collector = GetMainParticipant<Officer>(0);
            List<Officer> prisoners = GetEligiblePrisoners().ToList();
            if (collector == null || prisoners.Count == 0)
                return new List<GameResult>
                {
                    Stamp(
                        BuildCompletedResult(
                            MissionOutcome.Failed,
                            MissionCompletionReason.TargetUnavailable,
                            game
                        )
                    ),
                };

            foreach (Officer prisoner in prisoners)
            {
                prisoner.CaptorInstanceID = OwnerInstanceID;
                prisoner.CanEscape = Definition.CaptivesCanEscapeAfterPickup;
                _returnPassengerInstanceIDs.Add(prisoner.InstanceID);
            }
            return new List<GameResult>
            {
                Stamp(
                    new OfficerPickupResult
                    {
                        Officer = collector,
                        InProgress = false,
                        Tick = game.CurrentTick,
                    }
                ),
                Stamp(
                    new PrisonerPickupCompletedResult
                    {
                        Collector = collector,
                        Location = GetParent() as Planet,
                        Prisoners = prisoners,
                        Tick = game.CurrentTick,
                    }
                ),
                Stamp(BuildCompletedResult(MissionOutcome.Success, game)),
            };
        }

        private List<GameResult> ResolveForceConfrontation(
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            Officer subject = GetTarget<Officer>(game);
            Officer opponent = GetMainParticipant<Officer>(0);
            Officer authority = game.GetSceneNodeByInstanceID<Officer>(
                Definition.AuthorityUnitInstanceID
            );
            Planet location = GetParent() as Planet;
            if (Definition.Phase == CustomMissionPhase.GatherTarget)
            {
                return new List<GameResult>
                {
                    Stamp(
                        new CustomMissionRequestedResult
                        {
                            MissionDefinitionID = Definition.FollowUpMissionDefinitionID,
                            TargetInstanceID = TargetInstanceID,
                            MainParticipantInstanceIDs = new List<string> { opponent.InstanceID },
                            Tick = game.CurrentTick,
                        }
                    ),
                    Stamp(BuildCompletedResult(MissionOutcome.Success, game)),
                };
            }

            List<IMissionParticipant> participants = GetAllParticipants();
            game.MoveNode(subject, location);
            game.MoveNode(opponent, location);
            MainParticipants.Clear();
            bool subjectVictorious = subject.ForceRank >= Definition.VictoryForceRank;
            List<GameResult> results = new List<GameResult>();
            if (subjectVictorious)
            {
                SetCaptureState(subject, false, null, true, location, game, results);
                SetCaptureState(
                    opponent,
                    true,
                    subject.OwnerInstanceID,
                    Definition.CaptivesCanEscapeOnVictory,
                    location,
                    game,
                    results
                );
                SetCaptureState(
                    authority,
                    true,
                    subject.OwnerInstanceID,
                    Definition.CaptivesCanEscapeOnVictory,
                    location,
                    game,
                    results
                );
            }
            else
            {
                subject.CanEscape = false;
                int injury = provider.NextInt(
                    Definition.MinimumFailureInjury,
                    checked(Definition.MaximumFailureInjury + 1)
                );
                subject.ApplyInjury(injury, game.Config.Recovery.MaxInjuryPoints);
                results.Add(
                    Stamp(
                        new OfficerInjuredResult
                        {
                            Officer = subject,
                            Severity = injury,
                            Tick = game.CurrentTick,
                        }
                    )
                );
            }
            results.Add(
                Stamp(
                    new ForceConfrontationCompletedResult
                    {
                        Luke = subject,
                        Vader = opponent,
                        Palpatine = authority,
                        LukeVictorious = subjectVictorious,
                        Tick = game.CurrentTick,
                    }
                )
            );
            results.Add(Stamp(BuildCompletedResult(MissionOutcome.Success, game, participants)));
            return results;
        }

        private MissionCompletionReason? GetForceAbortReason(GameRoot game)
        {
            Officer subject = GetTarget<Officer>(game);
            Officer opponent = GetMainParticipant<Officer>(0);
            Officer authority = game.GetSceneNodeByInstanceID<Officer>(
                Definition.AuthorityUnitInstanceID
            );
            return
                subject?.IsKilled != false
                || subject?.IsCaptured != true
                || subject.CaptorInstanceID != Definition.CaptorFactionInstanceID
                || opponent?.IsKilled != false
                || opponent?.IsCaptured != false
                || authority?.IsKilled != false
                || authority?.IsCaptured != false
                || authority.IsOnMission()
                || authority.Movement != null
                ? MissionCompletionReason.TargetUnavailable
                : null;
        }

        private bool IsCaptiveAtLocation(GameRoot game)
        {
            Officer captive = GetTarget<Officer>(game);
            return captive?.IsCaptured == true && captive.GetParentOfType<Planet>() == GetParent();
        }

        private IEnumerable<Officer> GetEligiblePrisoners()
        {
            Planet location = GetParent() as Planet;
            return location
                    ?.GetAllOfficers()
                    .Where(officer =>
                        officer.IsCaptured
                        && !officer.IsKilled
                        && officer.OwnerInstanceID == Definition.CaptiveFactionInstanceID
                    )
                ?? Enumerable.Empty<Officer>();
        }

        private void SetCaptureState(
            Officer officer,
            bool captured,
            string captorInstanceId,
            bool canEscape,
            Planet location,
            GameRoot game,
            ICollection<GameResult> results
        )
        {
            officer.IsCaptured = captured;
            officer.CaptorInstanceID = captorInstanceId;
            officer.CanEscape = canEscape;
            results.Add(
                Stamp(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = officer,
                        IsCaptured = captured,
                        Context = location,
                        Tick = game.CurrentTick,
                    }
                )
            );
        }

        internal T GetTarget<T>(GameRoot game)
            where T : class => game.GetSceneNodeByInstanceID<T>(TargetInstanceID);

        internal T GetMainParticipant<T>(int index)
            where T : class =>
            index >= 0 && index < MainParticipants.Count ? MainParticipants[index] as T : null;

        private T Stamp<T>(T result)
            where T : GameResult
        {
            result.SourceEventInstanceID = SourceEventInstanceID;
            return result;
        }

        private void EnsureDefinition()
        {
            if (Definition == null)
                throw new InvalidOperationException(
                    $"Mission definition '{MissionDefinitionID}' has not been attached."
                );
        }

        private static List<IMissionParticipant> ResolveParticipants(
            IEnumerable<string> instanceIds,
            GameRoot game
        ) =>
            instanceIds
                ?.Select(game.GetSceneNodeByInstanceID<IMissionParticipant>)
                .Where(participant => participant != null)
                .ToList()
            ?? new List<IMissionParticipant>();

        private static string ResolveOwner(
            CustomMissionDefinition definition,
            string targetInstanceId,
            IEnumerable<string> mainParticipantInstanceIds,
            GameRoot game
        )
        {
            if (!string.IsNullOrWhiteSpace(definition.OwnerFactionInstanceID))
                return definition.OwnerFactionInstanceID;
            IMissionParticipant participant = ResolveParticipants(mainParticipantInstanceIds, game)
                .FirstOrDefault();
            ISceneNode target = game.GetSceneNodeByInstanceID<ISceneNode>(targetInstanceId);
            return participant?.OwnerInstanceID
                ?? target?.OwnerInstanceID
                ?? throw new InvalidOperationException(
                    $"Mission '{definition.InstanceID}' could not resolve an owner."
                );
        }

        private static Planet ResolveLocation(
            CustomMissionDefinition definition,
            string targetInstanceId,
            GameRoot game
        )
        {
            string locationSourceId =
                definition.Phase == CustomMissionPhase.EscortToDestination
                    ? definition.AuthorityUnitInstanceID
                    : targetInstanceId;
            ISceneNode source = game.GetSceneNodeByInstanceID<ISceneNode>(locationSourceId);
            return source as Planet
                ?? source?.GetParentOfType<Planet>()
                ?? throw new InvalidOperationException(
                    $"Mission '{definition.InstanceID}' could not resolve its location."
                );
        }
    }
}
