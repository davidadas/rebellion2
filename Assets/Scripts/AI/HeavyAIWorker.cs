using System.Collections.Generic;
using Rebellion.Game;

// Corresponds to LargeSelectionRecord (astruct_423), the "heavy AI worker" that drives
// all galaxy analysis and strategy record processing for one faction side.
//
// vtable: PTR_00657740_LargeSelectionRecord (96 slots).
//
// Key field offsets (relative to LargeSelectionRecord base):
//   +0x50 = MissionCycleState    (states: 1=startup, 2=startup-phase2, 3=batch, 4=calibrate, 5=cursor)
//   +0x64 = StrategyRecordList   (circular doubly-linked list of StrategyRecord objects)
//   +0x70 = OwnerSide            (faction side 0/1)
//   +0x80 = WorkCursor           (pointer to current StrategyRecord being processed in state 3/5)
//   +0x84 = Workspace            (AIWorkspace / scratchBlock)
//   +0x418 = SpecialModeFlag     (0 = normal cursor advance during state 5)
//   +0x420 = BatchCounter        (incremented each time a work item is dispatched; type != 0x203)
//   +0x424 = BatchCounterAux     (secondary counter; purpose unclear from disassembly)
//   +0x428 = BatchLimit          (set to 10 during at_start_setup_missions; resets when reached)
//   +0x88 = WorkerFlags          (bit 0x80000000 = abort flag; terminates batch early)
//
// Constructor: FUN_00484bf0_init_heavy_startup_side_worker
//   Calls FUN_00487940 (base init), FUN_004be1d0 (init callback queue at +0x54),
//   FUN_004bda80 (init strategy record list at +0x64), FUN_004170a0 / FUN_00417460
//   (init workspace at +0x84). Installs vtable PTR_00657740. Sets state = 1.
public class HeavyAIWorker
{
    // --- Per-worker state ---

    /// <summary>
    /// Mission-cycle state variable (+0x50).
    /// 1 = at_start phase A (galaxy analysis running)
    /// 2 = at_start phase B (apply strategy tables)
    /// 3 = batch work (call slot-5 on each record)
    /// 4 = calibrate (run calibration sub-machine)
    /// 5 = cursor advance (reset records, prime for next batch)
    /// Any other value → transitions to state 4.
    /// Initialized to 1 by the constructor; set to 4 after startup completes.
    /// </summary>
    private int _missionCycleState = 1;

    /// <summary>
    /// The circular ordered list of all 14 strategy records (+0x64).
    /// Populated during AtStartSetupMissions (startup state 2) in the order specified
    /// by FUN_004bdcb0_apply_table_values_to calls.
    /// </summary>
    private readonly StrategyRecordList _strategyRecordList;

    /// <summary>
    /// Current cursor into the strategy record list (+0x80).
    /// During state 5: points to the record being reset (cursor advance).
    /// During state 3: points to the record being ticked (batch work).
    /// Null when no record is currently selected.
    /// </summary>
    private StrategyRecord _workCursor;

    /// <summary>
    /// The shared analysis workspace (+0x84, the scratchBlock).
    /// Holds all galaxy analysis results that strategy records read.
    /// </summary>
    public AIWorkspace Workspace { get; }

    /// <summary>
    /// Faction side index (+0x70). 0 = Rebel, 1 = Empire.
    /// Passed to all strategy record constructors.
    /// </summary>
    public int OwnerSide { get; }

    /// <summary>
    /// Special mode flag (+0x418). When non-zero, the cursor advance in state 5
    /// is suppressed; the worker reuses the existing cursor position.
    /// Normal operation: 0.
    /// </summary>
    private int _specialModeFlag;

    /// <summary>
    /// Batch counter (+0x420). Incremented each time a strategy record produces a
    /// work item with TypeCode != 0x203. Reset to 0 when state transitions to 4.
    /// </summary>
    private int _batchCounter;

    /// <summary>
    /// Secondary batch counter (+0x424). Incremented alongside _batchCounter.
    /// Exact semantics unclear from disassembly alone.
    /// </summary>
    private int _batchCounterAux;

    /// <summary>
    /// Batch limit (+0x428). When _batchCounter >= _batchLimit, the batch phase ends
    /// and the cycle resets to state 4. Set to 10 during startup.
    /// </summary>
    private int _batchLimit;

    /// <summary>
    /// Worker flags (+0x88). Bit 0x80000000 = abort: terminates the batch phase early
    /// and forces a reset to state 4.
    /// </summary>
    private int _workerFlags;

    // The faction this worker serves (used when dispatching work items).
    private readonly Faction _faction;

    /// <summary>
    /// True while the worker is still running the two-phase startup sequence
    /// (states 1 and 2).  Once AtStartSetupMissions() returns true, startup is
    /// complete and Tick() should be called each frame instead.
    /// </summary>
    public bool IsInStartup => _missionCycleState == 1 || _missionCycleState == 2;

    public HeavyAIWorker(Faction faction, int ownerSide)
    {
        _faction = faction;
        OwnerSide = ownerSide;
        Workspace = new AIWorkspace { Owner = faction };
        _strategyRecordList = new StrategyRecordList();
        _missionCycleState = 1;
    }

    // ----------------------------------------------------------------
    // Slot 4: FUN_00484ea0_at_start_setup_missions
    //
    // Drives the two-phase startup:
    //   State 1: run GalaxyAnalysisPipeline until it completes → state 2
    //   State 2: create and register all 14 strategy records → state 4, return true
    //   Other:   reset to state 1, return false
    //
    // Called repeatedly until it returns true (startup complete). After returning
    // true the per-frame Tick() (slot 9) takes over.
    // ----------------------------------------------------------------
    public bool AtStartSetupMissions()
    {
        if (_missionCycleState == 1)
        {
            // State 1: run galaxy analysis pipeline until done.
            bool analysisComplete = GalaxyAnalysisPipeline.Tick(Workspace);
            if (analysisComplete)
                _missionCycleState = 2;
            return false;
        }

        if (_missionCycleState != 2)
        {
            _missionCycleState = 1;
            return false;
        }

        // State 2: apply all 14 strategy record tables.
        // Order matches FUN_00484ea0 assembly trace (slots called via apply_table_values_to):
        //   table ids: 0xd=13, 0x2=2, 0x1=1, 0x3=3, 0x6=6, 0x7=7, 0x4=4, 0x5=5,
        //              0x8=8, 0x9=9, 0xa=10, 0xb=11, 0xc=12, 0xe=14
        ApplyStrategyTable(13);
        ApplyStrategyTable(2);
        ApplyStrategyTable(1);
        ApplyStrategyTable(3);
        ApplyStrategyTable(6);
        ApplyStrategyTable(7);
        ApplyStrategyTable(4);
        ApplyStrategyTable(5);
        ApplyStrategyTable(8);
        ApplyStrategyTable(9);
        ApplyStrategyTable(10);
        ApplyStrategyTable(11);
        ApplyStrategyTable(12);
        ApplyStrategyTable(14);

        // Set batch limit and transition to operating state 4.
        _batchLimit = 10;
        _missionCycleState = 4;
        return true;
    }

    // FUN_004bdcb0_apply_table_values_to — creates and inserts one strategy record.
    // Only inserts if the table ID is not already in the list (idempotent).
    private void ApplyStrategyTable(int tableId)
    {
        if (_strategyRecordList.FindById(tableId) != null)
            return;

        StrategyRecord record = StrategyRecordFactory.Create(tableId, OwnerSide);
        if (record == null)
            return;

        // Slot 3: initialize with workspace reference (FUN_004bfa60 / FUN_0041c290 / FUN_0049dff0).
        record.Initialize(Workspace);

        // After slot-3 call: set active state to 0 (inactive; not yet active).
        record.ActiveState = 0;

        // Insert into BST-ordered list.
        _strategyRecordList.Insert(record);
    }

    // ----------------------------------------------------------------
    // Slot 9: FUN_00484f90 — per-frame mission-cycle state machine
    //
    // Returns false (0) = work in progress, call again next frame.
    // Returns true  (1) = batch phase complete or aborted; cycle reset to state 4.
    //
    // State machine:
    //   Any state not 3/4/5 → set state 4
    //   State 4 (calibrate): run CalibrationSubMachine on workspace; if done → state 5
    //   State 5 (cursor advance): walk cursor through record list, call Reset() on each;
    //                              when list exhausted → state 3
    //   State 3 (batch work): pull records, call Tick(), dispatch work items;
    //                          when batch full or list empty → state 4, return true
    // ----------------------------------------------------------------
    public bool Tick()
    {
        int state = _missionCycleState;

        if (state != 3 && state != 4 && state != 5)
        {
            _missionCycleState = 4;
            return false;
        }

        if (state == 4)
        {
            // Calibration phase: run FUN_00417cb0 equivalent.
            bool calibrationDone = GalaxyAnalysisPipeline.TickCalibration(Workspace);
            if (calibrationDone)
                _missionCycleState = 5;
            return false;
        }

        if (state == 5)
        {
            // Cursor-advance phase: walk through each record in list and call Reset().
            // Skipped when _specialModeFlag != 0.
            if (_specialModeFlag == 0)
            {
                if (_workCursor == null)
                {
                    // Start at the end of the list (FUN_005f35d0_get_last_node_in_list).
                    _workCursor = _strategyRecordList.GetLast();
                }

                if (_workCursor != null)
                {
                    // Call slot 4 (Reset) on current record, then advance cursor.
                    _workCursor.Reset();
                    _workCursor = _strategyRecordList.GetPrevious(_workCursor);
                }
            }

            if (_workCursor == null)
                _missionCycleState = 3;

            return false;
        }

        // State 3: batch work phase.
        if (_batchCounter >= _batchLimit)
        {
            // Batch full — reset cycle.
            _missionCycleState = 4;
            _batchCounter = 0;
            return true;
        }

        if (_workCursor == null)
        {
            // No current record: get next from priority queue (FUN_004bdd00).
            _workCursor = _strategyRecordList.GetNextWorkItem();
            if (_workCursor == null)
            {
                // List exhausted — reset cycle.
                _missionCycleState = 4;
                _batchCounter = 0;
                return true;
            }
            return false;
        }

        // Active cursor: tick the current record.
        _workCursor.TickCounter = 0;
        AIWorkItem workItem = _workCursor.Tick();

        if (workItem != null)
        {
            // Work item produced: dispatch it.
            AIDispatchResult result = new AIDispatchResult();
            bool dispatched = workItem.Dispatch(out result);

            if (!dispatched)
            {
                // Not dispatched: record handles it (slot 6).
                _workCursor.RecordResult(workItem);
            }
            else
            {
                // Dispatched: route to AI manager (FUN_00489ee0 equivalent).
                RouteWorkItemToManager(workItem);

                // Increment batch counter unless this is a special scheduler item (0x203).
                if (workItem.TypeCode != 0x203)
                    _batchCounter++;
            }
        }

        // Check if cursor record has completed (+0x20 = ReadyFlag).
        // If non-zero: clear cursor and check whether to end the batch.
        if (_workCursor != null && _workCursor.ReadyFlag != 0)
        {
            _workCursor.ActiveState = 0;
            _workCursor = null;

            if (workItem != null)
            {
                // Work was produced and record is done — end this batch pass.
                _missionCycleState = 4;
                _batchCounter = 0;
                return true;
            }

            // Check abort flag.
            if ((_workerFlags & unchecked((int)0x80000000)) != 0)
            {
                _missionCycleState = 4;
                _batchCounter = 0;
                return true;
            }
        }

        return false;
    }

    // FUN_00489ee0 equivalent: routes a completed work item to whatever system
    // should act on it (manufacturing, missions, fleet movement, etc.).
    private void RouteWorkItemToManager(AIWorkItem workItem)
    {
        // TODO: dispatch to the appropriate game system based on workItem type.
        // In the original this calls through the AI manager's routing table.
    }

    /// <summary>
    /// Slot 25: writes the fleet total capacity into the workspace.
    /// FUN_004852b0: *(scratchBlock+0x184) = *param_1; sets bit 0x80000000 in +0x4.
    /// </summary>
    public void SetFleetTotalCapacity(int value)
    {
        Workspace.FleetTotalCapacity = value;
        Workspace.StatusFlags |= unchecked((int)0x80000000);
    }

    /// <summary>
    /// Slot 26: writes the fleet assigned capacity into the workspace.
    /// FUN_004852d0: *(scratchBlock+0x188) = param_1; sets bit 0x80000000 in +0x4.
    /// </summary>
    public void SetFleetAssignedCapacity(int value)
    {
        Workspace.FleetAssignedCapacity = value;
        Workspace.StatusFlags |= unchecked((int)0x80000000);
    }

    /// <summary>
    /// Slot 6: FUN_00487a40 — increment day counter.
    /// Returns the old value of the day counter (this+0x34).
    /// </summary>
    public int IncrementDayCounter()
    {
        int old = _dayCounter;
        _dayCounter++;
        return old;
    }

    private int _dayCounter;
}

/// <summary>
/// Manages the ordered collection of strategy records for one HeavyAIWorker.
/// In the original game this is an AVL BST at worker+0x64. The ordering is by
/// assignment slot (FUN_005f39b0 inserts with auto-incremented slot ID).
///
/// The list is traversed in two ways:
///   1. Cursor advance (state 5): backwards from last node using previous_node links.
///   2. Batch work (state 3): FUN_004bdd00 scans for the first active (state=1) record,
///      using a priority scan (FUN_004be160) first, then a round-robin walk.
/// </summary>
public class StrategyRecordList
{
    private readonly List<StrategyRecord> _records = new List<StrategyRecord>();

    // Round-robin cursor for GetNextWorkItem (previous_node equivalent at list+0x10).
    private int _roundRobinCursor = -1;

    // High-priority cursor (next_reference equivalent at list+0x18).
    private StrategyRecord _priorityItem;

    /// <summary>Inserts a record at the end of the list.</summary>
    public void Insert(StrategyRecord record)
    {
        _records.Add(record);
    }

    /// <summary>
    /// Returns the record with the given type ID, or null if not present.
    /// Corresponds to FUN_005f3a70_get_table_by_id.
    /// </summary>
    public StrategyRecord FindById(int typeId)
    {
        foreach (StrategyRecord r in _records)
        {
            if (r.TypeId == typeId)
                return r;
        }
        return null;
    }

    /// <summary>
    /// Returns the last record in the list (the tail node).
    /// Corresponds to FUN_005f35d0_get_last_node_in_list.
    /// </summary>
    public StrategyRecord GetLast()
    {
        if (_records.Count == 0)
            return null;
        return _records[_records.Count - 1];
    }

    /// <summary>
    /// Returns the record before the given one in list order.
    /// Used during cursor-advance (state 5) to walk backwards through the list.
    /// Corresponds to node->previous_node traversal.
    /// </summary>
    public StrategyRecord GetPrevious(StrategyRecord current)
    {
        int idx = _records.IndexOf(current);
        if (idx <= 0)
            return null;
        return _records[idx - 1];
    }

    /// <summary>
    /// FUN_004bdd00: gets the next work item for batch processing.
    ///
    /// First tries the high-priority scan (FUN_004be160). If that returns a record,
    /// returns it and caches it as the next_reference.
    ///
    /// If no high-priority item: advances the round-robin cursor to find the first
    /// record with ActiveState == 1 (FUN_005f2ef0 check). Wraps around the list.
    /// If it wraps around to the starting position without finding one, returns null.
    /// </summary>
    public StrategyRecord GetNextWorkItem()
    {
        // Priority scan first (FUN_004be160 equivalent).
        StrategyRecord priority = ScanForPriorityItem();
        if (priority != null)
        {
            _priorityItem = priority;
            return priority;
        }

        // Round-robin walk: find first active record.
        if (_records.Count == 0)
            return null;

        // Initialize cursor to last record if not set.
        if (_roundRobinCursor < 0 || _roundRobinCursor >= _records.Count)
            _roundRobinCursor = _records.Count - 1;

        int startCursor = _roundRobinCursor;
        bool wrapped = false;

        while (true)
        {
            StrategyRecord candidate = _records[_roundRobinCursor];
            if (candidate.ActiveState == 1)
            {
                _priorityItem = candidate;
                return candidate;
            }

            // Advance to previous (backwards traversal).
            _roundRobinCursor--;
            if (_roundRobinCursor < 0)
            {
                _roundRobinCursor = _records.Count - 1;
                if (wrapped)
                    break;
            }

            if (_roundRobinCursor == startCursor)
            {
                if (wrapped)
                    break;
                wrapped = true;
            }
        }

        _priorityItem = null;
        return null;
    }

    // FUN_004be160: scans for a high-priority pending record.
    // In the original this is a separate fast-path scan for records that have been
    // explicitly enqueued as high-priority by the routing system.
    // Stub: always returns null (no priority overrides in the basic implementation).
    private StrategyRecord ScanForPriorityItem()
    {
        return null;
    }

    public int Count => _records.Count;
}
