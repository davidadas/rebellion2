using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Maintains system smuggling and redirects stolen resource output to its beneficiary.
    /// </summary>
    public sealed class SmugglingSystem
    {
        private const int _percentScale = 100;

        private readonly GameRoot _game;
        private readonly ProbabilityTable _lossPercentByMinimumSupport;
        private readonly Dictionary<string, PlanetSmugglingState> _states = new Dictionary<
            string,
            PlanetSmugglingState
        >(StringComparer.Ordinal);

        private sealed class PlanetSmugglingState
        {
            public int DiversionPercent { get; set; }
            public string ControllerInstanceID { get; set; }
            public string BeneficiaryInstanceID { get; set; }
        }

        /// <summary>
        /// Creates a smuggling system for the supplied game.
        /// </summary>
        /// <param name="game">The current game state.</param>
        public SmugglingSystem(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            EnsureConfigIsValid(_game.Config?.Smuggling);
            _lossPercentByMinimumSupport = new ProbabilityTable(
                _game.Config.Smuggling.LossPercentByMinimumSupport
            );
            InitializeStates();
        }

        private static void EnsureConfigIsValid(GameConfig.SmugglingConfig config)
        {
            if (config == null)
                throw new InvalidOperationException("Smuggling configuration is required.");
            if (
                config.LossPercentByMinimumSupport == null
                || config.LossPercentByMinimumSupport.Count == 0
            )
                throw new InvalidOperationException(
                    "Smuggling requires at least one support-loss threshold."
                );
            if (
                config.LossPercentByMinimumSupport.Any(entry =>
                    entry.Key is < 0 or > _percentScale || entry.Value is < 0 or > _percentScale
                )
            )
                throw new InvalidOperationException(
                    "Smuggling loss percentages must be between zero and 100."
                );
            if (
                config.CapitalShipSuppression < 0
                || config.StarfighterSuppression < 0
                || config.RegimentSuppression < 0
            )
                throw new InvalidOperationException(
                    "Smuggling suppression values cannot be negative."
                );
        }

        /// <summary>
        /// Rebuilds derived smuggling state without emitting start notifications after loading.
        /// </summary>
        private void InitializeStates()
        {
            foreach (
                Planet planet in _game
                    .GetGalaxyMap()
                    .PlanetSectors.SelectMany(sector => sector.Planets)
            )
            {
                Faction controller = FindFaction(planet.OwnerInstanceID);
                Faction beneficiary = FindBeneficiary(planet, controller);
                int percent = CalculatePercent(planet, controller, beneficiary);
                if (percent <= 0)
                    continue;

                _states[planet.InstanceID] = new PlanetSmugglingState
                {
                    DiversionPercent = percent,
                    ControllerInstanceID = controller.InstanceID,
                    BeneficiaryInstanceID = beneficiary.InstanceID,
                };
            }
        }

        /// <summary>
        /// Recomputes the support- and garrison-driven smuggling percentage.
        /// </summary>
        /// <returns>Results for percentages that changed during this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            foreach (
                Planet planet in _game
                    .GetGalaxyMap()
                    .PlanetSectors.SelectMany(sector => sector.Planets)
                    .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                RefreshPlanet(planet, results);
            }

            return results;
        }

        /// <summary>
        /// Applies the per-resource smuggling roll to completed output.
        /// </summary>
        /// <param name="controller">The faction that ordinarily receives production.</param>
        /// <param name="facility">The facility producing the resource.</param>
        /// <returns>The controlling faction or the faction benefiting from smuggling.</returns>
        public Faction ResolveProductionRecipient(Faction controller, Building facility)
        {
            Planet planet = facility.GetParentOfType<Planet>();
            if (
                planet == null
                || !_states.TryGetValue(planet.InstanceID, out PlanetSmugglingState state)
                || state.DiversionPercent <= 0
                || _game.Random.NextInt(0, _percentScale) >= state.DiversionPercent
            )
                return controller;

            return FindFaction(state.BeneficiaryInstanceID) ?? controller;
        }

        /// <summary>
        /// Reconciles one planet's smuggling state and emits changes.
        /// </summary>
        /// <param name="planet">The planet to reconcile.</param>
        /// <param name="results">The result collection receiving changes.</param>
        private void RefreshPlanet(Planet planet, List<GameResult> results)
        {
            _states.TryGetValue(planet.InstanceID, out PlanetSmugglingState previous);
            PlanetSmugglingState current = CalculateSmugglingState(planet);

            if (!IsSameRelationship(previous, current))
            {
                if (previous != null)
                    RecordSmugglingEnded(results, planet, previous);
                if (current != null)
                    RecordSmugglingStarted(results, planet, current);
            }
            else if (previous != null && previous.DiversionPercent != current.DiversionPercent)
            {
                RecordDiversionChanged(
                    results,
                    planet,
                    current,
                    previous.DiversionPercent,
                    current.DiversionPercent
                );
            }

            if (current == null)
                _states.Remove(planet.InstanceID);
            else
                _states[planet.InstanceID] = current;
        }

        /// <summary>
        /// Calculates the current smuggling relationship for a planet, or null when no output is diverted.
        /// </summary>
        private PlanetSmugglingState CalculateSmugglingState(Planet planet)
        {
            Faction controller = FindFaction(planet.OwnerInstanceID);
            Faction beneficiary = FindBeneficiary(planet, controller);
            int diversionPercent = CalculatePercent(planet, controller, beneficiary);
            return diversionPercent <= 0
                ? null
                : new PlanetSmugglingState
                {
                    DiversionPercent = diversionPercent,
                    ControllerInstanceID = controller.InstanceID,
                    BeneficiaryInstanceID = beneficiary.InstanceID,
                };
        }

        /// <summary>
        /// Returns whether two states describe smuggling between the same controlling and beneficiary factions.
        /// </summary>
        private static bool IsSameRelationship(
            PlanetSmugglingState left,
            PlanetSmugglingState right
        ) =>
            left == null && right == null
            || left != null
                && right != null
                && left.ControllerInstanceID == right.ControllerInstanceID
                && left.BeneficiaryInstanceID == right.BeneficiaryInstanceID;

        /// <summary>
        /// Records the beginning of resource diversion for a controller and beneficiary.
        /// </summary>
        private void RecordSmugglingStarted(
            List<GameResult> results,
            Planet planet,
            PlanetSmugglingState state
        )
        {
            RecordDiversionChanged(results, planet, state, 0, state.DiversionPercent);
            results.Add(
                new SmugglingChangedResult
                {
                    Planet = planet,
                    Controller = FindFaction(state.ControllerInstanceID),
                    Beneficiary = FindFaction(state.BeneficiaryInstanceID),
                    OldPercent = 0,
                    NewPercent = state.DiversionPercent,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Records the end of resource diversion for a controller and beneficiary.
        /// </summary>
        private void RecordSmugglingEnded(
            List<GameResult> results,
            Planet planet,
            PlanetSmugglingState state
        )
        {
            RecordDiversionChanged(results, planet, state, state.DiversionPercent, 0);
            results.Add(
                new SmugglingChangedResult
                {
                    Planet = planet,
                    Controller = FindFaction(state.ControllerInstanceID),
                    Beneficiary = FindFaction(state.BeneficiaryInstanceID),
                    OldPercent = state.DiversionPercent,
                    NewPercent = 0,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Records a numeric diversion change without implying that the smuggling relationship changed.
        /// </summary>
        private void RecordDiversionChanged(
            ICollection<GameResult> results,
            Planet planet,
            PlanetSmugglingState state,
            int oldPercent,
            int newPercent
        )
        {
            results.Add(
                new PlanetStatChangedResult
                {
                    Planet = planet,
                    Faction = FindFaction(state.ControllerInstanceID),
                    Category = PlanetChangeCategory.Smuggling,
                    OldValue = oldPercent,
                    NewValue = newPercent,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Calculates smuggling after support and garrison suppression.
        /// </summary>
        /// <param name="planet">The planet being evaluated.</param>
        /// <param name="controller">The faction controlling production.</param>
        /// <param name="beneficiary">The strongest opposing beneficiary.</param>
        /// <returns>The clamped resource-diversion percentage.</returns>
        private int CalculatePercent(Planet planet, Faction controller, Faction beneficiary)
        {
            if (
                controller == null
                || beneficiary == null
                || !planet.IsColonized
                || planet.IsDestroyed
            )
                return 0;

            GameConfig.SmugglingConfig config = _game.Config.Smuggling;
            int support = planet.GetPopularSupport(controller.InstanceID);
            Fleet[] fleets = planet
                .Fleets.Where(fleet =>
                    fleet.OwnerInstanceID == controller.InstanceID && fleet.Movement == null
                )
                .ToArray();
            if (
                fleets
                    .SelectMany(fleet => fleet.CapitalShips)
                    .Any(ship => IsOperational(ship) && ship.CanDestroyPlanets)
            )
                return 0;

            int percent = _lossPercentByMinimumSupport.Lookup(support);
            int capitalShips = fleets.Sum(fleet => fleet.GetOperationalCapitalShipCount());
            int starfighters = GetStationedStarfighters(planet, fleets, controller.InstanceID)
                .Count();
            int regiments = GetStationedRegiments(planet, fleets, controller.InstanceID).Count();

            return Math.Clamp(
                percent
                    - capitalShips * config.CapitalShipSuppression
                    - starfighters * config.StarfighterSuppression
                    - regiments * config.RegimentSuppression,
                0,
                _percentScale
            );
        }

        /// <summary>
        /// Returns completed, stationary starfighters owned by the controller that are stationed
        /// directly on the planet or carried by a stationary local fleet.
        /// </summary>
        private static IEnumerable<Starfighter> GetStationedStarfighters(
            Planet planet,
            IEnumerable<Fleet> fleets,
            string controllerInstanceID
        ) =>
            planet
                .Starfighters.Concat(fleets.SelectMany(fleet => fleet.GetStarfighters()))
                .Where(starfighter =>
                    starfighter.OwnerInstanceID == controllerInstanceID
                    && starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                    && starfighter.Movement == null
                );

        /// <summary>
        /// Returns completed, stationary regiments owned by the controller that are stationed
        /// directly on the planet or carried by a stationary local fleet.
        /// </summary>
        private static IEnumerable<Regiment> GetStationedRegiments(
            Planet planet,
            IEnumerable<Fleet> fleets,
            string controllerInstanceID
        ) =>
            planet
                .Regiments.Concat(fleets.SelectMany(fleet => fleet.GetRegiments()))
                .Where(regiment =>
                    regiment.OwnerInstanceID == controllerInstanceID
                    && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                );

        /// <summary>
        /// Selects the opposing faction with the strongest local support.
        /// </summary>
        /// <param name="planet">The planet being evaluated.</param>
        /// <param name="controller">The current controlling faction.</param>
        /// <returns>The deterministic beneficiary, or null without a controller.</returns>
        private Faction FindBeneficiary(Planet planet, Faction controller)
        {
            if (controller == null)
                return null;

            return _game
                .GetFactions()
                .Where(faction => faction.InstanceID != controller.InstanceID)
                .OrderByDescending(faction => planet.GetPopularSupport(faction.InstanceID))
                .ThenBy(faction => faction.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Resolves a faction by stable instance ID.
        /// </summary>
        /// <param name="instanceId">The faction instance ID.</param>
        /// <returns>The matching faction, or null.</returns>
        private Faction FindFaction(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;

            return _game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceId);
        }

        /// <summary>
        /// Returns whether a capital ship is complete and stationary.
        /// </summary>
        /// <param name="ship">The ship to inspect.</param>
        /// <returns>True when the ship can suppress smuggling.</returns>
        private static bool IsOperational(CapitalShip ship)
        {
            return ship.ManufacturingStatus == ManufacturingStatus.Complete
                && ship.Movement == null;
        }
    }
}
