using System;
using System.Collections.Generic;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;
using UnityEngine;

/// <summary>
/// Projects controller-owned Missions sessions into immutable themed presentation snapshots.
/// </summary>
internal sealed class MissionsWindowProjector
{
    private static readonly Color32 _white = Color.white;

    private readonly Func<string, ISceneNode> findVisibleNode;
    private readonly Func<UIContext> getUIContext;

    /// <summary>
    /// Creates a Missions-window projector with access to the current presentation context.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="findVisibleNode">Resolves a node from the visible galaxy snapshot.</param>
    public MissionsWindowProjector(
        Func<UIContext> getUIContext,
        Func<string, ISceneNode> findVisibleNode
    )
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.findVisibleNode =
            findVisibleNode ?? throw new ArgumentNullException(nameof(findVisibleNode));
    }

    /// <summary>
    /// Builds one complete Missions-window presentation snapshot.
    /// </summary>
    /// <param name="session">The controller-owned Missions session.</param>
    /// <param name="window">The owning window shell.</param>
    /// <param name="active">Whether the window currently has focus.</param>
    /// <returns>The complete immutable presentation snapshot.</returns>
    public MissionsWindowRenderData Build(
        MissionsWindowSession session,
        UIWindow window,
        bool active
    )
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        UIContext uiContext = GetRequiredUIContext();
        Mission selectedMission = session.SelectedMission;
        ISceneNode target = ResolveMissionTarget(selectedMission) ?? session.Planet.Planet;
        return new MissionsWindowRenderData(
            window.X,
            window.Y,
            GetTitleTexture(uiContext, session.Planet.OwnerFactionId, active),
            session.Planet.Planet.GetDisplayName(),
            session.ActiveRole,
            session.SelectedMissionIndex,
            selectedMission != null,
            selectedMission == null ? string.Empty : target?.GetDisplayName(),
            selectedMission == null ? null : uiContext.GetEntityTexture(target, true),
            BuildMissionRows(uiContext, session.Missions, session.SelectedMissionIndex),
            BuildTabs(uiContext, session.ActiveRole),
            BuildParticipantRows(uiContext, session.ActiveParticipants)
        );
    }

    /// <summary>
    /// Projects mission list rows in source mission order.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="missions">The source missions.</param>
    /// <param name="selectedMissionIndex">The selected source mission index.</param>
    /// <returns>The ordered immutable row snapshots.</returns>
    private static IReadOnlyList<MissionListRowRenderData> BuildMissionRows(
        UIContext uiContext,
        IReadOnlyList<Mission> missions,
        int selectedMissionIndex
    )
    {
        List<MissionListRowRenderData> rows = new List<MissionListRowRenderData>();
        Texture selectionTexture = uiContext.GetTexture(
            uiContext.GetPlayerFactionTheme()?.StrategyWindows?.Missions?.SelectionImagePath
        );
        for (int index = 0; index < missions.Count; index++)
        {
            Mission mission = missions[index];
            string iconKey = GetMissionIconKey(mission);
            string iconPath = uiContext
                .GetTheme(mission.OwnerInstanceID)
                ?.MissionIcons?.GetImagePath(iconKey, true);
            rows.Add(
                new MissionListRowRenderData(
                    mission.GetDisplayName(),
                    uiContext.GetTexture(iconPath),
                    index == selectedMissionIndex ? selectionTexture : null
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// Projects participant tabs with their active and pressed textures.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="activeRole">The selected participant role.</param>
    /// <returns>The ordered immutable tab snapshots.</returns>
    private static IReadOnlyList<MissionsWindowTabRenderData> BuildTabs(
        UIContext uiContext,
        MissionParticipantRole activeRole
    )
    {
        MissionsWindowTheme theme = uiContext.GetPlayerFactionTheme()?.StrategyWindows?.Missions;
        List<MissionsWindowTabRenderData> tabs = new List<MissionsWindowTabRenderData>();
        foreach (MissionParticipantRole role in MissionsWindowRenderData.OrderedRoles)
        {
            WindowTabImageTheme tabTheme = GetTabTheme(theme, role);
            tabs.Add(
                new MissionsWindowTabRenderData(
                    role,
                    uiContext.GetTexture(tabTheme?.GetImagePath(role == activeRole ? 0 : 1)),
                    uiContext.GetTexture(tabTheme?.GetImagePath(0))
                )
            );
        }

        return tabs;
    }

    /// <summary>
    /// Resolves faction art for one semantic mission-participant role.
    /// </summary>
    /// <param name="theme">The current faction's Missions-window theme.</param>
    /// <param name="role">The requested participant role.</param>
    /// <returns>The matching tab image theme.</returns>
    private static WindowTabImageTheme GetTabTheme(
        MissionsWindowTheme theme,
        MissionParticipantRole role
    )
    {
        return role switch
        {
            MissionParticipantRole.Agent => theme?.AgentsTab,
            MissionParticipantRole.Decoy => theme?.DecoysTab,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
    }

    /// <summary>
    /// Projects participant rows in mission role order.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="participants">The source participants.</param>
    /// <returns>The ordered immutable row snapshots.</returns>
    private static IReadOnlyList<MissionParticipantRowRenderData> BuildParticipantRows(
        UIContext uiContext,
        IReadOnlyList<IMissionParticipant> participants
    )
    {
        List<MissionParticipantRowRenderData> rows = new List<MissionParticipantRowRenderData>();
        foreach (IMissionParticipant participant in participants)
        {
            if (participant is not ISceneNode node)
                continue;

            bool isInTransit = node is IMovable movable && movable.GetTransitMovement() != null;
            Texture backgroundTexture = isInTransit
                ? uiContext.GetEntityStatusTexture(node, true)
                : null;
            rows.Add(
                new MissionParticipantRowRenderData(
                    node.GetDisplayName(),
                    _white,
                    backgroundTexture,
                    uiContext.GetEntityTexture(node, true),
                    isInTransit
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// Resolves a selected mission's concrete target from the visible strategy snapshot.
    /// </summary>
    /// <param name="mission">The selected mission.</param>
    /// <returns>The visible mission target, location, or parent.</returns>
    /// <remarks>Uses the visible-node resolver supplied to the projector.</remarks>
    private ISceneNode ResolveMissionTarget(Mission mission)
    {
        if (mission == null)
            return null;

        string targetInstanceId = GetMissionTargetInstanceId(mission);
        if (!string.IsNullOrEmpty(targetInstanceId))
            return findVisibleNode(targetInstanceId);

        return !string.IsNullOrEmpty(mission.LocationInstanceID)
            ? findVisibleNode(mission.LocationInstanceID)
            : mission.GetParent();
    }

    /// <summary>
    /// Gets the specific target identifier stored by mission variants with explicit targets.
    /// </summary>
    /// <param name="mission">The mission to inspect.</param>
    /// <returns>The explicit target identifier, or null.</returns>
    private static string GetMissionTargetInstanceId(Mission mission)
    {
        return mission switch
        {
            SabotageMission sabotage => sabotage.SabotageTargetInstanceID,
            RecruitmentMission recruitment => recruitment.TargetOfficerInstanceID,
            AbductionMission abduction => abduction.TargetOfficerInstanceID,
            AssassinationMission assassination => assassination.TargetOfficerInstanceID,
            RescueMission rescue => rescue.TargetOfficerInstanceID,
            _ => null,
        };
    }

    /// <summary>
    /// Resolves one mission's faction-themed icon key.
    /// </summary>
    /// <param name="mission">The mission to project.</param>
    /// <returns>The configured icon key.</returns>
    private static string GetMissionIconKey(Mission mission)
    {
        ResearchDiscipline? discipline = mission is ResearchMission researchMission
            ? researchMission.Discipline
            : null;
        return MissionIconKeys.GetMissionIconKey(mission.ConfigKey, discipline);
    }

    /// <summary>
    /// Gets the active or inactive window-title texture.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="ownerFactionId">The represented planet's owner identifier.</param>
    /// <param name="active">Whether the window currently has focus.</param>
    /// <returns>The resolved title texture.</returns>
    private static Texture GetTitleTexture(UIContext uiContext, string ownerFactionId, bool active)
    {
        WindowTitleTheme theme = uiContext.GetTheme(ownerFactionId)?.WindowTitleTheme;
        return uiContext.GetTexture(active ? theme?.ActiveImagePath : theme?.InactiveImagePath);
    }

    /// <summary>
    /// Gets the current presentation context or rejects an incomplete controller graph.
    /// </summary>
    /// <returns>The current strategy presentation context.</returns>
    private UIContext GetRequiredUIContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException("The Missions UI context is unavailable.");
    }
}
