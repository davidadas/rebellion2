using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Missions
{
    public enum StoryFinalBattlePhase
    {
        GatherLuke,
        EscortToPalpatine,
    }

    /// <summary>
    /// Persists one travel leg of the original Luke, Vader, and Palpatine story chain.
    /// </summary>
    [PersistableObject(Name = "StoryFinalBattleMission")]
    public sealed class StoryFinalBattleMission : Mission
    {
        public const string MissionTypeID = "StoryFinalBattle";

        public StoryFinalBattlePhase Phase { get; set; }
        public string LukeOfficerInstanceID { get; set; }
        public string VaderOfficerInstanceID { get; set; }
        public string PalpatineOfficerInstanceID { get; set; }
        public string CaptorFactionInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public int VictoryForceRank { get; set; }
        public int MinimumFailureInjury { get; set; }
        public int MaximumFailureInjury { get; set; }
        public bool CaptivesCanEscapeOnVictory { get; set; }

        [PersistableIgnore]
        private bool _lukeVictorious;

        public StoryFinalBattleMission()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "The Final Battle";
            ParticipantRating = OfficerRating.None;
        }

        public StoryFinalBattleMission(
            StoryFinalBattlePhase phase,
            Officer luke,
            Officer vader,
            Officer palpatine,
            Planet location,
            string captorFactionInstanceId,
            int durationTicks,
            int victoryForceRank,
            int minimumFailureInjury,
            int maximumFailureInjury,
            bool captivesCanEscapeOnVictory,
            string displayName,
            string sourceEventInstanceId
        )
            : base(
                MissionTypeID,
                vader?.OwnerInstanceID ?? throw new ArgumentNullException(nameof(vader)),
                location?.InstanceID ?? throw new ArgumentNullException(nameof(location)),
                phase == StoryFinalBattlePhase.GatherLuke
                    ? new List<IMissionParticipant> { vader }
                    : new List<IMissionParticipant> { vader, luke },
                new List<IMissionParticipant>(),
                OfficerRating.None,
                displayName
            )
        {
            if (luke == null)
                throw new ArgumentNullException(nameof(luke));
            if (palpatine == null)
                throw new ArgumentNullException(nameof(palpatine));
            if (durationTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(durationTicks));
            if (victoryForceRank < 0)
                throw new ArgumentOutOfRangeException(nameof(victoryForceRank));
            if (minimumFailureInjury < 0 || maximumFailureInjury < minimumFailureInjury)
                throw new ArgumentOutOfRangeException(nameof(minimumFailureInjury));

            Phase = phase;
            LukeOfficerInstanceID = luke.InstanceID;
            VaderOfficerInstanceID = vader.InstanceID;
            PalpatineOfficerInstanceID = palpatine.InstanceID;
            CaptorFactionInstanceID = captorFactionInstanceId;
            DurationTicks = durationTicks;
            VictoryForceRank = victoryForceRank;
            MinimumFailureInjury = minimumFailureInjury;
            MaximumFailureInjury = maximumFailureInjury;
            CaptivesCanEscapeOnVictory = captivesCanEscapeOnVictory;
            SourceEventInstanceID = sourceEventInstanceId;
        }

        public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;

        internal override bool AppliesFoiledParticipantConsequences => false;

        internal override bool SuccessfulParticipantsRemainAtLocation => true;

        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

        /// <summary>
        /// Keeps Vader's first-leg destination attached to Luke if the prisoner is relocated.
        /// </summary>
        internal void RefreshTravelTarget(GameRoot game)
        {
            if (Phase != StoryFinalBattlePhase.GatherLuke)
                return;

            Planet lukePlanet = ResolveOfficer(game, LukeOfficerInstanceID)
                ?.GetParentOfType<Planet>();
            if (lukePlanet != null && GetParent() != lukePlanet)
            {
                game.MoveNode(this, lukePlanet);
                LocationInstanceID = lukePlanet.InstanceID;
            }
        }

        public override MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetAbortReason(game);
            if (reason.HasValue)
                return reason;

            Officer luke = ResolveOfficer(game, LukeOfficerInstanceID);
            Officer vader = ResolveOfficer(game, VaderOfficerInstanceID);
            Officer palpatine = ResolveOfficer(game, PalpatineOfficerInstanceID);
            if (
                luke?.IsKilled != false
                || luke?.IsCaptured != true
                || luke.CaptorInstanceID != CaptorFactionInstanceID
                || vader?.IsKilled != false
                || vader?.IsCaptured != false
                || palpatine?.IsKilled != false
                || palpatine?.IsCaptured != false
                || palpatine.IsOnMission()
                || palpatine.Movement != null
            )
                return MissionCompletionReason.TargetUnavailable;

            return null;
        }

        internal override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            Officer luke = ResolveOfficer(game, LukeOfficerInstanceID);
            Officer vader = ResolveOfficer(game, VaderOfficerInstanceID);
            Officer palpatine = ResolveOfficer(game, PalpatineOfficerInstanceID);
            Planet location = GetParent() as Planet;

            if (Phase == StoryFinalBattlePhase.GatherLuke)
            {
                return new List<GameResult>
                {
                    Stamp(
                        new StoryFinalBattleEscortRequestedResult
                        {
                            Luke = luke,
                            Vader = vader,
                            Palpatine = palpatine,
                            CaptorFactionInstanceID = CaptorFactionInstanceID,
                            DurationTicks = DurationTicks,
                            VictoryForceRank = VictoryForceRank,
                            MinimumFailureInjury = MinimumFailureInjury,
                            MaximumFailureInjury = MaximumFailureInjury,
                            CaptivesCanEscapeOnVictory = CaptivesCanEscapeOnVictory,
                            DisplayName = DisplayName,
                            Tick = game.CurrentTick,
                        }
                    ),
                    Stamp(BuildCompletedResult(MissionOutcome.Success, game)),
                };
            }

            List<IMissionParticipant> participants = GetAllParticipants();
            game.MoveNode(luke, location);
            game.MoveNode(vader, location);
            MainParticipants.Clear();
            _lukeVictorious = luke.ForceRank >= VictoryForceRank;
            List<GameResult> results = new List<GameResult>();
            if (_lukeVictorious)
            {
                SetCaptureState(luke, false, null, true, location, game, results);
                SetCaptureState(
                    vader,
                    true,
                    luke.OwnerInstanceID,
                    CaptivesCanEscapeOnVictory,
                    location,
                    game,
                    results
                );
                SetCaptureState(
                    palpatine,
                    true,
                    luke.OwnerInstanceID,
                    CaptivesCanEscapeOnVictory,
                    location,
                    game,
                    results
                );
            }
            else
            {
                luke.CanEscape = false;
                int injury = provider.NextInt(
                    MinimumFailureInjury,
                    checked(MaximumFailureInjury + 1)
                );
                luke.ApplyInjury(injury, game.Config.Recovery.MaxInjuryPoints);
                results.Add(
                    Stamp(
                        new OfficerInjuredResult
                        {
                            Officer = luke,
                            Severity = injury,
                            Tick = game.CurrentTick,
                        }
                    )
                );
            }

            results.Add(
                Stamp(
                    new StoryFinalBattleCompletedResult
                    {
                        Luke = luke,
                        Vader = vader,
                        Palpatine = palpatine,
                        LukeVictorious = _lukeVictorious,
                        Tick = game.CurrentTick,
                    }
                )
            );
            results.Add(Stamp(BuildCompletedResult(MissionOutcome.Success, game, participants)));
            return results;
        }

        private static Officer ResolveOfficer(GameRoot game, string instanceId) =>
            game.GetSceneNodeByInstanceID<Officer>(instanceId);

        private void SetCaptureState(
            Officer officer,
            bool isCaptured,
            string captorInstanceId,
            bool canEscape,
            Planet location,
            GameRoot game,
            ICollection<GameResult> results
        )
        {
            officer.IsCaptured = isCaptured;
            officer.CaptorInstanceID = captorInstanceId;
            officer.CanEscape = canEscape;
            results.Add(
                Stamp(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = officer,
                        IsCaptured = isCaptured,
                        Context = location,
                        Tick = game.CurrentTick,
                    }
                )
            );
        }

        private T Stamp<T>(T result)
            where T : GameResult
        {
            result.SourceEventInstanceID = SourceEventInstanceID;
            return result;
        }
    }
}
