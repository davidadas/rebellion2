using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using UnityEngine.EventSystems;

public static class StrategyUIRequests
{
    public sealed class OpenPlanetSystemWindow : IUIDispatchRequest
    {
        public OpenPlanetSystemWindow(PlanetSystem system, int sourceX, int sourceY)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            System = system;
            SourceX = sourceX;
            SourceY = sourceY;
        }

        public PlanetSystem System { get; }
        public int SourceX { get; }
        public int SourceY { get; }
    }

    public sealed class OpenPlanetWindow : IUIDispatchRequest
    {
        public OpenPlanetWindow(Planet planet, PlanetIcon icon, int sourceX, int sourceY)
        {
            if (planet == null)
                throw new ArgumentNullException(nameof(planet));

            Planet = planet;
            Icon = icon;
            SourceX = sourceX;
            SourceY = sourceY;
        }

        public Planet Planet { get; }
        public PlanetIcon Icon { get; }
        public int SourceX { get; }
        public int SourceY { get; }
    }

    public sealed class StartWindowItemDrag : IUIDispatchRequest
    {
        public StartWindowItemDrag(int windowId, int sourceX, int sourceY)
        {
            WindowId = windowId;
            SourceX = sourceX;
            SourceY = sourceY;
        }

        public int WindowId { get; }
        public int SourceX { get; }
        public int SourceY { get; }
    }

    public sealed class WindowItemDragMove : IUIDispatchRequest
    {
        public WindowItemDragMove(PointerEventData eventData)
        {
            EventData = eventData ?? throw new ArgumentNullException(nameof(eventData));
        }

        public PointerEventData EventData { get; }
    }

    public sealed class WindowItemDragEnd : IUIDispatchRequest
    {
        public WindowItemDragEnd(PointerEventData eventData)
        {
            EventData = eventData ?? throw new ArgumentNullException(nameof(eventData));
        }

        public PointerEventData EventData { get; }
    }

    public sealed class OpenSelectedFinderItem : IUIDispatchRequest
    {
        public OpenSelectedFinderItem(int windowId)
        {
            WindowId = windowId;
        }

        public int WindowId { get; }
    }

    public sealed class ReleaseWindowButton : IUIDispatchRequest
    {
        public ReleaseWindowButton(int windowId, int action, int sourceX, int sourceY)
        {
            WindowId = windowId;
            Action = action;
            SourceX = sourceX;
            SourceY = sourceY;
        }

        public int WindowId { get; }
        public int Action { get; }
        public int SourceX { get; }
        public int SourceY { get; }
    }

    public sealed class ReleaseHudButton : IUIDispatchRequest
    {
        public ReleaseHudButton(int action, int sourceX, int sourceY)
        {
            Action = action;
            SourceX = sourceX;
            SourceY = sourceY;
        }

        public int Action { get; }
        public int SourceX { get; }
        public int SourceY { get; }
    }

    public sealed class OpenMessagesTab : IUIDispatchRequest
    {
        public OpenMessagesTab(int tab)
        {
            Tab = tab;
        }

        public int Tab { get; }
    }

    public sealed class OpenSpeedContextMenu : IUIDispatchRequest
    {
        public OpenSpeedContextMenu(int sourceX, int sourceY)
        {
            SourceX = sourceX;
            SourceY = sourceY;
        }

        public int SourceX { get; }
        public int SourceY { get; }
    }

    public sealed class RequestRender : IUIDispatchRequest { }

    public sealed class CloseWindow : IUIDispatchRequest
    {
        public CloseWindow(int windowId)
        {
            WindowId = windowId;
        }

        public int WindowId { get; }
    }

    public sealed class OpenConstructionInfo : IUIDispatchRequest
    {
        public OpenConstructionInfo(int windowId)
        {
            WindowId = windowId;
        }

        public int WindowId { get; }
    }

    public sealed class OpenStatusInfo : IUIDispatchRequest
    {
        public OpenStatusInfo(int windowId)
        {
            WindowId = windowId;
        }

        public int WindowId { get; }
    }

    public sealed class StartConstruction : IUIDispatchRequest
    {
        public StartConstruction(int windowId, int buildPanel, int buildSelection, int buildCount)
        {
            WindowId = windowId;
            BuildPanel = buildPanel;
            BuildSelection = buildSelection;
            BuildCount = buildCount;
        }

        public int WindowId { get; }
        public int BuildPanel { get; }
        public int BuildSelection { get; }
        public int BuildCount { get; }
    }

    public sealed class ConfirmDialogChoice : IUIDispatchRequest
    {
        public ConfirmDialogChoice(int windowId, bool confirmed)
        {
            WindowId = windowId;
            Confirmed = confirmed;
        }

        public int WindowId { get; }
        public bool Confirmed { get; }
    }

    public sealed class ExecuteMissionCreateCommand : IUIDispatchRequest
    {
        public ExecuteMissionCreateCommand(
            int windowId,
            MissionCreateWindowCommand command,
            StrategyMissionChoice choice = null,
            IReadOnlyList<IMissionParticipant> agents = null,
            IReadOnlyList<IMissionParticipant> decoys = null
        )
        {
            WindowId = windowId;
            Command = command;
            Choice = choice;
            Agents = agents;
            Decoys = decoys;
        }

        public int WindowId { get; }
        public MissionCreateWindowCommand Command { get; }
        public StrategyMissionChoice Choice { get; }
        public IReadOnlyList<IMissionParticipant> Agents { get; }
        public IReadOnlyList<IMissionParticipant> Decoys { get; }
    }
}

public static class StrategyHudActions
{
    public const int Options = 50;
    public const int Messages = 51;
    public const int SystemFinder = 52;
    public const int FleetFinder = 53;
    public const int TroopFinder = 54;
    public const int PersonnelFinder = 55;
    public const int Encyclopedia = 56;
}
