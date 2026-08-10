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
        /// <summary>Vader travels to Luke's current location.</summary>
        GatherLuke,

        /// <summary>Vader escorts Luke to Palpatine's current location.</summary>
        EscortToPalpatine,
    }

    /// <summary>
    /// Persists one travel leg of the Luke, Vader, and Palpatine story chain.
    /// </summary>
    [PersistableObject(Name = "StoryFinalBattleMission")]
    public sealed class StoryFinalBattleMission : Mission
    {
        public const string MissionTypeID = "StoryFinalBattle";

        [PersistableIgnore]
        private bool _lukeVictorious;

        // Story State.
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

        /// <summary>
        /// Creates an empty final-battle mission for deserialization.
        /// </summary>
        public StoryFinalBattleMission()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "The Final Battle";
            ParticipantRating = OfficerRating.None;
        }

        /// <summary>
        /// Creates one travel leg in the final-battle story chain.
        /// </summary>
        /// <param name="phase">The travel leg represented by this mission.</param>
        /// <param name="luke">The Luke officer participating in the story.</param>
        /// <param name="vader">The Vader officer performing the mission.</param>
        /// <param name="palpatine">The Palpatine officer awaiting the final encounter.</param>
        /// <param name="location">The mission's initial destination.</param>
        /// <param name="captorFactionInstanceId">The faction currently holding Luke captive.</param>
        /// <param name="durationTicks">The fixed duration of this travel leg.</param>
        /// <param name="victoryForceRank">The Force rank Luke needs to win.</param>
        /// <param name="minimumFailureInjury">The minimum injury applied on failure.</param>
        /// <param name="maximumFailureInjury">The maximum injury applied on failure.</param>
        /// <param name="captivesCanEscapeOnVictory">Whether defeated captives may escape.</param>
        /// <param name="displayName">The player-facing mission name.</param>
        /// <param name="sourceEventInstanceId">The event that started this mission.</param>
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

        /// <inheritdoc />
        public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;

        /// <inheritdoc />
        internal override bool AppliesFoiledParticipantConsequences => false;

        /// <inheritdoc />
        internal override bool SuccessfulParticipantsRemainAtLocation => true;

        /// <inheritdoc />
        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

        /// <summary>
        /// Keeps Vader's first-leg destination attached to Luke if the prisoner is relocated.
        /// </summary>
        /// <param name="game">The current game state.</param>
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <summary>
        /// Resolves a story participant from the registered scene-node catalog.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="instanceId">The officer's stable instance ID.</param>
        /// <returns>The matching officer, or null.</returns>
        private static Officer ResolveOfficer(GameRoot game, string instanceId) =>
            game.GetSceneNodeByInstanceID<Officer>(instanceId);

        /// <summary>
        /// Applies one final-battle capture transition and records it.
        /// </summary>
        /// <param name="officer">The affected officer.</param>
        /// <param name="isCaptured">The new capture state.</param>
        /// <param name="captorInstanceId">The new captor faction, if captured.</param>
        /// <param name="canEscape">Whether the captive may escape.</param>
        /// <param name="location">The location reported with the transition.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="results">The result collection receiving the transition.</param>
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

        /// <summary>
        /// Copies this mission's event provenance to a result.
        /// </summary>
        /// <typeparam name="T">The emitted result type.</typeparam>
        /// <param name="result">The result to stamp.</param>
        /// <returns>The stamped result.</returns>
        private T Stamp<T>(T result)
            where T : GameResult
        {
            result.SourceEventInstanceID = SourceEventInstanceID;
            return result;
        }
    }
}
