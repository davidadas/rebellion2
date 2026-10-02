using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>Routes garrison, support, and ownership changes to planetary control operations.</summary>
    public sealed class PlanetaryControlObserver : IResultObserver, IDisposable
    {
        private readonly GameRoot _game;
        private readonly PlanetaryControlCommands _commands;
        private readonly PlanetaryControlQueries _queries;
        private readonly HashSet<string> _controlShiftedOwners = new HashSet<string>();
        private IDisposable[] _subscriptions;
        private int _controlShiftTick = -1;

        /// <summary>Creates the planetary control listener.</summary>
        /// <param name="game">The active game state and planetary-control configuration.</param>
        /// <param name="commands">The ownership and support operations.</param>
        /// <param name="queries">The planetary-control state queries.</param>
        public PlanetaryControlObserver(
            GameRoot game,
            PlanetaryControlCommands commands,
            PlanetaryControlQueries queries
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>Registers garrison, support, and ownership callbacks with the result bus.</summary>
        /// <param name="results">The bus that delivers garrison, support, and ownership changes.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscriptions != null)
                throw new InvalidOperationException(
                    "Planetary control observer is already connected."
                );
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[]
            {
                results.Subscribe<BombardmentResult>(HandleResults),
                results.Subscribe<PlanetGarrisonChangedResult>(HandleResults),
                results.Subscribe<PopularSupportShiftResult>(HandleResults),
                results.Subscribe<PlanetOwnershipChangedResult>(HandleResults),
                results.Observe<GameResult>(CompleteBombardmentResults),
            };
        }

        /// <summary>Stops receiving garrison, support, and ownership changes.</summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions ?? Array.Empty<IDisposable>())
                subscription.Dispose();
        }

        /// <summary>
        /// Reconciles bombardment-driven garrison losses and records the resulting control change.
        /// </summary>
        /// <param name="results">The completed bombardments to inspect.</param>
        /// <returns>Any ownership changes caused by destroyed garrisons.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<BombardmentResult> results)
        {
            List<GameResult> controlResults = new List<GameResult>();
            if (results == null)
                return controlResults;

            foreach (BombardmentResult result in results)
            {
                if (result?.Planet == null)
                    continue;

                controlResults.AddRange(ApplyBombardmentSupport(result));
                if (result.DestroyedRegiments?.Count > 0)
                    controlResults.AddRange(ReconcileGarrisonChange(result.Planet));
            }

            return controlResults;
        }

        /// <summary>
        /// Records each bombardment's final ownership outcome after all reactions settle.
        /// </summary>
        /// <param name="results">The settled result batch.</param>
        private static void CompleteBombardmentResults(IReadOnlyList<GameResult> results)
        {
            if (results == null)
                return;

            List<PlanetOwnershipChangedResult> ownershipChanges = results
                .OfType<PlanetOwnershipChangedResult>()
                .ToList();
            foreach (BombardmentResult bombardment in results.OfType<BombardmentResult>())
            {
                List<PlanetOwnershipChangedResult> matchingChanges = ownershipChanges
                    .Where(change => change.Planet == bombardment.Planet)
                    .ToList();
                if (matchingChanges.Count == 0)
                    continue;

                PlanetOwnershipChangedResult first = matchingChanges[0];
                PlanetOwnershipChangedResult last = matchingChanges[^1];
                if (first.PreviousOwner?.InstanceID == last.NewOwner?.InstanceID)
                {
                    bombardment.OwnershipChange = null;
                    continue;
                }

                bombardment.OwnershipChange = new PlanetOwnershipChangedResult
                {
                    Planet = bombardment.Planet,
                    PreviousOwner = first.PreviousOwner,
                    NewOwner = last.NewOwner,
                    Reason = matchingChanges
                        .Select(change => change.Reason)
                        .LastOrDefault(reason => reason != PlanetOwnershipChangeReason.None),
                    Tick = first.Tick,
                    SourceEventInstanceID = first.SourceEventInstanceID,
                    ObserverFactionInstanceIDs = matchingChanges
                        .SelectMany(change =>
                            change.ObserverFactionInstanceIDs ?? new List<string>()
                        )
                        .Distinct()
                        .ToList(),
                };
            }
        }

        /// <summary>
        /// Reconciles planets whose active garrisons changed.
        /// </summary>
        /// <param name="results">The result batch to inspect.</param>
        /// <returns>Any ownership changes caused by the garrison changes.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetGarrisonChangedResult> results)
        {
            List<GameResult> controlResults = new List<GameResult>();
            if (results == null)
                return controlResults;

            IEnumerable<Planet> affectedPlanets = results
                .Select(result => result.Planet)
                .Where(planet => planet != null)
                .Distinct();
            foreach (Planet planet in affectedPlanets)
                controlResults.AddRange(ReconcileGarrisonChange(planet));

            return controlResults;
        }

        /// <summary>Applies support changes in their incoming batch order.</summary>
        /// <param name="results">The requested support shifts.</param>
        /// <returns>The completed stat and ownership changes.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PopularSupportShiftResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            foreach (PopularSupportShiftResult result in results)
            {
                if (result == null)
                    continue;

                reactions.AddRange(ApplySupportShift(result));
            }

            return reactions;
        }

        /// <summary>Applies sector support reactions in ownership-change order.</summary>
        /// <param name="results">The completed ownership changes.</param>
        /// <returns>Any ownership changes caused by the resulting support reactions.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            foreach (PlanetOwnershipChangedResult result in results)
                reactions.AddRange(ApplyOwnershipReaction(result));

            return reactions;
        }

        /// <summary>
        /// Resolves control after an active regiment garrison changes.
        /// </summary>
        /// <param name="planet">The planet whose garrison changed.</param>
        /// <returns>Any resulting ownership change.</returns>
        private List<GameResult> ReconcileGarrisonChange(Planet planet)
        {
            List<GameResult> results = new List<GameResult>();
            if (!planet.IsColonized)
            {
                string ownerInstanceId = planet.GetOwnerInstanceID();
                if (
                    !string.IsNullOrEmpty(ownerInstanceId)
                    && !_queries.GetActiveRegimentOwners(planet).Contains(ownerInstanceId)
                )
                {
                    results.Add(_commands.ClearPlanetOwnership(planet));
                }

                return results;
            }

            List<string> regimentOwners = _queries.GetActiveRegimentOwners(planet);
            Faction controller = _queries.GetPlanetController(planet, regimentOwners);
            PlanetOwnershipChangedResult change = _commands.ChangePlanetOwner(planet, controller);
            if (change == null)
                return results;

            if (regimentOwners.Count == 0)
                change.Reason = PlanetOwnershipChangeReason.GarrisonRemoved;
            results.Add(change);
            return results;
        }

        /// <summary>
        /// Applies political reactions to civilian destruction and destroyed planets.
        /// </summary>
        /// <param name="result">The completed bombardment to evaluate.</param>
        /// <returns>Ownership changes caused by the support penalties.</returns>
        private List<GameResult> ApplyBombardmentSupport(BombardmentResult result)
        {
            List<GameResult> results = new List<GameResult>();
            if (result.AttackingFaction == null)
                return results;

            if (result.PlanetDestroyed)
            {
                results.AddRange(
                    ApplyDirectBombardmentPenalty(result.Planet, result.AttackingFaction)
                );
                results.AddRange(ApplyDestroyedPlanetPenalty(result.AttackingFaction));
                return results;
            }

            bool damagedCivilianTarget =
                result.EnergyCapacityDamage > 0
                || result.AllocatedEnergyDamage > 0
                || result.DestroyedBuildings?.Any(BombardmentQueries.IsCivilianTarget) == true;
            if (!damagedCivilianTarget)
                return results;

            results.AddRange(ApplyDirectBombardmentPenalty(result.Planet, result.AttackingFaction));
            PlanetSector sector = result.Planet.GetParentOfType<PlanetSector>();
            if (sector == null)
                return results;

            int shift =
                sector.SectorType == PlanetSectorType.Core
                    ? result.AttackingFaction.Settings.CivilianBombardmentCoreSupportPenalty
                    : result.AttackingFaction.Settings.CivilianBombardmentOuterRimSupportPenalty;
            results.AddRange(
                ApplyBombardmentSupportShift(
                    sector
                        .GetChildren<Planet>()
                        .Where(planet => planet.IsPopulated() && !planet.IsDestroyed),
                    result.AttackingFaction,
                    shift
                )
            );
            return results;
        }

        /// <summary>
        /// Applies the direct popular-support penalty at a bombarded planet.
        /// </summary>
        /// <param name="planet">Planet receiving the support shift.</param>
        /// <param name="attacker">Faction responsible for the bombardment.</param>
        /// <returns>Ownership changes caused by the support shift.</returns>
        private List<GameResult> ApplyDirectBombardmentPenalty(Planet planet, Faction attacker)
        {
            return ApplyBombardmentSupportShift(
                new[] { planet },
                attacker,
                _game.Config.Combat.Bombardment.CivilianSupportPenalty
            );
        }

        /// <summary>
        /// Applies galaxy-wide popular-support penalties after a planet is destroyed.
        /// </summary>
        /// <param name="attacker">Faction responsible for destroying the planet.</param>
        /// <returns>Ownership changes caused by the support shifts.</returns>
        private List<GameResult> ApplyDestroyedPlanetPenalty(Faction attacker)
        {
            List<GameResult> results = new List<GameResult>();
            IEnumerable<Planet> corePlanets = _game
                .GetSceneNodesByType<PlanetSector>()
                .Where(sector => sector.SectorType == PlanetSectorType.Core)
                .SelectMany(sector => sector.GetChildren<Planet>())
                .Where(planet => planet.IsPopulated() && !planet.IsDestroyed);
            results.AddRange(
                ApplyBombardmentSupportShift(
                    corePlanets,
                    attacker,
                    _game.Config.Combat.Bombardment.DestroyPlanetCoreSupportPenalty
                )
            );

            IEnumerable<Planet> outerRimPlanets = _game
                .GetSceneNodesByType<PlanetSector>()
                .Where(sector => sector.SectorType == PlanetSectorType.OuterRim)
                .SelectMany(sector => sector.GetChildren<Planet>())
                .Where(planet => planet.IsPopulated() && !planet.IsDestroyed)
                .Where(planet =>
                    planet.GetPopularSupport(attacker.InstanceID)
                    < _game.Config.Combat.Bombardment.DestroyPlanetOuterRimSupportThreshold
                );
            results.AddRange(
                ApplyBombardmentSupportShift(
                    outerRimPlanets,
                    attacker,
                    _game.Config.Combat.Bombardment.DestroyPlanetOuterRimSupportPenalty
                )
            );
            return results;
        }

        /// <summary>
        /// Applies one bombardment support shift and reports resulting ownership changes.
        /// </summary>
        /// <param name="planets">Planets receiving the support shift.</param>
        /// <param name="faction">Faction whose support changes.</param>
        /// <param name="shift">Signed support adjustment.</param>
        /// <returns>Ownership changes caused by the support adjustment.</returns>
        private List<GameResult> ApplyBombardmentSupportShift(
            IEnumerable<Planet> planets,
            Faction faction,
            int shift
        )
        {
            List<GameResult> results = new List<GameResult>();
            if (planets == null || faction == null || shift == 0)
                return results;

            foreach (Planet planet in planets)
            {
                Faction previousOwner = _queries.GetPlanetOwner(planet);
                _commands.ChangePopularSupport(planet, faction, shift);
                Faction controller = _queries.GetPlanetController(planet);
                if (previousOwner?.InstanceID == controller?.InstanceID)
                    continue;

                PlanetOwnershipChangedResult change = _commands.ChangePlanetOwner(
                    planet,
                    controller
                );
                if (change == null)
                    continue;

                change.Reason = PlanetOwnershipChangeReason.PopularSupport;
                results.Add(change);
            }

            return results;
        }

        /// <summary>
        /// Applies one requested support shift and resolves its immediate control change.
        /// </summary>
        /// <param name="request">The requested support shift.</param>
        /// <returns>The completed stat and ownership changes.</returns>
        private List<GameResult> ApplySupportShift(PopularSupportShiftResult request)
        {
            List<GameResult> results = new List<GameResult>();
            if (request.Planet == null || request.Faction == null || request.Shift == 0)
                return results;

            int oldSupport = request.Planet.GetPopularSupport(request.Faction.InstanceID);
            _commands.ChangePopularSupport(request.Planet, request.Faction, request.Shift);
            int newSupport = request.Planet.GetPopularSupport(request.Faction.InstanceID);
            if (oldSupport == newSupport)
                return results;

            results.Add(
                new PlanetStatChangedResult
                {
                    Planet = request.Planet,
                    Faction = request.Faction,
                    Category = PlanetChangeCategory.Loyalty,
                    OldValue = oldSupport,
                    NewValue = newSupport,
                    Tick = request.Tick,
                }
            );

            Faction controller = _queries.GetPlanetController(request.Planet);
            PlanetOwnershipChangedResult ownershipChange = _commands.ChangePlanetOwner(
                request.Planet,
                controller
            );
            if (ownershipChange == null)
                return results;

            ownershipChange.Reason = PlanetOwnershipChangeReason.PopularSupport;
            ownershipChange.Tick = request.Tick;
            results.Add(ownershipChange);
            return results;
        }

        /// <summary>
        /// Applies the support reaction selected by a completed ownership change.
        /// </summary>
        /// <param name="change">The completed ownership change.</param>
        /// <returns>Any ownership changes caused by the support reaction.</returns>
        private List<GameResult> ApplyOwnershipReaction(PlanetOwnershipChangedResult change)
        {
            List<GameResult> results = new List<GameResult>();
            if (change?.Planet == null)
                return results;

            int shift;
            switch (change.Reason)
            {
                case PlanetOwnershipChangeReason.PopularSupport:
                    if (!CanApplyControlSupportShift(change.PreviousOwner))
                        return results;
                    shift = _game.Config.SupportShift.ControlChangeSupportShift;
                    if (change.PreviousOwner != null && change.NewOwner != null)
                        shift += _game.Config.SupportShift.ControlChangeSupportShift;
                    break;
                case PlanetOwnershipChangeReason.GarrisonRemoved:
                    shift = _game.Config.SupportShift.GarrisonRemovalSupportShift;
                    break;
                default:
                    return results;
            }

            Faction beneficiary = change.NewOwner ?? GetOpposingFaction(change.PreviousOwner);
            if (beneficiary == null || shift == 0)
                return results;

            Faction displacedFaction = change.PreviousOwner ?? GetOpposingFaction(change.NewOwner);

            PlanetSector sector = change.Planet.GetParentOfType<PlanetSector>();
            if (sector == null)
                return results;

            foreach (
                Planet planet in sector
                    .GetChildren<Planet>()
                    .Where(planet => planet.IsPopulated() && !planet.IsDestroyed)
            )
            {
                if (
                    change.Reason == PlanetOwnershipChangeReason.PopularSupport
                    && displacedFaction != null
                    && planet.GetOpposingPopularSupport(displacedFaction.InstanceID) == 0
                )
                {
                    continue;
                }

                Faction previousController = _queries.GetPlanetOwner(planet);
                _commands.ChangePopularSupport(planet, beneficiary, shift);
                Faction controller = _queries.GetPlanetController(planet);
                if (previousController?.InstanceID == controller?.InstanceID)
                    continue;

                PlanetOwnershipChangedResult reaction = _commands.ChangePlanetOwner(
                    planet,
                    controller
                );
                if (reaction == null)
                    continue;

                reaction.Reason = PlanetOwnershipChangeReason.PopularSupport;
                reaction.Tick = change.Tick;
                results.Add(reaction);
            }

            return results;
        }

        /// <summary>
        /// Limits support-driven cascades to one reaction per displaced faction each tick.
        /// </summary>
        /// <param name="previousOwner">The faction displaced by the ownership change.</param>
        /// <returns>True when the ownership change may trigger a support reaction.</returns>
        private bool CanApplyControlSupportShift(Faction previousOwner)
        {
            if (previousOwner == null)
                return true;

            if (_controlShiftTick != _game.CurrentTick)
            {
                _controlShiftTick = _game.CurrentTick;
                _controlShiftedOwners.Clear();
            }

            return _controlShiftedOwners.Add(previousOwner.InstanceID);
        }

        /// <summary>
        /// Returns the faction opposed to a displaced owner.
        /// </summary>
        /// <param name="previousOwner">The faction displaced by the ownership change.</param>
        /// <returns>The opposing faction, or null when none exists.</returns>
        private Faction GetOpposingFaction(Faction previousOwner)
        {
            return _game
                .GetFactions()
                .FirstOrDefault(candidate => candidate.InstanceID != previousOwner?.InstanceID);
        }
    }
}
