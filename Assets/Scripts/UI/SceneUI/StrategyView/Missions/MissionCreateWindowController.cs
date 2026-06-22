using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

public enum MissionCreateCommandResult
{
    None,
    OpenInfo,
    CloseWindow,
}

public sealed class MissionCreateWindowController
{
    private readonly GameManager gameManager;

    public MissionCreateWindowController(GameManager gameManager)
    {
        this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
    }

    public bool TryInitializeWindow(
        MissionCreateWindowView view,
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        if (view == null || sourceWindow == null || target == null || target.Item is Fleet)
            return false;

        List<IMissionParticipant> participants = GetMissionSourceParticipants(
            sourceItems,
            playerFactionId
        );
        if (participants.Count == 0)
            return false;

        List<StrategyMissionChoice> choices = BuildMissionChoices(
            participants,
            target,
            playerFactionId
        );
        if (choices.Count == 0)
            return false;

        view.InitializeWindow(sourceWindow, target, choices, participants);
        return true;
    }

    public MissionCreateCommandResult ExecuteCommand(
        MissionCreateWindowView view,
        StrategyUIRequests.ExecuteMissionCreateCommand request
    )
    {
        switch (request.Command)
        {
            case MissionCreateWindowCommand.Info:
                return MissionCreateCommandResult.OpenInfo;
            case MissionCreateWindowCommand.Cancel:
                return MissionCreateCommandResult.CloseWindow;
            case MissionCreateWindowCommand.Ok:
                return TryStartMission(view, request.Choice, request.Agents, request.Decoys)
                    ? MissionCreateCommandResult.CloseWindow
                    : MissionCreateCommandResult.None;
            default:
                return MissionCreateCommandResult.None;
        }
    }

    private bool TryStartMission(
        MissionCreateWindowView view,
        StrategyMissionChoice choice,
        IReadOnlyList<IMissionParticipant> agents,
        IReadOnlyList<IMissionParticipant> decoys
    )
    {
        if (
            choice == null
            || agents == null
            || agents.Count == 0
            || view?.MissionTarget?.Planet == null
        )
            return false;

        Planet missionPlanet = view.MissionTarget.Planet.Planet;
        if (missionPlanet == null)
            return false;

        return gameManager.InitiateMissionWithSpecificTarget(
            choice.Type,
            agents.ToList(),
            decoys?.ToList() ?? new List<IMissionParticipant>(),
            missionPlanet,
            view.MissionTarget.GetSpecificMissionTarget(choice.Type),
            view.MissionTarget.GetMissionTargetOfficer(choice.Type),
            choice.Discipline
        );
    }

    private List<IMissionParticipant> GetMissionSourceParticipants(
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        List<ISceneNode> items = sourceItems?.ToList() ?? new List<ISceneNode>();
        if (!StrategyContextMenuAvailability.CanCreateMission(items, playerFactionId, gameManager))
            return new List<IMissionParticipant>();

        return items.OfType<IMissionParticipant>().ToList();
    }

    private List<StrategyMissionChoice> BuildMissionChoices(
        List<IMissionParticipant> participants,
        StrategyMissionTarget target,
        string playerFactionId
    )
    {
        List<StrategyMissionChoice> choices = new List<StrategyMissionChoice>();
        if (target?.Planet?.Planet == null)
            return choices;

        foreach (
            MissionOption option in gameManager.GetCreatableMissionOptions(
                playerFactionId,
                participants,
                target.Planet.Planet,
                target.Item
            )
        )
            choices.Add(new StrategyMissionChoice(option));

        return choices;
    }
}
