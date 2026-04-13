// FUN_004bdda0_create_large_selection_strategy_table_record
// Creates the concrete StrategyRecord subclass for a given type ID.
// Type IDs 1-14 match the switch cases in the original factory function.
public static class StrategyRecordFactory
{
    /// <summary>
    /// Creates a strategy record for the given type ID and owner side.
    /// Returns null if the type ID is not recognized (matches original behavior
    /// when allocation fails or type falls through the switch).
    /// </summary>
    public static StrategyRecord Create(int typeId, int ownerSide)
    {
        return typeId switch
        {
            // 0x78 bytes — FUN_004d9cc0 — local shortage issue generator type 1
            1 => new LocalShortageGeneratorType1Record(ownerSide),
            // 0x80 bytes — FUN_004e1190 — local shortage issue generator type 2
            2 => new LocalShortageGeneratorType2Record(ownerSide),
            // 0x7c bytes — FUN_004d5e90 — type 3/7 (shared load function name)
            3 => new ShortageGeneratorType3Record(ownerSide),
            // 0x60 bytes — FUN_004d1590 — mission assignment
            4 => new MissionAssignmentRecord(ownerSide),
            // 0x88 bytes — FUN_004cee90
            5 => new StrategyRecordType5(ownerSide),
            // 0x8c bytes — FUN_004dc7d0 — three-phase cycling A
            6 => new ThreePhaseStrategyRecordA(ownerSide),
            // 0x7c bytes — FUN_004d2260 — three-phase cycling B
            7 => new ThreePhaseStrategyRecordB(ownerSide),
            // 0x58 bytes — FUN_004ce780
            8 => new StrategyRecordType8(ownerSide),
            // 0x58 bytes — FUN_004ce410
            9 => new StrategyRecordType9(ownerSide),
            // 0x84 bytes — FUN_004cba20
            10 => new StrategyRecordType10(ownerSide),
            // 0x88 bytes — FUN_004c7b90 — three-phase cycling C
            11 => new ThreePhaseStrategyRecordC(ownerSide),
            // 0x60 bytes — FUN_004c75d0 — FOIL production automation
            12 => new ProductionAutomationRecord(ownerSide),
            // 0x70 bytes — FUN_004be450 — RLEVAD diplomacy strategy
            13 => new DiplomacyStrategyRecord(ownerSide),
            // 0x4c bytes — FUN_004d1b80 — capital ship name generator
            14 => new CapitalShipNameGeneratorRecord(ownerSide),
            _ => null,
        };
    }
}
