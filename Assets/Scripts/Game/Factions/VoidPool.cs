using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Factions
{
    /// <summary>
    /// Persists faction-owned units that are temporarily or permanently outside the scene graph.
    /// </summary>
    [PersistableObject(Name = "VoidPool")]
    public sealed class VoidPool
    {
        // Stored Units.
        public List<Building> Buildings { get; set; } = new List<Building>();
        public List<CapitalShip> CapitalShips { get; set; } = new List<CapitalShip>();
        public List<Fleet> Fleets { get; set; } = new List<Fleet>();
        public List<Officer> Officers { get; set; } = new List<Officer>();
        public List<Regiment> Regiments { get; set; } = new List<Regiment>();
        public List<SpecialForces> SpecialForces { get; set; } = new List<SpecialForces>();
        public List<Starfighter> Starfighters { get; set; } = new List<Starfighter>();

        /// <summary>
        /// Returns whether the scene-node type can be retained outside the scene graph.
        /// </summary>
        /// <param name="child">The node to inspect.</param>
        /// <returns>True when the pool has a typed collection for the node.</returns>
        public bool CanStore(ISceneNode child) =>
            child is Building
            || child is CapitalShip
            || child is Fleet
            || child is Officer
            || child is Regiment
            || child is SpecialForces
            || child is Starfighter;

        /// <summary>
        /// Adds a detached unit to its typed storage collection.
        /// </summary>
        /// <param name="child">The detached unit to retain.</param>
        public void Add(ISceneNode child)
        {
            switch (child)
            {
                case Building value:
                    Buildings.Add(value);
                    break;
                case CapitalShip value:
                    CapitalShips.Add(value);
                    break;
                case Fleet value:
                    Fleets.Add(value);
                    break;
                case Officer value:
                    Officers.Add(value);
                    break;
                case Regiment value:
                    Regiments.Add(value);
                    break;
                case SpecialForces value:
                    SpecialForces.Add(value);
                    break;
                case Starfighter value:
                    Starfighters.Add(value);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"{child?.GetType().Name} cannot enter a void pool."
                    );
            }
        }

        /// <summary>
        /// Removes a unit from its typed storage collection.
        /// </summary>
        /// <param name="child">The retained unit to remove.</param>
        public void Remove(ISceneNode child)
        {
            switch (child)
            {
                case Building value:
                    Buildings.Remove(value);
                    break;
                case CapitalShip value:
                    CapitalShips.Remove(value);
                    break;
                case Fleet value:
                    Fleets.Remove(value);
                    break;
                case Officer value:
                    Officers.Remove(value);
                    break;
                case Regiment value:
                    Regiments.Remove(value);
                    break;
                case SpecialForces value:
                    SpecialForces.Remove(value);
                    break;
                case Starfighter value:
                    Starfighters.Remove(value);
                    break;
            }
        }

        /// <summary>
        /// Returns whether the exact unit instance is retained by this pool.
        /// </summary>
        /// <param name="child">The unit instance to find.</param>
        /// <returns>True when the unit is stored.</returns>
        public bool Contains(ISceneNode child)
        {
            if (child == null)
                return false;
            return GetUnits().Contains(child);
        }

        /// <summary>
        /// Enumerates every retained unit across the typed storage collections.
        /// </summary>
        /// <returns>All units in deterministic type-group order.</returns>
        public IEnumerable<ISceneNode> GetUnits()
        {
            foreach (ISceneNode node in Buildings)
                yield return node;
            foreach (ISceneNode node in CapitalShips)
                yield return node;
            foreach (ISceneNode node in Fleets)
                yield return node;
            foreach (ISceneNode node in Officers)
                yield return node;
            foreach (ISceneNode node in Regiments)
                yield return node;
            foreach (ISceneNode node in SpecialForces)
                yield return node;
            foreach (ISceneNode node in Starfighters)
                yield return node;
        }
    }
}
