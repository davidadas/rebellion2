using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a save-menu slider and emits normalized value changes.
/// </summary>
public sealed class SaveMenuSliderView : MonoBehaviour
{
    [SerializeField]
    private Slider slider;

    [SerializeField]
    private RawImage thumbImage;

    private bool bound;

    /// <summary>
    /// Occurs when the player changes the normalized slider value.
    /// </summary>
    public event Action<float> ValueChanged;

    /// <summary>
    /// Renders a normalized slider value and positions the authored thumb.
    /// </summary>
    /// <param name="value">The normalized value.</param>
    public void Render(float value)
    {
        VerifyReferences();
        BindControls();
        float normalizedValue = Mathf.Clamp01(value);
        slider.SetValueWithoutNotify(normalizedValue);
        SetThumbPosition(normalizedValue);
    }

    /// <summary>
    /// Verifies every authored reference required by the slider.
    /// </summary>
    public void VerifyReferences()
    {
        if (slider == null)
            throw new MissingReferenceException("Slider is missing.");
        if (thumbImage == null)
            throw new MissingReferenceException("ThumbImage is missing.");
        if (thumbImage.texture == null)
            throw new MissingReferenceException("ThumbImage has no authored texture.");
    }

    /// <summary>
    /// Binds the authored slider while the view is active.
    /// </summary>
    private void OnEnable()
    {
        if (ReferencesAssigned())
            BindControls();
    }

    /// <summary>
    /// Removes the authored slider listener while the view is inactive.
    /// </summary>
    private void OnDisable()
    {
        if (!bound)
            return;

        slider.onValueChanged.RemoveListener(HandleValueChanged);
        bound = false;
    }

    /// <summary>
    /// Attaches the value callback exactly once.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;

        slider.onValueChanged.AddListener(HandleValueChanged);
        bound = true;
    }

    /// <summary>
    /// Updates the local thumb before forwarding the semantic value change.
    /// </summary>
    /// <param name="value">The slider's current normalized value.</param>
    private void HandleValueChanged(float value)
    {
        float normalizedValue = Mathf.Clamp01(value);
        SetThumbPosition(normalizedValue);
        ValueChanged?.Invoke(normalizedValue);
    }

    /// <summary>
    /// Positions the thumb within the slider's authored source-space bounds.
    /// </summary>
    /// <param name="value">The normalized thumb position.</param>
    private void SetThumbPosition(float value)
    {
        RectTransform sliderRect = slider.transform as RectTransform;
        RectTransform thumbRect = thumbImage.rectTransform;
        int sliderWidth = GetRectWidth(sliderRect);
        int thumbWidth = GetRectWidth(thumbRect);
        int thumbHeight = GetRectHeight(thumbRect);
        int thumbX = Mathf.RoundToInt(
            Mathf.Clamp01(value) * Mathf.Max(0, sliderWidth - thumbWidth)
        );
        UILayout.SetSourceRect(thumbRect, thumbX, 0, thumbWidth, thumbHeight);
    }

    /// <summary>
    /// Resolves a stable authored width from a rect transform.
    /// </summary>
    /// <param name="rect">The rect transform to measure.</param>
    /// <returns>The non-negative rounded width.</returns>
    private static int GetRectWidth(RectTransform rect)
    {
        int width = Mathf.RoundToInt(rect.rect.width);
        if (width <= 0)
            width = Mathf.RoundToInt(rect.sizeDelta.x);

        return Mathf.Max(0, width);
    }

    /// <summary>
    /// Resolves a stable authored height from a rect transform.
    /// </summary>
    /// <param name="rect">The rect transform to measure.</param>
    /// <returns>The non-negative rounded height.</returns>
    private static int GetRectHeight(RectTransform rect)
    {
        int height = Mathf.RoundToInt(rect.rect.height);
        if (height <= 0)
            height = Mathf.RoundToInt(rect.sizeDelta.y);

        return Mathf.Max(0, height);
    }

    /// <summary>
    /// Checks whether the prefab has all references needed for early binding.
    /// </summary>
    /// <returns>True when every required authored reference is assigned.</returns>
    private bool ReferencesAssigned()
    {
        return slider && thumbImage && thumbImage.texture;
    }
}
