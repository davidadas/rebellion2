using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edits one faction color through a live sample, hexadecimal input, and color wheel.
/// </summary>
public sealed class ContentEditorColorPickerView : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI title;

    [SerializeField]
    private TextMeshProUGUI description;

    [SerializeField]
    private RawImage sample;

    [SerializeField]
    private Image swatch;

    [SerializeField]
    private TMP_InputField hexInput;

    [SerializeField]
    private ContentEditorColorWheel colorWheel;

    [SerializeField]
    private Slider brightnessSlider;

    [SerializeField]
    private TextMeshProUGUI hueValue;

    [SerializeField]
    private TextMeshProUGUI saturationValue;

    [SerializeField]
    private TextMeshProUGUI brightnessValue;

    [SerializeField]
    private TextMeshProUGUI error;

    [SerializeField]
    private UICheckboxView primaryColorLink;

    private ContentPackEditorField field;
    private Action<ContentPackEditorField, string> submitted;
    private Action changed;
    private Action<string> validationFailed;
    private Color fallbackColor;
    private Color primaryColor;
    private bool linked;
    private bool bound;
    private bool rendering;

    /// <summary>
    /// Gets the authored color represented by this card.
    /// </summary>
    public ContentPackEditorField Field => this.field;
    internal ContentPackEditorField LinkField { get; private set; }

    /// <summary>
    /// Connects the authored controls before the first render.
    /// </summary>
    private void Awake() => BindControls();

    /// <summary>
    /// Displays one color and its representative tinted interface mask.
    /// </summary>
    /// <param name="value">The color field.</param>
    /// <param name="sampleTexture">The interface mask used to preview the selected color.</param>
    /// <param name="defaultColor">The color shown when the field has no valid authored value.</param>
    /// <param name="link">The optional link field and its effective state and color.</param>
    /// <param name="onSubmitted">The validated color submission.</param>
    /// <param name="onChanged">The notification after a successful change.</param>
    /// <param name="onValidationFailed">The validation failure notification.</param>
    public void Render(
        ContentPackEditorField value,
        Texture2D sampleTexture,
        Color defaultColor,
        (ContentPackEditorField Field, bool Linked, Color Primary) link,
        Action<ContentPackEditorField, string> onSubmitted,
        Action onChanged,
        Action<string> onValidationFailed
    )
    {
        field = value ?? throw new ArgumentNullException(nameof(value));
        submitted = onSubmitted ?? throw new ArgumentNullException(nameof(onSubmitted));
        changed = onChanged;
        validationFailed = onValidationFailed;
        fallbackColor = defaultColor;
        LinkField = link.Field;
        primaryColor = link.Primary;
        linked = link.Linked;
        BindControls();
        title.text = field.Caption;
        description.text = field.HelpText;
        sample.texture = sampleTexture ?? Texture2D.whiteTexture;
        AspectRatioFitter fitter = sample.GetComponent<AspectRatioFitter>();
        if (fitter != null)
        {
            fitter.aspectRatio =
                sample.texture.height <= 0
                    ? 1f
                    : (float)sample.texture.width / sample.texture.height;
        }
        RenderLink();
        RenderAcceptedValue();
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Places the complete color card within the scrolling form.
    /// </summary>
    /// <param name="bounds">The card bounds in form coordinates.</param>
    public void SetLayout(Rect bounds) =>
        UILayout.SetSourceRect(
            (RectTransform)transform,
            Mathf.RoundToInt(bounds.x),
            Mathf.RoundToInt(bounds.y),
            Mathf.RoundToInt(bounds.width),
            Mathf.RoundToInt(bounds.height)
        );

    /// <summary>
    /// Releases project references before the pooled card is hidden.
    /// </summary>
    public void Clear()
    {
        field = null;
        LinkField = null;
        submitted = null;
        changed = null;
        validationFailed = null;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Binds wheel, slider, input, and commit listeners once.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;
        if (
            title == null
            || description == null
            || sample == null
            || swatch == null
            || hexInput == null
            || colorWheel == null
            || brightnessSlider == null
            || hueValue == null
            || saturationValue == null
            || brightnessValue == null
            || error == null
            || primaryColorLink == null
        )
            throw new MissingReferenceException(name + " has incomplete color-picker references.");

        colorWheel.Configure(PreviewWheelColor, CommitControlColor);
        brightnessSlider.onValueChanged.AddListener(PreviewBrightnessColor);
        hexInput.onValueChanged.AddListener(PreviewHexColor);
        hexInput.onEndEdit.AddListener(CommitHexColor);
        primaryColorLink.ValueChanged += ChangePrimaryColorLink;
        ContentEditorSliderCommit relay =
            brightnessSlider.GetComponent<ContentEditorSliderCommit>();
        if (relay == null)
            throw new MissingReferenceException(name + " has no brightness commit relay.");
        relay.Configure(CommitControlColor);
        bound = true;
    }

    /// <summary>
    /// Restores the committed field value and synchronizes every picker control.
    /// </summary>
    private void RenderAcceptedValue()
    {
        rendering = true;
        try
        {
            Color color =
                linked ? primaryColor
                : TryParseColor(field?.Value, out Color accepted) ? accepted
                : fallbackColor;
            Color.RGBToHSV(color, out float hue, out float saturation, out float brightness);
            colorWheel.SetValueWithoutNotify(hue, saturation);
            brightnessSlider.SetValueWithoutNotify(brightness);
            hexInput.SetTextWithoutNotify(ColorUtility.ToHtmlStringRGB(color));
            error.text = string.Empty;
            ApplyPreview(color);
        }
        finally
        {
            rendering = false;
        }
    }

    /// <summary>
    /// Shows the link checkbox and makes linked color controls read-only.
    /// </summary>
    private void RenderLink()
    {
        primaryColorLink.gameObject.SetActive(LinkField != null);
        primaryColorLink.SetIsCheckedWithoutNotify(linked);
        colorWheel.enabled = !linked;
        brightnessSlider.interactable = !linked;
        hexInput.interactable = !linked;
    }

    /// <summary>
    /// Commits one explicit primary-color link choice and refreshes the card.
    /// </summary>
    /// <param name="value">Whether the card should follow the primary color.</param>
    private void ChangePrimaryColorLink(bool value)
    {
        if (LinkField == null)
            return;
        try
        {
            submitted(LinkField, value ? "true" : "false");
            linked = value;
            RenderLink();
            RenderAcceptedValue();
            changed?.Invoke();
        }
        catch (ArgumentException exception)
        {
            RenderLink();
            ShowError(exception.Message);
        }
    }

    /// <summary>
    /// Previews a valid hexadecimal draft without committing project data.
    /// </summary>
    /// <param name="value">The draft hexadecimal value.</param>
    private void PreviewHexColor(string value)
    {
        if (rendering || !TryParseColor(value, out Color color))
            return;
        rendering = true;
        try
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float brightness);
            colorWheel.SetValueWithoutNotify(hue, saturation);
            brightnessSlider.SetValueWithoutNotify(brightness);
            ApplyPreview(color);
        }
        finally
        {
            rendering = false;
        }
    }

    /// <summary>
    /// Previews the color represented by the wheel and current brightness.
    /// </summary>
    /// <param name="hue">The normalized wheel hue.</param>
    /// <param name="saturation">The normalized wheel saturation.</param>
    private void PreviewWheelColor(float hue, float saturation)
    {
        if (rendering)
            return;
        Color color = Color.HSVToRGB(hue, saturation, brightnessSlider.value);
        rendering = true;
        try
        {
            hexInput.SetTextWithoutNotify(ColorUtility.ToHtmlStringRGB(color));
            ApplyPreview(color);
        }
        finally
        {
            rendering = false;
        }
    }

    /// <summary>
    /// Previews the color represented by the current wheel selection and brightness.
    /// </summary>
    /// <param name="brightness">The normalized brightness.</param>
    private void PreviewBrightnessColor(float brightness)
    {
        if (rendering)
            return;
        Color color = Color.HSVToRGB(colorWheel.Hue, colorWheel.Saturation, brightness);
        rendering = true;
        try
        {
            hexInput.SetTextWithoutNotify(ColorUtility.ToHtmlStringRGB(color));
            ApplyPreview(color);
        }
        finally
        {
            rendering = false;
        }
    }

    /// <summary>
    /// Commits the current control draft after a wheel or brightness gesture finishes.
    /// </summary>
    private void CommitControlColor() => CommitColor(hexInput.text);

    /// <summary>
    /// Commits a hexadecimal draft after editing ends.
    /// </summary>
    /// <param name="value">The proposed hexadecimal value.</param>
    private void CommitHexColor(string value) => CommitColor(value);

    /// <summary>
    /// Validates and submits a normalized six-digit RGB value.
    /// </summary>
    /// <param name="value">The proposed color.</param>
    private void CommitColor(string value)
    {
        if (field == null || rendering || linked)
            return;
        if (!TryParseColor(value, out Color color))
        {
            ShowError("Enter a hexadecimal color such as FF0000.");
            return;
        }

        string normalized = ColorUtility.ToHtmlStringRGB(color);
        if (string.Equals(normalized, field.Value, StringComparison.OrdinalIgnoreCase))
        {
            RenderAcceptedValue();
            return;
        }

        try
        {
            submitted?.Invoke(field, normalized);
            RenderAcceptedValue();
            changed?.Invoke();
        }
        catch (ArgumentException exception)
        {
            ShowError(exception.Message);
        }
    }

    /// <summary>
    /// Updates the swatch, representative mask, and numeric slider labels.
    /// </summary>
    /// <param name="color">The color currently being previewed.</param>
    private void ApplyPreview(Color color)
    {
        swatch.color = color;
        sample.color = color;
        hueValue.text = Mathf.RoundToInt(colorWheel.Hue * 360f) + "\u00B0";
        saturationValue.text = Mathf.RoundToInt(colorWheel.Saturation * 100f) + "%";
        brightnessValue.text = Mathf.RoundToInt(brightnessSlider.value * 100f) + "%";
        error.text = string.Empty;
    }

    /// <summary>
    /// Displays a local validation message and restores the accepted color.
    /// </summary>
    /// <param name="message">The validation failure.</param>
    private void ShowError(string message)
    {
        error.text = message;
        validationFailed?.Invoke(message);
        RenderAcceptedValue();
        error.text = message;
    }

    /// <summary>
    /// Parses RGB hexadecimal text with or without a leading hash character.
    /// </summary>
    /// <param name="value">The authored color text.</param>
    /// <param name="color">The parsed color.</param>
    /// <returns>True when the value is a supported RGB color.</returns>
    private static bool TryParseColor(string value, out Color color)
    {
        color = default;
        string normalized = value?.Trim() ?? string.Empty;
        if (!normalized.StartsWith("#", StringComparison.Ordinal))
            normalized = "#" + normalized;
        return normalized.Length == 7 && ColorUtility.TryParseHtmlString(normalized, out color);
    }
}
