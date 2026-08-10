using UnityEngine;

/// <summary>
/// Assigns the atmosphere rim material at runtime. A material baked into the prefab at build time
/// resolves to the error (magenta) shader here, whereas a runtime <c>Shader.Find</c> resolves
/// reliably — the same pattern the cloud layer uses.
/// </summary>
public sealed class AtmosphereMaterialFix : MonoBehaviour
{
    /// <summary>
    /// Resolves the rim shader and applies it to this object's renderer once play begins.
    /// </summary>
    private void Start()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            return;

        Shader shader = Shader.Find("Custom/AtmosphereRim");
        if (shader == null)
        {
            Debug.LogWarning("AtmosphereMaterialFix: 'Custom/AtmosphereRim' shader not found.");
            return;
        }

        meshRenderer.sharedMaterial = new Material(shader);
    }
}
