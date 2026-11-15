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
    /// Resolves locations that can accelerate unit recovery.
    /// </summary>
    internal static class RecoveryLocationRules
    {
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
        /// Returns whether a stationary unit is hosted at a friendly operational shipyard.
        /// </summary>
        /// <param name="unit">The unit to inspect.</param>
        /// <param name="movable">The unit movement contract.</param>
        /// <param name="shipyardLocations">The indexed locations of operational shipyards.</param>
        /// <returns>True when accelerated recovery is available.</returns>
        internal static bool HasFriendlyOperationalShipyard(
            ISceneNode unit,
            IMovable movable,
            ISet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations
        )
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (movable == null)
                throw new ArgumentNullException(nameof(movable));
            if (shipyardLocations == null)
                throw new ArgumentNullException(nameof(shipyardLocations));
            if (movable.GetTransitMovement() != null)
                return false;

            Planet planet = unit.GetParentOfType<Planet>();
            return planet != null
                && planet.OwnerInstanceID == unit.OwnerInstanceID
                && shipyardLocations.Contains((planet.InstanceID, unit.OwnerInstanceID));
        }
    }

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
            int interval = RecoveryLocationRules.HasFriendlyOperationalShipyard(
                squadron,
                squadron,
                shipyardLocations
            )
                ? config.FastReplacementIntervalTicks
                : config.NormalReplacementIntervalTicks;
            return Math.Max(1, interval);
        }
    }

    /// <summary>
    /// Resolves capital-ship repair timing and pulse strength.
    /// </summary>
    internal static class ShipRecoveryRules
    {
        private const int _repairPulseDivisor = 10;

        /// <summary>
        /// Returns the repair interval currently available to a capital ship.
        /// </summary>
        /// <param name="game">The active game state and recovery configuration.</param>
        /// <param name="ship">The capital ship whose repair interval is required.</param>
        /// <param name="shipyardLocations">The indexed locations of operational shipyards.</param>
        /// <returns>The positive number of ticks between repair pulses.</returns>
        internal static int GetRepairInterval(
            GameRoot game,
            CapitalShip ship,
            ISet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations
        )
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (ship == null)
                throw new ArgumentNullException(nameof(ship));
            if (shipyardLocations == null)
                throw new ArgumentNullException(nameof(shipyardLocations));

            GameConfig.RecoveryConfig config = game.Config.Recovery;
            bool repairsFast = RecoveryLocationRules.HasFriendlyOperationalShipyard(
                ship,
                ship,
                shipyardLocations
            );
            int interval = repairsFast
                ? config.FastRepairIntervalTicks
                : config.NormalRepairIntervalTicks;
            if (interval <= 0)
            {
                interval = repairsFast
                    ? config.FastReplacementIntervalTicks
                    : config.NormalReplacementIntervalTicks;
            }
            return Math.Max(1, interval);
        }

        /// <summary>
        /// Returns the hull restored by one repair pulse.
        /// </summary>
        /// <param name="ship">The capital ship being repaired.</param>
        /// <returns>One tenth of the ship's maximum hull strength, with at least one point restored.</returns>
        internal static int GetRepairAmount(CapitalShip ship)
        {
            if (ship == null)
                throw new ArgumentNullException(nameof(ship));
            return Math.Max(1, ship.MaxHullStrength / _repairPulseDivisor);
        }
    }

    /// <summary>
    /// Updates recovery schedules when movement changes shipyard eligibility.
    /// </summary>
    internal sealed class RecoveryObserver : IResultObserver, IDisposable
    {
        private readonly GameRoot _game;
        private IDisposable _arrivalSubscription;

        /// <summary>
        /// Creates recovery transition observation for one game.
        /// </summary>
        /// <param name="game">The active game state.</param>
        public RecoveryObserver(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Registers the recovery transition callback.
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
        /// Reschedules damaged ships and depleted squadrons when arrival changes eligibility.
        /// </summary>
        /// <param name="results">The completed unit arrivals.</param>
        /// <returns>No additional results because scheduling only changes recovery state.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<UnitArrivedResult> results)
        {
            Dictionary<Starfighter, int> fighterArrivals = new Dictionary<Starfighter, int>();
            Dictionary<CapitalShip, int> shipArrivals = new Dictionary<CapitalShip, int>();
            foreach (UnitArrivedResult result in results ?? Array.Empty<UnitArrivedResult>())
            {
                foreach (Starfighter squadron in GetArrivingSquadrons(result?.Unit))
                {
                    if (
                        squadron.HasLosses()
                        && squadron.ManufacturingStatus == ManufacturingStatus.Complete
                    )
                        fighterArrivals[squadron] = result.Tick;
                }

                foreach (CapitalShip ship in GetArrivingShips(result?.Unit))
                {
                    if (
                        ship.IsDamaged()
                        && ship.ManufacturingStatus == ManufacturingStatus.Complete
                    )
                        shipArrivals[ship] = result.Tick;
                }
            }

            if (fighterArrivals.Count == 0 && shipArrivals.Count == 0)
                return new List<GameResult>();

            HashSet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations =
                RecoveryLocationRules.BuildShipyardIndex(_game);
            foreach ((Starfighter squadron, int arrivalTick) in fighterArrivals)
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

            foreach ((CapitalShip ship, int arrivalTick) in shipArrivals)
            {
                int interval = ShipRecoveryRules.GetRepairInterval(_game, ship, shipyardLocations);
                if (ship.NextRepairTick <= 0 || ship.RepairIntervalTicks != interval)
                    ship.ScheduleRepair(arrivalTick, interval);
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

        /// <summary>
        /// Enumerates damaged capital ships contained by one arriving unit.
        /// </summary>
        /// <param name="unit">The unit that completed transit.</param>
        /// <returns>The arriving capital ships.</returns>
        private static IEnumerable<CapitalShip> GetArrivingShips(IGameEntity unit)
        {
            if (unit is CapitalShip ship)
                return new[] { ship };
            if (unit is ContainerNode container)
                return container.GetChildren<CapitalShip>(recursive: true);
            return Array.Empty<CapitalShip>();
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
            HashSet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations = null;

            HealOfficers(game, config, results);
            RepairShips(game, results, ref shipyardLocations);
            ReplaceSquadronLosses(game, results, ref shipyardLocations);

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
        /// Repairs hull damage when each damaged capital ship's repair timer expires.
        /// Friendly operational shipyards shorten the interval between repair pulses.
        /// Emits a result only when the ship is fully repaired.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="results">Collection to append repair results to.</param>
        /// <param name="shipyardLocations">The lazily built shipyard index shared by recovery phases.</param>
        private static void RepairShips(
            GameRoot game,
            List<GameResult> results,
            ref HashSet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations
        )
        {
            List<CapitalShip> ships = game.GetSceneNodesByType<CapitalShip>();
            if (
                !ships.Any(ship =>
                    ship.IsDamaged()
                    && ship.ManufacturingStatus == ManufacturingStatus.Complete
                    && ((IMovable)ship).GetTransitMovement() == null
                )
            )
            {
                foreach (CapitalShip ship in ships)
                    ship.ClearRepairSchedule();
                return;
            }

            shipyardLocations ??= RecoveryLocationRules.BuildShipyardIndex(game);
            foreach (CapitalShip ship in ships)
            {
                if (
                    !ship.IsDamaged()
                    || ship.ManufacturingStatus != ManufacturingStatus.Complete
                    || ((IMovable)ship).GetTransitMovement() != null
                )
                {
                    ship.ClearRepairSchedule();
                    continue;
                }

                int interval = ShipRecoveryRules.GetRepairInterval(game, ship, shipyardLocations);
                if (ship.NextRepairTick <= 0 || ship.RepairIntervalTicks != interval)
                {
                    ship.ScheduleRepair(game.CurrentTick, interval);
                    continue;
                }

                if (game.CurrentTick < ship.NextRepairTick)
                    continue;

                int before = ship.CurrentHullStrength;
                ship.RepairHull(ShipRecoveryRules.GetRepairAmount(ship));

                if (!ship.IsDamaged())
                {
                    ship.ClearRepairSchedule();
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
                else
                {
                    ship.ScheduleRepair(game.CurrentTick, interval);
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
        /// <param name="shipyardLocations">The lazily built shipyard index shared by recovery phases.</param>
        private static void ReplaceSquadronLosses(
            GameRoot game,
            List<GameResult> results,
            ref HashSet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations
        )
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

            shipyardLocations ??= RecoveryLocationRules.BuildShipyardIndex(game);
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
    }
}
