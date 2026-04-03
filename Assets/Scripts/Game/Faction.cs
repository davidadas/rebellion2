using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;
using Rebellion.Util.Attributes;

namespace Rebellion.Game
{
    /// <summary>
    /// Represents a faction in the game, managing its resources, technologies, and owned entities.
    /// </summary>
    public class Faction : BaseGameEntity
    {
        // Faction Info
        public List<Officer> UnrecruitedOfficers { get; set; } = new List<Officer>();

        /// <summary>
        /// Technology levels organized by manufacturing type and research level.
        /// NOT serialized - rebuilt from ManufacturingResearchLevels on load to respect mods.
        /// </summary>
        [PersistableIgnore]
        public Dictionary<
            ManufacturingType,
            SortedDictionary<int, List<Technology>>
        > TechnologyLevels { get; set; } =
            new Dictionary<ManufacturingType, SortedDictionary<int, List<Technology>>>();

        public Dictionary<ManufacturingType, int> ManufacturingResearchLevels { get; set; } =
            new Dictionary<ManufacturingType, int>()
            {
                { ManufacturingType.Building, 0 },
                { ManufacturingType.Ship, 0 },
                { ManufacturingType.Troop, 0 },
            };
        public string HQInstanceID { get; set; }
        public string PlayerID { get; set; }

        /// <summary>
        /// Faction-specific gameplay modifiers.
        /// Affects game mechanics regardless of whether faction is player or AI controlled.
        /// </summary>
        public FactionModifiers Modifiers { get; set; } = new FactionModifiers();

        /// <summary>
        /// Fog of war state - snapshots and entity tracking.
        /// </summary>
        public FogState Fog { get; set; } = new FogState();

        // Fleet naming counter for sequential fleet names (Fleet 1, Fleet 2, etc.)
        private int nextFleetNumber = 1;

        // Owned Entities (Fleets, Planets, etc).
        private Dictionary<Type, List<ISceneNode>> ownedEntities = new Dictionary<
            Type,
            List<ISceneNode>
        >()
        {
            { typeof(CapitalShip), new List<ISceneNode>() },
            { typeof(Building), new List<ISceneNode>() },
            { typeof(Fleet), new List<ISceneNode>() },
            { typeof(Officer), new List<ISceneNode>() },
            { typeof(Planet), new List<ISceneNode>() },
            { typeof(Regiment), new List<ISceneNode>() },
            { typeof(Starfighter), new List<ISceneNode>() },
        };

        // Messages and Notifications
        public Dictionary<MessageType, List<Message>> Messages = new Dictionary<
            MessageType,
            List<Message>
        >()
        {
            { MessageType.Conflict, new List<Message>() },
            { MessageType.Mission, new List<Message>() },
            { MessageType.PopularSupport, new List<Message>() },
            { MessageType.Resource, new List<Message>() },
        };

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Faction() { }

        /// <summary>
        /// Returns the instance ID of the faction's headquarters.
        /// </summary>
        /// <returns>The HQ instance ID.</returns>
        public string GetHQInstanceID() => HQInstanceID;

        /// <summary>
        /// Checks if the faction is controlled by AI.
        /// </summary>
        /// <returns>True if the faction is AI controlled, false otherwise.</returns>
        public bool IsAIControlled() => string.IsNullOrEmpty(PlayerID);

        /// <summary>
        /// Returns a list of all units owned by the faction.
        /// </summary>
        /// <returns>A list of all owned units.</returns>
        public List<ISceneNode> GetAllOwnedNodes()
        {
            return ownedEntities.Values.SelectMany(x => x).ToList();
        }

        /// <summary>
        /// Returns a list of units of a specific type owned by the faction.
        /// </summary>
        /// <typeparam name="T">The type of unit to get.</typeparam>
        /// <returns>A list of owned units of the specified type.</returns>
        public List<T> GetOwnedUnitsByType<T>()
            where T : ISceneNode
        {
            return ownedEntities[typeof(T)].Cast<T>().ToList();
        }

        public List<Fleet> GetFleetsByType(FleetRoleType roleType)
        {
            return GetOwnedUnitsByType<Fleet>().Where(f => f.RoleType == roleType).ToList();
        }

        /// <summary>
        /// Adds a unit to the faction's owned entities.
        /// </summary>
        /// <typeparam name="T">The type of unit to add.</typeparam>
        /// <param name="unit">The unit to add.</param>
        public void AddOwnedUnit<T>(T unit)
            where T : ISceneNode
        {
            if (ownedEntities.ContainsKey(unit.GetType()))
            {
                ownedEntities[unit.GetType()].Add(unit);
            }
        }

        /// <summary>
        /// Removes a unit from the faction's owned entities.
        /// </summary>
        /// <typeparam name="T">The type of unit to remove.</typeparam>
        /// <param name="unit">The unit to remove.</param>
        public void RemoveOwnedUnit<T>(T unit)
            where T : ISceneNode
        {
            if (ownedEntities.ContainsKey(unit.GetType()))
            {
                ownedEntities[unit.GetType()].Remove(unit);
            }
        }

        /// <summary>
        /// Adds a technology node to the faction's technology levels.
        /// </summary>
        /// <param name="level">The level of the technology.</param>
        /// <param name="node">The technology node to add.</param>
        public void AddTechnologyNode(int level, Technology node)
        {
            IManufacturable tech = node.GetReference();

            if (
                tech.AllowedOwnerInstanceIDs != null
                && !tech.AllowedOwnerInstanceIDs.Contains(InstanceID)
            )
            {
                throw new InvalidOperationException(
                    $"Cannot add technology {tech.GetDisplayName()} to faction {DisplayName}. Owner IDs do not match."
                );
            }

            if (
                !TechnologyLevels.TryGetValue(
                    tech.GetManufacturingType(),
                    out SortedDictionary<int, List<Technology>> techLevels
                )
            )
            {
                techLevels = new SortedDictionary<int, List<Technology>>();
                TechnologyLevels[tech.GetManufacturingType()] = techLevels;
            }

            if (!techLevels.TryGetValue(level, out List<Technology> nodesAtLevel))
            {
                nodesAtLevel = new List<Technology>();
                techLevels[level] = nodesAtLevel;
            }

            if (!nodesAtLevel.Contains(node))
            {
                nodesAtLevel.Add(node);
            }
        }

        /// <summary>
        /// Returns a list of technologies that have been researched for a specific manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to check.</param>
        /// <returns>A list of researched technologies.</returns>
        public List<Technology> GetResearchedTechnologies(ManufacturingType manufacturingType)
        {
            if (
                !TechnologyLevels.TryGetValue(
                    manufacturingType,
                    out SortedDictionary<int, List<Technology>> techLevels
                )
            )
            {
                return new List<Technology>();
            }

            int currentResearchLevel = GetResearchLevel(manufacturingType);

            return techLevels
                .Where(kvp => kvp.Key <= currentResearchLevel)
                .SelectMany(kvp => kvp.Value)
                .Where(tech => tech.GetRequiredResearchLevel() <= currentResearchLevel)
                .ToList();
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public Dictionary<ManufacturingType, int> GetResearchLevels()
        {
            return ManufacturingResearchLevels;
        }

        /// <summary>
        /// Returns the research level for a specific manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to check.</param>
        /// <returns>The research level for the specified manufacturing type.</returns>
        public int GetResearchLevel(ManufacturingType manufacturingType)
        {
            return ManufacturingResearchLevels[manufacturingType];
        }

        /// <summary>
        /// Sets the research level for a specific manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to set.</param>
        /// <param name="level">The new research level.</param>
        public void SetResearchLevel(ManufacturingType manufacturingType, int level)
        {
            ManufacturingResearchLevels[manufacturingType] = level;
        }

        /// <summary>
        /// Calculates the sum of resources across all owned planets.
        /// </summary>
        /// <param name="selector">A function that selects the resource value from a planet.</param>
        /// <returns>The total sum of the selected resource across all owned planets.</returns>
        private int SumPlanetaryResources(Func<Planet, int> selector)
        {
            List<Planet> planets = GetOwnedUnitsByType<Planet>();
            return planets.Sum(selector);
        }

        /// <summary>
        /// Gets the total number of raw resource nodes across all owned planets.
        /// Raw resource nodes include all nodes without considering external factors such as blockade
        /// status or whether related buildings are under construction.
        /// </summary>
        /// <returns>The total number of raw resource nodes.</returns>
        public int GetTotalRawResourceNodes() =>
            SumPlanetaryResources(p => p.GetRawResourceNodes());

        /// <summary>
        /// Gets the total number of available resource nodes across all owned planets.
        /// Available resource nodes take into account external factors such as blockaded planets
        /// or buildings still under construction that might reduce usability.
        /// </summary>
        /// <returns>The total number of available resource nodes.</returns>
        public int GetTotalAvailableResourceNodes() =>
            SumPlanetaryResources(p => p.GetAvailableResourceNodes());

        /// <summary>
        /// Gets the total number of raw mined resources across all owned planets.
        /// Raw mined resources include all resources mined, without factoring in external conditions
        /// like blockade or construction status of related buildings.
        /// </summary>
        /// <returns>The total number of raw mined resources.</returns>
        public int GetTotalRawMinedResources() =>
            SumPlanetaryResources(p => p.GetRawMinedResources());

        /// <summary>
        /// Gets the total number of available mined resources across all owned planets.
        /// Available mined resources consider blockaded planets or buildings under construction
        /// that might limit resource collection.
        /// </summary>
        /// <returns>The total number of available mined resources.</returns>
        public int GetTotalAvailableMinedResources() =>
            SumPlanetaryResources(p => p.GetAvailableMinedResources());

        /// <summary>
        /// Gets the total raw refinement capacity across all owned planets.
        /// Raw refinement capacity represents the total processing capability, without considering
        /// factors like blockades or buildings under construction.
        /// </summary>
        /// <returns>The total raw refinement capacity.</returns>
        public int GetTotalRawRefinementCapacity() =>
            SumPlanetaryResources(p => p.GetRawRefinementCapacity());

        /// <summary>
        /// Gets the total available refinement capacity across all owned planets.
        /// Available refinement capacity accounts for factors like blockaded planets or buildings
        /// that are still under construction.
        /// </summary>
        /// <returns>The total available refinement capacity.</returns>
        public int GetTotalAvailableRefinementCapacity() =>
            SumPlanetaryResources(p => p.GetAvailableRefinementCapacity());

        /// <summary>
        /// Gets the total number of unrefined raw mines across all owned planets.
        /// </summary>
        /// <returns>The total number of unrefined raw mines.</returns>
        public int GetTotalUnrefinedRawMines()
        {
            int totalMines = GetTotalRawMinedResources();
            int totalRefineries = GetTotalRawRefinementCapacity();

            return Math.Max(0, totalMines - totalRefineries);
        }

        /// <summary>
        /// Calculates the total raw refined capacity count (before multiplier).
        /// This includes raw resource nodes, mined resources, and refinement capacity without any
        /// consideration of external conditions such as blockades or construction status.
        /// </summary>
        /// <returns>The raw refined capacity count.</returns>
        public int GetTotalRawMaterialsRaw()
        {
            return CalculateRefinedCapacity(
                GetTotalRawResourceNodes(),
                GetTotalRawMinedResources(),
                GetTotalRawRefinementCapacity()
            );
        }

        /// <summary>
        /// Calculates the total available refined capacity count (before multiplier).
        /// This includes only resources and capacities that are available after accounting for
        /// blockades, buildings under construction, and other limiting factors.
        /// </summary>
        /// <returns>The available refined capacity count.</returns>
        public int GetTotalAvailableMaterialsRaw()
        {
            return CalculateRefinedCapacity(
                GetTotalAvailableResourceNodes(),
                GetTotalAvailableMinedResources(),
                GetTotalAvailableRefinementCapacity()
            );
        }

        /// <summary>
        /// Calculates the refined capacity count (before applying multiplier).
        /// Returns how many units of refinement can be performed.
        /// </summary>
        /// <param name="resourceNodes">The number of resource nodes.</param>
        /// <param name="minedResources">The amount of mined resources.</param>
        /// <param name="refinementCapacity">The refinement capacity.</param>
        /// <returns>The refined capacity count.</returns>
        private int CalculateRefinedCapacity(
            int resourceNodes,
            int minedResources,
            int refinementCapacity
        )
        {
            int unprocessedMaterials = Math.Min(minedResources, resourceNodes);
            int refinedCount = Math.Min(unprocessedMaterials, refinementCapacity);
            return refinedCount;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public int GetTotalUnitCost()
        {
            return ownedEntities
                .Where(kvp => typeof(IManufacturable).IsAssignableFrom(kvp.Key))
                .SelectMany(kvp => kvp.Value)
                .OfType<IManufacturable>()
                .Sum(manufacturable =>
                {
                    if (manufacturable.GetManufacturingStatus() != ManufacturingStatus.Building)
                    {
                        return manufacturable.GetMaintenanceCost();
                    }

                    return manufacturable.GetConstructionCost();
                });
        }

        /// <summary>
        /// Adds a message to the faction's message list.
        /// </summary>
        /// <param name="message">The message to add.</param>
        public void AddMessage(Message message)
        {
            Messages[message.Type].Add(message);
        }

        /// <summary>
        /// Removes a message from the faction's message list.
        /// </summary>
        /// <param name="message">The message to remove.</param>
        public void RemoveMessage(Message message)
        {
            Messages[message.Type].Remove(message);
        }

        /// <summary>
        /// Creates a new fleet with sequential naming (Fleet 1, Fleet 2, etc.).
        /// Returns a detached fleet - caller must attach to scene graph via game.AttachNode().
        /// </summary>
        /// <param name="game">The game instance for generating InstanceID.</param>
        /// <returns>A new detached Fleet.</returns>
        public Fleet CreateFleet(GameRoot game)
        {
            Fleet fleet = new Fleet(this.InstanceID, $"Fleet {nextFleetNumber}");

            nextFleetNumber++;
            return fleet;
        }

        /// <summary>
        /// Returns a list of available officers for the faction.
        /// </summary>
        /// <param name="faction"></param>
        /// <returns></returns>
        public List<Officer> GetAvailableOfficers(Faction faction)
        {
            return GetOwnedUnitsByType<Officer>().FindAll(o => o.IsMovable());
        }

        /// <summary>
        /// Returns the closest friendly planet to the given scene node.
        /// </summary>
        /// <param name="fromNode">The scene node to measure distance from.</param>
        /// <returns>The closest friendly planet, or null if no friendly planets are found.</returns>
        public Planet GetNearestFriendlyPlanetTo(ISceneNode fromNode)
        {
            Planet sourcePlanet = fromNode.GetParentOfType<Planet>();
            if (sourcePlanet == null)
            {
                throw new ArgumentException(
                    "The provided ISceneNode is not on a planet.",
                    nameof(fromNode)
                );
            }

            return GetOwnedUnitsByType<Planet>()
                .OrderBy(p => sourcePlanet.GetRawDistanceTo(p))
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns a list of planets with idle manufacturing facilities for a specific manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to check.</param>
        /// <returns>A list of planets with idle facilities.</returns>
        public List<Planet> GetIdleFacilities(ManufacturingType manufacturingType)
        {
            return GetOwnedUnitsByType<Planet>()
                .FindAll(p => p.GetIdleManufacturingFacilities(manufacturingType) > 0);
        }

        /// <summary>
        /// Rebuilds TechnologyLevels from current game data based on ManufacturingResearchLevels.
        /// Called after loading a save file to ensure mod compatibility.
        ///
        /// This method reconstructs the technology tree using the current game's entity definitions
        /// (from data files, including any active mods) rather than the serialized Technology objects,
        /// ensuring that balance changes and new content are reflected in loaded saves.
        /// </summary>
        /// <param name="game">The game instance containing current entity definitions.</param>
        public void RebuildTechnologyLevels(GameRoot game)
        {
            TechnologyLevels.Clear();

            // Rebuild Ship technologies
            List<CapitalShip> shipClasses = game.GetSceneNodesByType<CapitalShip>();
            foreach (CapitalShip shipClass in shipClasses)
            {
                // Skip instances (owned units), only process class templates
                if (shipClass.GetOwnerInstanceID() != null)
                    continue;

                int requiredLevel = shipClass.RequiredResearchLevel;
                int currentLevel = ManufacturingResearchLevels[ManufacturingType.Ship];

                if (currentLevel >= requiredLevel)
                {
                    Technology tech = new Technology(shipClass);
                    AddTechnologyNode(requiredLevel, tech);
                }
            }

            // Rebuild Troop technologies
            List<Regiment> troopClasses = game.GetSceneNodesByType<Regiment>();
            foreach (Regiment troopClass in troopClasses)
            {
                if (troopClass.GetOwnerInstanceID() != null)
                    continue;

                int requiredLevel = troopClass.RequiredResearchLevel;
                int currentLevel = ManufacturingResearchLevels[ManufacturingType.Troop];

                if (currentLevel >= requiredLevel)
                {
                    Technology tech = new Technology(troopClass);
                    AddTechnologyNode(requiredLevel, tech);
                }
            }

            // Rebuild Building technologies
            List<Building> buildingClasses = game.GetSceneNodesByType<Building>();
            foreach (Building buildingClass in buildingClasses)
            {
                if (buildingClass.GetOwnerInstanceID() != null)
                    continue;

                int requiredLevel = buildingClass.RequiredResearchLevel;
                int currentLevel = ManufacturingResearchLevels[ManufacturingType.Building];

                if (currentLevel >= requiredLevel)
                {
                    Technology tech = new Technology(buildingClass);
                    AddTechnologyNode(requiredLevel, tech);
                }
            }
        }
    }
}
