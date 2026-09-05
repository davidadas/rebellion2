using System.Collections.Generic;
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
        public BombardmentSystem Bombardment { get; }
        public PlanetaryAssaultSystem PlanetaryAssault { get; }
        public GalaxyMap FactionView { get; }
        public AIAssessment Assessment { get; }
        internal AISabotageTargetPolicy SabotageTargets { get; }

        // Turn Output.
        public IReadOnlyList<AIProposal> Proposals => _proposals;
        public IReadOnlyList<AIProposal> SelectedProposals => _selectedProposals;
        public IReadOnlyList<GameResult> Results => _results;

        private readonly List<AIProposal> _proposals = new List<AIProposal>();
        private readonly List<AIProposal> _selectedProposals = new List<AIProposal>();
        private readonly List<GameResult> _results = new List<GameResult>();
        private readonly Dictionary<SpecialForces, SpecialForcesIntent> _specialForcesIntents =
            new Dictionary<SpecialForces, SpecialForcesIntent>();

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
            GalaxyMap factionView = null
        )
        {
            Game = game;
            Faction = faction;
            Missions = missions;
            Movement = movement;
            Manufacturing = manufacturing;
            Bombardment = bombardment;
            PlanetaryAssault = planetaryAssault;
            Random = random;
            FactionView = factionView;
            Assessment = new AIAssessment(this);
            SabotageTargets = new AISabotageTargetPolicy(
                Assessment,
                game?.Config?.AI?.MissionPlanning
            );
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
}
