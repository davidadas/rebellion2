using System.Collections.Generic;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class EncyclopediaWindowView
    : MonoBehaviour,
        IStrategyUIContextReceiver,
        IStrategyWindowContent
{
    private readonly List<TextMeshProUGUI> detailLineTextFields = new List<TextMeshProUGUI>();
    private readonly List<string> renderedRowNames = new List<string>();
    private readonly List<string> renderedDetailLines = new List<string>();
    private readonly SelectableListSelection rowSelection = new SelectableListSelection();
    private static readonly int[] encyclopediaButtonActions =
    {
        StrategyDialogButtonActions.Close,
        StrategyDialogButtonActions.EncyclopediaTopic,
        StrategyDialogButtonActions.EncyclopediaIndex,
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
    private TextMeshProUGUI topicLabelTextField;

    [SerializeField]
    private RawImage[] tabImageSlots = System.Array.Empty<RawImage>();

    [SerializeField]
    private Button[] tabButtons = System.Array.Empty<Button>();

    [SerializeField]
    private TextMeshProUGUI tabTitleTextField;

    [SerializeField]
    private ScrollAreaView rowsScrollArea;

    [SerializeField]
    private EncyclopediaWindowRowView rowTemplate;

    [SerializeField]
    private TextMeshProUGUI rowTextTemplate;

    [SerializeField]
    private RawImage detailBackgroundImage;

    [SerializeField]
    private RawImage detailCardImage;

    [SerializeField]
    private RawImage detailPreviousButtonImage;

    [SerializeField]
    private Button detailPreviousButton;

    [SerializeField]
    private RawImage detailNextButtonImage;

    [SerializeField]
    private Button detailNextButton;

    [SerializeField]
    private TextMeshProUGUI detailTitleTextField;

    [SerializeField]
    private ScrollAreaView detailLinesScrollArea;

    [SerializeField]
    private TextMeshProUGUI detailLineTextTemplate;

    [SerializeField]
    private Texture2D topicBackgroundTexture;

    [SerializeField]
    private Texture2D allSystemsButtonUpTexture;

    [SerializeField]
    private Texture2D allSystemsButtonDownTexture;

    [SerializeField]
    private Texture2D systemButtonUpTexture;

    [SerializeField]
    private Texture2D systemButtonDownTexture;

    [SerializeField]
    private Texture2D previousButtonUpTexture;

    [SerializeField]
    private Texture2D previousButtonDisabledTexture;

    [SerializeField]
    private Texture2D nextButtonUpTexture;

    [SerializeField]
    private Texture2D nextButtonDisabledTexture;

    [SerializeField]
    private Texture2D backgroundTexture;

    private EncyclopediaWindowRenderData lastData;
    private UIContext uiContext;
    private bool stateInitialized;
    private bool panel;
    private int activeTab;
    private int renderedActiveTab = -1;
    private bool renderedAnyRows;
    private int renderedDetailSelectedIndex = -1;
    private bool renderedAnyDetailLines;
    private Texture defaultBackgroundTexture;
    private Texture defaultDetailBackgroundTexture;
    private Texture defaultOverlayFrameTexture;
    private Texture defaultButtonStripTexture;
    private Texture[] defaultUpperButtonTextures = System.Array.Empty<Texture>();
    private Texture[] defaultFourButtonTextures = System.Array.Empty<Texture>();
    private UIWindow windowShell;
    private bool requestedEntryPending;
    private string requestedEntryTypeId;
    private EncyclopediaEntries entries;
    private SelectableListView<EncyclopediaWindowRowView, EncyclopediaWindowRowRenderData> rowsList;

    public void Initialize(UIContext uiContext)
    {
        this.uiContext = uiContext;
    }

    public void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active)
    {
        if (window == null)
            return;

        Render(CreateWindowRenderData(window, context.UseUpperButtonLayout));
    }

    public void Render(EncyclopediaWindowRenderData data)
    {
        VerifyReferences();
        data = CreateRenderData(data);
        lastData = data;

        UILayout.SetSourcePosition(transform as RectTransform, data.Frame.X, data.Frame.Y);

        if (data.Panel)
            RenderDetail(data);
        else
            RenderIndex(data);

        gameObject.SetActive(true);
    }

    public void RequestEntry(ISceneNode target)
    {
        if (target == null)
            return;

        requestedEntryTypeId = GetEntryTypeId(target);
        requestedEntryPending = true;
        stateInitialized = false;
    }

    private static string GetEntryTypeId(ISceneNode target)
    {
        if (target is ResearchMission researchMission)
        {
            return researchMission.Discipline switch
            {
                ResearchDiscipline.ShipDesign => MissionIconKeys.ResearchShipDesign,
                ResearchDiscipline.FacilityDesign => MissionIconKeys.ResearchFacilityDesign,
                ResearchDiscipline.TroopTraining => MissionIconKeys.ResearchTroopTraining,
                _ => target.GetTypeID(),
            };
        }

        return target.GetTypeID();
    }

    internal bool TryConsumeRequestedEntry(out string typeId)
    {
        typeId = null;
        if (!requestedEntryPending)
            return false;

        typeId = requestedEntryTypeId;
        requestedEntryPending = false;
        return true;
    }

    internal static string GetTabText(int tab)
    {
        return tab switch
        {
            0 => "All Databases",
            1 => "System Database",
            2 => "Ship Database",
            3 => "Facilities Database",
            4 => "Missions Database",
            5 => "Troop Database",
            6 => "Personnel Database",
            _ => string.Empty,
        };
    }

    internal bool NeedsIndexScrollbar(int rowCount)
    {
        return rowCount * GetRowPitch() > rowsScrollArea.ViewportHeight;
    }

    internal int GetIndexScrollContentHeight(int rowCount)
    {
        return UILayout.GetSourceRect(rowTextTemplate.rectTransform).y
            + 1
            + rowCount * GetRowPitch();
    }

    internal int GetIndexScrollViewportHeight()
    {
        return Mathf.RoundToInt(rowsScrollArea.ViewportHeight);
    }

    internal int GetIndexScrollStep()
    {
        return GetRowPitch();
    }

    internal bool NeedsDetailScrollbar(int lineCount)
    {
        return lineCount * GetDetailLinePitch() > detailLinesScrollArea.ViewportHeight;
    }

    internal int GetDetailScrollContentHeight(int lineCount)
    {
        return UILayout.GetSourceRect(detailLineTextTemplate.rectTransform).y
            + 4
            + lineCount * GetDetailLinePitch();
    }

    internal int GetDetailScrollViewportHeight()
    {
        return Mathf.RoundToInt(detailLinesScrollArea.ViewportHeight);
    }

    internal int GetDetailScrollStep()
    {
        return Mathf.Max(1, GetDetailLinePitch() - 1);
    }

    internal int GetDetailWrapWidth()
    {
        return UILayout.GetSourceRect(detailLineTextTemplate.rectTransform).width;
    }

    internal int GetDetailWrapFontSize()
    {
        return Mathf.RoundToInt(detailLineTextTemplate.fontSize);
    }

    internal int GetDetailLineCount(string text)
    {
        return BuildDetailLines(text).Count;
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
        detailPreviousButton.onClick.AddListener(ShowPreviousEntry);
        detailNextButton.onClick.AddListener(ShowNextEntry);
    }

    private void BindTabButtons()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null)
                continue;

            int tab = i;
            tabButtons[i].onClick.AddListener(() => SelectTab(tab));
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
        IReadOnlyList<int> buttonActions = GetButtonActions();
        int count = Mathf.Min(buttonSlots.Length, buttonActions.Count);
        for (int i = 0; i < count; i++)
        {
            if (buttonSlots[i] == button)
                return GetButtonAction(buttonActions, i);
        }

        return 0;
    }

    private void ShowPreviousEntry()
    {
        RequestFocus();
        MoveSelection(-1);
    }

    private void ShowNextEntry()
    {
        RequestFocus();
        MoveSelection(1);
    }

    private bool MoveSelection(int direction)
    {
        if (!rowSelection.Move(lastData?.Entries?.Count ?? 0, direction))
            return false;

        RequestRender();
        return true;
    }

    private void HandleRowClicked(EncyclopediaWindowRowView row, PointerEventData eventData)
    {
        RequestFocus();
        rowSelection.SelectOnly(row.Index);
        RequestRender();
    }

    private void HandleRowDoubleClicked(EncyclopediaWindowRowView row, PointerEventData eventData)
    {
        RequestFocus();
        rowSelection.SelectOnly(row.Index);
        panel = true;
        RequestRender();
    }

    private void HandleRowContextRequested(
        EncyclopediaWindowRowView row,
        PointerEventData eventData
    )
    {
        GetWindowShell()?.RequestContext(eventData);
    }

    private void RequestFocus()
    {
        GetWindowShell()?.RequestFocus();
    }

    private void RequestRender()
    {
        uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private UIWindow GetWindowShell()
    {
        if (windowShell == null)
            windowShell = GetComponent<UIWindow>();

        return windowShell;
    }

    private bool CanNavigateRows()
    {
        return GetWindowShell()?.ActiveWindow == true;
    }

    private EncyclopediaWindowRenderData CreateWindowRenderData(
        UIWindow window,
        bool useUpperButtonLayout
    )
    {
        if (TryConsumeRequestedEntry(out string requestedTypeId))
            return CreateRequestedEntryRenderData(window, useUpperButtonLayout, requestedTypeId);

        bool panel = GetPanel(false);
        int activeTab = GetActiveTab(0);
        List<EncyclopediaEntry> rows = GetEntries()
            .GetRows(activeTab, GetPlayerFactionInstanceID());
        if (panel && rows.Count == 0)
            panel = false;
        int selectedIndex = GetSelectedIndex(-1, rows.Count);

        if (panel)
            selectedIndex = Mathf.Clamp(selectedIndex, 0, rows.Count - 1);

        return new EncyclopediaWindowRenderData
        {
            X = window.X,
            Y = window.Y,
            Panel = panel,
            UseUpperButtonLayout = useUpperButtonLayout,
            ActiveTab = activeTab,
            SelectedIndex = selectedIndex,
            Entries = rows,
        };
    }

    private EncyclopediaWindowRenderData CreateRequestedEntryRenderData(
        UIWindow window,
        bool useUpperButtonLayout,
        string requestedTypeId
    )
    {
        List<EncyclopediaEntry> rows = GetEntries().GetRows(0, GetPlayerFactionInstanceID());
        int selectedIndex = FindEntryIndex(rows, requestedTypeId);

        return new EncyclopediaWindowRenderData
        {
            X = window.X,
            Y = window.Y,
            Panel = selectedIndex >= 0,
            UseUpperButtonLayout = useUpperButtonLayout,
            ActiveTab = 0,
            SelectedIndex = selectedIndex,
            Entries = rows,
        };
    }

    private EncyclopediaEntries GetEntries()
    {
        entries ??= ResourceManager.GetData<EncyclopediaEntries>();
        return entries;
    }

    private string GetPlayerFactionInstanceID()
    {
        return uiContext?.GetPlayerFactionInstanceID();
    }

    private static int FindEntryIndex(IReadOnlyList<EncyclopediaEntry> rows, string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
            return -1;

        for (int i = 0; i < rows.Count; i++)
        {
            if (string.Equals(rows[i]?.TypeID, typeId, System.StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private void InitializeState(bool initialPanel, int initialActiveTab, int initialSelectedIndex)
    {
        if (stateInitialized)
            return;

        panel = initialPanel;
        activeTab = Mathf.Max(0, initialActiveTab);
        rowSelection.SelectOnly(initialSelectedIndex);
        stateInitialized = true;
    }

    private void SetActiveTab(int tab)
    {
        if (tab == activeTab)
            return;

        activeTab = tab;
        rowSelection.Clear();
        panel = false;
    }

    private bool ApplyLocalButtonAction(int action)
    {
        switch (action)
        {
            case StrategyDialogButtonActions.EncyclopediaTopic:
                SetPanel(true);
                return true;
            case StrategyDialogButtonActions.EncyclopediaIndex:
                SetPanel(false);
                return true;
            default:
                return false;
        }
    }

    private void SetPanel(bool value)
    {
        panel = value;
    }

    private EncyclopediaWindowRenderData CreateRenderData(EncyclopediaWindowRenderData source)
    {
        InitializeState(
            source?.Panel ?? false,
            source?.ActiveTab ?? 0,
            source?.SelectedIndex ?? -1
        );
        IReadOnlyList<EncyclopediaEntry> renderEntries =
            source?.Entries ?? System.Array.Empty<EncyclopediaEntry>();
        if (panel && renderEntries.Count == 0)
        {
            panel = false;
            rowSelection.Clear();
        }
        rowSelection.ClampToCount(renderEntries.Count);
        if (panel && rowSelection.SelectedIndex < 0 && renderEntries.Count > 0)
            rowSelection.SelectOnly(0);

        RectInt rootRect = UILayout.GetSourceRect(transform as RectTransform);
        EncyclopediaWindowRenderData data = new EncyclopediaWindowRenderData
        {
            X = source?.X ?? 0,
            Y = source?.Y ?? 0,
            Panel = panel,
            UseUpperButtonLayout = source?.UseUpperButtonLayout ?? false,
            ActiveTab = activeTab,
            SelectedIndex = rowSelection.SelectedIndex,
            Entries = renderEntries,
        };
        data.Frame =
            source?.Frame
            ?? new UtilityWindowFrameRenderData
            {
                X = data.X,
                Y = data.Y,
                Width = rootRect.width,
                Height = rootRect.height,
                Panel = data.Panel,
                UseUpperButtonLayout = data.UseUpperButtonLayout,
            };

        if (data.Panel)
            PopulateDetailRenderData(data, renderEntries);
        else
            PopulateIndexRenderData(data, renderEntries);

        return data;
    }

    internal bool GetPanel(bool initialPanel)
    {
        InitializeState(initialPanel, 0, -1);
        return panel;
    }

    internal int GetActiveTab(int initialActiveTab)
    {
        InitializeState(false, initialActiveTab, -1);
        return activeTab;
    }

    internal int GetSelectedIndex(int initialSelectedIndex, int rowCount)
    {
        InitializeState(false, 0, initialSelectedIndex);
        rowSelection.UseInitialSelection(initialSelectedIndex);
        rowSelection.ClampToCount(rowCount);

        return rowSelection.SelectedIndex;
    }

    private void PopulateIndexRenderData(
        EncyclopediaWindowRenderData data,
        IReadOnlyList<EncyclopediaEntry> rows
    )
    {
        data.TabTitle = GetTabText(data.ActiveTab);
        for (int i = 0; i < rows.Count; i++)
        {
            data.Rows.Add(
                new EncyclopediaWindowRowRenderData
                {
                    Name = rows[i].DisplayName,
                    Selected = i == data.SelectedIndex,
                }
            );
        }
    }

    private void PopulateDetailRenderData(
        EncyclopediaWindowRenderData data,
        IReadOnlyList<EncyclopediaEntry> rows
    )
    {
        if (rows.Count == 0)
        {
            data.Panel = false;
            data.Frame.Panel = false;
            PopulateIndexRenderData(data, rows);
            return;
        }

        int selectedIndex = Mathf.Clamp(data.SelectedIndex, 0, rows.Count - 1);
        EncyclopediaEntry entry = rows[selectedIndex];
        data.DetailPreviousDisabled = selectedIndex == 0;
        data.DetailNextDisabled = selectedIndex == rows.Count - 1;
        data.DetailTitle = entry.DisplayName;
        data.DetailImagePath = GetDetailImagePath(entry);
        data.DetailLines.AddRange(BuildDetailLines(entry.GetInfoText()));
    }

    private string GetDetailImagePath(EncyclopediaEntry entry)
    {
        return entry?.ImagePath;
    }

    private void RenderIndex(EncyclopediaWindowRenderData data)
    {
        RenderFrame(data.Frame);
        HideDetail();
        UILayout.SetTextContent(titleTextField, "Galactic Encyclopedia", Color.white);
        UILayout.SetTextContent(topicLabelTextField, "Topic", Color.white);
        RenderTabs(data.ActiveTab);
        UILayout.SetTextContent(tabTitleTextField, data.TabTitle, Color.white);
        RenderRows(data.Rows);
    }

    private void RenderDetail(EncyclopediaWindowRenderData data)
    {
        HideIndex();
        SetImageAtTemplateOrigin(
            detailBackgroundImage,
            topicBackgroundTexture ?? defaultDetailBackgroundTexture
        );
        SetImageAtTemplateOrigin(
            overlayFrameImage,
            GetOverlayFrameTexture() ?? defaultOverlayFrameTexture
        );
        RenderDetailImage(data.DetailImagePath);
        RenderDialogButtons(data.Frame);
        Texture previousUpTexture = GetPreviousButtonTexture(data.DetailPreviousDisabled);
        ConfigureLocalButton(detailPreviousButtonImage, previousUpTexture);
        detailPreviousButtonImage.raycastTarget = !data.DetailPreviousDisabled;
        detailPreviousButton.interactable = !data.DetailPreviousDisabled;
        Texture nextUpTexture = GetNextButtonTexture(data.DetailNextDisabled);
        ConfigureLocalButton(detailNextButtonImage, nextUpTexture);
        detailNextButtonImage.raycastTarget = !data.DetailNextDisabled;
        detailNextButton.interactable = !data.DetailNextDisabled;
        UILayout.SetTextContent(detailTitleTextField, data.DetailTitle, Color.white);
        RenderDetailLines(data.DetailLines);
    }

    private void RenderDetailImage(string imagePath)
    {
        Texture2D texture = GetTexture(imagePath);
        UILayout.SetImageTexture(detailCardImage, texture);
    }

    private void RenderFrame(UtilityWindowFrameRenderData frame)
    {
        SetImageAtTemplateOrigin(backgroundImage, backgroundTexture ?? defaultBackgroundTexture);
        SetImageAtTemplateOrigin(
            overlayFrameImage,
            GetOverlayFrameTexture() ?? defaultOverlayFrameTexture
        );
        RenderDialogButtons(frame);
    }

    private void RenderDialogButtons(UtilityWindowFrameRenderData frame)
    {
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

        IReadOnlyList<int> buttonActions = GetButtonActions();
        HideButtonSlots(upperButtonImages);
        HideButtonSlots(twoButtonImages);
        HideButtonSlots(fourButtonImages);

        RawImage[] buttonSlots = GetButtonSlots(frame);
        int count = Mathf.Min(buttonSlots.Length, buttonActions.Count);
        for (int i = 0; i < count; i++)
        {
            int buttonAction = GetButtonAction(buttonActions, i);
            Texture buttonUpTexture =
                GetDialogButtonTexture(frame, buttonAction, false)
                ?? GetDefaultButtonTexture(buttonSlots, i);
            SetDialogButton(buttonSlots[i], GetButtonComponents(frame), buttonUpTexture, frame, i);
        }
    }

    private void RenderTabs(int activeTab)
    {
        for (int i = 0; i < 7; i++)
        {
            RawImage image = GetTabImage(i);
            Texture texture = GetTabTexture(i, i == activeTab) ?? image.texture;
            UILayout.SetImageTexture(image, texture);
            image.raycastTarget = true;
            if (i < tabButtons.Length && tabButtons[i] != null)
                tabButtons[i].interactable = true;
        }

        if (tabImageSlots != null)
        {
            for (int i = 7; i < tabImageSlots.Length; i++)
            {
                if (tabImageSlots[i] != null)
                    tabImageSlots[i].gameObject.SetActive(false);
            }
        }
    }

    private void RenderRows(IReadOnlyList<EncyclopediaWindowRowRenderData> rows)
    {
        IReadOnlyList<EncyclopediaWindowRowRenderData> safeRows =
            rows ?? System.Array.Empty<EncyclopediaWindowRowRenderData>();
        bool resetScroll = RowsChanged(safeRows);
        RowsList.Render(
            safeRows,
            GetIndexScrollContentHeight(safeRows.Count),
            GetIndexScrollStep(),
            resetScroll,
            GetRowPitch(),
            (rowView, row, index) => rowView.Render(index, row, rowTextTemplate),
            (row, _) => row.Selected
        );

        renderedAnyRows = true;
        renderedActiveTab = activeTab;
        renderedRowNames.Clear();
        for (int i = 0; i < safeRows.Count; i++)
            renderedRowNames.Add(safeRows[i].Name ?? string.Empty);
    }

    private void RenderDetailLines(IReadOnlyList<string> lines)
    {
        detailLinesScrollArea.gameObject.SetActive(true);
        IReadOnlyList<string> safeLines = lines ?? System.Array.Empty<string>();
        bool resetScroll = DetailLinesChanged(safeLines);
        detailLinesScrollArea.SetContentHeight(
            GetDetailScrollContentHeight(safeLines.Count),
            GetDetailScrollStep(),
            resetScroll
        );
        RectInt template = UILayout.GetSourceRect(detailLineTextTemplate.rectTransform);
        int linePitch = GetDetailLinePitch();
        List<(int indentTabs, string[] cells, bool isTabbed)> rows = BuildDetailTextRows(safeLines);
        int[] columnX = BuildDetailColumnPositions(rows);
        int textIndex = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            (int indentTabs, string[] cells, bool isTabbed) = rows[i];
            int y = template.y + i * linePitch;
            if (isTabbed)
            {
                int indentX = indentTabs * 25;
                for (int c = 0; c < cells.Length; c++)
                {
                    string text = cells[c];
                    if (string.IsNullOrEmpty(text))
                        continue;

                    int x = c == 0 ? template.x + indentX : template.x + columnX[c];
                    RenderDetailTextCell(textIndex++, text, x, y, template);
                }
            }
            else
            {
                string text = cells.Length == 0 ? string.Empty : cells[0];
                if (!string.IsNullOrEmpty(text))
                {
                    int x = template.x + indentTabs * 25;
                    RenderDetailTextCell(textIndex++, text, x, y, template);
                }
            }
        }

        for (int i = textIndex; i < detailLineTextFields.Count; i++)
            detailLineTextFields[i].gameObject.SetActive(false);

        renderedAnyDetailLines = true;
        renderedDetailSelectedIndex = rowSelection.SelectedIndex;
        renderedDetailLines.Clear();
        renderedDetailLines.AddRange(safeLines);
    }

    private void RenderDetailTextCell(int index, string value, int x, int y, RectInt template)
    {
        TextMeshProUGUI text = GetDetailLineTextField(index);
        int width = Mathf.Max(1, template.x + template.width - x);
        UILayout.SetTemplateText(
            text,
            detailLineTextTemplate,
            value,
            Color.white,
            new RectInt(x, y, width, template.height)
        );
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private List<(int indentTabs, string[] cells, bool isTabbed)> BuildDetailTextRows(
        IReadOnlyList<string> lines
    )
    {
        List<(int indentTabs, string[] cells, bool isTabbed)> rows =
            new List<(int indentTabs, string[] cells, bool isTabbed)>();
        for (int i = 0; i < lines.Count; i++)
        {
            string rawLine = TrimDetailLine(lines[i]);
            if (rawLine.Length == 0)
            {
                rows.Add((0, System.Array.Empty<string>(), false));
                continue;
            }

            int indentTabs = CountLeadingTabs(rawLine);
            string afterIndent = rawLine.Substring(indentTabs);
            bool hasTabs = afterIndent.IndexOf('\t') >= 0;
            rows.Add(
                hasTabs
                    ? (indentTabs, SplitTabbedCells(afterIndent), true)
                    : (indentTabs, new[] { afterIndent }, false)
            );
        }

        return rows;
    }

    private int[] BuildDetailColumnPositions(
        IReadOnlyList<(int indentTabs, string[] cells, bool isTabbed)> rows
    )
    {
        int maxColumns = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].isTabbed && rows[i].cells.Length > maxColumns)
                maxColumns = rows[i].cells.Length;
        }

        int[] columnWidths = new int[maxColumns];
        for (int r = 0; r < rows.Count; r++)
        {
            if (!rows[r].isTabbed)
                continue;

            string[] cells = rows[r].cells;
            for (int c = 0; c < cells.Length; c++)
            {
                int width = Mathf.CeilToInt(GetDetailTextWidth(cells[c]));
                if (width > columnWidths[c])
                    columnWidths[c] = width;
            }
        }

        for (int c = 0; c < columnWidths.Length; c++)
            columnWidths[c] += 20;

        int[] columnX = new int[maxColumns];
        for (int c = 1; c < columnX.Length; c++)
            columnX[c] = columnX[c - 1] + columnWidths[c - 1];

        return columnX;
    }

    private static string TrimDetailLine(string line)
    {
        return (line ?? string.Empty).Trim().Trim('\0');
    }

    private static int CountLeadingTabs(string text)
    {
        int count = 0;
        while (count < text.Length && text[count] == '\t')
            count++;
        return count;
    }

    private static string[] SplitTabbedCells(string text)
    {
        List<string> cells = new List<string>();
        int start = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != '\t')
                continue;

            if (i > start)
                cells.Add(text.Substring(start, i - start));

            while (i + 1 < text.Length && text[i + 1] == '\t')
                i++;
            start = i + 1;
        }

        return cells.ToArray();
    }

    private void HideIndex()
    {
        backgroundImage.gameObject.SetActive(false);
        buttonStripImage.gameObject.SetActive(false);
        HideButtonSlots(upperButtonImages);
        HideButtonSlots(twoButtonImages);
        HideButtonSlots(fourButtonImages);
        titleTextField.gameObject.SetActive(false);
        topicLabelTextField.gameObject.SetActive(false);
        HideTabs();
        tabTitleTextField.gameObject.SetActive(false);
        rowsScrollArea.gameObject.SetActive(false);
        RowsList.Hide();
    }

    private void HideDetail()
    {
        detailBackgroundImage.gameObject.SetActive(false);
        detailCardImage.gameObject.SetActive(false);
        detailPreviousButtonImage.gameObject.SetActive(false);
        detailNextButtonImage.gameObject.SetActive(false);
        detailTitleTextField.gameObject.SetActive(false);
        detailLinesScrollArea.gameObject.SetActive(false);
        for (int i = 0; i < detailLineTextFields.Count; i++)
            detailLineTextFields[i].gameObject.SetActive(false);
    }

    private void HideTabs()
    {
        if (tabImageSlots == null)
            return;

        for (int i = 0; i < tabImageSlots.Length; i++)
        {
            if (tabImageSlots[i] != null)
                tabImageSlots[i].gameObject.SetActive(false);
        }
    }

    private RawImage GetTabImage(int index)
    {
        if (tabImageSlots != null && index >= 0 && index < tabImageSlots.Length)
        {
            RawImage slot = tabImageSlots[index];
            if (slot != null)
                return slot;
        }

        throw new MissingReferenceException($"{name}/TabImage{index} is missing.");
    }

    private TextMeshProUGUI GetDetailLineTextField(int index)
    {
        while (detailLineTextFields.Count <= index)
        {
            TextMeshProUGUI text = Instantiate(
                detailLineTextTemplate,
                detailLinesScrollArea.ContentRoot
            );
            text.name = $"DetailLineTextField{detailLineTextFields.Count}";
            detailLineTextFields.Add(text);
        }

        return detailLineTextFields[index];
    }

    private int GetRowPitch()
    {
        RectInt rowText = UILayout.GetSourceRect(rowTextTemplate.rectTransform);
        return rowText.y + rowText.height;
    }

    private int GetDetailLinePitch()
    {
        return UILayout.GetSourceRect(detailLineTextTemplate.rectTransform).height;
    }

    private RawImage[] GetButtonSlots(UtilityWindowFrameRenderData frame)
    {
        if (frame.UseUpperButtonLayout)
            return upperButtonImages;

        return fourButtonImages;
    }

    private Button[] GetButtonComponents(UtilityWindowFrameRenderData frame)
    {
        if (frame.UseUpperButtonLayout)
            return upperButtons;

        return fourButtons;
    }

    private static IReadOnlyList<int> GetButtonActions()
    {
        return encyclopediaButtonActions;
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

    private static void SetDialogButton(
        RawImage image,
        Button[] buttons,
        Texture texture,
        UtilityWindowFrameRenderData frame,
        int index
    )
    {
        if (image == null)
            return;
        if (texture == null)
        {
            image.gameObject.SetActive(false);
            if (buttons != null && index >= 0 && index < buttons.Length && buttons[index] != null)
                buttons[index].interactable = false;
            return;
        }

        UILayout.SetImage(
            image,
            texture,
            frame.Width - texture.width - GetDialogButtonSideOffset(frame),
            GetDialogButtonY(frame, index)
        );
        image.raycastTarget = true;
        if (buttons != null && index >= 0 && index < buttons.Length && buttons[index] != null)
            buttons[index].interactable = true;
    }

    private static int GetDialogButtonSideOffset(UtilityWindowFrameRenderData frame)
    {
        return frame.UseUpperButtonLayout ? 0 : 15;
    }

    private static int GetDialogButtonY(UtilityWindowFrameRenderData frame, int index)
    {
        if (frame.UseUpperButtonLayout)
            return index switch
            {
                0 => 21,
                1 => 89,
                2 => 143,
                _ => 21,
            };

        return index switch
        {
            0 => 25,
            1 => 93,
            2 => 147,
            _ => 25,
        };
    }

    private static void SetImageAtTemplateOrigin(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    private Texture2D GetTabTexture(int tab, bool active)
    {
        EncyclopediaWindowTheme theme = GetEncyclopediaTheme();
        return tab switch
        {
            0 => (active ? allSystemsButtonDownTexture : allSystemsButtonUpTexture)
                ?? GetStrategyViewButtonTexture(
                    "ui_strategyview_encyclopedia_window_all_systems_button",
                    active
                ),
            1 => (active ? systemButtonDownTexture : systemButtonUpTexture)
                ?? GetStrategyViewButtonTexture(
                    "ui_strategyview_encyclopedia_window_system_button",
                    active
                ),
            2 => GetThemedButtonTexture(theme?.ShipButton, active),
            3 => GetThemedButtonTexture(theme?.FacilityButton, active),
            4 => GetThemedButtonTexture(theme?.MissionsButton, active),
            5 => GetThemedButtonTexture(theme?.TroopButton, active),
            6 => GetThemedButtonTexture(theme?.PersonnelButton, active),
            _ => null,
        };
    }

    private Texture2D GetPreviousButtonTexture(bool disabled)
    {
        if (disabled)
            return previousButtonDisabledTexture;

        return previousButtonUpTexture;
    }

    private Texture2D GetNextButtonTexture(bool disabled)
    {
        if (disabled)
            return nextButtonDisabledTexture;

        return nextButtonUpTexture;
    }

    private Texture2D GetOverlayFrameTexture()
    {
        return GetTexture(GetEncyclopediaTheme()?.OverlayFrameImagePath);
    }

    private Texture2D GetButtonStripTexture(UtilityWindowFrameRenderData frame)
    {
        if (frame.UseUpperButtonLayout)
            return null;

        return GetTexture(GetEncyclopediaTheme()?.ButtonStripImagePath);
    }

    private Texture2D GetDialogButtonTexture(
        UtilityWindowFrameRenderData frame,
        int action,
        bool forcePressed
    )
    {
        if (action == 0)
            return null;

        bool pressed = forcePressed || IsDialogButtonActive(frame, action);
        EncyclopediaWindowTheme theme = GetEncyclopediaTheme();
        return action switch
        {
            StrategyDialogButtonActions.Close => GetThemedButtonTexture(
                theme?.CloseButton,
                pressed
            ),
            StrategyDialogButtonActions.EncyclopediaTopic => GetThemedButtonTexture(
                theme?.TopicButton,
                pressed
            ),
            StrategyDialogButtonActions.EncyclopediaIndex => GetThemedButtonTexture(
                theme?.IndexButton,
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

    private EncyclopediaWindowTheme GetEncyclopediaTheme()
    {
        return uiContext?.GetPlayerFactionTheme()?.StrategyWindows?.Encyclopedia;
    }

    private static bool IsDialogButtonActive(UtilityWindowFrameRenderData frame, int action)
    {
        return action switch
        {
            StrategyDialogButtonActions.EncyclopediaTopic => frame.Panel,
            StrategyDialogButtonActions.EncyclopediaIndex => !frame.Panel,
            _ => false,
        };
    }

    private List<string> BuildDetailLines(string text)
    {
        return WrapDetailText(text, GetDetailWrapWidth());
    }

    private List<string> WrapDetailText(string text, int width)
    {
        List<string> lines = new List<string>();
        if (string.IsNullOrEmpty(text))
            return lines;

        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] sourceLines = normalized.Split('\n');

        foreach (string sourceLine in sourceLines)
            AddMeasuredWrappedSourceLine(lines, sourceLine, width);

        return lines;
    }

    private void AddMeasuredWrappedSourceLine(List<string> lines, string sourceLine, int width)
    {
        sourceLine ??= string.Empty;
        if (sourceLine.IndexOf('\t') >= 0)
        {
            lines.Add(sourceLine);
            return;
        }

        if (GetDetailTextWidth(sourceLine) <= width)
        {
            lines.Add(sourceLine);
            return;
        }

        string[] words = sourceLine.Split(' ');
        if (words.Length == 0)
        {
            lines.Add(string.Empty);
            return;
        }

        string line = words[0];
        for (int i = 1; i < words.Length; i++)
        {
            string word = words[i];
            string next = line.Length == 0 ? word : line + " " + word;
            if (GetDetailTextWidth(next) > width)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = next;
            }
        }

        if (line.Length > 0)
            lines.Add(line);
    }

    private float GetDetailTextWidth(string text)
    {
        return detailLineTextTemplate.GetPreferredValues(text ?? string.Empty).x;
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
        if (topicLabelTextField == null)
            throw new MissingReferenceException($"{name}/TopicLabelTextField is missing.");
        if (tabImageSlots == null || tabImageSlots.Length == 0)
            throw new MissingReferenceException($"{name}/TabImageSlots are missing.");
        if (tabButtons == null || tabButtons.Length == 0)
            throw new MissingReferenceException($"{name}/TabButtons are missing.");
        if (tabTitleTextField == null)
            throw new MissingReferenceException($"{name}/TabTitleTextField is missing.");
        if (rowsScrollArea == null)
            throw new MissingReferenceException($"{name}/RowsScrollArea is missing.");
        if (rowTemplate == null)
            throw new MissingReferenceException($"{name}/RowTemplate is missing.");
        if (rowTextTemplate == null)
            throw new MissingReferenceException($"{name}/RowTextTemplate is missing.");
        if (detailBackgroundImage == null)
            throw new MissingReferenceException($"{name}/DetailBackgroundImage is missing.");
        if (detailCardImage == null)
            throw new MissingReferenceException($"{name}/DetailCardImage is missing.");
        if (detailPreviousButtonImage == null)
            throw new MissingReferenceException($"{name}/DetailPreviousButtonImage is missing.");
        if (detailPreviousButton == null)
            throw new MissingReferenceException($"{name}/DetailPreviousButton is missing.");
        if (detailNextButtonImage == null)
            throw new MissingReferenceException($"{name}/DetailNextButtonImage is missing.");
        if (detailNextButton == null)
            throw new MissingReferenceException($"{name}/DetailNextButton is missing.");
        if (detailTitleTextField == null)
            throw new MissingReferenceException($"{name}/DetailTitleTextField is missing.");
        if (detailLinesScrollArea == null)
            throw new MissingReferenceException($"{name}/DetailLinesScrollArea is missing.");
        if (detailLineTextTemplate == null)
            throw new MissingReferenceException($"{name}/DetailLineTextTemplate is missing.");

        rowTemplate.gameObject.SetActive(false);
        rowTextTemplate.gameObject.SetActive(false);
        detailLineTextTemplate.gameObject.SetActive(false);
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
        defaultDetailBackgroundTexture = detailBackgroundImage.texture;
        defaultOverlayFrameTexture = overlayFrameImage.texture;
        defaultButtonStripTexture = buttonStripImage.texture;
        defaultUpperButtonTextures = CaptureDefaultTextures(upperButtonImages);
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
            buttonSlots == upperButtonImages
                ? defaultUpperButtonTextures
                : defaultFourButtonTextures;
        return index >= 0 && index < textures.Length ? textures[index] : null;
    }

    private bool RowsChanged(IReadOnlyList<EncyclopediaWindowRowRenderData> rows)
    {
        if (
            !renderedAnyRows
            || renderedActiveTab != activeTab
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

    private bool DetailLinesChanged(IReadOnlyList<string> lines)
    {
        if (
            !renderedAnyDetailLines
            || renderedDetailSelectedIndex != rowSelection.SelectedIndex
            || renderedDetailLines.Count != lines.Count
        )
            return true;

        for (int i = 0; i < lines.Count; i++)
        {
            if (renderedDetailLines[i] != lines[i])
                return true;
        }

        return false;
    }

    private static void ConfigureLocalButton(RawImage image, Texture upTexture)
    {
        UILayout.SetInteractiveImageTexture(image, upTexture);
    }

    private SelectableListView<EncyclopediaWindowRowView, EncyclopediaWindowRowRenderData> RowsList
    {
        get
        {
            rowsList ??= new SelectableListView<
                EncyclopediaWindowRowView,
                EncyclopediaWindowRowRenderData
            >(
                rowsScrollArea,
                rowTemplate,
                "EncyclopediaRow",
                HandleRowClicked,
                HandleRowDoubleClicked,
                HandleRowContextRequested,
                CanNavigateRows,
                transform
            );
            return rowsList;
        }
    }
}

public sealed class EncyclopediaWindowRenderData
{
    public int X;
    public int Y;
    public bool Panel;
    public bool UseUpperButtonLayout;
    public int ActiveTab;
    public int SelectedIndex;
    public string TabTitle;
    public string DetailTitle;
    public string DetailImagePath;
    public UtilityWindowFrameRenderData Frame;
    public bool DetailPreviousDisabled;
    public bool DetailNextDisabled;
    public IReadOnlyList<EncyclopediaEntry> Entries = System.Array.Empty<EncyclopediaEntry>();
    public List<EncyclopediaWindowRowRenderData> Rows = new List<EncyclopediaWindowRowRenderData>();
    public List<string> DetailLines = new List<string>();
}

public sealed class EncyclopediaWindowRowRenderData
{
    public string Name;
    public bool Selected;
}
