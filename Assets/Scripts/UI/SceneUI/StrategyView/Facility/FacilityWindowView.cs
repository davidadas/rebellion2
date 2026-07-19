using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders the authored facility window and reports semantic pointer interaction.
/// </summary>
public sealed class FacilityWindowView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField]
    private RawImage titleImage;

    [SerializeField]
    private TextMeshProUGUI captionTextField;

    [SerializeField]
    private RawImage[] tabImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] tabPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] tabButtons = Array.Empty<Button>();

    [SerializeField]
    private Texture2D shipyardTabActiveTexture;

    [SerializeField]
    private Texture2D shipyardTabInactiveTexture;

    [SerializeField]
    private Texture2D shipyardTabDisabledTexture;

    [SerializeField]
    private Texture2D troopTabActiveTexture;

    [SerializeField]
    private Texture2D troopTabInactiveTexture;

    [SerializeField]
    private Texture2D troopTabDisabledTexture;

    [SerializeField]
    private Texture2D constructionTabActiveTexture;

    [SerializeField]
    private Texture2D constructionTabInactiveTexture;

    [SerializeField]
    private Texture2D constructionTabDisabledTexture;

    [SerializeField]
    private Texture2D refineryTabActiveTexture;

    [SerializeField]
    private Texture2D refineryTabInactiveTexture;

    [SerializeField]
    private Texture2D refineryTabDisabledTexture;

    [SerializeField]
    private Texture2D mineTabActiveTexture;

    [SerializeField]
    private Texture2D mineTabInactiveTexture;

    [SerializeField]
    private Texture2D mineTabDisabledTexture;

    [SerializeField]
    private RawImage manufacturingStripImage;

    [SerializeField]
    private ManufacturingLaneCardView[] manufacturingCardViews =
        Array.Empty<ManufacturingLaneCardView>();

    [SerializeField]
    private RectTransform inventoryRoot;

    [SerializeField]
    private TextMeshProUGUI inventoryTitleTextField;

    [SerializeField]
    private FacilityInventoryItemView inventoryItemTemplate;

    private readonly List<FacilityInventoryItemView> inventoryItemViews =
        new List<FacilityInventoryItemView>();
    private readonly List<UnityAction> tabButtonListeners = new List<UnityAction>();

    private bool eventsBound;
    private int inventoryColumnCount;
    private int inventoryColumnStride;
    private RectInt inventoryItemTemplateRect;

    /// <summary>
    /// Occurs when the background is clicked.
    /// </summary>
    public event Action<FacilityWindowView, PointerEventData> BackgroundClicked;

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    public event Action<FacilityWindowView> Destroyed;

    /// <summary>
    /// Occurs when the inventory item is double-clicked.
    /// </summary>
    public event Action<FacilityWindowView, int, PointerEventData> InventoryItemDoubleClicked;

    /// <summary>
    /// Occurs when the inventory item is pressed.
    /// </summary>
    public event Action<FacilityWindowView, int, PointerEventData> InventoryItemPressed;

    /// <summary>
    /// Occurs when the inventory item is released.
    /// </summary>
    public event Action<FacilityWindowView, int, PointerEventData> InventoryItemReleased;

    /// <summary>
    /// Occurs when the manufacturing card is pressed.
    /// </summary>
    public event Action<FacilityWindowView, int, PointerEventData> ManufacturingCardPressed;

    /// <summary>
    /// Occurs when the manufacturing card is released.
    /// </summary>
    public event Action<FacilityWindowView, int, PointerEventData> ManufacturingCardReleased;

    /// <summary>
    /// Occurs when the tab is selected.
    /// </summary>
    public event Action<FacilityWindowView, FacilityWindowTab> TabSelected;

    public int InventoryColumnCount => inventoryColumnCount;

    /// <summary>
    /// Applies one complete facility-window presentation snapshot.
    /// </summary>
    /// <param name="data">The facility presentation data.</param>
    public void Render(FacilityWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        UILayout.SetInteractiveImageTexture(titleImage, data.TitleTexture);
        UILayout.SetTextContent(captionTextField, data.Caption);
        RenderTabs(data);

        manufacturingStripImage.gameObject.SetActive(data.ShowManufacturing);
        if (data.ShowManufacturing)
            RenderManufacturingCards(data.ManufacturingCards);
        else
            HideManufacturingCards();

        inventoryRoot.gameObject.SetActive(!data.ShowManufacturing);
        if (data.ShowManufacturing)
            HideInventoryItems();
        else
            RenderInventory(data);

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Gets the manufacturing card under a pointer event.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="index">Receives the one-based manufacturing card index.</param>
    /// <returns>True when the pointer targets an authored manufacturing card.</returns>
    public bool TryGetManufacturingCardIndex(PointerEventData eventData, out int index)
    {
        ManufacturingLaneCardView card = GetRaycastTarget(eventData)
            ?.GetComponentInParent<ManufacturingLaneCardView>();
        if (card && card.transform.IsChildOf(transform))
        {
            index = card.Index;
            return true;
        }

        index = -1;
        return false;
    }

    /// <summary>
    /// Gets the inventory item under a pointer event.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="index">Receives the zero-based inventory item index.</param>
    /// <returns>True when the pointer targets an active inventory item.</returns>
    public bool TryGetInventoryItemIndex(PointerEventData eventData, out int index)
    {
        FacilityInventoryItemView item = GetRaycastTarget(eventData)
            ?.GetComponentInParent<FacilityInventoryItemView>();
        if (item && item.transform.IsChildOf(transform) && item != inventoryItemTemplate)
        {
            index = item.Index;
            return true;
        }

        index = -1;
        return false;
    }

    /// <summary>
    /// Reports a background click that may complete strategy targeting.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData?.button == PointerEventData.InputButton.Left)
            BackgroundClicked?.Invoke(this, eventData);
    }

    /// <summary>
    /// Validates authored references and binds controls.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        CaptureInventoryLayout();
        BindEvents();
    }

    /// <summary>
    /// Releases event subscriptions and reports view destruction.
    /// </summary>
    private void OnDestroy()
    {
        UnbindEvents();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Binds authored tab and manufacturing-card controls exactly once.
    /// </summary>
    private void BindEvents()
    {
        if (eventsBound)
            return;

        for (int index = 0; index < tabButtons.Length; index++)
        {
            FacilityWindowTab tab = FacilityWindowRenderData.OrderedTabs[index];
            UnityAction listener = () => TabSelected?.Invoke(this, tab);
            tabButtonListeners.Add(listener);
            tabButtons[index].onClick.AddListener(listener);
        }

        for (int index = 0; index < manufacturingCardViews.Length; index++)
        {
            ManufacturingLaneCardView card = manufacturingCardViews[index];
            card.SetIndex((int)FacilityWindowTab.Shipyards + index);
            card.Pressed += HandleManufacturingCardPressed;
            card.Released += HandleManufacturingCardReleased;
            card.Dropped += HandleManufacturingCardReleased;
        }

        eventsBound = true;
    }

    /// <summary>
    /// Releases authored control event subscriptions.
    /// </summary>
    private void UnbindEvents()
    {
        if (!eventsBound)
            return;

        for (int index = 0; index < tabButtonListeners.Count; index++)
            tabButtons[index].onClick.RemoveListener(tabButtonListeners[index]);
        tabButtonListeners.Clear();

        for (int index = 0; index < manufacturingCardViews.Length; index++)
        {
            ManufacturingLaneCardView card = manufacturingCardViews[index];
            card.Pressed -= HandleManufacturingCardPressed;
            card.Released -= HandleManufacturingCardReleased;
            card.Dropped -= HandleManufacturingCardReleased;
        }

        for (int index = 0; index < inventoryItemViews.Count; index++)
        {
            FacilityInventoryItemView item = inventoryItemViews[index];
            item.Pressed -= HandleInventoryItemPressed;
            item.Released -= HandleInventoryItemReleased;
            item.Dropped -= HandleInventoryItemReleased;
            item.DoubleClicked -= HandleInventoryItemDoubleClicked;
        }

        eventsBound = false;
    }

    /// <summary>
    /// Forwards a manufacturing-card press with its semantic index.
    /// </summary>
    /// <param name="card">The pressed card.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleManufacturingCardPressed(
        ManufacturingLaneCardView card,
        PointerEventData eventData
    )
    {
        if (card != null && eventData != null)
            ManufacturingCardPressed?.Invoke(this, card.Index, eventData);
    }

    /// <summary>
    /// Forwards a manufacturing-card release or drop with its semantic index.
    /// </summary>
    /// <param name="card">The released card.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleManufacturingCardReleased(
        ManufacturingLaneCardView card,
        PointerEventData eventData
    )
    {
        if (card != null && eventData != null)
            ManufacturingCardReleased?.Invoke(this, card.Index, eventData);
    }

    /// <summary>
    /// Forwards an inventory-item press with its semantic index.
    /// </summary>
    /// <param name="item">The pressed inventory item.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleInventoryItemPressed(
        FacilityInventoryItemView item,
        PointerEventData eventData
    )
    {
        if (item != null && eventData != null)
            InventoryItemPressed?.Invoke(this, item.Index, eventData);
    }

    /// <summary>
    /// Forwards an inventory-item release or drop with its semantic index.
    /// </summary>
    /// <param name="item">The released inventory item.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleInventoryItemReleased(
        FacilityInventoryItemView item,
        PointerEventData eventData
    )
    {
        if (item != null && eventData != null)
            InventoryItemReleased?.Invoke(this, item.Index, eventData);
    }

    /// <summary>
    /// Forwards an inventory-item double click with its semantic index.
    /// </summary>
    /// <param name="item">The double-clicked inventory item.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleInventoryItemDoubleClicked(
        FacilityInventoryItemView item,
        PointerEventData eventData
    )
    {
        if (item != null && eventData != null)
            InventoryItemDoubleClicked?.Invoke(this, item.Index, eventData);
    }

    /// <summary>
    /// Renders authored facility tab states.
    /// </summary>
    /// <param name="data">The facility presentation data.</param>
    private void RenderTabs(FacilityWindowRenderData data)
    {
        if (
            data.Tabs.Count != FacilityWindowRenderData.TabCount
            || tabImages.Length != FacilityWindowRenderData.TabCount
        )
            throw new ArgumentException(
                "Facility tab presentation count does not match the prefab."
            );

        for (int index = 0; index < tabImages.Length; index++)
        {
            FacilityWindowTabRenderData tab = data.Tabs[index];
            if (tab.Tab != FacilityWindowRenderData.OrderedTabs[index])
                throw new ArgumentException(
                    "Facility tab presentation order does not match the prefab."
                );

            Texture2D texture =
                tab.Tab == FacilityWindowTab.Manufacturing
                    ? data.ControlTabTexture
                    : GetFacilityTabTexture(tab.Tab, tab.State);
            Texture2D pressedTexture =
                tab.State == FacilityWindowTabState.Disabled ? null
                : tab.Tab == FacilityWindowTab.Manufacturing ? data.ControlTabPressedTexture
                : GetFacilityTabTexture(tab.Tab, FacilityWindowTabState.Active);
            tabPressVisuals[index].SetInteractiveTextures(texture, pressedTexture);
        }
    }

    /// <summary>
    /// Gets the authored texture for one facility inventory tab state.
    /// </summary>
    /// <param name="tab">The facility tab.</param>
    /// <param name="state">The active, inactive, or disabled state.</param>
    /// <returns>The authored tab texture.</returns>
    private Texture2D GetFacilityTabTexture(FacilityWindowTab tab, FacilityWindowTabState state)
    {
        return tab switch
        {
            FacilityWindowTab.Shipyards => SelectStateTexture(
                state,
                shipyardTabActiveTexture,
                shipyardTabInactiveTexture,
                shipyardTabDisabledTexture
            ),
            FacilityWindowTab.Training => SelectStateTexture(
                state,
                troopTabActiveTexture,
                troopTabInactiveTexture,
                troopTabDisabledTexture
            ),
            FacilityWindowTab.Construction => SelectStateTexture(
                state,
                constructionTabActiveTexture,
                constructionTabInactiveTexture,
                constructionTabDisabledTexture
            ),
            FacilityWindowTab.Refineries => SelectStateTexture(
                state,
                refineryTabActiveTexture,
                refineryTabInactiveTexture,
                refineryTabDisabledTexture
            ),
            FacilityWindowTab.Mines => SelectStateTexture(
                state,
                mineTabActiveTexture,
                mineTabInactiveTexture,
                mineTabDisabledTexture
            ),
            _ => null,
        };
    }

    /// <summary>
    /// Selects one of three authored tab textures.
    /// </summary>
    /// <param name="state">The active, inactive, or disabled state.</param>
    /// <param name="activeTexture">The active texture.</param>
    /// <param name="inactiveTexture">The inactive texture.</param>
    /// <param name="disabledTexture">The disabled texture.</param>
    /// <returns>The texture matching the state.</returns>
    private static Texture2D SelectStateTexture(
        FacilityWindowTabState state,
        Texture2D activeTexture,
        Texture2D inactiveTexture,
        Texture2D disabledTexture
    )
    {
        return state switch
        {
            FacilityWindowTabState.Active => activeTexture,
            FacilityWindowTabState.Disabled => disabledTexture,
            FacilityWindowTabState.Inactive => inactiveTexture,
            _ => null,
        };
    }

    /// <summary>
    /// Renders all manufacturing lane cards.
    /// </summary>
    /// <param name="cards">The manufacturing lane presentations.</param>
    private void RenderManufacturingCards(IReadOnlyList<ManufacturingLaneCardRenderData> cards)
    {
        int count = Mathf.Min(cards.Count, manufacturingCardViews.Length);
        for (int index = 0; index < count; index++)
            manufacturingCardViews[index].Render(cards[index]);

        for (int index = count; index < manufacturingCardViews.Length; index++)
            manufacturingCardViews[index].gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides every manufacturing card while an inventory tab is active.
    /// </summary>
    private void HideManufacturingCards()
    {
        for (int index = 0; index < manufacturingCardViews.Length; index++)
            manufacturingCardViews[index].gameObject.SetActive(false);
    }

    /// <summary>
    /// Renders the active facility inventory tab.
    /// </summary>
    /// <param name="data">The facility presentation data.</param>
    private void RenderInventory(FacilityWindowRenderData data)
    {
        UILayout.SetTextContent(inventoryTitleTextField, data.InventoryTitle);
        for (int index = 0; index < data.InventoryItems.Count; index++)
        {
            FacilityInventoryItemView itemView = GetInventoryItemView(index);
            itemView.Render(
                index,
                data.InventoryItems[index],
                data.InventorySelectionTexture,
                GetInventoryItemRect(index)
            );
        }

        for (int index = data.InventoryItems.Count; index < inventoryItemViews.Count; index++)
            inventoryItemViews[index].gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides every inventory item while the manufacturing tab is active.
    /// </summary>
    private void HideInventoryItems()
    {
        inventoryTitleTextField.gameObject.SetActive(false);
        for (int index = 0; index < inventoryItemViews.Count; index++)
            inventoryItemViews[index].gameObject.SetActive(false);
    }

    /// <summary>
    /// Gets or creates a reusable inventory item view.
    /// </summary>
    /// <param name="index">The requested item index.</param>
    /// <returns>The reusable inventory item view.</returns>
    private FacilityInventoryItemView GetInventoryItemView(int index)
    {
        while (inventoryItemViews.Count <= index)
        {
            FacilityInventoryItemView view = Instantiate(inventoryItemTemplate, inventoryRoot);
            view.name = $"InventoryItem{inventoryItemViews.Count}";
            view.Pressed += HandleInventoryItemPressed;
            view.Released += HandleInventoryItemReleased;
            view.Dropped += HandleInventoryItemReleased;
            view.DoubleClicked += HandleInventoryItemDoubleClicked;
            inventoryItemViews.Add(view);
        }

        return inventoryItemViews[index];
    }

    /// <summary>
    /// Captures the immutable inventory geometry authored by the facility prefab.
    /// </summary>
    private void CaptureInventoryLayout()
    {
        RectInt inventoryRootRect = UILayout.GetSourceRect(inventoryRoot);
        inventoryItemTemplateRect = UILayout.GetSourceRect(
            inventoryItemTemplate.transform as RectTransform
        );
        inventoryColumnCount = Mathf.Max(
            1,
            Mathf.FloorToInt(
                (inventoryRootRect.width - inventoryItemTemplateRect.x)
                    / inventoryItemTemplateRect.width
            )
        );
        inventoryColumnStride =
            inventoryColumnCount > 1
                ? Mathf.FloorToInt(
                    (
                        inventoryRootRect.width
                        - inventoryItemTemplateRect.x * 2
                        - inventoryItemTemplateRect.width
                    ) / (float)(inventoryColumnCount - 1)
                )
                : inventoryItemTemplateRect.width;
    }

    /// <summary>
    /// Calculates inventory item bounds from the cached authored geometry.
    /// </summary>
    /// <param name="index">The zero-based inventory index.</param>
    /// <returns>The source-space item bounds.</returns>
    private RectInt GetInventoryItemRect(int index)
    {
        int column = index % inventoryColumnCount;
        int row = index / inventoryColumnCount;
        return new RectInt(
            inventoryItemTemplateRect.x + column * inventoryColumnStride,
            inventoryItemTemplateRect.y + row * inventoryItemTemplateRect.height,
            inventoryItemTemplateRect.width,
            inventoryItemTemplateRect.height
        );
    }

    /// <summary>
    /// Gets the current pointer raycast target.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <returns>The current or pressed raycast target.</returns>
    private static GameObject GetRaycastTarget(PointerEventData eventData)
    {
        return eventData?.pointerCurrentRaycast.gameObject
            ?? eventData?.pointerPressRaycast.gameObject;
    }

    /// <summary>
    /// Validates the facility view's authored child references.
    /// </summary>
    private void VerifyReferences()
    {
        if (titleImage == null)
            throw new MissingReferenceException($"{name}/TitleImage is missing.");
        if (captionTextField == null)
            throw new MissingReferenceException($"{name}/CaptionTextField is missing.");
        if (tabImages == null || tabImages.Length != FacilityWindowRenderData.TabCount)
            throw new MissingReferenceException($"{name}/Tab images are missing.");
        if (tabPressVisuals == null || tabPressVisuals.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/Tab press visuals are missing.");
        if (tabButtons == null || tabButtons.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/Tab buttons are missing.");
        for (int index = 0; index < tabImages.Length; index++)
        {
            if (
                tabImages[index] == null
                || tabPressVisuals[index] == null
                || tabButtons[index] == null
            )
                throw new MissingReferenceException($"{name}/Tab{index} is incomplete.");
        }

        VerifyTabTextures();
        if (manufacturingStripImage == null)
            throw new MissingReferenceException($"{name}/ManufacturingStripImage is missing.");
        if (manufacturingCardViews == null || manufacturingCardViews.Length == 0)
            throw new MissingReferenceException($"{name}/Manufacturing lane cards are missing.");
        for (int index = 0; index < manufacturingCardViews.Length; index++)
        {
            if (manufacturingCardViews[index] == null)
                throw new MissingReferenceException(
                    $"{name}/ManufacturingLaneCard{index} is missing."
                );
        }

        if (inventoryRoot == null)
            throw new MissingReferenceException($"{name}/Inventory is missing.");
        if (inventoryTitleTextField == null)
            throw new MissingReferenceException($"{name}/InventoryTitleTextField is missing.");
        if (inventoryItemTemplate == null)
            throw new MissingReferenceException($"{name}/InventoryItemTemplate is missing.");
        inventoryItemTemplate.gameObject.SetActive(false);
    }

    /// <summary>
    /// Validates the complete authored facility-tab texture set.
    /// </summary>
    private void VerifyTabTextures()
    {
        VerifyTabTextures(
            "Shipyard",
            shipyardTabActiveTexture,
            shipyardTabInactiveTexture,
            shipyardTabDisabledTexture
        );
        VerifyTabTextures(
            "Troop",
            troopTabActiveTexture,
            troopTabInactiveTexture,
            troopTabDisabledTexture
        );
        VerifyTabTextures(
            "Construction",
            constructionTabActiveTexture,
            constructionTabInactiveTexture,
            constructionTabDisabledTexture
        );
        VerifyTabTextures(
            "Refinery",
            refineryTabActiveTexture,
            refineryTabInactiveTexture,
            refineryTabDisabledTexture
        );
        VerifyTabTextures(
            "Mine",
            mineTabActiveTexture,
            mineTabInactiveTexture,
            mineTabDisabledTexture
        );
    }

    /// <summary>
    /// Validates one authored three-state facility tab texture set.
    /// </summary>
    /// <param name="label">The tab label used in validation errors.</param>
    /// <param name="activeTexture">The active texture.</param>
    /// <param name="inactiveTexture">The inactive texture.</param>
    /// <param name="disabledTexture">The disabled texture.</param>
    private void VerifyTabTextures(
        string label,
        Texture2D activeTexture,
        Texture2D inactiveTexture,
        Texture2D disabledTexture
    )
    {
        if (activeTexture == null)
            throw new MissingReferenceException($"{name}/{label}TabActiveTexture is missing.");
        if (inactiveTexture == null)
            throw new MissingReferenceException($"{name}/{label}TabInactiveTexture is missing.");
        if (disabledTexture == null)
            throw new MissingReferenceException($"{name}/{label}TabDisabledTexture is missing.");
    }
}
