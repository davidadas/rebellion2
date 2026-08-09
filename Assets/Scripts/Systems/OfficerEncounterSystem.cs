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

        public OfficerEncounterSystem(GameRoot game, IRandomNumberProvider random)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _captureAvoidance = new ProbabilityTable(
                _game.Config.OfficerEncounter.CaptureAvoidanceTable
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

        private void Resolve(OfficerEncounterRequestedResult request, List<GameResult> reactions)
        {
            Officer encountered = request.EncounteredOfficer;
            Officer opposing = request.OpposingOfficer;
            Planet location = encountered.GetParentOfType<Planet>();
            int encounteredCombat = encountered.GetEffectiveRating(OfficerRating.Combat);
            int opposingCombat = opposing.GetEffectiveRating(OfficerRating.Combat);
            GameConfig.OfficerEncounterConfig config = _game.Config.OfficerEncounter;

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
                        VoicePath = request.VoicePath,
                        Tick = _game.CurrentTick,
                    },
                    request
                )
            );
        }

        private int TryRollInjury(int chance)
        {
            if (!RollPercent(chance))
                return 0;

            GameConfig.OfficerEncounterConfig config = _game.Config.OfficerEncounter;
            return config.InjuryBase
                + _random.NextInt(0, chance + 1)
                + _random.NextInt(0, config.InjurySecondaryRollMaximum + 1);
        }

        private bool RollPercent(int chance)
        {
            return _random.NextInt(0, 100) < Math.Min(100, Math.Max(0, chance));
        }

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
                _game.Config.OfficerEncounter.CombatReward
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

        private static T Stamp<T>(T reaction, OfficerEncounterRequestedResult request)
            where T : GameResult
        {
            reaction.SourceEventInstanceID = request.SourceEventInstanceID;
            return reaction;
        }
    }
}
