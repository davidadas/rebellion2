using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FinderWindowView
    : MonoBehaviour,
        IStrategyUIContextReceiver,
        IStrategyWindowContent
{
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 Gray = new Color32(128, 128, 128, 255);
    private readonly List<string> renderedRowNames = new List<string>();
    private readonly List<RawImage> runtimeTabImages = new List<RawImage>();
    private readonly List<Button> runtimeTabButtons = new List<Button>();
    private static readonly int[] defaultButtonActions =
    {
        StrategyDialogButtonActions.Close,
        StrategyDialogButtonActions.Target,
    };
    private static readonly int[] fleetFinderButtonActions =
    {
        StrategyDialogButtonActions.Close,
        StrategyDialogButtonActions.Target,
        StrategyDialogButtonActions.ShipFinder,
        StrategyDialogButtonActions.FleetFinder,
    };
    private static readonly int[] personnelFinderButtonActions =
    {
        StrategyDialogButtonActions.Close,
        StrategyDialogButtonActions.Target,
        StrategyDialogButtonActions.PersonnelFinder,
        StrategyDialogButtonActions.SpecForcesFinder,
    };

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage overlayFrameImage;

    [SerializeField]
    private RawImage buttonStripImage;

    [SerializeField]
    private RawImage[] upperButtonImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private Button[] upperButtons = System.Array.Empty<Button>();

    [SerializeField]
    private RawImage[] twoButtonImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private Button[] twoButtons = System.Array.Empty<Button>();

    [SerializeField]
    private RawImage[] fourButtonImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private Button[] fourButtons = System.Array.Empty<Button>();

    [SerializeField]
    private TextMeshProUGUI titleTextField;

    [SerializeField]
    private TextMeshProUGUI labelTextField;

    [SerializeField]
    private RawImage[] tabImageSlots = System.Array.Empty<RawImage>();

    [SerializeField]
    private Button[] tabButtons = System.Array.Empty<Button>();

    [SerializeField]
    private RectTransform[] defaultTabSlotTemplates = System.Array.Empty<RectTransform>();

    [SerializeField]
    private RectTransform[] compactTabSlotTemplates = System.Array.Empty<RectTransform>();

    [SerializeField]
    private TextMeshProUGUI tabTitleTextField;

    [SerializeField]
    private TextMeshProUGUI compactTabTitleTextTemplate;

    [SerializeField]
    private ScrollAreaView rowsScrollArea;

    [SerializeField]
    private RectTransform defaultRowsClipTemplate;

    [SerializeField]
    private RectTransform troopRowsClipTemplate;

    [SerializeField]
    private RectTransform personnelRowsClipTemplate;

    [SerializeField]
    private RectTransform personnelPanelRowsClipTemplate;

    [SerializeField]
    private RectTransform rowsScrollPaddingTemplate;

    [SerializeField]
    private FinderWindowRowView rowTemplate;

    [SerializeField]
    private FinderWindowRowView personnelRowTemplate;

    [SerializeField]
    private FinderWindowRowView personnelPanelRowTemplate;

    [SerializeField]
    private RectTransform defaultScrollbarTemplate;

    [SerializeField]
    private RectTransform compactScrollbarTemplate;

    [SerializeField]
    private Texture2D allSystemsButtonUpTexture;

    [SerializeField]
    private Texture2D allSystemsButtonDownTexture;

    [SerializeField]
    private Texture2D unexploredSystemsButtonUpTexture;

    [SerializeField]
    private Texture2D unexploredSystemsButtonDownTexture;

    [SerializeField]
    private Texture2D systemFinderBackgroundTexture;

    private FinderWindowRenderData lastData;
    private UIContext uiContext;
    private bool stateInitialized;
    private bool panel;
    private int activeTab;
    private int selectedIndex = -1;
    private bool renderedAnyRows;
    private FinderMode renderedMode;
    private bool renderedPanel;
    private int renderedActiveTab = -1;
    private FinderWindowRowView activeRowTemplate;
    private SelectableListView<FinderWindowRowView, FinderWindowRowRenderData> rowsList;
    private Texture defaultBackgroundTexture;
    private Texture defaultOverlayFrameTexture;
    private Texture defaultButtonStripTexture;
    private Texture[] defaultUpperButtonTextures = System.Array.Empty<Texture>();
    private Texture[] defaultTwoButtonTextures = System.Array.Empty<Texture>();
    private Texture[] defaultFourButtonTextures = System.Array.Empty<Texture>();
    private UIWindow windowShell;

    public FinderMode Mode { get; private set; }

    public void InitializeWindow(FinderMode mode)
    {
        Mode = mode;
    }

    public void Initialize(UIContext uiContext)
    {
        this.uiContext = uiContext;
    }

    public void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active)
    {
        if (window == null)
            return;

        Render(
            new FinderWindowRowBuilder(
                context.Sectors,
                context.GameManager?.GetGame()?.GetFactions()
            ).CreateRenderData(this, window, context.UseUpperButtonLayout)
        );
    }

    public void Render(FinderWindowRenderData data)
    {
        VerifyReferences();
        data = CreateRenderData(data);
        lastData = data;

        UILayout.SetSourcePosition(transform as RectTransform, data.Frame.X, data.Frame.Y);
        RenderFrame(data.Frame);
        UILayout.SetTextContent(titleTextField, data.Title, Color.white);
        UILayout.SetTextContent(labelTextField, data.Label, Color.white);
        RenderTabs(data.ActiveTab);
        TextMeshProUGUI tabTitleTemplate = GetTabTitleTemplate(data.Mode, data.Panel);
        UILayout.SetTemplateText(
            tabTitleTextField,
            tabTitleTemplate,
            data.ActiveTabText,
            Color.white,
            UILayout.GetSourceRect(tabTitleTemplate.rectTransform)
        );
        ApplyRowsScrollAreaLayout(data);
        RenderRows(data);
        gameObject.SetActive(true);
    }

    private FinderWindowRenderData CreateRenderData(FinderWindowRenderData state)
    {
        InitializeState(state.Panel, state.ActiveTab, state.SelectedIndex);
        IReadOnlyList<FinderWindowTab> tabs = state.Tabs ?? System.Array.Empty<FinderWindowTab>();
        if (activeTab >= tabs.Count)
            activeTab = tabs.Count - 1;
        if (activeTab < 0 && tabs.Count > 0)
            activeTab = 0;

        RectInt rootRect = UILayout.GetSourceRect(transform as RectTransform);
        FinderWindowRenderData data = new FinderWindowRenderData
        {
            Title = state.Title,
            Label = state.Label,
            ActiveTabText = string.IsNullOrEmpty(state.ActiveTabText)
                ? GetTabText(state.Mode, panel, GetTab(tabs, activeTab))
                : state.ActiveTabText,
            Mode = state.Mode,
            ActiveTab = activeTab,
            Panel = panel,
            UseUpperButtonLayout = state.UseUpperButtonLayout,
            SelectedIndex = selectedIndex,
            SourceRows = state.SourceRows,
            Tabs = tabs,
            Frame =
                state.Frame
                ?? new UtilityWindowFrameRenderData
                {
                    X = state.X,
                    Y = state.Y,
                    Width = rootRect.width,
                    Height = rootRect.height,
                    FinderMode = state.Mode,
                    Panel = panel,
                    UseUpperButtonLayout = state.UseUpperButtonLayout,
                },
        };

        IReadOnlyList<FinderWindowSourceRow> rows =
            state.SourceRows ?? System.Array.Empty<FinderWindowSourceRow>();
        for (int i = 0; i < rows.Count; i++)
        {
            FinderWindowSourceRow row = rows[i];
            FinderWindowRowRenderData rowData = new FinderWindowRowRenderData
            {
                Name = row.Name,
                Color = i == selectedIndex ? White : Gray,
            };

            for (int j = 0; j < row.Counts.Count; j++)
            {
                int count = row.Counts[j];
                if (count > 0)
                    rowData.Counts.Add(new FinderWindowCountRenderData { Text = count.ToString() });
            }

            data.Rows.Add(rowData);
        }

        return data;
    }

    internal bool GetPanel(bool initialPanel)
    {
        InitializeState(initialPanel, 0, -1);
        return panel;
    }

    internal int GetActiveTab(int initialActiveTab, int tabCount)
    {
        InitializeState(false, initialActiveTab, -1);
        if (activeTab >= tabCount)
            activeTab = tabCount - 1;
        if (activeTab < 0 && tabCount > 0)
            activeTab = 0;
        return activeTab;
    }

    internal int GetSelectedIndex(int initialSelectedIndex, int rowCount)
    {
        InitializeState(false, 0, initialSelectedIndex);
        if (selectedIndex < 0 && initialSelectedIndex >= 0)
            selectedIndex = initialSelectedIndex;
        if (selectedIndex >= rowCount)
            selectedIndex = rowCount - 1;

        return selectedIndex;
    }

    internal FinderWindowRow GetSelectedSourceRow()
    {
        if (lastData?.SourceRows == null)
            return null;

        int index = GetSelectedIndex(-1, lastData.SourceRows.Count);
        return index >= 0 && index < lastData.SourceRows.Count
            ? lastData.SourceRows[index].SourceRow
            : null;
    }

    internal bool NeedsScrollbar(FinderMode mode, bool panel, int rowCount)
    {
        return rowCount * GetFinderRowHeight(mode)
            > UILayout.GetSourceRect(GetRowsClipTemplate(mode, panel)).height;
    }

    internal int GetScrollContentHeight(FinderMode mode, int rowCount)
    {
        return UILayout.GetSourceRect(rowsScrollPaddingTemplate).height
            + rowCount * GetFinderRowHeight(mode);
    }

    internal int GetScrollViewportHeight(FinderMode mode, bool panel)
    {
        return UILayout.GetSourceRect(GetRowsClipTemplate(mode, panel)).height;
    }

    internal int GetScrollStep(FinderMode mode)
    {
        return GetFinderRowHeight(mode);
    }

    internal static string GetWindowTitle(FinderMode mode, bool panel)
    {
        return mode switch
        {
            FinderMode.Systems => "Planetary System Finder",
            FinderMode.Fleets => panel ? "Ship Finder" : "Fleet Finder",
            FinderMode.Troops => "Troop Finder",
            FinderMode.Personnel => panel ? "Special Forces Finder" : "Personnel Finder",
            _ => string.Empty,
        };
    }

    internal static string GetWindowLabel(FinderMode mode, bool panel)
    {
        return mode switch
        {
            FinderMode.Systems => "System Name",
            FinderMode.Fleets => panel ? "Ship Name" : "Fleet Name",
            FinderMode.Troops => "Troop Location",
            FinderMode.Personnel => panel ? "Special Forces Location" : "Personnel Name",
            _ => string.Empty,
        };
    }

    internal string GetTabText(FinderMode mode, bool panel, FinderWindowTab tab)
    {
        if (tab == null)
            return string.Empty;
        if (tab.IsAll)
            return mode == FinderMode.Fleets && panel ? "All Ships" : GetAllTabText(mode);
        if (tab.IsNeutral)
            return "Neutral Systems";
        if (tab.IsUnexplored)
            return "Unexplored Systems";

        FinderWindowTheme theme = GetFinderTheme(tab.FactionInstanceId);
        string factionName = tab.FactionDisplayName;
        return mode switch
        {
            FinderMode.Systems => GetThemeText(theme?.SystemsText, factionName + " Systems"),
            FinderMode.Fleets => panel
                ? GetThemeText(theme?.ShipsText, factionName + " Ships")
                : GetThemeText(theme?.FleetsText, factionName + " Fleets"),
            FinderMode.Troops => GetThemeText(theme?.TroopsText, factionName + " Troops"),
            FinderMode.Personnel => GetThemeText(theme?.PersonnelText, factionName + " Personnel"),
            _ => string.Empty,
        };
    }

    private void Awake()
    {
        VerifyReferences();
        CaptureDefaultTextures();
        BindControls();
    }

    private void BindControls()
    {
        BindTabButtons();
        BindDialogButtons(upperButtons);
        BindDialogButtons(twoButtons);
        BindDialogButtons(fourButtons);
    }

    private void BindTabButtons()
    {
        InitializeRuntimeTabSlots();
        for (int i = 0; i < runtimeTabButtons.Count; i++)
            BindTabButton(runtimeTabButtons[i], i);
    }

    private void BindTabButton(Button button, int tab)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectTab(tab));
    }

    private void InitializeRuntimeTabSlots()
    {
        if (runtimeTabImages.Count > 0)
            return;

        for (int i = 0; i < tabImageSlots.Length; i++)
        {
            RawImage image = tabImageSlots[i];
            runtimeTabImages.Add(image);
            runtimeTabButtons.Add(
                i < tabButtons.Length && tabButtons[i] != null ? tabButtons[i]
                : image == null ? null
                : image.GetComponent<Button>()
            );
        }
    }

    private void EnsureTabSlots(int count)
    {
        InitializeRuntimeTabSlots();
        if (count <= runtimeTabImages.Count)
            return;

        RawImage template = runtimeTabImages.Count > 0 ? runtimeTabImages[0] : null;
        if (template == null)
            throw new MissingReferenceException($"{name}/TabImage0 is missing.");

        Transform parent = template.transform.parent;
        while (runtimeTabImages.Count < count)
        {
            int tab = runtimeTabImages.Count;
            RawImage image = Instantiate(template, parent);
            image.name = $"TabImage{tab}";
            RectInt rect = GetTabRect(tab, Mode);
            UILayout.SetSourceRect(image.rectTransform, rect.x, rect.y, rect.width, rect.height);
            Button button = image.GetComponent<Button>() ?? image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            runtimeTabImages.Add(image);
            runtimeTabButtons.Add(button);
            BindTabButton(button, tab);
        }
    }

    private void BindDialogButtons(Button[] buttons)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            button.onClick.AddListener(() => HandleDialogButtonClicked(button));
        }
    }

    private void SelectTab(int tab)
    {
        RequestFocus();
        SetActiveTab(tab);
        RequestRender();
    }

    private void HandleDialogButtonClicked(Button button)
    {
        RequestFocus();
        int action = GetButtonAction(button);
        if (action == 0)
            return;

        if (ApplyLocalButtonAction(action))
            RequestRender();
        else if (action == StrategyDialogButtonActions.Close)
            GetWindowShell()?.RequestButton(StrategyWindowButtonActions.CloseWindow);
        else
            GetWindowShell()?.RequestButton(action);
    }

    private int GetButtonAction(Button button)
    {
        if (lastData?.Frame == null)
            return 0;

        Button[] buttonSlots = GetButtonComponents(lastData.Frame);
        IReadOnlyList<int> buttonActions = GetButtonActions(lastData.Frame.FinderMode);
        int count = Mathf.Min(buttonSlots.Length, buttonActions.Count);
        for (int i = 0; i < count; i++)
        {
            if (buttonSlots[i] == button)
                return GetButtonAction(buttonActions, i);
        }

        return 0;
    }

    private void HandleRowSelected(SelectableListRowView row, PointerEventData eventData)
    {
        if (row is not FinderWindowRowView finderRow)
            return;

        RequestFocus();
        selectedIndex = finderRow.Index;
        RequestRender();
    }

    private void HandleRowActivated(SelectableListRowView row, PointerEventData eventData)
    {
        if (row is not FinderWindowRowView finderRow)
            return;

        RequestFocus();
        selectedIndex = finderRow.Index;
        OpenSelectedRow();
    }

    private void HandleRowContextRequested(SelectableListRowView row, PointerEventData eventData)
    {
        GetWindowShell()?.RequestContext(eventData);
    }

    private void OpenSelectedRow()
    {
        if (GetSelectedSourceRow() == null)
            return;

        uiContext?.Dispatcher.Send(new StrategyUIRequests.OpenSelectedFinderItem(GetWindowId()));
    }

    private void RequestFocus()
    {
        GetWindowShell()?.RequestFocus();
    }

    private bool CanNavigateRows()
    {
        return GetWindowShell()?.ActiveWindow == true;
    }

    private void RequestRender()
    {
        uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private int GetWindowId()
    {
        return GetWindowShell()?.Id ?? 0;
    }

    private UIWindow GetWindowShell()
    {
        if (windowShell == null)
            windowShell = GetComponent<UIWindow>();

        return windowShell;
    }

    private void InitializeState(bool initialPanel, int initialActiveTab, int initialSelectedIndex)
    {
        if (stateInitialized)
            return;

        panel = initialPanel;
        activeTab = Mathf.Max(0, initialActiveTab);
        selectedIndex = initialSelectedIndex;
        stateInitialized = true;
    }

    private void SetActiveTab(int tab)
    {
        if (tab == activeTab)
            return;

        activeTab = tab;
        selectedIndex = -1;
    }

    private bool ApplyLocalButtonAction(int action)
    {
        switch (action)
        {
            case StrategyDialogButtonActions.ShipFinder:
            case StrategyDialogButtonActions.SpecForcesFinder:
                SetPanel(true);
                return true;
            case StrategyDialogButtonActions.FleetFinder:
            case StrategyDialogButtonActions.PersonnelFinder:
                SetPanel(false);
                return true;
            default:
                return false;
        }
    }

    private void SetPanel(bool value)
    {
        panel = value;
        activeTab = Mathf.Min(activeTab, GetTabCount(lastData) - 1);
        selectedIndex = -1;
    }

    private void RenderFrame(UtilityWindowFrameRenderData frame)
    {
        SetImageAtTemplateOrigin(
            backgroundImage,
            GetBackgroundTexture(frame) ?? defaultBackgroundTexture
        );
        SetImageAtTemplateOrigin(
            overlayFrameImage,
            GetOverlayFrameTexture() ?? defaultOverlayFrameTexture
        );
        RectInt buttonStripRect = UILayout.GetSourceRect(buttonStripImage.rectTransform);
        Texture buttonStripTexture = GetButtonStripTexture(frame);
        if (buttonStripTexture == null && !frame.UseUpperButtonLayout)
            buttonStripTexture = defaultButtonStripTexture;
        UILayout.SetImage(
            buttonStripImage,
            buttonStripTexture,
            frame.Width - (buttonStripTexture?.width ?? buttonStripRect.width),
            buttonStripRect.y
        );

        IReadOnlyList<int> buttonActions = GetButtonActions(frame.FinderMode);
        HideButtonSlots(upperButtonImages);
        HideButtonSlots(twoButtonImages);
        HideButtonSlots(fourButtonImages);

        RawImage[] buttonSlots = GetButtonSlots(frame);
        for (int i = 0; i < buttonSlots.Length; i++)
        {
            int buttonAction = GetButtonAction(buttonActions, i);
            Texture buttonUpTexture =
                GetDialogButtonTexture(frame, buttonAction)
                ?? GetDefaultButtonTexture(buttonSlots, i);
            SetDialogButton(buttonSlots[i], GetButtonComponents(frame), buttonUpTexture, frame, i);
        }
    }

    private void RenderTabs(int activeTab)
    {
        int tabCount = GetTabCount(lastData);
        EnsureTabSlots(tabCount);
        for (int i = 0; i < tabCount; i++)
        {
            RawImage image = GetTabImage(i);
            ApplyTabLayout(image, i, lastData.Mode);
            Texture texture =
                GetTabTexture(GetTab(lastData.Tabs, i), i == activeTab) ?? image.texture;
            UILayout.SetImageTexture(image, texture);
            image.raycastTarget = true;
            Button button = GetTabButton(i);
            if (button != null)
                button.interactable = true;
        }

        for (int i = tabCount; i < runtimeTabImages.Count; i++)
        {
            if (runtimeTabImages[i] != null)
                runtimeTabImages[i].gameObject.SetActive(false);
            if (i < runtimeTabButtons.Count && runtimeTabButtons[i] != null)
                runtimeTabButtons[i].interactable = false;
        }
    }

    private void RenderRows(FinderWindowRenderData data)
    {
        IReadOnlyList<FinderWindowRowRenderData> rows =
            data.Rows != null ? data.Rows : System.Array.Empty<FinderWindowRowRenderData>();
        bool resetScroll = RowsChanged(data, rows);
        int rowHeight = GetFinderRowHeight(data.Mode);
        FinderWindowRowView rowTemplate = GetRowTemplate(data.Mode, data.Panel);
        EnsureRowsList(rowTemplate)
            .Render(
                rows,
                GetScrollContentHeight(data.Mode, rows.Count),
                GetScrollStep(data.Mode),
                resetScroll,
                rowHeight,
                (rowView, row, index) =>
                {
                    rowView.SetPreferredHeight(rowHeight);
                    rowView.Render(index, row);
                },
                (_, index) => index == data.SelectedIndex
            );

        renderedAnyRows = true;
        renderedMode = data.Mode;
        renderedPanel = data.Panel;
        renderedActiveTab = data.ActiveTab;
        renderedRowNames.Clear();
        for (int i = 0; i < rows.Count; i++)
            renderedRowNames.Add(rows[i].Name ?? string.Empty);
    }

    private RawImage[] GetButtonSlots(UtilityWindowFrameRenderData frame)
    {
        if (frame.UseUpperButtonLayout)
            return upperButtonImages;

        return HasFourDialogButtons(frame.FinderMode) ? fourButtonImages : twoButtonImages;
    }

    private Button[] GetButtonComponents(UtilityWindowFrameRenderData frame)
    {
        if (frame.UseUpperButtonLayout)
            return upperButtons;

        return HasFourDialogButtons(frame.FinderMode) ? fourButtons : twoButtons;
    }

    private static bool HasFourDialogButtons(FinderMode mode)
    {
        return mode is FinderMode.Fleets or FinderMode.Personnel;
    }

    private static void SetDialogButton(
        RawImage image,
        Button[] buttons,
        Texture texture,
        UtilityWindowFrameRenderData frame,
        int index
    )
    {
        if (texture == null)
        {
            image.gameObject.SetActive(false);
            if (buttons != null && index >= 0 && index < buttons.Length && buttons[index] != null)
                buttons[index].interactable = false;
            return;
        }

        int y = GetDialogButtonY(frame, index);
        int sideOffset = GetDialogButtonSideOffset(frame);
        UILayout.SetImage(image, texture, frame.Width - texture.width - sideOffset, y);
        image.raycastTarget = true;
        if (buttons != null && index >= 0 && index < buttons.Length && buttons[index] != null)
            buttons[index].interactable = true;
    }

    private static int GetDialogButtonSideOffset(UtilityWindowFrameRenderData frame)
    {
        if (frame.UseUpperButtonLayout)
            return 0;

        return HasFourDialogButtons(frame.FinderMode) ? 15 : 12;
    }

    private static int GetDialogButtonY(UtilityWindowFrameRenderData frame, int index)
    {
        if (frame.UseUpperButtonLayout)
            return index switch
            {
                0 => 21,
                1 => 89,
                2 => 143,
                _ => 197,
            };

        return index switch
        {
            0 => 25,
            1 => 93,
            2 => 147,
            _ => 201,
        };
    }

    private static IReadOnlyList<int> GetButtonActions(FinderMode mode)
    {
        return mode switch
        {
            FinderMode.Fleets => fleetFinderButtonActions,
            FinderMode.Personnel => personnelFinderButtonActions,
            _ => defaultButtonActions,
        };
    }

    private static int GetButtonAction(IReadOnlyList<int> buttonActions, int index)
    {
        return index >= 0 && index < buttonActions.Count ? buttonActions[index] : 0;
    }

    private static void HideButtonSlots(RawImage[] images)
    {
        if (images == null)
            return;

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null)
                images[i].gameObject.SetActive(false);
        }
    }

    private RawImage GetTabImage(int index)
    {
        EnsureTabSlots(index + 1);
        if (index >= 0 && index < runtimeTabImages.Count && runtimeTabImages[index] != null)
            return runtimeTabImages[index];

        throw new MissingReferenceException($"{name}/TabImage{index} is missing.");
    }

    private void ApplyTabLayout(RawImage image, int index, FinderMode mode)
    {
        RectInt rect = GetTabRect(index, mode);
        UILayout.SetSourceRect(image.rectTransform, rect.x, rect.y, rect.width, rect.height);
    }

    private RectInt GetTabRect(int index, FinderMode mode)
    {
        RectTransform[] templates = GetTabLayoutTemplates(mode);
        RectInt first = UILayout.GetSourceRect(templates[0]);
        if (index <= 0)
            return first;

        RectInt second = UILayout.GetSourceRect(templates[1]);
        int tabPitch = second.x - first.x;
        return new RectInt(first.x + index * tabPitch, first.y, first.width, first.height);
    }

    private RectTransform[] GetTabLayoutTemplates(FinderMode mode)
    {
        return mode is FinderMode.Troops or FinderMode.Personnel
            ? compactTabSlotTemplates
            : defaultTabSlotTemplates;
    }

    private Button GetTabButton(int index)
    {
        EnsureTabSlots(index + 1);
        return index >= 0 && index < runtimeTabButtons.Count ? runtimeTabButtons[index] : null;
    }

    private Texture2D GetTabTexture(FinderWindowTab tab, bool active)
    {
        FinderWindowTheme playerTheme = GetFinderTheme();
        if (tab?.IsNeutral == true)
            return GetThemedButtonTexture(playerTheme?.NeutralSystemsButton, active)
                ?? GetStrategyViewButtonTexture(
                    "ui_strategyview_finder_window_neutral_systems_button",
                    active
                );
        if (tab?.IsUnexplored == true)
            return (active ? unexploredSystemsButtonDownTexture : unexploredSystemsButtonUpTexture)
                ?? GetStrategyViewButtonTexture(
                    "ui_strategyview_finder_window_unexplored_systems_button",
                    active
                );
        if (!string.IsNullOrEmpty(tab?.FactionInstanceId))
        {
            FinderWindowTheme factionTheme = GetFinderTheme(tab.FactionInstanceId);
            return GetThemedButtonTexture(
                factionTheme?.SystemsButton ?? playerTheme?.SystemsButton,
                active
            );
        }

        return (active ? allSystemsButtonDownTexture : allSystemsButtonUpTexture)
            ?? GetStrategyViewButtonTexture(
                "ui_strategyview_encyclopedia_window_all_systems_button",
                active
            );
    }

    private Texture2D GetBackgroundTexture(UtilityWindowFrameRenderData frame)
    {
        return frame.FinderMode switch
        {
            FinderMode.Systems => systemFinderBackgroundTexture
                ?? GetTexture(
                    "Art/UI/StrategyView/ui_strategyview_finder_window_system_finder_background"
                ),
            FinderMode.Fleets => GetTexture(
                frame.Panel
                    ? GetFinderTheme()?.ShipFinderBackgroundImagePath
                    : GetFinderTheme()?.FleetFinderBackgroundImagePath
            ),
            FinderMode.Troops => GetTexture(GetFinderTheme()?.TroopFinderBackgroundImagePath),
            FinderMode.Personnel => GetTexture(
                frame.Panel
                    ? GetFinderTheme()?.SpecialForcesFinderBackgroundImagePath
                    : GetFinderTheme()?.PersonnelFinderBackgroundImagePath
            ),
            _ => systemFinderBackgroundTexture,
        };
    }

    private Texture2D GetOverlayFrameTexture()
    {
        return GetTexture(GetFinderTheme()?.OverlayFrameImagePath);
    }

    private Texture2D GetButtonStripTexture(UtilityWindowFrameRenderData frame)
    {
        if (frame.UseUpperButtonLayout)
            return null;

        string path = HasFourDialogButtons(frame.FinderMode)
            ? GetFinderTheme()?.FourButtonStripImagePath
            : GetFinderTheme()?.TwoButtonStripImagePath;
        return GetTexture(path);
    }

    private Texture2D GetDialogButtonTexture(
        UtilityWindowFrameRenderData frame,
        int action,
        bool forcePressed = false
    )
    {
        if (action == 0)
            return null;

        bool pressed = forcePressed || IsDialogButtonActive(frame, action);
        return action switch
        {
            StrategyDialogButtonActions.Close => GetThemedButtonTexture(
                GetFinderTheme()?.CloseButton,
                pressed
            ),
            StrategyDialogButtonActions.Target => GetThemedButtonTexture(
                GetFinderTheme()?.TargetButton,
                pressed
            ),
            StrategyDialogButtonActions.ShipFinder => GetThemedButtonTexture(
                GetFinderTheme()?.ShipButton,
                pressed
            ),
            StrategyDialogButtonActions.FleetFinder => GetThemedButtonTexture(
                GetFinderTheme()?.FleetButton,
                pressed
            ),
            StrategyDialogButtonActions.PersonnelFinder => GetThemedButtonTexture(
                GetFinderTheme()?.PersonnelButton,
                pressed
            ),
            StrategyDialogButtonActions.SpecForcesFinder => GetThemedButtonTexture(
                GetFinderTheme()?.SpecialForcesButton,
                pressed
            ),
            _ => null,
        };
    }

    private Texture2D GetThemedButtonTexture(WindowButtonImageTheme theme, bool pressed)
    {
        return GetTexture(theme?.GetImagePath(pressed));
    }

    private Texture2D GetStrategyViewButtonTexture(string assetName, bool pressed)
    {
        return GetTexture("Art/UI/StrategyView/" + assetName + (pressed ? "_pressed" : "_up"));
    }

    private Texture2D GetTexture(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        Texture2D texture = uiContext?.GetTexture(path);
        if (texture != null)
            return texture;

        texture = ResourceManager.TryGetTexture(path);
        if (texture != null)
            return texture;

        Sprite sprite = ResourceManager.TryGetSprite(path);
        return sprite == null ? null : sprite.texture;
    }

    private FinderWindowTheme GetFinderTheme()
    {
        return uiContext?.GetPlayerFactionTheme()?.StrategyWindows?.Finder;
    }

    private FinderWindowTheme GetFinderTheme(string factionInstanceId)
    {
        return uiContext?.GetTheme(factionInstanceId)?.StrategyWindows?.Finder;
    }

    private static string GetThemeText(string text, string fallback)
    {
        return string.IsNullOrEmpty(text) ? fallback : text;
    }

    private static string GetAllTabText(FinderMode mode)
    {
        return mode switch
        {
            FinderMode.Systems => "All Systems",
            FinderMode.Fleets => "All Fleets",
            _ => string.Empty,
        };
    }

    private static int GetTabCount(FinderWindowRenderData data)
    {
        return data?.Tabs?.Count ?? 0;
    }

    private static FinderWindowTab GetTab(IReadOnlyList<FinderWindowTab> tabs, int index)
    {
        return tabs != null && index >= 0 && index < tabs.Count ? tabs[index] : null;
    }

    private static bool IsDialogButtonActive(UtilityWindowFrameRenderData frame, int action)
    {
        return action switch
        {
            StrategyDialogButtonActions.ShipFinder => frame.Panel,
            StrategyDialogButtonActions.FleetFinder => !frame.Panel,
            StrategyDialogButtonActions.PersonnelFinder => !frame.Panel,
            StrategyDialogButtonActions.SpecForcesFinder => frame.Panel,
            _ => false,
        };
    }

    private SelectableListView<FinderWindowRowView, FinderWindowRowRenderData> EnsureRowsList(
        FinderWindowRowView template
    )
    {
        if (activeRowTemplate == template && rowsList != null)
            return rowsList;

        rowsList?.Clear();
        activeRowTemplate = template;
        rowsList = new SelectableListView<FinderWindowRowView, FinderWindowRowRenderData>(
            rowsScrollArea,
            template,
            "FinderRow",
            HandleFinderRowSelected,
            HandleFinderRowActivated,
            HandleFinderRowContextRequested,
            CanNavigateRows,
            transform
        );
        return rowsList;
    }

    private void HandleFinderRowSelected(FinderWindowRowView row, PointerEventData eventData)
    {
        HandleRowSelected(row, eventData);
    }

    private void HandleFinderRowActivated(FinderWindowRowView row, PointerEventData eventData)
    {
        HandleRowActivated(row, eventData);
    }

    private void HandleFinderRowContextRequested(
        FinderWindowRowView row,
        PointerEventData eventData
    )
    {
        HandleRowContextRequested(row, eventData);
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (overlayFrameImage == null)
            throw new MissingReferenceException($"{name}/OverlayFrameImage is missing.");
        if (buttonStripImage == null)
            throw new MissingReferenceException($"{name}/ButtonStripImage is missing.");
        VerifyButtonSlotReferences("Upper", upperButtonImages);
        VerifyButtonReferences("Upper", upperButtons);
        VerifyButtonSlotReferences("Two", twoButtonImages);
        VerifyButtonReferences("Two", twoButtons);
        VerifyButtonSlotReferences("Four", fourButtonImages);
        VerifyButtonReferences("Four", fourButtons);
        if (titleTextField == null)
            throw new MissingReferenceException($"{name}/TitleTextField is missing.");
        if (labelTextField == null)
            throw new MissingReferenceException($"{name}/LabelTextField is missing.");
        if (tabImageSlots == null || tabImageSlots.Length == 0)
            throw new MissingReferenceException($"{name}/TabImageSlots are missing.");
        if (tabButtons == null || tabButtons.Length == 0)
            throw new MissingReferenceException($"{name}/TabButtons are missing.");
        VerifyTabLayoutTemplateReferences("Default", defaultTabSlotTemplates);
        VerifyTabLayoutTemplateReferences("Compact", compactTabSlotTemplates);
        if (tabTitleTextField == null)
            throw new MissingReferenceException($"{name}/TabTitleTextField is missing.");
        if (compactTabTitleTextTemplate == null)
            throw new MissingReferenceException($"{name}/CompactTabTitleTextTemplate is missing.");
        if (rowsScrollArea == null)
            throw new MissingReferenceException($"{name}/RowsScrollArea is missing.");
        if (defaultRowsClipTemplate == null)
            throw new MissingReferenceException($"{name}/DefaultRowsClipTemplate is missing.");
        if (troopRowsClipTemplate == null)
            throw new MissingReferenceException($"{name}/TroopRowsClipTemplate is missing.");
        if (personnelRowsClipTemplate == null)
            throw new MissingReferenceException($"{name}/PersonnelRowsClipTemplate is missing.");
        if (personnelPanelRowsClipTemplate == null)
            throw new MissingReferenceException(
                $"{name}/PersonnelPanelRowsClipTemplate is missing."
            );
        if (rowsScrollPaddingTemplate == null)
            throw new MissingReferenceException($"{name}/RowsScrollPaddingTemplate is missing.");
        if (rowTemplate == null)
            throw new MissingReferenceException($"{name}/RowTemplate is missing.");
        if (personnelRowTemplate == null)
            throw new MissingReferenceException($"{name}/PersonnelRowTemplate is missing.");
        if (personnelPanelRowTemplate == null)
            throw new MissingReferenceException($"{name}/PersonnelPanelRowTemplate is missing.");
        if (defaultScrollbarTemplate == null)
            throw new MissingReferenceException($"{name}/DefaultScrollbarTemplate is missing.");
        if (compactScrollbarTemplate == null)
            throw new MissingReferenceException($"{name}/CompactScrollbarTemplate is missing.");

        compactTabTitleTextTemplate.gameObject.SetActive(false);
        defaultRowsClipTemplate.gameObject.SetActive(false);
        troopRowsClipTemplate.gameObject.SetActive(false);
        personnelRowsClipTemplate.gameObject.SetActive(false);
        personnelPanelRowsClipTemplate.gameObject.SetActive(false);
        rowsScrollPaddingTemplate.gameObject.SetActive(false);
        rowTemplate.gameObject.SetActive(false);
        personnelRowTemplate.gameObject.SetActive(false);
        personnelPanelRowTemplate.gameObject.SetActive(false);
        defaultScrollbarTemplate.gameObject.SetActive(false);
        compactScrollbarTemplate.gameObject.SetActive(false);
    }

    private void VerifyTabLayoutTemplateReferences(string label, RectTransform[] templates)
    {
        if (templates == null || templates.Length < 2)
            throw new MissingReferenceException(
                $"{name}/{label} tab layout templates are missing."
            );
        for (int i = 0; i < templates.Length; i++)
        {
            if (templates[i] == null)
                throw new MissingReferenceException(
                    $"{name}/{label}TabSlotTemplate{i} is missing."
                );
        }
    }

    private void VerifyButtonSlotReferences(string label, RawImage[] images)
    {
        if (images == null || images.Length == 0)
            throw new MissingReferenceException($"{name}/{label} button images are missing.");
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null)
                throw new MissingReferenceException($"{name}/{label}ButtonImage{i} is missing.");
        }
    }

    private void VerifyButtonReferences(string label, Button[] buttons)
    {
        if (buttons == null || buttons.Length == 0)
            throw new MissingReferenceException($"{name}/{label} buttons are missing.");
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
                throw new MissingReferenceException($"{name}/{label}Button{i} is missing.");
        }
    }

    private void CaptureDefaultTextures()
    {
        defaultBackgroundTexture = backgroundImage.texture;
        defaultOverlayFrameTexture = overlayFrameImage.texture;
        defaultButtonStripTexture = buttonStripImage.texture;
        defaultUpperButtonTextures = CaptureDefaultTextures(upperButtonImages);
        defaultTwoButtonTextures = CaptureDefaultTextures(twoButtonImages);
        defaultFourButtonTextures = CaptureDefaultTextures(fourButtonImages);
    }

    private static Texture[] CaptureDefaultTextures(RawImage[] images)
    {
        if (images == null)
            return System.Array.Empty<Texture>();

        Texture[] textures = new Texture[images.Length];
        for (int i = 0; i < images.Length; i++)
            textures[i] = images[i]?.texture;
        return textures;
    }

    private Texture GetDefaultButtonTexture(RawImage[] buttonSlots, int index)
    {
        Texture[] textures =
            buttonSlots == upperButtonImages ? defaultUpperButtonTextures
            : buttonSlots == fourButtonImages ? defaultFourButtonTextures
            : defaultTwoButtonTextures;
        return index >= 0 && index < textures.Length ? textures[index] : null;
    }

    private TextMeshProUGUI GetTabTitleTemplate(FinderMode mode, bool panel)
    {
        return mode == FinderMode.Personnel && !panel
            ? compactTabTitleTextTemplate
            : tabTitleTextField;
    }

    private RectTransform GetRowsClipTemplate(FinderMode mode, bool panel)
    {
        if (mode == FinderMode.Troops)
            return troopRowsClipTemplate;
        if (mode == FinderMode.Personnel)
            return panel ? personnelPanelRowsClipTemplate : personnelRowsClipTemplate;
        return defaultRowsClipTemplate;
    }

    private FinderWindowRowView GetRowTemplate(FinderMode mode, bool panel)
    {
        if (mode != FinderMode.Personnel)
            return rowTemplate;

        return panel ? personnelPanelRowTemplate : personnelRowTemplate;
    }

    private RectTransform GetScrollbarTemplate(FinderMode mode, bool panel)
    {
        return mode is FinderMode.Troops or FinderMode.Personnel
            ? compactScrollbarTemplate
            : defaultScrollbarTemplate;
    }

    private int GetFinderRowHeight(FinderMode mode)
    {
        int height = UILayout
            .GetSourceRect(GetRowTemplate(mode, panel).transform as RectTransform)
            .height;
        return mode == FinderMode.Troops ? 25 : height;
    }

    private void ApplyRowsScrollAreaLayout(FinderWindowRenderData data)
    {
        RectInt rowsClip = UILayout.GetSourceRect(GetRowsClipTemplate(data.Mode, data.Panel));
        RectInt scrollbar = UILayout.GetSourceRect(GetScrollbarTemplate(data.Mode, data.Panel));
        RectInt bounds = Union(rowsClip, scrollbar);
        UILayout.SetSourceRect(
            rowsScrollArea.transform as RectTransform,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
        rowsScrollArea.SetLayout(
            new Vector2(rowsClip.x - bounds.x, rowsClip.y - bounds.y),
            new Vector2(rowsClip.width, rowsClip.height),
            new Vector2(scrollbar.x - bounds.x, scrollbar.y - bounds.y),
            new Vector2(scrollbar.width, scrollbar.height)
        );
    }

    private static void SetImageAtTemplateOrigin(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    private static RectInt Union(RectInt first, RectInt second)
    {
        int minX = Mathf.Min(first.x, second.x);
        int minY = Mathf.Min(first.y, second.y);
        int maxX = Mathf.Max(first.x + first.width, second.x + second.width);
        int maxY = Mathf.Max(first.y + first.height, second.y + second.height);
        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }

    private bool RowsChanged(
        FinderWindowRenderData data,
        IReadOnlyList<FinderWindowRowRenderData> rows
    )
    {
        if (
            !renderedAnyRows
            || renderedMode != data.Mode
            || renderedPanel != data.Panel
            || renderedActiveTab != data.ActiveTab
            || renderedRowNames.Count != rows.Count
        )
            return true;

        for (int i = 0; i < rows.Count; i++)
        {
            if (renderedRowNames[i] != (rows[i].Name ?? string.Empty))
                return true;
        }

        return false;
    }
}

public sealed class FinderWindowSourceRow
{
    public FinderWindowSourceRow(string name, List<int> counts = null)
    {
        Name = name;
        Counts = counts ?? new List<int>();
    }

    internal FinderWindowSourceRow(FinderWindowRow row)
    {
        SourceRow = row;
        Name = row?.Name ?? string.Empty;
        Counts = row?.Counts ?? new List<int>();
    }

    public string Name { get; }
    public List<int> Counts { get; }
    internal FinderWindowRow SourceRow { get; }
}

public sealed class UtilityWindowFrameRenderData
{
    public int X;
    public int Y;
    public int Width;
    public int Height;
    public FinderMode FinderMode;
    public bool Panel;
    public bool UseUpperButtonLayout;
}

public sealed class FinderWindowRenderData
{
    public int X;
    public int Y;
    public string Title;
    public string Label;
    public string ActiveTabText;
    public FinderMode Mode;
    public int ActiveTab;
    public bool Panel;
    public bool UseUpperButtonLayout;
    public int SelectedIndex;
    public IReadOnlyList<FinderWindowTab> Tabs;
    public IReadOnlyList<FinderWindowSourceRow> SourceRows;
    public UtilityWindowFrameRenderData Frame;
    public List<FinderWindowRowRenderData> Rows = new List<FinderWindowRowRenderData>();
}

public sealed class FinderWindowTab
{
    private FinderWindowTab(
        bool isAll,
        bool isNeutral,
        bool isUnexplored,
        string factionInstanceId,
        string factionDisplayName
    )
    {
        IsAll = isAll;
        IsNeutral = isNeutral;
        IsUnexplored = isUnexplored;
        FactionInstanceId = factionInstanceId;
        FactionDisplayName = factionDisplayName;
    }

    public bool IsAll { get; }
    public bool IsNeutral { get; }
    public bool IsUnexplored { get; }
    public string FactionInstanceId { get; }
    public string FactionDisplayName { get; }

    public static FinderWindowTab All()
    {
        return new FinderWindowTab(true, false, false, null, null);
    }

    public static FinderWindowTab Neutral()
    {
        return new FinderWindowTab(false, true, false, null, null);
    }

    public static FinderWindowTab Unexplored()
    {
        return new FinderWindowTab(false, false, true, null, null);
    }

    public static FinderWindowTab Faction(string factionInstanceId, string factionDisplayName)
    {
        return new FinderWindowTab(false, false, false, factionInstanceId, factionDisplayName);
    }
}

public sealed class FinderWindowRowRenderData
{
    public string Name;
    public Color32 Color;
    public List<FinderWindowCountRenderData> Counts = new List<FinderWindowCountRenderData>();
}

public sealed class FinderWindowCountRenderData
{
    public string Text;
}
