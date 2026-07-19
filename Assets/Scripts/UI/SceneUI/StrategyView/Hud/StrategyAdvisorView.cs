using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents advisor idle frames and queued animations and emits semantic advisor input requests.
/// </summary>
public sealed class StrategyAdvisorView : MonoBehaviour
{
    [SerializeField]
    private RawImage protocolImage;

    [SerializeField]
    private RawImage droidImage;

    [SerializeField]
    private UIRaycastArea protocolInput;

    [SerializeField]
    private UIRaycastArea droidInput;

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    public event Action<StrategyAdvisorView> Destroyed;

    /// <summary>
    /// Occurs when the droid is clicked.
    /// </summary>
    public event Action DroidClicked;

    /// <summary>
    /// Occurs when a droid context request is raised.
    /// </summary>
    public event Action<int, int> DroidContextRequested;

    /// <summary>
    /// Occurs when playback starts.
    /// </summary>
    public event Action<StrategyAdvisorAnimationViewData> PlaybackStarted;

    /// <summary>
    /// Occurs when a protocol context request is raised.
    /// </summary>
    public event Action<int, int> ProtocolContextRequested;

    private readonly Queue<StrategyAdvisorAnimationViewData> playbackQueue =
        new Queue<StrategyAdvisorAnimationViewData>();

    private StrategyAdvisorViewData presentation;
    private StrategyAdvisorAnimationViewData activeAnimation;
    private int activeFrameIndex;
    private float frameElapsedSeconds;
    private bool eventsBound;

    /// <summary>
    /// Validates authored references and subscribes advisor inputs when Unity creates the view.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindEvents();
    }

    /// <summary>
    /// Advances local advisor animation timing using unscaled frame time.
    /// </summary>
    private void Update()
    {
        AdvanceAnimation(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Releases authored input subscriptions and informs the feature controller of destruction.
    /// </summary>
    private void OnDestroy()
    {
        UnbindEvents();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies advisor idle presentation and clears playback from the previous theme.
    /// </summary>
    /// <param name="data">The advisor presentation snapshot.</param>
    public void Render(StrategyAdvisorViewData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        ClearPlayback();
        presentation = data;
        if (!data.Visible)
        {
            SetImage(protocolImage, null);
            SetImage(droidImage, null);
            protocolInput.gameObject.SetActive(false);
            droidInput.gameObject.SetActive(false);
            return;
        }

        SetSourceRect(protocolImage.rectTransform, data.ProtocolBounds);
        SetSourceRect(droidImage.rectTransform, data.DroidBounds);
        protocolInput.Render(data.ProtocolBounds);
        droidInput.Render(data.DroidBounds);
        SetIdleFrame(false);
        SetIdleFrame(true);
    }

    /// <summary>
    /// Queues a complete ordered advisor playback batch and starts it when the view is idle.
    /// </summary>
    /// <param name="animations">The animations in playback order.</param>
    public void EnqueuePlaybacks(IReadOnlyList<StrategyAdvisorAnimationViewData> animations)
    {
        if (animations == null)
            return;

        for (int i = 0; i < animations.Count; i++)
        {
            StrategyAdvisorAnimationViewData animation = animations[i];
            if (animation?.Frames.Count > 0)
                playbackQueue.Enqueue(animation);
        }

        StartNextAnimation();
    }

    /// <summary>
    /// Advances playback by an explicit unscaled duration for runtime and deterministic tests.
    /// </summary>
    /// <param name="elapsedSeconds">The unscaled elapsed duration.</param>
    internal void AdvanceAnimation(float elapsedSeconds)
    {
        if (
            activeAnimation == null
            || presentation == null
            || presentation.FrameIntervalSeconds <= 0f
            || elapsedSeconds <= 0f
        )
            return;

        frameElapsedSeconds += elapsedSeconds;
        while (activeAnimation != null && frameElapsedSeconds >= presentation.FrameIntervalSeconds)
        {
            frameElapsedSeconds -= presentation.FrameIntervalSeconds;
            activeFrameIndex++;
            if (activeFrameIndex >= activeAnimation.Frames.Count)
            {
                FinishAnimation();
                continue;
            }

            SetActiveFrame();
        }
    }

    /// <summary>
    /// Subscribes authored advisor hit areas exactly once.
    /// </summary>
    private void BindEvents()
    {
        if (eventsBound)
            return;

        protocolInput.ContextRequested += HandleProtocolContextRequested;
        droidInput.Clicked += HandleDroidClicked;
        droidInput.ContextRequested += HandleDroidContextRequested;
        eventsBound = true;
    }

    /// <summary>
    /// Releases subscriptions to the authored advisor hit areas.
    /// </summary>
    private void UnbindEvents()
    {
        if (!eventsBound)
            return;

        protocolInput.ContextRequested -= HandleProtocolContextRequested;
        droidInput.Clicked -= HandleDroidClicked;
        droidInput.ContextRequested -= HandleDroidContextRequested;
        eventsBound = false;
    }

    /// <summary>
    /// Begins the next queued animation without disturbing an active animation.
    /// </summary>
    private void StartNextAnimation()
    {
        if (activeAnimation != null || playbackQueue.Count == 0 || presentation == null)
            return;

        activeAnimation = playbackQueue.Dequeue();
        activeFrameIndex = 0;
        frameElapsedSeconds = 0f;
        if (activeAnimation.UsesDroid)
            SetIdleFrame(false);
        SetActiveFrame();
        PlaybackStarted?.Invoke(activeAnimation);
    }

    /// <summary>
    /// Applies the current active animation frame to its advisor image.
    /// </summary>
    private void SetActiveFrame()
    {
        RawImage image = activeAnimation.UsesDroid ? droidImage : protocolImage;
        SetImage(image, activeAnimation.Frames[activeFrameIndex]);
    }

    /// <summary>
    /// Restores the completed advisor to idle presentation and continues queued playback.
    /// </summary>
    private void FinishAnimation()
    {
        SetIdleFrame(activeAnimation.UsesDroid);
        activeAnimation = null;
        activeFrameIndex = 0;
        frameElapsedSeconds = 0f;
        StartNextAnimation();
    }

    /// <summary>
    /// Restores one advisor image to its configured idle frame.
    /// </summary>
    /// <param name="droid">Whether to restore the droid rather than the protocol advisor.</param>
    private void SetIdleFrame(bool droid)
    {
        if (presentation == null)
            return;

        SetImage(
            droid ? droidImage : protocolImage,
            droid ? presentation.DroidIdleTexture : presentation.ProtocolIdleTexture
        );
    }

    /// <summary>
    /// Clears all local animation state without changing current presentation data.
    /// </summary>
    private void ClearPlayback()
    {
        playbackQueue.Clear();
        activeAnimation = null;
        activeFrameIndex = 0;
        frameElapsedSeconds = 0f;
    }

    /// <summary>
    /// Emits the semantic request to open the messages window.
    /// </summary>
    /// <param name="area">The droid hit area.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDroidClicked(UIRaycastArea area, PointerEventData eventData)
    {
        DroidClicked?.Invoke();
    }

    /// <summary>
    /// Emits the protocol advisor menu request with source-space pointer coordinates.
    /// </summary>
    /// <param name="area">The protocol advisor hit area.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleProtocolContextRequested(UIRaycastArea area, PointerEventData eventData)
    {
        if (
            UILayout.TryGetSourcePosition(
                transform as RectTransform,
                eventData,
                out Vector2Int sourcePosition
            )
        )
            ProtocolContextRequested?.Invoke(sourcePosition.x, sourcePosition.y);
    }

    /// <summary>
    /// Emits the droid notification-menu request with source-space pointer coordinates.
    /// </summary>
    /// <param name="area">The droid hit area.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDroidContextRequested(UIRaycastArea area, PointerEventData eventData)
    {
        if (
            UILayout.TryGetSourcePosition(
                transform as RectTransform,
                eventData,
                out Vector2Int sourcePosition
            )
        )
            DroidContextRequested?.Invoke(sourcePosition.x, sourcePosition.y);
    }

    /// <summary>
    /// Applies optional source-space bounds to an advisor image.
    /// </summary>
    /// <param name="rect">The advisor image transform.</param>
    /// <param name="bounds">The optional source-space bounds.</param>
    private static void SetSourceRect(RectTransform rect, RectInt? bounds)
    {
        if (rect == null || bounds == null)
            return;

        RectInt sourceRect = bounds.Value;
        UILayout.SetSourceRect(
            rect,
            sourceRect.x,
            sourceRect.y,
            sourceRect.width,
            sourceRect.height
        );
    }

    /// <summary>
    /// Applies advisor image content while preserving authored hierarchy and raycast ownership.
    /// </summary>
    /// <param name="image">The advisor image.</param>
    /// <param name="texture">The frame texture.</param>
    private static void SetImage(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.raycastTarget = false;
    }

    /// <summary>
    /// Verifies every authored advisor image and hit-area reference.
    /// </summary>
    private void VerifyReferences()
    {
        if (protocolImage == null)
            throw new MissingReferenceException($"{name}/ProtocolImage is missing.");
        if (droidImage == null)
            throw new MissingReferenceException($"{name}/DroidImage is missing.");
        if (protocolInput == null)
            throw new MissingReferenceException($"{name}/ProtocolInput is missing.");
        if (droidInput == null)
            throw new MissingReferenceException($"{name}/DroidInput is missing.");
    }
}
