using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;

/// <summary>
/// Reads and edits AI-only category budgets used during starting-unit generation.
/// </summary>
internal static class StartingUnitBudgetEditor
{
    /// <summary>
    /// Reads an AI-only starting-unit bonus for one faction, difficulty, category, and galaxy size.
    /// </summary>
    /// <param name="config">Generation configuration to inspect.</param>
    /// <param name="factionID">Faction receiving the allocation.</param>
    /// <param name="difficulty">Difficulty selecting the allocation.</param>
    /// <param name="category">Unit category eligible for the allocation.</param>
    /// <param name="galaxySize">Galaxy size selecting the percentage.</param>
    /// <returns>The configured percentage, or zero when no allocation is authored.</returns>
    internal static int ReadAIOnlyBonus(
        GameGenerationConfig config,
        string factionID,
        GameDifficulty difficulty,
        StartingUnitBudgetCategory category,
        GameSize galaxySize
    )
    {
        StartingUnitBudgetBonus bonus = FindAIOnlyBonus(config, factionID, difficulty, category);
        return bonus
                ?.BudgetLevels?.FirstOrDefault(level => level.GalaxySize == (int)galaxySize)
                ?.Percentage
            ?? 0;
    }

    /// <summary>
    /// Writes an AI-only starting-unit bonus, creating a configuration record when the selected
    /// profile does not already define one.
    /// </summary>
    /// <param name="config">Generation configuration to update.</param>
    /// <param name="factionID">Faction receiving the allocation.</param>
    /// <param name="difficulty">Difficulty selecting the allocation.</param>
    /// <param name="category">Unit category eligible for the allocation.</param>
    /// <param name="galaxySize">Galaxy size selecting the percentage.</param>
    /// <param name="value">Percentage to store.</param>
    internal static void WriteAIOnlyBonus(
        GameGenerationConfig config,
        string factionID,
        GameDifficulty difficulty,
        StartingUnitBudgetCategory category,
        GameSize galaxySize,
        int value
    )
    {
        FactionBudget budget = config.UnitDeployment.FactionBudgets.First(candidate =>
            candidate.FactionID == factionID
        );
        budget.Bonuses ??= new List<StartingUnitBudgetBonus>();
        StartingUnitBudgetBonus bonus = FindAIOnlyBonus(config, factionID, difficulty, category);
        if (bonus == null)
        {
            if (value == 0)
                return;

            bonus = new StartingUnitBudgetBonus
            {
                Difficulty = difficulty,
                AIOnly = true,
                Category = category,
                BudgetLevels = Enum.GetValues(typeof(GameSize))
                    .Cast<GameSize>()
                    .Select(size => new BudgetLevel { GalaxySize = (int)size, Percentage = 0 })
                    .ToList(),
            };
            budget.Bonuses.Add(bonus);
        }

        bonus.BudgetLevels ??= new List<BudgetLevel>();
        BudgetLevel level = bonus.BudgetLevels.FirstOrDefault(candidate =>
            candidate.GalaxySize == (int)galaxySize
        );
        if (level == null)
        {
            level = new BudgetLevel { GalaxySize = (int)galaxySize };
            bonus.BudgetLevels.Add(level);
        }

        level.Percentage = value;
    }

    /// <summary>
    /// Finds one AI-only category budget in a faction's generation configuration.
    /// </summary>
    /// <param name="config">Generation configuration to inspect.</param>
    /// <param name="factionID">Faction receiving the allocation.</param>
    /// <param name="difficulty">Difficulty selecting the allocation.</param>
    /// <param name="category">Unit category eligible for the allocation.</param>
    /// <returns>The matching allocation, or null when none is authored.</returns>
    private static StartingUnitBudgetBonus FindAIOnlyBonus(
        GameGenerationConfig config,
        string factionID,
        GameDifficulty difficulty,
        StartingUnitBudgetCategory category
    ) =>
        config
            .UnitDeployment.FactionBudgets.FirstOrDefault(budget => budget.FactionID == factionID)
            ?.Bonuses?.FirstOrDefault(bonus =>
                bonus.AIOnly && bonus.Difficulty == difficulty && bonus.Category == category
            );
}
