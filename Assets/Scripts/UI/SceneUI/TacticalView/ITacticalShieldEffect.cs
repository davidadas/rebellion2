using UnityEngine;

/// <summary>
/// Provides tactical combat with a render-pipeline-independent shield presentation boundary.
/// </summary>
public interface ITacticalShieldEffect
{
    /// <summary>
    /// Connects the effect to the camera that renders its battle.
    /// </summary>
    /// <param name="camera">The tactical battle camera.</param>
    void Configure(Camera camera);

    /// <summary>
    /// Shows or hides the shield using its authored activation behavior.
    /// </summary>
    /// <param name="active">Whether the shield currently has strength.</param>
    void SetActive(bool active);

    /// <summary>
    /// Presents one absorbed weapon impact on the shield surface.
    /// </summary>
    /// <param name="position">The world-space hull contact to project onto the effect surface.</param>
    /// <param name="normal">The world-space impact normal.</param>
    /// <param name="scale">The world-space impact radius.</param>
    /// <param name="duration">The impact lifetime in seconds.</param>
    void ShowHit(Vector3 position, Vector3 normal, float scale, float duration);
}
