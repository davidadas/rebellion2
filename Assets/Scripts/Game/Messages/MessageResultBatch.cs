using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Normalizes one simulation-result batch before automatic message translation.
    /// </summary>
    internal sealed class MessageResultBatch
    {
        private readonly GameResult[] _automaticResults;

        private MessageResultBatch(
            GameResult[] automaticResults,
            MessageRequestedResult[] authoredRequests
        )
        {
            _automaticResults = automaticResults;
            AuthoredRequests = authoredRequests;
        }

        public IReadOnlyList<MessageRequestedResult> AuthoredRequests { get; }

        public IEnumerable<T> OfType<T>()
            where T : GameResult => _automaticResults.OfType<T>();

        public static MessageResultBatch Create(IEnumerable<GameResult> results)
        {
            GameResult[] completeResults =
                results?.Where(result => result != null).ToArray() ?? Array.Empty<GameResult>();
            foreach (
                SuppressNextAutomaticMessageResult suppression in completeResults.OfType<SuppressNextAutomaticMessageResult>()
            )
            {
                if (suppression.TargetResult != null)
                    suppression.TargetResult.SuppressDefaultMessage = true;
            }

            return new MessageResultBatch(
                completeResults
                    .Where(result =>
                        result.SuppressDefaultMessage != true
                        && result is not MessageRequestedResult
                        && result is not SuppressNextAutomaticMessageResult
                    )
                    .ToArray(),
                completeResults.OfType<MessageRequestedResult>().ToArray()
            );
        }
    }
}
