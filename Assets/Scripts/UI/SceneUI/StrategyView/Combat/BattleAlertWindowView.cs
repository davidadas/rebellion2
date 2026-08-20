using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Applies battle-alert presentation data and raises semantic player requests.
/// </summary>
public sealed class BattleAlertWindowView : MonoBehaviour
{
    private const int _personnelResultColumnCount = 3;
    private const int _resultNavigationButtonCount = 2;
    private const int _standardResultColumnCount = 2;

    private static readonly BattleAlertChoice[] _pendingChoices =
    {
        BattleAlertChoice.Retreat,
        BattleAlertChoice.AutoResolve,
        BattleAlertChoice.TakeCommand,
    };

    [Header("Panel")]
    [SerializeField]
    private RawImage panelBackgroundImage;

    [SerializeField]
    private RawImage frameImage;

    [SerializeField]
    private TextMeshProUGUI titleTextField;

    [SerializeField]
    private TextMeshProUGUI headerTextField;

    [SerializeField]
    private TextMeshProUGUI summaryTextField;

    [SerializeField]
    private ScrollAreaView rowsScrollArea;

    [SerializeField]
    private BattleAlertRowView rowTemplate;

    [Header("Result Header")]
    [SerializeField]
    private TextMeshProUGUI resultPlanetaryTitleTextField;

    [SerializeField]
    private TextMeshProUGUI resultFleetTitleTextField;

    [SerializeField]
    private TextMeshProUGUI resultSummaryTextField;

    [SerializeField]
    private TextMeshProUGUI resultDirectPromptTextField;

    [SerializeField]
    private TextMeshProUGUI resultPlanetaryForceHeaderTextField;

    [SerializeField]
    private TextMeshProUGUI resultFleetForceHeaderTextField;

    [SerializeField]
    private TextMeshProUGUI resultFleetFiltersTextField;

    [SerializeField]
    private TextMeshProUGUI resultPlanetaryTableTitleTextField;

    [SerializeField]
    private TextMeshProUGUI resultFleetTableTitleTextField;

    [SerializeField]
    private TextMeshProUGUI[] resultPlanetaryStandardColumnHeaderTextFields =
        Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private TextMeshProUGUI[] resultFleetStandardColumnHeaderTextFields =
        Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private TextMeshProUGUI[] resultPlanetaryPersonnelColumnHeaderTextFields =
        Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private TextMeshProUGUI[] resultFleetPersonnelColumnHeaderTextFields =
        Array.Empty<TextMeshProUGUI>();

    [Header("Result Table")]
    [SerializeField]
    private ScrollAreaView resultRowsScrollArea;

    [SerializeField]
    private RectTransform resultStandardOperationalColumn;

    [SerializeField]
    private RectTransform resultStandardDestroyedColumn;

    [SerializeField]
    private RectTransform resultPersonnelOperationalColumn;

    [SerializeField]
    private RectTransform resultPersonnelDestroyedColumn;

    [SerializeField]
    private BattleResultItemView resultStandardItemTemplate;

    [SerializeField]
    private BattleResultItemView resultPersonnelItemTemplate;

    [Header("Primary Controls")]
    [SerializeField]
    private RawImage[] viewButtonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] viewButtonPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] viewButtons = Array.Empty<Button>();

    [SerializeField]
    private RawImage[] commandButtonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] commandButtonPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] commandButtons = Array.Empty<Button>();

    [Header("Result Controls")]
    [SerializeField]
    private RawImage resultCloseButtonImage;

    [SerializeField]
    private RawImagePressVisual resultCloseButtonPressVisual;

    [SerializeField]
    private Button resultCloseButton;

    [SerializeField]
    private RawImage[] resultCategoryButtonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] resultCategoryButtonPressVisuals =
        Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] resultCategoryButtons = Array.Empty<Button>();

    [SerializeField]
    private RawImage[] resultDirectButtonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] resultDirectButtonPressVisuals =
        Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] resultDirectButtons = Array.Empty<Button>();

    private readonly List<BattleAlertRowView> rowViews = new List<BattleAlertRowView>();
    private readonly List<BattleResultItemView> standardOperationalResultItems =
        new List<BattleResultItemView>();
    private readonly List<BattleResultItemView> standardDestroyedResultItems =
        new List<BattleResultItemView>();
    private readonly List<BattleResultItemView> personnelOperationalResultItems =
        new List<BattleResultItemView>();
    private readonly List<BattleResultItemView> personnelDestroyedResultItems =
        new List<BattleResultItemView>();
    private readonly List<UnityAction> commandButtonListeners = new List<UnityAction>();
    private readonly List<UnityAction> resultCategoryButtonListeners = new List<UnityAction>();
    private readonly List<UnityAction> resultDirectButtonListeners = new List<UnityAction>();
    private readonly List<UnityAction> viewButtonListeners = new List<UnityAction>();
    private BattleAlertWindowRenderData lastRenderData;
    private UnityAction resultCloseButtonListener;
    private bool resetRowsScroll = true;

    /// <summary>
    /// Occurs when a choice request is raised.
    /// </summary>
    internal event Action<BattleAlertWindowView, BattleAlertChoice> ChoiceRequested;

    /// <summary>
    /// Occurs when a close request is raised.
    /// </summary>
    internal event Action<BattleAlertWindowView> CloseRequested;

    /// <summary>
    /// Occurs when the control is pressed.
    /// </summary>
    internal event Action ControlPressed;

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    internal event Action<BattleAlertWindowView> Destroyed;

    /// <summary>
    /// Occurs when an open-fleet request is raised.
    /// </summary>
    internal event Action<BattleAlertWindowView> OpenFleetRequested;

    /// <summary>
    /// Occurs when an open-system request is raised.
    /// </summary>
    internal event Action<BattleAlertWindowView> OpenSectorRequested;

    /// <summary>
    /// Occurs when a primary panel request is raised.
    /// </summary>
    internal event Action<BattleAlertWindowView, BattleAlertPanel> PrimaryPanelRequested;

    /// <summary>
    /// Occurs when a result category request is raised.
    /// </summary>
    internal event Action<BattleAlertWindowView, BattleResultCategory> ResultCategoryRequested;

    /// <summary>
    /// Verifies authored references and binds each authored control once.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindViewButtons();
        BindCommandButtons();
        BindResultButtons();
        BindControlPressVisuals();
    }

    /// <summary>
    /// Releases local presentation caches and notifies the owning controller.
    /// </summary>
    private void OnDestroy()
    {
        UnbindControls();
        Destroyed?.Invoke(this);
        rowViews.Clear();
        standardOperationalResultItems.Clear();
        standardDestroyedResultItems.Clear();
        personnelOperationalResultItems.Clear();
        personnelDestroyedResultItems.Clear();
    }

    /// <summary>
    /// Applies a complete immutable battle-alert presentation.
    /// </summary>
    /// <param name="data">The presentation to apply.</param>
    internal void Render(BattleAlertWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        if (HasScrollableContentChanged(lastRenderData, data))
            resetRowsScroll = true;
        lastRenderData = data;
        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);

        if (data.Mode == BattleAlertWindowMode.Hidden)
        {
            RenderHidden();
            return;
        }

        gameObject.SetActive(true);
        UILayout.SetImageTexture(panelBackgroundImage, data.BackgroundTexture);
        UILayout.SetInteractiveImageTexture(frameImage, data.FrameTexture);
        RenderButtons(viewButtonImages, viewButtonPressVisuals, viewButtons, data.ViewButtons);

        if (data.Mode == BattleAlertWindowMode.Result)
            RenderResult(data);
        else
            RenderPending(data);
    }

    /// <summary>
    /// Raises a combat-choice request for the owning controller.
    /// </summary>
    /// <param name="choice">The requested combat choice.</param>
    internal void RequestChoice(BattleAlertChoice choice)
    {
        ChoiceRequested?.Invoke(this, choice);
    }

    /// <summary>
    /// Raises a close request for the owning controller.
    /// </summary>
    internal void RequestClose()
    {
        CloseRequested?.Invoke(this);
    }

    /// <summary>
    /// Raises a fleet-navigation request for the owning controller.
    /// </summary>
    internal void RequestOpenFleet()
    {
        OpenFleetRequested?.Invoke(this);
    }

    /// <summary>
    /// Raises a sector-navigation request for the owning controller.
    /// </summary>
    internal void RequestOpenSector()
    {
        OpenSectorRequested?.Invoke(this);
    }

    /// <summary>
    /// Raises a primary panel request for the owning controller.
    /// </summary>
    /// <param name="panel">The requested primary panel.</param>
    internal void RequestPrimaryPanel(BattleAlertPanel panel)
    {
        PrimaryPanelRequested?.Invoke(this, panel);
    }

    /// <summary>
    /// Raises a completed-result category request for the owning controller.
    /// </summary>
    /// <param name="category">The requested completed-result category.</param>
    internal void RequestResultCategory(BattleResultCategory category)
    {
        ResultCategoryRequested?.Invoke(this, category);
    }

    /// <summary>
    /// Raises the shared control-press audio request for the owning controller.
    /// </summary>
    internal void RequestControlPress()
    {
        ControlPressed?.Invoke();
    }

    /// <summary>
    /// Binds the four primary panel controls to semantic panel requests.
    /// </summary>
    private void BindViewButtons()
    {
        int panelCount = Math.Min(viewButtons.Length, BattleAlertPanelCatalog.Ordered.Count);
        for (int i = 0; i < panelCount; i++)
        {
            BattleAlertPanel panel = BattleAlertPanelCatalog.Ordered[i];
            UnityAction listener = () => RequestPrimaryPanel(panel);
            viewButtonListeners.Add(listener);
            viewButtons[i].onClick.AddListener(listener);
        }
    }

    /// <summary>
    /// Binds the pending-combat command controls to semantic requests.
    /// </summary>
    private void BindCommandButtons()
    {
        int choiceCount = Math.Min(commandButtons.Length, _pendingChoices.Length);
        for (int i = 0; i < choiceCount; i++)
        {
            BattleAlertChoice choice = _pendingChoices[i];
            UnityAction listener = () => RequestChoice(choice);
            commandButtonListeners.Add(listener);
            commandButtons[i].onClick.AddListener(listener);
        }
    }

    /// <summary>
    /// Binds completed-result controls to semantic category and navigation requests.
    /// </summary>
    private void BindResultButtons()
    {
        resultCloseButtonListener = RequestClose;
        resultCloseButton.onClick.AddListener(resultCloseButtonListener);

        int categoryCount = Math.Min(
            resultCategoryButtons.Length,
            BattleResultCategoryCatalog.Ordered.Count
        );
        for (int i = 0; i < categoryCount; i++)
        {
            BattleResultCategory category = BattleResultCategoryCatalog.Ordered[i];
            UnityAction listener = () => RequestResultCategory(category);
            resultCategoryButtonListeners.Add(listener);
            resultCategoryButtons[i].onClick.AddListener(listener);
        }

        UnityAction openSector = RequestOpenSector;
        UnityAction openFleet = RequestOpenFleet;
        resultDirectButtonListeners.Add(openSector);
        resultDirectButtonListeners.Add(openFleet);
        resultDirectButtons[0].onClick.AddListener(openSector);
        resultDirectButtons[1].onClick.AddListener(openFleet);
    }

    /// <summary>
    /// Detaches every authored control listener owned by this view.
    /// </summary>
    private void UnbindControls()
    {
        UnbindControlPressVisuals();
        UnbindButtons(viewButtons, viewButtonListeners);
        UnbindButtons(commandButtons, commandButtonListeners);
        UnbindButtons(resultCategoryButtons, resultCategoryButtonListeners);
        UnbindButtons(resultDirectButtons, resultDirectButtonListeners);

        if (resultCloseButton != null && resultCloseButtonListener != null)
            resultCloseButton.onClick.RemoveListener(resultCloseButtonListener);

        resultCloseButtonListener = null;
    }

    /// <summary>
    /// Detaches an ordered listener collection from its authored buttons.
    /// </summary>
    /// <param name="buttons">The authored buttons.</param>
    /// <param name="listeners">The listeners retained when the buttons were bound.</param>
    private static void UnbindButtons(
        IReadOnlyList<Button> buttons,
        IReadOnlyList<UnityAction> listeners
    )
    {
        int count = Math.Min(buttons.Count, listeners.Count);
        for (int i = 0; i < count; i++)
        {
            if (buttons[i] != null && listeners[i] != null)
                buttons[i].onClick.RemoveListener(listeners[i]);
        }
    }

    /// <summary>
    /// Subscribes authored battle-alert press visuals to the semantic control-press request.
    /// </summary>
    private void BindControlPressVisuals()
    {
        BindControlPressVisuals(viewButtonPressVisuals);
        resultCloseButtonPressVisual.Pressed += RequestControlPress;
        BindControlPressVisuals(resultDirectButtonPressVisuals);
    }

    /// <summary>
    /// Releases authored battle-alert press-visual subscriptions.
    /// </summary>
    private void UnbindControlPressVisuals()
    {
        UnbindControlPressVisuals(viewButtonPressVisuals);
        if (resultCloseButtonPressVisual != null)
            resultCloseButtonPressVisual.Pressed -= RequestControlPress;
        UnbindControlPressVisuals(resultDirectButtonPressVisuals);
    }

    /// <summary>
    /// Subscribes a collection of press visuals to the semantic control-press request.
    /// </summary>
    /// <param name="pressVisuals">The authored press visuals to subscribe.</param>
    private void BindControlPressVisuals(IReadOnlyList<RawImagePressVisual> pressVisuals)
    {
        for (int i = 0; i < pressVisuals.Count; i++)
            pressVisuals[i].Pressed += RequestControlPress;
    }

    /// <summary>
    /// Releases semantic control-press subscriptions from a collection of press visuals.
    /// </summary>
    /// <param name="pressVisuals">The authored press visuals to unsubscribe.</param>
    private void UnbindControlPressVisuals(IReadOnlyList<RawImagePressVisual> pressVisuals)
    {
        for (int i = 0; i < pressVisuals.Count; i++)
        {
            if (pressVisuals[i] != null)
                pressVisuals[i].Pressed -= RequestControlPress;
        }
    }

    /// <summary>
    /// Applies the pending-combat portion of the current presentation.
    /// </summary>
    /// <param name="data">The complete battle-alert presentation.</param>
    private void RenderPending(BattleAlertWindowRenderData data)
    {
        BattleAlertPendingRenderData pending = data.Pending;
        HideResultPresentation();
        UILayout.SetTextContent(titleTextField, pending.Title, data.TitleColor);
        RenderButtons(
            commandButtonImages,
            commandButtonPressVisuals,
            commandButtons,
            pending.CommandButtons
        );

        if (pending.Panel == BattleAlertPanel.Summary)
        {
            headerTextField.gameObject.SetActive(false);
            rowsScrollArea.gameObject.SetActive(false);
            HideRows();
            UILayout.SetTextContent(summaryTextField, pending.Summary, data.TitleColor);
            resetRowsScroll = true;
            return;
        }

        summaryTextField.gameObject.SetActive(false);
        rowsScrollArea.gameObject.SetActive(true);
        UILayout.SetTextContent(headerTextField, pending.Header);
        RenderRows(pending.Rows);
    }

    /// <summary>
    /// Applies the completed-result portion of the current presentation.
    /// </summary>
    /// <param name="data">The complete battle-alert presentation.</param>
    private void RenderResult(BattleAlertWindowRenderData data)
    {
        BattleAlertResultRenderData result = data.Result;
        HidePendingPresentation();
        HideButtons(commandButtonImages, commandButtonPressVisuals, commandButtons);
        RenderButton(
            resultCloseButtonImage,
            resultCloseButtonPressVisual,
            resultCloseButton,
            result.ResultCloseButton
        );

        switch (result.Panel)
        {
            case BattleResultPanel.FirstForces:
            case BattleResultPanel.SecondForces:
                RenderResultDetail(data.TitleColor, result);
                break;
            case BattleResultPanel.Direct:
                RenderResultDirect(data.TitleColor, result);
                break;
            default:
                RenderResultSummary(data.TitleColor, result);
                break;
        }
    }

    /// <summary>
    /// Applies the completed-result summary panel.
    /// </summary>
    /// <param name="titleColor">The faction-themed title color.</param>
    /// <param name="result">The completed-result presentation.</param>
    private void RenderResultSummary(Color titleColor, BattleAlertResultRenderData result)
    {
        HideRows();
        HideResultItems();
        HideResultDetailLabels();
        HideResultCategoryButtons();
        HideResultDirectButtons();
        resultDirectPromptTextField.gameObject.SetActive(false);
        RenderResultTitle(titleColor, result);
        UILayout.SetTextContent(resultSummaryTextField, result.Summary, titleColor);
    }

    /// <summary>
    /// Applies a completed-result force-detail panel.
    /// </summary>
    /// <param name="titleColor">The faction-themed title color.</param>
    /// <param name="result">The completed-result presentation.</param>
    private void RenderResultDetail(Color titleColor, BattleAlertResultRenderData result)
    {
        HideRows();
        HideResultDirectButtons();
        resultDirectPromptTextField.gameObject.SetActive(false);
        RenderResultTitle(titleColor, result);
        resultSummaryTextField.gameObject.SetActive(false);
        RenderResultDetailLabels(result);
        RenderResultCategoryButtons(result.ResultCategories);
        RenderResultTable(result);
    }

    /// <summary>
    /// Applies the completed-result direct-navigation panel.
    /// </summary>
    /// <param name="titleColor">The faction-themed title color.</param>
    /// <param name="result">The completed-result presentation.</param>
    private void RenderResultDirect(Color titleColor, BattleAlertResultRenderData result)
    {
        HideRows();
        HideResultItems();
        HideResultDetailLabels();
        HideResultCategoryButtons();
        RenderResultTitle(titleColor, result);
        resultSummaryTextField.gameObject.SetActive(false);
        UILayout.SetTextContent(resultDirectPromptTextField, result.Summary, titleColor);
        RenderButtons(
            resultDirectButtonImages,
            resultDirectButtonPressVisuals,
            resultDirectButtons,
            result.ResultDirectButtons
        );
    }

    /// <summary>
    /// Applies pending-combat list rows and scroll extent.
    /// </summary>
    /// <param name="rows">The rows to display.</param>
    private void RenderRows(IReadOnlyList<BattleAlertRowRenderData> rows)
    {
        int rowHeight = rowTemplate.Height;
        rowsScrollArea.SetContentHeight(rows.Count * rowHeight, rowHeight, resetRowsScroll);

        for (int i = 0; i < rows.Count; i++)
            GetRowView(i).Render(rows[i]);

        for (int i = rows.Count; i < rowViews.Count; i++)
            rowViews[i].gameObject.SetActive(false);

        resetRowsScroll = false;
    }

    /// <summary>
    /// Returns an existing pending-combat row or instantiates one from its authored template.
    /// </summary>
    /// <param name="index">The requested row index.</param>
    /// <returns>The row view at that index.</returns>
    private BattleAlertRowView GetRowView(int index)
    {
        while (rowViews.Count <= index)
        {
            BattleAlertRowView row = Instantiate(rowTemplate, rowsScrollArea.ContentRoot);
            row.name = $"BattleAlertRow{rowViews.Count}";
            rowViews.Add(row);
        }

        return rowViews[index];
    }

    /// <summary>
    /// Applies completed-result force labels and column headers.
    /// </summary>
    /// <param name="result">The completed-result presentation.</param>
    private void RenderResultDetailLabels(BattleAlertResultRenderData result)
    {
        HideResultDetailLabels();
        bool planetary = result.UsesPlanetaryCategoryLayout;
        TextMeshProUGUI forceHeaderTextField = planetary
            ? resultPlanetaryForceHeaderTextField
            : resultFleetForceHeaderTextField;
        TextMeshProUGUI tableTitleTextField = planetary
            ? resultPlanetaryTableTitleTextField
            : resultFleetTableTitleTextField;
        IReadOnlyList<TextMeshProUGUI> standardColumnHeaderTextFields = planetary
            ? resultPlanetaryStandardColumnHeaderTextFields
            : resultFleetStandardColumnHeaderTextFields;
        IReadOnlyList<TextMeshProUGUI> personnelColumnHeaderTextFields = planetary
            ? resultPlanetaryPersonnelColumnHeaderTextFields
            : resultFleetPersonnelColumnHeaderTextFields;

        UILayout.SetTextContent(
            forceHeaderTextField,
            result.ResultForceHeader,
            result.ResultForceHeaderColor
        );
        resultFleetFiltersTextField.gameObject.SetActive(!planetary);
        UILayout.SetTextContent(tableTitleTextField, result.ResultTableTitle);

        if (result.UsesPersonnelColumns)
        {
            HideTextFields(standardColumnHeaderTextFields);
            SetTextFields(personnelColumnHeaderTextFields, result.ResultColumnHeaders);
        }
        else
        {
            HideTextFields(personnelColumnHeaderTextFields);
            SetTextFields(standardColumnHeaderTextFields, result.ResultColumnHeaders);
        }
    }

    /// <summary>
    /// Applies the completed-result title to its planetary or fleet layout.
    /// </summary>
    /// <param name="titleColor">The faction-themed title color.</param>
    /// <param name="result">The completed-result presentation.</param>
    private void RenderResultTitle(Color titleColor, BattleAlertResultRenderData result)
    {
        resultPlanetaryTitleTextField.gameObject.SetActive(false);
        resultFleetTitleTextField.gameObject.SetActive(false);
        UILayout.SetTextContent(
            result.UsesPlanetaryCategoryLayout
                ? resultPlanetaryTitleTextField
                : resultFleetTitleTextField,
            result.Title,
            titleColor
        );
    }

    /// <summary>
    /// Applies the completed-result category controls.
    /// </summary>
    /// <param name="categories">The categories displayed in source order.</param>
    private void RenderResultCategoryButtons(
        IReadOnlyList<BattleResultCategoryRenderData> categories
    )
    {
        for (int i = 0; i < resultCategoryButtons.Length; i++)
        {
            RenderButton(
                resultCategoryButtonImages[i],
                resultCategoryButtonPressVisuals[i],
                resultCategoryButtons[i],
                null
            );
        }

        foreach (BattleResultCategoryRenderData category in categories)
        {
            int index = GetCategoryButtonIndex(category.Category);
            if (index < 0 || index >= resultCategoryButtons.Length)
                continue;

            RenderButton(
                resultCategoryButtonImages[index],
                resultCategoryButtonPressVisuals[index],
                resultCategoryButtons[index],
                category.Button
            );
        }
    }

    /// <summary>
    /// Returns the authored button-array index for a result category.
    /// </summary>
    /// <param name="category">The result category.</param>
    /// <returns>The authored array index, or negative one when absent.</returns>
    private static int GetCategoryButtonIndex(BattleResultCategory category)
    {
        IReadOnlyList<BattleResultCategory> ordered = BattleResultCategoryCatalog.Ordered;
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i] == category)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Applies the completed-result table and scroll extent.
    /// </summary>
    /// <param name="result">The completed-result presentation.</param>
    private void RenderResultTable(BattleAlertResultRenderData result)
    {
        BattleResultTableRenderData table = result.ResultTable;
        if (table == null)
        {
            HideResultItems();
            return;
        }

        resultRowsScrollArea.gameObject.SetActive(true);
        int rowCount = Math.Max(table.Operational.Count, table.Destroyed.Count);
        int contentRows = Math.Max(1, rowCount);
        BattleResultItemView template = result.UsesPersonnelColumns
            ? resultPersonnelItemTemplate
            : resultStandardItemTemplate;
        RectTransform operationalColumn = result.UsesPersonnelColumns
            ? resultPersonnelOperationalColumn
            : resultStandardOperationalColumn;
        RectTransform destroyedColumn = result.UsesPersonnelColumns
            ? resultPersonnelDestroyedColumn
            : resultStandardDestroyedColumn;
        List<BattleResultItemView> operationalItems = result.UsesPersonnelColumns
            ? personnelOperationalResultItems
            : standardOperationalResultItems;
        List<BattleResultItemView> destroyedItems = result.UsesPersonnelColumns
            ? personnelDestroyedResultItems
            : standardDestroyedResultItems;

        SetResultColumnVisibility(result.UsesPersonnelColumns);
        resultRowsScrollArea.SetContentHeight(
            contentRows * template.Height,
            template.Height,
            resetRowsScroll
        );
        RenderResultColumn(table.Operational, operationalColumn, operationalItems, template);
        RenderResultColumn(table.Destroyed, destroyedColumn, destroyedItems, template);
        resetRowsScroll = false;
    }

    /// <summary>
    /// Applies one completed-result table column.
    /// </summary>
    /// <param name="data">The result items to display.</param>
    /// <param name="column">The authored column root.</param>
    /// <param name="items">The instantiated item cache for that column.</param>
    /// <param name="template">The authored row template.</param>
    private static void RenderResultColumn(
        IReadOnlyList<BattleResultItemRenderData> data,
        RectTransform column,
        List<BattleResultItemView> items,
        BattleResultItemView template
    )
    {
        for (int i = 0; i < data.Count; i++)
        {
            BattleResultItemView view = GetResultItemView(items, template, column, i);
            BattleResultItemRenderData item = data[i];
            UILayout.SetSourcePosition(view.transform as RectTransform, 0, i * template.Height);
            view.Render(item);
        }

        for (int i = data.Count; i < items.Count; i++)
            items[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns an existing result item or instantiates one from its authored template.
    /// </summary>
    /// <param name="items">The item cache for one result column.</param>
    /// <param name="template">The authored row template.</param>
    /// <param name="parent">The authored result column.</param>
    /// <param name="index">The requested result index.</param>
    /// <returns>The result item view at that index.</returns>
    private static BattleResultItemView GetResultItemView(
        List<BattleResultItemView> items,
        BattleResultItemView template,
        RectTransform parent,
        int index
    )
    {
        while (items.Count <= index)
        {
            BattleResultItemView item = Instantiate(template, parent);
            item.name = $"BattleResultTableItem{items.Count}";
            items.Add(item);
        }

        return items[index];
    }

    /// <summary>
    /// Shows the authored result columns for the active table layout.
    /// </summary>
    /// <param name="personnel">Whether the personnel layout is active.</param>
    private void SetResultColumnVisibility(bool personnel)
    {
        resultStandardOperationalColumn.gameObject.SetActive(!personnel);
        resultStandardDestroyedColumn.gameObject.SetActive(!personnel);
        resultPersonnelOperationalColumn.gameObject.SetActive(personnel);
        resultPersonnelDestroyedColumn.gameObject.SetActive(personnel);
        HideResultItemViews(
            personnel ? standardOperationalResultItems : personnelOperationalResultItems
        );
        HideResultItemViews(
            personnel ? standardDestroyedResultItems : personnelDestroyedResultItems
        );
    }

    /// <summary>
    /// Applies a collection of button presentations to matching authored controls.
    /// </summary>
    /// <param name="images">The authored button images.</param>
    /// <param name="pressVisuals">The authored pressed-state visuals.</param>
    /// <param name="buttons">The authored button controls.</param>
    /// <param name="data">The button presentations.</param>
    private static void RenderButtons(
        IReadOnlyList<RawImage> images,
        IReadOnlyList<RawImagePressVisual> pressVisuals,
        IReadOnlyList<Button> buttons,
        IReadOnlyList<BattleAlertButtonRenderData> data
    )
    {
        int count = Math.Min(images.Count, Math.Min(pressVisuals.Count, buttons.Count));
        for (int i = 0; i < count; i++)
        {
            RenderButton(images[i], pressVisuals[i], buttons[i], i < data.Count ? data[i] : null);
        }
    }

    /// <summary>
    /// Applies one button presentation to an authored control.
    /// </summary>
    /// <param name="image">The authored button image.</param>
    /// <param name="pressVisual">The authored pressed-state visual.</param>
    /// <param name="button">The authored button control.</param>
    /// <param name="data">The presentation to apply, or null to hide the control.</param>
    private static void RenderButton(
        RawImage image,
        RawImagePressVisual pressVisual,
        Button button,
        BattleAlertButtonRenderData data
    )
    {
        bool visible = data != null;
        button.interactable = data?.Interactable == true;
        if (data?.Bounds is RectInt bounds)
        {
            UILayout.SetSourceRect(
                image.rectTransform,
                bounds.x,
                bounds.y,
                bounds.width,
                bounds.height
            );
        }

        pressVisual.SetInteractiveTextures(
            visible ? data.Texture : null,
            visible ? data.PressedTexture : null
        );
    }

    /// <summary>
    /// Hides a collection of authored button controls.
    /// </summary>
    /// <param name="images">The authored button images.</param>
    /// <param name="pressVisuals">The authored pressed-state visuals.</param>
    /// <param name="buttons">The authored button controls.</param>
    private static void HideButtons(
        IReadOnlyList<RawImage> images,
        IReadOnlyList<RawImagePressVisual> pressVisuals,
        IReadOnlyList<Button> buttons
    )
    {
        int count = Math.Min(images.Count, Math.Min(pressVisuals.Count, buttons.Count));
        for (int i = 0; i < count; i++)
            RenderButton(images[i], pressVisuals[i], buttons[i], null);
    }

    /// <summary>
    /// Applies visible text values to matching authored labels.
    /// </summary>
    /// <param name="textFields">The authored labels.</param>
    /// <param name="values">The displayed text values.</param>
    private static void SetTextFields(
        IReadOnlyList<TextMeshProUGUI> textFields,
        IReadOnlyList<string> values
    )
    {
        int count = Math.Min(textFields.Count, values.Count);
        for (int i = 0; i < count; i++)
            UILayout.SetTextContent(textFields[i], values[i]);

        for (int i = count; i < textFields.Count; i++)
            textFields[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides authored text labels without changing their content or layout.
    /// </summary>
    /// <param name="textFields">The labels to hide.</param>
    private static void HideTextFields(IEnumerable<TextMeshProUGUI> textFields)
    {
        foreach (TextMeshProUGUI textField in textFields)
            textField.gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides every pending-combat presentation element.
    /// </summary>
    private void HidePendingPresentation()
    {
        titleTextField.gameObject.SetActive(false);
        headerTextField.gameObject.SetActive(false);
        summaryTextField.gameObject.SetActive(false);
        rowsScrollArea.gameObject.SetActive(false);
        HideRows();
    }

    /// <summary>
    /// Hides every completed-result presentation element.
    /// </summary>
    private void HideResultPresentation()
    {
        resultPlanetaryTitleTextField.gameObject.SetActive(false);
        resultFleetTitleTextField.gameObject.SetActive(false);
        resultSummaryTextField.gameObject.SetActive(false);
        resultDirectPromptTextField.gameObject.SetActive(false);
        HideResultDetailLabels();
        HideResultItems();
        HideResultCategoryButtons();
        HideResultDirectButtons();
        RenderButton(resultCloseButtonImage, resultCloseButtonPressVisual, resultCloseButton, null);
    }

    /// <summary>
    /// Hides completed-result detail labels.
    /// </summary>
    private void HideResultDetailLabels()
    {
        resultPlanetaryForceHeaderTextField.gameObject.SetActive(false);
        resultFleetForceHeaderTextField.gameObject.SetActive(false);
        resultFleetFiltersTextField.gameObject.SetActive(false);
        resultPlanetaryTableTitleTextField.gameObject.SetActive(false);
        resultFleetTableTitleTextField.gameObject.SetActive(false);
        HideTextFields(resultPlanetaryStandardColumnHeaderTextFields);
        HideTextFields(resultFleetStandardColumnHeaderTextFields);
        HideTextFields(resultPlanetaryPersonnelColumnHeaderTextFields);
        HideTextFields(resultFleetPersonnelColumnHeaderTextFields);
    }

    /// <summary>
    /// Hides completed-result category controls.
    /// </summary>
    private void HideResultCategoryButtons()
    {
        for (int i = 0; i < resultCategoryButtons.Length; i++)
        {
            RenderButton(
                resultCategoryButtonImages[i],
                resultCategoryButtonPressVisuals[i],
                resultCategoryButtons[i],
                null
            );
        }
    }

    /// <summary>
    /// Hides completed-result direct-navigation controls.
    /// </summary>
    private void HideResultDirectButtons()
    {
        HideButtons(resultDirectButtonImages, resultDirectButtonPressVisuals, resultDirectButtons);
    }

    /// <summary>
    /// Hides every completed-result item and authored result column.
    /// </summary>
    private void HideResultItems()
    {
        HideResultItemViews(standardOperationalResultItems);
        HideResultItemViews(standardDestroyedResultItems);
        HideResultItemViews(personnelOperationalResultItems);
        HideResultItemViews(personnelDestroyedResultItems);
        resultStandardOperationalColumn.gameObject.SetActive(false);
        resultStandardDestroyedColumn.gameObject.SetActive(false);
        resultPersonnelOperationalColumn.gameObject.SetActive(false);
        resultPersonnelDestroyedColumn.gameObject.SetActive(false);
        resultRowsScrollArea.gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides instantiated completed-result item views.
    /// </summary>
    /// <param name="items">The result item views to hide.</param>
    private static void HideResultItemViews(IEnumerable<BattleResultItemView> items)
    {
        foreach (BattleResultItemView item in items)
            item.gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides instantiated pending-combat rows.
    /// </summary>
    private void HideRows()
    {
        foreach (BattleAlertRowView row in rowViews)
            row.gameObject.SetActive(false);
    }

    /// <summary>
    /// Clears dynamic presentation and hides the battle-alert window.
    /// </summary>
    private void RenderHidden()
    {
        HidePendingPresentation();
        HideResultPresentation();
        HideButtons(viewButtonImages, viewButtonPressVisuals, viewButtons);
        HideButtons(commandButtonImages, commandButtonPressVisuals, commandButtons);
        UILayout.SetImageTexture(panelBackgroundImage, null);
        UILayout.SetImageTexture(frameImage, null);
        resetRowsScroll = true;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns whether a new immutable presentation changes scrollable content selection.
    /// </summary>
    /// <param name="previous">The previously rendered presentation.</param>
    /// <param name="next">The presentation about to be rendered.</param>
    /// <returns>True when scroll position should return to the top.</returns>
    private static bool HasScrollableContentChanged(
        BattleAlertWindowRenderData previous,
        BattleAlertWindowRenderData next
    )
    {
        if (previous == null || previous.Mode != next.Mode)
            return true;
        if (next.Mode == BattleAlertWindowMode.Pending)
            return previous.Pending?.Panel != next.Pending?.Panel;
        if (next.Mode != BattleAlertWindowMode.Result)
            return false;

        return previous.Result?.Panel != next.Result?.Panel
            || previous.Result?.Category != next.Result?.Category;
    }

    /// <summary>
    /// Verifies all authored battle-alert references and template cardinalities.
    /// </summary>
    private void VerifyReferences()
    {
        RequireReference(panelBackgroundImage, nameof(panelBackgroundImage));
        RequireReference(frameImage, nameof(frameImage));
        RequireReference(titleTextField, nameof(titleTextField));
        RequireReference(headerTextField, nameof(headerTextField));
        RequireReference(summaryTextField, nameof(summaryTextField));
        RequireReference(rowsScrollArea, nameof(rowsScrollArea));
        RequireReference(rowTemplate, nameof(rowTemplate));
        RequireReference(resultPlanetaryTitleTextField, nameof(resultPlanetaryTitleTextField));
        RequireReference(resultFleetTitleTextField, nameof(resultFleetTitleTextField));
        RequireReference(resultSummaryTextField, nameof(resultSummaryTextField));
        RequireReference(resultDirectPromptTextField, nameof(resultDirectPromptTextField));
        RequireReference(
            resultPlanetaryForceHeaderTextField,
            nameof(resultPlanetaryForceHeaderTextField)
        );
        RequireReference(resultFleetForceHeaderTextField, nameof(resultFleetForceHeaderTextField));
        RequireReference(resultFleetFiltersTextField, nameof(resultFleetFiltersTextField));
        RequireReference(
            resultPlanetaryTableTitleTextField,
            nameof(resultPlanetaryTableTitleTextField)
        );
        RequireReference(resultFleetTableTitleTextField, nameof(resultFleetTableTitleTextField));
        RequireReference(resultRowsScrollArea, nameof(resultRowsScrollArea));
        RequireReference(resultStandardOperationalColumn, nameof(resultStandardOperationalColumn));
        RequireReference(resultStandardDestroyedColumn, nameof(resultStandardDestroyedColumn));
        RequireReference(
            resultPersonnelOperationalColumn,
            nameof(resultPersonnelOperationalColumn)
        );
        RequireReference(resultPersonnelDestroyedColumn, nameof(resultPersonnelDestroyedColumn));
        RequireReference(resultStandardItemTemplate, nameof(resultStandardItemTemplate));
        RequireReference(resultPersonnelItemTemplate, nameof(resultPersonnelItemTemplate));
        RequireValid(
            resultPlanetaryStandardColumnHeaderTextFields?.Length == _standardResultColumnCount,
            nameof(resultPlanetaryStandardColumnHeaderTextFields)
        );
        RequireValid(
            resultFleetStandardColumnHeaderTextFields?.Length == _standardResultColumnCount,
            nameof(resultFleetStandardColumnHeaderTextFields)
        );
        RequireValid(
            resultPlanetaryPersonnelColumnHeaderTextFields?.Length == _personnelResultColumnCount,
            nameof(resultPlanetaryPersonnelColumnHeaderTextFields)
        );
        RequireValid(
            resultFleetPersonnelColumnHeaderTextFields?.Length == _personnelResultColumnCount,
            nameof(resultFleetPersonnelColumnHeaderTextFields)
        );
        RequireValid(
            viewButtonImages?.Length == BattleAlertPanelCatalog.Ordered.Count
                && viewButtonPressVisuals?.Length == viewButtonImages.Length
                && viewButtons?.Length == viewButtonImages.Length,
            "ViewButtons"
        );
        RequireValid(
            commandButtonImages?.Length == _pendingChoices.Length
                && commandButtonPressVisuals?.Length == commandButtonImages.Length
                && commandButtons?.Length == commandButtonImages.Length,
            "CommandButtons"
        );
        RequireValid(
            resultCloseButtonImage != null
                && resultCloseButtonPressVisual != null
                && resultCloseButton != null,
            "ResultCloseButton"
        );
        RequireValid(
            resultCategoryButtonImages?.Length == BattleResultCategoryCatalog.Ordered.Count
                && resultCategoryButtonPressVisuals?.Length == resultCategoryButtonImages.Length
                && resultCategoryButtons?.Length == resultCategoryButtonImages.Length,
            "ResultCategoryButtons"
        );
        RequireValid(
            resultDirectButtonImages?.Length == _resultNavigationButtonCount
                && resultDirectButtonPressVisuals?.Length == resultDirectButtonImages.Length
                && resultDirectButtons?.Length == resultDirectButtonImages.Length,
            "ResultDirectButtons"
        );

        rowTemplate.gameObject.SetActive(false);
        resultStandardItemTemplate.gameObject.SetActive(false);
        resultPersonnelItemTemplate.gameObject.SetActive(false);
    }

    /// <summary>
    /// Verifies a required authored object reference.
    /// </summary>
    /// <param name="reference">The authored object reference.</param>
    /// <param name="fieldName">The serialized field name.</param>
    private void RequireReference(UnityEngine.Object reference, string fieldName)
    {
        RequireValid(reference != null, fieldName);
    }

    /// <summary>
    /// Verifies an authored reference group or required cardinality.
    /// </summary>
    /// <param name="valid">Whether the authored data is valid.</param>
    /// <param name="fieldName">The serialized field or group name.</param>
    private void RequireValid(bool valid, string fieldName)
    {
        if (!valid)
            throw new MissingReferenceException($"{name}/{fieldName} is missing.");
    }
}
