using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using VictoryResult = Rebellion.Game.Results.VictoryResult;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Evaluates and records victory conditions.
    /// </summary>
    public class VictoryCommands
    {
        private readonly GameRoot _game;
        private bool _victoryDeclared;

        /// <summary>Raised when an immediate victory declaration produces a result.</summary>
        public event Action<IReadOnlyList<VictoryResult>> ResultsProduced;

        internal bool IsDeclared => _victoryDeclared;

        /// <summary>
        /// Creates victory operations for one game.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public VictoryCommands(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Declares the winning and losing factions and publishes the completed outcome.
        /// </summary>
        /// <param name="winner">The faction that won the game.</param>
        /// <param name="loser">The faction that lost the game.</param>
        /// <returns>The declared outcome, or null when the request is invalid or already settled.</returns>
        public VictoryResult TryDeclareVictory(Faction winner, Faction loser)
        {
            Faction liveWinner = ResolveFaction(winner);
            Faction liveLoser = ResolveFaction(loser);
            if (liveWinner == null || liveLoser == null || liveWinner == liveLoser)
                return null;

            VictoryResult result = CreateVictory(liveWinner, liveLoser);
            if (result != null)
                ResultsProduced?.Invoke(new[] { result });
            return result;
        }

        /// <summary>
        /// Records one terminal outcome without publishing it to presentation listeners.
        /// </summary>
        /// <param name="winner">The faction that won the game.</param>
        /// <param name="loser">The faction that lost the game.</param>
        /// <returns>The recorded outcome, or null when the game is already settled.</returns>
        private VictoryResult CreateVictory(Faction winner, Faction loser)
        {
            if (_victoryDeclared)
                return null;

            _victoryDeclared = true;
            return new VictoryResult
            {
                Winner = winner,
                Loser = loser,
                GameMode = _game.Summary.VictoryCondition,
                Tick = _game.CurrentTick,
            };
        }

        /// <summary>
        /// Resolves a faction argument to the faction registered with the active game.
        /// </summary>
        /// <param name="faction">The faction or snapshot to resolve.</param>
        /// <returns>The registered faction, or null when it cannot be resolved.</returns>
        private Faction ResolveFaction(Faction faction)
        {
            return string.IsNullOrEmpty(faction?.InstanceID)
                ? null
                : _game
                    .GetFactions()
                    .FirstOrDefault(candidate => candidate.InstanceID == faction.InstanceID);
        }
    }
}
