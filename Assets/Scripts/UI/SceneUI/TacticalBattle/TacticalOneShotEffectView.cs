using System;
using UnityEngine;

/// <summary>
/// Presents one camera-facing tactical animation and removes it after its final frame.
/// </summary>
public sealed class TacticalOneShotEffectView : MonoBehaviour
{
    private const float _frameDuration = 0.1f;
    private Sprite[] frames = Array.Empty<Sprite>();
    private SpriteRenderer spriteRenderer;
    private float elapsed;
    private int frameIndex;

    /// <summary>
    /// Configures the ordered animation frames and world-space diameter.
    /// </summary>
    /// <param name="animationFrames">The animation frames in playback order.</param>
    /// <param name="diameter">The effect diameter in tactical world units.</param>
    public void Initialize(Sprite[] animationFrames, float diameter)
    {
        if (animationFrames == null)
            throw new ArgumentNullException(nameof(animationFrames));
        if (animationFrames.Length == 0)
            throw new ArgumentException(
                "A tactical effect requires animation frames.",
                nameof(animationFrames)
            );
        if (diameter <= 0f)
            throw new ArgumentOutOfRangeException(nameof(diameter));

        frames = animationFrames;
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = frames[0];
        spriteRenderer.sortingOrder = 101;
        SetWorldDiameter(diameter);
    }

    /// <summary>
    /// Advances the effect once through its ordered frame sequence.
    /// </summary>
    private void Update()
    {
        elapsed += Time.deltaTime;
        while (elapsed >= _frameDuration)
        {
            elapsed -= _frameDuration;
            frameIndex++;
            if (frameIndex >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }

            spriteRenderer.sprite = frames[frameIndex];
        }
    }

    /// <summary>
    /// Keeps the planar effect facing the tactical camera.
    /// </summary>
    private void LateUpdate()
    {
        Camera battleCamera = Camera.main;
        if (battleCamera != null)
            transform.rotation = battleCamera.transform.rotation;
    }

    /// <summary>
    /// Compensates for the presentation parent's scale while retaining a world-space effect size.
    /// </summary>
    /// <param name="diameter">The requested world-space diameter.</param>
    private void SetWorldDiameter(float diameter)
    {
        Vector3 parentScale = transform.parent == null ? Vector3.one : transform.parent.lossyScale;
        transform.localScale = new Vector3(
            diameter / Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Epsilon),
            diameter / Mathf.Max(Mathf.Abs(parentScale.y), Mathf.Epsilon),
            diameter / Mathf.Max(Mathf.Abs(parentScale.z), Mathf.Epsilon)
        );
    }
}
