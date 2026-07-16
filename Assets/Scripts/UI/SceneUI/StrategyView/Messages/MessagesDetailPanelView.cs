using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the authored message-detail panel, text wrapping, repeated lines, and detail navigation.
/// </summary>
public sealed class MessagesDetailPanelView : MonoBehaviour
{
    private readonly List<TextMeshProUGUI> detailLineTextFields = new List<TextMeshProUGUI>();
    private readonly List<string> renderedDetailLines = new List<string>();

    [SerializeField]
    private RawImage stripImage;

    [SerializeField]
    private Texture2D stripTexture;

    [SerializeField]
    private RawImage cardImage;

    [SerializeField]
    private RawImage overlayImage;

    [SerializeField]
    private RawImage bodyImage;

    [SerializeField]
    private Texture2D bodyTexture;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private TextMeshProUGUI headerTextField;

    [SerializeField]
    private RawImage nextButtonImage;

    [SerializeField]
    private RawImagePressVisual nextButtonPressVisual;

    [SerializeField]
    private Button nextButton;

    [SerializeField]
    private Texture2D nextButtonUpTexture;

    [SerializeField]
    private Texture2D nextButtonDownTexture;

    [SerializeField]
    private Texture2D nextButtonDisabledTexture;

    [SerializeField]
    private RawImage previousButtonImage;

    [SerializeField]
    private RawImagePressVisual previousButtonPressVisual;

    [SerializeField]
    private Button previousButton;

    [SerializeField]
    private Texture2D previousButtonUpTexture;

    [SerializeField]
    private Texture2D previousButtonDownTexture;

    [SerializeField]
    private Texture2D previousButtonDisabledTexture;

    [SerializeField]
    private ScrollAreaView linesScrollArea;

    [SerializeField]
    private TextMeshProUGUI lineTextTemplate;

    [SerializeField]
    private int linesContentPadding;

    private RectInt cardTemplateRect;
    private bool initialized;
    private bool renderedAnyDetailLines;
    private string renderedMessageId = string.Empty;
    private RectInt overlayTemplateRect;

    /// <summary>
    /// Raised when the next message is requested.
    /// </summary>
    public event Action NextRequested;

    /// <summary>
    /// Raised when the previous message is requested.
    /// </summary>
    public event Action PreviousRequested;

    /// <summary>
    /// Applies a complete message-detail presentation snapshot.
    /// </summary>
    /// <param name="data">The projected detail presentation.</param>
    public void Render(MessagesDetailPanelRenderData data)
    {
        EnsureInitialized();
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        SetImageAtAuthoredRect(stripImage, stripTexture);
        RenderScaledImage(cardImage, data.CardTexture, cardTemplateRect);
        RenderScaledImage(overlayImage, data.OverlayTexture, overlayTemplateRect);
        SetImageAtAuthoredRect(bodyImage, bodyTexture);
        SetImageAtAuthoredRect(iconImage, data.IconTexture);
        UILayout.SetTextContent(headerTextField, data.Header);
        SetNavigationButton(
            nextButtonImage,
            nextButtonPressVisual,
            nextButton,
            data.NextDisabled,
            nextButtonUpTexture,
            nextButtonDownTexture,
            nextButtonDisabledTexture
        );
        SetNavigationButton(
            previousButtonImage,
            previousButtonPressVisual,
            previousButton,
            data.PreviousDisabled,
            previousButtonUpTexture,
            previousButtonDownTexture,
            previousButtonDisabledTexture
        );
        RenderLines(data.MessageId, UILayout.WrapText(lineTextTemplate, data.Text, GetWrapWidth()));
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides the complete detail panel without changing its local scroll history.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns the authored detail-line wrap width.
    /// </summary>
    /// <returns>The wrap width in source-space units.</returns>
    private int GetWrapWidth()
    {
        EnsureInitialized();
        return UILayout.GetSourceRect(lineTextTemplate.rectTransform).width;
    }

    /// <summary>
    /// Returns the content height required for a wrapped line count.
    /// </summary>
    /// <param name="lineCount">The number of wrapped lines.</param>
    /// <returns>The required content height in source-space units.</returns>
    private int GetContentHeight(int lineCount)
    {
        EnsureInitialized();
        RectInt template = UILayout.GetSourceRect(lineTextTemplate.rectTransform);
        return template.y + linesContentPadding + lineCount * GetLinePitch();
    }

    /// <summary>
    /// Returns the authored line pitch in source-space units.
    /// </summary>
    /// <returns>The detail-line pitch.</returns>
    private int GetLinePitch()
    {
        EnsureInitialized();
        return UILayout.GetSourceRect(lineTextTemplate.rectTransform).height;
    }

    /// <summary>
    /// Calculates the displayed detail-art rectangle within an authored width.
    /// </summary>
    /// <param name="texture">The displayed artwork.</param>
    /// <param name="template">The authored artwork rectangle.</param>
    /// <returns>The aspect-preserving source-space rectangle.</returns>
    internal static RectInt GetScaledImageRect(Texture texture, RectInt template)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0 || template.width <= 0)
            return template;

        int height = Mathf.RoundToInt(texture.height / (texture.width / (float)template.width));
        return new RectInt(template.x, template.y, template.width, height);
    }

    /// <summary>
    /// Verifies authored references, captures artwork slots, and binds navigation.
    /// </summary>
    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Detaches the authored navigation listeners owned by this view.
    /// </summary>
    private void OnDestroy()
    {
        if (nextButton != null)
            nextButton.onClick.RemoveListener(RequestNext);
        if (previousButton != null)
            previousButton.onClick.RemoveListener(RequestPrevious);
    }

    /// <summary>
    /// Initializes authored presentation state exactly once.
    /// </summary>
    private void EnsureInitialized()
    {
        if (initialized)
            return;

        VerifyReferences();
        cardTemplateRect = UILayout.GetSourceRect(cardImage.rectTransform);
        overlayTemplateRect = UILayout.GetSourceRect(overlayImage.rectTransform);
        nextButton.onClick.AddListener(RequestNext);
        previousButton.onClick.AddListener(RequestPrevious);
        lineTextTemplate.gameObject.SetActive(false);
        initialized = true;
    }

    /// <summary>
    /// Raises the semantic next-message request.
    /// </summary>
    private void RequestNext()
    {
        NextRequested?.Invoke();
    }

    /// <summary>
    /// Raises the semantic previous-message request.
    /// </summary>
    private void RequestPrevious()
    {
        PreviousRequested?.Invoke();
    }

    /// <summary>
    /// Reconciles repeated text lines and detail scroll state.
    /// </summary>
    /// <param name="messageId">The selected source message identifier.</param>
    /// <param name="lines">The wrapped detail lines.</param>
    private void RenderLines(string messageId, IReadOnlyList<string> lines)
    {
        IReadOnlyList<string> safeLines = lines ?? Array.Empty<string>();
        bool resetScroll = LinesChanged(messageId, safeLines);
        linesScrollArea.SetContentHeight(
            GetContentHeight(safeLines.Count),
            GetLinePitch(),
            resetScroll
        );

        RectInt template = UILayout.GetSourceRect(lineTextTemplate.rectTransform);
        int linePitch = GetLinePitch();
        for (int index = 0; index < safeLines.Count; index++)
        {
            TextMeshProUGUI text = GetLineTextField(index);
            UILayout.SetTemplateText(
                text,
                lineTextTemplate,
                safeLines[index],
                Color.white,
                new RectInt(
                    template.x,
                    template.y + index * linePitch,
                    template.width,
                    template.height
                )
            );
        }

        for (int index = safeLines.Count; index < detailLineTextFields.Count; index++)
            detailLineTextFields[index].gameObject.SetActive(false);

        renderedAnyDetailLines = true;
        renderedMessageId = messageId ?? string.Empty;
        renderedDetailLines.Clear();
        renderedDetailLines.AddRange(safeLines);
    }

    /// <summary>
    /// Returns whether selected detail or wrapped text changed since the previous render.
    /// </summary>
    /// <param name="messageId">The selected source message identifier.</param>
    /// <param name="lines">The current wrapped detail lines.</param>
    /// <returns><see langword="true"/> when scroll state should reset.</returns>
    private bool LinesChanged(string messageId, IReadOnlyList<string> lines)
    {
        if (
            !renderedAnyDetailLines
            || renderedMessageId != (messageId ?? string.Empty)
            || renderedDetailLines.Count != lines.Count
        )
            return true;

        for (int index = 0; index < lines.Count; index++)
        {
            if (renderedDetailLines[index] != lines[index])
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns or creates one repeated line text field from the authored template.
    /// </summary>
    /// <param name="index">The zero-based line index.</param>
    /// <returns>The repeated text field.</returns>
    private TextMeshProUGUI GetLineTextField(int index)
    {
        while (detailLineTextFields.Count <= index)
        {
            TextMeshProUGUI text = Instantiate(lineTextTemplate, linesScrollArea.ContentRoot);
            text.name = $"DetailLineTextField{detailLineTextFields.Count}";
            detailLineTextFields.Add(text);
        }

        return detailLineTextFields[index];
    }

    /// <summary>
    /// Applies an image texture while preserving its authored rectangle.
    /// </summary>
    /// <param name="image">The authored image.</param>
    /// <param name="texture">The displayed texture.</param>
    private static void SetImageAtAuthoredRect(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    /// <summary>
    /// Applies detail artwork with its source aspect ratio and authored width.
    /// </summary>
    /// <param name="image">The authored artwork image.</param>
    /// <param name="texture">The displayed artwork.</param>
    /// <param name="template">The authored artwork slot.</param>
    private static void RenderScaledImage(RawImage image, Texture texture, RectInt template)
    {
        if (texture == null)
        {
            image.gameObject.SetActive(false);
            return;
        }

        RectInt rect = GetScaledImageRect(texture, template);
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        UILayout.SetImage(image, texture, rect.x, rect.y, rect.width, rect.height);
    }

    /// <summary>
    /// Applies an enabled or disabled navigation-button presentation.
    /// </summary>
    /// <param name="image">The authored button image.</param>
    /// <param name="pressVisual">The authored pressed-state visual.</param>
    /// <param name="button">The authored button control.</param>
    /// <param name="disabled">Whether the control is disabled.</param>
    /// <param name="upTexture">The enabled normal texture.</param>
    /// <param name="downTexture">The enabled pressed texture.</param>
    /// <param name="disabledTexture">The disabled texture.</param>
    private static void SetNavigationButton(
        RawImage image,
        RawImagePressVisual pressVisual,
        Button button,
        bool disabled,
        Texture upTexture,
        Texture downTexture,
        Texture disabledTexture
    )
    {
        Texture texture = disabled ? disabledTexture : upTexture;
        pressVisual.SetInteractiveTextures(texture, disabled ? null : downTexture);
        button.interactable = !disabled && texture != null;
        image.raycastTarget = button.interactable;
    }

    /// <summary>
    /// Verifies every authored detail-panel reference before use.
    /// </summary>
    private void VerifyReferences()
    {
        if (stripImage == null || stripTexture == null)
            throw new MissingReferenceException($"{name}/StripImage is missing.");
        if (cardImage == null || overlayImage == null)
            throw new MissingReferenceException($"{name}/DetailArtwork is missing.");
        if (bodyImage == null || bodyTexture == null)
            throw new MissingReferenceException($"{name}/BodyImage is missing.");
        if (iconImage == null || headerTextField == null)
            throw new MissingReferenceException($"{name}/DetailHeader is missing.");
        if (nextButtonImage == null || nextButtonPressVisual == null || nextButton == null)
            throw new MissingReferenceException($"{name}/NextButton is missing.");
        if (
            previousButtonImage == null
            || previousButtonPressVisual == null
            || previousButton == null
        )
            throw new MissingReferenceException($"{name}/PreviousButton is missing.");
        if (linesScrollArea == null || lineTextTemplate == null)
            throw new MissingReferenceException($"{name}/DetailLines are missing.");
    }
}
