using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws one tactical unit sprite with its selection highlight and status bars.
/// </summary>
public sealed class TacticalUnitView : MonoBehaviour
{
    private const float _barHeight = 2f;
    private const float _barGap = 1f;
    private const float _selectionPadding = 3f;
    private static readonly Color _shieldColor = new Color(0.35f, 0.65f, 1f, 1f);
    private static readonly Color _barBackgroundColor = new Color(0f, 0f, 0f, 0.6f);

    [SerializeField]
    private RawImage spriteImage;

    [SerializeField]
    private RawImage selectionImage;

    [SerializeField]
    private RawImage barBackgroundImage;

    [SerializeField]
    private RawImage hullBarImage;

    [SerializeField]
    private RawImage shieldBarImage;

    /// <summary>
    /// Verifies authored references.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
    }

    /// <summary>
    /// Applies one unit presentation.
    /// </summary>
    /// <param name="data">The unit presentation to apply.</param>
    internal void Render(TacticalUnitRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        RectTransform rect = transform as RectTransform;
        float width = Mathf.Max(data.Width, 1f);
        float height = Mathf.Max(data.Height, 1f);
        float extent = Mathf.Max(width, height);
        rect.sizeDelta = new Vector2(extent, extent);
        rect.anchoredPosition = new Vector2(data.X, -data.Y);

        spriteImage.texture = data.Texture;
        spriteImage.enabled = data.Texture != null;
        spriteImage.color = new Color(1f, 1f, 1f, data.Alpha);
        spriteImage.rectTransform.sizeDelta = new Vector2(width, height);
        spriteImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, data.RotationDegrees);

        selectionImage.rectTransform.sizeDelta = new Vector2(
            extent + _selectionPadding * 2f,
            extent + _selectionPadding * 2f
        );
        selectionImage.gameObject.SetActive(data.Selected);

        // The bars stack directly above the hull: hull nearest the ship, shields above it.
        float barCount = data.ShowShields ? 2f : 1f;
        float blockHeight = barCount * _barHeight + (barCount - 1f) * _barGap;
        float blockTop = -(blockHeight + _barGap);
        SetBar(barBackgroundImage, 0f, blockTop, extent, blockHeight, _barBackgroundColor);
        SetBar(
            hullBarImage,
            0f,
            -(_barHeight + _barGap),
            extent * Mathf.Clamp01(data.HullFraction),
            _barHeight,
            new Color(data.BarColor.r, data.BarColor.g, data.BarColor.b, data.Alpha)
        );
        shieldBarImage.gameObject.SetActive(data.ShowShields);
        if (data.ShowShields)
        {
            SetBar(
                shieldBarImage,
                0f,
                blockTop,
                extent * Mathf.Clamp01(data.ShieldFraction),
                _barHeight,
                new Color(_shieldColor.r, _shieldColor.g, _shieldColor.b, data.Alpha)
            );
        }

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Positions one status bar relative to the unit's top-left corner.
    /// </summary>
    /// <param name="bar">The bar image.</param>
    /// <param name="x">The horizontal offset.</param>
    /// <param name="y">The vertical offset.</param>
    /// <param name="width">The bar width.</param>
    /// <param name="height">The bar height.</param>
    /// <param name="color">The bar color.</param>
    private static void SetBar(
        RawImage bar,
        float x,
        float y,
        float width,
        float height,
        Color color
    )
    {
        RectTransform rect = bar.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(Mathf.Max(width, 0f), height);
        bar.color = color;
    }

    /// <summary>
    /// Verifies the authored unit images.
    /// </summary>
    private void VerifyReferences()
    {
        if (spriteImage == null)
            throw new MissingReferenceException($"{name}/SpriteImage is missing.");
        if (selectionImage == null)
            throw new MissingReferenceException($"{name}/SelectionImage is missing.");
        if (barBackgroundImage == null)
            throw new MissingReferenceException($"{name}/BarBackgroundImage is missing.");
        if (hullBarImage == null)
            throw new MissingReferenceException($"{name}/HullBarImage is missing.");
        if (shieldBarImage == null)
            throw new MissingReferenceException($"{name}/ShieldBarImage is missing.");
    }
}
