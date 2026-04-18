using System;
using System.Collections.Generic;

// Faithful reconstitution of the galaxy analysis scoring pipeline.
// Source: FUN_00417cb0 (calibration state 5), calling:
//   FUN_0041af90 - per-system accumulator
//   FUN_0041b230 - per-fleet accumulator
//   FUN_0041b3c0 - per-character accumulator
//
// Every accumulator field, bitflag test, branch condition, max/min tracker,
// and counter matches the disassembly instruction-by-instruction.
// Field names derived from AI.md Sections 135-139 and the per-system
// analysis record field table.

// ================================================================
// PER-SYSTEM STATS (sub-object at node+0x70 in the system analysis list)
// Offsets are relative to sub-object start. Add +0x70 for node-level offset.
// ================================================================
public class PerSystemStats
{
    // Index accessor for FUN_004191b0 / FUN_00430eb0:
    // (&node->field40_0x70)[param_7] — DWORD at byte offset (param_7 * 4) from PerSystemStats start.
    // Byte offsets: 0x0c=FacilityCount, 0x10=EnemyTroopSurplus, 0x14=FriendlyTroopSurplus,
    //   0x24=NetCapitalShipSurplus, 0x28=NetFighterSurplus, 0x2c=SystemPriority,
    //   0x30=EntityClassification, 0x38=FightersAboveThreshold, 0x3c=AvailableFighters,
    //   0x40=CapShipAtTarget, 0x44=CapShipNotAtTarget, 0x48=RegimentAtTarget,
    //   0x4c=RegimentNotAtTarget, 0x50=StarfighterAtTarget, 0x54=StarfighterNotAtTarget.
    // DWORD index 4 (offset 0x10) = EnemyTroopSurplus
    // DWORD index 8 (offset 0x20) = unlisted (returns 0 until FUN_004319d0 fully implemented)
    // DWORD index 9 (offset 0x24) = NetCapitalShipSurplus
    // DWORD index 10 (offset 0x28) = NetFighterSurplus
    // DWORD index 21 (offset 0x54) = StarfighterNotAtTarget (StarfighterCount sum where cf & 0x3800000 == 0)
    public int GetStatByIndex(int index)
    {
        return (index * 4) switch
        {
            0x0c => FacilityCount,
            0x10 => EnemyTroopSurplus,
            0x14 => FriendlyTroopSurplus,
            0x24 => NetCapitalShipSurplus,
            0x28 => NetFighterSurplus,
            0x2c => SystemPriority,
            0x30 => EntityClassification,
            0x38 => FightersAboveThreshold,
            0x3c => AvailableFighters,
            0x40 => CapShipAtTarget,
            0x44 => CapShipNotAtTarget,
            0x48 => RegimentAtTarget,
            0x4c => RegimentNotAtTarget,
            0x50 => StarfighterAtTarget,
            0x54 => StarfighterNotAtTarget,
            0x7c => FacilityCountOwned,
            0x80 => EnemyShipCount,
            _ => 0, // unlisted fields return 0 until fully implemented
        };
    }

    public int FacilityCount; // +0x0c  (node+0x7c)
    public int EnemyTroopSurplus; // +0x10  (node+0x80)
    public int FriendlyTroopSurplus; // +0x14  (node+0x84)
    public int NetCapitalShipSurplus; // +0x24  (node+0x94) - own ships minus enemy ships, per planet
    public int NetFighterSurplus; // +0x28  (node+0x98) - own fighters minus enemy fighters, per planet
    public int SystemPriority; // +0x2c  (node+0x9c) - max(loyalty_urgency, config_base_priority), per planet
    public int EntityClassification; // +0x30  (node+0xa0) - min-tracked for enemies
    public int FightersAboveThreshold; // +0x38  (node+0xa8)
    public int AvailableFighters; // +0x3c  (node+0xac)
    // FUN_004319d0 lines 151-172: own-planet per-type unit count accumulators.
    // Set only in the own-faction path (sf & 0x1). Each pair splits on a CapabilityFlags bit.
    // "AtTarget": cf bit set by a deployment strategy record (FUN_004ca030 family for cap ships,
    //   FUN_004cfbd0 family for regiments, FUN_004da280 family for starfighters) when it designates
    //   this planet as its current operation target. "NotAtTarget": bit not set by that strategy.
    public int CapShipAtTarget; // +0x40  (node+0xb0): CapitalShipCount sum where cf & 0x200000 != 0
    public int CapShipNotAtTarget; // +0x44  (node+0xb4): CapitalShipCount sum where cf & 0x200000 == 0
    public int RegimentAtTarget; // +0x48  (node+0xb8): RegimentCount sum where cf & 0x400000 != 0
    public int RegimentNotAtTarget; // +0x4c  (node+0xbc): RegimentCount sum where cf & 0x400000 == 0
    public int StarfighterAtTarget; // +0x50  (node+0xc0): StarfighterCount sum where cf & 0x3800000 != 0
    public int StarfighterNotAtTarget; // +0x54  (node+0xc4): StarfighterCount sum where cf & 0x3800000 == 0 (statIndex 0x15)
    public int FacilityCountOwned; // +0x7c  (node+0xec)
    public int EnemyShipCount; // +0x80  (node+0xf0)
    public int ThreatenedCount; // +0x8c  (node+0xfc)
    public int AlignedEntityCount; // +0x90  (node+0x100)
    public int FleetSurplus; // +0x94  (node+0x104)
    public int UrgencyScore; // +0x98  (node+0x108) - 0-6 scale
    public int MinFleetThreshold; // +0xa0  (node+0x110)
    public int TotalTroopPower; // +0xc0  (node+0x130)
    public int FleetFacilityCombatPower; // +0xc4  (node+0x134) - max-tracked
    public int FighterPower; // +0xc8  (node+0x138) - max-tracked
    public int OwnShipyardStrength; // +0xd0  (node+0x140) - max-tracked
    public int FleetLeadership; // +0xf4  (node+0x164)
    public int CharGarrisonCountA; // +0xf8  (node+0x168)
    public int StandardCharCount; // +0x108 (node+0x178)
    public int SummaryScore; // +0x118 (node+0x188)
    public int FighterAvailabilitySummary; // +0x11c (node+0x18c)
    public int EnemyStrengthAccum; // +0x130 (node+0x1a0)
}

// ================================================================
// FLEET UNIT STATS (sub-object at node+0x50 in the fleet analysis list)
// ================================================================
public class FleetUnitStats
{
    // Template constructor sets CategoryFlags via OR masks per entity type:
    //   warship=0x200000, non-warship=0x100000, defense=0x20000/0x40000/0x80000,
    //   troop=0x400000/0x800000/0x10000, specforces=0x1/2/4/8_000000,
    //   manufacturing(atk<=def)=0x1000/(def<=atk)=0x2000,
    //   hull size: <1100=0x10, <2000=0x20, >=2000=0x40
    public int CategoryFlags; // +0x18 - bitmask, NOT a cost value
    public int CapabilityFlags; // +0x24 - entity+0x50 (research_order/capabilities)
    public int FactionAlignment; // +0x28 - 1=Rebel, 2=Empire, 0=neutral
    public int CombatModifier; // +0x2c - normalized primary weapon score
    public int ScaledCount; // +0x38 - warship: count*2.5; non-warship: has_bombardment; fleet: entity counter
    public int SizeTier; // +0x3c - warship: maneuverability; non-warship: shield_recharge*100
    public int WarshipAttribute; // +0x40 - warship: gravity_well; non-warship: hull_strength
    public int CapacityField; // +0x44 - warship: detection; non-warship: fighter_capacity
    public int CapacityProduct; // +0x48 - warship: hull*count; non-warship: troop_capacity
    public int WarshipCount; // +0x4c - warship: unit count; non-warship: detection
    public int Val_0x50; // +0x50 - non-warship: weapon_recharge_rate
    public int CombatStrength; // +0x64 - aggregate fleet combat score (populated by fleet list builder)
    public int SecondaryStrength; // +0x68 - secondary fleet combat metric (populated by fleet list builder)
    public int TertiaryValue; // +0x6c - own-side fleet value (populated by fleet list builder)
}

// ================================================================
// COMBAT ENGAGEMENT STATS (sub-object at node+0x40 in character list)
// ================================================================
public class CombatEngagementStats
{
    public int CombatScore; // +0x24
}

// ================================================================
// NODE STRUCTURES
// ================================================================

public class SystemAnalysisNode
{
    public SystemAnalysisNode Prev; // +0x10 - original traverses tail-to-head
    public uint DispositionFlags; // +0x24 - passed as param_2 to FUN_0041af90
    public uint CapabilityFlags; // +0x28 - passed as param_4 (swapped with +0x2c)
    public uint StatusFlags; // +0x2c - passed as param_3 (swapped with +0x28)
    public PerSystemStats Stats; // +0x70 - embedded sub-object
}

public class FleetAnalysisNode
{
    public FleetAnalysisNode Prev; // traversal link
    public uint OwnershipFlags; // +0x38 - bit 0: own side, bit 2: capital ship
    public FleetUnitStats Stats; // +0x50 - embedded sub-object
}

public class CharacterAnalysisNode
{
    public CharacterAnalysisNode Prev; // traversal link
    public uint EngagementFlags; // +0x30 - bit 1: in combat, bit 29: space battle
    public uint CategoryFlags; // +0x34 - unit category bits
    public CombatEngagementStats Stats; // +0x40 - embedded sub-object
}

// ================================================================
// MAIN SCORING CLASS
// ================================================================
public class GalaxyAnalysisScorer
{
    // ================================================================
    // STATE FLAGS (+0x04)
    // Bit 31 (0x80000000) = needs processing
    // Bit 29 (0x20000000) = system analysis ready
    // Bit 30 (0x40000000) = fleet analysis ready
    // Bit 28 (0x10000000) = character analysis ready
    // Lower bits set by accumulators during processing
    // ================================================================
    private uint _stateFlags;

    // ================================================================
    // ANALYSIS LISTS
    // Original: singly-linked lists traversed via Prev pointers.
    // C#: simple List<T> — same accumulator logic, no pointer chasing.
    // ================================================================
    private readonly List<SystemAnalysisNode> _systemNodes = new List<SystemAnalysisNode>();
    private readonly List<FleetAnalysisNode> _fleetNodes = new List<FleetAnalysisNode>();
    private readonly List<CharacterAnalysisNode> _characterNodes =
        new List<CharacterAnalysisNode>();

    // ================================================================
    // PRE-ACCUMULATION HOOKS (vtable slot 1, called before accumulation)
    // ================================================================
    private Action _hookA; // +0xc0
    private Action _hookB; // +0x104

    // ================================================================
    // PUBLIC POPULATION API
    // ================================================================

    /// <summary>Clears all node lists and state flags. Call before rebuilding each cycle.</summary>
    public void Clear()
    {
        _systemNodes.Clear();
        _fleetNodes.Clear();
        _characterNodes.Clear();
        _stateFlags = 0;
    }

    /// <summary>Adds a system analysis node to the scoring pass.</summary>
    public void AddSystemNode(SystemAnalysisNode node) => _systemNodes.Add(node);

    /// <summary>Adds a fleet unit analysis node to the scoring pass.</summary>
    public void AddFleetNode(FleetAnalysisNode node) => _fleetNodes.Add(node);

    /// <summary>Adds a character analysis node to the scoring pass.</summary>
    public void AddCharacterNode(CharacterAnalysisNode node) => _characterNodes.Add(node);

    /// <summary>Marks system data as ready for accumulation on next Score() call.</summary>
    public void MarkSystemsReady() => _stateFlags |= 0xa0000000u; // bits 31+29

    /// <summary>Marks fleet data as ready for accumulation on next Score() call.</summary>
    public void MarkFleetsReady() => _stateFlags |= 0xc0000000u; // bits 31+30

    /// <summary>Marks character data as ready for accumulation on next Score() call.</summary>
    public void MarkCharactersReady() => _stateFlags |= 0x90000000u; // bits 31+28

    // ================================================================
    // PUBLIC ACCUMULATED RESULT ACCESSORS
    // Strategy records read these after Score() runs.
    // ================================================================
    public int OwnControlledSystemCount => _ownControlledSystemCount;
    public int ContestedSystemCount => _contestedSystemCount;
    public int NeutralSystemCount => _neutralSystemCount;
    public int TotalFriendlyTroopSurplus => _totalFriendlyTroopSurplus;
    public int TotalEnemyTroopSurplus => _totalEnemyTroopSurplus;
    public int TotalFleetSurplus => _totalFleetSurplus;
    public int TotalFacilityCount => _totalFacilityCount;
    public int OwnFleetCombatStrength => _ownFleetCombatStrength;
    public int EnemyFleetCombatStrength => _enemyFleetCombatStrength;

    // ================================================================
    // SYSTEM ACCUMULATORS (40 dwords at +0x1ac, zeroed each pass)
    // ================================================================

    // --- Summed across all systems ---
    private int _totalFacilityCount; // +0x1ac <- +0x0c
    private int _totalEnemyTroopSurplus; // +0x1b0 <- +0x10
    private int _totalFriendlyTroopSurplus; // +0x1b4 <- +0x14
    private int _totalFightersAboveThreshold; // +0x1d0 <- +0x38
    private int _totalAvailableFighters; // +0x1d4 <- +0x3c
    private int _totalThreatenedCount; // +0x1d8 <- +0x8c
    private int _totalAlignedEntityCount; // +0x1dc <- +0x90
    private int _totalFleetSurplus; // +0x1e0 <- +0x94
    private int _totalNetCapitalShipSurplus; // +0x1e8 <- +0x24
    private int _totalNetFighterSurplus; // +0x1ec <- +0x28
    private int _totalMinFleetThreshold; // +0x1f0 <- +0xa0
    private int _totalUrgencyScore; // +0x1f4 <- +0x98
    private int _totalTroopPower; // +0x1f8 <- +0xc0
    private int _totalFacilityCountOwned; // +0x1cc <- +0x7c
    private int _totalSystemPriority; // +0x1fc <- +0x2c
    private int _totalEnemyShipCount; // +0x200 <- +0x80
    private int _totalFighterAvailability; // +0x204 <- +0x11c
    private int _totalFleetLeadership; // +0x21c <- +0xf4
    private int _totalSummaryScore; // +0x220 <- +0x118
    private int _totalEnemyStrength; // +0x224 <- +0x130
    private int _charGarrisonAccumA; // +0x214 <- +0xf8, then /5+1
    private int _standardCharAccum; // +0x218 <- +0x108, then /6+1

    // --- Max-tracked (galaxy-wide peaks) ---
    private int _maxFleetFacilityCombatPower; // +0x20c <- max of +0xc4
    private int _maxFighterPower; // +0x208 <- max of +0xc8
    private int _maxShipyardStrength; // +0x2fc <- max of +0xd0

    // --- Min-tracked (0 = unset sentinel) ---
    private int _minEntityClassification; // +0x210 <- min of +0x30

    // --- System counters (per-node flag tests) ---
    private int _ownControlledSystemCount; // +0x1b8  count where (statusFlags & 0x04)
    private int _contestedSystemCount; // +0x1bc  count where (statusFlags & 0x08)
    private int _neutralSystemCount; // +0x1c0  count where (statusFlags & 0x10)
    private int _uncontrolledCount; // +0x1c4  count where !(dispositionFlags & 0x40000000)
    private int _accessibleSystemCount; // +0x1c8  count where !(statusFlags & 0x02)

    // ================================================================
    // FLEET ACCUMULATORS (30 dwords at +0x24c, zeroed each pass)
    // ================================================================

    // --- Own-side (all owned units) ---
    private int _ownFleetScaledCount; // +0x24c <- +0x38
    private int _ownFleetSizeTier; // +0x250 <- +0x3c
    private int _ownFleetWarshipAttr; // +0x254 <- +0x40
    private int _ownFleetCombatStrength; // +0x258 <- +0x64
    private int _ownFleetCombatModifier; // +0x25c <- +0x2c
    private int _ownFleetTertiaryValue; // +0x260 <- +0x6c
    private int _ownFleetCapacity; // +0x264 <- +0x44

    // --- Own-side capital ships (owned AND flags & 0x04) ---
    private int _capitalShipCapacityProduct; // +0x268 <- +0x48
    private int _capitalShipWarshipCount; // +0x26c <- +0x4c
    private int _capitalShipVal_0x50; // +0x270 <- +0x50
    private int _capitalShipCombatStrength; // +0x274 <- +0x64
    private int _capitalShipSecondaryStr; // +0x278 <- +0x68
    private int _capitalShipFactionAlignment; // +0x27c <- +0x28
    private int _capitalShipCategoryFlags; // +0x280 <- +0x18

    // --- Enemy (units where flags & 0x01 == 0) ---
    private int _enemyFleetCount; // +0x284  incremented per enemy unit
    private int _enemyFleetCombatStrength; // +0x288 <- +0x64
    private int _enemyFleetFactionAlignment; // +0x28c <- +0x28
    private int _enemyFleetSecondaryStr; // +0x290 <- +0x68
    private int _enemyMaxCombatStrength; // +0x294  max of +0x64
    private int _enemyMaxSecondaryStr; // +0x298  paired with max
    private int _enemyMaxFactionAlignment; // +0x29c  paired with max

    // ================================================================
    // CHARACTER ACCUMULATORS (20 dwords at +0x2c4, zeroed each pass)
    // ================================================================

    private int _totalCombatScore; // +0x2c4 <- +0x24

    // --- Space battle (engagementFlags & 0x20000000) ---
    // 0x2000: char context = entity capability 0x100; fleet context = unarmed (no weapon flag 0x04)
    private int _spaceBattle_TypeA; // +0x2c8  count where (categoryFlags & 0x2000)

    // 0x0800: entity+0x50 flag 0x10 (specific capability, e.g. shield generator)
    private int _spaceBattle_TypeB; // +0x2cc  count where (categoryFlags & 0x0800)

    // 0x1000: char context = entity capability 0x80; fleet context = manufacturing (atk<=def)
    private int _spaceBattle_TypeC; // +0x2d0  count where (categoryFlags & 0x1000)

    // --- Ground battle (NOT space) ---
    private int _groundBattle_Infantry; // +0x2dc  count where (categoryFlags & 0x10000000)
    private int _groundBattle_SpecOps; // +0x2e0  count where (categoryFlags & 0x20000000)
    private int _groundBattle_Armor; // +0x2e4  count where (categoryFlags & 0x40000000)
    private int _groundBattle_Artillery; // +0x2e8  count where (categoryFlags & 0x80000000)

    // --- Ground elite sub-counts (category AND 0x800000) ---
    private int _groundElite_Infantry; // +0x2ec
    private int _groundElite_SpecOps; // +0x2f0
    private int _groundElite_Armor; // +0x2f4
    private int _groundElite_Artillery; // +0x2f8

    // ================================================================
    // ENTRY POINT - Calibration State 5 of FUN_00417cb0
    // ================================================================
    public void Score()
    {
        if ((_stateFlags & 0x80000000) == 0)
            return;

        _hookA?.Invoke();
        _hookB?.Invoke();

        AccumulateSystemScores();
        AccumulateFleetScores();
        AccumulateCharacterScores();

        _stateFlags &= 0x0FFFFFFF;
    }

    // ================================================================
    // SYSTEM ACCUMULATION - FUN_0041af90 per node
    // Original traverses tail-to-head via FUN_005f35d0_get_last_node_in_list
    // then following node+0x10 (prev pointer). Direction does not affect
    // results since all operations (addition, max, min) are commutative.
    // ================================================================
    private void AccumulateSystemScores()
    {
        if ((_stateFlags & 0x20000000) == 0)
            return;

        _totalFacilityCount = 0;
        _totalEnemyTroopSurplus = 0;
        _totalFriendlyTroopSurplus = 0;
        _totalFightersAboveThreshold = 0;
        _totalAvailableFighters = 0;
        _totalThreatenedCount = 0;
        _totalAlignedEntityCount = 0;
        _totalFleetSurplus = 0;
        _totalNetCapitalShipSurplus = 0;
        _totalNetFighterSurplus = 0;
        _totalMinFleetThreshold = 0;
        _totalUrgencyScore = 0;
        _totalTroopPower = 0;
        _totalFacilityCountOwned = 0;
        _totalSystemPriority = 0;
        _totalEnemyShipCount = 0;
        _totalFighterAvailability = 0;
        _totalFleetLeadership = 0;
        _totalSummaryScore = 0;
        _totalEnemyStrength = 0;
        _charGarrisonAccumA = 0;
        _standardCharAccum = 0;
        _maxFleetFacilityCombatPower = 0;
        _maxFighterPower = 0;
        _maxShipyardStrength = 0;
        _minEntityClassification = 0;
        _ownControlledSystemCount = 0;
        _contestedSystemCount = 0;
        _neutralSystemCount = 0;
        _uncontrolledCount = 0;
        _accessibleSystemCount = 0;

        _stateFlags &= 0xFFFFFA6F;

        foreach (SystemAnalysisNode node in _systemNodes)
        {
            AccumulateOneSystem(
                node.Stats,
                node.DispositionFlags,
                node.StatusFlags,
                node.CapabilityFlags
            );
        }

        if (_charGarrisonAccumA > 0)
            _charGarrisonAccumA = _charGarrisonAccumA / 5 + 1;

        if (_standardCharAccum > 0)
            _standardCharAccum = _standardCharAccum / 6 + 1;
    }

    // FUN_0041af90 - 22 additions, 3 max-trackers, 1 min-tracker, 5 flag counters.
    private void AccumulateOneSystem(
        PerSystemStats record,
        uint dispositionFlags, // node+0x24
        uint statusFlags, // node+0x2c (passed as param_3)
        uint capabilityFlags
    ) // node+0x28 (passed as param_4 - swapped with +0x2c)
    {
        // 22 unconditional additions
        _totalFacilityCount += record.FacilityCount;
        _totalEnemyTroopSurplus += record.EnemyTroopSurplus;
        _totalFriendlyTroopSurplus += record.FriendlyTroopSurplus;
        _totalFightersAboveThreshold += record.FightersAboveThreshold;
        _totalAvailableFighters += record.AvailableFighters;
        _totalThreatenedCount += record.ThreatenedCount;
        _totalAlignedEntityCount += record.AlignedEntityCount;
        _totalFleetSurplus += record.FleetSurplus;
        _totalNetCapitalShipSurplus += record.NetCapitalShipSurplus;
        _totalNetFighterSurplus += record.NetFighterSurplus;
        _totalMinFleetThreshold += record.MinFleetThreshold;
        _totalUrgencyScore += record.UrgencyScore;
        _totalTroopPower += record.TotalTroopPower;
        _totalFacilityCountOwned += record.FacilityCountOwned;
        _totalSystemPriority += record.SystemPriority;
        _totalEnemyShipCount += record.EnemyShipCount;
        _totalFighterAvailability += record.FighterAvailabilitySummary;
        _totalFleetLeadership += record.FleetLeadership;
        _totalSummaryScore += record.SummaryScore;
        _totalEnemyStrength += record.EnemyStrengthAccum;
        _charGarrisonAccumA += record.CharGarrisonCountA;
        _standardCharAccum += record.StandardCharCount;

        // 3 max-trackers
        if (_maxFleetFacilityCombatPower < record.FleetFacilityCombatPower)
            _maxFleetFacilityCombatPower = record.FleetFacilityCombatPower;

        if (_maxFighterPower < record.FighterPower)
            _maxFighterPower = record.FighterPower;

        if (_maxShipyardStrength < record.OwnShipyardStrength)
            _maxShipyardStrength = record.OwnShipyardStrength;

        // Own-controlled system (statusFlags bit 2)
        if ((statusFlags & 0x04) != 0)
        {
            _ownControlledSystemCount++;

            if ((statusFlags & 0x01000000) != 0)
                _stateFlags |= 0x10;

            if ((statusFlags & 0x02000000) != 0)
                _stateFlags |= 0x20;

            if ((statusFlags & 0x200) != 0)
                _stateFlags |= 0x80;

            if ((capabilityFlags & 0x08000000) != 0)
                _stateFlags |= 0x100;
        }

        // Contested system (statusFlags bit 3)
        if ((statusFlags & 0x08) != 0)
        {
            _contestedSystemCount++;

            if (
                record.EntityClassification < _minEntityClassification
                || _minEntityClassification == 0
            )
                _minEntityClassification = record.EntityClassification;
        }

        // Neutral system (statusFlags bit 4)
        if ((statusFlags & 0x10) != 0)
            _neutralSystemCount++;

        // Superweapon flag (statusFlags bit 31)
        if ((statusFlags & 0x80000000) != 0)
            _stateFlags |= 0x400;

        // Uncontrolled (dispositionFlags bit 30 NOT set)
        if ((dispositionFlags & 0x40000000) == 0)
            _uncontrolledCount++;

        // Accessible (statusFlags bit 1 NOT set)
        if ((statusFlags & 0x02) == 0)
            _accessibleSystemCount++;
    }

    // ================================================================
    // FLEET ACCUMULATION - FUN_0041b230 per node
    // Original uses SEH-based iterator (Ghidra shows single call + non-returning
    // cleanup). The actual runtime behavior iterates all fleet nodes.
    // ================================================================
    private void AccumulateFleetScores()
    {
        if ((_stateFlags & 0x40000000) == 0)
            return;

        _ownFleetScaledCount = 0;
        _ownFleetSizeTier = 0;
        _ownFleetWarshipAttr = 0;
        _ownFleetCombatStrength = 0;
        _ownFleetCombatModifier = 0;
        _ownFleetTertiaryValue = 0;
        _ownFleetCapacity = 0;
        _capitalShipCapacityProduct = 0;
        _capitalShipWarshipCount = 0;
        _capitalShipVal_0x50 = 0;
        _capitalShipCombatStrength = 0;
        _capitalShipSecondaryStr = 0;
        _capitalShipFactionAlignment = 0;
        _capitalShipCategoryFlags = 0;
        _enemyFleetCount = 0;
        _enemyFleetCombatStrength = 0;
        _enemyFleetFactionAlignment = 0;
        _enemyFleetSecondaryStr = 0;
        _enemyMaxCombatStrength = 0;
        _enemyMaxSecondaryStr = 0;
        _enemyMaxFactionAlignment = 0;

        _stateFlags &= 0xFFFFFFF3;

        foreach (FleetAnalysisNode node in _fleetNodes)
            AccumulateOneFleet(node.Stats, node.OwnershipFlags);
    }

    // FUN_0041b230 - own-side (flags & 1) vs enemy, with capital ship sub-tracking.
    private void AccumulateOneFleet(FleetUnitStats record, uint flags)
    {
        if ((flags & 0x01) != 0)
        {
            // Own-side capital ships (bit 2)
            if ((flags & 0x04) != 0)
            {
                _capitalShipCapacityProduct += record.CapacityProduct;
                _capitalShipWarshipCount += record.WarshipCount;
                _capitalShipVal_0x50 += record.Val_0x50;
                _capitalShipCombatStrength += record.CombatStrength;
                _capitalShipSecondaryStr += record.SecondaryStrength;
                _capitalShipFactionAlignment += record.FactionAlignment;
                _capitalShipCategoryFlags += record.CategoryFlags;
            }

            // All own-side units
            _ownFleetCombatModifier += record.CombatModifier;
            _ownFleetTertiaryValue += record.TertiaryValue;
            _ownFleetCombatStrength += record.CombatStrength;
            _ownFleetScaledCount += record.ScaledCount;
            _ownFleetSizeTier += record.SizeTier;
            _ownFleetWarshipAttr += record.WarshipAttribute;

            if ((flags & 0x10) != 0)
                _stateFlags |= 0x04;

            if ((flags & 0x20) != 0)
                _stateFlags |= 0x08;

            _ownFleetCapacity += record.CapacityField;
        }
        else
        {
            // Enemy unit
            _enemyFleetCount++;
            _enemyFleetCombatStrength += record.CombatStrength;
            _enemyFleetFactionAlignment += record.FactionAlignment;
            _enemyFleetSecondaryStr += record.SecondaryStrength;

            if (record.CombatStrength > _enemyMaxCombatStrength)
            {
                _enemyMaxCombatStrength = record.CombatStrength;
                _enemyMaxSecondaryStr = record.SecondaryStrength;
                _enemyMaxFactionAlignment = record.FactionAlignment;
            }
        }
    }

    // ================================================================
    // CHARACTER ACCUMULATION - FUN_0041b3c0 per node
    // Same SEH-iterator pattern as fleet. Iterates all character nodes.
    // ================================================================
    private void AccumulateCharacterScores()
    {
        if ((_stateFlags & 0x10000000) == 0)
            return;

        _totalCombatScore = 0;
        _spaceBattle_TypeA = 0;
        _spaceBattle_TypeB = 0;
        _spaceBattle_TypeC = 0;
        _groundBattle_Infantry = 0;
        _groundBattle_SpecOps = 0;
        _groundBattle_Armor = 0;
        _groundBattle_Artillery = 0;
        _groundElite_Infantry = 0;
        _groundElite_SpecOps = 0;
        _groundElite_Armor = 0;
        _groundElite_Artillery = 0;

        _stateFlags &= 0xFFFFFDFF;

        foreach (CharacterAnalysisNode node in _characterNodes)
            AccumulateOneCharacter(node.Stats, node.EngagementFlags, node.CategoryFlags);
    }

    // FUN_0041b3c0 - gates on in-combat (bit 1), then space vs ground branch.
    private void AccumulateOneCharacter(
        CombatEngagementStats record,
        uint engagementFlags,
        uint categoryFlags
    )
    {
        if ((engagementFlags & 0x02) == 0)
            return;

        _totalCombatScore += record.CombatScore;

        if ((engagementFlags & 0x20000000) != 0)
        {
            // Space battle
            if ((engagementFlags & 0x40) != 0)
                _stateFlags |= 0x200;

            if ((categoryFlags & 0x2000) != 0)
                _spaceBattle_TypeA++;

            if ((categoryFlags & 0x0800) != 0)
                _spaceBattle_TypeB++;

            if ((categoryFlags & 0x1000) != 0)
            {
                _spaceBattle_TypeC++;
                return;
            }
        }
        else
        {
            // Ground battle - 4 unit categories with elite sub-counts

            if ((categoryFlags & 0x10000000) != 0)
            {
                _groundBattle_Infantry++;
                if ((categoryFlags & 0x800000) != 0)
                    _groundElite_Infantry++;
            }
            else if ((categoryFlags & 0x20000000) != 0)
            {
                _groundBattle_SpecOps++;
                if ((categoryFlags & 0x800000) != 0)
                    _groundElite_SpecOps++;
            }

            if ((categoryFlags & 0x40000000) != 0)
            {
                _groundBattle_Armor++;
                if ((categoryFlags & 0x800000) != 0)
                    _groundElite_Armor++;
            }

            if ((categoryFlags & 0x80000000) != 0)
            {
                _groundBattle_Artillery++;
                if ((categoryFlags & 0x800000) != 0)
                    _groundElite_Artillery++;
            }
        }
    }
}
