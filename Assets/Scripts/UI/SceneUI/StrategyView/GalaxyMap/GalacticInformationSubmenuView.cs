using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents one authored galactic-information submenu and emits semantic filter input.
/// </summary>
public sealed class GalacticInformationSubmenuView : MonoBehaviour
{
    [SerializeField]
    private Image backgroundImage;

    [SerializeField]
    private GalacticInformationFrameView frameView;

    [SerializeField]
    private RawImage[] iconImages = Array.Empty<RawImage>();

    [SerializeField]
    private TextMeshProUGUI[] textFields = Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private UIRaycastArea[] hitAreas = Array.Empty<UIRaycastArea>();

    private readonly List<GalacticInformationFilterMode> renderedFilters =
        new List<GalacticInformationFilterMode>();

    private int categoryIndex = -1;
    private bool eventsBound;

    /// <summary>
    /// Raised when the pointer enters one rendered filter row.
    /// </summary>
    public event Action<int, int> FilterEntered;

    /// <summary>
    /// Raised when the pointer exits one rendered filter row.
    /// </summary>
    public event Action<int, int> FilterExited;

    /// <summary>
    /// Raised when one rendered filter row is selected.
    /// </summary>
    public event Action<GalacticInformationFilterMode> FilterSelected;

    /// <summary>
    /// Validates authored references and subscribes row input when Unity creates the view.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindEvents();
    }

    /// <summary>
    /// Releases row input subscriptions when Unity destroys the view.
    /// </summary>
    private void OnDestroy()
    {
        UnbindEvents();
    }

    /// <summary>
    /// Applies immutable submenu presentation data to authored row slots.
    /// </summary>
    /// <param name="data">The submenu presentation snapshot.</param>
    /// <param name="ownerCategoryIndex">The submenu's owning category index.</param>
    public void Render(GalacticInformationSubmenuRenderData data, int ownerCategoryIndex)
    {
        VerifyReferences();
        categoryIndex = ownerCategoryIndex;
        renderedFilters.Clear();
        if (data?.Visible != true)
        {
            HideRows(0);
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        UILayout.SetSourceRect(
            transform as RectTransform,
            data.Bounds.x,
            data.Bounds.y,
            data.Bounds.width,
            data.Bounds.height
        );
        UILayout.SetSourceRect(
            backgroundImage.rectTransform,
            0,
            0,
            data.Bounds.width,
            data.Bounds.height
        );
        backgroundImage.color = data.BackgroundColor;
        backgroundImage.raycastTarget = true;
        frameView.Render(data.Frame);

        int count = Mathf.Min(data.Filters.Count, hitAreas.Length);
        for (int i = 0; i < count; i++)
        {
            GalacticInformationFilterRenderData filter = data.Filters[i];
            renderedFilters.Add(filter.Mode);
            RenderRow(i, filter);
        }

        HideRows(count);
    }

    /// <summary>
    /// Applies one immutable filter presentation to an authored row slot.
    /// </summary>
    /// <param name="index">The authored row-slot index.</param>
    /// <param name="data">The filter-row presentation.</param>
    private void RenderRow(int index, GalacticInformationFilterRenderData data)
    {
        if (!data.Visible)
        {
            HideRow(index);
            return;
        }

        hitAreas[index].Render(data.HitBounds);
        ApplyImage(iconImages[index], data.Icon);
        ApplyText(textFields[index], data.Label);
    }

    /// <summary>
    /// Subscribes every authored filter hit area exactly once.
    /// </summary>
    private void BindEvents()
    {
        if (eventsBound)
            return;

        foreach (UIRaycastArea hitArea in hitAreas)
        {
            hitArea.Clicked += HandleFilterClicked;
            hitArea.Entered += HandleFilterEntered;
            hitArea.Exited += HandleFilterExited;
        }

        eventsBound = true;
    }

    /// <summary>
    /// Releases every authored filter hit-area subscription.
    /// </summary>
    private void UnbindEvents()
    {
        if (!eventsBound)
            return;

        foreach (UIRaycastArea hitArea in hitAreas)
        {
            hitArea.Clicked -= HandleFilterClicked;
            hitArea.Entered -= HandleFilterEntered;
            hitArea.Exited -= HandleFilterExited;
        }

        eventsBound = false;
    }

    /// <summary>
    /// Emits the semantic index of a hovered filter row.
    /// </summary>
    /// <param name="area">The entered filter hit area.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleFilterEntered(UIRaycastArea area, PointerEventData eventData)
    {
        int filterIndex = Array.IndexOf(hitAreas, area);
        if (filterIndex >= 0 && filterIndex < renderedFilters.Count)
            FilterEntered?.Invoke(categoryIndex, filterIndex);
    }

    /// <summary>
    /// Emits the semantic index of an exited filter row.
    /// </summary>
    /// <param name="area">The exited filter hit area.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleFilterExited(UIRaycastArea area, PointerEventData eventData)
    {
        int filterIndex = Array.IndexOf(hitAreas, area);
        if (filterIndex >= 0 && filterIndex < renderedFilters.Count)
            FilterExited?.Invoke(categoryIndex, filterIndex);
    }

    /// <summary>
    /// Emits the semantic filter mode selected from one rendered row.
    /// </summary>
    /// <param name="area">The selected filter hit area.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleFilterClicked(UIRaycastArea area, PointerEventData eventData)
    {
        int filterIndex = Array.IndexOf(hitAreas, area);
        if (filterIndex >= 0 && filterIndex < renderedFilters.Count)
            FilterSelected?.Invoke(renderedFilters[filterIndex]);
    }

    /// <summary>
    /// Applies a resolved texture and source-space placement to one authored image slot.
    /// </summary>
    /// <param name="image">The authored image slot.</param>
    /// <param name="data">The image presentation.</param>
    private static void ApplyImage(RawImage image, GalacticInformationImageRenderData data)
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
        image.gameObject.SetActive(data.Texture != null);
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
    /// Hides every authored row slot at or after the used row count.
    /// </summary>
    /// <param name="usedCount">The number of rows used by the current snapshot.</param>
    private void HideRows(int usedCount)
    {
        for (int i = usedCount; i < hitAreas.Length; i++)
            HideRow(i);
    }

    /// <summary>
    /// Hides one authored filter row slot.
    /// </summary>
    /// <param name="index">The authored row-slot index.</param>
    private void HideRow(int index)
    {
        hitAreas[index].gameObject.SetActive(false);
        iconImages[index].gameObject.SetActive(false);
        textFields[index].gameObject.SetActive(false);
    }

    /// <summary>
    /// Verifies every authored reference required for submenu presentation and input.
    /// </summary>
    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (frameView == null)
            throw new MissingReferenceException($"{name}/FrameView is missing.");
        if (
            iconImages == null
            || textFields == null
            || hitAreas == null
            || iconImages.Length == 0
            || iconImages.Length != textFields.Length
            || iconImages.Length != hitAreas.Length
        )
        {
            throw new MissingReferenceException($"{name}/Rows are missing.");
        }

        for (int i = 0; i < iconImages.Length; i++)
        {
            if (iconImages[i] == null || textFields[i] == null || hitAreas[i] == null)
                throw new MissingReferenceException($"{name}/Row{i} is incomplete.");
        }
    }
}
