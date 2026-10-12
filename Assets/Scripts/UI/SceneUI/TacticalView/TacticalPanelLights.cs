using UnityEngine;
using UnityEngine.UI;

/// <summary>Briefly dims staggered panel lamps without using the gameplay random stream.</summary>
public sealed class TacticalPanelLights : MonoBehaviour
{
    [SerializeField]
    private Image[] lamps;

    [SerializeField]
    private float phaseOffset;
    private Color[] _steadyColors;
    private bool[] _dimmed;

    /// <summary>Captures the authored steady state of every lamp.</summary>
    private void Awake()
    {
        _steadyColors = new Color[lamps.Length];
        _dimmed = new bool[lamps.Length];
        for (int i = 0; i < lamps.Length; i++)
            _steadyColors[i] = lamps[i].color;
    }

    /// <summary>Updates a handful of decorative lamps using presentation time only.</summary>
    private void Update() => Render(Time.unscaledTime);

    /// <summary>Applies occasional short dips, independently phased for each lamp.</summary>
    /// <param name="presentationTime">Unscaled presentation time in seconds.</param>
    public void Render(float presentationTime)
    {
        if (_steadyColors == null)
            return;
        for (int i = 0; i < lamps.Length; i++)
        {
            float period = 7.3f + i * 2.17f;
            bool dimmed = Mathf.Repeat(presentationTime + phaseOffset + i * 3.11f, period) < 0.16f;
            if (dimmed == _dimmed[i])
                continue;
            _dimmed[i] = dimmed;
            Color c = _steadyColors[i];
            lamps[i].color = dimmed ? new Color(c.r * 0.38f, c.g * 0.38f, c.b * 0.38f, c.a) : c;
        }
    }

    /// <summary>Restores steady lamps when the panel is disabled.</summary>
    private void OnDisable()
    {
        if (_steadyColors == null)
            return;
        for (int i = 0; i < lamps.Length; i++)
        {
            if (lamps[i])
                lamps[i].color = _steadyColors[i];
            _dimmed[i] = false;
        }
    }
}
