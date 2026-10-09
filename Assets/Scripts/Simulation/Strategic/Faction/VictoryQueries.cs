using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>Evaluates whether configured victory requirements are satisfied.</summary>
    public sealed class VictoryQueries
    {
        private readonly GameRoot _game;

        /// <summary>Creates victory queries for the active game.</summary>
        /// <param name="game">The active game graph and victory configuration.</param>
        public VictoryQueries(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>Determines whether losing headquarters satisfies the configured game mode.</summary>
        /// <param name="defender">The faction that lost its headquarters.</param>
        /// <returns>True when the headquarters loss may declare victory.</returns>
        public bool CanDeclareVictoryAfterHeadquartersLoss(Faction defender)
        {
            return CanDeclareVictoryAfterHeadquartersLoss(
                defender,
                _game.GetSceneNodesByType<Officer>()
            );
        }

        /// <summary>Evaluates headquarters loss against an existing officer assessment.</summary>
        /// <param name="defender">The faction that lost its headquarters.</param>
        /// <param name="officers">The officers in the active game.</param>
        /// <returns>True when the headquarters loss may declare victory.</returns>
        internal bool CanDeclareVictoryAfterHeadquartersLoss(
            Faction defender,
            IEnumerable<Officer> officers
        )
        {
            if (defender == null)
                return false;
            if (_game.Summary.VictoryCondition != GameVictoryCondition.Conquest)
                return true;

            foreach (Officer officer in officers ?? Array.Empty<Officer>())
            {
                if (
                    officer.GetOwnerInstanceID() == defender.InstanceID
                    && officer.IsMain
                    && !officer.IsCaptured
                )
                    return false;
            }

            return true;
        }
    }
}
