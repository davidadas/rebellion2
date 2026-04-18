using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

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

    // The game root — used by RouteWorkItemToManager to access game systems and config.
    private readonly GameRoot _game;

    // Manufacturing system — used to queue production when a ProductionWorkItem is dispatched.
    private readonly Rebellion.Systems.ManufacturingSystem _manufacturingManager;

    // Mission system — used to initiate character missions from ApplyMissionExecution.
    private readonly Rebellion.Systems.MissionSystem _missionManager;

    // RNG provider — passed through to MissionSystem for mission duration randomisation.
    private readonly Rebellion.Util.Common.IRandomNumberProvider _randomProvider;

    /// <summary>
    /// True while the worker is still running the two-phase startup sequence
    /// (states 1 and 2).  Once AtStartSetupMissions() returns true, startup is
    /// complete and Tick() should be called each frame instead.
    /// </summary>
    public bool IsInStartup => _missionCycleState == 1 || _missionCycleState == 2;

    // Movement system — used to issue fleet movement orders from ApplyFleetShortage.
    private readonly Rebellion.Systems.MovementSystem _movementManager;

    public HeavyAIWorker(
        Faction faction,
        int ownerSide,
        GameRoot game,
        Rebellion.Systems.ManufacturingSystem manufacturingManager = null,
        Rebellion.Systems.MovementSystem movementManager = null,
        Rebellion.Systems.MissionSystem missionManager = null,
        Rebellion.Util.Common.IRandomNumberProvider randomProvider = null
    )
    {
        _faction = faction;
        _game = game;
        _manufacturingManager = manufacturingManager;
        _movementManager = movementManager;
        _missionManager = missionManager;
        _randomProvider = randomProvider;
        OwnerSide = ownerSide;
        Workspace = new AIWorkspace
        {
            Owner = faction,
            GameRoot = game,
            // workspace+0x00 = faction side (1=Alliance, 2=Empire).
            // Read as *workspace by sub_419330 → sub_4f2090 → sub_53d660 to identify faction.
            FactionSide = ownerSide,
        };
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
        GameLogger.Log($"[AI] Record running: type={_workCursor?.TypeId}");
        _workCursor.TickCounter = 0;
        AIWorkItem workItem = _workCursor.Tick();
        if (workItem != null)
            GameLogger.Log($"[AI] Work item: type={workItem.TypeCode} from record={_workCursor?.TypeId}");

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

    /// <summary>
    /// Routes a completed work item to the game system that executes its action.
    /// Corresponds to FUN_00489ee0 → FUN_0041c690 → FUN_00435770 in the original,
    /// which routes items through the AI manager to per-faction handlers.
    ///
    /// In the original, routing enqueues items in inter-worker queues for deferred
    /// execution. In C# each worker is standalone so work items are executed directly.
    /// </summary>
    private void RouteWorkItemToManager(AIWorkItem workItem)
    {
        if (workItem is CapitalShipNameWorkItem nameItem)
        {
            ApplyCapitalShipName(nameItem);
            return;
        }

        if (workItem is FleetShortageWorkItem shortageItem)
        {
            ApplyFleetShortage(shortageItem);
            return;
        }

        if (workItem is AgentShortageWorkItem agentItem)
        {
            ApplyAgentShortage(agentItem);
            return;
        }

        if (workItem is ProductionWorkItem productionItem)
        {
            ApplyProduction(productionItem);
            return;
        }

        if (workItem is MissionExecutionWorkItem missionItem)
        {
            ApplyMissionExecution(missionItem);
            return;
        }

        // Other TypeCodes: implemented as the remaining strategy records are completed.
    }

    /// <summary>
    /// Handles a mission execution work item (TypeCode 0x201).
    ///
    /// Resolves the target system from item.SystemRef (typed reference, preferred) or
    /// item.EntityRef (legacy integer InternalId, MissionAssignmentEntry path).
    /// selects an available officer from this faction, chooses an appropriate MissionType
    /// based on the target planet's ownership, and calls MissionSystem.InitiateMission.
    ///
    /// Mission type heuristic:
    ///   Neutral planet  → Diplomacy  (recruit the system)
    ///   Enemy planet    → Espionage  (gather intel / weaken)
    ///   Own planet      → Recruitment (grow officer pool)
    ///
    /// In the original binary this routes through FUN_0042ecc0 (create mission instance),
    /// using PendingMissionTypeId and MissionParam from the MissionTargetEntry. Since
    /// FleetTarget.GetMissionInfo() is not yet wired (abstract method), those fields are
    /// always 0 and the heuristic above substitutes for them.
    /// </summary>
    private void ApplyMissionExecution(MissionExecutionWorkItem item)
    {
        if (_missionManager == null || _faction == null || _randomProvider == null)
            return;

        if (item.Workspace == null || item.SystemRef?.System == null)
            return;

        SystemAnalysisRecord sysRec = item.SystemRef;
        string factionId = _faction.InstanceID;

        if (item.TargetPlanet == null)
            return;

        Planet target = item.TargetPlanet;

        // Determine mission type from target ownership.
        string targetOwner = target.GetOwnerInstanceID();
        MissionType missionType;
        if (targetOwner == null)
            missionType = MissionType.Diplomacy;
        else if (targetOwner != factionId)
            missionType = MissionType.Espionage;
        else
            missionType = MissionType.Recruitment;

        // Verify the mission is valid before committing.
        if (!_missionManager.CanCreateMission(missionType, factionId, target, _randomProvider))
            return;

        // Pick the first available officer for this faction.
        List<Officer> officers = _faction.GetAvailableOfficers();
        if (officers.Count == 0)
            return;

        _missionManager.InitiateMission(missionType, officers[0], target, _randomProvider);
    }

    /// <summary>
    /// Handles a production work item (TypeCode 0x201).
    /// Calls ManufacturingSystem.Enqueue() to queue the unit at the target planet.
    /// </summary>
    private void ApplyProduction(ProductionWorkItem item)
    {
        if (item.TargetPlanet == null || item.Unit == null)
            return;

        _manufacturingManager?.Enqueue(
            item.TargetPlanet,
            item.Unit,
            item.Destination ?? item.TargetPlanet
        );
    }

    /// <summary>
    /// Handles an agent shortage work item (TypeCode 0x210/0x214).
    ///
    /// Routing (FUN_00435770 / AI.md Section 432):
    ///   Work item's +0x20 (Side) determines routing:
    ///   - Side 1: opposing worker's vtable[8] (FUN_00575590 = always-accept), then
    ///             own worker's vtable[7] (FUN_00489d70 = insert into priority tree at worker+0x4).
    ///   - Side 2: symmetric.
    ///   LargeSelectionRecord slot 8 ALWAYS returns 1 (unconditional accept).
    ///   Slot 7 (FUN_00489d70): inserts into priority tree, then calls FUN_005357e0
    ///   which adds to the worker's pending queue at worker+0x4.
    ///
    /// VERIFIED from disassembly (FUN_00435770, FUN_00489d70, FUN_004be160,
    /// FUN_004bdd00, FUN_00484f90, AI.md Sections 39/377/432/433):
    /// TypeCode 0x214 = production package (PRIMARY PRODUCTION ORDER), NOT agent movement.
    /// AI.md Section 39 confirms: carries seed_entity_id [0x90,0x98) and secondary_entity_id
    /// [0xa4,0xa6) for scheduling unit production at Training Facilities.
    /// The +0x4 priority tree drain is in AI.md Section 433 [INCOMPLETE].
    /// APPROXIMATION: officers moved as fallback; correct action requires redesigning
    /// AgentShortageWorkItem to carry production order data.
    /// </summary>
    private void ApplyAgentShortage(AgentShortageWorkItem item)
    {
        if (item.TargetSystem == null || _movementManager == null || _faction == null)
            return;

        string factionId = _faction.InstanceID;
        int count = System.Math.Max(1, item.AgentCount);

        // Find a destination planet in the target system.
        // Prefer own-faction planet; fall back to any planet.
        Planet destination =
            item.TargetSystem.Planets.FirstOrDefault(p => p.GetOwnerInstanceID() == factionId)
            ?? item.TargetSystem.Planets.FirstOrDefault();

        if (destination == null)
            return;

        var galaxy = _game?.Galaxy;
        if (galaxy == null)
            return;

        // Move up to AgentCount available officers to the target system.
        // Officers come from any system other than the destination.
        int moved = 0;
        foreach (var system in galaxy.GetChildren<PlanetSystem>(s => s != item.TargetSystem))
        {
            if (moved >= count)
                break;
            foreach (var planet in system.Planets)
            {
                if (moved >= count)
                    break;
                foreach (var officer in planet.Officers.ToList())
                {
                    if (moved >= count)
                        break;
                    if (officer.GetOwnerInstanceID() != factionId)
                        continue;
                    if (!officer.IsMovable())
                        continue;
                    if (officer.IsCaptured || officer.IsKilled)
                        continue;
                    _movementManager.RequestMove(officer, destination);
                    moved++;
                }
            }
        }
    }

    /// <summary>
    /// Handles a fleet shortage work item (TypeCode 0x200).
    ///
    /// In the original binary (FUN_00489ee0 → FUN_0041c690 → FUN_00435770), the work
    /// item is routed to the AI manager which enqueues it for the opposing worker's
    /// processing pipeline. The fleet assignment sub-machine (MissionTargetEntry.Dispatch)
    /// then handles the actual unit deployment.
    ///
    /// BLOCKED: the fleet assignment pipeline (workspace FleetAssignmentTable /
    /// FleetAvailabilityTable / MissionTargetEntry dispatch chain) is not yet fully
    /// implemented. Until it is, fleet shortage work items are acknowledged but have
    /// no game effect.
    /// </summary>
    private void ApplyFleetShortage(FleetShortageWorkItem item)
    {
        if (item.TargetSystem == null || _movementManager == null || _faction == null)
            return;

        string factionId = _faction.InstanceID;
        var galaxy = _game?.Galaxy;
        if (galaxy == null)
            return;

        // Find an idle friendly fleet at any system other than the shortage target.
        // The binary (FUN_004dbfb0) uses a complex scored selection; here we pick the
        // first available fleet with capital ships that isn't already at the target.
        Fleet candidate = null;
        foreach (var system in galaxy.GetChildren<PlanetSystem>(s => s != item.TargetSystem))
        {
            foreach (var planet in system.Planets)
            {
                foreach (var fleet in planet.Fleets)
                {
                    if (fleet.GetOwnerInstanceID() != factionId)
                        continue;
                    if (!fleet.IsMovable())
                        continue;
                    if (fleet.CapitalShips.Count == 0)
                        continue;
                    candidate = fleet;
                    break;
                }
                if (candidate != null)
                    break;
            }
            if (candidate != null)
                break;
        }

        if (candidate == null)
            return;

        // Move to the first own-faction planet in the target system, or any planet.
        Planet destination =
            item.TargetSystem.Planets.FirstOrDefault(p => p.GetOwnerInstanceID() == factionId)
            ?? item.TargetSystem.Planets.FirstOrDefault();

        if (destination == null)
            return;

        _movementManager.RequestMove(candidate, destination);
    }

    /// <summary>
    /// Applies a capital ship name assignment.
    /// Resolves the resource ID to a name string from config and sets DisplayName.
    /// If the name pool array is empty (data not yet loaded), the ship is left unnamed.
    /// </summary>
    private void ApplyCapitalShipName(CapitalShipNameWorkItem item)
    {
        if (item.Ship == null || _game?.Config?.AI?.ShipNaming == null)
            return;

        string name = ResolveShipName(item.NameResourceId, item.Side, _game.Config.AI.ShipNaming);
        if (name != null)
            item.Ship.DisplayName = name;
    }

    /// <summary>
    /// Maps a text resource ID from the original binary to a name string using the
    /// configured name pools. Returns null if the ID is out of range or the pool
    /// array for that range is empty.
    ///
    /// Resource-ID ranges (from FUN_004d1ea0):
    ///   Empire  pool 1: 0x5100..0x5126 (39 entries)
    ///   Empire  pool 2: 0x5160..0x5181 (34 entries)
    ///   Empire  pool 3: 0x51c0..0x51df (32 entries)
    ///   Alliance pool 1: 0x5200..0x5213 (20 entries)
    ///   Alliance pool 2: 0x5260..0x5282 (35 entries)
    ///   Alliance pool 3: 0x52c0..0x52cf (16 entries)
    /// </summary>
    private static string ResolveShipName(
        int nameResId,
        int side,
        GameConfig.ShipNamingConfig pools
    )
    {
        if (side == 1) // Alliance
        {
            if (nameResId >= 0x5200 && nameResId < 0x5214)
            {
                int idx = nameResId - 0x5200;
                return idx < pools.AlliancePool1.Length ? pools.AlliancePool1[idx] : null;
            }
            if (nameResId >= 0x5260 && nameResId < 0x5283)
            {
                int idx = nameResId - 0x5260;
                return idx < pools.AlliancePool2.Length ? pools.AlliancePool2[idx] : null;
            }
            if (nameResId >= 0x52c0 && nameResId < 0x52d0)
            {
                int idx = nameResId - 0x52c0;
                return idx < pools.AlliancePool3.Length ? pools.AlliancePool3[idx] : null;
            }
        }
        else // Empire (side == 0)
        {
            if (nameResId >= 0x5100 && nameResId < 0x5127)
            {
                int idx = nameResId - 0x5100;
                return idx < pools.EmpirePool1.Length ? pools.EmpirePool1[idx] : null;
            }
            if (nameResId >= 0x5160 && nameResId < 0x5182)
            {
                int idx = nameResId - 0x5160;
                return idx < pools.EmpirePool2.Length ? pools.EmpirePool2[idx] : null;
            }
            if (nameResId >= 0x51c0 && nameResId < 0x51e0)
            {
                int idx = nameResId - 0x51c0;
                return idx < pools.EmpirePool3.Length ? pools.EmpirePool3[idx] : null;
            }
        }
        return null;
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
