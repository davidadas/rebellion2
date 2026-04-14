using System.Collections.Generic;
using System.Linq;
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

    // +0xc0 — Fleet analysis scorer A (vtable[4] called in calibration state 5).
    // Accumulates system/fleet/character statistics for the AI's decision-making.
    public GalaxyAnalysisScorer FleetAnalysisSubObject { get; } = new GalaxyAnalysisScorer();

    // +0x104 — Fleet analysis scorer B (secondary; vtable[4] called in calibration state 5).
    public GalaxyAnalysisScorer FleetAnalysisSubObjectB { get; } = new GalaxyAnalysisScorer();

    // Calibration cursor — tracks position within SystemAnalysis during calibration state 1.
    // Corresponds to scratchBlock+0x178 (FUN_00417cb0 state 1 cursor pointer).
    public int CalibrationCursor { get; set; }

    // Faction owning this workspace (used to resolve entity side during analysis).
    public Faction Owner { get; set; }

    // Game root reference — used by GalaxyAnalysisPipeline to enumerate all entities.
    public GameRoot GameRoot { get; set; }

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

    // -------------------------------------------------------------------------
    // workspace+0x58 — fleet assignment records table.
    // Stores FleetAssignmentRecord entries indexed by ID. Searched by
    // FUN_004f4cc0 in CleanupProgressFlag, CleanupAssignmentList, and
    // BuildAssignmentCandidate. Entries track which fleets are registered for
    // a given mission assignment and carry capacity contribution fields.
    //
    // workspace+0x78 — fleet availability records table.
    // Stores FleetAvailabilityRecord entries indexed by ID. Searched by
    // FUN_004195f0 (= FUN_00475560) in CleanupProgressFlag, CleanupAssignmentList,
    // AccumulateCapacityData, BuildAssignmentCandidate, and EvaluateCapacityCondition.
    // Entries carry accumulated capacity fields and sub-assignment lists.
    // -------------------------------------------------------------------------

    /// <summary>Fleet assignment records (workspace+0x58).</summary>
    public List<FleetAssignmentRecord> FleetAssignmentTable { get; } = new List<FleetAssignmentRecord>();

    /// <summary>Fleet availability records (workspace+0x78).</summary>
    public List<FleetAvailabilityRecord> FleetAvailabilityTable { get; } = new List<FleetAvailabilityRecord>();

    /// <summary>Looks up a FleetAssignmentRecord by ID (FUN_004f4cc0 equivalent).</summary>
    public FleetAssignmentRecord FindFleetAssignment(int id) =>
        FleetAssignmentTable.FirstOrDefault(r => r.Id == id);

    /// <summary>Looks up a FleetAvailabilityRecord by ID (FUN_004195f0/FUN_00475560 equivalent).</summary>
    public FleetAvailabilityRecord FindFleetAvailability(int id) =>
        FleetAvailabilityTable.FirstOrDefault(r => r.Id == id);

    // Pending mission create/cancel written by the mission scheduling system.
    public int PendingMissionCancelId { get; set; } // workspace+0x314
    public int PendingMissionTypeId { get; set; } // workspace+0x318
    public int PendingMissionParameter { get; set; } // workspace+0x31c

    // Sequential ID generator for MissionTable and EntityTargetTable entries.
    public int NextMissionId { get; set; } = 1;

    // -------------------------------------------------------------------------
    // Supply-analysis configuration constants (workspace+0x364..+0x390).
    // Written once in Phase 1 by FUN_0041b4d0 (populate side analysis).
    // These are static thresholds used by the shortage generators to decide
    // how many agents/fleets are needed and in what ratios.
    // -------------------------------------------------------------------------
    public int SupplyThreshold364 { get; set; } = 3;  // +0x364
    public int SupplyThreshold368 { get; set; } = 3;  // +0x368
    public int SupplyThreshold36C { get; set; } = 2;  // +0x36c
    public int SupplyThreshold370 { get; set; } = 1;  // +0x370
    public int SupplyThreshold374 { get; set; } = 1;  // +0x374
    public int SupplyThreshold378 { get; set; } = 0;  // +0x378
    public int SupplyThreshold37C { get; set; } = 0;  // +0x37c
    public int SupplyThreshold380 { get; set; } = 3;  // +0x380
    public int SupplyThreshold384 { get; set; } = 9;  // +0x384
    public int SupplyThreshold388 { get; set; } = 0;  // +0x388
    public int SupplyThreshold38C { get; set; } = 6;  // +0x38c
    public int SupplyThreshold390 { get; set; } = 12; // +0x390

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

    // -------------------------------------------------------------------------
    // FUN_004191b0 — QuerySystemAnalysis
    //
    // Searches the system analysis list (workspace+0x44 = SystemAnalysis) for records
    // matching the 6 flag conditions, creates an IssueRecordContainer with (entityKey,
    // statValue) pairs sorted ascending by statValue (sort direction 1).
    //
    // Parameters match the original exactly:
    //   incl24 / excl24 = include / exclude masks for DispositionFlags (+0x24)
    //   incl28 / excl28 = include / exclude masks for FlagA (+0x28 = CapabilityFlags)
    //   incl2c / excl2c = include / exclude masks for FlagB (+0x2c = StatusFlags)
    //   statIndex       = DWORD index into PerSystemStats (byte offset = statIndex * 4)
    // -------------------------------------------------------------------------
    public IssueRecordContainer QuerySystemAnalysis(
        uint incl24, uint incl28, uint incl2c,
        uint excl24, uint excl28, uint excl2c,
        int statIndex
    )
    {
        var result = new IssueRecordContainer();
        foreach (SystemAnalysisRecord rec in SystemAnalysis)
        {
            uint d = rec.DispositionFlags;
            uint a = (uint)rec.FlagA;
            uint b = (uint)rec.FlagB;

            if ((incl24 & d) != incl24) continue;
            if ((excl24 & d) != 0) continue;
            if ((incl28 & a) != incl28) continue;
            if ((excl28 & a) != 0) continue;
            if ((incl2c & b) != incl2c) continue;
            if ((excl2c & b) != 0) continue;

            int statValue = rec.Stats.GetStatByIndex(statIndex);
            // entityKey = system identity. We use System's hash as a proxy for the
            // key-value that FUN_00403040 would produce from the original node pointer.
            int entityKey = rec.System?.GetHashCode() ?? 0;
            result.Add(new IssueRecord { EntityKey = entityKey, PropertyValue = statValue, Record = rec });
        }

        // FUN_0041b9e0: assign sequential priorities ascending by PropertyValue.
        result.FinalizeAndAssignPriorities();
        return result;
    }

    // -------------------------------------------------------------------------
    // FUN_00419af0 — QuerySystemPlanets (entity-scoped planet sub-object query)
    //
    // Resolves the system analysis record for the entity key at param_1,
    // then calls FUN_00430eb0 to query that system's 10 planet sub-objects
    // with the same 6-condition flag filter used by FUN_004191b0.
    //
    // Parameters (from FUN_00419af0 assembly):
    //   candidateEntityKey — entity key (from _candidateRefA etc.)
    //   incl28 / excl28 — include/exclude masks for planet.CapabilityFlags (+0x28)
    //   incl2c / excl2c — include/exclude masks for planet.ExtraFlags (+0x2c)
    //   incl30 / excl30 — include/exclude masks for planet.StatusFlags (+0x30)
    //   statIndex       — DWORD index into planet data fields (+0x48 base)
    //
    // Called from PreconditionCheck2 and SelectAgentSlot with specific params:
    //   sub_419af0(_candidateRefA, 0x800, 0, 0x1, 0x3800000, 0, 0, 6, 1)
    //   → own planets (StatusFlags & 0x1) with fleet deployment (CapabilityFlags & 0x800)
    //     but without mission-blocking flags (CapabilityFlags & 0x3800000 == 0)
    //     → stat = StarfighterCount (index 6)
    // -------------------------------------------------------------------------
    public IssueRecordContainer QuerySystemPlanets(
        int candidateEntityKey,
        uint incl28, uint incl2c, uint incl30,
        uint excl28, uint excl2c, uint excl30,
        int statIndex
    )
    {
        // FUN_0041b540: find the system analysis record matching this entity key.
        SystemAnalysisRecord sysRec = SystemAnalysis.FirstOrDefault(r =>
            r.System?.GetHashCode() == candidateEntityKey
        );
        if (sysRec == null)
            return new IssueRecordContainer();

        // FUN_00430eb0: filter the 10 planet sub-objects.
        var result = new IssueRecordContainer();
        foreach (PlanetSubobject sub in sysRec.PlanetSubobjects)
        {
            if (sub == null) continue;

            uint cap = sub.CapabilityFlags;
            uint ext = sub.ExtraFlags;
            uint sta = sub.StatusFlags;

            // 6-condition filter (identical structure to FUN_004191b0's system filter):
            if ((incl28 & cap) != incl28) continue;
            if ((excl28 & cap) != 0) continue;
            if ((incl2c & ext) != incl2c) continue;
            if ((excl2c & ext) != 0) continue;
            if ((incl30 & sta) != incl30) continue;
            if ((excl30 & sta) != 0) continue;

            int statValue = sub.GetStatByIndex(statIndex);
            result.Add(new IssueRecord
            {
                EntityKey = candidateEntityKey,
                PropertyValue = statValue,
                Record = sysRec,
            });
        }

        result.FinalizeAndAssignPriorities();
        return result;
    }

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
/// Corresponds to the 0x138-byte system analysis records in the system analysis list
/// at workspace+0x44.
///
/// Field mapping to original binary (FUN_004319d0 / FUN_00431860 / FUN_0041af90):
///   DispositionFlags (+0x24, field33_0x24): Character/mission availability flags.
///     Set by FUN_004319d0 from per-planet flag accumulation.
///     FUN_004191b0 filters on this field. Key bits:
///       0x8    = planet without specific faction alignment
///       0x20   = mission type A available
///       0x40   = mission type B available
///       0x80   = character slot type C (queried by UpdateShortageFleet 0x80 filter)
///       0x100  = specific capability flag
///       0x800  = ship/fleet assignment bit (queried in PreconditionCheck2 via sub_419af0)
///       0x2000 = fleet deployment condition (queried by PreconditionCheck2 0x2000 filter)
///       0x40000000 = own-side controlled
///   FlagA (+0x28, field34_0x28): Capability/unit flags.
///     bit 0x1-0x2 = enemy/own ownership (shortage gen requires 0x3 clear)
///     bit 0x800000 = garrison shortage candidate (set by UpdateShortageFleet)
///     bit 0x1000  = regiment capacity available
///   FlagB (+0x2c, field35_0x2c): Status/ownership flags.
///     bit 0x4   = own faction owns at least one planet here
///     bit 0x8   = enemy owns at least one planet here
///     bit 0x10  = neutral planet present
///   PresenceFlags (+0x30): Multi-bit presence flag.
///     bit 0x1          = faction has presence (own planets or fleet)
///     bit 0x10000000   = selected for shortage resolution (FinalizeShortageRecord)
/// </summary>
public class SystemAnalysisRecord
{
    public PlanetSystem System { get; set; }
    public PerSystemStats Stats { get; set; } = new PerSystemStats();

    /// <summary>
    /// Disposition/character flags (+0x24, field33_0x24).
    /// Queried by FUN_004191b0 as the primary filter field (param_1 = include mask).
    /// Set by FUN_004319d0 from per-planet data accumulation.
    /// </summary>
    public uint DispositionFlags { get; set; }

    /// <summary>
    /// The 10 planet sub-objects embedded in the system analysis record at +0x48..+0x6c.
    /// Each sub-object corresponds to one planet slot in the system. Populated by
    /// FUN_004334c0 (planet sub-object refresh) during FUN_00431860's 10-planet loop.
    /// Null slots are uninitialised planets.
    /// </summary>
    public PlanetSubobject[] PlanetSubobjects { get; } = new PlanetSubobject[10];

    /// <summary>
    /// Capability/unit flags word (system record +0x28, field34_0x28).
    /// Set by FUN_004319d0 accumulation from per-planet data.
    /// Shortage generators check: bits 0–1 must be CLEAR (no enemy planets).
    /// </summary>
    public int FlagA { get; set; }

    /// <summary>
    /// Status/ownership flags word (system record +0x2c, field35_0x2c).
    /// Set by FUN_004319d0 and FUN_00431860.
    /// Shortage generators and calibration check various bits here.
    /// </summary>
    public int FlagB { get; set; }

    /// <summary>
    /// Entity/fleet presence count (system record +0x30).
    /// Non-zero when this faction or an enemy has a fleet or character in this system.
    /// Shortage generators check: (PresenceFlags &amp; 0x1) must be non-zero to qualify.
    /// </summary>
    public int PresenceFlags { get; set; }

    // Score fields used by calibration state 1 (FUN_00431860 → FUN_0041af90 scoring).
    public int SystemScore { get; set; }
    public int ScoringFlags { get; set; }
}

/// <summary>
/// One result record from FUN_004191b0 (QuerySystemAnalysis).
/// Corresponds to the 24-byte astruct built by FUN_0041bb10.
/// Contains the system's entity key and the PerSystemStats property value at the queried index.
/// Priority is assigned by FUN_0041b9e0 (1 = highest priority = lowest property value).
/// </summary>
public class IssueRecord
{
    /// <summary>System entity key from FUN_00403040 (proxy: System.GetHashCode()).</summary>
    public int EntityKey { get; set; }

    /// <summary>PerSystemStats[statIndex] value (at result record +0x10).</summary>
    public int PropertyValue { get; set; }

    /// <summary>Sequential priority index assigned by FinalizeAndAssignPriorities (1 = first/best).</summary>
    public int Priority { get; set; }

    /// <summary>Back-reference to the source SystemAnalysisRecord for C# convenience.</summary>
    public SystemAnalysisRecord Record { get; set; }
}

/// <summary>
/// Container of IssueRecord results from FUN_004191b0 (astruct_340 / AutoClass761).
/// Items are sorted ascending by PropertyValue with random tiebreaking (FUN_0041ba00).
/// FUN_00434e10 splices items from a query result into this container.
/// FUN_00434e30 retrieves the top-priority entity key.
/// FUN_005f3dd0 clears the container.
/// </summary>
public class IssueRecordContainer
{
    private readonly List<IssueRecord> _records = new List<IssueRecord>();
    private static readonly System.Random _rng = new System.Random();

    /// <summary>Number of records in the container.</summary>
    public int Count => _records.Count;

    /// <summary>All records, in priority order after FinalizeAndAssignPriorities.</summary>
    public IReadOnlyList<IssueRecord> Records => _records;

    /// <summary>
    /// Adds a record (called by QuerySystemAnalysis during population).
    /// </summary>
    public void Add(IssueRecord record) => _records.Add(record);

    /// <summary>
    /// FUN_00434e10_store_mission_issue_record: splice source container items into this one.
    /// In the original this moves the sub-list from a query result (astruct_340+0xc) into
    /// the issue container (AutoClass415 / AutoClass761 at record+0x68).
    /// </summary>
    public void StoreFrom(IssueRecordContainer source)
    {
        if (source == null) return;
        _records.AddRange(source._records);
    }

    /// <summary>
    /// FUN_0041b9e0: assign sequential priority indices 1, 2, 3... in ascending order
    /// by PropertyValue (smallest = priority 1 = most urgent). Random tiebreaking
    /// matches FUN_0041ba00_compare_and_decide (roll d10, true if &lt; 5).
    /// </summary>
    public void FinalizeAndAssignPriorities()
    {
        _records.Sort((a, b) =>
        {
            if (a.PropertyValue != b.PropertyValue)
                return a.PropertyValue.CompareTo(b.PropertyValue);
            return _rng.Next(2) == 0 ? -1 : 1; // random tiebreak
        });
        for (int i = 0; i < _records.Count; i++)
            _records[i].Priority = i + 1;
    }

    /// <summary>
    /// FUN_00434e30_get_last_mission_issue_record_id: returns the top-priority record's
    /// EntityKey (priority 1 = smallest PropertyValue = most urgent shortage).
    /// Returns false if the container is empty.
    /// </summary>
    public bool TryGetTopEntityKey(out int entityKey)
    {
        if (_records.Count == 0) { entityKey = 0; return false; }
        entityKey = _records[0].EntityKey;
        return true;
    }

    /// <summary>
    /// FUN_00434e30: returns the top-priority SystemAnalysisRecord directly (C# convenience).
    /// </summary>
    public SystemAnalysisRecord GetTopRecord() =>
        _records.Count > 0 ? _records[0].Record : null;

    /// <summary>FUN_005f3dd0: clear all records.</summary>
    public void Clear() => _records.Clear();
}

/// <summary>
/// Planet sub-object embedded in SystemAnalysisRecord at +0x48 (10-slot array).
/// Corresponds to the variable-size struct (0x120+ bytes) populated by FUN_004334c0
/// (planet sub-object refresh) and consumed by FUN_004319d0 (per-planet accumulator).
///
/// Field layout (offsets relative to planet sub-object base):
///   +0x18 = entity type/ID packed field (high byte = entity type code)
///   +0x1c = owner side (1=Alliance, 2=Empire)
///   +0x20 = computed ownership code: (entity.OwnerField &gt;&gt; 6) & 3
///   +0x24 = dirty flag: non-zero means Refresh() must be called before this is valid
///   +0x28 = CapabilityFlags (FUN_004319d0 param_2)
///   +0x2c = ExtraFlags (FUN_004319d0 param_4)
///   +0x30 = StatusFlags (FUN_004319d0 param_3) — ownership bits gate everything
///   +0x48..end = data fields passed as param_1 to FUN_004319d0
/// </summary>
public class PlanetSubobject
{
    // +0x18 entity reference (type packed in high byte)
    public int EntityRef { get; set; }

    // +0x1c owner side (1=Alliance, 2=Empire, from the faction this workspace belongs to)
    public int OwnerSide { get; set; }

    // +0x20 ownership code derived from entity: (entity.ownerField >> 6) & 3
    public int OwnershipCode { get; set; }

    // +0x24 dirty flag — Refresh() must run when non-zero before this sub-object is valid
    public int DirtyFlag { get; set; }

    // --- Flag words queried by FUN_004191b0 and passed to FUN_004319d0 ---

    // +0x28 CapabilityFlags (FUN_004319d0 param_2 = &planet_subobj+0x28)
    // Key bits (from FUN_004334c0 assembly):
    //   0x1   = unit research bit set (troop type A)
    //   0x2   = warship/ship facility present
    //   0x4   = ?
    //   0x8   = facility capacity gate (troop type B)
    //   0x20  = own faction has available capacity (this+0x84 > 0)
    //   0x40  = own faction has more capacity (this+0x80 > 0)
    //   0x80  = character slot type (via fleet check)
    //   0x100 = unit count below capacity threshold
    //   0x200 = mission type flag
    //   0x800 = fleet deployment condition (FUN_004191b0 param_1=0x2000 filter downstream)
    //   0x1000 = character condition
    //   0x2000 = unit type C condition
    //   0x4000 = character strength condition
    //   0x8000 = troop deficit (unit count < capacity)
    //   0x10000000 = unit research bit 0x1000 (some tech)
    //   0x20000000 = unit research bit 0x2000
    //   0x40000000 = Death Star (Empire faction + Death Star check)
    //   0x80000000 = special entity OR sub_525c30 condition
    //   HIBYTE bits set by various unit capability checks
    public uint CapabilityFlags { get; set; }

    // +0x2c ExtraFlags (FUN_004319d0 param_4 = &planet_subobj+0x2c)
    public uint ExtraFlags { get; set; }

    // +0x30 StatusFlags (FUN_004319d0 param_3 = &planet_subobj+0x30)
    // Ownership bits (critical for all shortage detection):
    //   0x1   = own faction owns this planet (FUN_004319d0 branch: param_3 & 1 set → own path)
    //   0x2   = neutral/unowned planet
    //   0x4   = enemy faction owns this planet
    //   0x10  = entity has ship capability (entity+0x50 bit 3)
    //   0x20  = ownership set marker (always set when 0x1, 0x2, or 0x4 is set)
    //   0x40  = special entity type [0x92, 0x94)
    //   0x80  = character mission type (officer stationed here)
    //   0x100 = unit count threshold reached (this+0x64 >= 2)
    //   0x200 = fleet available at this planet
    //   0x800 = troop type garrison flag
    //   0x8000000 = entity type 0x121 (specific building/special)
    //   0x40000000 = entity ID 0x109 (Death Star)
    //   0x80000000 = sub_525c30 condition
    //   HIBYTE bits set by fleet/troop capability checks
    public uint StatusFlags { get; set; }

    // --- Data fields (param_1 to FUN_004319d0, base = planet_subobj+0x48) ---

    // +0x48 capacity threshold (100 = 0x64 when own planet with troops)
    public int CapacityThreshold { get; set; }

    // +0x4c entity capacity field (entity+0x5c)
    public int EntityCapacity { get; set; }

    // +0x50 fighter count A (sub_52c4d0 result)
    public int FighterCountA { get; set; }

    // +0x54 fighter count B (sub_52c0f0 result)
    public int FighterCountB { get; set; }

    // +0x58 capital ship count (incremented per capital ship)
    public int CapitalShipCount { get; set; }

    // +0x5c regiment count
    public int RegimentCount { get; set; }

    // +0x60 starfighter count
    public int StarfighterCount { get; set; }

    // +0x64 unit type D count
    public int UnitTypeDCount { get; set; }

    // +0x68 strength accumulator
    public int StrengthAccum { get; set; }

    // +0x6c computed garrison deficit (capacity - threshold)
    public int GarrisonDeficit { get; set; }

    // +0x70 fighter type A count (sub_51b460 result)
    public int FighterTypeA { get; set; }

    // +0x74 fighter type B count (sub_526490/sub_526700 result)
    public int FighterTypeB { get; set; }

    // +0x78 strength sum from units (unit+0x30 accumulation)
    public int UnitStrengthSum { get; set; }

    // +0x7c garrison deficit B
    public int GarrisonDeficitB { get; set; }

    // +0x80 available capacity B: entity+0x64 - entity+0x68
    public int AvailableCapacityB { get; set; }

    // +0x84 available capacity A: entity+0x5c - entity+0x60
    public int AvailableCapacityA { get; set; }

    // +0x88 total troop count (regiment + facility)
    public int TroopCount { get; set; }

    // +0x8c troops meeting capacity gate
    public int TroopGateCount { get; set; }

    // +0x90 additional count
    public int AuxCount { get; set; }

    // +0x98 urgency score (0-6 clamped, computed from capacity deficit)
    public int UrgencyScore { get; set; }

    // +0xa0 urgency vs threshold (max of urgency and workspace threshold)
    public int UrgencyThreshold { get; set; }

    // +0xa4 character count
    public int CharacterCount { get; set; }

    // +0xa8 troop count for characters
    public int CharTroopCount { get; set; }

    // +0xac character capability count
    public int CharCapabilityCount { get; set; }

    // +0xb0 character strength A
    public int CharStrengthA { get; set; }

    // +0xb4 character strength B
    public int CharStrengthB { get; set; }

    // +0xb8 character strength C
    public int CharStrengthC { get; set; }

    // +0xbc combined character/troop strength
    public int CombinedStrength { get; set; }

    // +0xc0 troop strength accumulator
    public int TroopStrength { get; set; }

    // +0xc4 unit strength accumulator
    public int UnitStrength { get; set; }

    // +0xc8 capital ship strength
    public int CapShipStrength { get; set; }

    // +0xcc, +0xd0, +0xd4, +0xd8 unit count accumulators by type
    public int UnitCountAccumA { get; set; }
    public int UnitCountAccumB { get; set; }
    public int UnitCountAccumC { get; set; }
    public int UnitCountAccumD { get; set; }

    // +0xdc fighter surplus/deficit
    public int FighterSurplus { get; set; }

    // +0xe0 troop deficit (own: a8 - threshold; enemy: ua count)
    public int TroopDeficit { get; set; }

    // +0xe4, +0xe8, +0xec fleet strength fields
    public int FleetStrengthA { get; set; }
    public int FleetStrengthB { get; set; }
    public int FleetStrengthC { get; set; }

    // +0xf0..+0xf8 fleet range/priority fields
    public int FleetRangeField { get; set; }

    // +0xf8, +0xfc special condition counters
    public int SpecialCountA { get; set; }
    public int SpecialCountB { get; set; }

    // +0x100..+0x118 additional accumulators
    public int FleetTypeCountA { get; set; }
    public int FleetTypeCountB { get; set; }
    public int FleetTypeCountC { get; set; }
    public int CharSummaryScore { get; set; }

    // +0x11c character count for shortage calculation
    public int CharShortageCount { get; set; }

    /// <summary>
    /// FUN_00430eb0 property accessor: (&amp;this->field51_0x48)[param_7]
    /// Returns the DWORD at byte offset (param_7 * 4) from the data region starting at +0x48.
    /// Used by FUN_00430eb0 to get a specific metric from this planet sub-object.
    /// </summary>
    public int GetStatByIndex(int index)
    {
        // Each index is a DWORD (4-byte) offset from the +0x48 data region start.
        // Byte offset = index * 4. Planet sub-object layout from +0x48 (FUN_004334c0):
        return (index * 4) switch
        {
            0x00 => CapacityThreshold,      // +0x48 index 0
            0x04 => EntityCapacity,          // +0x4c index 1
            0x08 => FighterCountA,           // +0x50 index 2
            0x0c => FighterCountB,           // +0x54 index 3
            0x10 => CapitalShipCount,        // +0x58 index 4
            0x14 => RegimentCount,           // +0x5c index 5
            0x18 => StarfighterCount,        // +0x60 index 6 (queried by sub_419af0 param_7=6)
            0x1c => UnitTypeDCount,          // +0x64 index 7
            0x20 => StrengthAccum,           // +0x68 index 8
            0x24 => GarrisonDeficit,         // +0x6c index 9
            0x28 => FighterTypeA,            // +0x70 index 10
            0x2c => FighterTypeB,            // +0x74 index 11
            0x30 => UnitStrengthSum,         // +0x78 index 12
            0x34 => GarrisonDeficitB,        // +0x7c index 13
            0x38 => AvailableCapacityB,      // +0x80 index 14
            0x3c => AvailableCapacityA,      // +0x84 index 15
            0x40 => TroopCount,              // +0x88 index 16
            0x44 => TroopGateCount,          // +0x8c index 17
            0x48 => AuxCount,                // +0x90 index 18
            0x50 => UrgencyScore,            // +0x98 index 20
            0x58 => UrgencyThreshold,        // +0xa0 index 22
            0x5c => CharacterCount,          // +0xa4 index 23
            _ => 0, // unmapped or out-of-range
        };
    }
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

    // +0x18: entity reference/key used in FUN_0047acc0, FUN_0047ad30, FUN_0041ace0
    // for record ownership validation and workspace mission lookup.
    public int FieldAt18 { get; set; }

    // +0x1c: current state of the 8-state dispatch machine (FUN_004bc170).
    // Default 0 → treated as default → resets to state 1.
    public int MachineState { get; set; }

    // +0x20: machine flags driving FUN_004bc170 state transitions.
    //   bit 0x1         = valid fleet assignment pending (gate for state 6)
    //   bit 0x4         = eligibility flag (cleared/set by FUN_004bc340)
    //   bit 0x8 & 0x10  = conditions for state 7→8 transition
    //   bit 0x100       = cleared in state 2 (FUN_0047a560)
    //   bit 0x10000000  = triggers FUN_0047a440 in state 1
    //   bit 0x60000000  = triggers FUN_0047a6a0 (produce work item) in state 1
    //   bit 0xe0000000  = exclusion mask for FUN_0047a440 condition
    public uint MachineFlags { get; set; }

    // +0x20: status flags. Bit layout used by ScanShipTypeList:
    //   bits 0-1 (0x3): if != 0x3 → SubAssignmentId |= 0x400
    //   bit 8 (0x100): if set → SubAssignmentId |= 0x80000000
    public int EntryStatusFlags { get; set; }

    // +0x38: fleet entity reference. Checked in FUN_004bc340 for type code [0x90,0x98).
    public int EntityRef38 { get; set; }

    // +0x3c: list A of assignment sub-records, iterated by FUN_0047a440/FUN_0047ae90.
    public readonly System.Collections.Generic.List<int> AssignmentListA
        = new System.Collections.Generic.List<int>();

    // +0x44: list B of assignment sub-records.
    public readonly System.Collections.Generic.List<int> AssignmentListB
        = new System.Collections.Generic.List<int>();

    // +0x68: back-reference to the owning AIWorkspace.
    // Set by FUN_0042ecc0 (field98_0x68 = container+0x14 = workspace ref).
    public AIWorkspace Workspace { get; set; }

    // +0xbc: mission type to create (0 = nothing pending).
    public int PendingMissionTypeId { get; set; }

    // +0xc0: parameter passed to the mission create call.
    public int PendingMissionParam { get; set; }

    // +0xc4: ID of a mission to cancel (0 = nothing to cancel).
    public int PendingCancelMissionId { get; set; }

    /// <summary>
    /// FUN_004bc170 — 8-state dispatch machine. Called as vtable[11] by TryDispatchMissionEntry.
    /// Writes dispatchOut=0 when work is absorbed, non-zero when pending.
    /// Returns a work item (TypeCode 0x201/0x240) when produced.
    ///
    /// State machine (from assembly, verified against trace):
    ///   default → state=1
    ///   1: MachineFlags & 0x60000000 → ProcessReturnWorkItemPhase, state=0, dispatchOut=1
    ///      MachineFlags & 0x10000000 → ProcessConditionCheckPhase1, dispatchOut=1 (stays 1)
    ///      else → state=2
    ///   2: ProcessPreparationPhase, state=3
    ///   3: MissionPhase3, state=4
    ///   4: MissionPhase4, state=5
    ///   5: MissionPhase5, state=6
    ///   6: CheckEligibilityCondition → non-zero: state=7; zero: dispatchOut=1
    ///   7: AssembleFinalWorkItem → work item; flags 0x8&&0x10: state=8; else: dispatchOut=1
    ///   8: ProcessTerminalWorkItem, dispatchOut=1
    ///   Post: if dispatchOut=1: state=0
    /// </summary>
    public AIWorkItem Dispatch(out int dispatchOut)
    {
        dispatchOut = 0;
        AIWorkItem result = null;

        switch (MachineState)
        {
            default:
                MachineState = 1;
                return null;

            case 1:
                if ((MachineFlags & 0x60000000u) != 0)
                {
                    result = ProcessReturnWorkItemPhase();
                    MachineState = 0;
                    dispatchOut = 1;
                }
                else if ((MachineFlags & 0x10000000u) != 0)
                {
                    ProcessConditionCheckPhase1();
                    dispatchOut = 1;
                    // state stays at 1 until bit 0x10000000 is cleared by the handler
                }
                else
                {
                    MachineState = 2;
                }
                break;

            case 2:
                ProcessPreparationPhase();
                MachineState = 3;
                break;

            case 3:
                MissionPhase3();
                MachineState = 4;
                break;

            case 4:
                MissionPhase4();
                MachineState = 5;
                break;

            case 5:
                MissionPhase5();
                MachineState = 6;
                break;

            case 6:
            {
                int eligible = CheckEligibilityCondition();
                if (eligible != 0)
                    MachineState = 7;
                else
                    dispatchOut = 1;
                break;
            }

            case 7:
            {
                result = AssembleFinalWorkItem();
                if ((MachineFlags & 0x8u) != 0 && (MachineFlags & 0x10u) != 0)
                    MachineState = 8;
                else
                    dispatchOut = 1;
                break;
            }

            case 8:
                result = ProcessTerminalWorkItem();
                dispatchOut = 1;
                break;
        }

        if (dispatchOut != 0)
            MachineState = 0;

        return result;
    }

    // FUN_0047a440: state 1 condition-check handler.
    // When MachineFlags & 0x10000000 && !(MachineFlags & 0xe0000000):
    //   iterate AssignmentListA (FUN_0047acc0), iterate AssignmentListB (FUN_0047ad30),
    //   call ProcessWorkspaceMissionLookup (FUN_0041ace0).
    // Always: ProcessMissionStateCallback (vtable+0x38).
    private void ProcessConditionCheckPhase1()
    {
        if ((MachineFlags & 0x10000000u) != 0 && (MachineFlags & 0xe0000000u) == 0)
        {
            foreach (int key in AssignmentListA.ToList())
                ProcessAssignmentRecordA(key);
            foreach (int key in AssignmentListB.ToList())
                ProcessAssignmentRecordB(key);
            if (Workspace != null)
                ProcessWorkspaceMissionLookup(FieldAt18);
        }
        ProcessMissionStateCallback();
    }

    // FUN_0047a560: state 2 preparation.
    // Clears MachineFlags bit 0x100 (bit 8). Processes both lists via FUN_0047ae90.
    private void ProcessPreparationPhase()
    {
        MachineFlags &= ~0x100u;
        foreach (int key in AssignmentListA.ToList())
            ValidateAndProcessRecord(key);
        foreach (int key in AssignmentListB.ToList())
            ValidateAndProcessRecord(key);
    }

    // FUN_0047a6a0: state 1 return-work-item handler.
    // Checks mission state via FUN_004f2380, creates TypeCode 0x250 work item if ready.
    private AIWorkItem ProcessReturnWorkItemPhase()
    {
        // Assembly: checks mission armed/working cycle (FUN_004f2380 + FUN_0051fcf0),
        // creates TypeCode 0x250 work item if MachineFlags & 0x40000000.
        if ((MachineFlags & 0x40000000u) != 0)
        {
            MachineFlags &= unchecked((uint)(~0x9fffffff));
            return new MissionExecutionWorkItem(FieldAt18, Workspace);
        }
        MachineFlags &= unchecked((uint)(~0x9fffffff));
        return null;
    }

    // FUN_004bc340: eligibility check for state 6.
    // Clears bit 0x4 from MachineFlags. If bit 0x1 set AND EntityRef38 is valid fleet
    // entity (type [0x90,0x98)), sets bit 0x4 and returns 1. Else returns 0.
    private int CheckEligibilityCondition()
    {
        MachineFlags &= ~0x4u;
        if ((MachineFlags & 0x1u) == 0)
            return 0;
        if (EntityRef38 != 0)
        {
            MachineFlags |= 0x4u;
            return 1;
        }
        return 0;
    }

    // FUN_004bc810: final work item assembly for state 7.
    // Clears MachineFlags bit 0x8. Processes lists, allocates TypeCode 0x201 work item.
    private AIWorkItem AssembleFinalWorkItem()
    {
        MachineFlags &= ~0x8u;
        return new MissionExecutionWorkItem(FieldAt18, Workspace);
    }

    // FUN_0047a7b0: terminal handler for state 8.
    // Allocates TypeCode 0x240 work item if EntityRef38 valid.
    private AIWorkItem ProcessTerminalWorkItem()
    {
        if (EntityRef38 != 0)
            return new MissionExecutionWorkItem(FieldAt18, Workspace);
        return null;
    }

    // FUN_0047acc0: find key in AssignmentListA, look up in shared table,
    // update flags and remove. Returns 1 if found.
    private void ProcessAssignmentRecordA(int key)
    {
        // FUN_0047acc0 assembly: sub_4f4cc0(this+0x3c, key) → find in list A;
        // if found: sub_4f4cc0(*(this+0x64), key) → find in shared table;
        // if found && record+0x24 == this+0x18: set record+0x30 |= 0x800000, clear fields;
        // sub_4f4c60(this+0x3c, key) → remove from list A.
        AssignmentListA.Remove(key);
    }

    // FUN_0047ad30: same as FUN_0047acc0 but for AssignmentListB.
    private void ProcessAssignmentRecordB(int key)
    {
        AssignmentListB.Remove(key);
    }

    // FUN_0041ace0: find mission in workspace.MissionTable (workspace+0xa8) by entity key,
    // call sub_417ca0(workspace), insert into workspace+0xc0 queue.
    private void ProcessWorkspaceMissionLookup(int entityKey)
    {
        // Pending workspace+0xc0 active-mission queue implementation.
    }

    // FUN_0047ae90 equivalent: validate record in shared table, call two methods if valid.
    private void ValidateAndProcessRecord(int key)
    {
        // Pending shared-table (+0x64) implementation.
    }

    // vtable+0x38: mission state callback — always called at end of ProcessConditionCheckPhase1.
    protected virtual void ProcessMissionStateCallback() { }

    // vtable+0x48, +0x4c, +0x50: type-specific mission phases.
    // Concrete subclasses override these based on mission type.
    protected virtual void MissionPhase3() { }
    protected virtual void MissionPhase4() { }
    protected virtual void MissionPhase5() { }
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

    // +0x18: entity type or family ID used as a filter key in registry lookups.
    // FUN_00476c60 filters AssignmentRegistryList entries by record+0x30 == this value.
    // FUN_00476da0 filters CandidateList entries by record+0x30 == this value.
    public int FilterTypeId { get; set; }

    // +0x1c: inner dispatch sub-state (0-6). Drives the 6-stage dispatch pipeline in
    // FUN_00476910 (fleet assignment initiation → finalisation).
    // Default/out-of-range → state 1; state 6 completes the cycle and sets dispatchOut=1.
    public int InnerDispatchState { get; set; }

    // +0x20: faction/side identifier for this assignment.
    // Written into work items allocated in FUN_00476e90 (BuildAssignmentCandidate).
    public int OwnerSide { get; set; }

    // +0x24: registry list of existing assignment records for cleanup and re-checking.
    // Each AssignmentRecord.Id is the key for a FleetAssignmentRecord in
    // AIWorkspace.FleetAssignmentTable. Iterated by FUN_00476c60 (CleanupProgressFlag).
    public List<AssignmentRecord> AssignmentRegistryList { get; } = new List<AssignmentRecord>();

    // +0x2c: candidate entity list for this mission target.
    // Each CandidateRecord.Id is the key for a FleetAvailabilityRecord in
    // AIWorkspace.FleetAvailabilityTable. Iterated by FUN_00476da0 (CleanupAssignmentList),
    // FUN_00476e90 (BuildAssignmentCandidate), and FUN_00477100 (AccumulateCapacityData).
    public List<CandidateRecord> CandidateList { get; } = new List<CandidateRecord>();

    // +0x34: status flag word. The high byte (bits >>0x18) encodes the entity's type code.
    // Type 8 (FUN_004ceb30) checks whether (high byte) is in [0x80, 0x90) to decide
    // whether to create fleet issue records or skip.
    public int StatusFlags { get; set; }

    // +0x38: assignment confirmation word. Set to 2 by FUN_004789b0 (FinalizeAssignment)
    // when a matching record is found in the registry and the assignment is confirmed.
    public int AssignmentConfirmWord { get; set; }

    // +0x3c: assignment state word. First field of the embedded astruct_187 subobject
    // spanning +0x3c..+0x58. Set to 2 by FUN_004ec230 in FUN_00478e20 (UpdateAssignmentState)
    // when AssignmentId bit 0x2000000 is NOT set. Passed by address as a subobject pointer
    // to Fleet vtable slot 4 (FUN_004f54d0) when AssignmentId bit 0x2000000 IS set.
    public int AssignmentStateWord { get; set; }

    // +0x40: astruct_187 subobject field at offset +0x04 within the embedded block.
    // Purpose not yet resolved from callee analysis; reserved for binary layout fidelity.
    public int EmbeddedSubField { get; set; }

    // +0x44: assignment target reference (astruct_187 subobject field at +0x08).
    // Written into FleetAvailabilityRecord.AssignmentRef by the accept path
    // (FUN_00478420) when a new candidate is confirmed: entity.AssignmentRef = this.AssignmentTargetId.
    public int AssignmentTargetId { get; set; }

    // +0x48: list of mission issue records created during FinalizeAssignment (FUN_004789b0,
    // dispatch case 6) when AssignmentId bit 0x2000000 is set.
    public List<object> MissionIssueRecordList { get; } = new List<object>();

    // +0x58: pointer to the owning AIWorkspace (astruct_187 subobject field at +0x1c).
    // All table lookups in the pipeline resolve through this reference:
    //   ContextObject.FleetAssignmentTable (+0x58)
    //   ContextObject.FleetAvailabilityTable (+0x78)
    //   ContextObject.MissionTable (+0xa8)
    //   ContextObject.ProductionTrackingTable (+0xec)
    public AIWorkspace ContextObject { get; set; }

    // +0x5c: fleet assignment target. Drives vtable slot calls for capacity, mission,
    // production, and sub-state machine dispatch. Null means no active assignment.
    public FleetAssignmentTarget FleetTarget { get; set; }

    // +0x60: assignment ID embedded in a 32-bit word.
    // Bit 0x4000000 = "entry is currently being assigned to a sector" (FUN_004cea70 eligibility flag).
    // Reset to 0 by FUN_004fbf90_reset_id at the start of the dispatch pipeline (case 1).
    // Bit 0x2000000: controls which path FUN_00478e20 (UpdateAssignmentState) and
    // FUN_004789b0 (FinalizeAssignment) take in case 6.
    public int AssignmentId { get; set; }

    // +0x64: secondary assignment ID, reset together with AssignmentId in dispatch state 1.
    // Encodes capability flags set by SetAssignmentCapabilityFlags (FUN_00478e50):
    //   bits 0x200000..0x10000000 driven by AccumulateCapacityData accumulator results;
    //   bits 0x100, 0x200, 0x400, 0x20000000, 0x80000000, 0x100000, 0x10000, 0x1c0000
    //   set by ScanShipTypeList (FUN_00477450) and ScanProductionList (FUN_00477590).
    public int SubAssignmentId { get; set; }

    // +0x68: in-progress flag. Set to 1 by Dispatch() whenever a non-null work item is
    // produced, or unconditionally when the default/case-6 path executes. Cleared to 0
    // at the start of Dispatch() if set from the previous cycle, triggering cleanup.
    public int InProgressFlag { get; set; }

    // +0x6c..+0x90: 10 capacity accumulator slots zeroed at the start of AccumulateCapacityData
    // (FUN_00477100 via rep stosd) and accumulated from each candidate entity's production fields.
    //   0 (+0x6c): sum of entity+0xb4 across all candidates
    //   1 (+0x70): sum of entity+0xb8
    //   2 (+0x74): sum of entity+0x84  (snapshot-copied to CapacitySnapshot0 after loop)
    //   3 (+0x78): sum of entity+0x78  (snapshot-copied to CapacitySnapshot1 after loop)
    //   4 (+0x7c): count of candidates with entity+0x38 & 0x10 set (NOT checked for SubAssignmentId flags)
    //   5 (+0x80): sum of entity+0x68
    //   6 (+0x84): count of candidates with entity+0x38 & 0x20 set
    //   7 (+0x88): sum of entity+0x7c
    //   8 (+0x8c): sum of entity+0x70
    //   9 (+0x90): sum of entity+0x9c
    public int CapacityAccumulator0 { get; set; }
    public int CapacityAccumulator1 { get; set; }
    public int CapacityAccumulator2 { get; set; }
    public int CapacityAccumulator3 { get; set; }
    public int CapacityAccumulator4 { get; set; }
    public int CapacityAccumulator5 { get; set; }
    public int CapacityAccumulator6 { get; set; }
    public int CapacityAccumulator7 { get; set; }
    public int CapacityAccumulator8 { get; set; }
    public int CapacityAccumulator9 { get; set; }

    // +0x94: snapshot of CapacityAccumulator2 copied after AccumulateCapacityData completes.
    public int CapacitySnapshot0 { get; set; }

    // +0x98: snapshot of CapacityAccumulator3 copied after AccumulateCapacityData completes.
    public int CapacitySnapshot1 { get; set; }

    // +0xb0: ship-type candidate list scanned by FUN_00477450 (ScanShipTypeList) in case 4.
    // Each int is an ID looked up in ContextObject.MissionTable (ContextObject+0xa8).
    public List<int> ShipTypeList { get; } = new List<int>();

    // +0xbc: mission type to create for this entity (0 = nothing pending).
    public int PendingMissionTypeId { get; set; }

    // +0xc0: mission parameter.
    public int MissionParam { get; set; }

    // +0xc4: ID of a mission to cancel for this entity (0 = nothing to cancel).
    public int PendingCancelId { get; set; }

    // +0xd4: production-type candidate list scanned by FUN_00477590 (ScanProductionList) in case 4.
    // Each int is an ID looked up in ContextObject.ProductionTrackingTable (ContextObject+0xec).
    public List<int> ProductionTypeList { get; } = new List<int>();

    // +0xe0: pending production tracking entry ID to install at this facility.
    // Non-zero means the AI wants to begin tracking a new manufacturing job here.
    // FUN_00476140 returns 1 when this or PreviousProductionId is non-zero.
    public int PendingProductionId { get; set; }

    // +0xe4: previous production tracking entry ID to be cancelled.
    // Set when a new production job replaces the old one at this facility.
    // Cleared by MarkProductionEntryCancelled in Type 12's LinkProductionEntryToEntityTarget.
    public int PreviousProductionId { get; set; }

    /// <summary>
    /// Inner fleet assignment dispatch pipeline (FUN_00476910). Drives the 6-state
    /// assignment cycle from candidate selection through fleet finalisation.
    ///
    /// Entry guard: if InProgressFlag is set from the previous cycle, calls
    /// CleanupProgressFlag and CleanupAssignmentList before clearing the flag.
    ///
    /// Returns the work item produced this tick, or null if the pipeline is between
    /// states or waiting for external completion. Sets dispatchOut=1 when the full
    /// cycle completes (case 6 falls through to default, resetting InnerDispatchState=1).
    /// </summary>
    public AIWorkItem Dispatch(out int dispatchOut)
    {
        if (InProgressFlag != 0)
        {
            CleanupProgressFlag();
            CleanupAssignmentList();
            InProgressFlag = 0;
        }

        dispatchOut = 0;
        AIWorkItem result = null;

        if (FleetTarget == null)
        {
            dispatchOut = 1;
            return null;
        }

        switch (InnerDispatchState)
        {
            default:
                InnerDispatchState = 1;
                InProgressFlag = 1;
                break;

            case 1:
                AssignmentId = 0;
                SubAssignmentId = 0;
                result = BuildAssignmentCandidate();
                InnerDispatchState = 2;
                break;

            case 2:
                AccumulateCapacityData();
                SubtractFleetCapacity();
                InnerDispatchState = 3;
                break;

            case 3:
            {
                int localFlag = 0;
                result = EvaluateCapacityCondition(ref localFlag);
                if (localFlag != 0)
                    InnerDispatchState = 4;
                break;
            }

            case 4:
                ScanShipTypeList();
                ScanProductionList();
                InnerDispatchState = 5;
                break;

            case 5:
            {
                int localFlag = 0;
                result = DispatchFleetSubStateMachine(ref localFlag);
                if (localFlag != 0)
                    InnerDispatchState = 6;
                break;
            }

            case 6:
                FinalizeAssignment();
                UpdateAssignmentState();
                CommitFleetAssignment();
                SetAssignmentCapabilityFlags();
                dispatchOut = 1;
                goto default;
        }

        if (result != null)
            InProgressFlag = 1;

        return result;
    }

    /// <summary>
    /// Cancels or re-accepts assignment records from AssignmentRegistryList (FUN_00476c60).
    /// Called at the start of Dispatch() when InProgressFlag was set from the prior cycle.
    ///
    /// For each AssignmentRecord, looks up the FleetAssignmentRecord in FleetAssignmentTable.
    /// Pass condition: entity.TypeId == FilterTypeId AND (entity.Flags &amp; 0x4000) != 0
    ///   Pass: extracts SubObjectId from entity, checks it is not already in AssignmentRegistryList,
    ///         then looks up in FleetAvailabilityTable; if avail.Flags bit 0x4 set, calls accept path.
    ///   Fail: calls remove/subtract path (FUN_00478520 equivalent).
    /// </summary>
    private void CleanupProgressFlag()
    {
        foreach (AssignmentRecord registryNode in AssignmentRegistryList.ToList())
        {
            FleetAssignmentRecord entity = ContextObject.FindFleetAssignment(registryNode.Id);

            if (entity != null
                && entity.TypeId == FilterTypeId
                && (entity.Flags & 0x4000) != 0)
            {
                // Pass: check if SubObjectId is not already tracked, then try to accept.
                int subId = entity.SubObjectId;
                if (!AssignmentRegistryList.Any(r => r.Id == subId))
                {
                    FleetAvailabilityRecord avail = ContextObject.FindFleetAvailability(subId);
                    if (avail != null && (avail.Flags & 0x4) != 0)
                        AcceptIntoCandidate(subId);
                }
            }
            else
            {
                // Fail: remove from registry and subtract capacity contributions.
                RemoveAssignment(registryNode.Id);
            }
        }
    }

    /// <summary>
    /// Accept path inlined from FUN_00478420. Adds subId to CandidateList and updates
    /// the corresponding FleetAvailabilityRecord.
    ///
    /// Guard: FleetTarget.InnerState must be in [0x8, 0x10). Returns without action otherwise.
    /// If the FleetAvailabilityRecord is found: clears Flags bit 0x4, sets TypeId = FilterTypeId,
    /// sets AssignmentRef = AssignmentTargetId (this+0x44). Appends a new CandidateRecord to CandidateList.
    /// If the record is not found: no action (the allocated node is discarded in the original).
    /// </summary>
    private void AcceptIntoCandidate(int subId)
    {
        int innerState = FleetTarget.InnerState;
        if (innerState < 0x8 || innerState >= 0x10)
            return;

        FleetAvailabilityRecord avail = ContextObject.FindFleetAvailability(subId);
        if (avail == null)
            return;

        avail.Flags &= ~0x4;
        avail.TypeId = FilterTypeId;
        avail.AssignmentRef = AssignmentTargetId;
        CandidateList.Add(new CandidateRecord { Id = subId });
    }

    /// <summary>
    /// Remove/subtract path inlined from FUN_00478520. Looks up the ID in both
    /// AssignmentRegistryList and FleetAssignmentTable.
    ///
    /// If found in AssignmentRegistryList AND in FleetAssignmentTable:
    ///   If entity.TypeId == FilterTypeId: sets entity.Flags |= 0x20000, clears bits 24-25,
    ///     zeroes entity.TypeId and entity.AssignmentTypeRef.
    ///   Unconditionally subtracts entity's capacity fields from the accumulators:
    ///     Acc0-=entity.Acc0, Acc1-=Acc1, Acc2-=Acc2, Acc3-=Acc3, Acc5-=Acc5, Acc7-=Acc7, Acc8-=Acc8.
    ///   Conditionally: if entity.Flags bit 7 (0x80): Acc4-=1;
    ///                  if entity.Flags bit 10 (0x400): Acc6-=1;
    ///                  if entity.Flags bit 1 (0x2): Acc9-=1.
    /// Removes the AssignmentRecord from AssignmentRegistryList.
    /// </summary>
    private void RemoveAssignment(int id)
    {
        AssignmentRecord registryEntry = AssignmentRegistryList.FirstOrDefault(r => r.Id == id);
        if (registryEntry == null)
            return;

        FleetAssignmentRecord entity = ContextObject.FindFleetAssignment(id);
        if (entity != null)
        {
            int originalFlags = entity.Flags;
            if (entity.TypeId == FilterTypeId)
            {
                entity.Flags = (originalFlags | 0x20000) & unchecked((int)0xfcffffff);
                entity.TypeId = 0;
                entity.AssignmentTypeRef = 0;
            }
            CapacityAccumulator0 -= entity.Acc0;
            CapacityAccumulator1 -= entity.Acc1;
            CapacityAccumulator2 -= entity.Acc2;
            CapacityAccumulator3 -= entity.Acc3;
            CapacityAccumulator5 -= entity.Acc5;
            CapacityAccumulator7 -= entity.Acc7;
            CapacityAccumulator8 -= entity.Acc8;
            if ((originalFlags & 0x80) != 0)
                CapacityAccumulator4 -= 1;
            if ((originalFlags & 0x400) != 0)
                CapacityAccumulator6 -= 1;
            if ((originalFlags & 0x2) != 0)
                CapacityAccumulator9 -= 1;
        }

        AssignmentRegistryList.Remove(registryEntry);
    }

    /// <summary>
    /// Removes ineligible candidates from CandidateList (FUN_00476da0).
    /// Called after CleanupProgressFlag when re-entering the pipeline after a suspended
    /// cycle. For each node: resolves entity via FUN_004195f0, checks record+0x30 ==
    /// FilterTypeId, flag bit 0x1, and FUN_005f3650 count gate. Calls FUN_478620 for
    /// entries that fail.
    /// INCOMPLETE(game-entity): requires resolved entity struct field definitions.
    /// </summary>
    private void CleanupAssignmentList()
    {
        foreach (CandidateRecord candidate in CandidateList.ToList())
        {
            FleetAvailabilityRecord entity = ContextObject.FindFleetAvailability(candidate.Id);
            bool pass = entity != null
                && entity.TypeId == FilterTypeId
                && (entity.Flags & 0x1) != 0
                && entity.SubEntries.Count > 0;
            if (!pass)
            {
                if (entity != null)
                {
                    entity.Flags |= 0x4;
                    entity.TypeId = 0;
                    entity.AssignmentRef = 0;
                }
                CandidateList.Remove(candidate);
            }
        }
    }

    /// <summary>
    /// Builds the initial fleet assignment candidate work item (FUN_00476e90).
    /// Called in dispatch case 1 after resetting AssignmentId and SubAssignmentId.
    /// Iterates CandidateList, resolves entities, builds a filtered local candidate list,
    /// and allocates a 0x270-byte work item via FUN_004f5060 when a candidate is found.
    /// Sets work-item+0x20 = OwnerSide. Calls vtable slots +0x24 and +0x2c on the work item.
    /// Returns the allocated work item or null if no candidate passes.
    /// INCOMPLETE(game-entity): requires resolved entity struct field definitions.
    /// </summary>
    private AIWorkItem BuildAssignmentCandidate()
    {
        bool found = false;
        foreach (CandidateRecord candidate in CandidateList)
        {
            if (found)
                break;

            FleetAvailabilityRecord entity = ContextObject.FindFleetAvailability(candidate.Id);
            if (entity == null)
                continue;
            // HIBYTE(Flags)&0x30 != 0 = bits 12-13 of Flags (0x3000) set.
            if ((entity.Flags & 0x3000) == 0)
                continue;
            // Flags & 0xf0000802 == 0 (none of those bits may be set).
            if ((entity.Flags & unchecked((int)0xf0000802)) != 0)
                continue;

            var workItem = new FleetAssignmentCandidateWorkItem { OwnerSide = OwnerSide };

            // Inner loop iterates ALL sub-entries; does not break on first match.
            foreach (SubAssignmentRecord sub in entity.SubEntries)
            {
                FleetAssignmentRecord record = ContextObject.FindFleetAssignment(sub.Id);
                if (record == null)
                    continue;
                if ((record.Flags & 0x801000) == 0)
                    continue;
                if ((record.Flags & 0x800) != 0)
                    continue;
                workItem.AssignmentIds.Add(sub.Id);
            }

            if (workItem.AssignmentIds.Count > 0)
            {
                workItem.InitializeAssignmentIds(workItem.AssignmentIds);
                workItem.InitializeSourceRef(entity.SourceRef);
                found = true;
                return workItem;
            }
        }
        return null;
    }

    /// <summary>
    /// Zeroes all 10 CapacityAccumulator slots then accumulates capacity data from each
    /// candidate entity (FUN_00477100). Called in dispatch case 2.
    /// After accumulation, sets SubAssignmentId flag bits for non-zero accumulators:
    ///   Accumulator0 != 0: SubAssignmentId |= 0x200000
    ///   Accumulator1 != 0: SubAssignmentId |= 0x400000
    ///   Accumulator2 != 0: SubAssignmentId |= 0x800000
    ///   Accumulator3 != 0: SubAssignmentId |= 0x1000000
    ///   Accumulator5 != 0: SubAssignmentId |= 0x2000000
    ///   Accumulator7 != 0: SubAssignmentId |= 0x4000000
    ///   Accumulator8 != 0: SubAssignmentId |= 0x8000000
    ///   Accumulator9 != 0: SubAssignmentId |= 0x10000000
    ///   Accumulator6 != 0: SubAssignmentId |= 0x100000
    /// Accumulator4 is NOT checked for SubAssignmentId flags.
    /// Copies Accumulator2 → CapacitySnapshot0 and Accumulator3 → CapacitySnapshot1.
    /// INCOMPLETE(game-entity): entity field reads require resolved struct definitions.
    /// </summary>
    private void AccumulateCapacityData()
    {
        CapacityAccumulator0 = 0;
        CapacityAccumulator1 = 0;
        CapacityAccumulator2 = 0;
        CapacityAccumulator3 = 0;
        CapacityAccumulator4 = 0;
        CapacityAccumulator5 = 0;
        CapacityAccumulator6 = 0;
        CapacityAccumulator7 = 0;
        CapacityAccumulator8 = 0;
        CapacityAccumulator9 = 0;

        foreach (CandidateRecord candidate in CandidateList)
        {
            FleetAvailabilityRecord entity = ContextObject.FindFleetAvailability(candidate.Id);
            if (entity == null)
                continue;
            CapacityAccumulator0 += entity.CapFieldB4;
            CapacityAccumulator1 += entity.CapFieldB8;
            CapacityAccumulator2 += entity.CapField84;
            CapacityAccumulator3 += entity.CapField78;
            if ((entity.CategoryFlags & 0x10) != 0) CapacityAccumulator4 += 1;
            CapacityAccumulator5 += entity.CapField68;
            if ((entity.CategoryFlags & 0x20) != 0) CapacityAccumulator6 += 1;
            CapacityAccumulator7 += entity.CapField7C;
            CapacityAccumulator8 += entity.CapField70;
            CapacityAccumulator9 += entity.CapField9C;
        }

        CapacitySnapshot0 = CapacityAccumulator2;
        CapacitySnapshot1 = CapacityAccumulator3;

        if (CapacityAccumulator0 != 0) SubAssignmentId |= 0x200000;
        if (CapacityAccumulator1 != 0) SubAssignmentId |= 0x400000;
        if (CapacityAccumulator2 != 0) SubAssignmentId |= 0x800000;
        if (CapacityAccumulator3 != 0) SubAssignmentId |= 0x1000000;
        if (CapacityAccumulator5 != 0) SubAssignmentId |= 0x2000000;
        if (CapacityAccumulator7 != 0) SubAssignmentId |= 0x4000000;
        if (CapacityAccumulator8 != 0) SubAssignmentId |= 0x8000000;
        if (CapacityAccumulator9 != 0) SubAssignmentId |= 0x10000000;
        if (CapacityAccumulator6 != 0) SubAssignmentId |= 0x100000;
        // CapacityAccumulator4 is NOT checked for SubAssignmentId flags.
    }

    /// <summary>
    /// Subtracts the current fleet's capacity from the accumulators produced by
    /// AccumulateCapacityData. Called immediately after AccumulateCapacityData in
    /// dispatch case 2.
    /// INCOMPLETE(fleet-vtable): requires Fleet interface to subtract capacity values.
    /// </summary>
    private void SubtractFleetCapacity()
    {
        FleetTarget.PrepareCapacityArray();
        CapacityAccumulator0 -= FleetTarget.RequiredCapacity[0];
        CapacityAccumulator1 -= FleetTarget.RequiredCapacity[1];
        CapacityAccumulator2 -= FleetTarget.RequiredCapacity[2];
        CapacityAccumulator3 -= FleetTarget.RequiredCapacity[3];
        CapacityAccumulator4 -= FleetTarget.RequiredCapacity[4];
        CapacityAccumulator5 -= FleetTarget.RequiredCapacity[5];
        CapacityAccumulator6 -= FleetTarget.RequiredCapacity[6];
        CapacityAccumulator7 -= FleetTarget.RequiredCapacity[7];
        CapacityAccumulator8 -= FleetTarget.RequiredCapacity[8];
        CapacityAccumulator9 -= FleetTarget.RequiredCapacity[9];
    }

    /// <summary>
    /// Evaluates accumulated capacity against assignment thresholds (dispatch case 3).
    /// Sets localFlag to non-zero when a qualifying candidate is found, signalling the
    /// pipeline to advance to case 4. Returns a work item if a candidate was committed.
    /// INCOMPLETE(game-entity): requires resolved entity struct field definitions.
    /// </summary>
    private AIWorkItem EvaluateCapacityCondition(ref int localFlag)
    {
        // Retrieve the last AssignmentRegistry entry and resolve its FleetAvailabilityRecord.
        AssignmentRecord lastRecord = AssignmentRegistryList.Count > 0
            ? AssignmentRegistryList[AssignmentRegistryList.Count - 1]
            : null;
        if (lastRecord == null)
        {
            localFlag = 1;
            return null;
        }

        FleetAvailabilityRecord entity = ContextObject.FindFleetAvailability(lastRecord.Id);
        if (entity == null)
        {
            localFlag = 1;
            return null;
        }

        // 8 sequential capacity checks. Any failure means the condition is not satisfied.
        if (entity.CapField7C > CapacityAccumulator0) return null;
        if (entity.CapField80 > CapacityAccumulator1) return null;
        if ((entity.Flags & 0x2) != 0 && CapacityAccumulator9 <= 0) return null;
        if ((entity.Flags & 0xc) != 0 && entity.CapField6C > CapacityAccumulator2) return null;
        if (entity.CapField60 > 0 && entity.CapField60 > CapacityAccumulator3) return null;
        if (entity.CapField48 > 0 && entity.CapField48 > CapacityAccumulator5) return null;
        if (CapacityAccumulator4 <= 0 && (entity.Flags & 0x80) != 0) return null;
        if (CapacityAccumulator6 <= 0 && (entity.Flags & 0x400) != 0) return null;

        // All conditions passed: mark the assignment path and signal advance to case 4.
        AssignmentId |= 0x2000000;
        localFlag = 1;
        // Work item allocation and vtable commit calls require further research (FUN_004772d0 callees).
        return null;
    }

    /// <summary>
    /// Scans ShipTypeList for eligible ship-type candidates (FUN_00477450).
    /// Called in dispatch case 4 alongside ScanProductionList.
    /// Zeroes PendingMissionTypeId, MissionParam, and PendingCancelId.
    /// Calls Fleet vtable slot 6 (FUN_004fd970) with 4 arguments.
    /// If PendingMissionTypeId != 0 after the call: SubAssignmentId |= 0x100.
    /// Iterates ShipTypeList via FUN_005f3a70 lookup in ContextObject's ship table (+0xa8);
    /// sets SubAssignmentId bits (0x20000000, high byte of 0x400, 0x80000000) per entry.
    /// Tracks minimum via Fleet vtable slot 8 (FUN_004fd9b0); stores best destination
    /// in PendingCancelId (+0xc4).
    /// INCOMPLETE(fleet-vtable): requires Fleet vtable slots 6 and 8.
    /// </summary>
    private void ScanShipTypeList()
    {
        PendingMissionTypeId = 0;
        MissionParam = 0;
        PendingCancelId = 0;

        // Vtable slot 6 call populates PendingMissionTypeId/MissionParam.
        // Abstract method not yet defined — awaiting research pass for FUN_004fd970.
        FleetTarget.GetMissionInfo(this);

        if (PendingMissionTypeId != 0)
            SubAssignmentId |= 0x100;

        int minCost = int.MaxValue;
        // Iterate ShipTypeList LIFO; each ID looks up a MissionAssignmentEntry.
        for (int i = ShipTypeList.Count - 1; i >= 0; i--)
        {
            MissionAssignmentEntry entry = ContextObject.MissionTable
                .FirstOrDefault(e => e.Id == ShipTypeList[i]);
            if (entry == null)
                continue;

            SubAssignmentId |= 0x20000000;
            if ((entry.EntryStatusFlags & 0x3) != 0x3)
                SubAssignmentId |= 0x400;
            if ((entry.EntryStatusFlags & 0x100) != 0) // HIBYTE & 0x1 = bit 8
                SubAssignmentId = unchecked((int)((uint)SubAssignmentId | 0x80000000));

            // Track minimum-cost entry; store its FieldAt18 in PendingCancelId.
            if (entry.FieldAt18 < minCost)
            {
                minCost = entry.FieldAt18;
                PendingCancelId = entry.FieldAt18;
            }
        }
    }

    /// <summary>
    /// <summary>
    /// Scans ProductionTypeList for eligible production candidates (FUN_00477590).
    /// Called in dispatch case 4 alongside ScanShipTypeList.
    /// Zeroes PendingProductionId (+0xe0) and PreviousProductionId (+0xe4).
    /// Clears bits 24-26 of AssignmentId (+0x60).
    /// First loop: iterates ProductionTypeList; checks flags at entry+0x20; sets SubAssignmentId/AssignmentId bits.
    /// Then calls FleetTarget vtable slot 7 with &amp;PendingProductionId; if non-zero: SubAssignmentId |= 0x200.
    /// Second loop: calls FUN_004acfc0 per entry to find PreviousProductionId.
    /// Remaining vtable/flag logic requires research pass (FUN_004acfc0 and slot 7 not yet read).
    /// </summary>
    private void ScanProductionList()
    {
        PendingProductionId = 0;
        PreviousProductionId = 0;
        AssignmentId &= unchecked((int)0xf8ffffff);
        // remaining: production flag loop + vtable slot 7 call — awaiting research spec
    }

    /// <summary>
    /// Drives the fleet sub-state machine (dispatch case 5). Advances internal fleet
    /// negotiation state; sets localFlag to non-zero when the sub-machine reaches a
    /// terminal state, signalling the pipeline to advance to case 6.
    /// Returns a work item if a sub-state action was committed, or null.
    /// INCOMPLETE(game-entity): requires fleet sub-state machine implementation.
    /// </summary>
    private AIWorkItem DispatchFleetSubStateMachine(ref int localFlag)
    {
        localFlag = 0;
        return null;
    }

    /// <summary>
    /// Finalises the fleet assignment and updates mission issue records (FUN_004789b0).
    /// Called in dispatch case 6.
    /// If AssignmentId bit 0x2000000 is NOT set: searches AssignmentRegistryList and the
    /// BST at ContextObject+0x58 for a record matching FilterTypeId; if found, clears
    /// AssignmentId bit 0x1000000 and writes 2 to AssignmentConfirmWord.
    /// If bit 0x2000000 IS set: creates mission issue records via FUN_0041a430,
    /// FUN_00434e10, and FUN_00434e30.
    /// INCOMPLETE(mission-issue): requires mission issue record type definitions.
    /// </summary>
    // FUN_004789b0: finalize the fleet assignment. Two paths based on AssignmentId bit 0x2000000.
    //
    // Non-set path (FUN_004789b0 assembly):
    //   Search AssignmentRegistryList for a record matching FilterTypeId.
    //   Also search FleetAssignmentTable via ContextObject.
    //   If match found AND record has bit 0x1000000: clear that bit, set AssignmentConfirmWord=2.
    //
    // Assembly trace (FUN_004789b0, fully read):
    //
    // NON-SET PATH (AssignmentId & 0x2000000 == 0):
    //   esi = &AssignmentConfirmWord (this+0x38).
    //   FUN_004f4cc0(AssignmentRegistryList=this+0x24, &AssignmentConfirmWord) → record1.
    //   FUN_004f4cc0(ContextObject+0x58, &AssignmentConfirmWord) → record2.
    //   If record2 found AND *(record2+0x30)==FilterTypeId AND *(record2+0x24)&0x1000000:
    //     *(record2+0x24) &= ~0x1000000 (clear confirmed flag).
    //   sub_4ec230(AssignmentConfirmWord) → reset to "unset".
    //
    // SET PATH (AssignmentId & 0x2000000 != 0):
    //   FUN_004f4cc0(AssignmentRegistryList, &AssignmentConfirmWord) → record1.
    //   FUN_004f4cc0(ContextObject+0x58, &AssignmentConfirmWord) → record2.
    //   If record2 found AND FilterTypeId matches AND *(record2+0x24)&0x1000000:
    //     var_24=1. If *(record2+0x24) & 0x2000000:
    //       *(record2+0x24) &= ~0x1000000.
    //     Else (var_24=1 & ~0x800): complex mission issue creation via sub_41a430.
    //   If var_24==0: iterate AssignmentId list (this+0x2c), sub_41a430 for each.
    //   Iterate AssignmentRegistryList: for each → sub_4f4cc0(ContextObject+0x58) → clear 0x2000000.
    //
    // BLOCKED: ContextObject fleet assignment table + mission issue records not fully built.
    private void FinalizeAssignment()
    {
        if ((AssignmentId & 0x2000000) == 0)
        {
            // Non-set path: find matching assignment record and confirm.
            foreach (AssignmentRecord reg in AssignmentRegistryList)
            {
                FleetAssignmentRecord entity = ContextObject?.FindFleetAssignment(reg.Id);
                if (entity != null && entity.TypeId == FilterTypeId)
                {
                    if ((entity.Flags & 0x1000000) != 0)
                        entity.Flags &= ~0x1000000;
                    AssignmentConfirmWord = 2;
                    return;
                }
            }
        }
        else
        {
            // Set path: create mission issue records using QuerySystemAnalysis.
            // FUN_0041a430 queries for mission assignment candidates.
            // Use workspace system analysis as the issue source.
            if (ContextObject != null)
            {
                IssueRecordContainer c = ContextObject.QuerySystemAnalysis(
                    incl24: 0x80, incl28: 0, incl2c: 0,
                    excl24: 0, excl28: 0, excl2c: 0,
                    statIndex: 4
                );
                foreach (IssueRecord r in c.Records)
                {
                    if (r.Record != null)
                        MissionIssueRecordList.Add(r.Record);
                }
            }
            AssignmentConfirmWord = 2;
        }
    }

    /// <summary>
    /// Updates the assignment state word based on AssignmentId flags (FUN_00478e20).
    /// Called in dispatch case 6 after FinalizeAssignment.
    /// If AssignmentId bit 0x2000000 is NOT set: writes 2 to AssignmentStateWord.
    /// If bit 0x2000000 IS set AND FleetTarget is non-null: calls Fleet vtable slot 4
    /// (FUN_004f54d0) with the embedded subobject at +0x3c; the callee walks the linked
    /// list at ContextObject via +0x1c links searching for a node whose vtable+0x4 returns 0xf2.
    /// INCOMPLETE(fleet-vtable): the set-bit path requires FUN_004f54d0 (Fleet vtable slot 4).
    /// </summary>
    private void UpdateAssignmentState()
    {
        if ((AssignmentId & 0x2000000) == 0)
        {
            AssignmentStateWord = 2;
        }
        else if (FleetTarget != null)
        {
            FleetTarget.UpdateAssignmentStateInfo(this);
        }
    }

    /// <summary>
    /// Commits the fleet assignment by calling Fleet vtable slot 8 (FUN_004fd9b0 →
    /// FUN_004fe200) on FleetTarget. Called in dispatch case 6. Return value is discarded.
    /// INCOMPLETE(fleet-vtable): requires Fleet interface to expose vtable slot 8.
    /// </summary>
    private void CommitFleetAssignment()
    {
        FleetTarget.CommitAssignment();
    }

    /// <summary>
    /// Sets capability flag bits on AssignmentId based on SubAssignmentId's lower bits
    /// (FUN_00478e50). Called in dispatch case 6 after CommitFleetAssignment.
    ///
    /// If (SubAssignmentId &amp; 0x4ff) == 0:          AssignmentId |= 0x1c0000
    /// Else if (SubAssignmentId &amp; 0x9fe00000) == 0: AssignmentId |= 0x10000
    /// Else:
    ///   AssignmentId |= 0x100000
    ///   If (SubAssignmentId &amp; 0x1f) == 0:          AssignmentId |= 0x80000
    /// </summary>
    private void SetAssignmentCapabilityFlags()
    {
        if ((SubAssignmentId & 0x4ff) == 0)
        {
            AssignmentId |= 0x1c0000;
        }
        else if ((SubAssignmentId & unchecked((int)0x9fe00000)) == 0)
        {
            AssignmentId |= 0x10000;
        }
        else
        {
            AssignmentId |= 0x100000;
            if ((SubAssignmentId & 0x1f) == 0)
                AssignmentId |= 0x80000;
        }
    }
}

/// <summary>
/// One entry in AIWorkspace.SelectedTargetTable (workspace+0x11c).
/// Represents a game entity (planet, fleet, or system) that the AI has selected as an
/// attack or scout target. Walked and dispatched by Type 9 (StrategyRecordType9).
///
/// The entry drives an 8-state inner dispatch pipeline (FUN_004737e0 via Dispatch()):
///   default/0 → state 1 (DirtyFlag=1, re-initialise).
///   state 1   → FUN_00473900 precondition check; found→state 2, not found→state 8.
///   state 2   → FUN_00473e00 (creates work item); state 3.
///   state 3   → FUN_00473fe0 capacity-delta setup; state 4.
///   state 4   → FUN_00474050 (writes *dispatchOut=1 when a candidate is found); if
///                *dispatchOut was written non-zero → state 5; otherwise stays at 4.
///   state 5   → FUN_00474130 create issue entry; state 6.
///   state 6   → FUN_00474440 lookup linked entry; state 7.
///   state 7   → FUN_00474780 (creates work item); state 0, dispatchOut=1.
///   state 8   → FUN_00473700 cleanup; state 0, dispatchOut=1.
///
/// DirtyFlag (+0x38): set to 1 when a non-null work item is returned or when the
///   default case fires. Checked at Dispatch() entry — if set, CleanupDirtyEntry()
///   (FUN_00473c00) is called and the flag cleared before the switch.
/// InProgressFlag (+0x68): set to 1 whenever a non-null work item is produced
///   (mirrors DirtyFlag post-switch behaviour; tracked separately per binary layout).
///
/// Note: FUN_004737e0 has NO null guard on +0x5c at its entry; the null check at
///   the top of the 6-state MissionTargetEntry pipeline (FUN_00476910) does not apply here.
/// </summary>
public class SelectedTargetEntry
{
    // Back-reference to the owning AIWorkspace. Set when entry is added to SelectedTargetTable.
    public AIWorkspace Workspace { get; set; }

    // Sequential unique ID for FUN_005f3a70_get_table_by_id lookup.
    public int Id { get; set; }

    // +0x1c: current state in the 8-stage inner dispatch pipeline. 0 or out-of-range → default.
    public int InnerState { get; set; }

    // +0x20: owner-side value copied verbatim into issue entries created by states 5 and 7
    //   (FUN_00474130 and FUN_00474780 write it to the new entry's +0x20 field).
    public int OwnerSide { get; set; }

    // +0x2c: primary entity ID used in fleet/character lookups during the pipeline.
    //   Passed to FUN_004ec1e0_set_id in multiple callee stages.
    public int EntityId { get; set; }

    // +0x30: packed entity type+ID field. High byte (>>0x18) encodes entity category;
    //   range [0x90, 0x98) = fleet-unit type (checked in FUN_00473900 and FUN_00474780).
    //   Full 32-bit value used as an entity lookup ID in those same stages.
    public int EntityTypePacked { get; set; }

    // +0x34: capacity-delta register. Zeroed and recomputed each time state 3 executes
    //   (FUN_00473fe0): starts at 0, adds pending-count from a related entity (+0xb4),
    //   then subtracts the fleet's required capacity (+0x164), doubled if a flag bit is set.
    public int CapacityDelta { get; set; }

    // +0x38: dirty/pending flag. Set to 1 when a non-null work item is produced or when
    //   the default case fires. Cleared by CleanupDirtyEntry() (FUN_00473c00) at Dispatch()
    //   entry if it was set from the previous tick.
    public int DirtyFlag { get; set; }

    // +0x4c: owning faction/planet context object. Used by pipeline stages to resolve
    //   entities through the faction's unit and character registries.
    //   Typed as object pending game-entity class definitions.
    public object FactionContext { get; set; }

    // +0x5c: target object reference (fleet, planet, or sector entity).
    //   Purpose within the 8-state pipeline not fully resolved from callee analysis;
    //   not used in FUN_004737e0's own entry guard or switch dispatch.
    public object TargetObject { get; set; }

    // +0x68: in-progress flag. Set to 1 whenever a non-null work item is produced.
    public int InProgressFlag { get; set; }

    /// <summary>
    /// 8-state inner dispatch pipeline for this selected-target entry (FUN_004737e0).
    /// Called by StrategyRecordType9 each tick while this entry is the active target.
    /// Writes dispatchOut=0 unconditionally on entry; sets dispatchOut=1 only in
    /// states 7 and 8 (pipeline complete) and when FUN_00474050 signals completion in state 4.
    /// Sets DirtyFlag=1 whenever a non-null work item is returned or the default case fires.
    /// If DirtyFlag was set on entry, calls CleanupDirtyEntry (FUN_00473c00) first.
    /// </summary>
    public AIWorkItem Dispatch(out int dispatchOut)
    {
        dispatchOut = 0;

        AIWorkItem result = null;

        if (DirtyFlag != 0)
        {
            CleanupDirtyEntry();
            DirtyFlag = 0;
        }

        switch (InnerState)
        {
            default:
                InnerState = 1;
                DirtyFlag = 1;
                break;
            case 1:
            {
                int found = CheckTargetPrecondition();
                InnerState = (found != 0) ? 2 : 8;
                break;
            }
            case 2:
                result = BuildTargetCandidate();
                InnerState = 3;
                break;
            case 3:
                ComputeCapacityDelta();
                InnerState = 4;
                break;
            case 4:
            {
                result = EvaluateIssueCondition(ref dispatchOut);
                if (dispatchOut != 0)
                    InnerState = 5;
                break;
            }
            case 5:
                result = CreateSelectedTargetIssueEntry();
                InnerState = 6;
                break;
            case 6:
                result = LookupLinkedTargetEntry();
                InnerState = 7;
                break;
            case 7:
                result = BuildLinkedIssue();
                InnerState = 0;
                dispatchOut = 1;
                break;
            case 8:
                RunTargetCleanup();
                InnerState = 0;
                dispatchOut = 1;
                break;
        }

        if (result != null)
            DirtyFlag = 1;

        return result;
    }

    // FUN_00473c00: cleanup when DirtyFlag was set from prior tick. Assembly trace (fully read).
    //
    // 1. Init entity ref from EntityId (this+0x2c) using FUN_004ec1e0_set_id.
    // 2. FUN_004195f0(FactionContext+0x78) — look up fleet object in FactionContext.
    //    If found with flags (0x1 & 0x8 set) AND count of nodes at (found+0x48) > 0: skip step 3.
    //    Else: FUN_004754d0(this) — some entity cleanup.
    // 3. Iterate entities at this+0x24 (entity list):
    //    For each: FUN_004f4cc0(FactionContext+0x58, nodeKey) → look up entity record.
    //    If found with flags (0x40000000 in field+0x24) and entity type [0x8,0x10):
    //      Sub_4195f0(FactionContext+0x78) — more fleet lookups.
    //      If eligible: FUN_004753b0(nodeKey, ...) → clear assignment flags.
    //    Else: FUN_004753b0 → clear assignment flags.
    //
    // BLOCKED: FactionContext is typed 'object'; fleet/entity struct not available.
    // Proxy: clear InProgressFlag only.
    private void CleanupDirtyEntry()
    {
        InProgressFlag = 0;
    }

    // FUN_00473900: state 1 precondition check. Assembly trace (fully read).
    //
    // 1. Init local entity refs and issue container.
    // 2. Check HIBYTE(EntityTypePacked=this+0x30) in [0x90,0x98) (fleet entity type):
    //    If fleet: esi = FactionContext (this+0x4c = AIWorkspace).
    //      Look up EntityTypePacked in workspace.SystemAnalysis.
    //      If found AND PresenceFlags & 0x1:
    //        If FlagA & 0x80000000 AND FlagA & 0x2 == 0:
    //          var_44=1, PresenceFlags |= 0x80000 (attack candidate marker).
    //        Else: reset EntityTypePacked, clear PresenceFlags & ~0x80000.
    //      Else: reset EntityTypePacked, clear PresenceFlags & ~0x80000.
    // 3. Final HIBYTE check on EntityTypePacked:
    //    If NOT fleet type (was reset or never fleet): search for attack target:
    //      sub_419980(FactionContext, 0x40000000, ...) → query attack targets.
    //      Do-while loop: for each candidate in [0x80,0x90):
    //        If var_44==0: sub_419c10(candidate, 0x80000000,...) → planet query.
    //        If result HIBYTE in [0x90,0x98): set EntityTypePacked = result,
    //          var_44=1, PresenceFlags |= 0x80000.
    //        Get next candidate from container, continue.
    // 4. Returns var_44 (1 = found valid fleet attack target, 0 = not found).
    //
    // Note: FactionContext (this+0x4c) is used as AIWorkspace (workspace+0x2c = SystemAnalysis).
    // BLOCKED: sub_419980/sub_419c10 not implemented; entity HIBYTE checks fail in C#.
    // Proxy: returns 0 always (no valid attack target in C# due to entity encoding).
    private int CheckTargetPrecondition()
    {
        // BLOCKED: All HIBYTE type checks fail in C# (entity keys are hash codes).
        // The function searches for fleet attack targets [0x90,0x98) with FlagA & 0x80000000.
        // In C#, HIBYTE(EntityTypePacked) is never in [0x90,0x98).
        return 0;
    }

    // FUN_00473e00: state 2 build target candidate. Assembly trace (fully read).
    //
    // 1. Init local node list, entity refs. Look up entity via FUN_004195f0(FactionContext+0x78).
    // 2. If found with flags (HIBYTE(field_0x38) & 0x30 != 0) AND NOT (field_0x38 & 0xf0000802):
    //    Iterate nodes in (found+0x48) list:
    //      For each: FUN_004f4cc0(FactionContext+0x58, nodeKey) → look up in fleet table.
    //      If found AND (field+0x24 & 0x801000 != 0) AND (field+0x24 & 0x800 == 0):
    //        Allocate 0x20 node, init, insert into local list.
    // 3. If local list non-empty: sub_4f5060(0x270) = TypeCode=0x270 work item.
    //    Set item+0x20 = OwnerSide (this+0x1c or 0x20).
    //    vtable+0x24 attach nodes. vtable+0x2c call with entity ref.
    //    Return work item. Else: return null.
    //
    // BLOCKED: FactionContext fleet lookups require entity infrastructure.
    // Proxy: returns FleetAssignmentCandidateWorkItem.
    private AIWorkItem BuildTargetCandidate()
    {
        if (TargetObject == null) return null;
        return new FleetAssignmentCandidateWorkItem { OwnerSide = OwnerSide };
    }

    // FUN_00473fe0: state 3 compute capacity delta. Assembly trace (fully read).
    //
    // 1. CapacityDelta (this+0x34) = 0.
    // 2. FUN_00475560(FactionContext, &EntityId) → look up entity from EntityId via FactionContext.
    //    If found: CapacityDelta += *(entity+0xb4) (pending count field).
    // 3. Check *(FactionContext+0x4) bit 0x1 via FUN_005f2ef0:
    //    If bit NOT set: CapacityDelta -= *(FactionContext+0x164) (required fleet capacity).
    //    If bit IS set:  CapacityDelta += (-*(FactionContext+0x164)) * 2.
    //                  = CapacityDelta -= *(FactionContext+0x164) * 2.
    //
    // Note: FactionContext (this+0x4c) is NOT AIWorkspace — it's a fleet context object
    //   with capacity field at +0x164 and flags at +0x4.
    // BLOCKED: FactionContext fleet/entity infrastructure not available.
    // Proxy: compute from Workspace capacity metrics.
    private void ComputeCapacityDelta()
    {
        CapacityDelta = 0;
        if (TargetObject == null) return;
        int available = Workspace?.FleetTotalCapacity - Workspace?.FleetAssignedCapacity ?? 0;
        CapacityDelta = available > 0 ? available : 0;
    }

    // FUN_00474050: state 4 evaluate issue condition. Assembly trace (fully read).
    //
    // 1. var_C = 1. HIBYTE(EntityId=this+0x2c) in [0x8, 0x10):
    //    If in range: look up FUN_004195f0(FactionContext+0x74).
    //      If found AND *(found+0x38) & 0x2: var_C = 0 (skip).
    // 2. If var_C == 0: goto terminal (return null, dispatchOut unchanged).
    // 3. If CapacityDelta < 0:
    //    sub_4748e0(this) → if result != null: return result (terminal).
    //    sub_474ce0(this) → advance.
    //    If either result != null: terminal.
    // 4. If CapacityDelta > 0:
    //    sub_4750c0(this) → work item. *dispatchOut = 1. Return work item.
    // 5. If CapacityDelta == 0:
    //    *dispatchOut = 1. Return null.
    //
    // BLOCKED: HIBYTE checks + sub_4748e0/sub_474ce0/sub_4750c0 require entity infrastructure.
    // Proxy: CapacityDelta > 0 path → create work item; else → check Workspace.
    private AIWorkItem EvaluateIssueCondition(ref int dispatchOut)
    {
        if (CapacityDelta > 0)
        {
            dispatchOut = 1;
            return new MissionExecutionWorkItem(EntityTypePacked, Workspace);
        }
        // Alternate path: check if target system has fleet shortage.
        if (Workspace != null && TargetObject != null)
        {
            var c = Workspace.QuerySystemAnalysis(incl24: 0x80, incl28: 0, incl2c: 0, excl24: 0, excl28: 0, excl2c: 0, statIndex: 4);
            if (c.Count > 0) { dispatchOut = 1; return null; }
        }
        return null;
    }

    // FUN_00474130: state 5 create selected-target issue entry. Assembly trace (fully read).
    //
    // 1. Look up EntityId via FUN_004195f0(FactionContext+0x78).
    //    If found AND *(found+0x80) > 0 (capacity):
    //      Look up in FactionContext+0x2c (system analysis). If found AND PresenceFlags & 0x1:
    //        sub_41a430(FactionContext, EntityId, 0x4000, 0x20800, 9, 1) — mission issue query.
    //        Do-while: for each result with HIBYTE in [0x14,0x1c):
    //          sub_4f21a0(OwnerSide, resultKey) — character lookup.
    //          sub_503dc0(char, 1) → iterate → 0x20 nodes → local list.
    // 2. If list non-empty: TypeCode=0x201 work item. Return.
    //
    // BLOCKED: FactionContext fleet/mission infra required.
    // Proxy: returns MissionExecutionWorkItem.
    private AIWorkItem CreateSelectedTargetIssueEntry()
    {
        if (TargetObject == null) return null;
        return new MissionExecutionWorkItem(EntityTypePacked, Workspace);
    }

    // FUN_00474440: state 6 look up linked target entry. Assembly trace (fully read).
    //
    // 1. Look up EntityId via FUN_004195f0(FactionContext+0x78).
    //    If found with flags (0x4000000 & ~0x2): look up in FactionContext+0x2c.
    //    If found AND PresenceFlags & 0x1: iterate nodes in entity+0x48 list:
    //      For each: look up in FactionContext+0x58 fleet table AND sub_4f21a0(OwnerSide, key).
    //      If both found with (field & ~0x800 != 0 AND 0x600000 set):
    //        sub_5355a0(char, 1) → character iterator.
    //        For each: look up in FactionContext+0x8c.
    //        Build 0x20 nodes (type-gated), insert into local list, var_54=1.
    // 2. If local list non-empty AND var_54: TypeCode=0x201 work item. Return.
    //
    // BLOCKED: FactionContext fleet/mission infra required.
    // Proxy: returns MissionExecutionWorkItem.
    private AIWorkItem LookupLinkedTargetEntry()
    {
        if (TargetObject == null) return null;
        int category = (EntityTypePacked >> 24) & 0xff;
        if (category < 0x90 || category >= 0x98) return null;
        return new MissionExecutionWorkItem(EntityTypePacked, Workspace);
    }

    // FUN_00474780: state 7 build final linked-issue work item. Assembly trace (fully read).
    //
    // 1. Check HIBYTE(EntityTypePacked=this+0x30) in [0x90,0x98) (fleet entity type).
    //    If fleet: look up EntityTypePacked in FactionContext+0x2c (system analysis).
    //    If found AND FlagA & 0x2 == 0 (no enemy):
    //      Update EntityId (this+0x2c) = entity key.
    //      FUN_004195f0(FactionContext+0x78) — fleet object lookup.
    //      If found AND *(found+0x28) != EntityTypePacked AND *(found+0x38) & 0x2 == 0:
    //        Allocate 0x20 node init with EntityId. Insert into local list.
    //        TypeCode=0x201 work item. vtable+0x24 attach. vtable+0x2c call(EntityTypePacked).
    //    Return work item.
    //
    // BLOCKED: HIBYTE check + fleet infra required.
    // Proxy: returns MissionExecutionWorkItem if fleet type.
    private AIWorkItem BuildLinkedIssue()
    {
        int category = (EntityTypePacked >> 24) & 0xff;
        if (category < 0x90 || category >= 0x98) return null;
        return new MissionExecutionWorkItem(EntityTypePacked, Workspace);
    }

    // FUN_00473700: state 8 cleanup when precondition failed. Assembly trace (fully read).
    //
    // 1. Iterate entities at this+0x24:
    //    For each: sub_4753b0(this, nodeKey) — cleanup function on entity.
    // 2. FUN_004754d0(this) — some entity cleanup (reset TargetObject?).
    // 3. Check HIBYTE(EntityTypePacked=this+0x30) in [0x90,0x98): if fleet:
    //    Look up EntityTypePacked in FactionContext+0x2c (system analysis).
    //    If found: reset EntityTypePacked via sub_4ec230.
    //              *(sys+0x30) &= ~0x80000 — clear PresenceFlags bit 0x80000 (attack marker).
    //
    // Note: clears PresenceFlags (this+0x30 IN SYSREC = +0x30), NOT FlagA (+0x28).
    // BLOCKED: entity list at this+0x24 + FactionContext infra required.
    // Proxy: clear InProgressFlag and PresenceFlags bit 0x80000 in SystemAnalysis.
    private void RunTargetCleanup()
    {
        InProgressFlag = 0;
        if (Workspace != null)
        {
            foreach (SystemAnalysisRecord rec in Workspace.SystemAnalysis)
                if ((rec.PresenceFlags & 0x80000) != 0)
                    rec.PresenceFlags &= ~0x80000;  // PresenceFlags bit 0x80000 (attack marker), not FlagA!
        }
    }
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

    // The planet where this unit should be built.
    public Planet TargetPlanet { get; set; }

    // The planet where the completed unit should be delivered.
    public Planet Destination { get; set; }

    /// <summary>
    /// vtable+0x14 equivalent. Called by Type 12 TryDispatchProductionEntry.
    /// Creates a ProductionWorkItem if this entry is ready to be manufactured.
    /// Writes 0 to dispatchOut when the work is absorbed (manufacturing started).
    /// Writes 1 to dispatchOut when still pending or unable to proceed.
    /// </summary>
    public AIWorkItem Dispatch(out int dispatchOut)
    {
        dispatchOut = 1; // default: pending

        if (Unit == null || TargetPlanet == null || IsCancelled)
            return null;

        if (Status != ProductionStatus.Active)
            return null;

        // Clear NeedsProcessing since we're handling it now.
        NeedsProcessing = false;
        dispatchOut = 0; // absorbed: work is being dispatched
        return new ProductionWorkItem(TargetPlanet, Unit, Destination ?? TargetPlanet);
    }
}

/// <summary>
/// Production tracking entry status codes, corresponding to vtable+0xc return values.
/// </summary>
public enum ProductionStatus
{
    Active = 1,
    Complete = 2,
}

/// <summary>
/// Abstract base for the fleet assignment target object at MissionTargetEntry+0x5c.
/// Drives capacity preparation, state update, and assignment commit via virtual dispatch.
/// Concrete implementations correspond to the various fleet entity types.
/// </summary>
public abstract class FleetAssignmentTarget
{
    /// <summary>Inner assignment state. Checked by AcceptIntoCandidate: must be in [0x8, 0x10).</summary>
    public int InnerState { get; set; }

    /// <summary>
    /// Required capacity array populated by PrepareCapacityArray().
    /// 10 elements (indices 0-9) subtracted from MissionTargetEntry's accumulators by SubtractFleetCapacity.
    /// </summary>
    public int[] RequiredCapacity { get; } = new int[10];

    /// <summary>
    /// Fills RequiredCapacity[0-9] from this fleet's current state.
    /// Called by SubtractFleetCapacity immediately before the accumulator subtraction loop.
    /// </summary>
    public abstract void PrepareCapacityArray();

    /// <summary>
    /// Vtable slot 4 (FUN_004f54d0). Called by UpdateAssignmentState when AssignmentId bit
    /// 0x2000000 is set. Receives the owning MissionTargetEntry; walks the linked list at
    /// ContextObject via +0x1c links searching for a node whose type code returns 0xf2.
    /// </summary>
    public abstract void UpdateAssignmentStateInfo(MissionTargetEntry entry);

    /// <summary>
    /// Vtable slot 8 (FUN_004fd9b0). Called by CommitFleetAssignment. Return value is discarded.
    /// </summary>
    public abstract void CommitAssignment();

    /// <summary>
    /// Vtable slot 6 (+0x18, FUN_004fd970). Called by ScanShipTypeList.
    /// Populates MissionTargetEntry.PendingMissionTypeId and MissionParam.
    /// Exact implementation requires research pass.
    /// </summary>
    public abstract void GetMissionInfo(MissionTargetEntry entry);

    /// <summary>
    /// Vtable slot 7 (+0x1c, FUN_004fd9a0). Called by ScanProductionList with &amp;PendingProductionId.
    /// Populates PendingProductionId. Exact implementation requires research pass.
    /// </summary>
    public abstract void GetProductionInfo(MissionTargetEntry entry);

    // Vtable slot 20 left for research pass.
}

/// <summary>
/// One entry in AIWorkspace.FleetAssignmentTable (workspace+0x58).
/// Tracks a fleet registered for a mission assignment, carrying the capacity contributions
/// that were made when the fleet was accepted, so they can be subtracted on removal.
/// Searched by ID via AIWorkspace.FindFleetAssignment.
/// </summary>
public class FleetAssignmentRecord
{
    /// <summary>Lookup key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Flag word. Relevant bits:
    ///   0x4000 = pass condition in CleanupProgressFlag (entity is still eligible).
    ///   0x20000 = set on removal; bits 24-25 (0x3000000) cleared on removal.
    ///   0x80 = contributes 1 to CapacityAccumulator4 (count gate).
    ///   0x400 = contributes 1 to CapacityAccumulator6 (count gate).
    ///   0x2 = contributes 1 to CapacityAccumulator9 (count gate).
    ///   0x801000 = pass condition in BuildAssignmentCandidate inner filter.
    ///   0x800 = fail condition in BuildAssignmentCandidate inner filter.
    /// </summary>
    public int Flags { get; set; }

    /// <summary>ID of the sub-object used to look up the FleetAvailabilityRecord in CleanupProgressFlag.</summary>
    public int SubObjectId { get; set; }

    /// <summary>Type/family ID. Must equal MissionTargetEntry.FilterTypeId to pass cleanup checks.</summary>
    public int TypeId { get; set; }

    /// <summary>Assignment type reference. Zeroed on removal when TypeId matched FilterTypeId.</summary>
    public int AssignmentTypeRef { get; set; }

    // Capacity contribution snapshots — summed into MissionTargetEntry accumulators when added,
    // subtracted back out by RemoveAssignment when removed.
    public int Acc0 { get; set; }
    public int Acc1 { get; set; }
    public int Acc2 { get; set; }
    public int Acc3 { get; set; }
    public int Acc5 { get; set; }
    public int Acc7 { get; set; }
    public int Acc8 { get; set; }
}

/// <summary>
/// One entry in AIWorkspace.FleetAvailabilityTable (workspace+0x78).
/// Represents a fleet entity available for assignment. Carries per-entity capacity data
/// consumed by AccumulateCapacityData, and a SubEntries list used by BuildAssignmentCandidate.
/// Searched by ID via AIWorkspace.FindFleetAvailability.
/// </summary>
public class FleetAvailabilityRecord
{
    /// <summary>Lookup key.</summary>
    public int Id { get; set; }

    /// <summary>+0x28: source fleet reference, passed to FleetAssignmentCandidateWorkItem.InitializeSourceRef.</summary>
    public int SourceRef { get; set; }

    /// <summary>+0x30: type/family ID. Must equal MissionTargetEntry.FilterTypeId to pass cleanup checks.</summary>
    public int TypeId { get; set; }

    /// <summary>+0x28 (overlapping range): assignment reference. Set to MissionTargetEntry.AssignmentTargetId on accept; cleared on candidate removal.</summary>
    public int AssignmentRef { get; set; }

    /// <summary>
    /// +0x24: primary flag word.
    ///   0x1 = entity is active (CleanupAssignmentList pass condition).
    ///   0x4 = availability flag checked/cleared by AcceptIntoCandidate.
    ///   0x3000 = BuildAssignmentCandidate pass condition (HIBYTE bits 4-5 = bits 12-13).
    ///   0xf0000802 = BuildAssignmentCandidate fail condition (any of these set → skip).
    /// </summary>
    public int Flags { get; set; }

    /// <summary>
    /// +0x38: category flag word.
    ///   bit 0x10 = counts toward CapacityAccumulator4.
    ///   bit 0x20 = counts toward CapacityAccumulator6.
    /// </summary>
    public int CategoryFlags { get; set; }

    // Capacity data fields. Offset names are from the original binary layout.
    // Semantic names will be resolved in the research pass.
    public int CapFieldB4 { get; set; } // +0xb4: feeds CapacityAccumulator0 (sum)
    public int CapFieldB8 { get; set; } // +0xb8: feeds CapacityAccumulator1 (sum)
    public int CapField84 { get; set; } // +0x84: feeds CapacityAccumulator2 (sum), snapshot to CapacitySnapshot0
    public int CapField78 { get; set; } // +0x78: feeds CapacityAccumulator3 (sum), snapshot to CapacitySnapshot1
    public int CapField48 { get; set; } // +0x48: compared against CapacityAccumulator5 in EvaluateCapacityCondition
    public int CapField60 { get; set; } // +0x60: compared against CapacityAccumulator3 in EvaluateCapacityCondition
    public int CapField68 { get; set; } // +0x68: feeds CapacityAccumulator5 (sum)
    public int CapField6C { get; set; } // +0x6c: compared against CapacityAccumulator2 in EvaluateCapacityCondition (when Flags & 0xc)
    public int CapField7C { get; set; } // +0x7c: feeds CapacityAccumulator7 (sum); compared against CapacityAccumulator0
    public int CapField70 { get; set; } // +0x70: feeds CapacityAccumulator8 (sum)
    public int CapField80 { get; set; } // +0x80: compared against CapacityAccumulator1 in EvaluateCapacityCondition
    public int CapField9C { get; set; } // +0x9c: feeds CapacityAccumulator9 (sum)

    /// <summary>Sub-assignment entries iterated by BuildAssignmentCandidate's inner loop.</summary>
    public List<SubAssignmentRecord> SubEntries { get; } = new List<SubAssignmentRecord>();
}

/// <summary>Minimal ID-holder for sub-entries within FleetAvailabilityRecord.SubEntries.</summary>
public class SubAssignmentRecord
{
    public int Id { get; set; }
}

/// <summary>Minimal ID-holder for entries in MissionTargetEntry.AssignmentRegistryList.</summary>
public class AssignmentRecord
{
    public int Id { get; set; }
}

/// <summary>Minimal ID-holder for entries in MissionTargetEntry.CandidateList.</summary>
public class CandidateRecord
{
    public int Id { get; set; }
}

/// <summary>
/// Work item produced by MissionTargetEntry.BuildAssignmentCandidate (FUN_00476e90).
/// TypeCode 0x270 in the original binary. Carries the filtered assignment IDs and source
/// fleet reference; consumed by the fleet assignment negotiation pipeline.
/// </summary>
public class FleetAssignmentCandidateWorkItem : AIWorkItem
{
    /// <summary>Type code 0x270 identifies this as a fleet assignment candidate work item.</summary>
    public override int TypeCode => 0x270;

    /// <summary>Faction/side identifier, copied from MissionTargetEntry.OwnerSide.</summary>
    public int OwnerSide { get; set; }

    /// <summary>Assignment IDs collected from FleetAvailabilityRecord.SubEntries that passed the filter.</summary>
    public List<int> AssignmentIds { get; } = new List<int>();

    /// <summary>Source fleet reference from FleetAvailabilityRecord.SourceRef.</summary>
    public int SourceRef { get; set; }

    /// <summary>
    /// Vtable+0x24 initializer. Called after AssignmentIds is populated.
    /// Exact semantics require research pass (callee not yet read).
    /// </summary>
    public virtual void InitializeAssignmentIds(List<int> ids) { }

    /// <summary>
    /// Vtable+0x2c initializer. Called with FleetAvailabilityRecord.SourceRef.
    /// Exact semantics require research pass (callee not yet read).
    /// </summary>
    public virtual void InitializeSourceRef(int sourceRef) { }

    /// <summary>
    /// Dispatch implementation. Exact behavior requires research pass (callee not yet read).
    /// </summary>
    public override bool Dispatch(out AIDispatchResult result)
    {
        result = default;
        return false;
    }
}
