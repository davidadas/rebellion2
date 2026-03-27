using UnityEngine;

/// <summary>
/// Handles audio feedback for UI buttons by mapping UIButton events to audio clips.
/// Each interaction event can have its own sound.
/// </summary>
[RequireComponent(typeof(UIButton))]
public sealed class UIButtonAudio : MonoBehaviour
{
    [Header("Sounds")]
    [SerializeField]
    private AudioClip hoverSound;

    [SerializeField]
    private AudioClip pressSound;

    [SerializeField]
    private AudioClip releaseSound;

    [SerializeField]
    private AudioClip clickSound;

    [SerializeField]
    private AudioClip doubleClickSound;

    [Header("Volume")]
    [SerializeField]
    [Range(0f, 1f)]
    private float hoverVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float pressVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float releaseVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float clickVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float doubleClickVolume = 1f;

    private UIButton uiButton;

    private void Awake()
    {
        uiButton = GetComponent<UIButton>();

        if (uiButton == null)
        {
            Debug.LogError(
                "UIButtonAudio requires a UIButton component on the same GameObject.",
                this
            );
            return;
        }

        // Map each event directly to its sound
        if (hoverSound != null)
            uiButton.OnHoverEnter.AddListener(PlayHoverSound);

        if (pressSound != null)
            uiButton.OnPressDown.AddListener(PlayPressSound);

        if (releaseSound != null)
            uiButton.OnPressUp.AddListener(PlayReleaseSound);

        if (clickSound != null)
            uiButton.OnClick.AddListener(PlayClickSound);

        if (doubleClickSound != null)
            uiButton.OnDoubleClick.AddListener(PlayDoubleClickSound);
    }

    private void OnDestroy()
    {
        if (uiButton != null)
        {
            uiButton.OnHoverEnter.RemoveListener(PlayHoverSound);
            uiButton.OnPressDown.RemoveListener(PlayPressSound);
            uiButton.OnPressUp.RemoveListener(PlayReleaseSound);
            uiButton.OnClick.RemoveListener(PlayClickSound);
            uiButton.OnDoubleClick.RemoveListener(PlayDoubleClickSound);
        }
    }

    private void PlayHoverSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(hoverSound, hoverVolume);
    }

    private void PlayPressSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(pressSound, pressVolume);
    }

    private void PlayReleaseSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(releaseSound, releaseVolume);
    }

    private void PlayClickSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clickSound, clickVolume);
    }

    private void PlayDoubleClickSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(doubleClickSound, doubleClickVolume);
    }
}
