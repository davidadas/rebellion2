using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Tactical.Automation;
using Rebellion.Tactical.Synchronization;
using Rebellion.Util.Random;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Runs an isolated tactical encounter with continuous movement and queued whole-second commands.
    /// Campaign units remain unchanged until the completed encounter is committed.
    /// </summary>
    public sealed partial class TacticalCombatSession
    {
        private readonly List<TacticalCombatUnit> _units;
        private readonly List<TacticalCombatGroup> _groups;
        private readonly Dictionary<int, TacticalCombatGroup> _groupsByID = new();
        private readonly TacticalCombatGroup[,] _objectGroups = new TacticalCombatGroup[2, 8];
        private readonly TacticalCombatGroup[,] _fighterGroups = new TacticalCombatGroup[2, 4];
        private readonly Dictionary<string, TacticalCombatUnit> _unitsByID = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _personnelHostCounts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TacticalCombatCarrier> _carriers = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<TacticalCombatUnit, Recovery> _recoveries = new();
        private readonly HashSet<TacticalCombatUnit> _tractorCommandsPending = new();
        private readonly Dictionary<
            TacticalCombatUnit,
            SortedDictionary<uint, TacticalCombatUnit>
        > _tractorSources = new();
        private readonly List<WithdrawalMember> _orderedWithdrawalMembers = new();
        private readonly Dictionary<int, TacticalCombatUnit> _withdrawalUnitsByIndex = new();
        private readonly Action<int> _requestUnreadyScuttle;
        private readonly List<ITacticalCombatWithdrawalMember>[] _withdrawalCapitals =
        {
            new(),
            new(),
        };
        private readonly List<ITacticalCombatWithdrawalMember>[] _withdrawalFighters =
        {
            new(),
            new(),
        };
        private readonly HashSet<TacticalCombatUnit> _departed = new();
        private readonly List<TacticalCombatUnit> _initiallyDockedFighters = new();
        private readonly SortedDictionary<int, Queue<Command>> _commands = new();
        private readonly TacticalCombatSpatialIndex _spatial;
        private readonly TacticalCombatFireControl _fireControl = new();
        private readonly Vector3[] _centers = new Vector3[2];
        private readonly HashSet<TacticalCombatGroup> _pendingManeuvers = new();
        private readonly HashSet<TacticalCombatGroup> _pendingAttackOrders = new();
        private readonly List<TacticalCombatGroup> _completedAttackOrders = new();
        private readonly TacticalCombatMotion _motion = new();
        private readonly TacticalCombatSide[] _sides;
        private readonly TacticalCombatTargeting _targeting;
        private readonly TacticalCombatPlanner _automation;
        private readonly int[] _nextGroupID = new int[2];
        private readonly IRandomNumberProvider _random;
        private int _progress;
        private int _phase;
        private float _elapsedInTick;
        private int _systemTime = 3;
        private bool _legacyTimeAdvanced;
        private TacticalCombatScheduler _scheduler;
        private readonly TacticalCombatBriefingSequence _briefing;
        private readonly TacticalCombatEnding _ending;
        private bool _completionStarted;
        private TacticalCombatArrival _arrival;
        private bool _arrivalStarting;
        private bool _userPaused;
        private bool _directCommandsSuspended;
        private int _moviePauses;
        private readonly SpaceCombatSideOutcome[] _outcomes = new SpaceCombatSideOutcome[2];

        public IReadOnlyList<TacticalCombatUnit> Units { get; }
        public IReadOnlyList<TacticalCombatGroup> Groups { get; }
        internal IReadOnlyList<TacticalCombatUnit> InitiallyDockedFighters =>
            _initiallyDockedFighters;
        internal bool CanStartBattleStationAttack => CaptureBriefingState().CanAttackBattleStation;
        public CombatSide PlayerSide { get; }
        public float BattleDepth { get; }
        public TacticalCombatNavigation Navigation { get; }
        public ITacticalCombatAudio Audio { get; }
        public TacticalCombatSelection Selection { get; }
        public int SystemTime =>
            _synchronizationReplica ? _synchronizedSystemTime
            : _scheduler == null ? _systemTime
            : unchecked((int)_scheduler.SystemTime);
        public bool IsComplete { get; private set; }
        public CombatSide? EnteringSide { get; }
        public bool IsArriving => _arrival?.IsComplete == false;
        public IReadOnlyList<bool> TerminalFlags => _ending.Flags;
        public CombatSide Winner { get; private set; } = CombatSide.Draw;
        public bool IsPaused
        {
            get => _userPaused || _moviePauses > 0;
            set
            {
                if (
                    !RequestSynchronizedCommand(
                        new() { Kind = TacticalCombatCommandKind.Pause, Paused = value }
                    )
                )
                    _userPaused = value;
            }
        }
        public TacticalCombatMovieRequest PendingAttackRunMovie { get; private set; }
        public ITacticalCombatSceneryRandom SceneryRandom { get; }
        public TacticalCombatBattleStation BattleStation { get; }
        public TacticalCombatDefinition Definition { get; }
        public TacticalCombatEnvironmentDefinition Environment { get; }
        public TacticalCombatAttackRun ActiveAttackRun { get; private set; }
        public event Action<TacticalCombatShot> ShotFired;

        /// <summary>
        /// Projects a stored weapon impact at its effect boundary within damage processing.
        /// </summary>
        public event Action<
            TacticalCombatUnit,
            TacticalWeaponType,
            TacticalCombatImpactKind
        > ImpactApplied;

        /// <summary>
        /// Requests an admitted direct destruction effect before deactivating its participant.
        /// </summary>
        public event Action<TacticalCombatUnit> DestructionEffectRequested;
        public event Action<TacticalCombatUnit> UnitDestroyed;
        public event Action<TacticalCombatUnit> UnitWithdrawn;
        public event Action<TacticalCombatUnit, TacticalSystemEvent> SystemChanged;
        public event Action<TacticalCombatUnit> FighterRecovered;
        public event Action<TacticalCombatGroup> RecoveryUnavailable;
        public event Action Completed;
        public event Action GroupCategoryChanged;
        public bool IsDirectControlEnabled => IsSideDirectControlEnabled(PlayerSide);

        /// <summary>
        /// Gets whether the player can hand the surviving encounter to automatic resolution.
        /// </summary>
        public bool CanRequestImmediateResult =>
            !IsSynchronized
            && !IsComplete
            && !IsArriving
            && !_completionStarted
            && PendingAttackRunMovie == null
            && (IsDirectControlEnabled || GetSide(PlayerSide).IsAutomated)
            && !_sides[0].IsWithdrawing
            && !_sides[1].IsWithdrawing;

        /// <summary>
        /// Gets whether completion requests automatic resolution of the surviving forces.
        /// </summary>
        public bool ImmediateResultRequested => _ending.Flags[6];

        public event Action<TacticalCombatUnit, TacticalCombatUnit> SuperlaserFired;
        public event Action<TacticalCombatAttackRun> AttackRunChanged;
        public event Action<TacticalCombatMovieRequest> AttackRunMovieRequested;

        /// <summary>
        /// Creates an encounter from its ordered units, groups, and captured side state.
        /// </summary>
        /// <param name="units">The deployment roster in battle registration order.</param>
        /// <param name="groups">The initial capital and fighter groups.</param>
        /// <param name="attacker">The attacker's command and withdrawal state.</param>
        /// <param name="defender">The defender's command and withdrawal state.</param>
        /// <param name="playerSide">The side controlled by the player.</param>
        /// <param name="battleDepth">The deployment span used by movement and withdrawal.</param>
        /// <param name="random">The encounter's random-number stream.</param>
        /// <param name="createAudio">Creates notification scheduling with this encounter's stream and pause state.</param>
        /// <param name="definition">The shared tactical presentation definitions.</param>
        /// <param name="environment">The planet-specific tactical scenery and texture palette.</param>
        /// <param name="sceneryRandom">The encounter-owned scenery stream, when presentation is available.</param>
        /// <param name="presentationClock">An optional unscaled millisecond clock for encounter replay.</param>
        /// <param name="personnelHostInstanceIDs">The captured host identity for each available embarked officer.</param>
        /// <param name="enteringSide">The force selected by retained space-presence activation for arrival presentation.</param>
        public TacticalCombatSession(
            IReadOnlyList<TacticalCombatUnit> units,
            IReadOnlyList<TacticalCombatGroup> groups,
            TacticalCombatSide attacker,
            TacticalCombatSide defender,
            CombatSide playerSide,
            float battleDepth,
            IRandomNumberProvider random,
            TacticalCombatAudioFactory createAudio,
            TacticalCombatDefinition definition = null,
            TacticalCombatEnvironmentDefinition environment = null,
            ITacticalCombatSceneryRandom sceneryRandom = null,
            Func<uint> presentationClock = null,
            IReadOnlyList<string> personnelHostInstanceIDs = null,
            CombatSide? enteringSide = null
        )
        {
            this._units = new(units ?? throw new ArgumentNullException(nameof(units)));
            this._groups = new(groups ?? throw new ArgumentNullException(nameof(groups)));
            this._random = random ?? throw new ArgumentNullException(nameof(random));
            _requestUnreadyScuttle = RequestUnreadyScuttle;
            _sides = new[]
            {
                attacker ?? throw new ArgumentNullException(nameof(attacker)),
                defender ?? throw new ArgumentNullException(nameof(defender)),
            };
            if (attacker.Side != CombatSide.Attacker || defender.Side != CombatSide.Defender)
                throw new ArgumentException(
                    "The encounter requires attacker and defender side state."
                );
            Definition = definition;
            Environment = environment;
            SceneryRandom = sceneryRandom;
            PlayerSide = playerSide;
            EnteringSide = enteringSide;
            BattleDepth = battleDepth;
            Navigation = new TacticalCombatNavigation(battleDepth);
            string playerFactionID = definition
                ?.Factions.Find(faction => faction.FactionIndex == GetSide(playerSide).FactionIndex)
                ?.FactionInstanceID;
            if (createAudio == null)
                throw new ArgumentNullException(nameof(createAudio));
            Audio =
                createAudio(definition, random, playerFactionID, () => IsPaused, presentationClock)
                ?? throw new InvalidOperationException(
                    "The encounter requires a notification service."
                );
            Units = this._units.AsReadOnly();
            Groups = this._groups.AsReadOnly();
            if (personnelHostInstanceIDs != null)
                foreach (string hostID in personnelHostInstanceIDs)
                {
                    _personnelHostCounts.TryGetValue(hostID, out int count);
                    _personnelHostCounts[hostID] = count + 1;
                }
            foreach (TacticalCombatUnit unit in this._units)
            {
                _unitsByID.Add(unit.InstanceID, unit);
                if (!unit.IsBattleStation)
                {
                    _orderedWithdrawalMembers.Add(new WithdrawalMember(this, unit));
                    _withdrawalUnitsByIndex.Add(unit.Index, unit);
                }
                if (unit.IsBattleStation)
                {
                    if (BattleStation != null)
                        throw new ArgumentException(
                            "An encounter can contain only one battle station.",
                            nameof(units)
                        );
                    BattleStation = new TacticalCombatBattleStation(
                        unit,
                        unit.HasPlanetaryProtection
                    );
                    BattleStation.ChargeCompleted += NotifyStationCharged;
                    Vector3 lane = GetSide(unit.Side).Forward;
                    unit.Position = new Vector3(
                        lane.Z * battleDepth * 0.5f,
                        0,
                        -lane.X * battleDepth * 0.5f
                    );
                }
                unit.CommandBudget = GetSide(unit.Side).GetCommandBudget(unit.IsFighter);
                unit.TurnRate = unit.GetInitialTurnRate();
                unit.Systems.SystemEventRequested += change => QueueSystemChange(unit, change);
                unit.Systems.SoundRequested += Audio.Enqueue;
                unit.Systems.LethalHit += attackerID => ReportDestruction(unit, attackerID);
                unit.Systems.ImpactRequested += (weapon, impact) =>
                    ImpactApplied?.Invoke(unit, weapon, impact);
                if (!unit.IsFighter)
                {
                    TacticalCombatCarrier carrier = new TacticalCombatCarrier(unit);
                    carrier.LaunchRequested += RequestLaunch;
                    _carriers.Add(unit.InstanceID, carrier);
                }
            }
            _orderedWithdrawalMembers.Sort(CompareWithdrawalMembers);
            foreach (TacticalCombatUnit unit in this._units)
                if (
                    unit.Status == TacticalCombatUnitStatus.Docked
                    && unit.CarrierID != null
                    && _carriers.TryGetValue(unit.CarrierID, out TacticalCombatCarrier carrier)
                )
                {
                    carrier.AddDocked(unit, SystemTime, _random);
                    _initiallyDockedFighters.Add(unit);
                }
            _initiallyDockedFighters.Sort(CompareUnits);
            int[] objectCategories = new int[2];
            foreach (TacticalCombatGroup group in this._groups)
            {
                RegisterGroup(group);
                if (group.IsFighterGroup)
                {
                    if (group.FighterCategory is >= 0 and < 4)
                        _fighterGroups[(int)group.Side, group.FighterCategory] = group;
                }
                else if (objectCategories[(int)group.Side] < 8)
                    _objectGroups[(int)group.Side, objectCategories[(int)group.Side]++] = group;
            }
            for (int side = 0; side < _sides.Length; side++)
                _nextGroupID[side] = _sides[side].FactionIndex * 1000000;
            foreach (TacticalCombatUnit unit in this._units)
                _nextGroupID[(int)unit.Side] = Math.Max(_nextGroupID[(int)unit.Side], unit.Index);
            foreach (TacticalCombatGroup group in this._groups)
                _nextGroupID[(int)group.Side] = Math.Max(_nextGroupID[(int)group.Side], group.ID);
            foreach (TacticalCombatUnit unit in this._units)
                if (unit.Index < 1000000)
                {
                    _nextGroupID[(int)unit.Side] += 105;
                    break;
                }
            _spatial = new TacticalCombatSpatialIndex(this._units);
            _targeting = new TacticalCombatTargeting(this);
            _automation = new TacticalCombatPlanner(this, _targeting);
            Selection = new TacticalCombatSelection(this);
            _briefing = new TacticalCombatBriefingSequence(
                GetSide(PlayerSide).FactionIndex,
                messageID => EnqueueFactionNotification(PlayerSide, messageID)
            );
            _ending = new TacticalCombatEnding(
                GetSide(PlayerSide).FactionIndex,
                messageID => EnqueueFactionNotification(PlayerSide, messageID),
                AssignSurvivors,
                Complete
            );
            _fireControl.ShotRequested += RequestShot;
            _fireControl.TractorTargetRequested += RequestTractorTarget;
        }

        /// <summary>
        /// Gets one side's captured command and departure state.
        /// </summary>
        /// <param name="side">The requested encounter side.</param>
        /// <returns>The side state.</returns>
        public TacticalCombatSide GetSide(CombatSide side) => _sides[(int)side];

        /// <summary>
        /// Connects detailed clock phases to the encounter's existing command, system, and movement owners.
        /// </summary>
        private sealed class SchedulerSteps : ITacticalCombatSchedulerSteps
        {
            private readonly TacticalCombatSession _session;
            public bool IsRunning => !_session._completionStarted;
            public bool HasTerminalResult => _session._ending.Flags[6];

            /// <summary>
            /// Borrows the encounter that owns every scheduled operation.
            /// </summary>
            /// <param name="session">The encounter being advanced.</param>
            public SchedulerSteps(TacticalCombatSession session) => _session = session;

            /// <summary>
            /// Applies queued display mutations and their ordered audio refreshes.
            /// </summary>
            public void DrainCommands() => _session.Selection.ProcessPendingCommands();

            /// <summary>
            /// Dispatches the completed progress interval through the encounter command queue.
            /// </summary>
            /// <param name="iteration">The admitted completed interval.</param>
            public void DispatchCommands(uint iteration) =>
                _session.DispatchCommands(unchecked((int)iteration));

            /// <summary>
            /// Advances the ordered side, group, and unit operations for one whole second.
            /// </summary>
            public void AdvanceSystems() => _session.AdvanceSystems();

            /// <summary>
            /// Consumes one terminal second through the ending owner.
            /// </summary>
            /// <returns>Whether the terminal interval has finished.</returns>
            public bool AdvanceEndingTick() => _session._ending.AdvanceTerminalTick();

            /// <summary>
            /// Advances the current movement owners using the separately admitted clock duration.
            /// </summary>
            /// <param name="_">The interval-start timestamp retained by the scheduler callback contract.</param>
            /// <param name="elapsedMilliseconds">The admitted continuous duration.</param>
            public void AdvanceMovement(float _, float elapsedMilliseconds) =>
                _session.AdvanceMovement(elapsedMilliseconds);
        }

        /// <summary>
        /// Identifies a queued mutation without exposing renderer or campaign implementation details.
        /// </summary>
        private enum CommandKind
        {
            DeleteGroup,
            CreateGroup,
            AddGroupMember,
            RemoveGroupMember,
            Order,
            AttackMode,
            Formation,
            Waypoint,
            RemoveWaypoint,
            RemoveTarget,
            Superlaser,
            AttackRun,
            Target,
            Fire,
            Hit,
            System,
            Launch,
            SetRecovery,
            Recover,
            TractorLock,
            TractorUnlock,
            Hyperspace,
            Withdraw,
            Destroy,
            Scuttle,
        }

        /// <summary>
        /// Carries the immutable payload of one queued battle mutation.
        /// </summary>
        private readonly struct Command
        {
            public CommandKind Kind { get; }
            public int Value { get; }
            public Vector3 Position { get; }
            public TacticalCombatUnit Source { get; }
            public TacticalCombatUnit Target { get; }
            public TacticalCombatShot Shot { get; }
            public TacticalSystemEvent Change { get; }
            public TacticalCombatGroup Group { get; }
            public bool Append { get; }
            public TacticalCombatAttackRun Run { get; }
            public TacticalCombatAttackRunState RunState { get; }

            /// <summary>
            /// Creates one queued command payload.
            /// </summary>
            /// <param name="kind">The mutation to apply.</param>
            /// <param name="source">The source or affected unit.</param>
            /// <param name="target">The optional opposing unit or carrier.</param>
            /// <param name="shot">The optional weapon discharge.</param>
            /// <param name="change">The optional system mutation.</param>
            /// <param name="group">The optional group receiving a command.</param>
            /// <param name="append">Whether to extend the group's target list.</param>
            /// <param name="run">The optional fighter attack sequence.</param>
            /// <param name="runState">The requested fighter attack phase.</param>
            /// <param name="value">The requested group behavior.</param>
            /// <param name="position">The requested navigation point.</param>
            public Command(
                CommandKind kind,
                TacticalCombatUnit source,
                TacticalCombatUnit target = null,
                TacticalCombatShot shot = default,
                TacticalSystemEvent change = default,
                TacticalCombatGroup group = null,
                bool append = false,
                TacticalCombatAttackRun run = null,
                TacticalCombatAttackRunState runState = default,
                int value = 0,
                Vector3 position = default
            )
            {
                Kind = kind;
                Value = value;
                Position = position;
                Source = source;
                Target = target;
                Shot = shot;
                Change = change;
                Group = group;
                Append = append;
                Run = run;
                RunState = runState;
            }
        }

        /// <summary>
        /// Reads live unit facts for the ordered withdrawal lifecycle without copying mutable readiness state.
        /// </summary>
        private sealed class WithdrawalMember : ITacticalCombatWithdrawalMember
        {
            private readonly TacticalCombatSession _session;
            public TacticalCombatUnit Unit { get; }
            public int Index => Unit.Index;
            public bool IsActiveMember => IsActiveDisplayObject(Unit);
            public bool IsAwaitingDeparture =>
                Unit.Status
                    is TacticalCombatUnitStatus.EnteringHyperspace
                        or TacticalCombatUnitStatus.AwaitingWithdrawal;

            /// <summary>
            /// Captures the session that owns the member's live support and retained tractor links.
            /// </summary>
            /// <param name="session">The owning local encounter.</param>
            /// <param name="unit">The registered non-station participant.</param>
            public WithdrawalMember(TacticalCombatSession session, TacticalCombatUnit unit)
            {
                _session = session;
                Unit = unit;
            }

            /// <summary>
            /// Reads the current budgets before the lifecycle observes participation state.
            /// </summary>
            /// <returns>Whether both live departure budgets are ordered and nonzero.</returns>
            public bool CheckWithdrawalReadiness() => _session.CheckWithdrawalReadiness(Unit);
        }

        /// <summary>
        /// Tracks the reservation, approach, queued recovery, and completed recovery states.
        /// </summary>
        private sealed class Recovery
        {
            public TacticalCombatCarrier Carrier { get; }
            public int State { get; set; } = 1;

            /// <summary>
            /// Creates a pending carrier assignment.
            /// </summary>
            /// <param name="carrier">The carrier holding the reserved berth.</param>
            public Recovery(TacticalCombatCarrier carrier) => Carrier = carrier;
        }
    }
}
