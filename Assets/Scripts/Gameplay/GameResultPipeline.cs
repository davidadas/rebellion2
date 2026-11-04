using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Util.Logging;

namespace Rebellion.Gameplay
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
            using var measurement = PerformanceLogger.Measure("Result pipeline");
            List<GameResult> resolvedResults = _getResults().Publish(results);
            if (resolvedResults.Count > 0)
                InvokeMeasured<IReadOnlyList<GameResult>>(
                    ResultsResolved,
                    resolvedResults,
                    "Resolved result callback",
                    resolvedResults.Count
                );
            List<PlanetaryAssaultResult> assaultResults = resolvedResults
                .OfType<PlanetaryAssaultResult>()
                .ToList();
            if (assaultResults.Count > 0)
                InvokeMeasured<IReadOnlyList<PlanetaryAssaultResult>>(
                    PlanetaryAssaultsResolved,
                    assaultResults,
                    "Assault result callback",
                    assaultResults.Count
                );

            List<VictoryResult> victoryResults = resolvedResults.OfType<VictoryResult>().ToList();
            if (victoryResults.Count > 0)
                InvokeMeasured<IReadOnlyList<VictoryResult>>(
                    VictoriesResolved,
                    victoryResults,
                    "Victory batch callback",
                    victoryResults.Count
                );

            if (processMessages)
                ProcessMessageReactions(resolvedResults);

            foreach (
                HeadquartersLostResult headquarters in resolvedResults.OfType<HeadquartersLostResult>()
            )
                InvokeMeasured(HeadquartersLost, headquarters, "Headquarters loss callback", 1);

            foreach (VictoryResult victory in resolvedResults.OfType<VictoryResult>())
                InvokeMeasured(VictoryDeclared, victory, "Victory callback", 1);
            return resolvedResults;
        }

        /// <summary>
        /// Delivers messages and drains any result reactions that request additional messages.
        /// </summary>
        /// <param name="resolvedResults">The already-processed results to extend in place.</param>
        public void ProcessMessageReactions(List<GameResult> resolvedResults)
        {
            using var measurement = PerformanceLogger.Measure("Message reaction waves");
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
                    InvokeMeasured(MessageDelivered, delivery, "Message delivery callback", 1);
                pendingMessageResults = deliveryReactions;
            }
        }

        /// <summary>
        /// Invokes the current callback snapshot in registration order with optional per-callback timing.
        /// </summary>
        /// <typeparam name="T">The callback argument type.</typeparam>
        /// <param name="callbacks">The current multicast callback snapshot.</param>
        /// <param name="value">The existing callback argument.</param>
        /// <param name="operation">The operation recorded for each callback.</param>
        /// <param name="items">The already known number of delivered results.</param>
        private static void InvokeMeasured<T>(
            Action<T> callbacks,
            T value,
            string operation,
            long items
        )
        {
            if (!PerformanceLogger.IsEnabled)
            {
                callbacks?.Invoke(value);
                return;
            }

            if (callbacks == null)
                return;
            foreach (Action<T> callback in callbacks.GetInvocationList())
            {
                using var measurement = PerformanceLogger.Measure(
                    operation,
                    items: items,
                    subjectType: callback.Method.DeclaringType
                );
                callback(value);
            }
        }

        /// <summary>
        /// Routes results emitted by an immediate command.
        /// </summary>
        /// <param name="results">The results emitted by the command.</param>
        public void ProcessImmediate(IReadOnlyList<GameResult> results)
        {
            using var measurement = PerformanceLogger.Measure("Immediate result pipeline");
            List<GameResult> resolvedResults = ProcessResults(results);
            foreach (BombardmentResult result in resolvedResults.OfType<BombardmentResult>())
                InvokeMeasured(BombardmentCompleted, result, "Bombardment callback", 1);
        }
    }
}
