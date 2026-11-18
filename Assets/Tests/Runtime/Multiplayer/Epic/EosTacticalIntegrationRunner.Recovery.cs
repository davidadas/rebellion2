using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using Rebellion.Tactical;
using Rebellion.Tactical.Integration.Campaign;
using UnityEngine;

namespace Rebellion.Tests.MultiplayerIntegration
{
    /// <summary>
    /// Verifies an explicitly requested departure, completed-checkpoint recovery, and fresh battle admission.
    /// </summary>
    public sealed partial class EosTacticalIntegrationRunner
    {
        private bool _initialRecoveryTickObserved;
        private bool _recoveryInterrupted;
        private bool _expectedDisconnectPublished;
        private bool _recoveredStatePublished;
        private bool _freshResponsesPublished;
        private bool _recoveryCompletePublished;
        private GameSession _observedSession;
        private ITacticalCombatPresentationDriver _interruptedDriver;
        private TacticalCombatSession _interruptedSession;
        private Dictionary<string, (float Hull, int Squadrons)> _originalRoster;

        /// <summary>
        /// Stops the original presentation only after the production survivor has entered recovery.
        /// </summary>
        /// <returns>Whether the original combat update is now owned by expected recovery.</returns>
        private bool TryEnterExpectedRecovery()
        {
            if (!_options.VerifyRecovery || _recoveryInterrupted || !_coordinator.IsRejoining)
                return false;
            Require(
                _options.Role == "host",
                "Only the expected host survivor may observe peer departure."
            );
            EosTacticalIntegrationReport peer = ReadPeerReport();
            if (peer == null)
                return true;
            Require(
                peer.expectedDisconnectSubmitted,
                "The real peer departed before its expected-disconnect evidence was published."
            );
            _report.peerDepartureObserved = true;
            _recoveryInterrupted = true;
            DetachCombatObservers();
            _state = "recovery";
            SetPhase("recovery-survivor");
            return true;
        }

        /// <summary>
        /// Disconnects the real guest once both players have exercised control and observed natural battle damage.
        /// </summary>
        /// <param name="session">The active authoritative-frame-backed battle.</param>
        /// <returns>Whether the original combat update must stop.</returns>
        private bool TryDisconnectForRecovery(TacticalCombatSession session)
        {
            if (!_options.VerifyRecovery || _options.Role != "guest" || _recoveryInterrupted)
                return false;
            if (
                !_report.sharedResumeObserved
                || !_report.hullDamageObserved
                || _report.capitalShots == 0
                || _report.fighterShots == 0
                || _peerReport?.sharedResumeObserved != true
                || !_peerReport.hullDamageObserved
                || _peerReport.capitalShots == 0
                || _peerReport.fighterShots == 0
                || session.IsComplete
            )
                return false;
            RequireNoRecoveryResults();
            if (!_report.expectedDisconnectSubmitted)
            {
                _report.expectedDisconnectSubmitted = true;
                Publish();
            }
            if (!_expectedDisconnectPublished)
                return false;
            _recoveryInterrupted = true;
            DetachCombatObservers();
            _coordinator.Disconnect();
            Require(
                _coordinator.HasRecovery,
                "The authenticated guest has no retained campaign recovery reservation."
            );
            _operation = null;
            _state = "recovery";
            SetPhase("recovery-disconnected");
            return true;
        }

        /// <summary>
        /// Waits for actual survivor rollback, public same-account rejoin, and matching paused recovered worlds.
        /// </summary>
        private void AdvanceRecovery()
        {
            RequireNoRecoveryResults();
            if (_options.Role == "guest" && !_report.recoveryResumeRequested)
            {
                if (_peerReport?.rollbackObserved != true)
                    return;
                Require(
                    _peerReport.recoveredTick == _report.recoveryInitialTick,
                    "The survivor did not return to the initial completed tick."
                );
                _report.recoveryResumeRequested = true;
                _operation = _coordinator.ResumeAsync();
                SetPhase("recovery-rejoining");
            }
            GameSession session = _runtime.GetActiveGameSession();
            if (_options.Role == "host" && !_report.rollbackObserved)
            {
                if (!session.CanCaptureSettledState || !_coordinator.IsRejoining)
                    return;
                Require(
                    session.Game.CurrentTick == _report.recoveryInitialTick,
                    "The survivor retained an interrupted campaign tick."
                );
                Require(
                    session.GetService<SpaceCombatCommands>().ActiveTacticalCombat == null
                        && !session.GetService<SpaceCombatCommands>().TryGetPendingCombat(out _),
                    "Rollback retained an interrupted battle or response."
                );
                _report.recoveredTick = session.Game.CurrentTick;
                _report.rollbackObserved = true;
                SetPhase("recovery-rolled-back");
            }
            if (!_coordinator.IsSynchronized)
                return;
            if (!_report.recoverySynchronized)
            {
                Authenticated();
                GameRoot game = session.Game;
                MultiplayerCampaignIdentity identity = game.Metadata.MultiplayerCampaign;
                Require(
                    session.CanCaptureSettledState
                        && game.CurrentTick == _report.recoveryInitialTick
                        && game.GetGameSpeed() == TickSpeed.Paused
                        && game.GetPlayers()
                            .Where(player => player.ControllerType == PlayerControllerType.Human)
                            .All(player => player.RequestedGameSpeed == TickSpeed.Paused),
                    "Rejoin must restore a settled paused completed checkpoint."
                );
                Require(
                    identity.HostUserID == _report.hostUserID
                        && identity.GuestUserID == _report.guestUserID
                        && _report.localUserID
                            == (
                                _options.Role == "host" ? identity.HostUserID : identity.GuestUserID
                            )
                        && game.LocalPlayerFactionID
                            == (
                                _options.Role == "host"
                                    ? identity.HostFactionID
                                    : identity.GuestFactionID
                            ),
                    "Recovery changed the authenticated participants or local faction ownership."
                );
                Require(
                    _options.Role != "guest" || _report.recoveryLaunchObserved,
                    "The returning guest did not launch the actual restored campaign."
                );
                _report.recoveredTick = game.CurrentTick;
                _report.recoveredCampaignFingerprint =
                    MultiplayerSnapshotCodec.ComputeStateFingerprint(game);
                _report.rollbackObserved = true;
                _report.recoveryRoleVerified = true;
                _report.recoverySynchronized = true;
                SetPhase("recovery-synchronized");
            }
            if (!_recoveredStatePublished || _peerReport?.recoverySynchronized != true)
                return;
            Require(
                _peerReport.recoveredTick == _report.recoveredTick
                    && _peerReport.recoveredCampaignFingerprint
                        == _report.recoveredCampaignFingerprint
                    && _peerReport.recoveryRoleVerified,
                "The rejoined peers disagree on their paused recovered campaign."
            );
            _dayStarted = false;
            _speedRequested = false;
            _begin = null;
            _operation = null;
            Require(_tick == null, "The interrupted campaign iterator still owns recovery.");
            _state = "campaign";
            SetPhase("recovery-fresh-day");
        }

        /// <summary>
        /// Requires both players to observe fresh unanswered decisions before either submits its new response.
        /// </summary>
        /// <param name="pending">The new ordinary tick's combat decision.</param>
        /// <returns>Whether both published response-reset checks permit normal battle admission.</returns>
        private bool VerifyFreshResponses(PendingCombatResult pending)
        {
            RequireNoRecoveryResults();
            if (!_report.freshResponsesVerified)
            {
                Require(
                    !pending.AttackerHasResponded && !pending.DefenderHasResponded,
                    "The recovered encounter retained a previous human response."
                );
                _report.freshResponsesVerified = true;
                Publish();
            }
            return _freshResponsesPublished && _peerReport?.freshResponsesVerified == true;
        }

        /// <summary>
        /// Records the first immutable roster and verifies a newly created driver and unmodified campaign roster on reentry.
        /// </summary>
        /// <param name="driver">The actual coordinator-admitted battle driver.</param>
        private void RecordRecoveryEntry(ITacticalCombatPresentationDriver driver)
        {
            if (!_options.VerifyRecovery)
                return;
            if (!_recoveryInterrupted)
            {
                _interruptedDriver = driver;
                _interruptedSession = driver.Session;
                _report.interruptedBattleID = _report.battleID;
                _originalRoster = driver.Session.Units.ToDictionary(
                    unit => unit.InstanceID,
                    unit => (unit.InitialHull, unit.InitialSquadronSize),
                    StringComparer.Ordinal
                );
                return;
            }
            Require(
                !ReferenceEquals(driver, _interruptedDriver)
                    && !ReferenceEquals(driver.Session, _interruptedSession),
                "Recovery reused the interrupted combat driver or session."
            );
            Require(
                driver.Session.Units.Count == _originalRoster.Count
                    && driver.Session.Units.All(unit =>
                        _originalRoster.TryGetValue(unit.InstanceID, out var original)
                        && unit.InitialHull == original.Hull
                        && unit.InitialSquadronSize == original.Squadrons
                    ),
                "The interrupted tactical damage leaked into the recovered campaign roster."
            );
        }

        /// <summary>
        /// Admits both real fresh views and verifies a new authoritative frame without completing another battle.
        /// </summary>
        private void AdvanceRecoveryEntry()
        {
            RequireNoRecoveryResults();
            _driver.Advance(Time.unscaledDeltaTime);
            TacticalCombatSession session = _driver.Session;
            _report.systemTime = session.SystemTime;
            if (
                session.IsArriving
                || session.IsReplica
                    && !_driver.ReplicaPlayback.TryGetPose(
                        session.Units[0].InstanceID,
                        out _,
                        out _
                    )
            )
                return;
            Require(
                !session.IsComplete
                    && !session.GetSide(CombatSide.Attacker).IsAutomated
                    && !session.GetSide(CombatSide.Defender).IsAutomated,
                "The new recovery encounter did not admit both manual players."
            );
            _report.freshBattleVerified = true;
            _report.recoveryComplete = true;
            _report.outcomeReady = true;
            _state = "recovery-outcome";
            SetPhase("recovery-outcome");
        }

        /// <summary>
        /// Ends the explicit recovery case after both peers prove fresh admission and zero unintended completion.
        /// </summary>
        private void CompareRecoveryOutcome()
        {
            _driver.Advance(Time.unscaledDeltaTime);
            RequireNoRecoveryResults();
            if (!_recoveryCompletePublished || _peerReport?.recoveryComplete != true)
                return;
            Require(
                _peerReport.verifyRecovery
                    && _peerReport.freshResponsesVerified
                    && _peerReport.freshBattleVerified
                    && _peerReport.freshBattleID == _report.freshBattleID
                    && _peerReport.recoveredCampaignFingerprint
                        == _report.recoveredCampaignFingerprint
                    && _peerReport.results == 0
                    && _peerReport.coordinatorResults == 0
                    && _peerReport.completedTicks == 0
                    && _peerReport.combatResumptions == 0,
                "The counterpart did not independently verify the same recovery without an unintended result."
            );
            _outcomePublished = true;
            SetPhase("cleanup");
            Cleanup();
            _state = "cleanup";
            _operation = EosTacticalLobbyCleanup.WaitForDisappearanceAsync(
                _report.localUserID,
                _report.lobbyID,
                TimeSpan.FromSeconds(30)
            );
        }

        /// <summary>
        /// Preserves exact zero result, day completion, and combat-resumption counts for this interrupted case.
        /// </summary>
        private void RequireNoRecoveryResults() =>
            Require(
                _report.results == 0
                    && _report.coordinatorResults == 0
                    && _report.completedTicks == 0
                    && _report.combatResumptions == 0,
                "Interrupted recovery unexpectedly committed a battle result or completed its campaign day."
            );

        /// <summary>
        /// Retains only evidence that was actually committed to this role's report.
        /// </summary>
        private void RecordRecoveryPublication()
        {
            _expectedDisconnectPublished |= _report.expectedDisconnectSubmitted;
            _recoveredStatePublished |= _report.recoverySynchronized;
            _freshResponsesPublished |= _report.freshResponsesVerified;
            _recoveryCompletePublished |= _report.recoveryComplete;
        }

        /// <summary>
        /// Attaches one test observer set to the stable runtime result and tick owners.
        /// </summary>
        /// <param name="session">The newly composed runtime session.</param>
        private void AttachRuntimeObservers(GameSession session)
        {
            _observedSession = session;
            session.Tick.TickCompleted += HandleTickCompleted;
            session.Tick.CombatResumed += HandleCombatResumed;
            _resultPipeline = session.Pipeline;
            _resultPipeline.ResultsResolved += HandleResolvedResults;
        }

        /// <summary>
        /// Releases the exact old runtime subscriptions before relaunch or cleanup.
        /// </summary>
        private void DetachRuntimeObservers()
        {
            if (_observedSession != null)
            {
                _observedSession.Tick.TickCompleted -= HandleTickCompleted;
                _observedSession.Tick.CombatResumed -= HandleCombatResumed;
                _observedSession = null;
            }
            if (_resultPipeline != null)
            {
                _resultPipeline.ResultsResolved -= HandleResolvedResults;
                _resultPipeline = null;
            }
        }

        /// <summary>
        /// Releases test callbacks while leaving unfinished driver disposal to the coordinator's connection owner.
        /// </summary>
        private void DetachCombatObservers()
        {
            if (_driver != null)
            {
                _driver.Session.NetworkCommandRequested -= HandleCommand;
                _driver.Session.ShotFired -= HandleShot;
                _driver.Session.ImpactApplied -= HandleImpact;
                _driver.Session.UnitDestroyed -= HandleDestroyed;
                _driver.Session.UnitWithdrawn -= HandleWithdrawn;
                _driver.Session.FighterRecovered -= HandleRecovered;
                _driver = null;
            }
            _evidence?.Dispose();
            _evidence = null;
        }
    }
}
