using System;
using Rebellion.Game;
using Rebellion.Game.Commands;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.UIState;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Owns one active simulation and exposes its clock, commands, queries, and resolved results.
    /// </summary>
    public sealed class GameSession
    {
        // Game State.
        private readonly GameRoot _game;
        private readonly GameClock _clock;
        private readonly SimulationFeatures _features;

        // Game Events.
        public event Action GameSpeedChanged;
        public event Action TickCompleted;

        /// <summary>
        /// Raised when tick processing pauses for a player-controlled space-combat decision.
        /// </summary>
        public event Action CombatDecisionRequired;

        /// <summary>
        /// Gets whether the simulation is between ticks with no unresolved decision.
        /// </summary>
        internal bool IsTickSettled => Tick.IsSettled;

        /// <summary>
        /// Gets the session clock.
        /// </summary>
        public GameClock Clock => _clock;

        /// <summary>
        /// Gets the command executor used by simulation consumers that collect raw results.
        /// </summary>
        public IGameCommandExecutor Commands => _features.Commands;

        /// <summary>
        /// Gets the read-only simulation queries.
        /// </summary>
        public GameQueries Queries { get; }

        /// <summary>
        /// Gets the session result pipeline and its presentation notifications.
        /// </summary>
        public GameResults Results { get; }

        /// <summary>
        /// Gets the ordered tick executor.
        /// </summary>
        public GameTick Tick { get; }

        /// <summary>
        /// Gets the internal feature graph for simulation-level integration tests.
        /// </summary>
        internal SimulationFeatures Features => _features;

        /// <summary>
        /// Executes one gameplay command and resolves the facts it produces.
        /// </summary>
        /// <param name="command">The gameplay command.</param>
        /// <returns>The command acceptance and fully resolved factual results.</returns>
        public GameCommandResult Execute(GameCommand command)
        {
            GameCommandResult outcome = _features.Commands.ExecuteRaw(command);
            if (outcome.Results.Count == 0)
                return outcome;
            return new GameCommandResult(outcome.Accepted, Results.Resolve(outcome.Results));
        }

        /// <summary>
        /// Creates a completed simulation session from its composed runtime services.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        /// <param name="features">The private simulation feature graph.</param>
        /// <param name="clock">The session clock.</param>
        /// <param name="queries">The session query facade.</param>
        /// <param name="results">The session result pipeline.</param>
        /// <param name="tick">The ordered tick executor.</param>
        internal GameSession(
            GameRoot game,
            SimulationFeatures features,
            GameClock clock,
            GameQueries queries,
            GameResults results,
            GameTick tick
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _features = features ?? throw new ArgumentNullException(nameof(features));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Queries = queries ?? throw new ArgumentNullException(nameof(queries));
            Results = results ?? throw new ArgumentNullException(nameof(results));
            Tick = tick ?? throw new ArgumentNullException(nameof(tick));

            _clock.SpeedChanged += HandleClockSpeedChanged;
            Tick.Completed += () => TickCompleted?.Invoke();
            Tick.CombatDecisionRequired += () => CombatDecisionRequired?.Invoke();
        }

        /// <summary>
        /// Reconstructs unresolved runtime decisions represented by loaded game state.
        /// </summary>
        internal void ReconcileLoadedState()
        {
            Tick.ReconcileLoadedState();
        }

        /// <summary>
        /// Returns the current game instance.
        /// </summary>
        /// <returns>The active GameRoot.</returns>
        public GameRoot GetGame() => _game;

        /// <summary>
        /// Returns the current tick count.
        /// </summary>
        /// <returns>The current tick number.</returns>
        public int GetCurrentTick() => _game.CurrentTick;

        /// <summary>
        /// Returns the player-controlled faction.
        /// </summary>
        /// <returns>The faction controlled by the human game participant.</returns>
        public Faction GetPlayerFaction() => _game.GetPlayerFaction();

        /// <summary>
        /// Returns the durable interface state for the local human participant.
        /// </summary>
        /// <returns>The local participant's interface state.</returns>
        public PlayerUIState GetPlayerUIState()
        {
            Faction faction = GetPlayerFaction();
            return _game.GetFactionPlayer(faction.InstanceID).UIState;
        }

        /// <summary>
        /// Immediately applies the current advisor automation choices for one faction.
        /// </summary>
        /// <param name="faction">The faction whose delegated work should run.</param>
        public void ProcessFactionAutomation(Faction faction)
        {
            Tick.ProcessAutomation(faction);
        }

        /// <summary>
        /// Returns the active game speed.
        /// </summary>
        /// <returns>The active game speed.</returns>
        public TickSpeed GetGameSpeed() => _game.GetGameSpeed();

        /// <summary>
        /// Sets the game speed and adjusts the tick interval accordingly.
        /// </summary>
        /// <param name="speed">The desired tick speed.</param>
        public void SetGameSpeed(TickSpeed speed)
        {
            _clock.SetSpeed(speed);
        }

        /// <summary>
        /// Resolves the pending combat encounter and resumes ticking.
        /// </summary>
        /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
        /// <returns>The space combat result generated by the encounter, when present.</returns>
        public SpaceCombatResult ResolveCombat(bool autoResolve)
        {
            SpaceCombatResult result = Tick.ResolveCombat(autoResolve);
            _clock.Reset();
            return result;
        }

        /// <summary>
        /// Resolves a pending retreat and routes its results before resuming ticks.
        /// </summary>
        /// <param name="retreatingFactionInstanceId">The faction withdrawing from combat.</param>
        /// <returns>The resulting space-combat summary, or null when retreat is unavailable.</returns>
        public SpaceCombatResult ResolveCombatRetreat(string retreatingFactionInstanceId)
        {
            SpaceCombatResult result = Tick.ResolveRetreat(retreatingFactionInstanceId);
            if (result != null)
                _clock.Reset();
            return result;
        }

        /// <summary>
        /// Forwards clock speed changes to session observers.
        /// </summary>
        private void HandleClockSpeedChanged()
        {
            GameSpeedChanged?.Invoke();
        }
    }
}
