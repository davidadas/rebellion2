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
    private TextMeshProUGUI resultTitleTextField;

    [SerializeField]
    private TextMeshProUGUI resultSummaryTextField;

    [SerializeField]
    private TextMeshProUGUI resultForceHeaderTextField;

    [SerializeField]
    private TextMeshProUGUI resultFiltersTextField;

    [SerializeField]
    private TextMeshProUGUI resultTableTitleTextField;

    [SerializeField]
    private TextMeshProUGUI[] resultStandardColumnHeaderTextFields = Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private TextMeshProUGUI[] resultPersonnelColumnHeaderTextFields =
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
    internal event Action<BattleAlertWindowView> OpenSystemRequested;

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
    /// Raises a system-navigation request for the owning controller.
    /// </summary>
    internal void RequestOpenSystem()
    {
        OpenSystemRequested?.Invoke(this);
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
            UnityAction listener = () => ExecuteControlAction(() => RequestPrimaryPanel(panel));
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
        resultCloseButtonListener = () => ExecuteControlAction(RequestClose);
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

        UnityAction openSystem = () => ExecuteControlAction(RequestOpenSystem);
        UnityAction openFleet = () => ExecuteControlAction(RequestOpenFleet);
        resultDirectButtonListeners.Add(openSystem);
        resultDirectButtonListeners.Add(openFleet);
        resultDirectButtons[0].onClick.AddListener(openSystem);
        resultDirectButtons[1].onClick.AddListener(openFleet);
    }

    /// <summary>
    /// Detaches every authored control listener owned by this view.
    /// </summary>
    private void UnbindControls()
    {
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
    /// Plays the shared control sound before executing a local control action.
    /// </summary>
    /// <param name="action">The local action to execute.</param>
    private void ExecuteControlAction(Action action)
    {
        RequestControlPress();
        action();
    }

    /// <summary>
    /// Applies the pending-combat portion of the current presentation.
    /// </summary>
    /// <param name="data">The complete battle-alert presentation.</param>
    private void RenderPending(BattleAlertWindowRenderData data)
    {
        BattleAlertPendingRenderData pending = data.Pending;
        if (pending == null)
        {
            RenderHidden();
            return;
        }

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
        if (result == null)
        {
            RenderHidden();
            return;
        }

        HidePendingPresentation();
        HideButtons(commandButtonImages, commandButtonPressVisuals, commandButtons);
        RenderButton(resultCloseButtonPressVisual, resultCloseButton, result.ResultCloseButton);

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
        UILayout.SetTextContent(resultTitleTextField, result.Title, titleColor);
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
        UILayout.SetTextContent(resultTitleTextField, result.Title, titleColor);
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
        UILayout.SetTextContent(resultTitleTextField, result.Title, titleColor);
        UILayout.SetTextContent(resultSummaryTextField, result.Summary, titleColor);
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
        UILayout.SetTextContent(
            resultForceHeaderTextField,
            result.ResultForceHeader,
            result.ResultForceHeaderColor
        );
        resultFiltersTextField.gameObject.SetActive(true);
        UILayout.SetTextContent(resultTableTitleTextField, result.ResultTableTitle);

        if (result.UsesPersonnelColumns)
        {
            HideTextFields(resultStandardColumnHeaderTextFields);
            SetTextFields(resultPersonnelColumnHeaderTextFields, result.ResultColumnHeaders);
        }
        else
        {
            HideTextFields(resultPersonnelColumnHeaderTextFields);
            SetTextFields(resultStandardColumnHeaderTextFields, result.ResultColumnHeaders);
        }
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
            BattleAlertButtonRenderData button = i < categories.Count ? categories[i].Button : null;
            RenderButton(resultCategoryButtonPressVisuals[i], resultCategoryButtons[i], button);
        }
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
            RenderButton(pressVisuals[i], buttons[i], i < data.Count ? data[i] : null);
    }

    /// <summary>
    /// Applies one button presentation to an authored control.
    /// </summary>
    /// <param name="pressVisual">The authored pressed-state visual.</param>
    /// <param name="button">The authored button control.</param>
    /// <param name="data">The presentation to apply, or null to hide the control.</param>
    private static void RenderButton(
        RawImagePressVisual pressVisual,
        Button button,
        BattleAlertButtonRenderData data
    )
    {
        bool visible = data != null;
        button.interactable = data?.Interactable == true;

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
            RenderButton(pressVisuals[i], buttons[i], null);
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
        resultTitleTextField.gameObject.SetActive(false);
        resultSummaryTextField.gameObject.SetActive(false);
        HideResultDetailLabels();
        HideResultItems();
        HideResultCategoryButtons();
        HideResultDirectButtons();
        RenderButton(resultCloseButtonPressVisual, resultCloseButton, null);
    }

    /// <summary>
    /// Hides completed-result detail labels.
    /// </summary>
    private void HideResultDetailLabels()
    {
        resultForceHeaderTextField.gameObject.SetActive(false);
        resultFiltersTextField.gameObject.SetActive(false);
        resultTableTitleTextField.gameObject.SetActive(false);
        HideTextFields(resultStandardColumnHeaderTextFields);
        HideTextFields(resultPersonnelColumnHeaderTextFields);
    }

    /// <summary>
    /// Hides completed-result category controls.
    /// </summary>
    private void HideResultCategoryButtons()
    {
        for (int i = 0; i < resultCategoryButtons.Length; i++)
            RenderButton(resultCategoryButtonPressVisuals[i], resultCategoryButtons[i], null);
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
        if (panelBackgroundImage == null)
            throw new MissingReferenceException($"{name}/PanelBackgroundImage is missing.");
        if (frameImage == null)
            throw new MissingReferenceException($"{name}/FrameImage is missing.");
        if (titleTextField == null)
            throw new MissingReferenceException($"{name}/TitleTextField is missing.");
        if (headerTextField == null)
            throw new MissingReferenceException($"{name}/HeaderTextField is missing.");
        if (summaryTextField == null)
            throw new MissingReferenceException($"{name}/SummaryTextField is missing.");
        if (rowsScrollArea == null)
            throw new MissingReferenceException($"{name}/RowsScrollArea is missing.");
        if (rowTemplate == null)
            throw new MissingReferenceException($"{name}/RowTemplate is missing.");
        if (resultTitleTextField == null)
            throw new MissingReferenceException($"{name}/ResultTitleTextField is missing.");
        if (resultSummaryTextField == null)
            throw new MissingReferenceException($"{name}/ResultSummaryTextField is missing.");
        if (resultForceHeaderTextField == null)
            throw new MissingReferenceException($"{name}/ResultForceHeaderTextField is missing.");
        if (resultFiltersTextField == null)
            throw new MissingReferenceException($"{name}/ResultFiltersTextField is missing.");
        if (resultTableTitleTextField == null)
            throw new MissingReferenceException($"{name}/ResultTableTitleTextField is missing.");
        if (resultStandardColumnHeaderTextFields?.Length != _standardResultColumnCount)
            throw new MissingReferenceException(
                $"{name}/ResultStandardColumnHeaderTextFields are missing."
            );
        if (resultPersonnelColumnHeaderTextFields?.Length != _personnelResultColumnCount)
            throw new MissingReferenceException(
                $"{name}/ResultPersonnelColumnHeaderTextFields are missing."
            );
        if (resultRowsScrollArea == null)
            throw new MissingReferenceException($"{name}/ResultRowsScrollArea is missing.");
        if (resultStandardOperationalColumn == null)
            throw new MissingReferenceException(
                $"{name}/ResultStandardOperationalColumn is missing."
            );
        if (resultStandardDestroyedColumn == null)
            throw new MissingReferenceException(
                $"{name}/ResultStandardDestroyedColumn is missing."
            );
        if (resultPersonnelOperationalColumn == null)
            throw new MissingReferenceException(
                $"{name}/ResultPersonnelOperationalColumn is missing."
            );
        if (resultPersonnelDestroyedColumn == null)
            throw new MissingReferenceException(
                $"{name}/ResultPersonnelDestroyedColumn is missing."
            );
        if (resultStandardItemTemplate == null)
            throw new MissingReferenceException($"{name}/ResultStandardItemTemplate is missing.");
        if (resultPersonnelItemTemplate == null)
            throw new MissingReferenceException($"{name}/ResultPersonnelItemTemplate is missing.");
        if (
            viewButtonImages?.Length != BattleAlertPanelCatalog.Ordered.Count
            || viewButtonPressVisuals?.Length != viewButtonImages.Length
            || viewButtons?.Length != viewButtonImages.Length
        )
            throw new MissingReferenceException($"{name}/ViewButtons are missing.");
        if (
            commandButtonImages?.Length != _pendingChoices.Length
            || commandButtonPressVisuals?.Length != commandButtonImages.Length
            || commandButtons?.Length != commandButtonImages.Length
        )
            throw new MissingReferenceException($"{name}/CommandButtons are missing.");
        if (
            resultCloseButtonImage == null
            || resultCloseButtonPressVisual == null
            || resultCloseButton == null
        )
            throw new MissingReferenceException($"{name}/ResultCloseButton is missing.");
        if (
            resultCategoryButtonImages?.Length != BattleResultCategoryCatalog.Ordered.Count
            || resultCategoryButtonPressVisuals?.Length != resultCategoryButtonImages.Length
            || resultCategoryButtons?.Length != resultCategoryButtonImages.Length
        )
            throw new MissingReferenceException($"{name}/ResultCategoryButtons are missing.");
        if (
            resultDirectButtonImages?.Length != _resultNavigationButtonCount
            || resultDirectButtonPressVisuals?.Length != resultDirectButtonImages.Length
            || resultDirectButtons?.Length != resultDirectButtonImages.Length
        )
            throw new MissingReferenceException($"{name}/ResultDirectButtons are missing.");

        rowTemplate.gameObject.SetActive(false);
        resultStandardItemTemplate.gameObject.SetActive(false);
        resultPersonnelItemTemplate.gameObject.SetActive(false);
    }
}
