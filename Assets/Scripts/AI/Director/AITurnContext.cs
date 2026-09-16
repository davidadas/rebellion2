using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.AI.Director
{
    /// <summary>
    /// Shared state for one faction AI turn.
    /// </summary>
    public sealed class AITurnContext
    {
        // Turn Dependencies.
        public GameRoot Game { get; }
        public Faction Faction { get; }
        public IRandomNumberProvider Random { get; }
        public MissionSystem Missions { get; }
        public MovementSystem Movement { get; }
        public ManufacturingSystem Manufacturing { get; }
        public MaintenanceSystem Maintenance { get; }
        public BombardmentSystem Bombardment { get; }
        public PlanetaryAssaultSystem PlanetaryAssault { get; }
        public GalaxyMap FactionView { get; }
        public AIAssessment Assessment { get; }
        public AIStrategicPlan StrategicPlan { get; }
        public AIPlanetDevelopmentAllocation DevelopmentAllocation =>
            _developmentAllocation ??= new AIPlanetDevelopmentAllocation(this);
        public AIReinforcementArrivalForecast ReinforcementArrivalForecast =>
            _reinforcementArrivalForecast ??= new AIReinforcementArrivalForecast(this);

        // Turn Output.
        public IReadOnlyList<AIProposal> Proposals => _proposals;
        public IReadOnlyList<AIProposal> SelectedProposals => _selectedProposals;
        public IReadOnlyList<GameResult> Results => _results;

        private readonly List<AIProposal> _proposals = new List<AIProposal>();
        private readonly List<AIProposal> _selectedProposals = new List<AIProposal>();
        private readonly List<GameResult> _results = new List<GameResult>();
        private readonly Dictionary<SpecialForces, SpecialForcesIntent> _specialForcesIntents =
            new Dictionary<SpecialForces, SpecialForcesIntent>();
        private readonly HashSet<string> _unlockedSpecialForcesMissionTypes;
        private AIPlanetDevelopmentAllocation _developmentAllocation;
        private AIReinforcementArrivalForecast _reinforcementArrivalForecast;

        /// <summary>
        /// Creates a turn context.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="faction">The faction being processed.</param>
        /// <param name="missions">Mission system used by mission proposals.</param>
        /// <param name="movement">Movement system used by movement proposals.</param>
        /// <param name="manufacturing">Manufacturing system used by production proposals.</param>
        /// <param name="bombardment">Bombardment system used by fleet attack proposals.</param>
        /// <param name="planetaryAssault">Planetary-assault system used by fleet attack proposals.</param>
        /// <param name="random">RNG provider used by probabilistic decisions.</param>
        /// <param name="factionView">The faction-visible galaxy state for this turn.</param>
        /// <param name="maintenance">Maintenance system used to project production capacity.</param>
        public AITurnContext(
            GameRoot game,
            Faction faction,
            MissionSystem missions,
            MovementSystem movement,
            ManufacturingSystem manufacturing,
            BombardmentSystem bombardment,
            PlanetaryAssaultSystem planetaryAssault,
            IRandomNumberProvider random,
            GalaxyMap factionView = null,
            MaintenanceSystem maintenance = null
        )
        {
            Game = game;
            Faction = faction;
            Missions = missions;
            Movement = movement;
            Manufacturing = manufacturing;
            Maintenance = maintenance;
            Bombardment = bombardment;
            PlanetaryAssault = planetaryAssault;
            Random = random;
            FactionView = factionView;
            _unlockedSpecialForcesMissionTypes =
                faction
                    ?.GetUnlockedTechnologies(ManufacturingType.Troop)
                    .Select(technology => technology.GetReference())
                    .OfType<SpecialForces>()
                    .SelectMany(unit => unit.AllowedMissionTypeIDs)
                    .ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);
            Assessment = new AIAssessment(this);
            StrategicPlan = new AIStrategicPlan(this);
        }

        /// <summary>
        /// Adds one proposal to the turn.
        /// </summary>
        /// <param name="proposal">The proposal to add.</param>
        public void AddProposal(AIProposal proposal)
        {
            if (proposal != null)
                _proposals.Add(proposal);
        }

        /// <summary>
        /// Adds a batch of proposals to the turn.
        /// </summary>
        /// <param name="proposals">The proposals to add.</param>
        public void AddProposals(IEnumerable<AIProposal> proposals)
        {
            if (proposals == null)
                return;

            foreach (AIProposal proposal in proposals)
                AddProposal(proposal);
        }

        /// <summary>
        /// Replaces the selected proposal set.
        /// </summary>
        /// <param name="proposals">The selected proposals.</param>
        public void SetSelectedProposals(IEnumerable<AIProposal> proposals)
        {
            _selectedProposals.Clear();

            if (proposals == null)
                return;

            foreach (AIProposal proposal in proposals)
            {
                if (proposal != null)
                    _selectedProposals.Add(proposal);
            }
        }

        /// <summary>
        /// Records how a special-forces unit should be used during this AI turn.
        /// </summary>
        /// <param name="unit">The special-forces unit being assigned.</param>
        /// <param name="intent">The unit's turn-scoped staffing intent.</param>
        public void SetSpecialForcesIntent(SpecialForces unit, SpecialForcesIntent intent)
        {
            if (unit != null)
                _specialForcesIntents[unit] = intent;
        }

        /// <summary>
        /// Returns how a special-forces unit should be used during this AI turn.
        /// </summary>
        /// <param name="unit">The special-forces unit to inspect.</param>
        /// <returns>The assigned intent, or primary agent when no intent has been assigned.</returns>
        public SpecialForcesIntent GetSpecialForcesIntent(SpecialForces unit)
        {
            return unit != null && _specialForcesIntents.TryGetValue(unit, out var intent)
                ? intent
                : SpecialForcesIntent.PrimaryAgent;
        }

        /// <summary>
        /// Returns whether this faction can manufacture special forces for a mission type.
        /// </summary>
        /// <param name="missionTypeId">The mission capability to inspect.</param>
        /// <returns>True when an unlocked special-forces template supports the mission.</returns>
        public bool HasUnlockedSpecialForcesForMission(string missionTypeId)
        {
            return !string.IsNullOrEmpty(missionTypeId)
                && _unlockedSpecialForcesMissionTypes.Contains(missionTypeId);
        }

        /// <summary>
        /// Adds one result to the turn.
        /// </summary>
        /// <param name="result">The result to add.</param>
        public void AddResult(GameResult result)
        {
            if (result != null)
                _results.Add(result);
        }

        /// <summary>
        /// Adds a batch of results to the turn.
        /// </summary>
        /// <param name="results">The results to add.</param>
        public void AddResults(IEnumerable<GameResult> results)
        {
            if (results == null)
                return;

            foreach (GameResult result in results)
                AddResult(result);
        }
    }

    /// <summary>
    /// Assigns the only planets in each sector that may host production facilities.
    /// The allocation is built once per AI turn and reused by construction and cleanup.
    /// </summary>
    public sealed class AIPlanetDevelopmentAllocation
    {
        private readonly Dictionary<BuildingType, Dictionary<string, int>> _capsByType = new();
        private readonly Dictionary<BuildingType, HashSet<string>> _primaryPlanetIdsByType = new();
        private readonly Dictionary<BuildingType, Dictionary<string, int>> _primaryTargetsByType =
            new();
        private readonly Dictionary<BuildingType, HashSet<string>> _incompletePrimarySystemsByType =
            new();
        private readonly Dictionary<string, Dictionary<BuildingType, int>> _reservedEnergyByPlanet =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Creates the turn-scoped planet development allocation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public AIPlanetDevelopmentAllocation(AITurnContext context)
        {
            if (context?.Assessment == null)
                return;

            BuildAllocations(context);
        }

        /// <summary>
        /// Returns energy that a building may consume without displacing planned development.
        /// </summary>
        /// <param name="planet">The prospective destination.</param>
        /// <param name="buildingType">The proposed building type.</param>
        /// <returns>The uncommitted energy available to the proposal.</returns>
        public int GetAvailableEnergy(Planet planet, BuildingType buildingType)
        {
            if (planet == null)
                return 0;

            int reservedEnergy = 0;
            if (
                _reservedEnergyByPlanet.TryGetValue(
                    planet.InstanceID,
                    out Dictionary<BuildingType, int> reservations
                )
            )
            {
                foreach (KeyValuePair<BuildingType, int> reservation in reservations)
                {
                    if (reservation.Key != buildingType)
                        reservedEnergy += reservation.Value;
                }
            }

            return Math.Max(0, planet.GetAvailableEnergy() - reservedEnergy);
        }

        /// <summary>
        /// Gets cap.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="buildingType">The building type.</param>
        /// <returns>The requested cap.</returns>
        public int GetCap(Planet planet, BuildingType buildingType)
        {
            return
                planet != null
                && _capsByType.TryGetValue(buildingType, out Dictionary<string, int> caps)
                && caps.TryGetValue(planet.InstanceID, out int cap)
                ? cap
                : 0;
        }

        /// <summary>
        /// Returns whether a planet is the designated primary site and has not reached its target.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="buildingType">The building type.</param>
        /// <param name="targetCount">The target count.</param>
        /// <returns>True when the incomplete primary hub condition is met; otherwise false.</returns>
        public bool IsIncompletePrimaryHub(
            Planet planet,
            BuildingType buildingType,
            int targetCount
        )
        {
            return planet != null
                && IsPrimaryHub(planet, buildingType)
                && planet.GetTotalBuildingTypeCount(buildingType)
                    < GetPrimaryTarget(planet, buildingType, targetCount);
        }

        /// <summary>
        /// Checks whether the primary hub condition is met.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="buildingType">The building type.</param>
        /// <returns>True when the primary hub condition is met; otherwise false.</returns>
        public bool IsPrimaryHub(Planet planet, BuildingType buildingType) =>
            planet != null
            && _primaryPlanetIdsByType.TryGetValue(buildingType, out HashSet<string> planetIds)
            && planetIds.Contains(planet.InstanceID);

        /// <summary>
        /// Gets primary target.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="buildingType">The building type.</param>
        /// <param name="fallbackTarget">The fallback target.</param>
        /// <returns>The requested primary target.</returns>
        public int GetPrimaryTarget(Planet planet, BuildingType buildingType, int fallbackTarget)
        {
            return
                planet != null
                && _primaryTargetsByType.TryGetValue(
                    buildingType,
                    out Dictionary<string, int> targets
                )
                && targets.TryGetValue(planet.InstanceID, out int target)
                ? target
                : fallbackTarget;
        }

        /// <summary>
        /// Returns whether a system's designated primary facility hub is incomplete.
        /// </summary>
        /// <param name="systemId">The system identifier.</param>
        /// <param name="buildingType">The facility type.</param>
        /// <returns>True when the system has an incomplete primary hub.</returns>
        public bool HasIncompletePrimaryHub(string systemId, BuildingType buildingType)
        {
            return !string.IsNullOrEmpty(systemId)
                && _incompletePrimarySystemsByType.TryGetValue(
                    buildingType,
                    out HashSet<string> systemIds
                )
                && systemIds.Contains(systemId);
        }

        /// <summary>
        /// Builds the development allocation for every owned system.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        private void BuildAllocations(AITurnContext context)
        {
            foreach (
                IGrouping<string, Planet> sector in context
                    .Assessment.OwnedPlanets.Where(planet => planet?.IsDestroyed == false)
                    .GroupBy(context.Assessment.GetPlanetSystemId)
            )
            {
                List<Planet> planets = sector.Where(IsUsable).ToList();
                List<Planet> constructionPlanets = sector
                    .Where(planet =>
                        IsUsable(planet)
                        || planet.GetParentOfType<PlanetSector>()?.SectorType
                            == PlanetSectorType.OuterRim
                    )
                    .ToList();
                HashSet<string> assignedPrimaryPlanetIds = new(StringComparer.Ordinal);
                AllocateType(
                    context,
                    constructionPlanets,
                    BuildingType.ConstructionFacility,
                    assignedPrimaryPlanetIds
                );
                AllocateType(context, planets, BuildingType.Shipyard, assignedPrimaryPlanetIds);
                AllocateType(
                    context,
                    planets,
                    BuildingType.TrainingFacility,
                    assignedPrimaryPlanetIds
                );
            }
        }

        /// <summary>
        /// Allocates primary and secondary planets for one facility type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="sector">The planets in the system being allocated.</param>
        /// <param name="buildingType">The facility type being allocated.</param>
        /// <param name="assignedPrimaryPlanetIds">Primary sites already assigned to another facility type.</param>
        private void AllocateType(
            AITurnContext context,
            IReadOnlyCollection<Planet> sector,
            BuildingType buildingType,
            HashSet<string> assignedPrimaryPlanetIds
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            Dictionary<string, int> caps = GetOrAdd(_capsByType, buildingType);
            HashSet<string> primaryPlanetIds = GetOrAdd(_primaryPlanetIdsByType, buildingType);
            Dictionary<string, int> primaryTargets = GetOrAdd(_primaryTargetsByType, buildingType);
            HashSet<string> incompletePrimarySystems = GetOrAdd(
                _incompletePrimarySystemsByType,
                buildingType
            );
            List<InfrastructureCandidate> candidates = sector
                .Select(planet => new InfrastructureCandidate(
                    planet,
                    GetFeasibleFacilityCount(planet, buildingType)
                ))
                .ToList();
            int primaryTarget = buildingType switch
            {
                BuildingType.Shipyard => config.ShipyardSectorHubTargetCount,
                BuildingType.ConstructionFacility => config.FacilitySectorHubTargetCount,
                _ => 0,
            };
            IEnumerable<InfrastructureCandidate> preferred =
                primaryTarget > 0
                    ? candidates.Where(item => item.FeasibleCount >= primaryTarget)
                    : candidates;
            IEnumerable<InfrastructureCandidate> fallback =
                primaryTarget > 0
                    ? candidates.Where(item => item.FeasibleCount < primaryTarget)
                    : Enumerable.Empty<InfrastructureCandidate>();
            List<Planet> ranked = RankCandidates(
                    context,
                    buildingType,
                    assignedPrimaryPlanetIds,
                    preferred
                )
                .Concat(RankCandidates(context, buildingType, assignedPrimaryPlanetIds, fallback))
                .Take(config.FacilityPlanetsPerSector)
                .Select(item => item.Planet)
                .ToList();
            if (ranked.Count == 0)
                return;

            Planet primary = ranked[0];
            int configuredTarget =
                buildingType == BuildingType.Shipyard
                    ? config.ShipyardSectorHubTargetCount
                    : config.FacilitySectorHubTargetCount;
            int currentCount = primary.GetTotalBuildingTypeCount(buildingType);
            int feasibleTarget = Math.Max(
                currentCount,
                Math.Min(configuredTarget, GetFeasibleFacilityCount(primary, buildingType))
            );
            caps[primary.InstanceID] = feasibleTarget;
            primaryPlanetIds.Add(primary.InstanceID);
            primaryTargets[primary.InstanceID] = feasibleTarget;
            if (currentCount < feasibleTarget)
            {
                string systemId = context.Assessment.GetPlanetSystemId(primary);
                if (!string.IsNullOrEmpty(systemId))
                    incompletePrimarySystems.Add(systemId);
            }
            assignedPrimaryPlanetIds.Add(primary.InstanceID);
            if (buildingType != BuildingType.TrainingFacility)
                ReserveEnergy(primary, buildingType, Math.Max(0, feasibleTarget - currentCount));

            for (int index = 1; index < ranked.Count; index++)
            {
                Planet secondary = ranked[index];
                int secondaryCurrent = secondary.GetTotalBuildingTypeCount(buildingType);
                int secondaryTarget = Math.Max(
                    secondaryCurrent,
                    Math.Min(
                        config.FacilitySectorSecondaryTargetCount,
                        GetFeasibleFacilityCount(secondary, buildingType)
                    )
                );
                caps[secondary.InstanceID] = secondaryTarget;
            }
        }

        private int GetFeasibleFacilityCount(Planet planet, BuildingType buildingType)
        {
            return planet.GetTotalBuildingTypeCount(buildingType)
                + GetAvailableEnergy(planet, buildingType);
        }

        private static IOrderedEnumerable<InfrastructureCandidate> RankCandidates(
            AITurnContext context,
            BuildingType buildingType,
            ISet<string> assignedPrimaryPlanetIds,
            IEnumerable<InfrastructureCandidate> candidates
        )
        {
            return candidates
                .OrderByDescending(candidate =>
                    assignedPrimaryPlanetIds?.Contains(candidate.Planet.InstanceID) != true
                )
                .ThenByDescending(candidate =>
                    candidate.Planet.GetTotalBuildingTypeCount(buildingType)
                )
                .ThenByDescending(candidate => candidate.FeasibleCount)
                .ThenByDescending(candidate =>
                    AIInfrastructureAllocationScorer.Score(
                        context,
                        candidate.Planet,
                        buildingType,
                        candidate.FeasibleCount,
                        assignedPrimaryPlanetIds
                    )
                )
                .ThenBy(candidate => candidate.Planet.InstanceID, StringComparer.Ordinal);
        }

        private void ReserveEnergy(Planet planet, BuildingType buildingType, int energy)
        {
            if (energy <= 0)
                return;

            if (
                !_reservedEnergyByPlanet.TryGetValue(
                    planet.InstanceID,
                    out Dictionary<BuildingType, int> reservations
                )
            )
            {
                reservations = new Dictionary<BuildingType, int>();
                _reservedEnergyByPlanet.Add(planet.InstanceID, reservations);
            }

            reservations[buildingType] = energy;
        }

        private static TValue GetOrAdd<TKey, TValue>(Dictionary<TKey, TValue> values, TKey key)
            where TValue : new()
        {
            if (!values.TryGetValue(key, out TValue value))
            {
                value = new TValue();
                values.Add(key, value);
            }

            return value;
        }

        /// <summary>
        /// Checks whether the usable condition is met.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <returns>True when the usable condition is met; otherwise false.</returns>
        private static bool IsUsable(Planet planet) =>
            planet?.IsColonized == true && !planet.IsDestroyed;

        private sealed class InfrastructureCandidate
        {
            public int FeasibleCount { get; }
            public Planet Planet { get; }

            public InfrastructureCandidate(Planet planet, int feasibleCount)
            {
                Planet = planet;
                FeasibleCount = feasibleCount;
            }
        }
    }
}
