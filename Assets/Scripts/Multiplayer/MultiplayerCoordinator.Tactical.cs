using System;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Tactical;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Connects ordered campaign encounters to their shared authoritative combat presentation.
    /// </summary>
    public sealed partial class MultiplayerCoordinator
    {
        private MultiplayerTacticalDriver _tacticalDriver;
        private MultiplayerPacket _pendingTacticalReady;
        public ITacticalCombatPresentationDriver ActiveTacticalCombatDriver => _tacticalDriver;
        public event Action<ITacticalCombatPresentationDriver> TacticalCombatAvailable;
        public event Action<SpaceCombatResult> CompletedBattle;

        /// <summary>
        /// Returns the driver already admitted by the ordered begin request for this exact encounter.
        /// </summary>
        /// <param name="session">The encounter whose local view is opening.</param>
        /// <returns>The connection-owned shared presentation driver.</returns>
        public ITacticalCombatPresentationDriver GetTacticalCombatDriver(
            TacticalCombatSession session
        )
        {
            if (
                !_synchronizationReady
                || _tacticalDriver == null
                || !ReferenceEquals(_tacticalDriver.Session, session)
            )
                throw new InvalidOperationException(
                    "The shared battle presentation is unavailable."
                );
            return _tacticalDriver;
        }

        /// <summary>
        /// Creates both local drivers only after the ordered campaign begin request has executed.
        /// </summary>
        /// <param name="request">The executed typed campaign request.</param>
        /// <param name="outcome">Its real domain acceptance.</param>
        private void HandleTacticalActionApplied(
            PlayerRequest request,
            PlayerRequestOutcome outcome
        )
        {
            if (
                request is not (BeginTacticalCombatRequest or ResolveCombatRequest)
                || !outcome.Accepted
            )
                return;
            SpaceCombatCommands commands = _sessionManager
                .GetActiveGameSession()
                .GetService<SpaceCombatCommands>();
            if (
                commands.ActiveTacticalCombat == null
                || ReferenceEquals(_tacticalDriver?.Session, commands.ActiveTacticalCombat)
            )
                return;
            if (_tacticalDriver?.CompletionReady == false)
                throw new InvalidOperationException(
                    "A shared battle presentation is already active."
                );
            ReleaseTacticalGameplay();
            _tacticalDriver = new MultiplayerTacticalDriver(
                _role,
                _initializationID,
                commands.ActiveTacticalBattleID,
                commands.ActiveTacticalCombat,
                commands,
                _turnProtocol,
                QueueSend
            );
            _tacticalDriver.CompletedBattle += HandleTacticalBattleCompleted;
            DeliverPendingTacticalReady();
            TacticalCombatAvailable?.Invoke(_tacticalDriver);
        }

        /// <summary>
        /// Retains the one readiness edge that can follow a begin commit in the same transport poll.
        /// </summary>
        /// <param name="packet">Readiness whose ordered campaign entry is still queued.</param>
        private void QueueTacticalReady(MultiplayerPacket packet)
        {
            if (
                _pendingTacticalReady != null
                && !string.Equals(
                    _pendingTacticalReady.Metadata,
                    packet.Metadata,
                    StringComparison.Ordinal
                )
            )
                throw new InvalidOperationException(
                    "More than one unadmitted battle requested readiness."
                );
            _pendingTacticalReady = packet;
        }

        /// <summary>
        /// Delivers deferred readiness after the ordered domain request has created its battle owner.
        /// </summary>
        private void DeliverPendingTacticalReady()
        {
            if (_pendingTacticalReady == null)
                return;
            if (
                _tacticalDriver?.CompletionReady == true
                && _turnProtocol?.HasQueuedTacticalEntry == true
            )
                return;
            if (_tacticalDriver == null)
            {
                if (_turnProtocol?.HasQueuedTacticalEntry != true)
                    StopGameplay(
                        new InvalidOperationException(
                            "The ready battle did not enter its campaign owner."
                        ),
                        true
                    );
                return;
            }
            MultiplayerPacket packet = _pendingTacticalReady;
            _pendingTacticalReady = null;
            _tacticalDriver.Receive(packet);
        }

        /// <summary>
        /// Publishes the already applied local campaign result without committing it a second time.
        /// </summary>
        /// <param name="result">The locally applied result of the ordered final outcome.</param>
        private void HandleTacticalBattleCompleted(SpaceCombatResult result) =>
            CompletedBattle?.Invoke(result);

        /// <summary>
        /// Releases battle transport and callbacks when the campaign connection or runtime is replaced.
        /// </summary>
        private void ReleaseTacticalGameplay()
        {
            if (_tacticalDriver == null)
                return;
            _tacticalDriver.CompletedBattle -= HandleTacticalBattleCompleted;
            _tacticalDriver.Release();
            _tacticalDriver = null;
        }
    }
}
