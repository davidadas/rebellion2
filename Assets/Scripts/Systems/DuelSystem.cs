using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Resolves asymmetric encounters between linked opposing officers.
    /// </summary>
    public sealed class DuelSystem : IGameResultHandler<DuelRequestedResult>
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _random;
        private readonly ProbabilityTable _captureAvoidance;

        /// <summary>
        /// Creates the authoritative resolver for officer encounter requests.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="random">The deterministic simulation random source.</param>
        public DuelSystem(GameRoot game, IRandomNumberProvider random)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _captureAvoidance = new ProbabilityTable(
                _game.Config.DuelResolution.CombatCaptureAvoidance
            );
        }

        /// <summary>
        /// Validates queued duel requests and emits capture, injury, and duel outcomes for each
        /// eligible opposing officer pair.
        /// </summary>
        public List<GameResult> HandleResults(IReadOnlyList<DuelRequestedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            foreach (DuelRequestedResult request in results)
            {
                if (CanResolveDuel(request))
                    ResolveDuel(request, reactions);
            }

            return reactions;
        }

        /// <summary>
        /// Returns whether both officers remain eligible opponents at the same planet.
        /// </summary>
        /// <param name="request">The encounter request to validate.</param>
        /// <returns>True when authoritative duel resolution may proceed.</returns>
        private static bool CanResolveDuel(DuelRequestedResult request)
        {
            Officer encountered = request?.EncounteredOfficer;
            Officer opposing = request?.OpposingOfficer;
            if (encountered == null || opposing == null)
                return false;
            if (encountered == opposing)
                return false;
            if (encountered.IsKilled || opposing.IsKilled)
                return false;
            if (encountered.IsCaptured || opposing.IsCaptured)
                return false;
            if (encountered.OwnerInstanceID == opposing.OwnerInstanceID)
                return false;
            Planet location = encountered.GetParentOfType<Planet>();
            return location != null && opposing.GetParentOfType<Planet>() == location;
        }

        /// <summary>
        /// Applies capture, injury, and advancement outcomes for one encounter.
        /// </summary>
        /// <param name="request">The validated encounter request.</param>
        /// <param name="reactions">The result collection receiving authoritative outcomes.</param>
        private void ResolveDuel(DuelRequestedResult request, List<GameResult> reactions)
        {
            Officer encountered = request.EncounteredOfficer;
            Officer opposing = request.OpposingOfficer;
            Planet location = encountered.GetParentOfType<Planet>();
            int encounteredCombat = encountered.GetEffectiveRating(OfficerRating.Combat);
            int opposingCombat = opposing.GetEffectiveRating(OfficerRating.Combat);
            bool captured = TryCaptureEncounteredOfficer(
                encountered,
                opposing,
                location,
                encounteredCombat,
                opposingCombat,
                request,
                reactions
            );
            int encounteredInjury = CalculateEncounteredOfficerInjury(
                captured,
                encounteredCombat,
                opposingCombat
            );
            int opposingInjury = CalculateOpposingOfficerInjury(encounteredCombat, opposingCombat);

            ApplyInjury(encountered, encounteredInjury, opposing, request, reactions);
            ApplyInjury(opposing, opposingInjury, encountered, request, reactions);
            RecordDuelOutcome(
                request,
                location,
                captured,
                encounteredInjury,
                opposingInjury,
                reactions
            );
        }

        /// <summary>
        /// Resolves whether the encountered officer avoids capture and records capture state when
        /// the opposing officer succeeds.
        /// </summary>
        private bool TryCaptureEncounteredOfficer(
            Officer encountered,
            Officer opposing,
            Planet location,
            int encounteredCombat,
            int opposingCombat,
            DuelRequestedResult request,
            ICollection<GameResult> reactions
        )
        {
            int avoidanceChance = _captureAvoidance.Lookup(encounteredCombat - opposingCombat);
            if (RollPercent(avoidanceChance))
                return false;

            encountered.IsCaptured = true;
            encountered.CaptorInstanceID = opposing.OwnerInstanceID;
            encountered.CanEscape = true;
            reactions.Add(
                Stamp(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = encountered,
                        IsCaptured = true,
                        CapturedOfficer = encountered,
                        LinkedOfficer = opposing,
                        Context = location,
                        Tick = _game.CurrentTick,
                    },
                    request
                )
            );
            return true;
        }

        /// <summary>
        /// Resolves injury to the encountered officer after capture or successful evasion.
        /// </summary>
        private int CalculateEncounteredOfficerInjury(
            bool captured,
            int encounteredCombat,
            int opposingCombat
        )
        {
            GameConfig.DuelResolutionConfig config = _game.Config.DuelResolution;
            int injury = captured
                ? 0
                : TryRollInjury(
                    Math.Max(
                        config.MinimumInjuryChance,
                        config.CaptureEvasionInjuryBaseChance - encounteredCombat
                    )
                );
            return injury != 0
                ? injury
                : TryRollInjury(
                    Math.Max(config.MinimumInjuryChance, opposingCombat - encounteredCombat)
                );
        }

        /// <summary>
        /// Resolves injury to the opposing officer from the encountered officer's combat advantage.
        /// </summary>
        private int CalculateOpposingOfficerInjury(int encounteredCombat, int opposingCombat) =>
            TryRollInjury(
                Math.Max(
                    _game.Config.DuelResolution.MinimumInjuryChance,
                    encounteredCombat - opposingCombat
                )
            );

        /// <summary>
        /// Records the complete duel outcome after capture and injury consequences are applied.
        /// </summary>
        private void RecordDuelOutcome(
            DuelRequestedResult request,
            Planet location,
            bool captured,
            int encounteredInjury,
            int opposingInjury,
            ICollection<GameResult> reactions
        )
        {
            reactions.Add(
                Stamp(
                    new DuelResult
                    {
                        EncounteredOfficer = request.EncounteredOfficer,
                        OpposingOfficer = request.OpposingOfficer,
                        Location = location,
                        EncounteredOfficerCaptured = captured,
                        EncounteredOfficerInjury = encounteredInjury,
                        OpposingOfficerInjury = opposingInjury,
                        ImagePath = request.ImagePath,
                        AudioPath = request.AudioPath,
                        Tick = _game.CurrentTick,
                    },
                    request
                )
            );
        }

        /// <summary>
        /// Resolves an injury chance and generates its configured severity.
        /// </summary>
        /// <param name="chance">The percentage chance of injury.</param>
        /// <returns>The injury severity, or zero when avoided.</returns>
        private int TryRollInjury(int chance)
        {
            if (!RollPercent(chance))
                return 0;

            GameConfig.DuelResolutionConfig config = _game.Config.DuelResolution;
            return config.InjuryBase
                + _random.NextInt(0, chance + 1)
                + _random.NextInt(0, config.InjurySecondaryRollMaximum + 1);
        }

        /// <summary>
        /// Rolls a clamped percentage against the deterministic simulation stream.
        /// </summary>
        /// <param name="chance">The percentage chance to test.</param>
        /// <returns>True when the roll succeeds.</returns>
        private bool RollPercent(int chance)
        {
            return _random.NextInt(0, 100) < Math.Min(100, Math.Max(0, chance));
        }

        /// <summary>
        /// Applies an injury and awards the opposing officer when severity is positive.
        /// </summary>
        /// <param name="injured">The officer receiving the injury.</param>
        /// <param name="injury">The resolved injury severity.</param>
        /// <param name="beneficiary">The opposing officer receiving combat growth.</param>
        /// <param name="request">The source encounter request.</param>
        /// <param name="reactions">The result collection receiving the injury report.</param>
        private void ApplyInjury(
            Officer injured,
            int injury,
            Officer beneficiary,
            DuelRequestedResult request,
            List<GameResult> reactions
        )
        {
            if (injury <= 0)
                return;

            injured.ApplyInjury(injury, _game.Config.Recovery.MaxInjuryPoints);
            beneficiary.IncrementBaseRating(
                OfficerRating.Combat,
                _game.Config.DuelResolution.CombatReward
            );
            reactions.Add(
                Stamp(
                    new OfficerInjuredResult
                    {
                        Officer = injured,
                        Severity = injury,
                        Tick = _game.CurrentTick,
                    },
                    request
                )
            );
        }

        /// <summary>
        /// Copies the originating event ID from an encounter request to a reaction.
        /// </summary>
        /// <typeparam name="T">The emitted result type.</typeparam>
        /// <param name="reaction">The reaction to stamp.</param>
        /// <param name="request">The source encounter request.</param>
        /// <returns>The stamped reaction.</returns>
        private static T Stamp<T>(T reaction, DuelRequestedResult request)
            where T : GameResult
        {
            reaction.SourceEventInstanceID = request?.SourceEventInstanceID;
            return reaction;
        }
    }
}
