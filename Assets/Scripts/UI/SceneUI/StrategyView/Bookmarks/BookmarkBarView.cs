using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BookmarkBarView : MonoBehaviour
{
    private readonly List<BookmarkSlotView> slotViews = new List<BookmarkSlotView>();

    [SerializeField]
    private BookmarkSlotView slotTemplate;

    public event Action<int> BookmarkRequested;

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

    private void Awake()
    {
        VerifyReferences();
    }

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

    private void HandleSlotDoubleClicked(BookmarkSlotView slot)
    {
        if (slot != null)
            BookmarkRequested?.Invoke(slot.Index);
    }

    private void VerifyReferences()
    {
        if (slotTemplate == null)
            throw new MissingReferenceException($"{name}/SlotTemplate is missing.");

        slotTemplate.gameObject.SetActive(false);
    }

    private static void VerifyLayout(StrategyBookmarkLayout layout)
    {
        if (layout == null)
            throw new MissingReferenceException("StrategyBookmarkLayout is missing.");
    }
}

public sealed class BookmarkRenderData
{
    public bool Active;
    public string Label;
    public Texture2D IconTexture;
}
