using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Resolves fighter replacement timing from squadron location and available facilities.
    /// </summary>
    internal static class FighterRecoveryRules
    {
        /// <summary>
        /// Returns the replacement interval currently available to a squadron.
        /// </summary>
        /// <param name="game">The active game state and recovery configuration.</param>
        /// <param name="squadron">The squadron whose replacement interval is required.</param>
        /// <param name="shipyardLocations">The indexed locations of operational shipyards.</param>
        /// <returns>The positive number of ticks between replacement pulses.</returns>
        internal static int GetReplacementInterval(
            GameRoot game,
            Starfighter squadron,
            ISet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations
        )
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (squadron == null)
                throw new ArgumentNullException(nameof(squadron));
            if (shipyardLocations == null)
                throw new ArgumentNullException(nameof(shipyardLocations));

            GameConfig.RecoveryConfig config = game.Config.Recovery;
            int interval = HasFriendlyOperationalShipyard(squadron, shipyardLocations)
                ? config.FastReplacementIntervalTicks
                : config.NormalReplacementIntervalTicks;
            return Math.Max(1, interval);
        }

        /// <summary>
        /// Builds an index of planets and owners that have an operational shipyard.
        /// </summary>
        /// <param name="game">The active game state to index.</param>
        /// <returns>The indexed shipyard locations.</returns>
        internal static HashSet<(
            string PlanetInstanceId,
            string OwnerInstanceId
        )> BuildShipyardIndex(GameRoot game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            HashSet<(string PlanetInstanceId, string OwnerInstanceId)> locations =
                new HashSet<(string PlanetInstanceId, string OwnerInstanceId)>();
            foreach (Building building in game.GetSceneNodesByType<Building>())
            {
                if (
                    building.BuildingType != BuildingType.Shipyard
                    || building.ManufacturingStatus != ManufacturingStatus.Complete
                    || building.Movement != null
                )
                    continue;

                Planet planet = building.GetParentOfType<Planet>();
                if (planet != null)
                    locations.Add((planet.InstanceID, building.OwnerInstanceID));
            }

            return locations;
        }

        /// <summary>
        /// Returns whether a stationary squadron is hosted at a friendly operational shipyard.
        /// </summary>
        /// <param name="squadron">The squadron to inspect.</param>
        /// <param name="shipyardLocations">The indexed locations of operational shipyards.</param>
        /// <returns>True when accelerated replacement is available.</returns>
        private static bool HasFriendlyOperationalShipyard(
            Starfighter squadron,
            ISet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations
        )
        {
            if (((IMovable)squadron).GetTransitMovement() != null)
                return false;

            Planet planet = squadron.GetParentOfType<Planet>();
            return planet != null
                && planet.OwnerInstanceID == squadron.OwnerInstanceID
                && shipyardLocations.Contains((planet.InstanceID, squadron.OwnerInstanceID));
        }
    }

    /// <summary>
    /// Updates fighter replacement schedules when movement changes recovery eligibility.
    /// </summary>
    internal sealed class RecoveryObserver : IResultObserver, IDisposable
    {
        private readonly GameRoot _game;
        private IDisposable _arrivalSubscription;

        /// <summary>
        /// Creates fighter recovery transition observation for one game.
        /// </summary>
        /// <param name="game">The active game state.</param>
        public RecoveryObserver(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Registers the fighter recovery transition callback.
        /// </summary>
        /// <param name="results">The bus that delivers completed arrivals.</param>
        public void Connect(GameResultBus results)
        {
            if (_arrivalSubscription != null)
                throw new InvalidOperationException("Recovery observer is already connected.");
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _arrivalSubscription = results.Subscribe<UnitArrivedResult>(HandleResults);
        }

        /// <summary>
        /// Stops receiving movement transitions.
        /// </summary>
        public void Dispose()
        {
            _arrivalSubscription?.Dispose();
            _arrivalSubscription = null;
        }

        /// <summary>
        /// Reschedules depleted squadrons from the tick on which their arrival changes eligibility.
        /// </summary>
        /// <param name="results">The completed unit arrivals.</param>
        /// <returns>No additional results because scheduling only changes recovery state.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<UnitArrivedResult> results)
        {
            Dictionary<Starfighter, int> arrivals = new Dictionary<Starfighter, int>();
            foreach (UnitArrivedResult result in results ?? Array.Empty<UnitArrivedResult>())
            {
                foreach (Starfighter squadron in GetArrivingSquadrons(result?.Unit))
                {
                    if (
                        squadron.HasLosses()
                        && squadron.ManufacturingStatus == ManufacturingStatus.Complete
                    )
                        arrivals[squadron] = result.Tick;
                }
            }

            if (arrivals.Count == 0)
                return new List<GameResult>();

            HashSet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations =
                FighterRecoveryRules.BuildShipyardIndex(_game);
            foreach ((Starfighter squadron, int arrivalTick) in arrivals)
            {
                int interval = FighterRecoveryRules.GetReplacementInterval(
                    _game,
                    squadron,
                    shipyardLocations
                );
                if (
                    squadron.NextReplacementTick <= 0
                    || squadron.ReplacementIntervalTicks != interval
                )
                    squadron.ScheduleReplacement(arrivalTick, interval);
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Enumerates depleted squadrons carried by one arriving unit.
        /// </summary>
        /// <param name="unit">The unit that completed transit.</param>
        /// <returns>The arriving fighter squadrons.</returns>
        private static IEnumerable<Starfighter> GetArrivingSquadrons(IGameEntity unit)
        {
            if (unit is Starfighter squadron)
                return new[] { squadron };
            if (unit is ContainerNode container)
                return container.GetChildren<Starfighter>(recursive: true);
            return Array.Empty<Starfighter>();
        }
    }

    /// <summary>
    /// Heals injured officers, repairs capital ships, and replenishes fighter squadrons each tick.
    /// </summary>
    internal sealed class RecoveryTickProcessor : ITickProcessor
    {
        /// <summary>
        /// Creates recovery tick processing.
        /// </summary>
        public RecoveryTickProcessor() { }

        /// <summary>
        /// Processes one tick of recovery for all officers, ships, and squadrons.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>Results emitted when a unit fully recovers.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            GameConfig.RecoveryConfig config = game.Config.Recovery;

            HealOfficers(game, config, results);
            RepairShips(game, config, results);
            ReplaceSquadronLosses(game, results);

            return results;
        }

        /// <summary>
        /// Heals each injured, non-captured officer by the configured amount.
        /// Emits a result only when the officer is fully healed.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="config">The recovery configuration.</param>
        /// <param name="results">Collection to append healing results to.</param>
        private static void HealOfficers(
            GameRoot game,
            GameConfig.RecoveryConfig config,
            List<GameResult> results
        )
        {
            foreach (Officer officer in game.GetSceneNodesByType<Officer>())
            {
                if (!officer.CanHeal())
                    continue;

                int amount = officer.HealsFast() ? config.FastHealAmount : config.NormalHealAmount;
                officer.Heal(amount);

                if (officer.InjuryPoints == 0)
                {
                    results.Add(
                        new OfficerInjuredResult
                        {
                            Officer = officer,
                            ParentAtInjury = officer.GetParent(),
                            Severity = 0,
                            Tick = game.CurrentTick,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Repairs hull damage and shield generators on each damaged capital ship.
        /// Ships at friendly orbital shipyards repair faster.
        /// Emits a result only when the ship's hull is fully repaired.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="config">The recovery configuration.</param>
        /// <param name="results">Collection to append repair results to.</param>
        private static void RepairShips(
            GameRoot game,
            GameConfig.RecoveryConfig config,
            List<GameResult> results
        )
        {
            foreach (CapitalShip ship in game.GetSceneNodesByType<CapitalShip>())
            {
                if (
                    ship.ManufacturingStatus != ManufacturingStatus.Complete
                    || ((IMovable)ship).GetTransitMovement() != null
                )
                    continue;

                // A knocked-out shield generator mends a tick at a time, twice as fast in dock.
                if (ship.ShieldGeneratorRepairDays > 0)
                    ship.RepairShieldGenerator(IsAtFriendlyShipyard(ship) ? 2 : 1);

                if (!ship.IsDamaged())
                    continue;

                int before = ship.CurrentHullStrength;
                int amount = IsAtFriendlyShipyard(ship)
                    ? config.FastRepairAmount
                    : config.NormalRepairAmount;
                ship.RepairHull(amount);

                if (!ship.IsDamaged())
                {
                    results.Add(
                        new ShipHullDamageResult
                        {
                            Ship = ship,
                            OldHull = before,
                            NewHull = ship.CurrentHullStrength,
                            Tick = game.CurrentTick,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Replaces one lost fighter when each depleted squadron's replacement timer expires.
        /// Friendly operational shipyards shorten the interval between replacement pulses.
        /// Emits a result only when the squadron is back to full strength.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="results">Collection to append replacement results to.</param>
        private static void ReplaceSquadronLosses(GameRoot game, List<GameResult> results)
        {
            List<Starfighter> squadrons = game.GetSceneNodesByType<Starfighter>();
            if (
                !squadrons.Any(squadron =>
                    squadron.HasLosses()
                    && squadron.ManufacturingStatus == ManufacturingStatus.Complete
                )
            )
            {
                foreach (Starfighter squadron in squadrons)
                    squadron.ClearReplacementSchedule();
                return;
            }

            HashSet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations =
                FighterRecoveryRules.BuildShipyardIndex(game);
            foreach (Starfighter squadron in squadrons)
            {
                if (
                    !squadron.HasLosses()
                    || squadron.ManufacturingStatus != ManufacturingStatus.Complete
                )
                {
                    squadron.ClearReplacementSchedule();
                    continue;
                }

                int interval = FighterRecoveryRules.GetReplacementInterval(
                    game,
                    squadron,
                    shipyardLocations
                );
                if (
                    squadron.NextReplacementTick <= 0
                    || squadron.ReplacementIntervalTicks != interval
                )
                {
                    squadron.ScheduleReplacement(game.CurrentTick, interval);
                    continue;
                }

                if (game.CurrentTick < squadron.NextReplacementTick)
                    continue;

                int before = squadron.CurrentSquadronSize;
                squadron.ReplaceFighters(1);

                if (!squadron.HasLosses())
                {
                    squadron.ClearReplacementSchedule();
                    results.Add(
                        new FighterDamageResult
                        {
                            Fighter = squadron,
                            OldSize = before,
                            NewSize = squadron.CurrentSquadronSize,
                            Tick = game.CurrentTick,
                        }
                    );
                }
                else
                {
                    squadron.ScheduleReplacement(game.CurrentTick, interval);
                }
            }
        }

        /// <summary>
        /// Returns true if the ship is at a friendly planet with an operational orbital shipyard.
        /// </summary>
        /// <param name="ship">The capital ship to check.</param>
        /// <returns>True when a friendly operational shipyard can accelerate repairs.</returns>
        private static bool IsAtFriendlyShipyard(CapitalShip ship)
        {
            Planet planet = ship.GetParentOfType<Planet>();
            return planet != null
                && planet.OwnerInstanceID == ship.OwnerInstanceID
                && planet
                    .GetChildren<Building>()
                    .Any(building =>
                        building.OwnerInstanceID == ship.OwnerInstanceID
                        && building.BuildingType == BuildingType.Shipyard
                        && building.ManufacturingStatus == ManufacturingStatus.Complete
                        && building.Movement == null
                    );
        }
    }
}
