using System;
using System.Collections.Generic;
using System.IO;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Multiplayer.Tactical
{
    internal sealed partial class MultiplayerTacticalProtocol : IDisposable
    {
        /// <summary>
        /// Captures complete state and sends its effects through a separate reliable ordered batch.
        /// </summary>
        public void PublishSnapshot()
        {
            if (!CanAdvance || _terminalSequence != 0)
                return;
            try
            {
                long sequence = checked(_lastSnapshot + 1);
                TacticalCombatSnapshot snapshot =
                    _captureSnapshot(sequence)
                    ?? throw new InvalidDataException("The captured battle projection is missing.");
                if (snapshot.Sequence != sequence)
                    throw new InvalidDataException(
                        "The captured battle projection identity is inconsistent."
                    );
                TacticalCombatEvent[] effects =
                    snapshot.Effects
                    ?? throw new InvalidDataException("The captured battle effects are missing.");
                snapshot.Effects = Array.Empty<TacticalCombatEvent>();
                // Whole milliseconds give binary state and JSON effect metadata the same exact cursor boundary.
                snapshot.TimestampMilliseconds = Math.Floor(Now());
                long watermark =
                    effects.Length == 0 ? _lastPublishedEffect : checked(_lastPublishedEffect + 1);
                bool checkpoint =
                    _latestSnapshot == null
                    || snapshot.MovieID != _latestSnapshot.MovieID
                    || snapshot.Complete
                    || Now() - _lastCheckpointAt >= _checkpointIntervalMilliseconds;
                _latestSnapshot = snapshot;
                _latestEffectWatermark = watermark;
                _lastSnapshot = sequence;
                _lastPublishedEffect = watermark;
                if (snapshot.Complete)
                {
                    _terminalSequence = sequence;
                    _terminalEffects = watermark;
                    _terminalAt = Now();
                }
                if (checkpoint)
                    SendCheckpoint();
                else
                    Send(
                        MultiplayerMessageType.TacticalSnapshot,
                        new Envelope { sequence = sequence },
                        MultiplayerTacticalSnapshotCodec.Encode(snapshot)
                    );
                if (Failure == null && effects.Length != 0)
                    Send(
                        MultiplayerMessageType.TacticalEffects,
                        new Envelope
                        {
                            sequence = sequence,
                            effectSequence = watermark,
                            timestampMilliseconds = snapshot.TimestampMilliseconds,
                        },
                        MultiplayerTacticalSnapshotCodec.EncodeEffects(effects)
                    );
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Records local movie completion without releasing the other presentation's pause.
        /// </summary>
        /// <param name="movieID">The exact displayed movie identity.</param>
        public void AcknowledgeMovie(int movieID)
        {
            if (
                _disposed
                || Failure != null
                || movieID <= 0
                || movieID != _getMovieID()
                || _localMovieAck == movieID
            )
                return;
            _localMovieAck = movieID;
            if (_role == MultiplayerRole.Guest)
                Send(
                    MultiplayerMessageType.TacticalMovieReceipt,
                    new Envelope { movieID = movieID }
                );
        }

        /// <summary>
        /// Applies newer complete states while permitting dropped or overtaken replaceable updates.
        /// </summary>
        /// <param name="item">The complete authoritative state or reliable checkpoint.</param>
        private void ApplySnapshot(Received item)
        {
            if (
                !_localReady
                || !_remoteReady
                || item.Envelope.sequence <= 0
                || item.Envelope.effectSequence < 0
            )
                throw new InvalidDataException(
                    "The battle projection is unavailable or has an invalid identity."
                );
            if (item.Envelope.sequence > _lastSnapshot)
            {
                TacticalCombatSnapshot snapshot = MultiplayerTacticalSnapshotCodec.Decode(
                    item.Payload
                );
                if (
                    snapshot.Sequence != item.Envelope.sequence
                    || snapshot.Effects == null
                    || snapshot.Effects.Length != 0
                    || !double.IsFinite(snapshot.TimestampMilliseconds)
                )
                    throw new InvalidDataException(
                        "The battle projection identity or event separation is inconsistent."
                    );
                _applySnapshot(snapshot);
                _lastSnapshot = snapshot.Sequence;
                _lastStateAt = Now();
            }
            if (item.Type == MultiplayerMessageType.TacticalCheckpoint)
            {
                if (
                    _checkpointReceipts.Count >= MaximumPendingEffectBatches
                    && !_checkpointReceipts.ContainsKey(item.Envelope.sequence)
                )
                    throw new InvalidDataException(
                        "Too many battle checkpoints await their effects."
                    );
                if (
                    _checkpointReceipts.TryGetValue(item.Envelope.sequence, out long existing)
                    && existing != item.Envelope.effectSequence
                )
                    throw new InvalidDataException(
                        "A repeated battle checkpoint changed its event fence."
                    );
                _checkpointReceipts[item.Envelope.sequence] = item.Envelope.effectSequence;
            }
            ApplyReadyEffects();
        }

        /// <summary>
        /// Retains each reliable effect batch once until sufficiently recent full state is available.
        /// </summary>
        /// <param name="item">The reliable batch and its required state identity.</param>
        private void RetainEffects(Received item)
        {
            if (
                item.Envelope.sequence <= 0
                || item.Envelope.effectSequence <= 0
                || !double.IsFinite(item.Envelope.timestampMilliseconds)
                || item.Envelope.timestampMilliseconds < 0
            )
                throw new InvalidDataException(
                    "The battle effect identity or timestamp is invalid."
                );
            if (item.Envelope.effectSequence <= _lastReceivedEffect)
            {
                if (
                    item.Envelope.effectSequence == _lastReceivedEffect
                    && !_lastEffectPayload.AsSpan().SequenceEqual(item.Payload)
                )
                    throw new InvalidDataException(
                        "A repeated battle effect batch changed its contents."
                    );
                return;
            }
            if (item.Envelope.effectSequence != checked(_lastReceivedEffect + 1))
                throw new InvalidDataException(
                    "The reliable battle effect sequence is incomplete."
                );
            if (
                _pendingEffects.Count >= MaximumPendingEffectBatches
                || item.Payload.Length > _maximumQueuedBytes - _pendingEffectBytes
            )
                throw new InvalidDataException("Too many battle effects await applicable state.");
            _lastReceivedEffect = item.Envelope.effectSequence;
            _lastEffectPayload = item.Payload;
            _pendingEffectBytes += item.Payload.Length;
            _pendingEffects.Enqueue(item);
            ApplyReadyEffects();
        }

        /// <summary>
        /// Applies effects once at their playback time before acknowledging the associated checkpoint fences.
        /// </summary>
        private void ApplyReadyEffects()
        {
            double cursor = _presentationTimestamp?.Invoke() ?? double.PositiveInfinity;
            if (double.IsNaN(cursor))
                throw new InvalidDataException("The battle presentation timestamp is invalid.");
            while (
                _pendingEffects.Count != 0
                && _pendingEffects.Peek().Envelope.sequence <= _lastSnapshot
                && _pendingEffects.Peek().Envelope.timestampMilliseconds <= cursor
            )
            {
                Received item = _pendingEffects.Dequeue();
                _pendingEffectBytes -= item.Payload.Length;
                TacticalCombatEvent[] effects = MultiplayerTacticalSnapshotCodec.DecodeEffects(
                    item.Payload
                );
                if (effects == null || effects.Length == 0)
                    throw new InvalidDataException("The reliable battle effect batch is empty.");
                _applyEffects(effects);
                _lastAppliedEffect = item.Envelope.effectSequence;
            }
            List<long> acknowledged = null;
            foreach (var checkpoint in _checkpointReceipts)
            {
                if (checkpoint.Key > _lastSnapshot || checkpoint.Value > _lastAppliedEffect)
                    continue;
                Send(
                    MultiplayerMessageType.TacticalSnapshotReceipt,
                    new Envelope { sequence = checkpoint.Key, effectSequence = checkpoint.Value }
                );
                (acknowledged ??= new()).Add(checkpoint.Key);
            }
            if (acknowledged != null)
                foreach (long sequence in acknowledged)
                    _checkpointReceipts.Remove(sequence);
        }

        /// <summary>
        /// Sends the latest captured state without consuming newly accumulated simulation effects.
        /// </summary>
        private void SendCheckpoint()
        {
            _lastCheckpointAt = Now();
            Send(
                MultiplayerMessageType.TacticalCheckpoint,
                new Envelope
                {
                    sequence = _latestSnapshot.Sequence,
                    effectSequence = _latestEffectWatermark,
                },
                MultiplayerTacticalSnapshotCodec.Encode(_latestSnapshot)
            );
        }
    }
}
