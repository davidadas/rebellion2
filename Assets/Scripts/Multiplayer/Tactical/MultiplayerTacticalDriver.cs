using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Multiplayer.Tactical
{
    /// <summary>
    /// Gives the host sole combat-clock ownership while each player prepares and inspects a local view.
    /// </summary>
    internal sealed class MultiplayerTacticalDriver : ITacticalCombatPresentationDriver
    {
        private const float _snapshotIntervalMilliseconds = 50;
        private readonly MultiplayerRole _role;
        private readonly string _battleID;
        private readonly SpaceCombatCommands _commands;
        private readonly MultiplayerTurnProtocol _turns;
        private readonly MultiplayerTacticalProtocol _protocol;
        private readonly Func<uint> _clockProvider;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private Task<PlayerRequestOutcome> _completion;
        private bool _prepared;
        private bool _ready;
        private bool _clockConfigured;
        private bool _disposed;
        private bool _completionAgreed;
        private bool _terminalSnapshotPublished;
        private float _snapshotMilliseconds;
        private bool _publishedSnapshot;
        private int _publishedMovieID;
        private bool _providerObserved;
        private uint _previousProviderTime;
        private double _providerMilliseconds;
        public TacticalCombatSession Session { get; }
        public TacticalCombatReplicaPlayback ReplicaPlayback { get; }
        ITacticalCombatPresentationTimeline ITacticalCombatPresentationDriver.ReplicaPlayback =>
            ReplicaPlayback;
        public bool IsAuthoritative => _role == MultiplayerRole.Host;
        public SpaceCombatResult CommittedResult { get; private set; }
        public event Action<SpaceCombatResult> CompletedBattle;
        public bool CompletionReady =>
            _completionAgreed && Session.IsComplete && CommittedResult != null;

        /// <summary>
        /// Connects an ordered campaign encounter to its authoritative battle transport.
        /// </summary>
        /// <param name="role">The authenticated local role.</param>
        /// <param name="initializationID">The active campaign initialization.</param>
        /// <param name="battleID">The host-issued battle identity.</param>
        /// <param name="session">The existing local encounter.</param>
        /// <param name="commands">The campaign owner of the encounter.</param>
        /// <param name="turns">The ordered campaign request owner.</param>
        /// <param name="send">The reliable admitted transport.</param>
        /// <param name="clockProvider">Optional monotonic clock for controlled playback.</param>
        public MultiplayerTacticalDriver(
            MultiplayerRole role,
            string initializationID,
            string battleID,
            TacticalCombatSession session,
            SpaceCombatCommands commands,
            MultiplayerTurnProtocol turns,
            Action<MultiplayerPacket> send,
            Func<uint> clockProvider = null
        )
        {
            if (role is not (MultiplayerRole.Host or MultiplayerRole.Guest))
                throw new ArgumentOutOfRangeException(nameof(role));
            _role = role;
            _battleID = battleID;
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _turns = turns ?? throw new ArgumentNullException(nameof(turns));
            _clockProvider = clockProvider;
            if (Session.IsSynchronized)
            {
                if (Session.IsReplica == IsAuthoritative)
                    throw new InvalidOperationException(
                        "The battle playback mode does not match its authenticated role."
                    );
            }
            else if (IsAuthoritative)
                Session.ConfigureSynchronizedAuthority();
            else
                Session.ConfigureSynchronizedReplica();
            CombatSide hostSide =
                IsAuthoritative ? session.PlayerSide
                : session.PlayerSide == CombatSide.Attacker ? CombatSide.Defender
                : CombatSide.Attacker;
            _protocol = new MultiplayerTacticalProtocol(
                role,
                initializationID,
                battleID,
                hostSide,
                Session.ApplySynchronizedCommand,
                Session.CaptureSnapshot,
                Session.ApplySnapshot,
                Session.ApplySynchronizedEvents,
                () => Session.PendingSynchronizedMovieID,
                Session.CompleteSynchronizedMovie,
                send,
                ReadSnapshotMilliseconds,
                () => ReplicaPlayback?.TimestampMilliseconds ?? 0
            );
            if (Session.IsReplica)
                ReplicaPlayback = new TacticalCombatReplicaPlayback(Session);
            _protocol.Faulted += _turns.ReportFailure;
            Session.SynchronizedCommandRequested += HandleCommand;
            _turns.ActionApplied += HandleAppliedAction;
            _turns.ActionAcknowledged += HandleAcknowledgedAction;
        }

        /// <summary>
        /// Starts only this player's loaded entry presentation before announcing readiness.
        /// </summary>
        /// <param name="presentation">The local renderer's arrival owner.</param>
        public void Prepare(ITacticalCombatEntry presentation)
        {
            if (_disposed || _prepared)
                throw new InvalidOperationException(
                    "The battle presentation is unavailable or already prepared."
                );
            Session.BeginArrival(presentation);
            _prepared = true;
        }

        /// <summary>
        /// Drains transport even when local assets or a movie prevent simulation advancement.
        /// </summary>
        public void Pump() => _protocol.Pump();

        /// <summary>
        /// Receives an addressed battle packet from the admitted counterpart.
        /// </summary>
        /// <param name="packet">The reliable battle packet.</param>
        public void Receive(MultiplayerPacket packet) => _protocol.Receive(packet);

        /// <summary>
        /// Advances the host independently of transport receipts and publishes state at a bounded cadence.
        /// </summary>
        /// <param name="elapsedSeconds">The local unscaled frame interval.</param>
        public void Advance(float elapsedSeconds)
        {
            if (_disposed || _turns.Failure != null)
                return;
            if (
                elapsedSeconds < 0
                || float.IsNaN(elapsedSeconds)
                || float.IsInfinity(elapsedSeconds)
            )
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            _protocol.Pump();
            if (!_prepared)
                return;
            if (!IsAuthoritative)
                ReplicaPlayback.Advance(elapsedSeconds * 1000);
            if (!_ready)
            {
                if (!Session.AdvanceSynchronizedArrival(elapsedSeconds * 1000))
                    return;
                _ready = true;
                _protocol.Ready();
            }
            Session.AdvanceLocalSelection();
            _protocol.Pump();
            if (!IsAuthoritative || !_protocol.CanAdvance || CommittedResult != null)
                return;
            if (Session.IsComplete)
            {
                if (!_terminalSnapshotPublished)
                {
                    _protocol.PublishSnapshot();
                    _terminalSnapshotPublished = true;
                    return;
                }
                if (_protocol.TerminalSnapshotAcknowledged)
                    SubmitCompletion();
                return;
            }
            if (!_clockConfigured)
            {
                Session.ConfigureClockedAdvance(() => unchecked((uint)ReadSnapshotMilliseconds()));
                _clockConfigured = true;
            }
            Session.AdvanceClocked(elapsedSeconds * 1000);
            _snapshotMilliseconds += elapsedSeconds * 1000;
            if (
                !_publishedSnapshot
                || Session.IsComplete
                || Session.PendingSynchronizedMovieID != _publishedMovieID
                || _snapshotMilliseconds >= _snapshotIntervalMilliseconds
            )
            {
                _protocol.PublishSnapshot();
                _publishedSnapshot = true;
                _publishedMovieID = Session.PendingSynchronizedMovieID;
                _snapshotMilliseconds %= _snapshotIntervalMilliseconds;
                _terminalSnapshotPublished = Session.IsComplete;
            }
        }

        /// <summary>
        /// Reads a continuous connection clock while retaining wrapping millisecond providers for battle scheduling.
        /// </summary>
        /// <returns>The monotonic milliseconds used by publication, interpolation stamps, and connection deadlines.</returns>
        private double ReadSnapshotMilliseconds()
        {
            if (_clockProvider == null)
                return _clock.Elapsed.TotalMilliseconds;
            uint current = _clockProvider();
            if (_providerObserved)
                _providerMilliseconds += unchecked(current - _previousProviderTime);
            else
            {
                _providerObserved = true;
                _providerMilliseconds = current;
            }
            _previousProviderTime = current;
            return _providerMilliseconds;
        }

        /// <summary>
        /// Acknowledges only the movie currently owned by this view without releasing its peer's pause.
        /// </summary>
        /// <param name="request">The local movie ownership token.</param>
        public void AcknowledgeMovie(TacticalCombatMovieRequest request)
        {
            if (ReferenceEquals(request, Session.PendingAttackRunMovie))
                _protocol.AcknowledgeMovie(Session.PendingSynchronizedMovieID);
        }

        /// <summary>
        /// Aborts incomplete shared preparation rather than leaving the counterpart waiting indefinitely.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            if (!CompletionReady && _turns.Failure == null)
                _protocol.ReportFailure(
                    new InvalidOperationException(
                        "The shared battle presentation ended before completion."
                    )
                );
            Release();
        }

        /// <summary>
        /// Releases connection-owned resources while its campaign owner handles disconnect or replacement.
        /// </summary>
        public void Release()
        {
            if (_disposed)
                return;
            _disposed = true;
            Session.SynchronizedCommandRequested -= HandleCommand;
            _turns.ActionApplied -= HandleAppliedAction;
            _turns.ActionAcknowledged -= HandleAcknowledgedAction;
            _protocol.Faulted -= _turns.ReportFailure;
            _protocol.Dispose();
            ReplicaPlayback?.Dispose();
        }

        /// <summary>
        /// Submits the final typed outcome only after the peer has acknowledged the terminal projection.
        /// </summary>
        private void SubmitCompletion()
        {
            if (_completion == null)
            {
                if (!_commands.TryGetPendingCombat(out PendingCombatResult pending))
                {
                    _protocol.ReportFailure(
                        new InvalidOperationException(
                            "The active battle lost its campaign decision."
                        )
                    );
                    return;
                }
                _completion = _turns.ExecuteAsync(
                    new CompleteTacticalCombatRequest
                    {
                        PlanetID = pending.PlanetInstanceID,
                        Tick = pending.Tick,
                        BattleID = _battleID,
                        Outcome = Session.CaptureSynchronizedOutcome(),
                    }
                );
            }
            if (_completion.IsCompleted && !_completion.GetAwaiter().GetResult().Accepted)
                _protocol.ReportFailure(
                    new InvalidOperationException("The shared battle result was not accepted.")
                );
        }

        /// <summary>
        /// Routes local semantic input to the authenticated command owner.
        /// </summary>
        /// <param name="command">The locally captured intent without an asserted actor.</param>
        private void HandleCommand(TacticalCombatCommand command) =>
            _protocol.ExecuteAsync(command);

        /// <summary>
        /// Retains the local campaign result produced by the ordered final commit.
        /// </summary>
        /// <param name="request">The executed typed operation.</param>
        /// <param name="outcome">Its actual local campaign result.</param>
        private void HandleAppliedAction(PlayerRequest request, PlayerRequestOutcome outcome)
        {
            if (
                request is CompleteTacticalCombatRequest complete
                && complete.BattleID == _battleID
                && outcome.Accepted
            )
            {
                CommittedResult =
                    outcome.SpaceCombatResult
                    ?? throw new InvalidOperationException(
                        "The completed battle did not produce a campaign result."
                    );
                if (!IsAuthoritative && _turns.Failure == null)
                    _completionAgreed = true;
                CompletedBattle?.Invoke(CommittedResult);
            }
        }

        /// <summary>
        /// Retains final battle agreement independently of receipts for subsequent campaign actions.
        /// </summary>
        /// <param name="kind">The exact acknowledged action discriminator.</param>
        private void HandleAcknowledgedAction(PlayerRequestKind kind)
        {
            if (kind == PlayerRequestKind.CompleteTacticalCombat && CommittedResult != null)
                _completionAgreed = true;
        }
    }
}
