using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

/// <summary>
/// Manages Force discovery state and force user scanning each tick.
/// </summary>
namespace Rebellion.Systems
{
    /// <summary>
    /// Manages Force discovery state and force user scanning each tick.
    /// </summary>
    public class JediSystem
    {
        private readonly GameRoot _game;

        public JediSystem(GameRoot game)
        {
            _game = game;
        }

        public List<GameResult> ProcessTick(IRandomNumberProvider rng)
        {
            List<GameResult> results = new List<GameResult>();

            foreach (Officer officer in _game.GetSceneNodesByType<Officer>())
            {
                if (!officer.IsJedi || !officer.IsForceEligible)
                    continue;

                RefreshDiscoveringForceUserState(officer, results);
            }

            ScanForForceUsers(rng, results);

            return results;
        }

        /// <summary>
        /// Updates discovery state based on ForceRank vs threshold.
        /// </summary>
        private void RefreshDiscoveringForceUserState(Officer officer, List<GameResult> results)
        {
            int threshold = _game.Config.Jedi.DiscoveringForceUserThreshold;
            bool shouldDiscover =
                officer.ForceRank >= threshold
                && !officer.IsCaptured
                && !officer.IsKilled
                && !officer.IsOnMission();

            if (shouldDiscover && !officer.IsDiscoveringForceUser)
            {
                officer.IsDiscoveringForceUser = true;

                results.Add(
                    new ForceDiscoveryResult
                    {
                        EventType = ForceEventType.DiscoveringForceUser,
                        Officer = officer,
                        ForceRank = officer.ForceRank,
                        Tick = _game.CurrentTick,
                    }
                );

                GameLogger.Log(
                    $"{officer.GetDisplayName()} is discovering Force abilities (rank {officer.ForceRank})"
                );
            }
            else if (!shouldDiscover && officer.IsDiscoveringForceUser)
            {
                officer.IsDiscoveringForceUser = false;
            }
        }

        /// <summary>
        /// Scans for hidden force users each tick at the scanner's location.
        /// </summary>
        private void ScanForForceUsers(IRandomNumberProvider rng, List<GameResult> results)
        {
            List<Officer> scanners = _game
                .GetSceneNodesByType<Officer>()
                .Where(o => o.IsDiscoveringForceUser)
                .ToList();

            if (scanners.Count == 0)
                return;

            int probabilityOffset = _game.Config.Jedi.EncounterProbabilityOffset;

            foreach (Officer scanner in scanners)
            {
                Planet planet = scanner.GetParentOfType<Planet>();
                if (planet == null)
                    continue;

                foreach (Officer candidate in planet.GetChildren<Officer>(_ => true, recurse: true))
                {
                    if (!IsDiscoveryCandidate(candidate))
                        continue;

                    int probability = scanner.ForceRank + candidate.ForceRank + probabilityOffset;

                    if (probability <= 0)
                        continue;

                    double roll = rng.NextDouble() * 100.0;
                    if (roll >= probability)
                        continue;

                    candidate.IsForceEligible = true;
                    candidate.ForceValue =
                        candidate.JediLevel + rng.NextInt(0, candidate.JediLevelVariance + 1);

                    results.Add(
                        new ForceDiscoveryResult
                        {
                            EventType = ForceEventType.ForceUserDiscovered,
                            Officer = candidate,
                            ForceRank = candidate.ForceRank,
                            Tick = _game.CurrentTick,
                        }
                    );

                    GameLogger.Log(
                        $"{scanner.GetDisplayName()} discovered {candidate.GetDisplayName()}'s Force potential (rank {candidate.ForceRank})"
                    );
                }
            }
        }

        private static bool IsDiscoveryCandidate(Officer officer)
        {
            return officer.IsJedi
                && !officer.IsForceEligible
                && !officer.IsCaptured
                && !officer.IsKilled
                && !officer.IsOnMission();
        }
    }
}
