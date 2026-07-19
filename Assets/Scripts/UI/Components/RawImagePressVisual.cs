using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Applies authored normal and pressed textures to an interactive raw image.
/// </summary>
[RequireComponent(typeof(RawImage))]
public sealed class RawImagePressVisual
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
{
    [SerializeField]
    private RawImage image;

    [SerializeField]
    private Button button;

    [SerializeField]
    private Texture upTexture;

    [SerializeField]
    private Texture downTexture;

    private bool pressed;

    /// <summary>
    /// Sets the normal and pressed textures and synchronizes image visibility and raycasting.
    /// </summary>
    /// <param name="normalTexture">The texture displayed while released.</param>
    /// <param name="pressedTexture">The texture displayed while pressed.</param>
    public void SetInteractiveTextures(Texture normalTexture, Texture pressedTexture)
    {
        VerifyReferences();
        image.texture = normalTexture;
        image.enabled = normalTexture != null;
        image.gameObject.SetActive(normalTexture != null);
        image.raycastTarget = normalTexture != null;
        ApplyTextures(normalTexture, pressedTexture);
    }

    /// <summary>
    /// Sets the normal and pressed textures and restores the normal state.
    /// </summary>
    /// <param name="normalTexture">The texture displayed while released.</param>
    /// <param name="pressedTexture">The texture displayed while pressed.</param>
    public void SetTextures(Texture normalTexture, Texture pressedTexture)
    {
        VerifyReferences();
        ApplyTextures(normalTexture, pressedTexture);
    }

    /// <summary>
    /// Stores the normal and pressed textures and restores the normal state.
    /// </summary>
    /// <param name="normalTexture">The texture displayed while released.</param>
    /// <param name="pressedTexture">The texture displayed while pressed.</param>
    private void ApplyTextures(Texture normalTexture, Texture pressedTexture)
    {
        upTexture = normalTexture;
        downTexture = pressedTexture;
        pressed = false;
        ApplyUpTexture();
    }

    /// <summary>
    /// Verifies the authored control references when the component is loaded.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
    }

    /// <summary>
    /// Restores the normal visual when the control becomes inactive.
    /// </summary>
    private void OnDisable()
    {
        pressed = false;
        ApplyUpTexture();
    }

    /// <summary>
    /// Applies the pressed texture when the interactive control receives a pointer press.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsPressable())
            return;

        pressed = true;
        image.texture = downTexture;
    }

    /// <summary>
    /// Restores the normal texture when the pointer is released.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    /// <summary>
    /// Restores the normal texture when a held pointer leaves the control.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        Release();
    }

    /// <summary>
    /// Determines whether the current control can display a pressed state.
    /// </summary>
    /// <returns>True when the image is visible, interactive, and has a pressed texture.</returns>
    private bool IsPressable()
    {
        return image
            && image.enabled
            && image.gameObject.activeInHierarchy
            && downTexture != null
            && (!button || button.interactable);
    }

    /// <summary>
    /// Restores the normal texture after an active pointer press.
    /// </summary>
    private void Release()
    {
        if (!pressed)
            return;

        pressed = false;
        ApplyUpTexture();
    }

    /// <summary>
    /// Applies the configured normal texture when the image is available.
    /// </summary>
    private void ApplyUpTexture()
    {
        if (image)
            image.texture = upTexture;
    }

    /// <summary>
    /// Verifies the authored image and optional button references.
    /// </summary>
    private void VerifyReferences()
    {
        if (image == null)
            throw new MissingReferenceException($"{name}/Image is missing.");
    }
}
