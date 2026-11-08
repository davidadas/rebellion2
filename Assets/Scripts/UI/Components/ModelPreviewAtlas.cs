using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combines loaded orthographic model previews into one camera while retaining their pixel framing.
/// </summary>
public sealed class ModelPreviewAtlas : MonoBehaviour
{
    [SerializeField]
    private Camera atlasCamera;

    [SerializeField]
    private Preview[] previews = Array.Empty<Preview>();

    private bool initialized;

    /// <summary>
    /// Assigns the shared camera and the authored preview regions before initialization.
    /// </summary>
    /// <param name="camera">The disabled orthographic camera targeting the atlas.</param>
    /// <param name="regions">The source rigs and their atlas pixel centers.</param>
    public void Configure(Camera camera, Preview[] regions)
    {
        atlasCamera = camera ?? throw new ArgumentNullException(nameof(camera));
        previews = regions ?? throw new ArgumentNullException(nameof(regions));
    }

    /// <summary>
    /// Moves the loaded rigs into the shared view without changing their animation or selection state.
    /// Call after model loading and normalization have completed, before showing the preview images.
    /// </summary>
    public void Initialize()
    {
        if (initialized)
            return;

        RenderTexture atlas = atlasCamera.targetTexture;
        float pixelsPerUnit = atlas.height / (2f * atlasCamera.orthographicSize);
        foreach (Preview preview in previews)
            RenderPreview(preview, atlas, pixelsPerUnit);

        atlasCamera.enabled = true;
        initialized = true;
    }

    /// <summary>
    /// Preserves one preview's projection, local lighting, and image crop in the shared camera.
    /// </summary>
    /// <param name="preview">The loaded preview and its output images.</param>
    /// <param name="atlas">The shared render target.</param>
    /// <param name="pixelsPerUnit">The shared camera's pixel density in world space.</param>
    private void RenderPreview(Preview preview, RenderTexture atlas, float pixelsPerUnit)
    {
        Camera source = preview.SourceCamera;
        RenderTexture target = source.targetTexture;
        float scale = target.height / (2f * source.orthographicSize) / pixelsPerUnit;
        float distance = Vector3.Dot(
            preview.Rig.position - source.transform.position,
            source.transform.forward
        );
        Vector3 cameraPosition = new Vector3(
            (preview.Center.x - atlas.width * 0.5f) / pixelsPerUnit,
            (preview.Center.y - atlas.height * 0.5f) / pixelsPerUnit,
            distance
        );

        source.enabled = false;
        preview.Rig.position = atlasCamera.transform.TransformPoint(cameraPosition);
        preview.Rig.localScale *= scale;

        // Uniformly scaling a lit rig also scales light distances. Candela must scale by the
        // squared distance to retain illuminance; the source radius retains its relative size.
        foreach (Light light in preview.Lights)
        {
            light.intensity *= scale * scale;
            light.range *= scale;
            light.shapeRadius *= scale;
        }

        Rect uv = new Rect(
            (preview.Center.x - target.width * 0.5f) / atlas.width,
            (preview.Center.y - target.height * 0.5f) / atlas.height,
            (float)target.width / atlas.width,
            (float)target.height / atlas.height
        );
        foreach (RawImage image in preview.Images)
        {
            image.texture = atlas;
            image.uvRect = uv;
        }
    }

    /// <summary>
    /// Describes one isolated source rig, its local lights, and the images displaying its crop.
    /// </summary>
    [Serializable]
    public sealed class Preview
    {
        [SerializeField]
        private Transform rig;

        [SerializeField]
        private Camera sourceCamera;

        [SerializeField]
        private Vector2Int center;

        [SerializeField]
        private RawImage[] images;

        [SerializeField]
        private Light[] lights;

        public Transform Rig => rig;
        public Camera SourceCamera => sourceCamera;
        public Vector2Int Center => center;
        public RawImage[] Images => images;
        public Light[] Lights => lights;

        /// <summary>
        /// Records a source preview and its authored atlas placement.
        /// </summary>
        /// <param name="rig">The root containing the model, pivot, source camera, and local lights.</param>
        /// <param name="sourceCamera">The orthographic camera defining the original pixel framing.</param>
        /// <param name="center">The center of the source image's region in atlas pixels.</param>
        /// <param name="images">The UI images displaying the source render texture.</param>
        /// <param name="lights">The rig's local lights, excluding shared directional lighting.</param>
        public Preview(
            Transform rig,
            Camera sourceCamera,
            Vector2Int center,
            RawImage[] images,
            Light[] lights
        )
        {
            this.rig = rig;
            this.sourceCamera = sourceCamera;
            this.center = center;
            this.images = images;
            this.lights = lights;
        }
    }
}
