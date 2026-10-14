using System;
using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Applies gameplay destruction and permanent-removal rules to units.
    /// </summary>
    public sealed class UnitCommands
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates unit commands for the active game.
        /// </summary>
        /// <param name="game">The active game state.</param>
        public UnitCommands(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Destroys a unit according to its gameplay lifecycle.
        /// </summary>
        /// <param name="unit">The live unit to destroy.</param>
        public void Destroy(ISceneNode unit)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));

            if (unit is Building { BuildingType: not BuildingType.Headquarters } building)
            {
                RemoveFromManufacturingQueue(building);
                CancelMaterialRequests(building);
                building.MarkDestroyed();
                return;
            }

            RemovePermanently(unit);
        }

        /// <summary>
        /// Permanently removes a unit after clearing gameplay state that references it.
        /// </summary>
        /// <param name="unit">The live unit to remove.</param>
        public void RemovePermanently(ISceneNode unit)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));

            ((BaseSceneNode)unit).TraverseIncludingDisabled(candidate =>
            {
                if (candidate is not Building building)
                    return;

                RemoveFromManufacturingQueue(building);
                CancelMaterialRequests(building);
                if (building.BuildingType == BuildingType.Headquarters)
                    ClearHeadquartersState(building);
            });
            _game.DeleteNode(unit);
        }

        /// <summary>
        /// Clears the planet and faction markers for a permanently destroyed headquarters.
        /// </summary>
        /// <param name="headquarters">The headquarters being removed.</param>
        private void ClearHeadquartersState(Building headquarters)
        {
            Planet planet = headquarters.GetParentOfType<Planet>();
            if (planet != null)
                planet.IsHeadquarters = false;

            Faction faction = string.IsNullOrEmpty(headquarters.OwnerInstanceID)
                ? null
                : _game.GetFactionByOwnerInstanceID(headquarters.OwnerInstanceID);
            if (
                faction != null
                && (
                    string.Equals(
                        faction.HQInstanceID,
                        headquarters.InstanceID,
                        StringComparison.Ordinal
                    )
                    || string.Equals(
                        faction.HQInstanceID,
                        planet?.InstanceID,
                        StringComparison.Ordinal
                    )
                )
            )
            {
                faction.HQInstanceID = null;
            }
        }

        /// <summary>
        /// Removes pending material requests owned by a building.
        /// </summary>
        /// <param name="building">The building whose requests should be removed.</param>
        private void CancelMaterialRequests(Building building)
        {
            if (string.IsNullOrEmpty(building?.OwnerInstanceID))
                return;

            _game
                .GetFactionByOwnerInstanceID(building.OwnerInstanceID)
                ?.CancelMaterialRequests(building);
        }

        /// <summary>
        /// Removes a building from the producer queue that still references it.
        /// </summary>
        /// <param name="building">The building whose queued work is ending.</param>
        private void RemoveFromManufacturingQueue(Building building)
        {
            if (string.IsNullOrEmpty(building?.ProducerPlanetID))
                return;

            Planet producer = _game.GetSceneNodeByInstanceID<Planet>(building.ProducerPlanetID);
            if (producer == null)
                return;

            ManufacturingType type = building.GetManufacturingType();
            if (!producer.ManufacturingQueue.TryGetValue(type, out List<IManufacturable> queue))
                return;

            queue.Remove(building);
            if (queue.Count == 0)
                producer.ManufacturingQueue.Remove(type);
        }
    }
}
