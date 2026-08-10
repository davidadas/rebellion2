using UnityEngine;

/// <summary>
/// Re-materialises a runtime-loaded cloud model with a built-in transparent material, reusing the
/// texture GLTFast loaded. The imported glTF transparent material does not render in this project's
/// built-in pipeline, so the cloud layer is rebuilt here once the model has finished loading.
/// </summary>
public sealed class CloudMaterialFix : MonoBehaviour
{
    private bool applied;

    /// <summary>
    /// Waits for the async-loaded cloud mesh to appear, then swaps in a transparent material.
    /// </summary>
    private void Update()
    {
        if (applied)
            return;

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0)
            return;

        Shader shader = ResolveTransparentShader();
        foreach (MeshRenderer renderer in renderers)
        {
            Texture texture = ExtractTexture(renderer.sharedMaterial);
            Material material = new Material(shader);
            if (material.HasProperty("_MainTex"))
                material.mainTexture = texture;
            if (material.HasProperty("_Color"))
                material.color = Color.white;
            renderer.sharedMaterial = material;
        }

        applied = true;
    }

    /// <summary>
    /// Finds the first available built-in alpha-blended shader.
    /// </summary>
    /// <returns>A transparent shader, falling back to Standard if none resolve.</returns>
    private static Shader ResolveTransparentShader()
    {
        string[] candidates =
        {
            "Unlit/Transparent",
            "Sprites/Default",
            "Legacy Shaders/Transparent/Diffuse",
            "Transparent/Diffuse",
        };
        foreach (string candidate in candidates)
        {
            Shader shader = Shader.Find(candidate);
            if (shader != null)
                return shader;
        }
        return Shader.Find("Standard");
    }

    /// <summary>
    /// Reads the base-color texture from a glTF-imported material regardless of its property naming.
    /// </summary>
    /// <param name="material">The imported material to read from.</param>
    /// <returns>The base-color texture, or null when none is present.</returns>
    private static Texture ExtractTexture(Material material)
    {
        if (material == null)
            return null;

        string[] properties = { "_BaseColorTexture", "_BaseMap", "_MainTex", "baseColorTexture" };
        foreach (string property in properties)
            if (material.HasProperty(property) && material.GetTexture(property) != null)
                return material.GetTexture(property);

        return material.mainTexture;
    }
}
