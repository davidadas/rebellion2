using System;
using Rebellion.Game.Results;

/// <summary>
/// Carries one pending fleet encounter between the strategy and tactical scenes.
/// </summary>
public static class TacticalBattleLaunchContext
{
    /// <summary>
    /// Gets the tactical battle scene name.
    /// </summary>
    public const string SceneName = "TacticalBattle";

    /// <summary>
    /// Gets the encounter selected for player command.
    /// </summary>
    public static PendingCombatResult Encounter { get; private set; }

    /// <summary>
    /// Stores a pending encounter for the tactical scene.
    /// </summary>
    /// <param name="encounter">The pending encounter to command.</param>
    public static void Open(PendingCombatResult encounter)
    {
        Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
    }

    /// <summary>
    /// Clears the encounter after tactical combat has completed or been abandoned.
    /// </summary>
    public static void Clear()
    {
        Encounter = null;
    }
}
