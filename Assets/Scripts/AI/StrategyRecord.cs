using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Util.Common;

// Abstract base for all 14 strategy record types that populate the heavy AI worker's
// BST-ordered work list. Corresponds to the common layout established by
// FUN_004e4320 (init_advanced_strategy_table_record) and the shared vtable structure
// (slots 3-6) present across all 14 concrete type vtables.
//
// Field layout (from FUN_004e4320 and class header):
//   +0x18 = TypeId           (written by FUN_005f3b30 called from FUN_004ebce0)
//   +0x1c = ActiveState      (sub-object; FUN_005f2ef0 reads its value; 1 = active)
//   +0x20 = ReadyFlag        (set to 1 when record has completed a cycle, ready to reset)
//   +0x24 = Capacity         (type-specific; set by each constructor)
//   +0x28 = TickSubObject    (sub-object inited by FUN_005f2ee0; tracks per-tick state)
//   +0x2c = Workspace        (pointer to AIWorkspace; set by slot-3 initialize)
//   +0x30 = OwnerSide        (0 or 1; identifies which faction this record belongs to)
//   +0x34 = TickCounter      (zeroed each time slot-5 is called; used internally)
//   +0x38 = Phase            (outer state machine phase; drives the outer switch)
//   +0x3c = SubState         (sub-state within current phase)
public abstract class StrategyRecord
{
    // --- Shared base fields (FUN_004e4320) ---

    /// <summary>
    /// Numeric type ID (1-14). Written during construction. Drives the factory switch.
    /// </summary>
    public int TypeId { get; private set; }

    /// <summary>
    /// Active state of this record. 1 = active (ready for slot-5 to produce work).
    /// Read by FUN_005f2ef0. Written to 0 during startup (apply_table_values_to).
    /// Written to 1 when the strategy record pipeline marks this record ready.
    /// </summary>
    public int ActiveState { get; set; }

    /// <summary>
    /// Set to 1 when the record has completed a production cycle and can be reset.
    /// Cleared (0) at the start of each new cycle. Checked by the mission-cycle batch
    /// loop to decide when to advance the cursor past this record.
    /// </summary>
    public int ReadyFlag { get; set; }

    /// <summary>
    /// Type-specific capacity constant. Set per type in the constructor.
    /// Controls how many issue entities this record can hold simultaneously.
    /// </summary>
    public int Capacity { get; private set; }

    /// <summary>
    /// Reference to the shared AIWorkspace (scratchBlock). Set by slot-3 Initialize().
    /// All inner state machines read analysis data from this workspace.
    /// </summary>
    public AIWorkspace Workspace { get; private set; }

    /// <summary>
    /// The faction side index (0 = Rebel, 1 = Empire). Set during construction.
    /// Read by inner handlers to filter entities to the correct side.
    /// </summary>
    public int OwnerSide { get; private set; }

    /// <summary>
    /// Tick counter. Zeroed at the start of each slot-5 call (mission-cycle batch loop
    /// zeroes it before calling Tick). Used by inner state machines to limit work per tick.
    /// </summary>
    public int TickCounter { get; set; }

    // --- Outer state machine fields ---

    /// <summary>
    /// Outer phase variable (+0x38). Drives the outer switch in each type's slot-5.
    /// Values are type-specific (e.g. 0x3ea, 0x3eb, 0x3ec, 0x3ed, 0x3ee, 0x3ef, etc.).
    /// </summary>
    protected int Phase { get; set; }

    /// <summary>
    /// Sub-state within the current phase (+0x3c). Used alongside Phase for
    /// multi-level inner state machines (e.g. types 1, 2, 3, 6, 7, 11).
    /// </summary>
    protected int SubState { get; set; }

    protected StrategyRecord(int typeId, int capacity, int ownerSide)
    {
        TypeId = typeId;
        Capacity = capacity;
        OwnerSide = ownerSide;
        ActiveState = 0;
        ReadyFlag = 0;
        Phase = 0;
        SubState = 0;
        TickCounter = 0;
    }

    /// <summary>
    /// Slot 3 — initialize. Called once during startup (apply_table_values_to) after
    /// construction, passing the shared AIWorkspace. Stores the workspace reference
    /// and performs any type-specific initialization that requires it.
    /// FUN_004bfa60 (used by most types) or FUN_0041c290 / FUN_0049dff0 (special types).
    /// </summary>
    public virtual void Initialize(AIWorkspace workspace)
    {
        Workspace = workspace;
    }

    /// <summary>
    /// Slot 4 — validity reset (FUN_004e1460). Called during the cursor-advance phase
    /// (state 5 of the mission cycle) on every record in the list.
    ///
    /// FUN_004e1460 exact behavior:
    ///   if ActiveState != 1 AND ActiveState != 2:
    ///     ActiveState = 1    ← primes this record to produce work
    ///     ReadyFlag   = 0    ← cleared; record starts a fresh cycle
    ///
    /// State 1 and 2 records are left untouched (already in progress).
    /// </summary>
    public void Reset()
    {
        if (ActiveState != 1 && ActiveState != 2)
        {
            ActiveState = 1;
            ReadyFlag = 0;
            GameLogger.Log($"[AI] Record activated: type={TypeId}");
        }
    }

    /// <summary>
    /// Slot 5 — type-specific tick. Called each time this record is the active cursor
    /// in the mission-cycle batch loop. Returns a non-null AIWorkItem when the record
    /// has produced a schedulable action; returns null while still working.
    ///
    /// The outer guard is identical across all types: if ActiveState != 1, reset and
    /// return null. The inner dispatch is type-specific.
    /// </summary>
    public abstract AIWorkItem Tick();

    /// <summary>
    /// Slot 6 — record result (FUN_004cbd60). Called when the dispatched work item
    /// was NOT routed to the manager (entity.vtable[7] returned 0). Marks the record
    /// as processed and frees the work item.
    /// </summary>
    public virtual void RecordResult(AIWorkItem workItem)
    {
        // Default: mark record done for this cycle.
        ReadyFlag = 1;
    }

    /// <summary>
    /// Performs the active-state guard that every type's slot-5 outer function checks
    /// first. If ActiveState != 1: resets Phase, SubState, ReadyFlag=1 and returns true
    /// (caller should return null). Returns false when the record is active and should
    /// proceed to the inner state machine.
    ///
    /// Types 4, 8, 9 do NOT reset Phase/SubState on guard failure (they re-enter);
    /// all others do. Subclasses that differ override this.
    /// </summary>
    /// <summary>
    /// The active-state guard that every type's slot-5 outer function checks first.
    /// If ActiveState != 1: resets Phase=0, SubState=0, ReadyFlag=1 and returns true
    /// (caller should return null immediately). Returns false when active and ready.
    ///
    /// Types 4, 8, 9 skip the Phase/SubState reset on guard failure — they override this.
    /// </summary>
    protected virtual bool ActiveGuardFails()
    {
        if (ActiveState != 1)
        {
            Phase = 0;
            SubState = 0;
            ReadyFlag = 1;
            GameLogger.Log($"[AI] Record inactive: type={TypeId}, ActiveState={ActiveState}");
            return true;
        }
        return false;
    }
}

/// <summary>
/// Represents a work item produced by a strategy record's Tick(). The mission-cycle
/// batch loop receives these from slot-5, dispatches them via the entity's own dispatch
/// method, and either routes them to the AI manager or records them on the strategy record.
///
/// Corresponds to astruct_194 (the various work-item entity types) returned by slot-5.
/// </summary>
public abstract class AIWorkItem
{
    /// <summary>
    /// Type code of this work item. Checked by the mission-cycle loop after dispatch:
    /// if TypeCode == 0x203, the batch counter is NOT incremented (special scheduler type).
    /// Corresponds to entity.vtable[3]() return value.
    /// </summary>
    public abstract int TypeCode { get; }

    /// <summary>
    /// Dispatch this work item. Returns true if the item should be routed to the AI
    /// manager (FUN_00489ee0 path); returns false if the strategy record should handle
    /// it directly (RecordResult path). Corresponds to entity.vtable[7](result).
    /// </summary>
    public abstract bool Dispatch(out AIDispatchResult result);
}

/// <summary>
/// TypeCode 0x200 work item produced by Type 1 (LocalShortageGeneratorType1Record).
/// Represents a fleet shortage issue for a specific system: the AI needs to send
/// units (regiments, capital ships, or starfighters) to cover the shortage there.
///
/// In the original binary this is the result of FUN_004db1e0 / FUN_004dab90:
/// a package of unit reference nodes pointing to available fleet assets.
/// RouteWorkItemToManager dispatches this to the fleet movement / mission system.
/// </summary>
public class FleetShortageWorkItem : AIWorkItem
{
    public override int TypeCode => 0x200;

    /// <summary>The system that has a fleet/troop shortage.</summary>
    public PlanetSystem TargetSystem { get; }

    /// <summary>Faction side (0=Empire, 1=Alliance) that owns the shortage.</summary>
    public int Side { get; }

    public FleetShortageWorkItem(PlanetSystem target, int side)
    {
        TargetSystem = target;
        Side = side;
    }

    public override bool Dispatch(out AIDispatchResult result)
    {
        result = new AIDispatchResult();
        return true; // route to manager for fleet assignment
    }
}

/// <summary>
/// Work item produced by MissionAssignmentEntry.Dispatch() (vtable[11] in original).
/// TypeCode 0x201 = AssembleFinalWorkItem (FUN_004bc810).
/// TypeCode 0x240 = ProcessTerminalWorkItem (FUN_0047a7b0).
/// TypeCode 0x250 = ProcessReturnWorkItemPhase (FUN_0047a6a0).
/// Carries the mission target system (typed reference) and workspace for RouteWorkItemToManager.
/// Legacy integer path retained for MissionAssignmentEntry callers not yet migrated.
/// </summary>
public class MissionExecutionWorkItem : AIWorkItem
{
    // TypeCode 0x201 for standard mission dispatch; other codes for specific paths.
    public override int TypeCode => 0x201;

    /// <summary>System analysis record for the mission target.</summary>
    public SystemAnalysisRecord SystemRef { get; }

    /// <summary>Workspace reference for mission creation.</summary>
    public AIWorkspace Workspace { get; }

    /// <summary>Characters selected during Stage evaluation for this mission.</summary>
    public List<CharacterAnalysisRecord> SelectedCharacters { get; } = new List<CharacterAnalysisRecord>();

    /// <summary>Specific planet within the target system, resolved from the planet sub-object filter. Null when no planet refinement was performed.</summary>
    public Planet TargetPlanet { get; set; }

    public MissionExecutionWorkItem(SystemAnalysisRecord systemRef, AIWorkspace workspace)
    {
        SystemRef = systemRef;
        Workspace = workspace;
    }

    public override bool Dispatch(out AIDispatchResult result)
    {
        result = new AIDispatchResult();
        return true; // route to manager for mission creation
    }
}

/// <summary>
/// Work item produced by ProductionTrackingEntry.Dispatch() (vtable+0x14 in original).
/// Carries the manufacturing request: which unit to build, at which planet, for which faction.
/// RouteWorkItemToManager dispatches this to ManufacturingSystem.Enqueue().
/// TypeCode 0x201 matches the original production work item type.
/// </summary>
public class ProductionWorkItem : AIWorkItem
{
    public override int TypeCode => 0x201;

    /// <summary>Planet where production should be queued.</summary>
    public Planet TargetPlanet { get; }

    /// <summary>Unit template to manufacture.</summary>
    public IManufacturable Unit { get; }

    /// <summary>Destination planet for the completed unit.</summary>
    public Planet Destination { get; }

    public ProductionWorkItem(Planet planet, IManufacturable unit, Planet destination)
    {
        TargetPlanet = planet;
        Unit = unit;
        Destination = destination;
    }

    public override bool Dispatch(out AIDispatchResult result)
    {
        result = new AIDispatchResult();
        return true; // route to manager for manufacturing
    }
}

/// <summary>
/// TypeCode 0x210/0x214 work item produced by Type 1 agent shortage methods.
/// 0x214 = CreateAgentShortageItem (FUN_004dbd60): agent assignment request.
/// 0x210 = FinalizeAgentShortageItem (FUN_004dbea0): agent finalization with count.
/// In the original, these carry agent entity references and target system data.
/// RouteWorkItemToManager dispatches to the mission/character assignment system.
/// </summary>
// TypeCode 0x211 = entity transfer follow-up (FUN_004cd800 for Type 10 PhaseB).
// Uses same class as AgentShortageWorkItem — TypeCode stored dynamically.

public class AgentShortageWorkItem : AIWorkItem
{
    private readonly int _typeCode;
    public override int TypeCode => _typeCode;

    /// <summary>Target system for the shortage.</summary>
    public PlanetSystem TargetSystem { get; }

    /// <summary>Number of agents requested for the assignment.</summary>
    public int AgentCount { get; }

    /// <summary>Faction side (0=Empire, 1=Alliance).</summary>
    public int Side { get; }

    public AgentShortageWorkItem(int typeCode, PlanetSystem target, int agentCount, int side)
    {
        _typeCode = typeCode;
        TargetSystem = target;
        AgentCount = agentCount;
        Side = side;
    }

    public override bool Dispatch(out AIDispatchResult result)
    {
        result = new AIDispatchResult();
        return true; // route to manager for agent assignment
    }
}

/// <summary>
/// Result record populated during dispatch. Corresponds to astruct_608 (local_result)
/// in the mission-cycle batch loop. Initialized by FUN_0051f750 before each dispatch call.
/// </summary>
public class AIDispatchResult
{
    public int Field0 { get; set; }
    public int Field4 { get; set; }
}

/// <summary>
/// Type-0x203 work item produced by CapitalShipNameGeneratorRecord (Type 14).
/// Carries a capital-ship name assignment: the ship to name, the text-resource ID
/// for the name string, and the owner side (0=Empire, 1=Alliance).
///
/// TypeCode = 0x203 means the HeavyAIWorker batch counter is NOT incremented after
/// this item is dispatched (special scheduler type — handled by the main loop).
///
/// Corresponds to the work item created by FUN_004f5060(0x203) in FUN_004d1ea0.
/// The name resource IDs are drawn from the side-specific naming pools:
///   Empire  pool-1: 0x5100..0x5126 (Hydra-class, 39 names)
///   Empire  pool-2: 0x5160..0x5181 (Master-class, 34 names)
///   Empire  pool-3: 0x51c0..0x51df (Judicator-class, 32 names)
///   Alliance pool-1: 0x5200..0x5213 (Swift-class, 20 names)
///   Alliance pool-2: 0x5260..0x5282 (Deliverance-class, 35 names)
///   Alliance pool-3: 0x52c0..0x52cf (Liberty-class, 16 names)
/// </summary>
public class CapitalShipNameWorkItem : AIWorkItem
{
    public override int TypeCode => 0x203;

    // The capital ship that needs a name assigned.
    public CapitalShip Ship { get; }

    // Text resource ID for the selected name string (e.g. 0x5100 = first Empire pool-1 name).
    public int NameResourceId { get; }

    // Faction side (paVar5->field10_0x20 in FUN_004d1ea0): 0=Empire, 1=Alliance.
    public int Side { get; }

    public CapitalShipNameWorkItem(CapitalShip ship, int nameResourceId, int side)
    {
        Ship = ship;
        NameResourceId = nameResourceId;
        Side = side;
    }

    /// <summary>
    /// Returns true: name work items ARE dispatched to the AI manager
    /// (RouteWorkItemToManager path), but the batch counter is not incremented
    /// because TypeCode == 0x203.
    /// </summary>
    public override bool Dispatch(out AIDispatchResult result)
    {
        result = new AIDispatchResult();
        return true;
    }
}
