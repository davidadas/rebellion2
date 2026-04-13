using System;
using System.Collections.Generic;
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

    // Extra fields within the 0x78-byte struct (base=0x40; extra at +0x40..+0x74).
    // +0x48: agent type+mode packed into a uint, written in GenerateShortageIssue case 6.
    //   0x2d000002 = agent type 0x2d, mode 2 (agent slot available)
    //   0x2c000001 = agent type 0x2c, mode 1 (agent slots full)
    private int _entityTypePacked;

    // +0x4c: flag set to 1 in GenerateShortageIssue case 6 (non-zero capacity-check path),
    //   consumed by FUN_004db9c0 (FindAgentForShortage).
    private int _agentCapacityFlag;

    public LocalShortageGeneratorType1Record(int ownerSide)
        : base(typeId: 1, capacity: 1, ownerSide: ownerSide) { }

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

    // FUN_004da010: Precondition check 1. Non-zero → Phase=PhaseGenerateIssue, return null.
    private int PreconditionCheck1()
    {
        // INCOMPLETE(engine): LocalShortageSystem does not yet support precondition check 1 for Type 1 (FUN_004da010).
        return 0;
    }

    // FUN_004da280: Precondition check 2. Non-zero → Phase=PhaseGenerateIssue, SubState=0, return null.
    private int PreconditionCheck2()
    {
        // INCOMPLETE(engine): LocalShortageSystem does not yet support precondition check 2 for Type 1 (FUN_004da280).
        return 0;
    }

    // FUN_004dbfb0: Updates the shortage fleet tracking state. Called conditionally on EntityTargetType.
    private void UpdateShortageFleet()
    {
        // INCOMPLETE(engine): ShortageFleetSystem does not yet support shortage fleet state update for Type 1 (FUN_004dbfb0).
    }

    // FUN_004dc490: Finalizes the shortage record entry. Called unconditionally after fleet update.
    private void FinalizeShortageRecord()
    {
        // INCOMPLETE(engine): ShortageRecordSystem does not yet support shortage record finalization for Type 1 (FUN_004dc490).
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

    // FUN_004da880: Shortage condition check for the fleet path.
    // Non-zero → fleet shortage candidate exists; zero → terminal (Phase=0, SubState=0, RF=1).
    private int CheckShortageConditionType1()
    {
        // INCOMPLETE(engine): ShortageConditionSystem does not yet support Type 1 shortage condition check (FUN_004da880).
        return 0;
    }

    // FUN_004dab90: Finds the next shortage fleet candidate.
    // Returns a work item if a candidate is found, null otherwise.
    private AIWorkItem FindShortageFleet()
    {
        // INCOMPLETE(engine): ShortageFleetSystem does not yet support shortage fleet candidate search for Type 1 (FUN_004dab90).
        return null;
    }

    // FUN_004db0d0: Computes the next sub-state for the shortage fleet walk.
    // Returns new SubState; 0 = walk complete (Phase=0 transition).
    private int GetNextShortageSubState()
    {
        // INCOMPLETE(engine): ShortageSubStateSystem does not yet support shortage sub-state computation for Type 1 (FUN_004db0d0).
        return 0;
    }

    // FUN_004db1e0: Creates the fleet shortage issue work item. Terminal.
    // Return value forwarded to caller.
    private AIWorkItem CreateFleetShortageIssue()
    {
        // INCOMPLETE(engine): FleetShortageIssueSystem does not yet support fleet shortage issue creation for Type 1 (FUN_004db1e0).
        return null;
    }

    // FUN_004db760: Selects an available agent slot for shortage assignment.
    // Reads _entityTypePacked (+0x48). Non-zero → slot found; zero → no slot.
    private int SelectAgentSlotAvailable()
    {
        // INCOMPLETE(engine): AgentSlotSystem does not yet support available-slot agent selection for Type 1 (FUN_004db760).
        return 0;
    }

    // FUN_004db4c0: Selects a full agent slot for shortage assignment.
    // Reads _entityTypePacked (+0x48). Non-zero → slot found; zero → no slot.
    private int SelectAgentSlotFull()
    {
        // INCOMPLETE(engine): AgentSlotSystem does not yet support full-slot agent selection for Type 1 (FUN_004db4c0).
        return 0;
    }

    // FUN_004db9c0: Finds an agent for shortage assignment.
    // Reads _agentCapacityFlag (+0x4c). Returns work item or null.
    private AIWorkItem FindAgentForShortage()
    {
        // INCOMPLETE(engine): AgentShortageSystem does not yet support agent shortage candidate search for Type 1 (FUN_004db9c0).
        return null;
    }

    // FUN_004dbd60: Creates the agent shortage work item.
    // Non-null → SubState=9; null → terminal.
    private AIWorkItem CreateAgentShortageItem()
    {
        // INCOMPLETE(engine): AgentShortageSystem does not yet support agent shortage item creation for Type 1 (FUN_004dbd60).
        return null;
    }

    // FUN_004dbea0: Finalizes the agent shortage item. Terminal.
    // Return value forwarded to caller.
    private AIWorkItem FinalizeAgentShortageItem()
    {
        // INCOMPLETE(engine): AgentShortageSystem does not yet support agent shortage item finalization for Type 1 (FUN_004dbea0).
        return null;
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

    public LocalShortageGeneratorType2Record(int ownerSide)
        : base(typeId: 2, capacity: 1, ownerSide: ownerSide) { }

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

    // FUN_004e1540: Initial setup / precondition check. Non-zero → set Phase based on agent
    // capacity comparison; zero → Phase=PhaseAgentIssue.
    private int InitialSetupCheck()
    {
        // INCOMPLETE(engine): LocalShortageType2System does not yet support initial setup check for Type 2 (FUN_004e1540).
        return 0;
    }

    // FUN_004e1930: Fleet/agent issue handler dispatched from phases 0x3ea and 0x3eb.
    // Return value forwarded directly to caller.
    private AIWorkItem HandleFleetIssue()
    {
        // INCOMPLETE(engine): FleetIssueSystem does not yet support fleet issue handling for Type 2 (FUN_004e1930).
        return null;
    }

    // FUN_004e1770: Agent/fallback issue handler dispatched from phase 0x3ec.
    // Return value forwarded directly to caller.
    private AIWorkItem HandleAgentIssue()
    {
        // INCOMPLETE(engine): AgentIssueSystem does not yet support agent issue handling for Type 2 (FUN_004e1770).
        return null;
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

    // Extra fields at +0x54..+0x60 (beyond the 0x40-byte base, within the 0x7c total).
    private int _phaseCSubObjRef; // +0x54: passed to FUN_004d91e0 in Phase C state 11
    private int _phaseASubObjRef; // +0x58: passed to FUN_004d91e0 in Phase A state 11
    private int _typeModePacked; // +0x5c: agent type+mode (0x2d000002 or 0x2c000001)
    private int _agentMatchFlag; // +0x60: 1 when Phase C state 9 found a matching agent

    public ShortageGeneratorType3Record(int ownerSide)
        : base(typeId: 3, capacity: 1, ownerSide: ownerSide)
    {
        _phaseCSubObjRef = 0;
        _phaseASubObjRef = 0;
        _typeModePacked = 0;
        _agentMatchFlag = 0;
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

    // --- INCOMPLETE helper stubs for RunPhaseB ---

    // FUN_004d6550: Checks the shortage condition for Phase B.
    // Non-zero → advance; zero → terminal.
    private int CheckShortageConditionB()
    {
        // INCOMPLETE(engine): ShortageConditionSystem does not yet support Phase B shortage condition check for Type 3 (FUN_004d6550).
        return 0;
    }

    // FUN_004d66a0: Creates a fleet shortage work item for Phase B.
    // Return value forwarded to caller.
    private AIWorkItem CreateFleetShortageItemB()
    {
        // INCOMPLETE(engine): FleetShortageSystem does not yet support Phase B fleet shortage item creation for Type 3 (FUN_004d66a0).
        return null;
    }

    // FUN_004d6a10: Checks agent assignment eligibility for Phase B.
    // Non-zero → advance; zero → terminal.
    private int CheckAgentAssignmentB()
    {
        // INCOMPLETE(engine): AgentAssignmentSystem does not yet support Phase B agent assignment check for Type 3 (FUN_004d6a10).
        return 0;
    }

    // FUN_004d7890 (Phase B): Creates an agent shortage work item for Phase B.
    // Return value forwarded to caller.
    private AIWorkItem CreateAgentShortageItemB()
    {
        // INCOMPLETE(engine): AgentShortageSystem does not yet support Phase B agent shortage item creation for Type 3 (FUN_004d7890).
        return null;
    }

    // --- INCOMPLETE helper stubs for RunPhaseC ---

    // FUN_004d6a60: Checks agent assignment eligibility for Phase C.
    // Non-zero → advance; zero → terminal.
    private int CheckAgentAssignmentC()
    {
        // INCOMPLETE(engine): AgentAssignmentSystem does not yet support Phase C agent assignment check for Type 3 (FUN_004d6a60).
        return 0;
    }

    // FUN_004d6e30: Checks for a fleet issue during Phase C.
    // Non-zero → advance; zero → terminal.
    private int CheckFleetIssueC()
    {
        // INCOMPLETE(engine): FleetIssueSystem does not yet support Phase C fleet issue check for Type 3 (FUN_004d6e30).
        return 0;
    }

    // FUN_004d77d0: Computes the next agent sub-state for Phase C.
    // Returns new SubState; 0 means done.
    private int GetNextAgentSubState()
    {
        // INCOMPLETE(engine): AgentSubStateSystem does not yet support Phase C agent sub-state computation for Type 3 (FUN_004d77d0).
        return 0;
    }

    // FUN_004d7060: Creates an agent match work item for Phase C.
    // Return value forwarded to caller.
    private AIWorkItem CreateAgentMatchItem()
    {
        // INCOMPLETE(engine): AgentMatchSystem does not yet support Phase C agent match item creation for Type 3 (FUN_004d7060).
        return null;
    }

    // FUN_004d7890 (Phase C): Creates an agent shortage work item for Phase C.
    // Return value forwarded to caller.
    private AIWorkItem CreateAgentShortageItemC()
    {
        // INCOMPLETE(engine): AgentShortageSystem does not yet support Phase C agent shortage item creation for Type 3 (FUN_004d7890).
        return null;
    }

    // FUN_004d8d40: Selects an available agent type slot.
    // Non-zero → slot found; zero → no slot.
    private int SelectAgentTypeSlotAvail()
    {
        // INCOMPLETE(engine): AgentSlotSystem does not yet support available agent type slot selection for Type 3 (FUN_004d8d40).
        return 0;
    }

    // FUN_004d8c10: Selects a full agent type slot.
    // Non-zero → slot found; zero → no slot.
    private int SelectAgentTypeSlotFull()
    {
        // INCOMPLETE(engine): AgentSlotSystem does not yet support full agent type slot selection for Type 3 (FUN_004d8c10).
        return 0;
    }

    // FUN_004d8e70: Creates a work item for the selected agent slot.
    // Return value forwarded to caller.
    private AIWorkItem CreateAgentSlotItem()
    {
        // INCOMPLETE(engine): AgentSlotSystem does not yet support agent slot work item creation for Type 3 (FUN_004d8e70).
        return null;
    }

    // FUN_004d91e0 (Phase C, &_phaseCSubObjRef): Dispatches the Phase C sub-object.
    // Return value forwarded to caller.
    private AIWorkItem DispatchPhaseCSubObject()
    {
        // INCOMPLETE(engine): SubObjectDispatchSystem does not yet support Phase C sub-object dispatch for Type 3 (FUN_004d91e0).
        return null;
    }

    // FUN_004d9320: Finalizes the agent assignment after dispatch.
    // Return value forwarded to caller.
    private AIWorkItem FinalizeAgentAssignment()
    {
        // INCOMPLETE(engine): AgentAssignmentSystem does not yet support agent assignment finalization for Type 3 (FUN_004d9320).
        return null;
    }

    // --- INCOMPLETE helper stubs for RunPhaseA ---

    // FUN_004d7e40: Checks the Phase A primary condition.
    // Non-zero → advance; zero → terminal.
    private int CheckPhaseACondition()
    {
        // INCOMPLETE(engine): PhaseASystem does not yet support Phase A primary condition check for Type 3 (FUN_004d7e40).
        return 0;
    }

    // FUN_004d8120: Checks the Phase A agent condition.
    // Non-zero → advance; zero → terminal.
    private int CheckPhaseAAgentCondition()
    {
        // INCOMPLETE(engine): PhaseASystem does not yet support Phase A agent condition check for Type 3 (FUN_004d8120).
        return 0;
    }

    // FUN_00419160(workspace): Advances the workspace bit-selection state.
    private void ClearFleetState()
    {
        Workspace.AdvanceBitSelection();
    }

    // FUN_004d8890: Computes the next Phase A sub-state.
    // Returns new SubState; 0 means done.
    private int GetNextPhaseASubState()
    {
        // INCOMPLETE(engine): PhaseASystem does not yet support Phase A sub-state computation for Type 3 (FUN_004d8890).
        return 0;
    }

    // FUN_004d8350: Creates a fleet assignment work item for Phase A.
    // Return value forwarded to caller.
    private AIWorkItem CreatePhaseAFleetItem()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support Phase A fleet item creation for Type 3 (FUN_004d8350).
        return null;
    }

    // FUN_004d8930: Creates an agent assignment work item for Phase A.
    // Return value forwarded to caller.
    private AIWorkItem CreatePhaseAAgentItem()
    {
        // INCOMPLETE(engine): AgentAssignmentSystem does not yet support Phase A agent item creation for Type 3 (FUN_004d8930).
        return null;
    }

    // FUN_004d91e0 (Phase A, &_phaseASubObjRef): Dispatches the Phase A sub-object.
    // Return value forwarded to caller.
    private AIWorkItem DispatchPhaseASubObject()
    {
        // INCOMPLETE(engine): SubObjectDispatchSystem does not yet support Phase A sub-object dispatch for Type 3 (FUN_004d91e0).
        return null;
    }

    // FUN_004d9440: Finalizes fleet assignment state for Phase A. Return value discarded.
    private void FinalizeFleetAssignmentA()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support Phase A fleet assignment finalization for Type 3 (FUN_004d9440).
    }

    // FUN_004d9980: Finalizes the shortage record for Phase A. Return value discarded.
    private void FinalizeShortageRecordA()
    {
        // INCOMPLETE(engine): ShortageRecordSystem does not yet support Phase A shortage record finalization for Type 3 (FUN_004d9980).
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

        // Entity found: vtable[11] call — not yet implemented.
        // Stub: treat as "absorbed" (write 0 to dispatchOut, return null).
        dispatchOut = 0;
        return null;
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

    // --- INCOMPLETE helper stubs for RunPhaseA ---

    // FUN_004cfbd0: Scans candidate list for an eligible fleet entity.
    // Non-zero result → SubState=9 (bit-selection check); zero → SubState=7 (dispatch check).
    private int ScanFleetCandidatesPhaseA()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support fleet candidate scanning for Type 5 PhaseA (FUN_004cfbd0).
        return 0;
    }

    // FUN_004cfeb0: Checks fleet assignment eligibility given the current workspace bit-selection
    // context (EntityTargetType == 0x4). Returns non-zero if eligible.
    private int CheckFleetAssignmentEligibility()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support fleet assignment eligibility check for Type 5 (FUN_004cfeb0).
        return 0;
    }

    // FUN_004d00a0: Creates a fleet assignment work item for the selected candidate.
    // Returns work item or null.
    private AIWorkItem CreateFleetAssignmentWorkItem()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support fleet assignment work item creation for Type 5 (FUN_004d00a0).
        return null;
    }

    // FUN_004d05e0: Computes the next SubState for the assignment pipeline.
    // Returns new SubState; 0 means done (Phase→PhaseB).
    private int ComputeAssignmentSubState()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support assignment sub-state computation for Type 5 (FUN_004d05e0).
        return 0;
    }

    // FUN_004d0080: Checks whether fleet dispatch conditions are met.
    // Non-zero → SubState=8 (dispatch); zero → SubState=0, Phase=PhaseB (no ReadyFlag).
    private int CheckFleetDispatchCondition()
    {
        // INCOMPLETE(engine): FleetDispatchSystem does not yet support fleet dispatch condition check for Type 5 (FUN_004d0080).
        return 0;
    }

    // FUN_004d0680: Creates a work item dispatching the fleet to the selected target.
    // Return value forwarded to terminal.
    private AIWorkItem CreateFleetDispatchWorkItem()
    {
        // INCOMPLETE(engine): FleetDispatchSystem does not yet support fleet dispatch work item creation for Type 5 (FUN_004d0680).
        return null;
    }

    // FUN_004d0a80 (__thiscall, param = ptr to record+0x5c): Dispatches the entity referenced
    // at record+0x5c to its assignment target. Returns work item or null.
    private AIWorkItem DispatchEntityToTarget()
    {
        // INCOMPLETE(engine): EntityDispatchSystem does not yet support entity-to-target dispatch for Type 5 (FUN_004d0a80).
        return null;
    }

    // FUN_004d0bc0: Creates a follow-up work item after a successful entity dispatch.
    // Return value forwarded to terminal.
    private AIWorkItem CreateEntityTransferFollowup()
    {
        // INCOMPLETE(engine): EntityDispatchSystem does not yet support entity transfer follow-up for Type 5 (FUN_004d0bc0).
        return null;
    }

    // FUN_004d0ce0: Clears batch state and prepares mission batch records. Return value discarded.
    private void BuildMissionBatch()
    {
        // INCOMPLETE(engine): MissionBatchSystem does not yet support mission batch building for Type 5 (FUN_004d0ce0).
    }

    // FUN_004d1240: Selects mission candidates from the batch. Return value discarded.
    private void SelectMissionCandidates()
    {
        // INCOMPLETE(engine): MissionBatchSystem does not yet support mission candidate selection for Type 5 (FUN_004d1240).
    }

    // --- INCOMPLETE helper stubs for RunPhaseB ---

    // FUN_004cf510: Scans for fleet entities eligible for PhaseB dispatch.
    // Non-zero → SubState=1; zero → SubState=6.
    private int ScanFleetCandidatesPhaseB()
    {
        // INCOMPLETE(engine): FleetDispatchSystem does not yet support PhaseB candidate scanning for Type 5 (FUN_004cf510).
        return 0;
    }

    // FUN_004cf7f0: Evaluates fleet dispatch readiness.
    // Non-zero → SubState=5 (compute sub-state); zero → SubState=6 (dispatch condition check).
    private int EvaluateFleetDispatchStatus()
    {
        // INCOMPLETE(engine): FleetDispatchSystem does not yet support fleet dispatch status evaluation for Type 5 (FUN_004cf7f0).
        return 0;
    }

    // FUN_004cf900: Computes the next SubState for the transport pipeline.
    // Returns new SubState; 0 means done (Phase→PhaseA).
    private int ComputeTransportSubState()
    {
        // INCOMPLETE(engine): TransportSystem does not yet support transport sub-state computation for Type 5 (FUN_004cf900).
        return 0;
    }

    // FUN_004cf8e0: Checks whether transport dispatch conditions are met.
    // Non-zero → SubState=2; zero → terminal (SubState=0, Phase=PhaseA, ReadyFlag=1).
    private int CheckTransportDispatchCondition()
    {
        // INCOMPLETE(engine): TransportSystem does not yet support transport dispatch condition check for Type 5 (FUN_004cf8e0).
        return 0;
    }

    // FUN_004cf980: Creates a transport work item for the selected entity.
    // Return value forwarded to terminal.
    private AIWorkItem CreateTransportWorkItem()
    {
        // INCOMPLETE(engine): TransportSystem does not yet support transport work item creation for Type 5 (FUN_004cf980).
        return null;
    }

    // FUN_004d0960: Creates a fleet transfer work item (PhaseB terminal path case 0xb).
    // Return value forwarded to terminal.
    private AIWorkItem CreateFleetTransferWorkItem()
    {
        // INCOMPLETE(engine): FleetTransferSystem does not yet support fleet transfer work item creation for Type 5 (FUN_004d0960).
        return null;
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
    // All fields below are INCOMPLETE — none are accessed until the helper methods are implemented.
    //   +0x40..+0x53: various unresolved tracking and cursor fields.
    //   +0x54: entity reference passed by pointer to FUN_004dfd70 (PhaseC case 0xc).
    //   +0x58: entity reference passed by pointer to FUN_004dffd0 (PhaseA case 0xc).
    //   +0x5c..+0x8b: additional fields whose purpose was not resolved from the analysed callees.

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

    // --- INCOMPLETE helper stubs for RunPhaseA ---

    // FUN_004df030: Scans candidate list for eligible entities.
    // Non-zero → SubState=5 (bit-selection check); zero → terminal (PhaseB).
    private int ScanPhaseACandidates()
    {
        // INCOMPLETE(engine): EntityScanSystem does not yet support phase A candidate scanning for Type 6 (FUN_004df030).
        return 0;
    }

    // FUN_004df310: Checks entity filter eligibility given the current workspace bit-selection
    // context (EntityTargetType == 0x20). Returns non-zero if eligible.
    private int CheckEntityFilterEligibility()
    {
        // INCOMPLETE(engine): EntityFilterSystem does not yet support entity filter eligibility check for Type 6 (FUN_004df310).
        return 0;
    }

    // FUN_004df4b0: Creates a work item for the filtered entity.
    // Returns work item or null; non-null → ReadyFlag=1 + TickCounter++.
    private AIWorkItem CreateFilteredWorkItem()
    {
        // INCOMPLETE(engine): EntityFilterSystem does not yet support filtered work item creation for Type 6 (FUN_004df4b0).
        return null;
    }

    // FUN_004df9f0: Computes the next SubState for the entity pipeline.
    // Returns new SubState; 0 means done (Phase→PhaseB, ReadyFlag=1).
    private int ComputeEntityPipelineSubState()
    {
        // INCOMPLETE(engine): EntityPipelineSystem does not yet support entity pipeline sub-state computation for Type 6 (FUN_004df9f0).
        return 0;
    }

    // FUN_004dfa90: Builds a batch entity work item.
    // Return value forwarded to terminal (PhaseB).
    private AIWorkItem BuildEntityBatchItem()
    {
        // INCOMPLETE(engine): EntityBatchSystem does not yet support batch entity item construction for Type 6 (FUN_004dfa90).
        return null;
    }

    // FUN_004dffd0: Creates a candidate dispatch work item using the entity reference at +0x58.
    // Returns work item or null; null → terminal with ReadyFlag.
    private AIWorkItem CreateCandidateDispatchWorkItem()
    {
        // INCOMPLETE(engine): CandidateDispatchSystem does not yet support candidate dispatch work item creation for Type 6 (FUN_004dffd0).
        return null;
    }

    // FUN_004e0110: Creates a follow-up work item after a successful candidate dispatch (case 0xd).
    // Return value forwarded to terminal.
    private AIWorkItem CreateFollowupWorkItem()
    {
        // INCOMPLETE(engine): CandidateDispatchSystem does not yet support follow-up work item creation for Type 6 (FUN_004e0110).
        return null;
    }

    // FUN_004e08f0: Clears batch records in preparation for candidate selection. Return value discarded.
    private void ClearBatchRecords()
    {
        // INCOMPLETE(engine): BatchSystem does not yet support batch record clearing for Type 6 (FUN_004e08f0).
    }

    // FUN_004e0e40: Selects batch candidates from cleared records. Return value discarded.
    private void SelectBatchCandidates()
    {
        // INCOMPLETE(engine): BatchSystem does not yet support batch candidate selection for Type 6 (FUN_004e0e40).
    }

    // --- INCOMPLETE helper stubs for RunPhaseB ---

    // FUN_004dceb0: Stage 1 evaluation. Null → advance to stage 2; non-null → terminal (PhaseC).
    private AIWorkItem EvaluatePipelineStage1()
    {
        // INCOMPLETE(engine): PipelineSystem does not yet support stage 1 evaluation for Type 6 (FUN_004dceb0).
        return null;
    }

    // FUN_004dd470: Stage 2 evaluation. Null → advance to stage 3; non-null → terminal (PhaseC).
    private AIWorkItem EvaluatePipelineStage2()
    {
        // INCOMPLETE(engine): PipelineSystem does not yet support stage 2 evaluation for Type 6 (FUN_004dd470).
        return null;
    }

    // FUN_004dda30: Stage 3 evaluation. Null → advance to stage 4; non-null → terminal (PhaseC).
    private AIWorkItem EvaluatePipelineStage3()
    {
        // INCOMPLETE(engine): PipelineSystem does not yet support stage 3 evaluation for Type 6 (FUN_004dda30).
        return null;
    }

    // FUN_004ddee0: Stage 4 evaluation. Always → terminal (PhaseC).
    private AIWorkItem EvaluatePipelineStage4()
    {
        // INCOMPLETE(engine): PipelineSystem does not yet support stage 4 evaluation for Type 6 (FUN_004ddee0).
        return null;
    }

    // --- INCOMPLETE helper stubs for RunPhaseC ---

    // FUN_004de4a0: Checks PhaseC condition A.
    // Non-zero → SubState=6; zero → SubState=0xb.
    private int CheckPhaseCConditionA()
    {
        // INCOMPLETE(engine): PhaseCSystem does not yet support condition A check for Type 6 (FUN_004de4a0).
        return 0;
    }

    // FUN_004de780: Checks PhaseC condition B.
    // Non-zero → SubState=7; zero → SubState=0xb.
    private int CheckPhaseCConditionB()
    {
        // INCOMPLETE(engine): PhaseCSystem does not yet support condition B check for Type 6 (FUN_004de780).
        return 0;
    }

    // FUN_004dece0: Computes the next SubState for the PhaseC entity pipeline.
    // Returns new SubState; 0 means done (Phase→PhaseA, ReadyFlag=1).
    private int ComputePhaseCNextState()
    {
        // INCOMPLETE(engine): PhaseCSystem does not yet support next-state computation for Type 6 (FUN_004dece0).
        return 0;
    }

    // FUN_004dedc0: Creates the terminal work item for PhaseC case 8.
    // Return value forwarded to terminal (PhaseA).
    private AIWorkItem CreatePhaseCTerminalWorkItem()
    {
        // INCOMPLETE(engine): PhaseCSystem does not yet support terminal work item creation for Type 6 (FUN_004dedc0).
        return null;
    }

    // FUN_004dedb0: Checks PhaseC branch condition (case 0xb).
    // Non-zero → SubState=8; zero → SubState=0, Phase=PhaseA, NO ReadyFlag.
    private int CheckPhaseCBranchCondition()
    {
        // INCOMPLETE(engine): PhaseCSystem does not yet support branch condition check for Type 6 (FUN_004dedb0).
        return 0;
    }

    // FUN_004dfd70: Creates a dispatch work item using the entity reference at +0x54.
    // Returns work item or null; null → terminal with ReadyFlag.
    private AIWorkItem CreatePhaseCDispatchWorkItem()
    {
        // INCOMPLETE(engine): PhaseCDispatchSystem does not yet support dispatch work item creation for Type 6 (FUN_004dfd70).
        return null;
    }

    // FUN_004dfeb0: Creates a follow-up work item after a successful PhaseC dispatch (case 0xd).
    // Return value forwarded to terminal.
    private AIWorkItem CreatePhaseCFollowupWorkItem()
    {
        // INCOMPLETE(engine): PhaseCDispatchSystem does not yet support follow-up work item creation for Type 6 (FUN_004dfeb0).
        return null;
    }
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

    // Extra fields beyond the 0x40-byte base (total struct size 0x7c, 15 extra 4-byte fields).
    // All fields below are INCOMPLETE — none are accessed until the helper methods are implemented.
    //   +0x40..+0x53: various unresolved tracking and cursor fields.
    //   +0x54: entity reference passed by pointer to FUN_004d5140 (PhaseC case 0xb).
    //   +0x58: entity reference passed by pointer to FUN_004d53a0 (PhaseA case 0xb).
    //   +0x5c..+0x7b: additional fields whose purpose was not resolved from the analysed callees.

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

    // --- INCOMPLETE helper stubs for RunPhaseA ---

    // FUN_004d4370: Scans candidate list for eligible entities.
    // Non-zero → SubState=5 (bit-selection check); zero → terminal (PhaseB).
    private int ScanPhaseACandidates()
    {
        // INCOMPLETE(engine): EntityScanSystem does not yet support phase A candidate scanning for Type 7 (FUN_004d4370).
        return 0;
    }

    // FUN_004d4650: Checks entity filter eligibility given the current workspace bit-selection
    // context (EntityTargetType == 0x8). Returns non-zero if eligible.
    private int CheckEntityFilterEligibility()
    {
        // INCOMPLETE(engine): EntityFilterSystem does not yet support entity filter eligibility check for Type 7 (FUN_004d4650).
        return 0;
    }

    // FUN_004d4dc0: Computes the next SubState for the entity pipeline.
    // Returns new SubState; 0 means done (Phase→PhaseB, ReadyFlag=1).
    private int ComputeEntityPipelineSubState()
    {
        // INCOMPLETE(engine): EntityPipelineSystem does not yet support entity pipeline sub-state computation for Type 7 (FUN_004d4dc0).
        return 0;
    }

    // FUN_004d4880: Creates a work item for the filtered entity.
    // Returns work item or null; non-null → ReadyFlag=1 + TickCounter++.
    private AIWorkItem CreateFilteredWorkItem()
    {
        // INCOMPLETE(engine): EntityFilterSystem does not yet support filtered work item creation for Type 7 (FUN_004d4880).
        return null;
    }

    // FUN_004d4e60: Builds a batch entity work item.
    // Return value forwarded to terminal (PhaseB).
    private AIWorkItem BuildEntityBatchItem()
    {
        // INCOMPLETE(engine): EntityBatchSystem does not yet support batch entity item construction for Type 7 (FUN_004d4e60).
        return null;
    }

    // FUN_004d53a0: Creates a candidate dispatch work item using the entity reference at +0x58.
    // Returns work item or null; null → terminal with ReadyFlag.
    private AIWorkItem CreateCandidateDispatchWorkItem()
    {
        // INCOMPLETE(engine): CandidateDispatchSystem does not yet support candidate dispatch work item creation for Type 7 (FUN_004d53a0).
        return null;
    }

    // FUN_004d54e0: Creates a follow-up work item after a successful candidate dispatch (case 0xc).
    // Return value forwarded to terminal.
    private AIWorkItem CreateFollowupWorkItem()
    {
        // INCOMPLETE(engine): CandidateDispatchSystem does not yet support follow-up work item creation for Type 7 (FUN_004d54e0).
        return null;
    }

    // FUN_004d5600: Clears batch records in preparation for candidate selection. Return value discarded.
    private void ClearBatchRecords()
    {
        // INCOMPLETE(engine): BatchSystem does not yet support batch record clearing for Type 7 (FUN_004d5600).
    }

    // FUN_004d5b50: Selects batch candidates from cleared records. Return value discarded.
    private void SelectBatchCandidates()
    {
        // INCOMPLETE(engine): BatchSystem does not yet support batch candidate selection for Type 7 (FUN_004d5b50).
    }

    // --- INCOMPLETE helper stubs for RunPhaseB ---

    // FUN_004d2830: Checks the PhaseB init condition.
    // Non-zero → SubState=2; zero → SubState=0, Phase=PhaseC, NO ReadyFlag.
    private int CheckPhaseBInitCondition()
    {
        // INCOMPLETE(engine): PhaseBSystem does not yet support init condition check for Type 7 (FUN_004d2830).
        return 0;
    }

    // FUN_004d2980: Boolean pipeline condition check; non-zero → SubState=3; zero → SubState=4.
    // Assembly: CONCAT31 bool return via NEG;SBB;ADD 4 pattern.
    private int EvaluatePipelineCondition()
    {
        // INCOMPLETE(engine): PhaseBSystem does not yet support pipeline condition evaluation for Type 7 (FUN_004d2980).
        return 0;
    }

    // FUN_004d2e00: Stage 3 pipeline evaluation. Null → SubState=4; non-null → terminal (PhaseC).
    private AIWorkItem EvaluatePipelineStage3()
    {
        // INCOMPLETE(engine): PipelineSystem does not yet support stage 3 evaluation for Type 7 (FUN_004d2e00).
        return null;
    }

    // FUN_004d3120: Stage 4 pipeline evaluation. Always → terminal (PhaseC).
    private AIWorkItem EvaluatePipelineStage4()
    {
        // INCOMPLETE(engine): PipelineSystem does not yet support stage 4 evaluation for Type 7 (FUN_004d3120).
        return null;
    }

    // --- INCOMPLETE helper stubs for RunPhaseC ---

    // FUN_004d36e0: Checks PhaseC condition A (case 2).
    // Non-zero → SubState=6; zero → SubState=10.
    private int CheckPhaseCConditionA()
    {
        // INCOMPLETE(engine): PhaseCSystem does not yet support condition A check for Type 7 (FUN_004d36e0).
        return 0;
    }

    // FUN_004d3360: Checks PhaseC condition B (case 5, initial entry).
    // Non-zero → SubState=2; zero → SubState=10.
    private int CheckPhaseCConditionB()
    {
        // INCOMPLETE(engine): PhaseCSystem does not yet support condition B check for Type 7 (FUN_004d3360).
        return 0;
    }

    // FUN_004d3a50: Computes the next SubState for the PhaseC entity pipeline.
    // Returns new SubState; 0 means done (Phase→PhaseA, ReadyFlag=1).
    private int ComputePhaseCNextState()
    {
        // INCOMPLETE(engine): PhaseCSystem does not yet support next-state computation for Type 7 (FUN_004d3a50).
        return 0;
    }

    // FUN_004d3b70: Creates the terminal work item for PhaseC case 7.
    // Return value forwarded to terminal (PhaseA).
    private AIWorkItem CreatePhaseCTerminalWorkItem()
    {
        // INCOMPLETE(engine): PhaseCSystem does not yet support terminal work item creation for Type 7 (FUN_004d3b70).
        return null;
    }

    // FUN_004d3b20: Checks PhaseC branch condition (case 10).
    // Non-zero → SubState=7; zero → SubState=0, Phase=PhaseA, NO ReadyFlag.
    private int CheckPhaseCBranchCondition()
    {
        // INCOMPLETE(engine): PhaseCSystem does not yet support branch condition check for Type 7 (FUN_004d3b20).
        return 0;
    }

    // FUN_004d5140: Creates a dispatch work item using the entity reference at +0x54.
    // Returns work item or null; null → terminal with ReadyFlag.
    private AIWorkItem CreatePhaseCDispatchWorkItem()
    {
        // INCOMPLETE(engine): PhaseCDispatchSystem does not yet support dispatch work item creation for Type 7 (FUN_004d5140).
        return null;
    }

    // FUN_004d5280: Creates a follow-up work item after a successful PhaseC dispatch (case 0xc).
    // Return value forwarded to terminal.
    private AIWorkItem CreatePhaseCFollowupWorkItem()
    {
        // INCOMPLETE(engine): PhaseCDispatchSystem does not yet support follow-up work item creation for Type 7 (FUN_004d5280).
        return null;
    }
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
    // SectorSearchState < 0:
    //   Walk EntityTargetTable (workspace+0xd8) from tail backward.
    //   If any entry has (AssignmentId & 0x4000000)==0 (unassigned), do nothing.
    //   If ALL entries have the bit set (all assigned), call FUN_0042e9d0 to reset
    //   the search state.
    //
    // SectorSearchState > 0:
    //   Call FUN_0042ea50 to locate the next sector for the search.
    //
    // SectorSearchState == 0:
    //   No-op.
    private void InitSectorSearch()
    {
        int sectorSearch = Workspace.SectorSearchState;

        if (sectorSearch < 0)
        {
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
                // FUN_0042e9d0: reset sector search state in EntityTargetTable.
                // INCOMPLETE(engine): SectorSearchSystem does not yet support
                // EntityTargetTable sector search state reset.
            }
        }
        else if (sectorSearch > 0)
        {
            // FUN_0042ea50: find the next sector to assign fleet targets to.
            // INCOMPLETE(engine): SectorSearchSystem does not yet support
            // sector-find operations on the EntityTargetTable.
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
    // Looks up entity target by _entityTargetId in workspace+0xd8. Not found → 0.
    // Checks entry.StatusFlags>>0x18 in [0x80, 0x90) (target type gate).
    // If type check fails → skips to the secondary path (no sub_419330 call).
    // Checks workspace.FleetAssignmentSubObject (workspace+0x44) for a pre-existing
    // assignment matching this entity target (sub_4f4cc0):
    //   Found pre-existing: calls sub_476840 to finalize → returns result.
    // No pre-existing: tries up to three fleet issue creation calls:
    //   1. sub_419330(..., 0x10000, ..., 0x2) — fleet issue type A (only if type gate passed)
    //   2. sub_419980(..., 0x8010000, ..., 0x2) — fleet issue type B
    //   3. sub_419980(..., 0x10000, ..., 0x2) — fleet issue type C
    // Each successful issue calls sub_475d00 + sub_476840 to finalize.
    // Returns 1 on success (fleet assignment created or confirmed), 0 on failure.
    private int CreateFleetOrderForTarget()
    {
        MissionTargetEntry entry = Workspace.EntityTargetTable.Find(e => e.Id == _entityTargetId);
        if (entry == null)
            return 0;

        // Check entity target type code (high byte of StatusFlags) in [0x80, 0x90).
        int typeCode = (entry.StatusFlags >> 0x18) & 0xff;
        bool typeOk = typeCode >= 0x80 && typeCode < 0x90;

        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support fleet order
        // creation (sub_419330/sub_419980), pre-existing assignment check (sub_4f4cc0),
        // fleet finalization (sub_476840), or assignment linking (sub_475d00).
        _ = typeOk;
        return 0;
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

        // FUN_00476910: 6-state inner pipeline driving sector-to-fleet assignment.
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support the inner
        // entity-target dispatch pipeline (FUN_00476910).
        return null;
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
        // INCOMPLETE(engine): ScoutAttackSystem does not yet support the inner
        // selected-target dispatch pipeline (FUN_004737e0).
        return null;
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

    // Extra fields beyond 0x40-byte base (total struct size 0x84, 17 extra 4-byte fields).
    // All fields below are INCOMPLETE — none are accessed until the helper methods are implemented.
    //   +0x40..+0x54: six fields whose layout was not resolved from the analysed callees.
    //   +0x58: fleet entity reference. HIBYTE encodes entity type; [0x80,0x90) is the fleet range.
    //   +0x5c: target entity reference. HIBYTE checked in [1,0xff) by FUN_004cd6c0 (PhaseB case 9).
    //   +0x60: dispatch entity reference. Passed to FUN_004cd920 (PhaseA case 9).
    //   +0x64: secondary entity reference. Copied to work item +0x44 by FUN_004cd800 (PhaseB case 0xa).
    //   +0x68: batch count. Copied to work item +0x48 by FUN_004cd800 (PhaseB case 0xa).
    //   +0x6c: max batch count.
    //   +0x70: entity capacity upper bound.
    //   +0x74: additional capacity limit.
    //   +0x78: candidate list head ID.
    //   +0x7c, +0x80: two additional fields whose purpose was not resolved.

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

    // --- INCOMPLETE helper stubs for RunPhaseA ---

    // FUN_004cc8f0: Backward walk of _candidateListHead (+0x78) to find an eligible fleet
    // entity. On success sets _fleetEntityId (+0x58) and _capacityLimit (+0x74). Returns 1 if found.
    private int ScanFleetCandidatesPhaseA()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support fleet candidate list scanning (FUN_004cc8f0).
        return 0;
    }

    // FUN_004ccbd0: Checks fleet assignment eligibility given the current workspace bit-selection
    // context. Uses _fleetEntityId (HIBYTE type in [0x80,0x90)). Returns non-zero if eligible.
    private int CheckFleetAssignmentEligibility()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support fleet assignment eligibility check (FUN_004ccbd0).
        return 0;
    }

    // FUN_004cd340: Selects the next assignment target. Sets SubState-indexed entity fields.
    // Returns new SubState (7 = dispatch target, 9 = mission target, or 0 = done).
    private int ComputeAssignmentTargetSubState()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support assignment target sub-state computation (FUN_004cd340).
        return 0;
    }

    // FUN_004cce00: Creates a 0x200-type fleet assignment work item for the selected candidate.
    // Returns work item or null.
    private AIWorkItem CreateFleetAssignmentWorkItem()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support fleet assignment work item creation (FUN_004cce00).
        return null;
    }

    // FUN_004cd3e0: Creates a work item dispatching the selected assignment target.
    // Return value forwarded to terminal path.
    private AIWorkItem CreateAssignmentDispatchWorkItem()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support assignment dispatch work item creation (FUN_004cd3e0).
        return null;
    }

    // FUN_004cd920 (__thiscall, param_1 = ptr to _dispatchEntityId): Creates a mission dispatch
    // work item for the entity at _dispatchEntityId (+0x60). Returns work item or null.
    private AIWorkItem DispatchMissionToEntity()
    {
        // INCOMPLETE(engine): MissionDispatchSystem does not yet support entity mission dispatch (FUN_004cd920).
        return null;
    }

    // FUN_004cda60: Creates a follow-up work item after a successful mission dispatch.
    // Return value forwarded to terminal path.
    private AIWorkItem CreateMissionFollowupWorkItem()
    {
        // INCOMPLETE(engine): MissionDispatchSystem does not yet support mission dispatch follow-up work item creation (FUN_004cda60).
        return null;
    }

    // FUN_004cdb80: Clears _fleetEntityId (+0x58), retrieves resource batch count via
    // FUN_004cdff0, creates a mission issue via FUN_004191b0, iterates the fleet list
    // checking entity HIBYTE in [0x80,0x90), builds troop assignments. Return value discarded.
    private void BuildTroopMissionBatch()
    {
        // INCOMPLETE(engine): TroopAssignmentSystem does not yet support troop mission batch building (FUN_004cdb80).
    }

    // FUN_004ce0d0: Walks _candidateListHead (+0x78), selects troop candidates by score at
    // _fleetEntityId (+0x58) and capacity at entity+0x90, sets/clears flag bit 0x10000000.
    // Return value discarded.
    private void SelectTroopCandidates()
    {
        // INCOMPLETE(engine): TroopAssignmentSystem does not yet support troop candidate selection and scoring (FUN_004ce0d0).
    }

    // --- INCOMPLETE helper stubs for RunPhaseB ---

    // FUN_004cc3b0: Checks whether the fleet at _fleetEntityId is ready for dispatch.
    // Returns non-zero if ready; zero otherwise.
    private int CheckFleetReadyForDispatch()
    {
        // INCOMPLETE(engine): FleetDispatchSystem does not yet support fleet dispatch readiness check (FUN_004cc3b0).
        return 0;
    }

    // FUN_004cc030: Evaluates fleet dispatch status for case 3. Returns non-zero if the
    // fleet passes the evaluation; zero otherwise.
    private int EvaluateFleetDispatchStatus()
    {
        // INCOMPLETE(engine): FleetDispatchSystem does not yet support fleet dispatch status evaluation (FUN_004cc030).
        return 0;
    }

    // FUN_004cc5a0: Selects a dispatch target for the fleet. Sets _targetEntityId (+0x5c).
    // Returns new SubState (non-zero = selected target); 0 means no target (Phase→PhaseA).
    private int SelectFleetDispatchTarget()
    {
        // INCOMPLETE(engine): FleetDispatchSystem does not yet support fleet dispatch target selection (FUN_004cc5a0).
        return 0;
    }

    // FUN_004cc680: Creates a work item dispatching the fleet to the selected target.
    // Return value forwarded to terminal path.
    private AIWorkItem CreateFleetDispatchWorkItem()
    {
        // INCOMPLETE(engine): FleetDispatchSystem does not yet support fleet dispatch work item creation (FUN_004cc680).
        return null;
    }

    // FUN_004cc660: Checks whether fleet dispatch conditions are met. Returns non-zero to
    // proceed to dispatch (SubState→5); zero means not met (Phase→PhaseA, no ReadyFlag).
    private int CheckFleetDispatchCondition()
    {
        // INCOMPLETE(engine): FleetDispatchSystem does not yet support fleet dispatch condition check (FUN_004cc660).
        return 0;
    }

    // FUN_004cd6c0 (__thiscall, param_1 = ptr to _targetEntityId): Checks *param_1 HIBYTE in
    // [1,0xff) AND _fleetEntityId (+0x58) HIBYTE in [0xa2,0xa4). Creates a 0x214-type work
    // item, clones the fleet entity, copies OwnerSide flags. Returns work item or null.
    private AIWorkItem DispatchEntityTransfer()
    {
        // INCOMPLETE(engine): EntityTransferSystem does not yet support entity transfer dispatch (FUN_004cd6c0).
        return null;
    }

    // FUN_004cd800 (__fastcall ECX=this): Checks _fleetEntityId (+0x58) HIBYTE in [0xa2,0xa4).
    // Creates a 0x211-type work item, clones the entity, copies _secondaryEntityId (+0x64) to
    // item+0x44 and _batchCount (+0x68) to item+0x48. Return value forwarded to terminal.
    private AIWorkItem CreateEntityTransferFollowup()
    {
        // INCOMPLETE(engine): EntityTransferSystem does not yet support entity transfer follow-up work item creation (FUN_004cd800).
        return null;
    }
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
    // All fields below are INCOMPLETE — none are accessed until the helper methods are implemented.
    //   +0x40: mission issue list anchor for this record (AutoClass415 in original).
    //   +0x44..+0x4c: three tracking/cursor fields whose purpose was not resolved.
    //   +0x50: secondary entity reference. HIBYTE [0xa0,0xa2) used by FUN_004caf20/FUN_004cb060;
    //           HIBYTE [0xa2,0xa4) used by FUN_004cae00. Set by FUN_004ca310/FUN_004c9670.
    //   +0x54: fleet or mission issue reference. Set by FUN_004c8200/FUN_004c8830/FUN_004c9020/FUN_004c9e90.
    //   +0x58: fleet entity reference. HIBYTE type in [0x90,0x98). Set by FUN_004ca030.
    //   +0x5c: target entity reference. Copied to work item +0x44 by FUN_004cb060/FUN_004cae00.
    //   +0x60: entity reference used by FUN_004c9c80 (PhaseC case 9).
    //   +0x64: secondary entity reference used by FUN_004c9c80 (PhaseC case 9).
    //   +0x68: field whose purpose was not resolved from the analysed callees.
    //   +0x6c: batch count. Zeroed/set by FUN_004caa80 and FUN_004c9c80. Copied to work item +0x48.
    //   +0x70: maximum capacity bound. Set by FUN_004ca310 = entity+0x60-1.
    //   +0x74: additional capacity limit. Set by FUN_004ca030 = entity+0x114.
    //   +0x78: candidate list head ID.
    //   +0x7c, +0x80, +0x84: three additional fields whose purpose was not resolved.

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

    // --- INCOMPLETE helper stubs for RunPhaseA ---

    // FUN_004ca030: Scans _candidateListHead (+0x78) for an eligible fleet entity with
    // active bit (+0x30 LOBYTE bit 0) set, not on mission (+0x28 LOBYTE bits 0x3 clear),
    // entity+0x114 capacity > 0, and minimum score at +0x58. Sets _capacityLimit (+0x74) =
    // entity+0x114 and _fleetEntityId (+0x58) = entity ID. If none found: creates mission
    // issues via FUN_004191b0. Returns 1 if found, 0 if not.
    private int ScanFleetCandidatesPhaseA()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support fleet candidate list scanning for Type 11 (FUN_004ca030).
        return 0;
    }

    // FUN_004ca310: Creates a fleet assignment issue via FUN_00419330(workspace, _fleetEntityId,
    // 0x1000). Stores issue ID at _fleetIssueRef (+0x54), checks _fleetEntityId HIBYTE in
    // [0x90,0x98), calls FUN_004f25a0 and FUN_005087e0, sets _maxCapacityBound (+0x70) =
    // entity+0x60-1. Returns 1 on success.
    private int CheckFleetAssignmentCapacity()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support fleet assignment capacity setup for Type 11 (FUN_004ca310).
        return 0;
    }

    // FUN_004caa80: Zeroes _batchCount (+0x6c). Calls FUN_004cb5f0 for resource count and
    // FUN_0041a9e0(workspace, _targetEntityId, 0x2a, 0x4000, 0x4000, 2) for capacity. Computes
    // _batchCount = count/capacity, clamped by _maxCapacityBound (+0x70), _capacityLimit (+0x74),
    // and entity(_fleetEntityId)+0x84. Returns 0 (no further state), 0xc, or 0xe.
    private int ComputeResourceBatchSubState()
    {
        // INCOMPLETE(engine): ResourceBatchSystem does not yet support resource batch sub-state computation (FUN_004caa80).
        return 0;
    }

    // FUN_004ca540: Checks fleet entity at _fleetEntityId (HIBYTE [0x90,0x98)), calls
    // FUN_004f25a0 for relationship, checks entity flags 0x400000/0x3800000, iterates unit
    // type lists via sub_52b900/sub_52b600/sub_51b460/sub_52c350/sub_52c7c0, creates a
    // 0x200-type work item. Returns work item or null.
    private AIWorkItem CreateFleetAssignmentWorkItem()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support fleet assignment work item creation for Type 11 (FUN_004ca540).
        return null;
    }

    // FUN_004cab20: Walks _candidateListHead (+0x78); for each entity checks _fleetEntityId > 0
    // and HIBYTE(+0x28) & 0x10. Finds the best candidate, stores candidate ID at _fleetEntityId
    // (+0x58). Calls FUN_004f25a0(OwnerSide, _fleetEntityId), creates 0x200-type work item via
    // sub_52bc60. Returns work item or null.
    private AIWorkItem CreateFleetTargetAssignmentWorkItem()
    {
        // INCOMPLETE(engine): FleetAssignmentSystem does not yet support fleet target assignment work item creation (FUN_004cab20).
        return null;
    }

    // FUN_004caf20 (__thiscall, param_1 = ptr to _fleetEntityId): Checks *param_1 HIBYTE in
    // [0x90,0x98) AND _secondaryEntityId (+0x50) HIBYTE in [0xa0,0xa2). Creates a 0x214-type
    // work item, clones the entity at _secondaryEntityId, copies OwnerSide flags, calls
    // vtable[9] and vtable[0xb]. Returns work item or null.
    private AIWorkItem DispatchFleetToTarget()
    {
        // INCOMPLETE(engine): FleetDispatchSystem does not yet support fleet-to-target dispatch for Type 11 (FUN_004caf20).
        return null;
    }

    // FUN_004cb060: Checks _secondaryEntityId (+0x50) HIBYTE in [0xa0,0xa2). Creates a
    // 0x210-type work item, clones the entity at _secondaryEntityId, copies OwnerSide flags,
    // copies _targetEntityId (+0x5c) to item+0x44 and _batchCount (+0x6c) to item+0x48.
    private AIWorkItem CreateFleetDispatchFollowup()
    {
        // INCOMPLETE(engine): FleetDispatchSystem does not yet support fleet dispatch follow-up work item creation (FUN_004cb060).
        return null;
    }

    // FUN_004cb180: Mirrors FUN_004cdb80 (Type 10) but calls FUN_004cb5f0 instead of
    // FUN_004cdff0 for the resource count. Clears _fleetEntityId (+0x58), creates a mission
    // issue via FUN_004191b0, iterates the fleet list checking entity HIBYTE type.
    // Return value discarded.
    private void BuildTroopMissionBatch()
    {
        // INCOMPLETE(engine): TroopAssignmentSystem does not yet support troop mission batch building for Type 11 (FUN_004cb180).
    }

    // FUN_004cb6d0: Mirrors FUN_004ce0d0 (Type 10). Walks candidate lists, iterates entities
    // by score, sorts capital ships. Return value discarded.
    private void SelectTroopCandidates()
    {
        // INCOMPLETE(engine): TroopAssignmentSystem does not yet support troop candidate selection for Type 11 (FUN_004cb6d0).
    }

    // --- INCOMPLETE helper stubs for RunPhaseB ---

    // FUN_004c8200: Creates a fleet target issue via FUN_00419c70(workspace, 0x1005, 0x802, 0, 1).
    // Stores issue ID at _fleetIssueRef (+0x54). Returns work item or null.
    private AIWorkItem CreateFleetTargetIssue()
    {
        // INCOMPLETE(engine): FleetIssueSystem does not yet support fleet target issue creation (FUN_004c8200).
        return null;
    }

    // FUN_004c8830: Creates a fleet dispatch issue via FUN_00419c70(workspace, 5, 0x3802, 0, 2).
    // Stores issue ID at _fleetIssueRef (+0x54). Iterates fleet entries via FUN_004195f0, creates
    // 0x270-type or 0x201-type work items for various fleet dispatch scenarios. Returns work item or null.
    private AIWorkItem CreateFleetDispatchIssue()
    {
        // INCOMPLETE(engine): FleetIssueSystem does not yet support fleet dispatch issue creation (FUN_004c8830).
        return null;
    }

    // FUN_004c9020: Checks workspace StatusFlags LOBYTE & 0x30. Creates a transport order issue
    // via FUN_00419640(workspace, 0, 0, 0x8000000, ...). Stores issue ID at _fleetIssueRef (+0x54).
    // Loops through fleet entries creating 0x201-type work items with cloned troop transports.
    // Returns work item or null.
    private AIWorkItem CreateTroopTransportOrder()
    {
        // INCOMPLETE(engine): TroopTransportSystem does not yet support troop transport order creation (FUN_004c9020).
        return null;
    }

    // --- INCOMPLETE helper stubs for RunPhaseC ---

    // FUN_004c9670: Clears workspace+0x24. Walks _candidateListHead (+0x78) for an entity with
    // flag bit 0x200000, HIBYTE(+0x28)==0x2, LOBYTE(+0x28) bits 0x3 clear, _fleetEntityId > 0.
    // If found: stores entity ID at _secondaryEntityId (+0x50), calls FUN_004f25a0 and FUN_005087e0.
    // Returns non-zero if found.
    private int FindShortageSourceEntity()
    {
        // INCOMPLETE(engine): ShortageSystem does not yet support shortage source entity scanning (FUN_004c9670).
        return 0;
    }

    // FUN_004c9950: Chooses the production shortage request family for the current shortage.
    // Sets production shortage request fields on the record using the entity at _secondaryEntityId (+0x50).
    // Return value discarded by caller.
    private void ChooseProductionShortageFamily()
    {
        // INCOMPLETE(engine): ShortageSystem does not yet support production shortage family selection (FUN_004c9950).
    }

    // FUN_004c9c80: Clears 3 bytes from record+0x28, zeroes _batchCount (+0x6c).
    // Calls FUN_004cb590 for resource count. If count > 0: creates shortage issue via
    // FUN_0041aa20(workspace, 0x28, _entityRef60, _entityRef64, 1), traverses production queue,
    // sets _batchCount = 1 if threshold met. Returns 0 (terminal), 0xa, or 0xf.
    private int ComputeProductionShortageSubState()
    {
        // INCOMPLETE(engine): ShortageSystem does not yet support production shortage sub-state computation (FUN_004c9c80).
        return 0;
    }

    // FUN_004c9e40: Pure arithmetic threshold check.
    // Returns true when: (param_1+0x24 * workspace+0x184 / 100 * 0x5a / 100) - workspace+0x24c < 0.
    // Detects whether the resource deficit exceeds the configured shortage threshold.
    private bool CheckResourceDeficitThreshold()
    {
        // INCOMPLETE(engine): ShortageSystem does not yet expose the workspace fields needed for resource deficit threshold check (FUN_004c9e40).
        return false;
    }

    // FUN_004c9e90: Creates a deficit transport issue via FUN_00419c70(workspace, 5, 0x402, 0xe, 2).
    // Stores issue ID at _fleetIssueRef (+0x54). Creates a transport order via FUN_0041a430,
    // clones a HIBYTE [0x14,0x1c) entity, creates a 0x200-type work item. Returns work item or null.
    private AIWorkItem CreateProductionDeficitTransportWorkItem()
    {
        // INCOMPLETE(engine): DeficitTransportSystem does not yet support production deficit transport work item creation (FUN_004c9e90).
        return null;
    }

    // FUN_004cae00 (__thiscall): Checks _secondaryEntityId (+0x50) HIBYTE in [0xa2,0xa4).
    // Creates a 0x211-type work item, clones the entity at _secondaryEntityId, copies OwnerSide
    // flags, copies _targetEntityId (+0x5c) to item+0x44 and _batchCount (+0x6c) to item+0x48.
    private AIWorkItem CreateShortageTransferWorkItem()
    {
        // INCOMPLETE(engine): ShortageSystem does not yet support shortage transfer work item creation (FUN_004cae00).
        return null;
    }
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
                // Out-of-range SubState: reset to 1 (fall through to return null).
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
                // INCOMPLETE(engine): ProductionTrackingSystem does not yet support
                // unlinking a cancelled manufacturing job from its facility target.
            }
        }

        entry.IsCancelled = true;
    }

    // FUN_004c79a0 — walk EntityTargetTable backward to find the next entity that
    // has a pending or previous production ID (FUN_00476140 check: entry+0xe0 != 0
    // OR entry+0xe4 != 0).
    //
    // Cursor: _entityCursor (this+0x44). If non-zero, starts at that entry; if zero
    // or not found, starts at the tail.
    // On success: _entityTargetId = found.Id, _entityCursor = previous entry's Id or 0.
    // On failure: both cleared to 0. Returns 0.
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
            if (startIdx < 0)
            {
                _entityTargetId = 0;
                _entityCursor = 0;
                return 0;
            }
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
                    // INCOMPLETE(engine): ProductionTrackingSystem does not yet support
                    // the sub-table allocation (FUN_00617140 + FUN_005f3b00), per-type
                    // counter update, set-id callback (FUN_004ec1e0), or the production
                    // dispatch callback (vtable+0x20) invoked by FUN_00476160.
                }
                // uVar3 = 1 is set regardless; return value discarded by caller.
            }
        }
    }

    // FUN_004c7ab0 — walk ProductionTrackingTable backward to find the next entry
    // whose vtable+0x10 (NeedsProcessing) returns non-zero.
    //
    // Cursor: _productionCursor (this+0x4c). If non-zero, starts at that entry;
    // if zero or not found, starts at the tail.
    // On success: _productionItemId = found.Id, _productionCursor = previous Id or 0.
    // On failure: both cleared to 0. Returns 0.
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
            if (startIdx < 0)
            {
                _productionItemId = 0;
                _productionCursor = 0;
                return 0;
            }
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
    // Entry not found: writes 1 to dispatchOut ("pending / not-found"), returns null.
    // Entry found:     calls vtable+0x14(param_1=&dispatchOut) which may write 0 to
    //                  dispatchOut ("absorbed / complete") or leave it non-zero
    //                  ("pending / more work"), and returns a work item or null.
    private AIWorkItem TryDispatchProductionEntry(out int dispatchOut)
    {
        // Default: 1 ("not found" path — caller resets SubState).
        dispatchOut = 1;

        ProductionTrackingEntry entry = Workspace.ProductionTrackingTable.Find(e =>
            e.Id == _productionItemId
        );
        if (entry == null)
            return null;

        // INCOMPLETE(engine): ProductionTrackingSystem does not yet support the
        // production dispatch pipeline (vtable+0x14 call that writes to dispatchOut
        // and returns a work item for the AI manager to schedule manufacturing).
        dispatchOut = 0;
        return null;
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
        // INCOMPLETE(engine): DiplomacySystem does not yet support followup issue
        // seeding (FUN_004c6bd0 / FUN_0049cba0) on the TickSubObject production context.
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
        // INCOMPLETE(engine): DiplomacySystem does not yet support primary issue
        // seeding (FUN_004c69c0 / FUN_0049ca40) on the TickSubObject production context.
        return 0;
    }

    // FUN_004c6d50 — compute and store the batch count for the current issue cycle.
    // Reads TickSubObject+0x80 and +0x84 for available capacity.  Uses SubState
    // (+0x3c) as the batch counter: clears it to 0, sets to 1 (default), or adjusts
    // to iVar4/result if the product would overflow.  Populates _batchCalcRef (+0x48).
    // Returns 1 if SubState > 0 after computation (batch is feasible), else 0.
    private int ComputeBatchCount()
    {
        // INCOMPLETE(engine): DiplomacySystem does not yet support batch count
        // computation (FUN_004c6d50 / FUN_0049cf00) from the TickSubObject context.
        // SubState (+0x3c) would be used as batch count and written by this function.
        SubState = 0;
        return 0;
    }

    // FUN_004c6e00 — emit one primary runtime work item for the current issue.
    // Checks _primaryIssueRef and _followupIssueRef type codes, allocates and
    // links a 0x214-type work item via FUN_004f5060, FUN_004f4ea0, FUN_004f4b30.
    // Returns the allocated work item, or null if allocation or type checks fail.
    private AIWorkItem EmitPrimaryRuntimeObject()
    {
        // INCOMPLETE(engine): DiplomacySystem does not yet support primary runtime
        // object emission (FUN_004c6e00 / FUN_004f5060(0x214)).
        _ = _primaryIssueRef;
        _ = _followupIssueRef;
        return null;
    }

    // FUN_004c6f40 — emit the counted companion runtime work item.
    // Checks _followupIssueRef type code in [0xa4, 0xa6), allocates a 0x212-type
    // work item, and embeds the batch count (SubState at +0x3c) into the item's
    // +0x48 field.  Returns the allocated work item, or null on failure.
    private AIWorkItem EmitCountedRuntimeObject()
    {
        // INCOMPLETE(engine): DiplomacySystem does not yet support counted runtime
        // object emission (FUN_004c6f40 / FUN_004f5060(0x212)) with batch count
        // embedding (SubState → item+0x48).
        _ = _followupIssueRef;
        _ = _batchCalcRef;
        return null;
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

    // FUN_004ec1e0_set_id: Called when nameCounter wraps to 0 (> 9).
    // Sets the name ID on the work-item sub-object (+0x40 in original).
    private void FinalizeNameSubObject()
    {
        // INCOMPLETE(engine): NameGeneratorSystem does not yet support name sub-object finalization for Type 14 (FUN_004ec1e0).
    }

    // FUN_004ec230_set_param_to_two: Called when the ship cursor is exhausted normally.
    // Marks the sub-object with param=2 to signal the naming cycle is complete.
    private void SetSubObjectParamTwo()
    {
        // INCOMPLETE(engine): NameGeneratorSystem does not yet support sub-object param-two marking for Type 14 (FUN_004ec230).
    }
}
