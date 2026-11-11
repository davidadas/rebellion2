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
        private readonly TacticalCombatAI _automation;
        private readonly int[] _nextGroupID = new int[2];
        private readonly IRandomNumberProvider _random;
        private int _progress;
        private int _phase;
        private float _elapsedInTick;
        private int _systemTime = 3;
        private bool _legacyTimeAdvanced;
        private TacticalCombatScheduler _scheduler;
        private readonly TacticalCombatDirector _director;
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
        internal bool CanStartBattleStationAttack => CaptureDirectorState().CanAttackBattleStation;
        public CombatSide PlayerSide { get; }
        public float BattleDepth { get; }
        public TacticalCombatNavigation Navigation { get; }
        public TacticalCombatAudio Audio { get; }
        public TacticalCombatSelection Selection { get; }
        public int SystemTime =>
            _scheduler == null ? _systemTime : unchecked((int)_scheduler.SystemTime);
        public bool IsComplete { get; private set; }
        public CombatSide? EnteringSide { get; }
        public bool IsArriving => _arrival?.IsComplete == false;
        public IReadOnlyList<bool> TerminalFlags => _ending.Flags;
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
        public bool IsDirectControlEnabled =>
            !_directCommandsSuspended && !IsArriving && !GetSide(PlayerSide).IsAutomated;

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
            Rebellion.Game.SpaceCombat.TacticalCombatDefinition definition = null,
            Rebellion.Game.SpaceCombat.TacticalCombatEnvironmentDefinition environment = null,
            TacticalCombatSceneryRandom sceneryRandom = null,
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
            _automation = new TacticalCombatAI(this);
            Selection = new TacticalCombatSelection(this);
            _director = new TacticalCombatDirector(
                GetSide(PlayerSide).FactionIndex,
                messageID => Audio.EnqueueNotification(messageID)
            );
            _ending = new TacticalCombatEnding(
                GetSide(PlayerSide).FactionIndex,
                messageID => Audio.EnqueueNotification(messageID),
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
        /// Removes category ownership now and queues deletion of the group's applied roster.
        /// </summary>
        /// <param name="group">The registered group being replaced.</param>
        public void DeleteGroup(TacticalCombatGroup group)
        {
            ValidateGroup(group);
            ClearGroupCategories(group);
            QueueCommand(new Command(CommandKind.DeleteGroup, null, group: group));
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
            if (side is not (CombatSide.Attacker or CombatSide.Defender))
                return false;
            TacticalCombatSide state = GetSide(side);
            if (!state.HasWithdrawalDestination || state.IsWithdrawing)
                return false;
            foreach (WithdrawalMember member in _orderedWithdrawalMembers)
                if (
                    member.Unit.Side != side
                    && !member.Unit.IsFighter
                    && IsDisplayedMember(member.Unit)
                    && member.Unit.HasGravityWell
                )
                    return false;
            for (int pass = 0; pass < 2; pass++)
                foreach (WithdrawalMember member in _orderedWithdrawalMembers)
                    if (
                        member.Unit.Side == side
                        && member.Unit.IsFighter == (pass == 0)
                        && IsDisplayedMember(member.Unit)
                        && member.CheckWithdrawalReadiness()
                    )
                        return true;
            return state.FactionIndex == 1
                && BattleStation != null
                && (BattleStation.IsOperational || !BattleStation.IsConstructionComplete);
        }

        /// <summary>
        /// Checks player departure admission and announces the first unready capital before confirmation opens.
        /// </summary>
        /// <returns>Whether the player may open the withdrawal confirmation.</returns>
        public bool PreparePlayerWithdrawal()
        {
            if (!CanWithdraw(PlayerSide))
                return false;
            foreach (WithdrawalMember member in _orderedWithdrawalMembers)
                if (
                    member.Unit.Side == PlayerSide
                    && !member.Unit.IsFighter
                    && IsDisplayedMember(member.Unit)
                    && !member.CheckWithdrawalReadiness()
                )
                {
                    Audio.EnqueueNotification(GetSide(PlayerSide).FactionIndex == 0 ? 149 : 270);
                    break;
                }
            return true;
        }

        /// <summary>
        /// Rechecks the confirmed player departure before queuing its semantic input command.
        /// Dispatch rechecks current availability when the queued command is drained.
        /// </summary>
        /// <returns>Whether the withdrawal command was queued for the player.</returns>
        public bool ConfirmPlayerWithdrawal()
        {
            if (!CanWithdraw(PlayerSide))
                return false;
            Selection.QueueWithdrawal();
            return true;
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
        /// Queues the superlaser visual and delayed destruction for an admitted opposing target.
        /// </summary>
        /// <param name="target">The opposing tactical unit.</param>
        /// <returns>Whether the target resolves within this encounter.</returns>
        public bool FireSuperlaser(TacticalCombatUnit target)
        {
            if (
                BattleStation == null
                || target == null
                || target.Side == BattleStation.Unit.Side
                || !_unitsByID.TryGetValue(target.InstanceID, out TacticalCombatUnit registered)
                || !ReferenceEquals(target, registered)
            )
                return false;
            BattleStation.Fire();
            QueueCommand(new Command(CommandKind.Superlaser, BattleStation.Unit, target));
            QueueCommand(new Command(CommandKind.Destroy, target, BattleStation.Unit), 1);
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
            ApplyWithdrawal(side);
            return true;
        }

        /// <summary>
        /// Suspends direct player orders while preserving camera and inspection ownership.
        /// </summary>
        internal void SuspendDirectCommands() => _directCommandsSuspended = true;

        /// <summary>
        /// Applies a withdrawal admitted by the caller's current eligibility check.
        /// </summary>
        /// <param name="side">The side whose admitted departure is being applied.</param>
        internal void ApplyWithdrawal(CombatSide side)
        {
            TacticalCombatSide state = GetSide(side);
            state.Withdrawal = new TacticalCombatWithdrawal(
                state.FactionIndex,
                state.InitialWithdrawalMilliseconds,
                Selection.ProcessStationDeparture,
                () => QueueCommand(new Command(CommandKind.Withdraw, BattleStation.Unit))
            );
            if (side == PlayerSide)
                Audio.EnqueueNotification(state.FactionIndex == 0 ? 143 : 269);
            PrepareWithdrawalGroups(side);
            state.Withdrawal.SelectInitialDelay(BattleStation?.IsOperational == true);
        }

        /// <summary>
        /// Changes retained group labels before requesting a separate departure group for each deployed capital.
        /// </summary>
        /// <param name="side">The side whose admitted withdrawal is being prepared.</param>
        private void PrepareWithdrawalGroups(CombatSide side)
        {
            List<TacticalCombatGroup> groups = new();
            foreach (TacticalCombatGroup group in _groups)
                if (group.Side == side)
                    groups.Add(group);
            groups.Sort(CompareGroupRegistration);
            foreach (TacticalCombatGroup group in groups)
            {
                if (!group.IsFighterGroup)
                {
                    SetOrder(group, TacticalCombatOrder.Withdraw);
                    continue;
                }
                if (side == PlayerSide)
                    ClearGroupCategories(group);
                if (group.Members.Count == 0)
                    continue;
                if (
                    group.ActiveOrder == TacticalCombatOrder.AttackBattleStation
                    && group.AttackRun != null
                )
                    RequestAttackRunState(group.AttackRun, TacticalCombatAttackRunState.Cancelled);
                SetOrder(
                    group,
                    group.Members[0].Systems.HasHyperdrive
                        ? TacticalCombatOrder.Withdraw
                        : TacticalCombatOrder.Recover
                );
            }
            foreach (WithdrawalMember member in _orderedWithdrawalMembers)
            {
                TacticalCombatUnit unit = member.Unit;
                if (unit.Side != side || unit.IsFighter || !IsDisplayedMember(unit))
                    continue;
                TacticalCombatGroup applied = GetGroup(unit);
                if (applied != null)
                {
                    if (applied.Side == PlayerSide && !HasUntrackedRequestedOwner(unit))
                        ClearGroupCategories(applied);
                    RemoveGroupMember(applied, unit);
                }
                TacticalCombatGroup departure = CreateGroup(new[] { unit });
                SetOrder(departure, TacticalCombatOrder.Withdraw);
            }
        }

        /// <summary>
        /// Checks whether requested ownership lies outside the player's retained object categories.
        /// </summary>
        /// <param name="unit">The deployed capital whose secondary ownership is inspected.</param>
        /// <returns>Whether a nonempty requested owner is not retained in an object category.</returns>
        private bool HasUntrackedRequestedOwner(TacticalCombatUnit unit)
        {
            TacticalCombatGroup requested = GetRequestedGroup(unit);
            if (requested == null)
                return false;
            for (int category = 0; category < 8; category++)
                if (ReferenceEquals(_objectGroups[(int)PlayerSide, category], requested))
                    return false;
            return true;
        }

        /// <summary>
        /// Compares retained group labels by unsigned battle registration identity.
        /// </summary>
        /// <param name="first">The first retained group.</param>
        /// <param name="second">The second retained group.</param>
        /// <returns>The ordered comparison of their registration keys.</returns>
        private static int CompareGroupRegistration(
            TacticalCombatGroup first,
            TacticalCombatGroup second
        ) => unchecked((uint)first.ID).CompareTo(unchecked((uint)second.ID));

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
                group.Side != PlayerSide
                && GetSide(group.Side).IsAutomated
                && order != TacticalCombatOrder.None
                && (
                    group.Order
                        is TacticalCombatOrder.Recover
                            or TacticalCombatOrder.AttackBattleStation
                    || group.ActiveOrder
                        is TacticalCombatOrder.Recover
                            or TacticalCombatOrder.AttackBattleStation
                )
            )
                return;
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
                run.OrderRequested += SetOrder;
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
        /// Starts the captured entering force's presentation before admitting battle commands or clocks.
        /// </summary>
        /// <param name="presentation">The renderer that owns arrival poses and the pause-control strobe.</param>
        public void BeginArrival(ITacticalCombatArrivalPresentation presentation)
        {
            if (!EnteringSide.HasValue)
                throw new InvalidOperationException(
                    "The encounter has no captured entering force."
                );
            if (_arrival != null || SystemTime != 3)
                throw new InvalidOperationException(
                    "Arrival can only begin once before combat starts."
                );

            List<TacticalCombatArrivalMember> members = new();
            foreach (TacticalCombatGroup group in _groups)
            {
                if (group.Side != EnteringSide.Value)
                    continue;
                foreach (TacticalCombatUnit unit in group.Members)
                    members.Add(
                        new TacticalCombatArrivalMember(
                            unit.Index,
                            unit.Definition.PresentationIndex,
                            unit.Side,
                            unit.IsFighter,
                            unit.Status == TacticalCombatUnitStatus.Docked,
                            new Vector4(unit.Position, -1),
                            new Vector4(unit.Heading, -1)
                        )
                    );
            }
            _arrival = new TacticalCombatArrival(
                EnteringSide.Value,
                members,
                GetSide(EnteringSide.Value).Forward,
                presentation,
                messageID => Audio.EnqueueNotification(messageID)
            );
            _arrivalStarting = true;
        }

        /// <summary>
        /// Binds the explicit millisecond provider used by the production battle scheduler.
        /// This clock remains live during pause and is sampled only after arrival admits scheduling.
        /// </summary>
        /// <param name="clock">The wrapping unsigned monotonic playback clock.</param>
        public void ConfigureClockedAdvance(Func<uint> clock)
        {
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));
            if (_scheduler != null || _legacyTimeAdvanced)
                throw new InvalidOperationException(
                    "The battle clock must be bound once before delta-driven time advances."
                );
            _scheduler = new TacticalCombatScheduler(clock, new SchedulerSteps(this));
        }

        /// <summary>
        /// Advances entry and briefing frame time separately from the explicitly bound battle clock.
        /// Pausing suspends scheduler calls without resetting or stopping its supplied clock.
        /// </summary>
        /// <param name="elapsedMilliseconds">The frame interval for entry, briefing, and continuous-work admission.</param>
        public void AdvanceClocked(float elapsedMilliseconds)
        {
            if (_scheduler == null)
                throw new InvalidOperationException(
                    "The battle requires an explicit clock before clocked advancement."
                );
            if (!AdvanceArrival(elapsedMilliseconds) || IsComplete)
                return;
            _director.Advance(elapsedMilliseconds, CaptureDirectorState());
            if (IsPaused)
            {
                Selection.ProcessPendingCommands();
                return;
            }
            if (_scheduler.Advance(elapsedMilliseconds * 0.4f))
                Complete();
        }

        /// <summary>
        /// Presents entry before either battle-advancement contract admits commands or clocks.
        /// </summary>
        /// <param name="elapsedMilliseconds">The frame interval used by the entry owner.</param>
        /// <returns>Whether entry has released the subsequent battle operations.</returns>
        private bool AdvanceArrival(float elapsedMilliseconds)
        {
            if (!IsArriving)
                return true;
            if (_arrivalStarting)
            {
                _arrivalStarting = false;
                return false;
            }
            return _arrival.Advance(elapsedMilliseconds);
        }

        /// <summary>
        /// Advances a delta-driven encounter used without a bound production clock.
        /// A zero interval can still drain pending commands after entry has completed.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed unpaused frame duration.</param>
        public void Advance(float elapsedMilliseconds)
        {
            if (_scheduler != null && elapsedMilliseconds != 0)
                throw new InvalidOperationException(
                    "Clock-bound encounters require clocked advancement."
                );
            if (elapsedMilliseconds != 0)
                _legacyTimeAdvanced = true;
            if (!AdvanceArrival(elapsedMilliseconds))
                return;
            Selection.ProcessPendingCommands();
            if (IsComplete || elapsedMilliseconds <= 0)
                return;
            _director.Advance(elapsedMilliseconds, CaptureDirectorState());
            _elapsedInTick += elapsedMilliseconds;
            if (IsPaused)
                return;
            if (_elapsedInTick >= 1000)
            {
                _elapsedInTick %= 1000;
                if (_phase != 5)
                    elapsedMilliseconds = 0;
                else if (_completionStarted && _ending.AdvanceTerminalTick())
                {
                    Complete();
                    return;
                }
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
                _systemTime++;
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
                group.AttackRun.OrderRequested -= SetOrder;
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
        /// Queues a formation change for a registered command or fighter group.
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
                && TacticalCombatGeometry.GetTractorDistance(unit.Position, target.Position)
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
            TacticalCombatSide first = GetSide(PlayerSide);
            TacticalCombatSide second = GetSide(
                PlayerSide == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker
            );
            AdvanceSide(first);
            AdvanceSide(second);
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
            UpdateTractorRestraint();
        }

        /// <summary>
        /// Prepares candidates, advances the Empire station, and checks withdrawal members before side planning.
        /// </summary>
        /// <param name="side">The side occupying this whole-tick update slot.</param>
        private void AdvanceSide(TacticalCombatSide side)
        {
            _automation.AssessStrength(side);
            AdvanceWithdrawalCountdown(side);
            _automation.PrepareCandidates(side);
            if (side.FactionIndex == 1)
                BattleStation?.Advance(1000);
            AdvanceWithdrawalMembers(side);
            _automation.Plan(side);
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
        /// Orders tactical object roots by unsigned registration identity.
        /// </summary>
        /// <param name="first">The first registered unit.</param>
        /// <param name="second">The second registered unit.</param>
        /// <returns>The unsigned registration comparison.</returns>
        private static int CompareUnits(TacticalCombatUnit first, TacticalCombatUnit second) =>
            unchecked((uint)first.Index).CompareTo(unchecked((uint)second.Index));

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
                    SetOrder(group, TacticalCombatOrder.None);
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
                    unit.Heading = -GetSide(unit.Side).Forward;
                    if (
                        unit.HyperspaceMilliseconds == 0
                        || float.IsNaN(unit.HyperspaceMilliseconds)
                    )
                        Audio.EnqueueNotification(unit.IsFighter ? 29 : 31);
                    unit.HyperspaceMilliseconds += elapsedMilliseconds;
                    if (unit.HyperspaceMilliseconds >= 4000)
                    {
                        unit.HyperspaceMilliseconds = 0;
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
        /// Counts down an admitted withdrawal before the side's candidate-maintenance phase.
        /// </summary>
        /// <param name="side">The withdrawing side.</param>
        private void AdvanceWithdrawalCountdown(TacticalCombatSide side)
        {
            TacticalCombatWithdrawal withdrawal = side.Withdrawal;
            if (withdrawal == null)
                return;
            withdrawal.AdvanceCountdown(
                1000,
                true,
                BattleStation?.IsOperational == true,
                side.Side == PlayerSide
            );
        }

        /// <summary>
        /// Rechecks retained capital members before fighters after the side's candidate-maintenance phase.
        /// </summary>
        /// <param name="side">The withdrawing side whose departure phase is active.</param>
        private void AdvanceWithdrawalMembers(TacticalCombatSide side)
        {
            TacticalCombatWithdrawal withdrawal = side.Withdrawal;
            if (withdrawal?.Phase != TacticalCombatWithdrawalPhase.Departing)
                return;
            List<ITacticalCombatWithdrawalMember> capitals = _withdrawalCapitals[(int)side.Side];
            List<ITacticalCombatWithdrawalMember> fighters = _withdrawalFighters[(int)side.Side];
            capitals.Clear();
            fighters.Clear();
            foreach (WithdrawalMember member in _orderedWithdrawalMembers)
                if (member.Unit.Side == side.Side && IsDisplayedMember(member.Unit))
                    (member.Unit.IsFighter ? fighters : capitals).Add(member);
            withdrawal.AdvanceMembers(capitals, fighters, _requestUnreadyScuttle);
        }

        /// <summary>
        /// Queues one addressed retained member's scuttling command without traversing the roster.
        /// </summary>
        /// <param name="index">The member's tactical registration key.</param>
        private void RequestUnreadyScuttle(int index) =>
            QueueCommand(new Command(CommandKind.Scuttle, _withdrawalUnitsByIndex[index]));

        /// <summary>
        /// Checks the deployed capital or fighter chain's retained participation domain independently of hull.
        /// </summary>
        /// <param name="unit">The registered non-station participant.</param>
        /// <returns>Whether the member remains in its side's deployed display chain.</returns>
        private static bool IsDisplayedMember(TacticalCombatUnit unit) =>
            unit.Status
                is TacticalCombatUnitStatus.Active
                    or TacticalCombatUnitStatus.PreparingHyperspace
                    or TacticalCombatUnitStatus.EnteringHyperspace
                    or TacticalCombatUnitStatus.AwaitingWithdrawal
                    or TacticalCombatUnitStatus.AttackingBattleStation;

        /// <summary>
        /// Compares tactical member keys using their unsigned registration ordering.
        /// </summary>
        /// <param name="first">The first registered member.</param>
        /// <param name="second">The second registered member.</param>
        /// <returns>The comparison of their unsigned keys.</returns>
        private static int CompareWithdrawalMembers(
            WithdrawalMember first,
            WithdrawalMember second
        ) => unchecked((uint)first.Index).CompareTo(unchecked((uint)second.Index));

        /// <summary>
        /// Reads a participant's movement using current command support and retained beam sources.
        /// </summary>
        /// <param name="unit">The participant being inspected.</param>
        /// <returns>The available movement before a single-precision speed store.</returns>
        public double GetMovementBudget(TacticalCombatUnit unit) =>
            unit.Systems.GetAvailableMovement(
                GetSide(unit.Side).GetMovementSupport(unit.IsFighter),
                GetTractorRestraint(unit)
            );

        /// <summary>
        /// Reads a participant's live movement relative to its complete engine budget.
        /// </summary>
        /// <param name="unit">The participant being inspected.</param>
        /// <returns>The available movement ratio using current command support and retained beams.</returns>
        public double GetMovementRatio(TacticalCombatUnit unit) =>
            unit.Systems.GetMovementRatio(
                GetSide(unit.Side).GetMovementSupport(unit.IsFighter),
                GetTractorRestraint(unit)
            );

        /// <summary>
        /// Refreshes departure readiness from live command support and retained tractor contributors.
        /// </summary>
        /// <param name="unit">The participating capital ship or fighter.</param>
        /// <returns>Whether its hyperdrive and movement budgets are ordered and nonzero.</returns>
        private bool CheckWithdrawalReadiness(TacticalCombatUnit unit) =>
            unit.Systems.HasHyperdrive
            && unit.Systems.CanEnterHyperspace(
                GetSide(unit.Side).GetMovementSupport(unit.IsFighter),
                GetTractorRestraint(unit)
            );

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
                            SetOrder(group, TacticalCombatOrder.None);
                        continue;
                    }
                    recovery = new Recovery(carrier);
                    _recoveries.Add(fighter, recovery);
                    QueueCommand(new Command(CommandKind.SetRecovery, fighter, carrier.Unit));
                }
                if (recovery.State == 2)
                {
                    float distance = TacticalCombatGeometry.GetRecoveryDistance(
                        fighter.Position,
                        recovery.Carrier.HangarPosition
                    );
                    if (distance < 2 || float.IsNaN(distance))
                    {
                        recovery.State = 3;
                        QueueCommand(
                            new Command(CommandKind.Recover, recovery.Carrier.Unit, fighter)
                        );
                    }
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
                case CommandKind.DeleteGroup:
                    if (
                        _groupsByID.TryGetValue(command.Group.ID, out TacticalCombatGroup deleted)
                        && ReferenceEquals(deleted, command.Group)
                    )
                    {
                        while (deleted.Members.Count != 0)
                            deleted.RemoveMember(deleted.Members[0]);
                        DeregisterGroup(deleted);
                    }
                    break;
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
                    if (IsActiveDisplayObject(source) && IsActiveDisplayObject(target))
                    {
                        Audio.Enqueue(GetFiringSound(command.Shot));
                        ShotFired?.Invoke(command.Shot);
                    }
                    break;
                case CommandKind.Hit:
                    if (IsActiveDisplayObject(target))
                    {
                        TacticalCombatImpactKind impact = target.Systems.ApplyHit(
                            command.Shot.Weapon,
                            command.Shot.Strength,
                            target.GetManeuverability(),
                            source.GetManeuverability(),
                            _random,
                            source.InstanceID
                        );
                        RefreshTractorContribution(target);
                        if (impact == TacticalCombatImpactKind.Destroyed)
                            Destroy(target);
                    }
                    break;
                case CommandKind.System:
                    TacticalSystemEvent change = command.Change;
                    if (change.Kind == TacticalSystemEventKind.Disruption)
                    {
                        if (change.AttackDelay.HasValue && change.MovementDelay.HasValue)
                            source.Systems.ApplyDisruption(
                                change.AttackDelay.Value,
                                change.MovementDelay.Value
                            );
                        for (int arc = 0; arc < 4; arc++)
                            if ((change.DischargedArcMask & (1 << arc)) != 0)
                                source.Weapons.ClearBattery((TacticalWeaponArc)arc);
                    }
                    else
                    {
                        source.Systems.ApplyComponentEvent(change.System, change.IsRepair);
                        if (
                            !change.IsRepair
                            && change.System == TacticalShipSystem.TractorBeam
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
                    RefreshTractorContribution(source);
                    SystemChanged?.Invoke(source, change);
                    break;
                case CommandKind.Launch:
                    if (
                        target.Systems.IsAlive
                        && GetGroup(target)?.ActiveOrder != TacticalCombatOrder.Recover
                        && _carriers[source.InstanceID].Launch(target)
                    )
                    {
                        _initiallyDockedFighters.Remove(target);
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
                    if (IsActiveDisplayObject(source) && IsActiveDisplayObject(target))
                        ApplyTractorLock(source, target);
                    break;
                case CommandKind.TractorUnlock:
                    _tractorCommandsPending.Remove(source);
                    source.TractorTargetID = null;
                    if (target != null)
                        ApplyTractorUnlock(source, target);
                    break;
                case CommandKind.Hyperspace:
                    if (!IsActiveDisplayObject(source))
                        break;
                    if (CheckWithdrawalReadiness(source))
                    {
                        source.Status = TacticalCombatUnitStatus.EnteringHyperspace;
                        _spatial.Update(source);
                    }
                    else
                        Scuttle(source);
                    break;
                case CommandKind.Withdraw:
                    if (source.IsBattleStation)
                    {
                        if (!BattleStation.TryWithdraw())
                            break;
                    }
                    else if (
                        source.Status
                        is not (
                            TacticalCombatUnitStatus.Active
                            or TacticalCombatUnitStatus.PreparingHyperspace
                            or TacticalCombatUnitStatus.EnteringHyperspace
                            or TacticalCombatUnitStatus.AwaitingWithdrawal
                        )
                    )
                        break;

                    source.Status = TacticalCombatUnitStatus.Withdrawn;
                    _departed.Add(source);
                    RemoveFromGroup(source);
                    _spatial.Update(source);
                    UnitWithdrawn?.Invoke(source);
                    break;
                case CommandKind.Destroy:
                    if (
                        IsActiveDisplayObject(source)
                        && (
                            (BattleStation != null && ReferenceEquals(target, BattleStation.Unit))
                            || source.Systems.Hull < 0
                            || float.IsNaN(source.Systems.Hull)
                        )
                    )
                    {
                        Audio.Enqueue(source.Systems.GetExplosionSound());
                        DestructionEffectRequested?.Invoke(source);
                        if (BattleStation == null || !ReferenceEquals(target, BattleStation.Unit))
                            ReportDestruction(source, target?.InstanceID);
                        Destroy(source);
                    }
                    break;
                case CommandKind.Scuttle:
                    Scuttle(source);
                    break;
            }
        }

        /// <summary>
        /// Queues the destruction report for the credited actor's applied group and current category.
        /// </summary>
        /// <param name="victim">The unit whose hull was lost.</param>
        /// <param name="attackerInstanceID">The firing unit's captured identity.</param>
        private void ReportDestruction(TacticalCombatUnit victim, string attackerInstanceID)
        {
            if (
                attackerInstanceID == null
                || !_unitsByID.TryGetValue(attackerInstanceID, out TacticalCombatUnit attacker)
            )
                return;
            bool credited = attacker.Side == PlayerSide;
            TacticalCombatUnit actor = credited ? attacker : victim;
            int bank = GetSide(PlayerSide).FactionIndex == 0 ? 0 : 122;
            int message = credited ? 85 : 98;
            if (actor.GroupID is int groupID)
            {
                if (actor.IsFighter)
                {
                    int category = GetReportFighterCategory(groupID);
                    if (category < 0)
                        return;
                    message = (credited ? 94 : 107) + category;
                }
                else
                {
                    if (
                        !credited
                        && _personnelHostCounts.TryGetValue(victim.InstanceID, out int count)
                    )
                        for (int index = 0; index < count; index++)
                            Audio.EnqueueNotification(bank == 0 ? 134 : 260);
                    int category = GetReportObjectCategory(groupID);
                    if (category >= 0)
                        message += 1 + category;
                }
            }
            Audio.EnqueueNotification(message + bank);
        }

        /// <summary>
        /// Finds an applied capital group in the player's current command slots.
        /// </summary>
        /// <param name="groupID">The applied group identity.</param>
        /// <returns>The matching command slot, or minus one when it has no slot.</returns>
        private int GetReportObjectCategory(int groupID)
        {
            for (int category = 0; category < 8; category++)
                if (_objectGroups[(int)PlayerSide, category]?.ID == groupID)
                    return category;
            return -1;
        }

        /// <summary>
        /// Resolves a fighter group's current slot or retained deployment category.
        /// </summary>
        /// <param name="groupID">The applied group identity.</param>
        /// <returns>The resolved category, or minus one for an unavailable fighter group.</returns>
        private int GetReportFighterCategory(int groupID)
        {
            for (int category = 0; category < 4; category++)
                if (_fighterGroups[(int)PlayerSide, category]?.ID == groupID)
                    return category;
            return
                _groupsByID.TryGetValue(groupID, out TacticalCombatGroup group)
                && group.IsFighterGroup
                ? group.FighterCategory
                : -1;
        }

        /// <summary>
        /// Checks the ordinary display states admitted by destruction and scuttling commands.
        /// Hull strength is evaluated separately by each command.
        /// </summary>
        /// <param name="unit">The registered encounter participant.</param>
        /// <returns>Whether the unit remains in an admitted display state.</returns>
        private static bool IsActiveDisplayObject(TacticalCombatUnit unit) =>
            unit?.IsActiveDisplayObject == true;

        /// <summary>
        /// Applies scuttling only to an active display object with ordered positive hull strength.
        /// </summary>
        /// <param name="unit">The unit addressed by the scuttling request.</param>
        private void Scuttle(TacticalCombatUnit unit)
        {
            if (!IsActiveDisplayObject(unit) || !(unit.Systems.Hull > 0))
                return;
            Audio.Enqueue(unit.Systems.GetExplosionSound());
            DestructionEffectRequested?.Invoke(unit);
            Destroy(unit);
        }

        /// <summary>
        /// Applies destruction, carrier payload losses, reservation cleanup, and spatial removal.
        /// </summary>
        /// <param name="unit">The destroyed tactical unit.</param>
        private void Destroy(TacticalCombatUnit unit)
        {
            if (unit.Status == TacticalCombatUnitStatus.Destroyed)
                return;
            if (unit.IsBattleStation)
                BattleStation.Destroy();
            else
                unit.Systems.Destroy();
            unit.Status = TacticalCombatUnitStatus.Destroyed;
            _initiallyDockedFighters.Remove(unit);
            if (unit.TractorTargetID != null)
            {
                _tractorCommandsPending.Add(unit);
                QueueCommand(
                    new Command(CommandKind.TractorUnlock, unit, GetUnit(unit.TractorTargetID))
                );
            }
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
            RefreshTractorContribution(unit);
            UnitDestroyed?.Invoke(unit);
        }

        /// <summary>
        /// Adds a retained tractor contributor after active-object command admission.
        /// </summary>
        /// <param name="source">The unit applying the tractor beam.</param>
        /// <param name="target">The unit whose movement chain retains the source.</param>
        private void ApplyTractorLock(TacticalCombatUnit source, TacticalCombatUnit target)
        {
            if (
                !_tractorSources.TryGetValue(
                    target,
                    out SortedDictionary<uint, TacticalCombatUnit> sources
                )
            )
            {
                sources = new();
                _tractorSources.Add(target, sources);
            }
            if (sources.Count == 0)
                Audio.Enqueue(TacticalCombatSound.TractorEngaged);
            source.TractorTargetID = target.InstanceID;
            sources.TryAdd(unchecked((uint)source.Index), source);
            target.TractorRestraint = GetTractorRestraint(target);
        }

        /// <summary>
        /// Removes an addressed contributor and announces every resulting empty tractor chain.
        /// </summary>
        /// <param name="source">The contributor addressed by the command.</param>
        /// <param name="target">The resolved target whose chain is updated.</param>
        private void ApplyTractorUnlock(TacticalCombatUnit source, TacticalCombatUnit target)
        {
            bool populated = _tractorSources.TryGetValue(
                target,
                out SortedDictionary<uint, TacticalCombatUnit> sources
            );
            if (populated)
                sources.Remove(unchecked((uint)source.Index));
            if (!populated || sources.Count == 0)
                Audio.Enqueue(TacticalCombatSound.TractorReleased);
            target.TractorRestraint = GetTractorRestraint(target);
        }

        /// <summary>
        /// Adds retained tractor contributions in unsigned source-key order with a store after every addition.
        /// </summary>
        /// <param name="target">The unit whose retained tractor chain is read.</param>
        /// <returns>The rounded sum of its current contributors, without a health or participation filter.</returns>
        private float GetTractorRestraint(TacticalCombatUnit target)
        {
            float restraint = 0;
            if (
                _tractorSources.TryGetValue(
                    target,
                    out SortedDictionary<uint, TacticalCombatUnit> sources
                )
            )
                foreach (TacticalCombatUnit source in sources.Values)
                    restraint = (float)(
                        (double)restraint + source.Systems.GetTractorContribution()
                    );
            return restraint;
        }

        /// <summary>
        /// Refreshes the one retained target affected by a contributor's hull or tractor-system mutation.
        /// </summary>
        /// <param name="source">The contributor whose current budget changed.</param>
        private void RefreshTractorContribution(TacticalCombatUnit source)
        {
            TacticalCombatUnit target = GetUnit(source.TractorTargetID);
            if (target != null)
                target.TractorRestraint = GetTractorRestraint(target);
        }

        /// <summary>
        /// Refreshes stored movement inputs after a retained lock or system mutation without changing link ownership.
        /// </summary>
        private void UpdateTractorRestraint()
        {
            foreach (TacticalCombatUnit unit in _units)
                unit.TractorRestraint = GetTractorRestraint(unit);
        }

        /// <summary>
        /// Captures the station, faction fighter roots, and retained behavior helpers for notification sequencing.
        /// </summary>
        /// <returns>The current admission and briefing facts, independent of hull and simulation pause.</returns>
        private TacticalCombatDirectorState CaptureDirectorState()
        {
            int allianceFighters = 0;
            int empireFighters = 0;
            int allianceOutsideAttackRuns = 0;
            foreach (TacticalCombatUnit unit in _units)
            {
                if (!unit.IsFighter || !unit.IsEngaged)
                    continue;
                if (GetSide(unit.Side).FactionIndex == 0)
                {
                    allianceFighters++;
                    if (unit.Status != TacticalCombatUnitStatus.AttackingBattleStation)
                        allianceOutsideAttackRuns++;
                }
                else
                    empireFighters++;
            }
            bool attackRunActive = false;
            bool allianceAttackRunOrdered = false;
            foreach (TacticalCombatGroup group in _groups)
            {
                attackRunActive |=
                    group.ActiveOrder == TacticalCombatOrder.AttackBattleStation
                    && group.AttackRun != null;
                if (GetSide(group.Side).FactionIndex == 0)
                    allianceAttackRunOrdered |=
                        group.Order == TacticalCombatOrder.AttackBattleStation
                        || group.ActiveOrder == TacticalCombatOrder.AttackBattleStation;
            }
            return new TacticalCombatDirectorState(
                IsArriving,
                BattleStation != null,
                BattleStation?.IsOperational == true,
                BattleStation?.HasPlanetaryProtection == true,
                attackRunActive,
                allianceFighters != 0,
                empireFighters != 0,
                allianceAttackRunOrdered,
                allianceFighters,
                empireFighters,
                allianceOutsideAttackRuns
            );
        }

        /// <summary>
        /// Starts the terminal assessment once active movement and object counting find an inactive faction.
        /// </summary>
        private void CheckCompletion()
        {
            if (_completionStarted)
                return;
            TacticalCombatForceAssessment first = CaptureForceAssessment(_sides[0]);
            TacticalCombatForceAssessment second = CaptureForceAssessment(_sides[1]);
            TacticalCombatForceAssessment alliance = _sides[0].FactionIndex == 0 ? first : second;
            TacticalCombatForceAssessment empire = _sides[0].FactionIndex == 1 ? first : second;
            if (
                !_ending.TryBegin(
                    alliance,
                    empire,
                    BattleStation?.IsOperational == true,
                    BattleStation?.HasDeparted == true
                )
            )
                return;
            _completionStarted = true;
            bool attacker =
                first.ActiveCount != 0
                || BattleStation?.Unit.Side == CombatSide.Attacker && BattleStation.IsOperational;
            bool defender =
                second.ActiveCount != 0
                || BattleStation?.Unit.Side == CombatSide.Defender && BattleStation.IsOperational;
            Winner =
                attacker == defender ? CombatSide.Draw
                : attacker ? CombatSide.Attacker
                : CombatSide.Defender;
            _outcomes[0] = GetEndingOutcome(CombatSide.Attacker, attacker);
            _outcomes[1] = GetEndingOutcome(CombatSide.Defender, defender);
        }

        /// <summary>
        /// Counts active-root members separately from ordinary roster participation and completed departures.
        /// </summary>
        /// <param name="side">The faction whose withdrawal and object facts are captured.</param>
        /// <returns>The immutable terminal assessment.</returns>
        private TacticalCombatForceAssessment CaptureForceAssessment(TacticalCombatSide side)
        {
            int active = 0;
            bool stillActive = false;
            bool departed = false;
            foreach (TacticalCombatUnit unit in _units)
            {
                if (unit.Side != side.Side || unit.IsBattleStation)
                    continue;
                if (unit.IsEngaged)
                    active++;
                stillActive |=
                    unit.Status
                        is TacticalCombatUnitStatus.Active
                            or TacticalCombatUnitStatus.PreparingHyperspace
                            or TacticalCombatUnitStatus.EnteringHyperspace
                            or TacticalCombatUnitStatus.AwaitingWithdrawal;
                departed |= unit.Status == TacticalCombatUnitStatus.Withdrawn;
            }
            int withdrawalState = side.WithdrawalPhase.HasValue
                ? (int)side.WithdrawalPhase.Value
                : 0;
            return new TacticalCombatForceAssessment(
                active,
                withdrawalState,
                stillActive,
                departed
            );
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
                IterationsCompleted =
                    _scheduler == null ? _progress : unchecked((int)_scheduler.Progress),
            };
            foreach (TacticalCombatUnit unit in _units)
            {
                if (unit.Source is CapitalShip ship)
                    result.ShipDamage.Add(
                        new ShipDamageResult
                        {
                            Ship = ship,
                            HullBefore = (int)unit.InitialHull,
                            HullAfter = GetCapitalResultHull(unit),
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
                            SquadsAfter = GetFighterResultCount(unit),
                        }
                    );
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
                if (HasDeparted(unit))
                    result.WithdrawnUnits.Add(unit.Source);
            }
            return result;
        }

        /// <summary>
        /// Captures hull for campaign import, retaining at least one point for a non-destroyed ordinary ship.
        /// Battle stations export their separate stored hull pool directly.
        /// </summary>
        /// <param name="unit">The capital ship whose final tactical state is being captured.</param>
        /// <returns>The truncated hull count carried into the campaign result.</returns>
        private static int GetCapitalResultHull(TacticalCombatUnit unit)
        {
            float hull = unit.Systems.Hull;
            if (unit.IsBattleStation)
                return TruncateResultCount(hull);
            if (unit.Status == TacticalCombatUnitStatus.Destroyed)
                return 0;
            return TruncateResultCount(hull < 1 ? 1 : hull);
        }

        /// <summary>
        /// Captures squadron strength from the retained participation state and final hull pool.
        /// </summary>
        /// <param name="unit">The fighter squadron whose final tactical state is being captured.</param>
        /// <returns>Zero for a destroyed squadron, or its truncated count with a zero count replaced by one.</returns>
        private static int GetFighterResultCount(TacticalCombatUnit unit)
        {
            if (unit.Status == TacticalCombatUnitStatus.Destroyed)
                return 0;
            int count = TruncateResultCount((double)unit.Systems.Hull / 8);
            return count == 0 ? 1 : count;
        }

        /// <summary>
        /// Truncates a final pool through a signed 64-bit count before retaining its low 32 bits.
        /// Nonfinite and out-of-range values carry a zero low word.
        /// </summary>
        /// <param name="value">The final hull or squadron count before integer conversion.</param>
        /// <returns>The low 32 bits of the truncated count.</returns>
        private static int TruncateResultCount(double value)
        {
            if (
                double.IsNaN(value)
                || value < -9223372036854775808d
                || value >= 9223372036854775808d
            )
                return 0;
            return unchecked((int)(long)value);
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
        /// Assigns retained fighters to ordinary carriers in registration order, then to the eligible station.
        /// </summary>
        private void AssignSurvivors()
        {
            _directCommandsSuspended = true;
            foreach (WithdrawalMember fighterMember in _orderedWithdrawalMembers)
            {
                TacticalCombatUnit fighter = fighterMember.Unit;
                if (
                    !fighter.IsFighter
                    || !(
                        fighter.IsActiveDisplayObject
                        || fighter.Status
                            is TacticalCombatUnitStatus.Docked
                                or TacticalCombatUnitStatus.Recovered
                                or TacticalCombatUnitStatus.Withdrawn
                    )
                )
                    continue;
                bool attached = false;
                foreach (WithdrawalMember carrierMember in _orderedWithdrawalMembers)
                {
                    TacticalCombatUnit carrier = carrierMember.Unit;
                    if (
                        !carrier.IsFighter
                        && carrier.Side == fighter.Side
                        && (
                            carrier.IsActiveDisplayObject
                            || carrier.Status
                                is TacticalCombatUnitStatus.EnteringHyperspace
                                    or TacticalCombatUnitStatus.AwaitingWithdrawal
                                    or TacticalCombatUnitStatus.Withdrawn
                        )
                        && _carriers[carrier.InstanceID].AttachSurvivor(fighter)
                    )
                    {
                        attached = true;
                        break;
                    }
                }
                if (
                    !attached
                    && GetSide(fighter.Side).FactionIndex == 1
                    && BattleStation != null
                    && (BattleStation.IsOperational || BattleStation.HasDeparted)
                )
                    _carriers[BattleStation.Unit.InstanceID].AttachSurvivor(fighter);
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
