using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

/// <summary>
/// Processes Jedi Force tier advancement and detection checks each tick.
///
/// TIER ADVANCEMENT:
/// Reads officer.ForceExperience and promotes ForceTier when XP crosses config thresholds.
/// XP accumulation is unimplemented - reserved for future mission integration.
///
/// DETECTION:
/// Every DetectionCheckInterval ticks, undiscovered Force users roll against a per-tier
/// probability. A successful roll sets IsDiscoveredJedi = true.
/// </summary>
namespace Rebellion.Systems
{
    public class JediSystem
    {
        private readonly GameRoot game;

        public JediSystem(GameRoot game)
        {
            this.game = game;
        }

        /// <summary>
        /// Processes Force tier advancement and detection for all officers.
        /// Logs each event, applies effects directly to officer state, and returns all results.
        /// </summary>
        public List<GameResult> ProcessTick(GameRoot game, IRandomNumberProvider rng)
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

            foreach (JediResult result in events)
            {
                GameLogger.Log(
                    $"{result.Officer.GetDisplayName()} {result.EventType}: {result.NewTier}"
                );
            }

            return new List<GameResult>(events);
        }

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

        private double DetectionProbability(ForceTier tier) =>
            tier switch
            {
                ForceTier.Aware => game.Config.Jedi.DetectProbAware,
                ForceTier.Training => game.Config.Jedi.DetectProbTraining,
                ForceTier.Experienced => game.Config.Jedi.DetectProbExperienced,
                _ => 0.0,
            };
    }
}
