using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one authored mission-list row and reports its pointer gestures.
/// </summary>
[RequireComponent(typeof(UIPointerGestureRelay))]
public sealed class MissionListRowView : MonoBehaviour, IStrategyStatusDoubleClickTarget
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private UIPointerGestureRelay pointerGestures;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    [SerializeField]
    private RawImage selectionImage;

    /// <summary>
    /// Occurs when the control is double-clicked.
    /// </summary>
    internal event Action<MissionListRowView, PointerEventData> DoubleClicked;

    /// <summary>
    /// Occurs when a pointer drop is received by the control.
    /// </summary>
    internal event Action<MissionListRowView, PointerEventData> Dropped;

    /// <summary>
    /// Occurs when the control is pressed.
    /// </summary>
    internal event Action<MissionListRowView, PointerEventData> Pressed;

    /// <summary>
    /// Occurs when the control is released.
    /// </summary>
    internal event Action<MissionListRowView, PointerEventData> Released;

    internal int Index { get; private set; }

    /// <summary>
    /// Verifies the row's authored references.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindPointerGestures();
    }

    /// <summary>
    /// Releases authored pointer-gesture subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        UnbindPointerGestures();
    }

    /// <summary>
    /// Binds shared pointer gestures to row-level semantic events.
    /// </summary>
    private void BindPointerGestures()
    {
        pointerGestures.Pressed += HandlePressed;
        pointerGestures.Released += HandleReleased;
        pointerGestures.DoubleClicked += HandleDoubleClicked;
        pointerGestures.Dropped += HandleDropped;
    }

    /// <summary>
    /// Releases shared pointer-gesture subscriptions.
    /// </summary>
    private void UnbindPointerGestures()
    {
        if (pointerGestures == null)
            return;

        pointerGestures.Pressed -= HandlePressed;
        pointerGestures.Released -= HandleReleased;
        pointerGestures.DoubleClicked -= HandleDoubleClicked;
        pointerGestures.Dropped -= HandleDropped;
    }

    /// <summary>
    /// Forwards a pointer press with the owning mission row.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePressed(PointerEventData eventData)
    {
        Pressed?.Invoke(this, eventData);
    }

    /// <summary>
    /// Forwards a primary-pointer release with the owning mission row.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleReleased(PointerEventData eventData)
    {
        Released?.Invoke(this, eventData);
    }

    /// <summary>
    /// Forwards a primary-pointer double click with the owning mission row.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDoubleClicked(PointerEventData eventData)
    {
        DoubleClicked?.Invoke(this, eventData);
    }

    /// <summary>
    /// Forwards a pointer drop with the owning mission row.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDropped(PointerEventData eventData)
    {
        Dropped?.Invoke(this, eventData);
    }

    /// <summary>
    /// Assigns the row's current visual index.
    /// </summary>
    /// <param name="index">The zero-based visual index.</param>
    internal void SetIndex(int index)
    {
        Index = index;
    }

    /// <summary>
    /// Applies one complete mission-row presentation snapshot.
    /// </summary>
    /// <param name="data">The immutable row snapshot.</param>
    internal void Render(MissionListRowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
        UILayout.SetImageTexture(iconImage, data.IconTexture);
        UILayout.SetTextContent(nameTextField, data.Name);
        UILayout.SetImageTexture(selectionImage, data.SelectionTexture);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Ensures every authored row reference is assigned.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (pointerGestures == null)
            throw new MissingReferenceException($"{name}/PointerGestures is missing.");
        if (iconImage == null)
            throw new MissingReferenceException($"{name}/IconImage is missing.");
        if (nameTextField == null)
            throw new MissingReferenceException($"{name}/NameTextField is missing.");
        if (selectionImage == null)
            throw new MissingReferenceException($"{name}/SelectionImage is missing.");
    }
}

/// <summary>
/// Contains immutable presentation data for one mission-list row.
/// </summary>
public sealed class MissionListRowRenderData
{
    /// <summary>
    /// Creates one complete mission-list row snapshot.
    /// </summary>
    /// <param name="name">The displayed mission name.</param>
    /// <param name="iconTexture">The displayed mission icon.</param>
    /// <param name="selectionTexture">The optional selection overlay.</param>
    public MissionListRowRenderData(string name, Texture iconTexture, Texture selectionTexture)
    {
        Name = name ?? string.Empty;
        IconTexture = iconTexture;
        SelectionTexture = selectionTexture;
    }

    public string Name { get; }

    public Texture IconTexture { get; }

    public Texture SelectionTexture { get; }
}
