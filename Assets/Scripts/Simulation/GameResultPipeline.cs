using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Preserves ordered gameplay reactions, presentation callbacks, and message delivery.
    /// </summary>
    public sealed class GameResultPipeline
    {
        private readonly Func<GameResultBus> _getResults;
        private readonly Func<MessageObserver> _getMessageObserver;

        public event Action<IReadOnlyList<GameResult>> ResultsResolved;
        public event Action<IReadOnlyList<PlanetaryAssaultResult>> PlanetaryAssaultsResolved;
        public event Action<IReadOnlyList<VictoryResult>> VictoriesResolved;
        public event Action<MessageDeliveredResult> MessageDelivered;
        public event Action<HeadquartersLostResult> HeadquartersLost;
        public event Action<VictoryResult> VictoryDeclared;
        public event Action<BombardmentResult> BombardmentCompleted;

        /// <summary>
        /// Creates ordered delivery against the current components during in-place replacement.
        /// </summary>
        /// <param name="getResults">Returns the current gameplay result bus.</param>
        /// <param name="getMessageObserver">Returns the current automatic-message observer.</param>
        internal GameResultPipeline(
            Func<GameResultBus> getResults,
            Func<MessageObserver> getMessageObserver
        )
        {
            _getResults = getResults ?? throw new ArgumentNullException(nameof(getResults));
            _getMessageObserver =
                getMessageObserver ?? throw new ArgumentNullException(nameof(getMessageObserver));
        }

        /// <summary>
        /// Resolves domain reactions and then presents the completed result batch to observers.
        /// Per-result logging belongs to the operation that produced the result.
        /// </summary>
        /// <param name="results">Batch of completed changes.</param>
        /// <param name="processMessages">Whether to create faction messages for this batch.</param>
        /// <returns>The initial results followed by every result produced by their reactions.</returns>
        public List<GameResult> ProcessResults(
            IEnumerable<GameResult> results,
            bool processMessages = true
        )
        {
            List<GameResult> resolvedResults = _getResults().Publish(results);
            if (resolvedResults.Count > 0)
                ResultsResolved?.Invoke(resolvedResults);
            List<PlanetaryAssaultResult> assaultResults = resolvedResults
                .OfType<PlanetaryAssaultResult>()
                .ToList();
            if (assaultResults.Count > 0)
                PlanetaryAssaultsResolved?.Invoke(assaultResults);

            List<VictoryResult> victoryResults = resolvedResults.OfType<VictoryResult>().ToList();
            if (victoryResults.Count > 0)
                VictoriesResolved?.Invoke(victoryResults);

            if (processMessages)
                ProcessMessageReactions(resolvedResults);

            foreach (
                HeadquartersLostResult headquarters in resolvedResults.OfType<HeadquartersLostResult>()
            )
                HeadquartersLost?.Invoke(headquarters);

            foreach (VictoryResult victory in resolvedResults.OfType<VictoryResult>())
                VictoryDeclared?.Invoke(victory);
            return resolvedResults;
        }

        /// <summary>
        /// Delivers messages and drains any result reactions that request additional messages.
        /// </summary>
        /// <param name="resolvedResults">The already-processed results to extend in place.</param>
        public void ProcessMessageReactions(List<GameResult> resolvedResults)
        {
            List<GameResult> pendingMessageResults = new List<GameResult>(resolvedResults);
            while (pendingMessageResults.Count > 0)
            {
                List<GameResult> deliveredResults = _getMessageObserver()
                    .ProcessResults(pendingMessageResults);
                if (deliveredResults.Count == 0)
                    return;
                List<GameResult> deliveryReactions = _getResults().Publish(deliveredResults);
                resolvedResults.AddRange(deliveryReactions);
                foreach (
                    MessageDeliveredResult delivery in deliveredResults.OfType<MessageDeliveredResult>()
                )
                    MessageDelivered?.Invoke(delivery);
                pendingMessageResults = deliveryReactions;
            }
        }

        /// <summary>
        /// Routes results emitted by an immediate command.
        /// </summary>
        /// <param name="results">The results emitted by the command.</param>
        public void ProcessImmediate(IReadOnlyList<GameResult> results)
        {
            List<GameResult> resolvedResults = ProcessResults(results);
            foreach (BombardmentResult result in resolvedResults.OfType<BombardmentResult>())
                BombardmentCompleted?.Invoke(result);
        }
    }
}
