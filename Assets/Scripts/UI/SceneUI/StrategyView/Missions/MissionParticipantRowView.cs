using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one authored mission-participant row and reports its pointer gestures.
/// </summary>
[RequireComponent(typeof(UIPointerGestureRelay))]
public sealed class MissionParticipantRowView : MonoBehaviour
{
    [SerializeField]
    private UIPointerGestureRelay pointerGestures;

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage entityImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    [SerializeField]
    private Texture2D backgroundTexture;

    [SerializeField]
    private Texture2D inTransitBackgroundTexture;

    private RectInt entitySlotRect;
    private bool hasEntitySlotRect;

    /// <summary>
    /// Occurs when the control is released.
    /// </summary>
    internal event Action<MissionParticipantRowView, PointerEventData> Released;

    /// <summary>
    /// Occurs when the control is pressed.
    /// </summary>
    internal event Action<MissionParticipantRowView, PointerEventData> Pressed;

    /// <summary>
    /// Gets the zero-based participant index represented by this row.
    /// </summary>
    internal int Index { get; private set; }

    /// <summary>
    /// Gets the semantic participant role assigned by the parent view.
    /// </summary>
    internal MissionParticipantRole Role { get; private set; }

    /// <summary>
    /// Verifies the row's authored references.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindPointerGestures();
    }

    /// <summary>
    /// Releases the shared pointer-gesture subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        UnbindPointerGestures();
    }

    /// <summary>
    /// Assigns the row's semantic role and visual index.
    /// </summary>
    /// <param name="role">The parent-defined participant role.</param>
    /// <param name="index">The zero-based visual index.</param>
    internal void SetPosition(MissionParticipantRole role, int index)
    {
        Role = role;
        Index = index;
    }

    /// <summary>
    /// Binds the shared pointer relay to the row's semantic gestures.
    /// </summary>
    private void BindPointerGestures()
    {
        pointerGestures.Pressed += HandlePressed;
        pointerGestures.Released += HandleReleased;
    }

    /// <summary>
    /// Releases the shared pointer relay from the row's semantic gestures.
    /// </summary>
    private void UnbindPointerGestures()
    {
        if (pointerGestures == null)
            return;

        pointerGestures.Pressed -= HandlePressed;
        pointerGestures.Released -= HandleReleased;
    }

    /// <summary>
    /// Forwards one supported pointer press.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePressed(PointerEventData eventData)
    {
        Pressed?.Invoke(this, eventData);
    }

    /// <summary>
    /// Forwards one completed primary-button gesture with its original click count.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleReleased(PointerEventData eventData)
    {
        Released?.Invoke(this, eventData);
    }

    /// <summary>
    /// Applies one complete participant-row presentation snapshot.
    /// </summary>
    /// <param name="data">The immutable row snapshot.</param>
    internal void Render(MissionParticipantRowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        UILayout.SetInteractiveImageTexture(
            backgroundImage,
            data.BackgroundTexture
                ?? (data.UseInTransitBackground ? inTransitBackgroundTexture : backgroundTexture)
        );
        UILayout.SetCenteredImage(entityImage, data.EntityTexture, entitySlotRect);
        UILayout.SetTextContent(nameTextField, data.Name, data.NameColor);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Ensures every authored row reference is assigned and caches its image slot.
    /// </summary>
    private void VerifyReferences()
    {
        if (pointerGestures == null)
            throw new MissingReferenceException($"{name}/PointerGestures is missing.");
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (entityImage == null)
            throw new MissingReferenceException($"{name}/EntityImage is missing.");
        if (nameTextField == null)
            throw new MissingReferenceException($"{name}/NameTextField is missing.");
        if (backgroundTexture == null)
            throw new MissingReferenceException($"{name}/BackgroundTexture is missing.");
        if (inTransitBackgroundTexture == null)
            throw new MissingReferenceException($"{name}/InTransitBackgroundTexture is missing.");

        if (hasEntitySlotRect)
            return;

        entitySlotRect = UILayout.GetSourceRect(entityImage.rectTransform);
        hasEntitySlotRect = true;
    }
}

/// <summary>
/// Contains immutable presentation data for one mission participant row.
/// </summary>
public sealed class MissionParticipantRowRenderData
{
    /// <summary>
    /// Creates one complete participant-row presentation snapshot.
    /// </summary>
    /// <param name="name">The displayed participant name.</param>
    /// <param name="nameColor">The displayed participant-name color.</param>
    /// <param name="backgroundTexture">The optional state background.</param>
    /// <param name="entityTexture">The displayed participant image.</param>
    /// <param name="useInTransitBackground">Whether the authored in-transit background is the fallback.</param>
    public MissionParticipantRowRenderData(
        string name,
        Color32 nameColor,
        Texture backgroundTexture,
        Texture entityTexture,
        bool useInTransitBackground = false
    )
    {
        Name = name ?? string.Empty;
        NameColor = nameColor;
        BackgroundTexture = backgroundTexture;
        EntityTexture = entityTexture;
        UseInTransitBackground = useInTransitBackground;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the name color.
    /// </summary>
    public Color32 NameColor { get; }

    /// <summary>
    /// Gets the background texture.
    /// </summary>
    public Texture BackgroundTexture { get; }

    /// <summary>
    /// Gets the entity texture.
    /// </summary>
    public Texture EntityTexture { get; }

    /// <summary>
    /// Gets a value indicating whether in transit background is used.
    /// </summary>
    public bool UseInTransitBackground { get; }
}
