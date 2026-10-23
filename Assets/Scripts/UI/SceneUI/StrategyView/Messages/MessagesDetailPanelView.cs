using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the authored message-detail panel, text wrapping, repeated lines, and detail navigation.
/// </summary>
public sealed class MessagesDetailPanelView : MonoBehaviour, IContentInitializable
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

    [SerializeField]
    private RawImage missionContinueButtonImage;

    [SerializeField]
    private RawImagePressVisual missionContinueButtonPressVisual;

    [SerializeField]
    private Button missionContinueButton;

    [SerializeField]
    private Texture2D missionContinueButtonUpTexture;

    [SerializeField]
    private Texture2D missionContinueButtonDownTexture;

    [SerializeField]
    private RawImage missionAbortButtonImage;

    [SerializeField]
    private RawImagePressVisual missionAbortButtonPressVisual;

    [SerializeField]
    private Button missionAbortButton;

    [SerializeField]
    private Texture2D missionAbortButtonUpTexture;

    [SerializeField]
    private Texture2D missionAbortButtonDownTexture;

    private RectInt cardTemplateRect;
    private bool initialized;
    private RectInt linesScrollAreaTemplateRect;
    private RectInt linesViewportTemplateRect;
    private RectInt linesScrollbarTemplateRect;
    private RectInt lineTextTemplateRect;
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
    /// Raised when the continuing mission should remain active.
    /// </summary>
    public event Action MissionContinueRequested;

    /// <summary>
    /// Raised when the continuing mission should be aborted.
    /// </summary>
    public event Action MissionAbortRequested;

    /// <summary>
    /// Raised when a mission-choice control is pressed.
    /// </summary>
    public event Action MissionChoiceControlPressed;

    /// <summary>
    /// Restores every content-sourced state texture from installation content.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        bodyTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_detail_body"
        );
        stripTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_detail_strip"
        );
        nextButtonUpTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_next_button_up"
        );
        nextButtonDownTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_next_button_pressed"
        );
        nextButtonDisabledTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_next_button_disabled"
        );
        previousButtonUpTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_previous_button_up"
        );
        previousButtonDownTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_previous_button_pressed"
        );
        previousButtonDisabledTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_previous_button_disabled"
        );
        missionContinueButtonUpTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_mission_continue_button_up"
        );
        missionContinueButtonDownTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_mission_continue_button_pressed"
        );
        missionAbortButtonUpTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_mission_abort_button_up"
        );
        missionAbortButtonDownTexture = ContentBindings.RequireTexture(
            contentAssets,
            "Application/Strategy/UI/Windows/ui_strategyview_messages_window_mission_abort_button_pressed"
        );
        VerifyReferences();
    }

    /// <summary>
    /// Applies a complete message-detail presentation snapshot.
    /// </summary>
    /// <param name="data">The projected detail presentation.</param>
    public void Render(MessagesDetailPanelRenderData data)
    {
        EnsureInitialized();
        VerifyReferences();
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
        SetNavigationButton(
            missionContinueButtonImage,
            missionContinueButtonPressVisual,
            missionContinueButton,
            false,
            missionContinueButtonUpTexture,
            missionContinueButtonDownTexture,
            null
        );
        SetNavigationButton(
            missionAbortButtonImage,
            missionAbortButtonPressVisual,
            missionAbortButton,
            false,
            missionAbortButtonUpTexture,
            missionAbortButtonDownTexture,
            null
        );
        RenderMissionContinuationChoice(data.MissionContinuationChoiceVisible);
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
    /// Calculates the displayed detail-art rectangle within its authored width.
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
        if (missionContinueButton != null)
            missionContinueButton.onClick.RemoveListener(RequestMissionContinue);
        if (missionAbortButton != null)
            missionAbortButton.onClick.RemoveListener(RequestMissionAbort);
    }

    /// <summary>
    /// Initializes authored presentation state exactly once.
    /// </summary>
    private void EnsureInitialized()
    {
        if (initialized)
            return;

        VerifyReferences(false);
        cardTemplateRect = UILayout.GetSourceRect(cardImage.rectTransform);
        overlayTemplateRect = UILayout.GetSourceRect(overlayImage.rectTransform);
        linesScrollAreaTemplateRect = UILayout.GetSourceRect(
            linesScrollArea.transform as RectTransform
        );
        linesViewportTemplateRect = UILayout.GetSourceRect(linesScrollArea.ViewportRoot);
        linesScrollbarTemplateRect = UILayout.GetSourceRect(
            linesScrollArea.GetComponentInChildren<Scrollbar>(true).transform as RectTransform
        );
        lineTextTemplateRect = UILayout.GetSourceRect(lineTextTemplate.rectTransform);
        nextButton.onClick.AddListener(RequestNext);
        previousButton.onClick.AddListener(RequestPrevious);
        missionContinueButton.onClick.AddListener(RequestMissionContinue);
        missionAbortButton.onClick.AddListener(RequestMissionAbort);
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
    /// Raises the semantic continue-mission choice.
    /// </summary>
    private void RequestMissionContinue()
    {
        MissionChoiceControlPressed?.Invoke();
        MissionContinueRequested?.Invoke();
    }

    /// <summary>
    /// Raises the semantic abort-mission choice.
    /// </summary>
    private void RequestMissionAbort()
    {
        MissionChoiceControlPressed?.Invoke();
        MissionAbortRequested?.Invoke();
    }

    /// <summary>
    /// Applies the continuing-mission choice visibility and reserves its detail-body width.
    /// </summary>
    /// <param name="visible">Whether the choice controls are available.</param>
    private void RenderMissionContinuationChoice(bool visible)
    {
        missionContinueButton.gameObject.SetActive(visible);
        missionAbortButton.gameObject.SetActive(visible);

        int scrollAreaWidth = linesScrollAreaTemplateRect.width;
        int viewportWidth = linesViewportTemplateRect.width;
        int scrollbarX = linesScrollbarTemplateRect.x;
        int lineWidth = lineTextTemplateRect.width;
        if (visible)
        {
            int choiceLeft = Mathf.Min(
                UILayout.GetSourceRect(missionContinueButton.transform as RectTransform).x,
                UILayout.GetSourceRect(missionAbortButton.transform as RectTransform).x
            );
            scrollAreaWidth = Mathf.Max(1, choiceLeft - linesScrollAreaTemplateRect.x - 4);
            viewportWidth = Mathf.Max(1, scrollAreaWidth - linesScrollbarTemplateRect.width);
            scrollbarX = viewportWidth;
            lineWidth = viewportWidth;
        }

        UILayout.SetSourceRect(
            linesScrollArea.transform as RectTransform,
            linesScrollAreaTemplateRect.x,
            linesScrollAreaTemplateRect.y,
            scrollAreaWidth,
            linesScrollAreaTemplateRect.height
        );
        linesScrollArea.SetLayout(
            new Vector2(linesViewportTemplateRect.x, linesViewportTemplateRect.y),
            new Vector2(viewportWidth, linesViewportTemplateRect.height),
            new Vector2(scrollbarX, linesScrollbarTemplateRect.y),
            new Vector2(linesScrollbarTemplateRect.width, linesScrollbarTemplateRect.height)
        );
        UILayout.SetSourceRect(
            lineTextTemplate.rectTransform,
            lineTextTemplateRect.x,
            lineTextTemplateRect.y,
            lineWidth,
            lineTextTemplateRect.height
        );
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
    /// <param name="verifyContent">Whether verify content.</param>
    private void VerifyReferences(bool verifyContent = true)
    {
        if (stripImage == null || (verifyContent && stripTexture == null))
            throw new MissingReferenceException($"{name}/StripImage is missing.");
        if (cardImage == null || overlayImage == null)
            throw new MissingReferenceException($"{name}/DetailArtwork is missing.");
        if (bodyImage == null || (verifyContent && bodyTexture == null))
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
        if (
            missionContinueButtonImage == null
            || missionContinueButtonPressVisual == null
            || missionContinueButton == null
            || missionAbortButtonImage == null
            || missionAbortButtonPressVisual == null
            || missionAbortButton == null
        )
            throw new MissingReferenceException($"{name}/MissionContinuationChoice is missing.");
        if (
            verifyContent
            && (
                missionContinueButtonUpTexture == null
                || missionContinueButtonDownTexture == null
                || missionAbortButtonUpTexture == null
                || missionAbortButtonDownTexture == null
            )
        )
            throw new MissingReferenceException(
                $"{name}/MissionContinuationChoiceTextures are missing."
            );
    }
}
