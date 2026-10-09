using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Selects automatic messages from a settled result batch at its existing message-release boundary.
    /// </summary>
    public sealed class MessageObserver
    {
        private readonly GameRoot _game;
        private readonly MessageFactory _messageFactory;
        private readonly MessageCommands _commands;

        /// <summary>
        /// Connects automatic message selection to durable delivery.
        /// </summary>
        /// <param name="game">The game supplying message context.</param>
        /// <param name="messageFactory">The shared factory that selects and resolves messages.</param>
        /// <param name="commands">The operations that persist messages on recipients.</param>
        public MessageObserver(
            GameRoot game,
            MessageFactory messageFactory,
            MessageCommands commands
        )
        {
            _game = game;
            _messageFactory = messageFactory;
            _commands = commands;
        }

        /// <summary>
        /// Creates and delivers faction messages for the supplied game results.
        /// </summary>
        /// <param name="results">The game results to process.</param>
        /// <returns>The result of process results.</returns>
        public List<GameResult> ProcessResults(IEnumerable<GameResult> results)
        {
            GameResult[] resultBatch =
                results?.Where(result => result != null).ToArray()
                ?? System.Array.Empty<GameResult>();
            IEnumerable<GameResult> automaticResults = resultBatch.Where(result =>
                string.IsNullOrWhiteSpace(result.SourceEventInstanceID)
            );
            return _commands.Deliver(_messageFactory.CreateMessages(automaticResults, _game));
        }
    }
}
