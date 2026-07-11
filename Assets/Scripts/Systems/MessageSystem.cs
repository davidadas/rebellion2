using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;

namespace Rebellion.Systems
{
    /// <summary>
    /// Converts game results into faction messages and delivers them to each faction.
    /// </summary>
    public class MessageSystem
    {
        private readonly GameRoot _game;
        private readonly MessageFactory _messageFactory;

        /// <summary>
        /// Initializes a message system for the supplied game state and message definitions.
        /// </summary>
        /// <param name="game">The game state used to resolve message context.</param>
        /// <param name="definitions">The message definitions used to build messages.</param>
        public MessageSystem(GameRoot game, IEnumerable<MessageDefinition> definitions)
        {
            _game = game;
            _messageFactory = new MessageFactory(definitions);
        }

        /// <summary>
        /// Creates and delivers faction messages for the supplied game results.
        /// </summary>
        /// <param name="results">The game results to process.</param>
        public void ProcessResults(IEnumerable<GameResult> results)
        {
            foreach (
                (Faction faction, Message message) delivery in _messageFactory.CreateMessages(
                    results,
                    _game
                )
            )
                delivery.faction?.AddMessage(delivery.message);
        }
    }
}
