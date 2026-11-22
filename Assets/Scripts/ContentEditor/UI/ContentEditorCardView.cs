using UnityEngine;
using UnityEngine.UI;
using ArgumentNullException = global::System.ArgumentNullException;

/// <summary>
/// Owns a clipped editor card and constrains every child to one consistent content inset.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(RectMask2D))]
public sealed class ContentEditorCardView : MonoBehaviour
{
    public const int ContentInset = 16;

    private RectTransform cardRect;

    /// <summary>
    /// Gets the card rectangle used to position its background in the scrolling form.
    /// </summary>
    public RectTransform RectTransform => cardRect ??= (RectTransform)transform;

    /// <summary>
    /// Positions the card before its final content height is known.
    /// </summary>
    /// <param name="x">The card's form-space left edge.</param>
    /// <param name="y">The card's form-space top edge.</param>
    /// <param name="width">The card's complete width.</param>
    public void Begin(float x, float y, float width)
    {
        UILayout.SetSourceRect(
            RectTransform,
            Mathf.RoundToInt(x),
            Mathf.RoundToInt(y),
            Mathf.Max(1, Mathf.RoundToInt(width)),
            1
        );
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Finalizes the card height after all constrained children have been placed.
    /// </summary>
    /// <param name="height">The complete card height including its lower inset.</param>
    public void Complete(float height)
    {
        RectInt rect = UILayout.GetSourceRect(RectTransform);
        UILayout.SetSourceRect(
            RectTransform,
            rect.x,
            rect.y,
            rect.width,
            Mathf.Max(1, Mathf.CeilToInt(height))
        );
    }

    /// <summary>
    /// Reparents and places a child while clamping every edge to the card's content bounds.
    /// </summary>
    /// <param name="child">The child controlled by this card.</param>
    /// <param name="requested">The requested card-local rectangle.</param>
    /// <returns>The rectangle actually assigned after enforcing the inset.</returns>
    public RectInt SetChildRect(RectTransform child, Rect requested)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));
        if (child.parent != transform)
            child.SetParent(transform, false);

        float left = Mathf.Clamp(
            requested.xMin,
            ContentInset,
            Mathf.Max(ContentInset, RectTransform.rect.width - ContentInset)
        );
        float right = Mathf.Clamp(
            requested.xMax,
            left + 1,
            Mathf.Max(left + 1, RectTransform.rect.width - ContentInset)
        );
        float top = Mathf.Max(ContentInset, requested.yMin);
        RectInt result = new(
            Mathf.RoundToInt(left),
            Mathf.RoundToInt(top),
            Mathf.Max(1, Mathf.RoundToInt(right - left)),
            Mathf.Max(1, Mathf.RoundToInt(requested.height))
        );
        UILayout.SetSourceRect(child, result.x, result.y, result.width, result.height);
        return result;
    }

    /// <summary>
    /// Returns the horizontal center of the padded content area for image placement.
    /// </summary>
    /// <param name="childWidth">The width of the child being centered.</param>
    /// <returns>The card-local left edge of the centered child.</returns>
    public float CenteredContentX(float childWidth)
    {
        float available = Mathf.Max(1, RectTransform.rect.width - ContentInset * 2);
        return ContentInset + Mathf.Max(0, (available - childWidth) * 0.5f);
    }
}
