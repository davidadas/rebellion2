using TMPro;
using UnityEngine;

/// <summary>
/// Displays the tactical view's frame rate independently of game speed or pause state.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class TacticalFrameRateView : MonoBehaviour
{
    private TMP_Text _label;
    private float _elapsed;
    private int _frames;

    /// <summary>
    /// Resets the sampling window when the counter becomes visible.
    /// </summary>
    private void OnEnable()
    {
        _label = GetComponent<TMP_Text>();
        _elapsed = 0f;
        _frames = 0;
        _label.text = "-- FPS";
    }

    /// <summary>
    /// Refreshes the display twice per second using unscaled frame time.
    /// </summary>
    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        _frames++;
        if (_elapsed < 0.5f)
            return;
        _label.SetText("{0:0} FPS", _frames / _elapsed);
        _elapsed = 0f;
        _frames = 0;
    }
}
