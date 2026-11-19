using System;
using System.IO;

namespace Rebellion.Multiplayer.Tactical
{
    internal sealed partial class MultiplayerTacticalProtocol : IDisposable
    {
        /// <summary>
        /// Fails stalled preparation, peer traffic, command confirmations, or terminal agreement.
        /// </summary>
        private void CheckDeadlines()
        {
            double now = Now();
            if ((!_localReady || !_remoteReady) && now - _createdAt >= ReadinessTimeoutMilliseconds)
                throw new TimeoutException("The peer did not finish preparing the shared battle.");
            if (
                _terminalSequence != 0
                && !_terminalAcknowledged
                && now - _terminalAt >= TerminalTimeoutMilliseconds
            )
                throw new TimeoutException(
                    "The peer did not confirm final battle state and effects."
                );
            if (
                _localReady
                && _remoteReady
                && now - _lastPeerActivity >= InactivityTimeoutMilliseconds
            )
                throw new TimeoutException("The shared battle peer stopped responding.");
            foreach (double submitted in _requestTimes.Values)
                if (now - submitted >= InactivityTimeoutMilliseconds)
                    throw new TimeoutException(
                        "A battle command was not confirmed before its deadline."
                    );
        }

        /// <summary>
        /// Reads the configured finite monotonic time source.
        /// </summary>
        /// <returns>The elapsed milliseconds.</returns>
        private double Now()
        {
            double value = _milliseconds();
            if (!double.IsFinite(value) || value < 0)
                throw new InvalidOperationException("The battle transport clock is invalid.");
            return value;
        }

        /// <summary>
        /// Rejects authority-bearing traffic delivered to the other role.
        /// </summary>
        /// <param name="role">The required receiving role.</param>
        private void RequireRole(MultiplayerRole role)
        {
            if (_role != role)
                throw new InvalidDataException("The battle event was sent to the wrong role.");
        }

        /// <summary>
        /// Records synchronization failure and releases retained work once.
        /// </summary>
        /// <param name="exception">The failed transport or domain boundary.</param>
        private void Fail(Exception exception)
        {
            if (_disposed || Failure != null)
                return;
            Failure = exception;
            CompleteUnknown();
            ClearTraffic();
            Faulted?.Invoke(exception);
        }

        /// <summary>
        /// Releases bounded traffic retained by a failed or disposed battle.
        /// </summary>
        private void ClearTraffic()
        {
            _events.Clear();
            _pendingEffects.Clear();
            _checkpointReceipts.Clear();
            _replaceableSnapshot = null;
            _latestSnapshot = null;
            _lastEffectPayload = null;
            _queuedBytes = _pendingEffectBytes = 0;
        }

        /// <summary>
        /// Completes unresolved operations without falsely reporting a domain rejection.
        /// </summary>
        private void CompleteUnknown()
        {
            foreach (var completion in _requests.Values)
                completion.TrySetResult(Unknown());
            _requests.Clear();
            _requestTimes.Clear();
        }
    }
}
