using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.SceneGraph;

// Corresponds to astruct_195 (the scratchBlock) embedded at offset +0x84 within
// LargeSelectionRecord (HeavyAIWorker). Holds all shared analysis state that the
// galaxy analysis pipeline writes and that strategy records read.
//
// The scratchBlock is 1076 bytes (0x434) in the original. Field offsets below
// are relative to the scratchBlock base (i.e., HeavyAIWorker + 0x84).
public class AIWorkspace
{
    // +0x04 — Status flags. Bit layout (from calibration and galaxy analysis):
    //   bit 0x1       = side maintenance summary present (galaxy analysis state 1)
    //   bit 0x80000000 = fleet analysis data ready (set in state 5)
    //   bit 0x20000000 = fleet score refresh needed
    //   bit 0x40000000 = fleet analysis secondary refresh needed
    //   bit 0x10000000 = character analysis refresh needed
    public int StatusFlags { get; set; }

    // +0x08 — Pending supply bitmask. Each bit represents a supply slot being tracked.
    //   Bit 7 (0x80) = Type-1 local shortage slot
    //   Bit 0 (0x01) = Type-3 local shortage slot
    public int PendingSupplyBitmask { get; set; }

    // +0x0c — Entity target type byte.
    //   0x80 = fleet-targeted context
    //   0x40 = agent-targeted context
    public int EntityTargetType { get; set; }

    // +0x10 — Next bit-selection mask. Rotates right through 0x80→0x40→…→0x01→0x80.
    //   Drives FUN_00419160 (AdvanceBitSelection): when PendingSupplyBitmask==0 this
    //   value is copied into EntityTargetType and then shifted right by 1 (wrapping to
    //   0x80 when it reaches 0). Initial value 0x80 = begin at the fleet-targeted slot.
    public int NextMask { get; set; } = 0x80;

    // +0xa4 — Production manager entity reference (written in galaxy analysis state 1).
    public ISceneNode ProductionManagerEntity { get; set; }

    // +0x184 — Fleet total capacity (written by LargeSelectionRecord slot 25).
    public int FleetTotalCapacity { get; set; }

    // +0x188 — Fleet assigned capacity (written by LargeSelectionRecord slot 26).
    public int FleetAssignedCapacity { get; set; }

    // +0x1ac — Fleet score array (40 ints, zeroed during calibration when bit 0x20000000 set).
    // Indexed by fleet entity or system index to store analysis scores.
    public int[] FleetScores { get; } = new int[40];

    // +0x1d0 — Agent total capacity.
    public int AgentTotalCapacity { get; set; }

    // +0x1d4 — Agent assigned capacity.
    public int AgentAssignedCapacity { get; set; }

    // +0x214 — Fleet analysis accumulator A (divided by 5+1 in calibration state 5).
    public int FleetAnalysisAccumulatorA { get; set; }

    // +0x218 — Fleet analysis accumulator B (divided by 6+1 in calibration state 5).
    public int FleetAnalysisAccumulatorB { get; set; }

    // +0x24c — Fleet analysis secondary score array (30 ints, zeroed during calibration).
    public int[] FleetSecondaryScores { get; } = new int[30];

    // +0x2c4 — Character score array (20 ints, zeroed during calibration when bit 0x10000000 set).
    public int[] CharacterScores { get; } = new int[20];

    // --- Galaxy analysis pipeline state ---

    // +0x17c — Galaxy analysis phase (FUN_00417a50 state variable).
    //   Default/invalid → 1. Advances 1→2→3→4→5→6. Returns done when 6 completes.
    public int GalaxyAnalysisPhase { get; set; } = 1;

    // +0x180 — Calibration sub-machine state (FUN_00417cb0 state variable).
    //   Default/invalid → 4. Valid states: 1, 4, 5.
    public int CalibrationState { get; set; } = 4;

    // +0x18c — System score accumulator (incremented by 10 per system node in galaxy
    //   analysis state 3, used to weight the analysis priority).
    public int SystemScoreAccumulator { get; set; }

    // --- Analysis lists ---

    // System analysis records (built in galaxy analysis state 2, FUN_004306c0).
    // Each entry corresponds to a PlanetSystem and holds per-system AI stats.
    public List<SystemAnalysisRecord> SystemAnalysis { get; } = new List<SystemAnalysisRecord>();

    // Fleet/capital ship analysis list (built in state 3, FUN_00430200).
    public List<FleetAnalysisRecord> FleetAnalysis { get; } = new List<FleetAnalysisRecord>();

    // Character/officer analysis list (built in state 5, FUN_004032c0).
    public List<CharacterAnalysisRecord> CharacterAnalysis { get; } =
        new List<CharacterAnalysisRecord>();

    // +0xc0 — Fleet analysis sub-object (vtable[4] called in calibration state 5).
    public object FleetAnalysisSubObject { get; set; }

    // +0x104 — Secondary fleet analysis sub-object (vtable[4] called in calibration state 5).
    public object FleetAnalysisSubObjectB { get; set; }

    // Calibration cursor — tracks position within SystemAnalysis during calibration state 1.
    // Corresponds to scratchBlock+0x178 (FUN_00417cb0 state 1 cursor pointer).
    public int CalibrationCursor { get; set; }

    // Faction owning this workspace (used to resolve entity side during analysis).
    public Faction Owner { get; set; }

    // -------------------------------------------------------------------------
    // Mission assignment tracking (used by MissionAssignmentRecord / Type 4).
    //
    // workspace+0xa8 — mission table (AutoClass34 *). Each entry tracks a pending
    //   mission create or cancel request. Navigated by ID via get_table_by_id.
    //   Validity check (FUN_00475fd0): entry.PendingMissionTypeId != 0 OR entry.PendingCancelMissionId != 0.
    //
    // workspace+0xd8 — entity target table. Each entry is a game entity with a
    //   pending mission assignment. Same validity check as mission table.
    //
    // workspace+0x314 — pending cancel ID (FUN_004d18f0: if non-zero, cancel and clear).
    // workspace+0x318 — pending mission type ID to create (if non-zero, create and clear).
    // workspace+0x31c — parameter for the pending create.
    // -------------------------------------------------------------------------

    public List<MissionAssignmentEntry> MissionTable { get; } = new List<MissionAssignmentEntry>();

    public List<MissionTargetEntry> EntityTargetTable { get; } = new List<MissionTargetEntry>();

    // -------------------------------------------------------------------------
    // Selected-target tracking (used by StrategyRecordType9).
    //
    // workspace+0x11c — selected target table. Each entry represents a game entity
    //   (planet, system, or fleet) that the AI has designated as an attack or scout
    //   target. Walked by Type 9's state 2 (FUN_004ce6c0) and dispatched in state 3
    //   (FUN_004ce720 → FUN_004737e0).
    // -------------------------------------------------------------------------

    public List<SelectedTargetEntry> SelectedTargetTable { get; } = new List<SelectedTargetEntry>();

    // -------------------------------------------------------------------------
    // workspace+0x44 — fleet assignment sub-object.
    // Used by FUN_004ceb30 (Type 8 state 3) which calls FUN_004f4cc0(this=workspace+0x44,
    // arg=&entry.StatusFlags) to verify a pre-existing fleet assignment condition before
    // creating new fleet issue records.
    // -------------------------------------------------------------------------

    public object FleetAssignmentSubObject { get; set; }

    // Pending mission create/cancel written by the mission scheduling system.
    public int PendingMissionCancelId { get; set; } // workspace+0x314
    public int PendingMissionTypeId { get; set; } // workspace+0x318
    public int PendingMissionParameter { get; set; } // workspace+0x31c

    // Sequential ID generator for MissionTable and EntityTargetTable entries.
    public int NextMissionId { get; set; } = 1;

    // -------------------------------------------------------------------------
    // Production automation tracking (used by ProductionAutomationRecord / Type 12).
    //
    // workspace+0xec — production tracking table. Each entry is a manufacturing
    //   job the AI is managing. Walked by Type 12 SubState 4 (FUN_004c7ab0).
    //
    // workspace+0x354 — ID of the most-recently-cancelled production tracking entry.
    //   Non-zero: FUN_0042e670 is called on it and the field is cleared (SubState 1).
    //
    // workspace+0x358 — ID of the most-recently-completed production tracking entry.
    //   Non-zero: remapped via FUN_0042e630, status checked via vtable+0xc; if Complete,
    //   the completion counter is incremented and the field is cleared (SubState 1).
    //
    // workspace+0x35c — completion counter array, indexed by ManufacturingType.
    //   Incremented by FUN_0041ad80 when a production job finishes (SubState 1).
    //   Array size 16 covers all present ManufacturingType values with margin.
    //
    // workspace+0x204 — signed int sector-search state variable used by
    //   FUN_004cea70 (Type 8 SubState 1). Negative = scan EntityTargetTable for
    //   flag 0x4000000; positive = call FUN_0042ea50_find_sector; zero = no-op.
    // -------------------------------------------------------------------------

    public List<ProductionTrackingEntry> ProductionTrackingTable { get; } =
        new List<ProductionTrackingEntry>();

    // workspace+0x354: ID of the production tracking entry to cancel this cycle.
    public int PendingProductionCancelId { get; set; }

    // workspace+0x358: ID of the production tracking entry that just completed.
    public int PendingProductionCompleteId { get; set; }

    // workspace+0x35c: per-type completion counter array (indexed by ManufacturingType).
    public int[] ProductionCompletionCounters { get; } = new int[16];

    // Sequential ID generator for ProductionTrackingTable entries.
    public int NextProductionId { get; set; } = 1;

    // workspace+0x204: sector-search state variable for Type 8 (FUN_004cea70).
    // Negative: scan EntityTargetTable for entities with flag bit 0x4000000 set.
    // Positive: call find-sector with this value as parameter.
    // Zero: do nothing this cycle.
    public int SectorSearchState { get; set; }

    // -------------------------------------------------------------------------
    // Capital ship naming flags (used by CapitalShipNameGeneratorRecord / Type 14).
    //
    // Maps CapitalShip.InstanceID → naming flag word:
    //   bit 0x4000     = ship is unnamed (needs a name assigned by the AI)
    //   bit 0x10       = pool-1 first (Hydra-class / Swift-class names)
    //   bit 0x20       = pool-2 first (Master-class / Deliverance-class names)
    //   bit 0x40       = pool-3 first (Judicator-class / Liberty-class names)
    //   bits 0x8002800 = excluded from AI naming (already named or flagged)
    //
    // Ships absent from this dictionary are not unnamed (0x4000 clear) and
    // will be skipped by ShipMeetsNamingCriteria.
    // -------------------------------------------------------------------------
    public Dictionary<string, int> CapitalShipNamingFlags { get; } = new Dictionary<string, int>();

    /// <summary>
    /// Advances the workspace bit-selection state (FUN_00419160).
    ///
    /// Two behaviors depending on PendingSupplyBitmask:
    ///
    /// If PendingSupplyBitmask == 0:
    ///   Copy NextMask into EntityTargetType, then shift NextMask right by 1.
    ///   If NextMask reaches 0 after the shift, wrap it back to 0x80.
    ///
    /// If PendingSupplyBitmask != 0:
    ///   Walk EntityTargetType right (>>1, wrapping to 0x80 when 0) until
    ///   (PendingSupplyBitmask &amp; EntityTargetType) != 0, then clear that bit:
    ///   PendingSupplyBitmask &amp;= ~EntityTargetType.
    ///
    /// Precondition: called only when the bit-select pipeline is active.
    /// </summary>
    public void AdvanceBitSelection()
    {
        uint bitmask = (uint)PendingSupplyBitmask;
        if (bitmask == 0)
        {
            EntityTargetType = NextMask;
            uint next = (uint)NextMask >> 1;
            NextMask = (int)(next == 0 ? 0x80u : next);
        }
        else
        {
            uint ett = (uint)EntityTargetType;
            while ((bitmask & ett) == 0)
            {
                ett >>= 1;
                if (ett == 0)
                    ett = 0x80;
                EntityTargetType = (int)ett;
            }
            PendingSupplyBitmask = (int)(~ett & bitmask);
        }
    }

    /// <summary>
    /// Resets the workspace to its initial state for a new galaxy analysis cycle.
    /// Called at the start of each calibration pass.
    /// </summary>
    public void ResetForGalaxyAnalysis()
    {
        GalaxyAnalysisPhase = 1;
        CalibrationState = 4;
        CalibrationCursor = 0;
        SystemScoreAccumulator = 0;
        SystemAnalysis.Clear();
        FleetAnalysis.Clear();
        CharacterAnalysis.Clear();
    }
}

/// <summary>
/// Per-system analysis record. Built during galaxy analysis state 2 (FUN_004306c0).
/// Corresponds to the 0xcc-byte system analysis records in the system analysis list.
/// </summary>
public class SystemAnalysisRecord
{
    public PlanetSystem System { get; set; }
    public PerSystemStats Stats { get; set; } = new PerSystemStats();

    // Score fields used by calibration state 1 (FUN_00431860 → FUN_0041af90 scoring).
    public int SystemScore { get; set; }
    public int ScoringFlags { get; set; }
}

/// <summary>
/// Per-fleet analysis record. Built during galaxy analysis state 3 (FUN_00430200).
/// Corresponds to the fleet entity cross-link records.
/// </summary>
public class FleetAnalysisRecord
{
    public Fleet Fleet { get; set; }
    public FleetUnitStats Stats { get; set; } = new FleetUnitStats();
    public int FleetScore { get; set; }
}

/// <summary>
/// Per-character (officer/agent) analysis record. Built during galaxy analysis state 5.
/// </summary>
public class CharacterAnalysisRecord
{
    public Officer Officer { get; set; }
    public int CharacterScore { get; set; }
    public int CapabilityFlags { get; set; }
}

/// <summary>
/// One entry in AIWorkspace.MissionTable (workspace+0xa8).
/// Each entry represents a tracked pending-mission record:
///   +0xbc (PendingMissionTypeId): if non-zero, a new mission of this type should be
///          created and registered (FUN_0042ecc0_create_and_register_mission_instance).
///   +0xc0 (PendingMissionParam): parameter for the mission create call.
///   +0xc4 (PendingCancelMissionId): if non-zero, cancel the mission with this ID
///          (FUN_0042ed10 deregister call).
///
/// Validity check (FUN_00475fd0): entry is "active" when
///   PendingMissionTypeId != 0 OR PendingCancelMissionId != 0.
/// </summary>
public class MissionAssignmentEntry
{
    // Sequential unique ID used to look up this entry by FUN_005f3a70_get_table_by_id.
    public int Id { get; set; }

    // +0xbc: mission type to create (0 = nothing pending).
    public int PendingMissionTypeId { get; set; }

    // +0xc0: parameter passed to the mission create call.
    public int PendingMissionParam { get; set; }

    // +0xc4: ID of a mission to cancel (0 = nothing to cancel).
    public int PendingCancelMissionId { get; set; }
}

/// <summary>
/// One entry in AIWorkspace.EntityTargetTable (workspace+0xd8).
/// Each entry represents a game entity tracked for both mission assignment (Type 4)
/// and production automation (Type 12). The entry is a large shared structure whose
/// fields are used by different strategy record types.
///
/// Mission fields (Type 4 — FUN_00475fd0 validity check):
///   +0xbc (PendingMissionTypeId): if non-zero, create this mission for the entity.
///   +0xc0 (MissionParam): parameter for the mission create call.
///   +0xc4 (PendingCancelId): if non-zero, cancel the mission with this ID.
///
/// Production fields (Type 12 — FUN_00476140 validity check):
///   +0xe0 (PendingProductionId): production tracking entry ID pending installation.
///   +0xe4 (PreviousProductionId): old production tracking entry ID to be cancelled.
///
/// Type 8 / status flags:
///   +0x34 (StatusFlags): bit field read by Type 8's backward walker.
/// </summary>
public class MissionTargetEntry
{
    // Sequential unique ID used to look up this entry by FUN_005f3a70_get_table_by_id.
    public int Id { get; set; }

    // +0xbc: mission type to create for this entity (0 = nothing pending).
    public int PendingMissionTypeId { get; set; }

    // +0xc0: mission parameter.
    public int MissionParam { get; set; }

    // +0xc4: ID of a mission to cancel for this entity (0 = nothing to cancel).
    public int PendingCancelId { get; set; }

    // +0xe0: pending production tracking entry ID to install at this facility.
    // Non-zero means the AI wants to begin tracking a new manufacturing job here.
    // FUN_00476140 returns 1 when this or PreviousProductionId is non-zero.
    public int PendingProductionId { get; set; }

    // +0xe4: previous production tracking entry ID to be cancelled.
    // Set when a new production job replaces the old one at this facility.
    // Cleared by MarkProductionEntryCancelled in Type 12's LinkProductionEntryToEntityTarget.
    public int PreviousProductionId { get; set; }

    // +0x34: status flag word. The high byte (bits >>0x18) encodes the entity's type code.
    // Type 8 (FUN_004ceb30) checks whether (high byte) is in [0x80, 0x90) to decide
    // whether to create fleet issue records or skip.
    public int StatusFlags { get; set; }

    // --- Dispatch sub-machine fields (Type 8 via FUN_00476910) ---

    // +0x1c: inner dispatch sub-state (0-6). Drives the 6-stage dispatch pipeline in
    // FUN_00476910 (fleet assignment initiation → finalisation).
    // Default/out-of-range → state 1; state 6 completes the cycle and writes *param_1=1.
    public int InnerDispatchState { get; set; }

    // +0x5c: pointer-equivalent reference to the fleet or character assigned to this target.
    // Null means no assignment is pending (FUN_00476910 immediately sets *param_1=1 and returns null).
    public object FleetTarget { get; set; }

    // +0x60: assignment ID embedded in a 32-bit word.
    // Bit 0x4000000 = "entry is currently being assigned to a sector" (FUN_004cea70 eligibility flag).
    // Reset to 0 (bit cleared) by FUN_004fbf90_reset_id at the start of the dispatch pipeline.
    public int AssignmentId { get; set; }

    // +0x64: secondary assignment ID, reset together with AssignmentId in dispatch state 1.
    public int SubAssignmentId { get; set; }

    // +0x68: in-progress flag. Set to 1 by FUN_00476910 whenever a non-null work item
    // is produced. Cleared to 0 at the start of dispatch if it was set from the previous cycle
    // (triggers FUN_00476c60 + FUN_00476da0 cleanup before re-entering the pipeline).
    public int InProgressFlag { get; set; }

    // +0x6c: dispatch sub-object initialised in pipeline state 2 (FUN_004acd70 call).
    // Carries intermediate fleet assignment state across pipeline ticks.
    public object AssignmentSubObject { get; set; }
}

/// <summary>
/// One entry in AIWorkspace.SelectedTargetTable (workspace+0x11c).
/// Represents a game entity (planet, fleet, or system) that the AI has selected as an
/// attack or scout target. Walked and dispatched by Type 9 (StrategyRecordType9).
///
/// The entry drives an 8-state inner dispatch pipeline (FUN_004737e0):
///   default/0 → state 1 (re-initialise).
///   state 1   → FUN_00473900 precondition check; result determines state 2 or 8.
///   state 2   → FUN_00473e00 (creates work item); state 3.
///   state 3   → FUN_00473fe0 setup; state 4.
///   state 4   → FUN_00474050 with out-param; if param!=null → state 5.
///   state 5   → FUN_00474130_create_selected_target_issue_entry; state 6.
///   state 6   → FUN_00474440_lookup_selected_target_linked_entry; state 7.
///   state 7   → FUN_00474780 (creates work item); state 0, *dispatchOut=1.
///   state 8   → FUN_00473700 cleanup; state 0, *dispatchOut=1.
///
/// DirtyFlag (+0x38): set to 1 when a non-null work item is produced during dispatch;
///   checked at entry to FUN_004737e0 — if set, FUN_00473c00 is called and the flag cleared.
/// InProgressFlag (+0x68): set to 1 when a non-null work item is produced (mirrors entry+0x38).
/// </summary>
public class SelectedTargetEntry
{
    // Sequential unique ID for FUN_005f3a70_get_table_by_id lookup.
    public int Id { get; set; }

    // +0x1c: current state in the 8-stage inner dispatch pipeline. 0 or out-of-range → default.
    public int InnerState { get; set; }

    // +0x38: dirty/pending flag. Set whenever a work item is produced during dispatch.
    // Cleared by FUN_00473c00 at the start of the next dispatch tick when set from a prior cycle.
    public int DirtyFlag { get; set; }

    // +0x5c: target object reference (fleet, planet, or sector entity).
    // Null means no target is assigned to this entry; FUN_004737e0 sets *dispatchOut=1 and
    // returns null immediately when this is null.
    public object TargetObject { get; set; }

    // +0x68: in-progress flag. Set to 1 whenever a non-null work item is produced.
    public int InProgressFlag { get; set; }
}

/// <summary>
/// One entry in AIWorkspace.ProductionTrackingTable (workspace+0xec).
/// Tracks a single manufacturing job that the production automation AI (Type 12) manages.
///
/// Vtable-equivalent checks used by the Type 12 state machine:
///   vtable+0xc  → Status: Active (1) = job is building; Complete (2) = job is done.
///   vtable+0x10 → NeedsProcessing: whether this entry requires AI action this cycle.
///   vtable+0x14 → Dispatch: produce a work item for this manufacturing job (called from
///                  TryDispatchProductionEntry in Type 12 state 5).
///   vtable+0x20 → LinkCallback: called by FUN_00476160 when linking to an entity target.
///   vtable+0x28 → ManufacturingType: used by FUN_0041ad80 to index the completion counter array.
///
/// Field layout (from FUN_0042e670, FUN_00476160, FUN_0041ad80):
///   entry+0x20  → StatusFlags (bit 0x80000000 = cancelled, set by FUN_0042e670).
///   entry+0x54  → EntityTargetId (back-ref to workspace+0xd8 entry, set by FUN_00476160).
/// </summary>
public class ProductionTrackingEntry
{
    // Sequential unique ID for FUN_005f3a70_get_table_by_id lookup.
    public int Id { get; set; }

    // entry+0x20 equivalent: Active = building, Complete = done (vtable+0xc).
    public ProductionStatus Status { get; set; } = ProductionStatus.Active;

    // entry+0x20 bit 0x80000000: set by FUN_0042e670 when this job is cancelled.
    public bool IsCancelled { get; set; }

    // entry+0x54: back-reference to the MissionTargetEntry.Id this job is linked to.
    // Written by FUN_00476160 during the link step (Type 12 SubState 3).
    public int EntityTargetId { get; set; }

    // vtable+0x28 equivalent: manufacturing type used to index ProductionCompletionCounters.
    public ManufacturingType ManufacturingType { get; set; }

    // vtable+0x10 equivalent: whether this entry currently requires AI processing.
    // Cleared by the AI after dispatch; set by the production scheduling system.
    public bool NeedsProcessing { get; set; }

    // The unit template being manufactured. Set when the job is created.
    public IManufacturable Unit { get; set; }
}

/// <summary>
/// Production tracking entry status codes, corresponding to vtable+0xc return values.
/// </summary>
public enum ProductionStatus
{
    Active = 1,
    Complete = 2,
}
