using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Util.Random;

namespace Rebellion.AI
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
        public MissionCommands Missions { get; }
        public MissionQueries MissionQueries { get; }
        public MovementCommands Movement { get; }
        public MovementQueries MovementQueries { get; }
        public HeadquartersCommands Headquarters { get; }
        public HeadquartersQueries HeadquartersQueries { get; }
        public ManufacturingCommands Manufacturing { get; }
        public MaintenanceCommands Maintenance { get; }
        public BombardmentCommands Bombardment { get; }
        public BombardmentQueries BombardmentQueries { get; }
        public PlanetaryAssaultCommands PlanetaryAssault { get; }
        public PlanetaryAssaultQueries PlanetaryAssaultQueries { get; }
        public GalaxyMap FactionView { get; }
        public AIAssessment Assessment { get; }
        public AIStrategicPlan StrategicPlan { get; }
        public IReadOnlyList<SpecialForces> UnlockedSpecialForcesTemplates { get; }
        public int AvailableProjectedMaintenanceHeadroom
        {
            get
            {
                EnsureManufacturingBudget();
                return _availableManufacturingHeadroom;
            }
        }

        // Turn Output.
        public bool HasGeneratedProductionDemands { get; private set; }
        public IReadOnlyCollection<AIAttackDemand> AttackDemands => _attackDemands.Values;
        public IReadOnlyList<AIProductionDemand> ProductionDemands => _productionDemands;
        public IReadOnlyList<AIProposal> Proposals => _proposals;
        public IReadOnlyList<AIProposal> SelectedProposals => _selectedProposals;
        public IReadOnlyList<GameResult> Results => _results;

        private readonly List<AIProductionDemand> _productionDemands =
            new List<AIProductionDemand>();
        private readonly Dictionary<string, AIAttackDemand> _attackDemands = new(
            StringComparer.Ordinal
        );
        private readonly List<AIProposal> _proposals = new List<AIProposal>();
        private readonly List<AIProposal> _selectedProposals = new List<AIProposal>();
        private readonly List<GameResult> _results = new List<GameResult>();
        private readonly Dictionary<SpecialForces, SpecialForcesIntent> _specialForcesIntents =
            new Dictionary<SpecialForces, SpecialForcesIntent>();
        private readonly Dictionary<
            string,
            Mission.MissionEvaluationState
        > _missionEvaluationStates = new Dictionary<string, Mission.MissionEvaluationState>(
            StringComparer.Ordinal
        );
        private readonly HashSet<SpecialForces> _headquartersEspionageAgents;
        private readonly HashSet<string> _unlockedSpecialForcesMissionTypes;
        private bool _hasManufacturingBudget;
        private int _availableManufacturingHeadroom;
        private long _manufacturingBudgetRevision = -1;

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
        /// <param name="assessment">The completed faction assessment for this turn.</param>
        /// <param name="strategicPlan">The strategic allocations derived from the assessment.</param>
        /// <param name="factionView">The faction-visible galaxy state for this turn.</param>
        /// <param name="maintenance">Maintenance system used to project production capacity.</param>
        /// <param name="headquarters">Headquarters system used by relocation proposals.</param>
        public AITurnContext(
            GameRoot game,
            Faction faction,
            MissionCommands missions,
            MovementCommands movement,
            ManufacturingCommands manufacturing,
            BombardmentCommands bombardment,
            PlanetaryAssaultCommands planetaryAssault,
            IRandomNumberProvider random,
            AIAssessment assessment,
            AIStrategicPlan strategicPlan,
            GalaxyMap factionView = null,
            MaintenanceCommands maintenance = null,
            HeadquartersCommands headquarters = null
        )
        {
            Game = game;
            Faction = faction;
            Missions = missions;
            MissionQueries = game == null ? null : new MissionQueries(game);
            Movement = movement;
            MovementQueries = game == null ? null : new MovementQueries(game);
            Headquarters = headquarters;
            HeadquartersQueries = game == null ? null : new HeadquartersQueries(game);
            Manufacturing = manufacturing;
            Maintenance = maintenance;
            Bombardment = bombardment;
            BombardmentQueries = game == null ? null : new BombardmentQueries(game);
            PlanetaryAssault = planetaryAssault;
            PlanetaryAssaultQueries = game == null ? null : new PlanetaryAssaultQueries(game);
            Random = random;
            FactionView = factionView;
            Assessment = assessment;
            StrategicPlan = strategicPlan;
            UnlockedSpecialForcesTemplates =
                faction
                    ?.GetUnlockedTechnologies(ManufacturingType.Troop)
                    .Select(technology => technology.GetReference())
                    .OfType<SpecialForces>()
                    .ToList()
                ?? new List<SpecialForces>();
            _unlockedSpecialForcesMissionTypes = UnlockedSpecialForcesTemplates
                .SelectMany(unit => unit.AllowedMissionTypeIDs)
                .ToHashSet(StringComparer.Ordinal);
            _headquartersEspionageAgents =
                faction?.Settings?.Headquarters?.IsMobile == true
                && assessment?.FactionHeadquarters != null
                    ? assessment
                        .AvailableMissionParticipants.OfType<SpecialForces>()
                        .Where(unit =>
                            unit.CanPerformMission(MissionTypeIDs.Espionage)
                            && unit.GetParentOfType<Planet>() == assessment.FactionHeadquarters
                        )
                        .ToHashSet()
                    : new HashSet<SpecialForces>();
        }

        /// <summary>
        /// Replaces the score-free attack demands generated for this turn.
        /// </summary>
        /// <param name="demands">The generated attack demands.</param>
        public void SetAttackDemands(IEnumerable<AIAttackDemand> demands)
        {
            _attackDemands.Clear();
            if (demands == null)
                return;

            foreach (AIAttackDemand demand in demands)
            {
                string targetId = demand?.TargetPlanet?.InstanceID;
                if (!string.IsNullOrEmpty(targetId))
                    _attackDemands[targetId] = demand;
            }
        }

        /// <summary>
        /// Returns the observed mission odds for a proposal during this AI turn.
        /// </summary>
        /// <param name="proposal">The mission proposal to evaluate.</param>
        /// <param name="includePersonnelLoss">Whether to calculate personnel-loss risk.</param>
        /// <returns>The mission odds, or null when the proposal cannot create a mission.</returns>
        internal MissionOdds GetMissionOdds(
            AIMissionProposal proposal,
            bool includePersonnelLoss = true
        )
        {
            if (proposal?.TargetPlanet == null || MissionQueries == null || Assessment == null)
                return null;

            string planetId = proposal.TargetPlanet.InstanceID;
            if (
                !_missionEvaluationStates.TryGetValue(
                    planetId,
                    out Mission.MissionEvaluationState evaluationState
                )
            )
            {
                evaluationState = new Mission.MissionEvaluationState(Game, proposal.TargetPlanet);
                _missionEvaluationStates.Add(planetId, evaluationState);
            }

            return MissionQueries.GetMissionOdds(
                proposal.CreateContext(),
                Assessment.GetMissionDetectors,
                includePersonnelLoss,
                evaluationState
            );
        }

        /// <summary>
        /// Returns the generated attack demand for a known planet.
        /// </summary>
        /// <param name="planet">The known target planet.</param>
        /// <returns>The attack demand, or null when the target has no generated demand.</returns>
        public AIAttackDemand GetAttackDemand(Planet planet)
        {
            return
                planet != null
                && _attackDemands.TryGetValue(planet.InstanceID, out AIAttackDemand demand)
                ? demand
                : null;
        }

        /// <summary>
        /// Replaces the production demands generated for this turn.
        /// </summary>
        /// <param name="demands">The generated production demands in strategic order.</param>
        public void SetProductionDemands(IEnumerable<AIProductionDemand> demands)
        {
            _productionDemands.Clear();
            HasGeneratedProductionDemands = true;

            if (demands == null)
                return;

            foreach (AIProductionDemand demand in demands)
            {
                if (demand != null)
                    _productionDemands.Add(demand);
            }
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
        /// Records the maintenance change produced by one manufacturing command.
        /// </summary>
        /// <param name="addedMaintenance">Maintenance committed by queued items.</param>
        /// <param name="releasedMaintenance">Maintenance released by removed items.</param>
        internal void RecordManufacturingMutation(int addedMaintenance, int releasedMaintenance)
        {
            if (!_hasManufacturingBudget)
            {
                EnsureManufacturingBudget();
                return;
            }

            long available =
                (long)_availableManufacturingHeadroom
                + Math.Max(0, releasedMaintenance)
                - Math.Max(0, addedMaintenance);
            _availableManufacturingHeadroom = (int)
                Math.Max(int.MinValue, Math.Min(int.MaxValue, available));
            _manufacturingBudgetRevision = Game?.EconomyRevision ?? -1;
        }

        /// <summary>
        /// Initializes the turn-scoped manufacturing budget from live economy state.
        /// </summary>
        private void EnsureManufacturingBudget()
        {
            long economyRevision = Game?.EconomyRevision ?? -1;
            if (_hasManufacturingBudget && _manufacturingBudgetRevision == economyRevision)
                return;

            _availableManufacturingHeadroom = CalculateAvailableManufacturingHeadroom();
            _hasManufacturingBudget = true;
            _manufacturingBudgetRevision = economyRevision;
        }

        /// <summary>
        /// Calculates current maintenance capacity minus projected maintenance cost.
        /// </summary>
        /// <returns>The faction's current manufacturing headroom.</returns>
        private int CalculateAvailableManufacturingHeadroom()
        {
            if (Game == null || Faction == null)
                return Assessment?.ProjectedMaintenanceHeadroom ?? 0;

            long available =
                (long)ResourceProductionQueries.CalculateMaintenanceCapacity(Game, Faction)
                - Faction.GetTotalProjectedMaintenanceCost();
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, available));
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
        /// Returns whether an available special-forces unit is an espionage agent stationed at
        /// this mobile faction's headquarters for the current turn.
        /// </summary>
        /// <param name="unit">The special-forces unit to inspect.</param>
        /// <returns>True when the unit is an available headquarters espionage agent.</returns>
        public bool IsHeadquartersEspionageAgent(SpecialForces unit)
        {
            return unit != null && _headquartersEspionageAgents.Contains(unit);
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
}
