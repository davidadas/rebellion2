using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MessagesWindowView
    : MonoBehaviour,
        IStrategyUIContextReceiver,
        IStrategyWindowContent
{
    private const int _chatTab = 8;

    private readonly List<TextMeshProUGUI> detailLineTextFields = new List<TextMeshProUGUI>();
    private readonly List<int> renderedRowIndexes = new List<int>();
    private readonly List<string> renderedDetailLines = new List<string>();
    private readonly SelectableListSelection rowSelection = new SelectableListSelection();

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage overlayFrameImage;

    [SerializeField]
    private RawImage buttonStripImage;

    [SerializeField]
    private RawImage closeButtonImage;

    [SerializeField]
    private RawImage displayButtonImage;

    [SerializeField]
    private RawImage indexButtonImage;

    [SerializeField]
    private RawImage signalButtonImage;

    [SerializeField]
    private RawImage signalTargetButtonImage;

    [SerializeField]
    private RawImage chatCommandButtonImage;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private Button displayButton;

    [SerializeField]
    private Button indexButton;

    [SerializeField]
    private Button signalButton;

    [SerializeField]
    private Button signalTargetButton;

    [SerializeField]
    private Button chatCommandButton;

    [SerializeField]
    private RawImage[] tabImageSlots = System.Array.Empty<RawImage>();

    [SerializeField]
    private Button[] tabButtons = System.Array.Empty<Button>();

    [SerializeField]
    private TextMeshProUGUI tabTitleTextField;

    [SerializeField]
    private ScrollAreaView rowsScrollArea;

    [SerializeField]
    private MessageWindowRowView rowTemplate;

    [SerializeField]
    private RawImage selectAllButtonImage;

    [SerializeField]
    private RawImage removeSelectedButtonImage;

    [SerializeField]
    private Button selectAllButton;

    [SerializeField]
    private Button removeSelectedButton;

    [SerializeField]
    private RawImage detailStripImage;

    [SerializeField]
    private RawImage detailBodyImage;

    [SerializeField]
    private RawImage detailCardImage;

    [SerializeField]
    private RawImage detailOverlayImage;

    [SerializeField]
    private TextMeshProUGUI detailHeaderTextField;

    [SerializeField]
    private RawImage detailNextButtonImage;

    [SerializeField]
    private RawImage detailPreviousButtonImage;

    [SerializeField]
    private Button detailNextButton;

    [SerializeField]
    private Button detailPreviousButton;

    [SerializeField]
    private ScrollAreaView detailLinesScrollArea;

    [SerializeField]
    private TextMeshProUGUI detailLineTextTemplate;

    [SerializeField]
    private Texture2D allButtonUpTexture;

    [SerializeField]
    private Texture2D allButtonDownTexture;

    [SerializeField]
    private Texture2D resourceButtonUpTexture;

    [SerializeField]
    private Texture2D resourceButtonDownTexture;

    [SerializeField]
    private Texture2D chatButtonUpTexture;

    [SerializeField]
    private Texture2D chatButtonDownTexture;

    [SerializeField]
    private Texture2D manufacturingButtonUpTexture;

    [SerializeField]
    private Texture2D manufacturingButtonDownTexture;

    [SerializeField]
    private Texture2D conflictButtonUpTexture;

    [SerializeField]
    private Texture2D conflictButtonDownTexture;

    [SerializeField]
    private Texture2D defenseButtonUpTexture;

    [SerializeField]
    private Texture2D defenseButtonDownTexture;

    [SerializeField]
    private Texture2D selectAllButtonUpTexture;

    [SerializeField]
    private Texture2D selectAllButtonDownTexture;

    [SerializeField]
    private Texture2D removeSelectedButtonUpTexture;

    [SerializeField]
    private Texture2D removeSelectedButtonDownTexture;

    [SerializeField]
    private Texture2D detailStripTexture;

    [SerializeField]
    private Texture2D detailBodyTexture;

    [SerializeField]
    private Texture2D previousButtonUpTexture;

    [SerializeField]
    private Texture2D previousButtonDownTexture;

    [SerializeField]
    private Texture2D previousButtonDisabledTexture;

    [SerializeField]
    private Texture2D nextButtonUpTexture;

    [SerializeField]
    private Texture2D nextButtonDownTexture;

    [SerializeField]
    private Texture2D nextButtonDisabledTexture;

    [SerializeField]
    private Texture2D resourceIconTexture;

    [SerializeField]
    private Texture2D conflictIconTexture;

    [SerializeField]
    private Texture2D backgroundTexture;

    private Texture defaultButtonStripTexture;
    private Texture defaultCloseButtonTexture;
    private Texture defaultDisplayButtonTexture;
    private Texture defaultIndexButtonTexture;
    private Texture defaultSignalButtonTexture;
    private Texture defaultSignalTargetButtonTexture;
    private Texture defaultChatCommandButtonTexture;
    private RectInt detailCardTemplateRect;
    private RectInt detailOverlayTemplateRect;
    private MessagesWindowRenderData lastData;
    private SelectableListView<MessageWindowRowView, MessageWindowRowRenderData> rowsList;

    private UIContext uiContext;
    private bool stateInitialized;
    private bool panel;
    private int activeTab;
    private int renderedActiveTab = -1;
    private bool renderedAnyRows;
    private int renderedDetailSelectedIndex = -1;
    private bool renderedAnyDetailLines;
    private UIWindow windowShell;

    public void Initialize(UIContext uiContext)
    {
        this.uiContext = uiContext;
    }

    public void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active)
    {
        if (window == null)
            return;

        bool currentPanel = GetPanel(false);
        int currentActiveTab = GetActiveTab(0);
        List<Message> messages = GetRows(context.PlayerFaction, currentActiveTab);
        if (currentPanel && messages.Count == 0)
            currentPanel = false;

        int currentSelectedIndex = GetSelectedIndex(-1, messages.Count);
        if (currentPanel)
            currentSelectedIndex = Mathf.Clamp(currentSelectedIndex, 0, messages.Count - 1);

        Render(
            new MessagesWindowRenderData
            {
                X = window.X,
                Y = window.Y,
                Panel = currentPanel,
                UseUpperButtonLayout = context.UseUpperButtonLayout,
                ActiveTab = currentActiveTab,
                SelectedIndex = currentSelectedIndex,
                SelectedItems = GetSelectedItems(),
                SourceMessages = messages,
            }
        );
    }

    public void Render(MessagesWindowRenderData data)
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

    internal static MessageType? GetMessageTypeForTab(int tab)
    {
        return tab switch
        {
            1 => MessageType.PopularSupport,
            2 => MessageType.Fleet,
            3 => MessageType.Mission,
            4 => MessageType.Resource,
            5 => MessageType.Manufacturing,
            6 => MessageType.Defense,
            7 => MessageType.Conflict,
            8 => MessageType.Chat,
            9 => MessageType.Advice,
            _ => null,
        };
    }

    internal static List<Message> GetRows(Faction faction, int tab)
    {
        if (faction == null)
            return new List<Message>();

        if (tab == 0)
            return faction.Messages.SelectMany(entry => entry.Value).ToList();

        MessageType? type = GetMessageTypeForTab(tab);
        if (!type.HasValue || !faction.Messages.TryGetValue(type.Value, out List<Message> messages))
            return new List<Message>();

        return messages;
    }

    internal static string GetTabTitle(int tab)
    {
        return tab switch
        {
            0 => "All Messages",
            1 => "Popular Support Messages",
            2 => "Fleet Messages",
            3 => "Mission Messages",
            4 => "Resource Messages",
            5 => "Manufacturing Messages",
            6 => "Defense Messages",
            7 => "Conflict Messages",
            8 => "Chat Messages",
            9 => "Advice Messages",
            _ => string.Empty,
        };
    }

    internal static string GetHeader(Message message)
    {
        if (message == null)
            return string.Empty;

        string header = !string.IsNullOrEmpty(message.Title) ? message.Title : message.Text;
        if (string.IsNullOrEmpty(header))
            return string.Empty;

        int lineBreak = header.IndexOf('\n', System.StringComparison.Ordinal);
        return lineBreak >= 0 ? header.Substring(0, lineBreak) : header;
    }

    internal bool NeedsIndexScrollbar(int rowCount)
    {
        return rowCount * GetIndexRowPitch() > rowsScrollArea.ViewportHeight;
    }

    internal int GetIndexScrollContentHeight(int rowCount)
    {
        return 5 + rowCount * GetIndexRowPitch();
    }

    internal int GetIndexScrollViewportHeight()
    {
        return Mathf.RoundToInt(rowsScrollArea.ViewportHeight);
    }

    internal int GetIndexScrollStep()
    {
        return GetIndexRowPitch();
    }

    internal int GetDetailWrapWidth()
    {
        return UILayout.GetSourceRect(detailLineTextTemplate.rectTransform).width;
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
        return GetDetailLinePitch();
    }

    internal int GetDetailLineCount(string text)
    {
        return BuildDetailLines(text).Count;
    }

    private void Awake()
    {
        VerifyReferences();
        OrderDetailLayers();
        CaptureDefaultTextures();
        CaptureDefaultRects();
        BindControls();
    }

    private void OrderDetailLayers()
    {
        detailCardImage.transform.SetAsLastSibling();
        detailOverlayImage.transform.SetAsLastSibling();
        detailStripImage.transform.SetAsLastSibling();
        detailBodyImage.transform.SetAsLastSibling();
        detailLinesScrollArea.transform.SetAsLastSibling();
        detailHeaderTextField.transform.SetAsLastSibling();
        detailNextButtonImage.transform.SetAsLastSibling();
        detailPreviousButtonImage.transform.SetAsLastSibling();
        overlayFrameImage.transform.SetAsLastSibling();
        buttonStripImage.transform.SetAsLastSibling();
        closeButtonImage.transform.parent.SetAsLastSibling();
    }

    private void BindControls()
    {
        BindTabButtons();
        closeButton.onClick.AddListener(CloseWindow);
        displayButton.onClick.AddListener(ShowSelectedMessage);
        indexButton.onClick.AddListener(ShowMessageIndex);
        chatCommandButton.onClick.AddListener(ShowChatMessages);
        selectAllButton.onClick.AddListener(SelectAllMessages);
        removeSelectedButton.onClick.AddListener(RemoveSelectedMessages);
        detailNextButton.onClick.AddListener(ShowNextMessage);
        detailPreviousButton.onClick.AddListener(ShowPreviousMessage);
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

    private void SelectTab(int tab)
    {
        RequestFocus();
        SetActiveTab(tab);
        RequestRender();
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

    private void SelectAllMessages()
    {
        RequestFocus();
        rowSelection.SelectAll(lastData?.SourceMessages?.Count ?? 0);
        RequestRender();
    }

    private void RemoveSelectedMessages()
    {
        RequestFocus();
        RequestRender();
    }

    private void CloseWindow()
    {
        RequestFocus();
        UIWindow shell = GetWindowShell();
        if (shell != null)
            uiContext?.Dispatcher.Send(new StrategyUIRequests.CloseWindow(shell.Id));
    }

    private void ShowSelectedMessage()
    {
        RequestFocus();
        IReadOnlyList<Message> messages = lastData?.SourceMessages ?? System.Array.Empty<Message>();
        if (messages.Count == 0)
            return;

        if (rowSelection.SelectedIndex < 0 || rowSelection.SelectedIndex >= messages.Count)
            rowSelection.SelectOnly(messages.Count - 1);

        panel = true;
        RequestRender();
    }

    private void ShowMessageIndex()
    {
        RequestFocus();
        panel = false;
        RequestRender();
    }

    private void ShowChatMessages()
    {
        SelectTab(_chatTab);
    }

    private void ShowPreviousMessage()
    {
        RequestFocus();
        if (rowSelection.SelectedIndex > 0)
            rowSelection.SelectOnly(rowSelection.SelectedIndex - 1);
        RequestRender();
    }

    private void ShowNextMessage()
    {
        RequestFocus();
        if (
            lastData?.SourceMessages != null
            && rowSelection.SelectedIndex < lastData.SourceMessages.Count - 1
        )
            rowSelection.SelectOnly(rowSelection.SelectedIndex + 1);
        RequestRender();
    }

    private void HandleRowClicked(MessageWindowRowView row, PointerEventData eventData)
    {
        RequestFocus();
        rowSelection.SelectOnly(row.Index);
        RequestRender();
    }

    private void HandleRowDoubleClicked(MessageWindowRowView row, PointerEventData eventData)
    {
        RequestFocus();
        rowSelection.SelectOnly(row.Index);
        panel = true;
        RequestRender();
    }

    private void HandleRowContextRequested(MessageWindowRowView row, PointerEventData eventData)
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

    private MessagesWindowRenderData CreateRenderData(MessagesWindowRenderData source)
    {
        InitializeState(
            source?.Panel ?? false,
            source?.ActiveTab ?? 0,
            source?.SelectedIndex ?? -1
        );
        IReadOnlyList<Message> sourceMessages =
            source?.SourceMessages ?? System.Array.Empty<Message>();
        if (panel && sourceMessages.Count == 0)
        {
            panel = false;
            rowSelection.Clear();
        }
        rowSelection.ClampToCount(sourceMessages.Count);
        if (panel && rowSelection.SelectedIndex < 0 && sourceMessages.Count > 0)
            rowSelection.SelectOnly(0);

        RectInt rootRect = UILayout.GetSourceRect(transform as RectTransform);
        MessagesWindowRenderData data = new MessagesWindowRenderData
        {
            X = source?.X ?? 0,
            Y = source?.Y ?? 0,
            Panel = panel,
            UseUpperButtonLayout = source?.UseUpperButtonLayout ?? false,
            ActiveTab = activeTab,
            SelectedIndex = rowSelection.SelectedIndex,
            SelectedItems = rowSelection.SelectedIndexes,
            SourceMessages = sourceMessages,
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
            PopulateDetailRenderData(data, sourceMessages);
        else
            PopulateIndexRenderData(data, sourceMessages);

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

    internal IReadOnlyCollection<int> GetSelectedItems()
    {
        return rowSelection.SelectedIndexes;
    }

    internal void OpenTab(int tab)
    {
        InitializeState(false, tab, -1);
        activeTab = Mathf.Clamp(tab, 0, 9);
        rowSelection.Clear();
        panel = false;
    }

    private void PopulateIndexRenderData(
        MessagesWindowRenderData data,
        IReadOnlyList<Message> messages
    )
    {
        data.TabTitle = GetTabTitle(data.ActiveTab);
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            Message message = messages[i];
            data.Rows.Add(
                new MessageWindowRowRenderData
                {
                    Index = i,
                    Header = GetHeader(message),
                    Type = message.Type,
                    Selected = ContainsIndex(data.SelectedItems, i),
                }
            );
        }
    }

    private void PopulateDetailRenderData(
        MessagesWindowRenderData data,
        IReadOnlyList<Message> messages
    )
    {
        if (messages.Count == 0)
        {
            data.Panel = false;
            data.Frame.Panel = false;
            PopulateIndexRenderData(data, messages);
            return;
        }

        int selectedIndex = Mathf.Clamp(data.SelectedIndex, 0, messages.Count - 1);
        Message message = messages[selectedIndex];
        data.DetailType = message.Type;
        data.DetailHeader = GetHeader(message);
        data.DetailImagePath = GetDetailImagePath(message);
        data.DetailOverlayImagePath = message.OverlayImagePath;
        data.DetailNextDisabled = selectedIndex == messages.Count - 1;
        data.DetailPreviousDisabled = selectedIndex == 0;
        data.DetailLines.AddRange(BuildDetailLines(message.GetText()));
    }

    private void RenderIndex(MessagesWindowRenderData data)
    {
        SetImageAtTemplateOrigin(backgroundImage, backgroundTexture);
        SetImageAtTemplateOrigin(overlayFrameImage, GetOverlayFrameTexture());
        RenderCommandButtons(data);
        HideDetail();
        RenderTabs(data.ActiveTab);
        UILayout.SetTextContent(tabTitleTextField, data.TabTitle, Color.white);
        RenderRows(data.Rows);
        SetButtonVisual(selectAllButtonImage, selectAllButton, selectAllButtonUpTexture, true);
        SetButtonVisual(
            removeSelectedButtonImage,
            removeSelectedButton,
            removeSelectedButtonUpTexture,
            true
        );
    }

    private void RenderDetail(MessagesWindowRenderData data)
    {
        HideIndex();
        OrderDetailLayers();
        SetImageAtTemplateOrigin(detailStripImage, detailStripTexture);
        RenderDetailCard(data.DetailImagePath);
        RenderDetailOverlay(data.DetailOverlayImagePath);
        SetImageAtTemplateOrigin(detailBodyImage, detailBodyTexture);
        SetImageAtTemplateOrigin(overlayFrameImage, GetOverlayFrameTexture());
        RenderCommandButtons(data);
        UILayout.SetTextContent(detailHeaderTextField, data.DetailHeader, Color.white);
        SetButtonVisual(
            detailNextButtonImage,
            detailNextButton,
            GetNextButtonTexture(data.DetailNextDisabled),
            !data.DetailNextDisabled
        );
        SetButtonVisual(
            detailPreviousButtonImage,
            detailPreviousButton,
            GetPreviousButtonTexture(data.DetailPreviousDisabled),
            !data.DetailPreviousDisabled
        );
        RenderDetailLines(data.DetailLines);
    }

    private void RenderCommandButtons(MessagesWindowRenderData data)
    {
        MessagesWindowTheme theme = GetMessagesTheme();
        UILayout.SetImageTexture(
            buttonStripImage,
            uiContext?.GetTexture(theme?.ButtonStripImagePath) ?? defaultButtonStripTexture
        );
        RenderCommandButton(
            closeButtonImage,
            closeButton,
            theme?.CloseButton,
            true,
            false,
            defaultCloseButtonTexture
        );
        if (data.Panel)
        {
            HideCommandButton(displayButtonImage, displayButton);
            RenderCommandButton(
                indexButtonImage,
                indexButton,
                theme?.IndexButton,
                true,
                false,
                defaultIndexButtonTexture
            );
        }
        else
        {
            RenderCommandButton(
                displayButtonImage,
                displayButton,
                theme?.DisplayButton,
                data.SourceMessages.Count > 0,
                false,
                defaultDisplayButtonTexture
            );
            HideCommandButton(indexButtonImage, indexButton);
        }

        RenderCommandButton(
            signalButtonImage,
            signalButton,
            theme?.SignalButton,
            false,
            false,
            defaultSignalButtonTexture
        );
        RenderCommandButton(
            signalTargetButtonImage,
            signalTargetButton,
            theme?.SignalTargetButton,
            false,
            false,
            defaultSignalTargetButtonTexture
        );
        RenderCommandButton(
            chatCommandButtonImage,
            chatCommandButton,
            theme?.ChatCommandButton,
            data.ActiveTab != _chatTab || data.Panel,
            data.ActiveTab == _chatTab && !data.Panel,
            defaultChatCommandButtonTexture
        );
    }

    private void RenderTabs(int activeTab)
    {
        for (int i = 0; i < 10; i++)
        {
            RawImage image = GetTabImage(i);
            UILayout.SetImageTexture(image, GetTabTexture(i, i == activeTab));
            image.raycastTarget = true;
            if (i < tabButtons.Length && tabButtons[i] != null)
                tabButtons[i].interactable = true;
        }

        if (tabImageSlots != null)
        {
            for (int i = 10; i < tabImageSlots.Length; i++)
            {
                if (tabImageSlots[i] != null)
                    tabImageSlots[i].gameObject.SetActive(false);
            }
        }
    }

    private void RenderRows(IReadOnlyList<MessageWindowRowRenderData> rows)
    {
        IReadOnlyList<MessageWindowRowRenderData> safeRows =
            rows ?? System.Array.Empty<MessageWindowRowRenderData>();
        bool resetScroll = RowsChanged(safeRows);
        RowsList.Render(
            safeRows,
            GetIndexScrollContentHeight(safeRows.Count),
            GetIndexScrollStep(),
            resetScroll,
            GetIndexRowPitch(),
            (rowView, row, _) =>
                rowView.Render(
                    row,
                    GetSelectionTexture(),
                    GetMessageIconTexture(row.Type),
                    GetRowTextColor(row.Selected)
                ),
            (row, _) => row.Selected
        );

        renderedAnyRows = true;
        renderedActiveTab = activeTab;
        renderedRowIndexes.Clear();
        for (int i = 0; i < safeRows.Count; i++)
            renderedRowIndexes.Add(safeRows[i].Index);
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
        for (int i = 0; i < safeLines.Count; i++)
        {
            TextMeshProUGUI text = GetDetailLineTextField(i);
            UILayout.SetTemplateText(
                text,
                detailLineTextTemplate,
                safeLines[i],
                Color.white,
                new RectInt(template.x, template.y + i * linePitch, template.width, template.height)
            );
        }

        for (int i = safeLines.Count; i < detailLineTextFields.Count; i++)
            detailLineTextFields[i].gameObject.SetActive(false);

        renderedAnyDetailLines = true;
        renderedDetailSelectedIndex = rowSelection.SelectedIndex;
        renderedDetailLines.Clear();
        renderedDetailLines.AddRange(safeLines);
    }

    private void HideIndex()
    {
        backgroundImage.gameObject.SetActive(false);
        HideTabs();
        tabTitleTextField.gameObject.SetActive(false);
        rowsScrollArea.gameObject.SetActive(false);
        HideRows();
        selectAllButtonImage.gameObject.SetActive(false);
        removeSelectedButtonImage.gameObject.SetActive(false);
    }

    private void HideDetail()
    {
        detailStripImage.gameObject.SetActive(false);
        detailBodyImage.gameObject.SetActive(false);
        detailCardImage.gameObject.SetActive(false);
        detailOverlayImage.gameObject.SetActive(false);
        detailHeaderTextField.gameObject.SetActive(false);
        detailNextButtonImage.gameObject.SetActive(false);
        detailPreviousButtonImage.gameObject.SetActive(false);
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

    private void HideRows()
    {
        RowsList.Hide();
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

    private int GetIndexRowPitch()
    {
        return UILayout.GetSourceRect(rowTemplate.transform as RectTransform).height;
    }

    private int GetDetailLinePitch()
    {
        return UILayout.GetSourceRect(detailLineTextTemplate.rectTransform).height;
    }

    private static void SetImageAtTemplateOrigin(RawImage image, Texture texture)
    {
        RectInt rect = UILayout.GetSourceRect(image.rectTransform);
        UILayout.SetImage(image, texture, rect.x, rect.y);
    }

    internal static RectInt GetDetailImageRect(Texture texture, RectInt template)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0 || template.width <= 0)
            return template;

        int height = Mathf.RoundToInt(texture.height / (texture.width / (float)template.width));
        return new RectInt(template.x, template.y, template.width, height);
    }

    internal static Rect GetDetailImageUvRect(Texture texture, RectInt template)
    {
        return new Rect(0f, 0f, 1f, 1f);
    }

    private void RenderDetailCard(string imagePath)
    {
        Texture2D texture = uiContext?.GetTexture(imagePath);
        if (texture == null)
        {
            detailCardImage.gameObject.SetActive(false);
            return;
        }

        RectInt rect = GetDetailImageRect(texture, detailCardTemplateRect);
        detailCardImage.uvRect = GetDetailImageUvRect(texture, detailCardTemplateRect);
        UILayout.SetImage(detailCardImage, texture, rect.x, rect.y, rect.width, rect.height);
    }

    private void RenderDetailOverlay(string imagePath)
    {
        Texture2D texture = uiContext?.GetTexture(imagePath);
        if (texture == null)
        {
            detailOverlayImage.gameObject.SetActive(false);
            return;
        }

        RectInt rect = GetDetailImageRect(texture, detailOverlayTemplateRect);
        detailOverlayImage.uvRect = GetDetailImageUvRect(texture, detailOverlayTemplateRect);
        UILayout.SetImage(detailOverlayImage, texture, rect.x, rect.y, rect.width, rect.height);
    }

    private void RenderCommandButton(
        RawImage image,
        Button button,
        WindowButtonImageTheme theme,
        bool enabled,
        bool pressed,
        Texture fallbackTexture
    )
    {
        Texture upTexture = GetThemedButtonTexture(theme, false, enabled) ?? fallbackTexture;
        Texture downTexture = GetThemedButtonTexture(theme, true, enabled) ?? upTexture;
        if (pressed && enabled)
            upTexture = downTexture;

        SetButtonVisual(image, button, upTexture, enabled);
        ApplyCommandButtonLayout(image, theme);
    }

    private static void SetButtonVisual(
        RawImage image,
        Button button,
        Texture upTexture,
        bool enabled
    )
    {
        UILayout.SetInteractiveImageTexture(image, upTexture);
        button.interactable = enabled && upTexture != null;
        image.raycastTarget = enabled && upTexture != null;
    }

    private static void HideCommandButton(RawImage image, Button button)
    {
        UILayout.SetInteractiveImageTexture(image, null);
        image.raycastTarget = false;
        button.interactable = false;
    }

    private static void ApplyCommandButtonLayout(RawImage image, WindowButtonImageTheme theme)
    {
        SourceRectLayout layout = theme?.SourceLayout;
        if (layout == null)
            return;

        UILayout.SetSourceRect(
            image.rectTransform,
            layout.X,
            layout.Y,
            layout.Width,
            layout.Height
        );
    }

    private Texture2D GetTabTexture(int tab, bool active)
    {
        MessagesWindowTheme theme = GetMessagesTheme();
        return tab switch
        {
            0 => active ? allButtonDownTexture : allButtonUpTexture,
            1 => GetThemedButtonTexture(theme?.SupportButton, active),
            2 => GetThemedButtonTexture(theme?.FleetButton, active),
            3 => GetThemedButtonTexture(theme?.MissionsButton, active),
            4 => active ? resourceButtonDownTexture : resourceButtonUpTexture,
            5 => active ? manufacturingButtonDownTexture : manufacturingButtonUpTexture,
            6 => active ? defenseButtonDownTexture : defenseButtonUpTexture,
            7 => active ? conflictButtonDownTexture : conflictButtonUpTexture,
            8 => active ? chatButtonDownTexture : chatButtonUpTexture,
            9 => GetThemedButtonTexture(theme?.AdviceButton, active),
            _ => null,
        };
    }

    private Texture2D GetMessageIconTexture(MessageType type)
    {
        MessagesWindowTheme theme = GetMessagesTheme();
        return type switch
        {
            MessageType.PopularSupport => uiContext?.GetTexture(theme?.LoyaltyIconImagePath),
            MessageType.Mission => uiContext?.GetTexture(theme?.MissionIconImagePath),
            MessageType.Resource => resourceIconTexture,
            MessageType.Conflict => conflictIconTexture,
            _ => null,
        };
    }

    private Texture2D GetSelectionTexture()
    {
        return uiContext?.GetTexture(GetMessagesTheme()?.SelectionImagePath);
    }

    private Texture2D GetOverlayFrameTexture()
    {
        return uiContext?.GetTexture(GetMessagesTheme()?.OverlayFrameImagePath);
    }

    private Texture2D GetThemedButtonTexture(WindowButtonImageTheme theme, bool pressed)
    {
        return uiContext?.GetTexture(theme?.GetImagePath(pressed));
    }

    private Texture2D GetThemedButtonTexture(
        WindowButtonImageTheme theme,
        bool pressed,
        bool enabled
    )
    {
        if (!enabled && theme != null && !string.IsNullOrEmpty(theme.DisabledImagePath))
            return uiContext?.GetTexture(theme.DisabledImagePath);

        return GetThemedButtonTexture(theme, pressed);
    }

    private MessagesWindowTheme GetMessagesTheme()
    {
        return uiContext?.GetPlayerFactionTheme()?.StrategyWindows?.Messages;
    }

    private string GetDetailImagePath(Message message)
    {
        if (message == null)
            return null;

        if (!string.IsNullOrEmpty(message.DisplayImagePath))
            return message.DisplayImagePath;

        string key = !string.IsNullOrEmpty(message.DisplayImageKey)
            ? message.DisplayImageKey
            : GetDefaultDetailImageKey(message.Type);

        return GetMessagesTheme()?.GetDetailImagePath(key);
    }

    private static string GetDefaultDetailImageKey(MessageType type)
    {
        return type == MessageType.Advice ? "advice" : null;
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

    private Color32 GetRowTextColor(bool selected)
    {
        if (!selected)
            return Color.gray;

        return GetMessagesTheme()?.GetSelectedRowTextColor() ?? Color.white;
    }

    private List<string> BuildDetailLines(string text)
    {
        return WrapText(text, GetDetailWrapWidth());
    }

    private List<string> WrapText(string text, int width)
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

    private bool RowsChanged(IReadOnlyList<MessageWindowRowRenderData> rows)
    {
        if (
            !renderedAnyRows
            || renderedActiveTab != activeTab
            || renderedRowIndexes.Count != rows.Count
        )
            return true;

        for (int i = 0; i < rows.Count; i++)
        {
            if (renderedRowIndexes[i] != rows[i].Index)
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

    private void CaptureDefaultTextures()
    {
        defaultButtonStripTexture = buttonStripImage.texture;
        defaultCloseButtonTexture = closeButtonImage.texture;
        defaultDisplayButtonTexture = displayButtonImage.texture;
        defaultIndexButtonTexture = indexButtonImage.texture;
        defaultSignalButtonTexture = signalButtonImage.texture;
        defaultSignalTargetButtonTexture = signalTargetButtonImage.texture;
        defaultChatCommandButtonTexture = chatCommandButtonImage.texture;
    }

    private void CaptureDefaultRects()
    {
        detailCardTemplateRect = UILayout.GetSourceRect(detailCardImage.rectTransform);
        detailOverlayTemplateRect = UILayout.GetSourceRect(detailOverlayImage.rectTransform);
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (overlayFrameImage == null)
            throw new MissingReferenceException($"{name}/OverlayFrameImage is missing.");
        if (buttonStripImage == null)
            throw new MissingReferenceException($"{name}/ButtonStripImage is missing.");
        if (closeButtonImage == null)
            throw new MissingReferenceException($"{name}/CloseButtonImage is missing.");
        if (displayButtonImage == null)
            throw new MissingReferenceException($"{name}/DisplayButtonImage is missing.");
        if (indexButtonImage == null)
            throw new MissingReferenceException($"{name}/IndexButtonImage is missing.");
        if (signalButtonImage == null)
            throw new MissingReferenceException($"{name}/SignalButtonImage is missing.");
        if (signalTargetButtonImage == null)
            throw new MissingReferenceException($"{name}/SignalTargetButtonImage is missing.");
        if (chatCommandButtonImage == null)
            throw new MissingReferenceException($"{name}/ChatCommandButtonImage is missing.");
        if (closeButton == null)
            throw new MissingReferenceException($"{name}/CloseButton is missing.");
        if (displayButton == null)
            throw new MissingReferenceException($"{name}/DisplayButton is missing.");
        if (indexButton == null)
            throw new MissingReferenceException($"{name}/IndexButton is missing.");
        if (signalButton == null)
            throw new MissingReferenceException($"{name}/SignalButton is missing.");
        if (signalTargetButton == null)
            throw new MissingReferenceException($"{name}/SignalTargetButton is missing.");
        if (chatCommandButton == null)
            throw new MissingReferenceException($"{name}/ChatCommandButton is missing.");
        if (tabTitleTextField == null)
            throw new MissingReferenceException($"{name}/TabTitleTextField is missing.");
        if (rowsScrollArea == null)
            throw new MissingReferenceException($"{name}/RowsScrollArea is missing.");
        if (tabImageSlots == null || tabImageSlots.Length == 0)
            throw new MissingReferenceException($"{name}/TabImageSlots are missing.");
        if (tabButtons == null || tabButtons.Length == 0)
            throw new MissingReferenceException($"{name}/TabButtons are missing.");
        if (rowTemplate == null)
            throw new MissingReferenceException($"{name}/RowTemplate is missing.");
        if (selectAllButtonImage == null)
            throw new MissingReferenceException($"{name}/SelectAllButtonImage is missing.");
        if (removeSelectedButtonImage == null)
            throw new MissingReferenceException($"{name}/RemoveSelectedButtonImage is missing.");
        if (selectAllButton == null)
            throw new MissingReferenceException($"{name}/SelectAllButton is missing.");
        if (removeSelectedButton == null)
            throw new MissingReferenceException($"{name}/RemoveSelectedButton is missing.");
        if (detailStripImage == null)
            throw new MissingReferenceException($"{name}/DetailStripImage is missing.");
        if (detailBodyImage == null)
            throw new MissingReferenceException($"{name}/DetailBodyImage is missing.");
        if (detailCardImage == null)
            throw new MissingReferenceException($"{name}/DetailCardImage is missing.");
        if (detailOverlayImage == null)
            throw new MissingReferenceException($"{name}/DetailOverlayImage is missing.");
        if (detailHeaderTextField == null)
            throw new MissingReferenceException($"{name}/DetailHeaderTextField is missing.");
        if (detailNextButtonImage == null)
            throw new MissingReferenceException($"{name}/DetailNextButtonImage is missing.");
        if (detailPreviousButtonImage == null)
            throw new MissingReferenceException($"{name}/DetailPreviousButtonImage is missing.");
        if (detailNextButton == null)
            throw new MissingReferenceException($"{name}/DetailNextButton is missing.");
        if (detailPreviousButton == null)
            throw new MissingReferenceException($"{name}/DetailPreviousButton is missing.");
        if (detailLinesScrollArea == null)
            throw new MissingReferenceException($"{name}/DetailLinesScrollArea is missing.");
        if (detailLineTextTemplate == null)
            throw new MissingReferenceException($"{name}/DetailLineTextTemplate is missing.");
        rowTemplate.gameObject.SetActive(false);
        detailLineTextTemplate.gameObject.SetActive(false);
    }

    private SelectableListView<MessageWindowRowView, MessageWindowRowRenderData> RowsList
    {
        get
        {
            rowsList ??= new SelectableListView<MessageWindowRowView, MessageWindowRowRenderData>(
                rowsScrollArea,
                rowTemplate,
                "MessageRow",
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

public sealed class MessagesWindowRenderData
{
    public int X;
    public int Y;
    public bool Panel;
    public bool UseUpperButtonLayout;
    public int ActiveTab;
    public int SelectedIndex;
    public string TabTitle;
    public string DetailHeader;
    public string DetailImagePath;
    public string DetailOverlayImagePath;
    public UtilityWindowFrameRenderData Frame;
    public MessageType DetailType;
    public bool DetailNextDisabled;
    public bool DetailPreviousDisabled;
    public IReadOnlyList<Message> SourceMessages = System.Array.Empty<Message>();
    public IReadOnlyCollection<int> SelectedItems = System.Array.Empty<int>();
    public List<MessageWindowRowRenderData> Rows = new List<MessageWindowRowRenderData>();
    public List<string> DetailLines = new List<string>();
}

public sealed class MessageWindowRowRenderData
{
    public int Index;
    public string Header;
    public MessageType Type;
    public bool Selected;
}
