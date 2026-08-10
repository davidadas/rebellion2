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
        public CustomMissionDefinition Definition { get; private set; }

        public string MissionDefinitionID { get; set; }
        public List<MissionRoleAssignment> Roles { get; set; } = new List<MissionRoleAssignment>();

        public CustomMission()
        {
            ConfigKey = "CustomMission";
            ParticipantRating = OfficerRating.None;
        }

        public CustomMission(
            CustomMissionDefinition definition,
            IEnumerable<MissionRoleAssignment> roles,
            string sourceEventInstanceId,
            GameRoot game
        )
            : base(
                definition?.InstanceID ?? throw new ArgumentNullException(nameof(definition)),
                ResolveRole<IMissionParticipant>(roles, definition.OwnerRole, game)?.OwnerInstanceID
                    ?? throw new InvalidOperationException(
                        $"Mission '{definition.InstanceID}' could not resolve owner role '{definition.OwnerRole}'."
                    ),
                ResolvePlanet(roles, definition.LocationRole, game)?.InstanceID
                    ?? throw new InvalidOperationException(
                        $"Mission '{definition.InstanceID}' could not resolve location role '{definition.LocationRole}'."
                    ),
                ResolveParticipants(definition, roles, game),
                new List<IMissionParticipant>(),
                OfficerRating.None,
                definition.DisplayName
            )
        {
            Definition = definition;
            MissionDefinitionID = definition.InstanceID;
            Roles = roles?.Select(CloneRole).ToList() ?? new List<MissionRoleAssignment>();
            CanAbort = definition.CanAbort;
            SourceEventInstanceID = sourceEventInstanceId;
        }

        public void SetDefinition(CustomMissionDefinition definition)
        {
            if (definition?.InstanceID != MissionDefinitionID)
                throw new InvalidOperationException(
                    $"Mission definition '{MissionDefinitionID}' is unavailable."
                );
            Definition = definition;
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
            if (string.IsNullOrWhiteSpace(Definition.TrackedLocationRole))
                return;
            Planet tracked = GetRole<ISceneNode>(game, Definition.TrackedLocationRole)
                ?.GetParentOfType<Planet>();
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

        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

        public override MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            MissionCompletionReason? baseReason = base.GetAbortReason(game);
            if (baseReason.HasValue)
                return baseReason;
            EnsureDefinition();

            return Definition.Resolution switch
            {
                CustomMissionResolution.OfficerRescue => IsCaptiveAtLocation(
                    game,
                    Definition.CaptiveRole
                )
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
            Officer target = GetRole<Officer>(game, Definition.TargetRole);
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
            Officer rescuer = GetRole<Officer>(game, Definition.RescuerRole);
            Officer captive = GetRole<Officer>(game, Definition.CaptiveRole);
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
            Officer collector = GetRole<Officer>(game, Definition.CollectorRole);
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
            Officer subject = GetRole<Officer>(game, Definition.SubjectRole);
            Officer opponent = GetRole<Officer>(game, Definition.OpponentRole);
            Officer authority = GetRole<Officer>(game, Definition.AuthorityRole);
            Planet location = GetParent() as Planet;
            if (Definition.Phase == CustomMissionPhase.GatherTarget)
            {
                return new List<GameResult>
                {
                    Stamp(
                        new CustomMissionRequestedResult
                        {
                            MissionDefinitionID = Definition.FollowUpMissionDefinitionID,
                            Roles = Roles.ConvertAll(CloneRole),
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
            Officer subject = GetRole<Officer>(game, Definition.SubjectRole);
            Officer opponent = GetRole<Officer>(game, Definition.OpponentRole);
            Officer authority = GetRole<Officer>(game, Definition.AuthorityRole);
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

        private bool IsCaptiveAtLocation(GameRoot game, string role)
        {
            Officer captive = GetRole<Officer>(game, role);
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

        internal T GetRole<T>(GameRoot game, string role)
            where T : class =>
            game.GetSceneNodeByInstanceID<T>(
                Roles.FirstOrDefault(candidate => candidate.Name == role)?.UnitInstanceID
            );

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
            CustomMissionDefinition definition,
            IEnumerable<MissionRoleAssignment> roles,
            GameRoot game
        ) =>
            definition
                .ParticipantRoles.Select(role =>
                    ResolveRole<IMissionParticipant>(roles, role, game)
                )
                .Where(participant => participant != null)
                .ToList();

        private static T ResolveRole<T>(
            IEnumerable<MissionRoleAssignment> roles,
            string role,
            GameRoot game
        )
            where T : class =>
            game.GetSceneNodeByInstanceID<T>(
                roles?.FirstOrDefault(candidate => candidate.Name == role)?.UnitInstanceID
            );

        private static Planet ResolvePlanet(
            IEnumerable<MissionRoleAssignment> roles,
            string role,
            GameRoot game
        )
        {
            ISceneNode node = ResolveRole<ISceneNode>(roles, role, game);
            return node as Planet ?? node?.GetParentOfType<Planet>();
        }

        private static MissionRoleAssignment CloneRole(MissionRoleAssignment role) =>
            new MissionRoleAssignment { Name = role.Name, UnitInstanceID = role.UnitInstanceID };
    }
}
