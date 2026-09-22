using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Creates detached runtime units from the immutable templates loaded with the active content.
    /// </summary>
    public sealed class UnitFactory
    {
        private readonly Dictionary<string, ISceneNode> _templates;

        /// <summary>
        /// Builds a unit-template lookup from every repeatable unit category.
        /// </summary>
        /// <param name="buildings">The buildings.</param>
        /// <param name="capitalShips">The capital ships.</param>
        /// <param name="starfighters">The starfighters.</param>
        /// <param name="regiments">The regiments.</param>
        /// <param name="specialForces">The special forces.</param>
        public UnitFactory(
            IEnumerable<Building> buildings,
            IEnumerable<CapitalShip> capitalShips,
            IEnumerable<Starfighter> starfighters,
            IEnumerable<Regiment> regiments,
            IEnumerable<SpecialForces> specialForces
        )
        {
            _templates = Combine(buildings, capitalShips, starfighters, regiments, specialForces)
                .Where(template => !string.IsNullOrWhiteSpace(template.TypeID))
                .GroupBy(template => template.TypeID, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        /// <summary>
        /// Creates one complete, stationary unit of the expected category.
        /// </summary>
        /// <param name="typeID">The type id.</param>
        /// <param name="ownerInstanceID">The owner instance id.</param>
        /// <typeparam name="T">The manufacturable scene-node type to create.</typeparam>
        /// <returns>The created unit.</returns>
        public T Create<T>(string typeID, string ownerInstanceID)
            where T : class, ISceneNode, IManufacturable
        {
            if (!_templates.TryGetValue(typeID, out ISceneNode template))
                throw new InvalidOperationException($"Unknown unit TypeID '{typeID}'.");
            if (template is not T typedTemplate)
                throw new InvalidOperationException(
                    $"Unit TypeID '{typeID}' is not a {typeof(T).Name}."
                );

            T unit = (T)typedTemplate.CreateCopy();
            unit.InstanceID = null;
            unit.SetParent(null);
            unit.SetOwnerInstanceID(ownerInstanceID);
            unit.ManufacturingStatus = ManufacturingStatus.Complete;
            unit.ManufacturingProgress = 0;
            if (unit is IMovable movable)
                movable.Movement = null;
            return unit;
        }

        /// <summary>
        /// Creates one complete, stationary unit while accepting its authored category.
        /// </summary>
        /// <param name="typeID">The type id.</param>
        /// <param name="ownerInstanceID">The owner instance id.</param>
        /// <returns>The created value.</returns>
        public ISceneNode Create(string typeID, string ownerInstanceID)
        {
            if (!_templates.TryGetValue(typeID, out ISceneNode template))
                throw new InvalidOperationException($"Unknown unit TypeID '{typeID}'.");

            return template switch
            {
                Building => Create<Building>(typeID, ownerInstanceID),
                CapitalShip => Create<CapitalShip>(typeID, ownerInstanceID),
                Starfighter => Create<Starfighter>(typeID, ownerInstanceID),
                Regiment => Create<Regiment>(typeID, ownerInstanceID),
                SpecialForces => Create<SpecialForces>(typeID, ownerInstanceID),
                _ => throw new InvalidOperationException(
                    $"Unit TypeID '{typeID}' has unsupported type '{template.GetType().Name}'."
                ),
            };
        }

        /// <summary>
        /// Returns the maintenance cost authored on one unit template.
        /// </summary>
        /// <param name="typeID">The type id.</param>
        /// <returns>The requested maintenance cost.</returns>
        public int GetMaintenanceCost(string typeID)
        {
            if (!_templates.TryGetValue(typeID, out ISceneNode template))
                throw new InvalidOperationException($"Unknown unit TypeID '{typeID}'.");
            if (template is not IManufacturable manufacturable)
                throw new InvalidOperationException(
                    $"Unit TypeID '{typeID}' is not manufacturable."
                );
            return manufacturable.GetMaintenanceCost();
        }

        /// <summary>
        /// Combines the available unit-definition categories into one template sequence.
        /// </summary>
        /// <param name="categories">The categories.</param>
        /// <returns>The result of combine.</returns>
        private static IEnumerable<ISceneNode> Combine(
            params IEnumerable<ISceneNode>[] categories
        ) => categories.SelectMany(category => category ?? Enumerable.Empty<ISceneNode>());
    }
}
