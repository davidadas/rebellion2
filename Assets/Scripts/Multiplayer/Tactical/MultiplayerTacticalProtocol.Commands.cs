using System;
using System.IO;
using System.Text;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Tactical.Synchronization;
using UnityEngine;

namespace Rebellion.Multiplayer.Tactical
{
    internal sealed partial class MultiplayerTacticalProtocol : IDisposable
    {
        /// <summary>
        /// Applies an authenticated command once and replays only its latest duplicate outcome.
        /// </summary>
        /// <param name="item">The immutable captured command request.</param>
        private void ApplyCommand(Received item)
        {
            if (
                _lastCommands.TryGetValue(item.Envelope.actor, out Received previous)
                && item.Envelope.requestID <= previous.Envelope.requestID
            )
            {
                if (item.Envelope.requestID == previous.Envelope.requestID)
                {
                    if (!item.Payload.AsSpan().SequenceEqual(previous.Payload))
                        throw new InvalidDataException(
                            "A duplicate battle request changed its parameters."
                        );
                    SendResult(previous.Envelope);
                }
                return;
            }
            if (item.Envelope.actor == MultiplayerRole.Guest)
            {
                if (item.Envelope.requestID != checked(_lastRemoteRequestID + 1))
                    throw new InvalidDataException("The battle command sequence is incomplete.");
                _lastRemoteRequestID = item.Envelope.requestID;
            }
            TacticalCombatCommand command =
                JsonUtility.FromJson<TacticalCombatCommand>(Encoding.UTF8.GetString(item.Payload))
                ?? throw new InvalidDataException("The battle command is missing.");
            CombatSide actor =
                item.Envelope.actor == MultiplayerRole.Host ? _hostSide
                : _hostSide == CombatSide.Attacker ? CombatSide.Defender
                : CombatSide.Attacker;
            item.Envelope.accepted = _localReady && _remoteReady && _applyCommand(actor, command);
            _lastCommands[item.Envelope.actor] = item;
            SendResult(item.Envelope);
        }

        /// <summary>
        /// Completes a guest request only for an identity it actually submitted.
        /// </summary>
        /// <param name="item">The authenticated host's command outcome.</param>
        private void ApplyResult(Received item)
        {
            if (
                item.Envelope.actor != MultiplayerRole.Guest
                || item.Envelope.requestID < 2000000001
                || item.Envelope.requestID >= _nextRequestID
            )
                throw new InvalidDataException(
                    "The battle outcome does not address a submitted command."
                );
            if (_requests.Remove(item.Envelope.requestID, out var completion))
            {
                _requestTimes.Remove(item.Envelope.requestID);
                completion.TrySetResult(
                    new PlayerRequestOutcome(
                        item.Envelope.accepted,
                        item.Envelope.accepted ? null : "The battle command is unavailable."
                    )
                );
            }
        }

        /// <summary>
        /// Returns an executed command outcome to its authenticated submitting role.
        /// </summary>
        /// <param name="envelope">The admitted command outcome.</param>
        private void SendResult(Envelope envelope)
        {
            if (envelope.actor == MultiplayerRole.Host)
            {
                if (_requests.Remove(envelope.requestID, out var completion))
                {
                    _requestTimes.Remove(envelope.requestID);
                    completion.TrySetResult(
                        new PlayerRequestOutcome(
                            envelope.accepted,
                            envelope.accepted ? null : "The battle command is unavailable."
                        )
                    );
                }
            }
            else
                Send(MultiplayerMessageType.TacticalCommandResult, envelope);
        }
    }
}
