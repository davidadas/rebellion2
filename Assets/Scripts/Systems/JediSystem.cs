using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Systems.Results;

/// <summary>
/// Manages Jedi Force progression and detection during each game tick.
/// Reads officer.ForceExperience and advances ForceTier when XP crosses thresholds.
/// Performs periodic detection checks for undiscovered Force users.
/// XP accumulation is currently unimplemented (reserved for future mission integration).
/// </summary>
namespace Rebellion.Systems
{
    public class JediSystem
    {
        private readonly GameRoot game;

        /// <summary>
        /// Creates a new JediSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public JediSystem(GameRoot game)
        {
            this.game = game;
        }

        /// <summary>
        /// Processes Jedi Force tier advancement and detection checks.
        /// Effects are applied directly to officer state; returned events are for logging only.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="rng">Random number provider for detection checks.</param>
        /// <returns>List of JediResult events (tier advancements, training completions, discoveries).</returns>
        public List<JediResult> ProcessTick(GameRoot game, IRandomNumberProvider rng)
        {
            List<JediResult> events = new List<JediResult>();

            foreach (Officer officer in game.GetSceneNodesByType<Officer>())
            {
                // Skip officers with no Force potential
                if (officer.ForceExperience == 0 && officer.ForceTier == ForceTier.None)
                    continue;

                // 1. Check tier advancement
                ForceTier newTier = TierForXP(officer.ForceExperience);
                if (newTier > officer.ForceTier)
                {
                    ForceTier oldTier = officer.ForceTier;
                    officer.ForceTier = newTier;

                    events.Add(
                        new JediResult
                        {
                            EventType = JediEventType.TierAdvanced,
                            Officer = officer,
                            OldTier = oldTier,
                            NewTier = newTier,
                            Tick = game.CurrentTick,
                        }
                    );

                    // Training complete when reaching Experienced tier
                    if (newTier == ForceTier.Experienced)
                    {
                        events.Add(
                            new JediResult
                            {
                                EventType = JediEventType.TrainingComplete,
                                Officer = officer,
                                OldTier = oldTier,
                                NewTier = newTier,
                                Tick = game.CurrentTick,
                            }
                        );
                    }
                }

                // 2. Detection check (every DetectionCheckInterval ticks)
                if (
                    game.CurrentTick % game.Config.Jedi.DetectionCheckInterval == 0
                    && !officer.IsDiscoveredJedi
                    && officer.ForceTier != ForceTier.None
                )
                {
                    double detectProb = DetectionProbability(officer.ForceTier);
                    if (rng.NextDouble() < detectProb)
                    {
                        officer.IsDiscoveredJedi = true;
                        events.Add(
                            new JediResult
                            {
                                EventType = JediEventType.JediDiscovered,
                                Officer = officer,
                                OldTier = officer.ForceTier,
                                NewTier = officer.ForceTier,
                                Tick = game.CurrentTick,
                            }
                        );
                    }
                }
            }

            return events;
        }

        /// <summary>
        /// Determines Force tier for a given XP value using config thresholds.
        /// </summary>
        private ForceTier TierForXP(int xp)
        {
            if (xp >= game.Config.Jedi.XpToExperienced)
                return ForceTier.Experienced;
            if (xp >= game.Config.Jedi.XpToTraining)
                return ForceTier.Training;
            if (xp > 0)
                return ForceTier.Aware;
            return ForceTier.None;
        }

        /// <summary>
        /// Returns detection probability per check interval for a given Force tier.
        /// </summary>
        private double DetectionProbability(ForceTier tier)
        {
            switch (tier)
            {
                case ForceTier.None:
                    return 0.0;
                case ForceTier.Aware:
                    return game.Config.Jedi.DetectProbAware;
                case ForceTier.Training:
                    return game.Config.Jedi.DetectProbTraining;
                case ForceTier.Experienced:
                    return game.Config.Jedi.DetectProbExperienced;
                default:
                    return 0.0;
            }
        }
    }
}
