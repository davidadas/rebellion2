using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
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
        private readonly List<CombatUnit> _units;
        private readonly List<TacticalCombatGroup> _groups;
        private readonly Dictionary<int, TacticalCombatGroup> _groupsByID = new();

        // Each index refers back to one of the groups' ordered member lists.
        private readonly Dictionary<CombatUnit, TacticalCombatGroup> _appliedGroups = new();
        private readonly Dictionary<CombatUnit, TacticalCombatGroup> _requestedGroups = new();
        private readonly TacticalCombatGroup[,] _objectGroups = new TacticalCombatGroup[2, 8];
        private readonly TacticalCombatGroup[,] _fighterGroups = new TacticalCombatGroup[2, 4];
        private readonly Dictionary<string, CombatUnit> _unitsByID = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _personnelHostCounts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TacticalCombatCarrier> _carriers = new(
            StringComparer.Ordinal
        );

        // Index the carriers' recovery assignments by fighter for queued commands and movement.
        private readonly Dictionary<CombatUnit, TacticalCombatCarrier.Recovery> _recoveries = new();
        private readonly HashSet<CombatUnit> _tractorCommandsPending = new();
        private readonly Dictionary<
            CombatUnit,
            SortedDictionary<uint, CombatUnit>
        > _tractorSources = new();
        private readonly List<CombatUnit> _registeredMobileUnits = new();
        private readonly HashSet<CombatUnit> _departed = new();
        private readonly List<CombatUnit> _initiallyDockedFighters = new();
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
        private readonly Action<TacticalCombatSound> _queueCombatSound;
        internal TacticalCombatDamage Damage { get; } = new();
        private readonly int[] _nextGroupID = new int[2];
        private readonly IRandomNumberProvider _random;
        private TacticalCombatScheduler _scheduler;
        private double _elapsedFrameClock;
        private bool _usesFrameClock;
        private readonly TacticalCombatBriefingSequence _briefing;
        private readonly TacticalCombatEnding _ending = new();
        private bool _completionStarted;
        private ITacticalCombatEntry _arrival;
        private bool _arrivalStarting;
        private bool _userPaused;
        private bool _directCommandsSuspended;
        private int _moviePauses;
        private readonly SpaceCombatResult.SideOutcome[] _outcomes =
            new SpaceCombatResult.SideOutcome[2];

        public IReadOnlyList<CombatUnit> Units { get; }
        public IReadOnlyList<TacticalCombatGroup> Groups { get; }
        internal IReadOnlyList<CombatUnit> InitiallyDockedFighters => _initiallyDockedFighters;
        internal bool CanStartBattleStationAttack =>
            CaptureBattleStationAttackReadiness().CanAttackBattleStation;
        public CombatSide PlayerSide { get; }
        public float BattleDepth { get; }
        public TacticalCombatNavigation Navigation { get; }
        public TacticalCombatAudio Audio { get; }
        public TacticalCombatSelection Selection { get; }
        internal TacticalCombatTargeting Targeting => _targeting;
        public int SystemTime =>
            _synchronizationReplica
                ? _synchronizedSystemTime
                : unchecked((int)(_scheduler?.SystemTime ?? 3));
        public bool IsComplete { get; private set; }
        public bool IsArriving => _arrival?.IsComplete == false;
        public SpaceCombatResult.BattleOutcome Winner { get; private set; } =
            SpaceCombatResult.BattleOutcome.Draw;
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
            CombatUnit,
            HardpointWeaponType,
            TacticalCombatImpactKind
        > ImpactApplied;

        /// <summary>
        /// Requests an admitted direct destruction effect before deactivating its participant.
        /// </summary>
        public event Action<CombatUnit> DestructionEffectRequested;
        public event Action<CombatUnit> UnitDestroyed;
        public event Action<CombatUnit> UnitWithdrawn;
        public event Action<CombatUnit, bool> TractorSourcesChanged;
        public event Action<CombatUnit> GravityWellPresentationRequested;
        public event Action<CombatUnit, TacticalSystemEvent> SystemChanged;
        public event Action<CombatUnit> FighterRecovered;
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
        public bool ImmediateResultRequested => _ending.ImmediateResultRequested;

        public event Action<CombatUnit, CombatUnit> SuperlaserFired;
        public event Action<TacticalCombatAttackRun> AttackRunChanged;
        public event Action<TacticalCombatMovieRequest> AttackRunMovieRequested;
        internal event Action<TacticalCombatSide> AutomationRequested;

        /// <summary>
        /// Creates the simulation over the battle's existing combatants.
        /// </summary>
        /// <param name="battle">The battle owning the units to simulate.</param>
        /// <param name="groups">The initial capital and fighter groups.</param>
        /// <param name="attacker">The attacker's command and withdrawal state.</param>
        /// <param name="defender">The defender's command and withdrawal state.</param>
        /// <param name="playerSide">The side controlled by the player.</param>
        /// <param name="battleDepth">The deployment span used by movement and withdrawal.</param>
        /// <param name="random">The encounter's random-number stream.</param>
        /// <param name="attachAutomation">Attaches the selected tactical AI after encounter initialization.</param>
        /// <param name="definition">The shared tactical presentation definitions.</param>
        /// <param name="environment">The planet-specific tactical scenery and texture palette.</param>
        /// <param name="sceneryRandom">The encounter-owned scenery stream, when presentation is available.</param>
        /// <param name="battleClock">The host clock or controlled clock for the battle scheduler.</param>
        /// <param name="personnelHostInstanceIDs">The captured host identity for each available embarked officer.</param>
        public TacticalCombatSession(
            ActiveBattle battle,
            IReadOnlyList<TacticalCombatGroup> groups,
            TacticalCombatSide attacker,
            TacticalCombatSide defender,
            CombatSide playerSide,
            float battleDepth,
            IRandomNumberProvider random,
            Action<TacticalCombatSession> attachAutomation,
            TacticalCombatDefinition definition = null,
            TacticalCombatEnvironmentDefinition environment = null,
            ITacticalCombatSceneryRandom sceneryRandom = null,
            Func<uint> battleClock = null,
            IReadOnlyList<string> personnelHostInstanceIDs = null
        )
            : this(
                (battle ?? throw new ArgumentNullException(nameof(battle)))
                    .Combatants.OrderBy(unit => unit.Index)
                    .ToArray(),
                groups,
                attacker,
                defender,
                playerSide,
                battleDepth,
                random,
                attachAutomation,
                definition,
                environment,
                sceneryRandom,
                personnelHostInstanceIDs: personnelHostInstanceIDs
            )
        {
            if (battleClock != null)
                ConfigureClockedAdvance(battleClock);
        }

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
        /// <param name="attachAutomation">Attaches the selected tactical AI after encounter initialization.</param>
        /// <param name="definition">The shared tactical presentation definitions.</param>
        /// <param name="environment">The planet-specific tactical scenery and texture palette.</param>
        /// <param name="sceneryRandom">The encounter-owned scenery stream, when presentation is available.</param>
        /// <param name="presentationClock">An optional unscaled millisecond clock for encounter replay.</param>
        /// <param name="personnelHostInstanceIDs">The captured host identity for each available embarked officer.</param>
        internal TacticalCombatSession(
            IReadOnlyList<CombatUnit> units,
            IReadOnlyList<TacticalCombatGroup> groups,
            TacticalCombatSide attacker,
            TacticalCombatSide defender,
            CombatSide playerSide,
            float battleDepth,
            IRandomNumberProvider random,
            Action<TacticalCombatSession> attachAutomation,
            TacticalCombatDefinition definition = null,
            TacticalCombatEnvironmentDefinition environment = null,
            ITacticalCombatSceneryRandom sceneryRandom = null,
            Func<uint> presentationClock = null,
            IReadOnlyList<string> personnelHostInstanceIDs = null
        )
        {
            this._units = new(units ?? throw new ArgumentNullException(nameof(units)));
            this._groups = new(groups ?? throw new ArgumentNullException(nameof(groups)));
            this._random = random ?? throw new ArgumentNullException(nameof(random));
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
            BattleDepth = battleDepth;
            Navigation = new TacticalCombatNavigation(battleDepth);
            string playerFactionID = definition
                ?.Factions.Find(faction => faction.FactionIndex == GetSide(playerSide).FactionIndex)
                ?.FactionInstanceID;
            if (attachAutomation == null)
                throw new ArgumentNullException(nameof(attachAutomation));
            Audio = TacticalCombatAudio.Create(
                definition,
                random,
                playerFactionID,
                () => IsPaused,
                presentationClock
            );
            _queueCombatSound = Audio.Enqueue;
            Damage.SystemChangeRequested += QueueSystemChange;
            Damage.LethalHit += ReportDestruction;
            Damage.ImpactApplied += (unit, weapon, impact) =>
                ImpactApplied?.Invoke(unit, weapon, impact);
            Units = this._units.AsReadOnly();
            Groups = this._groups.AsReadOnly();
            if (personnelHostInstanceIDs != null)
                foreach (string hostID in personnelHostInstanceIDs)
                {
                    _personnelHostCounts.TryGetValue(hostID, out int count);
                    _personnelHostCounts[hostID] = count + 1;
                }
            foreach (CombatUnit unit in this._units)
            {
                _unitsByID.Add(unit.InstanceID, unit);
                if (!unit.IsBattleStation)
                    _registeredMobileUnits.Add(unit);
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
                unit.TurnRate = TacticalCombatMotion.GetInitialTurnRate(unit);
                if (!unit.IsFighter)
                {
                    TacticalCombatCarrier carrier = new TacticalCombatCarrier(unit);
                    carrier.LaunchRequested += RequestLaunch;
                    _carriers.Add(unit.InstanceID, carrier);
                }
            }
            _registeredMobileUnits.Sort(CompareUnits);
            for (int index = 1; index < _registeredMobileUnits.Count; index++)
                if (_registeredMobileUnits[index - 1].Index == _registeredMobileUnits[index].Index)
                    throw new ArgumentException(
                        "Tactical units share a registration index.",
                        nameof(units)
                    );
            foreach (CombatUnit unit in this._units)
                if (
                    unit.Status == CombatUnitStatus.Docked
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
            foreach (CombatUnit unit in this._units)
                _nextGroupID[(int)unit.Side] = Math.Max(_nextGroupID[(int)unit.Side], unit.Index);
            foreach (TacticalCombatGroup group in this._groups)
                _nextGroupID[(int)group.Side] = Math.Max(_nextGroupID[(int)group.Side], group.ID);
            foreach (CombatUnit unit in this._units)
                if (unit.Index < 1000000)
                {
                    _nextGroupID[(int)unit.Side] += 105;
                    break;
                }
            _spatial = new TacticalCombatSpatialIndex(this._units);
            _targeting = new TacticalCombatTargeting(this);
            Selection = new TacticalCombatSelection(this);
            _briefing = new TacticalCombatBriefingSequence(
                GetSide(PlayerSide).FactionIndex,
                messageID => EnqueueFactionNotification(PlayerSide, messageID)
            );
            _fireControl.ShotRequested += RequestShot;
            _fireControl.TractorTargetRequested += RequestTractorTarget;
            attachAutomation(this);
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
            public bool HasTerminalResult => _session.ImmediateResultRequested;

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
            public CombatUnit Source { get; }
            public CombatUnit Target { get; }
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
                CombatUnit source,
                CombatUnit target = null,
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
    }
}
