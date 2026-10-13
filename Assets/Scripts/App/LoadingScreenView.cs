using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents an application-owned hyperspace scene while a saved game loads.
/// </summary>
[DisallowMultipleComponent]
public sealed class LoadingScreenView : MonoBehaviour
{
    private const string _loadingText = "Loading ...";
    private const string _shuttleRelativePath =
        "Application/Loading/Models/lambda-imperial-shuttle.glb";
    private const int _presentationLayer = 31;
    private const int _renderTextureWidth = 1280;
    private const int _renderTextureHeight = 720;
    private const float _maximumYaw = 45f;
    private const float _yawDegreesPerScreen = 110f;
    private const float _orbitSmoothTime = 0.1f;

    [SerializeField]
    private TextMeshProUGUI statusText;

    [SerializeField]
    private RawImage shuttleViewport;

    [SerializeField]
    private HyperspaceStarfieldGraphic starfield;

    [SerializeField]
    private CanvasGroup presentationGroup;

    [SerializeField]
    private CanvasGroup fadeOverlayGroup;

    [SerializeField]
    private Transform shuttleCameraOrbit;

    [SerializeField]
    private Camera shuttleCamera;

    [SerializeField]
    private Transform shuttleModelRoot;

    private CancellationTokenSource cancellation;
    private ContentModelInstance modelInstance;
    private Material shuttleExhaustCoreMaterial;
    private Material shuttleExhaustMaterial;
    private Mesh shuttleExhaustMesh;
    private Transform[] shuttleExhaustPlumes;
    private Vector3[] shuttleExhaustScales;
    private RenderTexture shuttleRenderTexture;
    private LimitedYawDrag yawDrag;
    private float displayedYaw;
    private float yawVelocity;
    private Quaternion shuttleCameraOrbitRestRotation;
    private Vector3 shuttleRestPosition;
    private Quaternion shuttleRestRotation;
    private Vector3 shuttleRestScale;
    private bool modelReady;

    /// <summary>
    /// Verifies the generated presentation, creates its render target, and seeds the starfield.
    /// </summary>
    private void Awake()
    {
        if (
            statusText == null
            || shuttleViewport == null
            || starfield == null
            || presentationGroup == null
            || fadeOverlayGroup == null
            || shuttleCameraOrbit == null
            || shuttleCamera == null
            || shuttleModelRoot == null
        )
        {
            throw new MissingReferenceException(
                $"{name} requires status, starfield, transition, viewport, camera-orbit, camera, and model-root references."
            );
        }

        statusText.text = _loadingText;
        yawDrag = new LimitedYawDrag(_maximumYaw, _yawDegreesPerScreen);
        shuttleCameraOrbitRestRotation = shuttleCameraOrbit.localRotation;
        cancellation = new CancellationTokenSource();
        CreateRenderTarget();
    }

    /// <summary>
    /// Starts loading the shuttle from application content before the active content pack is
    /// replaced.
    /// </summary>
    /// <param name="contentRootPath">The absolute external content root.</param>
    public void Initialize(string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath))
            throw new ArgumentException(
                "A content root path is required.",
                nameof(contentRootPath)
            );

        cancellation ??= new CancellationTokenSource();
        _ = LoadShuttleAsync(
            Path.Combine(
                contentRootPath,
                _shuttleRelativePath.Replace('/', Path.DirectorySeparatorChar)
            ),
            cancellation.Token
        );
    }

    /// <summary>
    /// Advances the looping hyperspace streaks and gives the shuttle a subtle in-flight drift.
    /// </summary>
    private void Update()
    {
        float time = Time.unscaledTime;
        int dotCount = 1 + Mathf.FloorToInt(time * 2.25f) % 3;
        statusText.text = $"LOADING{new string('.', dotCount)}";
        statusText.alpha = Mathf.Lerp(0.72f, 1f, (Mathf.Sin(time * 3.2f) + 1f) * 0.5f);
        UpdateCameraDragInput();
        displayedYaw = Mathf.SmoothDampAngle(
            displayedYaw,
            yawDrag.TargetYaw,
            ref yawVelocity,
            _orbitSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );
        if (modelReady)
        {
            float thrust = (Mathf.Sin(time * 0.9f) + 1f) * 0.5f;
            shuttleModelRoot.localPosition =
                shuttleRestPosition
                + new Vector3(
                    Mathf.Sin(time * 0.37f) * 0.022f,
                    Mathf.Sin(time * 0.53f) * 0.035f,
                    Mathf.Sin(time * 0.41f) * 0.018f
                );
            shuttleModelRoot.localRotation =
                shuttleRestRotation
                * Quaternion.Euler(
                    Mathf.Sin(time * 0.55f) * 0.8f,
                    Mathf.Sin(time * 0.31f) * 1.35f,
                    Mathf.Sin(time * 0.42f) * 0.55f
                );
            shuttleModelRoot.localScale = shuttleRestScale * Mathf.Lerp(0.997f, 1.004f, thrust);
            UpdateEngineExhaust(time);
        }

        UpdateCameraView();
    }

    /// <summary>
    /// Captures and updates a left-button horizontal drag independently from scene event systems.
    /// </summary>
    private void UpdateCameraDragInput()
    {
        if (Input.GetMouseButtonDown(0))
            yawDrag.Begin(Input.mousePosition.x, displayedYaw);

        if (Input.GetMouseButton(0))
            yawDrag.Move(Input.mousePosition.x, Screen.width);

        if (Input.GetMouseButtonUp(0))
            yawDrag.End();
    }

    /// <summary>
    /// Orbits the camera around the shuttle and keeps the camera aimed at it.
    /// </summary>
    private void UpdateCameraView()
    {
        shuttleCameraOrbit.localRotation =
            Quaternion.Euler(0f, displayedYaw, 0f) * shuttleCameraOrbitRestRotation;

        starfield.SetViewYaw(displayedYaw / _maximumYaw);
    }

    /// <summary>
    /// Shows or hides the prewarmed presentation without releasing its rendering resources.
    /// </summary>
    /// <param name="visible">Whether the presentation should render.</param>
    public void SetVisible(bool visible)
    {
        if (visible)
        {
            ResetCameraInteraction();
            presentationGroup.alpha = 1f;
            fadeOverlayGroup.alpha = 0f;
        }
        else
        {
            yawDrag?.End();
        }

        gameObject.SetActive(visible);
    }

    /// <summary>
    /// Activates the transition layer while leaving the outgoing scene visible beneath it.
    /// </summary>
    internal void PrepareEntrance()
    {
        ResetCameraInteraction();
        presentationGroup.alpha = 0f;
        fadeOverlayGroup.alpha = 0f;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Controls whether the loading presentation is visible behind the black transition layer.
    /// </summary>
    /// <param name="visible">Whether the loading presentation should render.</param>
    internal void SetPresentationVisible(bool visible)
    {
        presentationGroup.alpha = visible ? 1f : 0f;
    }

    /// <summary>
    /// Sets the opacity of the full-screen black transition layer.
    /// </summary>
    /// <param name="opacity">The desired opacity from transparent to opaque.</param>
    internal void SetBlackOpacity(float opacity)
    {
        fadeOverlayGroup.alpha = Mathf.Clamp01(opacity);
    }

    /// <summary>
    /// Recenters the camera and clears any active drag gesture.
    /// </summary>
    private void ResetCameraInteraction()
    {
        yawDrag?.Reset();
        displayedYaw = 0f;
        yawVelocity = 0f;
        UpdateCameraView();
    }

    /// <summary>
    /// Cancels the model load and releases all runtime rendering resources.
    /// </summary>
    private void OnDestroy()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;

        modelInstance?.Dispose();
        modelInstance = null;

        if (shuttleExhaustMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(shuttleExhaustMaterial);
            else
                DestroyImmediate(shuttleExhaustMaterial);
            shuttleExhaustMaterial = null;
        }
        if (shuttleExhaustCoreMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(shuttleExhaustCoreMaterial);
            else
                DestroyImmediate(shuttleExhaustCoreMaterial);
            shuttleExhaustCoreMaterial = null;
        }
        if (shuttleExhaustMesh != null)
        {
            if (Application.isPlaying)
                Destroy(shuttleExhaustMesh);
            else
                DestroyImmediate(shuttleExhaustMesh);
            shuttleExhaustMesh = null;
        }

        if (shuttleCamera != null)
            shuttleCamera.targetTexture = null;
        if (shuttleViewport != null)
            shuttleViewport.texture = null;
        if (shuttleRenderTexture != null)
        {
            shuttleRenderTexture.Release();
            if (Application.isPlaying)
                Destroy(shuttleRenderTexture);
            else
                DestroyImmediate(shuttleRenderTexture);
            shuttleRenderTexture = null;
        }
    }

    /// <summary>
    /// Creates the transparent render target composited over the hyperspace starfield.
    /// </summary>
    private void CreateRenderTarget()
    {
        shuttleRenderTexture = new RenderTexture(
            _renderTextureWidth,
            _renderTextureHeight,
            24,
            RenderTextureFormat.ARGB32
        )
        {
            name = "Loading Shuttle",
            antiAliasing = 4,
            useMipMap = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        shuttleRenderTexture.Create();
        shuttleCamera.targetTexture = shuttleRenderTexture;
        shuttleViewport.texture = shuttleRenderTexture;
    }

    /// <summary>
    /// Loads, instantiates, and frames the shuttle independently from the active content services.
    /// </summary>
    /// <param name="filePath">The absolute shuttle GLB path.</param>
    /// <param name="cancellationToken">Cancels loading when the presentation closes.</param>
    /// <returns>A task representing the asynchronous model load.</returns>
    private async Task LoadShuttleAsync(string filePath, CancellationToken cancellationToken)
    {
        ContentModelResource resource = null;
        ContentModelInstance loadedInstance = null;
        try
        {
            resource = await ContentModelLoader.LoadResourceAsync(filePath, cancellationToken);
            loadedInstance = await resource.InstantiateAsync(shuttleModelRoot, cancellationToken);
            loadedInstance.TakeOwnership(resource);
            resource = null;
            cancellationToken.ThrowIfCancellationRequested();

            modelInstance = loadedInstance;
            loadedInstance = null;
            PoseShuttle(modelInstance.ModelRoot);
            modelReady = true;
        }
        catch (OperationCanceledException)
        {
            // The loading page completed before its decorative model finished loading.
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Loading shuttle unavailable; continuing with hyperspace.\n{exception}"
            );
        }
        finally
        {
            loadedInstance?.Dispose();
            resource?.Dispose();
        }
    }

    /// <summary>
    /// Normalizes, centers, lights, and angles the loaded shuttle for the loading composition.
    /// </summary>
    /// <param name="model">The instantiated shuttle model.</param>
    private void PoseShuttle(Transform model)
    {
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        PoseWingsForFlight(model);
        if (TryGetBounds(model, out Bounds bounds))
        {
            float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largestDimension > 0f)
                model.localScale *= 2.05f / largestDimension;

            if (TryGetBounds(model, out bounds))
                model.position += shuttleModelRoot.position - bounds.center;
        }

        SetLayerRecursively(model.gameObject, _presentationLayer);
        if (TryGetNamedRendererBounds(model, "ion-engine_39", out Bounds engineBounds))
            CreateEngineExhaust(engineBounds);
        else
            Debug.LogWarning("Loading shuttle exhaust skipped because its engine mesh is missing.");
        shuttleRestPosition = shuttleModelRoot.localPosition;
        shuttleRestRotation = shuttleModelRoot.localRotation;
        shuttleRestScale = shuttleModelRoot.localScale;
    }

    /// <summary>
    /// Adds compact blue exhaust plumes directly behind the shuttle's engine banks.
    /// </summary>
    /// <param name="engineBounds">The bounds of the shuttle's ion-engine mesh.</param>
    private void CreateEngineExhaust(Bounds engineBounds)
    {
        Shader exhaustShader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (exhaustShader == null)
            exhaustShader = Shader.Find("Sprites/Default");
        if (exhaustShader == null)
        {
            Debug.LogWarning(
                "Loading shuttle exhaust skipped because no particle shader is available."
            );
            return;
        }

        shuttleExhaustMaterial = CreateExhaustMaterial(
            exhaustShader,
            "Loading Shuttle Exhaust",
            new Color(0.015f, 0.28f, 1f, 0.34f)
        );
        shuttleExhaustCoreMaterial = CreateExhaustMaterial(
            exhaustShader,
            "Loading Shuttle Exhaust Core",
            new Color(0.04f, 0.82f, 1f, 0.72f)
        );
        shuttleExhaustMesh = CreateExhaustPlumeMesh();

        Vector3 rearCenter = shuttleModelRoot.InverseTransformPoint(
            new Vector3(engineBounds.center.x, engineBounds.center.y, engineBounds.min.z)
        );
        float width = engineBounds.size.x;
        float bankWidth = width * 0.49f;
        float bankHeight = Mathf.Max(engineBounds.size.y * 0.72f, width * 0.055f);
        float plumeLength = width * 0.62f;
        float[] horizontalOffsets = { -0.25f, 0.25f };
        shuttleExhaustPlumes = new Transform[horizontalOffsets.Length * 2];
        shuttleExhaustScales = new Vector3[shuttleExhaustPlumes.Length];
        int plumeIndex = 0;
        foreach (float horizontalOffset in horizontalOffsets)
        {
            Vector3 position = rearCenter + new Vector3(width * horizontalOffset, 0f, -0.012f);
            CreateEnginePlume(
                plumeIndex++,
                "EnginePlumeOuter",
                position,
                new Vector3(bankWidth, bankHeight, plumeLength),
                shuttleExhaustMaterial
            );
            CreateEnginePlume(
                plumeIndex++,
                "EnginePlumeCore",
                position + new Vector3(0f, 0f, -0.01f),
                new Vector3(bankWidth * 0.82f, bankHeight * 0.52f, plumeLength * 0.62f),
                shuttleExhaustCoreMaterial
            );
        }
    }

    /// <summary>
    /// Creates one tapered plasma plume aimed behind the shuttle.
    /// </summary>
    /// <param name="index">The plume's animation slot.</param>
    /// <param name="name">The plume object name.</param>
    /// <param name="localPosition">The engine position in shuttle-root space.</param>
    /// <param name="localScale">The plume width, height, and length.</param>
    /// <param name="material">The additive plume material.</param>
    private void CreateEnginePlume(
        int index,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Material material
    )
    {
        GameObject plumeObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        plumeObject.layer = _presentationLayer;
        plumeObject.transform.SetParent(shuttleModelRoot, false);
        plumeObject.transform.localPosition = localPosition;
        plumeObject.transform.localScale = localScale;
        plumeObject.GetComponent<MeshFilter>().sharedMesh = shuttleExhaustMesh;
        plumeObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        shuttleExhaustPlumes[index] = plumeObject.transform;
        shuttleExhaustScales[index] = localScale;
    }

    /// <summary>
    /// Pulses the layered engine plumes using unscaled loading-screen time.
    /// </summary>
    /// <param name="time">The current unscaled time.</param>
    private void UpdateEngineExhaust(float time)
    {
        if (shuttleExhaustPlumes == null)
            return;

        for (int index = 0; index < shuttleExhaustPlumes.Length; index++)
        {
            Transform plume = shuttleExhaustPlumes[index];
            if (plume == null)
                continue;

            Vector3 baseScale = shuttleExhaustScales[index];
            float pulse = 1f + Mathf.Sin(time * 19f + index * 1.73f) * 0.075f;
            float ripple = 1f + Mathf.Sin(time * 11f + index * 2.31f) * 0.035f;
            plume.localScale = new Vector3(
                baseScale.x * ripple,
                baseScale.y * ripple,
                baseScale.z * pulse
            );
        }
    }

    /// <summary>
    /// Creates one additive exhaust material with a compatible tint property.
    /// </summary>
    /// <param name="shader">The selected transparent shader.</param>
    /// <param name="name">The runtime material name.</param>
    /// <param name="color">The plume tint and opacity.</param>
    /// <returns>The configured runtime material.</returns>
    private static Material CreateExhaustMaterial(Shader shader, string name, Color color)
    {
        Material material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        return material;
    }

    /// <summary>
    /// Builds a rectangular tapered plume with a visible engine-facing cap.
    /// </summary>
    /// <returns>A unit plume extending one unit along local negative Z.</returns>
    private static Mesh CreateExhaustPlumeMesh()
    {
        const float tipScale = 0.18f;
        Vector3[] vertices =
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f * tipScale, -0.5f * tipScale, -1f),
            new Vector3(-0.5f * tipScale, 0.5f * tipScale, -1f),
            new Vector3(0.5f * tipScale, 0.5f * tipScale, -1f),
            new Vector3(0.5f * tipScale, -0.5f * tipScale, -1f),
        };
        int[] triangles =
        {
            0,
            1,
            2,
            0,
            2,
            3,
            0,
            4,
            5,
            0,
            5,
            1,
            1,
            5,
            6,
            1,
            6,
            2,
            2,
            6,
            7,
            2,
            7,
            3,
            3,
            7,
            4,
            3,
            4,
            0,
        };
        Color32 baseColor = new Color(0.12f, 0.82f, 1f, 0.92f);
        Color32 tipColor = new Color(0.015f, 0.08f, 1f, 0f);
        Color32[] colors =
        {
            baseColor,
            baseColor,
            baseColor,
            baseColor,
            tipColor,
            tipColor,
            tipColor,
            tipColor,
        };
        Mesh mesh = new Mesh { name = "Loading Shuttle Exhaust Plume" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors32 = colors;
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Samples the shuttle's authored wing motion at its fully deployed in-flight pose.
    /// </summary>
    /// <param name="model">The instantiated shuttle model.</param>
    private static void PoseWingsForFlight(Transform model)
    {
        Animation animation = model.GetComponentInParent<Animation>();
        if (animation == null || animation.clip == null)
            return;

        AnimationState state = animation[animation.clip.name];
        if (state == null)
            return;

        state.enabled = true;
        state.weight = 1f;
        state.time = state.length * 0.5f;
        animation.Sample();
        animation.enabled = false;
    }

    /// <summary>
    /// Computes the combined renderer bounds beneath a specifically named model node.
    /// </summary>
    /// <param name="model">The model hierarchy to search.</param>
    /// <param name="nodeName">The exact authored node name.</param>
    /// <param name="bounds">The combined bounds when the node and renderers exist.</param>
    /// <returns>True when the named node contains at least one renderer.</returns>
    private static bool TryGetNamedRendererBounds(
        Transform model,
        string nodeName,
        out Bounds bounds
    )
    {
        foreach (Transform candidate in model.GetComponentsInChildren<Transform>(true))
        {
            if (!string.Equals(candidate.name, nodeName, StringComparison.Ordinal))
                continue;

            Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                break;

            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return true;
        }

        bounds = default;
        return false;
    }

    /// <summary>
    /// Computes the combined renderer bounds beneath a model root.
    /// </summary>
    /// <param name="model">The model to measure.</param>
    /// <param name="bounds">The combined bounds when renderers exist.</param>
    /// <returns>True when the hierarchy contains at least one renderer.</returns>
    private static bool TryGetBounds(Transform model, out Bounds bounds)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return true;
    }

    /// <summary>
    /// Assigns the dedicated presentation layer to a hierarchy.
    /// </summary>
    /// <param name="model">The hierarchy root.</param>
    /// <param name="layer">The layer to assign.</param>
    private static void SetLayerRecursively(GameObject model, int layer)
    {
        model.layer = layer;
        foreach (Transform child in model.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
