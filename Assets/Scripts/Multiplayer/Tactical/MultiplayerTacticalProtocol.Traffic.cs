using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Rebellion.Multiplayer.Tactical
{
    internal sealed partial class MultiplayerTacticalProtocol : IDisposable
    {
        /// <summary>
        /// Validates identities and bounded payloads before retaining authenticated traffic.
        /// </summary>
        /// <param name="packet">The admitted counterpart's packet.</param>
        public void Receive(MultiplayerPacket packet)
        {
            if (_disposed || Failure != null)
                return;
            try
            {
                if (
                    packet == null
                    || Encoding.UTF8.GetByteCount(packet.Metadata)
                        > MultiplayerPacketCodec.MaximumTacticalMetadataBytes
                )
                    throw new InvalidDataException("Battle metadata exceeds its limit.");
                Envelope envelope =
                    JsonUtility.FromJson<Envelope>(packet.Metadata)
                    ?? throw new InvalidDataException("Battle metadata is missing.");
                if (
                    !MultiplayerSessionIdentity.Matches(
                        _initializationID,
                        envelope.initializationID
                    ) || !string.Equals(_battleID, envelope.battleID, StringComparison.Ordinal)
                )
                    return;
                ValidatePayload(packet);
                switch (packet.Type)
                {
                    case MultiplayerMessageType.TacticalReady:
                        if (!_remoteReady)
                            _lastPeerActivity = _lastStateAt = Now();
                        _remoteReady = true;
                        break;
                    case MultiplayerMessageType.TacticalCommand:
                        RequireRole(MultiplayerRole.Host);
                        envelope.actor = MultiplayerRole.Guest;
                        if (envelope.requestID < 2000000001)
                            throw new InvalidDataException(
                                "The battle request identity is invalid."
                            );
                        Enqueue(new Received(packet.Type, envelope, packet.Payload));
                        break;
                    case MultiplayerMessageType.TacticalCommandResult:
                    case MultiplayerMessageType.TacticalCheckpoint:
                    case MultiplayerMessageType.TacticalEffects:
                        RequireRole(MultiplayerRole.Guest);
                        Enqueue(new Received(packet.Type, envelope, packet.Payload));
                        break;
                    case MultiplayerMessageType.TacticalSnapshot:
                        RequireRole(MultiplayerRole.Guest);
                        if (envelope.sequence <= 0)
                            throw new InvalidDataException(
                                "The battle projection identity is invalid."
                            );
                        if (
                            envelope.sequence > _lastSnapshot
                            && (
                                _replaceableSnapshot == null
                                || envelope.sequence > _replaceableSnapshot.Envelope.sequence
                            )
                        )
                            _replaceableSnapshot = new Received(
                                packet.Type,
                                envelope,
                                packet.Payload
                            );
                        break;
                    case MultiplayerMessageType.TacticalSnapshotReceipt:
                        RequireRole(MultiplayerRole.Host);
                        if (
                            envelope.sequence <= 0
                            || envelope.sequence > _lastSnapshot
                            || envelope.effectSequence < 0
                            || envelope.effectSequence > _lastPublishedEffect
                        )
                            throw new InvalidDataException(
                                "The peer acknowledged unpublished battle state or effects."
                            );
                        if (
                            _terminalSequence != 0
                            && envelope.sequence >= _terminalSequence
                            && envelope.effectSequence >= _terminalEffects
                        )
                            _terminalAcknowledged = true;
                        break;
                    case MultiplayerMessageType.TacticalMovieReceipt:
                        RequireRole(MultiplayerRole.Host);
                        if (envelope.movieID <= _completedMovie)
                            break;
                        if (envelope.movieID != _getMovieID())
                            throw new InvalidDataException(
                                "The movie receipt does not address the active movie."
                            );
                        _remoteMovieAck = envelope.movieID;
                        break;
                    case MultiplayerMessageType.TacticalResync:
                        RequireRole(MultiplayerRole.Host);
                        if (
                            _latestSnapshot != null
                            && Now() - _lastCheckpointAt >= _checkpointIntervalMilliseconds
                        )
                            SendCheckpoint();
                        break;
                    default:
                        throw new InvalidDataException("The packet is not a tactical event.");
                }
                _lastPeerActivity = Now();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Applies reliable events and newest state without coupling the host clock to receipts.
        /// </summary>
        public void Pump()
        {
            if (_disposed || Failure != null)
                return;
            try
            {
                CheckDeadlines();
                while (_events.Count != 0 && Failure == null)
                {
                    if (
                        _events.Peek().Type == MultiplayerMessageType.TacticalCommand
                        && (!_localReady || !_remoteReady)
                    )
                        break;
                    Received item = _events.Dequeue();
                    _queuedBytes -= item.Payload.Length;
                    switch (item.Type)
                    {
                        case MultiplayerMessageType.TacticalCommand:
                            ApplyCommand(item);
                            break;
                        case MultiplayerMessageType.TacticalCommandResult:
                            ApplyResult(item);
                            break;
                        case MultiplayerMessageType.TacticalEffects:
                            RetainEffects(item);
                            break;
                        default:
                            ApplySnapshot(item);
                            break;
                    }
                }
                if (_replaceableSnapshot != null && _localReady && _remoteReady)
                {
                    Received state = _replaceableSnapshot;
                    _replaceableSnapshot = null;
                    ApplySnapshot(state);
                }
                ApplyReadyEffects();
                int movieID = _getMovieID();
                if (
                    _role == MultiplayerRole.Host
                    && movieID > _completedMovie
                    && _localMovieAck == movieID
                    && _remoteMovieAck == movieID
                )
                {
                    if (!_completeMovie(movieID))
                        throw new InvalidDataException(
                            "The acknowledged movie could not complete."
                        );
                    _completedMovie = movieID;
                }
                if (
                    CanAdvance
                    && _latestSnapshot != null
                    && Now() - _lastCheckpointAt >= _checkpointIntervalMilliseconds
                )
                    SendCheckpoint();
                else if (
                    _role == MultiplayerRole.Guest
                    && _localReady
                    && _remoteReady
                    && Now() - _lastStateAt >= _resyncIntervalMilliseconds
                    && Now() - _lastResyncAt >= _resyncIntervalMilliseconds
                )
                {
                    _lastResyncAt = Now();
                    Send(
                        MultiplayerMessageType.TacticalResync,
                        new Envelope { sequence = _lastSnapshot }
                    );
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }
    }
}
