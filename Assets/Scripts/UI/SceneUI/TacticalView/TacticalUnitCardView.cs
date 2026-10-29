using System;
using System.Collections.Generic;
using Rebellion.Game.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One reusable roster card bound to a real combatant's portrait and durability.
/// </summary>
public sealed class TacticalUnitCardView
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
{
    [SerializeField]
    private TMP_Text unitCount;

    [SerializeField]
    private Button[] memberButtons;

    [SerializeField]
    private RawImage portrait;

    [SerializeField]
    private AspectRatioFitter portraitAspect;

    [SerializeField]
    private RectTransform hullFill;

    [SerializeField]
    private RectTransform shieldFill;

    [SerializeField]
    private TMP_Text hullLabel;

    [SerializeField]
    private TMP_Text shieldLabel;

    [SerializeField]
    private Button button;

    [SerializeField]
    private GameObject durabilityTooltip;
    private SpaceCombatUnit _combatant;
    private IReadOnlyList<SpaceCombatUnit> _combatants = Array.Empty<SpaceCombatUnit>();
    private readonly SpaceCombatUnit[] _single = new SpaceCombatUnit[1];
    private int _hull = int.MinValue;
    private int _maximumHull;
    private int? _shields;
    private int _maximumShields = -1;

    public event Action<IReadOnlyList<SpaceCombatUnit>> Clicked;
    public event Action<IReadOnlyList<SpaceCombatUnit>> DoubleClicked;
    public SpaceCombatUnit Combatant => _combatant;
    public IReadOnlyList<SpaceCombatUnit> Combatants => _combatants;

    /// <summary>
    /// Connects the authored roster button.
    /// </summary>
    private void Awake()
    {
        button.onClick.AddListener(Select);
        for (int i = 0; i < memberButtons.Length; i++)
        {
            int memberIndex = i;
            memberButtons[i].onClick.AddListener(() => SelectMember(memberIndex));
        }
    }

    /// <summary>
    /// Binds the roster card without creating or modifying game data.
    /// </summary>
    /// <param name="combatant">The combatant represented by the card.</param>
    /// <param name="texture">The combatant's resolved portrait, when available.</param>
    public void Bind(SpaceCombatUnit combatant, Texture2D texture)
    {
        _single[0] = combatant ?? throw new ArgumentNullException(nameof(combatant));
        BindGroup(_single, texture);
    }

    /// <summary>
    /// Binds one type-grouped selection card to its actual battle members.
    /// </summary>
    /// <param name="combatants">The nonempty group owned by the selection view.</param>
    /// <param name="texture">The group's resolved portrait.</param>
    public void BindGroup(IReadOnlyList<SpaceCombatUnit> combatants, Texture2D texture)
    {
        if (combatants == null || combatants.Count == 0)
            throw new ArgumentException(
                "A roster card needs at least one battle unit.",
                nameof(combatants)
            );
        _combatants = combatants;
        _combatant = combatants[0];
        unitCount.text = $"x{combatants.Count:00}";
        for (int i = 0; i < memberButtons.Length; i++)
            memberButtons[i].gameObject.SetActive(i < combatants.Count);
        portrait.texture = texture;
        portrait.enabled = texture;
        if (texture)
            portraitAspect.aspectRatio = (float)texture.width / texture.height;
        _hull = int.MinValue;
        _maximumShields = -1;
        Refresh();
    }

    /// <summary>
    /// Releases a pooled card's reference to a previous battle unit.
    /// </summary>
    public void Unbind()
    {
        _combatant = null;
        _combatants = Array.Empty<SpaceCombatUnit>();
        _single[0] = null;
        portrait.texture = null;
        portrait.enabled = false;
        durabilityTooltip.SetActive(false);
    }

    /// <summary>
    /// Refreshes only changed durability values on this selected combatant.
    /// </summary>
    public void Refresh()
    {
        if (_combatant == null)
            return;
        int hull = 0;
        int maximumHull = 0;
        int maximumShields = 0;
        int? shields = 0;
        float hullFraction = 0f;
        float shieldFraction = 0f;
        for (int i = 0; i < _combatants.Count; i++)
        {
            SpaceCombatUnit combatant = _combatants[i];
            int memberHull = combatant.Condition.CurrentHullStrength;
            int memberMaximumHull = combatant.MaxHullStrength;
            int memberMaximumShields = combatant.MaxShieldStrength;
            hull += memberHull;
            maximumHull += memberMaximumHull;
            maximumShields += memberMaximumShields;
            shields += combatant.Condition.CurrentShieldStrength;
            float memberHullFraction =
                memberMaximumHull > 0 ? Mathf.Clamp01((float)memberHull / memberMaximumHull) : 0f;
            hullFraction += memberHullFraction;
            if (i < memberButtons.Length)
                memberButtons[i].targetGraphic.color = Color.Lerp(
                    new Color(0.95f, 0.2f, 0.12f),
                    new Color(0.25f, 0.82f, 0.92f),
                    memberHullFraction
                );
            shieldFraction +=
                memberMaximumShields > 0 && combatant.Condition.CurrentShieldStrength.HasValue
                    ? Mathf.Clamp01(
                        (float)combatant.Condition.CurrentShieldStrength.Value
                            / memberMaximumShields
                    )
                    : 0f;
        }
        hullFill.anchorMax = new Vector2(hullFraction / _combatants.Count, 1f);
        shieldFill.anchorMax = new Vector2(shieldFraction / _combatants.Count, 1f);
        if (hull != _hull || maximumHull != _maximumHull)
        {
            if (maximumHull > 0)
                hullLabel.SetText("Hull: {0} / {1}", hull, maximumHull);
            else
                hullLabel.text = "Hull: Not configured";
            _hull = hull;
            _maximumHull = maximumHull;
        }
        if (_shields != shields || _maximumShields != maximumShields)
        {
            if (maximumShields <= 0)
                shieldLabel.text = "Shields: None";
            else if (shields.HasValue)
                shieldLabel.SetText("Shields: {0} / {1}", shields.Value, maximumShields);
            else
                shieldLabel.text = "Shields: Not recorded";
            _shields = shields;
            _maximumShields = maximumShields;
        }
    }

    /// <summary>
    /// Forwards a roster click to its selection owner.
    /// </summary>
    private void Select() => Clicked?.Invoke(_combatants);

    /// <summary>
    /// Selects one member through the same selection owner as the grouped card.
    /// </summary>
    /// <param name="index">The authored member position.</param>
    private void SelectMember(int index)
    {
        if (index < _combatants.Count)
            Clicked?.Invoke(new[] { _combatants[index] });
    }

    /// <summary>
    /// Shows exact durability values while the player inspects a roster card.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerEnter(PointerEventData eventData) => durabilityTooltip.SetActive(true);

    /// <summary>
    /// Releases the durability tooltip when the pointer leaves a card.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerExit(PointerEventData eventData) => durabilityTooltip.SetActive(false);

    /// <summary>
    /// Forwards a primary-button double click as a request to center on this card's unit.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
            DoubleClicked?.Invoke(_combatants);
    }

    /// <summary>
    /// Hides transient hover feedback when a pooled card is released.
    /// </summary>
    private void OnDisable() => durabilityTooltip.SetActive(false);

    /// <summary>
    /// Releases the authored button subscription.
    /// </summary>
    private void OnDestroy() => button.onClick.RemoveListener(Select);
}
