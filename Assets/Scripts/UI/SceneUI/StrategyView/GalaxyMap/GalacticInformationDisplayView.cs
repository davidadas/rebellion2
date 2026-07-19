using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents the authored galactic-information selector and emits semantic menu input.
/// </summary>
public sealed class GalacticInformationDisplayView : MonoBehaviour
{
    [SerializeField]
    private UIRaycastArea dismissHitArea;

    [SerializeField]
    private RectTransform selectorPanel;

    [SerializeField]
    private Image backgroundImage;

    [SerializeField]
    private GalacticInformationFrameView frameView;

    [SerializeField]
    private RawImage[] categoryIconImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImage[] categoryArrowImages = Array.Empty<RawImage>();

    [SerializeField]
    private TextMeshProUGUI[] categoryTextFields = Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private UIRaycastArea[] categoryHitAreas = Array.Empty<UIRaycastArea>();

    [SerializeField]
    private GalacticInformationSubmenuView[] submenuViews =
        Array.Empty<GalacticInformationSubmenuView>();

    [SerializeField]
    private TextMeshProUGUI displayOffTextField;

    [SerializeField]
    private UIRaycastArea displayOffHitArea;

    private bool eventsBound;

    /// <summary>
    /// Raised when a category should open from hover or click input.
    /// </summary>
    public event Action<int> CategoryRequested;

    /// <summary>
    /// Raised when Unity destroys the authored selector view.
    /// </summary>
    public event Action<GalacticInformationDisplayView> Destroyed;

    /// <summary>
    /// Raised when the selector's outside dismiss area is clicked.
    /// </summary>
    public event Action DismissRequested;

    /// <summary>
    /// Raised when the pointer enters the display-off row.
    /// </summary>
    public event Action DisplayOffEntered;

    /// <summary>
    /// Raised when the pointer exits the display-off row.
    /// </summary>
    public event Action DisplayOffExited;

    /// <summary>
    /// Raised when the display-off row is selected.
    /// </summary>
    public event Action DisplayOffSelected;

    /// <summary>
    /// Raised when the pointer enters a submenu filter row.
    /// </summary>
    public event Action<int, int> FilterEntered;

    /// <summary>
    /// Raised when the pointer exits a submenu filter row.
    /// </summary>
    public event Action<int, int> FilterExited;

    /// <summary>
    /// Raised when a submenu filter is selected.
    /// </summary>
    public event Action<GalacticInformationFilterMode> FilterSelected;

    /// <summary>
    /// Validates authored references and subscribes selector input when Unity creates the view.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindEvents();
    }

    /// <summary>
    /// Releases selector input subscriptions and informs the feature controller of destruction.
    /// </summary>
    private void OnDestroy()
    {
        UnbindEvents();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies a complete immutable selector presentation snapshot.
    /// </summary>
    /// <param name="data">The selector presentation snapshot.</param>
    public void Render(GalacticInformationDisplayRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        if (!data.Visible)
        {
            HideAllSubmenus();
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        UILayout.SetSourceRect(
            selectorPanel,
            data.SelectorBounds.x,
            data.SelectorBounds.y,
            data.SelectorBounds.width,
            data.SelectorBounds.height
        );
        UILayout.SetSourceRect(
            backgroundImage.rectTransform,
            0,
            0,
            data.SelectorBounds.width,
            data.SelectorBounds.height
        );
        backgroundImage.color = data.BackgroundColor;
        backgroundImage.raycastTarget = true;
        frameView.Render(data.Frame);
        RenderCategories(data.Categories);
        RenderDisplayOffRow(data.DisplayOffRow);
    }

    /// <summary>
    /// Applies projected categories to authored selector slots.
    /// </summary>
    /// <param name="categories">The category presentations in authored-slot order.</param>
    private void RenderCategories(
        System.Collections.Generic.IReadOnlyList<GalacticInformationCategoryRenderData> categories
    )
    {
        int count = Mathf.Min(categories?.Count ?? 0, categoryHitAreas.Length);
        for (int i = 0; i < count; i++)
            RenderCategory(i, categories[i]);

        for (int i = count; i < categoryHitAreas.Length; i++)
            HideCategory(i);
    }

    /// <summary>
    /// Applies one projected category to its authored selector slot.
    /// </summary>
    /// <param name="index">The authored category-slot index.</param>
    /// <param name="data">The category presentation.</param>
    private void RenderCategory(int index, GalacticInformationCategoryRenderData data)
    {
        if (data?.Visible != true)
        {
            HideCategory(index);
            return;
        }

        categoryHitAreas[index].Render(data.HitBounds);
        ApplyImage(categoryIconImages[index], data.Icon, false);
        ApplyImage(categoryArrowImages[index], data.Arrow, true);
        ApplyText(categoryTextFields[index], data.Label);
        submenuViews[index].Render(data.Submenu, index);
    }

    /// <summary>
    /// Applies the projected display-off row to its authored controls.
    /// </summary>
    /// <param name="data">The display-off row presentation.</param>
    private void RenderDisplayOffRow(GalacticInformationTextRowRenderData data)
    {
        if (!data.Visible)
        {
            displayOffHitArea.gameObject.SetActive(false);
            displayOffTextField.gameObject.SetActive(false);
            return;
        }

        displayOffHitArea.Render(data.HitBounds);
        ApplyText(displayOffTextField, data.Label);
    }

    /// <summary>
    /// Subscribes selector, category, and submenu input exactly once.
    /// </summary>
    private void BindEvents()
    {
        if (eventsBound)
            return;

        dismissHitArea.Clicked += HandleDismissClicked;
        displayOffHitArea.Clicked += HandleDisplayOffClicked;
        displayOffHitArea.Entered += HandleDisplayOffEntered;
        displayOffHitArea.Exited += HandleDisplayOffExited;
        for (int i = 0; i < categoryHitAreas.Length; i++)
        {
            categoryHitAreas[i].Clicked += HandleCategoryRequested;
            categoryHitAreas[i].Entered += HandleCategoryRequested;
            submenuViews[i].FilterEntered += HandleFilterEntered;
            submenuViews[i].FilterExited += HandleFilterExited;
            submenuViews[i].FilterSelected += HandleFilterSelected;
        }

        eventsBound = true;
    }

    /// <summary>
    /// Releases selector, category, and submenu input subscriptions.
    /// </summary>
    private void UnbindEvents()
    {
        if (!eventsBound)
            return;

        dismissHitArea.Clicked -= HandleDismissClicked;
        displayOffHitArea.Clicked -= HandleDisplayOffClicked;
        displayOffHitArea.Entered -= HandleDisplayOffEntered;
        displayOffHitArea.Exited -= HandleDisplayOffExited;
        for (int i = 0; i < categoryHitAreas.Length; i++)
        {
            categoryHitAreas[i].Clicked -= HandleCategoryRequested;
            categoryHitAreas[i].Entered -= HandleCategoryRequested;
            submenuViews[i].FilterEntered -= HandleFilterEntered;
            submenuViews[i].FilterExited -= HandleFilterExited;
            submenuViews[i].FilterSelected -= HandleFilterSelected;
        }

        eventsBound = false;
    }

    /// <summary>
    /// Emits a semantic category-open request for one authored hit area.
    /// </summary>
    /// <param name="area">The requested category hit area.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleCategoryRequested(UIRaycastArea area, PointerEventData eventData)
    {
        int categoryIndex = Array.IndexOf(categoryHitAreas, area);
        if (categoryIndex >= 0)
            CategoryRequested?.Invoke(categoryIndex);
    }

    /// <summary>
    /// Emits display-off hover entry.
    /// </summary>
    /// <param name="area">The display-off hit area.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleDisplayOffEntered(UIRaycastArea area, PointerEventData eventData)
    {
        DisplayOffEntered?.Invoke();
    }

    /// <summary>
    /// Emits display-off hover exit.
    /// </summary>
    /// <param name="area">The display-off hit area.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleDisplayOffExited(UIRaycastArea area, PointerEventData eventData)
    {
        DisplayOffExited?.Invoke();
    }

    /// <summary>
    /// Emits display-off selection.
    /// </summary>
    /// <param name="area">The display-off hit area.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleDisplayOffClicked(UIRaycastArea area, PointerEventData eventData)
    {
        DisplayOffSelected?.Invoke();
    }

    /// <summary>
    /// Forwards submenu filter hover entry to the feature controller.
    /// </summary>
    /// <param name="categoryIndex">The filter's category index.</param>
    /// <param name="filterIndex">The hovered filter index.</param>
    private void HandleFilterEntered(int categoryIndex, int filterIndex)
    {
        FilterEntered?.Invoke(categoryIndex, filterIndex);
    }

    /// <summary>
    /// Forwards submenu filter hover exit to the feature controller.
    /// </summary>
    /// <param name="categoryIndex">The filter's category index.</param>
    /// <param name="filterIndex">The exited filter index.</param>
    private void HandleFilterExited(int categoryIndex, int filterIndex)
    {
        FilterExited?.Invoke(categoryIndex, filterIndex);
    }

    /// <summary>
    /// Forwards semantic filter selection to the feature controller.
    /// </summary>
    /// <param name="mode">The selected filter mode.</param>
    private void HandleFilterSelected(GalacticInformationFilterMode mode)
    {
        FilterSelected?.Invoke(mode);
    }

    /// <summary>
    /// Emits an outside-click dismissal request.
    /// </summary>
    /// <param name="area">The outside dismiss hit area.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleDismissClicked(UIRaycastArea area, PointerEventData eventData)
    {
        DismissRequested?.Invoke();
    }

    /// <summary>
    /// Applies a resolved texture and source-space placement to one authored image slot.
    /// </summary>
    /// <param name="image">The authored image slot.</param>
    /// <param name="data">The image presentation.</param>
    /// <param name="retainSlot">Whether the authored slot remains active without a texture.</param>
    private static void ApplyImage(
        RawImage image,
        GalacticInformationImageRenderData data,
        bool retainSlot
    )
    {
        image.texture = data.Texture;
        image.enabled = data.Texture != null;
        image.raycastTarget = false;
        UILayout.SetSourceRect(
            image.rectTransform,
            data.Bounds.x,
            data.Bounds.y,
            data.Bounds.width,
            data.Bounds.height
        );
        image.gameObject.SetActive(retainSlot || data.Texture != null);
    }

    /// <summary>
    /// Applies text, color, and source-space placement to one authored label slot.
    /// </summary>
    /// <param name="field">The authored label slot.</param>
    /// <param name="data">The text presentation.</param>
    private static void ApplyText(TextMeshProUGUI field, GalacticInformationTextRenderData data)
    {
        field.text = data.Text;
        field.color = data.Color;
        field.raycastTarget = false;
        UILayout.SetSourceRect(
            field.rectTransform,
            data.Bounds.x,
            data.Bounds.y,
            data.Bounds.width,
            data.Bounds.height
        );
        field.gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides one unused authored category slot and its submenu.
    /// </summary>
    /// <param name="index">The authored category-slot index.</param>
    private void HideCategory(int index)
    {
        categoryHitAreas[index].gameObject.SetActive(false);
        categoryIconImages[index].gameObject.SetActive(false);
        categoryArrowImages[index].gameObject.SetActive(false);
        categoryTextFields[index].gameObject.SetActive(false);
        submenuViews[index].gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides every authored category submenu.
    /// </summary>
    private void HideAllSubmenus()
    {
        foreach (GalacticInformationSubmenuView submenuView in submenuViews)
            submenuView.gameObject.SetActive(false);
    }

    /// <summary>
    /// Verifies every authored reference required for selector presentation and input.
    /// </summary>
    private void VerifyReferences()
    {
        if (dismissHitArea == null)
            throw new MissingReferenceException($"{name}/DismissHitArea is missing.");
        if (selectorPanel == null || backgroundImage == null || frameView == null)
            throw new MissingReferenceException($"{name}/SelectorPanel is incomplete.");
        if (
            categoryIconImages == null
            || categoryArrowImages == null
            || categoryTextFields == null
            || categoryHitAreas == null
            || submenuViews == null
            || categoryIconImages.Length == 0
            || categoryIconImages.Length != categoryArrowImages.Length
            || categoryIconImages.Length != categoryTextFields.Length
            || categoryIconImages.Length != categoryHitAreas.Length
            || categoryIconImages.Length != submenuViews.Length
        )
        {
            throw new MissingReferenceException($"{name}/Categories are missing.");
        }

        for (int i = 0; i < categoryIconImages.Length; i++)
        {
            if (
                categoryIconImages[i] == null
                || categoryArrowImages[i] == null
                || categoryTextFields[i] == null
                || categoryHitAreas[i] == null
                || submenuViews[i] == null
            )
            {
                throw new MissingReferenceException($"{name}/Category{i} is incomplete.");
            }
        }

        if (displayOffTextField == null || displayOffHitArea == null)
            throw new MissingReferenceException($"{name}/DisplayOff row is missing.");
    }
}
