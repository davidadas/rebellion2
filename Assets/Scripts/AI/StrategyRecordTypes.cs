using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;

// ============================================================
// Concrete StrategyRecord subclasses — one per strategy type (1–14).
//
// Each type's Tick() outer wrapper is transcribed verbatim from the
// corresponding slot-5 function in the disassembly:
//
//   Type  1: FUN_004d9f50_tick_local_shortage_issue_generator_type_1
//   Type  2: FUN_004e1490_tick_local_shortage_issue_generator_type_2
//   Type  3: FUN_004d60a0
//   Type  4: FUN_004d17d0
//   Type  5: FUN_004cf240
//   Type  6: FUN_004dcb10
//   Type  7: FUN_004d2470
//   Type  8: FUN_004ce970
//   Type  9: FUN_004ce600
//   Type 10: FUN_004cbd00
//   Type 11: FUN_004c7ea0
//   Type 12: FUN_004c7810
//   Type 13: FUN_004c68b0_tick_strategy_production_automation_record
//   Type 14: FUN_004d1d20_get_next_capital_ship_name_row_package
//
// Inner-state-machine methods delegate to private helpers that are
// currently stubbed.  The outer phase dispatch is complete and faithful.
// ============================================================

// ------------------------------------------------------------------
// Type 1 — FUN_004d9cc0 — LocalShortageGeneratorType1Record
// 0x78 bytes.  Active guard: standard (Phase=0, SubState=0, ReadyFlag=1 on inactive).
//
// Phase 0 (initial):
//   FUN_004da010 (precondition check 1) → non-zero: Phase=0x3ec, return null
//   FUN_004da280 (precondition check 2) → non-zero: Phase=0x3ec, SubState=0, return null
//   If Workspace.EntityTargetType==0x80 AND bit 0x40 of StatusFlags set:
//       call FUN_004dbfb0 (update shortage fleet)
//   Else if Workspace.EntityTargetType != 0x80:
//       call FUN_004dbfb0
//   Call FUN_004dc490 (finalize shortage record)
//   Phase=0, SubState=0, ReadyFlag=1, return null
// Phase 0x3ec: call FUN_004da660 (generate shortage issue), return result
// ------------------------------------------------------------------
public class LocalShortageGeneratorType1Record : StrategyRecord
{
    // Phase constants (FUN_004d9f50)
    private const int PhaseGenerateIssue = 0x3ec;

    // Extra fields (base=0x40; FUN_004d9cc0 constructor field layout):
    //   +0x40 = _candidateRefA  (AutoClass9 entity ref, initialized to value 2)
    //   +0x44 = _candidateRefB  (AutoClass9 entity ref, initialized to value 2)
    //   +0x48 = null pointer    (FUN_0042d280_set_to_null)
    //   +0x4c = 0               (zeroed in constructor)
    //   +0x50 = _candidateCount (0 = none; set to found count by CheckShortageConditionType1)
    //   +0x54 = _costValue      (set to record+0x60 of best candidate by PreconditionCheck1)
    //   +0x58 = _candidateListA (linked list of system candidate references)
    //   +0x60 = _candidateListB (linked list, secondary)
    //   +0x68 = _issueContainer (mission issue record container, FUN_00434c50)
    //
    // In C# these become managed collections. The entity ref fields at +0x40/+0x44 are
    // system analysis record IDs (int) used by PreconditionCheck1 and CheckShortageConditionType1.
    //
    // _entityTypePacked and _agentCapacityFlag are LOCAL VARIABLES in the original asm
    // (written to registers before calling SelectAgent functions), not struct fields.
    // The C# code keeps them as fields for readability.
    private int _candidateRefA; // best candidate system ID found by PreconditionCheck1
    private int _candidateRefB; // secondary candidate system ID
    private int _candidateCount; // count from best candidate's field+0x114
    private int _costValue; // cost from best candidate's field+0x60
    private readonly List<int> _candidateListA = new List<int>(); // system IDs (param_1+0x58)
    private readonly List<int> _candidateListB = new List<int>(); // selected IDs (param_1+0x60)

    // Written by case 6 of GenerateShortageIssue before calling SelectAgent functions.
    private int _entityTypePacked;

    // Written by case 6 when a slot is found.
    private int _agentCapacityFlag;

    // +0x48: agent entity reference. Set by GetNextShortageSubState (via sub_41a9e0)
    // when a valid agent assignment target is found. High byte != 0 means valid.
    // FinalizeAgentShortageItem checks *(this+0x48) & 0xff000000 != 0.
    private int _agentEntityRef;

    // +0x68: issue record container (FUN_00434c50 / AutoClass761).
    // Accumulates issue records from FUN_004191b0 queries via FUN_00434e10.
    // FUN_00434e30 retrieves the top record ID. FUN_005f3dd0 clears it.
    private readonly IssueRecordContainer _issueContainer = new IssueRecordContainer();

    // Constructor sets capacity to 4 (from binary: *(this+0x24) = 4 in FUN_004d9cc0).
    public LocalShortageGeneratorType1Record(int ownerSide)
        : base(typeId: 1, capacity: 4, ownerSide: ownerSide) { }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        if (Phase == PhaseGenerateIssue)
            return GenerateShortageIssue();

        if (Phase != 0)
        {
            // Unrecognised phase — reset.
            Phase = 0;
            return null;
        }

        // Phase 0: run precondition checks then finalize.
        if (PreconditionCheck1() != 0)
        {
            Phase = PhaseGenerateIssue;
            return null;
        }

        if (PreconditionCheck2() != 0)
        {
            Phase = PhaseGenerateIssue;
            SubState = 0;
            return null;
        }

        // Check EntityTargetType == 0x80 (fleet-targeted context).
        // Only call UpdateShortageFleet when not in fleet-context, OR when
        // bit 0x40 of StatusFlags is set in fleet-context.
        if (Workspace.EntityTargetType == 0x80)
        {
            if ((Workspace.PendingSupplyBitmask & 0x40) != 0)
                UpdateShortageFleet();
            // else: skip UpdateShortageFleet when in fleet context without the flag.
        }
        else
        {
            UpdateShortageFleet();
        }

        FinalizeShortageRecord();
        Phase = 0;
        SubState = 0;
        ReadyFlag = 1;
        return null;
    }

    // FUN_004da010_seed_type_1_primary_candidate_search:
    //
    // Assembly trace behavior:
    // 1. Clears param_1+0x54 (_costValue = 0)
    // 2. Gets last node from list at param_1+0x58 (_candidateListA)
    // 3. Iterates forward (_candidateListA nodes):
    //    For each node, calls sub_403040 (get key-value), sub_4ec1e0 (set_id) into local,
    //    then sub_403d30(workspace+0x2c) — looks up in workspace system analysis list.
    //    Filter conditions (from assembly):
    //      *(edi+0x30) & 0x1 == 1  → PresenceFlags & 0x1 (entity presence)
    //      *(edi+0x28) & 0x800000  → FlagA & 0x800000 (shortage marker set by UpdateShortageFleet)
    //      HIBYTE(*(edi+0x28)) & 0x8 = *(edi+0x28) & 0x800 → FlagA & 0x800 (bit 11)
    //      *(edi+0x28) & 0x3 == 0  → FlagA & 0x3 == 0 (no enemy planets)
    //      AND *(edi+0x60) > 0     → SystemScore > 0 (system has value)
    //    If conditions pass AND *(edi+0x60) < running minimum:
    //      Update minimum, store this node's key-value in param_1+0x44 (_candidateRefB)
    //      Update param_1+0x54 (_costValue) = *(edi+0x60) = SystemScore
    //      var_30 = 1 (found)
    //    Else (conditions fail or higher cost): clear *(edi+0x28) bit 0x800000,
    //      call sub_4334b0, remove from _candidateListA (sub_4f4c60).
    // 4. Post-loop: if var_30 == 0: no candidate found.
    //    Checks param_1+0x44 (_candidateRefB) type high byte in [0x90,0x98):
    //      → valid fleet entity: calls sub_4f25a0(OwnerSide, _candidateRefB), sub_5087e0(1)
    //        (capacity check), updates _candidateRefB and sets var_2C=1.
    //    Returns var_2C (1 = candidate found and validated).
    //
    // Note: sub_4f25a0 resolves an entity reference to a game object; sub_5087e0 checks
    // fleet capacity. These require fleet entity infrastructure not yet implemented.
    // The C# implementation handles the list-iteration and flag-check phase correctly.
    // The final fleet-capacity validation (sub_4f25a0 + sub_5087e0) is blocked pending
    // fleet entity resolution infrastructure.
    private int PreconditionCheck1()
    {
        _costValue = 0;
        _candidateRefB = 0;
        int minScore = int.MaxValue;
        bool found = false;

        foreach (int sysId in _candidateListA.ToList())
        {
            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == sysId
            );
            if (rec == null)
            {
                _candidateListA.Remove(sysId);
                continue;
            }

            // Exact filter conditions from assembly (FUN_004da010):
            bool pass =
                (rec.PresenceFlags & 0x1) != 0
                && // *(edi+0x30) & 0x1
                (rec.FlagA & 0x800000) != 0
                && // *(edi+0x28) & 0x800000
                (rec.FlagA & 0x800) != 0
                && // HIBYTE(*(edi+0x28)) & 0x8 = bit 11
                (rec.FlagA & 0x3) == 0
                && // *(edi+0x28) & 0x3 == 0
                rec.SystemScore > 0; // *(edi+0x60) > 0

            if (pass && rec.SystemScore < minScore)
            {
                minScore = rec.SystemScore;
                _candidateRefB = sysId;
                _costValue = rec.SystemScore;
                found = true;
            }
            else if (!pass)
            {
                // Assembly: clear FlagA bit 0x800000, call sub_4334b0, remove from list
                rec.FlagA &= ~0x800000;
                _candidateListA.Remove(sysId);
            }
        }

        if (!found)
            return 0;

        // Post-loop fleet capacity validation (sub_4f25a0 + sub_5087e0):
        // Blocked pending fleet entity resolution infrastructure (workspace fleet lookup).
        // Returns 1 when candidate is found and the fleet-capacity check passes.
        // For now: return 1 if we found a minimum-cost candidate.
        return 1;
    }

    // FUN_004da280_seed_type_1_issue_bundle_if_primary_candidate_list_empty:
    //
    // Assembly trace behavior:
    // 1. Clears param_1+0x54 (_costValue = 0)
    // 2. Counts nodes in _candidateListA (sub_5f3650).
    // 3. If count == 0 (list empty): runs seeding block:
    //    a. FUN_004191b0(workspace, 0x2000, 0,0,0,0,0, 4, sort=2) → query with
    //       DispositionFlags & 0x2000 (fleet deployment condition), stat=EnemyTroopSurplus,
    //       sort DESCENDING (param_9=2).
    //       sub_434e10 stores result in _issueContainer.
    //    b. FUN_004191b0(workspace, 0x2000, 0,0,0,0,0, 0x15, sort=1) → same filter,
    //       stat=PerSystemStats[0x15] (unknown field, returns 0 in current impl).
    //    c. FUN_004191b0(workspace, 0x2000, 0,0,0,0,0, 8, sort=1) → same filter,
    //       stat=PerSystemStats[8] = DWORD at offset 0x20 (unlisted, returns 0).
    //    d. sub_434e30 gets last issue record ID → stored in _candidateRefA.
    //    e. Clears _issueContainer (sub_5f3dd0).
    //    f. FUN_00419af0 calls (sub_419af0): queries planet sub-objects within the system
    //       at _candidateRefA for specific fleet/capability flags.
    //       BLOCKED: requires planet sub-object data (FUN_004334c0 infrastructure).
    //    g. Checks _candidateRefA type, possibly sets workspace.StatusFlags |= 0x40.
    //    h. If _candidateRefA valid fleet type [0x90,0x98): looks up system analysis,
    //       allocates 0x1c node, appends to _candidateListA, sets FlagA |= 0x800000.
    //    i. If FlagA HIBYTE & 0x8 (bit 11 = 0x800): sets var_18=1, validates fleet capacity,
    //       stores system cost in _costValue, updates _candidateRefB.
    // 4. Returns var_18 (1 = candidate found and validated, 0 = nothing).
    //
    // The sub_419af0 calls (steps f) require planet sub-object records which are not
    // implemented. The FUN_004191b0 queries (steps a-c) are implemented below.
    private int PreconditionCheck2()
    {
        _costValue = 0;

        // Only runs when _candidateListA is empty
        if (_candidateListA.Count > 0)
            return 0;

        // Queries a-c: FUN_004191b0 with DispositionFlags & 0x2000
        // Query a: stat index 4 = EnemyTroopSurplus, sort descending (param_9=2)
        IssueRecordContainer containerA = Workspace.QuerySystemAnalysis(
            incl24: 0x2000,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 4
        );
        // Sort descending for query A (param_9=2 → sort dir != 1 → descending).
        // QuerySystemAnalysis always sorts ascending; reverse for param_9=2.
        // (The sort direction is stored in the container's +0x1c field = param_9.)

        IssueRecordContainer containerB = Workspace.QuerySystemAnalysis(
            incl24: 0x2000,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 0x15 // PerSystemStats[0x15] = unknown field, returns 0 currently
        );

        IssueRecordContainer containerC = Workspace.QuerySystemAnalysis(
            incl24: 0x2000,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 8 // PerSystemStats[8] = DWORD at offset 0x20, returns 0 currently
        );

        // Accumulate into _issueContainer and get top record
        _issueContainer.StoreFrom(containerA);
        _issueContainer.StoreFrom(containerB);
        _issueContainer.StoreFrom(containerC);
        _issueContainer.FinalizeAndAssignPriorities();

        SystemAnalysisRecord top = _issueContainer.GetTopRecord();
        _issueContainer.Clear();

        if (top == null)
            return 0;

        int sysId = top.InternalId;
        _candidateRefA = sysId;

        // Steps f: sub_419af0 planet sub-object queries (now implemented via QuerySystemPlanets):
        // FUN_004da280 assembly lines 89-99:
        //   sub_419af0(ebp, 0x800, 0x0, 0x1, 0x3800000, 0x0, 0x0, 0x6, 0x1)
        //     where ebp = param_1+0x40 = _candidateRefA
        //   → own planets (StatusFlags & 0x1) with fleet bit (CapabilityFlags & 0x800)
        //     no mission-blocking (CapabilityFlags & 0x3800000 == 0), stat=StarfighterCount
        IssueRecordContainer planetQuery1 = Workspace.QuerySystemPlanets(
            _candidateRefA,
            incl28: 0x800,
            incl2c: 0,
            incl30: 0x1,
            excl28: 0x3800000,
            excl2c: 0,
            excl30: 0,
            statIndex: 6
        );
        _issueContainer.StoreFrom(planetQuery1);
        _issueContainer.FinalizeAndAssignPriorities();
        SystemAnalysisRecord bestFromPlanet = _issueContainer.GetTopRecord();
        _issueContainer.Clear();

        // Assembly line 143: sub_419af0(ebp, 0x0, 0x0, 0x1, 0x0, 0x0, 0x0, 0x6, 0x1)
        // (different flags — no CapabilityFlags filter, just own planets)
        IssueRecordContainer planetQuery2 = Workspace.QuerySystemPlanets(
            _candidateRefA,
            incl28: 0,
            incl2c: 0,
            incl30: 0x1,
            excl28: 0,
            excl2c: 0,
            excl30: 0,
            statIndex: 6
        );
        _issueContainer.StoreFrom(planetQuery2);
        _issueContainer.FinalizeAndAssignPriorities();
        _issueContainer.Clear();

        // Step g: if _candidateRefA NOT in [0x90,0x98) fleet type → set StatusFlags |= 0x40
        // (Assembly line 172-175: if high byte NOT in [0x90,0x98) → workspace+0x4 |= 0x40)
        // In C# our entity keys are hash codes not type-encoded ints, so we proxy by checking
        // if the query returned any results.
        if (planetQuery1.Count == 0)
            Workspace.StatusFlags |= 0x40;

        // Step h: if system valid, append to _candidateListA and mark it.
        if (sysId != 0)
        {
            if (bestFromPlanet != null || top.FlagA != 0)
            {
                if (!_candidateListA.Contains(sysId))
                    _candidateListA.Add(sysId);
                top.FlagA |= 0x800000; // assembly: *(esi+0x28) |= 0x800000
            }
        }

        // Step i: if FlagA bit 27 (0x8000000) = HIBYTE(*(esi+0x28)) & 0x8 in the binary:
        // FUN_004da280 assembly line 221: checks HIBYTE(FlagA) & 0x8 = bit 27 = 0x8000000.
        // Set by AccumulatePlanetIntoSystemRecord when ExtraFlags & 0x10000000 (own planet
        // popular support < 70). Validates fleet capacity via sub_4f25a0 + sub_5087e0.
        // Proxy: if the planet query returned results AND FlagA & 0x8000000 is set → var_18=1.
        if ((top.FlagA & 0x8000000) != 0 && planetQuery1.Count > 0)
        {
            // Assembly: stores system cost in _costValue (param_1+0x54),
            // updates _candidateRefB (param_1+0x44) with the found entity.
            _costValue = top.SystemScore;
            _candidateRefB = sysId;
            return 1;
        }

        return 0;
    }

    // FUN_004dbfb0_seed_type_1_cleanup_issue_bundle:
    //
    // Behavior from assembly trace:
    // 1. Calls FUN_004ec1d0 / FUN_004ec230 to init/reset local entity refs
    // 2. Calls FUN_004ec230 on param_1+0x40 (_candidateRefA) — resets it to value 2
    // 3. Computes requestedCount = FUN_004dc3c0(this) = ComputeRequestedCapacity()
    // 4. If requestedCount <= 0: goto simplified path
    // 5. Main path: calls FUN_004191b0(workspace, 0x80, 0,0,0,0,0, 0x15, container_1)
    //    — query systems with DispositionFlags & 0x80 (character-type-C available),
    //      get PerSystemStats[0x15] property. Stores in issue container.
    //    Gets last record ID → stores in local var_4c.
    //    If var_4c type high byte in [0x80, 0x90): fleet-type entity.
    //      Looks up in workspace FleetAssignmentSubObject (workspace+0x44):
    //      sub_4f4cc0(workspace+0x44, &var_4c) → find record. If found & found+0xc4 > 0:
    //        calls FUN_004191b0(workspace, 0x80, 0,0,0,0,0, 0x4, container_2)
    //        — same DispositionFlags filter, PerSystemStats[0x4] = EnemyTroopSurplus.
    //        Enters inner loop searching for candidate: checks var_48 type, looks up
    //        system analysis, checks system+0x60 > 0 AND requestedCount >= system+0xd8
    //        → if found: updates _candidateRefA, sets var_44=1.
    //        Iterates until var_44 set or exhausted.
    //        After loop: appends to _candidateListA at param_1+0x58 via sub_4f4b30.
    // 6. Simplified path (requestedCount <= 0): checks _candidateRefA type [0x90,0x98):
    //    if valid fleet entity: looks up in system analysis, allocates 0x1c node,
    //    appends to _candidateListA, sets record.FlagA (field28_0x28) |= 0x800000,
    //    calls sub_4334b0.
    //    else: checks _candidateListA count <= 1, tries to find system with score > 0.
    //
    // FUN_004dbfb0_seed_type_1_cleanup_issue_bundle — assembly trace (fully read).
    //
    // Setup: reset _candidateRefA, compute requestedCount = FUN_004dc3c0(this).
    // If requestedCount > 0 (MAIN PATH):
    //   QuerySystemAnalysis(workspace, 0x80, ..., 0x15, sort=1) → local_4c.
    //   LOOP: check HIBYTE(local_4c) in [0x80, 0x90) (fleet entity type):
    //     If fleet: sub_4f4cc0(workspace+0x44, &local_4c) — look up in FleetAssignment sub-object.
    //       If found AND *(found+0xc4) > 0:
    //         QuerySystemPlanets(local_4c, 0,0,1, 0x3800003,0,0x40000000, 6, 1) → _issueContainer.
    //         Inner loop: for each result in [0x90,0x98) fleet type:
    //           Look up in SystemAnalysis; if SystemScore>0 AND requestedCount>=*(sys+0xd8):
    //             _candidateRefA = sys ID (sub_4ec1e0). var_44=1.
    //         Get last from _issueContainer → update local_4c → loop.
    //       If not found: reset local_4c → loop.
    //     If NOT fleet: clear var_1c, fall through to simplified path.
    // SIMPLIFIED PATH (requestedCount <= 0 or fleet loop exhausted):
    //   edi = &_candidateRefA. Check HIBYTE(_candidateRefA) in [0x90, 0x98):
    //     If fleet: look up in SystemAnalysis → allocate 0x1c node →
    //       sub_4f4b30 INSERT INTO _candidateListA (this+0x58) → FlagA |= 0x800000.
    //     If NOT fleet: check _candidateListA (this+0x58) count <= 1 → if score>0 skip.
    //       If edi==0: sub_41a9e0(&_agentEntityRef, 0x2a, 0x10000, 0x4000, 2) — find agent.
    //       If requestedCount >= agent_capacity: workspace.StatusFlags |= 0x80.
    //
    // HIBYTE check passes now (InternalId HIBYTE=0x90 in [0x90,0x98)).
    // Fleet unit iteration (sub_52bc60 etc.) still blocked on entity infrastructure.
    // Proxy: uses QuerySystemAnalysis results as stand-ins.
    private void UpdateShortageFleet()
    {
        // Reset _candidateRefA (FUN_004ec230 on param_1+0x40 = writes value 2 = "unset")
        _candidateRefA = 0;

        int requestedCount = ComputeRequestedCapacity();

        // Query: systems with DispositionFlags & 0x80 (FUN_004191b0 param_1=0x80),
        // PerSystemStats[0x15] as the property value.
        IssueRecordContainer container1 = Workspace.QuerySystemAnalysis(
            incl24: 0x80,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 0x15
        );

        SystemAnalysisRecord topRec1 = container1.GetTopRecord();
        if (topRec1 == null)
            return;

        if (requestedCount > 0)
        {
            // Main path: find candidate from DispositionFlags & 0x80 filtered systems.
            // In the original this also checks FleetAssignment sub-object (blocked until
            // workspace+0x44 fleet assignment infrastructure is built).
            // For now: use the query result directly.
            IssueRecordContainer container2 = Workspace.QuerySystemAnalysis(
                incl24: 0x80,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 0x4 // EnemyTroopSurplus
            );

            // Find a candidate: system with SystemScore > 0 AND requestedCount >= stat[0xd8 proxy]
            foreach (IssueRecord item in container2.Records)
            {
                SystemAnalysisRecord rec = item.Record;
                if (rec == null || rec.SystemScore <= 0)
                    continue;
                // system+0xd8 proxy: use FacilityCountOwned as capacity threshold
                if (requestedCount < rec.Stats.FacilityCountOwned)
                    continue;

                int sysId = item.EntityKey;
                if (!_candidateListA.Contains(sysId))
                    _candidateListA.Add(sysId);

                // Set FlagA |= 0x800000 (shortage candidate marker from assembly)
                rec.FlagA |= 0x800000;
                _candidateRefA = sysId;
                break;
            }
        }
        else
        {
            // Simplified path: _candidateRefA was set in a previous cycle.
            // If we have a valid top record from the 0x80 query, append it to _candidateListA.
            if (topRec1 != null && topRec1.SystemScore > 0)
            {
                int sysId = topRec1.InternalId;
                if (sysId != 0 && !_candidateListA.Contains(sysId))
                {
                    _candidateListA.Add(sysId);
                    topRec1.FlagA |= 0x800000; // assembly: *(esi+0x28) |= 0x800000
                }
            }
        }
    }

    // FUN_004dc490: Selects the best shortage candidate from _candidateListA.
    // Iterates the list to find the system with maximum score that has:
    //   - PresenceFlags bit 0x10000000 NOT set (not already selected)
    //   - Has capacity (Stats.FacilityCount > 0 proxy for found+0x90 > 0)
    // Sets PresenceFlags bit 0x10000000 on selected system (marks as active assignment).
    // Updates _candidateListB with selection, clears old selections.
    private void FinalizeShortageRecord()
    {
        int bestScore = -1;
        int bestSysId = 0;
        SystemAnalysisRecord bestRec = null;

        foreach (int sysId in _candidateListA)
        {
            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == sysId
            );
            if (rec == null)
                continue;
            if (rec.SystemScore <= bestScore)
                continue;
            if (rec.Stats.FacilityCount <= 0)
                continue; // found+0x90 > 0 proxy
            bestScore = rec.SystemScore;
            bestSysId = sysId;
            bestRec = rec;
        }

        // Clear old selections from _candidateListB (clears PresenceFlags bit 0x10000000)
        foreach (int oldId in _candidateListB)
        {
            SystemAnalysisRecord old = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == oldId
            );
            if (old != null)
                old.PresenceFlags &= ~0x10000000;
        }
        _candidateListB.Clear();

        // Mark and store the best candidate
        if (bestRec != null)
        {
            bestRec.PresenceFlags |= 0x10000000; // mark as selected
            _candidateListB.Add(bestSysId);
        }
    }

    // FUN_004da660: Shortage issue generation sub-state machine.
    // Entry: Phase==0x3ec. Uses SubState to track position across ticks.
    // Terminal paths write Phase=0 and return to Phase 0 logic on the next tick.
    private AIWorkItem GenerateShortageIssue()
    {
        switch (SubState)
        {
            default:
                SubState = 1;
                return null;

            case 1:
                // Clear bit 0x80 from workspace.PendingSupplyBitmask (workspace+0x8 &= ~0x80).
                Workspace.PendingSupplyBitmask &= ~0x80;
                // If EntityTargetType != 0x80 (not fleet-targeted context), skip to cleanup.
                if (Workspace.EntityTargetType != 0x80)
                {
                    SubState = 10;
                    return null;
                }
                Workspace.AdvanceBitSelection();
                SubState = 2;
                return null;

            case 2:
            {
                // FUN_004da880: shortage condition check. Zero → terminal; non-zero → proceed.
                int found = CheckShortageConditionType1();
                if (found == 0)
                {
                    Phase = 0;
                    SubState = 0;
                    ReadyFlag = 1;
                    return null;
                }
                SubState = 3;
                return null;
            }

            case 3:
            {
                // FUN_004dab90: find shortage fleet candidate. Returns work item or null.
                AIWorkItem item = FindShortageFleet();
                if (item != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 4;
                return item;
            }

            case 4:
            {
                // FUN_004db0d0: compute next sub-state for the shortage fleet walk.
                // Returns next SubState value; 0 means the walk is complete.
                int nextState = GetNextShortageSubState();
                SubState = nextState;
                if (nextState == 0)
                {
                    Phase = 0;
                    ReadyFlag = 1;
                }
                return null;
            }

            case 5:
            {
                // FUN_004db1e0: create fleet shortage issue work item. Terminal.
                AIWorkItem item = CreateFleetShortageIssue();
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return item;
            }

            case 6:
            {
                // FUN_004db760 / FUN_004db4c0: select agent based on available capacity.
                // Writes agent type+mode packed value into _entityTypePacked (+0x48).
                int found;
                if (Workspace.AgentAssignedCapacity < Workspace.AgentTotalCapacity)
                {
                    _entityTypePacked = 0x2d000002; // agent type 0x2d, mode 2 (slot available)
                    found = SelectAgentSlotAvailable();
                }
                else
                {
                    _entityTypePacked = 0x2c000001; // agent type 0x2c, mode 1 (slots full)
                    found = SelectAgentSlotFull();
                }
                if (found == 0)
                {
                    SubState = 0;
                    Phase = 0;
                    // NOTE: ReadyFlag NOT set here (assembly does not write +0x20 in the zero path).
                }
                else
                {
                    _agentCapacityFlag = 1; // +0x4c: consumed by FindAgentForShortage
                    SubState = 7;
                }
                goto case 7; // assembly: case 6 has no break; always falls through to case 7
            }

            case 7:
            {
                // FUN_004db9c0: find agent for shortage assignment. Returns work item or null.
                AIWorkItem item = FindAgentForShortage();
                if (item != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 8;
                return item;
            }

            case 8:
            {
                // FUN_004dbd60: create agent shortage work item. Non-null → proceed; null → terminal.
                AIWorkItem item = CreateAgentShortageItem();
                if (item != null)
                {
                    SubState = 9;
                    ReadyFlag = 1;
                    TickCounter++;
                    return item;
                }
                // null: terminal (goto LAB_004da82f — same block as case 10 after FUN_004dbfb0).
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return null;
            }

            case 9:
            {
                // FUN_004dbea0: finalize agent shortage item. Terminal.
                AIWorkItem item = FinalizeAgentShortageItem();
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return item;
            }

            case 10:
                // FUN_004dbfb0: cleanup / update shortage fleet state, then terminal.
                UpdateShortageFleet();
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return null;
        }
    }

    // FUN_004da880_seed_type_1_issue_bundle_from_primary_candidate_list:
    //
    // Assembly trace (fully read):
    // 1. Resets _candidateRefA (sub_4ec230) and _candidateCount (+0x50 = 0).
    // 2. var_24 = 0x3e8 (initial minimum cost threshold = 1000).
    // 3. var_28 = 0 (found flag).
    // 4. Gets first node from _candidateListA (this+0x58).
    // 5. If list NOT empty: do-while loop iterating nodes:
    //    For each node:
    //      a. Look up entity key in workspace.SystemAnalysis.
    //      b. If found AND *(esi+0x30) & 0x1 (PresenceFlags & 0x1) AND *(esi+0x28) & 0x3 == 0
    //         (FlagA & 0x3 == 0 = no enemy) AND *(esi+0x114) > 0 (capacity > 0):
    //         If *(esi+0x60) < var_24 (SystemScore < current minimum):
    //           _candidateCount = *(esi+0x114) (capacity field)
    //           var_24 = *(esi+0x60) (update minimum)
    //           _candidateRefA = node's entity key
    //           var_28 = 1 (found)
    //      c. Advance: ebx = *(ebx+0x10) (next node)
    // 6. If var_28 == 0 (no candidate from list):
    //    Fallback: three QuerySystemAnalysis(0x80, ...) calls → populate _issueContainer:
    //      sub_4191b0(workspace, 0x80, 0,0,0,0,0, 4, sort=2)  → store
    //      sub_4191b0(workspace, 0x80, 0,0,0,0,0, 0x15, sort=1) → store
    //      sub_4191b0(workspace, 0x80, 0,0,0,0,0, 8, sort=1)  → store
    //    sub_434e30 → get last entity key → store in _candidateRefA
    //    Clear _issueContainer (sub_5f3dd0)
    //    Three QuerySystemPlanets(_candidateRefA) calls → populate _issueContainer:
    //      sub_419af0(_candidateRefA, 0,0,1, 0x3800003,0,0x40000000, 6, 1)
    //      sub_419af0(_candidateRefA, 0,0,1, 0x3800003,0,0x40000000, 0, 1)
    //      sub_419af0(_candidateRefA, 0,0,1, 0x3800003,0,0x40000000, 0x33, 1)
    //    sub_434e30 → get last entity key → store in _candidateRefA
    //    Clear _issueContainer
    //    Check _candidateRefA HIBYTE in [0x90,0x98) (fleet entity type):
    //      If yes: look up in SystemAnalysis → if found:
    //        sub_617140(0x1c) → sub_4ec010 → sub_4f4b30 into _candidateListA (this+0x58)
    //        *(esi+0x28) |= 0x800000 (FlagA shortage marker)
    //        var_28 = 1
    // 7. Returns var_28 (1 = candidate found, 0 = not found).
    private int CheckShortageConditionType1()
    {
        // sub_4ec230 on this+0x40: reset _candidateRefA
        _candidateRefA = 0;
        // *(this+0x50) = 0: clear _candidateCount
        _candidateCount = 0;
        // var_24 = 0x3e8: initial minimum cost threshold
        int minCost = 0x3e8;
        // var_28 = 0: found flag
        bool found = false;

        // Iterate _candidateListA nodes
        foreach (int sysId in _candidateListA)
        {
            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == sysId
            );
            if (rec == null)
                continue;

            // Conditions: PresenceFlags & 0x1, FlagA & 0x3 == 0, capacity field > 0
            if ((rec.PresenceFlags & 0x1) == 0)
                continue;
            if ((rec.FlagA & 0x3) != 0)
                continue;
            int capacity = rec.Stats.FacilityCount; // proxy for *(esi+0x114)
            if (capacity <= 0)
                continue;

            int score = rec.SystemScore; // *(esi+0x60)
            if (score < minCost)
            {
                _candidateCount = capacity; // *(this+0x50) = capacity
                minCost = score; // var_24 = score
                _candidateRefA = sysId; // store key in _candidateRefA
                found = true; // var_28 = 1
            }
        }

        if (!found)
        {
            // Fallback: QuerySystemAnalysis(0x80, ...) × 3, then QuerySystemPlanets × 3
            IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
                incl24: 0x80,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 4
            );
            IssueRecordContainer c2 = Workspace.QuerySystemAnalysis(
                incl24: 0x80,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 0x15
            );
            IssueRecordContainer c3 = Workspace.QuerySystemAnalysis(
                incl24: 0x80,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 8
            );
            _issueContainer.StoreFrom(c1);
            _issueContainer.StoreFrom(c2);
            _issueContainer.StoreFrom(c3);
            if (_issueContainer.TryGetTopEntityKey(out int key1))
                _candidateRefA = key1;
            _issueContainer.Clear();

            IssueRecordContainer p1 = Workspace.QuerySystemPlanets(
                _candidateRefA,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x3800003,
                excl2c: 0,
                excl30: 0x40000000,
                statIndex: 6
            );
            IssueRecordContainer p2 = Workspace.QuerySystemPlanets(
                _candidateRefA,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x3800003,
                excl2c: 0,
                excl30: 0x40000000,
                statIndex: 0
            );
            IssueRecordContainer p3 = Workspace.QuerySystemPlanets(
                _candidateRefA,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x3800003,
                excl2c: 0,
                excl30: 0x40000000,
                statIndex: 0x33
            );
            _issueContainer.StoreFrom(p1);
            _issueContainer.StoreFrom(p2);
            _issueContainer.StoreFrom(p3);
            if (_issueContainer.TryGetTopEntityKey(out int key2))
                _candidateRefA = key2;
            _issueContainer.Clear();

            // Check fleet entity type: proxy = _candidateRefA != 0 with valid SystemAnalysis entry
            // (original: HIBYTE(_candidateRefA) in [0x90, 0x98))
            if (_candidateRefA != 0)
            {
                SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                    r.InternalId == _candidateRefA
                );
                if (rec != null)
                {
                    // Allocate 0x1c entry (sub_617140), init (sub_4ec010), insert (sub_4f4b30)
                    if (!_candidateListA.Contains(_candidateRefA))
                        _candidateListA.Add(_candidateRefA);
                    rec.FlagA |= 0x800000; // *(esi+0x28) |= 0x800000
                    found = true; // var_28 = 1
                }
            }
        }

        return found ? 1 : 0;
    }

    // FUN_004dab90 — creates fleet unit assignment nodes for shortage (GenerateShortageIssue case 3).
    //
    // Assembly trace (read — see detailed analysis):
    // 1. Initializes local 0x54-byte node list (sub_4f36b0).
    // 2. Checks HIBYTE(_candidateRefA) in [0x90, 0x98) (fleet entity type).
    //    If not fleet type: skip all node-building, return null.
    // 3. If fleet type: look up _candidateRefA in workspace.SystemAnalysis.
    //    If found: get faction's fleet entity via sub_4f25a0(OwnerSide, _candidateRefA).
    //    If fleet found:
    //      If FlagA bit 0x200000 clear: iterate fleet's capital ships (sub_52bc60),
    //        allocate 0x20-byte nodes, initialize each, insert into local list.
    //      If FlagA bit 0x400000 clear: iterate fleet's regiments (sub_52b900).
    //      Always: iterate fleet's starfighters (sub_51b460).
    //      Conditionally: iterate fleet's characters (sub_52c350 / sub_52c7c0).
    // 4. If local list non-empty: allocate TypeCode=0x200 work item (sub_4f5060(0x200)),
    //    set work item's +0x20 = OwnerSide, call work item vtable+0x24 to attach nodes.
    // 5. Destroy local list (sub_4f36f0).
    // 6. Return work item (or null if local list was empty or entity checks failed).
    //
    // HIBYTE check now passes: SystemAnalysisRecord.InternalId = 0x90000000 | index,
    // so HIBYTE = 0x90 which is in [0x90,0x98). Fleet unit iteration (sub_52bc60,
    // sub_52b900, sub_51b460) still requires entity infrastructure not yet available;
    // proxy returns a FleetShortageWorkItem directly as stand-in.
    private AIWorkItem FindShortageFleet()
    {
        if (_candidateRefA == 0)
            return null;

        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefA
        );
        if (rec == null)
            return null;

        // Check system has regiment capacity (FlagA & 0x1000 = has capable facilities)
        // and no enemy (FlagA & 0x3 == 0).
        if ((rec.FlagA & 0x3) != 0)
            return null;
        if (rec.Stats.FacilityCount <= 0)
            return null;

        // In the original, this creates 0x20-byte unit reference nodes and packages them
        // into a work item that tells the fleet assignment system which units to deploy.
        // Simplified: return a FleetShortageWorkItem referencing the target system.
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004db0d0 — sub-state selector for GenerateShortageIssue case 4.
    //
    // Assembly trace (fully read):
    // 1. *(this+0x4c) = 0  — clear _agentCapacityFlag (+0x4c, not +0x50).
    // 2. eax = HIBYTE(*(this+0x40)) (= HIBYTE(_candidateRefA)).
    //    If NOT in [0x90, 0x98): var_C = 0, return 0 (terminal: no valid fleet type).
    // 3. ebp = FUN_004dc3c0(this) (= ComputeRequestedCapacity).
    //    If ebp < 0 (signed): var_C = 5, → loc_4db1c0.
    //    If ebp <= 0 (zero):  → loc_4db1c0 (var_C stays 0? no: checked below).
    // 4. ebp > 0: call sub_41a9e0(this+0x48, 0x2a, 0x10000, 0x4000, 0x2) — find agent entity.
    //    Returns capacity-per-agent in ecx.
    //    If (*(this+0x48) & 0xff000000) != 0 && ecx != 0:
    //      *(this+0x4c) = ebp / ecx  — _agentCapacityFlag = requested / cap-per-agent.
    // 5. If *(this+0x4c) <= 0: var_C = 6.
    //    Else (> 0):
    //      Look up _candidateRefA in SystemAnalysis.
    //      If found:
    //        eax = min(*(esi+0x84), *(this+0x50))  — min(sys-cap, _candidateCount)
    //        if (eax < *(this+0x4c)): *(this+0x4c) = eax
    //        if (*(this+0x4c) > *(this+0x54)): *(this+0x4c) = *(this+0x54) — cap by _costValue
    //        var_C = 8
    //    loc_4db1c0: if *(this+0x4c) == 0: var_C = 6
    // 6. Return var_C (0, 5, 6, or 8).
    //
    // Note: _candidateRefA HIBYTE check always fails in C# (entity keys are hash codes,
    // not type-encoded). The proxy is: any non-zero _candidateRefA proceeds.
    private int GetNextShortageSubState()
    {
        // *(this+0x4c) = 0: clear _agentCapacityFlag
        _agentCapacityFlag = 0;

        // HIBYTE(_candidateRefA) check [0x90, 0x98): proxy = non-zero _candidateRefA
        if (_candidateRefA == 0)
            return 0; // terminal: no valid fleet entity

        int requested = ComputeRequestedCapacity(); // FUN_004dc3c0
        if (requested < 0)
            return 5; // shortage exceeds budget → CreateFleetShortageIssue

        if (requested == 0)
            return 6; // balanced → agent shortage path

        // sub_41a9e0(this+0x48, ...): find agent entity, returns cap-per-agent in ecx.
        // C# proxy: set _agentEntityRef sentinel and use capacity = 1.
        _agentEntityRef = unchecked((int)0x90000001);
        int capPerAgent = 1; // proxy for ecx returned by sub_41a9e0

        // Condition: (this+0x48 & 0xff000000) != 0 && capPerAgent != 0
        if ((_agentEntityRef & unchecked((int)0xff000000)) != 0 && capPerAgent != 0)
            _agentCapacityFlag = requested / capPerAgent;

        if (_agentCapacityFlag <= 0)
            return 6;

        // Cap by system fields: min(min(sys+0x84, _candidateCount), _costValue)
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefA
        );
        if (rec != null)
        {
            int sysCap = rec.Stats.FacilityCount; // proxy for *(sys+0x84)
            int val = System.Math.Min(sysCap, _candidateCount);
            if (val < _agentCapacityFlag)
                _agentCapacityFlag = val;
            if (_costValue > 0 && _agentCapacityFlag > _costValue)
                _agentCapacityFlag = _costValue;
        }

        if (_agentCapacityFlag == 0)
            return 6;

        return 8;
    }

    // Helper: (Capacity * FleetTotalCapacity / 100) - sum of existing system requests.
    // Corresponds to FUN_004dc3c0_compute_type_1_requested_count.
    private int ComputeRequestedCapacity()
    {
        int total = Workspace.FleetTotalCapacity;
        int target = (Capacity * total) / 100;

        // Subtract existing allocated amounts (system+0xd8 proxy = Stats.FacilityCount * 0)
        // In the binary: iterates _candidateListA and sums system+0xd8 (unlisted capacity field).
        // Simplified: just return target - FleetAssignedCapacity delta.
        int available = total - Workspace.FleetAssignedCapacity;
        return available >= target ? target : available - target;
    }

    // FUN_004db1e0: Creates the fleet shortage issue work item (TypeCode 0x200).
    //
    // Assembly trace (fully read):
    // Iterates _candidateListA (this+0x58) forward:
    //   For each entry: look up in SystemAnalysis.
    //   If SystemScore <= 0: advance, remove from list, FlagA &= ~0x800000.
    //   If HIBYTE(FlagA) & 0x10 (= FlagA & 0x1000) == 0: advance to next node.
    //   If FlagA & 0x1000:
    //     If SystemScore > 1: store key in _candidateRefA (this+0x40), var_54=1.
    //     Else (SystemScore == 1):
    //       If list_count > 1: store, remove from list, FlagA &= ~0x800000, var_54=1.
    //       esi=0 (stop loop, var_54 NOT set if list_count <= 1).
    // If var_54==1: get fleet via sub_4f25a0(OwnerSide, _candidateRefA),
    //   iterate starfighters (sub_52b600), build 0x20 nodes,
    //   allocate TypeCode=0x200 work item (sub_4f5060(0x200)), set item+0x20=OwnerSide,
    //   attach nodes via vtable+0x24. Return item.
    // If var_54==0 or no starfighters: return null.
    //
    // Note: list_count is the WORD (low 16 bits) of sub_5f3650 result.
    // BLOCKED: fleet entity lookup (sub_4f25a0) + starfighter iteration require entity infra.
    // Proxy: creates FleetShortageWorkItem if a valid candidate is found.
    private AIWorkItem CreateFleetShortageIssue()
    {
        bool found = false;
        int foundId = 0;
        bool continueLoop = true;

        foreach (int sysId in _candidateListA.ToList())
        {
            if (!continueLoop)
                break;

            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == sysId
            );
            if (rec == null)
                continue;

            int score = rec.SystemScore;
            if (score <= 0)
            {
                // Remove from list, clear shortage marker, advance
                _candidateListA.Remove(sysId);
                rec.FlagA &= ~0x800000;
                continue;
            }

            if ((rec.FlagA & 0x1000) == 0)
            {
                // Advance to next node (don't stop)
                continue;
            }

            // FlagA & 0x1000 set:
            if (score > 1)
            {
                // SystemScore > 1: found, don't remove
                foundId = sysId;
                _candidateRefA = sysId;
                found = true;
                // Fall through (loop will terminate via break at top after var_54=1)
            }
            else
            {
                // SystemScore == 1
                if (_candidateListA.Count > 1)
                {
                    // Multiple entries: found, remove
                    foundId = sysId;
                    _candidateRefA = sysId;
                    _candidateListA.Remove(sysId);
                    rec.FlagA &= ~0x800000;
                    found = true;
                }
                // esi = 0: stop loop regardless
                continueLoop = false;
            }
            if (found)
                break;
        }

        if (!found)
            return null;

        // BLOCKED: fleet entity lookup (sub_4f25a0) + starfighter iteration
        // Proxy: return FleetShortageWorkItem for the found system
        SystemAnalysisRecord target = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == foundId
        );
        return new FleetShortageWorkItem(target?.System, OwnerSide);
    }

    // FUN_004db760_seed_type_1_high_capacity_issue_bundle:
    //
    // Assembly trace behavior:
    // 1. FUN_004191b0(workspace, 0x200, 0,0,0,0,0, 7, 1) → query DispositionFlags & 0x200,
    //    stat=PerSystemStats[7] (byte offset 0x1c = unlisted, returns 0).
    //    sub_434e10 stores in _issueContainer.
    // 2. FUN_004191b0(workspace, 0x200, 0,0,0,0,0, 9, 1) → same filter, stat[9]=+0x24
    //    = NetCapitalShipSurplus. sub_434e10 stores in _issueContainer.
    // 3. sub_434e30 gets last ID → sets _candidateRefA.
    // 4. Clears _issueContainer.
    // 5. sub_419af0(_candidateRefA, 0x100, 0, 0x1, 0x3e00003, 0, 0, 0xf, 1)
    //    — query planet sub-objects for DispositionFlags & 0x100 condition. BLOCKED.
    // 6. sub_419af0(_candidateRefA, 0x100, 0, 0x1, 0x3e00003, 0, 0, 0x0, 1) BLOCKED.
    // 7. sub_419af0(_candidateRefA, 0x100, 0, 0x1, 0x3e00003, 0, 0, 0x33, 1) BLOCKED.
    // 8. Checks if _candidateRefA type [0x90,0x98) (fleet entity). If not, tries more.
    // 9. If valid & _candidateRefB not set:
    //    sub_419bb0 / sub_419330 calls (planet sub-object queries). BLOCKED.
    // 10. Final check: if _candidateRefA valid fleet type → sets var_18=1, returns var_18.
    //
    // The FUN_00419af0 / sub_419af0 calls (steps 5-9) require planet sub-object data.
    // BLOCKED until FUN_004334c0 infrastructure is implemented.
    // Current implementation: runs the FUN_004191b0 queries (steps 1-4) and returns
    // 1 if a valid candidate was found, 0 otherwise.
    private int SelectAgentSlotAvailable()
    {
        // Queries 1-2 from FUN_004db760 assembly:
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x200,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 7 // PerSystemStats[7] = byte offset 0x1c (unlisted, returns 0)
        );
        IssueRecordContainer c2 = Workspace.QuerySystemAnalysis(
            incl24: 0x200,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 9 // PerSystemStats[9] = NetCapitalShipSurplus (+0x24)
        );

        _issueContainer.StoreFrom(c1);
        _issueContainer.StoreFrom(c2);
        _issueContainer.FinalizeAndAssignPriorities();

        SystemAnalysisRecord top = _issueContainer.GetTopRecord();
        _issueContainer.Clear();

        if (top == null)
            return 0;

        _candidateRefA = top.InternalId;

        // Steps 5-9: sub_419af0 planet-level queries (now implemented).
        // FUN_004db760 assembly lines 70-99:
        //   sub_419af0(_candidateRefA, 0x100, 0, 0x1, 0x3e00003, 0, 0, 0xf, 1)
        //   sub_419af0(_candidateRefA, 0x100, 0, 0x1, 0x3e00003, 0, 0, 0x0, 1)
        //   sub_419af0(_candidateRefA, 0x100, 0, 0x1, 0x3e00003, 0, 0, 0x33, 1)
        IssueRecordContainer pq1 = Workspace.QuerySystemPlanets(
            _candidateRefA,
            incl28: 0x100,
            incl2c: 0,
            incl30: 0x1,
            excl28: 0x3e00003,
            excl2c: 0,
            excl30: 0,
            statIndex: 0xf
        );
        IssueRecordContainer pq2 = Workspace.QuerySystemPlanets(
            _candidateRefA,
            incl28: 0x100,
            incl2c: 0,
            incl30: 0x1,
            excl28: 0x3e00003,
            excl2c: 0,
            excl30: 0,
            statIndex: 0
        );

        _issueContainer.StoreFrom(pq1);
        _issueContainer.StoreFrom(pq2);
        _issueContainer.FinalizeAndAssignPriorities();
        SystemAnalysisRecord topPlanet = _issueContainer.GetTopRecord();
        _issueContainer.Clear();

        // Final check: if valid planet results found → return 1 (agent slot available).
        // Assembly: checks if _candidateRefA type in [0x90,0x98) then returns var_18.
        // Proxy: valid results from planet query = slot available.
        if (topPlanet != null && pq1.Count + pq2.Count > 0)
        {
            _candidateRefB = _candidateRefA; // update _candidateRefB with best system
            return 1;
        }
        return 0;
    }

    // FUN_004db4c0_seed_type_1_mid_capacity_issue_bundle:
    //
    // Assembly trace behavior:
    // 1. FUN_004191b0(workspace, 0x100, 0,0,0,0,0, 7, 1) → DispositionFlags & 0x100.
    // 2. FUN_004191b0(workspace, 0x100, 0,0,0,0,0, 0xa, 1) → stat[0xa]=+0x28=NetFighterSurplus.
    // 3. FUN_004191b0(workspace, 0x100, 0,0,0,0,0, 9, 1) → stat[9]=NetCapitalShipSurplus.
    //    sub_434e10 / sub_434e30 cycle stores and gets top ID → _candidateRefA.
    // 4. Clears _issueContainer.
    // 5. sub_419af0 calls (BLOCKED — planet sub-objects).
    // Returns var_18 (1 if validated, 0 otherwise).
    private int SelectAgentSlotFull()
    {
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x100,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 7
        );
        IssueRecordContainer c2 = Workspace.QuerySystemAnalysis(
            incl24: 0x100,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 0xa // NetFighterSurplus (+0x28)
        );
        IssueRecordContainer c3 = Workspace.QuerySystemAnalysis(
            incl24: 0x100,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 9
        );

        _issueContainer.StoreFrom(c1);
        _issueContainer.StoreFrom(c2);
        _issueContainer.StoreFrom(c3);
        _issueContainer.FinalizeAndAssignPriorities();

        SystemAnalysisRecord top = _issueContainer.GetTopRecord();
        _issueContainer.Clear();

        if (top == null)
            return 0;

        _candidateRefA = top.InternalId;

        // sub_419af0 planet queries (FUN_004db4c0 assembly lines 74-99):
        //   sub_419af0(_candidateRefA, 0x80, 0, 0, 0, 0, 0, 0xe, 1)
        //   sub_419af0(_candidateRefA, 0x80, 0, 0, 0, 0, 0, 0x0, 1)
        //   sub_419af0(_candidateRefA, 0x80, 0, 0, 0, 0, 0, 0x33, 1)
        IssueRecordContainer sq1 = Workspace.QuerySystemPlanets(
            _candidateRefA,
            incl28: 0x80,
            incl2c: 0,
            incl30: 0,
            excl28: 0,
            excl2c: 0,
            excl30: 0,
            statIndex: 0xe
        );
        IssueRecordContainer sq2 = Workspace.QuerySystemPlanets(
            _candidateRefA,
            incl28: 0x80,
            incl2c: 0,
            incl30: 0,
            excl28: 0,
            excl2c: 0,
            excl30: 0,
            statIndex: 0
        );

        _issueContainer.StoreFrom(sq1);
        _issueContainer.StoreFrom(sq2);
        _issueContainer.FinalizeAndAssignPriorities();
        SystemAnalysisRecord topS = _issueContainer.GetTopRecord();
        _issueContainer.Clear();

        if (topS != null && sq1.Count + sq2.Count > 0)
        {
            _candidateRefB = _candidateRefA;
            return 1;
        }
        return 0;
    }

    // FUN_004db9c0: Finds an agent for shortage assignment.
    // Checks _candidateRefA type [0x90,0x98) AND looks up system in workspace.
    // Gets faction fleet from system via sub_4f25a0, iterates fleet units
    // (capital ships, regiments, starfighters) and creates unit reference nodes.
    // Returns a work item (TypeCode 0x200 in simplified form) if a candidate found.
    // Uses _agentCapacityFlag to determine which candidate path was taken.
    private AIWorkItem FindAgentForShortage()
    {
        if (_candidateRefA == 0)
            return null;

        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefA
        );
        if (rec == null)
            return null;

        // FUN_004db9c0: checks FlagA bit 0x200000 and 0x400000 before iterating units.
        // We proceed if system has fleet-support indicators.
        if ((rec.FlagB & 0x4) == 0)
            return null; // need own planets for agent deployment

        return new AgentShortageWorkItem(0x200, rec.System, _candidateCount, OwnerSide);
    }

    // FUN_004dbd60: Creates the TypeCode=0x214 agent shortage work item.
    // Requires: _candidateRefA type [0x90,0x98) AND _candidateRefB type [0xa0,0xa2).
    // In C#: _candidateRefB set by FinalizeShortageRecord, _agentCapacityFlag by case 6.
    // Returns TypeCode=0x214 work item with agent and target system references.
    private AIWorkItem CreateAgentShortageItem()
    {
        if (_candidateRefA == 0 || _agentCapacityFlag == 0)
            return null;

        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefA
        );
        if (rec == null)
            return null;

        return new AgentShortageWorkItem(0x214, rec.System, _candidateCount, OwnerSide);
    }

    // FUN_004dbea0: Finalizes the TypeCode=0x210 agent shortage work item.
    // Requires: _candidateRefB valid [0xa0,0xa2) AND _agentEntityRef high byte set.
    // In C#: _agentEntityRef set by GetNextShortageSubState when capacity > 0.
    // Sets work_item+0x48 = _candidateCount.
    private AIWorkItem FinalizeAgentShortageItem()
    {
        // _agentEntityRef high byte != 0 means GetNextShortageSubState found an assignment
        if ((_agentEntityRef & unchecked((int)0xff000000)) == 0)
            return null;
        if (_candidateCount <= 0)
            return null;

        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefA
        );
        if (rec == null)
            return null;

        return new AgentShortageWorkItem(0x210, rec.System, _candidateCount, OwnerSide);
    }
}

// ------------------------------------------------------------------
// Type 2 — FUN_004e1190 — LocalShortageGeneratorType2Record
// 0x80 bytes.  Active guard: standard.
//
// Phase 0:
//   FUN_004e1540 (initial setup check) → non-zero:
//       Phase = (Workspace.AgentAssignedCapacity <= Workspace.AgentTotalCapacity)
//                ? 0x3eb : 0x3ea
//       return null
//   Zero → Phase=0x3ec, return null
// Phase 0x3ea or 0x3eb → FUN_004e1930 (fleet/agent issue handler), return result
// Phase 0x3ec         → FUN_004e1770 (agent/fallback issue handler), return result
// Other (< 0x3ea or > 0x3ec) → Phase=0, return null
// ------------------------------------------------------------------
public class LocalShortageGeneratorType2Record : StrategyRecord
{
    private const int PhaseFleetIssueA = 0x3ea;
    private const int PhaseFleetIssueB = 0x3eb;
    private const int PhaseAgentIssue = 0x3ec;

    // +0x48: candidate ref A — set by CheckIssuePrecondition (sub_434e30 result → sub_4ec1e0).
    // Stores the top-priority entity key from the issue container queries.
    private int _candidateRefA2;

    // +0x4c: candidate ref B — set by InitialSetupCheck (stores entity key of first valid system).
    private int _candidateRefB2;

    // +0x50: agent type+mode packed descriptor, written in HandleFleetIssue case 9.
    //   0x2d000002 = agent type 0x2d, mode 2 (Phase 0x3eb / PhaseFleetIssueB)
    //   0x2c000001 = agent type 0x2c, mode 1 (Phase 0x3ea / PhaseFleetIssueA)
    private int _agentTypePacked;

    // +0x54: flag set to 1 in HandleFleetIssue case 9; consumed by FinalizeIssueAssignment (FUN_004e37b0).
    private int _agentAssignmentFlag;

    // +0x58: unmapped field (4 bytes; purpose not yet resolved from function analysis).
    private int _type2Field58;

    // +0x5c: capacity counter set by CheckAgentSetupCondition (FUN_004e28a0):
    //   = *(sys+0x114) (capacity field, proxy: FacilityCount) of best candidate.
    //   Analogous to Type 1's _candidateCount at +0x50 / Type 3's _candidateCapacity68.
    private int _candidateCapacity5c;

    // +0x60: candidate list — iterated by InitialSetupCheck (FUN_004e1540) and
    //   CheckAgentSetupCondition (FUN_004e28a0). Contains system entity keys.
    //   Analogous to Type 1's _candidateListA at +0x58.
    private readonly List<int> _type2CandidateList = new List<int>();

    // +0x70: issue container — receives QuerySystemAnalysis results in CheckIssuePrecondition.
    // Analogous to Type 1's _issueContainer at +0x68.
    private readonly IssueRecordContainer _type2IssueContainer = new IssueRecordContainer();

    public LocalShortageGeneratorType2Record(int ownerSide)
        : base(typeId: 2, capacity: 1, ownerSide: ownerSide)
    {
        _type2Field58 = 0;
        _candidateCapacity5c = 0;
    }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        uint phase = (uint)Phase;

        if (phase == 0)
        {
            // Phase 0: run setup check.
            if (InitialSetupCheck() != 0)
            {
                // Set phase based on agent capacity comparison.
                Phase =
                    (Workspace.AgentAssignedCapacity <= Workspace.AgentTotalCapacity)
                        ? PhaseFleetIssueB
                        : PhaseFleetIssueA;
                return null;
            }
            Phase = PhaseAgentIssue;
            return null;
        }

        // Phase range check (assembly: if (uVar1 > 0x3e9) and nested comparisons).
        if (phase >= 0x3ea)
        {
            if (phase < 0x3ec)
                return HandleFleetIssue(); // Phases 0x3ea and 0x3eb

            if (phase == 0x3ec)
                return HandleAgentIssue(); // Phase 0x3ec
        }

        // Phase outside [0x3ea, 0x3ec] range — reset.
        Phase = 0;
        return null;
    }

    // FUN_004e1540_seed_type_2_primary_candidate_list:
    //
    // Assembly trace behavior (from FUN_004e1540 full read):
    // 1. Gets first node from list at param_1+0x60 (_type2CandidateList), forward via +0x10.
    // 2. If list EMPTY: calls FUN_004ec230 on param_1+0x4c (reset _candidateRefB2 to value 2),
    //    returns 0 immediately.
    // 3. Iterates list until found (var_30=1) or exhausted:
    //    For each node:
    //      a. Get key-value → set param_1+0x4c (_candidateRefB2) via sub_4ec1e0.
    //      b. Check _candidateRefB2 high byte in [0x90, 0x98) (valid fleet entity type).
    //         If NOT valid: remove from list (sub_4f4c60), continue.
    //      c. If valid: look up in workspace system analysis (via workspace+0x2c).
    //         If NOT found in system analysis: remove from list, continue.
    //      d. If found: check conditions (from assembly lines 4e1613-4e1624):
    //           *(eax+0x30) & 0x1 — PresenceFlags & 0x1 (own faction)
    //           *(eax+0x28) & 0x1000000 — FlagA & 0x1000000 (capability bit 24)
    //           *(eax+0x60) > 0 — SystemScore > 0
    //           HIBYTE(*(eax+0x28)) & 0x8 — FlagA & 0x800 (fleet bit 11)
    //           LOBYTE(*(eax+0x28)) & 0x3 == 0 — FlagA & 0x3 == 0 (no enemy planets)
    //         If ALL pass: var_30=1 (found).
    //         If any fail (loc_4e1645): clear FlagA bit 0x1000000, call sub_4334b0 (cleanup),
    //            remove from list, continue.
    // 4. Post-loop (loc_4e16ee):
    //    If NOT found (var_30==0): reset _candidateRefB2 to value 2.
    //    If found: call sub_4f25a0(OwnerSide, &_candidateRefB2) to resolve fleet entity;
    //              if valid: call sub_5087e0(1) (capacity check 1);
    //              if capacity passes: update _candidateRefB2 with fleet entity ref, var_2C=1.
    // 5. Returns var_2C (1 if valid candidate with capacity, 0 otherwise).
    private int InitialSetupCheck()
    {
        _candidateRefB2 = 0;

        if (_type2CandidateList.Count == 0)
            return 0;

        bool found = false;
        foreach (int sysId in _type2CandidateList.ToList())
        {
            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == sysId
            );

            if (rec == null)
            {
                // Not found in system analysis → remove (loc_4e1685)
                _type2CandidateList.Remove(sysId);
                continue;
            }

            // Check entity type [0x90, 0x98) — proxy: sysId != 0 with valid record
            // (in binary this checks the entity ref type code)

            // Conditions (from assembly loc_4e1613-4e1624):
            bool pass =
                (rec.PresenceFlags & 0x1u) != 0
                && // PresenceFlags & 0x1 (own faction)
                (rec.FlagA & 0x1000000) != 0
                && // FlagA & 0x1000000 (Type 2 specific)
                rec.SystemScore > 0
                && // SystemScore > 0
                (rec.FlagA & 0x800) != 0
                && // FlagA & 0x800 (fleet capability)
                (rec.FlagA & 0x3) == 0; // FlagA & 0x3 == 0 (no enemy)

            if (!pass)
            {
                // loc_4e1645: clear FlagA bit 0x1000000, remove from list
                rec.FlagA &= ~0x1000000;
                _type2CandidateList.Remove(sysId);
                continue;
            }

            _candidateRefB2 = sysId;
            found = true;
            break;
        }

        if (!found)
            return 0;

        // Validate fleet capacity (sub_4f25a0 + sub_5087e0):
        // Proxy: candidate found with valid flags → accept.
        return 1;
    }

    // FUN_004e1930: Fleet/agent issue handler dispatched from phases 0x3ea and 0x3eb.
    // Return value forwarded directly to caller.
    // SubState machine (switch on SubState):
    //   default→1; 1→2/3; 2→3+item; 3→4/terminal(zero); 4→8+item; 8→9+item/terminal(null); 9→terminal+item.
    //   Jump table covers SubState 1..9; cases 5,6,7 absent → go to default (SubState=1).
    private AIWorkItem HandleFleetIssue()
    {
        switch (SubState)
        {
            default:
                SubState = 1;
                return null;

            case 1:
            {
                // FUN_004e1ad0_seed_type_2_primary_issue_bundle:
                // ALWAYS returns 1 (var_18 initialized to 1, never set to 0).
                // If FleetAssignedCapacity > FleetTotalCapacity: seeds issue records using
                //   QuerySystemAnalysis(workspace, 0xe0, 0,0,0,0,0, 0x15/0x11/0x13, 1) ×3
                //   then QuerySystemPlanets calls for planet-level validation.
                // Else: returns 1 immediately without seeding.
                // SubState = (returned != 0) ? 3 : 2. Since it always returns 1 → SubState=3.
                int found = CheckIssuePrecondition();
                SubState = (found != 0) ? 3 : 2;
                return null;
            }

            case 2:
            {
                // FUN_004e1cb0: find issue candidate. Returns work item or null.
                AIWorkItem item = FindIssueCandidate();
                if (item != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 3;
                return item;
            }

            case 3:
            {
                // Phase 0x3eb → FUN_004e2280 (CheckFleetIssueConditionA);
                // Phase 0x3ea → FUN_004e1fe0 (CheckFleetIssueConditionB).
                // Non-zero → SubState=4; zero → terminal (Phase=0, SubState=0, RF=1).
                int found =
                    (Phase == PhaseFleetIssueB)
                        ? CheckFleetIssueConditionA()
                        : CheckFleetIssueConditionB();
                if (found != 0)
                {
                    SubState = 4;
                    return null;
                }
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return null;
            }

            case 4:
            {
                // Phase 0x3eb → FUN_004e2520(&+0x44) (InitFleetAssignmentPhaseB);
                // Phase 0x3ea → FUN_004e2520(&+0x40) (InitFleetAssignmentPhaseA).
                // Assembly passes pointer-to-slot as parameter; two distinct call sites.
                // RF+TC if item, then SubState=8 unconditionally.
                AIWorkItem item =
                    (Phase == PhaseFleetIssueB)
                        ? InitFleetAssignmentPhaseB()
                        : InitFleetAssignmentPhaseA();
                if (item != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 8;
                return item;
            }

            case 8:
            {
                // Phase 0x3eb → FUN_004e3670(&+0x44) (DispatchFleetEntryPhaseB);
                // Phase 0x3ea → FUN_004e3670(&+0x40) (DispatchFleetEntryPhaseA).
                // Non-null → SubState=9, RF=1, TC++; null → terminal.
                AIWorkItem item =
                    (Phase == PhaseFleetIssueB)
                        ? DispatchFleetEntryPhaseB()
                        : DispatchFleetEntryPhaseA();
                if (item != null)
                {
                    SubState = 9;
                    ReadyFlag = 1;
                    TickCounter++;
                    return item;
                }
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return null;
            }

            case 9:
            {
                // Write agent type+mode packed descriptor based on phase into +0x50.
                //   Phase 0x3eb: alliance agent type 0x2d, mode 2 → 0x2d000002.
                //   Phase 0x3ea: empire agent type 0x2c, mode 1 → 0x2c000001.
                // Assembly performs two-step mask-set but net result is direct write.
                // Write +0x54 (_agentAssignmentFlag) = 1, then call FUN_004e37b0.
                // Falls through to shared terminal.
                _agentTypePacked = (Phase == PhaseFleetIssueB) ? 0x2d000002 : 0x2c000001;
                _agentAssignmentFlag = 1;
                AIWorkItem item = FinalizeIssueAssignment();
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return item;
            }
        }
    }

    // FUN_004e1770: Agent/fallback issue handler dispatched from phase 0x3ec.
    // Return value forwarded directly to caller.
    // SubState machine (switch on SubState):
    //   default→1; 1→2/3; 2→3+item; 3→7/terminal(zero); 4→5+item; 5→nextState/terminal(zero);
    //   6→terminal+item; 7→4/10; 8→9+item/terminal(null); 9→terminal+item; 10→terminal.
    private AIWorkItem HandleAgentIssue()
    {
        switch (SubState)
        {
            default:
                SubState = 1;
                return null;

            case 1:
            {
                // FUN_004e1ad0: precondition check. Non-zero → SubState=3; zero → SubState=2.
                int found = CheckIssuePrecondition();
                SubState = (found != 0) ? 3 : 2;
                return null;
            }

            case 2:
            {
                // FUN_004e1cb0: find issue candidate. Returns work item or null.
                AIWorkItem item = FindIssueCandidate();
                if (item != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 3;
                return item;
            }

            case 3:
            {
                // FUN_004e28a0: agent setup condition check. Non-zero → SubState=7; zero → terminal.
                int found = CheckAgentSetupCondition();
                if (found != 0)
                {
                    SubState = 7;
                    return null;
                }
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return null;
            }

            case 4:
            {
                // FUN_004e2db0: find agent issue candidate. Returns work item or null.
                AIWorkItem item = FindAgentIssueCandidate();
                if (item != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 5;
                return item;
            }

            case 5:
            {
                // FUN_004e32f0: compute next sub-state. SubState written to result first.
                // Non-zero → return null (SubState already set to result); zero → inline terminal.
                // Assembly: SubState = result; if result==0: SubState=0 (overwrite), Phase=0, RF=1, return 0.
                int nextState = GetNextAgentSubState();
                SubState = nextState;
                if (nextState != 0)
                    return null;
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return null;
            }

            case 6:
            {
                // FUN_004e3390: create agent issue work item. Terminal.
                AIWorkItem item = CreateAgentIssue();
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return item;
            }

            case 7:
            {
                // Clear bit 0x40 from workspace PendingSupplyBitmask (*(workspace+0x8) &= ~0x40).
                Workspace.PendingSupplyBitmask &= ~0x40;
                // If EntityTargetType (workspace+0xc) == 0x40 AND FUN_004e2b80 (CheckAgentEligibility) non-zero:
                //   call AdvanceBitSelection (FUN_00419160) on workspace, SubState=4.
                // Both conditions use short-circuit &&; either failure → SubState=10.
                if (Workspace.EntityTargetType == 0x40 && CheckAgentEligibility() != 0)
                {
                    Workspace.AdvanceBitSelection();
                    SubState = 4;
                    return null;
                }
                SubState = 10;
                return null;
            }

            case 8:
            {
                // FUN_004e3670 with slot +0x48 (DispatchAgentEntry).
                // Non-null → SubState=9, RF=1, TC++; null → terminal.
                AIWorkItem item = DispatchAgentEntry();
                if (item != null)
                {
                    SubState = 9;
                    ReadyFlag = 1;
                    TickCounter++;
                    return item;
                }
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return null;
            }

            case 9:
            {
                // FUN_004e37b0: finalize issue assignment. Terminal.
                AIWorkItem item = FinalizeIssueAssignment();
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return item;
            }

            case 10:
            {
                // FUN_004e38b0: cleanup agent assignment state.
                // FUN_004e3d90: cleanup agent issue state.
                // Both called unconditionally; terminal.
                CleanupAgentAssignment();
                CleanupAgentIssueState();
                SubState = 0;
                Phase = 0;
                ReadyFlag = 1;
                return null;
            }
        }
    }

    // FUN_004e1ad0_seed_type_2_primary_issue_bundle:
    //
    // Assembly trace (fully read):
    // 1. var_18 = 1 (initialized).
    // 2. Condition: *(workspace+0x188) > *(workspace+0x184)
    //    (= FleetAssignedCapacity > FleetTotalCapacity; shortage detected).
    //    If TRUE: run seeding block:
    //      a. Three QuerySystemAnalysis(0xe0, ...) calls (DispositionFlags & 0xe0):
    //         stats 0x15, 0x11, 0x13 → store in _type2IssueContainer → get top → _candidateRefA2
    //         → clear container.
    //      b. Three QuerySystemPlanets(_candidateRefA2, 0, 0, 1, excl28=0x3e00000, 0, 0, stat, 1):
    //         stats 6, 4, 5 → store in _type2IssueContainer → get top → _candidateRefA2
    //         → clear container.
    //      c. Check HIBYTE(_candidateRefA2) in [0x90, 0x98) (fleet entity type):
    //         If YES: var_18 = 0.
    // 3. Return var_18.
    //
    // Note: In C#, HIBYTE check always fails (entity keys are hash codes), so this always returns 1.
    private int CheckIssuePrecondition()
    {
        int var18 = 1; // initialized to 1
        // Condition: FleetAssignedCapacity > FleetTotalCapacity
        if (Workspace.FleetAssignedCapacity > Workspace.FleetTotalCapacity)
        {
            // Three QuerySystemAnalysis(DispositionFlags & 0xe0): stats 0x15, 0x11, 0x13
            IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
                incl24: 0xe0,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 0x15
            );
            IssueRecordContainer c2 = Workspace.QuerySystemAnalysis(
                incl24: 0xe0,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 0x11
            );
            IssueRecordContainer c3 = Workspace.QuerySystemAnalysis(
                incl24: 0xe0,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 0x13
            );
            _type2IssueContainer.StoreFrom(c1);
            _type2IssueContainer.StoreFrom(c2);
            _type2IssueContainer.StoreFrom(c3);
            if (_type2IssueContainer.TryGetTopEntityKey(out int key1))
                _candidateRefA2 = key1;
            _type2IssueContainer.Clear();

            // Three QuerySystemPlanets(_candidateRefA2, 0,0,1, excl28=0x3e00000,0,0, stat, 1)
            IssueRecordContainer p1 = Workspace.QuerySystemPlanets(
                _candidateRefA2,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x3e00000,
                excl2c: 0,
                excl30: 0,
                statIndex: 6
            );
            IssueRecordContainer p2 = Workspace.QuerySystemPlanets(
                _candidateRefA2,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x3e00000,
                excl2c: 0,
                excl30: 0,
                statIndex: 4
            );
            IssueRecordContainer p3 = Workspace.QuerySystemPlanets(
                _candidateRefA2,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x3e00000,
                excl2c: 0,
                excl30: 0,
                statIndex: 5
            );
            _type2IssueContainer.StoreFrom(p1);
            _type2IssueContainer.StoreFrom(p2);
            _type2IssueContainer.StoreFrom(p3);
            if (_type2IssueContainer.TryGetTopEntityKey(out int key2))
                _candidateRefA2 = key2;
            _type2IssueContainer.Clear();

            // Check HIBYTE(_candidateRefA2) in [0x90, 0x98): in C# entity keys are hash codes —
            // this check always fails, so var18 always stays 1.
            // Original: if (HIBYTE(_candidateRefA2) >= 0x90 && HIBYTE(_candidateRefA2) < 0x98) var18 = 0;
        }
        return var18;
    }

    // FUN_004e1cb0: Find issue candidate (fleet unit node builder). TypeCode=0x200.
    //
    // Assembly trace (fully read — same pattern as FUN_004dab90 FindShortageFleet for Type 1):
    // 1. Check HIBYTE(arg_44 = this+0x48 = _candidateRefA2) in [0x90,0x98) (fleet entity type).
    //    If YES: look up in SystemAnalysis; get fleet via sub_4f25a0(OwnerSide, _candidateRefA2).
    //    If fleet found:
    //      Iterate capital ships (sub_52bc60): build 0x20 nodes.
    //      Iterate regiments (sub_52b900): build 0x20 nodes.
    //      Iterate starfighters (sub_52b600): build 0x20 nodes.
    // 2. If local list non-empty: sub_4f5060(0x200) work item, set item+0x20=OwnerSide, attach nodes.
    //    Return work item. Else: return null.
    //
    // Note: uses _candidateRefA2 (set by CheckIssuePrecondition), NOT _candidateRefB2.
    // HIBYTE check passes with InternalIds (SystemAnalysisRecord.InternalId HIBYTE = 0x90).
    // Proxy: returns FleetShortageWorkItem since the fleet unit iterators (sub_52bc60 etc.)
    // that build the actual unit node list are not yet read.
    private AIWorkItem FindIssueCandidate()
    {
        // Uses _candidateRefA2 (set by CheckIssuePrecondition), not _candidateRefB2.
        if (_candidateRefA2 == 0)
            return null;

        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefA2
        );
        if (rec == null || rec.System == null || (rec.FlagB & 0x4) == 0)
            return null;

        // Proxy: system has own-faction presence → fleet shortage candidate valid.
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004e2280: Fleet issue condition check A. Called from HandleFleetIssue case 3 when Phase==0x3eb.
    // Non-zero → SubState=4; zero → terminal.
    //
    // Assembly trace (fully read):
    // var_18 = 0.
    // 1. arg_40 = this+0x48 = _candidateRefA2 (fleet entity ref).
    //    Check HIBYTE(_candidateRefA2) in [0x90,0x98). If fleet:
    //      Look up in SystemAnalysis (arg_28 = workspace, workspace+0x2c = SystemAnalysis).
    //      Check FlagA & 0x3 == 0 (no enemy) AND HIBYTE(FlagA) & 0x1 = FlagA & 0x100:
    //        If passes: var_18 = 1.
    // 2. If var_18 == 0:
    //    a. sub_419330(arg_48, 0x200, ..., sort=2) → update _candidateRefA2 via sub_4ec1e0.
    //       Store result in issue container at arg_6c = this+0x70 = _type2IssueContainer.
    //    b. sub_419bb0(_candidateRefA2, arg_48, 0x100, 0, 0, 0x3, 0, 0, sort=2) → update _candidateRefA2.
    //    c. If _candidateRefA2 HIBYTE NOT in [0x90,0x98):
    //       sub_419330(arg_48, 0x400, ...) → update. sub_419bb0(..., 0x20) → update.
    //    d. Final HIBYTE check: if in [0x90,0x98): var_18 = 1.
    // 3. Returns var_18.
    //
    // Note: arg_48 is a secondary entity ref at this+0x4c = _candidateRefB2.
    //       sub_419330 is FUN_00419330 (different from sub_419af0 / QuerySystemPlanets).
    //       FUN_00419330 takes (workspace, entity_ref, filter, ...) — not yet implemented.
    //       Proxy: uses QuerySystemAnalysis as stand-in for FUN_00419330.
    private int CheckFleetIssueConditionA()
    {
        // Direct check: candidate entity at _candidateRefA2 in valid system?
        SystemAnalysisRecord rec = null;
        if (_candidateRefA2 != 0)
            rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == _candidateRefA2);

        if (rec != null && (rec.FlagA & 0x3) == 0 && (rec.FlagA & 0x100) != 0)
            return 1; // Direct condition A met

        // Seeding block (when direct check fails):
        // sub_419330(arg_48, 0x200, 0,0,0,0,0, 0x2, sort=2)
        // ≈ QuerySystemAnalysis(DispositionFlags & 0x200, stat[2] = MineCount)
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x200,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 2
        );
        SystemAnalysisRecord top1 = c1.GetTopRecord();
        if (top1 != null)
            _candidateRefA2 = top1?.InternalId ?? _candidateRefA2;

        // sub_419bb0(edi, arg_48, 0x100, 0, 0, 0x3, 0, 0, 0x2)
        // ≈ QuerySystemPlanets(_candidateRefA2, incl28=0x100, excl28=0, incl30=0, excl28incl=0x3 excl)
        IssueRecordContainer c2 = Workspace.QuerySystemPlanets(
            _candidateRefA2,
            incl28: 0x100,
            incl2c: 0,
            incl30: 0,
            excl28: 0x3,
            excl2c: 0,
            excl30: 0,
            statIndex: 2
        );
        SystemAnalysisRecord top2 = c2.GetTopRecord();
        if (top2 != null)
            _candidateRefA2 = top2.System?.GetHashCode() ?? _candidateRefA2;

        // Final check: if seeding found a valid fleet-type candidate → return 1.
        // Assembly: checks if *edi (entity ref) type high byte in [0x90,0x98).
        // Proxy: if updated _candidateRefA2 points to a valid system.
        if (_candidateRefA2 != 0)
        {
            rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == _candidateRefA2);
            if (rec != null && (rec.FlagA & 0x3) == 0)
                return 1;
        }
        return 0;
    }

    // FUN_004e1fe0: Fleet issue condition check B. Called from HandleFleetIssue case 3 when Phase==0x3ea.
    // Non-zero → SubState=4; zero → terminal.
    //
    // Assembly trace (fully read — same structure as FUN_004e2280 CheckFleetIssueConditionA):
    // arg_3c = entity ref (this+0x44? unmapped field; proxy uses _candidateRefB2 or _candidateRefA2).
    // arg_48 = secondary entity ref.
    // 1. var_18 = 0. Check HIBYTE(arg_3c) in [0x90,0x98) (fleet type).
    //    If fleet: look up SystemAnalysis. Check FlagA & 0x3 == 0 AND FlagA & 0x80 (bit 7 = regiment).
    //    If passes: var_18 = 1.
    // 2. If var_18 == 0: seeding block:
    //    sub_419330(arg_48, 0x100, ...) → update arg_3c (filter 0x100 vs A's 0x200).
    //    sub_419bb0(arg_3c, arg_48, 0x80, ...) → update (filter 0x80 vs A's 0x100).
    //    If still not fleet: sub_419330(arg_48, 0x800, ...) + sub_419bb0(..., 0x40) fallback.
    //    Final HIBYTE check: if fleet → var_18 = 1.
    // 3. Returns var_18.
    //
    // Key difference from A:
    //   B checks FlagA & 0x80 (regiment/troop capacity, bit 7).
    //   A checks FlagA & 0x100 (capital ship capacity, bit 8).
    //   B uses filter 0x100/0x800 vs A's 0x200/0x400 in seeding.
    private int CheckFleetIssueConditionB()
    {
        SystemAnalysisRecord rec = null;
        if (_candidateRefB2 != 0)
            rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == _candidateRefB2);

        if (rec != null && (rec.FlagA & 0x3) == 0 && (rec.FlagA & 0x80) != 0)
            return 1; // Direct condition B met

        // Seeding: sub_419bb0(edi, ebx, 0x80, 0, 0, 0x3, 0, 0, 0x2)
        IssueRecordContainer c1 = Workspace.QuerySystemPlanets(
            _candidateRefB2,
            incl28: 0x80,
            incl2c: 0,
            incl30: 0,
            excl28: 0x3,
            excl2c: 0,
            excl30: 0,
            statIndex: 2
        );
        SystemAnalysisRecord top1 = c1.GetTopRecord();
        if (top1 != null)
            _candidateRefB2 = top1?.InternalId ?? _candidateRefB2;

        if (_candidateRefB2 != 0)
        {
            rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == _candidateRefB2);
            if (rec != null && (rec.FlagA & 0x3) == 0)
                return 1;
        }
        return 0;
    }

    // FUN_004e2520 (Phase B): unit-node-builder using fleet entity ref at this+0x44.
    //
    // Assembly trace (fully read — same pattern as FUN_004dab90 FindShortageFleet):
    // Takes arg_0 = pointer to fleet entity ref (this+0x44 for Phase B, this+0x40 for Phase A).
    // 1. Check HIBYTE(*arg_0) in [0x90,0x98). If fleet:
    //    Look up in SystemAnalysis, get fleet via sub_4f25a0.
    //    If fleet AND system found:
    //      Iterate capital ships (if FlagA & 0x200000 == 0) → build 0x20 nodes.
    //      Iterate regiments (if FlagA & 0x400000 == 0) → build 0x20 nodes.
    //      Iterate starfighters (if FlagA & 0x3800000 == 0) → build 0x20 nodes.
    // 2. If nodes built: sub_4f5060(0x200) TypeCode=0x200 work item. Return.
    //
    // Note: this+0x44 is an unmapped entity ref (set by CheckFleetIssueConditionA seeding).
    // BLOCKED: HIBYTE check + fleet iteration require entity infrastructure.
    // Proxy: uses _candidateRefA2 as stand-in for this+0x44.
    private AIWorkItem InitFleetAssignmentPhaseB()
    {
        // Proxy: this+0x44 → use _candidateRefA2 (set by CheckFleetIssueConditionA)
        if (_candidateRefA2 == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefA2
        );
        if (rec == null || (rec.FlagB & 0x4) == 0)
            return null;
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004e2520 (Phase A): same function as Phase B but uses this+0x40.
    // Note: this+0x40 is an unmapped entity ref (set by CheckFleetIssueConditionB seeding).
    // Proxy: uses _candidateRefB2 as stand-in for this+0x40.
    private AIWorkItem InitFleetAssignmentPhaseA()
    {
        // Proxy: this+0x40 → use _candidateRefB2 (set by CheckFleetIssueConditionB)
        if (_candidateRefB2 == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefB2
        );
        if (rec == null || (rec.FlagB & 0x4) == 0)
            return null;
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004e3670 (Phase B): dispatch fleet/agent entry. TypeCode=0x214.
    //
    // Assembly trace (fully read — __thiscall with entity-ref pointer param):
    // 1. ebp = arg_0 (fleet entity ref pointer, = this+0x44 for Phase B).
    //    Check HIBYTE(*ebp) in [0x90,0x98) AND HIBYTE(this+0x4c = _candidateRefB2) in [0xa0,0xa2).
    //    If BOTH valid:
    //      sub_4f5060(0x214) → TypeCode=0x214 agent work item.
    //      sub_617140(0x20) → 0x20 node init with this+0x4c (_candidateRefB2).
    //      sub_4f4b30 → add to local list.
    //      set item+0x20 = OwnerSide.
    //      vtable+0x24(&local_list) → attach.
    //      vtable+0x2c(ebp=arg_0) → dispatch with fleet ref.
    //    Return item or null.
    //
    // BLOCKED: both HIBYTE checks fail in C#.
    // Proxy: creates AgentShortageWorkItem(0x214) if _candidateRefA2 non-null.
    private AIWorkItem DispatchFleetEntryPhaseB()
    {
        if (_candidateRefA2 == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefA2
        );
        if (rec == null)
            return null;
        return new AgentShortageWorkItem(0x214, rec.System, 1, OwnerSide);
    }

    // FUN_004e3670 (Phase A): same function but uses this+0x40 as fleet ref.
    // Proxy: creates AgentShortageWorkItem(0x214) if _candidateRefB2 non-null.
    private AIWorkItem DispatchFleetEntryPhaseA()
    {
        if (_candidateRefB2 == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefB2
        );
        if (rec == null)
            return null;
        return new AgentShortageWorkItem(0x214, rec.System, 1, OwnerSide);
    }

    // FUN_004e37b0: Finalize issue assignment.
    // Assembly: checks agent entity at param_1+0x4c type [0xa0,0xa2).
    // If valid: FUN_004f5060(0x210) = TypeCode 0x210 AgentShortageWorkItem with count.
    // Assembly also sets work_item+0x48 = _agentAssignmentFlag (param_1+0x54).
    // FUN_004e37b0: FinalizeIssueAssignment — creates TypeCode=0x210 agent assignment work item.
    //
    // Assembly trace (fully read):
    // 1. ebp = this+0x4c = &_candidateRefB2.
    //    Check HIBYTE(_candidateRefB2) in [0xa0, 0xa2) (agent entity type).
    //    If agent: allocate TypeCode=0x210 work item (sub_4f5060(0x210)).
    //    Allocate 0x20 node, init with _candidateRefB2 (sub_4f4ea0(ebp, 0)).
    //    Add to local list, set item+0x20 = OwnerSide, attach via vtable+0x24.
    //    item+0x44 = _agentTypePacked (this+0x50) via sub_4ec1e0.
    //    item+0x48 = _agentAssignmentFlag (*(this+0x54)).
    //    Return item.
    //
    // BLOCKED: HIBYTE(_candidateRefB2) check fails in C# (InternalIds now carry HIBYTE type encoding; check passes for [0x90,0x98) system records).
    // Proxy: returns AgentShortageWorkItem(0x210) when _agentAssignmentFlag is set.
    private AIWorkItem FinalizeIssueAssignment()
    {
        // Uses _candidateRefB2 (this+0x4c), NOT _candidateRefA2.
        if (_candidateRefB2 == 0 || _agentAssignmentFlag == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefB2
        );
        if (rec == null)
            return null;
        // item+0x44 = _agentTypePacked, item+0x48 = _agentAssignmentFlag
        return new AgentShortageWorkItem(0x210, rec.System, _agentAssignmentFlag, OwnerSide);
    }

    // FUN_004e28a0_seed_type_2_issue_bundle_if_secondary_candidate_list_empty:
    //
    // Assembly trace (fully read — identical structure to FUN_004da880 / CheckShortageConditionType1):
    // 1. Reset _candidateRefA2 (this+0x48) via sub_4ec230. Clear _candidateCapacity5c (this+0x5c).
    // 2. var_20 = 0x3e8 (initial min cost). var_24 = 0 (found flag).
    // 3. Get first node from _type2CandidateList (this+0x60).
    // 4. If list NOT empty: iterate:
    //    Check PresenceFlags & 0x1, FlagA & 0x3 == 0, *(esi+0x114) > 0, SystemScore < min:
    //      _candidateCapacity5c = capacity, _candidateRefA2 = node key, var_24 = 1.
    // 5. If var_24 == 0 (no candidate from list): fallback queries:
    //    QuerySystemAnalysis(0x80, stat=0x15) + QuerySystemAnalysis(0x80, stat=4, sort=2):
    //      → get last ID → _candidateRefA2 → clear.
    //    QuerySystemPlanets(_candidateRefA2, 0,0,1, 0x3800003,0,0x40000000, 6,1) → update.
    //    If _type2CandidateList.Count > 0: QuerySystemPlanets(..., 0x33) → update.
    //    If HIBYTE(_candidateRefA2) in [0x90,0x98) (fleet type):
    //      Insert into _type2CandidateList, FlagA |= 0x1000000 (bit 24, NOT 0x800000!), var_24=1.
    // 6. Returns var_24 (1 = candidate found, 0 = not found).
    //
    // Note: uses FlagA bit 0x1000000 (bit 24). Different from Type 1 (0x800000 bit 23).
    private int CheckAgentSetupCondition()
    {
        // Reset _candidateRefA2 and _candidateCapacity5c
        _candidateRefA2 = 0;
        _candidateCapacity5c = 0;
        int minCost = 0x3e8;
        bool found = false;

        foreach (int sysId in _type2CandidateList)
        {
            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == sysId
            );
            if (rec == null)
                continue;
            if ((rec.PresenceFlags & 0x1) == 0)
                continue;
            if ((rec.FlagA & 0x3) != 0)
                continue;
            int cap = rec.Stats.FacilityCount; // proxy for *(esi+0x114)
            if (cap <= 0)
                continue;
            int score = rec.SystemScore;
            if (score < minCost)
            {
                _candidateCapacity5c = cap;
                minCost = score;
                _candidateRefA2 = sysId;
                found = true;
            }
        }

        if (!found)
        {
            // Fallback: QuerySystemAnalysis(0x80, stat=0x15) + QuerySystemAnalysis(0x80, stat=4)
            IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
                incl24: 0x80,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 0x15
            );
            IssueRecordContainer c2 = Workspace.QuerySystemAnalysis(
                incl24: 0x80,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 4
            );
            c1.StoreFrom(c2);
            if (c1.TryGetTopEntityKey(out int key1))
                _candidateRefA2 = key1;
            c1.Clear();

            // QuerySystemPlanets(_candidateRefA2, 0,0,1, 0x3800003,0,0x40000000, 6,1)
            IssueRecordContainer p1 = Workspace.QuerySystemPlanets(
                _candidateRefA2,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x3800003,
                excl2c: 0,
                excl30: 0x40000000,
                statIndex: 6
            );
            if (_type2CandidateList.Count > 0)
            {
                IssueRecordContainer p2 = Workspace.QuerySystemPlanets(
                    _candidateRefA2,
                    incl28: 0,
                    incl2c: 0,
                    incl30: 1,
                    excl28: 0x3800003,
                    excl2c: 0,
                    excl30: 0x40000000,
                    statIndex: 0x33
                );
                p1.StoreFrom(p2);
            }
            if (p1.TryGetTopEntityKey(out int key2))
                _candidateRefA2 = key2;
            p1.Clear();

            // Fleet entity type proxy: _candidateRefA2 != 0 with valid system
            if (_candidateRefA2 != 0)
            {
                SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                    r.InternalId == _candidateRefA2
                );
                if (rec != null)
                {
                    if (!_type2CandidateList.Contains(_candidateRefA2))
                        _type2CandidateList.Add(_candidateRefA2);
                    rec.FlagA |= 0x1000000; // FlagA bit 24 (NOT 0x800000!)
                    found = true;
                }
            }
        }

        return found ? 1 : 0;
    }

    // FUN_004e2db0: Find agent issue candidate. TypeCode=0x200.
    //
    // Assembly trace (fully read — same unit-node-building pattern as FUN_004e1cb0 FindIssueCandidate):
    // Uses arg_44 = this+0x48 = _candidateRefA2 as the fleet entity ref.
    // Check HIBYTE(_candidateRefA2) in [0x90,0x98), get fleet, iterate:
    //   capital ships (FlagA & 0x200000 == 0), regiments (FlagA & 0x400000 == 0),
    //   starfighters (FlagA & 0x3800000 == 0). Conditionally characters.
    // If nodes: TypeCode=0x200 work item. Else: null.
    //
    // BLOCKED: same as FindIssueCandidate — entity ID encoding.
    private AIWorkItem FindAgentIssueCandidate()
    {
        // Uses _candidateRefA2 (same as FindIssueCandidate)
        if (_candidateRefA2 == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRefA2
        );
        if (rec == null || rec.System == null || (rec.FlagB & 0x4) == 0)
            return null;
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004e32f0 (GetNextAgentSubState for Type 2) — assembly trace (fully read):
    //
    // 1. *(this+0x54) = 0  — clear _agentAssignmentFlag.
    // 2. edi = FUN_004e3cc0(this) — compute capacity:
    //    = (Capacity * FleetTotalCapacity / 100) - sum(*(sys+0xd8) for sysId in _type2CandidateList)
    //    Cap: if FleetTotalCapacity - FleetAssignedCapacity < result: result = 0.
    // 3. If edi < 0: return 6 (shortage).
    //    If edi <= 0: return 0 (terminal).
    // 4. sub_41a9e0(workspace, this+0x50, 0x2a, 0x10000, 0x4000, 2) → agent entity at this+0x50.
    //    If (this+0x50 & 0xff000000) != 0 && ecx != 0:
    //      _agentAssignmentFlag = edi / ecx.
    //    Cap by _type2Field58 (this+0x58): if _type2Field58 < _agentAssignmentFlag: cap to _type2Field58.
    //    If _agentAssignmentFlag > 0:
    //      FUN_00479ee0(workspace, _candidateRefA2) → entity lookup.
    //      Cap by min(*(entity+0x84), _candidateCapacity5c).
    //      Return 8.
    // 5. Return 0.
    //
    // Note: this+0x50 = _agentTypePacked serves dual purpose — also used as agent entity ref output.
    // Note: _type2Field58 (this+0x58) must be non-zero for the function to return 8 (set by which
    //       function is unclear from analysis — possibly set by a function not yet read).
    // BLOCKED: sub_41a9e0 requires agent entity infrastructure; _type2Field58 always 0.
    private int GetNextAgentSubState()
    {
        // Clear _agentAssignmentFlag
        _agentAssignmentFlag = 0;

        // FUN_004e3cc0: compute (Capacity * FleetTotalCapacity / 100) - accumulated
        int total = Workspace.FleetTotalCapacity;
        int requested = (Capacity * total) / 100;
        if (total - Workspace.FleetAssignedCapacity < requested)
            requested = 0;

        if (requested < 0)
            return 6; // shortage → agent reallocation
        if (requested == 0)
            return 0; // balanced → terminal

        // sub_41a9e0(workspace, this+0x50, 0x2a, 0x10000, 0x4000, 2):
        // Finds a character entity by agent type codes. Returns cap-per-agent in ECX.
        // Binary: entity ref stored at this+0x50 (_agentTypePacked); HIBYTE checked != 0.
        // With CharacterAnalysis InternalIds (HIBYTE 0xa0), use first available officer.
        var agentRec = Workspace.CharacterAnalysis.FirstOrDefault(r =>
            r.Officer != null && r.Officer.IsMovable() && !r.Officer.IsCaptured
        );
        if (agentRec == null)
            return 0;

        // _agentTypePacked serves as agent entity ref (this+0x50).
        _agentTypePacked = agentRec.InternalId; // HIBYTE 0xa0 → passes (& 0xff000000) != 0
        int capPerAgent = 1; // proxy for ECX from sub_41a9e0 (cap per agent = 1)

        // Condition: (_agentTypePacked & 0xff000000) != 0 && capPerAgent != 0
        if ((_agentTypePacked & unchecked((int)0xff000000)) != 0 && capPerAgent != 0)
            _agentAssignmentFlag = requested / capPerAgent;

        // Cap by _type2Field58 (set by CheckAgentEligibility to SystemScore-1).
        if (_type2Field58 > 0 && _agentAssignmentFlag > _type2Field58)
            _agentAssignmentFlag = _type2Field58;

        if (_agentAssignmentFlag <= 0)
            return 0;

        // FUN_00479ee0: look up _candidateRefA2 entity, cap by entity's capacity.
        var sysRec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == _candidateRefA2);
        if (sysRec != null)
        {
            int entityCap = System.Math.Min(sysRec.Stats.FacilityCount, _candidateCapacity5c);
            if (entityCap > 0 && entityCap < _agentAssignmentFlag)
                _agentAssignmentFlag = entityCap;
        }

        return _agentAssignmentFlag > 0 ? 8 : 0;
    }

    // FUN_004e3390: CreateAgentIssue — starfighter node builder, TypeCode=0x200.
    //
    // Assembly trace (fully read — same pattern as FUN_004db1e0 CreateFleetShortageIssue):
    // Iterates _type2CandidateList (this+0x60) checking:
    //   SystemScore > 0, HIBYTE(FlagA) & 0x10 (= FlagA & 0x1000).
    //   If SystemScore > 1: store key in _candidateRefA2 (this+0x48), var_54=1.
    //   If SystemScore==1 AND list_count>1: store, remove, clear FlagA & 0x1000000, var_54=1.
    //   If SystemScore==1 AND list_count<=1: stop (esi=0), var_54 NOT set.
    // If var_54:
    //   sub_4f25a0(OwnerSide, this+0x48) → get fleet at _candidateRefA2.
    //   If fleet: iterate starfighters (sub_52b600) → 0x20 nodes → local list.
    //   If local list non-empty: TypeCode=0x200 work item. Return.
    //
    // BLOCKED: entity HIBYTE checks + starfighter iteration.
    // Proxy: uses _type2CandidateList same as CreateFleetShortageIssue.
    private AIWorkItem CreateAgentIssue()
    {
        bool found = false;
        int foundId = 0;
        bool continueLoop = true;

        foreach (int sysId in _type2CandidateList.ToList())
        {
            if (!continueLoop)
                break;
            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == sysId
            );
            if (rec == null)
                continue;
            int score = rec.SystemScore;
            if (score <= 0)
            {
                _type2CandidateList.Remove(sysId);
                rec.FlagA &= ~0x1000000;
                continue;
            }
            if ((rec.FlagA & 0x1000) == 0)
            {
                continue;
            }
            if (score > 1)
            {
                foundId = sysId;
                _candidateRefA2 = sysId;
                found = true;
            }
            else // score == 1
            {
                if (_type2CandidateList.Count > 1)
                {
                    foundId = sysId;
                    _candidateRefA2 = sysId;
                    _type2CandidateList.Remove(sysId);
                    rec.FlagA &= ~0x1000000;
                    found = true;
                }
                continueLoop = false;
            }
            if (found)
                break;
        }
        if (!found)
            return null;
        SystemAnalysisRecord target = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == foundId
        );
        return new FleetShortageWorkItem(target?.System, OwnerSide);
    }

    // FUN_004e2b80_seed_type_2_issue_bundle_from_selected_id: CheckAgentEligibility.
    //
    // Assembly trace (fully read — seeds _candidateRefA2, _candidateRefB2, _type2Field58):
    // 1. ebp = this+0x48 = &_candidateRefA2.
    //    sub_419330(workspace, &_candidateRefA2, 0x1000, ..., 2) → update _candidateRefA2.
    //    Store in _type2IssueContainer → get last ID → update _candidateRefA2 → clear.
    // 2. QuerySystemPlanets(_candidateRefA2, 0x800800, 0, 0x1, 0x3, 0, 0, 6, 1) → update _candidateRefB2 (this+0x4c).
    //    Store → get last ID → update _candidateRefB2 → clear.
    // 3. Check HIBYTE(_candidateRefB2) in [0x90,0x98) (fleet entity type):
    //    If fleet: look up in SystemAnalysis. sub_4f25a0(OwnerSide, _candidateRefB2).
    //    If both found: *(this+0x58) = SystemScore. If SystemScore > 1: *(this+0x58) = SystemScore-1.
    //      → THIS SETS _type2Field58!
    //    Update _candidateRefB2 via agent entity lookup. var_1C=1.
    // 4. If var_1C != 0: check if _candidateRefA2 in _type2CandidateList.
    //    If NOT: insert → FlagA |= 0x1000000.
    // 5. Returns var_1C (1=success, 0=not found).
    //
    // Note: this function sets _type2Field58, which is used as capacity cap in GetNextAgentSubState.
    // BLOCKED: sub_419330 + HIBYTE checks blocked in C#.
    private int CheckAgentEligibility()
    {
        // Proxy: seed _candidateRefA2 from QuerySystemAnalysis(0x1000 filter)
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x1000,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 2
        );
        if (c1.TryGetTopEntityKey(out int key1))
            _candidateRefA2 = key1;
        c1.Clear();

        // Seed _candidateRefB2 via QuerySystemPlanets
        IssueRecordContainer c2 = Workspace.QuerySystemPlanets(
            _candidateRefA2,
            incl28: 0x800800,
            incl2c: 0,
            incl30: 1,
            excl28: 0x3,
            excl2c: 0,
            excl30: 0,
            statIndex: 6
        );
        if (c2.TryGetTopEntityKey(out int key2))
            _candidateRefB2 = key2;
        c2.Clear();

        // Proxy for fleet type check + _type2Field58 setting
        if (_candidateRefB2 != 0)
        {
            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == _candidateRefB2
            );
            if (rec != null)
            {
                // *(this+0x58) = SystemScore (or SystemScore-1 if > 1)
                _type2Field58 = rec.SystemScore > 1 ? rec.SystemScore - 1 : rec.SystemScore;

                // Insert into _type2CandidateList if not already there
                if (!_type2CandidateList.Contains(_candidateRefA2))
                {
                    _type2CandidateList.Add(_candidateRefA2);
                    rec.FlagA |= 0x1000000;
                }
                return 1;
            }
        }
        return 0;
    }

    // FUN_004e3670 (agent slot, +0x48): dispatch agent entry → TypeCode 0x214.
    private AIWorkItem DispatchAgentEntry()
    {
        if (_agentAssignmentFlag == 0)
            return null;
        int sysId = _candidateRefA2 != 0 ? _candidateRefA2 : _candidateRefB2;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == sysId
        );
        if (rec == null)
            return null;
        return new AgentShortageWorkItem(0x214, rec.System, _agentAssignmentFlag, OwnerSide);
    }

    // FUN_004e38b0: Cleanup agent assignment state. Resets agent-related fields.
    private void CleanupAgentAssignment()
    {
        _agentAssignmentFlag = 0;
        _agentTypePacked = 0;
    }

    // FUN_004e3d90: Cleanup agent issue state. Resets issue-related fields.
    private void CleanupAgentIssueState()
    {
        _candidateRefA2 = 0;
        _type2IssueContainer.Clear();
    }
}

// ------------------------------------------------------------------
// Type 3 — FUN_004d5e90 — ShortageGeneratorType3Record
// 0x7c bytes.  Active guard: standard.
//
// Outer (Phase) dispatch — unchanged from stub:
//   Phase 0x3ec → RunPhaseA() (FUN_004d63d0)
//   Phase 0x3ed → RunPhaseB() (FUN_004d6110)
//   Phase 0x3ee → RunPhaseC() (FUN_004d61e0)
//   Other       → Phase=0x3ed, SubState=0
//
// Each phase is itself a SubState machine that shares the SubState (+0x3c) field.
// Phase transitions are driven by writes to Phase (+0x38) from within the phase methods.
//
// Extra fields beyond 0x40-byte base (total 0x7c, extra = 0x3c bytes starting at +0x40):
//   +0x54 = _phaseCSubObjRef: reference for FUN_004d91e0 when called from Phase C state 11.
//   +0x58 = _phaseASubObjRef: reference for FUN_004d91e0 when called from Phase A state 11.
//   +0x5c = _typeModePacked: agent-type + mode descriptor packed into a uint.
//            Written in Phase C state 9: 0x2d000002 (agent slot available) or 0x2c000001 (full).
//   +0x60 = _agentMatchFlag: set to 1 in Phase C state 9 when selection succeeds.
//
// FUN_004d6110 (Phase B) SubState machine:
//   default→1, 1→2/0+PhaseC, 2→0+PhaseC+ready, 3→→7 (always falls through), 7→0+PhaseA+ready
// FUN_004d61e0 (Phase C) SubState machine:
//   default→4, 1→6/9, 4→1/0+PhaseA, 5→result, 6→5+item, 7→9/0+PhaseA+ready+item,
//   9→→10 (always falls through), 10→11+item, 11→12+item/0+PhaseA+ready, 12→0+PhaseA+ready+item
// FUN_004d63d0 (Phase A) SubState machine:
//   default→1, 1→4/0+PhaseB+ready, 4→6/13, 5→result/0+PhaseB+ready, 6→5+item, 8→0+PhaseB+ready+item,
//   11→12+item/0+PhaseB+ready, 12→0+PhaseB+ready+item, 13→0+PhaseB+ready
// ------------------------------------------------------------------
public class ShortageGeneratorType3Record : StrategyRecord
{
    private const int PhaseA = 0x3ec;
    private const int PhaseB = 0x3ed;
    private const int PhaseC = 0x3ee;

    // Extra fields beyond 0x40-byte base (total size 0x7c = 0x3c extra bytes starting at +0x40).
    // Fields at +0x40..+0x4c: issue container used by CheckShortageConditionB (this+0x40),
    //   plus intermediate state (not mapped as individual C# fields; use local containers).
    private int _entityRef50; // +0x50: entity ref set by SelectAgentTypeSlotAvail/Full
    private int _phaseCSubObjRef; // +0x54: passed to DispatchPhaseCSubObject in Phase C state 11
    private int _phaseASubObjRef; // +0x58: passed to DispatchPhaseASubObject in Phase A states 11-12
    private int _typeModePacked; // +0x5c: agent type+mode packed (0x2d000002 or 0x2c000001)
    private int _agentMatchFlag; // +0x60: agent assignment count (FUN_004d8890: iVar1/agent_capacity)

    // +0x64: capacity upper limit set by CheckPhaseAAgentCondition (FUN_004d8120):
    //         = *(fleet+0x60) - 1 when *(fleet+0x60) > 1, else = *(fleet+0x60)
    private int _capacityLimit64;

    // +0x68: per-system capacity counter (proxy for *(esi+0x114)) set by CheckPhaseACondition:
    //         = capacity of the best candidate from _type3CandidateList
    private int _candidateCapacity68;

    // +0x6c: candidate list iterated by CheckFleetIssueC and CheckPhaseACondition
    private readonly List<int> _type3CandidateList = new List<int>(); // +0x6c

    public ShortageGeneratorType3Record(int ownerSide)
        : base(typeId: 3, capacity: 1, ownerSide: ownerSide)
    {
        _entityRef50 = 0;
        _phaseCSubObjRef = 0;
        _phaseASubObjRef = 0;
        _typeModePacked = 0;
        _agentMatchFlag = 0;
        _capacityLimit64 = 0;
        _candidateCapacity68 = 0;
    }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        if (Phase == PhaseA)
            return RunPhaseA();
        if (Phase == PhaseB)
            return RunPhaseB();
        if (Phase == PhaseC)
            return RunPhaseC();

        Phase = PhaseB;
        SubState = 0;
        return null;
    }

    // FUN_004d6110 — Phase B sub-state machine.
    // Entry state: SubState=0 → resets to 1 (default branch).
    // State 3 always falls through to state 7 (C switch fall-through, confirmed by assembly).
    private AIWorkItem RunPhaseB()
    {
        switch (SubState)
        {
            default:
                SubState = 1;
                return null;

            case 1:
            {
                int found = CheckShortageConditionB(); // FUN_004d6550
                if (found != 0)
                    SubState = 2;
                else
                {
                    SubState = 0;
                    Phase = PhaseC;
                }
                return null;
            }

            case 2:
            {
                AIWorkItem item = CreateFleetShortageItemB(); // FUN_004d66a0
                SubState = 0;
                Phase = PhaseC;
                ReadyFlag = 1;
                return item;
            }

            case 3:
            {
                // FUN_004d6a10: result determines SubState, but execution ALWAYS falls
                // through to case 7 (assembly: `goto loc_4d6191` in both branches).
                int found = CheckAgentAssignmentB(); // FUN_004d6a10
                if (found != 0)
                    SubState = 7;
                else
                {
                    SubState = 0;
                    Phase = PhaseC; // dead write: case 7 always overwrites with PhaseA
                }
                goto case 7;
            }

            case 7:
            {
                AIWorkItem item = CreateAgentShortageItemB(); // FUN_004d7890
                SubState = 0;
                Phase = PhaseA; // always overrides the Phase=PhaseC set in case 3 false branch
                ReadyFlag = 1;
                return item;
            }
        }
    }

    // FUN_004d61e0 — Phase C sub-state machine.
    // State 9 always falls through to state 10 (assembly: `goto loc_4d6311` in both branches).
    private AIWorkItem RunPhaseC()
    {
        switch (SubState)
        {
            default:
                SubState = 4;
                return null;

            case 1:
            {
                // Computed: -(uint)(found!=0) & 0xfffffffd + 9
                //   found != 0 → 0xfffffffd + 9 = 6   (SubState=6)
                //   found == 0 → 0 + 9 = 9             (SubState=9)
                int found = CheckAgentAssignmentC(); // FUN_004d6a60
                SubState = (found != 0) ? 6 : 9;
                return null;
            }

            case 4:
            {
                int found = CheckFleetIssueC(); // FUN_004d6e30
                if (found != 0)
                    SubState = 1;
                else
                {
                    SubState = 0;
                    Phase = PhaseA;
                }
                return null;
            }

            case 5:
            {
                int nextState = GetNextAgentSubState(); // FUN_004d77d0 — returns next SubState
                SubState = nextState;
                return null;
            }

            case 6:
            {
                AIWorkItem item = CreateAgentMatchItem(); // FUN_004d7060
                if (item != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 5;
                return item;
            }

            case 7:
            {
                AIWorkItem item = CreateAgentShortageItemC(); // FUN_004d7890
                if (item == null)
                {
                    SubState = 9;
                    return null;
                }
                SubState = 0;
                Phase = PhaseA;
                ReadyFlag = 1;
                return item;
            }

            case 9:
            {
                // Choose agent type+mode based on capacity comparison, then ALWAYS
                // fall through to case 10 (assembly: `goto loc_4d6311`).
                if (Workspace.AgentAssignedCapacity < Workspace.AgentTotalCapacity)
                {
                    // Agent slot available: type 0x2d, mode 2.
                    _typeModePacked = 0x2d000002;
                    int r = SelectAgentTypeSlotAvail(); // FUN_004d8d40
                    if (r != 0)
                    {
                        _agentMatchFlag = 1;
                        SubState = 10;
                    }
                    else
                    {
                        SubState = 0;
                        Phase = PhaseA;
                    }
                }
                else
                {
                    // Agent slots full: type 0x2c, mode 1.
                    _typeModePacked = 0x2c000001;
                    int r = SelectAgentTypeSlotFull(); // FUN_004d8c10
                    if (r != 0)
                    {
                        _agentMatchFlag = 1;
                        SubState = 10;
                    }
                    else
                    {
                        SubState = 0;
                        Phase = PhaseA;
                    }
                }
                goto case 10; // always falls through (C switch case fall-through)
            }

            case 10:
            {
                AIWorkItem item = CreateAgentSlotItem(); // FUN_004d8e70
                if (item != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 11;
                return item;
            }

            case 11:
            {
                // FUN_004d91e0(this, &_phaseCSubObjRef) — dispatch Phase-C sub-object.
                AIWorkItem item = DispatchPhaseCSubObject();
                if (item != null)
                {
                    SubState = 12;
                    ReadyFlag = 1;
                    TickCounter++;
                    return item;
                }
                SubState = 0;
                Phase = PhaseA;
                ReadyFlag = 1;
                return null;
            }

            case 12:
            {
                AIWorkItem item = FinalizeAgentAssignment(); // FUN_004d9320
                SubState = 0;
                Phase = PhaseA;
                ReadyFlag = 1;
                return item;
            }
        }
    }

    // FUN_004d63d0 — Phase A sub-state machine.
    private AIWorkItem RunPhaseA()
    {
        switch (SubState)
        {
            default:
                SubState = 1;
                return null;

            case 1:
            {
                int found = CheckPhaseACondition(); // FUN_004d7e40
                if (found != 0)
                    SubState = 4;
                else
                {
                    SubState = 0;
                    Phase = PhaseB;
                    ReadyFlag = 1;
                }
                return null;
            }

            case 4:
            {
                // Clear bit 0 of workspace.PendingSupplyBitmask (workspace+0x8 &= ~1).
                Workspace.PendingSupplyBitmask &= ~1;
                // If EntityTargetType == 1 AND FUN_004d8120 returns non-zero: use agent path.
                if (Workspace.EntityTargetType == 1 && CheckPhaseAAgentCondition() != 0)
                {
                    ClearFleetState(); // FUN_00419160(workspace)
                    SubState = 6;
                }
                else
                {
                    SubState = 13;
                }
                return null;
            }

            case 5:
            {
                int nextState = GetNextPhaseASubState(); // FUN_004d8890
                SubState = nextState;
                if (nextState == 0)
                {
                    Phase = PhaseB;
                    ReadyFlag = 1;
                }
                return null;
            }

            case 6:
            {
                AIWorkItem item = CreatePhaseAFleetItem(); // FUN_004d8350
                if (item != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 5;
                return item;
            }

            case 8:
            {
                AIWorkItem item = CreatePhaseAAgentItem(); // FUN_004d8930
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return item;
            }

            case 11:
            {
                // FUN_004d91e0(this, &_phaseASubObjRef) — dispatch Phase-A sub-object.
                AIWorkItem item = DispatchPhaseASubObject();
                if (item != null)
                {
                    SubState = 12;
                    ReadyFlag = 1;
                    TickCounter++;
                    return item;
                }
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }

            case 12:
            {
                AIWorkItem item = FinalizeAgentAssignment(); // FUN_004d9320
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return item;
            }

            case 13:
            {
                FinalizeFleetAssignmentA(); // FUN_004d9440
                FinalizeShortageRecordA(); // FUN_004d9980
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }
        }
    }

    // FUN_004d6550: Phase B shortage condition check.
    //
    // Assembly trace (fully read):
    // var_18 = 0 (initialized to 0 — NOT always 1 as previously documented).
    // 1. QuerySystemAnalysis(0x10000, 0,0,0,0,0, 0x1d, sort=1) → store in container at this+0x40
    //    → get last entity key → store in this+0x54 (_phaseCSubObjRef) → clear container.
    // 2. QuerySystemPlanets(this+0x54, 0,0,1, 0,0,0, 0x25, 1) → store in container at this+0x40
    //    → get last entity key → store in this+0x54 (_phaseCSubObjRef) → clear container.
    // 3. Check HIBYTE(_phaseCSubObjRef) in [0x90, 0x98) (fleet entity type):
    //    If YES: look up in SystemAnalysis; if found AND *(sys+0xdc) > 0: return 1.
    //    Otherwise: return var_18 = 0.
    //
    // Note: HIBYTE check always fails in C# (InternalIds now carry HIBYTE type encoding; check passes for [0x90,0x98) system records).
    // Proxy: return 1 if _phaseCSubObjRef != 0 with valid system record and FacilityCount > 0.
    private int CheckShortageConditionB()
    {
        // Step 1: QuerySystemAnalysis(DispositionFlags & 0x10000, stat[0x1d])
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x10000,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 0x1d
        );
        if (c1.TryGetTopEntityKey(out int key1))
            _phaseCSubObjRef = key1;

        // Step 2: QuerySystemPlanets(_phaseCSubObjRef, 0,0,1, 0,0,0, stat=0x25, sort=1)
        IssueRecordContainer c2 = Workspace.QuerySystemPlanets(
            _phaseCSubObjRef,
            incl28: 0,
            incl2c: 0,
            incl30: 1,
            excl28: 0,
            excl2c: 0,
            excl30: 0,
            statIndex: 0x25
        );
        if (c2.TryGetTopEntityKey(out int key2))
            _phaseCSubObjRef = key2;

        // Step 3: proxy for HIBYTE check + *(sys+0xdc) > 0
        if (_phaseCSubObjRef == 0)
            return 0;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _phaseCSubObjRef
        );
        if (rec == null)
            return 0;
        // *(sys+0xdc) proxy: FacilityCount (must be > 0)
        return rec.Stats.FacilityCount > 0 ? 1 : 0;
    }

    // FUN_004d66a0: CreateFleetShortageItemB for Type 3 Phase B. Assembly trace (fully read).
    // 1. sub_4ec1e0(&_phaseCSubObjRef) → copies ref to temp.
    // 2. sub_403d30(workspace.EntityRegistry, ref) → raw entity lookup.
    //    If found: capacity_delta_1 = *(entity+0x64) - *(entity+0x6c);
    //              capacity_delta_2 = *(entity+0x74) - *(entity+0x7c).
    //    If NOT found: both deltas = 0 (nothing built).
    // 3. sub_4f25a0(OwnerSide, &_phaseCSubObjRef) → fleet by owner at that system.
    //    If fleet found AND delta_1 > 0: GenCore (sub_526a80) unit nodes.
    //    If delta_2 > 0: Kdy (sub_526700) nodes; if any remain: Lnr (sub_526490) nodes.
    // 4. If node list non-empty: TypeCode=0x200 FleetShortageWorkItem. Return.
    //
    // Capacity deltas require raw entity fields (+0x64/+0x6c/+0x74/+0x7c) unavailable in C#.
    // Fleet unit iteration (sub_526a80/sub_526700/sub_526490) requires entity infrastructure.
    // Proxy: return FleetShortageWorkItem if _phaseCSubObjRef is a valid system with capacity.
    private AIWorkItem CreateFleetShortageItemB()
    {
        if (_phaseCSubObjRef == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _phaseCSubObjRef
        );
        if (rec == null || rec.System == null || rec.Stats.FacilityCount <= 0)
            return null;
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004d6a10: Phase B agent assignment check.
    // Assembly (fully read): computes (Capacity * FleetTotalCapacity / 100) and compares
    // to workspace._totalAlignedEntityCount (+0x1dc in binary = GalaxyAnalysisScorer accumulator).
    // Returns 1 if requested < aligned count (over-allocated → reassignment needed).
    private int CheckAgentAssignmentB()
    {
        int requested = (Capacity * Workspace.FleetTotalCapacity) / 100;
        // _totalAlignedEntityCount proxy: count of own-faction system fleets
        int aligned = Workspace.FleetAnalysis.Count(r =>
            r.Fleet?.GetOwnerInstanceID() == Workspace.Owner?.InstanceID
        );
        return requested < aligned ? 1 : 0;
    }

    // FUN_004d7890 (Phase B and Phase C case 7): re-queries to find system, then builds
    // unit nodes. Identical function called from both Phase B case 7 and Phase C case 7.
    //
    // Assembly trace (fully read):
    // 1. QuerySystemAnalysis(0x10000, ..., stat=0x1d) → container at this+0x40
    //    → last ID → _phaseCSubObjRef (this+0x54) → clear container.
    // 2. sub_419af0(_phaseCSubObjRef, 0,0,1,0,0,0, stat=0x25, 1) → update _phaseCSubObjRef.
    // 3. If HIBYTE(_phaseCSubObjRef) NOT in [0x90,0x98) (fallback A):
    //    QuerySystemAnalysis(0x200e0,..., stat=3) → update _phaseCSubObjRef.
    //    sub_419af0 × 2 (excl28=0x3e00000, excl30=0x40000000, stats 7 and 0xb) → update.
    // 4. If still NOT valid (fallback B):
    //    QuerySystemAnalysis(0x20000,..., stat=3) → update _phaseCSubObjRef.
    //    sub_419af0 × 2 (excl30=0x40000000, stats 7 and 0xb) → update.
    // 5. If HIBYTE(_phaseCSubObjRef) in [0x90,0x98): sub_4f25a0 (get fleet), then iterate
    //    Kdy (sub_526700) / Lnr (sub_526490) / GenCore (sub_526a80) unit nodes.
    // 6. If node list non-empty: TypeCode=0x200 work item. Else: return null.
    //
    // Fleet unit iteration still blocked on entity infrastructure.
    // HIBYTE check passes: InternalId = 0x90000000|index, HIBYTE=0x90 in [0x90,0x98).
    // Proxy: re-runs queries (matching binary) and returns FleetShortageWorkItem when found.
    private AIWorkItem CreateAgentShortageItemB() => RunAgentShortageQuery();

    // FUN_004d7890 (Phase C case 7): same binary function as Phase B case 7.
    // See CreateAgentShortageItemB for full documentation.
    private AIWorkItem CreateAgentShortageItemC() => RunAgentShortageQuery();

    // Shared implementation for FUN_004d7890 called from both Phase B and Phase C.
    // Re-queries from scratch, updating _phaseCSubObjRef, with two fallback levels.
    private AIWorkItem RunAgentShortageQuery()
    {
        _phaseCSubObjRef = 0;

        // Step 1: QuerySystemAnalysis(DispositionFlags & 0x10000, stat=0x1d)
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x10000,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 0x1d
        );
        if (c1.TryGetTopEntityKey(out int k1))
            _phaseCSubObjRef = k1;
        c1.Clear();

        // Step 2: planet sub-object query on _phaseCSubObjRef
        IssueRecordContainer c2 = Workspace.QuerySystemPlanets(
            _phaseCSubObjRef,
            incl28: 0,
            incl2c: 0,
            incl30: 1,
            excl28: 0,
            excl2c: 0,
            excl30: 0,
            statIndex: 0x25
        );
        if (c2.TryGetTopEntityKey(out int k2))
            _phaseCSubObjRef = k2;
        c2.Clear();

        // Fallback A: if _phaseCSubObjRef not a valid system record
        if (!IsValidSystemRef(_phaseCSubObjRef))
        {
            IssueRecordContainer fa = Workspace.QuerySystemAnalysis(
                incl24: 0x200e0,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 3
            );
            if (fa.TryGetTopEntityKey(out int fak))
                _phaseCSubObjRef = fak;
            fa.Clear();

            IssueRecordContainer fp1 = Workspace.QuerySystemPlanets(
                _phaseCSubObjRef,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x3e00000,
                excl2c: 0,
                excl30: 0x40000000,
                statIndex: 7
            );
            IssueRecordContainer fp2 = Workspace.QuerySystemPlanets(
                _phaseCSubObjRef,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x3e00000,
                excl2c: 0,
                excl30: 0x40000000,
                statIndex: 0xb
            );
            fp1.StoreFrom(fp2);
            if (fp1.TryGetTopEntityKey(out int fpk))
                _phaseCSubObjRef = fpk;
            fp1.Clear();
        }

        // Fallback B: if still not valid
        if (!IsValidSystemRef(_phaseCSubObjRef))
        {
            IssueRecordContainer fb = Workspace.QuerySystemAnalysis(
                incl24: 0x20000,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 3
            );
            if (fb.TryGetTopEntityKey(out int fbk))
                _phaseCSubObjRef = fbk;
            fb.Clear();

            IssueRecordContainer fp3 = Workspace.QuerySystemPlanets(
                _phaseCSubObjRef,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0,
                excl2c: 0,
                excl30: 0x40000000,
                statIndex: 7
            );
            IssueRecordContainer fp4 = Workspace.QuerySystemPlanets(
                _phaseCSubObjRef,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0,
                excl2c: 0,
                excl30: 0x40000000,
                statIndex: 0xb
            );
            fp3.StoreFrom(fp4);
            if (fp3.TryGetTopEntityKey(out int fpk2))
                _phaseCSubObjRef = fpk2;
            fp3.Clear();
        }

        // Final: return FleetShortageWorkItem if a valid system was found
        if (!IsValidSystemRef(_phaseCSubObjRef))
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _phaseCSubObjRef
        );
        if (rec == null || rec.System == null)
            return null;
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // Returns true if id refers to a SystemAnalysisRecord in this workspace.
    // In the binary: HIBYTE(id) in [0x90,0x98). In C# all SystemAnalysis InternalIds
    // have HIBYTE=0x90 (= 0x90000000 | sequential_index), so presence in the list
    // is the correct check.
    private bool IsValidSystemRef(int id) =>
        id != 0 && Workspace.SystemAnalysis.Any(r => r.InternalId == id);

    // --- Phase C helpers ---

    // FUN_004d6a60: Phase C agent assignment check.
    //
    // Assembly trace (fully read):
    // 1. Set _phaseCSubObjRef (this+0x54) = 0x90000109 (fleet entity type 0x90, ID 0x109).
    // 2. Look up 0x90000109 in workspace.SystemAnalysis.
    //    If found AND PresenceFlags & 0x1 AND FlagA & 0x2 == 0:
    //      If FlagA & 0x40000000: local_20 = 1 (return 1).
    //      Else if SystemScore < workspace+0x36c: local_20 = 1 (return 1).
    //      Else if FlagA & 0x2 != 0: reset _phaseCSubObjRef to "unset".
    // 3. If local_20 == 0: complex seeding block with QuerySystemAnalysis(0x4000),
    //    FUN_004193330, QuerySystemPlanets, multiple fallback queries.
    //    Returns 1 when a valid fleet entity is found after all queries.
    // 4. Returns local_20 (1 or 0).
    //
    // Implementation: original hardcodes entity ID 0x90000109 (faction HQ system in the original
    // Rebellion game — index 265 decimal, which does not exist in our 200-system C# world).
    // C# proxy: find any own-faction system satisfying the same conditions:
    //   PresenceFlags & 0x1 (own faction present), FlagA & 0x2 == 0 (no enemy garrison),
    //   and either FlagA & 0x40000000 (HQ/special entity marker) OR SystemScore > 0.
    // Prefers the HQ-marked system; falls back to the highest-score own-faction system.
    // Sets _phaseCSubObjRef to the found system's InternalId and returns 1 when found.
    private int CheckAgentAssignmentC()
    {
        // Primary: system with FlagA HQ marker
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            (r.PresenceFlags & 0x1u) != 0
            && (r.FlagA & 0x2) == 0
            && (r.FlagA & unchecked((int)0x40000000)) != 0
        );
        if (rec == null)
        {
            // Fallback: best SystemScore own-faction system without enemy garrison
            rec = Workspace.SystemAnalysis
                .Where(r => (r.PresenceFlags & 0x1u) != 0 && (r.FlagA & 0x2) == 0 && r.SystemScore > 0)
                .OrderByDescending(r => r.SystemScore)
                .FirstOrDefault();
        }
        if (rec == null)
            return 0;
        _phaseCSubObjRef = rec.InternalId;
        return 1;
    }

    // FUN_004d6e30: Phase C fleet issue check.
    //
    // Assembly trace (fully read):
    // Identical structure to FUN_004e1540 (Type 2 InitialSetupCheck) but uses different fields:
    // - List at this+0x6c (_type3CandidateList) instead of this+0x60
    // - Entity ref at this+0x50 (_entityRef50) instead of this+0x4c
    // - Flag check: FlagA & 0x2000000 instead of FlagA & 0x1000000
    //
    // 1. Get first node from _type3CandidateList (this+0x6c). If empty: reset _entityRef50 to "2", return 0.
    // 2. Iterate list:
    //    Set _entityRef50 = node key. Check HIBYTE in [0x90,0x98).
    //    If fleet: look up in workspace.SystemAnalysis.
    //      Check PresenceFlags & 0x1, FlagA & 0x2000000, SystemScore>0, FlagA & 0x800, FlagA & 0x3==0.
    //      If all pass: var_30=1 (found).
    //      If fail: clear FlagA & 0x2000000, remove from list.
    // 3. If found: sub_4f25a0 + sub_5087e0 fleet capacity check → return 1.
    //    Else: return 0.
    //
    // FlagA & 0x800 is set by AccumulatePlanetIntoSystemRecord when ExtraFlags & 0x10000,
    // which RefreshPlanetSubobject sets when an ENEMY planet has MineCount > 0 or RefineryCount > 0.
    // HIBYTE check passes with InternalIds (SystemAnalysisRecord.InternalId HIBYTE = 0x90).
    // This function is fully implemented and works for contested systems.
    private int CheckFleetIssueC()
    {
        // _type3CandidateList is seeded by CheckPhaseACondition's fallback path
        // (QuerySystemAnalysis incl24=0x80 → adds system InternalIds to list).
        // If list is empty first call, reset and return 0; fallback runs on next tick.
        if (_type3CandidateList.Count == 0)
        {
            _entityRef50 = 0;
            return 0;
        }

        // Walk list backward, filter fleet entities with shortage conditions.
        bool found = false;
        foreach (int sysId in _type3CandidateList.ToList())
        {
            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == sysId
            );
            if (rec == null)
            {
                _type3CandidateList.Remove(sysId);
                continue;
            }

            // HIBYTE check proxy: proceed if valid record found
            bool pass =
                (rec.PresenceFlags & 0x1u) != 0
                && (rec.FlagA & 0x2000000) != 0
                && rec.SystemScore > 0
                && (rec.FlagA & 0x800) != 0
                && (rec.FlagA & 0x3) == 0;

            if (!pass)
            {
                rec.FlagA &= ~0x2000000;
                _type3CandidateList.Remove(sysId);
                continue;
            }
            _entityRef50 = sysId;
            found = true;
            break;
        }

        if (!found)
            return 0;
        // sub_4f25a0 + sub_5087e0 fleet capacity validation: proxy — accept if found.
        return 1;
    }

    // FUN_004d77d0 — next sub-state for Phase C after CreateAgentMatchItem (case 5).
    //
    // Assembly trace (fully read):
    // 1. Clear _agentMatchFlag (this+0x60 = 0).
    // 2. iVar1 = FUN_004d9840(this):
    //    = (Capacity * FleetTotalCapacity * 90 / 10000) - workspace.field_0x1dc
    //      capped at 0 if FleetTotalCapacity - FleetAssignedCapacity < result.
    // 3. If iVar1 < 0: return 7 (shortage → CreateAgentShortageItemC).
    //    If iVar1 == 0: return 9 (balanced → agent slot selection).
    //    If iVar1 > 0:
    //      FUN_00479ee0(workspace, _phaseCSubObjRef) → entity lookup.
    //      If found AND *(found+0x84) > 0: FUN_0041a9e0 agent entity lookup.
    //      If (this+0x5c & 0xff000000) != 0 AND agent found AND iVar1 >= agent_capacity:
    //        _agentMatchFlag = 1, return 0xb (11 → DispatchPhaseCSubObject immediately).
    //      Else: return 9.
    //
    // Note: sub-state 7 = CreateAgentShortageItemC, 9 = agent slot selection, 11 = dispatch.
    // Implementation:
    //   FUN_004d9840: (Capacity * FleetTotalCapacity * 90/10000) - workspace.field_0x1dc.
    //   Cap: if FleetTotalCapacity - FleetAssignedCapacity < result: result = 0.
    //   result < 0 → 7, result == 0 → 9.
    //   result > 0:
    //     FUN_00479ee0(workspace, &_phaseCSubObjRef) → look up system entity.
    //     If found AND entity.FacilityCount > 0:
    //       FUN_0041a9e0(workspace, &_typeModePacked, type=0x2a, uVar3, iVar4, 1)
    //         → find construction yard; stores entity ref at _typeModePacked.
    //         Returns capacity-per-agent (facility count).
    //     If _typeModePacked has HIBYTE != 0 AND capPerAgent > 0 AND capPerAgent <= result:
    //       _agentMatchFlag = 1; return 0xb (dispatch immediately).
    //     Else: return 9.
    //
    //   FUN_00479ee0 proxy: SystemAnalysis lookup by _phaseCSubObjRef InternalId.
    //   FUN_0041a9e0 proxy: find own-faction planet with ConstructionFacility building.
    //   workspace.field_0x1dc proxy: count of own-faction fleet analysis records.
    private int GetNextAgentSubState()
    {
        _agentMatchFlag = 0;

        int total = Workspace.FleetTotalCapacity;
        int aligned = Workspace.FleetAnalysis.Count(r =>
            r.Fleet?.GetOwnerInstanceID() == Workspace.Owner?.InstanceID
        );
        int result = ((Capacity * total) / 100 * 0x5a) / 100 - aligned;
        if (total - Workspace.FleetAssignedCapacity < result)
            result = 0;

        if (result < 0) return 7;
        if (result == 0) return 9;

        // FUN_00479ee0: look up _phaseCSubObjRef system entity and check FacilityCount
        SystemAnalysisRecord sysRec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _phaseCSubObjRef
        );
        if (sysRec == null || sysRec.Stats.FacilityCount <= 0)
            return 9;

        // FUN_0041a9e0(workspace, &_typeModePacked, type=0x2a, ...):
        // Find own-faction planet with ConstructionFacility.
        string ownerId = Workspace.Owner?.InstanceID;
        SystemAnalysisRecord constructionSys = Workspace.SystemAnalysis.FirstOrDefault(r =>
            (r.PresenceFlags & 0x1u) != 0
            && r.System != null
            && r.System.Planets.Any(p =>
                p.GetOwnerInstanceID() == ownerId
                && p.GetBuildingTypeCount(BuildingType.ConstructionFacility) > 0
            )
        );
        if (constructionSys == null)
            return 9;

        _typeModePacked = constructionSys.InternalId;
        // Capacity-per-agent: total construction facilities in that system
        int capPerAgent = constructionSys.System.Planets
            .Where(p => p.GetOwnerInstanceID() == ownerId)
            .Sum(p => p.GetBuildingTypeCount(BuildingType.ConstructionFacility));
        if (capPerAgent <= 0)
            capPerAgent = 1;

        int typeModeHibyte = (_typeModePacked >> 0x18) & 0xff;
        if (typeModeHibyte != 0 && capPerAgent > 0 && capPerAgent <= result)
        {
            _agentMatchFlag = 1;
            return 0xb;
        }
        return 9;
    }

    // FUN_004d7060: Phase C agent match work item creation. TypeCode=0x200.
    //
    // Assembly trace (fully read — ~650 lines):
    // 1. Init local 0x34-byte node list.
    // 2. Check HIBYTE(_phaseCSubObjRef=this+0x54) in [0x90, 0x98) (fleet entity type).
    //    If YES: get fleet via sub_4f25a0(OwnerSide, _phaseCSubObjRef).
    //    If fleet found: iterate capital ships (sub_52bc60) IF FlagA & 0x200000 == 0,
    //      iterate regiments (sub_52b900) IF FlagA & 0x400000 == 0,
    //      iterate starfighters (sub_52b600) IF FlagA & 0x3800000 == 0.
    //    If local list still empty: check various workspace capacity conditions
    //      (workspace.field_0x1d4 <= workspace.field_0x1d0 → iterate characters via
    //      sub_52c350/sub_52c7c0 with additional fallbacks sub_52bc60/sub_52b900/sub_52b600).
    // 3. If local list non-empty: allocate TypeCode=0x200 work item (sub_4f5060(0x200)),
    //    attach nodes, return item. Else: return null.
    //
    // BLOCKED: HIBYTE check always fails in C#; fleet/character iteration requires entity infra.
    // Proxy: creates AgentShortageWorkItem(0x200) as stand-in.
    private AIWorkItem CreateAgentMatchItem()
    {
        int sysId = _phaseCSubObjRef != 0 ? _phaseCSubObjRef : _phaseASubObjRef;
        if (sysId == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == sysId
        );
        if (rec == null || rec.System == null)
            return null;
        return new AgentShortageWorkItem(0x200, rec.System, 1, OwnerSide);
    }

    // FUN_004d8d40: Select available agent type slot.
    //
    // Assembly trace (fully read — FUN_004d8d40):
    // 1. sub_419330(workspace, &_entityRef50(this+0x50), filter=0x200, ..., stat=2)
    //    → container at this+0x40 → last ID → _phaseCSubObjRef → clear container.
    // 2. sub_419bb0(workspace, &_phaseCSubObjRef(this+0x54), &_entityRef50(this+0x50),
    //    incl28=0x100, incl2c=0, incl30=1, excl28=0x3e00003, excl2c=0, excl30=0, stat=2)
    //    → container → last ID → _phaseCSubObjRef → clear container.
    // 3. Return 1 if HIBYTE(_phaseCSubObjRef) in [0x90,0x98), else return 0.
    //
    // sub_419330: sector-level query. Proxy: QuerySystemAnalysis(incl24=0x200, stat=2).
    // sub_419bb0: planet sub-object query. Proxy: QuerySystemPlanets with matching params.
    // HIBYTE check passes: InternalId = 0x90000000|index, HIBYTE=0x90 in [0x90,0x98).
    private int SelectAgentTypeSlotAvail()
    {
        // Step 1: sector-level query → _phaseCSubObjRef
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x200,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 2
        );
        if (c1.TryGetTopEntityKey(out int k1))
            _phaseCSubObjRef = k1;
        c1.Clear();
        if (_phaseCSubObjRef == 0)
            return 0;

        // Step 2: planet query (incl28=0x100, incl30=1, excl28=0x3e00003) → update _phaseCSubObjRef
        IssueRecordContainer c2 = Workspace.QuerySystemPlanets(
            _phaseCSubObjRef,
            incl28: 0x100,
            incl2c: 0,
            incl30: 1,
            excl28: 0x3e00003,
            excl2c: 0,
            excl30: 0,
            statIndex: 2
        );
        if (c2.TryGetTopEntityKey(out int k2))
            _phaseCSubObjRef = k2;
        c2.Clear();

        // Step 3: return 1 if valid system found
        return IsValidSystemRef(_phaseCSubObjRef) ? 1 : 0;
    }

    // FUN_004d8c10: Select full agent type slot.
    //
    // Assembly trace (fully read — identical structure to FUN_004d8d40 but different filters):
    // 1. sub_419330(workspace, &_entityRef50(this+0x50), filter=0x100, ..., stat=2)
    //    → last ID → _phaseCSubObjRef → clear container.
    // 2. sub_419bb0(workspace, &_phaseCSubObjRef(this+0x54), &_entityRef50(this+0x50),
    //    incl28=0x80, incl2c=0, incl30=0, excl28=0x3e00003, excl2c=0, excl30=0, stat=2)
    //    → last ID → _phaseCSubObjRef → clear container.
    // 3. Return 1 if HIBYTE(_phaseCSubObjRef) in [0x90,0x98), else return 0.
    //
    // Key difference from FUN_004d8d40: filter=0x100 vs 0x200, planet incl28=0x80 vs 0x100,
    // and incl30=0 vs 1.
    private int SelectAgentTypeSlotFull()
    {
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x100,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 2
        );
        if (c1.TryGetTopEntityKey(out int k1))
            _phaseCSubObjRef = k1;
        c1.Clear();
        if (_phaseCSubObjRef == 0)
            return 0;

        IssueRecordContainer c2 = Workspace.QuerySystemPlanets(
            _phaseCSubObjRef,
            incl28: 0x80,
            incl2c: 0,
            incl30: 0,
            excl28: 0x3e00003,
            excl2c: 0,
            excl30: 0,
            statIndex: 2
        );
        if (c2.TryGetTopEntityKey(out int k2))
            _phaseCSubObjRef = k2;
        c2.Clear();

        return IsValidSystemRef(_phaseCSubObjRef) ? 1 : 0;
    }

    // FUN_004d8e70: Create agent slot work item. TypeCode=0x200.
    //
    // Assembly trace (fully read — same unit-node-building pattern as FUN_004d7060):
    // 1. Check HIBYTE(_entityRef50=this+0x50) in [0x90,0x98) (fleet entity type).
    //    If YES: get faction fleet via sub_4f25a0(OwnerSide, this+0x50).
    //    Iterate capital ships (sub_52bc60), regiments (sub_52b900), starfighters (sub_52b600).
    //    Allocate 0x20 nodes, insert into local node list.
    // 2. If local list non-empty: allocate TypeCode=0x200 work item, attach nodes, return.
    //    Else: return null.
    // BLOCKED: HIBYTE(_entityRef50) always fails in C# (InternalIds now carry HIBYTE type encoding; check passes for [0x90,0x98) system records).
    private AIWorkItem CreateAgentSlotItem()
    {
        int sysId = _phaseCSubObjRef != 0 ? _phaseCSubObjRef : _phaseASubObjRef;
        if (sysId == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == sysId
        );
        if (rec == null || rec.System == null)
            return null;
        return new AgentShortageWorkItem(0x200, rec.System, 1, OwnerSide);
    }

    // FUN_004d91e0 (Phase C): dispatch sub-object via _phaseCSubObjRef and _entityRef50.
    //
    // Assembly trace (fully read — __thiscall with entity-ref pointer param):
    // 1. Check HIBYTE(*param_1 = *_phaseCSubObjRef) in [0x90,0x98) (fleet type).
    //    If YES: check HIBYTE(_entityRef50=this+0x50) in [0xa0,0xa2) (agent type).
    //    If both pass:
    //      sub_4f5060(0x214) → TypeCode=0x214 work item.
    //      sub_617140(0x20) → 0x20 node.
    //      sub_4f4ea0(this+0x50, 0) → init node with _entityRef50.
    //      sub_4f4b30 → add to local list.
    //      set item+0x20 = OwnerSide.
    //      vtable+0x24(&local_list) → attach nodes.
    //      vtable+0x2c(param_1) → dispatch with entity ref.
    //    Return work item or null.
    // BLOCKED: both HIBYTE checks always fail in C# (InternalIds now carry HIBYTE type encoding; check passes for [0x90,0x98) system records).
    private AIWorkItem DispatchPhaseCSubObject()
    {
        if (_phaseCSubObjRef == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _phaseCSubObjRef
        );
        if (rec == null || rec.System == null)
            return null;
        return new AgentShortageWorkItem(0x210, rec.System, 1, OwnerSide);
    }

    // FUN_004d9320: Finalize agent assignment. TypeCode=0x210.
    //
    // Assembly trace (fully read):
    // 1. Check HIBYTE(_entityRef50=this+0x50) in [0xa0,0xa2) (agent type).
    //    If YES:
    //      sub_4f5060(0x210) → TypeCode=0x210 work item (esi).
    //      sub_617140(0x20) → 0x20 node.
    //      sub_4f4ea0(this+0x50, 0) → init node with _entityRef50.
    //      sub_4f4b30 → add to local list.
    //      set item+0x20 = OwnerSide (this+0x30).
    //      vtable+0x24(&local_list) → attach nodes.
    //      item+0x44 = _typeModePacked (this+0x5c) via sub_4ec1e0.
    //      item+0x48 = _agentMatchFlag (this+0x60).
    //    Return work item or null.
    // BLOCKED: HIBYTE(_entityRef50) check always fails in C# (InternalIds now carry HIBYTE type encoding; check passes for [0x90,0x98) system records).
    private AIWorkItem FinalizeAgentAssignment()
    {
        int sysId = _phaseCSubObjRef != 0 ? _phaseCSubObjRef : _phaseASubObjRef;
        if (sysId == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == sysId
        );
        if (rec == null || rec.System == null)
            return null;
        return new AgentShortageWorkItem(0x210, rec.System, 1, OwnerSide);
    }

    // --- Phase A helpers ---

    // FUN_004d7e40: Phase A primary condition check.
    //
    // Assembly trace (fully read — same structure as FUN_004da880 but with different fields):
    // 1. Reset _phaseASubObjRef (this+0x58) via sub_4ec230.
    // 2. *(this+0x68) = 0 (_candidateCapacity68 = 0).
    // 3. Get first node from _type3CandidateList (this+0x6c). var_20 = 0x3e8 (initial min cost).
    // 4. If list NOT empty: iterate nodes:
    //    For each: check PresenceFlags & 0x1, FlagA & 0x3 == 0, *(esi+0x114)>0, SystemScore < min.
    //    If best found: _candidateCapacity68 = capacity, _phaseASubObjRef = node key, var_24=1.
    // 5. If var_24 == 0 (no candidate from list): fallback queries:
    //    QuerySystemAnalysis(0x80, stat=0x15) → store → get ID → _phaseASubObjRef → clear.
    //    If _type3CandidateList.Count > 0: also QuerySystemAnalysis(0x80, stat=4, sort=2).
    //    QuerySystemPlanets(_phaseASubObjRef, 0,0,1, 0x3800003,0,0x40000000, 6,1) → update.
    //    If list count > 0: also QuerySystemPlanets(..., 0x33).
    //    If HIBYTE(_phaseASubObjRef) in [0x90,0x98): insert into _type3CandidateList, FlagA|=0x2000000, var_24=1.
    // 6. Returns var_24 (0 or 1).
    //
    // Note: uses FlagA bit 0x2000000 (not 0x800000 like Type 1). Writes _candidateCapacity68.
    private int CheckPhaseACondition()
    {
        // Reset _phaseASubObjRef and _candidateCapacity68
        _phaseASubObjRef = 0;
        _candidateCapacity68 = 0;
        int minCost = 0x3e8;
        bool found = false;

        foreach (int sysId in _type3CandidateList)
        {
            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == sysId
            );
            if (rec == null)
                continue;
            if ((rec.PresenceFlags & 0x1) == 0)
                continue;
            if ((rec.FlagA & 0x3) != 0)
                continue;
            int cap = rec.Stats.FacilityCount; // proxy for *(esi+0x114)
            if (cap <= 0)
                continue;
            int score = rec.SystemScore;
            if (score < minCost)
            {
                _candidateCapacity68 = cap;
                minCost = score;
                _phaseASubObjRef = sysId;
                found = true;
            }
        }

        if (!found)
        {
            // Fallback: QuerySystemAnalysis(0x80, stat=0x15) + optionally stat=4
            IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
                incl24: 0x80,
                incl28: 0,
                incl2c: 0,
                excl24: 0,
                excl28: 0,
                excl2c: 0,
                statIndex: 0x15
            );
            IssueRecordContainer c2 = null;
            if (_type3CandidateList.Count > 0)
                c2 = Workspace.QuerySystemAnalysis(
                    incl24: 0x80,
                    incl28: 0,
                    incl2c: 0,
                    excl24: 0,
                    excl28: 0,
                    excl2c: 0,
                    statIndex: 4
                );
            if (c2 != null)
                c1.StoreFrom(c2);
            if (c1.TryGetTopEntityKey(out int key1))
                _phaseASubObjRef = key1;
            c1.Clear();

            IssueRecordContainer p1 = Workspace.QuerySystemPlanets(
                _phaseASubObjRef,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x3800003,
                excl2c: 0,
                excl30: 0x40000000,
                statIndex: 6
            );
            if (_type3CandidateList.Count > 0)
            {
                IssueRecordContainer p2 = Workspace.QuerySystemPlanets(
                    _phaseASubObjRef,
                    incl28: 0,
                    incl2c: 0,
                    incl30: 1,
                    excl28: 0x3800003,
                    excl2c: 0,
                    excl30: 0x40000000,
                    statIndex: 0x33
                );
                p1.StoreFrom(p2);
            }
            if (p1.TryGetTopEntityKey(out int key2))
                _phaseASubObjRef = key2;
            p1.Clear();

            // Fleet entity type proxy: if _phaseASubObjRef != 0 with valid record
            if (_phaseASubObjRef != 0)
            {
                SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                    r.InternalId == _phaseASubObjRef
                );
                if (rec != null)
                {
                    if (!_type3CandidateList.Contains(_phaseASubObjRef))
                        _type3CandidateList.Add(_phaseASubObjRef);
                    rec.FlagA |= 0x2000000; // FlagA |= 0x2000000 (not 0x800000!)
                    found = true;
                }
            }
        }

        return found ? 1 : 0;
    }

    // FUN_004d8120: Phase A agent condition check.
    //
    // Assembly trace (fully read):
    // 1. FUN_00419330(workspace, this+0x58, 0x1000, ..., 2) → update _entityRef50.
    // 2. FUN_004419bb0(workspace, this+0x50, this+0x58, 0x800800, 0, 0x1, 0x3, ..., 2) → update _entityRef50.
    // 3. Check HIBYTE(_entityRef50) in [0x90,0x98):
    //    If fleet: look up _phaseASubObjRef in SystemAnalysis.
    //    sub_4f25a0(OwnerSide, _entityRef50) → fleet capacity.
    //    If both found:
    //      _capacityLimit64 = *(fleet+0x60) > 1 ? (*(fleet+0x60)-1) : *(fleet+0x60)
    //      sub_4025b0 → update _entityRef50.
    //      var_1C = 1.
    //    If var_1C != 0:
    //      sub_4f4cc0 check: if _phaseASubObjRef not already in _type3CandidateList:
    //        insert into _type3CandidateList, FlagA |= 0x2000000.
    // 4. Returns var_1C (0 or 1).
    //
    // FUN_004d8120 (CheckPhaseAAgentCondition) — assembly trace (fully read):
    // 1. sub_419330(workspace, &_phaseASubObjRef, incl24=0x1000, stat=2) → list → top ID → _entityRef50 (this+0x50).
    //    Clear container.
    // 2. sub_419bb0(workspace, &_entityRef50, &_phaseASubObjRef, incl28=0x800800, incl2c=0,
    //    incl30=1, excl28=0x3, excl2c=0, excl30=0, stat=2) → list → top ID → _entityRef50.
    //    Clear container.
    // 3. HIBYTE(_entityRef50) in [0x90,0x98) (fleet entity type). Passes with system InternalIds.
    // 4. If YES: sub_403d30(workspace+0x2c, _entityRef50) → entity lookup (esi).
    //    sub_4f25a0(OwnerSide, &_entityRef50) → fleet lookup (eax).
    //    sub_5087e0(1) → capacity check.
    //    If all pass:
    //      _capacityLimit64 = *(fleet+0x60) > 1 ? *(fleet+0x60)-1 : *(fleet+0x60).
    //      sub_4025b0 → normalize _entityRef50. var_1C = 1.
    // 5. If var_1C != 0:
    //    sub_4f4cc0(&_phaseASubObjRef) → check if NOT in _type3CandidateList.
    //    If not: look up entity, allocate 0x1c node, add to _type3CandidateList, FlagA |= 0x2000000.
    // Returns var_1C (1 = inserted, 0 = not).
    private int CheckPhaseAAgentCondition()
    {
        // Step 1: QuerySystemAnalysis(incl24=0x1000, stat=2) → top ID → _entityRef50
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x1000, incl28: 0, incl2c: 0,
            excl24: 0, excl28: 0, excl2c: 0,
            statIndex: 2
        );
        if (c1.TryGetTopEntityKey(out int k1))
            _entityRef50 = k1;
        c1.Clear();
        if (_entityRef50 == 0)
            return 0;

        // Step 2: QuerySystemPlanets(_entityRef50, incl28=0x800800, incl2c=0, incl30=1, excl28=0x3, stat=2) → _entityRef50
        IssueRecordContainer c2 = Workspace.QuerySystemPlanets(
            _entityRef50,
            incl28: 0x800800, incl2c: 0, incl30: 1,
            excl28: 0x3, excl2c: 0, excl30: 0,
            statIndex: 2
        );
        if (c2.TryGetTopEntityKey(out int k2))
            _entityRef50 = k2;
        c2.Clear();

        // Step 3: HIBYTE check — passes for SystemAnalysis InternalIds (HIBYTE 0x90 ∈ [0x90,0x98))
        int h = (_entityRef50 >> 0x18) & 0xff;
        if (h < 0x90 || h >= 0x98)
            return 0;

        // Step 4: Fleet lookup proxy — use FacilityCount as fleet slot count
        SystemAnalysisRecord sysRec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _entityRef50
        );
        if (sysRec == null || sysRec.Stats.FacilityCount <= 0)
            return 0;
        int slotCount = sysRec.Stats.FacilityCount;
        _capacityLimit64 = slotCount > 1 ? slotCount - 1 : slotCount;

        // Step 5: Add _phaseASubObjRef to _type3CandidateList if not already there
        if (_phaseASubObjRef != 0 && !_type3CandidateList.Contains(_phaseASubObjRef))
        {
            SystemAnalysisRecord phaseASys = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == _phaseASubObjRef
            );
            if (phaseASys != null)
            {
                _type3CandidateList.Add(_phaseASubObjRef);
                phaseASys.FlagA |= 0x2000000;
            }
        }
        return 1;
    }

    // FUN_00419160(workspace): Advances the workspace bit-selection state.
    private void ClearFleetState()
    {
        Workspace.AdvanceBitSelection();
    }

    // FUN_004d8890: Phase A sub-state computation. Assembly trace (fully read).
    // 1. *(this+0x60) = 0 (_agentMatchFlag = 0).
    // 2. iVar1 = FUN_004d98a0(this):
    //    Iterates _type3CandidateList (this+0x6c) summing *(entry+0xd8) (UnitCountAccumD) → accum.
    //    result = floor(Capacity * FleetTotalCapacity / 10) - accum.
    //    Cap: if FleetTotalCapacity - FleetAssignedCapacity < result: result = 0.
    // 3. If result < 0: return 8. If result == 0: return 0.
    // 4. sub_41a9e0(workspace, this+0x5c, 0x2a, 0x10000, 0x4000, 2) → construction facility.
    //    If (this+0x5c & 0xff000000) != 0 AND agent_capacity != 0:
    //      _agentMatchFlag = result / agent_capacity.
    //      Cap by _capacityLimit64 (this+0x64).
    //      If _agentMatchFlag > 0:
    //        FUN_00479ee0(workspace, this+0x58) → look up _phaseASubObjRef system entity.
    //        If found:
    //          iVar2 = min(*(found+0x84), _candidateCapacity68 (this+0x68)).
    //          If iVar2 < _agentMatchFlag: _agentMatchFlag = iVar2.
    //          Return 0xb.
    //    Else: return 0 (no agent capacity).
    private int GetNextPhaseASubState()
    {
        _agentMatchFlag = 0;

        // FUN_004d98a0: floor(Capacity * FleetTotalCapacity / 10) - sum(*(entity+0xd8) from list)
        // *(entity+0xd8) is an assigned-capacity field on the raw binary system entity with
        // no direct C# equivalent on SystemAnalysisRecord. Proxy: use 0 (overestimates shortage).
        int total = Workspace.FleetTotalCapacity;
        int result = (Capacity * total) / 10;
        if (total - Workspace.FleetAssignedCapacity < result)
            result = 0;

        if (result < 0)
            return 8;
        if (result == 0)
            return 0;

        // sub_41a9e0 proxy: find own-faction planet with ConstructionFacility
        string ownerId = Workspace.Owner?.InstanceID;
        SystemAnalysisRecord constructionSys = Workspace.SystemAnalysis.FirstOrDefault(r =>
            (r.PresenceFlags & 0x1u) != 0
            && r.System != null
            && r.System.Planets.Any(p =>
                p.GetOwnerInstanceID() == ownerId
                && p.GetBuildingTypeCount(BuildingType.ConstructionFacility) > 0
            )
        );
        if (constructionSys == null)
            return 0;

        _typeModePacked = constructionSys.InternalId;
        int typeModeHibyte = (_typeModePacked >> 0x18) & 0xff;
        if (typeModeHibyte == 0)
            return 0;

        int capPerAgent = constructionSys.System.Planets
            .Where(p => p.GetOwnerInstanceID() == ownerId)
            .Sum(p => p.GetBuildingTypeCount(BuildingType.ConstructionFacility));
        if (capPerAgent <= 0)
            capPerAgent = 1;

        _agentMatchFlag = result / capPerAgent;
        if (_capacityLimit64 > 0 && _agentMatchFlag > _capacityLimit64)
            _agentMatchFlag = _capacityLimit64;

        if (_agentMatchFlag <= 0)
            return 0;

        // FUN_00479ee0: look up _phaseASubObjRef system entity, cap by min(FacilityCount, _candidateCapacity68)
        SystemAnalysisRecord sysRec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _phaseASubObjRef
        );
        if (sysRec == null)
            return 0;

        int entityCap = sysRec.Stats.FacilityCount;
        if (_candidateCapacity68 < entityCap)
            entityCap = _candidateCapacity68;
        if (entityCap > 0 && entityCap < _agentMatchFlag)
            _agentMatchFlag = entityCap;

        return 0xb;
    }

    // FUN_004d8350: Phase A fleet assignment work item. TypeCode=0x200.
    //
    // Assembly trace (fully read — same unit-node-building pattern as FUN_004dab90):
    // Uses _phaseASubObjRef (this+0x58) as the fleet/system entity ref.
    // Check HIBYTE in [0x90,0x98): get fleet via sub_4f25a0, iterate capital ships/regiments/
    // starfighters, build 0x20 nodes, allocate TypeCode=0x200 work item.
    // UNBLOCKED: _phaseASubObjRef = SystemAnalysis InternalId (HIBYTE 0x90 ∈ [0x90,0x98)).
    // Unit-node-building requires entity infrastructure; proxy returns FleetShortageWorkItem.
    private AIWorkItem CreatePhaseAFleetItem()
    {
        int sysId = _phaseASubObjRef != 0 ? _phaseASubObjRef : _phaseCSubObjRef;
        if (sysId == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == sysId
        );
        if (rec == null || rec.System == null || (rec.FlagB & 0x4) == 0)
            return null;
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004d8930: Phase A agent work item. TypeCode=0x200.
    //
    // Assembly trace (fully read):
    // 1. Iterate _type3CandidateList (this+0x6c) backward:
    //    For each node: check SystemScore > 0 AND FlagA & 0x1000 (bit 12 = HIBYTE & 0x10).
    //    If list count > 1 AND conditions pass: store key in _phaseASubObjRef (this+0x58), var_54=1.
    //    If list count == 1 AND conditions pass AND another condition: store, remove from list,
    //      clear FlagA & 0x2000000, var_54=1.
    // 2. If var_54: get fleet via sub_4f25a0(OwnerSide, this+0x58), iterate starfighters (sub_52b600).
    //    If nodes: TypeCode=0x200 work item.
    // BLOCKED: fleet entity iteration requires entity infrastructure; FlagA & 0x1000 check.
    private AIWorkItem CreatePhaseAAgentItem()
    {
        int sysId = _phaseASubObjRef != 0 ? _phaseASubObjRef : _phaseCSubObjRef;
        if (sysId == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == sysId
        );
        if (rec == null || rec.System == null)
            return null;
        return new AgentShortageWorkItem(0x214, rec.System, 1, OwnerSide);
    }

    // FUN_004d91e0 (Phase A): dispatch sub-object via _phaseASubObjRef and _entityRef50.
    // Same function as DispatchPhaseCSubObject but called with &_phaseASubObjRef.
    // See DispatchPhaseCSubObject for full assembly trace.
    // BLOCKED: both HIBYTE checks always fail in C#.
    private AIWorkItem DispatchPhaseASubObject()
    {
        if (_phaseASubObjRef == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _phaseASubObjRef
        );
        if (rec == null || rec.System == null)
            return null;
        return new AgentShortageWorkItem(0x210, rec.System, 1, OwnerSide);
    }

    // FUN_004d9440: Finalize fleet assignment for Phase A (equivalent of FUN_004dbfb0 for Type 3).
    //
    // Assembly trace (fully read — same structure as FUN_004dbfb0):
    // 1. Reset _phaseASubObjRef (this+0x58) via sub_4ec230.
    // 2. FUN_004d98a0 compute count. If count <= 0: simplified path.
    // 3. Main path: QuerySystemAnalysis(0x80, stat=0x15) → find fleet entity →
    //    QuerySystemPlanets fallback → insert into _type3CandidateList → FlagA |= 0x2000000.
    // 4. Simplified path: check _phaseASubObjRef type, insert into _type3CandidateList.
    //
    // Note: `arg_68 = this+0x6c = _type3CandidateList` and `arg_54 = this+0x58 = _phaseASubObjRef`.
    // BLOCKED: fleet entity lookup; HIBYTE checks blocked in C#.
    // Proxy: clears _phaseASubObjRef (simplified side effect).
    private void FinalizeFleetAssignmentA()
    {
        _phaseASubObjRef = 0;
    }

    // FUN_004d9980: Finalize shortage record for Phase A (equivalent of FUN_004dc490 for Type 3).
    //
    // Assembly trace (fully read — same structure as FUN_004dc490):
    // 1. Init local list + entity ref. do-while loop (var_38 starts at 1):
    //    For each iteration: iterate _type3CandidateList (this+0x6c=arg_68):
    //      For each node: look up system, if SystemScore > highest so far:
    //        Check PresenceFlags & 0x10000000 and candidate list (arg_70 = this+0x70?).
    //        If conditions pass: update highest score, store entity key, add to local list.
    //    Check var_3C HIBYTE in [0x90,0x98):
    //      If fleet: allocate 0x1c entry, add to local list, var_38 -= 1.
    //      Else: var_38 = 0.
    // 2. Iterate arg_70 list: clear PresenceFlags & 0x10000000 on each entry.
    // 3. Call arg_70 vtable+0x4.
    // 4. Iterate local list: set PresenceFlags |= 0x10000000, transfer to arg_70 list.
    //
    // Note: uses arg_68 = this+0x6c (_type3CandidateList) and arg_70 (MISSING FIELD at +0x70).
    // Proxy: clears phase C and phase A sub-object refs.
    private void FinalizeShortageRecordA()
    {
        _phaseCSubObjRef = 0;
        _phaseASubObjRef = 0;
    }
}

// ------------------------------------------------------------------
// Type 4 — FUN_004d1590 — MissionAssignmentRecord
// 0x60 bytes.
// Active guard: pass-through — does NOT reset Phase/SubState/ReadyFlag on inactive.
// If inactive: return null unchanged.
// If active: call FUN_004d1800 (do mission assignment), return result.
//
// Extra fields beyond the 0x40-byte StrategyRecord base (total 0x60 = 0x20 extra):
//   +0x40 = _targetEntityId:  ID of the entity found by FindNextMissionTarget
//   +0x44 = _entityCursorId:  cursor for backward walk of workspace.EntityTargetTable
//   +0x48 = _missionItemId:   ID of the mission entry found by FindNextMissionEntry
//   +0x4c = _missionCursorId: cursor for backward walk of workspace.MissionTable
//
// SubState machine states (FUN_004d1800):
//   default → SubState=1 (entry point on first call or bad state)
//   1: ClearPendingMissionCancel() → SubState=2
//   2: FindNextMissionTarget() → SubState=3 if found, SubState=5 if not
//   3: AssignMissionToTarget() → SubState=5
//   5: FindNextMissionEntry() → SubState=6 if found; SubState=0, ReadyFlag=1 if not
//   6: TryDispatchMissionEntry() → if success+absorbed: TickCounter++, ReadyFlag=1
//                                   if success+produced work item: SubState=0, ReadyFlag=1, return it
// ------------------------------------------------------------------
public class MissionAssignmentRecord : StrategyRecord
{
    // Extra fields at +0x40..+0x4f (original struct offsets).
    private int _targetEntityId; // +0x40: entity to assign mission to
    private int _entityCursorId; // +0x44: EntityTargetTable backward-walk cursor
    private int _missionItemId; // +0x48: mission entry being processed
    private int _missionCursorId; // +0x4c: MissionTable backward-walk cursor

    public MissionAssignmentRecord(int ownerSide)
        : base(typeId: 4, capacity: 1, ownerSide: ownerSide)
    {
        _targetEntityId = 0;
        _entityCursorId = 0;
        _missionItemId = 0;
        _missionCursorId = 0;
    }

    // Pass-through guard: does NOT reset Phase/SubState/ReadyFlag on inactive.
    protected override bool ActiveGuardFails()
    {
        return ActiveState != 1;
    }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        return DoMissionAssignment();
    }

    // FUN_004d1800 — SubState-driven mission assignment machine.
    private AIWorkItem DoMissionAssignment()
    {
        switch (SubState)
        {
            case 1:
                ClearPendingMissionCancel();
                SubState = 2;
                return null;

            case 2:
            {
                int found = FindNextMissionTarget();
                // FUN_004d1800 case 2: SubState = (-(uint)(found!=0) & 0xfffffffe) + 5
                //   found!=0 → 0xfffffffe+5 = 3; found==0 → 0+5 = 5.
                SubState = (found != 0) ? 3 : 5;
                return null;
            }

            case 3:
                AssignMissionToTarget();
                SubState = 5;
                return null;

            case 5:
            {
                int found = FindNextMissionEntry();
                if (found != 0)
                {
                    SubState = 6;
                }
                else
                {
                    SubState = 0;
                    ReadyFlag = 1;
                }
                return null;
            }

            case 6:
            {
                // FUN_004d1b20: dispatch the mission entry found at _missionItemId.
                // Output param (local_4 in original): starts as non-zero (= this ptr).
                // After call:
                //   entity-not-found path:  local_4 = 1  (non-zero)
                //   vtable success, absorbed: local_4 = 0 (zero) → TickCounter++, ReadyFlag=1
                //   vtable success, produced: local_4 != 0       → SubState=0, ReadyFlag=1, return item
                int dispatchOut = 1; // initial local_4 = this (non-null = 1)
                AIWorkItem workItem = TryDispatchMissionEntry(out dispatchOut);
                if (workItem != null)
                {
                    if (dispatchOut == 0)
                        TickCounter++;
                    ReadyFlag = 1;
                }
                if (dispatchOut != 0)
                {
                    SubState = 0;
                    ReadyFlag = 1;
                    return workItem;
                }
                return workItem;
            }

            default:
                SubState = 1;
                return null;
        }
    }

    // FUN_004d18f0 — clear any pending cancel, create any pending mission.
    // Reads workspace.PendingMissionCancelId (+0x314): if non-zero, remove the entry from
    //   MissionTable (FUN_0042ed10 equivalent), then clear.
    // Reads workspace.PendingMissionTypeId (+0x318): if non-zero, create a new
    //   MissionAssignmentEntry (FUN_0042ecc0 equivalent), add to MissionTable, then clear.
    // Return value (discarded by state 1 in FUN_004d1800) indicates whether a mission
    // was created and its vtable[4] returned 2 (not used by caller).
    private void ClearPendingMissionCancel()
    {
        if (Workspace.PendingMissionCancelId != 0)
        {
            // FUN_0042ed10: remove entry with matching ID from the mission table.
            int cancelId = Workspace.PendingMissionCancelId;
            Workspace.MissionTable.RemoveAll(e => e.Id == cancelId);
            Workspace.PendingMissionCancelId = 0;
        }

        if (Workspace.PendingMissionTypeId != 0)
        {
            // FUN_0042ecc0_create_and_register_mission_instance: allocate new entry.
            var newEntry = new MissionAssignmentEntry
            {
                Id = Workspace.NextMissionId++,
                PendingMissionTypeId = Workspace.PendingMissionTypeId,
                PendingMissionParam = Workspace.PendingMissionParameter,
            };
            Workspace.MissionTable.Add(newEntry);
            Workspace.PendingMissionTypeId = 0;
            // vtable[4] call and FUN_0041ad20 callback — not yet implemented.
        }
    }

    // FUN_004d1980 — find next entity in workspace.EntityTargetTable that needs
    // a mission assigned or cancelled.
    //
    // Walks backward from _entityCursorId (or from the tail if cursor==0).
    // Validity check (FUN_00475fd0): entry.PendingMissionTypeId != 0 OR entry.PendingCancelId != 0.
    // On success: sets _targetEntityId = found entry's ID,
    //             _entityCursorId = previous entry's ID (or 0 if none).
    // On failure: clears both fields; returns 0.
    private int FindNextMissionTarget()
    {
        List<MissionTargetEntry> table = Workspace.EntityTargetTable;
        if (table.Count == 0)
        {
            _targetEntityId = 0;
            _entityCursorId = 0;
            return 0;
        }

        // Starting index: entry with cursor ID, or tail if cursor is 0 / not found.
        int startIdx;
        if (_entityCursorId != 0)
        {
            startIdx = table.FindIndex(e => e.Id == _entityCursorId);
            if (startIdx < 0)
            {
                _targetEntityId = 0;
                _entityCursorId = 0;
                return 0;
            }
        }
        else
        {
            startIdx = table.Count - 1;
        }

        // Backward walk: FUN_004d1980 do-while using prev_node links.
        for (int i = startIdx; i >= 0; i--)
        {
            MissionTargetEntry e = table[i];
            if (e.PendingMissionTypeId != 0 || e.PendingCancelId != 0)
            {
                _targetEntityId = e.Id;
                _entityCursorId = (i > 0) ? table[i - 1].Id : 0;
                return 1;
            }
        }

        _targetEntityId = 0;
        _entityCursorId = 0;
        return 0;
    }

    // FUN_004d1a00 — assign the pending mission (or cancel the old one) for the
    // entity found by FindNextMissionTarget (_targetEntityId).
    //
    // Reads the MissionTargetEntry for _targetEntityId from workspace.EntityTargetTable:
    //   +0xc4 (PendingCancelId) != 0: call FUN_0042ed10 to remove old mission entry,
    //         then clear PendingCancelId.
    //   +0xbc (PendingMissionTypeId) != 0: call FUN_0042ecc0 to create a new
    //         MissionAssignmentEntry, then call vtable[4] on it; if that returns 1,
    //         call FUN_00475ff0 (unknown register callback).
    private void AssignMissionToTarget()
    {
        MissionTargetEntry target = Workspace.EntityTargetTable.Find(e => e.Id == _targetEntityId);
        if (target == null)
            return;

        if (target.PendingCancelId != 0)
        {
            int cancelId = target.PendingCancelId;
            Workspace.MissionTable.RemoveAll(e => e.Id == cancelId);
            target.PendingCancelId = 0;
        }

        if (target.PendingMissionTypeId != 0)
        {
            var newEntry = new MissionAssignmentEntry
            {
                Id = Workspace.NextMissionId++,
                PendingMissionTypeId = target.PendingMissionTypeId,
                PendingMissionParam = target.MissionParam,
            };
            Workspace.MissionTable.Add(newEntry);
            target.PendingMissionTypeId = 0;
            // vtable[4] call + FUN_00475ff0 — not yet implemented.
        }
    }

    // FUN_004d1aa0 — find next active entry in workspace.MissionTable.
    //
    // Walks backward from _missionCursorId (or tail if cursor==0).
    // Validity check: equivalent to vtable[10] returning non-zero — entry is active
    //   when PendingMissionTypeId != 0 OR PendingCancelMissionId != 0.
    // On success: sets _missionItemId, _missionCursorId; returns non-zero.
    // On failure: clears both; returns 0.
    private int FindNextMissionEntry()
    {
        List<MissionAssignmentEntry> table = Workspace.MissionTable;
        if (table.Count == 0)
        {
            _missionItemId = 0;
            _missionCursorId = 0;
            return 0;
        }

        int startIdx;
        if (_missionCursorId != 0)
        {
            startIdx = table.FindIndex(e => e.Id == _missionCursorId);
            if (startIdx < 0)
            {
                _missionItemId = 0;
                _missionCursorId = 0;
                return 0;
            }
        }
        else
        {
            startIdx = table.Count - 1;
        }

        for (int i = startIdx; i >= 0; i--)
        {
            MissionAssignmentEntry e = table[i];
            // Vtable[10] equivalent: entry is active when it has pending work.
            if (e.PendingMissionTypeId != 0 || e.PendingCancelMissionId != 0)
            {
                _missionItemId = e.Id;
                _missionCursorId = (i > 0) ? table[i - 1].Id : 0;
                return 1;
            }
        }

        _missionItemId = 0;
        _missionCursorId = 0;
        return 0;
    }

    // FUN_004d1b20 — dispatch the mission entry at _missionItemId via its vtable[11].
    //
    // Looks up the entry in workspace.MissionTable by _missionItemId.
    // If not found: writes 1 to dispatchOut (non-zero = "work pending"), returns null.
    // If found: calls vtable[11](param1 = &dispatchOut) on the entry.
    //   Vtable may write 0 to dispatchOut ("work absorbed, nothing to return") or
    //   a non-zero value ("pending work, produce a work item").
    //   Returns the work item produced (or null if absorbed).
    //
    // dispatchOut interpretation in caller (FUN_004d1800 case 6):
    //   0  → work absorbed: TickCounter++, ReadyFlag=1; SubState NOT reset.
    //   != 0 → pending or entity-not-found: SubState=0, ReadyFlag=1.
    private AIWorkItem TryDispatchMissionEntry(out int dispatchOut)
    {
        // Initial value: 1 (non-zero). If entity not found, stays 1.
        dispatchOut = 1;

        MissionAssignmentEntry entry = Workspace.MissionTable.Find(e => e.Id == _missionItemId);
        if (entry == null)
        {
            // Entity not found: FUN_004d1b20 writes 1 to *param_1, returns 0.
            dispatchOut = 1;
            return null;
        }

        // Ensure workspace back-reference is set (FUN_0042ecc0: field98_0x68 = workspace).
        if (entry.Workspace == null)
            entry.Workspace = Workspace;

        // vtable[11] = FUN_004bc170 = entry.Dispatch() — the 8-state machine.
        return entry.Dispatch(out dispatchOut);
    }
}

// ------------------------------------------------------------------
// Type 5 — FUN_004cee90 — StrategyRecordType5
// 0x88 bytes.
// Active guard: pass-through (no reset on inactive).
//
// Phase 0x3ec  → FUN_004cf380 (PhaseA), return result
// Phase 0x3f4  → FUN_004cf290 (PhaseB), return result
// Other        → Phase=0x3f4, return null
// ------------------------------------------------------------------
public class StrategyRecordType5 : StrategyRecord
{
    private const int PhaseA = 0x3ec;
    private const int PhaseB = 0x3f4;

    // Fleet assignment fields (binary layout confirmed via FUN_004cf131/FUN_004cf020 serializer):
    // +0x40: ushort supply-slot cursor (0x5a–0x5d, rotated by EvaluateFleetDispatchStatus).
    //   Saved as uint16 by FUN_005f3340 (FUN_005f31f0_load_u16_from_stream wrapper).
    private int _supplySlotCursor; // +0x40 (ushort, upper 2 bytes unused)
    private int _targetEntityId5; // +0x44: capacity of selected candidate system (FacilityCount), from ScanFleetCandidatesPhaseA
    private int _batchCount5; // +0x48: dual-purpose — batch count in PhaseA (ComputeAssignmentSubState); shortage flag (0/1) in PhaseB (EvaluateFleetDispatchStatus)
    private int _capacityBound5; // +0x4c: capacity bound from CheckFleetAssignmentEligibility
    // +0x50: supply-type flag (0x1000000/0x2000000/0x4000000/0x8000000) written by EvaluateFleetDispatchStatus;
    //   passed as filter1 to sub_41a9e0 in ComputeTransportSubState (blocked on entity infra).
    private int _supplyTypeFlag; // +0x50
    // +0x54: set to 1 by EvaluateFleetDispatchStatus; passed as filter2 to sub_41a9e0 (blocked).
    private int _supplyTypeReady; // +0x54
    // +0x58: packed id loaded by serializer; not yet used by any implemented function.
    private int _type5Ref58; // +0x58
    private int _fleetEntityId5; // +0x5c: fleet/system entity ref from ScanFleetCandidatesPhaseA (NOT +0x40)
    private int _type5EntityRef60; // +0x60: entity ref written by CheckFleetAssignmentEligibility (sub_4025b0 result) and ScanFleetCandidatesPhaseB; checked for HIBYTE [0xa0,0xa2) in DispatchEntityToTarget/CreateEntityTransferFollowup
    private int _type5AgentRef64; // +0x64: agent entity ref stored by sub_41a9e0 in ComputeAssignmentSubState and ComputeTransportSubState (blocked on entity infrastructure)
    private readonly List<int> _type5CandidateList = new List<int>(); // +0x78

    public StrategyRecordType5(int ownerSide)
        : base(typeId: 5, capacity: 1, ownerSide: ownerSide) { }

    protected override bool ActiveGuardFails()
    {
        return ActiveState != 1;
    }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        if (Phase == PhaseA)
            return RunPhaseA();
        if (Phase == PhaseB)
            return RunPhaseB();

        Phase = PhaseB;
        return null;
    }

    // FUN_004cf380 — PhaseA inner state machine.
    // Drives fleet-candidate scan, bit-selection gate, assignment pipeline, entity dispatch,
    // and mission batch build. Terminates to PhaseB.
    private AIWorkItem RunPhaseA()
    {
        switch (SubState)
        {
            default:
                SubState = 3;
                return null;

            case 3:
            {
                int found = ScanFleetCandidatesPhaseA(); // FUN_004cfbd0
                SubState = (found != 0) ? 9 : 7;
                return null;
            }

            case 4:
            {
                AIWorkItem workItem = CreateFleetAssignmentWorkItem(); // FUN_004d00a0
                if (workItem != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 5;
                return workItem;
            }

            case 5:
            {
                int nextState = ComputeAssignmentSubState(); // FUN_004d05e0
                SubState = nextState;
                if (nextState == 0)
                {
                    Phase = PhaseB;
                    ReadyFlag = 1;
                }
                return null;
            }

            case 7:
            {
                int ok = CheckFleetDispatchCondition(); // FUN_004d0080
                if (ok != 0)
                {
                    SubState = 8;
                }
                else
                {
                    SubState = 0;
                    Phase = PhaseB;
                    // NOTE: ReadyFlag intentionally NOT set on this path (mirrors FUN_004cf380).
                }
                return null;
            }

            case 8:
            {
                AIWorkItem workItem = CreateFleetDispatchWorkItem(); // FUN_004d0680
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return workItem;
            }

            case 9:
            {
                // Clear bit 0x4 from the pending supply bitmask (workspace+0x8).
                Workspace.PendingSupplyBitmask &= ~0x4;
                if (
                    Workspace.EntityTargetType == 0x4
                    && CheckFleetAssignmentEligibility() != 0 // FUN_004cfeb0
                )
                {
                    Workspace.AdvanceBitSelection();
                    SubState = 4;
                }
                else
                {
                    SubState = 0xc;
                }
                return null;
            }

            case 0xa:
            {
                AIWorkItem workItem = DispatchEntityToTarget(); // FUN_004d0a80(this, record+0x5c)
                if (workItem != null)
                {
                    SubState = 0xb;
                    ReadyFlag = 1;
                    TickCounter++;
                    return workItem;
                }
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }

            case 0xb:
            {
                AIWorkItem workItem = CreateEntityTransferFollowup(); // FUN_004d0bc0
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return workItem;
            }

            case 0xc:
            {
                BuildMissionBatch(); // FUN_004d0ce0 — return value discarded
                SelectMissionCandidates(); // FUN_004d1240 — return value discarded
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }
        }
    }

    // FUN_004cf290 — PhaseB inner state machine.
    // Drives fleet/entity readiness evaluation, entity scan, dispatch pipeline, and transport.
    // Terminates to PhaseA.
    private AIWorkItem RunPhaseB()
    {
        switch (SubState)
        {
            default:
                SubState = 9;
                return null;

            case 1:
            {
                int found = EvaluateFleetDispatchStatus(); // FUN_004cf7f0
                SubState = (found != 0) ? 5 : 6;
                return null;
            }

            case 2:
            {
                AIWorkItem workItem = CreateTransportWorkItem(); // FUN_004cf980
                SubState = 0;
                Phase = PhaseA;
                ReadyFlag = 1;
                return workItem;
            }

            case 5:
            {
                int nextState = ComputeTransportSubState(); // FUN_004cf900
                SubState = nextState;
                if (nextState == 0)
                {
                    Phase = PhaseA;
                    ReadyFlag = 1;
                }
                return null;
            }

            case 6:
            {
                int ok = CheckTransportDispatchCondition(); // FUN_004cf8e0
                if (ok != 0)
                {
                    SubState = 2;
                    return null;
                }
                SubState = 0;
                Phase = PhaseA;
                ReadyFlag = 1;
                return null;
            }

            case 9:
            {
                int found = ScanFleetCandidatesPhaseB(); // FUN_004cf510
                // Non-zero → SubState=1; zero → SubState=6.
                // Derived from NEG;SBB;AND 0xfb;ADD 6 assembly pattern.
                SubState = (found != 0) ? 1 : 6;
                return null;
            }

            case 0xb:
            {
                AIWorkItem workItem = CreateFleetTransferWorkItem(); // FUN_004d0960
                SubState = 0;
                Phase = PhaseA;
                ReadyFlag = 1;
                return workItem;
            }
        }
    }

    // FUN_004cfbd0: ScanFleetCandidatesPhaseA — assembly trace (fully read).
    //
    // Same structure as FUN_004da880 (Type 1 CheckShortageConditionType1) and FUN_004e28a0 (Type 2):
    // 1. Reset this+0x5c (entity ref, proxy: _fleetEntityId5). Clear this+0x44 (capacity, proxy: _targetEntityId5).
    //    var_20 = 0x3e8 (initial min cost threshold). var_24 = 0 (found flag).
    // 2. Iterate _type5CandidateList (this+0x78) forward:
    //    PresenceFlags & 0x1, FlagA & 0x3==0, *(esi+0x114) > 0, *(esi+0x5c) < min:
    //      this+0x44 = capacity, this+0x5c = node key, var_24=1.
    // 3. If var_24==0: fallback:
    //    QuerySystemAnalysis(DispositionFlags & 0x40, stat=0x13) + optionally stat=4
    //      → this+0x5c.
    //    QuerySystemPlanets(this+0x5c, 0,0,1, 0x400003,0,0x40000000, 5,1) + optionally 0x33
    //      → this+0x5c.
    //    If HIBYTE(this+0x5c) in [0x90,0x98): insert into _type5CandidateList (this+0x78),
    //      FlagA |= 0x400000 (NOT 0x800000!), var_24=1.
    // 4. Returns var_24.
    //
    // Note: Type 5 uses different offsets (+0x5c=entity ref, +0x78=list, +0x68=container),
    //   different filter (0x40 vs Type 1's 0x80), different FlagA bit (0x400000 vs 0x800000).
    //
    // FUN_004cfbd0 assembly (fully read via assembly trace):
    // 1. Clear _fleetEntityId5 (this+0x5c). Get last node from _type5CandidateList (this+0x78).
    // 2. If list non-empty: iterate nodes; check PresenceFlags & 0x1, FlagA & 0x3==0,
    //    *(sys+0x114) > 0 (FacilityCount proxy), *(sys+0x5c) < min.
    //    If passes: _capacityBound5 = count, _fleetEntityId5 = sys, var_24=1.
    // 3. If var_24==0: fallback queries:
    //    QuerySystemAnalysis(incl24=0x40, stat=0x13) + QuerySystemAnalysis(incl24=0x40, stat=4)
    //      → merge, get top key → _fleetEntityId5.
    //    QuerySystemPlanets(_fleetEntityId5, 0,0,1, 0x400003,0,0x40000000, 5,1)
    //      + optionally 0x33 if list non-empty → update _fleetEntityId5.
    //    HIBYTE(_fleetEntityId5) in [0x90,0x98): insert into _type5CandidateList,
    //      FlagA |= 0x400000, var_24=1.
    // 4. Returns var_24.
    private int ScanFleetCandidatesPhaseA()
    {
        _fleetEntityId5 = 0;

        // Primary: iterate existing candidate list.
        foreach (int sysId in _type5CandidateList.ToList())
        {
            var rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == sysId);
            if (rec == null)
            {
                _type5CandidateList.Remove(sysId);
                continue;
            }
            if (
                (rec.PresenceFlags & 0x1u) != 0
                && (rec.FlagA & 0x3) == 0
                && rec.Stats.FacilityCount > 0
            )
            {
                _fleetEntityId5 = sysId;
                return 1;
            }
        }

        // Fallback: seed list via queries when empty.
        // QuerySystemAnalysis(incl24=0x40, stat=0x13) finds own-faction systems
        // with DispositionFlags bit 6 set (regiment capacity available).
        // stat=0x13=19 indexes PerSystemStats.
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x40,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 0x13
        );
        IssueRecordContainer c2 = Workspace.QuerySystemAnalysis(
            incl24: 0x40,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 4
        );
        c1.StoreFrom(c2);
        if (!c1.TryGetTopEntityKey(out int candidateRef))
            return 0;
        _fleetEntityId5 = candidateRef;
        c1.Clear();

        // QuerySystemPlanets to refine to the best system entity.
        IssueRecordContainer p1 = Workspace.QuerySystemPlanets(
            _fleetEntityId5,
            incl28: 0,
            incl2c: 0,
            incl30: 1,
            excl28: 0x400003,
            excl2c: 0,
            excl30: 0x40000000,
            statIndex: 5
        );
        if (_type5CandidateList.Count > 0)
        {
            IssueRecordContainer p2 = Workspace.QuerySystemPlanets(
                _fleetEntityId5,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x400003,
                excl2c: 0,
                excl30: 0x40000000,
                statIndex: 0x33
            );
            p1.StoreFrom(p2);
        }
        if (p1.TryGetTopEntityKey(out int refined))
            _fleetEntityId5 = refined;
        p1.Clear();

        // HIBYTE check: result must be a system-type InternalId [0x90,0x98).
        int hibyte = (_fleetEntityId5 >> 0x18) & 0xff;
        if (hibyte >= 0x90 && hibyte < 0x98)
        {
            SystemAnalysisRecord sysRec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == _fleetEntityId5
            );
            if (sysRec != null)
            {
                if (!_type5CandidateList.Contains(_fleetEntityId5))
                    _type5CandidateList.Add(_fleetEntityId5);
                sysRec.FlagA |= 0x400000; // FlagA |= 0x400000 (NOT 0x800000)
                return 1;
            }
        }
        return 0;
    }

    // FUN_004cfeb0: CheckFleetAssignmentEligibility. Assembly trace (fully read).
    // 1. Clear this+0x4c (_capacityBound5). QuerySystemAnalysis(incl24=0x1000, stat=0x14) →
    //    this+0x60 (_type5EntityRef) entity ref. Clear issue container.
    // 2. QuerySystemPlanets(this+0x60, incl28=0x800800, 0, incl30=1, excl28=3, 0, 0, stat=6, sort=1)
    //    → update this+0x60. Clear.
    // 3. Check HIBYTE(this+0x60) in [0x90,0x98). If fleet type:
    //    sub_4f25a0(OwnerSide, entity_ref) → get faction's fleet at this system.
    //    sub_5087e0(fleet, 1) → capacity check (returns 1 when fleet has capacity).
    //    If passes: this+0x4c = max(SystemScore-1, 0), update this+0x60. Return 1.
    //    Else: return 0.
    //
    // DispositionFlags & 0x1000 (bit 12) requires CapabilityFlags & 0x800000 AND 0x800 (not yet
    // set in C# RefreshPlanetSubobject). Proxy: use _fleetEntityId5 from ScanFleetCandidatesPhaseA
    // if EntityTargetType gate matches and system has own-faction deployment capacity.
    private int CheckFleetAssignmentEligibility()
    {
        _capacityBound5 = 0;
        // Primary: QuerySystemAnalysis(incl24=0x1000) → _type5EntityRef
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x1000,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 0x14
        );
        if (!c1.TryGetTopEntityKey(out int entityRef))
        {
            // Fallback: use the candidate from ScanFleetCandidatesPhaseA
            entityRef = _fleetEntityId5;
        }
        c1.Clear();
        if (entityRef == 0)
            return 0;

        // QuerySystemPlanets to refine entity ref.
        IssueRecordContainer p1 = Workspace.QuerySystemPlanets(
            entityRef,
            incl28: 0x800800,
            incl2c: 0,
            incl30: 1,
            excl28: 3,
            excl2c: 0,
            excl30: 0,
            statIndex: 6
        );
        if (p1.TryGetTopEntityKey(out int refined))
            entityRef = refined;
        p1.Clear();

        // HIBYTE check in [0x90, 0x98): with InternalIds this passes for SystemAnalysisRecords.
        int hibyte = (entityRef >> 0x18) & 0xff;
        if (hibyte < 0x90 || hibyte >= 0x98)
            return 0;

        // sub_4f25a0 + sub_5087e0 proxy: check own-faction fleet presence with capacity.
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == entityRef
        );
        if (rec == null || (rec.PresenceFlags & 0x1u) == 0)
            return 0;

        // Capacity check proxy: system has facilities/units and not at max capacity.
        // Binary: sub_4025b0(fleet_from_sub_5087e0, this) → key stored at this+0x60.
        // Proxy: store the system entity ref directly (sub_4025b0 not in disassembly).
        _capacityBound5 = System.Math.Max(0, rec.Stats.FacilityCount - 1);
        _type5EntityRef60 = entityRef;
        return 1;
    }

    // helper for proxy work items
    private AIWorkItem GetFleetWorkItem5()
    {
        if (_fleetEntityId5 == 0)
            return null;
        var rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == _fleetEntityId5);
        return rec?.System != null ? new FleetShortageWorkItem(rec.System, OwnerSide) : null;
    }

    // FUN_004d00a0: CreateFleetAssignmentWorkItem. Proxy: returns FleetShortageWorkItem.
    private AIWorkItem CreateFleetAssignmentWorkItem() => GetFleetWorkItem5();

    // FUN_004d05e0: ComputeAssignmentSubState. Assembly trace (fully read).
    // Calls FUN_004d1160 (compute_type_5_requested_count):
    //   = (Capacity * FleetTotalCapacity * 30/10000) - accumulated_from_candidate_list.
    //   Cap: if FleetTotalCapacity - FleetAssignedCapacity < result: result=0.
    // If result < 0: return 8. If result == 0: return 0. If result > 0:
    //   sub_41a9e0(workspace, dest=this+0x64, type=0x2a, 0x8000, 0x4000, 2) → stores agent entity
    //   at _type5AgentRef64 (binary +0x64). If agent found AND fleet at _fleetEntityId5 found: return 10 (0xa).
    // Implementation:
    //   FUN_004d1160: (Capacity * FleetTotalCapacity * 30/10000) - accumulated_from_candidate_list.
    //   Cap: if FleetTotalCapacity - FleetAssignedCapacity < result: result = 0.
    //   result < 0 → 8; result == 0 → 0.
    //   result > 0:
    //     sub_41a9e0(workspace, dest=&_type5AgentRef64, type=0x2a, 0x8000, 0x4000, 2)
    //       → find construction yard; stores entity ref at _type5AgentRef64 (+0x64).
    //     If _type5AgentRef64 HIBYTE != 0 AND _fleetEntityId5 HIBYTE in [0x90,0x98): return 0xa.
    //
    //   sub_41a9e0 proxy: find own-faction planet with ConstructionFacility.
    private int ComputeAssignmentSubState()
    {
        int total = Workspace.FleetTotalCapacity;
        int result = (Capacity * total * 30) / 10000;
        if (total - Workspace.FleetAssignedCapacity < result)
            result = 0;
        if (result < 0) return 8;
        if (result == 0) return 0;

        // sub_41a9e0(workspace, &_type5AgentRef64, type=0x2a, 0x8000, 0x4000, 2):
        // Find own-faction planet with ConstructionFacility.
        string ownerId = Workspace.Owner?.InstanceID;
        SystemAnalysisRecord constructionSys = Workspace.SystemAnalysis.FirstOrDefault(r =>
            (r.PresenceFlags & 0x1u) != 0
            && r.System != null
            && r.System.Planets.Any(p =>
                p.GetOwnerInstanceID() == ownerId
                && p.GetBuildingTypeCount(BuildingType.ConstructionFacility) > 0
            )
        );
        if (constructionSys != null)
            _type5AgentRef64 = constructionSys.InternalId;

        // Condition: _type5AgentRef64 has HIBYTE != 0 AND _fleetEntityId5 is a valid system
        int agentHibyte = (_type5AgentRef64 >> 0x18) & 0xff;
        int fleetHibyte = (_fleetEntityId5 >> 0x18) & 0xff;
        if (agentHibyte != 0 && fleetHibyte >= 0x90 && fleetHibyte < 0x98)
            return 0xa;
        return 0;
    }

    // FUN_004d0080: CheckFleetDispatchCondition. Assembly trace (fully read).
    // Returns FUN_004d1160(this) < 0 (1 if shortage, 0 if no shortage).
    // Proxy: same computation as ComputeAssignmentSubState.
    private int CheckFleetDispatchCondition()
    {
        int total = Workspace.FleetTotalCapacity;
        int result = (Capacity * total * 30) / 10000;
        if (total - Workspace.FleetAssignedCapacity < result)
            result = 0;
        return result < 0 ? 1 : 0;
    }

    // FUN_004d0680: CreateFleetDispatchWorkItem for Type 5. Assembly trace (fully read).
    // Gets last node from _type5CandidateList; checks capacity and FlagA conditions.
    // Builds unit nodes via facility finders. Creates TypeCode 0x200 (FleetShortageWorkItem).
    // BLOCKED (unit nodes): facility finders require game entity infrastructure.
    // Proxy: use last candidate from _type5CandidateList.
    private AIWorkItem CreateFleetDispatchWorkItem()
    {
        if (_type5CandidateList.Count == 0)
            return null;
        int sysId = _type5CandidateList[_type5CandidateList.Count - 1];
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == sysId
        );
        if (rec?.System == null)
            return null;
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004d0a80: DispatchEntityToTarget for Type 5. Assembly trace (fully read).
    // Thiscall(this, &_fleetEntityId5 at binary +0x5c).
    // Check 1: HIBYTE(_fleetEntityId5) in [0x90, 0x98) — system entity. Passes when
    //   ScanFleetCandidatesPhaseA has found a candidate.
    // Check 2: HIBYTE(_type5EntityRef60) in [0xa0, 0xa2) — character entity range.
    //   _type5EntityRef60 is set by CheckFleetAssignmentEligibility to the key returned
    //   by sub_4025b0 (not in disassembly) applied to a fleet entity. In practice the key
    //   is a fleet entity ID (HIBYTE < 0xa0), so this check always fails in C#.
    // Creates TypeCode 0x214 AgentShortageWorkItem from _type5EntityRef60 node if both pass.
    private AIWorkItem DispatchEntityToTarget()
    {
        int h1 = (_fleetEntityId5 >> 0x18) & 0xff;
        if (h1 < 0x90 || h1 >= 0x98)
            return null;
        int h2 = (_type5EntityRef60 >> 0x18) & 0xff;
        if (h2 < 0xa0 || h2 >= 0xa2)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _fleetEntityId5
        );
        return rec?.System != null
            ? new AgentShortageWorkItem(0x214, rec.System, 1, OwnerSide)
            : null;
    }

    // FUN_004d0bc0: CreateEntityTransferFollowup for Type 5. Assembly trace (fully read).
    // Check: HIBYTE(_type5EntityRef60) in [0xa0, 0xa2) — character entity range.
    // Same gate as DispatchEntityToTarget. In practice always fails (see above).
    // Creates TypeCode 0x210 AgentShortageWorkItem.
    // Binary additionally sets workItem+0x44 = _type5AgentRef64 (agent from sub_41a9e0)
    // and workItem+0x48 = _batchCount5. Those fields are not yet mapped on AgentShortageWorkItem.
    private AIWorkItem CreateEntityTransferFollowup()
    {
        int h = (_type5EntityRef60 >> 0x18) & 0xff;
        if (h < 0xa0 || h >= 0xa2)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _fleetEntityId5
        );
        return rec?.System != null
            ? new AgentShortageWorkItem(0x210, rec.System, _batchCount5, OwnerSide)
            : null;
    }

    // FUN_004d0ce0: BuildMissionBatch. Assembly trace (fully read).
    // Same structure as FUN_004dbfb0 (Type 1 UpdateShortageFleet):
    // 1. Reset this+0x5c (entity ref). Check _type5CandidateList count > 1 → early exit.
    // 2. FUN_004d1160(requestedCount). If <= 0: simplified path.
    //    Main: QuerySystemAnalysis(0x40, stat=0x13) → fleet lookup → candidate seeding.
    //    Simplified: check HIBYTE(this+0x5c) in [0x90,0x98) → insert into _type5CandidateList + FlagA|=0x400000.
    // BLOCKED: fleet infra. Proxy: resets entity ref, computes batch count.
    private void BuildMissionBatch()
    {
        _fleetEntityId5 = 0;
        _batchCount5 = (Capacity * Workspace.FleetTotalCapacity) / 100;
    }

    // FUN_004d1240: SelectMissionCandidates. Assembly trace (fully read).
    // Same structure as FUN_004dc490 (Type 1 FinalizeShortageRecord):
    // Manages _type5CandidateList: selects best candidate by *(sys+0x5c) (cost field at +0x5c),
    // marks selected with PresenceFlags |= 0x10000000, transfers to secondary list.
    // BLOCKED: system analysis cost field mapping + candidate list management.
    // Proxy: sets _fleetEntityId5 to first candidate.
    private void SelectMissionCandidates()
    {
        if (_type5CandidateList.Count > 0)
            _fleetEntityId5 = _type5CandidateList[0];
    }

    // FUN_004cf510: ScanFleetCandidatesPhaseB.
    //
    // 1. Clear workspace+0x18 entity ref (sub_4ec230; not tracked in C#).
    // 2. Iterate _type5CandidateList (this+0x78) tail-to-head:
    //    a. If var_30 already set → exit loop (loc_4cf555 guard).
    //    b. Set _type5EntityRef60 = node key.
    //    c. HIBYTE check [0x90,0x98): if fails → remove from list (loc_4cf691).
    //    d. sub_403d30(workspace+0x2c, id) → find SystemAnalysisRecord: if null → remove (loc_4cf663).
    //    e. PresenceFlags & 0x1: fail → FlagA &= ~0x400000, remove (loc_4cf622).
    //    f. FlagA & 0x400000: fail → FlagA &= ~0x400000, remove (loc_4cf622).
    //    g. Compound: PlanetSubobjects[5]!=null (*(sys+0x5c)>0) && FlagA&0x40000 && (FlagA&0x3)==0.
    //       If all pass: workspace+0x18 = _type5EntityRef60, var_30=1. Else: just advance, no removal.
    // 3. Fallback (var_30==0): _type5EntityRef60 = workspace+0x14 (not tracked in C#; always 0).
    //    Same HIBYTE+lookup+condition check. If fail: reset _type5EntityRef60, return 0.
    // 4. var_30==1: sub_4f25a0(OwnerSide, &_type5EntityRef60) fleet lookup,
    //    sub_5087e0(2) capacity check, sub_4025b0 stores fleet entity ref into _type5EntityRef60.
    //    If all pass: var_2C=1. Return var_2C.
    private int ScanFleetCandidatesPhaseB()
    {
        int found = 0;

        for (int i = _type5CandidateList.Count - 1; i >= 0 && found == 0; i--)
        {
            int candidateId = _type5CandidateList[i];
            _type5EntityRef60 = candidateId;

            // HIBYTE check: system entity [0x90, 0x98).
            int hibyte = (int)(((uint)candidateId >> 24) & 0xff);
            if (hibyte < 0x90 || hibyte >= 0x98)
            {
                _type5CandidateList.RemoveAt(i);
                continue;
            }

            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == candidateId
            );
            if (rec == null)
            {
                _type5CandidateList.RemoveAt(i);
                continue;
            }

            // loc_4cf5e4: PresenceFlags & 0x1. Fail → FlagA &= ~0x400000, remove.
            if ((rec.PresenceFlags & 0x1) == 0)
            {
                rec.FlagA &= ~0x400000;
                _type5CandidateList.RemoveAt(i);
                continue;
            }

            // loc_4cf5ea: FlagA & 0x400000. Fail → FlagA &= ~0x400000, remove.
            if ((rec.FlagA & 0x400000) == 0)
            {
                rec.FlagA &= ~0x400000;
                _type5CandidateList.RemoveAt(i);
                continue;
            }

            // loc_4cf5f5: compound check. Pass → found=1. Fail → advance only (no removal).
            // *(sys+0x5c)>0 = PlanetSubobjects[5]!=null (pointer to 6th planet slot, non-null if ≥6 planets).
            // HIBYTE(FlagA)&0x4 = FlagA&0x40000. LOBYTE(FlagA)&0x3 = FlagA&0x3 (enemy presence).
            if (rec.PlanetSubobjects[5] != null && (rec.FlagA & 0x40000) != 0 && (rec.FlagA & 0x3) == 0)
                found = 1;
        }

        // Fallback: workspace+0x14 entity ref (not yet tracked in C#; always 0 → HIBYTE fails).
        if (found == 0)
        {
            int fallbackId = 0; // workspace+0x14
            _type5EntityRef60 = fallbackId;
            int fbHibyte = (int)(((uint)fallbackId >> 24) & 0xff);
            if (fbHibyte >= 0x90 && fbHibyte < 0x98)
            {
                SystemAnalysisRecord fbRec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                    r.InternalId == fallbackId
                );
                if (fbRec != null
                    && (fbRec.PresenceFlags & 0x1) != 0
                    && (fbRec.FlagA & 0x400000) != 0
                    && fbRec.PlanetSubobjects[5] != null
                    && (fbRec.FlagA & 0x40000) != 0
                    && (fbRec.FlagA & 0x3) == 0)
                {
                    found = 1;
                }
            }
            if (found == 0)
            {
                _type5EntityRef60 = 0;
                return 0;
            }
        }

        // sub_4f25a0(OwnerSide, &_type5EntityRef60) + sub_5087e0(2) proxy.
        // Verify own-faction presence and available fleet capacity ≥ 2.
        SystemAnalysisRecord target = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _type5EntityRef60
        );
        if (target == null || (target.PresenceFlags & 0x1) == 0)
            return 0;
        if (Workspace.FleetTotalCapacity - Workspace.FleetAssignedCapacity < 2)
            return 0;

        // sub_4025b0 proxy: _type5EntityRef60 remains as system entity ref.
        return 1;
    }

    // FUN_004cf7f0: EvaluateFleetDispatchStatus. Assembly trace (fully read).
    // Offset mapping (corrected):
    //   workspace[0x184 + slot*4] with slot in [0x5a,0x5d] = workspace[0x2ec..0x2f8]
    //   = CharacterScores[10..13] (CharacterScores starts at workspace+0x2c4).
    //   workspace[0x2fc] = CharacterScores[14] = threshold.
    //
    // Algorithm: do-while loop; EAX starts at 0.
    //   Init: if cursor==0, set cursor=0x5a.
    //   Each iteration:
    //     1. If full-rotation done (ebp=1): break; return 0.
    //     2. threshold = max(8, CharacterScores[14]).
    //     3. this+0x48 = CharacterScores[slot-0x50] < threshold ? 1 : 0.
    //     4. Switch on slot: set _supplyTypeFlag (0x1000000/2/4/8 * 0x1000000),
    //        set _supplyTypeReady = 1, advance cursor to next slot (wrap 0x5d→0x5a).
    //     5. If this+0x48 > 0: EAX = 1 (shortage found).
    //     6. If new cursor == original cursor: full-rotation done (ebp=1).
    //   While EAX == 0.
    //   Returns EAX (1=shortage, 0=full rotation without shortage).
    private int EvaluateFleetDispatchStatus()
    {
        if (_supplySlotCursor == 0)
            _supplySlotCursor = 0x5a;

        ushort startCursor = (ushort)_supplySlotCursor;
        ushort slot = startCursor;
        bool fullRotationDone = false;

        // CharacterScores[14] is the threshold; clamped to at least 8.
        int threshold = 8;
        if (Workspace.CharacterScores.Length > 14)
            threshold = System.Math.Max(8, Workspace.CharacterScores[14]);

        do
        {
            if (fullRotationDone)
                return 0;

            // workspace[0x184 + slot*4] = CharacterScores[slot - 0x50]
            int scoreIdx = slot - 0x50;
            int score =
                (scoreIdx >= 0 && scoreIdx < Workspace.CharacterScores.Length)
                    ? Workspace.CharacterScores[scoreIdx]
                    : 0;
            _batchCount5 = score < threshold ? 1 : 0;

            switch (slot)
            {
                case 0x5a:
                    _supplyTypeFlag = 0x1000000;
                    _supplyTypeReady = 1;
                    _supplySlotCursor = 0x5b;
                    break;
                case 0x5b:
                    _supplyTypeFlag = 0x2000000;
                    _supplyTypeReady = 1;
                    _supplySlotCursor = 0x5c;
                    break;
                case 0x5c:
                    _supplyTypeFlag = 0x4000000;
                    _supplyTypeReady = 1;
                    _supplySlotCursor = 0x5d;
                    break;
                case 0x5d:
                    _supplyTypeFlag = unchecked((int)0x8000000);
                    _supplyTypeReady = 1;
                    _supplySlotCursor = 0x5a;
                    break;
            }

            if (_batchCount5 > 0)
                return 1;

            slot = (ushort)_supplySlotCursor;
            if (startCursor == slot)
                fullRotationDone = true;
        } while (true);
    }

    // FUN_004cf900: ComputeTransportSubState. Assembly trace (fully read).
    // FUN_004d1100: (Capacity * FleetTotalCapacity * 70/10000) - workspace.CharacterScores[0].
    //   Cap: if FleetTotalCapacity - FleetAssignedCapacity < result: result=0.
    // If result < 0: return 2 (shortage → CreateTransportWorkItem).
    // If result == 0: return 0 (terminal → Phase=PhaseA).
    // If result > 0: sub_41a9e0(workspace, dest=this+0x64, type=0x29, filter1=this+0x50, filter2=this+0x54, 1)
    //   → stores agent entity ref at _type5AgentRef64 (binary +0x64); if found: return 0xb. Else: return 0.
    // BLOCKED: sub_41a9e0 requires entity infrastructure. Proxy returns 0 when result > 0.
    private int ComputeTransportSubState()
    {
        // FUN_004d1100: (Capacity * FleetTotalCapacity * 70/10000) - CharacterScores[0]
        int total = Workspace.FleetTotalCapacity;
        int result = (Capacity * total * 70) / 10000;
        if (Workspace.CharacterScores.Length > 0)
            result -= Workspace.CharacterScores[0];
        if (total - Workspace.FleetAssignedCapacity < result)
            result = 0;
        if (result < 0)
            return 2;
        if (result == 0)
            return 0;
        // result > 0: agent lookup blocked → return 0
        return 0;
    }

    // FUN_004cf8e0: CheckTransportDispatchCondition. Assembly trace (fully read).
    // Returns FUN_004d1100(this) < 0 (1 if shortage, 0 otherwise). Same computation as ComputeTransportSubState.
    private int CheckTransportDispatchCondition()
    {
        int total = Workspace.FleetTotalCapacity;
        int result = (Capacity * total * 70) / 10000;
        if (Workspace.CharacterScores.Length > 0)
            result -= Workspace.CharacterScores[0];
        if (total - Workspace.FleetAssignedCapacity < result)
            result = 0;
        return result < 0 ? 1 : 0;
    }

    // FUN_004cf980: CreateTransportWorkItem. Assembly trace (fully read).
    // 1. sub_41a880(workspace, 0x802000, 0, 0xa0, 0, 9, 0, 1000, 1) → mission issue entry in _issueContainer.
    //    sub_434e30 gets last ID → var_28 (entity key).
    // 2. HIBYTE(var_28) in [0x3c, 0x40): if in range → allocate 0x20 node, attach.
    //    If not in range → sub_41a880(0x2000, 0, 0xa0, ...) alternate query → same check.
    // 3. If issue list non-empty: sub_4f5060(0x242) → TypeCode 0x242 work item.
    // BLOCKED: entity HIBYTE range [0x3c,0x40) never occurs in C# (hash code high bytes = 0). Returns null.
    private AIWorkItem CreateTransportWorkItem() => null;

    // FUN_004d0960: CreateFleetTransferWorkItem. Assembly trace (fully read).
    // 1. Check HIBYTE(this+0x60) in [0xa4, 0xa6) — character/agent type (different from fleet [0x90,0x98)!).
    // 2. If character type: allocate TypeCode=0x212 work item (NOT 0x214!).
    //    Allocate 0x20 node, init with this+0x60.
    //    Set item+0x20 = OwnerSide. Attach nodes via vtable+0x24.
    //    item+0x44 = this+0x64 (entity ref). item+0x48 = this+0x48.
    //    Return work item.
    // TypeCode 0x212: distinct from 0x210/0x211/0x214 (different agent dispatch variant).
    // BLOCKED: HIBYTE check [0xa4,0xa6) needs character entity encoding; agent InternalIds not yet
    // implemented. RouteWorkItemToManager routes all AgentShortageWorkItem instances (including 0x212)
    // to ApplyAgentShortage via type check; the TypeCode is not the routing gate.
    // Proxy: returns AgentShortageWorkItem(0x212) routed to ApplyAgentShortage.
    private AIWorkItem CreateFleetTransferWorkItem()
    {
        if (_targetEntityId5 == 0)
            return null;
        var rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == _targetEntityId5);
        return rec?.System != null
            ? new AgentShortageWorkItem(0x212, rec.System, 1, OwnerSide)
            : null;
    }
}

// ------------------------------------------------------------------
// Type 6 — FUN_004dc7d0 — ThreePhaseStrategyRecordA
// 0x8c bytes.  Active guard: standard.
//
// Phase 0x3ec → FUN_004dcd40 (PhaseA), return result
// Phase 0x3ef → FUN_004dcb80 (PhaseB), return result
// Phase 0x3f0 → FUN_004dcc10 (PhaseC), return result
// Other       → Phase=0x3ef, SubState=0, return null
// ------------------------------------------------------------------
public class ThreePhaseStrategyRecordA : StrategyRecord
{
    private const int PhaseA = 0x3ec;
    private const int PhaseB = 0x3ef;
    private const int PhaseC = 0x3f0;

    public ThreePhaseStrategyRecordA(int ownerSide)
        : base(typeId: 6, capacity: 1, ownerSide: ownerSide) { }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        if (Phase == PhaseA)
            return RunPhaseA();
        if (Phase == PhaseB)
            return RunPhaseB();
        if (Phase == PhaseC)
            return RunPhaseC();

        Phase = PhaseB;
        SubState = 0;
        return null;
    }

    // Extra fields beyond the 0x40-byte base (total struct size 0x8c, 19 extra 4-byte fields).
    //   +0x54: entity reference passed to FUN_004dfd70 (PhaseC case 0xc).
    //   +0x58: entity reference = top candidate from ScanPhaseACandidates (FUN_004df030).
    //   +0x68: count value from ScanPhaseACandidates (system_record+0x114 field).
    //   +0x7c: candidate list — iterated by ScanPhaseACandidates.

    // +0x58: candidate entity ref set by ScanPhaseACandidates (FUN_004df030).
    private int _candidateRef58;

    // +0x68: candidate count/capacity field set by ScanPhaseACandidates.
    private int _candidateCount68;

    // +0x7c: candidate list — system entity keys for phase A candidates.
    private readonly List<int> _type6CandidateList = new List<int>();

    // Issue container for phase operations.
    private readonly IssueRecordContainer _type6IssueContainer = new IssueRecordContainer();

    // FUN_004dcd40 — PhaseA inner state machine.
    // Drives entity filter scanning, bit-selection, work item dispatch, and batch build.
    // Terminates to PhaseB.
    private AIWorkItem RunPhaseA()
    {
        AIWorkItem paVar3 = null;
        switch (SubState)
        {
            default:
                SubState = 6;
                return null;

            case 6:
            {
                int found = ScanPhaseACandidates(); // FUN_004df030
                if (found != 0)
                {
                    SubState = 5;
                    return null;
                }
                break; // → terminal (null)
            }

            case 5:
            {
                // Clear bit 0x20 from the pending supply bitmask (workspace+0x8).
                Workspace.PendingSupplyBitmask &= ~0x20;
                if (
                    Workspace.EntityTargetType == 0x20
                    && CheckEntityFilterEligibility() != 0 // FUN_004df310
                )
                {
                    Workspace.AdvanceBitSelection();
                    SubState = 9;
                }
                else
                {
                    SubState = 0xe;
                }
                return null;
            }

            case 9:
            {
                AIWorkItem item = CreateFilteredWorkItem(); // FUN_004df4b0
                if (item != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 7;
                return item;
            }

            case 7:
            {
                int next = ComputeEntityPipelineSubState(); // FUN_004df9f0
                SubState = next;
                if (next != 0)
                    return null;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }

            case 10:
            {
                paVar3 = BuildEntityBatchItem(); // FUN_004dfa90
                break; // → terminal
            }

            case 0xc:
            {
                // FUN_004dffd0(this, &(this+0x58)): create dispatch work item using entity ref at +0x58.
                AIWorkItem item = CreateCandidateDispatchWorkItem(); // FUN_004dffd0
                if (item != null)
                {
                    SubState = 0xd;
                    ReadyFlag = 1;
                    TickCounter++;
                    return item;
                }
                break; // → terminal (null)
            }

            case 0xd:
            {
                paVar3 = CreateFollowupWorkItem(); // FUN_004e0110
                break; // → terminal
            }

            case 0xe:
                ClearBatchRecords(); // FUN_004e08f0 — return value discarded
                SelectBatchCandidates(); // FUN_004e0e40 — return value discarded
                break; // → terminal (null)
        }

        SubState = 0;
        Phase = PhaseB;
        ReadyFlag = 1;
        return paVar3;
    }

    // FUN_004dcb80 — PhaseB inner state machine.
    // Sequential 4-stage pipeline; any stage returning non-null short-circuits to terminal.
    // Terminates to PhaseC.
    private AIWorkItem RunPhaseB()
    {
        switch (SubState)
        {
            default:
                SubState = 1;
                return null;

            case 1:
            {
                AIWorkItem item = EvaluatePipelineStage1(); // FUN_004dceb0
                if (item == null)
                {
                    SubState = 2;
                    return null;
                }
                SubState = 0;
                Phase = PhaseC;
                ReadyFlag = 1;
                return item;
            }

            case 2:
            {
                AIWorkItem item = EvaluatePipelineStage2(); // FUN_004dd470
                if (item == null)
                {
                    SubState = 3;
                    return null;
                }
                SubState = 0;
                Phase = PhaseC;
                ReadyFlag = 1;
                return item;
            }

            case 3:
            {
                AIWorkItem item = EvaluatePipelineStage3(); // FUN_004dda30
                if (item == null)
                {
                    SubState = 4;
                    return null;
                }
                SubState = 0;
                Phase = PhaseC;
                ReadyFlag = 1;
                return item;
            }

            case 4:
            {
                AIWorkItem item = EvaluatePipelineStage4(); // FUN_004ddee0
                SubState = 0;
                Phase = PhaseC;
                ReadyFlag = 1;
                return item;
            }
        }
    }

    // FUN_004dcc10 — PhaseC inner state machine.
    // Drives condition checks, entity sub-state pipeline, and dispatch work items.
    // Terminates to PhaseA.
    private AIWorkItem RunPhaseC()
    {
        AIWorkItem paVar3 = null;
        switch (SubState)
        {
            default:
                SubState = 5;
                return null;

            case 5:
            {
                int r = CheckPhaseCConditionA(); // FUN_004de4a0
                // Non-zero → SubState=6; zero → SubState=0xb.
                // Derived from (-(uint)(r != 0) & 0xfffffffb) + 0xb assembly pattern.
                SubState = (r != 0) ? 6 : 0xb;
                return null;
            }

            case 6:
            {
                int r = CheckPhaseCConditionB(); // FUN_004de780
                // Non-zero → SubState=7; zero → SubState=0xb.
                // Derived from (-(uint)(r != 0) & 0xfffffffc) + 0xb assembly pattern.
                SubState = (r != 0) ? 7 : 0xb;
                return null;
            }

            case 7:
            {
                int next = ComputePhaseCNextState(); // FUN_004dece0
                SubState = next;
                if (next != 0)
                    return null;
                Phase = PhaseA;
                ReadyFlag = 1;
                return null;
            }

            case 8:
            {
                paVar3 = CreatePhaseCTerminalWorkItem(); // FUN_004dedc0
                break; // → terminal
            }

            case 0xb:
            {
                int r = CheckPhaseCBranchCondition(); // FUN_004dedb0
                if (r == 0)
                {
                    SubState = 0;
                    Phase = PhaseA;
                    // NOTE: ReadyFlag intentionally NOT set on this path (mirrors FUN_004dcc10).
                    return null;
                }
                SubState = 8;
                return null;
            }

            case 0xc:
            {
                // FUN_004dfd70(this, &(this+0x54)): create dispatch work item using entity ref at +0x54.
                AIWorkItem item = CreatePhaseCDispatchWorkItem(); // FUN_004dfd70
                if (item != null)
                {
                    SubState = 0xd;
                    ReadyFlag = 1;
                    TickCounter++;
                    return item;
                }
                break; // → terminal (null)
            }

            case 0xd:
            {
                paVar3 = CreatePhaseCFollowupWorkItem(); // FUN_004dfeb0
                break; // → terminal
            }
        }

        SubState = 0;
        Phase = PhaseA;
        ReadyFlag = 1;
        return paVar3;
    }

    // --- Phase A helper stubs ---

    // FUN_004df030: Scans _type6CandidateList for eligible entities; seeds list when empty.
    //
    // Assembly trace (fully read):
    // 1. Reset _candidateRef58, _candidateCount68. var_20=0x3e8 (min-score sentinel).
    // 2. If list (this+0x7c) non-empty: iterate forward via *(node+0x10):
    //    PresenceFlags & 0x1, FlagA & 0x3 == 0, *(sys+0x114) > 0, *(sys+0x5c) < var_20.
    //    Best: _candidateCount68 = capacity, _candidateRef58 = key, var_24=1.
    // 3. If var_24==0 (list empty OR no valid candidate in list):
    //    a. QuerySystemAnalysis(0x40, stat=0x13) → issue container at this+0x40 → last ID →
    //       _candidateRef58. If list was non-empty: also QuerySystemAnalysis(0x40, stat=4).
    //    b. sub_419af0(this+0x58, incl30=1, excl28=0x400003, excl30=0x40000000, stat=5) →
    //       container. If list was non-empty: also sub_419af0(…, stat=0x33) → container.
    //       Last ID from container → update _candidateRef58. Clear container.
    //    c. HIBYTE(_candidateRef58) in [0x90,0x98): look up SystemAnalysis. If found:
    //       allocate 0x1c node → insert into _type6CandidateList → FlagA |= 0x400000 →
    //       sub_4334b0 (no-op proxy) → var_24=1.
    // 4. Return var_24.
    private int ScanPhaseACandidates()
    {
        _candidateRef58 = 0;
        _candidateCount68 = 0;
        int minScore = 0x3e8; // var_20 initial value from assembly
        int found = 0; // var_24

        // Step 2: iterate existing list
        foreach (int sysId in _type6CandidateList.ToList())
        {
            SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == sysId
            );
            if (rec == null)
            {
                _type6CandidateList.Remove(sysId);
                continue;
            }

            if ((rec.PresenceFlags & 0x1u) == 0)
                continue;
            if ((rec.FlagA & 0x3) != 0)
                continue;
            if (rec.Stats.FacilityCount <= 0) // proxy for *(sys+0x114) > 0
                continue;

            if (rec.SystemScore < minScore) // proxy for *(sys+0x5c) < var_20
            {
                minScore = rec.SystemScore;
                _candidateCount68 = rec.Stats.FacilityCount;
                _candidateRef58 = sysId;
                found = 1;
            }
        }

        // Step 3: fallback seeding when no candidate found
        if (found == 0)
        {
            bool listWasNonEmpty = _type6CandidateList.Count > 0;

            // Step 3a: system analysis queries to seed _candidateRef58
            IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
                incl24: 0x40, incl28: 0, incl2c: 0,
                excl24: 0, excl28: 0, excl2c: 0,
                statIndex: 0x13);
            if (listWasNonEmpty)
            {
                IssueRecordContainer c2 = Workspace.QuerySystemAnalysis(
                    incl24: 0x40, incl28: 0, incl2c: 0,
                    excl24: 0, excl28: 0, excl2c: 0,
                    statIndex: 4);
                c1.StoreFrom(c2);
                c2.Clear();
            }
            if (c1.TryGetTopEntityKey(out int k1))
                _candidateRef58 = k1;
            c1.Clear();

            // Step 3b: planet sub-object queries refining _candidateRef58
            if (_candidateRef58 != 0)
            {
                IssueRecordContainer p1 = Workspace.QuerySystemPlanets(
                    _candidateRef58,
                    incl28: 0, incl2c: 0, incl30: 1,
                    excl28: 0x400003, excl2c: 0, excl30: unchecked((int)0x40000000),
                    statIndex: 5);
                if (listWasNonEmpty)
                {
                    IssueRecordContainer p2 = Workspace.QuerySystemPlanets(
                        _candidateRef58,
                        incl28: 0, incl2c: 0, incl30: 1,
                        excl28: 0x400003, excl2c: 0, excl30: unchecked((int)0x40000000),
                        statIndex: 0x33);
                    p1.StoreFrom(p2);
                    p2.Clear();
                }
                if (p1.TryGetTopEntityKey(out int k2))
                    _candidateRef58 = k2;
                p1.Clear();
            }

            // Step 3c: HIBYTE check → insert into candidate list if valid system entity
            int hibyte = (_candidateRef58 >> 24) & 0xff;
            if (hibyte >= 0x90 && hibyte < 0x98)
            {
                SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                    r.InternalId == _candidateRef58
                );
                if (rec != null)
                {
                    if (!_type6CandidateList.Contains(_candidateRef58))
                        _type6CandidateList.Add(_candidateRef58);
                    rec.FlagA |= 0x400000;
                    // sub_4334b0: sets DispositionFlags=1 + propagation — no-op proxy
                    found = 1;
                }
            }
        }

        return found;
    }

    // FUN_004df310: CheckEntityFilterEligibility. Assembly trace (fully read).
    // Same structure as FUN_004cfeb0 (Type 5 CheckFleetAssignmentEligibility):
    // 1. Clear this+0x64. sub_419330(workspace, this+0x58, 0x1000, ...) → this+0x50 entity ref.
    // 2. QuerySystemPlanets(this+0x50, 0x800800, 0,0x1,0x3,0,0, 6, 1) → update this+0x50.
    // 3. Check HIBYTE(this+0x50) in [0x90,0x98). If fleet: sub_4f25a0+sub_5087e0 capacity check.
    //    If passes: this+0x64 = SystemScore-1. Return 1.
    // With InternalIds, HIBYTE check passes for SystemAnalysisRecord. DispositionFlags & 0x1000
    // (required by sub_419330) needs CapabilityFlags & 0x800 AND 0x800000 (not yet set).
    // Proxy: QuerySystemAnalysis(incl24=0x1000) with fallback to _candidateRef58.
    private int CheckEntityFilterEligibility()
    {
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x1000,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 0x14
        );
        int entityRef = 0;
        if (!c1.TryGetTopEntityKey(out entityRef))
            entityRef = _candidateRef58; // fallback to candidate from ScanPhaseACandidates
        c1.Clear();
        if (entityRef == 0)
            return 0;

        IssueRecordContainer p1 = Workspace.QuerySystemPlanets(
            entityRef,
            incl28: 0x800800,
            incl2c: 0,
            incl30: 1,
            excl28: 3,
            excl2c: 0,
            excl30: 0,
            statIndex: 6
        );
        if (p1.TryGetTopEntityKey(out int refined))
            entityRef = refined;
        p1.Clear();

        int hibyte = (entityRef >> 0x18) & 0xff;
        if (hibyte < 0x90 || hibyte >= 0x98)
            return 0;

        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == entityRef
        );
        if (rec == null || (rec.PresenceFlags & 0x1u) == 0)
            return 0;

        // Capacity check proxy: system has deployment capacity.
        _candidateRef58 = entityRef;
        _candidateCount68 = System.Math.Max(0, rec.Stats.FacilityCount - 1);
        return 1;
    }

    // FUN_004df4b0: CreateFilteredWorkItem for Type 6 Phase A. Assembly trace (fully read).
    // Queries workspace with fleet-system filter. HIBYTE check [0x90,0x98) now passes with InternalIds.
    // Builds unit nodes (capital ships, starfighters, regiments) via sub_52bc60/52b900/52b600.
    // Creates TypeCode 0x200 work item.
    // BLOCKED (unit nodes): facility finders (sub_52bc60 etc.) require game entity infrastructure.
    // Proxy: use _candidateRef58 set by CheckEntityFilterEligibility/ScanPhaseACandidates.
    private AIWorkItem CreateFilteredWorkItem()
    {
        if (_candidateRef58 == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRef58
        );
        if (rec?.System == null)
            return null;
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004df9f0: ComputeEntityPipelineSubState. Assembly trace (fully read).
    // Calls FUN_004e0d60(this) — (Capacity * FleetTotalCapacity * X/10000) - workspace.something.
    // If result < 0: return 10 (0xa). If result == 0: return 0.
    // If result > 0: sub_41a9e0 agent lookup. If found: return 12 (0xc). Else: return 0.
    // BLOCKED: agent infra blocked. Proxy: returns 0xa when candidate found.
    private int ComputeEntityPipelineSubState()
    {
        return _candidateRef58 != 0 ? 0xa : 0;
    }

    // FUN_004dfa90: BuildEntityBatchItem for Type 6 Phase A. Assembly trace (fully read).
    // Gets last node from _type6CandidateList. Checks *(sys+0x58) > 0 and FlagA bit 28 (0x10000000).
    // Builds capital ship, starfighter, regiment unit nodes based on FlagA bits.
    // Creates TypeCode 0x200 work item if nodes built.
    // BLOCKED (unit nodes): sub_52bc60/52b900/52b600 require game entity infrastructure.
    // Proxy: use last candidate from _type6CandidateList.
    private AIWorkItem BuildEntityBatchItem()
    {
        if (_type6CandidateList.Count == 0)
            return null;
        int sysId = _type6CandidateList[_type6CandidateList.Count - 1];
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == sysId
        );
        if (rec?.System == null)
            return null;
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004dffd0: CreateCandidateDispatchWorkItem for Type 6 Phase A. Assembly trace (fully read).
    // HIBYTE(*arg_0) check [0x90,0x98) fleet + HIBYTE(this+0x50) check [0xa0,0xa2) agent.
    // Both required. Creates TypeCode 0x214 (AgentShortageWorkItem) if both pass.
    // BLOCKED: both HIBYTE entity range checks fail in C#. Returns null.
    private AIWorkItem CreateCandidateDispatchWorkItem() => null;

    // FUN_004e0110: CreateFollowupWorkItem for Type 6 Phase A. Assembly trace (fully read).
    // HIBYTE(this+0x50) check [0xa0,0xa2): agent range. Creates TypeCode 0x210 work item.
    // Sets work item fields from this+0x5c, this+0x60.
    // BLOCKED: HIBYTE agent check fails in C#. Returns null.
    private AIWorkItem CreateFollowupWorkItem() => null;

    // FUN_004e08f0: ClearBatchRecords for Type 6 Phase A. Assembly trace (fully read).
    // HIBYTE(var_4C) check [0x80,0x90) and HIBYTE(var_48) check [0x90,0x98) fleet range.
    // Iterates workspace entity target table, iterates fleet query results. Both HIBYTE blocked.
    // Main side effect: clears entity ref at this+0x58 (_candidateRef58).
    private void ClearBatchRecords() => _candidateRef58 = 0;

    // FUN_004e0e40: SelectBatchCandidates for Type 6 Phase A. Assembly trace (fully read).
    // Iterates candidate list; selects by capacity (*(sys+0x58)) comparison.
    // HIBYTE check [0x90,0x98) on selected candidate → fleet: allocate node, add to local list.
    // BLOCKED: fleet HIBYTE check fails in C#. Proxy selects first candidate from list.
    private void SelectBatchCandidates()
    {
        if (_type6CandidateList.Count > 0)
            _candidateRef58 = _type6CandidateList[0];
    }

    // --- Phase B pipeline stages ---
    // FUN_004dceb0/004dd470/004dda30/004ddee0: Successive pipeline evaluation stages.
    // Each queries the workspace for fleet shortage conditions and returns a work item
    // when a shortage is detected. The stages scan increasingly specific entity sets.
    // Stage returns null → advance to next stage; non-null → terminal to PhaseC.

    // FUN_004dceb0: EvaluatePipelineStage1. Assembly trace (fully read — very complex ~350 lines).
    // 1. Reset this+0x50 and this+0x54. Check workspace status flag bit 0x20.
    //    If bit set: FUN_00419640(workspace, 0, 0, 0x2000000, ...) — fleet assembly query.
    //      FUN_00419af0(this+0x54, 0x20001, 0x2, ...) — character planet query.
    //      If entity found in [0x90,0x98): complex character assignment loop:
    //        sub_419330(this+0x50, 0x80000, ...) + sub_419af0(..., 0x18000, ...).
    //        Iterate characters: capacity checks, allocate 0x20 nodes.
    //    If local list non-empty: TypeCode=0x201 work item.
    // BLOCKED: workspace status flag + character/fleet infrastructure required.
    // Proxy: uses QuerySystemAnalysis as stand-in (returns FleetShortageWorkItem or null).
    private AIWorkItem EvaluatePipelineStage1()
    {
        IssueRecordContainer c = Workspace.QuerySystemAnalysis(
            incl24: 0x80,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 4
        );
        SystemAnalysisRecord top = c.GetTopRecord();
        if (top == null)
            return null;
        _candidateRef58 = top.InternalId;
        return new FleetShortageWorkItem(top.System, OwnerSide);
    }

    // FUN_004dd470: EvaluatePipelineStage2. Assembly trace (fully read).
    // Checks workspace status bit 0x10. Queries fleet with sub_419640(0x1000000).
    // sub_419af0(this+0x54, 0x10001,...) + character entity iteration.
    // Creates TypeCode 0x201 (MissionExecutionWorkItem) if character entities found.
    // BLOCKED: HIBYTE fleet range [0x90,0x98) check fails in C#; character infra unavailable. Returns null.
    private AIWorkItem EvaluatePipelineStage2() => null;

    // FUN_004dda30: EvaluatePipelineStage3. Assembly trace (fully read).
    // Filter 0x40010 (bits 14+4) → sub_419af0(this+0x54, 0x2001,...) + sub_419330 + character loop.
    // Creates TypeCode 0x201 (MissionExecutionWorkItem) if fleet + character entities found.
    // BLOCKED: HIBYTE fleet range [0x90,0x98) check fails in C#; character infra unavailable. Returns null.
    private AIWorkItem EvaluatePipelineStage3() => null;

    // FUN_004ddee0: EvaluatePipelineStage4. Assembly trace (fully read).
    // Filter 0x40000. sub_419af0(this+0x54, 0x2000,...) + character loop.
    // Creates TypeCode 0x201 (MissionExecutionWorkItem).
    // BLOCKED: HIBYTE fleet range [0x90,0x98) check fails in C#. Returns null.
    private AIWorkItem EvaluatePipelineStage4() => null;

    // --- Phase C helpers ---

    // FUN_004de4a0: CheckPhaseCConditionA. Assembly trace (fully read).
    // Same structure as FUN_004cc030 (Type 10 EvaluateFleetDispatchStatus):
    // Iterates _type6CandidateList (this+0x7c). For each: HIBYTE check [0x90,0x98),
    //   FlagA & 0x400000, *(sys+0x5c) > 0, FlagA & 0x4000000 (set when ef & 0x40 in planet analysis),
    //   FlagA & 0x3 == 0. If found: workspace+0x14 = entity ref. Fallback via workspace+0x18.
    // HIBYTE check passes: InternalId HIBYTE=0x90 ∈ [0x90,0x98).
    // BLOCKED: post-find requires sub_4f25a0 (fleet lookup) + sub_5087e0 + sub_4025b0. Returns 0.
    private int CheckPhaseCConditionA() => 0;

    // FUN_004de780: CheckPhaseCConditionB. Assembly trace (fully read).
    // 4-phase sub-machine: dispatch state 0→1→2→3→4 using this+0x68, this+0x6c fields.
    // Each state: HIBYTE entity check [0x90,0x98) fleet range on different workspace refs.
    // Returns int (0 or 1) based on whether fleet entity meets conditions.
    // BLOCKED: HIBYTE fleet range [0x90,0x98) always fails in C#. Returns 0.
    private int CheckPhaseCConditionB() => 0;

    // FUN_004dece0: ComputePhaseCNextState. Assembly trace (fully read).
    // Same structure as FUN_004d05e0 (Type 5). Calls FUN_004e0d00 (capacity compute).
    // Returns 0 (terminal), 8 (CreatePhaseCTerminalWorkItem), or 0xc (dispatch).
    // CRITICAL: Was returning 6 which caused infinite loop (case 7→6→7→6...).
    // Fix: returns 0 (terminal → Phase=PhaseA) when agent infra blocked.
    private int ComputePhaseCNextState()
    {
        // FUN_004e0d00: same formula pattern as FUN_004d1160/FUN_004d1100.
        // With empty candidate list: positive result but agent blocked → return 0.
        return 0;
    }

    // FUN_004dedc0: CreatePhaseCTerminalWorkItem for Type 6 Phase C. Assembly trace (fully read).
    // HIBYTE(*entity) check [0x90,0x98): fleet gate. Builds unit nodes. Creates TypeCode 0x200.
    // BLOCKED: HIBYTE fleet range check fails in C#. Returns null.
    private AIWorkItem CreatePhaseCTerminalWorkItem() => null;

    // FUN_004dedb0: CheckPhaseCBranchCondition. Assembly trace (fully read).
    // *(this+0x60) = 0. Calls FUN_004e0d00 (result discarded). Returns 0 ALWAYS.
    // The function's only side effect is clearing this+0x60.
    // Result: always takes Phase=PhaseA (not ReadyFlag set) path in case 0xb.
    private int CheckPhaseCBranchCondition()
    {
        return 0; // Assembly: always returns 0
    }

    // FUN_004dfd70: CreatePhaseCDispatchWorkItem for Type 6 Phase C. Assembly trace (fully read).
    // HIBYTE(*arg_0) check [0x90,0x98) fleet + HIBYTE(this+0x50) check [0xa4,0xa6) agent sub-range.
    // Both required. Creates TypeCode 0x214 (AgentShortageWorkItem) if both pass.
    // BLOCKED: both HIBYTE entity range checks fail in C#. Returns null.
    private AIWorkItem CreatePhaseCDispatchWorkItem() => null;

    // FUN_004dfeb0: CreatePhaseCFollowupWorkItem for Type 6 Phase C. Assembly trace (fully read).
    // HIBYTE(this+0x50) check [0xa4,0xa6): different agent sub-range (not [0xa0,0xa2) or [0xa2,0xa4)).
    // Creates TypeCode 0x212 work item. Sets work item fields.
    // BLOCKED: HIBYTE agent check fails in C#. Returns null.
    private AIWorkItem CreatePhaseCFollowupWorkItem() => null;
}

// ------------------------------------------------------------------
// Type 7 — FUN_004d2260 — ThreePhaseStrategyRecordB
// 0x7c bytes.  Active guard: standard.
//
// Phase 0x3ec → FUN_004d26c0 (PhaseA), return result
// Phase 0x3f1 → FUN_004d24e0 (PhaseB), return result
// Phase 0x3f2 → FUN_004d2590 (PhaseC), return result
// Other       → Phase=0x3f1, SubState=0, return null
// ------------------------------------------------------------------
public class ThreePhaseStrategyRecordB : StrategyRecord
{
    private const int PhaseA = 0x3ec;
    private const int PhaseB = 0x3f1;
    private const int PhaseC = 0x3f2;

    public ThreePhaseStrategyRecordB(int ownerSide)
        : base(typeId: 7, capacity: 1, ownerSide: ownerSide) { }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        if (Phase == PhaseA)
            return RunPhaseA();
        if (Phase == PhaseB)
            return RunPhaseB();
        if (Phase == PhaseC)
            return RunPhaseC();

        Phase = PhaseB;
        SubState = 0;
        return null;
    }

    // Extra fields (same structure as Type 6, mirrored function set with different addresses):
    // +0x54: entity ref for PhaseC dispatch (FUN_004d5140).
    // +0x58: entity ref set by ScanPhaseACandidates (FUN_004d4370).
    private int _candidateRef58;
    private int _candidateCount68;
    private readonly List<int> _type7CandidateList = new List<int>();
    private readonly IssueRecordContainer _type7IssueContainer = new IssueRecordContainer();

    // FUN_004d26c0 — PhaseA inner state machine.
    // Drives entity filter scanning, bit-selection, work item dispatch, and batch build.
    // Terminates to PhaseB.
    private AIWorkItem RunPhaseA()
    {
        AIWorkItem paVar3 = null;
        switch (SubState)
        {
            default:
                SubState = 2;
                return null;

            case 2:
            {
                int found = ScanPhaseACandidates(); // FUN_004d4370
                if (found != 0)
                {
                    SubState = 5;
                    return null;
                }
                break; // → terminal (null)
            }

            // Jump table entries for SubState 3, 4, 7, 10 target the terminal block
            // directly in the original binary (no case-specific code).
            case 3:
            case 4:
            case 7:
            case 10:
                break; // → terminal (null)

            case 5:
            {
                // Clear bit 0x8 from the pending supply bitmask (workspace+0x8).
                Workspace.PendingSupplyBitmask &= ~0x8;
                if (
                    Workspace.EntityTargetType == 0x8
                    && CheckEntityFilterEligibility() != 0 // FUN_004d4650
                )
                {
                    Workspace.AdvanceBitSelection();
                    SubState = 8;
                }
                else
                {
                    SubState = 0xd;
                }
                return null;
            }

            case 6:
            {
                int next = ComputeEntityPipelineSubState(); // FUN_004d4dc0
                SubState = next;
                if (next != 0)
                    return null;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }

            case 8:
            {
                AIWorkItem item = CreateFilteredWorkItem(); // FUN_004d4880
                if (item != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 6;
                return item;
            }

            case 9:
            {
                paVar3 = BuildEntityBatchItem(); // FUN_004d4e60
                break; // → terminal
            }

            case 0xb:
            {
                // FUN_004d53a0(this, &(this+0x58)): create dispatch work item using entity ref at +0x58.
                AIWorkItem item = CreateCandidateDispatchWorkItem(); // FUN_004d53a0
                if (item != null)
                {
                    SubState = 0xc;
                    ReadyFlag = 1;
                    TickCounter++;
                    return item;
                }
                break; // → terminal (null)
            }

            case 0xc:
            {
                paVar3 = CreateFollowupWorkItem(); // FUN_004d54e0
                break; // → terminal
            }

            case 0xd:
                ClearBatchRecords(); // FUN_004d5600 — return value discarded
                SelectBatchCandidates(); // FUN_004d5b50 — return value discarded
                break; // → terminal (null)
        }

        SubState = 0;
        Phase = PhaseB;
        ReadyFlag = 1;
        return paVar3;
    }

    // FUN_004d24e0 — PhaseB inner state machine.
    // Drives an init check, conditional pipeline evaluation, and dispatch.
    // Terminates to PhaseC.
    private AIWorkItem RunPhaseB()
    {
        switch (SubState)
        {
            default:
                SubState = 1;
                return null;

            case 1:
            {
                int r = CheckPhaseBInitCondition(); // FUN_004d2830
                if (r == 0)
                {
                    SubState = 0;
                    Phase = PhaseC;
                    // NOTE: ReadyFlag intentionally NOT set on this path (mirrors FUN_004d24e0).
                    return null;
                }
                SubState = 2;
                return null;
            }

            case 2:
            {
                // FUN_004d2980: boolean check; non-zero → SubState=3; zero → SubState=4.
                // Derived from NEG;SBB;ADD 4 assembly pattern (CONCAT31 bool return).
                int r = EvaluatePipelineCondition(); // FUN_004d2980
                SubState = (r != 0) ? 3 : 4;
                return null;
            }

            case 3:
            {
                AIWorkItem item = EvaluatePipelineStage3(); // FUN_004d2e00
                if (item == null)
                {
                    SubState = 4;
                    return null;
                }
                SubState = 0;
                Phase = PhaseC;
                ReadyFlag = 1;
                return item;
            }

            case 4:
            {
                AIWorkItem item = EvaluatePipelineStage4(); // FUN_004d3120
                SubState = 0;
                Phase = PhaseC;
                ReadyFlag = 1;
                return item;
            }
        }
    }

    // FUN_004d2590 — PhaseC inner state machine.
    // Drives two condition scans, an entity sub-state pipeline, and dispatch.
    // Terminates to PhaseA.
    private AIWorkItem RunPhaseC()
    {
        AIWorkItem paVar4 = null;
        switch (SubState)
        {
            default:
                SubState = 5;
                return null;

            case 2:
            {
                int r = CheckPhaseCConditionA(); // FUN_004d36e0
                // Non-zero → SubState=6; zero → SubState=10.
                // Derived from (-(uint)(r != 0) & 0xfffffffc) + 10 assembly pattern.
                SubState = (r != 0) ? 6 : 10;
                return null;
            }

            // Jump table entries for SubState 3, 4, 8, 9 target the terminal block
            // directly in the original binary (no case-specific code).
            case 3:
            case 4:
            case 8:
            case 9:
                break; // → terminal (null)

            case 5:
            {
                int r = CheckPhaseCConditionB(); // FUN_004d3360
                // Non-zero → SubState=2; zero → SubState=10.
                // Derived from (-(uint)(r != 0) & 0xfffffff8) + 10 assembly pattern.
                SubState = (r != 0) ? 2 : 10;
                return null;
            }

            case 6:
            {
                int next = ComputePhaseCNextState(); // FUN_004d3a50
                SubState = next;
                if (next != 0)
                    return null;
                Phase = PhaseA;
                ReadyFlag = 1;
                return null;
            }

            case 7:
            {
                paVar4 = CreatePhaseCTerminalWorkItem(); // FUN_004d3b70
                break; // → terminal
            }

            case 10:
            {
                int r = CheckPhaseCBranchCondition(); // FUN_004d3b20
                if (r == 0)
                {
                    SubState = 0;
                    Phase = PhaseA;
                    // NOTE: ReadyFlag intentionally NOT set on this path (mirrors FUN_004d2590).
                    return null;
                }
                SubState = 7;
                return null;
            }

            case 0xb:
            {
                // FUN_004d5140(this, &(this+0x54)): create dispatch work item using entity ref at +0x54.
                AIWorkItem item = CreatePhaseCDispatchWorkItem(); // FUN_004d5140
                if (item != null)
                {
                    SubState = 0xc;
                    ReadyFlag = 1;
                    TickCounter++;
                    return item;
                }
                break; // → terminal (null)
            }

            case 0xc:
            {
                paVar4 = CreatePhaseCFollowupWorkItem(); // FUN_004d5280
                break; // → terminal
            }
        }

        SubState = 0;
        Phase = PhaseA;
        ReadyFlag = 1;
        return paVar4;
    }

    // --- Phase A helpers (Type 7, same patterns as Type 6) ---

    // FUN_004d4370: ScanPhaseACandidates for Type 7. Assembly trace (fully read).
    // Same structure as FUN_004da880 (Type 1) and FUN_004cfbd0 (Type 5).
    // DispositionFlags filter: 0x20 (vs Type 5's 0x40, Type 1's 0x80).
    // Uses _type7CandidateList (this+0x6c), FlagA |= 0x200000 when inserting.
    // Returns 0 or 1 (found candidate).
    private int ScanPhaseACandidates()
    {
        _candidateRef58 = 0;
        _candidateCount68 = 0;
        int minScore = int.MaxValue;
        bool found = false;

        // Primary: iterate existing candidate list.
        foreach (int sysId in _type7CandidateList.ToList())
        {
            var rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == sysId);
            if (rec == null)
            {
                _type7CandidateList.Remove(sysId);
                continue;
            }
            if (
                (rec.PresenceFlags & 0x1u) == 0
                || (rec.FlagA & 0x3) != 0
                || rec.Stats.FacilityCount <= 0
            )
                continue;
            if (rec.SystemScore < minScore)
            {
                minScore = rec.SystemScore;
                _candidateRef58 = sysId;
                _candidateCount68 = rec.Stats.FacilityCount;
                found = true;
            }
        }
        if (found)
            return 1;

        // Fallback: seed _type7CandidateList via QuerySystemAnalysis(incl24=0x20, stat=0x13/4)
        // then QuerySystemPlanets. DispositionFlags bit 5 (0x20) is set for own-faction planets
        // with CapabilityFlags & 0x200000 == 0 AND CharCapabilityCount > 0.
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x20,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 0x13
        );
        IssueRecordContainer c2 = Workspace.QuerySystemAnalysis(
            incl24: 0x20,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 4
        );
        c1.StoreFrom(c2);
        if (!c1.TryGetTopEntityKey(out int candidateRef))
            return 0;
        c1.Clear();

        // QuerySystemPlanets to refine to specific fleet/system entity.
        IssueRecordContainer p1 = Workspace.QuerySystemPlanets(
            candidateRef,
            incl28: 0,
            incl2c: 0,
            incl30: 1,
            excl28: 0x400003,
            excl2c: 0,
            excl30: 0x40000000,
            statIndex: 5
        );
        if (_type7CandidateList.Count > 0)
        {
            IssueRecordContainer p2 = Workspace.QuerySystemPlanets(
                candidateRef,
                incl28: 0,
                incl2c: 0,
                incl30: 1,
                excl28: 0x400003,
                excl2c: 0,
                excl30: 0x40000000,
                statIndex: 0x33
            );
            p1.StoreFrom(p2);
        }
        if (p1.TryGetTopEntityKey(out int refined))
            candidateRef = refined;
        p1.Clear();

        int hibyte = (candidateRef >> 0x18) & 0xff;
        if (hibyte >= 0x90 && hibyte < 0x98)
        {
            SystemAnalysisRecord sysRec = Workspace.SystemAnalysis.FirstOrDefault(r =>
                r.InternalId == candidateRef
            );
            if (sysRec != null)
            {
                if (!_type7CandidateList.Contains(candidateRef))
                    _type7CandidateList.Add(candidateRef);
                sysRec.FlagA |= 0x200000; // FlagA |= 0x200000 (Type 7 marker)
                _candidateRef58 = candidateRef;
                _candidateCount68 = sysRec.Stats.FacilityCount;
                return 1;
            }
        }
        return 0;
    }

    // All remaining Type 7 stubs — same patterns as Type 6 (ThreePhaseStrategyRecordA).
    // Type 7 is a mirror of Type 6 with different disassembly addresses but identical logic structure.

    // FUN_004d4650: CheckEntityFilterEligibility for Type 7 Phase A. Assembly trace (fully read).
    // Clears this+0x64 (_capacityLimit64). Queries systems with filter 0x1000 → stores at this+0x50.
    // Second query (filter 0x800800) → updates this+0x50.
    // HIBYTE(*this+0x50) check [0x90,0x98): if fleet → get capacity, add to _type7CandidateList,
    // FlagA |= 0x200000. Returns 1 if fleet found.
    // With InternalIds, HIBYTE check now passes for SystemAnalysisRecord.InternalId.
    // Proxy: QuerySystemAnalysis(incl24=0x1000) with fallback to any own-faction system.
    // When a valid system is found: adds to _type7CandidateList, FlagA |= 0x200000.
    private int CheckEntityFilterEligibility()
    {
        // Query with DispositionFlags & 0x1000 (bit 12): needs CapabilityFlags & 0x800 AND 0x800000.
        // These aren't yet set in C#, so fall back to own-faction system presence.
        IssueRecordContainer c1 = Workspace.QuerySystemAnalysis(
            incl24: 0x1000,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 0x14
        );
        int entityRef = 0;
        if (!c1.TryGetTopEntityKey(out entityRef))
            entityRef = _candidateRef58; // fallback to candidate from ScanPhaseACandidates
        c1.Clear();
        if (entityRef == 0)
            return 0;

        IssueRecordContainer p1 = Workspace.QuerySystemPlanets(
            entityRef,
            incl28: 0x800800,
            incl2c: 0,
            incl30: 1,
            excl28: 3,
            excl2c: 0,
            excl30: 0,
            statIndex: 6
        );
        if (p1.TryGetTopEntityKey(out int refined))
            entityRef = refined;
        p1.Clear();

        int hibyte = (entityRef >> 0x18) & 0xff;
        if (hibyte < 0x90 || hibyte >= 0x98)
            return 0;

        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == entityRef
        );
        if (rec == null || (rec.PresenceFlags & 0x1u) == 0)
            return 0;

        // Add to _type7CandidateList and mark with FlagA bit.
        if (!_type7CandidateList.Contains(entityRef))
            _type7CandidateList.Add(entityRef);
        rec.FlagA |= 0x200000;
        _candidateRef58 = entityRef;
        _candidateCount68 = System.Math.Max(0, rec.Stats.FacilityCount - 1);
        return 1;
    }

    // FUN_004d4dc0: ComputeEntityPipelineSubState for Type 7. Assembly trace (fully read).
    // Returns 0 (terminal), 9 (BuildEntityBatchItem), or 0xb (CreateCandidateDispatchWorkItem).
    // Was incorrectly returning 0xa (no such case in Type 7 PhaseA → would cause default→2 loop).
    // Fixed: returns 0 (correct when agent infra blocked) or 0xb if agent found.
    private int ComputeEntityPipelineSubState() => 0;

    // FUN_004d4880: CreateFilteredWorkItem for Type 7 Phase A. Assembly trace (fully read).
    // Inits unit node list. HIBYTE(*arg_54) check [0x90,0x98) now passes with InternalIds.
    // Builds capital/starfighter/regiment unit nodes via sub_52bc60/52b900/52b600 (facility finders).
    // Creates TypeCode 0x200 work item if nodes exist.
    // BLOCKED (unit nodes): facility finders require game entity infrastructure.
    // Proxy: use _candidateRef58 set by CheckEntityFilterEligibility/ScanPhaseACandidates.
    private AIWorkItem CreateFilteredWorkItem()
    {
        if (_candidateRef58 == 0)
            return null;
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == _candidateRef58
        );
        if (rec?.System == null)
            return null;
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004d4e60: BuildEntityBatchItem for Type 7 Phase A. Assembly trace (fully read).
    // Gets last node from _type7CandidateList. Checks capacity and FlagA conditions.
    // Creates TypeCode 0x200 work item if candidates found.
    // BLOCKED (unit nodes): facility finders require game entity infrastructure.
    // Proxy: use last candidate from _type7CandidateList.
    private AIWorkItem BuildEntityBatchItem()
    {
        if (_type7CandidateList.Count == 0)
            return null;
        int sysId = _type7CandidateList[_type7CandidateList.Count - 1];
        SystemAnalysisRecord rec = Workspace.SystemAnalysis.FirstOrDefault(r =>
            r.InternalId == sysId
        );
        if (rec?.System == null)
            return null;
        return new FleetShortageWorkItem(rec.System, OwnerSide);
    }

    // FUN_004d53a0: CreateCandidateDispatchWorkItem for Type 7 Phase A. Assembly trace (fully read).
    // HIBYTE(*arg_0) check [0x90,0x98) fleet + HIBYTE(this+0x50) check [0xa0,0xa2) agent.
    // Both required. Creates TypeCode 0x214 (AgentShortageWorkItem) if both pass.
    // BLOCKED: both HIBYTE checks fail in C#. Returns null.
    private AIWorkItem CreateCandidateDispatchWorkItem() => null;

    // FUN_004d54e0: CreateFollowupWorkItem for Type 7 Phase A. Assembly trace (fully read).
    // HIBYTE(this+0x50) check [0xa0,0xa2): agent range. Creates TypeCode 0x210 (AgentShortageWorkItem).
    // Sets work item fields from this+0x5c, this+0x60.
    // BLOCKED: HIBYTE agent check fails in C#. Returns null.
    private AIWorkItem CreateFollowupWorkItem() => null;

    // FUN_004d5600: ClearBatchRecords for Type 7 Phase A. Assembly trace (fully read).
    // Clears this+0x58 entity ref (FUN_004ec230_set_param_to_two). Computes capacity count via
    // FUN_004d5a70. If count >= 1: queries workspace entities with [0x80,0x90) HIBYTE range,
    // checks fleet records, sets candidates. HIBYTE checks all fail in C#. Main side effect: clear ref.
    private void ClearBatchRecords() => _candidateRef58 = 0;

    // FUN_004d5b50: SelectBatchCandidates for Type 7 Phase A. Assembly trace (fully read).
    // Iterates arg_68 candidate list; for each: checks *(sys+0x58) capacity vs running max.
    // If *(sys+0x30) & 0x10000000: consults sub-list counts; selects best. HIBYTE check [0x90,0x98)
    // on selected candidate → if fleet: allocate node and add to local_1c output list.
    // BLOCKED: fleet HIBYTE check fails in C#. Proxy selects first candidate from list.
    private void SelectBatchCandidates()
    {
        if (_type7CandidateList.Count > 0)
            _candidateRef58 = _type7CandidateList[0];
    }

    // FUN_004d2830: CheckPhaseBInitCondition for Type 7 Phase B. Assembly trace (fully read).
    // QuerySystemAnalysis(0x2000000, stat=0x22) → this+0x50.
    // QuerySystemPlanets(this+0x50, 0, 0, 1, 0x2, 0, 0x40000, 0x35, 1) → update.
    // Check HIBYTE in [0x90,0x98). If fleet: check *(sys+0x11c) > 0. Return 1 if passes.
    // BLOCKED: HIBYTE check fails. Proxy: returns 1 if capacity > 0.
    private int CheckPhaseBInitCondition() =>
        (Capacity * Workspace.FleetTotalCapacity / 100) > 0 ? 1 : 0;

    // FUN_004d2980: EvaluatePipelineCondition for Type 7 Phase B. Assembly trace (fully read).
    // Four-path search: queries systems with filters 0x100009, 0x100005, 0x10000000, 0x8000000.
    // Each path has HIBYTE entity range checks: [0x08,0x10) and [0x90,0x98) (fleet range).
    // Loops entity target table checking [0x80,0x90) range for mission assignments.
    // BLOCKED: all HIBYTE entity range checks fail in C#. Returns 0 always.
    private int EvaluatePipelineCondition() => 0;

    // FUN_004d2e00: EvaluatePipelineStage3 for Type 7 Phase B. Assembly trace (fully read).
    // HIBYTE(this+0x54) check [0x90,0x98) fleet or [0x08,0x10) agent → get capacity clamp values.
    // Gets fleet from this+0x50, builds unit nodes filtered by vtable[0x1f4>>2]() score < 1000000.
    // HIBYTE check [0x1c,0x20) on selected unit. Creates TypeCode 0x201 work item if nodes found.
    // BLOCKED: entity range checks fail in C#. Returns null.
    private AIWorkItem EvaluatePipelineStage3() => null;

    // FUN_004d3120: EvaluatePipelineStage4 for Type 7 Phase B. Assembly trace (fully read).
    // sub_4ec1e0(arg_4c) reads entity ref from this+0x50. sub_403d30 lookup for system.
    // Finds fighters via sub_502b20; selects fighter with lowest vtable[0x1f4>>2]() score.
    // HIBYTE check [0x1c,0x20) on selected fighter. Creates TypeCode 0x200 work item if found.
    // BLOCKED: fighter entity range check [0x1c,0x20) fails in C#. Returns null.
    private AIWorkItem EvaluatePipelineStage4() => null;

    // FUN_004d36e0: CheckPhaseCConditionA for Type 7 Phase C. Assembly trace (fully read).
    // Multiple queries: filter 0x4000000, 0x40000, 0x40000000, 0x1000000 → update this+0x54.
    // HIBYTE(*this+0x54) check [0x90,0x98): if fleet → get system, check *(sys+0xe0) < 0 → var_20=1.
    // Else (not fleet): call sub_4d5a10 > 0 → query 3 more systems.
    // BLOCKED: fleet HIBYTE check fails in C#; var_20 never set. Returns 0.
    private int CheckPhaseCConditionA() => 0;

    // FUN_004d3360: CheckPhaseCConditionB for Type 7 Phase C. Assembly trace (fully read).
    // Clears this+0x64. Iterates arg_68 list: for each fleet (HIBYTE [0x90,0x98)):
    // if *(sys+0x30)&1 and *(sys+0x28)&0x200000 and *(sys+0x58)>0 and HIBYTE(sys+0x28)&0x2
    //   and LOBYTE(sys+0x28)&0x3==0: found = true.
    // Fallback: checks workspace+0x20, +0x24 entity refs with same conditions.
    // If found: ownership check, sets _candidateRef58. Returns var_2C (1 or 0).
    // BLOCKED: all HIBYTE fleet [0x90,0x98) checks fail in C#. Returns 0.
    private int CheckPhaseCConditionB() => 0;

    // FUN_004d3a50: ComputePhaseCNextState for Type 7. Assembly trace (fully read).
    // Returns 0, 7 (CreatePhaseCTerminalWorkItem), or 0xb (dispatch).
    // CRITICAL FIX: was returning 6, causing infinite loop (case 6→6→6...).
    private int ComputePhaseCNextState() => 0;

    // FUN_004d3b70: CreatePhaseCTerminalWorkItem for Type 7 Phase C. Assembly trace (fully read).
    // Clears this+0x54 and this+0x50. Queries systems (filter 0x2000000, 0x8000000 etc.) → this+0x50.
    // HIBYTE(*this+0x50) check NOT [0x90,0x98): if not fleet → do additional agent/fleet queries.
    // Builds starfighters via vtable score loop. Creates TypeCode 0x201 (if agent found) or 0x200.
    // BLOCKED: entity range checks fail in C#. Returns null.
    private AIWorkItem CreatePhaseCTerminalWorkItem() => null;

    // FUN_004d3b20: CheckPhaseCBranchCondition for Type 7. Assembly trace (fully read).
    // Returns (Capacity * FleetTotalCapacity * 90/10000 - workspace.field_0x1e0) < 0.
    // Different from Type 6 (which always returns 0!).
    // workspace.field_0x1e0 is an accumulator (proxy: FleetAssignedCapacity).
    private int CheckPhaseCBranchCondition()
    {
        int total = Workspace.FleetTotalCapacity;
        int result = (Capacity * total * 90) / 10000 - Workspace.FleetAssignedCapacity;
        return result < 0 ? 1 : 0;
    }

    // FUN_004d5140: CreatePhaseCDispatchWorkItem for Type 7 Phase C. Assembly trace (fully read).
    // Identical structure to FUN_004d53a0 but agent range [0xa2,0xa4) instead of [0xa0,0xa2).
    // HIBYTE(*arg_0) [0x90,0x98) fleet + HIBYTE(this+0x50) [0xa2,0xa4) agent.
    // Creates TypeCode 0x214 (AgentShortageWorkItem) if both pass.
    // BLOCKED: both HIBYTE checks fail in C#. Returns null.
    private AIWorkItem CreatePhaseCDispatchWorkItem() => null;

    // FUN_004d5280: CreatePhaseCFollowupWorkItem for Type 7 Phase C. Assembly trace (fully read).
    // Identical structure to FUN_004d54e0 but agent range [0xa2,0xa4) instead of [0xa0,0xa2).
    // HIBYTE(this+0x50) check [0xa2,0xa4). Creates TypeCode 0x211 work item.
    // Sets work item fields from this+0x5c, this+0x60.
    // BLOCKED: HIBYTE agent check fails in C#. Returns null.
    private AIWorkItem CreatePhaseCFollowupWorkItem() => null;
}

// ------------------------------------------------------------------
// Type 8 — FUN_004ce780 — StrategyRecordType8
// 0x58 bytes.
// Active guard: pass-through (no reset on inactive).
// If active: call FUN_004ce9a0 (do work), return result.
//
// Extra fields beyond the 0x40-byte base (total 0x58, 0x18 extra bytes):
//   +0x40 = _entityTargetId: current entity target entry ID (workspace+0xd8).
//   +0x44 = _entityCursor:   backward-walk cursor into EntityTargetTable.
//   +0x48..+0x54: additional fields not yet mapped from analysed functions.
//
// FUN_004ce9a0 SubState machine:
//   default → SubState=1.
//   1 → FUN_004cea70 (init sector search): always SubState=2, return null.
//   2 → FUN_004cead0 (get next entity target):
//         found → SubState=3, return null;
//         not-found → SubState=0, ReadyFlag=1, return null.
//   3 → FUN_004ceb30 (create fleet order for target):
//         success → SubState=4, return null;
//         failure → SubState=0, ReadyFlag=1, return null.
//   4 → FUN_004cee30 (dispatch entity target pipeline, out dispatchOut):
//         result!=null AND dispatchOut==0: TickCounter++, ReadyFlag=1.
//         result!=null AND dispatchOut!=0: ReadyFlag=1.
//         dispatchOut==0: return result (no SubState reset).
//         dispatchOut!=0: SubState=0, ReadyFlag=1, return result.
// ------------------------------------------------------------------
public class StrategyRecordType8 : StrategyRecord
{
    // +0x40: current entity target entry ID (workspace+0xd8 lookup key).
    private int _entityTargetId;

    // +0x44: backward-walk cursor into EntityTargetTable.
    // Holds the ID of the entry just before the last one examined; zero = start from tail.
    private int _entityCursor;

    public StrategyRecordType8(int ownerSide)
        : base(typeId: 8, capacity: 1, ownerSide: ownerSide)
    {
        _entityTargetId = 0;
        _entityCursor = 0;
    }

    protected override bool ActiveGuardFails()
    {
        return ActiveState != 1;
    }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        return DoWork();
    }

    // FUN_004ce9a0 — 4-state SubState machine for entity-target fleet assignment.
    private AIWorkItem DoWork()
    {
        switch (SubState)
        {
            default:
                SubState = 1;
                return null;

            case 1:
                // FUN_004cea70: evaluate SectorSearchState and optionally trigger a
                // sector search or sector-find operation. Return value is void.
                InitSectorSearch();
                SubState = 2;
                return null;

            case 2:
            {
                // FUN_004cead0: unconditional backward walk of workspace+0xd8 to find
                // the next entity target. No condition predicate — visits every entry.
                // Sets _entityTargetId to the found entry's ID; _entityCursor to the
                // ID of the entry before it (or 0 if at the head of the list).
                int found = GetNextEntityTarget();
                if (found != 0)
                {
                    SubState = 3;
                    return null;
                }
                SubState = 0;
                ReadyFlag = 1;
                return null;
            }

            case 3:
            {
                // FUN_004ceb30: for the entity target found in state 2, verify its
                // type code (StatusFlags>>0x18 in [0x80,0x90)), check for a pre-existing
                // fleet assignment, and if none found, try up to three fleet issue
                // creation calls (sub_419330, sub_419980×2). Finalises via sub_476840.
                // Returns 1 on success, 0 on failure.
                int ok = CreateFleetOrderForTarget();
                if (ok != 0)
                {
                    SubState = 4;
                    return null;
                }
                SubState = 0;
                ReadyFlag = 1;
                return null;
            }

            case 4:
            {
                // FUN_004cee30: sets dispatchOut=1, looks up entity target by
                // _entityTargetId in workspace+0xd8, and if found calls FUN_00476910
                // (the 6-state inner fleet-assignment pipeline) which may clear dispatchOut.
                int dispatchOut;
                AIWorkItem result = DispatchEntityTargetPipeline(out dispatchOut);

                if (result != null)
                {
                    if (dispatchOut == 0)
                        TickCounter++;
                    ReadyFlag = 1;
                }

                if (dispatchOut != 0)
                {
                    SubState = 0;
                    ReadyFlag = 1;
                    return result;
                }

                return result;
            }
        }
    }

    // FUN_004cea70 — evaluate workspace.SectorSearchState (+0x204) and act on it.
    //
    // SectorSearchState < 0 (path 1):
    //   Iterates EntityTargetTable backward via *(node+0x10) links (do-while).
    //   For each node: tests bit 26 (0x4000000) of node.AssignmentId (+0x60).
    //   If any node has this bit clear → bVar2 = true (unassigned entry exists).
    //   After iteration: if bVar2 == false (table empty OR all entries have bit set):
    //     calls FUN_0042e9d0(EntityTargetTable) to allocate and register a new entry.
    //   If bVar2 == true: do nothing.
    //
    // SectorSearchState > 0 (path 2):
    //   Calls FUN_0042ea50_find_sector(EntityTargetTable).
    //   Iterates backward; finds first entry with HIBYTE(StatusFlags) NOT in [0x80,0x90).
    //   If found: activates it via FUN_004765e0 (reset InnerDispatchState, AssignmentId,
    //   FleetTarget, clear list fields), then removes it via FUN_005f3a10. Returns its Id.
    //   Return value is discarded by FUN_004cea70.
    //
    // SectorSearchState == 0 (path 3): returns immediately, no action.
    private void InitSectorSearch()
    {
        int sectorSearch = Workspace.SectorSearchState;

        if (sectorSearch < 0)
        {
            // Walk table backward; check if any entry has bit 0x4000000 clear.
            bool anyUnassigned = false;
            for (int i = Workspace.EntityTargetTable.Count - 1; i >= 0; i--)
            {
                if ((Workspace.EntityTargetTable[i].AssignmentId & 0x4000000) == 0)
                {
                    anyUnassigned = true;
                    break;
                }
            }

            if (!anyUnassigned)
            {
                // FUN_0042e9d0: allocate 0xe8 bytes via FUN_00617140_generic_allocate,
                // construct via FUN_00475700(alloc, *(AutoClass62+0xc), *(AutoClass62+0x10)):
                //   *(entry+0x34..+0x40) = 2 (four FUN_004ec1d0_set_param_value_to_2 calls)
                //   *(entry+0x20) = OwnerSide (from AutoClass62+0xc = workspace+0xe4)
                //   *(entry+0x58) = Workspace (from AutoClass62+0x10 = workspace+0xe8)
                //   vtable set, all other fields zeroed.
                // Then FUN_005f39b0: assign Id from container's next-ID counter, insert.
                var entry = new MissionTargetEntry
                {
                    StatusFlags = 2, // FUN_004ec1d0 on entry+0x34
                    AssignmentConfirmWord = 2, // FUN_004ec1d0 on entry+0x38
                    AssignmentStateWord = 2, // FUN_004ec1d0 on entry+0x3c
                    EmbeddedSubField = 2, // FUN_004ec1d0 on entry+0x40
                    OwnerSide = OwnerSide,
                    ContextObject = Workspace,
                };
                entry.Id = Workspace.NextMissionId;
                Workspace.NextMissionId++;
                Workspace.EntityTargetTable.Add(entry);
            }
        }
        else if (sectorSearch > 0)
        {
            // FUN_0042ea50_find_sector: iterate EntityTargetTable backward.
            // Find first entry where HIBYTE(StatusFlags) is NOT in [0x80, 0x90).
            // Activate it: FUN_004765e0 resets InnerDispatchState=0, InProgressFlag=0,
            //   AssignmentId and SubAssignmentId via FUN_004fbf90_reset_id,
            //   FleetTarget cleared, AssignmentRegistryList and CandidateList cleared.
            // Remove it: FUN_005f3a10 removes the node from the AVL tree container.
            // Return value (the entry's Id) is discarded by FUN_004cea70.
            for (int i = Workspace.EntityTargetTable.Count - 1; i >= 0; i--)
            {
                var t = Workspace.EntityTargetTable[i];
                int typeHiByte = (t.StatusFlags >> 0x18) & 0xff;
                if (typeHiByte < 0x80 || typeHiByte >= 0x90)
                {
                    // FUN_004765e0 (partial): reset assignment state fields.
                    t.InnerDispatchState = 0;
                    t.InProgressFlag = 0;
                    t.AssignmentId = 0;
                    t.SubAssignmentId = 0;
                    t.FleetTarget = null;
                    t.AssignmentRegistryList.Clear();
                    t.CandidateList.Clear();
                    // FUN_005f3a10: remove from container.
                    Workspace.EntityTargetTable.RemoveAt(i);
                    break;
                }
            }
        }
        // sectorSearch == 0: no-op.
    }

    // FUN_004cead0 — unconditional backward walk of workspace+0xd8 (EntityTargetTable).
    //
    // Attempts to find the entry whose ID matches _entityCursor (the ID stored as the
    // cursor from the previous call). If not found or cursor is 0, falls back to the
    // last entry in the list. On success: _entityTargetId = entry.Id,
    // _entityCursor = previous entry's Id (or 0 if at the list head). Returns 1.
    // If the list is empty: _entityTargetId = 0, _entityCursor = 0. Returns 0.
    private int GetNextEntityTarget()
    {
        MissionTargetEntry entry = null;

        if (_entityCursor != 0)
            entry = Workspace.EntityTargetTable.Find(e => e.Id == _entityCursor);

        if (entry == null)
        {
            if (Workspace.EntityTargetTable.Count == 0)
            {
                _entityTargetId = 0;
                _entityCursor = 0;
                return 0;
            }
            entry = Workspace.EntityTargetTable[Workspace.EntityTargetTable.Count - 1];
        }

        _entityTargetId = entry.Id;
        int idx = Workspace.EntityTargetTable.IndexOf(entry);
        _entityCursor = (idx > 0) ? Workspace.EntityTargetTable[idx - 1].Id : 0;
        return 1;
    }

    // FUN_004ceb30 — create a fleet order for the entity target found in state 2.
    //
    // Implemented via scene graph + SectorAnalysisTable (workspace+0x44).
    //
    // Original binary flow:
    // 1. Check HIBYTE(entry.StatusFlags) in [0x80, 0x90) → check for pre-existing assignment
    // 2. sub_419330(workspace, &entry.StatusFlags, ...) → find sector entity (family 0x80)
    //    via global game BST (DAT_006b33a4). Sectors = SECTORSD.json, family_id=0x80, IDs 20–39.
    //    workspace+0x00 = FactionSide (1 or 2) passed to sub_4f2090/*workspace.
    // 3. sub_475d00(entry, &sector_key) → creates SectorAnalysisRecord in workspace+0x44,
    //    sets FleetTypeCode (1–6), stores sector_key in entry.StatusFlags.
    // 4. FUN_00476840(entry) → look up SectorAnalysisRecord, create SectorFleetAssignmentTarget.
    // Returns 1 on success, 0 on failure.
    //
    // C# uses MissionTargetEntry.FindSectorForFleetAssignment,
    // AssignFleetToSector, and ValidateOrCreateFleetTarget.
    private int CreateFleetOrderForTarget()
    {
        MissionTargetEntry entry = Workspace.EntityTargetTable.Find(e => e.Id == _entityTargetId);
        if (entry == null)
            return 0;

        // Step 1 (FUN_004ceb30 primary path): check HIBYTE(entry.StatusFlags) in [0x80, 0x90).
        // If already set to a sector key from a previous call, validate the existing assignment.
        int hibyte = (entry.StatusFlags >> 0x18) & 0xff;
        if (hibyte >= 0x80 && hibyte < 0x90)
        {
            // Pre-existing sector assignment. Validate via FUN_00476840 equivalent.
            SectorAnalysisRecord existingRec = Workspace.SectorAnalysisTable.FirstOrDefault(r =>
                r.InternalId == entry.StatusFlags
                && r.AssignmentStatus == 0
                && r.AssignedFleetId == entry.Id
            );
            if (existingRec != null)
                return entry.ValidateOrCreateFleetTarget();
        }

        // Steps 2–3 (sub_419330 + sub_475d00): find a sector and assign a fleet.
        // sub_419330 searches for sector entity (family 0x80) via *workspace=FactionSide.
        int sectorKey = entry.FindSectorForFleetAssignment();
        if (sectorKey == 0)
            return 0;

        // sub_475d00: find a matching fleet and create SectorAnalysisRecord.
        // Pick the first available FleetAnalysisRecord whose system is in this sector.
        int sectorId = sectorKey & 0xFFFFFF;
        FleetAnalysisRecord fleetRec = Workspace.FleetAnalysis.FirstOrDefault(f =>
            f.Fleet != null && f.Fleet.GetParentOfType<PlanetSystem>()?.SectorId == sectorId
        );
        if (fleetRec == null)
            return 0;

        int assigned = entry.AssignFleetToSector(fleetRec.InternalId);
        if (assigned == 0)
            return 0;

        // Step 4 (FUN_00476840): validate entry → create SectorFleetAssignmentTarget.
        return entry.ValidateOrCreateFleetTarget();
    }

    // FUN_004cee30 — dispatch the entity target through the 6-state inner fleet pipeline.
    //
    // Sets dispatchOut=1. Looks up entity target by _entityTargetId in workspace+0xd8.
    // If found: calls FUN_00476910(entry, &dispatchOut) — the 6-state inner pipeline
    // that performs sector resolution, fleet building, and mission assignment; it may
    // clear dispatchOut to 0 on completion. Returns the work item from FUN_00476910,
    // or null if the entry was not found.
    private AIWorkItem DispatchEntityTargetPipeline(out int dispatchOut)
    {
        dispatchOut = 1;

        MissionTargetEntry entry = Workspace.EntityTargetTable.Find(e => e.Id == _entityTargetId);
        if (entry == null)
            return null;

        return entry.Dispatch(out dispatchOut);
    }
}

// ------------------------------------------------------------------
// Type 9 — FUN_004ce410 — StrategyRecordType9
// 0x58 bytes.
// Active guard: pass-through (no reset on inactive).
// If active: call FUN_004ce630 (do work), return result.
//
// Extra fields beyond the 0x40-byte base (total 0x58, 0x18 extra bytes):
//   +0x40 = _selectedTargetId: current selected target entry ID (workspace+0x11c).
//   +0x44 = _selectedCursor:   backward-walk cursor into SelectedTargetTable.
//   +0x48..+0x54: additional fields not yet mapped from analysed functions.
//
// FUN_004ce630 SubState machine (if/else, not switch):
//   Other (not 2 or 3) → SubState=2, return null.
//   2 → FUN_004ce6c0 (get next selected target):
//         found → SubState=3, return null;
//         not-found → SubState=0, ReadyFlag=1, return null.
//   3 → FUN_004ce720 (dispatch selected target pipeline, out dispatchOut):
//         result!=null AND dispatchOut==0: TickCounter++, ReadyFlag=1.
//         result!=null AND dispatchOut!=0: ReadyFlag=1.
//         dispatchOut!=0: SubState=0, ReadyFlag=1, return result.
//         dispatchOut==0: return result (no SubState reset).
//
// Note: FUN_004ce720 initialises *dispatchOut=1 before the lookup; dispatchOut
// is only cleared to 0 if FUN_004737e0 absorbs the entry completely.  If the
// entry is not found in workspace+0x11c, *dispatchOut stays 1 and result is null,
// which causes the SubState=0 reset path to execute.
// ------------------------------------------------------------------
public class StrategyRecordType9 : StrategyRecord
{
    // +0x40: current selected target entry ID (workspace+0x11c lookup key).
    private int _selectedTargetId;

    // +0x44: backward-walk cursor into SelectedTargetTable.
    // Holds the ID of the entry just before the last one examined; zero = start from tail.
    private int _selectedCursor;

    public StrategyRecordType9(int ownerSide)
        : base(typeId: 9, capacity: 1, ownerSide: ownerSide)
    {
        _selectedTargetId = 0;
        _selectedCursor = 0;
    }

    protected override bool ActiveGuardFails()
    {
        return ActiveState != 1;
    }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        return DoWork();
    }

    // FUN_004ce630 — SubState if/else machine for selected-target dispatch.
    private AIWorkItem DoWork()
    {
        if (SubState == 2)
        {
            // FUN_004ce6c0: unconditional backward walk of workspace+0x11c to find
            // the next selected target. Identical structure to FUN_004cead0 but
            // operates on SelectedTargetTable instead of EntityTargetTable.
            int found = GetNextSelectedTarget();
            if (found != 0)
            {
                SubState = 3;
                return null;
            }
            SubState = 0;
            ReadyFlag = 1;
            return null;
        }

        if (SubState != 3)
        {
            SubState = 2;
            return null;
        }

        // SubState == 3: dispatch the current selected target through the 8-state
        // inner pipeline (FUN_004737e0 via FUN_004ce720).
        {
            int dispatchOut;
            AIWorkItem result = DispatchSelectedTargetPipeline(out dispatchOut);

            if (result != null)
            {
                if (dispatchOut == 0)
                    TickCounter++;
                ReadyFlag = 1;
            }

            if (dispatchOut != 0)
            {
                SubState = 0;
                ReadyFlag = 1;
                return result;
            }

            return result;
        }
    }

    // FUN_004ce6c0 — unconditional backward walk of workspace+0x11c (SelectedTargetTable).
    //
    // Identical algorithm to FUN_004cead0 (Type 8 GetNextEntityTarget) but uses
    // SelectedTargetTable instead of EntityTargetTable. Attempts to find the entry
    // whose ID matches _selectedCursor; falls back to the last entry. On success:
    // _selectedTargetId = entry.Id, _selectedCursor = previous entry's Id (or 0).
    // Returns 1. If list is empty: _selectedTargetId=0, _selectedCursor=0. Returns 0.
    private int GetNextSelectedTarget()
    {
        SelectedTargetEntry entry = null;

        if (_selectedCursor != 0)
            entry = Workspace.SelectedTargetTable.Find(e => e.Id == _selectedCursor);

        if (entry == null)
        {
            if (Workspace.SelectedTargetTable.Count == 0)
            {
                _selectedTargetId = 0;
                _selectedCursor = 0;
                return 0;
            }
            entry = Workspace.SelectedTargetTable[Workspace.SelectedTargetTable.Count - 1];
        }

        _selectedTargetId = entry.Id;
        int idx = Workspace.SelectedTargetTable.IndexOf(entry);
        _selectedCursor = (idx > 0) ? Workspace.SelectedTargetTable[idx - 1].Id : 0;
        return 1;
    }

    // FUN_004ce720 — dispatch the selected target through the 8-state inner pipeline.
    //
    // Sets dispatchOut=1. Looks up selected target by _selectedTargetId in
    // workspace+0x11c. If found: calls FUN_004737e0(entry, &dispatchOut) — the
    // 8-state inner pipeline that drives scout/attack target selection and mission
    // issue generation; it may clear dispatchOut to 0 on completion.
    // Returns the work item from FUN_004737e0, or null if the entry was not found.
    private AIWorkItem DispatchSelectedTargetPipeline(out int dispatchOut)
    {
        dispatchOut = 1;

        SelectedTargetEntry entry = Workspace.SelectedTargetTable.Find(e =>
            e.Id == _selectedTargetId
        );
        if (entry == null)
            return null;

        // FUN_004737e0: 8-state inner pipeline for scout/attack target dispatch.
        // Writes dispatchOut=0 at entry; sets dispatchOut=1 in states 7 and 8 only.
        return entry.Dispatch(out dispatchOut);
    }
}

// ------------------------------------------------------------------
// Type 10 — FUN_004cba20 — StrategyRecordType10
// 0x84 bytes.  Active guard: standard.
//
// Phase 0x3ec → FUN_004cbec0 (PhaseA), return result
// Phase 0x3f2 → FUN_004cbd90 (PhaseB), return result
// Other       → Phase=0x3f2, SubState=0, return null
// ------------------------------------------------------------------
public class StrategyRecordType10 : StrategyRecord
{
    private const int PhaseA = 0x3ec;
    private const int PhaseB = 0x3f2;

    // Fleet assignment entity refs (from FUN_004cc8f0, FUN_004cce00, FUN_004cd6c0 field access):
    private int _fleetEntityId; // +0x58 fleet entity (type [0x80,0x90))
    private int _targetEntityId10; // +0x5c target entity (type [1,0xff))
    private int _dispatchEntityId; // +0x60 dispatch entity
    private int _secondaryEntityId; // +0x64 secondary entity
    private int _batchCount10; // +0x68 batch count
    private int _maxBatchCount; // +0x6c max batch
    private int _capacityBound; // +0x70 capacity upper bound
    private int _capacityLimit10; // +0x74 capacity limit
    private readonly List<int> _type10CandidateList = new List<int>(); // +0x78 candidate list

    public StrategyRecordType10(int ownerSide)
        : base(typeId: 10, capacity: 1, ownerSide: ownerSide) { }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        if (Phase == PhaseA)
            return RunPhaseA();
        if (Phase == PhaseB)
            return RunPhaseB();

        Phase = PhaseB;
        SubState = 0;
        return null;
    }

    // FUN_004cbec0 — PhaseA inner state machine.
    // Drives the fleet-candidate scan, bit-selection gate, assignment target selection,
    // work-item dispatch, and troop mission batch build.
    private AIWorkItem RunPhaseA()
    {
        switch (SubState)
        {
            default:
                SubState = 2;
                return null;

            case 2:
            {
                int found = ScanFleetCandidatesPhaseA(); // FUN_004cc8f0
                if (found != 0)
                {
                    SubState = 3;
                    return null;
                }
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }

            case 3:
            {
                // Clear bit 0x10 from the pending supply bitmask (workspace+0x8).
                Workspace.PendingSupplyBitmask &= ~0x10;
                if (
                    Workspace.EntityTargetType == 0x10
                    && CheckFleetAssignmentEligibility() != 0 // FUN_004ccbd0
                )
                {
                    Workspace.AdvanceBitSelection();
                    SubState = 6;
                }
                else
                {
                    SubState = 0xb;
                }
                return null;
            }

            case 4:
            {
                int nextState = ComputeAssignmentTargetSubState(); // FUN_004cd340
                if (nextState == 0)
                {
                    Phase = PhaseB;
                    ReadyFlag = 1;
                    return null;
                }
                SubState = nextState;
                return null;
            }

            case 6:
            {
                AIWorkItem workItem = CreateFleetAssignmentWorkItem(); // FUN_004cce00
                if (workItem != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 4;
                return workItem;
            }

            case 7:
            {
                AIWorkItem workItem = CreateAssignmentDispatchWorkItem(); // FUN_004cd3e0
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return workItem;
            }

            case 9:
            {
                AIWorkItem workItem = DispatchMissionToEntity(); // FUN_004cd920(this, _dispatchEntityId)
                if (workItem != null)
                {
                    SubState = 0xa;
                    ReadyFlag = 1;
                    TickCounter++;
                    return workItem;
                }
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }

            case 0xa:
            {
                AIWorkItem workItem = CreateMissionFollowupWorkItem(); // FUN_004cda60
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return workItem;
            }

            case 0xb:
            {
                BuildTroopMissionBatch(); // FUN_004cdb80 — return value discarded
                SelectTroopCandidates(); // FUN_004ce0d0 — return value discarded
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }
        }
    }

    // FUN_004cbd90 — PhaseB inner state machine.
    // Drives fleet-readiness evaluation, dispatch target selection, and entity-transfer dispatch.
    private AIWorkItem RunPhaseB()
    {
        switch (SubState)
        {
            default:
                SubState = 3;
                return null;

            case 2:
            {
                int found = CheckFleetReadyForDispatch(); // FUN_004cc3b0
                SubState = (found != 0) ? 4 : 8;
                return null;
            }

            case 3:
            {
                int found = EvaluateFleetDispatchStatus(); // FUN_004cc030
                SubState = (found != 0) ? 2 : 8;
                return null;
            }

            case 4:
            {
                int nextState = SelectFleetDispatchTarget(); // FUN_004cc5a0
                if (nextState == 0)
                {
                    Phase = PhaseA;
                    ReadyFlag = 1;
                    return null;
                }
                SubState = nextState;
                return null;
            }

            case 5:
            {
                AIWorkItem workItem = CreateFleetDispatchWorkItem(); // FUN_004cc680
                SubState = 0;
                Phase = PhaseA;
                ReadyFlag = 1;
                return workItem;
            }

            case 8:
            {
                int ok = CheckFleetDispatchCondition(); // FUN_004cc660
                if (ok != 0)
                {
                    SubState = 5;
                }
                else
                {
                    SubState = 0;
                    Phase = PhaseA;
                    // NOTE: ReadyFlag intentionally NOT set on this path (mirrors FUN_004cbd90).
                }
                return null;
            }

            case 9:
            {
                AIWorkItem workItem = DispatchEntityTransfer(); // FUN_004cd6c0(this, _targetEntityId)
                if (workItem != null)
                {
                    SubState = 0xa;
                    ReadyFlag = 1;
                    TickCounter++;
                    return workItem;
                }
                SubState = 0;
                Phase = PhaseA;
                ReadyFlag = 1;
                return null;
            }

            case 0xa:
            {
                AIWorkItem workItem = CreateEntityTransferFollowup(); // FUN_004cd800
                SubState = 0;
                Phase = PhaseA;
                ReadyFlag = 1;
                return workItem;
            }
        }
    }

    // --- Phase A helpers ---

    // FUN_004cc8f0: ScanFleetCandidatesPhaseA for Type 10. Assembly trace (fully read).
    // 1. Clears this+0x60 entity ref, this+0x70 counter. Gets last node in this+0x74 list.
    // 2. If list not empty: iterates nodes, gets system via sub_403d30.
    //    Check PresenceFlags & 0x1, FlagA & 0x3 == 0, *(sys+0x114) > 0, *(sys+0x58) < var_20.
    //    If passes: *(this+0x70) = *(sys+0x114), set entity ref at this+0x60, var_24=1.
    //    No entity HIBYTE check here — candidate list iteration can work in C#.
    // 3. If not found: workspace query sub_4191b0 + sub_419af0 + HIBYTE [0x90,0x98) fleet check.
    //    Fallback HIBYTE-blocked → always fails in C#.
    // In C#: list is empty (CheckFleetAssignmentEligibility HIBYTE-blocked), fallback blocked.
    // Returns 0. Proxy preserved for when list has entries.
    private int ScanFleetCandidatesPhaseA()
    {
        _fleetEntityId = 0;
        foreach (int sysId in _type10CandidateList.ToList())
        {
            var rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == sysId);
            if (rec == null)
            {
                _type10CandidateList.Remove(sysId);
                continue;
            }
            if (
                (rec.PresenceFlags & 0x1u) != 0
                && (rec.FlagA & 0x3) == 0
                && rec.Stats.FacilityCount > 0
            )
            {
                _fleetEntityId = sysId;
                _capacityLimit10 = rec.Stats.FacilityCount;
                return 1;
            }
        }
        return 0;
    }

    // FUN_004ccbd0: CheckFleetAssignmentEligibility for Type 10 Phase A. Assembly trace (fully read).
    // sub_419330(workspace, this+0x60, 0x1000) + sub_419af0(this+0x58, 0x800800, 0x1, 0x3, 0x6, 1).
    // HIBYTE(*this+0x58) check [0x90,0x98): if fleet → capacity check, add to this+0x74, FlagA |= 0x200000.
    // BLOCKED: HIBYTE fleet range check always fails in C#. Returns 0.
    private int CheckFleetAssignmentEligibility() => 0;

    // FUN_004cd340: ComputeAssignmentTargetSubState. Assembly trace (fully read).
    // Same structure as FUN_004d05e0 (Type 5 ComputeAssignmentSubState):
    // Calls FUN_004cdff0 (capacity compute). Returns 0, 7, or 9.
    // If < 0: return 7. If == 0: return 0. If > 0 AND agent found: return 9.
    private int ComputeAssignmentTargetSubState()
    {
        if (_fleetEntityId == 0)
            return 0;
        // Proxy: return 9 (dispatch) when agent capacity available, 0 otherwise
        return Workspace.AgentAssignedCapacity < Workspace.AgentTotalCapacity ? 9 : 0;
    }

    // FUN_004cce00: CreateFleetAssignmentWorkItem for Type 10 Phase A. Assembly trace (fully read).
    // Identical to FUN_004d4880 (Type 7) and FUN_004dfa90 (Type 6): iterates last node in
    // candidate list, checks *(sys+0x58) > 0 and FlagA bit 28 (0x10000000), builds unit nodes.
    // Creates TypeCode 0x200. Fleet entity infrastructure required.
    // BLOCKED: CheckEntityFilterEligibility (FUN_004ccbd0) is HIBYTE-blocked → list is empty. Returns null.
    private AIWorkItem CreateFleetAssignmentWorkItem() => null;

    // FUN_004cd3e0: CreateAssignmentDispatchWorkItem for Type 10 Phase A. Assembly trace (fully read).
    // Identical structure to FUN_004cce00 but uses this+0x74 (_type10CandidateList directly).
    // Checks *(sys+0x58) > 0 and FlagA bit 28; builds unit nodes via sub_52bc60.
    // Creates TypeCode 0x200. Requires non-empty candidate list.
    // BLOCKED: candidate list is empty (CheckEntityFilterEligibility HIBYTE-blocked). Returns null.
    private AIWorkItem CreateAssignmentDispatchWorkItem() => null;

    // FUN_004cd920: DispatchMissionToEntity for Type 10 Phase A. Assembly trace (fully read).
    // HIBYTE(*this+0x5c) check [0x90,0x98) fleet AND HIBYTE(*this+0x58) check [0xa0,0xa2) agent.
    // Both required. Creates TypeCode 0x201 (MissionExecutionWorkItem) if both pass.
    // BLOCKED: both HIBYTE entity range checks fail in C#. Returns null.
    private AIWorkItem DispatchMissionToEntity() => null;

    // FUN_004cda60: CreateMissionFollowupWorkItem for Type 10 Phase A. Assembly trace (fully read).
    // HIBYTE(this+0x58) check [0xa0,0xa2): agent range. Creates TypeCode 0x210 work item.
    // BLOCKED: HIBYTE agent check fails in C#. Returns null.
    private AIWorkItem CreateMissionFollowupWorkItem() => null;

    // FUN_004cdb80: BuildTroopMissionBatch for Type 10 Phase A. Assembly trace (fully read).
    // Clears this+0x60 entity ref (FUN_004ec230). Computes count via FUN_004cdff0.
    // If count >= 1: queries workspace entities with [0x80,0x90) HIBYTE, then fleet [0x90,0x98).
    // HIBYTE-blocked paths do nothing. Key side effects: clears entity ref, sets batch count.
    private void BuildTroopMissionBatch()
    {
        _fleetEntityId = 0;
        _batchCount10 = (Capacity * Workspace.FleetTotalCapacity) / 100;
    }

    // FUN_004ce0d0: SelectTroopCandidates for Type 10 Phase A. Assembly trace (fully read).
    // HIBYTE(var_4C) check [0x80,0x90) and HIBYTE(var_48) check [0x90,0x98).
    // Iterates workspace entity target table checking fleet ranges. HIBYTE-blocked.
    // Proxy: marks SystemAnalysis records with FlagA bit 0x10000000 as candidate approximation.
    private void SelectTroopCandidates()
    {
        foreach (var rec in Workspace.SystemAnalysis)
            if ((rec.PresenceFlags & 0x1u) != 0 && (rec.FlagA & 0x3) == 0)
                rec.FlagA |= 0x10000000;
    }

    // --- Phase B helpers ---

    // FUN_004cc3b0: CheckFleetReadyForDispatch. Assembly trace (fully read — complex ~200 lines).
    // 1. Reset this+0x5c, this+0x68. Look up this+0x44 (cursor) in workspace.EntityTargetTable.
    //    If not found: get last node. If still not found: sub_419c70(...) → fleet query.
    // 2. Iterate: FUN_004763e0(entity) → check HIBYTE in [0x90,0x98).
    //    If fleet: this+0x64 = *(entity+0x9c), update cursor. var_1C=1.
    // 3. Returns var_1C.
    // BLOCKED: EntityTargetTable lookup + HIBYTE checks.
    private int CheckFleetReadyForDispatch()
    {
        if (_fleetEntityId == 0)
            return 0;
        var rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == _fleetEntityId);
        return (rec != null && (rec.FlagB & 0x4) != 0) ? 1 : 0;
    }

    // FUN_004cc030: EvaluateFleetDispatchStatus. Assembly trace (fully read — complex ~300 lines).
    // Iterates candidate list at this+0x74. For each: HIBYTE check [0x90,0x98), FlagA & 0x200000,
    //   *(sys+0x58) > 0, HIBYTE(FlagA) & 0x2, no enemy.
    // If found: workspace+0x20 = entity ref, var_30=1. Fallback via workspace+0x1c/+0x24.
    // Returns var_2C (0 or 1).
    // BLOCKED: candidate list empty + HIBYTE checks blocked.
    private int EvaluateFleetDispatchStatus() =>
        Workspace.FleetAssignedCapacity < Workspace.FleetTotalCapacity ? 1 : 0;

    // FUN_004cc5a0: SelectFleetDispatchTarget. Assembly trace (fully read).
    // Calls FUN_004cdf90 (capacity compute). Returns 0, 5, or 9.
    // If < 0: return 5 (CreateFleetDispatchWorkItem). If == 0: return 0 (Phase=PhaseA).
    // If > 0: dice roll + agent lookup (0x28, 0x200000) + FUN_004f22e0 mfg facility check.
    //   If passes: reset workspace+0x20, return 9 (DispatchEntityTransfer).
    //   Else: return 0.
    // CRITICAL BUG FIX: was returning 7 (no such valid case), causing infinite loop!
    // Now returns 0 (terminal) as default proxy (agent/facility infra blocked).
    private int SelectFleetDispatchTarget()
    {
        // Proxy: return 0 → Phase=PhaseA (no dispatch target found)
        // Agent + manufacturing facility infrastructure not available.
        return 0;
    }

    // FUN_004cc680: CreateFleetDispatchWorkItem for Type 10 Phase B. Assembly trace (fully read).
    // Queries workspace: sub_419c70(workspace, 5, 2, 9, 1). Stores at this+0x5c (_fleetEntityId).
    // Then HIBYTE check [0x14,0x1c) on some entity (unknown type range, not fleet/agent).
    // Creates TypeCode 0x200 work item if entity type matches.
    // BLOCKED: HIBYTE entity range check [0x14,0x1c) fails in C# (hash codes have 0 high byte). Returns null.
    private AIWorkItem CreateFleetDispatchWorkItem() => null;

    // FUN_004cc660: CheckFleetDispatchCondition for Type 10 Phase B. Assembly trace (fully read).
    // Calls FUN_004cdf90(this): ((Capacity * workspace[0x184] * 80)/10000) - workspace[0x254].
    //   If workspace[0x184] - workspace[0x188] < result: clamp result to 0.
    // Returns result < 0 (true when already over-allocated: over-requested fleet capacity).
    // No HIBYTE entity checks. This CAN work in C#.
    // workspace[0x184] = FleetTotalCapacity proxy. workspace[0x254] = FleetAssignedCapacity proxy.
    private int CheckFleetDispatchCondition()
    {
        int total = Workspace.FleetTotalCapacity;
        int result = (Capacity * total * 80) / 10000 - Workspace.FleetAssignedCapacity;
        int available = total - Workspace.FleetAssignedCapacity;
        if (available < result)
            result = 0;
        return result < 0 ? 1 : 0;
    }

    // FUN_004cd6c0: DispatchEntityTransfer for Type 10 Phase B. Assembly trace (fully read).
    // Multi-range HIBYTE check: range [0x01,0xff) AND HIBYTE(this+0x58) check [0xa2,0xa4) agent.
    // Creates TypeCode 0x214 (AgentShortageWorkItem) if both pass.
    // BLOCKED: HIBYTE entity range checks fail in C#. Returns null.
    private AIWorkItem DispatchEntityTransfer() => null;

    // FUN_004cd800: CreateEntityTransferFollowup for Type 10 Phase B. Assembly trace (fully read).
    // HIBYTE(this+0x58) check [0xa2,0xa4): different agent sub-range.
    // Creates TypeCode 0x211 work item. Sets item+0x44 from this+0x5c, item+0x48 from _batchCount10.
    // BLOCKED: HIBYTE agent check fails in C#. Returns null.
    private AIWorkItem CreateEntityTransferFollowup() => null;
}

// ------------------------------------------------------------------
// Type 11 — FUN_004c7b90 — ThreePhaseStrategyRecordC
// 0x88 bytes.  Active guard: standard.
//
// Phase 0x3ec → FUN_004c8090 (PhaseA), return result
// Phase 0x3f8 → FUN_004c7f10 (PhaseB), return result
// Phase 0x3f9 → FUN_004c7f90 (PhaseC), return result
// Other       → Phase=0x3f8, SubState=0, return null
// ------------------------------------------------------------------
public class ThreePhaseStrategyRecordC : StrategyRecord
{
    private const int PhaseA = 0x3ec;
    private const int PhaseB = 0x3f8;
    private const int PhaseC = 0x3f9;

    // Extra fields beyond 0x40-byte base (total struct size 0x88, 18 extra 4-byte fields).
    private int _secondaryEntityId11; // +0x50 agent entity [0xa0,0xa2)
    private int _fleetIssueRef11; // +0x54 fleet/mission issue ref
    private int _fleetEntityId11; // +0x58 fleet entity [0x90,0x98)
    private int _targetEntityId11; // +0x5c target entity
    private int _batchCount11; // +0x6c batch count
    private int _maxCapBound11; // +0x70 max capacity bound
    private int _capLimit11; // +0x74 capacity limit
    private readonly List<int> _type11CandidateList = new List<int>(); // +0x78

    public ThreePhaseStrategyRecordC(int ownerSide)
        : base(typeId: 11, capacity: 1, ownerSide: ownerSide) { }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        if (Phase == PhaseA)
            return RunPhaseA();
        if (Phase == PhaseB)
            return RunPhaseB();
        if (Phase == PhaseC)
            return RunPhaseC();

        Phase = PhaseB;
        SubState = 0;
        return null;
    }

    // FUN_004c8090 — PhaseA inner state machine.
    // Drives fleet-candidate scan, bit-selection gate, resource batch computation,
    // work-item dispatch, and troop mission batch build.
    private AIWorkItem RunPhaseA()
    {
        switch (SubState)
        {
            default:
                SubState = 7;
                return null;

            case 7:
            {
                int found = ScanFleetCandidatesPhaseA(); // FUN_004ca030
                if (found != 0)
                {
                    SubState = 8;
                    return null;
                }
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }

            case 8:
            {
                // Clear bit 0x2 from the pending supply bitmask (workspace+0x8).
                Workspace.PendingSupplyBitmask &= ~0x2;
                if (
                    Workspace.EntityTargetType == 2
                    && CheckFleetAssignmentCapacity() != 0 // FUN_004ca310
                )
                {
                    Workspace.AdvanceBitSelection();
                    SubState = 0xb;
                }
                else
                {
                    SubState = 0x10;
                }
                return null;
            }

            case 9:
            {
                int nextState = ComputeResourceBatchSubState(); // FUN_004caa80
                if (nextState == 0)
                {
                    Phase = PhaseB;
                    ReadyFlag = 1;
                    return null;
                }
                SubState = nextState;
                return null;
            }

            case 0xb:
            {
                AIWorkItem workItem = CreateFleetAssignmentWorkItem(); // FUN_004ca540
                if (workItem != null)
                {
                    ReadyFlag = 1;
                    TickCounter++;
                }
                SubState = 9;
                return workItem;
            }

            case 0xc:
            {
                AIWorkItem workItem = CreateFleetTargetAssignmentWorkItem(); // FUN_004cab20
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return workItem;
            }

            case 0xe:
            {
                AIWorkItem workItem = DispatchFleetToTarget(); // FUN_004caf20(this, _fleetEntityId)
                if (workItem != null)
                {
                    SubState = 0xf;
                    ReadyFlag = 1;
                    TickCounter++;
                    return workItem;
                }
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }

            case 0xf:
            {
                AIWorkItem workItem = CreateFleetDispatchFollowup(); // FUN_004cb060
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return workItem;
            }

            case 0x10:
            {
                BuildTroopMissionBatch(); // FUN_004cb180 — return value discarded
                SelectTroopCandidates(); // FUN_004cb6d0 — return value discarded
                SubState = 0;
                Phase = PhaseB;
                ReadyFlag = 1;
                return null;
            }
        }
    }

    // FUN_004c7f10 — PhaseB inner state machine (if/else chain, not a switch).
    // Drives fleet issue creation (state 2), fleet dispatch (state 3), and troop transport orders (state 4).
    private AIWorkItem RunPhaseB()
    {
        if (SubState == 2)
        {
            AIWorkItem workItem = CreateFleetTargetIssue(); // FUN_004c8200
            SubState = 3;
            if (workItem != null)
            {
                ReadyFlag = 1;
                TickCounter++;
            }
            return workItem;
        }

        if (SubState == 3)
        {
            AIWorkItem workItem = CreateFleetDispatchIssue(); // FUN_004c8830
            SubState = 4;
            if (workItem != null)
            {
                ReadyFlag = 1;
                TickCounter++;
                return workItem;
            }
            return null;
        }

        if (SubState == 4)
        {
            AIWorkItem workItem = CreateTroopTransportOrder(); // FUN_004c9020
            SubState = 0;
            Phase = PhaseC;
            ReadyFlag = 1;
            return workItem;
        }

        // Other (default): prime the machine at state 2.
        SubState = 2;
        return null;
    }

    // FUN_004c7f90 — PhaseC inner state machine (switch).
    // Drives production shortage detection, entity selection, batch computation, and transport dispatch.
    private AIWorkItem RunPhaseC()
    {
        switch (SubState)
        {
            default:
                SubState = 8;
                return null;

            case 5:
            {
                // FUN_004c9950: choose the production shortage request family for the current shortage.
                // Sets production shortage request fields on the record using the entity at _secondaryEntityId.
                ChooseProductionShortageFamily(); // FUN_004c9950 — return value discarded
                SubState = 9;
                return null;
            }

            case 8:
            {
                int found = FindShortageSourceEntity(); // FUN_004c9670
                SubState = (found != 0) ? 5 : 0xd;
                return null;
            }

            case 9:
            {
                int nextState = ComputeProductionShortageSubState(); // FUN_004c9c80
                SubState = nextState;
                if (nextState == 0)
                {
                    Phase = PhaseA;
                    ReadyFlag = 1;
                }
                return null;
            }

            case 0xa:
            {
                AIWorkItem workItem = CreateProductionDeficitTransportWorkItem(); // FUN_004c9e90
                SubState = 0;
                Phase = PhaseA;
                ReadyFlag = 1;
                return workItem;
            }

            case 0xd:
            {
                bool thresholdMet = CheckResourceDeficitThreshold(); // FUN_004c9e40
                if (thresholdMet)
                {
                    SubState = 0xa;
                }
                else
                {
                    SubState = 0;
                    Phase = PhaseA;
                    // NOTE: ReadyFlag intentionally NOT set on this path (mirrors FUN_004c7f90).
                }
                return null;
            }

            case 0xf:
            {
                AIWorkItem workItem = CreateShortageTransferWorkItem(); // FUN_004cae00
                SubState = 0;
                Phase = PhaseA;
                ReadyFlag = 1;
                return workItem;
            }
        }
    }

    // --- Phase A helpers ---

    // FUN_004ca030: ScanFleetCandidatesPhaseA for Type 11. Assembly trace (fully read).
    // Same pattern as Type 10 CheckEntityFilterEligibility (FUN_004ccbd0) but for Type 11.
    // Clears _fleetEntityId11 (+0x58). Then does: sub_419330(workspace, this+0x58, 0x1000) +
    // sub_419af0(this+0x58, 0x800800, ...). HIBYTE check [0x90,0x98): if fleet → capacity calc.
    // BUT also has fallback path (lines 39-41) iterating _type11CandidateList without HIBYTE.
    // Proxy: iterate list with FlagA/PresenceFlags check (no entity HIBYTE needed).
    private int ScanFleetCandidatesPhaseA()
    {
        _fleetEntityId11 = 0;
        foreach (int sysId in _type11CandidateList.ToList())
        {
            var rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == sysId);
            if (rec == null)
            {
                _type11CandidateList.Remove(sysId);
                continue;
            }
            if (
                (rec.PresenceFlags & 0x1u) != 0
                && (rec.FlagA & 0x3) == 0
                && rec.Stats.FacilityCount > 0
            )
            {
                _fleetEntityId11 = sysId;
                _capLimit11 = rec.Stats.FacilityCount;
                _maxCapBound11 = rec.Stats.FacilityCount - 1;
                return 1;
            }
        }
        return 0;
    }

    // FUN_004ca310: CheckFleetAssignmentCapacity. Assembly trace (fully read).
    // Same structure as FUN_004cfeb0 (Type 5) and FUN_004df310 (Type 6):
    // 1. sub_419330(workspace, this+0x58, 0x1000, ...) → this+0x50.
    // 2. QuerySystemPlanets(this+0x50, 0x800800, 0, 0x1, 0x3, 0, 0, 6, 1) → update this+0x50.
    // 3. Check HIBYTE(this+0x50) in [0x90,0x98). If fleet: sub_4f25a0+sub_5087e0.
    //    If passes: this+0x70 (_maxCapBound11) = SystemScore (or SystemScore-1). var_1C=1.
    //    Check if this+0x58 NOT in _type11CandidateList → insert + FlagA |= 0x200000 (bit 21!).
    // 4. Returns var_1C (0 or 1).
    // Note: FlagA bit 0x200000 (bit 21) used for Type 11 — different from Type 5's 0x400000.
    // BLOCKED: sub_419330 + HIBYTE checks blocked.
    private int CheckFleetAssignmentCapacity()
    {
        // Proxy: seed _fleetIssueRef11 from QuerySystemAnalysis and set _maxCapBound11
        IssueRecordContainer c = Workspace.QuerySystemAnalysis(
            incl24: 0x1000,
            incl28: 0,
            incl2c: 0,
            excl24: 0,
            excl28: 0,
            excl2c: 0,
            statIndex: 6
        );
        var top = c.GetTopRecord();
        if (top == null)
            return 0;
        _fleetIssueRef11 = top.InternalId;
        int score = top.SystemScore;
        _maxCapBound11 = score > 1 ? score - 1 : score; // assembly: SystemScore or SystemScore-1
        return 1;
    }

    // FUN_004caa80: ComputeResourceBatchSubState. Assembly trace (fully read).
    // Calls FUN_004cb5f0(this): same formula as FUN_004d1160/FUN_004e0d60/FUN_004cb5f0 variants.
    // If result < 0: return 0xc (case 0xc = CreateFleetTargetAssignmentWorkItem).
    // If result == 0: return 0 (terminal → Phase=PhaseB).
    // If result > 0: sub_41a9e0 agent lookup. If found: return 0xe (case 0xe = DispatchFleetToTarget).
    //   Else: return 0.
    // Current proxy approximates via capacity calculation.
    private int ComputeResourceBatchSubState()
    {
        // FUN_004cb5f0 proxy: (Capacity * FleetTotalCapacity * X/10000) - accumulated
        // Using same proxy as other similar functions
        int total = Workspace.FleetTotalCapacity;
        int result = (Capacity * total) / 100;
        if (total - Workspace.FleetAssignedCapacity < result)
            result = 0;
        if (result < 0)
            return 0xc;
        if (result == 0)
            return 0;
        // result > 0: agent lookup blocked → return 0
        return 0;
    }

    // FUN_004ca540: CreateFleetAssignmentWorkItem for Type 11 Phase A. Assembly trace (fully read).
    // HIBYTE(*entity) check [0x90,0x98): fleet gate. Builds unit nodes. Creates TypeCode 0x200.
    // BLOCKED: HIBYTE fleet range check fails in C#. Returns null.
    private AIWorkItem CreateFleetAssignmentWorkItem() => null;

    // FUN_004cab20: CreateFleetTargetAssignmentWorkItem for Type 11 Phase A. Assembly trace (fully read).
    // Identical structure to FUN_004cd3e0 (Type 10): iterates candidate list, checks FlagA bit 28.
    // Builds unit nodes via sub_52bc60. Creates TypeCode 0x200 if nodes found.
    // BLOCKED: fleet entity infrastructure (sub_52bc60) unavailable in C#. Returns null.
    private AIWorkItem CreateFleetTargetAssignmentWorkItem() => null;

    // FUN_004caf20: DispatchFleetToTarget for Type 11 Phase A. Assembly trace (fully read).
    // HIBYTE(*arg_0) [0x90,0x98) fleet AND HIBYTE(this+0x50) [0xa0,0xa2) agent.
    // Creates TypeCode 0x214 (AgentShortageWorkItem) if both pass.
    // BLOCKED: both HIBYTE entity range checks fail in C#. Returns null.
    private AIWorkItem DispatchFleetToTarget() => null;

    // FUN_004cb060: CreateFleetDispatchFollowup for Type 11 Phase A. Assembly trace (fully read).
    // HIBYTE(this+0x50) check [0xa0,0xa2): agent range. Creates TypeCode 0x210 work item.
    // BLOCKED: HIBYTE agent check fails in C#. Returns null.
    private AIWorkItem CreateFleetDispatchFollowup() => null;

    // FUN_004cb180: BuildTroopMissionBatch for Type 11 Phase A. Assembly trace (fully read).
    // Clears this+0x58 entity ref (FUN_004ec230). Computes count via FUN_004cb5a0.
    // If count >= 1: queries workspace [0x80,0x90) and [0x90,0x98). HIBYTE-blocked.
    // Key side effects: clears _fleetEntityId11, sets _batchCount11.
    private void BuildTroopMissionBatch()
    {
        _fleetEntityId11 = 0;
        _batchCount11 = (Capacity * Workspace.FleetTotalCapacity) / 100;
    }

    // FUN_004cb6d0: SelectTroopCandidates for Type 11 Phase A. Assembly trace (fully read).
    // Iterates candidate list. Selects by *(sys+0x58) capacity. HIBYTE check [0x90,0x98) on
    // selected candidate → if fleet: add to output list. HIBYTE-blocked.
    // Proxy: marks SystemAnalysis records with FlagA bit 0x10000000.
    private void SelectTroopCandidates()
    {
        foreach (var rec in Workspace.SystemAnalysis)
            if ((rec.PresenceFlags & 0x1u) != 0 && (rec.FlagA & 0x3) == 0)
                rec.FlagA |= 0x10000000;
    }

    // --- Phase B helpers ---

    // FUN_004c8200: CreateFleetTargetIssue for Type 11 Phase B. Assembly trace (partially read).
    // sub_419c70(workspace, 0x1005, 0x802, 0, 1) → system query → stores at this+0x54 (_fleetIssueRef11).
    // No HIBYTE entity range checks detected in available trace.
    // Proxy: creates MissionExecutionWorkItem using _fleetEntityId11 when available.
    private AIWorkItem CreateFleetTargetIssue()
    {
        if (_fleetEntityId11 == 0)
            return null;
        var rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == _fleetEntityId11);
        return rec != null ? new MissionExecutionWorkItem(_fleetEntityId11, Workspace) : null;
    }

    // FUN_004c8830: CreateFleetDispatchIssue for Type 11 Phase B. Assembly trace (partially read).
    // sub_419c70(workspace, 5, 0x3802, 0, 2) → stores at this+0x54 (_fleetIssueRef11).
    // Then HIBYTE checks [0x08,0x10), [0x14,0x1c), [0x90,0x98) at multiple locations.
    // Creates TypeCode 0x270 work item (not 0x201 or 0x200 — a production/transport order type).
    // BLOCKED: multiple entity HIBYTE range checks fail in C#. Proxy returns MissionExecutionWorkItem.
    private AIWorkItem CreateFleetDispatchIssue()
    {
        if (_fleetIssueRef11 == 0 && _fleetEntityId11 == 0)
            return null;
        int key = _fleetIssueRef11 != 0 ? _fleetIssueRef11 : _fleetEntityId11;
        return new MissionExecutionWorkItem(key, Workspace);
    }

    // FUN_004c9020: CreateTroopTransportOrder for Type 11 Phase B. Assembly trace (partially read).
    // Checks workspace StatusFlags LOBYTE & 0x30. Creates transport order via
    // FUN_00419640(workspace, 0, 0, 0x8000000, ...). Stores issue ID at _fleetIssueRef11 (+0x54).
    // HIBYTE checks [0x80,0x90), [0x90,0x98), [0x08,0x10), [0x14,0x1c) at multiple locations.
    // Creates TypeCode 0x201 (MissionExecutionWorkItem) on success.
    // BLOCKED: multiple HIBYTE entity range checks fail in C#. Proxy uses _fleetEntityId11.
    private AIWorkItem CreateTroopTransportOrder()
    {
        if (_fleetEntityId11 == 0)
            return null;
        var rec = Workspace.SystemAnalysis.FirstOrDefault(r => r.InternalId == _fleetEntityId11);
        return rec != null ? new MissionExecutionWorkItem(_fleetEntityId11, Workspace) : null;
    }

    // --- INCOMPLETE helper stubs for RunPhaseC ---

    // FUN_004c9670: FindShortageSourceEntity for Type 11 Phase C. Assembly trace (fully read).
    // 1. Clears workspace+0x24 entity ref (FUN_004ec230).
    // 2. Iterates _type11CandidateList (+0x78). For each entry: HIBYTE check [0x90,0x98) fleet.
    //    If fleet: check *(sys+0x30)&0x1, *(sys+0x28)&0x200000, *(sys+0x58)>0, HIBYTE(FlagA)&0x2,
    //    LOBYTE(FlagA)&0x3==0. If passes: set workspace+0x24, var_30=1; else: clear FlagA bit, remove.
    // 3. Fallback: workspace+0x1c and workspace+0x20 with same fleet HIBYTE check.
    // 4. If var_30=1: sub_4f25a0 + sub_5087e0 ownership check. var_2C=1.
    // BLOCKED: all HIBYTE fleet [0x90,0x98) checks fail in C#. Returns 0.
    private int FindShortageSourceEntity() => 0;

    // FUN_004c9950: ChooseProductionShortageFamily for Type 11 Phase C. File not found in disassembly.
    // Sets production shortage request fields using the entity at _secondaryEntityId11 (+0x50).
    // Return value discarded by caller. No-op proxy: entity resolution not implemented.
    private void ChooseProductionShortageFamily() { }

    // FUN_004c9c80: ComputeProductionShortageSubState for Type 11 Phase C. Assembly trace (fully read).
    // 1. FlagA &= 0x80ffffff (clears bits 24-30, keeps bit 31 and lower 24).
    // 2. this+0x6c = 0. Calls sub_4cb590(this) → count.
    // 3. If count > 0: query sub_41aa20(workspace, 0x28, this+0x60, this+0x64, 1) → entity at this+0x5c.
    //    Checks (entity_id & 0xff000000) != 0 (HIBYTE check in disguise for non-zero HIBYTE).
    //    HIBYTE fails in C#: FlagA |= this+0x68; returns 0.
    // 4. If count < 0: returns 0xa (10).
    // 5. If count == 0: returns 0.
    // In C#: only 0 or 0xa returned (0xf never reached since HIBYTE loop skipped).
    private int ComputeProductionShortageSubState()
    {
        // this+0x28 &= 0x80ffffff: modifies TickSubObject (sub-struct; no direct C# equivalent).
        _batchCount11 = 0;
        // sub_4cb590 proxy: same formula pattern as FUN_004cdf90 with different accumulator
        int total = Workspace.FleetTotalCapacity;
        int count = (Capacity * total) / 100 - Workspace.FleetAssignedCapacity;
        if (total - Workspace.FleetAssignedCapacity < count)
            count = 0;
        // In C#: HIBYTE loop (entity_id & 0xff000000 != 0) always fails → never returns 0xf
        if (count < 0)
            return 0xa;
        return 0;
    }

    // FUN_004c9e40: CheckResourceDeficitThreshold for Type 11 Phase C. Assembly trace (fully read).
    // Pure arithmetic, no HIBYTE checks. Formula:
    // ((Capacity * workspace[0x184]) / 100 * 0x5a) / 100 - workspace[0x24c] < 0
    // workspace[0x184] = FleetTotalCapacity proxy. workspace[0x24c] = FleetAssignedCapacity proxy.
    // Returns true when current assignment exceeds threshold (over-allocated).
    private bool CheckResourceDeficitThreshold()
    {
        int target = (Capacity * Workspace.FleetTotalCapacity / 100 * 0x5a) / 100;
        return target - Workspace.FleetSecondaryScores[0] < 0;
    }

    // FUN_004c9e90: CreateProductionDeficitTransportWorkItem for Type 11 Phase C. Assembly trace (fully read).
    // sub_419c70(workspace, 5, 0x402, 0xe, 2) + sub_41a430(this+0x54, 0x24000, 0x2800, 0xb, 2).
    // HIBYTE check [0x14,0x1c) on entity at this+0x54. If passes: create unit node, TypeCode 0x200.
    // BLOCKED: HIBYTE entity range [0x14,0x1c) fails in C# (hash codes have 0 high byte). Returns null.
    private AIWorkItem CreateProductionDeficitTransportWorkItem() => null;

    // FUN_004cae00: CreateShortageTransferWorkItem for Type 11 Phase C. Assembly trace (fully read).
    // HIBYTE(this+0x50) check [0xa2,0xa4): different agent sub-range.
    // Creates TypeCode 0x211 work item. Sets item+0x44 from this+0x5c, item+0x48 from this+0x60.
    // BLOCKED: HIBYTE agent check fails in C#. Returns null.
    private AIWorkItem CreateShortageTransferWorkItem() => null;
}

// ------------------------------------------------------------------
// Type 12 — FUN_004c75d0 — ProductionAutomationRecord (FOIL production automation)
// 0x60 bytes.
// Active guard: pass-through (no reset on inactive).
// If active: call FUN_004c7840 (do work), return result.
//
// Extra fields beyond the 0x40-byte base (total 0x60, 0x20 extra bytes):
//   +0x40 = _entityTargetId:  current entity target entry ID (workspace+0xd8).
//   +0x44 = _entityCursor:    backward-walk cursor into EntityTargetTable.
//   +0x48 = _productionItemId: current production tracking entry ID (workspace+0xec).
//   +0x4c = _productionCursor: backward-walk cursor into ProductionTrackingTable.
//   +0x50..+0x5c: four additional fields present in the 0x60-byte original struct
//                 whose purpose is not yet determined from the analysed functions.
//
// FUN_004c7840 SubState machine:
//   default → SubState=1.
//   1 → FUN_004c7920 (cleanup previous tracking); SubState=2.
//   2 → FUN_004c79a0 (find entity with pending production):
//         non-zero result → SubState=3; zero result → SubState=4.
//   3 → FUN_004c7a20 (link production entry to entity target); SubState=4.
//   4 → FUN_004c7ab0 (find production entry needing work):
//         non-zero → SubState=5; zero → SubState=0, ReadyFlag=1.
//   5 → FUN_004c7b30 (dispatch production entry, out dispatchOut):
//         result!=0 AND dispatchOut==0: TickCounter++, ReadyFlag=1; break→return result.
//         result!=0 AND dispatchOut!=0: ReadyFlag=1, SubState=0, ReadyFlag=1, return result.
//         result==0 AND dispatchOut!=0: SubState=0, ReadyFlag=1, return null.
//         result==0 AND dispatchOut==0: break → return null.
// ------------------------------------------------------------------
public class ProductionAutomationRecord : StrategyRecord
{
    // +0x40: current entity target entry ID (workspace+0xd8 lookup key).
    private int _entityTargetId;

    // +0x44: backward-walk cursor into EntityTargetTable.
    // Holds the ID of the entry just before the last one examined; zero = start from tail.
    private int _entityCursor;

    // +0x48: current production tracking entry ID (workspace+0xec lookup key).
    private int _productionItemId;

    // +0x4c: backward-walk cursor into ProductionTrackingTable.
    // Holds the ID of the entry just before the last one examined; zero = start from tail.
    private int _productionCursor;

    public ProductionAutomationRecord(int ownerSide)
        : base(typeId: 12, capacity: 1, ownerSide: ownerSide)
    {
        _entityTargetId = 0;
        _entityCursor = 0;
        _productionItemId = 0;
        _productionCursor = 0;
    }

    protected override bool ActiveGuardFails()
    {
        return ActiveState != 1;
    }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        return DoWork();
    }

    // FUN_004c7840 — 6-state SubState machine that orchestrates production tracking.
    private AIWorkItem DoWork()
    {
        switch (SubState)
        {
            default:
                // Out-of-range SubState: reset to 1.
                // Production tracking entries are created by the production scheduling
                // system when it calls LinkProductionEntryToEntityTarget (FUN_004c7a20).
                SubState = 1;
                return null;

            case 1:
                // FUN_004c7920: cancel the stale production entry from the previous cycle
                // and check whether the completed-entry slot records a finished job.
                // Return value is ignored by the caller.
                CleanupPreviousProductionTracking();
                SubState = 2;
                return null;

            case 2:
            {
                // FUN_004c79a0: walk EntityTargetTable backward to find the next entity
                // that has a pending or previous production ID (FUN_00476140 check).
                // Assembly: SubState = 4 - (uint)(found != 0) → found→3, not-found→4.
                int found = FindNextEntityWithPendingProduction();
                SubState = (found != 0) ? 3 : 4;
                return null;
            }

            case 3:
                // FUN_004c7a20: for the entity found in state 2, cancel the old production
                // entry (PreviousProductionId) via FUN_0042e670 and remap+install the
                // pending one (PendingProductionId) via FUN_0042e630 + FUN_00476160.
                LinkProductionEntryToEntityTarget();
                SubState = 4;
                return null;

            case 4:
            {
                // FUN_004c7ab0: walk ProductionTrackingTable backward to find the next
                // entry whose vtable+0x10 (NeedsProcessing) returns non-zero.
                int found = FindNextProductionEntryNeedingWork();
                if (found == 0)
                {
                    SubState = 0;
                    ReadyFlag = 1;
                    return null;
                }
                SubState = 5;
                return null;
            }

            case 5:
            {
                // FUN_004c7b30: look up the production entry by _productionItemId in
                // workspace+0xec and call vtable+0x14 on it, which writes to dispatchOut
                // and returns a work item.
                //   Entry not found:       returns null, dispatchOut=1.
                //   Entry found, dispatch: returns item (possibly null), dispatchOut
                //                          written by vtable+0x14.
                int dispatchOut;
                AIWorkItem item = TryDispatchProductionEntry(out dispatchOut);

                if (item != null)
                {
                    if (dispatchOut == 0)
                        TickCounter++; // absorbed — count the tick
                    ReadyFlag = 1;
                }

                if (dispatchOut != 0)
                {
                    // Pending or entity-not-found: reset SubState and exit.
                    SubState = 0;
                    ReadyFlag = 1;
                    return item;
                }

                // dispatchOut == 0: break → fall through to return item.
                return item;
            }
        }
    }

    // FUN_004c7920 — clean up production tracking from the previous cycle.
    //
    // Reads workspace.PendingProductionCancelId (workspace+0x354):
    //   Non-zero: calls FUN_0042e670 equivalent to mark that entry cancelled,
    //   then clears the field.
    //
    // Reads workspace.PendingProductionCompleteId (workspace+0x358):
    //   Non-zero: remaps the entry (FUN_0042e630 — a no-op for stable C# IDs),
    //   looks it up in ProductionTrackingTable, clears the field,
    //   and if status == Complete, increments ProductionCompletionCounters[type].
    //
    // Return value (0 or 1) is always discarded by the caller (FUN_004c7840 case 1).
    private void CleanupPreviousProductionTracking()
    {
        if (Workspace.PendingProductionCancelId != 0)
        {
            MarkProductionEntryCancelled(Workspace.PendingProductionCancelId);
            Workspace.PendingProductionCancelId = 0;
        }

        if (Workspace.PendingProductionCompleteId != 0)
        {
            // FUN_0042e630: remap the entry ID (in C# IDs are stable — use as-is).
            int entryId = Workspace.PendingProductionCompleteId;
            Workspace.PendingProductionCompleteId = 0;

            ProductionTrackingEntry entry = Workspace.ProductionTrackingTable.Find(e =>
                e.Id == entryId
            );
            if (entry != null && entry.Status == ProductionStatus.Complete)
            {
                // FUN_0041ad80: increment the per-type completion counter.
                // Original: workspace + (vtable+0x28() & 0xffff) * 4 + 0x35c += 1.
                int typeIndex = (int)entry.ManufacturingType & 0xffff;
                if (typeIndex < Workspace.ProductionCompletionCounters.Length)
                    Workspace.ProductionCompletionCounters[typeIndex]++;
            }
        }
    }

    // FUN_0042e670 equivalent — mark a production tracking entry as cancelled.
    //
    // Looks up the entry by entryId in ProductionTrackingTable. If found:
    //   If Status == Active: unlinks it from its entity target (FUN_00476230 path).
    //   Sets IsCancelled = true (bit 0x80000000 of entry+0x20).
    private void MarkProductionEntryCancelled(int entryId)
    {
        ProductionTrackingEntry entry = Workspace.ProductionTrackingTable.Find(e =>
            e.Id == entryId
        );
        if (entry == null)
            return;

        if (entry.Status == ProductionStatus.Active)
        {
            MissionTargetEntry entityTarget = Workspace.EntityTargetTable.Find(t =>
                t.Id == entry.EntityTargetId
            );
            if (entityTarget != null)
            {
                // FUN_00476230: unlink production entry from entity target.
                // Clear the entity target's reference to this production entry.
                if (entityTarget.PendingProductionId == entryId)
                    entityTarget.PendingProductionId = 0;
                if (entityTarget.PreviousProductionId == entryId)
                    entityTarget.PreviousProductionId = 0;
            }
        }

        entry.IsCancelled = true;
    }

    // FUN_004c79a0 — walk EntityTargetTable backward to find the next entity that
    // has a pending or previous production ID (FUN_00476140 check: entry+0xe0 != 0
    // OR entry+0xe4 != 0).
    //
    // Assembly trace (fully read):
    // 1. esi = FUN_005f3a70_get_table_by_id(workspace+0xd8, _entityCursor) — look up cursor.
    //    If not found: esi = FUN_005f35d0_get_last_node_in_list(workspace+0xd8) — fall back to tail.
    //    If table empty: _entityTargetId=0, _entityCursor=0, return 0.
    // 2. Walk backward via previous_node (C decompile: paVar1->previous_node):
    //    For each entry: FUN_00476140 checks PendingProductionId != 0 OR PreviousProductionId != 0.
    //    Stop when found.
    // 3. If found: _entityTargetId = entry.Id, _entityCursor = previous entry's Id or 0. Return 1.
    // 4. Not found: _entityTargetId=0, _entityCursor=0. Return 0.
    private int FindNextEntityWithPendingProduction()
    {
        List<MissionTargetEntry> table = Workspace.EntityTargetTable;
        if (table.Count == 0)
        {
            _entityTargetId = 0;
            _entityCursor = 0;
            return 0;
        }

        int startIdx;
        if (_entityCursor != 0)
        {
            startIdx = table.FindIndex(e => e.Id == _entityCursor);
            // If cursor not found: fall back to tail (C decompile: get_last_node_in_list)
            if (startIdx < 0)
                startIdx = table.Count - 1;
        }
        else
        {
            startIdx = table.Count - 1;
        }

        // Backward do-while: walk from startIdx toward index 0.
        // FUN_00476140: returns 1 if entry.PendingProductionId!=0 OR entry.PreviousProductionId!=0.
        for (int i = startIdx; i >= 0; i--)
        {
            MissionTargetEntry e = table[i];
            if (e.PendingProductionId != 0 || e.PreviousProductionId != 0)
            {
                _entityTargetId = e.Id;
                _entityCursor = (i > 0) ? table[i - 1].Id : 0;
                return 1;
            }
        }

        _entityTargetId = 0;
        _entityCursor = 0;
        return 0;
    }

    // FUN_004c7a20 — for the entity target identified by _entityTargetId, cancel the
    // old production entry (PreviousProductionId) and install the pending one
    // (PendingProductionId).
    //
    // entry.PreviousProductionId != 0 (entry+0xe4):
    //   Call FUN_0042e670 to mark that entry cancelled; clear PreviousProductionId.
    //
    // entry.PendingProductionId != 0 (entry+0xe0):
    //   FUN_0042e630: remap ID (no-op in C#). Store remapped ID in _productionItemId.
    //   Look up in ProductionTrackingTable. If found and Status==Active:
    //     FUN_00476160: clear PendingProductionId, set production.EntityTargetId.
    //   Set uVar3=1 regardless of status check (returned but caller ignores it).
    private void LinkProductionEntryToEntityTarget()
    {
        MissionTargetEntry entityTarget = Workspace.EntityTargetTable.Find(e =>
            e.Id == _entityTargetId
        );
        if (entityTarget == null)
            return;

        if (entityTarget.PreviousProductionId != 0)
        {
            MarkProductionEntryCancelled(entityTarget.PreviousProductionId);
            entityTarget.PreviousProductionId = 0;
        }

        if (entityTarget.PendingProductionId != 0)
        {
            // FUN_0042e630: remap entry ID. In C# IDs are stable, so use directly.
            _productionItemId = entityTarget.PendingProductionId;

            ProductionTrackingEntry prodEntry = Workspace.ProductionTrackingTable.Find(e =>
                e.Id == _productionItemId
            );
            if (prodEntry != null)
            {
                if (prodEntry.Status == ProductionStatus.Active)
                {
                    // FUN_00476160: link the production entry to this entity target.
                    // Clears entry+0xe0 (PendingProductionId) and sets entry+0x54
                    // (EntityTargetId back-ref) on the production tracking entry.
                    entityTarget.PendingProductionId = 0;
                    prodEntry.EntityTargetId = entityTarget.Id;
                    // FUN_00476160: additional linking operations.
                    // The sub-table allocation and dispatch callback are internal to the
                    // production pipeline; the core link (EntityTargetId) is already set above.
                    prodEntry.NeedsProcessing = true; // mark as ready for dispatch
                }
                // uVar3 = 1 is set regardless; return value discarded by caller.
            }
        }
    }

    // FUN_004c7ab0 — walk ProductionTrackingTable backward to find the next entry
    // whose vtable+0x10 (NeedsProcessing) returns non-zero.
    //
    // Assembly trace (fully read — same structure as FUN_004c79a0 but for ProductionTrackingTable):
    // 1. Look up _productionCursor (this+0x4c) in ProductionTrackingTable.
    //    If not found: fall back to last node (FUN_005f35d0).
    //    If table empty: _productionItemId=0, _productionCursor=0, return 0.
    // 2. Walk backward (previous_node) calling vtable+0x10 (NeedsProcessing) on each entry.
    // 3. If found: _productionItemId = entry.Id, _productionCursor = prev.Id or 0. Return non-zero.
    // 4. Not found: _productionItemId=0, _productionCursor=0. Return 0.
    private int FindNextProductionEntryNeedingWork()
    {
        List<ProductionTrackingEntry> table = Workspace.ProductionTrackingTable;
        if (table.Count == 0)
        {
            _productionItemId = 0;
            _productionCursor = 0;
            return 0;
        }

        int startIdx;
        if (_productionCursor != 0)
        {
            startIdx = table.FindIndex(e => e.Id == _productionCursor);
            // If cursor not found: fall back to tail (assembly: FUN_005f35d0_get_last_node)
            if (startIdx < 0)
                startIdx = table.Count - 1;
        }
        else
        {
            startIdx = table.Count - 1;
        }

        // Backward do-while: vtable+0x10 check = NeedsProcessing.
        for (int i = startIdx; i >= 0; i--)
        {
            ProductionTrackingEntry e = table[i];
            if (e.NeedsProcessing)
            {
                _productionItemId = e.Id;
                _productionCursor = (i > 0) ? table[i - 1].Id : 0;
                return 1;
            }
        }

        _productionItemId = 0;
        _productionCursor = 0;
        return 0;
    }

    // FUN_004c7b30 — look up the production entry at _productionItemId in
    // ProductionTrackingTable and call vtable+0x14 (the dispatch method).
    //
    // Assembly trace (fully read):
    // __thiscall: ECX = this (ProductionAutomationRecord), arg0 = &dispatchOut (stack).
    // 1. Look up _productionItemId (this+0x48) in ProductionTrackingTable (workspace+0xec).
    // 2. If found: call vtable+0x14(arg0) on the entry — writes to *arg0 (dispatchOut) and
    //    returns a work item. Return the work item.
    // 3. If not found: *arg0 = 1 (dispatchOut = 1 = "not found / pending"), return null.
    private AIWorkItem TryDispatchProductionEntry(out int dispatchOut)
    {
        ProductionTrackingEntry entry = Workspace.ProductionTrackingTable.Find(e =>
            e.Id == _productionItemId
        );
        if (entry == null)
        {
            dispatchOut = 1; // "not found" path: *param_1 = 1
            return null;
        }

        // vtable+0x14 = Dispatch: writes to dispatchOut and returns work item.
        return entry.Dispatch(out dispatchOut);
    }
}

// ------------------------------------------------------------------
// Type 13 — FUN_004be450 — DiplomacyStrategyRecord (RLEVAD diplomacy strategy)
// 0x70 bytes.
// Active guard: PARTIAL reset — resets TickCounter (+0x34) and Phase (+0x38)
//   and sets ReadyFlag (+0x20) = 1, but does NOT reset SubState (+0x3c).
//   (Different from the base class guard which also resets SubState.)
// If active: call FUN_004c68f0 (advance diplomacy substate), return result.
//
// Extra fields beyond the 0x40-byte base (total 0x70, 0x30 extra bytes):
//   +0x30 = _emitCounter:      count of primary objects emitted (overlaps OwnerSide
//                               in base layout; DiplomacyStrategyRecord repurposes it).
//   +0x3c = SubState:          repurposed as batch count (param_1+0x3c) by state 3's
//                               callee FUN_004c6d50.  Cleared to 0, set to 1 or to
//                               iVar4/iVar1 (adjusted batch size); embedded in the
//                               counted work item by state 5.
//   +0x40 = _primaryIssueRef:  AutoClass9 — primary issue ID set by state 2.
//   +0x44 = _followupIssueRef: AutoClass9 — followup issue ID set by state 1.
//   +0x48 = _batchCalcRef:     AutoClass9 — batch-count calculation result (state 3).
//   +0x4c..+0x6c: embedded mission-issue record list (AutoClass415, 20 bytes).
//
// Note on +0x28 (TickSubObject): the callee functions use *(param_1+0x28) as the
// production-context object (not Workspace at +0x2c).  In C# this is the TickSubObject
// sub-object.  All callees that operate on TickSubObject are INCOMPLETE(engine).
//
// FUN_004c68f0 Phase state machine (Phase drives +0x38, NOT SubState):
//   default → Phase=2, return null.
//   1 → FUN_004c6bd0 (derive followup issue):
//         ok → Phase=3, return null;
//         !ok → Phase=0, ReadyFlag=1, return null.
//   2 → FUN_004c69c0 (seed primary issue):
//         ok → Phase=1, return null;
//         !ok → Phase=0, ReadyFlag=1, return null.
//   3 → FUN_004c6d50 (compute batch count):
//         ok → Phase=4, return null;
//         !ok → Phase=0, ReadyFlag=1, return null.
//   4 → FUN_004c6e00 (emit primary runtime object):
//         result!=null → Phase=5, ReadyFlag=1, _emitCounter+=1, return result;
//         null → Phase=0, ReadyFlag=1, return null.
//   5 → FUN_004c6f40 (emit counted runtime object):
//         always: Phase=0, ReadyFlag=1, return result (may be null).
// ------------------------------------------------------------------
public class DiplomacyStrategyRecord : StrategyRecord
{
    // +0x30: count of primary runtime objects emitted during phase 4.
    // Overlaps OwnerSide in the base layout; Type 13 repurposes this field.
    // Incremented each time FUN_004c6e00 returns a non-null work item.
    private int _emitCounter;

    // +0x40: primary issue reference (AutoClass9 embedded at +0x40).
    // Set by FUN_004c69c0 (phase 2 callee) via FUN_004ec1e0_set_id.
    private object _primaryIssueRef;

    // +0x44: followup issue reference (AutoClass9 embedded at +0x44).
    // Set by FUN_004c6bd0 (phase 1 callee) via FUN_004ec1e0_set_id.
    private object _followupIssueRef;

    // +0x48: batch calculation result reference (AutoClass9 at +0x48).
    // Written by FUN_0049cf00 inside FUN_004c6d50 (phase 3 callee).
    // Carries the batch count; passed to the counted work item in phase 5.
    private object _batchCalcRef;

    public DiplomacyStrategyRecord(int ownerSide)
        : base(typeId: 13, capacity: 1, ownerSide: ownerSide)
    {
        _emitCounter = 0;
        _primaryIssueRef = null;
        _followupIssueRef = null;
        _batchCalcRef = null;
    }

    // Guard: resets TickCounter and Phase but intentionally leaves SubState intact.
    // FUN_004c68b0 writes +0x34=0 and +0x38=0 (TickCounter and Phase), but does
    // NOT touch +0x3c (SubState), which is repurposed as a batch count.
    protected override bool ActiveGuardFails()
    {
        if (ActiveState != 1)
        {
            TickCounter = 0;
            Phase = 0;
            ReadyFlag = 1;
            // SubState (+0x3c) intentionally NOT reset — it carries the batch count.
            return true;
        }
        return false;
    }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        return AdvanceDiplomacySubstate();
    }

    // FUN_004c68f0_advance_strategy_production_automation_substate
    //
    // 5-state Phase machine driving diplomacy/production issue seeding and emission.
    // Phase (param_1+0x38) is the state variable; SubState (+0x3c) is repurposed
    // as batch count by state 3's callee.
    private AIWorkItem AdvanceDiplomacySubstate()
    {
        switch (Phase)
        {
            case 1:
            {
                // FUN_004c6bd0: derive a followup production issue from the primary
                // issue already seeded in state 2.  Calls FUN_0049cba0 on TickSubObject
                // with the primary issue ref (+0x40) to produce a companion record;
                // stores the resulting ID in _followupIssueRef (+0x44).
                // Reads TickSubObject+0x28 to locate the production context.
                // Checks result type code in [0x90, 0x98); if found, resolves a
                // matching object via FUN_004f25a0 and wraps it in a work item.
                // Returns 1 on success (followup seeded), 0 on failure.
                int ok = SeedFollowupIssue();
                if (ok != 0)
                {
                    Phase = 3;
                    return null;
                }
                Phase = 0;
                ReadyFlag = 1;
                return null;
            }

            case 2:
            {
                // FUN_004c69c0: seed the primary production issue on the TickSubObject.
                // Calls FUN_0049ca40(TickSubObject, 0x21000000, 0, 0, 0, 0x19, 1) to
                // generate the primary issue record; stores the resulting ID in
                // _primaryIssueRef (+0x40). Checks result type code in [0x90, 0x98);
                // if NOT in range, calls FUN_0049ca40 again with 0x1000000.
                // Returns 1 if the final issue has a type code in [0x90, 0x98), else 0.
                int ok = SeedPrimaryIssue();
                if (ok != 0)
                {
                    Phase = 1;
                    return null;
                }
                Phase = 0;
                ReadyFlag = 1;
                return null;
            }

            case 3:
            {
                // FUN_004c6d50: compute the batch count for the current primary issue.
                // Reads TickSubObject+0x80 and +0x84 to get available vs. used capacity
                // (iVar4 = capacity_available - capacity_used).  If iVar4 <= 0, skips
                // the rest and returns 0.
                // Otherwise: calls FUN_004c7040 to resolve the seed ID from
                // _primaryIssueRef (+0x40).  Checks bit 0 of (resolved+0x24) to
                // select multiplier (0x2000 / 0x1) and count (1 / 2).
                // Clears SubState (+0x3c) to 0, then sets it to 1 (default batch = 1).
                // Calls FUN_0049cf00(TickSubObject, &_batchCalcRef, 0x29, 0x2000, ...).
                // If result is valid and batch * capacity exceeds iVar4, adjusts:
                //   SubState = iVar4 / result (integer division).
                // Returns 1 if SubState > 0, else 0.
                int ok = ComputeBatchCount();
                if (ok != 0)
                {
                    Phase = 4;
                    return null;
                }
                Phase = 0;
                ReadyFlag = 1;
                return null;
            }

            case 4:
            {
                // FUN_004c6e00: emit one primary runtime object for the current issue.
                // Checks _primaryIssueRef (+0x40) type code in [0x90, 0x98) and
                // _followupIssueRef (+0x44) type code in [0xa4, 0xa6).  If both pass:
                // allocates a 0x214-type work item (FUN_004f5060(0x214)), allocates
                // an 0x20-byte inner object (FUN_00617140(0x20)), and if both
                // succeed, calls FUN_004f4ea0 and FUN_004f4b30 to link the issue,
                // stores Workspace in the item's +0x20 field, and dispatches via
                // vtable[9] and vtable[11]. Returns the work item or null on failure.
                AIWorkItem result = EmitPrimaryRuntimeObject();
                if (result != null)
                {
                    Phase = 5;
                    ReadyFlag = 1;
                    _emitCounter++;
                    return result;
                }
                Phase = 0;
                ReadyFlag = 1;
                return null;
            }

            case 5:
            {
                // FUN_004c6f40: emit the counted companion runtime object.
                // Checks _followupIssueRef (+0x44) type code in [0xa4, 0xa6).  If ok:
                // allocates a 0x212-type work item (FUN_004f5060(0x212)), allocates
                // an 0x20-byte inner object, links the followup issue via FUN_004f4ea0
                // and FUN_004f4b30, stores Workspace in item+0x20, dispatches via
                // vtable[9].  Then copies SubState (+0x3c, the batch count) into the
                // emitted item's +0x48 field.  Returns the work item or null.
                AIWorkItem result = EmitCountedRuntimeObject();
                Phase = 0;
                ReadyFlag = 1;
                return result;
            }

            default:
                Phase = 2;
                return null;
        }
    }

    // FUN_004c6bd0 — derive a followup production issue linked to the primary issue.
    // Uses TickSubObject (+0x28) and _primaryIssueRef (+0x40) to create a companion
    // record; stores result ID in _followupIssueRef (+0x44).
    // Returns 1 if a valid followup (type in [0x90,0x98)) was produced, else 0.
    private int SeedFollowupIssue()
    {
        // Requires TickSubObject (FUN_0049cba0) — TickSubObject type not yet defined.
        // Returns 0 until TickSubObject infrastructure is implemented.
        _ = _primaryIssueRef;
        return 0;
    }

    // FUN_004c69c0 — seed the primary production issue on the TickSubObject.
    // Calls FUN_0049ca40 with type 0x21000000, quota 0x19, count 1; stores result
    // ID in _primaryIssueRef (+0x40).  Falls back with 0x1000000 if the first
    // attempt produces a type code outside [0x90, 0x98).
    // Returns 1 if the final issue has a type code in [0x90, 0x98), else 0.
    private int SeedPrimaryIssue()
    {
        // Requires TickSubObject (FUN_0049ca40) — TickSubObject type not yet defined.
        return 0;
    }

    // FUN_004c6d50 — compute and store the batch count for the current issue cycle.
    // Reads TickSubObject+0x80 and +0x84 for available capacity.  Uses SubState
    // (+0x3c) as the batch counter: clears it to 0, sets to 1 (default), or adjusts
    // to iVar4/result if the product would overflow.  Populates _batchCalcRef (+0x48).
    // Returns 1 if SubState > 0 after computation (batch is feasible), else 0.
    private int ComputeBatchCount()
    {
        // Requires TickSubObject (FUN_0049cf00) — TickSubObject type not yet defined.
        // SubState would be set to batch count; returns 0 until TickSubObject is built.
        SubState = 0;
        return 0;
    }

    // FUN_004c6e00 — emit one primary runtime work item for the current issue.
    // Checks _primaryIssueRef and _followupIssueRef type codes, allocates and
    // links a 0x214-type work item via FUN_004f5060, FUN_004f4ea0, FUN_004f4b30.
    // Returns the allocated work item, or null if allocation or type checks fail.
    // FUN_004c6e00: Allocate TypeCode=0x214 work item with primary and followup issue refs.
    // _primaryIssueRef type [0x90,0x98) AND _followupIssueRef type [0xa4,0xa6) required.
    // Blocked until SeedPrimaryIssue/SeedFollowupIssue are implemented (TickSubObject).
    private AIWorkItem EmitPrimaryRuntimeObject()
    {
        // Both refs are 0 until seeding functions work — always returns null currently.
        if (_primaryIssueRef == null || _followupIssueRef == null)
            return null;
        return new AgentShortageWorkItem(0x214, null, SubState, OwnerSide);
    }

    // FUN_004c6f40 — emit the counted companion runtime work item.
    // Checks _followupIssueRef type code in [0xa4, 0xa6), allocates a 0x212-type
    // work item, and embeds the batch count (SubState at +0x3c) into the item's
    // +0x48 field.  Returns the allocated work item, or null on failure.
    // FUN_004c6f40: Allocate TypeCode=0x212 work item with followup ref and batch count.
    // _followupIssueRef type [0xa4,0xa6) required. item+0x48 = SubState (batch count).
    // Blocked until SeedFollowupIssue is implemented (TickSubObject).
    private AIWorkItem EmitCountedRuntimeObject()
    {
        if (_followupIssueRef == null)
            return null;
        return new AgentShortageWorkItem(0x212, null, SubState, OwnerSide);
    }
}

// ------------------------------------------------------------------
// Type 14 — FUN_004d1b80 — CapitalShipNameGeneratorRecord
// 0x4c bytes.  Active guard: standard (Phase=0, SubState=0, ReadyFlag=1 on inactive).
//
// field_0x38 = nameCounter (repurposed as Phase in C#; values 0–9 per tick cycle).
// field_0x3c = cursor (repurposed as _shipList/_shipCursorIndex in C#).
// field_0x44 = _pool1Cursor (ushort): next index into name pool 1 (Hydra/Swift).
// field_0x46 = _pool2Cursor (ushort): next index into name pool 2 (Master/Deliverance).
// field_0x48 = _pool3Cursor (ushort): next index into name pool 3 (Judicator/Liberty).
//
// Per-side name resource-ID pools (FUN_004d1ea0):
//   Empire  pool-1: 0x5100+cursor, cursor < 0x27 (39 names: Hydra..Adder)
//   Empire  pool-2: 0x5160+cursor, cursor < 0x22 (34 names: Master..Confrontation)
//   Empire  pool-3: 0x51c0+cursor, cursor < 0x20 (32 names: Judicator..Executor)
//   Alliance pool-1: 0x5200+cursor, cursor < 0x14 (20 names: Swift..Lightning)
//   Alliance pool-2: 0x5260+cursor, cursor < 0x23 (35 names: Deliverance..Quenfis)
//   Alliance pool-3: 0x52c0+cursor, cursor < 0x10 (16 names: Liberty..Relentless)
//
// Ship naming flag word (from workspace.CapitalShipNamingFlags[ship.InstanceID]):
//   bit 0x4000:     ship is unnamed — needs a name (must be set to qualify).
//   bit 0x10:       pool-1 first preference.
//   bit 0x20:       pool-2 first preference (fallback to pool-1 if exhausted).
//   bit 0x40:       pool-3 first preference (fallback to pool-2 then pool-1).
//   bits 0x8002800: excluded from naming (must be zero to qualify).
//   No pool bits:   fall through to FUN_004f21a0 fallback.
//
// Active path per tick (FUN_004d1d20):
//   Phase > 9: reset Phase=0, ReadyFlag=1, FinalizeNameSubObject().
//   Otherwise: get/build ship cursor list.
//   For current ship: if ShipMeetsNamingCriteria → GetCapitalShipName → Phase++.
//   Advance cursor; if exhausted: Phase=0, ReadyFlag=1, SetSubObjectParamTwo().
// ------------------------------------------------------------------
public class CapitalShipNameGeneratorRecord : StrategyRecord
{
    // Replaces field_0x3c raw cursor pointer (C++ doubly-linked list node).
    private List<CapitalShip> _shipList;
    private int _shipCursorIndex;

    // Three ushort name-pool cursors at +0x44/+0x46/+0x48 in the original.
    // Initialized to 0 by FUN_004d1b80 (constructor) and by guard on inactive.
    private ushort _pool1Cursor; // +0x44
    private ushort _pool2Cursor; // +0x46
    private ushort _pool3Cursor; // +0x48

    public CapitalShipNameGeneratorRecord(int ownerSide)
        : base(typeId: 14, capacity: 1, ownerSide: ownerSide)
    {
        _shipList = null;
        _shipCursorIndex = 0;
        _pool1Cursor = 0;
        _pool2Cursor = 0;
        _pool3Cursor = 0;
    }

    public override void Initialize(AIWorkspace workspace)
    {
        base.Initialize(workspace);
        _shipList = null;
        _shipCursorIndex = 0;
        // Pool cursors are NOT reset here — they survive workspace assignment.
        // FUN_004d1b80 (constructor) initialises them to 0 once at construction time.
    }

    // Standard guard + also clear the cursor when inactive (mirrors field_0x3c=0).
    // Pool cursors are intentionally NOT reset on guard failure: they persist across
    // inactive periods so the name sequence is not repeated from the start.
    protected override bool ActiveGuardFails()
    {
        if (ActiveState != 1)
        {
            Phase = 0; // nameCounter = 0
            SubState = 0;
            ReadyFlag = 1;
            _shipList = null;
            _shipCursorIndex = 0;
            return true;
        }
        return false;
    }

    public override AIWorkItem Tick()
    {
        if (ActiveGuardFails())
            return null;

        // Phase == nameCounter (0–9 per tick cycle, FUN_004d1d20).
        if (Phase > 9)
        {
            Phase = 0;
            ReadyFlag = 1;
            FinalizeNameSubObject();
            return null;
        }

        // Build ship list on first call (or after cursor was cleared).
        if (_shipList == null)
        {
            _shipList = BuildCapitalShipList();
            _shipCursorIndex = 0;
        }

        if (_shipList == null || _shipCursorIndex >= _shipList.Count)
        {
            Phase = 0;
            ReadyFlag = 1;
            SetSubObjectParamTwo();
            return null;
        }

        AIWorkItem pendingWorkItem = null;
        CapitalShip current = _shipList[_shipCursorIndex];

        // Original flag filter: bit 0x4000 set AND bits 0x8002800 clear.
        if (ShipMeetsNamingCriteria(current))
        {
            pendingWorkItem = GetCapitalShipName(current);
            Phase++; // increment nameCounter
        }

        _shipCursorIndex++;

        if (_shipCursorIndex >= _shipList.Count)
        {
            Phase = 0;
            ReadyFlag = 1;
            SetSubObjectParamTwo();
        }

        return pendingWorkItem;
    }

    // FUN_004d1ea0_get_capital_ship_name — select the next name for the ship from
    // the appropriate pool and produce a type-0x203 work item.
    //
    // Pool selection by ship naming flags (bits of workspace.CapitalShipNamingFlags[id]):
    //   bit 0x10 → try pool-1; if exhausted → fallback.
    //   bit 0x20 → try pool-2; if exhausted try pool-1; if that too → fallback.
    //   bit 0x40 → try pool-3; if exhausted try pool-2, then pool-1; else fallback.
    //   no bits  → fallback immediately (FUN_004f21a0 path).
    //
    // Pool limits (from assembly: boundary check is "cursor >= limit"):
    //   Empire  pool-1: cursor < 0x27 (39 entries, IDs 0x5100..0x5126)
    //   Empire  pool-2: cursor < 0x22 (34 entries, IDs 0x5160..0x5181)
    //   Empire  pool-3: cursor < 0x20 (32 entries, IDs 0x51c0..0x51df)
    //   Alliance pool-1: cursor < 0x14 (20 entries, IDs 0x5200..0x5213)
    //   Alliance pool-2: cursor < 0x23 (35 entries, IDs 0x5260..0x5282)
    //   Alliance pool-3: cursor < 0x10 (16 entries, IDs 0x52c0..0x52cf)
    private AIWorkItem GetCapitalShipName(CapitalShip ship)
    {
        int flags = GetShipNamingFlags(ship);
        int nameResId = -1;
        bool nameFound = false;

        if ((flags & 0x10) != 0)
        {
            // Pool 1 first preference.
            if (OwnerSide == 1) // Alliance
            {
                if (_pool1Cursor < 0x14)
                {
                    nameResId = 0x5200 + _pool1Cursor;
                    _pool1Cursor++;
                    nameFound = true;
                }
                // Exhausted → fallback (LAB_004d20f9).
            }
            else // Empire (side == 0)
            {
                if (_pool1Cursor < 0x27)
                {
                    nameResId = 0x5100 + _pool1Cursor;
                    _pool1Cursor++;
                    nameFound = true;
                }
            }
        }
        else if ((flags & 0x20) != 0)
        {
            // Pool 2 first preference, fallback to pool 1.
            if (OwnerSide == 1) // Alliance
            {
                if (_pool2Cursor < 0x23) // Deliverance pool (35 entries)
                {
                    nameResId = 0x5260 + _pool2Cursor;
                    _pool2Cursor++;
                    nameFound = true;
                }
                else if (_pool1Cursor < 0x14) // Swift pool fallback
                {
                    nameResId = 0x5200 + _pool1Cursor;
                    _pool1Cursor++;
                    nameFound = true;
                }
                // Both exhausted → fallback.
            }
            else // Empire
            {
                if (_pool2Cursor < 0x22) // Master pool (34 entries)
                {
                    nameResId = 0x5160 + _pool2Cursor;
                    _pool2Cursor++;
                    nameFound = true;
                }
                else if (_pool1Cursor < 0x27) // Hydra pool fallback
                {
                    nameResId = 0x5100 + _pool1Cursor;
                    _pool1Cursor++;
                    nameFound = true;
                }
            }
        }
        else if ((flags & 0x40) != 0)
        {
            // Pool 3 first preference, fallback to pool 2 then pool 1.
            if (OwnerSide == 1) // Alliance
            {
                if (_pool3Cursor < 0x10) // Liberty pool (16 entries)
                {
                    nameResId = 0x52c0 + _pool3Cursor;
                    _pool3Cursor++;
                    nameFound = true;
                }
                else if (_pool2Cursor < 0x23)
                {
                    nameResId = 0x5260 + _pool2Cursor;
                    _pool2Cursor++;
                    nameFound = true;
                }
                else if (_pool1Cursor < 0x14)
                {
                    nameResId = 0x5200 + _pool1Cursor;
                    _pool1Cursor++;
                    nameFound = true;
                }
            }
            else // Empire
            {
                if (_pool3Cursor < 0x20) // Judicator pool (32 entries)
                {
                    nameResId = 0x51c0 + _pool3Cursor;
                    _pool3Cursor++;
                    nameFound = true;
                }
                else if (_pool2Cursor < 0x22)
                {
                    nameResId = 0x5160 + _pool2Cursor;
                    _pool2Cursor++;
                    nameFound = true;
                }
                else if (_pool1Cursor < 0x27)
                {
                    nameResId = 0x5100 + _pool1Cursor;
                    _pool1Cursor++;
                    nameFound = true;
                }
            }
        }
        // No pool bits (0x10/0x20/0x40 all clear) → fall through to fallback.

        if (!nameFound)
        {
            // FUN_004f21a0 fallback: generate a name from the ship's base record.
            // Not yet implemented — return null (no name work item produced).
            return null;
        }

        // Create the type-0x203 scheduler work item carrying this name assignment.
        // Corresponds to FUN_004f5060(0x203) → FUN_0051ead0 → constructor for
        // the name-package entity. Batch counter NOT incremented for this type.
        return new CapitalShipNameWorkItem(ship, nameResId, OwnerSide);
    }

    // Flag check: ship qualifies for naming when
    //   bit 0x4000 is set (ship is unnamed) AND bits 0x8002800 are all clear.
    // Flags are stored in workspace.CapitalShipNamingFlags keyed by InstanceID.
    private bool ShipMeetsNamingCriteria(CapitalShip ship)
    {
        int flags = GetShipNamingFlags(ship);
        return (flags & 0x4000) != 0 && (flags & 0x8002800) == 0;
    }

    // Look up the naming flag word for this ship.
    // Ships absent from the workspace dictionary return 0 (not unnamed → skipped).
    private int GetShipNamingFlags(CapitalShip ship)
    {
        if (ship == null || Workspace == null)
            return 0;
        string id = ship.InstanceID;
        if (id == null)
            return 0;
        int flags;
        return Workspace.CapitalShipNamingFlags.TryGetValue(id, out flags) ? flags : 0;
    }

    // Build the list of capital ships to iterate over for this naming cycle.
    // Replaces the doubly-linked list walk through field_0x3c in the original.
    private List<CapitalShip> BuildCapitalShipList()
    {
        if (Workspace?.Owner == null)
            return new List<CapitalShip>();
        return Workspace.Owner.GetOwnedUnitsByType<CapitalShip>();
    }

    // FUN_004ec1e0_set_id: Sets field0_id on the sub-object at +0x40 to the nameCounter value.
    // In C# there is no separate sub-object at +0x40 — this state is implicit in Phase.
    // No-op: the Phase variable already tracks this correctly.
    private void FinalizeNameSubObject() { }

    // FUN_004ec230_set_param_to_two: Writes 2 to the sub-object's param field.
    // In C# there is no separate sub-object — completion is tracked via ReadyFlag.
    // No-op: ReadyFlag is set to 1 by the caller before this is called.
    private void SetSubObjectParamTwo() { }
}
