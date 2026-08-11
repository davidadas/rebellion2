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
    public sealed class OfficerEncounterSystem : IGameResultHandler<OfficerEncounterRequestedResult>
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _random;
        private readonly ProbabilityTable _captureAvoidance;

        /// <summary>
        /// Creates the authoritative resolver for officer encounter requests.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="random">The deterministic simulation random source.</param>
        public OfficerEncounterSystem(GameRoot game, IRandomNumberProvider random)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _captureAvoidance = new ProbabilityTable(
                _game.Config.DuelResolution.CaptureAvoidanceTable
            );
        }

        /// <inheritdoc />
        public List<GameResult> HandleResults(
            IReadOnlyList<OfficerEncounterRequestedResult> results
        )
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            foreach (OfficerEncounterRequestedResult request in results)
            {
                if (CanResolve(request))
                    Resolve(request, reactions);
            }

            return reactions;
        }

        /// <summary>
        /// Returns whether both officers remain eligible and share a planet.
        /// </summary>
        /// <param name="request">The encounter request to validate.</param>
        /// <returns>True when authoritative resolution may proceed.</returns>
        private bool CanResolve(OfficerEncounterRequestedResult request)
        {
            Officer encountered = request?.EncounteredOfficer;
            Officer opposing = request?.OpposingOfficer;
            return encountered != null
                && opposing != null
                && encountered != opposing
                && !encountered.IsKilled
                && !opposing.IsKilled
                && !encountered.IsCaptured
                && !opposing.IsCaptured
                && encountered.OwnerInstanceID != opposing.OwnerInstanceID
                && encountered.GetParentOfType<Planet>() is Planet location
                && opposing.GetParentOfType<Planet>() == location;
        }

        /// <summary>
        /// Applies capture, injury, and advancement outcomes for one encounter.
        /// </summary>
        /// <param name="request">The validated encounter request.</param>
        /// <param name="reactions">The result collection receiving authoritative outcomes.</param>
        private void Resolve(OfficerEncounterRequestedResult request, List<GameResult> reactions)
        {
            Officer encountered = request.EncounteredOfficer;
            Officer opposing = request.OpposingOfficer;
            Planet location = encountered.GetParentOfType<Planet>();
            int encounteredCombat = encountered.GetEffectiveRating(OfficerRating.Combat);
            int opposingCombat = opposing.GetEffectiveRating(OfficerRating.Combat);
            GameConfig.DuelResolutionConfig config = _game.Config.DuelResolution;

            int avoidanceChance = _captureAvoidance.Lookup(encounteredCombat - opposingCombat);
            bool avoidedCapture = RollPercent(avoidanceChance);
            bool captured = !avoidedCapture;
            int encounteredInjury = 0;

            if (captured)
            {
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
            }
            else
            {
                encounteredInjury = TryRollInjury(
                    Math.Max(
                        config.MinimumInjuryChance,
                        config.CaptureEvasionInjuryBaseChance - encounteredCombat
                    )
                );
            }

            if (encounteredInjury == 0)
            {
                encounteredInjury = TryRollInjury(
                    Math.Max(config.MinimumInjuryChance, opposingCombat - encounteredCombat)
                );
            }

            int opposingInjury = TryRollInjury(
                Math.Max(config.MinimumInjuryChance, encounteredCombat - opposingCombat)
            );

            ApplyInjury(encountered, encounteredInjury, opposing, request, reactions);
            ApplyInjury(opposing, opposingInjury, encountered, request, reactions);

            reactions.Add(
                Stamp(
                    new OfficerEncounterResult
                    {
                        EncounteredOfficer = encountered,
                        OpposingOfficer = opposing,
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
            OfficerEncounterRequestedResult request,
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
        /// Copies event provenance from an encounter request to a reaction.
        /// </summary>
        /// <typeparam name="T">The emitted result type.</typeparam>
        /// <param name="reaction">The reaction to stamp.</param>
        /// <param name="request">The source encounter request.</param>
        /// <returns>The stamped reaction.</returns>
        private static T Stamp<T>(T reaction, OfficerEncounterRequestedResult request)
            where T : GameResult
        {
            reaction.SourceEventInstanceID = request.SourceEventInstanceID;
            return reaction;
        }
    }
}
