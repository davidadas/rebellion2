using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Extensions;

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
        StrategyMenuAction action,
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
        IsSubmenu = submenuCommands != null;
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
        : this(StrategyMenuAction.None, text, enabled, 0, submenuCommands) { }

    public StrategyMenuAction Action { get; }

    public string Text { get; }

    public bool Enabled { get; }

    public int IconKey { get; }

    public IReadOnlyList<StrategyMenuCommand> SubmenuCommands { get; }

    public IReadOnlyList<IContextMenuCommand> ChildCommands => SubmenuCommands;

    public bool HasIcon => IconKey != StrategyContextMenuIconKeys.None;

    public bool IsSubmenu { get; }

    public bool UsesIconColumn { get; }
}

/// <summary>
/// Identifies semantic actions emitted by strategy context menus.
/// </summary>
public enum StrategyMenuAction
{
    None,
    Build,
    Stop,
    CreateMission,
    Destination,
    Scrap,
    Move,
    Retire,
    Status,
    MoveConfirm,
    PlanetaryBombardment,
    Encyclopedia,
    Abort,
    DestroySystem,
    Rename,
    CreateFleet,
    BombardMilitaryFacilities,
    BombardCivilianFacilities,
    GeneralBombardment,
    PlanetaryAssault,
    GameSpeedPause,
    GameSpeedVerySlow,
    GameSpeedSlow,
    GameSpeedMedium,
    GameSpeedFast,
    AdvisorBuildShips,
    AdvisorBuildTroops,
    AdvisorBuildFacilities,
    AdvisorGalaxyOverview,
    AdvisorObjectives,
    AdvisorTranslateCounterpart,
    AdvisorAgentAdvice,
    AdvisorMessages,
    AdvisorLoyaltyMessages,
    AdvisorFleetMessages,
    AdvisorMissionMessages,
    AdvisorResourceMessages,
    AdvisorManufacturingMessages,
    AdvisorDefenseMessages,
    AdvisorConflictMessages,
    AdvisorChatMessages,
    AdvisorAdviceMessages,
}

/// <summary>
/// Resolves strategy menu actions to their domain values.
/// </summary>
public static class StrategyMenuActionExtensions
{
    /// <summary>
    /// Tries to resolve a game-speed action to its simulation speed.
    /// </summary>
    /// <param name="action">The semantic action identifier.</param>
    /// <param name="speed">Receives the matching simulation speed.</param>
    /// <returns><see langword="true"/> when the action changes game speed.</returns>
    public static bool TryGetGameSpeed(this StrategyMenuAction action, out TickSpeed speed)
    {
        switch (action)
        {
            case StrategyMenuAction.GameSpeedPause:
                speed = TickSpeed.Paused;
                return true;
            case StrategyMenuAction.GameSpeedVerySlow:
                speed = TickSpeed.VerySlow;
                return true;
            case StrategyMenuAction.GameSpeedSlow:
                speed = TickSpeed.Slow;
                return true;
            case StrategyMenuAction.GameSpeedMedium:
                speed = TickSpeed.Medium;
                return true;
            case StrategyMenuAction.GameSpeedFast:
                speed = TickSpeed.Fast;
                return true;
            default:
                speed = default;
                return false;
        }
    }

    /// <summary>
    /// Tries to resolve a context-menu action to its bombardment target profile.
    /// </summary>
    /// <param name="action">The semantic action identifier.</param>
    /// <param name="type">Receives the matching bombardment target profile.</param>
    /// <returns>True when the action executes a bombardment.</returns>
    public static bool TryGetBombardmentType(
        this StrategyMenuAction action,
        out BombardmentType type
    )
    {
        switch (action)
        {
            case StrategyMenuAction.BombardMilitaryFacilities:
                type = BombardmentType.Military;
                return true;
            case StrategyMenuAction.BombardCivilianFacilities:
                type = BombardmentType.Civilian;
                return true;
            case StrategyMenuAction.GeneralBombardment:
                type = BombardmentType.General;
                return true;
            case StrategyMenuAction.DestroySystem:
                type = BombardmentType.DestroySystem;
                return true;
            default:
                type = default;
                return false;
        }
    }
}

/// <summary>
/// Builds the source-ordered planetary bombardment submenu.
/// </summary>
internal static class StrategyBombardmentMenuBuilder
{
    /// <summary>
    /// Creates the bombardment parent and its four target-profile commands.
    /// </summary>
    /// <param name="canBombard">Whether ordinary bombardment profiles can execute.</param>
    /// <param name="canDestroySystem">Whether the selected fleets can destroy the system.</param>
    /// <returns>The complete bombardment submenu.</returns>
    public static StrategyMenuCommand Build(bool canBombard, bool canDestroySystem)
    {
        return new StrategyMenuCommand(
            StrategyMenuAction.PlanetaryBombardment,
            "Planetary Bombardment",
            canBombard || canDestroySystem,
            submenuCommands: new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(
                    StrategyMenuAction.BombardMilitaryFacilities,
                    "Target Military Facilities",
                    canBombard
                ),
                new StrategyMenuCommand(
                    StrategyMenuAction.BombardCivilianFacilities,
                    "Target Civilian Facilities",
                    canBombard
                ),
                new StrategyMenuCommand(
                    StrategyMenuAction.GeneralBombardment,
                    "General Bombardment",
                    canBombard
                ),
                new StrategyMenuCommand(
                    StrategyMenuAction.DestroySystem,
                    "Destroy System",
                    canDestroySystem
                ),
            }
        );
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
            if (
                item is not IManufacturable { ManufacturingStatus: ManufacturingStatus.Building }
                && IsInTransit(item)
            )
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
                if (officer.IsCaptured || officer.InjuryPoints > 0 || IsInTransit(item))
                    return false;

                participants.Add(officer);
                continue;
            }

            if (item is SpecialForces specialForces)
            {
                if (
                    IsInTransit(item)
                    || specialForces.ManufacturingStatus == ManufacturingStatus.Building
                )
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
    /// Determines whether an item is currently traveling, including travel inherited from a container.
    /// </summary>
    /// <param name="item">The scene node to evaluate.</param>
    /// <returns><see langword="true"/> when the item is in transit.</returns>
    private static bool IsInTransit(ISceneNode item)
    {
        return item is IMovable movable && movable.GetTransitMovement() != null;
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
