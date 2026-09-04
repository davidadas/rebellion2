using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;
using UnityEngine;

/// <summary>
/// Projects controller-owned Mission Create sessions into immutable themed presentation snapshots.
/// </summary>
internal sealed class MissionCreateWindowProjector
{
    private static readonly Color32 _gray = Color.gray;
    private static readonly Color32 _white = Color.white;

    private readonly Func<string, Planet> getObservedPlanet;
    private readonly Func<UIContext> getUIContext;
    private readonly Func<MissionStartRequest, MissionOdds> getMissionOdds;

    /// <summary>
    /// Creates a Mission Create projector with access to the current presentation context.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="getMissionOdds">Calculates complete odds for a mission configuration.</param>
    /// <param name="getObservedPlanet">Returns the latest player-visible planet snapshot by ID.</param>
    public MissionCreateWindowProjector(
        Func<UIContext> getUIContext,
        Func<MissionStartRequest, MissionOdds> getMissionOdds = null,
        Func<string, Planet> getObservedPlanet = null
    )
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.getMissionOdds = getMissionOdds ?? (_ => null);
        this.getObservedPlanet = getObservedPlanet ?? (_ => null);
    }

    /// <summary>
    /// Builds one complete Mission Create presentation snapshot.
    /// </summary>
    /// <param name="session">The controller-owned Mission Create session.</param>
    /// <param name="window">The owning window shell.</param>
    /// <returns>The complete immutable presentation snapshot.</returns>
    public MissionCreateWindowRenderData Build(MissionCreateWindowSession session, UIWindow window)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        UIContext uiContext = GetRequiredUIContext();
        FactionTheme playerTheme = uiContext.GetPlayerFactionTheme();
        MissionCreateWindowTheme theme = playerTheme?.StrategyWindows?.MissionCreate;
        StrategyCheckboxTheme checkboxTheme = playerTheme?.StrategyCheckboxTheme;
        StrategyMissionChoice selectedChoice = session.SelectedChoice;
        ISceneNode target = session.Target.GetMissionTarget(
            selectedChoice?.TargetKind ?? MissionTargetKind.Planet
        );
        MissionOddsRenderData selectedMissionOdds = BuildMissionOdds(session, selectedChoice);
        return new MissionCreateWindowRenderData(
            window.X,
            window.Y,
            session.ActiveTab,
            session.DropdownOpen,
            session.Agents.Count > 0,
            uiContext.GetTexture(theme?.TitleImagePath),
            selectedChoice?.Name,
            GetMissionChoiceTexture(uiContext, selectedChoice),
            target?.GetDisplayName(),
            GetTargetTexture(uiContext, target),
            target is Planet,
            uiContext.GetTexture(theme?.AgentsHeaderImagePath),
            uiContext.GetTexture(theme?.DecoysHeaderImagePath),
            BuildTabs(uiContext, theme, session.ActiveTab),
            session.DropdownOpen
                ? BuildDropdownItems(uiContext, session, selectedMissionOdds)
                : Array.Empty<StrategyDropdownItemRenderData>(),
            session.ActiveTab == MissionCreateWindowTab.Personnel
                ? BuildParticipantRows(uiContext, session.Agents, session.SelectedAgents)
                : Array.Empty<MissionParticipantRowRenderData>(),
            session.ActiveTab == MissionCreateWindowTab.Personnel
                ? BuildParticipantRows(uiContext, session.Decoys, session.SelectedDecoys)
                : Array.Empty<MissionParticipantRowRenderData>(),
            selectedMissionOdds,
            session.ShowMissionOdds,
            uiContext.GetTexture(checkboxTheme?.FrameImagePath),
            uiContext.GetTexture(checkboxTheme?.CheckMarkImagePath)
        );
    }

    /// <summary>
    /// Projects workflow tabs with faction art.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="theme">The current faction's Mission Create theme.</param>
    /// <param name="activeTab">The selected workflow tab.</param>
    /// <returns>The ordered immutable tab snapshots.</returns>
    private static IReadOnlyList<MissionCreateTabRenderData> BuildTabs(
        UIContext uiContext,
        MissionCreateWindowTheme theme,
        MissionCreateWindowTab activeTab
    )
    {
        List<MissionCreateTabRenderData> tabs = new List<MissionCreateTabRenderData>();
        foreach (MissionCreateWindowTab tab in MissionCreateWindowRenderData.OrderedTabs)
        {
            WindowTabImageTheme tabTheme = GetTabTheme(theme, tab);
            bool active = tab == activeTab;
            tabs.Add(
                new MissionCreateTabRenderData(
                    tab,
                    uiContext.GetTexture(tabTheme?.GetImagePath(active ? 0 : 1)),
                    uiContext.GetTexture(tabTheme?.GetImagePath(0))
                )
            );
        }

        return tabs;
    }

    /// <summary>
    /// Resolves faction art for one semantic Mission Create tab.
    /// </summary>
    /// <param name="theme">The current faction's Mission Create theme.</param>
    /// <param name="tab">The requested semantic tab.</param>
    /// <returns>The matching tab image theme.</returns>
    private static WindowTabImageTheme GetTabTheme(
        MissionCreateWindowTheme theme,
        MissionCreateWindowTab tab
    )
    {
        return tab switch
        {
            MissionCreateWindowTab.Mission => theme?.MissionTab,
            MissionCreateWindowTab.Personnel => theme?.PersonnelTab,
            _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, null),
        };
    }

    /// <summary>
    /// Projects mission-choice dropdown rows in source option order.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="session">The source Mission Create session.</param>
    /// <param name="selectedMissionOdds">The already calculated selected-choice estimate.</param>
    /// <returns>The ordered immutable dropdown snapshots.</returns>
    private IReadOnlyList<StrategyDropdownItemRenderData> BuildDropdownItems(
        UIContext uiContext,
        MissionCreateWindowSession session,
        MissionOddsRenderData selectedMissionOdds
    )
    {
        List<StrategyDropdownItemRenderData> rows = new List<StrategyDropdownItemRenderData>();
        for (int index = 0; index < session.Choices.Count; index++)
        {
            StrategyMissionChoice choice = session.Choices[index];
            rows.Add(
                new StrategyDropdownItemRenderData(
                    GetMissionChoiceTexture(uiContext, choice),
                    choice.Name,
                    index == session.SelectedMissionIndex ? _white : _gray,
                    index == session.SelectedMissionIndex
                        ? selectedMissionOdds
                        : BuildMissionOdds(session, choice)
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// Builds the current participant split's estimate for one mission choice.
    /// </summary>
    /// <param name="session">The source Mission Create session.</param>
    /// <param name="choice">The mission choice to evaluate.</param>
    /// <returns>The rounded presentation estimate, or null when the choice is invalid.</returns>
    private MissionOddsRenderData BuildMissionOdds(
        MissionCreateWindowSession session,
        StrategyMissionChoice choice
    )
    {
        Planet sessionPlanet = session?.Target?.Planet?.Planet;
        Planet planet = GetLatestObservedPlanet(sessionPlanet);
        if (
            choice == null
            || planet == null
            || session.Agents.Count == 0
            || !session.ShowMissionOdds
        )
            return null;

        MissionOdds odds = getMissionOdds(
            session.CreateMissionRequest(
                choice,
                planet,
                GetLatestObservedTarget(session.Target.Item, sessionPlanet, planet)
            )
        );
        return odds == null
            ? null
            : new MissionOddsRenderData(odds.OverallSuccessProbability, odds.FoilProbability);
    }

    /// <summary>
    /// Resolves the current player-visible planet so an open mission window reflects fleet changes.
    /// </summary>
    /// <param name="sessionPlanet">The snapshot captured when targeting began.</param>
    /// <returns>The latest visible snapshot, falling back to the session snapshot.</returns>
    private Planet GetLatestObservedPlanet(Planet sessionPlanet)
    {
        if (sessionPlanet == null)
            return null;

        return getObservedPlanet(sessionPlanet.InstanceID) ?? sessionPlanet;
    }

    /// <summary>
    /// Rebinds the selected target into the latest visible planet hierarchy.
    /// </summary>
    /// <param name="sessionTarget">The target captured when targeting began.</param>
    /// <param name="sessionPlanet">The planet captured when targeting began.</param>
    /// <param name="observedPlanet">The latest player-visible planet snapshot.</param>
    /// <returns>The corresponding current target, or the original target when it is unavailable.</returns>
    private static ISceneNode GetLatestObservedTarget(
        ISceneNode sessionTarget,
        Planet sessionPlanet,
        Planet observedPlanet
    )
    {
        if (sessionTarget == null || observedPlanet == null)
            return sessionTarget;
        if (
            ReferenceEquals(sessionTarget, sessionPlanet)
            || sessionTarget.InstanceID == sessionPlanet?.InstanceID
        )
            return observedPlanet;

        return observedPlanet
                .GetChildren<ISceneNode>(recursive: true)
                .FirstOrDefault(node => node.InstanceID == sessionTarget.InstanceID)
            ?? sessionTarget;
    }

    /// <summary>
    /// Projects one participant role list with selection and movement presentation.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="participants">The source participants.</param>
    /// <param name="selection">The selected source indices.</param>
    /// <returns>The ordered immutable participant snapshots.</returns>
    private static IReadOnlyList<MissionParticipantRowRenderData> BuildParticipantRows(
        UIContext uiContext,
        IReadOnlyList<IMissionParticipant> participants,
        IReadOnlyCollection<int> selection
    )
    {
        List<MissionParticipantRowRenderData> rows = new List<MissionParticipantRowRenderData>();
        DefenseWindowTheme defenseTheme = uiContext
            .GetPlayerFactionTheme()
            ?.StrategyWindows?.Defense;
        Texture normalBackground = uiContext.GetTexture(defenseTheme?.PersonnelBackgroundImagePath);
        Texture enrouteBackground = uiContext.GetTexture(defenseTheme?.EnrouteBackgroundImagePath);
        for (int index = 0; index < participants.Count; index++)
        {
            if (participants[index] is not ISceneNode node)
                continue;

            bool isInTransit = node is IMovable movable && movable.GetTransitMovement() != null;
            Texture backgroundTexture = isInTransit
                ? uiContext.GetEntityStatusTexture(node, true) ?? enrouteBackground
                : normalBackground;
            rows.Add(
                new MissionParticipantRowRenderData(
                    node.GetDisplayName(),
                    selection.Contains(index) ? _white : _gray,
                    backgroundTexture,
                    uiContext.GetEntityTexture(node, true)
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// Gets one mission choice's large faction icon.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="choice">The mission choice to project.</param>
    /// <returns>The resolved icon texture, or null.</returns>
    private static Texture GetMissionChoiceTexture(
        UIContext uiContext,
        StrategyMissionChoice choice
    )
    {
        string path = uiContext
            .GetPlayerFactionTheme()
            ?.MissionIcons?.GetImagePath(choice?.IconKey, false);
        return uiContext.GetTexture(path);
    }

    /// <summary>
    /// Gets the mission target image when it comes from an entity asset.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="target">The mission target.</param>
    /// <returns>The resolved target texture, or null.</returns>
    private static Texture GetTargetTexture(UIContext uiContext, ISceneNode target)
    {
        if (target == null)
            return null;

        return target is Planet planet
            ? uiContext.GetPlanetTexture(planet)
            : uiContext.GetEntityTexture(target, true);
    }

    /// <summary>
    /// Gets the current presentation context or rejects an incomplete controller graph.
    /// </summary>
    /// <returns>The current strategy presentation context.</returns>
    private UIContext GetRequiredUIContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException("The Mission Create UI context is unavailable.");
    }
}
