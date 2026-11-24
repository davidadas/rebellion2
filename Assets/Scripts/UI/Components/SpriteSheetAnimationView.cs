using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a looping sprite sheet on an authored image without creating per-frame resources.
/// </summary>
public sealed class SpriteSheetAnimationView : MonoBehaviour
{
    [SerializeField]
    private RawImage image;

    private SpriteSheetAnimationViewData animationData;
    private double elapsed;
    private int frameIndex = -1;
    private bool requestedVisible;

    /// <summary>
    /// Checks the authored image reference before playback starts.
    /// </summary>
    private void Awake()
    {
        if (image == null)
            throw new MissingReferenceException($"{name}/Image is missing.");
    }

    /// <summary>
    /// Restores the requested visibility when the owner becomes active.
    /// </summary>
    private void OnEnable()
    {
        image.enabled = requestedVisible && animationData != null;
    }

    /// <summary>
    /// Conceals the image when playback is disabled by its owner.
    /// </summary>
    private void OnDisable()
    {
        if (image != null)
            image.enabled = false;
    }

    /// <summary>
    /// Advances the animation independently of simulation speed and pauses.
    /// </summary>
    private void Update()
    {
        AdvanceAnimation(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Selects the animation and starts it from its first frame whenever it becomes visible.
    /// </summary>
    /// <param name="data">The resolved animation, or null when no artwork is configured.</param>
    /// <param name="visible">Whether playback should be visible and running.</param>
    public void Render(SpriteSheetAnimationViewData data, bool visible)
    {
        bool changed =
            animationData?.Texture != data?.Texture
            || animationData?.FrameCount != data?.FrameCount
            || animationData?.Columns != data?.Columns
            || animationData?.FramesPerSecond != data?.FramesPerSecond;
        bool restart = changed || visible && !requestedVisible;
        animationData = data;
        requestedVisible = visible;
        image.texture = data?.Texture;
        if (restart)
        {
            elapsed = 0d;
            frameIndex = -1;
            SetFrame(0);
        }
        bool active = visible && data != null;
        enabled = active;
        image.enabled = active;
    }

    /// <summary>
    /// Advances playback by a supplied presentation duration.
    /// </summary>
    /// <param name="elapsedSeconds">The unscaled elapsed duration.</param>
    internal void AdvanceAnimation(float elapsedSeconds)
    {
        if (!requestedVisible || animationData == null || elapsedSeconds <= 0f)
            return;
        double duration = animationData.FrameCount / (double)animationData.FramesPerSecond;
        elapsed = (elapsed + elapsedSeconds) % duration;
        SetFrame((int)(elapsed * animationData.FramesPerSecond) % animationData.FrameCount);
    }

    /// <summary>
    /// Updates the image crop only when the displayed frame changes.
    /// </summary>
    /// <param name="index">The zero-based animation frame.</param>
    private void SetFrame(int index)
    {
        if (animationData == null || frameIndex == index)
            return;
        frameIndex = index;
        image.uvRect = new Rect(
            (float)(index % animationData.Columns) / animationData.Columns,
            1f - (float)(index / animationData.Columns + 1) / animationData.Rows,
            1f / animationData.Columns,
            1f / animationData.Rows
        );
    }
}
