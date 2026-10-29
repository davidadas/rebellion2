using System;
using Rebellion.Tactical;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Presents the supported tactical-battle speeds and forwards player selections.
/// </summary>
public sealed class TacticalTimeControlsView : MonoBehaviour
{
    private static readonly Color _normalColor = new Color(0.015f, 0.085f, 0.12f);
    private static readonly Color _selectedColor = new Color(0.08f, 0.48f, 0.58f, 0.98f);

    [SerializeField]
    private Button[] buttons = Array.Empty<Button>();

    [SerializeField]
    private TacticalHudGraphic[] faces = Array.Empty<TacticalHudGraphic>();

    private UnityAction[] clickHandlers = Array.Empty<UnityAction>();

    /// <summary>
    /// Raised when the player selects a supported tactical-battle speed.
    /// </summary>
    public event Action<TacticalBattleSpeed> SpeedRequested;

    /// <summary>
    /// Validates the authored controls and connects their click handlers.
    /// </summary>
    private void Awake()
    {
        int speedCount = Enum.GetValues(typeof(TacticalBattleSpeed)).Length;
        if (buttons == null || buttons.Length != speedCount)
        {
            throw new MissingReferenceException(
                $"{name} expects {speedCount} tactical-time buttons."
            );
        }
        if (faces == null || faces.Length != speedCount)
        {
            throw new MissingReferenceException(
                $"{name} expects {speedCount} tactical-time button faces."
            );
        }

        clickHandlers = new UnityAction[speedCount];
        for (int index = 0; index < speedCount; index++)
        {
            if (buttons[index] == null || faces[index] == null)
                throw new MissingReferenceException($"{name} has an incomplete time control.");
            TacticalBattleSpeed speed = (TacticalBattleSpeed)index;
            clickHandlers[index] = () => SpeedRequested?.Invoke(speed);
            buttons[index].onClick.AddListener(clickHandlers[index]);
        }
    }

    /// <summary>
    /// Removes runtime click handlers owned by this view.
    /// </summary>
    private void OnDestroy()
    {
        int count = Math.Min(buttons?.Length ?? 0, clickHandlers?.Length ?? 0);
        for (int index = 0; index < count; index++)
        {
            if (buttons[index] != null && clickHandlers[index] != null)
                buttons[index].onClick.RemoveListener(clickHandlers[index]);
        }
    }

    /// <summary>
    /// Highlights the active tactical-battle speed.
    /// </summary>
    /// <param name="speed">The active speed.</param>
    public void Render(TacticalBattleSpeed speed)
    {
        if (!Enum.IsDefined(typeof(TacticalBattleSpeed), speed))
            throw new ArgumentOutOfRangeException(nameof(speed));
        for (int index = 0; index < faces.Length; index++)
            faces[index].color = index == (int)speed ? _selectedColor : _normalColor;
    }
}
