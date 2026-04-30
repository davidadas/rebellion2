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
        public List<MissionType> DisallowedMissionTypes { get; set; } = new List<MissionType>();

        /// <summary>
        /// Faction-specific gameplay modifiers.
        /// These affect game mechanics regardless of whether faction is player or AI controlled.
        /// </summary>
        public FactionModifiers Modifiers { get; set; } = new FactionModifiers();

        /// <summary>
        /// Flat sorted list of technologies per manufacturing type.
        /// NOT serialized — rebuilt from templates on load to respect mods.
        /// </summary>
        [PersistableIgnore]
        public Dictionary<ManufacturingType, List<Technology>> ResearchQueue { get; set; } =
            new Dictionary<ManufacturingType, List<Technology>>();

        /// <summary>
        /// Side-level research progression state.
        /// </summary>
        public FactionResearchState ResearchState { get; set; } = new FactionResearchState();

        /// <summary>
        /// Sorted research catalog entries per discipline.
        /// NOT serialized â€” rebuilt from templates on load to respect mods.
        /// </summary>
        [PersistableIgnore]
        public Dictionary<
            ResearchDiscipline,
            List<ResearchCatalogEntry>
        > ResearchCatalog { get; set; } =
            new Dictionary<ResearchDiscipline, List<ResearchCatalogEntry>>();

        public string HQInstanceID { get; set; }
        public string PlayerID { get; set; }

        /// <summary>
        /// Accumulated raw materials (pre-refinement). Filled each tick from planet income.
        /// </summary>
        public int RawMaterialStockpile { get; set; }

        /// <summary>
        /// Accumulated refined materials (post-refinement). This is the spendable pool —
        /// builds and maintenance deduct from it.
        /// </summary>
        public int RefinedMaterialStockpile { get; set; }

        /// <summary>
        /// Fog of war state - snapshots and entity tracking.
        /// </summary>
        public FogState Fog { get; set; } = new FogState();

        // Fleet naming counter for sequential fleet names (Fleet 1, Fleet 2, etc.)
        private int _nextFleetNumber = 1;

        // Owned Entities (Fleets, Planets, etc).
        private Dictionary<Type, List<ISceneNode>> _ownedEntities = new Dictionary<
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
            return _ownedEntities.Values.SelectMany(x => x).ToList();
        }

        /// <summary>
        /// Returns a list of units of a specific type owned by the faction.
        /// </summary>
        /// <typeparam name="T">The type of unit to get.</typeparam>
        /// <returns>A list of owned units of the specified type.</returns>
        public List<T> GetOwnedUnitsByType<T>()
            where T : ISceneNode
        {
            return _ownedEntities[typeof(T)].Cast<T>().ToList();
        }

        /// <summary>
        /// Returns the faction's owned fleets filtered by the given role type.
        /// </summary>
        /// <param name="roleType">The fleet role (Battle, Patrol, etc.) to match.</param>
        /// <returns>The matching fleets, or an empty list if none qualify.</returns>
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
            if (_ownedEntities.ContainsKey(unit.GetType()))
            {
                _ownedEntities[unit.GetType()].Add(unit);
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
            if (_ownedEntities.ContainsKey(unit.GetType()))
            {
                _ownedEntities[unit.GetType()].Remove(unit);
            }
        }

        /// <summary>
        /// Returns technologies unlocked for a specific manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to query.</param>
        /// <returns>Unlocked technologies for the given type.</returns>
        public List<Technology> GetUnlockedTechnologies(ManufacturingType manufacturingType)
        {
            ResearchDiscipline discipline = ToResearchDiscipline(manufacturingType);
            if (!ResearchCatalog.TryGetValue(discipline, out List<ResearchCatalogEntry> entries))
                return new List<Technology>();

            int unlockedOrder = GetHighestUnlockedOrder(manufacturingType);
            return entries
                .Where(entry => entry.Order <= unlockedOrder)
                .Select(entry => entry.Technology)
                .ToList();
        }

        /// <summary>
        /// Returns the next technology to be researched for a manufacturing type,
        /// or null if all technologies are unlocked.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to query.</param>
        /// <returns>The next technology to research, or null if all are unlocked.</returns>
        public Technology GetCurrentResearchTarget(ManufacturingType manufacturingType)
        {
            ResearchCatalogEntry nextEntry = GetNextResearchEntry(
                ToResearchDiscipline(manufacturingType)
            );
            return nextEntry?.Technology;
        }

        /// <summary>
        /// Returns the highest unlocked research order for a specific manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to query.</param>
        /// <returns>The highest unlocked research order.</returns>
        public int GetHighestUnlockedOrder(ManufacturingType manufacturingType)
        {
            return ResearchState.Disciplines[ToResearchDiscipline(manufacturingType)].CurrentOrder;
        }

        /// <summary>
        /// Sets the highest unlocked research order for a specific manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to update.</param>
        /// <param name="order">The new highest unlocked research order.</param>
        public void SetHighestUnlockedOrder(ManufacturingType manufacturingType, int order)
        {
            ResearchDiscipline discipline = ToResearchDiscipline(manufacturingType);
            ResearchState.Disciplines[discipline].CurrentOrder = order;
            ResearchState.Disciplines[discipline].IsExhausted = false;
        }

        /// <summary>
        /// Returns the remaining research capacity for a discipline.
        /// </summary>
        /// <param name="discipline">The discipline to query.</param>
        /// <returns>The remaining research capacity.</returns>
        public int GetResearchCapacityRemaining(ResearchDiscipline discipline)
        {
            return ResearchState.Disciplines[discipline].CapacityRemaining;
        }

        /// <summary>
        /// Sets the remaining research capacity for a discipline.
        /// </summary>
        /// <param name="discipline">The discipline to update.</param>
        /// <param name="capacityRemaining">The new remaining capacity value.</param>
        public void SetResearchCapacityRemaining(
            ResearchDiscipline discipline,
            int capacityRemaining
        )
        {
            ResearchState.Disciplines[discipline].CapacityRemaining = capacityRemaining;
        }

        /// <summary>
        /// Returns whether a discipline has no further advances available.
        /// </summary>
        /// <param name="discipline">The discipline to query.</param>
        /// <returns>True if the discipline is exhausted.</returns>
        public bool IsResearchExhausted(ResearchDiscipline discipline)
        {
            return ResearchState.Disciplines[discipline].IsExhausted;
        }

        /// <summary>
        /// Sets whether a discipline has no further advances available.
        /// </summary>
        /// <param name="discipline">The discipline to update.</param>
        /// <param name="isExhausted">The new exhausted state.</param>
        public void SetResearchExhausted(ResearchDiscipline discipline, bool isExhausted)
        {
            ResearchState.Disciplines[discipline].IsExhausted = isExhausted;
        }

        /// <summary>
        /// Returns the next catalog entry after the current order for a discipline.
        /// </summary>
        /// <param name="discipline">The discipline to query.</param>
        /// <returns>The next catalog entry, or null if none remain.</returns>
        public ResearchCatalogEntry GetNextResearchEntry(ResearchDiscipline discipline)
        {
            if (!ResearchCatalog.TryGetValue(discipline, out List<ResearchCatalogEntry> entries))
                return null;

            int currentOrder = ResearchState.Disciplines[discipline].CurrentOrder;
            return entries.FirstOrDefault(entry => entry.Order > currentOrder);
        }

        /// <summary>
        /// Tries to resolve the catalog entry for a specific discipline and research order.
        /// </summary>
        /// <param name="discipline">The discipline to resolve within.</param>
        /// <param name="order">The research order to resolve.</param>
        /// <param name="entry">The resolved catalog entry, if found.</param>
        /// <returns>True if a matching catalog entry was found.</returns>
        public bool TryResolveResearchEntry(
            ResearchDiscipline discipline,
            int order,
            out ResearchCatalogEntry entry
        )
        {
            entry = null;

            if (!ResearchCatalog.TryGetValue(discipline, out List<ResearchCatalogEntry> entries))
                return false;

            entry = entries.FirstOrDefault(candidate => candidate.Order == order);
            return entry != null;
        }

        /// <summary>
        /// Returns a snapshot of the current research state for one discipline.
        /// </summary>
        /// <param name="discipline">The discipline to inspect.</param>
        /// <returns>The current research snapshot for the discipline.</returns>
        public ResearchProgressSnapshot GetResearchProgressSnapshot(ResearchDiscipline discipline)
        {
            ResearchDisciplineState state = ResearchState.Disciplines[discipline];
            ResearchCatalogEntry nextEntry = GetNextResearchEntry(discipline);
            int? nextEntryScaledDifficulty =
                nextEntry == null
                    ? null
                    : nextEntry.Difficulty * ResearchState.CostScalePercent / 100;

            return new ResearchProgressSnapshot
            {
                CurrentOrder = state.CurrentOrder,
                CapacityRemaining = state.CapacityRemaining,
                IsExhausted = state.IsExhausted,
                NextEntry = nextEntry,
                NextEntryScaledDifficulty = nextEntryScaledDifficulty,
            };
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
        /// Gets the total energy capacity across all owned planets.
        /// </summary>
        /// <returns>The total energy capacity.</returns>
        public int GetTotalEnergyCapacity() => SumPlanetaryResources(p => p.GetEnergyCapacity());

        /// <summary>
        /// Gets the total energy used by facilities across all owned planets.
        /// </summary>
        /// <returns>The total energy used.</returns>
        public int GetTotalEnergyUsed() => SumPlanetaryResources(p => p.GetEnergyUsed());

        /// <summary>
        /// Gets the total available energy across all owned planets.
        /// </summary>
        /// <returns>The total available energy.</returns>
        public int GetTotalAvailableEnergy() => SumPlanetaryResources(p => p.GetAvailableEnergy());

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
        /// Returns all owned entities that implement IManufacturable.
        /// </summary>
        public List<IManufacturable> GetAllOwnedManufacturables()
        {
            return _ownedEntities
                .Where(kvp => typeof(IManufacturable).IsAssignableFrom(kvp.Key))
                .SelectMany(kvp => kvp.Value)
                .OfType<IManufacturable>()
                .ToList();
        }

        /// <summary>
        /// Returns the total maintenance cost of all completed units. Excludes in-progress
        /// construction (which is deducted from the stockpile at enqueue time).
        /// </summary>
        public int GetTotalMaintenanceCost()
        {
            return GetAllOwnedManufacturables()
                .Where(m => m.GetManufacturingStatus() != ManufacturingStatus.Building)
                .Sum(m => m.GetMaintenanceCost());
        }

        /// <summary>
        /// Returns the total construction cost of all units currently under construction.
        /// </summary>
        public int GetTotalInProgressConstructionCost()
        {
            return GetAllOwnedManufacturables()
                .Where(m => m.GetManufacturingStatus() == ManufacturingStatus.Building)
                .Sum(m => m.GetConstructionCost());
        }

        /// <summary>
        /// Returns the total maintenance cost of all completed units plus the construction
        /// cost of all units currently being built.
        /// </summary>
        public int GetTotalUnitCost()
        {
            return GetTotalMaintenanceCost() + GetTotalInProgressConstructionCost();
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
        /// Creates a new fleet with the given capital ships.
        /// Returns a detached fleet — caller must attach to scene graph via game.AttachNode().
        /// Capital ships must be detached (no parent) before passing in.
        /// </summary>
        public Fleet CreateFleet(
            CapitalShip[] capitalShips = null,
            FleetRoleType roleType = FleetRoleType.None
        )
        {
            Fleet fleet = new Fleet(this.InstanceID, $"Fleet {_nextFleetNumber}");
            fleet.RoleType = roleType;

            if (capitalShips != null)
            {
                foreach (CapitalShip ship in capitalShips)
                {
                    if (ship.GetParent() != null)
                        throw new InvalidOperationException(
                            $"Capital ship {ship.GetDisplayName()} must be detached before adding to a new fleet."
                        );

                    fleet.AddChild(ship);
                    ship.SetParent(fleet);
                }
            }

            _nextFleetNumber++;
            return fleet;
        }

        /// <summary>
        /// Returns all officers owned by this faction that are not currently in transit.
        /// </summary>
        /// <returns>A list of movable officers belonging to this faction.</returns>
        public List<Officer> GetAvailableOfficers()
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
        /// Applies a research capacity delta and evaluates at most one order advance.
        /// This mirrors the original side-capacity setter callbacks, which process one
        /// next candidate per invocation instead of draining the full queue in one pass.
        /// </summary>
        /// <param name="discipline">The discipline to update.</param>
        /// <param name="capacityDelta">The capacity delta to add.</param>
        public void ApplyResearchCapacityChange(ResearchDiscipline discipline, int capacityDelta)
        {
            ResearchDisciplineState state = ResearchState.Disciplines[discipline];

            state.CapacityRemaining += capacityDelta;

            ResearchCatalogEntry nextEntry = GetNextResearchEntry(discipline);
            if (nextEntry == null)
            {
                state.IsExhausted = true;
                return;
            }

            int scaledDifficulty = nextEntry.Difficulty * ResearchState.CostScalePercent / 100;
            if (state.CapacityRemaining < scaledDifficulty)
                return;

            state.CurrentOrder = nextEntry.Order;
            state.IsExhausted = false;
            state.CapacityRemaining -= scaledDifficulty;
        }

        /// <summary>
        /// Rebuilds the compatibility queue and discipline catalog from the given templates.
        /// </summary>
        /// <param name="templates">The manufacturable templates to build queues from.</param>
        public void RebuildResearchQueues(IManufacturable[] templates)
        {
            ResearchQueue.Clear();
            ResearchCatalog.Clear();

            foreach (IManufacturable template in templates)
            {
                if (
                    template.AllowedOwnerInstanceIDs?.Count > 0
                    && !template.AllowedOwnerInstanceIDs.Contains(InstanceID)
                )
                    continue;

                ManufacturingType type = template.GetManufacturingType();
                ResearchDiscipline discipline = ToResearchDiscipline(type);
                Technology technology = new Technology(template);

                if (!ResearchQueue.TryGetValue(type, out List<Technology> queue))
                {
                    queue = new List<Technology>();
                    ResearchQueue[type] = queue;
                }

                queue.Add(technology);

                if (
                    !ResearchCatalog.TryGetValue(discipline, out List<ResearchCatalogEntry> entries)
                )
                {
                    entries = new List<ResearchCatalogEntry>();
                    ResearchCatalog[discipline] = entries;
                }

                entries.Add(
                    new ResearchCatalogEntry
                    {
                        Discipline = discipline,
                        Order = technology.GetResearchOrder(),
                        Technology = technology,
                        Difficulty = technology.GetResearchDifficulty(),
                    }
                );
            }

            foreach (List<Technology> queue in ResearchQueue.Values)
                queue.Sort((a, b) => a.GetResearchOrder().CompareTo(b.GetResearchOrder()));

            foreach (List<ResearchCatalogEntry> entries in ResearchCatalog.Values)
                entries.Sort((left, right) => left.Order.CompareTo(right.Order));
        }

        /// <summary>
        /// Maps a manufacturing type to the matching research discipline.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to map.</param>
        /// <returns>The matching research discipline.</returns>
        public static ResearchDiscipline ToResearchDiscipline(ManufacturingType manufacturingType)
        {
            return manufacturingType switch
            {
                ManufacturingType.Ship => ResearchDiscipline.ShipDesign,
                ManufacturingType.Building => ResearchDiscipline.FacilityDesign,
                ManufacturingType.Troop => ResearchDiscipline.TroopTraining,
                _ => throw new ArgumentOutOfRangeException(nameof(manufacturingType)),
            };
        }

        /// <summary>
        /// Maps a research discipline to the matching manufacturing type.
        /// </summary>
        /// <param name="discipline">The research discipline to map.</param>
        /// <returns>The matching manufacturing type.</returns>
        public static ManufacturingType ToManufacturingType(ResearchDiscipline discipline)
        {
            return discipline switch
            {
                ResearchDiscipline.ShipDesign => ManufacturingType.Ship,
                ResearchDiscipline.FacilityDesign => ManufacturingType.Building,
                ResearchDiscipline.TroopTraining => ManufacturingType.Troop,
                _ => throw new ArgumentOutOfRangeException(nameof(discipline)),
            };
        }
    }
}
