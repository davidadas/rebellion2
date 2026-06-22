using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MissionsWindowView
    : MonoBehaviour,
        IStrategyUIRuntimeReceiver,
        IStrategyWindowStatusTargetView,
        IStrategyWindowContent,
        IPlanetIconWindowView
{
    private readonly List<MissionListRowView> missionRowViews = new List<MissionListRowView>();
    private readonly List<MissionParticipantRowView> participantRowViews =
        new List<MissionParticipantRowView>();
    private readonly List<string> renderedMissionNames = new List<string>();
    private readonly List<string> renderedParticipantNames = new List<string>();

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage titleImage;

    [SerializeField]
    private TextMeshProUGUI captionTextField;

    [SerializeField]
    private RawImage[] buttonImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private int[] buttonActions = System.Array.Empty<int>();

    [SerializeField]
    private ScrollAreaView missionListScrollArea;

    [SerializeField]
    private MissionListRowView missionListRowTemplate;

    [SerializeField]
    private RectTransform missionListContentPaddingTemplate;

    [SerializeField]
    private TextMeshProUGUI targetTitleTextField;

    [SerializeField]
    private RawImage targetImage;

    [SerializeField]
    private TextMeshProUGUI targetNameTextField;

    [SerializeField]
    private RectTransform tabsRoot;

    [SerializeField]
    private RawImage[] tabImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private Button[] tabButtons = System.Array.Empty<Button>();

    [SerializeField]
    private ScrollAreaView participantsScrollArea;

    [SerializeField]
    private MissionParticipantRowView participantRowTemplate;

    [SerializeField]
    private Texture2D backgroundTexture;

    [SerializeField]
    private Texture2D openSectorButtonUpTexture;

    [SerializeField]
    private Texture2D openSectorButtonDownTexture;

    [SerializeField]
    private Texture2D minimizeButtonUpTexture;

    [SerializeField]
    private Texture2D minimizeButtonDownTexture;

    [SerializeField]
    private Texture2D closeButtonUpTexture;

    [SerializeField]
    private Texture2D closeButtonDownTexture;

    [SerializeField]
    private Texture2D participantEnrouteBackgroundTexture;

    private MissionsWindowRenderData lastData;
    private RectInt targetImageSlotRect;
    private bool hasTargetImageSlotRect;
    private StrategyUIRuntime uiRuntime;
    private UIContext uiContext;
    private bool stateInitialized;
    private int activeTab;
    private int selectedIndex = -1;
    private bool renderedAnyMissions;
    private int renderedParticipantActiveTab = -1;
    private int renderedParticipantSelectedIndex = -1;
    private bool renderedAnyParticipants;

    public GalaxyMapPlanet GalaxyMapPlanet { get; private set; }
    public PlanetIcon PlanetIcon => PlanetIcon.Mission;

    public void InitializeWindow(GalaxyMapPlanet planet)
    {
        GalaxyMapPlanet = planet;
    }

    public void ReconcilePlanet(GalaxyMapPlanet planet)
    {
        InitializeWindow(planet);
    }

    public void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active)
    {
        if (window == null)
            return;

        List<Mission> missions = GalaxyMapPlanet?.Planet?.Missions?.ToList() ?? new List<Mission>();
        int currentActiveTab = GetActiveTab(0);
        int selectedMissionIndex = GetSelectedIndex(-1, missions.Count);
        Mission mission =
            selectedMissionIndex >= 0 && selectedMissionIndex < missions.Count
                ? missions[selectedMissionIndex]
                : null;
        ISceneNode target =
            mission == null
                ? null
                : context.FindVisibleNode(mission.TargetInstanceID) ?? GalaxyMapPlanet?.Planet;
        List<IMissionParticipant> participants =
            mission == null ? new List<IMissionParticipant>()
            : currentActiveTab == 0 ? mission.MainParticipants
            : mission.DecoyParticipants;

        Render(
            new MissionsWindowRenderData
            {
                X = window.X,
                Y = window.Y,
                GalaxyMapPlanet = GalaxyMapPlanet,
                OwnerFactionId = GalaxyMapPlanet?.OwnerFactionId,
                ActiveTab = currentActiveTab,
                Active = active,
                Caption = GalaxyMapPlanet?.Planet.GetDisplayName() ?? string.Empty,
                Target = target,
                SelectedIndex = selectedMissionIndex,
                SourceMissions = missions,
                SourceParticipants = participants,
            }
        );
    }

    public void Initialize(StrategyUIRuntime uiRuntime)
    {
        this.uiRuntime = uiRuntime;
        uiContext = uiRuntime?.Context;
    }

    public void Render(MissionsWindowRenderData data)
    {
        VerifyReferences();
        data = CreateRenderData(data);
        lastData = data;

        RectTransform rect = transform as RectTransform;
        UILayout.SetSourcePosition(rect, data.X, data.Y);
        SetImageFromTemplate(backgroundImage, backgroundTexture);
        UILayout.SetInteractiveImageTexture(
            titleImage,
            GetTitleTexture(data.OwnerFactionId, data.Active)
        );
        SetTemplateText(captionTextField, data.Caption);

        RenderButtons();
        RenderMissionRows(data.Missions);
        RenderSelectedMission(data);
        gameObject.SetActive(true);
    }

    private MissionsWindowRenderData CreateRenderData(MissionsWindowRenderData state)
    {
        InitializeState(state.ActiveTab, state.SelectedIndex);
        int missionCount = state.SourceMissions?.Count ?? 0;
        if (selectedIndex < 0 && missionCount > 0)
            selectedIndex = 0;
        if (selectedIndex >= missionCount)
            selectedIndex = missionCount - 1;

        MissionsWindowRenderData data = new MissionsWindowRenderData
        {
            X = state.X,
            Y = state.Y,
            OwnerFactionId = state.OwnerFactionId,
            ActiveTab = activeTab,
            Active = state.Active,
            Caption = state.Caption ?? string.Empty,
            Target = state.Target,
            SourceMissions = state.SourceMissions,
            SourceParticipants = state.SourceParticipants,
            SelectedIndex = selectedIndex,
        };

        IReadOnlyList<Mission> missions = state.SourceMissions ?? System.Array.Empty<Mission>();
        Texture2D missionSelectionTexture = uiContext?.GetTexture(
            GetMissionsTheme()?.SelectionImagePath
        );
        for (int i = 0; i < missions.Count; i++)
        {
            Mission mission = missions[i];
            data.Missions.Add(
                new MissionListRowRenderData
                {
                    Name = mission.GetDisplayName(),
                    IconTexture = GetMissionIconTexture(mission, true),
                    SelectionTexture = i == selectedIndex ? missionSelectionTexture : null,
                }
            );
        }

        if (state.Target != null)
        {
            data.HasSelectedMission = true;
            data.TargetName = state.Target.GetDisplayName();
            data.TargetTexture = uiContext?.GetEntityTexture(state.Target, true);
        }

        IReadOnlyList<IMissionParticipant> participants =
            state.SourceParticipants ?? System.Array.Empty<IMissionParticipant>();
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i] is ISceneNode participant)
            {
                data.Participants.Add(
                    new MissionParticipantRowRenderData
                    {
                        Name = participant.GetDisplayName(),
                        NameColor = Color.white,
                        BackgroundTexture = GetParticipantBackgroundTexture(participant),
                        EntityTexture = uiContext?.GetEntityTexture(participant, true),
                    }
                );
            }
        }

        return data;
    }

    internal int GetActiveTab(int initialTab)
    {
        InitializeState(initialTab, -1);
        return activeTab;
    }

    internal int GetSelectedIndex(int initialSelectedIndex, int missionCount)
    {
        InitializeState(0, initialSelectedIndex);
        if (selectedIndex < 0 && initialSelectedIndex >= 0)
            selectedIndex = initialSelectedIndex;
        if (selectedIndex < 0 && missionCount > 0)
            selectedIndex = 0;
        if (selectedIndex >= missionCount)
            selectedIndex = missionCount - 1;

        return selectedIndex;
    }

    internal bool NeedsMissionListScrollbar(int missionCount)
    {
        return missionCount * GetMissionListRowHeight() > missionListScrollArea.ViewportHeight;
    }

    internal int GetMissionListScrollContentHeight(int missionCount)
    {
        return UILayout.GetSourceRect(missionListContentPaddingTemplate).height
            + missionCount * GetMissionListRowHeight();
    }

    internal int GetMissionListScrollViewportHeight()
    {
        return Mathf.RoundToInt(missionListScrollArea.ViewportHeight);
    }

    internal int GetMissionListScrollStep()
    {
        return GetMissionListRowHeight();
    }

    internal bool NeedsParticipantScrollbar(int participantCount)
    {
        return participantCount * GetParticipantRowHeight() > participantsScrollArea.ViewportHeight;
    }

    internal int GetParticipantScrollContentHeight(int participantCount)
    {
        return UILayout.GetSourceRect(participantRowTemplate.transform as RectTransform).y
            + participantCount * GetParticipantRowHeight();
    }

    internal int GetParticipantScrollViewportHeight()
    {
        return Mathf.RoundToInt(participantsScrollArea.ViewportHeight);
    }

    internal int GetParticipantScrollStep()
    {
        return GetParticipantRowHeight();
    }

    private Texture2D GetMissionIconTexture(Mission mission, bool small)
    {
        string iconKey = GetMissionIconKey(mission);
        string imagePath = uiContext
            ?.GetTheme(mission.OwnerInstanceID)
            ?.MissionIcons?.GetImagePath(iconKey, small);
        return uiContext?.GetTexture(imagePath);
    }

    private static string GetMissionIconKey(Mission mission)
    {
        return mission.ConfigKey switch
        {
            "Diplomacy" => MissionIconKeys.Diplomacy,
            "Rescue" => MissionIconKeys.Rescue,
            "Sabotage" => MissionIconKeys.Sabotage,
            "Espionage" => MissionIconKeys.Espionage,
            "Reconnaissance" => MissionIconKeys.Reconnaissance,
            "Recruitment" => MissionIconKeys.Recruitment,
            "Abduction" => MissionIconKeys.Abduction,
            "Research" => GetResearchMissionIconKey(mission),
            "InciteUprising" => MissionIconKeys.InciteUprising,
            "JediTraining" => MissionIconKeys.JediTraining,
            "SubdueUprising" => MissionIconKeys.SubdueUprising,
            "Assassination" => MissionIconKeys.Assassination,
            _ => null,
        };
    }

    private static string GetResearchMissionIconKey(Mission mission)
    {
        return mission is ResearchMission researchMission
            ? researchMission.Discipline switch
            {
                ResearchDiscipline.ShipDesign => MissionIconKeys.ResearchShipDesign,
                ResearchDiscipline.FacilityDesign => MissionIconKeys.ResearchFacilityDesign,
                ResearchDiscipline.TroopTraining => MissionIconKeys.ResearchTroopTraining,
                _ => null,
            }
            : MissionIconKeys.ResearchShipDesign;
    }

    internal List<StrategyMenuCommand> BuildContextMenu()
    {
        bool hasMission = GetSelectedMission() != null;
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(
                StrategyContextMenuActions.Encyclopedia,
                "Encyclopedia",
                hasMission
            ),
            new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", hasMission),
            new StrategyMenuCommand(StrategyContextMenuActions.Abort, "Abort", hasMission),
        };
    }

    internal StrategyStatusTarget GetStatusTarget(GalaxyMapPlanet planet)
    {
        Mission mission = GetSelectedMission();
        return mission == null ? null : new StrategyStatusTarget(planet, mission);
    }

    StrategyStatusTarget IStrategyWindowStatusTargetView.GetStatusTarget(GalaxyMapPlanet planet)
    {
        return GetStatusTarget(planet);
    }

    public ISceneNode GetDestinationTargetItem()
    {
        return GetSelectedMission();
    }

    private void Awake()
    {
        VerifyReferences();
        BindControls();
    }

    private void BindControls()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int tab = i;
            tabButtons[i].onClick.AddListener(() => SelectTab(tab));
        }
    }

    private void InitializeState(int initialTab, int initialSelectedIndex)
    {
        if (stateInitialized)
            return;

        activeTab = Mathf.Max(0, initialTab);
        selectedIndex = initialSelectedIndex;
        stateInitialized = true;
    }

    private void SetActiveTab(int tab)
    {
        if (tab == activeTab)
            return;

        activeTab = tab;
    }

    private void SelectTab(int tab)
    {
        if (lastData == null || !lastData.HasSelectedMission || tab < 0 || tab >= tabButtons.Length)
            return;

        SetActiveTab(tab);
        uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void SelectMission(int index)
    {
        if (lastData?.SourceMissions == null || index < 0 || index >= lastData.SourceMissions.Count)
            return;

        selectedIndex = index;
        uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private bool TrySelectTarget(int itemIndex)
    {
        if (uiRuntime?.Targeting.IsTargeting != true || lastData?.GalaxyMapPlanet == null)
            return false;

        return uiRuntime.Targeting.TrySelectTarget(
            new StrategyMissionTarget(lastData.GalaxyMapPlanet, GetMissionAt(itemIndex))
        );
    }

    private static GameObject GetRaycastTarget(PointerEventData eventData)
    {
        if (eventData == null)
            return null;

        return eventData.pointerCurrentRaycast.gameObject
            ?? eventData.pointerPressRaycast.gameObject;
    }

    private Mission GetSelectedMission()
    {
        return GetMissionAt(selectedIndex);
    }

    private Mission GetMissionAt(int index)
    {
        if (lastData?.SourceMissions == null || index < 0 || index >= lastData.SourceMissions.Count)
            return null;

        return lastData.SourceMissions[index];
    }

    private void RenderSelectedMission(MissionsWindowRenderData data)
    {
        targetTitleTextField.gameObject.SetActive(data.HasSelectedMission);
        targetImage.gameObject.SetActive(data.HasSelectedMission && data.TargetTexture != null);
        targetNameTextField.gameObject.SetActive(data.HasSelectedMission);
        tabsRoot.gameObject.SetActive(data.HasSelectedMission);
        participantsScrollArea.gameObject.SetActive(data.HasSelectedMission);

        if (!data.HasSelectedMission)
        {
            HideTabs();
            HideParticipants();
            return;
        }

        SetTemplateText(targetTitleTextField, "Target");
        UILayout.SetCenteredImage(targetImage, data.TargetTexture, targetImageSlotRect);
        SetTemplateText(targetNameTextField, data.TargetName);
        RenderTabs(data.ActiveTab);
        RenderParticipants(data.Participants);
    }

    private void RenderButtons()
    {
        for (int i = 0; i < buttonImages.Length; i++)
        {
            RawImage image = buttonImages[i];
            if (image == null)
                continue;

            int action = GetButtonAction(i);
            ConfigureWindowButton(image, action);
        }
    }

    private void ConfigureWindowButton(RawImage image, int action)
    {
        UILayout.SetInteractiveImageTexture(image, GetButtonTexture(action, false));
    }

    private void RenderMissionRows(IReadOnlyList<MissionListRowRenderData> rows)
    {
        IReadOnlyList<MissionListRowRenderData> safeRows =
            rows ?? System.Array.Empty<MissionListRowRenderData>();
        bool resetScroll = MissionsChanged(safeRows);
        missionListScrollArea.SetContentHeight(
            GetMissionListScrollContentHeight(safeRows.Count),
            GetMissionListScrollStep(),
            resetScroll
        );
        for (int i = 0; i < safeRows.Count; i++)
        {
            MissionListRowView row = GetMissionRowView(i);
            row.SetIndex(i);
            row.Render(safeRows[i]);
        }

        for (int i = safeRows.Count; i < missionRowViews.Count; i++)
            missionRowViews[i].gameObject.SetActive(false);

        renderedAnyMissions = true;
        renderedMissionNames.Clear();
        for (int i = 0; i < safeRows.Count; i++)
            renderedMissionNames.Add(safeRows[i].Name ?? string.Empty);
    }

    private void RenderTabs(int activeTab)
    {
        for (int i = 0; i < tabImages.Length; i++)
        {
            RawImage image = tabImages[i];
            image.gameObject.SetActive(true);
            UILayout.SetInteractiveImageTexture(image, GetTabTexture(i, i == activeTab));
        }
    }

    private void HideTabs()
    {
        for (int i = 0; i < tabImages.Length; i++)
            tabImages[i].gameObject.SetActive(false);
    }

    private void RenderParticipants(IReadOnlyList<MissionParticipantRowRenderData> rows)
    {
        IReadOnlyList<MissionParticipantRowRenderData> safeRows =
            rows ?? System.Array.Empty<MissionParticipantRowRenderData>();
        bool resetScroll = ParticipantsChanged(safeRows);
        participantsScrollArea.SetContentHeight(
            GetParticipantScrollContentHeight(safeRows.Count),
            GetParticipantScrollStep(),
            resetScroll
        );
        for (int i = 0; i < safeRows.Count; i++)
        {
            MissionParticipantRowView row = GetParticipantRowView(i);
            row.Render(safeRows[i]);
        }

        for (int i = safeRows.Count; i < participantRowViews.Count; i++)
            participantRowViews[i].gameObject.SetActive(false);

        renderedAnyParticipants = true;
        renderedParticipantActiveTab = activeTab;
        renderedParticipantSelectedIndex = selectedIndex;
        renderedParticipantNames.Clear();
        for (int i = 0; i < safeRows.Count; i++)
            renderedParticipantNames.Add(safeRows[i].Name ?? string.Empty);
    }

    private void HideParticipants()
    {
        for (int i = 0; i < participantRowViews.Count; i++)
            participantRowViews[i].gameObject.SetActive(false);
    }

    private int GetButtonAction(int index)
    {
        return index >= 0 && index < buttonActions.Length ? buttonActions[index] : 0;
    }

    private Texture2D GetTitleTexture(string ownerFactionId, bool active)
    {
        WindowTitleTheme theme = uiContext?.GetTheme(ownerFactionId)?.WindowTitleTheme;
        return uiContext?.GetTexture(active ? theme?.ActiveImagePath : theme?.InactiveImagePath);
    }

    private Texture2D GetButtonTexture(int action, bool pressed)
    {
        return action switch
        {
            StrategyWindowButtonActions.OpenSector => pressed
                ? openSectorButtonDownTexture
                : openSectorButtonUpTexture,
            StrategyWindowButtonActions.MinimizeWindow => pressed
                ? minimizeButtonDownTexture
                : minimizeButtonUpTexture,
            StrategyWindowButtonActions.CloseWindow => pressed
                ? closeButtonDownTexture
                : closeButtonUpTexture,
            _ => null,
        };
    }

    private Texture2D GetTabTexture(int tab, bool active)
    {
        MissionsWindowTheme theme = GetMissionsTheme();
        WindowTabImageTheme tabTheme = tab switch
        {
            0 => theme?.AgentsTab,
            1 => theme?.DecoysTab,
            _ => null,
        };

        return uiContext?.GetTexture(tabTheme?.GetImagePath(active ? 0 : 1));
    }

    private MissionsWindowTheme GetMissionsTheme()
    {
        return uiContext?.GetPlayerFactionTheme()?.StrategyWindows?.Missions;
    }

    private MissionListRowView GetMissionRowView(int index)
    {
        while (missionRowViews.Count <= index)
        {
            MissionListRowView row = Instantiate(
                missionListRowTemplate,
                missionListScrollArea.ContentRoot
            );
            row.name = $"MissionListRow{missionRowViews.Count}";
            row.Pressed += HandleMissionRowPressed;
            row.Released += HandleMissionRowReleased;
            row.Dropped += HandleMissionRowReleased;
            missionRowViews.Add(row);
        }

        return missionRowViews[index];
    }

    private void HandleMissionRowPressed(MissionListRowView row, PointerEventData eventData)
    {
        if (
            uiRuntime?.Targeting.IsTargeting == true
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        if (row != null)
            SelectMission(row.Index);
    }

    private void HandleMissionRowReleased(MissionListRowView row, PointerEventData eventData)
    {
        if (row != null)
            TrySelectTarget(row.Index);
    }

    private MissionParticipantRowView GetParticipantRowView(int index)
    {
        while (participantRowViews.Count <= index)
        {
            MissionParticipantRowView row = Instantiate(
                participantRowTemplate,
                participantsScrollArea.ContentRoot
            );
            row.name = $"MissionParticipantRow{participantRowViews.Count}";
            participantRowViews.Add(row);
        }

        return participantRowViews[index];
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (titleImage == null)
            throw new MissingReferenceException($"{name}/TitleImage is missing.");
        if (captionTextField == null)
            throw new MissingReferenceException($"{name}/CaptionTextField is missing.");
        if (buttonImages == null || buttonImages.Length == 0)
            throw new MissingReferenceException($"{name}/Button images are missing.");
        if (buttonActions == null || buttonActions.Length != buttonImages.Length)
            throw new MissingReferenceException($"{name}/Button actions are missing.");
        for (int i = 0; i < buttonImages.Length; i++)
        {
            if (buttonImages[i] == null)
                throw new MissingReferenceException($"{name}/ButtonImage{i} is missing.");
        }
        if (missionListScrollArea == null)
            throw new MissingReferenceException($"{name}/MissionListScrollArea is missing.");
        if (missionListRowTemplate == null)
            throw new MissingReferenceException($"{name}/MissionListRowTemplate is missing.");
        if (missionListContentPaddingTemplate == null)
            throw new MissingReferenceException(
                $"{name}/MissionListContentPaddingTemplate is missing."
            );
        if (targetTitleTextField == null)
            throw new MissingReferenceException($"{name}/TargetTitleTextField is missing.");
        if (targetImage == null)
            throw new MissingReferenceException($"{name}/TargetImage is missing.");
        if (targetNameTextField == null)
            throw new MissingReferenceException($"{name}/TargetNameTextField is missing.");
        if (tabsRoot == null)
            throw new MissingReferenceException($"{name}/Tabs is missing.");
        if (tabImages == null || tabImages.Length != 2)
            throw new MissingReferenceException($"{name}/TabImages are missing.");
        if (tabButtons == null || tabButtons.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/TabButtons are missing.");
        for (int i = 0; i < tabImages.Length; i++)
        {
            if (tabImages[i] == null)
                throw new MissingReferenceException($"{name}/TabImage{i} is missing.");
            if (tabButtons[i] == null)
                throw new MissingReferenceException($"{name}/TabButton{i} is missing.");
        }
        if (participantsScrollArea == null)
            throw new MissingReferenceException($"{name}/ParticipantsScrollArea is missing.");
        if (participantRowTemplate == null)
            throw new MissingReferenceException($"{name}/ParticipantRowTemplate is missing.");
        if (backgroundTexture == null)
            throw new MissingReferenceException($"{name}/BackgroundTexture is missing.");
        if (openSectorButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/OpenSectorButtonUpTexture is missing.");
        if (openSectorButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/OpenSectorButtonDownTexture is missing.");
        if (minimizeButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/MinimizeButtonUpTexture is missing.");
        if (minimizeButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/MinimizeButtonDownTexture is missing.");
        if (closeButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonUpTexture is missing.");
        if (closeButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonDownTexture is missing.");
        if (participantEnrouteBackgroundTexture == null)
            throw new MissingReferenceException(
                $"{name}/ParticipantEnrouteBackgroundTexture is missing."
            );
        missionListRowTemplate.gameObject.SetActive(false);
        missionListContentPaddingTemplate.gameObject.SetActive(false);
        participantRowTemplate.gameObject.SetActive(false);
        InitializeTemplateRects();
    }

    private bool MissionsChanged(IReadOnlyList<MissionListRowRenderData> rows)
    {
        if (!renderedAnyMissions || renderedMissionNames.Count != rows.Count)
            return true;

        for (int i = 0; i < rows.Count; i++)
        {
            if (renderedMissionNames[i] != (rows[i].Name ?? string.Empty))
                return true;
        }

        return false;
    }

    private bool ParticipantsChanged(IReadOnlyList<MissionParticipantRowRenderData> rows)
    {
        if (
            !renderedAnyParticipants
            || renderedParticipantActiveTab != activeTab
            || renderedParticipantSelectedIndex != selectedIndex
            || renderedParticipantNames.Count != rows.Count
        )
            return true;

        for (int i = 0; i < rows.Count; i++)
        {
            if (renderedParticipantNames[i] != (rows[i].Name ?? string.Empty))
                return true;
        }

        return false;
    }

    private void InitializeTemplateRects()
    {
        if (hasTargetImageSlotRect)
            return;

        targetImageSlotRect = UILayout.GetSourceRect(targetImage.rectTransform);
        hasTargetImageSlotRect = true;
    }

    private int GetMissionListRowHeight()
    {
        return UILayout.GetSourceRect(missionListRowTemplate.transform as RectTransform).height;
    }

    private int GetParticipantRowHeight()
    {
        return UILayout.GetSourceRect(participantRowTemplate.transform as RectTransform).height;
    }

    private static void SetImage(RawImage image, Texture texture, int x, int y)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
        if (texture != null)
            SetSourceRect(image.rectTransform, x, y, texture.width, texture.height);
    }

    private static void SetImage(
        RawImage image,
        Texture texture,
        int x,
        int y,
        int width,
        int height
    )
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
        if (texture != null)
            SetSourceRect(image.rectTransform, x, y, width, height);
    }

    private Texture2D GetParticipantBackgroundTexture(ISceneNode participant)
    {
        if (participant is not IMovable { Movement: not null })
            return null;

        return uiContext?.GetEntityStatusTexture(participant, true)
            ?? participantEnrouteBackgroundTexture;
    }

    private static void SetImageFromTemplate(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    private static void SetTemplateText(TextMeshProUGUI textField, string text)
    {
        UILayout.SetTextContent(textField, text, textField.color);
    }

    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }
}

public sealed class MissionsWindowRenderData
{
    public int X;
    public int Y;
    internal GalaxyMapPlanet GalaxyMapPlanet;
    public string OwnerFactionId;
    public int ActiveTab;
    public bool Active;
    public bool HasSelectedMission;
    public string Caption;
    public string TargetName;
    public Texture2D TargetTexture;
    public ISceneNode Target;
    public int SelectedIndex;
    public IReadOnlyList<Mission> SourceMissions;
    public IReadOnlyList<IMissionParticipant> SourceParticipants;
    public List<MissionListRowRenderData> Missions = new List<MissionListRowRenderData>();
    public List<MissionParticipantRowRenderData> Participants =
        new List<MissionParticipantRowRenderData>();
}
