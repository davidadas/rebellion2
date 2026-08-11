using System;
using UnityEngine;

/// <summary>
/// Presents one looping, camera-facing tactical effect around a ship.
/// </summary>
public sealed class TacticalPersistentEffectView : MonoBehaviour
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
    /// <param name="diameter">The effect diameter in the parent unit's local space.</param>
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
        spriteRenderer.sortingOrder = 100;
        transform.localScale = Vector3.one * diameter;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Starts or stops the looping effect.
    /// </summary>
    /// <param name="visible">Whether the effect should be visible.</param>
    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf == visible)
            return;

        if (visible)
        {
            elapsed = 0f;
            frameIndex = 0;
            spriteRenderer.sprite = frames[0];
        }

        gameObject.SetActive(visible);
    }

    /// <summary>
    /// Advances the effect through its repeating eight-frame sequence.
    /// </summary>
    private void Update()
    {
        elapsed += Time.deltaTime;
        while (elapsed >= _frameDuration)
        {
            elapsed -= _frameDuration;
            frameIndex = (frameIndex + 1) % frames.Length;
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
}
