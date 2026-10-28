using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.AI.Selectors;
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
    /// Describes how the AI intends to use an available special-forces unit this turn.
    /// </summary>
    public enum SpecialForcesIntent
    {
        PrimaryAgent,
        Decoy,
        Reserve,
    }

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
        public OfficerCommandCommands OfficerCommands { get; }
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
        public IReadOnlyList<Fleet> FleetProductionPriorities { get; private set; } =
            Array.Empty<Fleet>();
        public IReadOnlyList<AIProposal> Proposals => _proposals;
        public IReadOnlyList<AIProposal> SelectedProposals => _selectedProposals;

        /// <summary>
        /// Gets or sets shared reservations for selection stages within this faction turn.
        /// </summary>
        internal AISelectionState SelectionState { get; set; }

        /// <summary>
        /// Gets or sets the production selector retaining this turn's accepted budgets and claims.
        /// </summary>
        internal AIProductionSelector ProductionSelector { get; set; }

        /// <summary>
        /// Gets or sets the proposal boundary already considered by an earlier selection stage.
        /// </summary>
        internal int SelectionProposalCount { get; set; }
        public IReadOnlyList<GameResult> Results => _results;

        private IReadOnlyList<AIProductionDemand> _productionDemands =
            Array.Empty<AIProductionDemand>();
        private readonly Dictionary<string, AIAttackDemand> _attackDemands = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<
            (string Fleet, string Planet),
            AIAttackDemand
        > _fleetAttackDemands = new();
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
        /// <param name="officerCommands">Officer command appointment operations.</param>
        /// <param name="missionQueries">Optional mission queries prepared for the current assessment.</param>
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
            HeadquartersCommands headquarters = null,
            OfficerCommandCommands officerCommands = null,
            MissionQueries missionQueries = null
        )
        {
            Game = game;
            Faction = faction;
            Missions = missions;
            MissionQueries =
                missionQueries
                ?? (
                    game == null ? null
                    : assessment == null ? new MissionQueries(game)
                    : new MissionQueries(
                        game,
                        assessment.GetMissionDetectorCommander,
                        new MissionEncounterOdds.Cache(
                            game.Config.AI.MissionPlanning.EncounterCacheCapacity
                        )
                    )
                );
            Movement = movement;
            MovementQueries = game == null ? null : new MovementQueries(game);
            Headquarters = headquarters;
            OfficerCommands = officerCommands;
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
            _fleetAttackDemands.Clear();
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
        /// <param name="fleet">Optional attacking fleet whose arrival was assessed.</param>
        /// <returns>The attack demand, or null when the target has no generated demand.</returns>
        public AIAttackDemand GetAttackDemand(Planet planet, Fleet fleet = null)
        {
            if (
                planet != null
                && fleet != null
                && _fleetAttackDemands.TryGetValue(
                    (fleet.InstanceID, planet.InstanceID),
                    out AIAttackDemand arrivalDemand
                )
            )
                return arrivalDemand;
            return
                planet != null
                && _attackDemands.TryGetValue(planet.InstanceID, out AIAttackDemand demand)
                ? demand
                : null;
        }

        /// <summary>
        /// Resolves ground-force requirements against the turn's measured attack demand.
        /// </summary>
        /// <param name="planet">The target whose current defenses are assessed.</param>
        /// <param name="fleet">The attacking fleet.</param>
        /// <param name="projected">Whether to include units committed to the fleet.</param>
        /// <param name="additionalBombardmentStrength">Additional proposed bombardment strength.</param>
        /// <returns>The required regiment count and attack strength after available bombardment.</returns>
        internal (int Count, int Strength) GetAttackRegimentRequirements(
            Planet planet,
            Fleet fleet,
            bool projected = false,
            int additionalBombardmentStrength = 0
        )
        {
            AIAttackDemand demand = GetAttackDemand(planet, fleet);
            if (demand == null)
                return (0, 0);
            return Assessment.CanBombardDefenders(
                fleet,
                planet,
                projected,
                additionalBombardmentStrength,
                demand.ShieldStrength
            )
                ? (demand.OccupationRegimentCount, 0)
                : (demand.RegimentCount, demand.RegimentStrength);
        }

        /// <summary>
        /// Returns whether ready bombardment can damage military targets at the assessed arrival.
        /// </summary>
        /// <param name="fleet">The attacking fleet.</param>
        /// <param name="planet">The known attack target.</param>
        /// <returns>True when bombardment exceeds the expected shields and has military targets.</returns>
        internal bool CanBombardAttackTarget(Fleet fleet, Planet planet)
        {
            AIAttackDemand demand = GetAttackDemand(planet, fleet);
            int shieldStrength = Math.Max(
                demand?.ShieldStrength ?? 0,
                Assessment.GetBombardmentShieldStrength(planet)
            );
            return fleet != null
                && planet != null
                && Assessment.GetFleetBombardmentStrength(fleet) > shieldStrength
                && (Assessment.HasBombardmentTargets(planet) || shieldStrength > 0);
        }

        /// <summary>
        /// Separates fleet launch readiness from the regiments needed to complete the attack.
        /// </summary>
        /// <param name="fleet">The attacking fleet.</param>
        /// <param name="planet">The faction-visible attack target.</param>
        /// <returns>Whether the fleet and its regiments satisfy their launch requirements.</returns>
        internal (bool FleetReady, bool RegimentsReady) GetAttackReadiness(
            Fleet fleet,
            Planet planet
        )
        {
            AIAttackDemand demand = GetAttackDemand(planet, fleet);
            if (
                fleet == null
                || demand == null
                || !Assessment.HasCurrentHeadquartersAttackIntel(planet)
                || !StrategicPlan.CanFleetDepart(fleet)
                || !fleet.HasOperationalCapitalShips()
            )
                return default;

            int combat = Assessment.GetReadyFleetCombatValueAgainstCapitalShips(fleet);
            var required = GetAttackRegimentRequirements(planet, fleet);
            if (
                combat <= 0
                || combat < demand.CombatStrength
                || Assessment.GetReadyFleetRegimentCapacity(fleet) < required.Count
                || Assessment.GetFleetBombardmentStrength(fleet) < demand.BombardmentStrength
            )
                return default;

            bool canBombard = CanBombardAttackTarget(fleet, planet);
            if (!canBombard && demand.IsAssaultBlockedByShields)
                return default;

            bool regimentsReady =
                Assessment.GetReadyFleetRegimentCount(fleet) >= required.Count
                && Assessment.GetReadyFleetRegimentAttackStrength(fleet) >= required.Strength
                && (
                    canBombard
                    || Assessment.GetPlanetaryAssaultSuccessPercent(fleet, planet)
                        >= Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent
                );
            return (true, regimentsReady);
        }

        /// <summary>
        /// Counts the current attack capabilities satisfied by a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>The number of satisfied attack capabilities.</returns>
        internal int CountCurrentAttackCapabilities(Fleet fleet, Planet targetPlanet)
        {
            AIAttackDemand demand = GetAttackDemand(targetPlanet, fleet);
            if (demand == null)
                return 0;

            var required = GetAttackRegimentRequirements(targetPlanet, fleet);
            int count = 0;
            if (fleet?.HasOperationalCapitalShips() == true)
                count++;
            int combat = Assessment.GetReadyFleetCombatValueAgainstCapitalShips(fleet);
            if (combat > 0 && combat >= demand.CombatStrength)
                count++;
            if (Assessment.GetReadyFleetRegimentCount(fleet) >= required.Count)
                count++;
            if (Assessment.GetReadyFleetRegimentCapacity(fleet) >= required.Count)
                count++;
            if (Assessment.GetReadyFleetRegimentAttackStrength(fleet) >= required.Strength)
                count++;
            if (Assessment.GetFleetBombardmentStrength(fleet) >= demand.BombardmentStrength)
                count++;
            return count;
        }

        /// <summary>
        /// Returns whether a fleet can defeat known orbital defenders.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>True when ready combat strength satisfies the orbital demand.</returns>
        internal bool CanWinOrbitalCombat(Fleet fleet, Planet targetPlanet)
        {
            int required = GetAttackDemand(targetPlanet, fleet)?.OrbitalStrength ?? 0;
            return required > 0
                && fleet?.HasOperationalCapitalShips() == true
                && Assessment.GetReadyFleetCombatValueAgainstCapitalShips(fleet) >= required;
        }

        /// <summary>
        /// Stores an arrival-specific demand during attack assessment.
        /// </summary>
        /// <param name="fleet">The fleet whose travel time was assessed.</param>
        /// <param name="demand">The measured requirements at that fleet's arrival.</param>
        internal void SetFleetAttackDemand(Fleet fleet, AIAttackDemand demand)
        {
            _fleetAttackDemands[(fleet.InstanceID, demand.TargetPlanet.InstanceID)] = demand;
        }

        /// <summary>
        /// Replaces the production demands generated for this turn.
        /// </summary>
        /// <param name="demands">The generated production demands in strategic order.</param>
        public void SetProductionDemands(IEnumerable<AIProductionDemand> demands)
        {
            _productionDemands =
                demands?.Where(demand => demand != null).ToArray()
                ?? Array.Empty<AIProductionDemand>();
            HasGeneratedProductionDemands = true;
        }

        /// <summary>
        /// Finds the current requirement for an exact queued manufacturing order.
        /// </summary>
        /// <param name="order">The queued object evaluated by production selection.</param>
        /// <returns>The first matching requirement, or null when the order has no current demand.</returns>
        internal AIProductionDemand GetCurrentProductionDemand(IManufacturable order)
        {
            if (order == null)
                return null;
            foreach (AIProductionDemand demand in _productionDemands)
                if (ReferenceEquals(demand.ExistingOrder, order))
                    return demand;
            return null;
        }

        /// <summary>
        /// Sets attack fleets in the order they receive suitable production capacity this turn.
        /// </summary>
        /// <param name="fleets">The ranked fleets, or null when no attack fleet needs production.</param>
        public void SetFleetProductionPriorities(IEnumerable<Fleet> fleets)
        {
            FleetProductionPriorities =
                fleets?.Where(fleet => fleet != null).Distinct().ToArray() ?? Array.Empty<Fleet>();
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
        }

        /// <summary>
        /// Invalidates manufacturing headroom after another proposal may have changed economy state.
        /// </summary>
        internal void InvalidateManufacturingBudget()
        {
            _hasManufacturingBudget = false;
        }

        /// <summary>
        /// Initializes the turn-scoped manufacturing budget from live economy state.
        /// </summary>
        private void EnsureManufacturingBudget()
        {
            if (_hasManufacturingBudget)
                return;

            _availableManufacturingHeadroom = CalculateAvailableManufacturingHeadroom();
            _hasManufacturingBudget = true;
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
