using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.SceneGraph;
using Rebellion.Util.Attributes;

namespace Rebellion.Game
{
    /// <summary>
    /// Represents a planet in the game. A planet is a scene node that can contain fleets,
    /// officers, regiments, missions, and buildings. It also has a popular support rating,
    /// which is a measure of how much the planet's population supports a given faction.
    /// </summary>
    public class Planet : ContainerNode
    {
        // Planet Properties
        public bool IsColonized { get; set; }
        public int NumRawResourceNodes { get; set; }
        public int EnergyCapacity { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }

        // Planet Asset Info
        public string PlanetIconPath { get; set; }

        // Planet Status
        public bool IsInUprising { get; set; }
        public bool IsDestroyed { get; set; }
        public bool IsHeadquarters { get; set; }

        // Popular Support
        public Dictionary<string, int> PopularSupport = new Dictionary<string, int>();

        // Child Nodes
        public List<Fleet> Fleets = new List<Fleet>();
        public List<Officer> Officers = new List<Officer>();
        public List<Regiment> Regiments = new List<Regiment>();
        public List<Starfighter> Starfighters = new List<Starfighter>();
        public List<Mission> Missions = new List<Mission>();
        public List<Building> Buildings = new List<Building>();

        // Manufacturing Status
        [PersistableIgnore]
        public Dictionary<ManufacturingType, List<IManufacturable>> ManufacturingQueue { get; } =
            new Dictionary<ManufacturingType, List<IManufacturable>>();

        // Visitor Status
        public List<string> VisitingFactionIDs = new List<string>();

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Planet() { }

        /// <summary>
        /// Checks if the planet is blockaded.
        /// A planet is blockaded ONLY when hostile fleets are present AND no defending fleets exist.
        /// </summary>
        /// <returns>True if the planet is blockaded, false otherwise.</returns>
        public bool IsBlockaded()
        {
            // Neutral planets cannot be blockaded
            if (string.IsNullOrEmpty(OwnerInstanceID))
                return false;

            bool hasHostile = Fleets.Any(f => f.OwnerInstanceID != OwnerInstanceID);
            bool hasDefender = Fleets.Any(f => f.OwnerInstanceID == OwnerInstanceID);

            return hasHostile && !hasDefender;
        }

        /// <summary>
        /// Checks if the planet is contested (has any hostile fleets present, regardless of defenders).
        /// </summary>
        /// <returns>True if any hostile fleet is present, false otherwise.</returns>
        public bool IsContested()
        {
            return Fleets.Any(fleet => fleet.OwnerInstanceID != this.OwnerInstanceID);
        }

        /// <summary>
        /// Returns true if planet has any faction presence.
        /// Used by uprising system to filter unpopulated planets.
        /// </summary>
        /// <returns>True if any faction has popular support on this planet.</returns>
        public bool IsPopulated()
        {
            return PopularSupport.Any(kvp => kvp.Value > 0);
        }

        /// <summary>
        /// Loyalty value for uprising calculation.
        /// Owner's popularity scaled to [-50, +50]. Negative = danger zone.
        /// Self-sufficient - no Game dependency required.
        /// </summary>
        /// <returns>Loyalty in [-50, +50]; negative means uprising risk.</returns>
        public int CalculateLoyalty()
        {
            if (string.IsNullOrEmpty(OwnerInstanceID))
                return 0;

            float ownerPop = PopularSupport.TryGetValue(OwnerInstanceID, out int pop)
                ? pop / 100f
                : 0.5f;

            return (int)(ownerPop * 100f) - 50;
        }

        /// <summary>
        /// Begin uprising - set uprising flag then transfer ownership.
        /// Order matters: uprising state is set before ownership changes.
        /// Centralizes all uprising state mutations in one place.
        /// </summary>
        public void BeginUprising()
        {
            IsInUprising = true;
        }

        /// <summary>
        /// End uprising - clear uprising flag.
        /// Ownership remains with current owner.
        /// </summary>
        public void EndUprising()
        {
            IsInUprising = false;
        }

        /// <summary>
        /// Gets the total energy capacity for this planet.
        /// Each facility uses 1 energy; this caps how many facilities can exist.
        /// </summary>
        /// <returns>Maximum number of energy units available.</returns>
        public int GetEnergyCapacity()
        {
            return EnergyCapacity;
        }

        /// <summary>
        /// Gets the number of energy units currently consumed by facilities on this planet.
        /// </summary>
        /// <returns>Number of energy units in use.</returns>
        public int GetEnergyUsed()
        {
            return Buildings.Count;
        }

        /// <summary>
        /// Gets the remaining energy available for new facilities.
        /// </summary>
        /// <returns>Remaining energy units (capacity minus used).</returns>
        public int GetAvailableEnergy()
        {
            return Math.Max(0, EnergyCapacity - GetEnergyUsed());
        }

        /// <summary>
        /// Gets the total number of raw resource nodes available on the planet or system.
        /// This represents the maximum number of resources that can be utilized.
        /// </summary>
        /// <returns>The total number of raw resource nodes.</returns>
        public int GetRawResourceNodes()
        {
            return NumRawResourceNodes;
        }

        /// <summary>
        /// Gets the number of available resource nodes that are not blockaded.
        /// If the location is blockaded, no resources can be accessed.
        /// </summary>
        /// <returns>The number of accessible resource nodes, or 0 if blockaded.</returns>
        public int GetAvailableResourceNodes()
        {
            return IsBlockaded() ? 0 : GetRawResourceNodes();
        }

        /// <summary>
        /// Gets the total number of mined resource nodes, capped by the raw resource node count.
        /// This reflects the effective number of resource nodes that can be mined based on mining buildings.
        /// </summary>
        /// <returns>The total number of mined resource nodes, limited by the raw node count.</returns>
        public int GetRawMinedResources()
        {
            int mineCount = GetBuildingTypeCount(BuildingType.Mine, EntityStateFilter.All);
            return Math.Min(NumRawResourceNodes, mineCount);
        }

        /// <summary>
        /// Gets the number of mined resources that are available and not under construction.
        /// If the location is blockaded, no resources are available.
        /// </summary>
        /// <returns>The number of available mined resources, or 0 if blockaded.</returns>
        public int GetAvailableMinedResources()
        {
            if (IsBlockaded())
            {
                return 0;
            }

            int mineCount = GetBuildingTypeCount(BuildingType.Mine);
            return Math.Min(NumRawResourceNodes, mineCount);
        }

        /// <summary>
        /// Gets the total refinement capacity based on available refinery buildings.
        /// This represents the maximum number of resources that can be refined.
        /// </summary>
        /// <returns>The total refinement capacity.</returns>
        public int GetRawRefinementCapacity()
        {
            return GetBuildingTypeCount(BuildingType.Refinery, EntityStateFilter.All);
        }

        /// <summary>
        /// Gets the available refinement capacity that is not blockaded and excludes refineries under construction.
        /// If the location is blockaded, no refinement capacity is available.
        /// </summary>
        /// <returns>The available refinement capacity, or 0 if blockaded.</returns>
        public int GetAvailableRefinementCapacity()
        {
            if (IsBlockaded())
            {
                return 0;
            }

            return GetBuildingTypeCount(BuildingType.Refinery);
        }

        /// <summary>
        /// Returns the popular support for a faction on the planet.
        /// </summary>
        /// <param name="factionInstanceId">The instance ID of the faction.</param>
        /// <returns>The popular support for the faction.</returns>
        public int GetPopularSupport(string factionInstanceId)
        {
            return PopularSupport.TryGetValue(factionInstanceId, out int support) ? support : 0;
        }

        /// <summary>
        /// Sets the popular support for a faction on the planet.
        /// </summary>
        /// <param name="factionInstanceId">The instance ID of the faction.</param>
        /// <param name="support">The new level of support for the faction.</param>
        /// <param name="maxSupport">The maximum total support allowed across all factions.</param>
        public void SetPopularSupport(string factionInstanceId, int support, int maxSupport)
        {
            // Calculate the difference between the new support and the current support.
            int currentSupport = PopularSupport.TryGetValue(
                factionInstanceId,
                out int existingSupport
            )
                ? existingSupport
                : 0;
            int supportDifference = support - currentSupport;
            int totalSupport = PopularSupport.Values.Sum();

            // Check if the total support is within the maximum limit.
            if (totalSupport + supportDifference <= maxSupport)
            {
                PopularSupport[factionInstanceId] = support;
            }
            // If the total support exceeds the maximum limit, shift support from other factions.
            else
            {
                int overage = totalSupport + supportDifference - maxSupport;
                ShiftFactionSupport(factionInstanceId, overage);
                PopularSupport[factionInstanceId] = support;
            }
        }

        /// <summary>
        /// Shifts the support of other factions to accommodate the increase in support for the given faction.
        /// </summary>
        /// <param name="excludedFactionId">The faction ID to exclude from reduction.</param>
        /// <param name="overage">The amount of support to reduce from other factions.</param>
        private void ShiftFactionSupport(string excludedFactionId, int overage)
        {
            PopularSupport
                .Where(kvp => kvp.Key != excludedFactionId) // Filter out the excluded faction.
                .OrderByDescending(kvp => kvp.Value) // Sort factions by support in descending order.
                .ToList()
                .ForEach(kvp =>
                {
                    // Decrease faction's support by the overage amount.
                    int reduction = Math.Min(overage, kvp.Value);
                    PopularSupport[kvp.Key] -= reduction;
                    overage -= reduction;

                    if (overage <= 0)
                    {
                        return;
                    }
                });
        }

        /// <summary>
        /// Gets the position of the planet as a Point.
        /// </summary>
        /// <returns>A Point representing the planet's position.</returns>
        public Point GetPosition()
        {
            return new Point(PositionX, PositionY);
        }

        /// <summary>
        /// Calculates the raw Euclidean distance to another planet.
        /// Used for comparison/sorting. For scaled travel time, use GetDistanceTo with config parameters.
        /// </summary>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>Raw Euclidean distance.</returns>
        public double GetRawDistanceTo(Planet targetPlanet)
        {
            int dx = this.PositionX - targetPlanet.PositionX;
            int dy = this.PositionY - targetPlanet.PositionY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Calculates the travel time to another planet using Euclidean distance.
        /// The travel time is calculated in ticks.
        /// </summary>
        /// <param name="targetPlanet">The target planet.</param>
        /// <param name="divisor">The distance divisor for scaling (from config).</param>
        /// <param name="distanceBase">The distance base for scaling (from config).</param>
        /// <returns>Travel time in ticks.</returns>
        public int GetDistanceTo(Planet targetPlanet, int divisor, int distanceBase)
        {
            double rawDistance = GetRawDistanceTo(targetPlanet);
            return (int)(rawDistance / divisor * distanceBase / 100);
        }

        /// <summary>
        /// Returns the path to the planet's icon asset.
        /// This is used by the UI to load and display the correct icon for the planet.
        /// </summary>
        /// <returns>The path to the planet's icon asset.</returns>
        public string GetPlanetIconPath()
        {
            return PlanetIconPath;
        }

        /// <summary>
        /// Calculates the total number of days required to produce a specified number of manufacturable items.
        /// </summary>
        /// <param name="manufacturable">The item to be manufactured, which implements IManufacturable.</param>
        /// <param name="quantity">The number of items to manufacture.</param>
        /// <returns>The total days required to complete the manufacturing.</returns>
        public int GetBuildTime(IManufacturable manufacturable, int quantity)
        {
            // Calculate the total material cost and the combined production rate for the manufacturable.
            int totalMaterialCost = manufacturable.GetConstructionCost() * quantity;

            // Calculate the combined time required to produce the items.
            ManufacturingType requiredManufacturingType = manufacturable.GetManufacturingType();
            double combinedRate = GetCombinedProductionRate(requiredManufacturingType);

            if (combinedRate == 0)
            {
                return 0;
            }

            // Calculate the combined time required to produce the items.
            double combinedTime = totalMaterialCost / combinedRate;

            return (int)Math.Ceiling(combinedTime);
        }

        /// <summary>
        /// Calculates the combined production rate for a specific manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to calculate the rate for.</param>
        /// <returns>The combined production rate.</returns>
        private double GetCombinedProductionRate(ManufacturingType manufacturingType)
        {
            return Buildings
                .Where(building =>
                    building.GetProductionType() == manufacturingType
                    && building.GetManufacturingStatus() == ManufacturingStatus.Complete
                )
                .Sum(building => 1.0 / building.GetProcessRate());
        }

        /// <summary>
        /// Calculates the total production progress per tick for a given manufacturing type on a planet.
        /// </summary>
        /// <param name="type">The manufacturing type.</param>
        /// <returns>The calculated progress.</returns>
        public int GetProductionRate(ManufacturingType type)
        {
            double combinedRate = GetCombinedProductionRate(type);

            return (int)Math.Ceiling(combinedRate);
        }

        /// <summary>
        /// Checks if units can be manufactured on this planet.
        /// </summary>
        /// <returns>True if units can be manufactured, false otherwise.</returns>
        public bool CanManufactureUnits()
        {
            return !IsBlockaded() && !IsDestroyed && !IsInUprising;
        }

        /// <summary>
        /// Gets the manufacturing queue for the planet.
        /// </summary>
        /// <returns>The manufacturing queue.</returns>
        public Dictionary<ManufacturingType, List<IManufacturable>> GetManufacturingQueue()
        {
            return ManufacturingQueue;
        }

        /// <summary>
        /// Adds a manufacturable unit to the manufacturing queue.
        /// </summary>
        /// <param name="manufacturable">The unit to be added to the manufacturing queue.</param>
        public void AddToManufacturingQueue(IManufacturable manufacturable)
        {
            ValidateManufacturable(manufacturable);
            ManufacturingType type = manufacturable.GetManufacturingType();

            if (!ManufacturingQueue.ContainsKey(type))
            {
                ManufacturingQueue.Add(type, new List<IManufacturable>());
            }

            // Don't call SetPosition - units in queue aren't built yet, position comes from parent planet when completed
            ManufacturingQueue[type].Add(manufacturable);
        }

        /// <summary>
        /// Validates if a manufacturable can be added to the manufacturing queue.
        /// </summary>
        /// <param name="manufacturable">The manufacturable to validate.</param>
        ///
        private void ValidateManufacturable(IManufacturable manufacturable)
        {
            if (manufacturable is ISceneNode sceneNode && sceneNode.GetParent() == null)
            {
                throw new InvalidOperationException(
                    $"Unit {sceneNode.GetDisplayName()} must have a parent to be added to the manufacturing queue."
                );
            }

            if (this.OwnerInstanceID != manufacturable.OwnerInstanceID)
            {
                throw new SceneAccessException(manufacturable, this);
            }
        }

        /// <summary>
        /// Retrieves the count of idle production facilities for a specific manufacturing type.
        /// </summary>
        /// <param name="type">The manufacturing type.</param>
        /// <returns>The count of idle production facilities of the specified type.</returns>
        public int GetIdleManufacturingFacilities(ManufacturingType type)
        {
            if (
                ManufacturingQueue.TryGetValue(type, out List<IManufacturable> manufacturingQueue)
                && manufacturingQueue.Count > 0
            )
            {
                return 0;
            }

            return Buildings.Count(building =>
                building.GetProductionType() == type
                && building.GetManufacturingStatus() == ManufacturingStatus.Complete
                && building.Movement == null
            );
        }

        /// <summary>
        /// Records that the given faction has visited this planet.
        /// </summary>
        /// <param name="factionInstanceId">The instance ID of the visiting faction.</param>
        public void AddVisitor(string factionInstanceId)
        {
            if (!VisitingFactionIDs.Contains(factionInstanceId))
            {
                VisitingFactionIDs.Add(factionInstanceId);
            }
        }

        /// <summary>
        /// Returns true if the given faction has visited this planet.
        /// </summary>
        /// <param name="factionInstanceId">The instance ID of the faction to check.</param>
        /// <returns>True if the faction has visited; otherwise false.</returns>
        public bool WasVisitedBy(string factionInstanceId)
        {
            return VisitingFactionIDs.Contains(factionInstanceId);
        }

        /// <summary>
        /// Gets the fleets on the planet.
        /// </summary>
        /// <returns>A list of fleets.</returns>
        public List<Fleet> GetFleets()
        {
            return Fleets;
        }

        /// <summary>
        /// Adds a fleet to the planet.
        /// </summary>
        /// <param name="fleet">The fleet to add.</param>
        public void AddFleet(Fleet fleet)
        {
            Fleets.Add(fleet);
        }

        /// <summary>
        /// Removes a fleet from the planet.
        /// </summary>
        /// <param name="fleet">The fleet to remove.</param>
        public void RemoveFleet(Fleet fleet)
        {
            Fleets.Remove(fleet);
        }

        /// <summary>
        /// Gets all buildings on the planet.
        /// </summary>
        /// <returns>A list of buildings.</returns>
        public List<Building> GetAllBuildings()
        {
            return Buildings.ToList();
        }

        /// <summary>
        /// Gets the buildings of a specific production type.
        /// </summary>
        /// <param name="productionType">The production type.</param>
        /// <returns>A list of buildings of the specified production type.</returns>
        public List<Building> GetBuildings(ManufacturingType productionType)
        {
            return Buildings
                .Where(building => building.GetProductionType() == productionType)
                .ToList();
        }

        /// <summary>
        /// Calculates the production modifier applied when this planet is under blockade.
        /// Returns a value from 0 to 100, where 100 means no reduction and 0 means fully
        /// suppressed. Each hostile capital ship and starfighter reduces the modifier by the
        /// supplied penalty values.
        /// </summary>
        /// <param name="capitalShipPenalty">Reduction per hostile capital ship.</param>
        /// <param name="fighterPenalty">Reduction per hostile starfighter.</param>
        /// <returns>A production modifier percentage in the range [0, 100].</returns>
        public int GetBlockadeModifier(int capitalShipPenalty, int fighterPenalty)
        {
            string ownerId = GetOwnerInstanceID();

            int hostileCapitalShips = Fleets
                .Where(f => f.GetOwnerInstanceID() != null && f.GetOwnerInstanceID() != ownerId)
                .Sum(f => f.CapitalShips.Count);

            int hostileFighters = Starfighters.Count(s =>
                s.GetOwnerInstanceID() != null && s.GetOwnerInstanceID() != ownerId
            );

            int modifier =
                100 - hostileCapitalShips * capitalShipPenalty - hostileFighters * fighterPenalty;
            return Math.Max(0, modifier);
        }

        /// <summary>
        /// Calculates total defense strength from defensive buildings.
        /// </summary>
        /// <param name="filter">Which building states to include (default Active).</param>
        /// <returns>Sum of shield strength from Shield buildings (type 0x24 only).</returns>
        public int GetDefenseStrength(EntityStateFilter filter = EntityStateFilter.Active)
        {
            return GetAllBuildings()
                .Where(b =>
                    b.DefenseFacilityClass == DefenseFacilityClass.Shield
                    && IsEntityActive(b, filter)
                )
                .Sum(b => b.ShieldStrength);
        }

        /// <summary>
        /// Gets the count of buildings of a specific type.
        /// </summary>
        /// <param name="buildingType">The type of building to count.</param>
        /// <param name="filter">Active (default) counts only operational buildings; All includes under construction and in transit.</param>
        /// <returns>The count of buildings of the specified type.</returns>
        public int GetBuildingTypeCount(
            BuildingType buildingType,
            EntityStateFilter filter = EntityStateFilter.Active
        )
        {
            return Buildings.Count(b =>
                b.GetBuildingType() == buildingType && IsEntityActive(b, filter)
            );
        }

        private static bool IsEntityActive(IManufacturable entity, EntityStateFilter filter)
        {
            if (filter == EntityStateFilter.All)
                return true;
            return entity.ManufacturingStatus == ManufacturingStatus.Complete
                && ((IMovable)entity).Movement == null;
        }

        /// <summary>
        /// Adds a building to the planet.
        /// </summary>
        /// <param name="building">The building to add.</param>
        /// <exception cref="InvalidOperationException">Thrown when the planet is not colonized or at capacity.</exception>
        private void AddBuilding(Building building)
        {
            ValidateBuilding(building);
            Buildings.Add(building);
        }

        /// <summary>
        /// Validates if a building can be added to the planet.
        /// </summary>
        /// <param name="building">The building to validate.</param>
        private void ValidateBuilding(Building building)
        {
            // Check if the planet is colonized.
            if (!IsColonized)
            {
                throw new InvalidOperationException(
                    $"Cannot add building {building.GetDisplayName()} to {this.GetDisplayName()}. Planet is not colonized."
                );
            }

            // Check if the building is owned by the planet's owner.
            if (building.GetOwnerInstanceID() != this.GetOwnerInstanceID())
            {
                throw new SceneAccessException(building, this);
            }

            // Check if the planet is at energy capacity.
            if (GetAvailableEnergy() <= 0)
            {
                throw new InvalidOperationException(
                    $"Cannot add {building.GetDisplayName()} to {this.GetDisplayName()}. Planet is at capacity."
                );
            }
        }

        /// <summary>
        /// Removes a building from the planet.
        /// </summary>
        /// <param name="building">The building to remove.</param>
        private void RemoveBuilding(Building building)
        {
            Buildings.Remove(building);
        }

        /// <summary>
        /// Adds an officer to the planet.
        /// </summary>
        /// <param name="officer">The officer to add.</param>
        private void AddOfficer(Officer officer)
        {
            if (!officer.IsCaptured && officer.GetOwnerInstanceID() != this.OwnerInstanceID)
            {
                throw new SceneAccessException(officer, this);
            }
            Officers.Add(officer);
        }

        /// <summary>
        /// Removes an officer from the planet.
        /// </summary>
        /// <param name="officer">The officer to remove.</param>
        private void RemoveOfficer(Officer officer)
        {
            Officers.Remove(officer);
        }

        /// <summary>
        /// Gets the missions on the planet.
        /// </summary>
        /// <returns>All missions currently active on this planet.</returns>
        public List<Mission> GetMissions()
        {
            return Missions;
        }

        /// <summary>
        /// Returns the owner instance IDs of all factions with active missions on this planet.
        /// </summary>
        /// <returns>A list of faction instance IDs.</returns>
        public List<string> GetMissionFactionInstanceIDs()
        {
            return Missions.ConvertAll(mission => mission.GetOwnerInstanceID());
        }

        /// <summary>
        /// Adds a mission to the planet.
        /// </summary>
        /// <param name="mission">The mission to add.</param>
        private void AddMission(Mission mission)
        {
            Missions.Add(mission);
        }

        /// <summary>
        /// Removes a mission from the planet.
        /// </summary>
        /// <param name="mission">The mission to remove.</param>
        private void RemoveMission(Mission mission)
        {
            Missions.Remove(mission);
        }

        /// <summary>
        /// Adds a regiment to the planet.
        /// </summary>
        /// <param name="regiment">The regiment to add.</param>
        private void AddRegiment(Regiment regiment)
        {
            if (regiment.GetOwnerInstanceID() != this.GetOwnerInstanceID())
                throw new SceneAccessException(regiment, this);

            Regiments.Add(regiment);
        }

        /// <summary>
        /// Removes a regiment from the planet.
        /// </summary>
        /// <param name="regiment">The regiment to remove.</param>
        private void RemoveRegiment(Regiment regiment)
        {
            Regiments.Remove(regiment);
        }

        /// <summary>
        /// Adds a starfighter to the planet.
        /// </summary>
        /// <param name="starfighter">The starfighter to add.</param>
        private void AddStarfighter(Starfighter starfighter)
        {
            if (starfighter.GetOwnerInstanceID() != this.GetOwnerInstanceID())
                throw new SceneAccessException(starfighter, this);

            Starfighters.Add(starfighter);
        }

        /// <summary>
        /// Removes a starfighter from the planet.
        /// </summary>
        /// <param name="starfighter">The starfighter to remove.</param>
        private void RemoveStarfighter(Starfighter starfighter)
        {
            Starfighters.Remove(starfighter);
        }

        /// <summary>
        /// Returns true if any regiments, officers, or starfighters are present on the planet.
        /// </summary>
        /// <returns>True if any garrison units are present; otherwise false.</returns>
        public bool HasGarrison()
        {
            return Regiments.Count > 0 || Officers.Count > 0 || Starfighters.Count > 0;
        }

        /// <summary>
        /// Returns all officers on the planet.
        /// </summary>
        /// <returns>A list of officers currently on this planet.</returns>
        public List<Officer> GetAllOfficers()
        {
            return Officers.ToList();
        }

        /// <summary>
        /// Returns all starfighters on the planet.
        /// </summary>
        /// <returns>A list of starfighters currently on this planet.</returns>
        public List<Starfighter> GetAllStarfighters()
        {
            return Starfighters.ToList();
        }

        /// <summary>
        /// Returns all regiments on the planet.
        /// </summary>
        /// <returns>A list of regiments currently on this planet.</returns>
        public List<Regiment> GetAllRegiments()
        {
            return Regiments.ToList();
        }

        /// <summary>
        /// Returns the number of officers on the planet.
        /// </summary>
        /// <returns>The count of officers currently on this planet.</returns>
        public int GetOfficerCount()
        {
            return Officers.Count;
        }

        /// <summary>
        /// Returns the number of starfighters on the planet.
        /// </summary>
        /// <returns>The count of starfighters currently on this planet.</returns>
        public int GetStarfighterCount()
        {
            return Starfighters.Count;
        }

        /// <summary>
        /// Returns the number of regiments on the planet.
        /// </summary>
        /// <returns>The count of regiments currently on this planet.</returns>
        public int GetRegimentCount()
        {
            return Regiments.Count;
        }

        /// <summary>
        /// Returns true if this planet can accept the child. Fleets and Missions are always
        /// accepted. Officers require owner match or captured status. Regiments and Starfighters
        /// require owner match. Buildings require colonization, owner match, and available energy.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <returns>True if AddChild would succeed; otherwise false.</returns>
        public override bool CanAcceptChild(ISceneNode child)
        {
            switch (child)
            {
                case Fleet _:
                case Mission _:
                    return true;
                case Officer officer:
                    return officer.IsCaptured || officer.GetOwnerInstanceID() == OwnerInstanceID;
                case Regiment regiment:
                    return regiment.GetOwnerInstanceID() == GetOwnerInstanceID();
                case Starfighter starfighter:
                    return starfighter.GetOwnerInstanceID() == GetOwnerInstanceID();
                case Building building:
                    return IsColonized
                        && building.GetOwnerInstanceID() == GetOwnerInstanceID()
                        && GetAvailableEnergy() > 0;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Adds a reference node to the game.
        /// </summary>
        /// <param name="child">The game node to add as a reference.</param>
        public override void AddChild(ISceneNode child)
        {
            switch (child)
            {
                case Fleet fleet:
                    AddFleet(fleet);
                    break;
                case Officer officer:
                    AddOfficer(officer);
                    break;
                case Building building:
                    AddBuilding(building);
                    break;
                case Mission mission:
                    AddMission(mission);
                    break;
                case Regiment regiment:
                    AddRegiment(regiment);
                    break;
                case Starfighter starfighter:
                    AddStarfighter(starfighter);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Cannot add {child.GetDisplayName()} to {this.GetDisplayName()}. "
                            + $"Only fleets, officers, buildings, missions, and regiments are allowed."
                    );
            }
        }

        /// <summary>
        /// Removes a child node from the planet.
        /// </summary>
        /// <param name="child">The child node to remove.</param>
        public override void RemoveChild(ISceneNode child)
        {
            switch (child)
            {
                case Fleet fleet:
                    RemoveFleet(fleet);
                    break;
                case Officer officer:
                    RemoveOfficer(officer);
                    break;
                case Building building:
                    RemoveBuilding(building);
                    break;
                case Mission mission:
                    RemoveMission(mission);
                    break;
                case Regiment regiment:
                    RemoveRegiment(regiment);
                    break;
                case Starfighter starfighter:
                    RemoveStarfighter(starfighter);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Cannot remove {child.GetDisplayName()} from {this.GetDisplayName()}. "
                            + $"Only fleets, officers, buildings, missions, regiments, and starfighters are allowed."
                    );
            }
        }

        /// <summary>
        /// Gets the child nodes of the planet.
        /// </summary>
        /// <returns>An enumerable of child nodes.</returns>
        public override IEnumerable<ISceneNode> GetChildren()
        {
            return Fleets
                .Cast<ISceneNode>()
                .Concat(Officers)
                .Concat(Missions)
                .Concat(Regiments)
                .Concat(Starfighters)
                .Concat(Buildings);
        }
    }
}
