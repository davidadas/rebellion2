using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents an interactive hue and saturation wheel with a movable selection indicator.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(RawImage))]
public sealed class ContentEditorColorWheel
    : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
{
    private const int TextureSize = 192;

    [SerializeField]
    private RectTransform selector;

    private RawImage image;
    private Texture2D texture;
    private Action<float, float> previewed;
    private Action committed;
    private float hue;
    private float saturation;

    /// <summary>
    /// Gets the currently selected normalized hue.
    /// </summary>
    public float Hue => hue;

    /// <summary>
    /// Gets the currently selected normalized saturation.
    /// </summary>
    public float Saturation => saturation;

    /// <summary>
    /// Creates the wheel texture before the first interaction.
    /// </summary>
    private void Awake() => EnsureInitialized();

    /// <summary>
    /// Updates the selection indicator after a layout change.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled)
            UpdateSelector();
    }

    /// <summary>
    /// Releases the procedurally generated texture with the wheel instance.
    /// </summary>
    private void OnDestroy()
    {
        if (texture == null)
            return;
        if (Application.isPlaying)
            Destroy(texture);
        else
            DestroyImmediate(texture);
    }

    /// <summary>
    /// Assigns callbacks for live selection changes and completed gestures.
    /// </summary>
    /// <param name="onPreviewed">The callback receiving normalized hue and saturation.</param>
    /// <param name="onCommitted">The callback invoked when a pointer gesture finishes.</param>
    public void Configure(Action<float, float> onPreviewed, Action onCommitted)
    {
        previewed = onPreviewed;
        committed = onCommitted;
        EnsureInitialized();
    }

    /// <summary>
    /// Synchronizes the selected hue and saturation without notifying listeners.
    /// </summary>
    /// <param name="valueHue">The normalized hue.</param>
    /// <param name="valueSaturation">The normalized saturation.</param>
    public void SetValueWithoutNotify(float valueHue, float valueSaturation)
    {
        hue = Mathf.Repeat(valueHue, 1f);
        saturation = Mathf.Clamp01(valueSaturation);
        EnsureInitialized();
        UpdateSelector();
    }

    /// <summary>
    /// Begins a wheel selection gesture at the pointer position.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData) => UpdateFromPointer(eventData);

    /// <summary>
    /// Continues a wheel selection gesture at the pointer position.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnDrag(PointerEventData eventData) => UpdateFromPointer(eventData);

    /// <summary>
    /// Applies the final pointer position and commits the completed gesture.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        UpdateFromPointer(eventData);
        committed?.Invoke();
    }

    /// <summary>
    /// Creates and assigns the procedural wheel texture when needed.
    /// </summary>
    private void EnsureInitialized()
    {
        image ??= GetComponent<RawImage>();
        if (texture != null)
            return;

        texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "Content editor color wheel",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        Color32[] pixels = new Color32[TextureSize * TextureSize];
        float center = (TextureSize - 1) * 0.5f;
        for (int y = 0; y < TextureSize; y++)
        {
            float normalizedY = (y - center) / center;
            for (int x = 0; x < TextureSize; x++)
            {
                float normalizedX = (x - center) / center;
                float radius = Mathf.Sqrt(
                    (normalizedX * normalizedX) + (normalizedY * normalizedY)
                );
                if (radius > 1f)
                {
                    pixels[(y * TextureSize) + x] = Color.clear;
                    continue;
                }

                float pixelHue = Mathf.Repeat(
                    Mathf.Atan2(normalizedY, normalizedX) / (Mathf.PI * 2f),
                    1f
                );
                pixels[(y * TextureSize) + x] = Color.HSVToRGB(pixelHue, radius, 1f);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = true;
    }

    /// <summary>
    /// Converts a pointer position into hue and saturation and notifies the live preview.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void UpdateFromPointer(PointerEventData eventData)
    {
        RectTransform rectTransform = (RectTransform)transform;
        if (
            eventData == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint
            )
        )
            return;

        Vector2 centered = localPoint - rectTransform.rect.center;
        float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
        if (radius <= 0f)
            return;
        hue = Mathf.Repeat(Mathf.Atan2(centered.y, centered.x) / (Mathf.PI * 2f), 1f);
        saturation = Mathf.Clamp01(centered.magnitude / radius);
        UpdateSelector();
        previewed?.Invoke(hue, saturation);
    }

    /// <summary>
    /// Positions the selection indicator over the selected point on the wheel.
    /// </summary>
    private void UpdateSelector()
    {
        if (selector == null)
            return;
        RectTransform rectTransform = (RectTransform)transform;
        float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
        float angle = hue * Mathf.PI * 2f;
        selector.anchoredPosition =
            new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * saturation * radius;
    }
}
