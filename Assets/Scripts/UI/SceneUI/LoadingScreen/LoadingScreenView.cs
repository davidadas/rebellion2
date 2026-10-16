using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Presents an application-owned hyperspace scene while a saved game loads.
/// </summary>
[DisallowMultipleComponent]
public sealed class LoadingScreenView : MonoBehaviour
{
    private enum PresentationState
    {
        Loading,
        Ready,
        Continuing,
        Failed,
        TerminalFailure,
    }

    private const string _loadingText = "LOADING, PLEASE WAIT...";
    private const string _shuttleRelativePath =
        "Application/Loading/Models/lambda-imperial-shuttle.glb";
    private const string _flightAudioRelativePath = "Application/Loading/Audio/shuttle-reactor.wav";
    private const float _flightAudioVolume = 0.22f;
    private const int _presentationLayer = 31;
    private const int _renderTextureWidth = 1280;
    private const int _renderTextureHeight = 720;
    private const float _maximumFlightYaw = 24f;
    private const float _maximumFlightPitch = 15f;
    private const float _maximumFlightRoll = 10f;
    private const float _maximumFlightOffsetX = 1.35f;
    private const float _maximumFlightOffsetY = 0.55f;
    private const float _flightPositionSmoothTime = 0.28f;
    private const float _flightAttitudeSmoothTime = 0.08f;

    [SerializeField]
    private TextMeshProUGUI statusText;

    [SerializeField]
    private TextMeshProUGUI failureDetailText;

    [SerializeField]
    private Button continueButton;

    [SerializeField]
    private TextMeshProUGUI continueLabel;

    [SerializeField]
    private TextMeshProUGUI continueKeyboardHint;

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

    [SerializeField]
    private AudioSource shuttleFlightAudio;

    private CancellationTokenSource cancellation;
    private ContentModelInstance modelInstance;
    private Material shuttleExhaustCoreMaterial;
    private Material shuttleExhaustMaterial;
    private Mesh shuttleExhaustMesh;
    private Transform[] shuttleExhaustPlumes;
    private Vector3[] shuttleExhaustScales;
    private RenderTexture shuttleRenderTexture;
    private Vector2 flightOffset;
    private Vector2 flightOffsetVelocity;
    private Vector2 flightSteering;
    private Vector2 flightSteeringVelocity;
    private Quaternion shuttleCameraOrbitRestRotation;
    private Vector3 shuttleRestPosition;
    private Quaternion shuttleRestRotation;
    private Vector3 shuttleRestScale;
    private bool modelReady;
    private bool presentationVisible;
    private PresentationState presentationState;

    /// <summary>
    /// Raised when the player accepts a fully prepared saved game.
    /// </summary>
    internal event Action ContinueRequested;

    /// <summary>
    /// Verifies the generated presentation, creates its render target, and seeds the starfield.
    /// </summary>
    private void Awake()
    {
        if (
            statusText == null
            || failureDetailText == null
            || continueButton == null
            || continueLabel == null
            || continueKeyboardHint == null
            || shuttleViewport == null
            || starfield == null
            || presentationGroup == null
            || fadeOverlayGroup == null
            || shuttleCameraOrbit == null
            || shuttleCamera == null
            || shuttleModelRoot == null
            || shuttleFlightAudio == null
        )
        {
            throw new MissingReferenceException(
                $"{name} requires status, continue, starfield, transition, viewport, camera-orbit, camera, model-root, and flight-audio references."
            );
        }

        statusText.text = _loadingText;
        failureDetailText.gameObject.SetActive(false);
        continueButton.onClick.AddListener(RequestContinue);
        continueButton.gameObject.SetActive(false);
        shuttleCameraOrbitRestRotation = shuttleCameraOrbit.localRotation;
        presentationVisible = gameObject.activeInHierarchy && presentationGroup.alpha > 0f;
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
        _ = LoadFlightAudioAsync(
            Path.Combine(
                contentRootPath,
                _flightAudioRelativePath.Replace('/', Path.DirectorySeparatorChar)
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
        if (presentationState == PresentationState.Loading)
        {
            int dotCount = 1 + Mathf.FloorToInt(time * 2.25f) % 3;
            statusText.text = $"LOADING, PLEASE WAIT{new string('.', dotCount)}";
            statusText.alpha = Mathf.Lerp(0.72f, 1f, (Mathf.Sin(time * 3.2f) + 1f) * 0.5f);
        }
        if (
            (
                presentationState == PresentationState.Ready
                || presentationState == PresentationState.TerminalFailure
            ) && IsKeyboardContinuePressed()
        )
            RequestContinue();

        Vector2 targetInput = GetPointerFlightInput();
        Vector2 targetOffset = new Vector2(
            targetInput.x * _maximumFlightOffsetX,
            targetInput.y * _maximumFlightOffsetY
        );
        flightOffset = Vector2.SmoothDamp(
            flightOffset,
            targetOffset,
            ref flightOffsetVelocity,
            _flightPositionSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );
        Vector2 remainingFlight = new Vector2(
            targetInput.x - flightOffset.x / _maximumFlightOffsetX,
            targetInput.y - flightOffset.y / _maximumFlightOffsetY
        );
        flightSteering = Vector2.SmoothDamp(
            flightSteering,
            Vector2.ClampMagnitude(remainingFlight, 1f),
            ref flightSteeringVelocity,
            _flightAttitudeSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );
        if (modelReady)
        {
            float thrust = (Mathf.Sin(time * 0.9f) + 1f) * 0.5f;
            shuttleModelRoot.localPosition =
                shuttleRestPosition
                + new Vector3(
                    flightOffset.x + Mathf.Sin(time * 0.37f) * 0.022f,
                    flightOffset.y + Mathf.Sin(time * 0.53f) * 0.035f,
                    Mathf.Sin(time * 0.41f) * 0.018f
                );
            shuttleModelRoot.localRotation = shuttleRestRotation * GetFlightRotation(time);
            shuttleModelRoot.localScale = shuttleRestScale * Mathf.Lerp(0.997f, 1.004f, thrust);
            UpdateEngineExhaust(time);
        }

        KeepCameraCentered();
    }

    /// <summary>
    /// Converts the pointer position into centered horizontal and vertical flight input.
    /// </summary>
    /// <returns>The clamped flight input, or zero while the pointer is unavailable.</returns>
    private static Vector2 GetPointerFlightInput()
    {
        if (!Application.isFocused || Screen.width <= 0 || Screen.height <= 0)
            return Vector2.zero;

        Vector3 pointer = Input.mousePosition;
        if (
            pointer.x < 0f
            || pointer.x > Screen.width
            || pointer.y < 0f
            || pointer.y > Screen.height
        )
            return Vector2.zero;

        return new Vector2(
            Mathf.Clamp(pointer.x / Screen.width * 2f - 1f, -1f, 1f),
            Mathf.Clamp(pointer.y / Screen.height * 2f - 1f, -1f, 1f)
        );
    }

    /// <summary>
    /// Builds the shuttle attitude from cursor steering and subtle in-flight drift.
    /// </summary>
    /// <param name="time">The current unscaled presentation time.</param>
    /// <returns>The local rotation relative to the shuttle's resting orientation.</returns>
    private Quaternion GetFlightRotation(float time)
    {
        return Quaternion.Euler(
            -flightSteering.y * _maximumFlightPitch + Mathf.Sin(time * 0.55f) * 0.8f,
            flightSteering.x * _maximumFlightYaw + Mathf.Sin(time * 0.31f) * 1.35f,
            -flightSteering.x * _maximumFlightRoll + Mathf.Sin(time * 0.42f) * 0.55f
        );
    }

    /// <summary>
    /// Holds the viewpoint behind the shuttle while cursor input moves only the model.
    /// </summary>
    private void KeepCameraCentered()
    {
        shuttleCameraOrbit.localRotation = shuttleCameraOrbitRestRotation;
        starfield.SetViewYaw(0f);
    }

    /// <summary>
    /// Shows or hides the prewarmed presentation without releasing its rendering resources.
    /// </summary>
    /// <param name="visible">Whether the presentation should render.</param>
    public void SetVisible(bool visible)
    {
        if (visible)
        {
            ResetFlightInteraction();
            presentationGroup.alpha = 1f;
            presentationVisible = true;
            StartFlightAudio();
            SetBlackOpacity(0f);
        }
        else
        {
            presentationState = PresentationState.Continuing;
            continueButton.gameObject.SetActive(false);
            presentationVisible = false;
            StopFlightAudio();
        }

        gameObject.SetActive(visible);
    }

    /// <summary>
    /// Activates the transition layer while leaving the outgoing scene visible beneath it.
    /// </summary>
    internal void PrepareEntrance()
    {
        presentationState = PresentationState.Loading;
        statusText.text = _loadingText;
        statusText.alpha = 1f;
        failureDetailText.gameObject.SetActive(false);
        continueLabel.text = "CONTINUE";
        continueKeyboardHint.text = "PRESS ENTER";
        continueButton.interactable = true;
        continueButton.gameObject.SetActive(false);
        ResetFlightInteraction();
        presentationGroup.alpha = 0f;
        presentationVisible = false;
        StopFlightAudio();
        SetBlackOpacity(0f);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Controls whether the loading presentation is visible behind the black transition layer.
    /// </summary>
    /// <param name="visible">Whether the loading presentation should render.</param>
    internal void SetPresentationVisible(bool visible)
    {
        presentationGroup.alpha = visible ? 1f : 0f;
        presentationVisible = visible;
        if (visible)
            StartFlightAudio();
        else
            StopFlightAudio();
    }

    /// <summary>
    /// Sets the opacity of the full-screen black transition layer.
    /// </summary>
    /// <param name="opacity">The desired opacity from transparent to opaque.</param>
    internal void SetBlackOpacity(float opacity)
    {
        float clampedOpacity = Mathf.Clamp01(opacity);
        fadeOverlayGroup.alpha = clampedOpacity;
        fadeOverlayGroup.blocksRaycasts = clampedOpacity > 0f;
    }

    /// <summary>
    /// Replaces the animated loading label with a stable recovery message.
    /// </summary>
    /// <param name="details">A concise explanation of why loading failed.</param>
    internal void ShowLoadFailure(string details = null)
    {
        presentationState = PresentationState.Failed;
        statusText.text = "LOAD FAILED\nRETURNING TO MENU...";
        statusText.alpha = 1f;
        failureDetailText.text = FormatFailureDetail(details);
        failureDetailText.gameObject.SetActive(true);
        continueButton.gameObject.SetActive(false);
        StopFlightAudio();
    }

    /// <summary>
    /// Presents an unrecoverable application failure with an explicit exit action.
    /// </summary>
    /// <param name="details">A concise explanation of why recovery failed.</param>
    internal void ShowTerminalFailure(string details = null)
    {
        presentationState = PresentationState.TerminalFailure;
        statusText.text = "APPLICATION RECOVERY FAILED";
        statusText.alpha = 1f;
        failureDetailText.text = FormatFailureDetail(details);
        failureDetailText.gameObject.SetActive(true);
        continueLabel.text = "QUIT";
        continueKeyboardHint.text = "PRESS ENTER";
        continueButton.onClick.RemoveListener(RequestContinue);
        continueButton.onClick.AddListener(RequestContinue);
        continueButton.interactable = true;
        continueButton.gameObject.SetActive(true);
        SetBlackOpacity(0f);
        StopFlightAudio();
    }

    /// <summary>
    /// Replaces the loading animation with a stable player-controlled handoff.
    /// </summary>
    public void ShowReady()
    {
        presentationState = PresentationState.Ready;
        statusText.text = string.Empty;
        statusText.alpha = 1f;
        failureDetailText.gameObject.SetActive(false);
        continueLabel.text = "CONTINUE";
        continueKeyboardHint.text = "PRESS ENTER";
        continueButton.onClick.RemoveListener(RequestContinue);
        continueButton.onClick.AddListener(RequestContinue);
        continueButton.interactable = true;
        continueButton.gameObject.SetActive(true);
        SetBlackOpacity(0f);
    }

    /// <summary>
    /// Raises the continue request once for the current ready state.
    /// </summary>
    private void RequestContinue()
    {
        if (
            presentationState != PresentationState.Ready
            && presentationState != PresentationState.TerminalFailure
        )
            return;

        presentationState = PresentationState.Continuing;
        continueButton.interactable = false;
        StopFlightAudio();
        ContinueRequested?.Invoke();
    }

    /// <summary>
    /// Returns whether a supported keyboard key requested the ready-state handoff this frame.
    /// </summary>
    /// <returns>True when Enter, keypad Enter, or Space was pressed.</returns>
    private static bool IsKeyboardContinuePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (
                keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame
            );
    }

    /// <summary>
    /// Produces a bounded single-message explanation suitable for the loading presentation.
    /// </summary>
    /// <param name="details">The raw loading error message.</param>
    /// <returns>The normalized user-facing explanation.</returns>
    private static string FormatFailureDetail(string details)
    {
        const int maximumLength = 240;
        string normalized = string.IsNullOrWhiteSpace(details)
            ? "The saved game could not be loaded."
            : details.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : normalized.Substring(0, maximumLength - 3) + "...";
    }

    /// <summary>
    /// Recenters the shuttle attitude and restores the fixed rear viewpoint.
    /// </summary>
    private void ResetFlightInteraction()
    {
        flightOffset = Vector2.zero;
        flightOffsetVelocity = Vector2.zero;
        flightSteering = Vector2.zero;
        flightSteeringVelocity = Vector2.zero;
        KeepCameraCentered();
    }

    /// <summary>
    /// Cancels the model load and releases all runtime rendering resources.
    /// </summary>
    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(RequestContinue);
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;

        modelInstance?.Dispose();
        modelInstance = null;

        StopFlightAudio();
        AudioClip flightAudioClip = shuttleFlightAudio?.clip;
        if (flightAudioClip != null)
        {
            shuttleFlightAudio.clip = null;
            if (Application.isPlaying)
                Destroy(flightAudioClip);
            else
                DestroyImmediate(flightAudioClip);
        }

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
            cancellationToken.ThrowIfCancellationRequested();
            if (shuttleModelRoot == null)
                return;

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
    /// Loads and starts the application-owned shuttle reactor ambience.
    /// </summary>
    /// <param name="filePath">The absolute WAV path.</param>
    /// <param name="cancellationToken">Cancels loading when the presentation closes.</param>
    /// <returns>A task representing the asynchronous audio load.</returns>
    private async Task LoadFlightAudioAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                new Uri(filePath).AbsoluteUri,
                AudioType.WAV
            );
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException(
                    $"Loading-screen reactor audio could not be loaded at '{filePath}': {request.error}"
                );

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
                throw new IOException(
                    $"Loading-screen reactor audio could not be decoded at '{filePath}'."
                );

            cancellationToken.ThrowIfCancellationRequested();
            if (shuttleFlightAudio == null)
            {
                if (Application.isPlaying)
                    Destroy(clip);
                else
                    DestroyImmediate(clip);
                return;
            }

            clip.name = Path.GetFileNameWithoutExtension(filePath);
            shuttleFlightAudio.clip = clip;
            StartFlightAudio();
        }
        catch (OperationCanceledException)
        {
            // The loading page completed before its decorative audio finished loading.
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Loading-screen reactor audio unavailable; continuing silently.\n{exception}"
            );
        }
    }

    /// <summary>
    /// Starts the shuttle reactor while the loading presentation is visible.
    /// </summary>
    private void StartFlightAudio()
    {
        if (
            !presentationVisible
            || presentationState == PresentationState.Continuing
            || presentationState == PresentationState.Failed
            || presentationState == PresentationState.TerminalFailure
            || shuttleFlightAudio.clip == null
            || shuttleFlightAudio.isPlaying
        )
            return;

        AudioManager audioManager = AudioManager.Instance;
        float settingsVolume =
            audioManager == null ? 1f : audioManager.MasterVolume * audioManager.AmbienceVolume;
        shuttleFlightAudio.volume = _flightAudioVolume * settingsVolume;
        shuttleFlightAudio.Play();
    }

    /// <summary>
    /// Stops the shuttle reactor without releasing its reusable clip.
    /// </summary>
    private void StopFlightAudio()
    {
        if (shuttleFlightAudio?.isPlaying == true)
            shuttleFlightAudio.Stop();
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
        if (
            TryGetNamedRendererBounds(
                model,
                "ion-engine_39",
                out Transform engineRoot,
                out Bounds engineBounds
            )
        )
            CreateEngineExhaust(engineRoot, engineBounds);
        else
            Debug.LogWarning("Loading shuttle exhaust skipped because its engine mesh is missing.");
        shuttleRestPosition = shuttleModelRoot.localPosition;
        shuttleRestRotation = shuttleModelRoot.localRotation;
        shuttleRestScale = shuttleModelRoot.localScale;
    }

    /// <summary>
    /// Adds compact blue exhaust plumes directly behind the shuttle's engine banks.
    /// </summary>
    /// <param name="engineRoot">The authored ion-engine transform.</param>
    /// <param name="engineBounds">The ion-engine bounds in engine-root space.</param>
    private void CreateEngineExhaust(Transform engineRoot, Bounds engineBounds)
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

        Vector3 rearCenter = new Vector3(
            engineBounds.center.x,
            engineBounds.center.y,
            engineBounds.min.z
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
                engineRoot,
                position,
                new Vector3(bankWidth, bankHeight, plumeLength),
                shuttleExhaustMaterial
            );
            CreateEnginePlume(
                plumeIndex++,
                "EnginePlumeCore",
                engineRoot,
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
    /// <param name="parent">The authored engine transform.</param>
    /// <param name="localPosition">The plume position in engine-root space.</param>
    /// <param name="localScale">The plume width, height, and length.</param>
    /// <param name="material">The additive plume material.</param>
    private void CreateEnginePlume(
        int index,
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material
    )
    {
        GameObject plumeObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        plumeObject.layer = _presentationLayer;
        plumeObject.transform.SetParent(parent, false);
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
    /// <param name="node">The matching authored node.</param>
    /// <param name="bounds">The combined bounds in matching-node space.</param>
    /// <returns>True when the named node contains at least one renderer.</returns>
    private static bool TryGetNamedRendererBounds(
        Transform model,
        string nodeName,
        out Transform node,
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

            node = candidate;
            bounds = GetRendererBounds(candidate, renderers[0]);
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(GetRendererBounds(candidate, renderers[index]));
            return true;
        }

        node = null;
        bounds = default;
        return false;
    }

    /// <summary>
    /// Converts one renderer's local bounds into an axis-aligned box in an ancestor's space.
    /// </summary>
    /// <param name="ancestor">The space in which to express the result.</param>
    /// <param name="renderer">The renderer to measure.</param>
    /// <returns>The renderer bounds in ancestor space.</returns>
    private static Bounds GetRendererBounds(Transform ancestor, Renderer renderer)
    {
        Bounds localBounds = renderer.localBounds;
        Vector3 minimum = localBounds.min;
        Vector3 maximum = localBounds.max;
        Vector3 firstCorner = ancestor.InverseTransformPoint(
            renderer.transform.TransformPoint(minimum)
        );
        Bounds bounds = new Bounds(firstCorner, Vector3.zero);
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Vector3 corner = new Vector3(
                        x == 0 ? minimum.x : maximum.x,
                        y == 0 ? minimum.y : maximum.y,
                        z == 0 ? minimum.z : maximum.z
                    );
                    bounds.Encapsulate(
                        ancestor.InverseTransformPoint(renderer.transform.TransformPoint(corner))
                    );
                }
            }
        }

        return bounds;
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
