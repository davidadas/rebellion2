using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders an authored construction window and emits semantic construction interactions.
/// </summary>
public sealed class ConstructionWindowView : MonoBehaviour, IPointerClickHandler
{
    private readonly List<StrategyDropdownItemView> dropdownItemRows =
        new List<StrategyDropdownItemView>();
    private readonly List<string> renderedDropdownItemNames = new List<string>();

    [Header("Frame")]
    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage titleImage;

    [SerializeField]
    private TextMeshProUGUI captionTextField;

    [SerializeField]
    private RawImage[] buttonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] buttonPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private int[] buttonActions = Array.Empty<int>();

    [Header("Selection")]
    [SerializeField]
    private RawImage selectedItemImage;

    [SerializeField]
    private TextMeshProUGUI selectedNameTextField;

    [SerializeField]
    private TextMeshProUGUI buildCountLabelTextField;

    [SerializeField]
    private TextMeshProUGUI buildCountTextField;

    [SerializeField]
    private RawImage incrementButtonImage;

    [SerializeField]
    private RawImagePressVisual incrementButtonPressVisual;

    [SerializeField]
    private RawImage decrementButtonImage;

    [SerializeField]
    private RawImagePressVisual decrementButtonPressVisual;

    [SerializeField]
    private TextMeshProUGUI constructionCostTextField;

    [SerializeField]
    private TextMeshProUGUI maintenanceCostTextField;

    [SerializeField]
    private TextMeshProUGUI completionLabelTextField;

    [SerializeField]
    private TextMeshProUGUI completionValueTextField;

    [SerializeField]
    private TextMeshProUGUI completionDaysTextField;

    [SerializeField]
    private TextMeshProUGUI deploymentLabelTextField;

    [SerializeField]
    private TextMeshProUGUI deploymentValueTextField;

    [SerializeField]
    private TextMeshProUGUI deploymentDaysTextField;

    [Header("Commands")]
    [SerializeField]
    private RawImage dropdownButtonImage;

    [SerializeField]
    private RawImagePressVisual dropdownButtonPressVisual;

    [SerializeField]
    private RawImage infoButtonImage;

    [SerializeField]
    private RawImagePressVisual infoButtonPressVisual;

    [SerializeField]
    private RawImage okButtonImage;

    [SerializeField]
    private RawImagePressVisual okButtonPressVisual;

    [SerializeField]
    private RawImage cancelButtonImage;

    [SerializeField]
    private RawImagePressVisual cancelButtonPressVisual;

    [SerializeField]
    private Button incrementButton;

    [SerializeField]
    private Button decrementButton;

    [SerializeField]
    private Button dropdownButton;

    [SerializeField]
    private Button infoButton;

    [SerializeField]
    private Button okButton;

    [SerializeField]
    private Button cancelButton;

    [Header("Dropdown")]
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
    private StrategyDropdownItemView dropdownItemRowTemplate;

    [Header("Button Art")]
    [SerializeField]
    private Texture2D closeButtonUpTexture;

    [SerializeField]
    private Texture2D incrementButtonUpTexture;

    [SerializeField]
    private Texture2D incrementButtonDownTexture;

    [SerializeField]
    private Texture2D decrementButtonUpTexture;

    [SerializeField]
    private Texture2D decrementButtonDownTexture;

    [SerializeField]
    private Texture2D dropdownButtonUpTexture;

    [SerializeField]
    private Texture2D dropdownButtonDownTexture;

    [SerializeField]
    private Texture2D infoButtonUpTexture;

    [SerializeField]
    private Texture2D infoButtonDownTexture;

    [SerializeField]
    private Texture2D okButtonUpTexture;

    [SerializeField]
    private Texture2D okButtonDownTexture;

    [SerializeField]
    private Texture2D okButtonDisabledTexture;

    [SerializeField]
    private Texture2D cancelButtonUpTexture;

    [SerializeField]
    private Texture2D cancelButtonDownTexture;

    private RectInt selectedItemSlotRect;
    private bool hasSelectedItemSlotRect;
    private bool renderedAnyDropdownItems;
    private bool renderedDropdownOpen;
    private ConstructionWindowRenderData lastData;

    /// <summary>
    /// Occurs when a cancel request is raised.
    /// </summary>
    public event Action<ConstructionWindowView> CancelRequested;

    /// <summary>
    /// Occurs when a decrement request is raised.
    /// </summary>
    public event Action<ConstructionWindowView> DecrementRequested;

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    public event Action<ConstructionWindowView> Destroyed;

    /// <summary>
    /// Occurs when a dismiss dropdown request is raised.
    /// </summary>
    public event Action<ConstructionWindowView> DismissDropdownRequested;

    /// <summary>
    /// Occurs when an increment request is raised.
    /// </summary>
    public event Action<ConstructionWindowView> IncrementRequested;

    /// <summary>
    /// Occurs when an info request is raised.
    /// </summary>
    public event Action<ConstructionWindowView> InfoRequested;

    /// <summary>
    /// Occurs when the item is selected.
    /// </summary>
    public event Action<ConstructionWindowView, int> ItemSelected;

    /// <summary>
    /// Occurs when a start request is raised.
    /// </summary>
    public event Action<ConstructionWindowView> StartRequested;

    /// <summary>
    /// Occurs when a toggle dropdown request is raised.
    /// </summary>
    public event Action<ConstructionWindowView> ToggleDropdownRequested;

    /// <summary>
    /// Verifies authored references and binds local input listeners.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        incrementButton.onClick.AddListener(RequestIncrement);
        decrementButton.onClick.AddListener(RequestDecrement);
        dropdownButton.onClick.AddListener(RequestToggleDropdown);
        infoButton.onClick.AddListener(RequestInfo);
        okButton.onClick.AddListener(RequestStart);
        cancelButton.onClick.AddListener(RequestCancel);
    }

    /// <summary>
    /// Removes local listeners and notifies the feature controller of destruction.
    /// </summary>
    private void OnDestroy()
    {
        if (incrementButton != null)
            incrementButton.onClick.RemoveListener(RequestIncrement);
        if (decrementButton != null)
            decrementButton.onClick.RemoveListener(RequestDecrement);
        if (dropdownButton != null)
            dropdownButton.onClick.RemoveListener(RequestToggleDropdown);
        if (infoButton != null)
            infoButton.onClick.RemoveListener(RequestInfo);
        if (okButton != null)
            okButton.onClick.RemoveListener(RequestStart);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(RequestCancel);
        UnbindDropdownRows();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies a complete construction-window presentation to the authored hierarchy.
    /// </summary>
    /// <param name="data">The immutable presentation snapshot.</param>
    public void Render(ConstructionWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        lastData = data;
        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        UILayout.SetInteractiveImageTexture(titleImage, data.TitleTexture);
        RenderWindowButtons();
        RenderSelectedItem(data);
        RenderSelectionDetails(data);
        RenderDropdown(data);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Requests dismissal when a left click reaches the window behind an open dropdown.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && lastData?.DropdownOpen == true)
            DismissDropdownRequested?.Invoke(this);
    }

    /// <summary>
    /// Returns the authored dropdown content height for a projected item count.
    /// </summary>
    /// <param name="itemCount">The projected item count.</param>
    /// <returns>The source-space content height.</returns>
    internal int GetDropdownScrollContentHeight(int itemCount)
    {
        return itemCount * dropdownItemRowTemplate.Height;
    }

    /// <summary>
    /// Emits a request to increment the build count.
    /// </summary>
    internal void RequestIncrement()
    {
        IncrementRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits a request to decrement the build count.
    /// </summary>
    internal void RequestDecrement()
    {
        DecrementRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits a request to toggle the build-item dropdown.
    /// </summary>
    internal void RequestToggleDropdown()
    {
        ToggleDropdownRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits a request to open Encyclopedia information for the selected build item.
    /// </summary>
    internal void RequestInfo()
    {
        InfoRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits a request to start the selected construction order.
    /// </summary>
    internal void RequestStart()
    {
        StartRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits a request to close this construction window.
    /// </summary>
    internal void RequestCancel()
    {
        CancelRequested?.Invoke(this);
    }

    /// <summary>
    /// Applies the selected item image and name.
    /// </summary>
    /// <param name="data">The current presentation snapshot.</param>
    private void RenderSelectedItem(ConstructionWindowRenderData data)
    {
        if (!hasSelectedItemSlotRect)
        {
            selectedItemSlotRect = UILayout.GetSourceRect(selectedItemImage.rectTransform);
            hasSelectedItemSlotRect = true;
        }

        bool visible = data.HasSelection;
        selectedItemImage.gameObject.SetActive(visible && data.SelectedTexture != null);
        if (visible && data.SelectedTexture != null)
            UILayout.SetHorizontallyCenteredImage(
                selectedItemImage,
                data.SelectedTexture,
                selectedItemSlotRect
            );

        selectedNameTextField.gameObject.SetActive(visible);
        if (visible)
            UILayout.SetTextContent(selectedNameTextField, data.SelectedName);
    }

    /// <summary>
    /// Applies dynamic values and command states for the current selection.
    /// </summary>
    /// <param name="data">The current presentation snapshot.</param>
    private void RenderSelectionDetails(ConstructionWindowRenderData data)
    {
        SetSelectionControlsVisible(data.HasSelection);
        if (!data.HasSelection)
            return;

        UILayout.SetTextContent(buildCountTextField, data.BuildCount.ToString());
        UILayout.SetTextContent(constructionCostTextField, data.ConstructionCost);
        UILayout.SetTextContent(maintenanceCostTextField, data.MaintenanceCost);
        RenderEstimate(
            completionValueTextField,
            completionDaysTextField,
            data.CompletionEstimate,
            data.CompletionHasDays
        );
        RenderEstimate(
            deploymentValueTextField,
            deploymentDaysTextField,
            data.DeploymentEstimate,
            data.DeploymentHasDays
        );
        incrementButtonPressVisual.SetInteractiveTextures(
            incrementButtonUpTexture,
            incrementButtonDownTexture
        );
        decrementButtonPressVisual.SetInteractiveTextures(
            decrementButtonUpTexture,
            decrementButtonDownTexture
        );
        dropdownButtonPressVisual.SetInteractiveTextures(
            data.DropdownOpen ? dropdownButtonDownTexture : dropdownButtonUpTexture,
            dropdownButtonDownTexture
        );
        infoButtonPressVisual.SetInteractiveTextures(infoButtonUpTexture, infoButtonDownTexture);
        okButtonPressVisual.SetInteractiveTextures(
            data.CanStart ? okButtonUpTexture : okButtonDisabledTexture,
            data.CanStart ? okButtonDownTexture : null
        );
        cancelButtonPressVisual.SetInteractiveTextures(
            cancelButtonUpTexture,
            cancelButtonDownTexture
        );
        okButton.interactable = data.CanStart;
    }

    /// <summary>
    /// Shows or hides every authored selection control as one coherent group.
    /// </summary>
    /// <param name="visible">Whether selection controls should be visible.</param>
    private void SetSelectionControlsVisible(bool visible)
    {
        GameObject[] objects =
        {
            buildCountLabelTextField.gameObject,
            buildCountTextField.gameObject,
            constructionCostTextField.gameObject,
            maintenanceCostTextField.gameObject,
            completionLabelTextField.gameObject,
            completionValueTextField.gameObject,
            completionDaysTextField.gameObject,
            deploymentLabelTextField.gameObject,
            deploymentValueTextField.gameObject,
            deploymentDaysTextField.gameObject,
            incrementButtonImage.gameObject,
            decrementButtonImage.gameObject,
            dropdownButtonImage.gameObject,
            infoButtonImage.gameObject,
            okButtonImage.gameObject,
            cancelButtonImage.gameObject,
        };
        foreach (GameObject item in objects)
            item.SetActive(visible);
    }

    /// <summary>
    /// Applies one projected estimate and its optional days suffix.
    /// </summary>
    /// <param name="valueTextField">The estimate value field.</param>
    /// <param name="daysTextField">The authored days field.</param>
    /// <param name="value">The displayed estimate value.</param>
    /// <param name="showDays">Whether the days field is visible.</param>
    private static void RenderEstimate(
        TextMeshProUGUI valueTextField,
        TextMeshProUGUI daysTextField,
        string value,
        bool showDays
    )
    {
        UILayout.SetTextContent(valueTextField, value);
        daysTextField.gameObject.SetActive(showDays);
    }

    /// <summary>
    /// Applies authored chrome-button textures.
    /// </summary>
    private void RenderWindowButtons()
    {
        for (int i = 0; i < buttonImages.Length; i++)
        {
            RawImage image = buttonImages[i];
            if (image == null)
                continue;

            Texture upTexture =
                GetButtonAction(i) == StrategyWindowButtonActions.CloseWindow
                    ? closeButtonUpTexture
                    : image.texture;
            buttonPressVisuals[i].SetInteractiveTextures(upTexture, null);
        }
    }

    /// <summary>
    /// Applies dropdown visibility, rows, selection colors, and scroll reconciliation.
    /// </summary>
    /// <param name="data">The current presentation snapshot.</param>
    private void RenderDropdown(ConstructionWindowRenderData data)
    {
        dropdownRoot.gameObject.SetActive(data.DropdownOpen);
        if (!data.DropdownOpen)
        {
            HideDropdownItems();
            return;
        }

        bool resetScroll = DropdownItemsChanged(data.DropdownItems) || !renderedDropdownOpen;
        dropdownScrollArea.SetContentHeight(
            GetDropdownScrollContentHeight(data.DropdownItems.Count),
            dropdownItemRowTemplate.Height,
            resetScroll
        );
        for (int i = 0; i < data.DropdownItems.Count; i++)
            GetDropdownItemRow(i).Render(data.DropdownItems[i]);

        HideDropdownRowsFrom(data.DropdownItems.Count);
        StoreRenderedDropdownItems(data.DropdownItems);
        renderedDropdownOpen = true;
    }

    /// <summary>
    /// Gets or creates a reusable authored dropdown row instance.
    /// </summary>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable row view.</returns>
    private StrategyDropdownItemView GetDropdownItemRow(int index)
    {
        while (dropdownItemRows.Count <= index)
        {
            StrategyDropdownItemView row = Instantiate(
                dropdownItemRowTemplate,
                dropdownScrollArea.ContentRoot
            );
            row.name = $"DropdownItemRow{dropdownItemRows.Count}";
            row.SetIndex(dropdownItemRows.Count);
            row.Clicked += HandleDropdownItemClicked;
            dropdownItemRows.Add(row);
        }

        return dropdownItemRows[index];
    }

    /// <summary>
    /// Emits a semantic selection request for a clicked dropdown row.
    /// </summary>
    /// <param name="row">The clicked row.</param>
    private void HandleDropdownItemClicked(StrategyDropdownItemView row)
    {
        if (row != null)
            ItemSelected?.Invoke(this, row.Index);
    }

    /// <summary>
    /// Releases selection subscriptions from every instantiated dropdown row.
    /// </summary>
    private void UnbindDropdownRows()
    {
        foreach (StrategyDropdownItemView row in dropdownItemRows)
        {
            if (row != null)
                row.Clicked -= HandleDropdownItemClicked;
        }
    }

    /// <summary>
    /// Determines whether dropdown identity or ordering changed since the last open render.
    /// </summary>
    /// <param name="items">The next dropdown items.</param>
    /// <returns>True when item count, identity, or order changed.</returns>
    private bool DropdownItemsChanged(IReadOnlyList<StrategyDropdownItemRenderData> items)
    {
        if (!renderedAnyDropdownItems || renderedDropdownItemNames.Count != items.Count)
            return true;

        for (int i = 0; i < items.Count; i++)
        {
            if (renderedDropdownItemNames[i] != items[i].Label)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Stores dropdown identity and ordering for scroll reconciliation.
    /// </summary>
    /// <param name="items">The rendered dropdown items.</param>
    private void StoreRenderedDropdownItems(IReadOnlyList<StrategyDropdownItemRenderData> items)
    {
        renderedAnyDropdownItems = true;
        renderedDropdownItemNames.Clear();
        for (int i = 0; i < items.Count; i++)
            renderedDropdownItemNames.Add(items[i].Label);
    }

    /// <summary>
    /// Hides all cached dropdown rows and records the closed state.
    /// </summary>
    private void HideDropdownItems()
    {
        HideDropdownRowsFrom(0);
        renderedDropdownOpen = false;
    }

    /// <summary>
    /// Hides cached dropdown rows beginning at the supplied index.
    /// </summary>
    /// <param name="firstHiddenIndex">The first row to hide.</param>
    private void HideDropdownRowsFrom(int firstHiddenIndex)
    {
        for (int i = firstHiddenIndex; i < dropdownItemRows.Count; i++)
            dropdownItemRows[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns the authored window-shell action for a chrome-button index.
    /// </summary>
    /// <param name="index">The chrome-button index.</param>
    /// <returns>The configured shell action, or zero.</returns>
    private int GetButtonAction(int index)
    {
        return index >= 0 && index < buttonActions.Length ? buttonActions[index] : 0;
    }

    /// <summary>
    /// Verifies every authored reference required by the construction presentation.
    /// </summary>
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
        if (buttonPressVisuals == null || buttonPressVisuals.Length != buttonImages.Length)
            throw new MissingReferenceException($"{name}/Button press visuals are missing.");
        if (buttonActions == null || buttonActions.Length != buttonImages.Length)
            throw new MissingReferenceException($"{name}/Button actions are missing.");
        for (int i = 0; i < buttonImages.Length; i++)
        {
            if (buttonImages[i] == null || buttonPressVisuals[i] == null)
                throw new MissingReferenceException(
                    $"{name}/Window command button slot {i} is missing."
                );
        }
        if (selectedItemImage == null)
            throw new MissingReferenceException($"{name}/SelectedItemImage is missing.");
        if (selectedNameTextField == null)
            throw new MissingReferenceException($"{name}/SelectedNameTextField is missing.");
        if (buildCountLabelTextField == null)
            throw new MissingReferenceException($"{name}/BuildCountLabelTextField is missing.");
        if (buildCountTextField == null)
            throw new MissingReferenceException($"{name}/BuildCountTextField is missing.");
        if (incrementButtonImage == null)
            throw new MissingReferenceException($"{name}/IncrementButtonImage is missing.");
        if (incrementButtonPressVisual == null)
            throw new MissingReferenceException($"{name}/IncrementButtonPressVisual is missing.");
        if (decrementButtonImage == null)
            throw new MissingReferenceException($"{name}/DecrementButtonImage is missing.");
        if (decrementButtonPressVisual == null)
            throw new MissingReferenceException($"{name}/DecrementButtonPressVisual is missing.");
        if (constructionCostTextField == null)
            throw new MissingReferenceException($"{name}/ConstructionCostTextField is missing.");
        if (maintenanceCostTextField == null)
            throw new MissingReferenceException($"{name}/MaintenanceCostTextField is missing.");
        if (completionLabelTextField == null)
            throw new MissingReferenceException($"{name}/CompletionLabelTextField is missing.");
        if (completionValueTextField == null)
            throw new MissingReferenceException($"{name}/CompletionValueTextField is missing.");
        if (completionDaysTextField == null)
            throw new MissingReferenceException($"{name}/CompletionDaysTextField is missing.");
        if (deploymentLabelTextField == null)
            throw new MissingReferenceException($"{name}/DeploymentLabelTextField is missing.");
        if (deploymentValueTextField == null)
            throw new MissingReferenceException($"{name}/DeploymentValueTextField is missing.");
        if (deploymentDaysTextField == null)
            throw new MissingReferenceException($"{name}/DeploymentDaysTextField is missing.");
        if (dropdownButtonImage == null)
            throw new MissingReferenceException($"{name}/DropdownButtonImage is missing.");
        if (dropdownButtonPressVisual == null)
            throw new MissingReferenceException($"{name}/DropdownButtonPressVisual is missing.");
        if (infoButtonImage == null)
            throw new MissingReferenceException($"{name}/InfoButtonImage is missing.");
        if (infoButtonPressVisual == null)
            throw new MissingReferenceException($"{name}/InfoButtonPressVisual is missing.");
        if (okButtonImage == null)
            throw new MissingReferenceException($"{name}/OkButtonImage is missing.");
        if (okButtonPressVisual == null)
            throw new MissingReferenceException($"{name}/OkButtonPressVisual is missing.");
        if (cancelButtonImage == null)
            throw new MissingReferenceException($"{name}/CancelButtonImage is missing.");
        if (cancelButtonPressVisual == null)
            throw new MissingReferenceException($"{name}/CancelButtonPressVisual is missing.");
        if (incrementButton == null)
            throw new MissingReferenceException($"{name}/IncrementButton is missing.");
        if (decrementButton == null)
            throw new MissingReferenceException($"{name}/DecrementButton is missing.");
        if (dropdownButton == null)
            throw new MissingReferenceException($"{name}/DropdownButton is missing.");
        if (infoButton == null)
            throw new MissingReferenceException($"{name}/InfoButton is missing.");
        if (okButton == null)
            throw new MissingReferenceException($"{name}/OkButton is missing.");
        if (cancelButton == null)
            throw new MissingReferenceException($"{name}/CancelButton is missing.");
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
        if (dropdownBackgroundImages == null || dropdownBackgroundImages.Length != 3)
            throw new MissingReferenceException($"{name}/DropdownBackgroundImages is missing.");
        if (dropdownScrollArea == null)
            throw new MissingReferenceException($"{name}/DropdownScrollArea is missing.");
        if (dropdownItemRowTemplate == null)
            throw new MissingReferenceException($"{name}/DropdownItemRowTemplate is missing.");
        if (closeButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonUpTexture is missing.");
        if (incrementButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/IncrementButtonUpTexture is missing.");
        if (incrementButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/IncrementButtonDownTexture is missing.");
        if (decrementButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/DecrementButtonUpTexture is missing.");
        if (decrementButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/DecrementButtonDownTexture is missing.");
        if (dropdownButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/DropdownButtonUpTexture is missing.");
        if (dropdownButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/DropdownButtonDownTexture is missing.");
        if (infoButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/InfoButtonUpTexture is missing.");
        if (infoButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/InfoButtonDownTexture is missing.");
        if (okButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/OkButtonUpTexture is missing.");
        if (okButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/OkButtonDownTexture is missing.");
        if (okButtonDisabledTexture == null)
            throw new MissingReferenceException($"{name}/OkButtonDisabledTexture is missing.");
        if (cancelButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CancelButtonUpTexture is missing.");
        if (cancelButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/CancelButtonDownTexture is missing.");
    }
}
