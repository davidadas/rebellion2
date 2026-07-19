using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Describes one immutable command presented by a strategy context menu.
/// </summary>
public sealed class StrategyMenuCommand : IContextMenuParentCommand
{
    /// <summary>
    /// Creates one strategy context-menu command.
    /// </summary>
    /// <param name="action">The semantic action identifier.</param>
    /// <param name="text">The displayed command text.</param>
    /// <param name="enabled">Whether the command can be selected.</param>
    /// <param name="iconKey">The optional semantic icon identifier.</param>
    /// <param name="submenuCommands">The ordered child commands.</param>
    /// <param name="usesIconColumn">Whether the row reserves space for an icon.</param>
    public StrategyMenuCommand(
        int action,
        string text,
        bool enabled,
        int iconKey = 0,
        IReadOnlyList<StrategyMenuCommand> submenuCommands = null,
        bool usesIconColumn = false
    )
    {
        Action = action;
        Text = text;
        Enabled = enabled;
        IconKey = iconKey;
        SubmenuCommands = submenuCommands?.ToList() ?? new List<StrategyMenuCommand>();
        UsesIconColumn = usesIconColumn || HasIcon || IsSubmenu;
    }

    /// <summary>
    /// Creates one submenu command.
    /// </summary>
    /// <param name="text">The displayed submenu text.</param>
    /// <param name="enabled">Whether the submenu can be opened.</param>
    /// <param name="submenuCommands">The ordered child commands.</param>
    public StrategyMenuCommand(
        string text,
        bool enabled,
        IReadOnlyList<StrategyMenuCommand> submenuCommands
    )
        : this(StrategyContextMenuActions.Submenu, text, enabled, 0, submenuCommands) { }

    public int Action { get; }

    public string Text { get; }

    public bool Enabled { get; }

    public int IconKey { get; }

    public IReadOnlyList<StrategyMenuCommand> SubmenuCommands { get; }

    public IReadOnlyList<IContextMenuCommand> ChildCommands => SubmenuCommands;

    public bool HasIcon => IconKey != StrategyContextMenuIconKeys.None;

    public bool IsSubmenu =>
        Action == StrategyContextMenuActions.Submenu || SubmenuCommands.Count > 0;

    public bool UsesIconColumn { get; }
}

/// <summary>
/// Defines stable action identifiers emitted by strategy context menus.
/// </summary>
public static class StrategyContextMenuActions
{
    public const int Submenu = 1003;
    public const int Build = 1004;
    public const int Stop = 1005;
    public const int CreateMission = 1006;
    public const int Destination = 1007;
    public const int Scrap = 1008;
    public const int Move = 1013;
    public const int Retire = 1014;
    public const int Status = 1015;
    public const int MoveConfirm = 1016;
    public const int PlanetaryBombardment = 1017;
    public const int Encyclopedia = 1019;
    public const int Abort = 1020;
    public const int DestroySystem = 1021;
    public const int Rename = 1022;
    public const int CreateFleet = 1023;
    public const int GameSpeedPause = 9000;
    public const int GameSpeedVerySlow = 9001;
    public const int GameSpeedSlow = 9002;
    public const int GameSpeedMedium = 9003;
    public const int GameSpeedFast = 9004;
    public const int AdvisorBuildShips = 9100;
    public const int AdvisorBuildTroops = 9101;
    public const int AdvisorBuildFacilities = 9102;
    public const int AdvisorGalaxyOverview = 9103;
    public const int AdvisorObjectives = 9104;
    public const int AdvisorTranslateCounterpart = 9107;
    public const int AdvisorAgentAdvice = 9108;
    public const int AdvisorMessages = 9120;
    public const int AdvisorLoyaltyMessages = 9121;
    public const int AdvisorFleetMessages = 9122;
    public const int AdvisorMissionMessages = 9123;
    public const int AdvisorResourceMessages = 9124;
    public const int AdvisorManufacturingMessages = 9125;
    public const int AdvisorDefenseMessages = 9126;
    public const int AdvisorConflictMessages = 9127;
    public const int AdvisorChatMessages = 9128;
    public const int AdvisorAdviceMessages = 9129;

    /// <summary>
    /// Tries to resolve a game-speed action to its simulation speed.
    /// </summary>
    /// <param name="action">The semantic action identifier.</param>
    /// <param name="speed">Receives the matching simulation speed.</param>
    /// <returns><see langword="true"/> when the action changes game speed.</returns>
    public static bool TryGetGameSpeed(int action, out TickSpeed speed)
    {
        switch (action)
        {
            case GameSpeedPause:
                speed = TickSpeed.Paused;
                return true;
            case GameSpeedVerySlow:
                speed = TickSpeed.VerySlow;
                return true;
            case GameSpeedSlow:
                speed = TickSpeed.Slow;
                return true;
            case GameSpeedMedium:
                speed = TickSpeed.Medium;
                return true;
            case GameSpeedFast:
                speed = TickSpeed.Fast;
                return true;
            default:
                speed = default;
                return false;
        }
    }
}

/// <summary>
/// Evaluates whether selected strategy items can execute shared context-menu actions.
/// </summary>
public static class StrategyContextMenuAvailability
{
    /// <summary>
    /// Determines whether every selected item can move under player control.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns><see langword="true"/> when the complete selection can move.</returns>
    public static bool CanMoveItems(IReadOnlyList<ISceneNode> items, string playerFactionId)
    {
        if (items == null || items.Count == 0)
            return false;

        if (!PlayerControlsItems(items, playerFactionId))
            return false;

        foreach (ISceneNode item in items)
        {
            if (HasMoveBlockingStatus(item))
                return false;
        }

        return CapturedOfficersHavePlayerEscort(items, playerFactionId);
    }

    /// <summary>
    /// Determines whether every selected item can participate in a new mission.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns><see langword="true"/> when the complete selection can create a mission.</returns>
    public static bool CanCreateMission(IReadOnlyList<ISceneNode> items, string playerFactionId)
    {
        if (items == null || items.Count == 0)
            return false;

        if (!PlayerControlsItems(items, playerFactionId))
            return false;

        List<IMissionParticipant> participants = new List<IMissionParticipant>();
        foreach (ISceneNode item in items)
        {
            if (item is Officer officer)
            {
                if (
                    officer.IsCaptured
                    || officer.InjuryPoints > 0
                    || officer.Movement != null
                    || HasMoveBlockingStatus(item)
                )
                    return false;

                participants.Add(officer);
                continue;
            }

            if (item is SpecialForces specialForces)
            {
                if (HasMoveBlockingStatus(item))
                    return false;

                participants.Add(specialForces);
                continue;
            }

            return false;
        }

        return participants.Count > 0;
    }

    /// <summary>
    /// Determines whether the player controls every selected item.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns><see langword="true"/> when the player controls the complete selection.</returns>
    public static bool PlayerControlsItems(IReadOnlyList<ISceneNode> items, string playerFactionId)
    {
        if (items == null || items.Count == 0)
            return false;

        foreach (ISceneNode item in items)
        {
            if (!PlayerControlsItem(item, playerFactionId))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the player controls one scene node.
    /// </summary>
    /// <param name="item">The scene node to evaluate.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns><see langword="true"/> when the player controls the item.</returns>
    public static bool PlayerControlsItem(ISceneNode item, string playerFactionId)
    {
        if (item is Officer { IsCaptured: true } officer)
            return string.Equals(
                officer.CaptorInstanceID,
                playerFactionId,
                System.StringComparison.Ordinal
            );

        return string.Equals(
            item?.GetOwnerInstanceID(),
            playerFactionId,
            System.StringComparison.Ordinal
        );
    }

    /// <summary>
    /// Determines whether movement or manufacturing prevents moving one item.
    /// </summary>
    /// <param name="item">The scene node to evaluate.</param>
    /// <returns><see langword="true"/> when the item's current status prevents movement.</returns>
    private static bool HasMoveBlockingStatus(ISceneNode item)
    {
        if (item is IMovable movable && movable.Movement != null)
            return true;

        return item is IManufacturable manufacturable
            && manufacturable.GetManufacturingStatus() == ManufacturingStatus.Building;
    }

    /// <summary>
    /// Determines whether every captured officer has a player-controlled escort in the selection.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns><see langword="true"/> when each captured officer has an escort.</returns>
    private static bool CapturedOfficersHavePlayerEscort(
        IReadOnlyList<ISceneNode> items,
        string playerFactionId
    )
    {
        if (items == null)
            return false;

        foreach (
            Officer capturedOfficer in items.OfType<Officer>().Where(officer => officer.IsCaptured)
        )
        {
            bool hasEscort = items
                .OfType<Officer>()
                .Any(officer =>
                    !ReferenceEquals(officer, capturedOfficer)
                    && !officer.IsCaptured
                    && string.Equals(
                        officer.GetOwnerInstanceID(),
                        playerFactionId,
                        System.StringComparison.Ordinal
                    )
                );

            if (!hasEscort)
                return false;
        }

        return true;
    }
}
