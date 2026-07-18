using System.Collections.Generic;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.AI.Director
{
    /// <summary>
    /// Shared state for one faction AI turn.
    /// </summary>
    public sealed class AITurnContext
    {
        /// <summary>
        /// Game instance being processed.
        /// </summary>
        public GameRoot Game { get; }

        /// <summary>
        /// Faction being processed.
        /// </summary>
        public Faction Faction { get; }

        /// <summary>
        /// RNG provider used by this turn.
        /// </summary>
        public IRandomNumberProvider Random { get; }

        /// <summary>
        /// Mission system used by mission proposals.
        /// </summary>
        public MissionSystem Missions { get; }

        /// <summary>
        /// Movement system used by movement proposals.
        /// </summary>
        public MovementSystem Movement { get; }

        /// <summary>
        /// Manufacturing system used by production proposals.
        /// </summary>
        public ManufacturingSystem Manufacturing { get; }

        /// <summary>
        /// Bombardment system used by fleet attack proposals.
        /// </summary>
        public BombardmentSystem Bombardment { get; }

        /// <summary>
        /// Planetary-assault system used by fleet attack proposals.
        /// </summary>
        public PlanetaryAssaultSystem PlanetaryAssault { get; }

        /// <summary>
        /// Derived AI assessment for this turn.
        /// </summary>
        public AIAssessment Assessment { get; }

        /// <summary>
        /// Proposals generated during planning.
        /// </summary>
        public IReadOnlyList<AIProposal> Proposals => _proposals;

        /// <summary>
        /// Proposals selected for execution.
        /// </summary>
        public IReadOnlyList<AIProposal> SelectedProposals => _selectedProposals;

        /// <summary>
        /// Results produced during proposal execution.
        /// </summary>
        public IReadOnlyList<GameResult> Results => _results;

        private readonly List<AIProposal> _proposals = new List<AIProposal>();
        private readonly List<AIProposal> _selectedProposals = new List<AIProposal>();
        private readonly List<GameResult> _results = new List<GameResult>();

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
        public AITurnContext(
            GameRoot game,
            Faction faction,
            MissionSystem missions,
            MovementSystem movement,
            ManufacturingSystem manufacturing,
            BombardmentSystem bombardment,
            PlanetaryAssaultSystem planetaryAssault,
            IRandomNumberProvider random
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
