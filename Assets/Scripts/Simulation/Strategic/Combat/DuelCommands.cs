using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Resolves asymmetric encounters between linked opposing officers.
    /// </summary>
    public sealed class DuelCommands
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _random;
        private readonly ProbabilityTable _captureAvoidance;

        /// <summary>
        /// Creates the authoritative resolver for officer encounter requests.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="random">The deterministic simulation random source.</param>
        public DuelCommands(GameRoot game, IRandomNumberProvider random)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _captureAvoidance = new ProbabilityTable(
                _game.Config.DuelResolution.CombatCaptureAvoidance
            );
        }

        /// <summary>
        /// Returns whether both officers remain eligible opponents at the same planet.
        /// </summary>
        /// <param name="encountered">The officer facing capture or evasion.</param>
        /// <param name="opposing">The opposing officer.</param>
        /// <returns>True when authoritative duel resolution may proceed.</returns>
        private static bool CanResolveDuel(Officer encountered, Officer opposing)
        {
            if (encountered == null || opposing == null)
                return false;
            if (encountered == opposing)
                return false;
            if (encountered.IsKilled || opposing.IsKilled)
                return false;
            if (encountered.IsCaptured || opposing.IsCaptured)
                return false;
            if (encountered.Movement != null || opposing.Movement != null)
                return false;
            if (encountered.OwnerInstanceID == opposing.OwnerInstanceID)
                return false;
            Planet location = encountered.GetParentOfType<Planet>();
            return location != null && opposing.GetParentOfType<Planet>() == location;
        }

        /// <summary>
        /// Applies capture, injury, and advancement outcomes for one encounter.
        /// </summary>
        /// <param name="encountered">The officer facing capture or evasion.</param>
        /// <param name="opposing">The opposing officer.</param>
        /// <param name="imagePath">The image accompanying the duel report.</param>
        /// <param name="audioPath">The audio accompanying the duel report.</param>
        /// <param name="sourceEventInstanceID">The authored event initiating the encounter, when applicable.</param>
        /// <returns>The ordered capture, injury, and duel results; empty when the opponents are ineligible.</returns>
        public List<GameResult> Resolve(
            Officer encountered,
            Officer opposing,
            string imagePath = null,
            string audioPath = null,
            string sourceEventInstanceID = null
        )
        {
            List<GameResult> reactions = new List<GameResult>();
            if (!CanResolveDuel(encountered, opposing))
                return reactions;

            Planet location = encountered.GetParentOfType<Planet>();
            int injuryBefore = encountered.InjuryPoints;
            int encounteredCombat = encountered.GetEffectiveRating(SkillRating.Combat);
            int opposingCombat = opposing.GetEffectiveRating(SkillRating.Combat);
            bool captured = TryCaptureEncounteredOfficer(
                encountered,
                opposing,
                location,
                encounteredCombat,
                opposingCombat,
                sourceEventInstanceID,
                reactions
            );
            int encounteredInjury = encountered.InjuryPoints - injuryBefore;
            encounteredCombat = encountered.GetEffectiveRating(SkillRating.Combat);
            opposingCombat = opposing.GetEffectiveRating(SkillRating.Combat);
            if (encounteredInjury == 0)
            {
                encounteredInjury = TryRollInjury(
                    Math.Max(
                        _game.Config.DuelResolution.MinimumInjuryChance,
                        opposingCombat - encounteredCombat
                    )
                );
                ApplyInjury(
                    encountered,
                    encounteredInjury,
                    opposing,
                    sourceEventInstanceID,
                    reactions
                );
            }
            int opposingInjury = CalculateOpposingOfficerInjury(encounteredCombat, opposingCombat);

            ApplyInjury(opposing, opposingInjury, encountered, sourceEventInstanceID, reactions);
            if (opposingInjury > 0)
                encountered.IncrementBaseRating(
                    SkillRating.Combat,
                    _game.Config.DuelResolution.CombatReward
                );
            if (encounteredInjury > 0)
                opposing.IncrementBaseRating(
                    SkillRating.Combat,
                    _game.Config.DuelResolution.CombatReward
                );
            reactions.Add(
                Stamp(
                    new DuelResult
                    {
                        EncounteredOfficer = encountered,
                        OpposingOfficer = opposing,
                        Location = location,
                        EncounteredOfficerCaptured = captured && !encountered.IsKilled,
                        EncounteredOfficerInjury = encounteredInjury,
                        OpposingOfficerInjury = opposingInjury,
                        ImagePath = imagePath,
                        AudioPath = audioPath,
                        Tick = _game.CurrentTick,
                    },
                    sourceEventInstanceID
                )
            );
            return reactions;
        }

        /// <summary>
        /// Resolves whether the encountered officer avoids capture and records capture state when
        /// the opposing officer succeeds.
        /// </summary>
        /// <param name="encountered">The encountered.</param>
        /// <param name="opposing">The opposing.</param>
        /// <param name="location">The location.</param>
        /// <param name="encounteredCombat">The encountered combat.</param>
        /// <param name="opposingCombat">The opposing combat.</param>
        /// <param name="sourceEventInstanceID">The authored event initiating the encounter, when applicable.</param>
        /// <param name="reactions">The reactions.</param>
        /// <returns>True when the encountered officer is captured; otherwise false.</returns>
        private bool TryCaptureEncounteredOfficer(
            Officer encountered,
            Officer opposing,
            Planet location,
            int encounteredCombat,
            int opposingCombat,
            string sourceEventInstanceID,
            List<GameResult> reactions
        )
        {
            int avoidanceChance = _captureAvoidance.Lookup(encounteredCombat - opposingCombat);
            bool evaded = RollPercent(avoidanceChance);
            GameConfig.DuelResolutionConfig config = _game.Config.DuelResolution;
            int injury = TryRollInjury(
                Math.Max(
                    config.MinimumInjuryChance,
                    config.CaptureEvasionInjuryBaseChance - encounteredCombat
                )
            );
            ApplyInjury(encountered, injury, opposing, sourceEventInstanceID, reactions);
            if (evaded || encountered.IsKilled)
                return false;

            if (!encountered.TryCapture(opposing.OwnerInstanceID))
                return false;

            reactions.Add(
                Stamp(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = encountered,
                        IsCaptured = true,
                        ParentAtCapture = encountered.GetParent(),
                        CapturingUnit = opposing,
                        CapturedOfficer = encountered,
                        LinkedOfficer = opposing,
                        Context = location,
                        Tick = _game.CurrentTick,
                    },
                    sourceEventInstanceID
                )
            );
            return true;
        }

        /// <summary>
        /// Resolves injury to the opposing officer from the encountered officer's combat advantage.
        /// </summary>
        /// <param name="encounteredCombat">The encountered combat.</param>
        /// <param name="opposingCombat">The opposing combat.</param>
        /// <returns>The calculated opposing officer injury.</returns>
        private int CalculateOpposingOfficerInjury(int encounteredCombat, int opposingCombat) =>
            TryRollInjury(
                Math.Max(
                    _game.Config.DuelResolution.MinimumInjuryChance,
                    encounteredCombat - opposingCombat
                )
            );

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
        /// Applies positive injury and resolves the injured minor officer's survival.
        /// </summary>
        /// <param name="injured">The officer receiving the injury.</param>
        /// <param name="injury">The resolved injury severity.</param>
        /// <param name="beneficiary">The opposing officer responsible for the injury.</param>
        /// <param name="sourceEventInstanceID">The authored event initiating the encounter, when applicable.</param>
        /// <param name="reactions">The result collection receiving the injury report.</param>
        private void ApplyInjury(
            Officer injured,
            int injury,
            Officer beneficiary,
            string sourceEventInstanceID,
            List<GameResult> reactions
        )
        {
            if (injury <= 0)
                return;

            injured.ApplyInjury(injury, _game.Config.Recovery.MaxInjuryPoints);
            reactions.Add(
                Stamp(
                    new OfficerInjuredResult
                    {
                        Officer = injured,
                        Severity = injury,
                        Tick = _game.CurrentTick,
                    },
                    sourceEventInstanceID
                )
            );
            if (!injured.IsMain && RollPercent(_game.Config.Assassination.KillProbability))
            {
                new PersonnelCommands(new PersonnelQueries(_game)).KillOfficer(injured);
                reactions.Add(
                    Stamp(
                        new OfficerKilledResult
                        {
                            TargetOfficer = injured,
                            Assassin = beneficiary,
                            Context = injured.GetParentOfType<Planet>(),
                            Tick = _game.CurrentTick,
                        },
                        sourceEventInstanceID
                    )
                );
            }
        }

        /// <summary>
        /// Assigns the originating event ID to a reaction.
        /// </summary>
        /// <typeparam name="T">The emitted result type.</typeparam>
        /// <param name="reaction">The reaction to stamp.</param>
        /// <param name="sourceEventInstanceID">The authored event initiating the encounter, when applicable.</param>
        /// <returns>The stamped reaction.</returns>
        private static T Stamp<T>(T reaction, string sourceEventInstanceID)
            where T : GameResult
        {
            reaction.SourceEventInstanceID = sourceEventInstanceID;
            return reaction;
        }
    }
}
