using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Runs an isolated tactical encounter with continuous movement and queued whole-second commands.
    /// Campaign units remain unchanged until the completed encounter is committed.
    /// </summary>
    public sealed class TacticalCombatSession
    {
        private readonly List<TacticalCombatUnit> _units;
        private readonly List<TacticalCombatGroup> _groups;
        private readonly Dictionary<int, TacticalCombatGroup> _groupsByID = new();
        private readonly TacticalCombatGroup[,] _objectGroups = new TacticalCombatGroup[2, 8];
        private readonly TacticalCombatGroup[,] _fighterGroups = new TacticalCombatGroup[2, 4];
        private readonly Dictionary<string, TacticalCombatUnit> _unitsByID = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, TacticalCombatCarrier> _carriers = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<TacticalCombatUnit, Recovery> _recoveries = new();
        private readonly HashSet<TacticalCombatUnit> _tractorCommandsPending = new();
        private HashSet<TacticalCombatUnit> _tractorTargets = new();
        private HashSet<TacticalCombatUnit> _nextTractorTargets = new();
        private readonly HashSet<TacticalCombatUnit> _departed = new();
        private readonly SortedDictionary<int, Queue<Command>> _commands = new();
        private readonly TacticalCombatSpatialIndex _spatial;
        private readonly TacticalCombatFireControl _fireControl = new();
        private readonly Vector3[] _centers = new Vector3[2];
        private readonly HashSet<TacticalCombatGroup> _pendingManeuvers = new();
        private readonly HashSet<TacticalCombatGroup> _pendingAttackOrders = new();
        private readonly List<TacticalCombatGroup> _completedAttackOrders = new();
        private readonly TacticalCombatMotion _motion = new();
        private readonly TacticalCombatSide[] _sides;
        private readonly TacticalCombatAI _automation;
        private readonly int[] _nextGroupID = new int[2];
        private readonly IRandomNumberProvider _random;
        private int _progress;
        private int _phase;
        private float _elapsedInTick;
        private float _completionMilliseconds;
        private bool _completionStarted;
        private bool _userPaused;
        private int _moviePauses;
        private readonly SpaceCombatSideOutcome[] _outcomes = new SpaceCombatSideOutcome[2];

        public IReadOnlyList<TacticalCombatUnit> Units { get; }
        public IReadOnlyList<TacticalCombatGroup> Groups { get; }
        public CombatSide PlayerSide { get; }
        public float BattleDepth { get; }
        public TacticalCombatNavigation Navigation { get; }
        public TacticalCombatAudio Audio { get; }
        public int SystemTime { get; private set; } = 3;
        public bool IsComplete { get; private set; }
        public CombatSide Winner { get; private set; } = CombatSide.Draw;
        public bool IsPaused
        {
            get => _userPaused || _moviePauses > 0;
            set => _userPaused = value;
        }
        public TacticalCombatMovieRequest PendingAttackRunMovie { get; private set; }
        public TacticalCombatSceneryRandom SceneryRandom { get; }
        public TacticalCombatBattleStation BattleStation { get; }
        public Rebellion.Game.SpaceCombat.TacticalCombatDefinition Definition { get; }
        public Rebellion.Game.SpaceCombat.TacticalCombatEnvironmentDefinition Environment { get; }
        public TacticalCombatAttackRun ActiveAttackRun { get; private set; }
        public event Action<TacticalCombatShot> ShotFired;
        public event Action<TacticalCombatShot, TacticalCombatImpactKind> ShotApplied;
        public event Action<TacticalCombatUnit> UnitDestroyed;
        public event Action<TacticalCombatUnit> UnitWithdrawn;
        public event Action<TacticalCombatUnit, TacticalSystemEvent> SystemChanged;
        public event Action<TacticalCombatUnit> FighterRecovered;
        public event Action<TacticalCombatGroup> RecoveryUnavailable;
        public event Action Completed;
        public event Action GroupCategoryChanged;
        public bool IsDirectControlEnabled => !GetSide(PlayerSide).IsAutomated;
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
        /// <param name="definition">The shared tactical presentation definitions.</param>
        /// <param name="environment">The planet-specific tactical scenery and texture palette.</param>
        /// <param name="sceneryRandom">The encounter-owned scenery stream, when presentation is available.</param>
        /// <param name="presentationClock">An optional unscaled millisecond clock for encounter replay.</param>
        public TacticalCombatSession(
            IReadOnlyList<TacticalCombatUnit> units,
            IReadOnlyList<TacticalCombatGroup> groups,
            TacticalCombatSide attacker,
            TacticalCombatSide defender,
            CombatSide playerSide,
            float battleDepth,
            IRandomNumberProvider random,
            Rebellion.Game.SpaceCombat.TacticalCombatDefinition definition = null,
            Rebellion.Game.SpaceCombat.TacticalCombatEnvironmentDefinition environment = null,
            TacticalCombatSceneryRandom sceneryRandom = null,
            Func<uint> presentationClock = null
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
            Audio = new TacticalCombatAudio(
                definition?.Sounds ?? new List<TacticalCombatSoundDefinition>(),
                random,
                definition?.Notifications,
                playerFactionID,
                () => IsPaused,
                presentationClock
            );
            Units = this._units.AsReadOnly();
            Groups = this._groups.AsReadOnly();
            foreach (TacticalCombatUnit unit in this._units)
            {
                _unitsByID.Add(unit.InstanceID, unit);
                if (unit.IsBattleStation)
                {
                    if (BattleStation != null)
                        throw new ArgumentException(
                            "An encounter can contain only one battle station.",
                            nameof(units)
                        );
                    Planet planet = unit.Source.GetParentOfType<Planet>();
                    bool protectedByFacility = false;
                    if (planet != null)
                        foreach (Building facility in planet.GetChildren<Building>())
                            protectedByFacility |=
                                facility.ManufacturingStatus == ManufacturingStatus.Complete
                                && facility.Movement == null
                                && facility.ProtectedUnitTypeIDs?.Contains(unit.TypeID) == true;
                    BattleStation = new TacticalCombatBattleStation(unit, protectedByFacility);
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
                if (!unit.IsFighter)
                {
                    TacticalCombatCarrier carrier = new TacticalCombatCarrier(unit);
                    carrier.LaunchRequested += RequestLaunch;
                    _carriers.Add(unit.InstanceID, carrier);
                }
            }
            foreach (TacticalCombatUnit unit in this._units)
                if (
                    unit.Status == TacticalCombatUnitStatus.Docked
                    && unit.CarrierID != null
                    && _carriers.TryGetValue(unit.CarrierID, out TacticalCombatCarrier carrier)
                )
                    carrier.AddDocked(unit);
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
            _automation = new TacticalCombatAI(this);
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
        /// Gets the requested command group currently occupying an object category.
        /// </summary>
        /// <param name="side">The category's owning side.</param>
        /// <param name="category">The zero-based category from zero through seven.</param>
        /// <returns>The current owner, or null for an empty category.</returns>
        public TacticalCombatGroup GetObjectGroup(CombatSide side, int category) =>
            _objectGroups[(int)side, category];

        /// <summary>
        /// Gets the deployed fighter group currently occupying a fighter category.
        /// </summary>
        /// <param name="side">The category's owning side.</param>
        /// <param name="category">The zero-based category from zero through three.</param>
        /// <returns>The current owner, or null for an empty category.</returns>
        public TacticalCombatGroup GetFighterGroup(CombatSide side, int category) =>
            _fighterGroups[(int)side, category];

        /// <summary>
        /// Assigns a requested command group to a reusable object category.
        /// </summary>
        /// <param name="side">The category's owning side.</param>
        /// <param name="category">The zero-based category from zero through seven.</param>
        /// <param name="group">The new owner, or null to clear the category.</param>
        public void SetObjectGroup(CombatSide side, int category, TacticalCombatGroup group)
        {
            if (group != null)
            {
                ValidateGroup(group);
                if (group.Side != side || group.IsFighterGroup)
                    throw new ArgumentException(
                        "An object category requires a command group of the same side.",
                        nameof(group)
                    );
            }
            _objectGroups[(int)side, category] = group;
            GroupCategoryChanged?.Invoke();
        }

        /// <summary>
        /// Changes a side's command authority while retaining its current applied group orders.
        /// </summary>
        /// <param name="side">The controlled side.</param>
        /// <param name="automated">Whether tactical automation should issue its orders.</param>
        public void SetAutomated(CombatSide side, bool automated)
        {
            GetSide(side).IsAutomated = automated;
            foreach (TacticalCombatGroup group in _groupsByID.Values)
                if (group.Side == side)
                    group.IsAutomated = automated;
        }

        /// <summary>
        /// Clears category ownership only when it still names the group being removed.
        /// </summary>
        /// <param name="group">The previous category owner.</param>
        private void ClearGroupCategories(TacticalCombatGroup group)
        {
            bool changed = false;
            for (int category = 0; category < 8; category++)
                if (ReferenceEquals(_objectGroups[(int)group.Side, category], group))
                {
                    _objectGroups[(int)group.Side, category] = null;
                    changed = true;
                    break;
                }
            for (int category = 0; category < 4; category++)
                if (ReferenceEquals(_fighterGroups[(int)group.Side, category], group))
                {
                    _fighterGroups[(int)group.Side, category] = null;
                    changed = true;
                    break;
                }
            if (changed)
                GroupCategoryChanged?.Invoke();
        }

        /// <summary>
        /// Gets a tactical unit by its campaign instance identifier.
        /// </summary>
        /// <param name="instanceID">The campaign unit identifier.</param>
        /// <returns>The unit, or null when it is outside this encounter.</returns>
        public TacticalCombatUnit GetUnit(string instanceID) =>
            instanceID != null && _unitsByID.TryGetValue(instanceID, out TacticalCombatUnit unit)
                ? unit
                : null;

        /// <summary>
        /// Gets the active group containing a tactical unit.
        /// </summary>
        /// <param name="unit">The group's member.</param>
        /// <returns>The member's group, or null after removal.</returns>
        public TacticalCombatGroup GetGroup(TacticalCombatUnit unit) =>
            unit.GroupID is int groupID
            && _groupsByID.TryGetValue(groupID, out TacticalCombatGroup group)
                ? group
                : null;

        /// <summary>
        /// Gets the latest requested owner before queued membership changes apply.
        /// </summary>
        /// <param name="unit">The selected unit.</param>
        /// <returns>The requested group, or null after requested removal.</returns>
        public TacticalCombatGroup GetRequestedGroup(TacticalCombatUnit unit) =>
            unit.RequestedGroupID is int groupID
            && _groupsByID.TryGetValue(groupID, out TacticalCombatGroup group)
                ? group
                : null;

        /// <summary>
        /// Requests an ordered group while retaining prior applied ownership until command dispatch.
        /// </summary>
        /// <param name="members">The ordered members of one side.</param>
        /// <returns>The newly registered tactical group.</returns>
        public TacticalCombatGroup CreateGroup(IReadOnlyList<TacticalCombatUnit> members)
        {
            if (members == null || members.Count == 0)
                throw new ArgumentException(
                    "A tactical group requires at least one member.",
                    nameof(members)
                );
            CombatSide side = members[0].Side;
            foreach (TacticalCombatUnit member in members)
                if (
                    member.Side != side
                    || !_unitsByID.TryGetValue(member.InstanceID, out TacticalCombatUnit registered)
                    || !ReferenceEquals(member, registered)
                )
                    throw new ArgumentException(
                        "A tactical group requires registered members of one side.",
                        nameof(members)
                    );
            if (new HashSet<TacticalCombatUnit>(members).Count != members.Count)
                throw new ArgumentException(
                    "A tactical group cannot repeat a member.",
                    nameof(members)
                );
            TacticalCombatUnit first = members[0];
            RequestMemberRemoval(first);
            TacticalCombatGroup group = new(
                ++_nextGroupID[(int)side],
                first,
                GetSide(side).Forward
            );
            RegisterGroup(group);
            QueueCommand(new Command(CommandKind.CreateGroup, first, group: group));
            for (int index = 1; index < members.Count; index++)
                AddGroupMember(group, members[index]);
            return group;
        }

        /// <summary>
        /// Requests a member transfer while preserving its current formation until dispatch.
        /// </summary>
        /// <param name="group">The requested destination group.</param>
        /// <param name="unit">The transferring unit.</param>
        public void AddGroupMember(TacticalCombatGroup group, TacticalCombatUnit unit)
        {
            ValidateGroup(group);
            if (
                unit == null
                || unit.Side != group.Side
                || !_unitsByID.TryGetValue(unit.InstanceID, out TacticalCombatUnit registered)
                || !ReferenceEquals(registered, unit)
            )
                throw new ArgumentException(
                    "A member must belong to this encounter and side.",
                    nameof(unit)
                );
            RequestMemberRemoval(unit);
            group.AddRequestedMember(unit);
            QueueCommand(new Command(CommandKind.AddGroupMember, unit, group: group));
        }

        /// <summary>
        /// Requests removal from one group without changing a later requested owner.
        /// </summary>
        /// <param name="group">The group losing the member.</param>
        /// <param name="unit">The requested member to remove.</param>
        public void RemoveGroupMember(TacticalCombatGroup group, TacticalCombatUnit unit)
        {
            ValidateGroup(group);
            if (
                unit == null
                || !_unitsByID.TryGetValue(unit.InstanceID, out TacticalCombatUnit registered)
                || !ReferenceEquals(registered, unit)
            )
                throw new ArgumentException(
                    "The member does not belong to this encounter.",
                    nameof(unit)
                );
            group.RemoveRequestedMember(unit);
            if (group.RequestedMembers.Count == 0)
                ClearGroupCategories(group);
            QueueCommand(new Command(CommandKind.RemoveGroupMember, unit, group: group));
        }

        /// <summary>
        /// Removes requested membership and queues the corresponding active-roster removal.
        /// </summary>
        /// <param name="unit">The member leaving its requested group.</param>
        private void RequestMemberRemoval(TacticalCombatUnit unit)
        {
            TacticalCombatGroup previous = GetRequestedGroup(unit);
            if (previous == null)
                return;
            RemoveGroupMember(previous, unit);
        }

        /// <summary>
        /// Queues a designated target command for a registered group.
        /// </summary>
        /// <param name="group">The attacking group.</param>
        /// <param name="target">The designated encounter unit.</param>
        /// <param name="append">Whether to append to the existing target list.</param>
        public void SetTarget(
            TacticalCombatGroup group,
            TacticalCombatUnit target,
            bool append = false
        )
        {
            ValidateGroup(group);
            if (
                target == null
                || !_unitsByID.TryGetValue(target.InstanceID, out TacticalCombatUnit registered)
                || !ReferenceEquals(target, registered)
            )
                throw new ArgumentException(
                    "The target does not belong to this encounter.",
                    nameof(target)
                );
            group.RequestTarget(target, append);
            QueueCommand(
                new Command(CommandKind.Target, null, target, group: group, append: append)
            );
        }

        /// <summary>
        /// Checks withdrawal eligibility against the live battle's gravity wells and movement systems.
        /// </summary>
        /// <param name="side">The side requesting withdrawal.</param>
        /// <returns>Whether a legal route and at least one mobile hyperdrive unit remain.</returns>
        public bool CanWithdraw(CombatSide side)
        {
            TacticalCombatSide state = GetSide(side);
            if (!state.HasWithdrawalDestination || state.IsWithdrawing || _completionStarted)
                return false;
            bool mobile = false;
            for (int index = 0; index < _units.Count; index++)
            {
                TacticalCombatUnit unit = _units[index];
                if (unit.IsBattleStation && unit.Side == side && BattleStation.IsOperational)
                    mobile = true;
                if (!unit.IsTargetable)
                    continue;
                if (unit.Side != side && unit.HasGravityWell)
                    return false;
                if (
                    unit.Side == side
                    && (unit.IsBattleStation || unit.Systems.HasHyperdrive && unit.GetSpeed() > 0)
                )
                    mobile = true;
            }
            return mobile;
        }

        /// <summary>
        /// Checks station protection, attack availability, and current squadron superiority.
        /// </summary>
        /// <param name="side">The side requesting a fighter attack.</param>
        /// <returns>Whether a battle-station attack is currently available.</returns>
        public bool CanAttackBattleStation(CombatSide side)
        {
            if (
                BattleStation?.CanReceiveAttackRun != true
                || BattleStation.Unit.Side == side
                || GetSide(side).FactionIndex != 0
                || GetSide(side).IsWithdrawing
            )
                return false;
            foreach (TacticalCombatGroup group in _groups)
                if (
                    group.Order == TacticalCombatOrder.AttackBattleStation
                    || group.ActiveOrder == TacticalCombatOrder.AttackBattleStation
                )
                    return false;
            int own = 0;
            int opposing = 0;
            foreach (TacticalCombatUnit unit in _units)
                if (unit.IsFighter && unit.IsEngaged)
                {
                    if (unit.Side == side)
                        own++;
                    else
                        opposing++;
                }
            return own > opposing;
        }

        /// <summary>
        /// Selects an opposing unit for a charged station's delayed superlaser attack.
        /// </summary>
        /// <param name="target">The opposing tactical unit.</param>
        /// <returns>Whether a superlaser attack was queued.</returns>
        public bool FireSuperlaser(TacticalCombatUnit target)
        {
            if (
                BattleStation == null
                || target?.IsTargetable != true
                || target.Side == BattleStation.Unit.Side
                || GetSide(BattleStation.Unit.Side).IsWithdrawing
                || !BattleStation.TryFire()
            )
                return false;
            QueueCommand(new Command(CommandKind.Superlaser, BattleStation.Unit, target));
            QueueCommand(new Command(CommandKind.Scuttle, target), 1);
            return true;
        }

        /// <summary>
        /// Starts the side's departure countdown and assigns withdrawal or recovery orders.
        /// </summary>
        /// <param name="side">The side leaving the encounter.</param>
        /// <returns>Whether withdrawal was started.</returns>
        public bool BeginWithdrawal(CombatSide side)
        {
            if (!CanWithdraw(side))
                return false;
            TacticalCombatSide state = GetSide(side);
            state.IsWithdrawing = true;
            bool station = false;
            foreach (TacticalCombatUnit unit in _units)
                station |= unit.Side == side && unit.IsBattleStation && BattleStation.IsOperational;
            state.WithdrawalMilliseconds = Math.Max(
                state.InitialWithdrawalMilliseconds,
                station ? 150000 : 30000
            );
            foreach (TacticalCombatGroup group in _groups)
                if (group.Side == side && group.Members.Count > 0)
                    SetOrder(
                        group,
                        group.IsFighterGroup && !group.Members[0].Systems.HasHyperdrive
                            ? TacticalCombatOrder.Recover
                            : TacticalCombatOrder.Withdraw
                    );
            return true;
        }

        /// <summary>
        /// Changes one group's behavior while releasing recovery reservations owned by its previous order.
        /// </summary>
        /// <param name="group">The group receiving the command.</param>
        /// <param name="order">The requested behavior.</param>
        public void SetOrder(TacticalCombatGroup group, TacticalCombatOrder order)
        {
            if (
                !_groupsByID.TryGetValue(group.ID, out TacticalCombatGroup existing)
                || !ReferenceEquals(existing, group)
            )
                throw new ArgumentException("The group is outside this encounter.", nameof(group));
            if (
                order == TacticalCombatOrder.AttackBattleStation
                && (!group.IsFighterGroup || !CanAttackBattleStation(group.Side))
            )
                throw new InvalidOperationException(
                    "A battle-station attack is unavailable for this group."
                );
            group.SetOrder(order);
        }

        /// <summary>
        /// Applies a delayed group behavior and its recovery or attack-run lifecycle changes.
        /// </summary>
        /// <param name="group">The registered group.</param>
        /// <param name="order">The due behavior.</param>
        private void ApplyOrder(TacticalCombatGroup group, TacticalCombatOrder order)
        {
            if (
                group.AttackRun?.IsInProgress == true
                && order != TacticalCombatOrder.AttackBattleStation
            )
                group.AttackRun.ApplyState(TacticalCombatAttackRunState.Cancelled);
            if (group.ActiveOrder == TacticalCombatOrder.Recover)
                foreach (TacticalCombatUnit unit in group.Members)
                    ReleaseRecovery(unit);
            group.ApplyOrder(order);
            if (
                group.IsFighterGroup
                && order is TacticalCombatOrder.Recover or TacticalCombatOrder.AttackBattleStation
            )
                ClearGroupCategories(group);
            _pendingManeuvers.Remove(group);
            _pendingAttackOrders.Remove(group);
            if (
                order
                is TacticalCombatOrder.AttackFighters
                    or TacticalCombatOrder.AttackCapitalShips
            )
                _pendingAttackOrders.Add(group);
            if (
                order
                is TacticalCombatOrder.LeftHook
                    or TacticalCombatOrder.RightHook
                    or TacticalCombatOrder.Hammer
                    or TacticalCombatOrder.Anvil
            )
                _pendingManeuvers.Add(group);
            if (order == TacticalCombatOrder.AttackBattleStation)
            {
                TacticalCombatAttackRun run = new(
                    group,
                    _random,
                    () => GetSide(group.Side).GetCommandBudget(true)
                );
                run.StateChangeRequested += RequestAttackRunState;
                run.MovieRequested += RequestAttackRunMovie;
                run.UnitDestroyed += Destroy;
                run.UnitParticipationChanged += _spatial.Update;
                group.AttackRun = run;
                ActiveAttackRun = run;
                AttackRunChanged?.Invoke(run);
            }
        }

        /// <summary>
        /// Queues the next phase of a fighter group's staged station attack.
        /// </summary>
        /// <param name="run">The group's active attack sequence.</param>
        /// <param name="state">The requested phase.</param>
        private void RequestAttackRunState(
            TacticalCombatAttackRun run,
            TacticalCombatAttackRunState state
        ) => QueueCommand(new Command(CommandKind.AttackRun, null, run: run, runState: state));

        /// <summary>
        /// Applies the station outcome before beginning its modal movie pause.
        /// </summary>
        /// <param name="run">The attack whose conclusion is launching a movie.</param>
        private void RequestAttackRunMovie(TacticalCombatAttackRun run)
        {
            if (run.State == TacticalCombatAttackRunState.Succeeded && BattleStation != null)
                Destroy(BattleStation.Unit);
            _moviePauses++;
            TacticalCombatMovieRequest request = new(run, CompleteAttackRunMovie);
            PendingAttackRunMovie = request;
            bool notificationsEnabled = Audio.Notifications.Enabled;
            Audio.Notifications.Enabled = false;
            try
            {
                AttackRunMovieRequested?.Invoke(request);
            }
            finally
            {
                Audio.Notifications.Enabled = notificationsEnabled;
            }
        }

        /// <summary>
        /// Releases only the pause owned by the movie that completed or was cancelled.
        /// </summary>
        /// <param name="request">The completed movie's ownership token.</param>
        private void CompleteAttackRunMovie(TacticalCombatMovieRequest request)
        {
            _moviePauses = Math.Max(0, _moviePauses - 1);
            if (ReferenceEquals(PendingAttackRunMovie, request))
                PendingAttackRunMovie = null;
        }

        /// <summary>
        /// Advances one presentation frame without tying movement to whole-second weapon updates.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed unpaused frame duration.</param>
        public void Advance(float elapsedMilliseconds)
        {
            Audio.Update();
            if (IsComplete || elapsedMilliseconds <= 0)
                return;
            _elapsedInTick += elapsedMilliseconds;
            if (IsPaused)
                return;
            if (_elapsedInTick >= 1000)
            {
                _elapsedInTick %= 1000;
                if (_phase != 5)
                    elapsedMilliseconds = 0;
                _progress++;
                _phase = 0;
            }
            if (_phase == 0)
            {
                if (_progress >= 3)
                    DispatchCommands(_progress - 1);
                _phase = 1;
            }
            else if (_phase == 1)
            {
                if (!_completionStarted)
                    AdvanceSystems();
                else
                {
                    _completionMilliseconds -= 1000;
                    if (_completionMilliseconds <= 0)
                        Complete();
                }
                SystemTime++;
                _phase = 5;
            }
            if (_progress >= 3 && !_completionStarted && elapsedMilliseconds > 0)
                AdvanceMovement(elapsedMilliseconds);
        }

        /// <summary>
        /// Registers group lifecycle callbacks used by queued withdrawal and recovery transitions.
        /// </summary>
        /// <param name="group">The new encounter group.</param>
        private void RegisterGroup(TacticalCombatGroup group)
        {
            _groupsByID.Add(group.ID, group);
            group.IsAutomated = GetSide(group.Side).IsAutomated;
            group.HyperspaceRequested += RequestHyperspace;
            group.OrderRequested += RequestOrder;
            group.AttackModeRequested += QueueAttackMode;
        }

        /// <summary>
        /// Removes both ownership lanes when a unit stops participating in the battle.
        /// </summary>
        /// <param name="unit">The destroyed, departing, or recovering member.</param>
        private void RemoveFromGroup(TacticalCombatUnit unit)
        {
            TacticalCombatGroup requested = GetRequestedGroup(unit);
            requested?.RemoveRequestedMember(unit);
            TacticalCombatGroup applied = GetGroup(unit);
            if (applied != null)
                RemoveAppliedMember(applied, unit);
            if (requested?.Members.Count == 0 && requested.RequestedMembers.Count == 0)
                DeregisterGroup(requested);
        }

        /// <summary>
        /// Applies membership removal and deletes the group after its final active member leaves.
        /// </summary>
        /// <param name="group">The previous applied owner.</param>
        /// <param name="unit">The departing member.</param>
        private void RemoveAppliedMember(TacticalCombatGroup group, TacticalCombatUnit unit)
        {
            group.RemoveMember(unit);
            if (group.Members.Count == 0)
                DeregisterGroup(group);
        }

        /// <summary>
        /// Releases a group's registry, requested ownership, and lifecycle subscriptions.
        /// </summary>
        /// <param name="group">The group whose active roster is empty.</param>
        private void DeregisterGroup(TacticalCombatGroup group)
        {
            ClearGroupCategories(group);
            while (group.RequestedMembers.Count != 0)
                group.RemoveRequestedMember(group.RequestedMembers[0]);
            group.HyperspaceRequested -= RequestHyperspace;
            group.OrderRequested -= RequestOrder;
            group.AttackModeRequested -= QueueAttackMode;
            if (group.AttackRun != null)
            {
                group.AttackRun.StateChangeRequested -= RequestAttackRunState;
                group.AttackRun.MovieRequested -= RequestAttackRunMovie;
                group.AttackRun.UnitDestroyed -= Destroy;
                group.AttackRun.UnitParticipationChanged -= _spatial.Update;
            }
            _pendingManeuvers.Remove(group);
            _pendingAttackOrders.Remove(group);
            _completedAttackOrders.Remove(group);
            _groupsByID.Remove(group.ID);
            _groups.Remove(group);
        }

        /// <summary>
        /// Queues a behavior requested by a registered group's label change.
        /// </summary>
        /// <param name="group">The requesting group.</param>
        /// <param name="order">The requested behavior.</param>
        private void RequestOrder(TacticalCombatGroup group, TacticalCombatOrder order)
        {
            if (
                group.IsFighterGroup
                && order is TacticalCombatOrder.Recover or TacticalCombatOrder.AttackBattleStation
            )
                ClearGroupCategories(group);
            QueueCommand(new Command(CommandKind.Order, null, group: group, value: (int)order));
        }

        /// <summary>
        /// Queues a requested attack-mode change through the battle command stream.
        /// </summary>
        /// <param name="group">The registered group.</param>
        /// <param name="closeAttack">Whether to close with the target.</param>
        private void QueueAttackMode(TacticalCombatGroup group, bool closeAttack) =>
            QueueCommand(
                new Command(CommandKind.AttackMode, null, group: group, value: closeAttack ? 1 : 0)
            );

        /// <summary>
        /// Queues a formation change for a registered capital-ship group.
        /// </summary>
        /// <param name="group">The group receiving the formation.</param>
        /// <param name="formation">The requested formation.</param>
        public void SetFormation(TacticalCombatGroup group, TacticalCombatFormationKind formation)
        {
            if (
                group == null
                || !_groupsByID.TryGetValue(group.ID, out TacticalCombatGroup registered)
                || !ReferenceEquals(group, registered)
            )
                throw new ArgumentException(
                    "The group does not belong to this encounter.",
                    nameof(group)
                );
            if (group.IsFighterGroup || formation == TacticalCombatFormationKind.Direct)
                throw new ArgumentException(
                    "A selectable formation requires a capital-ship group.",
                    nameof(group)
                );
            group.RequestFormation(formation);
            QueueCommand(
                new Command(CommandKind.Formation, null, group: group, value: (int)formation)
            );
        }

        /// <summary>
        /// Queues a navigation-list change for the battle's delayed command phase.
        /// </summary>
        /// <param name="group">The moving group.</param>
        /// <param name="position">The selected navigation point.</param>
        /// <param name="append">Whether to extend the route.</param>
        public void SetWaypoint(TacticalCombatGroup group, Vector3 position, bool append = false)
        {
            ValidateGroup(group);
            group.RequestWaypoint(position, append);
            QueueCommand(
                new Command(
                    CommandKind.Waypoint,
                    null,
                    group: group,
                    append: append,
                    position: position
                )
            );
        }

        /// <summary>
        /// Removes a requested target and queues removal from the active target list.
        /// </summary>
        /// <param name="group">The attacking group.</param>
        /// <param name="target">The target to remove.</param>
        public void RemoveTarget(TacticalCombatGroup group, TacticalCombatUnit target)
        {
            ValidateGroup(group);
            group.RemoveTarget(target, true);
            QueueCommand(new Command(CommandKind.RemoveTarget, null, target, group: group));
        }

        /// <summary>
        /// Removes a requested waypoint and queues removal from the active route.
        /// </summary>
        /// <param name="group">The moving group.</param>
        /// <param name="position">The waypoint to remove.</param>
        public void RemoveWaypoint(TacticalCombatGroup group, Vector3 position)
        {
            ValidateGroup(group);
            group.RemoveWaypoint(position, true);
            QueueCommand(
                new Command(CommandKind.RemoveWaypoint, null, group: group, position: position)
            );
        }

        /// <summary>
        /// Rejects commands for a group owned by another encounter or already removed.
        /// </summary>
        /// <param name="group">The commanded group.</param>
        private void ValidateGroup(TacticalCombatGroup group)
        {
            if (
                group == null
                || !_groupsByID.TryGetValue(group.ID, out TacticalCombatGroup registered)
                || !ReferenceEquals(registered, group)
            )
                throw new ArgumentException(
                    "The group does not belong to this encounter.",
                    nameof(group)
                );
        }

        /// <summary>
        /// Applies due commands in their stable insertion order, including commands generated during dispatch.
        /// </summary>
        /// <param name="dueTime">The inclusive command time.</param>
        private void DispatchCommands(int dueTime)
        {
            while (_commands.Count > 0)
            {
                using SortedDictionary<int, Queue<Command>>.Enumerator entries =
                    _commands.GetEnumerator();
                entries.MoveNext();
                int time = entries.Current.Key;
                if (time > dueTime)
                    break;
                Queue<Command> bucket = entries.Current.Value;
                _commands.Remove(time);
                while (bucket.Count > 0)
                    ApplyCommand(bucket.Dequeue());
            }
        }

        /// <summary>
        /// Enqueues a typed battle mutation at the requested whole-second command time.
        /// </summary>
        /// <param name="command">The requested mutation.</param>
        /// <param name="delay">The additional command delay.</param>
        private void QueueCommand(Command command, int delay = 0)
        {
            int time = SystemTime + delay;
            if (!_commands.TryGetValue(time, out Queue<Command> bucket))
            {
                bucket = new();
                _commands.Add(time, bucket);
            }
            bucket.Enqueue(command);
        }

        /// <summary>
        /// Queues a discharge animation and its separately delayed damage command.
        /// </summary>
        /// <param name="shot">The selected battery discharge.</param>
        private void RequestShot(TacticalCombatShot shot)
        {
            QueueCommand(
                new Command(CommandKind.Fire, shot.Source, shot.Target, shot),
                shot.FiringDelay
            );
            QueueCommand(
                new Command(CommandKind.Hit, shot.Source, shot.Target, shot),
                shot.ImpactDelay
            );
        }

        /// <summary>
        /// Queues a component mutation raised by hull damage, ion damage, or damage control.
        /// </summary>
        /// <param name="unit">The affected unit.</param>
        /// <param name="change">The requested mutation.</param>
        private void QueueSystemChange(TacticalCombatUnit unit, TacticalSystemEvent change) =>
            QueueCommand(new Command(CommandKind.System, unit, null, default, change));

        /// <summary>
        /// Queues a launch request without changing a docked fighter during carrier enumeration.
        /// </summary>
        /// <param name="carrier">The launching carrier.</param>
        /// <param name="fighter">The next docked squadron.</param>
        private void RequestLaunch(TacticalCombatCarrier carrier, TacticalCombatUnit fighter) =>
            QueueCommand(new Command(CommandKind.Launch, carrier.Unit, fighter));

        /// <summary>
        /// Queues the transition from departure preparation into the hyperspace animation.
        /// </summary>
        /// <param name="unit">The departing unit.</param>
        private void RequestHyperspace(TacticalCombatUnit unit) =>
            QueueCommand(new Command(CommandKind.Hyperspace, unit));

        /// <summary>
        /// Requests a tractor lock or unlock while allowing only one unresolved tractor command per ship.
        /// </summary>
        /// <param name="unit">The tractor source.</param>
        /// <param name="target">The designated primary target.</param>
        private void RequestTractorTarget(TacticalCombatUnit unit, TacticalCombatUnit target)
        {
            if (_tractorCommandsPending.Contains(unit) || unit.TractorRange == 0)
                return;
            bool valid =
                target?.IsTargetable == true
                && unit.Systems.GetTractorBudget() > 0
                && TacticalCombatGeometry.GetLength(target.Position - unit.Position)
                    <= unit.TractorRange;
            if (unit.TractorTargetID != null)
            {
                if (valid && target.InstanceID == unit.TractorTargetID)
                    return;
                _tractorCommandsPending.Add(unit);
                QueueCommand(
                    new Command(CommandKind.TractorUnlock, unit, GetUnit(unit.TractorTargetID))
                );
            }
            else if (valid)
            {
                _tractorCommandsPending.Add(unit);
                QueueCommand(new Command(CommandKind.TractorLock, unit, target));
            }
        }

        /// <summary>
        /// Advances side countdowns, carrier operations, formation assessments, and unit systems once per second.
        /// </summary>
        private void AdvanceSystems()
        {
            BattleStation?.Advance(1000);
            TacticalCombatSide first = GetSide(PlayerSide);
            TacticalCombatSide second = GetSide(
                PlayerSide == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker
            );
            AdvanceWithdrawal(first);
            _automation.Advance(first);
            AdvanceWithdrawal(second);
            _automation.Advance(second);
            foreach (TacticalCombatGroup group in _groups)
            {
                group.Refresh(_units, SystemTime);
                if (group.ActiveOrder == TacticalCombatOrder.Recover)
                    AdvanceRecovery(group);
                group.AttackRun?.Advance(1000);
            }
            foreach (TacticalCombatUnit unit in _units)
            {
                unit.CommandBudget = GetSide(unit.Side).GetCommandBudget(unit.IsFighter);
                if (unit.IsBattleStation)
                {
                    _carriers[unit.InstanceID].Advance(SystemTime, _random);
                    continue;
                }
                if (!unit.IsTargetable)
                    continue;
                unit.Systems.Advance(1000, _random);
                unit.Weapons.Advance(1, unit.Systems.GetWeaponRecharge());
                if (_carriers.TryGetValue(unit.InstanceID, out TacticalCombatCarrier carrier))
                    carrier.Advance(SystemTime, _random);
                TacticalCombatGroup group = GetGroup(unit);
                if (group != null)
                    _fireControl.AcquireTargets(unit, group, _spatial, _random);
            }
        }

        /// <summary>
        /// Queues one preferred category target when an explicit attack behavior first finds an eligible candidate.
        /// </summary>
        private void AdvanceAttackOrders()
        {
            _completedAttackOrders.Clear();
            foreach (TacticalCombatGroup group in _pendingAttackOrders)
            {
                TacticalCombatUnit target = _automation.SelectBehaviorTarget(group);
                if (target == null)
                    continue;
                SetTarget(group, target);
                _completedAttackOrders.Add(group);
            }
            foreach (TacticalCombatGroup group in _completedAttackOrders)
                _pendingAttackOrders.Remove(group);
        }

        /// <summary>
        /// Resolves each newly activated maneuver once using the preceding frame's opposing center.
        /// </summary>
        private void AdvanceManeuvers()
        {
            foreach (TacticalCombatGroup group in _pendingManeuvers)
                if (
                    Navigation.TryGetManeuver(
                        group,
                        _units,
                        _centers[1 - (int)group.Side],
                        out Vector3 point
                    )
                )
                    SetWaypoint(group, point);
                else
                    group.SetOrder(TacticalCombatOrder.None);
            _pendingManeuvers.Clear();
        }

        /// <summary>
        /// Captures active side centers in battle registration order for the following frame's maneuvers.
        /// </summary>
        private void UpdateCenters()
        {
            _centers[0] = _centers[1] = Vector3.Zero;
            int firstCount = 0;
            int secondCount = 0;
            foreach (TacticalCombatUnit unit in _units)
            {
                if (!unit.IsEngaged || unit.IsBattleStation)
                    continue;
                if (unit.Side == CombatSide.Attacker)
                    firstCount++;
                else
                    secondCount++;
                if (unit.IsTargetable)
                    _centers[(int)unit.Side] += unit.Position;
            }
            _centers[0] *= firstCount == 0 ? 0 : 1f / firstCount;
            _centers[1] *= secondCount == 0 ? 0 : 1f / secondCount;
        }

        /// <summary>
        /// Advances formations, unit movement, and four-second hyperspace animations.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed movement interval.</param>
        private void AdvanceMovement(float elapsedMilliseconds)
        {
            AdvanceAttackOrders();
            AdvanceManeuvers();
            foreach (TacticalCombatGroup group in _groups)
                group.Advance(
                    elapsedMilliseconds,
                    BattleDepth,
                    GetSide(group.Side).WithdrawalMilliseconds,
                    _groupsByID
                );
            foreach (TacticalCombatUnit unit in _units)
            {
                if (unit.Status == TacticalCombatUnitStatus.EnteringHyperspace)
                {
                    if (
                        unit.HyperspaceMilliseconds == 0
                        || float.IsNaN(unit.HyperspaceMilliseconds)
                    )
                        Audio.EnqueueNotification(unit.IsFighter ? 29 : 31);
                    unit.HyperspaceMilliseconds += elapsedMilliseconds;
                    if (unit.HyperspaceMilliseconds >= 4000)
                    {
                        unit.Status = TacticalCombatUnitStatus.AwaitingWithdrawal;
                        QueueCommand(new Command(CommandKind.Withdraw, unit));
                    }
                    continue;
                }
                TacticalCombatGroup group = GetGroup(unit);
                if (group != null)
                    _motion.Advance(
                        unit,
                        group,
                        GetRecoveryCarrier(unit)?.Unit,
                        BattleDepth,
                        _spatial,
                        elapsedMilliseconds
                    );
            }
            CheckCompletion();
            UpdateCenters();
        }

        /// <summary>
        /// Counts down a departing side and scuttles units that cannot leave after its mobile units depart.
        /// </summary>
        /// <param name="side">The withdrawing side.</param>
        private void AdvanceWithdrawal(TacticalCombatSide side)
        {
            if (!side.IsWithdrawing)
                return;
            side.WithdrawalMilliseconds -= 1000;
            if (side.WithdrawalMilliseconds > 0 || side.UnreadyScuttleRequested)
                return;
            if (
                !side.DepartureCountdownCompleted
                && BattleStation != null
                && BattleStation.Unit.Side == side.Side
                && BattleStation.IsOperational
            )
                QueueCommand(new Command(CommandKind.Withdraw, BattleStation.Unit));
            side.DepartureCountdownCompleted = true;
            foreach (TacticalCombatUnit unit in _units)
                if (
                    unit.Side == side.Side
                    && unit.Systems.HasHyperdrive
                    && unit.GetSpeed() > 0
                    && (
                        unit.IsTargetable
                        || unit.Status
                            is TacticalCombatUnitStatus.EnteringHyperspace
                                or TacticalCombatUnitStatus.AwaitingWithdrawal
                    )
                )
                    return;
            side.UnreadyScuttleRequested = true;
            foreach (TacticalCombatUnit unit in _units)
                if (
                    unit.Side == side.Side
                    && unit.IsTargetable
                    && (!unit.Systems.HasHyperdrive || unit.GetSpeed() == 0)
                )
                    QueueCommand(new Command(CommandKind.Scuttle, unit));
        }

        /// <summary>
        /// Reserves carrier berths and queues recovery for squadrons within their hangar entrance.
        /// </summary>
        /// <param name="group">The recovering fighter group.</param>
        private void AdvanceRecovery(TacticalCombatGroup group)
        {
            bool warned = false;
            foreach (TacticalCombatUnit fighter in group.Members)
            {
                if (!fighter.IsTargetable)
                    continue;
                if (!_recoveries.TryGetValue(fighter, out Recovery recovery))
                {
                    TacticalCombatCarrier carrier = null;
                    if (
                        fighter.CarrierID != null
                        && _carriers.TryGetValue(
                            fighter.CarrierID,
                            out TacticalCombatCarrier linked
                        )
                        && linked.Reserve(fighter)
                    )
                        carrier = linked;
                    bool full = true;
                    if (carrier == null)
                        foreach (TacticalCombatUnit candidate in _units)
                            if (
                                !candidate.IsFighter
                                && candidate.Side == fighter.Side
                                && (
                                    candidate.IsTargetable
                                    || candidate.IsBattleStation && BattleStation.IsOperational
                                )
                            )
                            {
                                TacticalCombatCarrier candidateCarrier = _carriers[
                                    candidate.InstanceID
                                ];
                                if (candidateCarrier.Reserve(fighter))
                                {
                                    carrier = candidateCarrier;
                                    break;
                                }
                                full &= candidateCarrier.IsFullAndLaunched;
                            }
                    if (carrier == null)
                    {
                        if (!warned)
                        {
                            RecoveryUnavailable?.Invoke(group);
                            warned = true;
                        }
                        if (full && GetSide(group.Side).IsWithdrawing)
                            QueueCommand(new Command(CommandKind.Scuttle, fighter));
                        else if (full)
                            group.SetOrder(TacticalCombatOrder.None);
                        continue;
                    }
                    recovery = new Recovery(carrier);
                    _recoveries.Add(fighter, recovery);
                    QueueCommand(new Command(CommandKind.SetRecovery, fighter, carrier.Unit));
                }
                if (
                    recovery.State == 2
                    && TacticalCombatGeometry.GetLength(
                        fighter.Position - recovery.Carrier.HangarPosition
                    ) < 2
                )
                {
                    recovery.State = 3;
                    QueueCommand(new Command(CommandKind.Recover, recovery.Carrier.Unit, fighter));
                }
            }
        }

        /// <summary>
        /// Gets the live carrier assigned by an applied recovery command.
        /// </summary>
        /// <param name="fighter">The approaching fighter squadron.</param>
        /// <returns>The assigned carrier, or null before assignment.</returns>
        private TacticalCombatCarrier GetRecoveryCarrier(TacticalCombatUnit fighter) =>
            _recoveries.TryGetValue(fighter, out Recovery recovery) && recovery.State >= 2
                ? recovery.Carrier
                : null;

        /// <summary>
        /// Releases a fighter's pending recovery reservation.
        /// </summary>
        /// <param name="fighter">The reassigned or destroyed squadron.</param>
        private void ReleaseRecovery(TacticalCombatUnit fighter)
        {
            if (!_recoveries.Remove(fighter, out Recovery recovery))
                return;
            recovery.Carrier.Detach(fighter);
            fighter.CarrierID = null;
        }

        /// <summary>
        /// Announces a station charge crossing to the faction whose tactical advisor reports it.
        /// The station calls this before storing the completed charge.
        /// </summary>
        private void NotifyStationCharged()
        {
            if (GetSide(PlayerSide).FactionIndex == 1)
                Audio.EnqueueNotification(275);
        }

        /// <summary>
        /// Selects the firing sound from the firing unit's weapon and authored faction.
        /// </summary>
        /// <param name="shot">The queued discharge.</param>
        /// <returns>The sound requested before drawing the weapon effect.</returns>
        private TacticalCombatSound GetFiringSound(TacticalCombatShot shot) =>
            shot.Weapon switch
            {
                TacticalWeaponType.LaserCannon when shot.Source.IsFighter => GetSide(
                    shot.Source.Side
                ).FactionIndex == 0
                    ? TacticalCombatSound.AllianceFighterLaser
                    : TacticalCombatSound.EmpireFighterLaser,
                TacticalWeaponType.LaserCannon => TacticalCombatSound.CapitalLaser,
                TacticalWeaponType.IonCannon => shot.Source.IsFighter
                    ? TacticalCombatSound.FighterIon
                    : TacticalCombatSound.CapitalIon,
                _ => TacticalCombatSound.HeavyWeapon,
            };

        /// <summary>
        /// Applies one queued mutation after resolving its current participation guards.
        /// </summary>
        /// <param name="command">The due command.</param>
        private void ApplyCommand(Command command)
        {
            TacticalCombatUnit source = command.Source;
            TacticalCombatUnit target = command.Target;
            switch (command.Kind)
            {
                case CommandKind.CreateGroup:
                case CommandKind.AddGroupMember:
                case CommandKind.RemoveGroupMember:
                    if (
                        _groupsByID.TryGetValue(command.Group.ID, out TacticalCombatGroup owner)
                        && ReferenceEquals(owner, command.Group)
                    )
                    {
                        if (command.Kind == CommandKind.RemoveGroupMember)
                            RemoveAppliedMember(owner, source);
                        else
                        {
                            owner.AddMember(source);
                            if (command.Kind == CommandKind.CreateGroup)
                            {
                                owner.Formation.SetPosition(source.Position);
                                _groups.Add(owner);
                                _groups.Sort((first, second) => first.ID.CompareTo(second.ID));
                            }
                        }
                    }
                    break;
                case CommandKind.Order:
                case CommandKind.AttackMode:
                case CommandKind.Formation:
                case CommandKind.Waypoint:
                case CommandKind.RemoveWaypoint:
                    if (
                        _groupsByID.TryGetValue(command.Group.ID, out TacticalCombatGroup commanded)
                        && ReferenceEquals(commanded, command.Group)
                    )
                    {
                        if (command.Kind == CommandKind.Order)
                            ApplyOrder(commanded, (TacticalCombatOrder)command.Value);
                        else if (command.Kind == CommandKind.AttackMode)
                            commanded.ApplyAttackMode(command.Value != 0);
                        else if (command.Kind == CommandKind.Formation)
                            commanded.ApplyFormation((TacticalCombatFormationKind)command.Value);
                        else if (command.Kind == CommandKind.Waypoint)
                            commanded.ApplyWaypoint(command.Position, command.Append);
                        else
                            commanded.RemoveWaypoint(command.Position, false);
                    }
                    break;
                case CommandKind.Superlaser:
                    if (target.IsTargetable)
                    {
                        Audio.Enqueue(TacticalCombatSound.Superlaser);
                        if (GetSide(PlayerSide).FactionIndex == 0)
                            Audio.EnqueueNotification(145);
                    }
                    SuperlaserFired?.Invoke(source, target);
                    break;
                case CommandKind.AttackRun:
                    if (
                        _groupsByID.TryGetValue(
                            command.Run.Group.ID,
                            out TacticalCombatGroup attacking
                        )
                        && ReferenceEquals(attacking, command.Run.Group)
                        && ReferenceEquals(attacking.AttackRun, command.Run)
                        && attacking.ActiveOrder == TacticalCombatOrder.AttackBattleStation
                    )
                    {
                        command.Run.ApplyState(command.RunState);
                        AttackRunChanged?.Invoke(command.Run);
                    }
                    break;
                case CommandKind.Target:
                case CommandKind.RemoveTarget:
                    if (
                        _groupsByID.TryGetValue(
                            command.Group.ID,
                            out TacticalCombatGroup registered
                        ) && ReferenceEquals(registered, command.Group)
                    )
                    {
                        if (command.Kind == CommandKind.Target)
                            command.Group.ApplyTarget(target, command.Append);
                        else
                            command.Group.RemoveTarget(target, false);
                    }
                    break;
                case CommandKind.Fire:
                    if (source.IsTargetable && target.IsTargetable)
                    {
                        Audio.Enqueue(GetFiringSound(command.Shot));
                        ShotFired?.Invoke(command.Shot);
                    }
                    break;
                case CommandKind.Hit:
                    if (target.IsTargetable)
                    {
                        TacticalCombatImpactKind impact = target.Systems.ApplyHit(
                            command.Shot.Weapon,
                            command.Shot.Strength,
                            target.GetManeuverability(),
                            source.GetManeuverability(),
                            _random
                        );
                        ShotApplied?.Invoke(command.Shot, impact);
                        if (!target.Systems.IsAlive)
                            Destroy(target);
                    }
                    break;
                case CommandKind.System:
                    if (!source.IsTargetable)
                        break;
                    TacticalSystemEvent change = command.Change;
                    if (change.Kind == TacticalSystemEventKind.Disruption)
                    {
                        source.Systems.ApplyDisruption(change.AttackDelay, change.MovementDelay);
                        for (int arc = 0; arc < 4; arc++)
                            if ((change.DischargedArcMask & (1 << arc)) != 0)
                                source.Weapons.ClearBattery((TacticalWeaponArc)arc);
                    }
                    else
                    {
                        source.Systems.ApplyComponentEvent(change.System, change.IsRepair);
                        if (
                            change.System == TacticalShipSystem.TractorBeam
                            && source.TractorTargetID != null
                            && source.Systems.GetTractorBudget() == 0
                        )
                        {
                            _tractorCommandsPending.Add(source);
                            QueueCommand(
                                new Command(
                                    CommandKind.TractorUnlock,
                                    source,
                                    GetUnit(source.TractorTargetID)
                                )
                            );
                        }
                    }
                    UpdateTractorRestraint();
                    SystemChanged?.Invoke(source, change);
                    break;
                case CommandKind.Launch:
                    if (
                        target.Systems.IsAlive
                        && GetGroup(target)?.ActiveOrder != TacticalCombatOrder.Recover
                        && _carriers[source.InstanceID].Launch(target)
                    )
                    {
                        Audio.Enqueue(TacticalCombatSound.FighterLaunch);
                        _spatial.Update(target);
                    }
                    break;
                case CommandKind.SetRecovery:
                    if (target == null)
                        ReleaseRecovery(source);
                    else if (
                        source.IsTargetable
                        && _recoveries.TryGetValue(source, out Recovery recovery)
                        && ReferenceEquals(recovery.Carrier.Unit, target)
                    )
                    {
                        recovery.State = 2;
                        source.CarrierID = target.InstanceID;
                    }
                    break;
                case CommandKind.Recover:
                    if (target.IsTargetable && _carriers[source.InstanceID].Recover(target))
                    {
                        _recoveries[target].State = 4;
                        RemoveFromGroup(target);
                        _spatial.Update(target);
                        FighterRecovered?.Invoke(target);
                    }
                    break;
                case CommandKind.TractorLock:
                    _tractorCommandsPending.Remove(source);
                    if (source.IsTargetable && target.IsTargetable)
                        source.TractorTargetID = target.InstanceID;
                    UpdateTractorRestraint();
                    break;
                case CommandKind.TractorUnlock:
                    _tractorCommandsPending.Remove(source);
                    if (source.TractorTargetID == target?.InstanceID)
                        source.TractorTargetID = null;
                    UpdateTractorRestraint();
                    break;
                case CommandKind.Hyperspace:
                    if (!source.IsTargetable)
                        break;
                    if (source.Systems.HasHyperdrive && source.GetSpeed() > 0)
                    {
                        source.Status = TacticalCombatUnitStatus.EnteringHyperspace;
                        source.TractorTargetID = null;
                        _spatial.Update(source);
                        UpdateTractorRestraint();
                    }
                    else
                        Destroy(source);
                    break;
                case CommandKind.Withdraw:
                    if (
                        source.Systems.IsAlive
                        && source.Status != TacticalCombatUnitStatus.Destroyed
                    )
                    {
                        source.Status = TacticalCombatUnitStatus.Withdrawn;
                        _departed.Add(source);
                        RemoveFromGroup(source);
                        _spatial.Update(source);
                        UnitWithdrawn?.Invoke(source);
                    }
                    break;
                case CommandKind.Scuttle:
                    if (source.IsTargetable && !source.IsBattleStation)
                        Audio.Enqueue(source.Systems.GetExplosionSound());
                    if (
                        source.Status
                        is not TacticalCombatUnitStatus.EnteringHyperspace
                            and not TacticalCombatUnitStatus.AwaitingWithdrawal
                            and not TacticalCombatUnitStatus.Withdrawn
                    )
                        Destroy(source);
                    break;
            }
        }

        /// <summary>
        /// Applies destruction, carrier payload losses, reservation cleanup, and spatial removal.
        /// </summary>
        /// <param name="unit">The destroyed tactical unit.</param>
        private void Destroy(TacticalCombatUnit unit)
        {
            if (unit.Status == TacticalCombatUnitStatus.Destroyed)
                return;
            unit.Systems.Destroy();
            unit.Status = TacticalCombatUnitStatus.Destroyed;
            unit.TractorTargetID = null;
            RemoveFromGroup(unit);
            _spatial.Update(unit);
            if (_carriers.TryGetValue(unit.InstanceID, out TacticalCombatCarrier carrier))
            {
                foreach (TacticalCombatUnit fighter in _units)
                    if (fighter.IsFighter && fighter.CarrierID == unit.InstanceID)
                    {
                        if (
                            fighter.Status
                            is TacticalCombatUnitStatus.Docked
                                or TacticalCombatUnitStatus.Recovered
                        )
                            Destroy(fighter);
                        else if (_recoveries.ContainsKey(fighter))
                            ReleaseRecovery(fighter);
                    }
                carrier.Clear();
            }
            else
            {
                ReleaseRecovery(unit);
                if (
                    unit.CarrierID != null
                    && _carriers.TryGetValue(unit.CarrierID, out TacticalCombatCarrier host)
                )
                    host.Detach(unit);
                unit.CarrierID = null;
            }
            UpdateTractorRestraint();
            UnitDestroyed?.Invoke(unit);
        }

        /// <summary>
        /// Rebuilds tractor restraint from current live locks without querying campaign state.
        /// </summary>
        private void UpdateTractorRestraint()
        {
            _nextTractorTargets.Clear();
            foreach (TacticalCombatUnit unit in _units)
                unit.TractorRestraint = 0;
            foreach (TacticalCombatUnit unit in _units)
            {
                TacticalCombatUnit target = GetUnit(unit.TractorTargetID);
                if (target != null && unit.IsTargetable && target.IsTargetable)
                {
                    target.TractorRestraint += unit.Systems.GetTractorBudget();
                    _nextTractorTargets.Add(target);
                }
                else
                    unit.TractorTargetID = null;
            }
            foreach (TacticalCombatUnit target in _nextTractorTargets)
                if (!_tractorTargets.Contains(target))
                    Audio.Enqueue(TacticalCombatSound.TractorEngaged);
            foreach (TacticalCombatUnit target in _tractorTargets)
                if (!_nextTractorTargets.Contains(target))
                    Audio.Enqueue(TacticalCombatSound.TractorReleased);
            (_tractorTargets, _nextTractorTargets) = (_nextTractorTargets, _tractorTargets);
        }

        /// <summary>
        /// Starts result presentation after one side has no active or departing units.
        /// </summary>
        private void CheckCompletion()
        {
            bool attacker = false;
            bool defender = false;
            foreach (TacticalCombatUnit unit in _units)
                if (unit.IsEngaged && (!unit.IsBattleStation || BattleStation.IsOperational))
                {
                    if (unit.Side == CombatSide.Attacker)
                        attacker = true;
                    else
                        defender = true;
                }
            if (attacker && defender)
                return;
            _completionStarted = true;
            _completionMilliseconds = 4000;
            Winner =
                attacker == defender ? CombatSide.Draw
                : attacker ? CombatSide.Attacker
                : CombatSide.Defender;
            _outcomes[0] = GetEndingOutcome(CombatSide.Attacker, attacker);
            _outcomes[1] = GetEndingOutcome(CombatSide.Defender, defender);
            AssignSurvivors();
        }

        /// <summary>
        /// Gets the resolved side outcome captured when active combat ended.
        /// </summary>
        /// <param name="side">The requested encounter side.</param>
        /// <returns>The side's resolved participation outcome.</returns>
        public SpaceCombatSideOutcome GetOutcome(CombatSide side) => _outcomes[(int)side];

        /// <summary>
        /// Captures final losses, component damage, recovery assignments, and actual departures without mutating campaign units.
        /// </summary>
        /// <returns>The completed encounter's unit outcomes.</returns>
        public SpaceCombatResult CreateResult()
        {
            if (!IsComplete)
                throw new InvalidOperationException("The tactical encounter has not completed.");
            SpaceCombatResult result = new()
            {
                Winner = Winner,
                AttackerOutcome = _outcomes[0],
                DefenderOutcome = _outcomes[1],
                IterationsCompleted = _progress,
            };
            foreach (TacticalCombatUnit unit in _units)
            {
                if (unit.Source is CapitalShip ship)
                    result.ShipDamage.Add(
                        new ShipDamageResult
                        {
                            Ship = ship,
                            HullBefore = (int)unit.InitialHull,
                            HullAfter = Math.Max((int)unit.Systems.Hull, 0),
                            SystemDamage = unit.Systems.CaptureDamage(
                                ship.Hyperdrive != 0,
                                unit.Definition?.BackupHyperdrive > 0
                            ),
                        }
                    );
                else if (unit.Source is Starfighter fighter)
                {
                    result.FighterLosses.Add(
                        new FighterLossResult
                        {
                            Fighter = fighter,
                            SquadsBefore = unit.InitialSquadronSize,
                            SquadsAfter = unit.SquadronSize,
                        }
                    );
                    if (unit.Systems.IsAlive)
                    {
                        TacticalCombatUnit carrier = unit.Status
                            is TacticalCombatUnitStatus.Docked
                                or TacticalCombatUnitStatus.Recovered
                            ? GetUnit(unit.CarrierID)
                            : null;
                        result.FighterRecovery.Add(
                            new FighterRecoveryResult
                            {
                                Fighter = fighter,
                                Carrier = carrier?.Source as CapitalShip,
                            }
                        );
                    }
                }
                if (unit.Systems.IsAlive && HasDeparted(unit))
                    result.WithdrawnUnits.Add(unit.Source);
            }
            return result;
        }

        /// <summary>
        /// Checks whether a unit or its recovered carrier completed departure.
        /// </summary>
        /// <param name="unit">The surviving encounter unit.</param>
        /// <returns>Whether its final campaign location must follow a departure route.</returns>
        public bool HasDeparted(TacticalCombatUnit unit)
        {
            TacticalCombatUnit carrier =
                unit.IsFighter
                && unit.Status
                    is TacticalCombatUnitStatus.Docked
                        or TacticalCombatUnitStatus.Recovered
                    ? GetUnit(unit.CarrierID)
                    : null;
            return _departed.Contains(carrier ?? unit);
        }

        /// <summary>
        /// Captures whether an inactive side departed or lost all active forces.
        /// </summary>
        /// <param name="side">The resolved side.</param>
        /// <param name="active">Whether active units remain.</param>
        /// <returns>The resolved side outcome.</returns>
        private SpaceCombatSideOutcome GetEndingOutcome(CombatSide side, bool active)
        {
            if (active)
                return SpaceCombatSideOutcome.Active;
            foreach (TacticalCombatUnit unit in _departed)
                if (unit.Side == side)
                    return SpaceCombatSideOutcome.Withdrawn;
            return SpaceCombatSideOutcome.Destroyed;
        }

        /// <summary>
        /// Assigns surviving fighters to available surviving carriers before exposing the completed result.
        /// </summary>
        private void AssignSurvivors()
        {
            foreach (TacticalCombatUnit fighter in _units)
            {
                if (!fighter.IsFighter || !fighter.Systems.IsAlive)
                    continue;
                foreach (TacticalCombatUnit carrier in _units)
                    if (
                        !carrier.IsFighter
                        && carrier.Side == fighter.Side
                        && carrier.Systems.IsAlive
                        && (
                            carrier.IsTargetable
                            || carrier.IsBattleStation && BattleStation.IsOperational
                            || carrier.Status
                                is TacticalCombatUnitStatus.EnteringHyperspace
                                    or TacticalCombatUnitStatus.AwaitingWithdrawal
                                    or TacticalCombatUnitStatus.Withdrawn
                        )
                        && _carriers[carrier.InstanceID].AttachSurvivor(fighter)
                    )
                        break;
            }
        }

        /// <summary>
        /// Exposes the final result after the end-of-combat presentation delay.
        /// </summary>
        private void Complete()
        {
            IsComplete = true;
            Completed?.Invoke();
        }

        /// <summary>
        /// Identifies a queued mutation without exposing renderer or campaign implementation details.
        /// </summary>
        private enum CommandKind
        {
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
