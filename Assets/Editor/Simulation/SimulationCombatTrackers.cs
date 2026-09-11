using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Planners.Demand;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

public static partial class HeadlessSimulationRunner
{
    /// <summary>
    /// Records the existing bombardment results that prove the final defending garrison was
    /// removed and the sector-wide support shift was applied. This is event-backed and performs
    /// no scene-graph polling.
    /// </summary>
    private sealed class GarrisonRemovalBombardmentTracker
    {
        private readonly int _supportShift;
        private readonly Dictionary<
            string,
            List<GarrisonRemovalBombardmentSimulationResult>
        > _results = new(StringComparer.Ordinal);

        public GarrisonRemovalBombardmentTracker(GameRoot game)
        {
            _supportShift = game.Config.SupportShift.GarrisonRemovalSupportShift;
        }

        public void Record(IReadOnlyList<GameResult> results)
        {
            if (results == null)
                return;

            foreach (BombardmentResult result in results.OfType<BombardmentResult>())
            {
                PlanetOwnershipChangedResult directChange = result.OwnershipChange;
                if (
                    directChange?.Planet == null
                    || result.DestroyedRegiments == null
                    || result.DestroyedRegiments.Count == 0
                )
                    continue;

                string factionId = result.AttackerOwnerInstanceID ?? string.Empty;
                if (
                    !_results.TryGetValue(
                        factionId,
                        out List<GarrisonRemovalBombardmentSimulationResult> items
                    )
                )
                {
                    items = new List<GarrisonRemovalBombardmentSimulationResult>();
                    _results[factionId] = items;
                }

                string[] additionalFlips = result
                    .Events.OfType<PlanetOwnershipChangedResult>()
                    .Where(change => change.Planet != null && change.Planet != directChange.Planet)
                    .Select(change =>
                        $"{change.Planet.InstanceID}:{change.Planet.GetDisplayName()}"
                    )
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                items.Add(
                    new GarrisonRemovalBombardmentSimulationResult
                    {
                        Tick = result.Tick,
                        AttackerFactionId = factionId,
                        PlanetId = directChange.Planet.InstanceID,
                        PlanetName = directChange.Planet.GetDisplayName(),
                        PreviousOwnerFactionId = directChange.PreviousOwner?.InstanceID,
                        NewOwnerFactionId = directChange.NewOwner?.InstanceID,
                        AdditionalFlippedPlanets = additionalFlips,
                    }
                );
            }
        }

        public GarrisonRemovalBombardmentSimulationSummary BuildSummary(string factionId)
        {
            GarrisonRemovalBombardmentSimulationResult[] results = _results.TryGetValue(
                factionId ?? string.Empty,
                out List<GarrisonRemovalBombardmentSimulationResult> items
            )
                ? items.ToArray()
                : Array.Empty<GarrisonRemovalBombardmentSimulationResult>();
            return new GarrisonRemovalBombardmentSimulationSummary
            {
                Triggered = results.Length,
                SupportShiftPerAffectedPlanet = _supportShift,
                AdditionalPlanetsFlipped = results.Sum(result =>
                    result.AdditionalFlippedPlanets?.Length ?? 0
                ),
                Results = results,
            };
        }
    }

    private sealed class PlanetaryAssaultTracker
    {
        private readonly GameRoot _game;
        private readonly Dictionary<string, List<PlanetaryAssaultSimulationResult>> _results = new(
            StringComparer.Ordinal
        );

        /// <summary>
        /// Creates an assault tracker for the simulated game.
        /// </summary>
        /// <param name="game">The simulated game.</param>
        public PlanetaryAssaultTracker(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Records resolved planetary assaults.
        /// </summary>
        /// <param name="results">The resolved assault results.</param>
        public void Record(IReadOnlyList<GameResult> results)
        {
            if (results == null)
                return;

            HashSet<Planet> uprisingPlanets = results
                .OfType<PlanetUprisingStartedResult>()
                .Select(result => result.Planet)
                .Where(planet => planet != null)
                .ToHashSet();
            foreach (PlanetaryAssaultResult result in results.OfType<PlanetaryAssaultResult>())
            {
                string factionId = result.AttackerOwnerInstanceID ?? string.Empty;
                int requiredGarrison = GetRequiredGarrison(result);
                bool immediateUprising =
                    result.Success
                    && result.Planet != null
                    && uprisingPlanets.Contains(result.Planet);
                if (
                    !_results.TryGetValue(
                        factionId,
                        out List<PlanetaryAssaultSimulationResult> items
                    )
                )
                {
                    items = new List<PlanetaryAssaultSimulationResult>();
                    _results[factionId] = items;
                }

                items.Add(
                    new PlanetaryAssaultSimulationResult
                    {
                        Tick = result.Tick,
                        PlanetId = result.Planet?.InstanceID,
                        PlanetName = result.Planet?.GetDisplayName(),
                        Success = result.Success,
                        InitialAttackerRegimentCount = result.InitialAttackerRegimentCount,
                        RemainingAttackerRegimentCount = result.RemainingAttackerRegimentCount,
                        InitialDefenderRegimentCount = result.InitialDefenderRegimentCount,
                        RemainingDefenderRegimentCount = result.RemainingDefenderRegimentCount,
                        ImmediateUprising = immediateUprising,
                        RequiredGarrisonCount = requiredGarrison,
                        GarrisonDeficit = Math.Max(
                            0,
                            requiredGarrison - result.RemainingAttackerRegimentCount
                        ),
                    }
                );
            }
        }

        /// <summary>
        /// Calculates the post-assault garrison required to keep the captured planet stable.
        /// </summary>
        /// <param name="result">The resolved assault.</param>
        /// <returns>The required regiment count, or zero when the assault did not capture a planet.</returns>
        private int GetRequiredGarrison(PlanetaryAssaultResult result)
        {
            if (!result.Success || result.Planet == null || result.AttackingFaction == null)
                return 0;

            int requirement = UprisingSystem.CalculateGarrisonRequirement(
                result.Planet,
                result.AttackingFaction,
                _game.Config.AI.Garrison
            );
            int uprisingMultiplier = _game.Config.AI.Garrison.UprisingMultiplier;
            return result.Planet.IsInUprising && uprisingMultiplier > 1
                ? requirement / uprisingMultiplier
                : requirement;
        }

        /// <summary>
        /// Builds the assault summary for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The faction's assault summary.</returns>
        public PlanetaryAssaultSimulationSummary BuildSummary(string factionId)
        {
            PlanetaryAssaultSimulationResult[] results = _results.TryGetValue(
                factionId ?? string.Empty,
                out List<PlanetaryAssaultSimulationResult> items
            )
                ? items.ToArray()
                : Array.Empty<PlanetaryAssaultSimulationResult>();

            return new PlanetaryAssaultSimulationSummary
            {
                Attempted = results.Length,
                Succeeded = results.Count(result => result.Success),
                Failed = results.Count(result => !result.Success),
                ImmediateUprisings = results.Count(result => result.ImmediateUprising),
                Results = results,
            };
        }
    }
}
