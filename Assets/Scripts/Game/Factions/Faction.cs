using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Factions
{
    /// <summary>
    /// Represents a faction in the game, managing its resources, technologies, and owned entities.
    /// </summary>
    public class Faction : BaseGameEntity
    {
        // Faction Info.
        public List<Officer> UnrecruitedOfficers { get; set; } = new List<Officer>();
        public List<string> DisallowedMissionTypeIDs { get; set; } = new List<string>();

        private FactionSettings _settings = new FactionSettings();

        /// <summary>
        /// Faction-specific gameplay settings.
        /// These affect game mechanics regardless of whether faction is player or AI controlled.
        /// </summary>
        public FactionSettings Settings
        {
            get => _settings ??= new FactionSettings();
            set => _settings = value ?? new FactionSettings();
        }

        /// <summary>
        /// Side-level research progression state.
        /// </summary>
        public FactionResearchState ResearchState { get; set; } = new FactionResearchState();

        /// <summary>
        /// Sorted research catalog entries per discipline.
        /// </summary>
        [PersistableIgnore]
        public Dictionary<
            ResearchDiscipline,
            List<ResearchCatalogEntry>
        > ResearchCatalog { get; set; } =
            new Dictionary<ResearchDiscipline, List<ResearchCatalogEntry>>();

        /// <summary>
        /// Compatibility view of research catalog entries grouped by manufacturing type.
        /// </summary>
        [PersistableIgnore]
        public Dictionary<ManufacturingType, List<Technology>> ResearchQueue { get; set; } =
            new Dictionary<ManufacturingType, List<Technology>>();

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

        public int RawMaterials => RawMaterialStockpile;

        public int RefinedMaterials => RefinedMaterialStockpile;

        public int RawMaterialSupply =>
            GetTotalAvailableMinedResources() * Settings.RefinementMultiplier;

        public int RefinedMaterialSupply =>
            GetTotalAvailableMaterialsRaw() * Settings.RefinementMultiplier;

        public int MaintenanceCapacity =>
            GetTotalAvailableMaterialsRaw() * Settings.ResourceProcessingPointsPerFacility;

        public int MaintenanceHeadroom => ProjectedMaintenanceHeadroom;

        public int ProjectedMaintenanceHeadroom =>
            MaintenanceCapacity - GetTotalProjectedMaintenanceCost();

        public int GetProjectedMaintenanceHeadroom(IManufacturable item)
        {
            return MaintenanceCapacity - GetTotalProjectedMaintenanceCost(item);
        }

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
            { typeof(SpecialForces), new List<ISceneNode>() },
            { typeof(Starfighter), new List<ISceneNode>() },
        };

        // Messages and Notifications.
        public Dictionary<MessageType, List<Message>> Messages = new Dictionary<
            MessageType,
            List<Message>
        >()
        {
            { MessageType.PopularSupport, new List<Message>() },
            { MessageType.Fleet, new List<Message>() },
            { MessageType.Mission, new List<Message>() },
            { MessageType.Resource, new List<Message>() },
            { MessageType.Manufacturing, new List<Message>() },
            { MessageType.Defense, new List<Message>() },
            { MessageType.Conflict, new List<Message>() },
            { MessageType.Chat, new List<Message>() },
            { MessageType.Advice, new List<Message>() },
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
        /// Returns colonized planets owned by the faction.
        /// </summary>
        /// <returns>Owned colonized planets.</returns>
        public List<Planet> GetOwnedColonizedPlanets()
        {
            return GetOwnedUnitsByType<Planet>().Where(planet => planet.IsColonized).ToList();
        }

        /// <summary>
        /// Returns the closest currently owned planet to a position.
        /// </summary>
        /// <param name="position">The position to measure from.</param>
        /// <param name="exclude">A planet to exclude from the search.</param>
        /// <returns>The closest owned planet, or null.</returns>
        public Planet GetNearestOwnedPlanetTo(Point position, Planet exclude = null)
        {
            return GetOwnedUnitsByType<Planet>()
                .Where(planet => planet != exclude && planet.GetOwnerInstanceID() == InstanceID)
                .OrderBy(planet => planet.GetRawDistanceTo(position))
                .FirstOrDefault();
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
        /// Returns technologies unlocked for a specific research discipline.
        /// </summary>
        /// <param name="discipline">The research discipline to query.</param>
        /// <returns>Unlocked technologies for the given discipline.</returns>
        public List<Technology> GetUnlockedTechnologies(ResearchDiscipline discipline)
        {
            if (!ResearchCatalog.TryGetValue(discipline, out List<ResearchCatalogEntry> entries))
            {
                ManufacturingType manufacturingType = discipline.ToManufacturingType();
                return ResearchQueue.TryGetValue(
                    manufacturingType,
                    out List<Technology> researchQueue
                )
                    ? researchQueue.ToList()
                    : new List<Technology>();
            }

            int unlockedOrder = GetHighestUnlockedOrder(discipline);
            return entries
                .Where(entry => entry.Order <= unlockedOrder)
                .Select(entry => entry.Technology)
                .ToList();
        }

        /// <summary>
        /// Returns technologies unlocked for a manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to query.</param>
        /// <returns>Unlocked technologies for the given manufacturing type.</returns>
        public List<Technology> GetUnlockedTechnologies(ManufacturingType manufacturingType)
        {
            return GetUnlockedTechnologies(manufacturingType.ToResearchDiscipline());
        }

        /// <summary>
        /// Returns the next technology to be researched for a discipline,
        /// or null if all technologies are unlocked.
        /// </summary>
        /// <param name="discipline">The research discipline to query.</param>
        /// <returns>The next technology to research, or null if all are unlocked.</returns>
        public Technology GetCurrentResearchTarget(ResearchDiscipline discipline)
        {
            return GetNextResearchEntry(discipline)?.Technology;
        }

        /// <summary>
        /// Returns the next technology to be researched for a manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to query.</param>
        /// <returns>The next technology to research, or null if all are unlocked.</returns>
        public Technology GetCurrentResearchTarget(ManufacturingType manufacturingType)
        {
            return GetCurrentResearchTarget(manufacturingType.ToResearchDiscipline());
        }

        /// <summary>
        /// Returns the highest unlocked research order for a discipline.
        /// </summary>
        /// <param name="discipline">The research discipline to query.</param>
        /// <returns>The highest unlocked research order.</returns>
        public int GetHighestUnlockedOrder(ResearchDiscipline discipline)
        {
            return ResearchState.Disciplines[discipline].CurrentOrder;
        }

        /// <summary>
        /// Returns the highest unlocked research order for a manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to query.</param>
        /// <returns>The highest unlocked research order.</returns>
        public int GetHighestUnlockedOrder(ManufacturingType manufacturingType)
        {
            return GetHighestUnlockedOrder(manufacturingType.ToResearchDiscipline());
        }

        /// <summary>
        /// Sets the highest unlocked research order for a discipline.
        /// </summary>
        /// <param name="discipline">The research discipline to update.</param>
        /// <param name="order">The new highest unlocked research order.</param>
        public void SetHighestUnlockedOrder(ResearchDiscipline discipline, int order)
        {
            ResearchState.Disciplines[discipline].CurrentOrder = order;
        }

        /// <summary>
        /// Sets the highest unlocked research order for a manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to update.</param>
        /// <param name="order">The new highest unlocked research order.</param>
        public void SetHighestUnlockedOrder(ManufacturingType manufacturingType, int order)
        {
            SetHighestUnlockedOrder(manufacturingType.ToResearchDiscipline(), order);
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
        /// Returns whether a discipline has no further advances available.
        /// Derived from the catalog: true iff there is no entry above the current order.
        /// </summary>
        /// <param name="discipline">The discipline to query.</param>
        /// <returns>True if the discipline is exhausted.</returns>
        public bool IsResearchExhausted(ResearchDiscipline discipline)
        {
            return GetNextResearchEntry(discipline) == null;
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
        /// Returns the total maintenance cost of all completed units.
        /// </summary>
        /// <returns>The total maintenance burden for the faction.</returns>
        public int GetTotalMaintenanceCost()
        {
            return GetAllOwnedManufacturables()
                .Where(m => m.GetManufacturingStatus() == ManufacturingStatus.Complete)
                .Sum(m => m.GetMaintenanceCost());
        }

        /// <summary>
        /// Returns the projected maintenance burden of completed and in-progress manufacturables.
        /// </summary>
        /// <returns>The total projected maintenance burden for the faction.</returns>
        public int GetTotalProjectedMaintenanceCost()
        {
            return GetAllOwnedManufacturables()
                .Where(m =>
                    m.GetManufacturingStatus()
                        is ManufacturingStatus.Complete
                            or ManufacturingStatus.Building
                )
                .Sum(m => m.GetMaintenanceCost());
        }

        /// <summary>
        /// Returns the projected maintenance burden if another manufacturable is committed.
        /// </summary>
        /// <param name="item">The manufacturable being evaluated.</param>
        /// <returns>The projected maintenance burden including the supplied manufacturable.</returns>
        public int GetTotalProjectedMaintenanceCost(IManufacturable item)
        {
            int projectedMaintenanceCost = GetTotalProjectedMaintenanceCost();
            if (item != null)
            {
                projectedMaintenanceCost += item.GetMaintenanceCost();
            }

            return projectedMaintenanceCost;
        }

        /// <summary>
        /// Returns the total construction cost of all owned manufacturables that are still
        /// being built.
        /// </summary>
        /// <returns>The total in-progress construction cost.</returns>
        public int GetTotalInProgressConstructionCost()
        {
            return GetAllOwnedManufacturables()
                .Where(m => m.GetManufacturingStatus() == ManufacturingStatus.Building)
                .Sum(m => m.GetConstructionCost());
        }

        /// <summary>
        /// Adds a message to the faction's message list.
        /// </summary>
        /// <param name="message">The message to add.</param>
        public void AddMessage(Message message)
        {
            if (message == null)
                return;

            Messages ??= new Dictionary<MessageType, List<Message>>();

            if (!Messages.TryGetValue(message.Type, out List<Message> messages))
            {
                messages = new List<Message>();
                Messages[message.Type] = messages;
            }

            messages.Add(message);
        }

        /// <summary>
        /// Removes a message from the faction's message list.
        /// </summary>
        /// <param name="message">The message to remove.</param>
        public void RemoveMessage(Message message)
        {
            if (message == null)
                return;

            if (Messages == null)
                return;

            if (Messages.TryGetValue(message.Type, out List<Message> messages))
                messages.Remove(message);
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
        /// Returns mission participants owned by this faction that can receive mission orders.
        /// </summary>
        /// <returns>Available mission participants in deterministic order.</returns>
        public List<IMissionParticipant> GetAvailableMissionParticipants()
        {
            return GetOwnedUnitsByType<Officer>()
                .Cast<IMissionParticipant>()
                .Concat(GetOwnedUnitsByType<SpecialForces>())
                .Where(IsAvailableMissionParticipant)
                .OrderBy(participant =>
                    participant
                        .GetParentOfType<Planet>()
                        ?.GetParentOfType<PlanetSystem>()
                        ?.PositionX
                    ?? 0
                )
                .ThenBy(participant => participant.GetParentOfType<Planet>()?.InstanceID)
                .ThenBy(participant => participant.InstanceID)
                .ToList();
        }

        /// <summary>
        /// Returns whether a mission participant can currently receive mission orders.
        /// </summary>
        /// <param name="participant">The participant to inspect.</param>
        /// <returns>True if the participant can currently receive mission orders.</returns>
        private bool IsAvailableMissionParticipant(IMissionParticipant participant)
        {
            if (participant == null || participant.OwnerInstanceID != InstanceID)
                return false;

            if (participant.IsOnMission() || !participant.IsMovable())
                return false;

            if (participant is Officer officer)
                return !officer.IsCaptured && !officer.IsKilled;

            if (participant is SpecialForces specialForces)
            {
                return specialForces.ManufacturingStatus == ManufacturingStatus.Complete
                    && specialForces.AllowedMissionTypeIDs.Count > 0;
            }

            return true;
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

            return GetNearestOwnedPlanetTo(sourcePlanet.GetPosition());
        }

        /// <summary>
        /// Adds research progress to a discipline and advances the order if the new
        /// capacity covers the next entry's cost.
        /// </summary>
        /// <param name="discipline">The discipline to update.</param>
        /// <param name="amount">The progress amount to add (may be negative).</param>
        /// <returns>The technology unlocked by this call, or null if no advance occurred.</returns>
        public Technology ApplyResearchProgress(ResearchDiscipline discipline, int amount)
        {
            ResearchDisciplineState state = ResearchState.Disciplines[discipline];
            state.CapacityRemaining += amount;

            ResearchCatalogEntry nextEntry = GetNextResearchEntry(discipline);
            if (nextEntry == null)
                return null;

            int scaledDifficulty = nextEntry.Difficulty * ResearchState.CostScalePercent / 100;
            if (state.CapacityRemaining < scaledDifficulty)
                return null;

            state.CurrentOrder = nextEntry.Order;
            state.CapacityRemaining -= scaledDifficulty;
            return nextEntry.Technology;
        }

        /// <summary>
        /// Rebuilds the discipline catalog from the given manufacturable templates.
        /// </summary>
        /// <param name="templates">The manufacturable templates to build the catalog from.</param>
        public void RebuildResearchCatalog(IManufacturable[] templates)
        {
            ResearchCatalog.Clear();
            ResearchQueue.Clear();

            foreach (IManufacturable template in templates)
            {
                if (
                    template.AllowedOwnerInstanceIDs?.Count > 0
                    && !template.AllowedOwnerInstanceIDs.Contains(InstanceID)
                )
                    continue;

                ResearchDiscipline discipline = template
                    .GetManufacturingType()
                    .ToResearchDiscipline();
                ManufacturingType manufacturingType = GetResearchQueueType(template);
                Technology technology = new Technology(template);

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

                if (
                    !ResearchQueue.TryGetValue(
                        manufacturingType,
                        out List<Technology> researchQueue
                    )
                )
                {
                    researchQueue = new List<Technology>();
                    ResearchQueue[manufacturingType] = researchQueue;
                }

                researchQueue.Add(technology);
            }

            foreach (List<ResearchCatalogEntry> entries in ResearchCatalog.Values)
                entries.Sort((left, right) => left.Order.CompareTo(right.Order));

            foreach (List<Technology> researchQueue in ResearchQueue.Values)
                researchQueue.Sort(
                    (left, right) => left.GetResearchOrder().CompareTo(right.GetResearchOrder())
                );
        }

        private static ManufacturingType GetResearchQueueType(IManufacturable template)
        {
            return template switch
            {
                Regiment => ManufacturingType.Troop,
                _ => template.GetManufacturingType(),
            };
        }
    }
}
