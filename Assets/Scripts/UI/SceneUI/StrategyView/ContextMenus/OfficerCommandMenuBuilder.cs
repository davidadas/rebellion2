using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

/// <summary>
/// Builds the command-rank submenu shared by stationed and fleet personnel views.
/// </summary>
internal static class OfficerCommandMenuBuilder
{
    /// <summary>
    /// Builds the Command submenu for one officer.
    /// </summary>
    /// <param name="officer">The selected officer.</param>
    /// <param name="playerControlsOfficer">Whether the player controls the officer.</param>
    /// <returns>The complete command submenu.</returns>
    public static StrategyMenuCommand Build(Officer officer, bool playerControlsOfficer)
    {
        bool canChangeCommand =
            playerControlsOfficer
            && !officer.IsCaptured
            && !officer.IsKilled
            && !officer.IsRetired
            && officer.InjuryPoints <= 0
            && !officer.IsOnMission()
            && ((IMovable)officer).GetTransitMovement() == null
            && (
                officer.GetParentOfType<Fleet>() != null
                || officer.GetParentOfType<Planet>() != null
            );
        bool hasCommandChoice =
            officer.CurrentRank != OfficerRank.None
            || officer.AllowedRanks?.Any(rank => rank != OfficerRank.None) == true;
        bool commandMenuEnabled = canChangeCommand && hasCommandChoice;

        return new StrategyMenuCommand(
            StrategyMenuAction.Command,
            "Command",
            commandMenuEnabled,
            submenuCommands: new List<StrategyMenuCommand>
            {
                BuildRankCommand(
                    StrategyMenuAction.CommandNone,
                    "None",
                    OfficerRank.None,
                    officer,
                    commandMenuEnabled
                ),
                BuildRankCommand(
                    StrategyMenuAction.CommandCommander,
                    "Commander",
                    OfficerRank.Commander,
                    officer,
                    commandMenuEnabled
                ),
                BuildRankCommand(
                    StrategyMenuAction.CommandAdmiral,
                    "Admiral",
                    OfficerRank.Admiral,
                    officer,
                    commandMenuEnabled
                ),
                BuildRankCommand(
                    StrategyMenuAction.CommandGeneral,
                    "General",
                    OfficerRank.General,
                    officer,
                    commandMenuEnabled
                ),
            }
        );
    }

    /// <summary>
    /// Builds one radio-style command choice.
    /// </summary>
    /// <param name="action">The semantic appointment action.</param>
    /// <param name="text">The menu label.</param>
    /// <param name="rank">The represented command post.</param>
    /// <param name="officer">The selected officer.</param>
    /// <param name="canChangeCommand">Whether command changes are currently allowed.</param>
    /// <returns>The command choice.</returns>
    private static StrategyMenuCommand BuildRankCommand(
        StrategyMenuAction action,
        string text,
        OfficerRank rank,
        Officer officer,
        bool canChangeCommand
    )
    {
        bool rankAllowed = rank == OfficerRank.None || officer.AllowedRanks?.Contains(rank) == true;
        return new StrategyMenuCommand(
            action,
            text,
            canChangeCommand && rankAllowed,
            officer.CurrentRank == rank
                ? StrategyContextMenuIconKeys.CheckMark
                : StrategyContextMenuIconKeys.None
        );
    }
}
