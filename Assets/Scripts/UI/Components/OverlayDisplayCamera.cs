using UnityEngine;

/// <summary>
/// Marks a display camera that clears the screen beneath screen-space overlay canvases.
/// GraphicsManager omits scene rendering and camera effects for this role; 3D previews are
/// rendered separately into the textures displayed by the canvases.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class OverlayDisplayCamera : MonoBehaviour { }
