using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders the authored strategy bookmark bar and emits bookmark activation requests.
/// </summary>
public sealed class BookmarkBarView : MonoBehaviour
{
    private readonly List<BookmarkSlotView> slotViews = new List<BookmarkSlotView>();

    [SerializeField]
    private BookmarkSlotView slotTemplate;

    /// <summary>
    /// Raised when a bookmark slot is activated.
    /// </summary>
    public event Action<int> BookmarkRequested;

    /// <summary>
    /// Applies bookmark presentations in authored slot order.
    /// </summary>
    /// <param name="bookmarks">The ordered immutable bookmark presentations.</param>
    /// <param name="layout">The current faction's bookmark layout.</param>
    public void Render(IReadOnlyList<BookmarkRenderData> bookmarks, StrategyBookmarkLayout layout)
    {
        VerifyReferences();
        VerifyLayout(layout);
        IReadOnlyList<BookmarkRenderData> safeBookmarks =
            bookmarks ?? Array.Empty<BookmarkRenderData>();

        for (int i = 0; i < safeBookmarks.Count; i++)
        {
            BookmarkRenderData bookmark = safeBookmarks[i];
            if (bookmark == null || !bookmark.Active)
            {
                if (i < slotViews.Count)
                    slotViews[i].gameObject.SetActive(false);
                continue;
            }

            GetSlotView(i).Render(i, bookmark, layout);
        }

        for (int i = safeBookmarks.Count; i < slotViews.Count; i++)
            slotViews[i].gameObject.SetActive(false);

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Verifies the authored bookmark template before first use.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
    }

    /// <summary>
    /// Detaches listeners from bookmark slots owned by this bar.
    /// </summary>
    private void OnDestroy()
    {
        for (int index = 0; index < slotViews.Count; index++)
        {
            BookmarkSlotView slot = slotViews[index];
            if (slot != null)
                slot.DoubleClicked -= HandleSlotDoubleClicked;
        }
    }

    /// <summary>
    /// Gets or creates one reusable bookmark slot from the authored template.
    /// </summary>
    /// <param name="index">The requested slot index.</param>
    /// <returns>The reusable bookmark slot.</returns>
    private BookmarkSlotView GetSlotView(int index)
    {
        while (slotViews.Count <= index)
        {
            BookmarkSlotView slot = Instantiate(slotTemplate, transform);
            slot.name = $"BookmarkSlot{slotViews.Count}";
            slot.DoubleClicked += HandleSlotDoubleClicked;
            slotViews.Add(slot);
        }

        return slotViews[index];
    }

    /// <summary>
    /// Forwards a double-clicked slot as a semantic bookmark request.
    /// </summary>
    /// <param name="slot">The activated bookmark slot.</param>
    private void HandleSlotDoubleClicked(BookmarkSlotView slot)
    {
        if (slot != null)
            BookmarkRequested?.Invoke(slot.Index);
    }

    /// <summary>
    /// Verifies the authored bookmark-bar references.
    /// </summary>
    private void VerifyReferences()
    {
        if (slotTemplate == null)
            throw new MissingReferenceException($"{name}/SlotTemplate is missing.");

        slotTemplate.gameObject.SetActive(false);
    }

    /// <summary>
    /// Verifies that the current faction supplies bookmark geometry.
    /// </summary>
    /// <param name="layout">The current faction's bookmark layout.</param>
    private static void VerifyLayout(StrategyBookmarkLayout layout)
    {
        if (layout == null)
            throw new MissingReferenceException("StrategyBookmarkLayout is missing.");
    }
}

/// <summary>
/// Contains one immutable bookmark-slot presentation snapshot.
/// </summary>
public sealed class BookmarkRenderData
{
    /// <summary>
    /// Creates one bookmark-slot presentation snapshot.
    /// </summary>
    /// <param name="active">Whether the slot contains a visible bookmark.</param>
    /// <param name="label">The displayed planet label.</param>
    /// <param name="iconTexture">The displayed feature icon.</param>
    public BookmarkRenderData(bool active, string label, Texture2D iconTexture)
    {
        Active = active;
        Label = label ?? string.Empty;
        IconTexture = iconTexture;
    }

    /// <summary>
    /// Gets a value indicating whether the bookmark is active.
    /// </summary>
    public bool Active { get; }

    /// <summary>
    /// Gets the label.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the icon texture.
    /// </summary>
    public Texture2D IconTexture { get; }
}
