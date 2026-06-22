using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MissionCreateWindowView
    : MonoBehaviour,
        IStrategyUIContextReceiver,
        IStrategyWindowContent,
        IPointerClickHandler
{
    private readonly List<RectTransform> dropdownItemRows = new List<RectTransform>();
    private readonly List<RawImage> dropdownItemImages = new List<RawImage>();
    private readonly List<TextMeshProUGUI> dropdownItemTextFields = new List<TextMeshProUGUI>();
    private readonly List<MissionParticipantRowView> agentRowViews =
        new List<MissionParticipantRowView>();
    private readonly List<MissionParticipantRowView> decoyRowViews =
        new List<MissionParticipantRowView>();
    private readonly List<string> renderedDropdownItemNames = new List<string>();
    private readonly List<string> renderedAgentNames = new List<string>();
    private readonly List<string> renderedDecoyNames = new List<string>();

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private TextMeshProUGUI titleTextField;

    [SerializeField]
    private RawImage[] titleImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private RawImage[] buttonImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private int[] buttonActions = System.Array.Empty<int>();

    [SerializeField]
    private RectTransform tabsRoot;

    [SerializeField]
    private RawImage[] tabImages;

    [SerializeField]
    private Button[] tabButtons = System.Array.Empty<Button>();

    [SerializeField]
    private Texture2D[] tabActiveTextures = System.Array.Empty<Texture2D>();

    [SerializeField]
    private Texture2D[] tabInactiveTextures = System.Array.Empty<Texture2D>();

    [SerializeField]
    private RawImage infoButtonImage;

    [SerializeField]
    private RawImage okButtonImage;

    [SerializeField]
    private RawImage cancelButtonImage;

    [SerializeField]
    private Button infoButton;

    [SerializeField]
    private Button okButton;

    [SerializeField]
    private Button cancelButton;

    [SerializeField]
    private RectTransform missionSelectionRoot;

    [SerializeField]
    private RawImage dropdownButtonImage;

    [SerializeField]
    private Button dropdownButton;

    [SerializeField]
    private TextMeshProUGUI targetLabelTextField;

    [SerializeField]
    private RawImage selectedMissionImage;

    [SerializeField]
    private TextMeshProUGUI selectedMissionNameTextField;

    [SerializeField]
    private RawImage targetPreviewImage;

    [SerializeField]
    private TextMeshProUGUI targetPreviewNameTextField;

    [SerializeField]
    private RectTransform dropdownRoot;

    [SerializeField]
    private Image dropdownFrameFillImage;

    [SerializeField]
    private Image dropdownFrameTopImage;

    [SerializeField]
    private Image dropdownFrameBottomImage;

    [SerializeField]
    private Image dropdownFrameLeftImage;

    [SerializeField]
    private Image dropdownFrameRightImage;

    [SerializeField]
    private RawImage[] dropdownBackgroundImages;

    [SerializeField]
    private ScrollAreaView dropdownScrollArea;

    [SerializeField]
    private RawImage dropdownItemImageTemplate;

    [SerializeField]
    private TextMeshProUGUI dropdownItemTextTemplate;

    [SerializeField]
    private RectTransform dropdownItemRowTemplate;

    [SerializeField]
    private RectTransform dropdownItemImageAreaTemplate;

    [SerializeField]
    private RectTransform dropdownContentPaddingTemplate;

    [SerializeField]
    private RectTransform personnelRoot;

    [SerializeField]
    private RawImage agentsHeaderImage;

    [SerializeField]
    private RawImage decoysHeaderImage;

    [SerializeField]
    private RawImage moveRightButtonImage;

    [SerializeField]
    private RawImage moveLeftButtonImage;

    [SerializeField]
    private Button moveRightButton;

    [SerializeField]
    private Button moveLeftButton;

    [SerializeField]
    private ScrollAreaView agentsScrollArea;

    [SerializeField]
    private MissionParticipantRowView agentRowTemplate;

    [SerializeField]
    private ScrollAreaView decoysScrollArea;

    [SerializeField]
    private MissionParticipantRowView decoyRowTemplate;

    [SerializeField]
    private Texture2D missionBackgroundTexture;

    [SerializeField]
    private Texture2D personnelBackgroundTexture;

    [SerializeField]
    private Texture2D titleTexture;

    [SerializeField]
    private Texture2D closeButtonUpTexture;

    [SerializeField]
    private Texture2D infoButtonUpTexture;

    [SerializeField]
    private Texture2D infoButtonDownTexture;

    [SerializeField]
    private Texture2D okButtonUpTexture;

    [SerializeField]
    private Texture2D okButtonDownTexture;

    [SerializeField]
    private Texture2D cancelButtonUpTexture;

    [SerializeField]
    private Texture2D cancelButtonDownTexture;

    [SerializeField]
    private Texture2D dropdownButtonUpTexture;

    [SerializeField]
    private Texture2D dropdownButtonDownTexture;

    [SerializeField]
    private Texture2D dropdownBackgroundTexture;

    [SerializeField]
    private Texture2D moveRightButtonUpTexture;

    [SerializeField]
    private Texture2D moveRightButtonDownTexture;

    [SerializeField]
    private Texture2D moveLeftButtonUpTexture;

    [SerializeField]
    private Texture2D moveLeftButtonDownTexture;

    [SerializeField]
    private Texture2D participantEnrouteBackgroundTexture;

    private readonly List<StrategyMissionChoice> missionChoices = new List<StrategyMissionChoice>();
    private readonly List<IMissionParticipant> sourceAgents = new List<IMissionParticipant>();
    private readonly List<IMissionParticipant> sourceDecoys = new List<IMissionParticipant>();
    private readonly HashSet<int> selectedAgents = new HashSet<int>();
    private readonly HashSet<int> selectedDecoys = new HashSet<int>();
    private bool stateInitialized;
    private int activeTab;
    private int selectedMissionIndex = -1;
    private bool dropdownOpen;
    private MissionCreateWindowRenderData lastData;
    private RectInt targetPreviewSlotRect;
    private bool hasTargetPreviewSlotRect;
    private UIContext uiContext;
    private bool renderedAnyDropdownItems;
    private bool renderedAnyAgents;
    private bool renderedAnyDecoys;
    private Texture2D planetTargetPreviewTexture;
    private UIWindow windowShell;
    public UIWindow SourceWindow { get; private set; }
    public StrategyMissionTarget MissionTarget { get; private set; }

    public void InitializeWindow(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IEnumerable<StrategyMissionChoice> choices,
        IEnumerable<IMissionParticipant> agents
    )
    {
        SourceWindow = sourceWindow;
        MissionTarget = target;
        InitializeState(choices, agents, null);
    }

    public void Initialize(UIContext uiContext)
    {
        this.uiContext = uiContext;
    }

    internal void InitializeState(
        IEnumerable<StrategyMissionChoice> choices,
        IEnumerable<IMissionParticipant> agents,
        IEnumerable<IMissionParticipant> decoys
    )
    {
        if (stateInitialized)
            return;

        missionChoices.Clear();
        sourceAgents.Clear();
        sourceDecoys.Clear();

        if (choices != null)
            missionChoices.AddRange(choices);
        if (agents != null)
            sourceAgents.AddRange(agents);
        if (decoys != null)
            sourceDecoys.AddRange(decoys);

        selectedMissionIndex = missionChoices.Count > 0 ? 0 : -1;
        activeTab = 0;
        dropdownOpen = false;
        selectedAgents.Clear();
        selectedDecoys.Clear();
        stateInitialized = true;
    }

    internal int GetActiveTab()
    {
        return activeTab;
    }

    public void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active)
    {
        if (window == null)
            return;

        Render(
            new MissionCreateWindowRenderData
            {
                X = window.X,
                Y = window.Y,
                Target = MissionTarget?.Item ?? MissionTarget?.Planet?.Planet,
            }
        );
    }

    internal bool IsDropdownOpen()
    {
        return dropdownOpen;
    }

    internal int GetMissionChoiceCount()
    {
        return missionChoices.Count;
    }

    internal int GetAgentCount()
    {
        return sourceAgents.Count;
    }

    internal int GetDecoyCount()
    {
        return sourceDecoys.Count;
    }

    internal static List<MissionCreateWindowChoiceSource> CreateChoiceSources(
        IEnumerable<StrategyMissionChoice> choices
    )
    {
        return choices
            .Select(choice => new MissionCreateWindowChoiceSource(choice.Name, choice.IconKey))
            .ToList();
    }

    internal static void MoveSelectedParticipants(
        List<IMissionParticipant> source,
        List<IMissionParticipant> destination,
        HashSet<int> selection
    )
    {
        if (source.Count == 0 || selection.Count == 0)
            return;

        List<IMissionParticipant> remaining = new List<IMissionParticipant>();
        for (int i = 0; i < source.Count; i++)
        {
            if (selection.Contains(i))
                destination.Add(source[i]);
            else
                remaining.Add(source[i]);
        }

        source.Clear();
        source.AddRange(remaining);
        selection.Clear();
    }

    public void Render(MissionCreateWindowRenderData data)
    {
        VerifyReferences();
        data = CreateRenderData(data);
        lastData = data;

        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        SetImageFromTemplate(backgroundImage, GetBackgroundTexture(data.ActiveTab));
        RenderTitleImages();
        SetTemplateText(
            titleTextField,
            "Create Mission",
            data.ActiveTab == 0 ? Color.black : Color.white
        );
        RenderButtons();
        RenderTabs(data.ActiveTab);
        ConfigureLocalButton(infoButtonImage, infoButtonUpTexture);
        ConfigureLocalButton(okButtonImage, okButtonUpTexture);
        ConfigureLocalButton(cancelButtonImage, cancelButtonUpTexture);

        if (data.ActiveTab == 0)
        {
            RenderMissionSelectionPane(data);
            HidePersonnelPane();
        }
        else
        {
            HideMissionSelectionPane();
            RenderPersonnelPane(data);
        }

        gameObject.SetActive(true);
    }

    internal bool NeedsParticipantScrollbar(int participantCount)
    {
        return participantCount * GetParticipantRowHeight() > agentsScrollArea.ViewportHeight;
    }

    internal int GetParticipantScrollContentHeight(int participantCount)
    {
        return UILayout.GetSourceRect(agentRowTemplate.transform as RectTransform).y
            + participantCount * GetParticipantRowHeight();
    }

    internal int GetParticipantScrollViewportHeight(bool scrollbarVisible)
    {
        return Mathf.RoundToInt(agentsScrollArea.ViewportHeight);
    }

    internal int GetParticipantScrollStep()
    {
        return GetParticipantRowHeight();
    }

    internal bool NeedsDropdownScrollbar(int itemCount)
    {
        return GetDropdownScrollContentHeight(itemCount) > GetDropdownScrollViewportHeight();
    }

    internal int GetDropdownScrollContentHeight(int itemCount)
    {
        return UILayout.GetSourceRect(dropdownContentPaddingTemplate).height
            + itemCount * GetDropdownItemHeight();
    }

    internal int GetDropdownScrollViewportHeight()
    {
        return Mathf.RoundToInt(dropdownScrollArea.ViewportHeight);
    }

    internal int GetDropdownScrollStep()
    {
        return GetDropdownItemHeight();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !dropdownOpen)
            return;
        if (IsDropdownInteraction(eventData))
            return;

        RequestFocus();
        dropdownOpen = false;
        RequestRender();
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
            int tabIndex = i;
            tabButtons[i].onClick.AddListener(() => SelectTab(tabIndex));
        }

        dropdownButton.onClick.AddListener(ToggleDropdown);
        moveRightButton.onClick.AddListener(MoveSelectedAgents);
        moveLeftButton.onClick.AddListener(MoveSelectedDecoys);
        infoButton.onClick.AddListener(OpenInfo);
        okButton.onClick.AddListener(ConfirmMission);
        cancelButton.onClick.AddListener(Cancel);
    }

    private void SelectTab(int tab)
    {
        if (tab < 0 || tab >= tabButtons.Length)
            return;

        RequestFocus();
        activeTab = tab;
        dropdownOpen = false;
        RequestRender();
    }

    private void ToggleDropdown()
    {
        RequestFocus();
        dropdownOpen = !dropdownOpen;
        RequestRender();
    }

    private void SelectDropdownItem(int index)
    {
        if (lastData?.DropdownItems == null || index < 0 || index >= lastData.DropdownItems.Count)
            return;

        RequestFocus();
        selectedMissionIndex = index;
        dropdownOpen = false;
        RequestRender();
    }

    private void MoveSelectedAgents()
    {
        RequestFocus();
        MoveSelectedParticipants(sourceAgents, sourceDecoys, selectedAgents);
        RequestRender();
    }

    private void MoveSelectedDecoys()
    {
        RequestFocus();
        MoveSelectedParticipants(sourceDecoys, sourceAgents, selectedDecoys);
        RequestRender();
    }

    private void OpenInfo()
    {
        if (uiContext == null)
            return;

        RequestFocus();
        uiContext.Dispatcher.Send(
            new StrategyUIRequests.ExecuteMissionCreateCommand(
                GetWindowId(),
                MissionCreateWindowCommand.Info
            )
        );
    }

    private void ConfirmMission()
    {
        if (uiContext == null)
            return;

        RequestFocus();
        uiContext.Dispatcher.Send(
            new StrategyUIRequests.ExecuteMissionCreateCommand(
                GetWindowId(),
                MissionCreateWindowCommand.Ok,
                GetSelectedMissionChoice(),
                sourceAgents.ToList(),
                sourceDecoys.ToList()
            )
        );
    }

    private void Cancel()
    {
        if (uiContext == null)
            return;

        RequestFocus();
        uiContext.Dispatcher.Send(
            new StrategyUIRequests.ExecuteMissionCreateCommand(
                GetWindowId(),
                MissionCreateWindowCommand.Cancel
            )
        );
    }

    private void HandleParticipantRowClicked(
        MissionParticipantRowView row,
        PointerEventData eventData
    )
    {
        if (row == null || uiContext == null)
            return;

        RequestFocus();
        if (row.ListId == 1)
            SelectAgentRow(row.Index, eventData.clickCount);
        else if (row.ListId == 2)
            SelectDecoyRow(row.Index, eventData.clickCount);
    }

    private void SelectAgentRow(int index, int clickCount)
    {
        if (index < 0 || index >= sourceAgents.Count)
            return;

        if (clickCount > 1)
        {
            selectedAgents.Clear();
            selectedAgents.Add(index);
            MoveSelectedParticipants(sourceAgents, sourceDecoys, selectedAgents);
        }
        else
        {
            SelectParticipant(selectedAgents, index, sourceAgents.Count);
        }

        RequestRender();
    }

    private void SelectDecoyRow(int index, int clickCount)
    {
        if (index < 0 || index >= sourceDecoys.Count)
            return;

        if (clickCount > 1)
        {
            selectedDecoys.Clear();
            selectedDecoys.Add(index);
            MoveSelectedParticipants(sourceDecoys, sourceAgents, selectedDecoys);
        }
        else
        {
            SelectParticipant(selectedDecoys, index, sourceDecoys.Count);
        }

        RequestRender();
    }

    private int GetWindowId()
    {
        if (windowShell == null)
            windowShell = GetComponent<UIWindow>();

        return windowShell == null ? 0 : windowShell.Id;
    }

    private void RequestFocus()
    {
        if (windowShell == null)
            windowShell = GetComponent<UIWindow>();

        windowShell?.RequestFocus();
    }

    private void RequestRender()
    {
        uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private MissionCreateWindowRenderData CreateRenderData(MissionCreateWindowRenderData source)
    {
        MissionCreateWindowRenderData data = new MissionCreateWindowRenderData
        {
            X = source?.X ?? 0,
            Y = source?.Y ?? 0,
            ActiveTab = activeTab,
            DropdownOpen = dropdownOpen,
            SelectedMissionIndex = selectedMissionIndex,
            Target = source?.Target,
            MissionChoices = CreateChoiceSources(missionChoices),
            SourceAgents = sourceAgents,
            SourceDecoys = sourceDecoys,
            SelectedAgents = selectedAgents,
            SelectedDecoys = selectedDecoys,
        };

        if (data.ActiveTab == 0)
            PopulateMissionSelectionRenderData(data);
        else
            PopulatePersonnelRenderData(data);

        return data;
    }

    private void PopulateMissionSelectionRenderData(MissionCreateWindowRenderData data)
    {
        if (data.SelectedMissionIndex >= 0 && data.SelectedMissionIndex < data.MissionChoices.Count)
        {
            MissionCreateWindowChoiceSource choice = data.MissionChoices[data.SelectedMissionIndex];
            data.MissionName = choice.Name;
            data.SelectedMissionTexture = GetMissionChoiceTexture(choice, false);
        }

        if (data.Target != null)
            data.TargetName = data.Target.GetDisplayName();

        if (!data.DropdownOpen)
            return;

        for (int i = 0; i < data.MissionChoices.Count; i++)
        {
            MissionCreateWindowChoiceSource choice = data.MissionChoices[i];
            data.DropdownItems.Add(
                new MissionDropdownItemRenderData
                {
                    Name = choice.Name,
                    Color = i == data.SelectedMissionIndex ? Color.white : Color.gray,
                    Texture = GetMissionChoiceTexture(choice, false),
                }
            );
        }
    }

    private void PopulatePersonnelRenderData(MissionCreateWindowRenderData data)
    {
        PopulateParticipantRows(data.SourceAgents, data.SelectedAgents, data.AgentRows);
        PopulateParticipantRows(data.SourceDecoys, data.SelectedDecoys, data.DecoyRows);
    }

    private void PopulateParticipantRows(
        IReadOnlyList<IMissionParticipant> participants,
        IReadOnlyCollection<int> selected,
        List<MissionParticipantRowRenderData> rows
    )
    {
        for (int i = 0; i < participants.Count; i++)
        {
            ISceneNode node = participants[i] as ISceneNode;
            if (node == null)
                continue;

            rows.Add(
                new MissionParticipantRowRenderData
                {
                    Name = node.GetDisplayName(),
                    NameColor = ContainsIndex(selected, i) ? Color.white : Color.gray,
                    BackgroundTexture = GetParticipantBackgroundTexture(node),
                    EntityTexture = uiContext?.GetEntityTexture(node, true),
                }
            );
        }
    }

    private void RenderMissionSelectionPane(MissionCreateWindowRenderData data)
    {
        missionSelectionRoot.gameObject.SetActive(true);
        UILayout.SetInteractiveImageTexture(
            dropdownButtonImage,
            data.DropdownOpen ? dropdownButtonDownTexture : dropdownButtonUpTexture
        );
        SetTemplateText(targetLabelTextField, "Target");

        selectedMissionImage.gameObject.SetActive(data.SelectedMissionTexture != null);
        if (data.SelectedMissionTexture != null)
            SetImageAtTemplateOrigin(selectedMissionImage, data.SelectedMissionTexture);

        selectedMissionNameTextField.gameObject.SetActive(!string.IsNullOrEmpty(data.MissionName));
        if (!string.IsNullOrEmpty(data.MissionName))
            SetTemplateText(selectedMissionNameTextField, data.MissionName);

        Texture2D targetTexture = GetTargetTexture(data.Target);
        targetPreviewImage.gameObject.SetActive(targetTexture != null);
        if (targetTexture != null)
            UILayout.SetCenteredImage(targetPreviewImage, targetTexture, targetPreviewSlotRect);

        targetPreviewNameTextField.gameObject.SetActive(!string.IsNullOrEmpty(data.TargetName));
        if (!string.IsNullOrEmpty(data.TargetName))
            SetTemplateText(targetPreviewNameTextField, data.TargetName);

        RenderDropdown(data);
    }

    private Texture2D GetTargetTexture(ISceneNode target)
    {
        return target is Planet
            ? planetTargetPreviewTexture
            : uiContext?.GetEntityTexture(target, true);
    }

    private void HideMissionSelectionPane()
    {
        missionSelectionRoot.gameObject.SetActive(false);
        dropdownRoot.gameObject.SetActive(false);
        HideDropdownItems();
    }

    private void RenderDropdown(MissionCreateWindowRenderData data)
    {
        dropdownRoot.gameObject.SetActive(data.DropdownOpen);
        if (!data.DropdownOpen)
        {
            HideDropdownItems();
            return;
        }

        for (int i = 0; i < dropdownBackgroundImages.Length; i++)
            SetImageFromTemplate(dropdownBackgroundImages[i], dropdownBackgroundTexture);

        dropdownScrollArea.gameObject.SetActive(true);
        IReadOnlyList<MissionDropdownItemRenderData> items =
            data.DropdownItems != null
                ? data.DropdownItems
                : System.Array.Empty<MissionDropdownItemRenderData>();
        bool resetScroll = DropdownItemsChanged(items);
        dropdownScrollArea.SetContentHeight(
            GetDropdownScrollContentHeight(items.Count),
            GetDropdownScrollStep(),
            resetScroll
        );
        for (int i = 0; i < items.Count; i++)
        {
            MissionDropdownItemRenderData item = items[i];
            RectTransform rowRoot = GetDropdownItemRow(i);
            rowRoot.gameObject.SetActive(true);

            RectInt imageArea = UILayout.GetSourceRect(dropdownItemImageAreaTemplate);
            RectInt textRect = UILayout.GetSourceRect(dropdownItemTextTemplate.rectTransform);
            RawImage image = dropdownItemImages[i];
            TextMeshProUGUI textField = dropdownItemTextFields[i];
            UILayout.SetHorizontallyCenteredImage(
                image,
                item.Texture,
                new RectInt(imageArea.x, imageArea.y, imageArea.width, imageArea.height)
            );
            UILayout.SetTemplateText(
                textField,
                dropdownItemTextTemplate,
                item.Name,
                item.Color,
                textRect
            );
        }

        for (int i = items.Count; i < dropdownItemRows.Count; i++)
            dropdownItemRows[i].gameObject.SetActive(false);

        renderedAnyDropdownItems = true;
        renderedDropdownItemNames.Clear();
        for (int i = 0; i < items.Count; i++)
            renderedDropdownItemNames.Add(items[i].Name ?? string.Empty);
    }

    private void HideDropdownItems()
    {
        for (int i = 0; i < dropdownItemRows.Count; i++)
            dropdownItemRows[i].gameObject.SetActive(false);
    }

    private void RenderPersonnelPane(MissionCreateWindowRenderData data)
    {
        personnelRoot.gameObject.SetActive(true);
        SetImageAtTemplateOrigin(agentsHeaderImage, GetAgentsHeaderTexture());
        SetImageAtTemplateOrigin(decoysHeaderImage, GetDecoysHeaderTexture());
        ConfigureLocalButton(moveRightButtonImage, moveRightButtonUpTexture);
        ConfigureLocalButton(moveLeftButtonImage, moveLeftButtonUpTexture);
        RenderParticipantRows(data.AgentRows, agentRowViews, agentRowTemplate, agentsScrollArea, 1);
        RenderParticipantRows(data.DecoyRows, decoyRowViews, decoyRowTemplate, decoysScrollArea, 2);
    }

    private void HidePersonnelPane()
    {
        personnelRoot.gameObject.SetActive(false);
        HideRows(agentRowViews);
        HideRows(decoyRowViews);
    }

    private void RenderButtons()
    {
        for (int i = 0; i < buttonImages.Length; i++)
        {
            RawImage image = buttonImages[i];
            if (image == null)
                continue;

            int action = GetButtonAction(i);
            ConfigureWindowButton(image, GetWindowButtonTexture(action));
        }
    }

    private static void ConfigureWindowButton(RawImage image, Texture upTexture)
    {
        UILayout.SetInteractiveImageTexture(image, upTexture);
    }

    private static void ConfigureLocalButton(RawImage image, Texture upTexture)
    {
        UILayout.SetInteractiveImageTexture(image, upTexture);
    }

    private void RenderTabs(int activeTab)
    {
        for (int i = 0; i < tabImages.Length; i++)
        {
            tabImages[i].gameObject.SetActive(true);
            bool active = activeTab == i;
            UILayout.SetInteractiveImageTexture(tabImages[i], GetTabTexture(i, active));
        }
    }

    private void RenderTitleImages()
    {
        Texture2D texture = GetTitleTexture();
        for (int i = 0; i < titleImages.Length; i++)
        {
            if (titleImages[i] != null)
                SetImageFromTemplate(titleImages[i], texture);
        }
    }

    private void RenderParticipantRows(
        IReadOnlyList<MissionParticipantRowRenderData> rows,
        List<MissionParticipantRowView> views,
        MissionParticipantRowView template,
        ScrollAreaView scrollArea,
        int listId
    )
    {
        IReadOnlyList<MissionParticipantRowRenderData> safeRows =
            rows ?? System.Array.Empty<MissionParticipantRowRenderData>();
        bool resetScroll = ParticipantRowsChanged(safeRows, listId);
        scrollArea.SetContentHeight(
            GetParticipantScrollContentHeight(safeRows.Count),
            GetParticipantScrollStep(),
            resetScroll
        );
        for (int i = 0; i < safeRows.Count; i++)
        {
            MissionParticipantRowView row = GetParticipantRowView(
                i,
                views,
                template,
                scrollArea.ContentRoot,
                listId
            );
            row.SetPosition(listId, i);
            row.Render(safeRows[i]);
        }

        for (int i = safeRows.Count; i < views.Count; i++)
            views[i].gameObject.SetActive(false);

        List<string> renderedNames = listId == 1 ? renderedAgentNames : renderedDecoyNames;
        renderedNames.Clear();
        for (int i = 0; i < safeRows.Count; i++)
            renderedNames.Add(safeRows[i].Name ?? string.Empty);

        if (listId == 1)
            renderedAnyAgents = true;
        else
            renderedAnyDecoys = true;
    }

    private static void HideRows(List<MissionParticipantRowView> rows)
    {
        for (int i = 0; i < rows.Count; i++)
            rows[i].gameObject.SetActive(false);
    }

    private int GetButtonAction(int index)
    {
        return index >= 0 && index < buttonActions.Length ? buttonActions[index] : 0;
    }

    private Texture2D GetBackgroundTexture(int activeTab)
    {
        return activeTab == 0 ? missionBackgroundTexture : personnelBackgroundTexture;
    }

    private Texture2D GetWindowButtonTexture(int action)
    {
        if (action != StrategyWindowButtonActions.CloseWindow)
            return null;

        return closeButtonUpTexture;
    }

    private Texture2D GetTabTexture(int tab, bool active)
    {
        MissionCreateWindowTheme theme = GetMissionCreateTheme();
        WindowTabImageTheme tabTheme = tab switch
        {
            0 => theme?.MissionTab,
            1 => theme?.PersonnelTab,
            _ => null,
        };

        Texture2D texture = uiContext?.GetTexture(tabTheme?.GetImagePath(active ? 0 : 1));
        return texture != null ? texture : GetFallbackTabTexture(tab, active);
    }

    private Texture2D GetFallbackTabTexture(int tab, bool active)
    {
        Texture2D[] textures = active ? tabActiveTextures : tabInactiveTextures;
        return textures != null && tab >= 0 && tab < textures.Length ? textures[tab] : null;
    }

    private Texture2D GetTitleTexture()
    {
        Texture2D texture = uiContext?.GetTexture(GetMissionCreateTheme()?.TitleImagePath);
        return texture != null ? texture : titleTexture;
    }

    private Texture2D GetAgentsHeaderTexture()
    {
        return uiContext?.GetTexture(GetMissionCreateTheme()?.AgentsHeaderImagePath);
    }

    private Texture2D GetDecoysHeaderTexture()
    {
        return uiContext?.GetTexture(GetMissionCreateTheme()?.DecoysHeaderImagePath);
    }

    private MissionCreateWindowTheme GetMissionCreateTheme()
    {
        return uiContext?.GetPlayerFactionTheme()?.StrategyWindows?.MissionCreate;
    }

    private RectTransform GetDropdownItemRow(int index)
    {
        while (dropdownItemRows.Count <= index)
        {
            RectTransform row = Instantiate(
                dropdownItemRowTemplate,
                dropdownScrollArea.ContentRoot
            );
            row.name = $"DropdownItemRow{dropdownItemRows.Count}";
            row.gameObject.SetActive(true);
            Button button = row.GetComponent<Button>();
            if (button == null)
                throw new MissingReferenceException($"{row.name}/Button is missing.");

            int itemIndex = dropdownItemRows.Count;
            button.onClick.AddListener(() => SelectDropdownItem(itemIndex));
            RawImage image = Instantiate(dropdownItemImageTemplate, row);
            image.name = $"DropdownItemImage{dropdownItemImages.Count}";
            TextMeshProUGUI textField = Instantiate(dropdownItemTextTemplate, row);
            textField.name = $"DropdownItemTextField{dropdownItemTextFields.Count}";
            dropdownItemRows.Add(row);
            dropdownItemImages.Add(image);
            dropdownItemTextFields.Add(textField);
        }

        return dropdownItemRows[index];
    }

    private MissionParticipantRowView GetParticipantRowView(
        int index,
        List<MissionParticipantRowView> views,
        MissionParticipantRowView template,
        RectTransform parent,
        int listId
    )
    {
        while (views.Count <= index)
        {
            MissionParticipantRowView row = Instantiate(template, parent);
            row.name = $"MissionParticipantRow{views.Count}";
            row.SetPosition(listId, views.Count);
            row.Clicked += HandleParticipantRowClicked;
            views.Add(row);
        }

        return views[index];
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (titleTextField == null)
            throw new MissingReferenceException($"{name}/TitleTextField is missing.");
        if (titleImages == null || titleImages.Length != 2)
            throw new MissingReferenceException($"{name}/TitleImages is missing.");
        for (int i = 0; i < titleImages.Length; i++)
        {
            if (titleImages[i] == null)
                throw new MissingReferenceException($"{name}/TitleImage{i} is missing.");
        }
        if (buttonImages == null || buttonImages.Length == 0)
            throw new MissingReferenceException($"{name}/Button images are missing.");
        if (buttonActions == null || buttonActions.Length != buttonImages.Length)
            throw new MissingReferenceException($"{name}/Button actions are missing.");
        for (int i = 0; i < buttonImages.Length; i++)
        {
            if (buttonImages[i] == null)
                throw new MissingReferenceException($"{name}/ButtonImage{i} is missing.");
        }
        if (tabsRoot == null)
            throw new MissingReferenceException($"{name}/Tabs is missing.");
        if (tabImages == null || tabImages.Length != 2)
            throw new MissingReferenceException($"{name}/TabImages is missing.");
        if (tabButtons == null || tabButtons.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/TabButtons are missing.");
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null)
                throw new MissingReferenceException($"{name}/TabButton{i} is missing.");
        }
        if (tabActiveTextures == null || tabActiveTextures.Length != 2)
            throw new MissingReferenceException($"{name}/TabActiveTextures are missing.");
        if (tabInactiveTextures == null || tabInactiveTextures.Length != 2)
            throw new MissingReferenceException($"{name}/TabInactiveTextures are missing.");
        if (infoButtonImage == null)
            throw new MissingReferenceException($"{name}/InfoButtonImage is missing.");
        if (okButtonImage == null)
            throw new MissingReferenceException($"{name}/OkButtonImage is missing.");
        if (cancelButtonImage == null)
            throw new MissingReferenceException($"{name}/CancelButtonImage is missing.");
        if (infoButton == null)
            throw new MissingReferenceException($"{name}/InfoButton is missing.");
        if (okButton == null)
            throw new MissingReferenceException($"{name}/OkButton is missing.");
        if (cancelButton == null)
            throw new MissingReferenceException($"{name}/CancelButton is missing.");
        if (missionSelectionRoot == null)
            throw new MissingReferenceException($"{name}/MissionSelection is missing.");
        if (dropdownButtonImage == null)
            throw new MissingReferenceException($"{name}/DropdownButtonImage is missing.");
        if (dropdownButton == null)
            throw new MissingReferenceException($"{name}/DropdownButton is missing.");
        if (targetLabelTextField == null)
            throw new MissingReferenceException($"{name}/TargetLabelTextField is missing.");
        if (selectedMissionImage == null)
            throw new MissingReferenceException($"{name}/SelectedMissionImage is missing.");
        if (selectedMissionNameTextField == null)
            throw new MissingReferenceException($"{name}/SelectedMissionNameTextField is missing.");
        if (targetPreviewImage == null)
            throw new MissingReferenceException($"{name}/TargetPreviewImage is missing.");
        if (targetPreviewNameTextField == null)
            throw new MissingReferenceException($"{name}/TargetPreviewNameTextField is missing.");
        planetTargetPreviewTexture ??= targetPreviewImage.texture as Texture2D;
        if (dropdownRoot == null)
            throw new MissingReferenceException($"{name}/Dropdown is missing.");
        if (dropdownFrameFillImage == null)
            throw new MissingReferenceException($"{name}/DropdownFrameFillImage is missing.");
        if (dropdownFrameTopImage == null)
            throw new MissingReferenceException($"{name}/DropdownFrameTopImage is missing.");
        if (dropdownFrameBottomImage == null)
            throw new MissingReferenceException($"{name}/DropdownFrameBottomImage is missing.");
        if (dropdownFrameLeftImage == null)
            throw new MissingReferenceException($"{name}/DropdownFrameLeftImage is missing.");
        if (dropdownFrameRightImage == null)
            throw new MissingReferenceException($"{name}/DropdownFrameRightImage is missing.");
        if (dropdownBackgroundImages == null || dropdownBackgroundImages.Length != 2)
            throw new MissingReferenceException($"{name}/DropdownBackgroundImages is missing.");
        if (dropdownScrollArea == null)
            throw new MissingReferenceException($"{name}/DropdownScrollArea is missing.");
        if (dropdownItemImageTemplate == null)
            throw new MissingReferenceException($"{name}/DropdownItemImageTemplate is missing.");
        if (dropdownItemTextTemplate == null)
            throw new MissingReferenceException($"{name}/DropdownItemTextTemplate is missing.");
        if (dropdownItemRowTemplate == null)
            throw new MissingReferenceException($"{name}/DropdownItemRowTemplate is missing.");
        if (dropdownItemRowTemplate.GetComponent<Button>() == null)
            throw new MissingReferenceException(
                $"{name}/DropdownItemRowTemplate/Button is missing."
            );
        if (dropdownItemImageAreaTemplate == null)
            throw new MissingReferenceException(
                $"{name}/DropdownItemImageAreaTemplate is missing."
            );
        if (dropdownContentPaddingTemplate == null)
            throw new MissingReferenceException(
                $"{name}/DropdownContentPaddingTemplate is missing."
            );
        if (personnelRoot == null)
            throw new MissingReferenceException($"{name}/Personnel is missing.");
        if (agentsHeaderImage == null)
            throw new MissingReferenceException($"{name}/AgentsHeaderImage is missing.");
        if (decoysHeaderImage == null)
            throw new MissingReferenceException($"{name}/DecoysHeaderImage is missing.");
        if (moveRightButtonImage == null)
            throw new MissingReferenceException($"{name}/MoveRightButtonImage is missing.");
        if (moveLeftButtonImage == null)
            throw new MissingReferenceException($"{name}/MoveLeftButtonImage is missing.");
        if (moveRightButton == null)
            throw new MissingReferenceException($"{name}/MoveRightButton is missing.");
        if (moveLeftButton == null)
            throw new MissingReferenceException($"{name}/MoveLeftButton is missing.");
        if (agentsScrollArea == null)
            throw new MissingReferenceException($"{name}/AgentsScrollArea is missing.");
        if (agentRowTemplate == null)
            throw new MissingReferenceException($"{name}/AgentRowTemplate is missing.");
        if (decoysScrollArea == null)
            throw new MissingReferenceException($"{name}/DecoysScrollArea is missing.");
        if (decoyRowTemplate == null)
            throw new MissingReferenceException($"{name}/DecoyRowTemplate is missing.");
        if (missionBackgroundTexture == null)
            throw new MissingReferenceException($"{name}/MissionBackgroundTexture is missing.");
        if (personnelBackgroundTexture == null)
            throw new MissingReferenceException($"{name}/PersonnelBackgroundTexture is missing.");
        if (titleTexture == null)
            throw new MissingReferenceException($"{name}/TitleTexture is missing.");
        if (closeButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonUpTexture is missing.");
        if (infoButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/InfoButtonUpTexture is missing.");
        if (infoButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/InfoButtonDownTexture is missing.");
        if (okButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/OkButtonUpTexture is missing.");
        if (okButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/OkButtonDownTexture is missing.");
        if (cancelButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CancelButtonUpTexture is missing.");
        if (cancelButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/CancelButtonDownTexture is missing.");
        if (dropdownButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/DropdownButtonUpTexture is missing.");
        if (dropdownButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/DropdownButtonDownTexture is missing.");
        if (dropdownBackgroundTexture == null)
            throw new MissingReferenceException($"{name}/DropdownBackgroundTexture is missing.");
        if (moveRightButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/MoveRightButtonUpTexture is missing.");
        if (moveRightButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/MoveRightButtonDownTexture is missing.");
        if (moveLeftButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/MoveLeftButtonUpTexture is missing.");
        if (moveLeftButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/MoveLeftButtonDownTexture is missing.");
        if (participantEnrouteBackgroundTexture == null)
            throw new MissingReferenceException(
                $"{name}/ParticipantEnrouteBackgroundTexture is missing."
            );

        dropdownItemImageTemplate.gameObject.SetActive(false);
        dropdownItemTextTemplate.gameObject.SetActive(false);
        dropdownItemRowTemplate.gameObject.SetActive(false);
        dropdownItemImageAreaTemplate.gameObject.SetActive(false);
        dropdownContentPaddingTemplate.gameObject.SetActive(false);
        agentRowTemplate.gameObject.SetActive(false);
        decoyRowTemplate.gameObject.SetActive(false);
        InitializeTemplateRects();
    }

    private void InitializeTemplateRects()
    {
        if (hasTargetPreviewSlotRect)
            return;

        targetPreviewSlotRect = UILayout.GetSourceRect(targetPreviewImage.rectTransform);
        hasTargetPreviewSlotRect = true;
    }

    private int GetParticipantRowHeight()
    {
        return UILayout.GetSourceRect(agentRowTemplate.transform as RectTransform).height;
    }

    private bool DropdownItemsChanged(IReadOnlyList<MissionDropdownItemRenderData> items)
    {
        if (!renderedAnyDropdownItems || renderedDropdownItemNames.Count != items.Count)
            return true;

        for (int i = 0; i < items.Count; i++)
        {
            if (renderedDropdownItemNames[i] != (items[i].Name ?? string.Empty))
                return true;
        }

        return false;
    }

    private bool ParticipantRowsChanged(
        IReadOnlyList<MissionParticipantRowRenderData> rows,
        int listId
    )
    {
        bool renderedAny = listId == 1 ? renderedAnyAgents : renderedAnyDecoys;
        List<string> renderedNames = listId == 1 ? renderedAgentNames : renderedDecoyNames;
        if (!renderedAny || renderedNames.Count != rows.Count)
            return true;

        for (int i = 0; i < rows.Count; i++)
        {
            if (renderedNames[i] != (rows[i].Name ?? string.Empty))
                return true;
        }

        return false;
    }

    private int GetDropdownItemHeight()
    {
        return UILayout.GetSourceRect(dropdownItemRowTemplate).height;
    }

    private Texture2D GetMissionChoiceTexture(MissionCreateWindowChoiceSource choice, bool small)
    {
        return uiContext?.GetTexture(GetMissionIconPath(choice.IconKey, small));
    }

    private Texture2D GetParticipantBackgroundTexture(ISceneNode participant)
    {
        if (participant is not IMovable { Movement: not null })
            return null;

        return uiContext?.GetEntityStatusTexture(participant, true)
            ?? participantEnrouteBackgroundTexture;
    }

    private string GetMissionIconPath(string iconKey, bool small)
    {
        return uiContext?.GetPlayerFactionTheme()?.MissionIcons?.GetImagePath(iconKey, small);
    }

    private static bool ContainsIndex(IReadOnlyCollection<int> selectedItems, int index)
    {
        if (selectedItems == null)
            return false;

        foreach (int selectedItem in selectedItems)
        {
            if (selectedItem == index)
                return true;
        }

        return false;
    }

    private bool IsDropdownInteraction(PointerEventData eventData)
    {
        return IsRaycastTargetUnder(eventData.pointerCurrentRaycast.gameObject, dropdownRoot)
            || IsRaycastTargetUnder(eventData.pointerPressRaycast.gameObject, dropdownRoot)
            || IsRaycastTargetUnder(eventData.pointerCurrentRaycast.gameObject, dropdownButtonImage)
            || IsRaycastTargetUnder(eventData.pointerPressRaycast.gameObject, dropdownButtonImage);
    }

    private static bool IsRaycastTargetUnder(GameObject target, Component root)
    {
        return target != null && root != null && target.transform.IsChildOf(root.transform);
    }

    private StrategyMissionChoice GetSelectedMissionChoice()
    {
        return selectedMissionIndex >= 0 && selectedMissionIndex < missionChoices.Count
            ? missionChoices[selectedMissionIndex]
            : null;
    }

    private static void SelectParticipant(HashSet<int> selection, int index, int count)
    {
        SelectableListSelection.SelectRangeItem(selection, index, count);
    }

    private static void SetImageFromTemplate(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    private static void SetImageAtTemplateOrigin(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    private static void SetImage(RawImage image, Texture texture, int x, int y)
    {
        int width = texture == null ? 0 : texture.width;
        int height = texture == null ? 0 : texture.height;
        SetImage(image, texture, x, y, width, height);
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

    private static void SetTemplateText(TextMeshProUGUI textField, string text)
    {
        SetTemplateText(textField, text, textField.color);
    }

    private static void SetTemplateText(TextMeshProUGUI textField, string text, Color32 color)
    {
        UILayout.SetTextContent(textField, text, color);
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

public sealed class MissionCreateWindowRenderData
{
    public int X;
    public int Y;
    public int ActiveTab;
    public int SelectedMissionIndex;
    public bool DropdownOpen;
    public string MissionName;
    public string TargetName;
    public ISceneNode Target;
    public Texture2D SelectedMissionTexture;
    public IReadOnlyList<MissionCreateWindowChoiceSource> MissionChoices =
        System.Array.Empty<MissionCreateWindowChoiceSource>();
    public IReadOnlyList<IMissionParticipant> SourceAgents =
        System.Array.Empty<IMissionParticipant>();
    public IReadOnlyList<IMissionParticipant> SourceDecoys =
        System.Array.Empty<IMissionParticipant>();
    public IReadOnlyCollection<int> SelectedAgents = System.Array.Empty<int>();
    public IReadOnlyCollection<int> SelectedDecoys = System.Array.Empty<int>();
    public List<MissionDropdownItemRenderData> DropdownItems =
        new List<MissionDropdownItemRenderData>();
    public List<MissionParticipantRowRenderData> AgentRows =
        new List<MissionParticipantRowRenderData>();
    public List<MissionParticipantRowRenderData> DecoyRows =
        new List<MissionParticipantRowRenderData>();
}

public sealed class MissionCreateWindowChoiceSource
{
    public MissionCreateWindowChoiceSource(string name, string iconKey)
    {
        Name = name;
        IconKey = iconKey;
    }

    public string Name { get; }
    public string IconKey { get; }
}

public sealed class MissionDropdownItemRenderData
{
    public string Name;
    public Color32 Color;
    public Texture2D Texture;
}

public enum MissionCreateWindowCommand
{
    None,
    Info,
    Ok,
    Cancel,
}
