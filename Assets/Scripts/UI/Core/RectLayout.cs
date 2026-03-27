using Rebellion.Util.Attributes;
using UnityEngine;

/// <summary>
///
/// </summary>
[PersistableObject]
public class RectLayout
{
    [PersistableAttributeAttribute(Name = "AnchorMinX")]
    public float AnchorMinX { get; set; } = 0.0f;

    [PersistableAttributeAttribute(Name = "AnchorMinY")]
    public float AnchorMinY { get; set; } = 0.0f;

    [PersistableAttributeAttribute(Name = "AnchorMaxX")]
    public float AnchorMaxX { get; set; } = 0.0f;

    [PersistableAttributeAttribute(Name = "AnchorMaxY")]
    public float AnchorMaxY { get; set; } = 0.0f;

    [PersistableAttributeAttribute(Name = "OffsetMinX")]
    public float OffsetMinX { get; set; } = 0.0f;

    [PersistableAttributeAttribute(Name = "OffsetMinY")]
    public float OffsetMinY { get; set; } = 0.0f;

    [PersistableAttributeAttribute(Name = "OffsetMaxX")]
    public float OffsetMaxX { get; set; } = 0.0f;

    [PersistableAttributeAttribute(Name = "OffsetMaxY")]
    public float OffsetMaxY { get; set; } = 0.0f;

    /// <summary>
    ///
    /// </summary>
    /// <param name="rect"></param>
    public void Apply(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(AnchorMinX, AnchorMinY);
        rect.anchorMax = new Vector2(AnchorMaxX, AnchorMaxY);

        rect.offsetMin = new Vector2(OffsetMinX, OffsetMinY);
        rect.offsetMax = new Vector2(OffsetMaxX, OffsetMaxY);
    }
}
