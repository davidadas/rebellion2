using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    public sealed partial class GameEventExecutor
    {
        /// <summary>Resolves an authored selector without changing its evaluation timing.</summary>
        /// <param name="definition">The authored selector to interpret.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random provider used by randomized selection.</param>
        /// <param name="context">The activation bindings available to the selector.</param>
        /// <returns>The selected scene-node sequence.</returns>
        internal static IEnumerable<ISceneNode> Select(
            GameEventSelector definition,
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        ) =>
            definition switch
            {
                SelectPlanets value => Select(value, game),
                SelectPlanetSectors value => Select(value, game),
                SelectOfficers value => Select(value, game, context),
                SelectSpecialForces value => Select(value, game, context),
                SelectFleets value => Select(value, game, context),
                SelectMissions value => Select(value, game, context),
                SelectCapitalShips value => Select(value, game, context),
                SelectStarfighters value => Select(value, game, context),
                SelectRegiments value => Select(value, game, context),
                SelectBuildings value => Select(value, game, context),
                SelectManufacturingOrders value => Select(value, game, context),
                SelectRandom value => Select(value, game, provider, context),
                SelectFirst value => Select(value, game, provider, context),
                SelectBinding value => Select(value, game, context),
                SelectNearestParent value => Select(value, game, provider, context),
                SelectPreviousLocation value => Select(value, game, context),
                SpawnUnits => throw new InvalidOperationException(
                    "SpawnUnits may only be used as a PlaceUnits unit source."
                ),
                null => throw new NullReferenceException(),
                _ => throw new InvalidOperationException(
                    $"Unsupported selector '{definition.GetType().Name}'."
                ),
            };

        /// <summary>
        /// Returns registered nodes that match the authored activity and identity filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <typeparam name="T">The selected scene-node type.</typeparam>
        /// <returns>The selected owned.</returns>
        private static IEnumerable<T> SelectOwned<T>(
            OwnedSceneNodeSelector<T> definition,
            GameRoot game
        )
            where T : class, ISceneNode
        {
            IEnumerable<T> nodes = definition.IncludeInactive
                ? game.GetRegisteredSceneNodesByType<T>(includeDisabled: true)
                : Active<T>(game);
            return SelectOwned(definition, nodes.Where(node => node.GetParent() != null));
        }

        /// <summary>
        /// Filters a supplied node sequence by authored identity and ownership.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="nodes">The nodes.</param>
        /// <typeparam name="T">The selected scene-node type.</typeparam>
        /// <returns>The selected owned.</returns>
        private static IEnumerable<T> SelectOwned<T>(
            OwnedSceneNodeSelector<T> definition,
            IEnumerable<T> nodes
        )
            where T : class, ISceneNode
        {
            return nodes
                .Where(node =>
                    string.IsNullOrWhiteSpace(definition.InstanceID)
                    || node.InstanceID == definition.InstanceID
                )
                .Where(node =>
                    string.IsNullOrWhiteSpace(definition.OwnerFactionInstanceID)
                    || node.OwnerInstanceID == definition.OwnerFactionInstanceID
                );
        }

        /// <summary>
        /// Returns owned nodes located at the selected planet.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <typeparam name="T">The selected scene-node type.</typeparam>
        /// <returns>The selected located.</returns>
        private static IEnumerable<T> SelectLocated<T>(
            LocatedSceneNodeSelector<T> definition,
            GameRoot game,
            GameEventEvaluationContext context
        )
            where T : class, ISceneNode =>
            SelectOwned(definition, game)
                .Where(node =>
                    MatchesLocation(
                        node,
                        context,
                        definition.PlanetInstanceID,
                        definition.PlanetBinding
                    )
                );

        /// <summary>
        /// Returns located units matching the authored manufacturing filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <typeparam name="T">The selected scene-node type.</typeparam>
        /// <returns>The selected manufacturable.</returns>
        private static IEnumerable<T> SelectManufacturable<T>(
            ManufacturableSelector<T> definition,
            GameRoot game,
            GameEventEvaluationContext context
        )
            where T : class, ISceneNode, IManufacturable =>
            SelectLocated(definition, game, context)
                .Where(unit =>
                    string.IsNullOrWhiteSpace(definition.TypeID) || unit.TypeID == definition.TypeID
                )
                .Where(unit =>
                    !definition.ManufacturingStatus.HasValue
                    || unit.ManufacturingStatus == definition.ManufacturingStatus.Value
                );

        /// <summary>
        /// Returns active planets that match the authored ownership and sector filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(SelectPlanets definition, GameRoot game) =>
            SelectOwned(definition, game)
                .Where(planet => !planet.IsDestroyed)
                .Where(planet =>
                    !definition.SectorType.HasValue
                    || planet.GetParentOfType<PlanetSector>()?.SectorType
                        == definition.SectorType.Value
                );

        /// <summary>
        /// Returns active planet sectors that match the authored filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectPlanetSectors definition,
            GameRoot game
        ) =>
            (
                definition.IncludeInactive
                    ? game.GetRegisteredSceneNodesByType<PlanetSector>(includeDisabled: true)
                    : Active<PlanetSector>(game)
            )
                .Where(sector => sector.GetParent() != null)
                .Where(sector =>
                    string.IsNullOrWhiteSpace(definition.InstanceID)
                    || sector.InstanceID == definition.InstanceID
                )
                .Where(sector =>
                    !definition.SectorType.HasValue
                    || sector.SectorType == definition.SectorType.Value
                );

        /// <summary>
        /// Returns officers that match the authored location and captivity filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectOfficers definition,
            GameRoot game,
            GameEventEvaluationContext context
        )
        {
            return SelectOwned(definition, game)
                .Where(node =>
                    MatchesLocation(
                        node,
                        context,
                        definition.PlanetInstanceID,
                        definition.PlanetBinding
                    )
                )
                .Where(officer =>
                    !definition.IsCaptured.HasValue
                    || officer.IsCaptured == definition.IsCaptured.Value
                );
        }

        /// <summary>
        /// Returns special-forces units that match the authored location filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectSpecialForces definition,
            GameRoot game,
            GameEventEvaluationContext context
        ) => SelectLocated(definition, game, context);

        /// <summary>
        /// Returns fleets that match the authored location filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectFleets definition,
            GameRoot game,
            GameEventEvaluationContext context
        ) => SelectLocated(definition, game, context);

        /// <summary>
        /// Returns missions that match the authored location filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectMissions definition,
            GameRoot game,
            GameEventEvaluationContext context
        ) => SelectLocated(definition, game, context);

        /// <summary>
        /// Returns capital ships that match the authored unit filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectCapitalShips definition,
            GameRoot game,
            GameEventEvaluationContext context
        ) => SelectManufacturable(definition, game, context);

        /// <summary>
        /// Returns starfighters that match the authored unit filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectStarfighters definition,
            GameRoot game,
            GameEventEvaluationContext context
        ) => SelectManufacturable(definition, game, context);

        /// <summary>
        /// Returns regiments that match the authored unit filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectRegiments definition,
            GameRoot game,
            GameEventEvaluationContext context
        ) => SelectManufacturable(definition, game, context);

        /// <summary>
        /// Returns buildings that match the authored unit and strategic-category filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectBuildings definition,
            GameRoot game,
            GameEventEvaluationContext context
        ) =>
            SelectManufacturable(definition, game, context)
                .Where(building => MatchesCategory(definition, building));

        /// <summary>
        /// Returns whether a building belongs to the authored strategic category.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="building">The building.</param>
        /// <returns>True when the value matches category; otherwise false.</returns>
        private static bool MatchesCategory(SelectBuildings definition, Building building) =>
            definition.Category switch
            {
                BuildingSelectionCategory.Any => true,
                BuildingSelectionCategory.PlanetaryDefense => building.BuildingType
                    is BuildingType.Defense
                        or BuildingType.Weapon,
                BuildingSelectionCategory.ManufacturingFacility => building.BuildingType
                    is BuildingType.Shipyard
                        or BuildingType.TrainingFacility
                        or BuildingType.ConstructionFacility,
                _ => false,
            };

        /// <summary>
        /// Returns manufacturing orders that match the authored planet, owner, and type filters.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectManufacturingOrders definition,
            GameRoot game,
            GameEventEvaluationContext context
        )
        {
            Planet boundPlanet = !string.IsNullOrWhiteSpace(definition.PlanetBinding)
                ? context?.GetBindingReference<Planet>(definition.PlanetBinding)
                : null;
            string planetID = boundPlanet?.InstanceID ?? definition.PlanetInstanceID;
            IEnumerable<Planet> planets = (
                definition.IncludeInactive
                    ? game.GetRegisteredSceneNodesByType<Planet>(includeDisabled: true)
                    : Active<Planet>(game)
            )
                .Where(planet => planet.GetParent() != null)
                .Where(planet =>
                    string.IsNullOrWhiteSpace(planetID) || planet.InstanceID == planetID
                )
                .Where(planet =>
                    string.IsNullOrWhiteSpace(definition.OwnerFactionInstanceID)
                    || planet.OwnerInstanceID == definition.OwnerFactionInstanceID
                );
            return planets
                .SelectMany(planet => planet.ManufacturingQueue)
                .Where(entry =>
                    !definition.ManufacturingType.HasValue
                    || entry.Key == definition.ManufacturingType.Value
                )
                .SelectMany(entry => entry.Value)
                .Cast<ISceneNode>();
        }

        /// <summary>
        /// Randomly samples the authored candidate selectors within the configured limits.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectRandom definition,
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        )
        {
            if (definition.ChancePercent < 0 || definition.ChancePercent > 100)
                throw new InvalidOperationException(
                    "SelectRandom ChancePercent must be between 0 and 100."
                );
            if (
                definition.Count is < 0
                || definition.MinimumCount < 0
                || definition.MaximumCount is < 0
            )
                throw new InvalidOperationException("SelectRandom counts cannot be negative.");
            if (
                definition.Count.HasValue
                && (definition.MinimumCount != 0 || definition.MaximumCount.HasValue)
            )
                throw new InvalidOperationException(
                    "SelectRandom Count cannot be combined with MinimumCount or MaximumCount."
                );
            if (
                definition.MaximumCount.HasValue
                && definition.MaximumCount.Value < definition.MinimumCount
            )
                throw new InvalidOperationException(
                    "SelectRandom MaximumCount cannot be less than MinimumCount."
                );

            List<ISceneNode> remaining = definition
                .Selectors.SelectMany(selector => Select(selector, game, provider, context))
                .Distinct()
                .OrderBy(node => node.InstanceID, StringComparer.Ordinal)
                .ToList();
            List<ISceneNode> selected = new List<ISceneNode>();
            int minimum = definition.Count ?? definition.MinimumCount;
            int maximum = definition.Count ?? definition.MaximumCount ?? remaining.Count;
            foreach (ISceneNode candidate in remaining.ToList())
            {
                if (provider.NextInt(0, 100) >= definition.ChancePercent)
                    continue;
                selected.Add(candidate);
                remaining.Remove(candidate);
            }
            while (selected.Count < Math.Min(minimum, selected.Count + remaining.Count))
            {
                int index = provider.NextInt(0, remaining.Count);
                selected.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
            while (selected.Count > maximum)
                selected.RemoveAt(provider.NextInt(0, selected.Count));
            return selected;
        }

        /// <summary>
        /// Returns the first distinct node produced by the authored candidate selectors.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectFirst definition,
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        ) => SelectCandidates(definition, game, provider, context).Take(1);

        /// <summary>
        /// Returns the distinct candidate sequence before taking its first node.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected candidates.</returns>
        internal static IEnumerable<ISceneNode> SelectCandidates(
            SelectFirst definition,
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        ) =>
            definition
                .Selectors.SelectMany(selector => Select(selector, game, provider, context))
                .Distinct();

        /// <summary>
        /// Returns the scene node or nodes held by the authored event binding.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectBinding definition,
            GameRoot game,
            GameEventEvaluationContext context
        )
        {
            if (context?.TryGetBindingReference(definition.Binding, out object value) != true)
                throw new InvalidOperationException(
                    $"SelectBinding could not resolve binding '{definition.Binding}'."
                );

            IEnumerable<ISceneNode> nodes = value switch
            {
                ISceneNode node => new[] { node },
                IEnumerable<ISceneNode> collection => collection,
                _ => throw new InvalidOperationException(
                    $"SelectBinding '{definition.Binding}' does not contain scene nodes."
                ),
            };
            List<ISceneNode> selected = new List<ISceneNode>();
            foreach (ISceneNode node in nodes)
            {
                ISceneNode canonical =
                    node == null
                        ? null
                        : game.GetSceneNodeByInstanceID<ISceneNode>(
                            node.InstanceID,
                            includeDisabled: true
                        );
                if (canonical == null)
                    throw new InvalidOperationException(
                        $"SelectBinding '{definition.Binding}' contains an unregistered scene node."
                    );
                if (selected.All(existing => existing.InstanceID != canonical.InstanceID))
                    selected.Add(canonical);
            }
            return selected;
        }

        /// <summary>
        /// Returns the nearest parent of the requested type for each authored candidate node.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectNearestParent definition,
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        ) =>
            definition
                .Selectors.SelectMany(selector => Select(selector, game, provider, context))
                .Select(node => SceneAncestors.Resolve(node, definition.Type))
                .Where(node => node != null)
                .Distinct();

        /// <summary>
        /// Returns the remembered previous location of the authored unit.
        /// </summary>
        /// <param name="definition">The authored selection and filters.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        private static IEnumerable<ISceneNode> Select(
            SelectPreviousLocation definition,
            GameRoot game,
            GameEventEvaluationContext context
        )
        {
            bool hasInstanceID = !string.IsNullOrWhiteSpace(definition.UnitInstanceID);
            bool hasBinding = !string.IsNullOrWhiteSpace(definition.UnitBinding);
            if (hasInstanceID == hasBinding)
                throw new InvalidOperationException(
                    "SelectPreviousLocation requires exactly one of UnitInstanceID or UnitBinding."
                );
            ISceneNode unit = hasBinding
                ? context?.GetBindingReference<ISceneNode>(definition.UnitBinding)
                : game.GetSceneNodeByInstanceID<ISceneNode>(
                    definition.UnitInstanceID,
                    includeDisabled: true
                );
            if (unit == null)
                return Enumerable.Empty<ISceneNode>();
            ISceneNode parent = game.GetSceneNodeByInstanceID<ISceneNode>(
                unit.LastParentInstanceID,
                includeDisabled: true
            );
            return parent == null ? Enumerable.Empty<ISceneNode>() : new[] { parent };
        }

        /// <summary>
        /// Returns registered nodes that remain attached to active gameplay containment.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <typeparam name="T">The active scene-node type to select.</typeparam>
        /// <returns>The attached, active nodes of the requested type.</returns>
        private static IEnumerable<T> Active<T>(GameRoot game)
            where T : class, ISceneNode
        {
            return game.GetRegisteredSceneNodesByType<T>().Where(node => node.GetParent() != null);
        }

        /// <summary>
        /// Returns whether a node is located at the explicitly named or bound planet.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="context">The context.</param>
        /// <param name="planetInstanceID">The planet instance id.</param>
        /// <param name="planetBinding">The planet binding.</param>
        /// <returns>True when the value matches location; otherwise false.</returns>
        private static bool MatchesLocation(
            ISceneNode node,
            GameEventEvaluationContext context,
            string planetInstanceID,
            string planetBinding
        )
        {
            Planet planet = !string.IsNullOrWhiteSpace(planetBinding)
                ? context?.GetBindingReference<Planet>(planetBinding)
                : null;
            string expected = planet?.InstanceID ?? planetInstanceID;
            return string.IsNullOrWhiteSpace(expected)
                || node is Planet selectedPlanet && selectedPlanet.InstanceID == expected
                || node.GetParentOfType<Planet>()?.InstanceID == expected;
        }
    }

    internal static class SceneAncestors
    {
        /// <summary>
        /// Resolves the requested operation.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="type">The type.</param>
        /// <returns>The resolved value.</returns>
        internal static ISceneNode Resolve(ISceneNode node, SceneAncestorType type) =>
            type switch
            {
                SceneAncestorType.Galaxy => node.GetParentOfType<GalaxyMap>(),
                SceneAncestorType.PlanetSector => node.GetParentOfType<PlanetSector>(),
                SceneAncestorType.Planet => node.GetParentOfType<Planet>(),
                SceneAncestorType.Fleet => node.GetParentOfType<Fleet>(),
                SceneAncestorType.Mission => node.GetParentOfType<Mission>(),
                SceneAncestorType.CapitalShip => node.GetParentOfType<CapitalShip>(),
                _ => null,
            };
    }
}
