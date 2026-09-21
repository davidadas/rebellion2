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
            int encounteredInjury = CalculateEncounteredOfficerInjury(
                captured,
                encounteredCombat,
                opposingCombat
            );
            int opposingInjury = CalculateOpposingOfficerInjury(encounteredCombat, opposingCombat);

            ApplyInjury(encountered, encounteredInjury, opposing, sourceEventInstanceID, reactions);
            ApplyInjury(opposing, opposingInjury, encountered, sourceEventInstanceID, reactions);
            reactions.Add(
                Stamp(
                    new DuelResult
                    {
                        EncounteredOfficer = encountered,
                        OpposingOfficer = opposing,
                        Location = location,
                        EncounteredOfficerCaptured = captured,
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
        /// Resolves injury to the encountered officer after capture or successful evasion.
        /// </summary>
        /// <param name="captured">Whether captured.</param>
        /// <param name="encounteredCombat">The encountered combat.</param>
        /// <param name="opposingCombat">The opposing combat.</param>
        /// <returns>The calculated encountered officer injury.</returns>
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
        /// Applies an injury and awards the opposing officer when severity is positive.
        /// </summary>
        /// <param name="injured">The officer receiving the injury.</param>
        /// <param name="injury">The resolved injury severity.</param>
        /// <param name="beneficiary">The opposing officer receiving combat growth.</param>
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
            beneficiary.IncrementBaseRating(
                SkillRating.Combat,
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
                    sourceEventInstanceID
                )
            );
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
