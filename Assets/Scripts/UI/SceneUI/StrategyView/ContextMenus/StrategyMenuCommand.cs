using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

public sealed class StrategyMenuCommand : IContextMenuCommand
{
    public StrategyMenuCommand(int action, string text, bool enabled, int iconKey = 0)
    {
        Action = action;
        Text = text;
        Enabled = enabled;
        IconKey = iconKey;
    }

    public int Action { get; }
    public string Text { get; }
    public bool Enabled { get; }
    public int IconKey { get; }
    public bool HasIcon => IconKey != StrategyContextMenuIconKeys.None;
    public bool IsSubmenu => Action == StrategyContextMenuActions.Submenu;
    public bool UsesIconColumn => HasIcon || IsSubmenu;
}

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
    public const int Encyclopedia = 1019;
    public const int Abort = 1020;
    public const int DestroySystem = 1021;
    public const int GameSpeedPause = 9000;
    public const int GameSpeedVerySlow = 9001;
    public const int GameSpeedSlow = 9002;
    public const int GameSpeedMedium = 9003;
    public const int GameSpeedFast = 9004;

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

public static class StrategyContextMenuAvailability
{
    public static bool CanMoveItems(List<ISceneNode> items, string playerFactionId)
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

    public static bool CanCreateMission(
        List<ISceneNode> items,
        string playerFactionId,
        GameManager gameManager
    )
    {
        if (items == null || items.Count == 0 || gameManager == null)
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

        return gameManager.HasCreatableMissionOptions(playerFactionId, participants);
    }

    public static bool CanRetireFleet(List<ISceneNode> items, string playerFactionId)
    {
        if (items == null || items.Count == 0)
            return false;

        if (!PlayerControlsItems(items, playerFactionId))
            return false;

        foreach (ISceneNode item in items)
        {
            if (HasMoveBlockingStatus(item))
                return false;

            if (item is Officer officer && officer.IsMain)
                return false;

            if (item is not Officer and not SpecialForces)
                return false;
        }

        return true;
    }

    public static bool PlayerControlsItems(List<ISceneNode> items, string playerFactionId)
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

    private static bool HasMoveBlockingStatus(ISceneNode item)
    {
        if (item is IMovable movable && movable.Movement != null)
            return true;

        return item is IManufacturable manufacturable
            && manufacturable.GetManufacturingStatus() == ManufacturingStatus.Building;
    }

    private static bool CapturedOfficersHavePlayerEscort(
        List<ISceneNode> items,
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
