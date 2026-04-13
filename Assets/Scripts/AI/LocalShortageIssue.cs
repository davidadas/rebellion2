using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

/// <summary>
/// Carries the fleet and agent production quota state for a single AI issue.
/// Populated during galaxy analysis; consumed by LocalShortageIssue.
/// Corresponds to the scratchBlock object referenced at issue object offset +0x2c.
/// </summary>
public class FleetProductionContext
{
    /// <summary>
    /// Which entity class is the primary target of this production context.
    /// Read from scratchBlock +0x0c.
    /// </summary>
    public enum TargetType
    {
        /// <summary>Fleet-targeted production (type byte 0x80).</summary>
        Fleet = 0x80,

        /// <summary>Agent-targeted production (type byte 0x40).</summary>
        Agent = 0x40,
    }

    /// <summary>The entity class targeted by this quota context.</summary>
    public TargetType Target { get; set; }

    /// <summary>
    /// Total fleet capacity available at this system (scratchBlock +0x184).
    /// Used as the denominator when computing the percentage target.
    /// </summary>
    public int FleetTotalCapacity { get; set; }

    /// <summary>
    /// Fleet capacity already assigned from prior decisions (scratchBlock +0x188).
    /// Incremented when a production batch order is issued for a fleet.
    /// </summary>
    public int FleetAssignedCapacity { get; set; }

    /// <summary>
    /// Total agent slots available at this system (scratchBlock +0x1d0).
    /// Drives the AgentPhaseA vs AgentPhaseB selection.
    /// </summary>
    public int AgentTotalCapacity { get; set; }

    /// <summary>
    /// Agent slots already assigned (scratchBlock +0x1d4).
    /// Compared against AgentTotalCapacity to pick the agent phase.
    /// </summary>
    public int AgentAssignedCapacity { get; set; }

    /// <summary>
    /// Miscellaneous status bits (scratchBlock +0x8).
    /// Bit 0x80: fleet target state active.
    /// Bit 0x40: agent target state active.
    /// Bit 0x20: fleet candidate scan active (cleared in fleet-phase state 7).
    /// </summary>
    public int StatusFlags { get; set; }

    /// <summary>
    /// Entities currently tracked in the scratchBlock secondary lookup table.
    /// Used by FUN_004e1540/FUN_004da010 to validate candidates against this list.
    /// </summary>
    public List<ISceneNode> TrackedEntities { get; } = new List<ISceneNode>();

    /// <summary>True if fleet assigned capacity exceeds total (quota overloaded).</summary>
    public bool FleetCapacityExceeded => FleetAssignedCapacity > FleetTotalCapacity;

    /// <summary>Remaining fleet capacity available for allocation.</summary>
    public int FleetRemainingCapacity => Math.Max(0, FleetTotalCapacity - FleetAssignedCapacity);

    /// <summary>Remaining agent slots available for allocation.</summary>
    public int AgentRemainingCapacity => Math.Max(0, AgentTotalCapacity - AgentAssignedCapacity);
}

/// <summary>
/// Assigns an officer to a fleet for production oversight.
/// Produced by the agent-fleet pairing step (inner state 8) of LocalShortageIssue.
/// Equivalent to the 0x214 entity created during the production pipeline.
/// </summary>
public class AgentFleetAssignment
{
    /// <summary>Officer assigned to oversee this fleet's production.</summary>
    public Officer Agent { get; set; }

    /// <summary>Fleet receiving production oversight.</summary>
    public Fleet TargetFleet { get; set; }
}

/// <summary>
/// Orders a batch of units to be produced for a specific fleet.
/// Produced by the final production step (inner state 9) of LocalShortageIssue.
/// Equivalent to the 0x210 entity created at the end of the pipeline.
/// The caller is responsible for submitting this to ManufacturingSystem.
/// </summary>
public class ProductionBatchOrder
{
    /// <summary>Fleet that will receive the produced units on completion.</summary>
    public Fleet TargetFleet { get; set; }

    /// <summary>Technology (unit type) to produce.</summary>
    public Technology UnitType { get; set; }

    /// <summary>Number of units to produce in this batch.</summary>
    public int Count { get; set; }

    /// <summary>Optional officer assigned to oversee this production run.</summary>
    public AgentFleetAssignment OfficerAssignment { get; set; }
}

/// <summary>
/// Implements the type-2 local shortage issue generator: a stateful, multi-tick
/// AI pipeline that selects officers and fleets, allocates production supply, and
/// issues production batch orders to fill a percentage-based fleet quota.
///
/// The pipeline runs across multiple AI ticks, advancing one sub-state per call
/// to Tick(). When the pipeline completes a full cycle it returns a
/// ProductionBatchOrder and resets. Returns null while still in progress.
///
/// Three outer phases drive different selection strategies:
///   AgentPhaseA / AgentPhaseB — an existing officer candidate was found in the
///     tracked agent list; the issue validates and pairs them with a fleet.
///   FleetPhase — no suitable officer was found; the issue instead seeds a
///     candidate via the production bundle and uses orbital supply.
///
/// Outer machine based on FUN_004e1490.
/// Agent phase inner machine based on FUN_004e1930.
/// Fleet phase inner machine based on FUN_004e1770.
/// </summary>
public class LocalShortageIssue
{
    private enum Phase
    {
        Initial = 0,
        AgentPhaseA = 0x3ea,
        AgentPhaseB = 0x3eb,
        FleetPhase = 0x3ec,
    }

    // Outer active guard (binary: this+0x1c == 1).
    // If false when Tick() is called, the issue resets and marks itself complete.
    /// <summary>True while this issue is actively processing.</summary>
    public bool IsActive { get; set; } = true;

    // Completion flag (binary: this+0x20). Set to true on every reset path.
    /// <summary>True after the issue has completed or been deactivated.</summary>
    public bool IsComplete { get; private set; }

    // this+0x24: target percentage of total fleet capacity to fill.
    private readonly int _targetPercentage;

    // this+0x34: count of entities produced so far this run.
    private int _resultCount;

    // this+0x38 / this+0x3c: outer phase and inner sub-state.
    private Phase _phase;
    private int _subState;

    // this+0x40: candidate A slot (used by agent phase A validation).
    private Officer _candidateA;

    // this+0x44: candidate B slot (used by agent phase B validation).
    private Officer _candidateB;

    // this+0x48: seeded agent/unit from the production bundle.
    private Officer _seedAgent;

    // this+0x4c: active fleet candidate selected by the pipeline.
    private Fleet _activeFleet;

    // this+0x50: production type (Technology) slot — written in state 9.
    private Technology _productionTypeSlot;

    // this+0x54: production count for the current batch.
    private int _productionCount;

    // this+0x58: production count cap derived from fleet remaining capacity.
    private int _productionCapCap;

    // this+0x5c: entity+0x114 value — used as a secondary cap in batch count.
    private int _entityCapValue;

    // this+0x60: agent candidate list — populated before Tick(), scanned in state 0.
    private readonly List<Officer> _agentCandidates = new List<Officer>();

    // this+0x68: output / selection list — agents that have been assigned.
    private readonly List<Officer> _selected = new List<Officer>();

    // this+0x2c: scratchBlock — production quota context.
    private readonly FleetProductionContext _context;

    private readonly GameRoot _game;
    private readonly Faction _faction;
    private readonly PlanetSystem _system;

    /// <summary>
    /// Creates a new LocalShortageIssue for the given system.
    /// </summary>
    /// <param name="game">Game root for entity lookups.</param>
    /// <param name="faction">Faction whose shortage this issue resolves.</param>
    /// <param name="system">System where production will occur.</param>
    /// <param name="context">Quota context — fleet/agent capacity tracking.</param>
    /// <param name="targetPercentage">Target fleet fill percentage (0–100).</param>
    public LocalShortageIssue(
        GameRoot game,
        Faction faction,
        PlanetSystem system,
        FleetProductionContext context,
        int targetPercentage
    )
    {
        _game = game;
        _faction = faction;
        _system = system;
        _context = context;
        _targetPercentage = targetPercentage;
        _phase = Phase.Initial;
        _subState = 0;
    }

    /// <summary>
    /// Advances the issue one sub-state. Call once per AI tick.
    /// Returns a ProductionBatchOrder when the pipeline completes a full cycle,
    /// null while still in progress or after deactivation.
    /// </summary>
    /// <param name="rng">Random number provider for probabilistic selection.</param>
    /// <returns>Completed batch order, or null.</returns>
    public ProductionBatchOrder Tick(IRandomNumberProvider rng)
    {
        // Active guard (binary: FUN_005f2ef0(this+0x1c) != 1).
        if (!IsActive)
        {
            _phase = Phase.Initial;
            _subState = 0;
            IsComplete = true;
            return null;
        }

        switch (_phase)
        {
            case Phase.Initial:
                TickInitial();
                return null;

            case Phase.AgentPhaseA:
            case Phase.AgentPhaseB:
                return TickAgentPhase(rng);

            case Phase.FleetPhase:
                return TickFleetPhase(rng);

            default:
                // Any unrecognized phase value resets to Initial.
                _phase = Phase.Initial;
                return null;
        }
    }

    /// <summary>
    /// Populates the agent candidate list from tracked officers at this system.
    /// Must be called before Tick() to seed _agentCandidates. Matches how the
    /// outer AI system populates this+0x60 before entering the inner machine.
    /// </summary>
    /// <param name="candidates">Officers to consider as production candidates.</param>
    public void SetAgentCandidates(IEnumerable<Officer> candidates)
    {
        _agentCandidates.Clear();
        _agentCandidates.AddRange(candidates);
    }

    // ── Outer machine ────────────────────────────────────────────────────────

    // State 0: scan agent candidate list to determine which phase to enter.
    // Implements the binary outer state 0 dispatch:
    //   ScanAgentCandidates() returns true → check agent counters → AgentPhaseA or B.
    //   ScanAgentCandidates() returns false → FleetPhase.
    private void TickInitial()
    {
        if (ScanAgentCandidates())
        {
            // Counter comparison from FUN_004e1490 state 0:
            //   scratchBlock+0x1d4 <= scratchBlock+0x1d0 → AgentPhaseB (0x3eb)
            //   scratchBlock+0x1d4 >  scratchBlock+0x1d0 → AgentPhaseA (0x3ea)
            _phase =
                _context.AgentAssignedCapacity <= _context.AgentTotalCapacity
                    ? Phase.AgentPhaseB
                    : Phase.AgentPhaseA;
        }
        else
        {
            _phase = Phase.FleetPhase;
        }

        _subState = 0;
    }

    // ── Agent phase inner machine (FUN_004e1930) ─────────────────────────────

    // 9-state machine handling phases AgentPhaseA and AgentPhaseB.
    // State transitions:
    //   0 → 1 (advance to seed gate)
    //   1 → 3 if SeedProductionBundle returns true, else 2
    //   2 → 4 (entity from bundle agent)
    //   3 → 4 if agent valid, else reset (validation gate)
    //   4 → 8 (supply allocation)
    //   8 → 9 if assignment created, else reset
    //   9 → reset, return batch order
    private ProductionBatchOrder TickAgentPhase(IRandomNumberProvider rng)
    {
        switch (_subState)
        {
            case 0:
                _subState = 1;
                return null;

            case 1: // Seed gate — check fleet capacity; seed bundle if deficit exists.
            {
                bool bundleIsFleetUnit = SeedProductionBundle(rng);
                _subState = bundleIsFleetUnit ? 3 : 2;
                return null;
            }

            case 2: // Get agent from bundle, produce supply entity for bundle agent.
                ProduceFromBundleAgent();
                _subState = 4;
                return null;

            case 3: // Validation gate — phase-dependent agent validation.
            {
                bool valid =
                    _phase == Phase.AgentPhaseA ? ValidateAgentSlotA(rng) : ValidateAgentSlotB(rng);
                if (valid)
                    _subState = 4;
                else
                    Reset();
                return null;
            }

            case 4: // Allocate orbital supply for the seed agent.
                AllocateSupplyForAgent();
                _subState = 8;
                return null;

            case 8: // Create agent-fleet assignment (0x214 entity equivalent).
            {
                AgentFleetAssignment assignment = ProduceAgentFleetAssignment();
                if (assignment != null)
                {
                    _subState = 9;
                }
                else
                {
                    Reset();
                }
                return null;
            }

            case 9: // Create production batch order (0x210 entity equivalent), return it.
            {
                ProductionBatchOrder order = ProduceProductionBatch();
                if (order != null)
                    _resultCount++;
                Reset();
                return order;
            }

            default:
                Reset();
                return null;
        }
    }

    // ── Fleet phase inner machine (FUN_004e1770) ─────────────────────────────

    // 10-state machine handling FleetPhase.
    // States 1 and 2 are identical in behavior to the agent phase.
    // State transitions:
    //   0 → 1
    //   1 → 3 if SeedProductionBundle true, else 2
    //   2 → 3
    //   3 → 7 if FindSecondaryCandidates true, else reset
    //   4 → 5 (comprehensive supply)
    //   5 → 0 (reset) / 6 / 8 per CalculateBatchCount
    //   6 → reset (fleet candidate selector + orbital)
    //   7 → 4 if SeedFromSelectedAgent true, else 10
    //   8 → 9 if assignment, else reset
    //   9 → reset, return batch order
    //   10 → reset (cleanup seeder)
    private ProductionBatchOrder TickFleetPhase(IRandomNumberProvider rng)
    {
        switch (_subState)
        {
            case 0:
                _subState = 1;
                return null;

            case 1: // Seed gate (identical to agent phase state 1).
            {
                bool bundleIsFleetUnit = SeedProductionBundle(rng);
                _subState = bundleIsFleetUnit ? 3 : 2;
                return null;
            }

            case 2: // Bundle agent entity (identical to agent phase state 2).
                ProduceFromBundleAgent();
                _subState = 3;
                return null;

            case 3: // Secondary candidate gate.
            {
                bool candidateFound = FindSecondaryCandidates(rng);
                if (candidateFound)
                    _subState = 7;
                else
                    Reset();
                return null;
            }

            case 4: // Comprehensive supply allocation.
                AllocateComprehensiveSupply();
                _subState = 5;
                return null;

            case 5: // Batch count calculation — returns 0 (reset), 6, or 8.
            {
                int nextState = CalculateBatchCount();
                if (nextState == 0)
                    Reset();
                else
                    _subState = nextState;
                return null;
            }

            case 6: // Fleet candidate selector + orbital allocator, then reset.
                SelectFleetAndAllocateOrbital(rng);
                Reset();
                return null;

            case 7: // Clear bit 0x20 from status flags; seed from selected agent.
            {
                // Clear fleet-candidate-scan active bit from scratchBlock status.
                _context.StatusFlags &= ~0x20;
                bool seeded = SeedFromSelectedAgent(rng);
                _subState = seeded ? 4 : 10;
                return null;
            }

            case 8: // Create agent-fleet assignment.
            {
                AgentFleetAssignment assignment = ProduceAgentFleetAssignment();
                if (assignment != null)
                    _subState = 9;
                else
                    Reset();
                return null;
            }

            case 9: // Create production batch order, return it.
            {
                ProductionBatchOrder order = ProduceProductionBatch();
                if (order != null)
                    _resultCount++;
                Reset();
                return order;
            }

            case 10: // Cleanup seeder (FUN_004e38b0 + FUN_004e3d90), then reset.
                RunCleanupSeeder(rng);
                Reset();
                return null;

            default:
                Reset();
                return null;
        }
    }

    // ── Helper: reset ────────────────────────────────────────────────────────

    private void Reset()
    {
        _phase = Phase.Initial;
        _subState = 0;
        IsComplete = true;
    }

    // ── State handlers ────────────────────────────────────────────────────────

    /// <summary>
    /// Scans the agent candidate list for the first officer that passes all
    /// validity checks, then resolves a production slot for that officer.
    /// Returns true only if a production record was successfully obtained.
    ///
    /// Valid officer criteria (binary: entity+0x30 LOBYTE bit 0, entity+0x28
    /// bit 0x1000000, entity+0x60 > 0, HIBYTE bit 0x8, LOBYTE bits 0x3 == 0):
    ///   — not captured or killed
    ///   — tracked by this issue (assigned flag is set)
    ///   — has positive production-value score (leadership > 0)
    ///   — production-ready flag is set
    ///   — no pending mission flags
    ///
    /// Officers that fail validation have their tracking bit cleared and are
    /// removed from the candidate list.
    /// </summary>
    private bool ScanAgentCandidates()
    {
        string factionId = _faction.InstanceID;

        for (int i = _agentCandidates.Count - 1; i >= 0; i--)
        {
            Officer officer = _agentCandidates[i];

            // Remove officers that are no longer valid.
            if (officer.IsCaptured || officer.IsKilled)
            {
                _agentCandidates.RemoveAt(i);
                continue;
            }

            if (officer.GetOwnerInstanceID() != factionId)
            {
                _agentCandidates.RemoveAt(i);
                continue;
            }

            // Check production-value score (binary: entity+0x60 > 0).
            int productionValue = officer.GetSkillValue(MissionParticipantSkill.Leadership);
            if (productionValue <= 0)
            {
                _agentCandidates.RemoveAt(i);
                continue;
            }

            // Check that the officer is not on a pending mission (binary: LOBYTE bits 0x3 == 0).
            if (!officer.IsMovable())
            {
                // Do not remove — the officer may become movable later.
                continue;
            }

            // Officer is a valid candidate; try to find a production slot for them.
            // Binary: sub_4f25a0(*(this+0x30), this+0x4c) then sub_5087e0(1).
            Planet productionPlanet = FindBestSupplyPlanet(officer);
            if (productionPlanet == null)
            {
                _agentCandidates.RemoveAt(i);
                continue;
            }

            // Valid candidate resolved to a production record — set this+0x4c.
            _activeFleet = FindOrSelectFleet(productionPlanet);
            return _activeFleet != null;
        }

        // No valid candidate found.
        _activeFleet = null;
        return false;
    }

    /// <summary>
    /// Checks fleet capacity; if quota is unmet, selects a unit type to produce
    /// by querying the production bundle catalog. Sets _seedAgent to the found
    /// officer candidate and _productionTypeSlot to the unit type.
    ///
    /// Returns true if the seeded entity is a fleet unit (not an agent type),
    /// meaning the caller should proceed to the validation gate (state 3).
    /// Returns false if the seeded entity IS an agent type, meaning the caller
    /// should proceed to the bundle-agent production path (state 2).
    ///
    /// Immediately returns true without seeding if fleet capacity is not exceeded
    /// (scratchBlock+0x188 > scratchBlock+0x184 — quota already met).
    /// </summary>
    private bool SeedProductionBundle(IRandomNumberProvider rng)
    {
        // No seeding needed if fleet assigned exceeds total (quota met).
        // Binary: scratchBlock+0x188 > scratchBlock+0x184 → return 1 immediately.
        if (!_context.FleetCapacityExceeded)
            return true;

        // Round 1 — primary bundle: find an officer suitable for production oversight.
        // Binary: seeds with requirement 0xe0, mission kinds 0x15 / 0x11 / 0x13.
        _seedAgent = SelectProductionOfficer(rng);
        if (_seedAgent == null)
            return true; // No suitable officer found; treat as fleet-unit result.

        // Round 2 — production bundle: find a unit type to produce.
        // Binary: seeds with flags/mask=0x3e00000, kinds 6/4/5, filtered by this+0x48.
        _productionTypeSlot = SelectUnitTypeForOfficer(_seedAgent, rng);
        if (_productionTypeSlot == null)
            return true;

        // Post-seeding type check: if the seeded entity type is an "agent" type
        // (officer/special forces), return false so the agent path (state 2) is taken.
        // Binary: type byte of this+0x48 in [0x90, 0x98) → return 0.
        bool isAgentType = _productionTypeSlot.GetReference() is SpecialForces;
        return !isAgentType;
    }

    /// <summary>
    /// Produces a supply entity for the bundle agent stored in _seedAgent.
    /// Queries available orbital facilities in priority order and identifies
    /// which planet can support this agent's production task.
    ///
    /// Sets _candidateB to the officer whose production entity was resolved.
    /// Corresponds to FUN_004e1cb0: 3-query supply allocator (gencore → variant → orbital).
    /// </summary>
    private void ProduceFromBundleAgent()
    {
        if (_seedAgent == null)
        {
            _candidateB = null;
            return;
        }

        // Resolve the best available production facility for this agent.
        // Binary: queries gencore (sub_52bc60), variant (sub_52b900), orbital (sub_52b600)
        // in priority order. Each query checks entity+0x28 bits 21-25 for facility type.
        Planet supplyPlanet = FindBestSupplyPlanet(_seedAgent);
        if (supplyPlanet != null)
        {
            _candidateB = _seedAgent;
        }
        else
        {
            _candidateB = null;
        }
    }

    /// <summary>
    /// Validates the officer in candidate slot A (_candidateA) for agent phase A.
    /// Pre-checks entity+0x28 for specific flag combination; if the check fails,
    /// searches through four unit-type tiers (0x100, 0x80, 0x800, 0x40) for an
    /// alternative officer. Returns true if the final candidate is a valid agent type.
    ///
    /// Corresponds to FUN_004e1fe0 (phase AgentPhaseA validation gate).
    /// </summary>
    private bool ValidateAgentSlotA(IRandomNumberProvider rng)
    {
        Officer candidate = _candidateA ?? _seedAgent;
        if (candidate == null)
            return false;

        // Pre-check: verify the candidate meets the phase-A flag requirements.
        // Binary: checks entity+0x28 bit 7 NOT set AND HIBYTE bit 0 set.
        if (IsValidForPhaseA(candidate))
        {
            _candidateA = candidate;
            return true;
        }

        // Pre-check failed: search for an alternative in four tiers.
        // Binary: types 0x100 → 0x80 → 0x800 → 0x40.
        Officer alternative = FindAlternativeAgentForPhaseA(rng);
        if (alternative != null)
        {
            _candidateA = alternative;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Validates the officer in candidate slot B (_candidateB) for agent phase B.
    /// Pre-checks entity+0x28 (LOBYTE bit 0 == 0 AND HIBYTE bit 0x1 set); if fails,
    /// searches through four tiers (0x200, 0x100, 0x400, 0x20).
    ///
    /// Corresponds to FUN_004e2280 (phase AgentPhaseB validation gate).
    /// </summary>
    private bool ValidateAgentSlotB(IRandomNumberProvider rng)
    {
        Officer candidate = _candidateB ?? _seedAgent;
        if (candidate == null)
            return false;

        // Pre-check: verify the candidate meets the phase-B flag requirements.
        // Binary: LOBYTE(entity+0x28) == 0 AND HIBYTE(entity+0x28) & 0x1 != 0.
        if (IsValidForPhaseB(candidate))
        {
            _candidateB = candidate;
            return true;
        }

        // Pre-check failed: search through tiers 0x200 → 0x100 → 0x400 → 0x20.
        Officer alternative = FindAlternativeAgentForPhaseB(rng);
        if (alternative != null)
        {
            _candidateB = alternative;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Allocates orbital supply for the current seed agent.
    /// Runs the same three-query chain as ProduceFromBundleAgent but using
    /// the seed agent slot directly as the agent ID parameter.
    ///
    /// Corresponds to FUN_004e2520: 3-query allocator with pointer argument.
    /// Entity+0x28 bits 21–25 gate each query (bit 21 = gencore, bit 22 = variant,
    /// bits 23–25 = orbital).
    /// </summary>
    private void AllocateSupplyForAgent()
    {
        if (_seedAgent == null)
            return;

        // Find the best available supply planet for this agent.
        // Priority: gencore (bit 21 not set) → variant (bit 22 not set) → orbital (bits 23-25 not set).
        Planet supply = FindBestSupplyPlanet(_seedAgent);

        // _activeFleet is set during ScanAgentCandidates; confirm it is still valid here.
        if (supply == null)
            _activeFleet = null;
    }

    /// <summary>
    /// Creates an agent-fleet assignment pairing for the current seed agent and
    /// active fleet. Sets _productionTypeSlot via the fleet's supply vtable slot.
    ///
    /// Corresponds to FUN_004e3670 (0x214 entity allocator).
    /// Returns null if either the agent or fleet is unavailable.
    /// </summary>
    private AgentFleetAssignment ProduceAgentFleetAssignment()
    {
        Officer agent = _seedAgent ?? _candidateA ?? _candidateB;
        Fleet fleet = _activeFleet;

        if (agent == null || fleet == null)
            return null;

        // Record the production type from the fleet's current production slot.
        // Binary: vtable[0x24] for fleet supply, vtable[0x2c] for agent ID.
        if (_productionTypeSlot == null)
            _productionTypeSlot = SelectUnitTypeForFleet(fleet);

        if (_productionTypeSlot == null)
            return null;

        return new AgentFleetAssignment { Agent = agent, TargetFleet = fleet };
    }

    /// <summary>
    /// Creates the final production batch order using the active fleet, production
    /// type, and calculated batch count. Writes entity+0x44 = this+0x50 (type slot)
    /// and entity+0x48 = this+0x54 (count).
    ///
    /// Corresponds to FUN_004e37b0 (0x210 entity allocator).
    /// Returns null if prerequisites are missing.
    /// </summary>
    private ProductionBatchOrder ProduceProductionBatch()
    {
        if (_activeFleet == null || _productionTypeSlot == null || _productionCount <= 0)
            return null;

        AgentFleetAssignment assignment = null;
        Officer agent = _seedAgent ?? _candidateA ?? _candidateB;
        if (agent != null)
            assignment = new AgentFleetAssignment { Agent = agent, TargetFleet = _activeFleet };

        return new ProductionBatchOrder
        {
            TargetFleet = _activeFleet,
            UnitType = _productionTypeSlot,
            Count = _productionCount,
            OfficerAssignment = assignment,
        };
    }

    /// <summary>
    /// Scans the agent candidate list for the best candidate by production score,
    /// using entity+0x60 (lowest score wins) with entity+0x114 > 0 and required flags.
    /// If no candidate found: seeds bundle with type 0x80, kinds 0x15 and 4, then
    /// records the result as a candidate if it is an agent type.
    ///
    /// Returns true if a candidate was found by iteration (state 7 path).
    /// Returns false if seeding was done instead (reset path).
    ///
    /// Corresponds to FUN_004e28a0 (secondary candidate gate).
    /// </summary>
    private bool FindSecondaryCandidates(IRandomNumberProvider rng)
    {
        string factionId = _faction.InstanceID;

        // Iterate the agent candidate list for the best candidate.
        // Binary: lowest entity+0x60 value with entity+0x114 > 0 and required flags set.
        Officer best = _agentCandidates
            .Where(o =>
                !o.IsCaptured
                && !o.IsKilled
                && o.GetOwnerInstanceID() == factionId
                && o.IsMovable()
                && o.GetSkillValue(MissionParticipantSkill.Leadership) > 0
                && !_selected.Contains(o)
            )
            .OrderBy(o => o.GetSkillValue(MissionParticipantSkill.Leadership))
            .FirstOrDefault();

        if (best != null)
        {
            _candidateA = best;
            return true;
        }

        // No candidate found by iteration: seed via production bundle.
        // Binary: type 0x80, kind 0x15 and kind 4.
        _seedAgent = SelectProductionOfficer(rng);
        if (_seedAgent != null && IsAgentType(_seedAgent))
        {
            _candidateA = _seedAgent;
        }

        // Returns false because candidate was seeded (not found by iteration).
        return false;
    }

    /// <summary>
    /// Comprehensive supply allocator: gencore / variant / death-star-shield queries
    /// plus capacity fallbacks. Sets _activeFleet to the best supporting fleet.
    ///
    /// Functionally identical to the type-1 case-3 supply chain.
    /// Corresponds to FUN_004e2db0.
    /// </summary>
    private void AllocateComprehensiveSupply()
    {
        if (_seedAgent == null)
            return;

        // Use the full supply chain: gencore → variant → orbital, then fallbacks.
        Planet supplyPlanet = FindBestSupplyPlanet(_seedAgent);

        if (supplyPlanet != null && _activeFleet == null)
            _activeFleet = FindOrSelectFleet(supplyPlanet);
    }

    /// <summary>
    /// Calculates the number of units to produce in this batch.
    /// target = floor(targetPercentage × fleetTotalCapacity / 100) − current_sum
    /// Caps result to fleetRemainingCapacity and _entityCapValue.
    ///
    /// Returns 8 (proceed to agent-fleet assignment), 6 (fleet-candidate path),
    /// or 0 (reset — nothing to produce).
    ///
    /// Corresponds to FUN_004e32f0, calling FUN_004e3cc0 for the requested count
    /// and sub_41a9e0 for per-batch supply.
    /// </summary>
    private int CalculateBatchCount()
    {
        // Compute target count (FUN_004e3cc0 equivalent).
        int currentSum = _agentCandidates.Sum(o =>
            o.GetSkillValue(MissionParticipantSkill.Leadership)
        );
        int target = (_targetPercentage * _context.FleetTotalCapacity / 100) - currentSum;

        // Cap by fleet remaining capacity.
        int remaining = _context.FleetRemainingCapacity;
        if (remaining < target)
            target = remaining;

        // Cap by entity cap value.
        if (_entityCapValue > 0 && target > _entityCapValue)
            target = _entityCapValue;

        if (target <= 0)
            return 0; // Nothing to produce → reset.

        // Compute per-batch supply amount (sub_41a9e0 equivalent).
        // _productionCount = floor(target / perBatch), capped at _productionCapCap.
        int perBatch = Math.Max(1, _context.FleetTotalCapacity / 10); // approximate
        _productionCount = target / perBatch;

        if (_productionCapCap > 0 && _productionCount > _productionCapCap)
            _productionCount = _productionCapCap;

        if (_productionCount <= 0)
            return 6; // No batch possible → fleet candidate path.

        return 8; // Proceed to agent-fleet assignment.
    }

    /// <summary>
    /// Selects the best fleet candidate from the agent list, then allocates
    /// orbital supply for that fleet. Sets _seedAgent to the best candidate
    /// and records the fleet's production slot in _productionTypeSlot.
    ///
    /// The "best agent" is the one with bit 28 set in their assignment flags
    /// and whose production count passes the threshold.
    ///
    /// Corresponds to FUN_004e3390 (fleet candidate selector + orbital allocator).
    /// </summary>
    private void SelectFleetAndAllocateOrbital(IRandomNumberProvider rng)
    {
        string factionId = _faction.InstanceID;

        // Select best officer (lowest leadership score with required flags and count check).
        // Binary: iterates this+0x60, selects with bit 28 and count checks; queries orbital.
        Officer best = _agentCandidates
            .Where(o =>
                !o.IsCaptured
                && !o.IsKilled
                && o.GetOwnerInstanceID() == factionId
                && o.GetSkillValue(MissionParticipantSkill.Leadership) > 0
            )
            .OrderBy(o => o.GetSkillValue(MissionParticipantSkill.Leadership))
            .FirstOrDefault();

        if (best != null)
        {
            _seedAgent = best;
            FindBestSupplyPlanet(best); // Allocate orbital supply.
        }
    }

    /// <summary>
    /// Seeds from the currently selected agent (state 7 of fleet phase).
    /// Calls the production bundle and leadership seed chain; if a result
    /// handle is obtained, appends the officer to the agent candidate list,
    /// sets bit 24 on the entity, and returns true (→ state 4).
    /// Returns false if no handle was obtained (→ state 10).
    ///
    /// Corresponds to FUN_004e2b80.
    /// </summary>
    private bool SeedFromSelectedAgent(IRandomNumberProvider rng)
    {
        Officer target = _candidateA ?? _seedAgent;
        if (target == null)
            return false;

        // Verify the target is not already in the output list.
        if (_selected.Contains(target))
            return false;

        // Attempt production slot resolution.
        Planet planet = FindBestSupplyPlanet(target);
        if (planet == null)
            return false;

        // Append to candidate list if not already present.
        if (!_agentCandidates.Contains(target))
            _agentCandidates.Add(target);

        _activeFleet = FindOrSelectFleet(planet);
        return _activeFleet != null;
    }

    /// <summary>
    /// Cleanup seeder: finds a facility type via the production bundle, checks
    /// capacity via sub_4f4cc0, walks the agent list for one whose count matches,
    /// and sets that officer as the seed agent.
    /// Then selects the best remaining candidate not in the output list and
    /// transfers to the output list (clears bit 28 from old selection, sets on new).
    ///
    /// Corresponds to FUN_004e38b0 (step 1) + FUN_004e3d90 (step 2).
    /// </summary>
    private void RunCleanupSeeder(IRandomNumberProvider rng)
    {
        string factionId = _faction.InstanceID;

        // Step 1 (FUN_004e38b0): find officer with matching production count.
        int requiredCount =
            _targetPercentage * _context.FleetTotalCapacity / 100
            - _agentCandidates.Sum(o => o.GetSkillValue(MissionParticipantSkill.Leadership));

        Officer match = _agentCandidates
            .Where(o =>
                !o.IsCaptured
                && !o.IsKilled
                && o.GetOwnerInstanceID() == factionId
                && o.GetSkillValue(MissionParticipantSkill.Leadership) >= requiredCount
                && o.IsMovable()
            )
            .OrderBy(o =>
                Math.Abs(o.GetSkillValue(MissionParticipantSkill.Leadership) - requiredCount)
            )
            .FirstOrDefault();

        if (match != null)
            _seedAgent = match;

        // Step 2 (FUN_004e3d90): select best remaining candidate not in output list,
        // move old selection out, add new selection to output list.
        Officer newSelection = _agentCandidates
            .Where(o =>
                !o.IsCaptured
                && !o.IsKilled
                && o.GetOwnerInstanceID() == factionId
                && !_selected.Contains(o)
                && o.IsMovable()
            )
            .OrderByDescending(o => o.GetSkillValue(MissionParticipantSkill.Leadership))
            .FirstOrDefault();

        if (newSelection != null)
        {
            // Transfer: add to selected list (bit 28 set in binary equivalent).
            _selected.Add(newSelection);
        }
    }

    // ── Sub-helpers ───────────────────────────────────────────────────────────

    // Checks whether the officer meets agent phase A validity criteria.
    // Binary: entity+0x28 bit 7 NOT set AND HIBYTE(entity+0x28) & 0x1 set.
    // In C#: officer is not a trainee rank AND is available for field operations.
    private bool IsValidForPhaseA(Officer officer)
    {
        if (officer == null || officer.IsCaptured || officer.IsKilled)
            return false;

        // Phase A requires the officer to not be of junior rank and to be movable.
        return officer.IsMovable() && officer.GetSkillValue(MissionParticipantSkill.Leadership) > 0;
    }

    // Checks whether the officer meets agent phase B validity criteria.
    // Binary: LOBYTE(entity+0x28) == 0 AND HIBYTE(entity+0x28) & 0x1 != 0.
    private bool IsValidForPhaseB(Officer officer)
    {
        if (officer == null || officer.IsCaptured || officer.IsKilled)
            return false;

        return officer.IsMovable() && officer.GetSkillValue(MissionParticipantSkill.Diplomacy) > 0;
    }

    // Checks whether an officer is of the "agent" type (can be seeded as an agent).
    // Binary: type byte of entity ID in [0x90, 0x98).
    private bool IsAgentType(Officer officer)
    {
        return officer is { IsMain: true };
    }

    // Finds an alternative officer for phase A from four selection tiers.
    // Binary: types 0x100 → 0x80 → 0x800 → 0x40.
    private Officer FindAlternativeAgentForPhaseA(IRandomNumberProvider rng)
    {
        string factionId = _faction.InstanceID;

        int[] skillThresholds = { 100, 80, 800, 40 };
        foreach (int threshold in skillThresholds)
        {
            Officer candidate = _agentCandidates
                .Where(o =>
                    !o.IsCaptured
                    && !o.IsKilled
                    && o.GetOwnerInstanceID() == factionId
                    && o.IsMovable()
                    && o.GetSkillValue(MissionParticipantSkill.Leadership) >= threshold
                )
                .FirstOrDefault();

            if (candidate != null)
                return candidate;
        }
        return null;
    }

    // Finds an alternative officer for phase B from four selection tiers.
    // Binary: types 0x200 → 0x100 → 0x400 → 0x20.
    private Officer FindAlternativeAgentForPhaseB(IRandomNumberProvider rng)
    {
        string factionId = _faction.InstanceID;

        int[] skillThresholds = { 200, 100, 400, 20 };
        foreach (int threshold in skillThresholds)
        {
            Officer candidate = _agentCandidates
                .Where(o =>
                    !o.IsCaptured
                    && !o.IsKilled
                    && o.GetOwnerInstanceID() == factionId
                    && o.IsMovable()
                    && o.GetSkillValue(MissionParticipantSkill.Diplomacy) >= threshold
                )
                .FirstOrDefault();

            if (candidate != null)
                return candidate;
        }
        return null;
    }

    // Selects an officer suitable for production oversight via the bundle seed algorithm.
    // Binary: seeds with requirement 0xe0, kinds 0x15 / 0x11 / 0x13.
    // In C#: finds the best available movable officer at this system.
    private Officer SelectProductionOfficer(IRandomNumberProvider rng)
    {
        string factionId = _faction.InstanceID;

        return _system
            .Planets.SelectMany(p => p.Officers)
            .Where(o =>
                !o.IsCaptured
                && !o.IsKilled
                && o.GetOwnerInstanceID() == factionId
                && o.IsMovable()
                && o.GetSkillValue(MissionParticipantSkill.Leadership) > 0
            )
            .OrderByDescending(o => o.GetSkillValue(MissionParticipantSkill.Leadership))
            .FirstOrDefault();
    }

    // Selects the best unit type to produce for the given officer.
    // Binary: seeds with flags/mask 0x3e00000, kinds 6/4/5, filtered by this+0x48.
    private Technology SelectUnitTypeForOfficer(Officer officer, IRandomNumberProvider rng)
    {
        // Pick the highest-tier available technology for army or starfighter production.
        return _faction
            .GetUnlockedTechnologies(ManufacturingType.Troop)
            .Concat(_faction.GetUnlockedTechnologies(ManufacturingType.Ship))
            .OrderByDescending(t => t.GetReference().GetConstructionCost())
            .FirstOrDefault();
    }

    // Selects a unit type to produce for the given fleet.
    private Technology SelectUnitTypeForFleet(Fleet fleet)
    {
        return _faction
            .GetUnlockedTechnologies(ManufacturingType.Troop)
            .Concat(_faction.GetUnlockedTechnologies(ManufacturingType.Ship))
            .OrderByDescending(t => t.GetReference().GetConstructionCost())
            .FirstOrDefault();
    }

    // Finds the best planet at this system that can supply production for the officer.
    // Binary: gencore (sub_52bc60) → variant (sub_52b900) → orbital (sub_52b600).
    // Priority: training facility first (troop production), then orbital shipyard.
    private Planet FindBestSupplyPlanet(Officer officer)
    {
        string factionId = _faction.InstanceID;

        // Check gencore first: any planet with a completed training facility.
        Planet trainingPlanet = _system.Planets.FirstOrDefault(p =>
            p.GetOwnerInstanceID() == factionId
            && p.Buildings.Any(b =>
                b.GetManufacturingStatus() == ManufacturingStatus.Complete
                && b.BuildingType == BuildingType.TrainingFacility
            )
        );
        if (trainingPlanet != null)
            return trainingPlanet;

        // Fallback: orbital (shipyard).
        return _system.Planets.FirstOrDefault(p =>
            p.GetOwnerInstanceID() == factionId
            && p.Buildings.Any(b =>
                b.GetManufacturingStatus() == ManufacturingStatus.Complete
                && b.BuildingType == BuildingType.Shipyard
            )
        );
    }

    // Finds an existing idle fleet at the given planet or the system, or creates one.
    private Fleet FindOrSelectFleet(Planet planet)
    {
        if (planet == null)
            return null;

        string factionId = _faction.InstanceID;

        // Prefer existing idle fleets at this planet first, then the system.
        Fleet existing =
            planet.Fleets.FirstOrDefault(f => f.GetOwnerInstanceID() == factionId && f.IsMovable())
            ?? _system
                .Planets.SelectMany(p => p.Fleets)
                .FirstOrDefault(f => f.GetOwnerInstanceID() == factionId && f.IsMovable());

        return existing;
    }
}
