using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

public static partial class HeadlessSimulationRunner
{
    /// <summary>
    /// Records event-backed force ratios and losses from resolved space battles.
    /// </summary>
    private sealed class SpaceCombatCalibrationTracker
    {
        private readonly Dictionary<string, List<SpaceCombatCalibrationResult>> _results = new(
            StringComparer.Ordinal
        );

        /// <summary>
        /// Records completed space-combat results without querying the scene graph.
        /// </summary>
        /// <param name="results">The resolved game results.</param>
        public void Record(IReadOnlyList<GameResult> results)
        {
            foreach (
                SpaceCombatResult result in results?.OfType<SpaceCombatResult>()
                    ?? Enumerable.Empty<SpaceCombatResult>()
            )
            {
                CombatStrengthSnapshot attacker = CalculateStrength(
                    result.AttackingUnits,
                    result.ShipDamage,
                    result.FighterLosses
                );
                CombatStrengthSnapshot defender = CalculateStrength(
                    result.DefendingUnits,
                    result.ShipDamage,
                    result.FighterLosses
                );
                string factionId = result.AttackerOwnerInstanceID ?? string.Empty;
                if (!_results.TryGetValue(factionId, out List<SpaceCombatCalibrationResult> items))
                {
                    items = new List<SpaceCombatCalibrationResult>();
                    _results[factionId] = items;
                }

                items.Add(
                    new SpaceCombatCalibrationResult
                    {
                        Tick = result.Tick,
                        PlanetId = result.Planet?.InstanceID,
                        PlanetName = result.Planet?.GetDisplayName(),
                        AttackerInitialCombat = attacker.Initial,
                        DefenderInitialCombat = defender.Initial,
                        AttackerSurvivingCombat = attacker.Surviving,
                        DefenderSurvivingCombat = defender.Surviving,
                        AttackerToDefenderRatio =
                            defender.Initial > 0
                                ? attacker.Initial / (double)defender.Initial
                                : double.PositiveInfinity,
                        AttackerLossPercent = GetLossPercent(attacker),
                        DefenderLossPercent = GetLossPercent(defender),
                        AttackerWon = result.Winner == CombatSide.Attacker,
                        AttackerOutcome = result.AttackerOutcome.ToString(),
                        DefenderOutcome = result.DefenderOutcome.ToString(),
                    }
                );
            }
        }

        /// <summary>
        /// Builds the recorded calibration results for an attacking faction.
        /// </summary>
        /// <param name="factionId">The attacking faction identifier.</param>
        /// <returns>The faction's resolved space combats.</returns>
        public SpaceCombatCalibrationSummary BuildSummary(string factionId)
        {
            return new SpaceCombatCalibrationSummary
            {
                Results = _results.TryGetValue(
                    factionId ?? string.Empty,
                    out List<SpaceCombatCalibrationResult> results
                )
                    ? results.ToArray()
                    : Array.Empty<SpaceCombatCalibrationResult>(),
            };
        }

        /// <summary>
        /// Calculates initial and surviving strategic combat values for one combat side.
        /// </summary>
        /// <param name="units">Detached pre-combat unit snapshots.</param>
        /// <param name="shipDamage">Resolved capital-ship hull changes.</param>
        /// <param name="fighterLosses">Resolved fighter squadron losses.</param>
        /// <returns>The initial and surviving combat values.</returns>
        private static CombatStrengthSnapshot CalculateStrength(
            IReadOnlyList<CombatUnitSnapshot> units,
            IReadOnlyList<ShipDamageResult> shipDamage,
            IReadOnlyList<FighterLossResult> fighterLosses
        )
        {
            Dictionary<string, int> survivingHull = (shipDamage ?? Array.Empty<ShipDamageResult>())
                .Where(damage => damage?.Ship != null)
                .ToDictionary(damage => damage.Ship.InstanceID, damage => damage.HullAfter);
            Dictionary<string, int> survivingFighters = (
                fighterLosses ?? Array.Empty<FighterLossResult>()
            )
                .Where(loss => loss?.Fighter != null)
                .ToDictionary(loss => loss.Fighter.InstanceID, loss => loss.SquadsAfter);
            int initial = 0;
            int surviving = 0;
            foreach (CombatUnitSnapshot snapshot in units ?? Array.Empty<CombatUnitSnapshot>())
            {
                if (snapshot?.WasOperational != true)
                    continue;

                if (snapshot.Unit is CapitalShip ship)
                {
                    initial += CalculateCapitalShipCombatValue(ship, ship.CurrentHullStrength);
                    int hull =
                        snapshot.Destroyed ? 0
                        : survivingHull.TryGetValue(ship.InstanceID, out int hullAfter) ? hullAfter
                        : ship.CurrentHullStrength;
                    surviving += CalculateCapitalShipCombatValue(ship, hull);
                }
                else if (snapshot.Unit is Starfighter fighter)
                {
                    initial += CalculateStarfighterCombatValue(
                        fighter,
                        fighter.CurrentSquadronSize
                    );
                    int count =
                        snapshot.Destroyed ? 0
                        : survivingFighters.TryGetValue(fighter.InstanceID, out int countAfter)
                            ? countAfter
                        : fighter.CurrentSquadronSize;
                    surviving += CalculateStarfighterCombatValue(fighter, count);
                }
            }

            return new CombatStrengthSnapshot(initial, surviving);
        }

        /// <summary>
        /// Calculates strategic combat value for a capital ship at a specified hull strength.
        /// </summary>
        /// <param name="ship">The capital ship definition.</param>
        /// <param name="hullStrength">The hull strength to evaluate.</param>
        /// <returns>The strategic combat value.</returns>
        private static int CalculateCapitalShipCombatValue(CapitalShip ship, int hullStrength)
        {
            int attackStrength = ship?.GetPrimaryWeaponStrength() ?? 0;
            long durability =
                Math.Max(0L, hullStrength) + Math.Max(0L, ship?.MaxShieldStrength ?? 0);
            if (attackStrength <= 0 || durability <= 0)
                return attackStrength;

            double value = Math.Sqrt(attackStrength * (double)durability);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        /// <summary>
        /// Calculates strategic combat value for a fighter squadron at a specified size.
        /// </summary>
        /// <param name="fighter">The fighter definition.</param>
        /// <param name="squadronSize">The surviving fighter count.</param>
        /// <returns>The strategic combat value.</returns>
        private static int CalculateStarfighterCombatValue(Starfighter fighter, int squadronSize)
        {
            return fighter == null ? 0 : fighter.GetWeaponStrength() * Math.Max(0, squadronSize);
        }

        /// <summary>
        /// Returns the percentage of initial combat value lost.
        /// </summary>
        /// <param name="strength">The initial and surviving strength.</param>
        /// <returns>The loss percentage from zero through one hundred.</returns>
        private static double GetLossPercent(CombatStrengthSnapshot strength)
        {
            return strength.Initial > 0
                ? 100 * (1 - strength.Surviving / (double)strength.Initial)
                : 0;
        }

        private readonly struct CombatStrengthSnapshot
        {
            internal int Initial { get; }
            internal int Surviving { get; }

            /// <summary>
            /// Creates a combat-strength snapshot.
            /// </summary>
            /// <param name="initial">The initial strength.</param>
            /// <param name="surviving">The surviving strength.</param>
            internal CombatStrengthSnapshot(int initial, int surviving)
            {
                Initial = initial;
                Surviving = surviving;
            }
        }
    }

    [Serializable]
    private sealed class SpaceCombatCalibrationSummary
    {
        public SpaceCombatCalibrationResult[] Results;
    }

    [Serializable]
    private sealed class SpaceCombatCalibrationResult
    {
        public int Tick;
        public string PlanetId;
        public string PlanetName;
        public int AttackerInitialCombat;
        public int DefenderInitialCombat;
        public int AttackerSurvivingCombat;
        public int DefenderSurvivingCombat;
        public double AttackerToDefenderRatio;
        public double AttackerLossPercent;
        public double DefenderLossPercent;
        public bool AttackerWon;
        public string AttackerOutcome;
        public string DefenderOutcome;
    }

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

        /// <summary>
        /// Initializes a new instance of the GarrisonRemovalBombardmentTracker class.
        /// </summary>
        /// <param name="game">The game.</param>
        public GarrisonRemovalBombardmentTracker(GameRoot game)
        {
            _supportShift = game.Config.SupportShift.GarrisonRemovalSupportShift;
        }

        /// <summary>
        /// Executes record.
        /// </summary>
        /// <param name="results">The results.</param>
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

        /// <summary>
        /// Builds summary.
        /// </summary>
        /// <param name="factionId">The faction id.</param>
        /// <returns>The constructed summary.</returns>
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
                        LandedRegimentCount = result.LandedRegiments.Count,
                        AttackingCombatValue = GetAttackingCombatValue(result),
                        AttackingCapitalShipCount = CountOperationalUnits<CapitalShip>(result),
                        AttackingStarfighterCount = CountOperationalUnits<Starfighter>(result),
                        ImmediateUprising = immediateUprising,
                        RequiredGarrisonCount = requiredGarrison,
                        GarrisonDeficit = Math.Max(
                            0,
                            requiredGarrison - result.LandedRegiments.Count
                        ),
                    }
                );
            }
        }

        /// <summary>
        /// Calculates the strategic combat value of the operational fleet units that initiated
        /// an assault.
        /// </summary>
        /// <param name="result">The resolved planetary assault.</param>
        /// <returns>The attacking fleet's combat value when the assault began.</returns>
        private static int GetAttackingCombatValue(PlanetaryAssaultResult result)
        {
            return result
                .AttackingUnits.Where(snapshot => snapshot.WasOperational)
                .Sum(snapshot =>
                    snapshot.Unit switch
                    {
                        CapitalShip ship => ship.GetCombatValue(),
                        Starfighter fighter when fighter.MaxSquadronSize > 0 =>
                            fighter.GetWeaponStrength()
                                * fighter.CurrentSquadronSize
                                / fighter.MaxSquadronSize,
                        Starfighter fighter => fighter.GetWeaponStrength(),
                        _ => 0,
                    }
                );
        }

        /// <summary>
        /// Counts operational attacking units of one type at the start of an assault.
        /// </summary>
        /// <typeparam name="TUnit">The unit type to count.</typeparam>
        /// <param name="result">The resolved planetary assault.</param>
        /// <returns>The operational attacking-unit count.</returns>
        private static int CountOperationalUnits<TUnit>(PlanetaryAssaultResult result)
            where TUnit : class
        {
            return result.AttackingUnits.Count(snapshot =>
                snapshot.WasOperational && snapshot.Unit is TUnit
            );
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
