using System;
using System.IO;
using UnityEngine;

namespace Rebellion.Multiplayer.Tactical
{
    internal sealed partial class MultiplayerTacticalProtocol : IDisposable
    {
        /// <summary>
        /// Enforces bounded reliable retention before the next main-thread pump.
        /// </summary>
        /// <param name="item">The authenticated reliable event.</param>
        private void Enqueue(Received item)
        {
            if (
                _events.Count >= MaximumQueuedEvents
                || item.Payload.Length > _maximumQueuedBytes - _queuedBytes
            )
                throw new InvalidDataException("The battle event queue exceeded its limit.");
            _queuedBytes += item.Payload.Length;
            _events.Enqueue(item);
        }

        /// <summary>
        /// Applies message-specific limits before decoding or retaining peer-controlled bytes.
        /// </summary>
        /// <param name="packet">The addressed authenticated packet.</param>
        private static void ValidatePayload(MultiplayerPacket packet)
        {
            int limit = packet.Type switch
            {
                MultiplayerMessageType.TacticalSnapshot
                or MultiplayerMessageType.TacticalCheckpoint =>
                    MultiplayerTacticalSnapshotCodec.MaximumSnapshotPayloadBytes,
                MultiplayerMessageType.TacticalEffects =>
                    MultiplayerTacticalSnapshotCodec.MaxEffectsPayloadBytes,
                MultiplayerMessageType.TacticalCommand => MaximumCommandBytes,
                _ => 0,
            };
            if (packet.Payload.Length > limit)
                throw new InvalidDataException("The battle event exceeds its payload limit.");
        }

        /// <summary>
        /// Adds both negotiated identities to event metadata.
        /// </summary>
        /// <param name="envelope">The event metadata.</param>
        /// <returns>The addressed event metadata.</returns>
        private Envelope CreateEnvelope(Envelope envelope)
        {
            envelope.initializationID = _initializationID;
            envelope.battleID = _battleID;
            return envelope;
        }

        /// <summary>
        /// Sends an addressed packet through its message-specific transport policy.
        /// </summary>
        /// <param name="type">The closed wire discriminator.</param>
        /// <param name="envelope">The event metadata.</param>
        /// <param name="payload">The closed typed payload.</param>
        private void Send(MultiplayerMessageType type, Envelope envelope, byte[] payload = null)
        {
            if (_disposed || Failure != null)
                return;
            try
            {
                _send(
                    new MultiplayerPacket(
                        type,
                        JsonUtility.ToJson(CreateEnvelope(envelope)),
                        payload
                    )
                );
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }
    }
}
