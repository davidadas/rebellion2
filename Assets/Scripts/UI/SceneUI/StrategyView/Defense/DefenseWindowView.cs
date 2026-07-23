using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders an authored Defense window and emits semantic Defense-window gestures.
/// </summary>
public sealed class DefenseWindowView : MonoBehaviour, IPointerClickHandler
{
    private readonly List<StrategyUnitCardView> itemCards = new List<StrategyUnitCardView>();
    private readonly List<string> renderedItemNames = new List<string>();

    [Header("Window")]
    [SerializeField]
    private UIWindow windowShell;

    [SerializeField]
    private RawImage titleImage;

    [SerializeField]
    private TextMeshProUGUI captionTextField;

    [Header("Tabs")]
    [SerializeField]
    private RawImage[] tabImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] tabPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] tabButtons = Array.Empty<Button>();

    [SerializeField]
    private TextMeshProUGUI tabTitleTextField;

    [SerializeField]
    private TextMeshProUGUI garrisonRequirementTextField;

    [Header("Items")]
    [SerializeField]
    private ScrollAreaView itemsScrollArea;

    [SerializeField]
    private GridLayoutGroup itemsGridLayout;

    [SerializeField]
    private StrategyUnitCardView itemCardTemplate;

    private DefenseWindowTab currentActiveTab = DefenseWindowTab.Personnel;
    private int currentWindowX;
    private int currentWindowY;
    private DefenseWindowTab renderedActiveTab = DefenseWindowTab.Personnel;
    private bool renderedAnyItems;
    private bool scrollEventsBound;
    private UnityAction[] tabListeners = Array.Empty<UnityAction>();

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    internal event Action<DefenseWindowView> Destroyed;

    /// <summary>
    /// Occurs when the item is double-clicked.
    /// </summary>
    internal event Action<DefenseWindowView, int, PointerEventData> ItemDoubleClicked;

    /// <summary>
    /// Occurs when a pointer drop is received by the item.
    /// </summary>
    internal event Action<DefenseWindowView, int, PointerEventData> ItemDropped;

    /// <summary>
    /// Occurs when the item is pressed.
    /// </summary>
    internal event Action<DefenseWindowView, int, PointerEventData> ItemPressed;

    /// <summary>
    /// Occurs when the item is released.
    /// </summary>
    internal event Action<DefenseWindowView, int, PointerEventData> ItemReleased;

    /// <summary>
    /// Occurs while the scroll area is dragged.
    /// </summary>
    internal event Action<DefenseWindowView, PointerEventData> ScrollDragged;

    /// <summary>
    /// Occurs when scrolling ends.
    /// </summary>
    internal event Action<DefenseWindowView, PointerEventData> ScrollDragEnded;

    /// <summary>
    /// Occurs when the surface is clicked.
    /// </summary>
    internal event Action<DefenseWindowView, PointerEventData> SurfaceClicked;

    /// <summary>
    /// Occurs when a tab request is raised.
    /// </summary>
    internal event Action<DefenseWindowView, DefenseWindowTab> TabRequested;

    internal UIWindow WindowShell => windowShell;

    /// <summary>
    /// Gets the item column count.
    /// </summary>
    internal int ItemColumnCount => Mathf.Max(1, itemsGridLayout.constraintCount);

    /// <summary>
    /// Verifies the authored hierarchy and binds local controls.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindControls();
        BindScrollEvents();
    }

    /// <summary>
    /// Releases local subscriptions and notifies the feature controller.
    /// </summary>
    private void OnDestroy()
    {
        UnbindControls();
        UnbindItemCards();
        UnbindScrollEvents();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies one complete Defense-window presentation snapshot.
    /// </summary>
    /// <param name="data">The immutable Defense-window snapshot.</param>
    public void Render(DefenseWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        currentWindowX = data.X;
        currentWindowY = data.Y;
        currentActiveTab = data.ActiveTab;

        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        UILayout.SetInteractiveImageTexture(titleImage, data.TitleTexture);
        UILayout.SetTextContent(captionTextField, data.Caption);
        RenderTabs(data.Tabs);
        UILayout.SetTextContent(tabTitleTextField, data.TabTitle);
        UILayout.SetTextContent(garrisonRequirementTextField, data.GarrisonRequirementText);
        RenderItems(data.Items);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Emits a semantic surface click for targeting or selection clearing.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData?.button == PointerEventData.InputButton.Left)
            SurfaceClicked?.Invoke(this, eventData);
    }

    /// <summary>
    /// Reports whether a pointer event originated from one rendered unit card.
    /// </summary>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <returns>True when a rendered unit card received the event.</returns>
    internal bool IsSelectionItemClick(PointerEventData eventData)
    {
        return TryGetItemIndex(eventData, out _);
    }

    /// <summary>
    /// Resolves a rendered unit-card index from the pointer raycast.
    /// </summary>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <param name="itemIndex">Receives the rendered unit-card index.</param>
    /// <returns>True when the pointer raycast belongs to a rendered unit card.</returns>
    internal bool TryGetItemIndex(PointerEventData eventData, out int itemIndex)
    {
        itemIndex = -1;
        StrategyUnitCardView card = GetRaycastTarget(eventData)
            ?.GetComponentInParent<StrategyUnitCardView>();
        if (card == null || !itemCards.Contains(card))
            return false;

        itemIndex = card.Index;
        return true;
    }

    /// <summary>
    /// Reports whether one unit card's authored entity image contains the pointer.
    /// </summary>
    /// <param name="itemIndex">The rendered unit-card index.</param>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <returns>True when the card's drag image contains the pointer.</returns>
    internal bool ItemContainsDragSource(int itemIndex, PointerEventData eventData)
    {
        return itemIndex >= 0
            && itemIndex < itemCards.Count
            && itemCards[itemIndex].ContainsDragSource(eventData);
    }

    /// <summary>
    /// Converts a pointer event to authored strategy-desktop coordinates.
    /// </summary>
    /// <param name="eventData">The pointer event to convert.</param>
    /// <param name="x">Receives the source-space horizontal coordinate.</param>
    /// <param name="y">Receives the source-space vertical coordinate.</param>
    /// <returns>True when the pointer could be converted.</returns>
    internal bool TryGetDesktopPosition(PointerEventData eventData, out int x, out int y)
    {
        x = 0;
        y = 0;
        return eventData != null
            && windowShell.TryGetDesktopPosition(eventData, eventData.position, out x, out y);
    }

    /// <summary>
    /// Creates a drag preview from one controller-selected unit-card image.
    /// </summary>
    /// <param name="itemIndex">The selected unit-card index.</param>
    /// <param name="sourceX">The pointer's source-space horizontal coordinate.</param>
    /// <param name="sourceY">The pointer's source-space vertical coordinate.</param>
    /// <param name="preview">Receives the drag preview.</param>
    /// <returns>True when a drawable preview source is available.</returns>
    internal bool TryCreateDragPreview(
        int itemIndex,
        int sourceX,
        int sourceY,
        out DragPreview preview
    )
    {
        preview = null;
        if (
            itemIndex < 0
            || itemIndex >= itemCards.Count
            || !itemCards[itemIndex]
                .TryGetDragImage(out Texture texture, out RectTransform imageTransform)
        )
            return false;

        RectInt cardRect = UILayout.GetSourceRect(itemCards[itemIndex].transform as RectTransform);
        RectInt imageRect = UILayout.GetSourceRect(imageTransform);
        RectInt sourceRect = GetVisibleContentRect(OffsetRect(imageRect, cardRect));
        preview = UILayout.CreateDragPreview(texture, sourceRect, sourceX, sourceY);
        return preview != null;
    }

    /// <summary>
    /// Calculates the authored scroll-content height for a unit-card count.
    /// </summary>
    /// <param name="itemCount">The number of rendered unit cards.</param>
    /// <returns>The required source-space content height.</returns>
    internal int GetItemScrollContentHeight(int itemCount)
    {
        int rows = Mathf.CeilToInt(itemCount / (float)ItemColumnCount);
        return rows * GetItemCellHeight();
    }

    /// <summary>
    /// Applies the ordered tab textures and pressed states.
    /// </summary>
    /// <param name="tabs">The ordered tab presentations.</param>
    private void RenderTabs(IReadOnlyList<DefenseWindowTabRenderData> tabs)
    {
        if (
            tabs == null
            || tabs.Count != DefenseWindowRenderData.TabCount
            || tabImages.Length != DefenseWindowRenderData.TabCount
        )
            throw new ArgumentException(
                "Defense tab presentation count does not match the prefab."
            );

        for (int i = 0; i < tabImages.Length; i++)
        {
            if (tabs[i].Tab != DefenseWindowRenderData.OrderedTabs[i])
                throw new ArgumentException(
                    "Defense tab presentation order does not match the prefab."
                );

            UILayout.SetInteractiveImageTexture(tabImages[i], tabs[i].Texture);
            tabPressVisuals[i].SetTextures(tabs[i].Texture, tabs[i].PressedTexture);
        }
    }

    /// <summary>
    /// Renders all ordered unit cards and reconciles the authored scroll area.
    /// </summary>
    /// <param name="items">The ordered unit-card presentations.</param>
    private void RenderItems(IReadOnlyList<StrategyUnitCardRenderData> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        bool resetScroll = ItemsChanged(items);
        itemsScrollArea.SetContentHeight(
            GetItemScrollContentHeight(items.Count),
            GetItemScrollStep(),
            resetScroll
        );

        for (int i = 0; i < items.Count; i++)
        {
            StrategyUnitCardView card = GetItemCard(i);
            card.SetIndex(i);
            card.Render(items[i]);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(itemsScrollArea.ContentRoot);
        for (int i = items.Count; i < itemCards.Count; i++)
            itemCards[i].gameObject.SetActive(false);

        renderedAnyItems = true;
        renderedActiveTab = currentActiveTab;
        renderedItemNames.Clear();
        for (int i = 0; i < items.Count; i++)
            renderedItemNames.Add(items[i].Name);
    }

    /// <summary>
    /// Gets or creates one repeated unit-card view.
    /// </summary>
    /// <param name="index">The requested card index.</param>
    /// <returns>The card assigned to the requested index.</returns>
    private StrategyUnitCardView GetItemCard(int index)
    {
        while (itemCards.Count <= index)
            itemCards.Add(CreateItemCard(itemCards.Count));

        return itemCards[index];
    }

    /// <summary>
    /// Instantiates and binds one repeated unit card from the authored template.
    /// </summary>
    /// <param name="index">The stable card index.</param>
    /// <returns>The bound unit-card view.</returns>
    private StrategyUnitCardView CreateItemCard(int index)
    {
        StrategyUnitCardView card = Instantiate(itemCardTemplate, itemsScrollArea.ContentRoot);
        card.name = $"ItemCard{index}";
        card.gameObject.SetActive(false);
        card.DoubleClicked += HandleItemDoubleClicked;
        card.Dropped += HandleItemDropped;
        card.Pressed += HandleItemPressed;
        card.Released += HandleItemReleased;
        return card;
    }

    /// <summary>
    /// Binds the authored tab controls exactly once.
    /// </summary>
    private void BindControls()
    {
        tabListeners = new UnityAction[tabButtons.Length];
        for (int i = 0; i < tabButtons.Length; i++)
        {
            DefenseWindowTab tab = DefenseWindowRenderData.OrderedTabs[i];
            tabListeners[i] = () => TabRequested?.Invoke(this, tab);
            tabButtons[i].onClick.AddListener(tabListeners[i]);
        }
    }

    /// <summary>
    /// Releases the authored tab-control subscriptions.
    /// </summary>
    private void UnbindControls()
    {
        int count = Mathf.Min(tabButtons.Length, tabListeners.Length);
        for (int i = 0; i < count; i++)
        {
            if (tabButtons[i] != null && tabListeners[i] != null)
                tabButtons[i].onClick.RemoveListener(tabListeners[i]);
        }
        tabListeners = Array.Empty<UnityAction>();
    }

    /// <summary>
    /// Binds the authored scroll-area drag relay exactly once.
    /// </summary>
    private void BindScrollEvents()
    {
        if (scrollEventsBound)
            return;

        itemsScrollArea.Dragged += HandleScrollDragged;
        itemsScrollArea.DragEnded += HandleScrollDragEnded;
        scrollEventsBound = true;
    }

    /// <summary>
    /// Releases the authored scroll-area drag relay.
    /// </summary>
    private void UnbindScrollEvents()
    {
        if (!scrollEventsBound)
            return;

        itemsScrollArea.Dragged -= HandleScrollDragged;
        itemsScrollArea.DragEnded -= HandleScrollDragEnded;
        scrollEventsBound = false;
    }

    /// <summary>
    /// Releases repeated unit-card subscriptions.
    /// </summary>
    private void UnbindItemCards()
    {
        foreach (StrategyUnitCardView card in itemCards)
        {
            if (card == null)
                continue;

            card.DoubleClicked -= HandleItemDoubleClicked;
            card.Dropped -= HandleItemDropped;
            card.Pressed -= HandleItemPressed;
            card.Released -= HandleItemReleased;
        }
    }

    /// <summary>
    /// Emits one semantic unit-card press.
    /// </summary>
    /// <param name="card">The pressed unit card.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleItemPressed(StrategyUnitCardView card, PointerEventData eventData)
    {
        if (card != null)
            ItemPressed?.Invoke(this, card.Index, eventData);
    }

    /// <summary>
    /// Emits one semantic unit-card release.
    /// </summary>
    /// <param name="card">The released unit card.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleItemReleased(StrategyUnitCardView card, PointerEventData eventData)
    {
        if (card != null)
            ItemReleased?.Invoke(this, card.Index, eventData);
    }

    /// <summary>
    /// Emits one semantic unit-card drop.
    /// </summary>
    /// <param name="card">The drop-target unit card.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleItemDropped(StrategyUnitCardView card, PointerEventData eventData)
    {
        if (card != null)
            ItemDropped?.Invoke(this, card.Index, eventData);
    }

    /// <summary>
    /// Emits one semantic unit-card double click.
    /// </summary>
    /// <param name="card">The double-clicked unit card.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleItemDoubleClicked(StrategyUnitCardView card, PointerEventData eventData)
    {
        if (card != null)
            ItemDoubleClicked?.Invoke(this, card.Index, eventData);
    }

    /// <summary>
    /// Emits one semantic scroll-drag update.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleScrollDragged(PointerEventData eventData)
    {
        if (eventData != null)
            ScrollDragged?.Invoke(this, eventData);
    }

    /// <summary>
    /// Emits one semantic scroll-drag completion.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleScrollDragEnded(PointerEventData eventData)
    {
        if (eventData != null)
            ScrollDragEnded?.Invoke(this, eventData);
    }

    /// <summary>
    /// Reports whether the ordered item identity changed since the previous render.
    /// </summary>
    /// <param name="items">The current ordered unit-card presentations.</param>
    /// <returns>True when scroll position should reset.</returns>
    private bool ItemsChanged(IReadOnlyList<StrategyUnitCardRenderData> items)
    {
        if (
            !renderedAnyItems
            || renderedActiveTab != currentActiveTab
            || renderedItemNames.Count != items.Count
        )
            return true;

        for (int i = 0; i < items.Count; i++)
        {
            if (renderedItemNames[i] != items[i].Name)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the authored unit-card row height.
    /// </summary>
    /// <returns>The source-space row height.</returns>
    private int GetItemCellHeight()
    {
        return Mathf.RoundToInt(itemsGridLayout.cellSize.y);
    }

    /// <summary>
    /// Gets the authored scroll increment for one unit-card row.
    /// </summary>
    /// <returns>The source-space scroll increment.</returns>
    private int GetItemScrollStep()
    {
        return GetItemCellHeight();
    }

    /// <summary>
    /// Converts one content-space rectangle to visible strategy-desktop coordinates.
    /// </summary>
    /// <param name="contentRect">The rectangle relative to the scroll content.</param>
    /// <returns>The visible source-space rectangle.</returns>
    private RectInt GetVisibleContentRect(RectInt contentRect)
    {
        RectInt scrollAreaRect = UILayout.GetSourceRect(itemsScrollArea.transform as RectTransform);
        RectInt viewportRect = UILayout.GetSourceRect(itemsScrollArea.ViewportRoot);
        int scrollY = Mathf.RoundToInt(itemsScrollArea.ContentRoot.anchoredPosition.y);
        return new RectInt(
            currentWindowX + scrollAreaRect.x + viewportRect.x + contentRect.x,
            currentWindowY + scrollAreaRect.y + viewportRect.y + contentRect.y - scrollY,
            contentRect.width,
            contentRect.height
        );
    }

    /// <summary>
    /// Resolves the current pointer raycast target.
    /// </summary>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <returns>The current or pressed raycast target.</returns>
    private static GameObject GetRaycastTarget(PointerEventData eventData)
    {
        return eventData == null
            ? null
            : eventData.pointerCurrentRaycast.gameObject
                ?? eventData.pointerPressRaycast.gameObject;
    }

    /// <summary>
    /// Offsets a child rectangle by its parent rectangle.
    /// </summary>
    /// <param name="rect">The child rectangle.</param>
    /// <param name="offset">The parent rectangle.</param>
    /// <returns>The combined rectangle.</returns>
    private static RectInt OffsetRect(RectInt rect, RectInt offset)
    {
        return new RectInt(offset.x + rect.x, offset.y + rect.y, rect.width, rect.height);
    }

    /// <summary>
    /// Verifies all required authored references.
    /// </summary>
    private void VerifyReferences()
    {
        if (windowShell == null)
            throw new MissingReferenceException($"{name}/WindowShell is missing.");
        if (titleImage == null)
            throw new MissingReferenceException($"{name}/TitleImage is missing.");
        if (captionTextField == null)
            throw new MissingReferenceException($"{name}/CaptionTextField is missing.");
        if (tabImages == null || tabImages.Length != DefenseWindowRenderData.TabCount)
            throw new MissingReferenceException($"{name}/Tab images are missing.");
        if (tabPressVisuals == null || tabPressVisuals.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/Tab press visuals are missing.");
        if (tabButtons == null || tabButtons.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/Tab buttons are missing.");
        for (int i = 0; i < tabImages.Length; i++)
        {
            if (tabImages[i] == null)
                throw new MissingReferenceException(
                    $"{name}/{DefenseWindowRenderData.OrderedTabs[i]}TabButtonImage is missing."
                );
            if (tabPressVisuals[i] == null)
                throw new MissingReferenceException($"{name}/TabPressVisual{i} is missing.");
            if (tabButtons[i] == null)
                throw new MissingReferenceException($"{name}/TabButton{i} is missing.");
        }
        if (tabTitleTextField == null)
            throw new MissingReferenceException($"{name}/TabTitleTextField is missing.");
        if (garrisonRequirementTextField == null)
            throw new MissingReferenceException($"{name}/GarrisonRequirementTextField is missing.");
        if (itemsScrollArea == null)
            throw new MissingReferenceException($"{name}/ItemsScrollArea is missing.");
        if (itemsGridLayout == null)
            throw new MissingReferenceException($"{name}/ItemsGridLayout is missing.");
        if (itemCardTemplate == null)
            throw new MissingReferenceException($"{name}/ItemCardTemplate is missing.");
        itemCardTemplate.gameObject.SetActive(false);
    }
}
