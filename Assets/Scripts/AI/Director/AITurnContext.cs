using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Proposals;
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
        public AIFacilityAllocationPolicy FacilityAllocation =>
            _facilityAllocation ??= new AIFacilityAllocationPolicy(this);

        // Turn Output.
        public IReadOnlyList<AIProposal> Proposals => _proposals;
        public IReadOnlyList<AIProposal> SelectedProposals => _selectedProposals;
        public IReadOnlyList<GameResult> Results => _results;

        private readonly List<AIProposal> _proposals = new List<AIProposal>();
        private readonly List<AIProposal> _selectedProposals = new List<AIProposal>();
        private readonly List<GameResult> _results = new List<GameResult>();
        private readonly Dictionary<SpecialForces, SpecialForcesIntent> _specialForcesIntents =
            new Dictionary<SpecialForces, SpecialForcesIntent>();
        private AIFacilityAllocationPolicy _facilityAllocation;

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
            Assessment = new AIAssessment(this);
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
    public sealed class AIFacilityAllocationPolicy
    {
        private readonly Dictionary<BuildingType, Dictionary<string, int>> _capsByType = new();
        private readonly Dictionary<BuildingType, HashSet<string>> _primaryPlanetIdsByType = new();
        private readonly Dictionary<BuildingType, Dictionary<string, int>> _primaryTargetsByType =
            new();

        public AIFacilityAllocationPolicy(AITurnContext context)
        {
            if (context?.Assessment == null)
                return;

            BuildCaps(context, BuildingType.Shipyard);
            BuildCaps(context, BuildingType.ConstructionFacility);
            BuildCaps(context, BuildingType.TrainingFacility);
        }

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
        public bool IsIncompletePrimaryHub(Planet planet, BuildingType buildingType, int targetCount)
        {
            return planet != null
                && IsPrimaryHub(planet, buildingType)
                && planet.GetTotalBuildingTypeCount(buildingType)
                    < GetPrimaryTarget(planet, buildingType, targetCount);
        }

        public bool IsPrimaryHub(Planet planet, BuildingType buildingType) =>
            planet != null
            && _primaryPlanetIdsByType.TryGetValue(buildingType, out HashSet<string> planetIds)
            && planetIds.Contains(planet.InstanceID);

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

        private void BuildCaps(AITurnContext context, BuildingType buildingType)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            Dictionary<string, int> caps = new(StringComparer.Ordinal);
            HashSet<string> primaryPlanetIds = new(StringComparer.Ordinal);
            Dictionary<string, int> primaryTargets = new(StringComparer.Ordinal);
            foreach (
                IGrouping<string, Planet> sector in context
                    .Assessment.OwnedPlanets.Where(IsUsable)
                    .GroupBy(context.Assessment.GetPlanetSystemId)
            )
            {
                List<Planet> ranked = sector
                    .Select(planet => new
                    {
                        Planet = planet,
                        FeasibleCount = GetFeasibleFacilityCount(context, planet, buildingType),
                    })
                    .OrderByDescending(item =>
                        buildingType != BuildingType.Shipyard
                        || item.FeasibleCount >= config.ShipyardSectorHubTargetCount
                    )
                    .ThenByDescending(item => item.FeasibleCount)
                    .ThenByDescending(item =>
                        item.Planet.GetTotalBuildingTypeCount(buildingType)
                    )
                    .ThenByDescending(item => context.Assessment.GetPlanetValue(item.Planet))
                    .ThenBy(item => item.Planet.InstanceID, StringComparer.Ordinal)
                    .Take(3)
                    .Select(item => item.Planet)
                    .ToList();
                if (ranked.Count > 0)
                {
                    Planet primary = ranked[0];
                    int configuredTarget =
                        buildingType == BuildingType.Shipyard
                            ? config.ShipyardSectorHubTargetCount
                            : config.FacilitySectorHubTargetCount;
                    int feasibleTarget = Math.Max(
                        primary.GetTotalBuildingTypeCount(buildingType),
                        Math.Min(
                            configuredTarget,
                            GetFeasibleFacilityCount(context, primary, buildingType)
                        )
                    );
                    caps[primary.InstanceID] = Math.Max(
                        feasibleTarget,
                        primary.GetTotalBuildingTypeCount(buildingType)
                    );
                    primaryPlanetIds.Add(primary.InstanceID);
                    primaryTargets[primary.InstanceID] = feasibleTarget;
                }
                for (int index = 1; index < ranked.Count; index++)
                    caps[ranked[index].InstanceID] = config.FacilitySectorSecondaryTargetCount;
            }

            _capsByType[buildingType] = caps;
            _primaryPlanetIdsByType[buildingType] = primaryPlanetIds;
            _primaryTargetsByType[buildingType] = primaryTargets;
        }

        private static int GetFeasibleFacilityCount(
            AITurnContext context,
            Planet planet,
            BuildingType buildingType
        )
        {
            int defensiveReserve = context.Assessment.GetPlanetaryDefenseEnergyDeficit(planet);
            int availableEnergy = Math.Max(0, planet.GetAvailableEnergy() - defensiveReserve);
            return planet.GetTotalBuildingTypeCount(buildingType) + availableEnergy;
        }

        private static bool IsUsable(Planet planet) =>
            planet != null && planet.IsColonized && !planet.IsDestroyed;
    }
}
