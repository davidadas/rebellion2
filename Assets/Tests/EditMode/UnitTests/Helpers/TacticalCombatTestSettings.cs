using Rebellion.Game;

/// <summary>
/// Supplies the tactical-battle settings that content normally provides, for tests that build a
/// space-combat configuration by hand.
/// </summary>
public static class TacticalCombatTestSettings
{
    /// <summary>
    /// Fills in the shipped tactical settings on a hand-built configuration.
    /// </summary>
    /// <param name="config">The configuration to complete.</param>
    /// <returns>The same configuration, for chaining.</returns>
    public static GameConfig.SpaceCombatConfig WithShippedTacticalSettings(
        this GameConfig.SpaceCombatConfig config
    )
    {
        config.TacticalFighterHullStrength = 5;
        config.TacticalFighterShieldMultiplier = 3;
        config.TacticalFighterRechargeMultiplier = 0.6;
        config.TacticalTurbolaserFighterHitChance = 0.15;
        config.TacticalCapitalWeaponRangeMultiplier = 2;
        config.TacticalShieldGeneratorHitChance = 0.03;
        config.TacticalShieldRechargeMultiplier = 0.5;
        config.TacticalShieldGeneratorRepairMultiplier = 2;
        config.BattleStationSuperlaserRechargeRounds = 360;
        config.BattleStationTrenchRunBaseChance = 0.9;
        config.BattleStationTrenchRunMinimumChance = 0.25;
        return config;
    }
}
