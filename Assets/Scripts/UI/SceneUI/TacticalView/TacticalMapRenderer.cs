using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

/// <summary>
/// Materializes an authored battle map as Unity presentation objects beneath one scene root.
/// </summary>
public sealed class TacticalMapRenderer : MonoBehaviour
{
    private const string _starShaderName = "Rebellion/Unlit Additive";
    private const string _planetSurfaceShaderName = "Custom/PlanetSurface";
    private const string _planetAtmosphereShaderName = "Custom/PlanetAtmosphere";

    private readonly List<Transform> cameraRelativeRoots = new List<Transform>();
    private readonly List<ContentModelInstance> embeddedModelInstances =
        new List<ContentModelInstance>();
    private readonly List<ContentModelResource> embeddedModelResources =
        new List<ContentModelResource>();
    private readonly List<Texture2D> embeddedTextures = new List<Texture2D>();
    private Camera battleCamera;
    private CancellationTokenSource cancellation;
    private Task ready = Task.CompletedTask;
    private Material starMaterial;
    private Texture2D starTexture;

    /// <summary>
    /// Gets the task that completes when all embedded map models have been instantiated.
    /// </summary>
    public Task Ready => ready;

    /// <summary>
    /// Creates the lifetime token used by asynchronous embedded-model loading.
    /// </summary>
    private void Awake()
    {
        cancellation = new CancellationTokenSource();
    }

    /// <summary>
    /// Rebuilds the complete map presentation from authored map data.
    /// </summary>
    /// <param name="battleMap">The map to render.</param>
    /// <param name="camera">The tactical camera that camera-relative scenery follows.</param>
    public void Render(BattleMap battleMap, Camera camera)
    {
        if (battleMap == null)
            throw new ArgumentNullException(nameof(battleMap));
        if (camera == null)
            throw new ArgumentNullException(nameof(camera));

        Clear();
        cancellation = new CancellationTokenSource();
        battleCamera = camera;
        ConfigureEnvironment(battleMap.Environment);
        foreach (BattleMapDirectionalLight light in battleMap.GetDirectionalLights())
            RenderDirectionalLight(light);
        foreach (BattleMapStarfield starfield in battleMap.GetStarfields())
            RenderStarfield(starfield);
        PositionCameraRelativeRoots();
        ready = RenderEmbeddedObjectsAsync(battleMap, cancellation.Token);
    }

    /// <summary>
    /// Keeps camera-relative scenery centered as the tactical camera moves through the map.
    /// </summary>
    private void LateUpdate()
    {
        PositionCameraRelativeRoots();
    }

    /// <summary>
    /// Releases materials and textures owned by this renderer.
    /// </summary>
    private void OnDestroy()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
        DisposeEmbeddedModels();
        DisposeEmbeddedTextures();
        DestroyOwnedAsset(starMaterial);
        DestroyOwnedAsset(starTexture);
        starMaterial = null;
        starTexture = null;
    }

    /// <summary>
    /// Applies scene-wide rendering settings authored by the map.
    /// </summary>
    /// <param name="environment">The map environment.</param>
    private static void ConfigureEnvironment(BattleMapEnvironment environment)
    {
        if (environment == null)
            throw new InvalidOperationException("The battle map has no environment settings.");

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ToUnityColor(environment.AmbientSkyColor);
        RenderSettings.ambientEquatorColor = ToUnityColor(environment.AmbientEquatorColor);
        RenderSettings.ambientGroundColor = ToUnityColor(environment.AmbientGroundColor);
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.fog = false;
        RenderSettings.skybox = null;
        RenderSettings.sun = null;
    }

    /// <summary>
    /// Creates one directional light from authored map data.
    /// </summary>
    /// <param name="definition">The light to create.</param>
    private void RenderDirectionalLight(BattleMapDirectionalLight definition)
    {
        GameObject lightObject = new GameObject(definition.InstanceID, typeof(Light));
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localRotation = Quaternion.Euler(ToUnityVector(definition.Rotation));
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = ToUnityColor(definition.Color);
        light.intensity = definition.Intensity;
        light.shadows = definition.CastShadows ? LightShadows.Soft : LightShadows.None;
        if (definition.IsSun)
            RenderSettings.sun = light;
    }

    /// <summary>
    /// Loads one packaged planet model and creates its cloud and atmospheric presentation.
    /// </summary>
    /// <param name="battleMap">The map that owns the packaged planet assets.</param>
    /// <param name="definition">The planet to create.</param>
    /// <param name="cancellationToken">Cancels loading when the renderer is rebuilt or destroyed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RenderPlanetAsync(
        BattleMap battleMap,
        BattleMapPlanet definition,
        CancellationToken cancellationToken
    )
    {
        Vector3 position = ToUnityVector(definition.Position);
        Quaternion rotation = Quaternion.Euler(ToUnityVector(definition.Rotation));
        Vector3 sunDirection = ToUnityVector(definition.SunDirection).normalized;
        byte[] modelBytes = GetRequiredEmbeddedAsset(battleMap, definition.ModelAssetPath);
        byte[] cloudBytes = GetRequiredEmbeddedAsset(battleMap, definition.CloudTextureAssetPath);
        Texture2D cloudTexture = CreateEmbeddedTexture(
            cloudBytes,
            definition.CloudTextureAssetPath
        );
        embeddedTextures.Add(cloudTexture);

        GameObject planetObject = new GameObject(definition.InstanceID);
        planetObject.transform.SetParent(transform, false);
        planetObject.transform.SetLocalPositionAndRotation(position, rotation);
        ContentModelResource resource = await ContentModelLoader.LoadResourceAsync(
            modelBytes,
            definition.ModelAssetPath,
            cancellationToken
        );
        embeddedModelResources.Add(resource);
        ContentModelInstance instance = await resource.InstantiateAsync(
            planetObject.transform,
            cancellationToken
        );
        embeddedModelInstances.Add(instance);
        instance.ApplyPose(
            planetObject.transform,
            definition.Diameter / 2f,
            Vector3.zero,
            false,
            true,
            true,
            -1
        );
        PlanetSurfaceBinding surfaceBinding = planetObject.AddComponent<PlanetSurfaceBinding>();
        surfaceBinding.Configure(
            _planetSurfaceShaderName,
            cloudTexture,
            definition.CloudRotationSpeed,
            sunDirection
        );

        RenderPlanetAtmosphere(definition, position, rotation, sunDirection);
    }

    /// <summary>
    /// Creates the atmospheric-scattering shell around one loaded planet.
    /// </summary>
    /// <param name="definition">The planet whose atmosphere is created.</param>
    /// <param name="position">The planet position.</param>
    /// <param name="rotation">The planet rotation.</param>
    /// <param name="sunDirection">The normalized world-space direction toward the sun.</param>
    private void RenderPlanetAtmosphere(
        BattleMapPlanet definition,
        Vector3 position,
        Quaternion rotation,
        Vector3 sunDirection
    )
    {
        GameObject atmosphereObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        atmosphereObject.name = definition.InstanceID + " Atmosphere";
        atmosphereObject.transform.SetParent(transform, false);
        atmosphereObject.transform.SetLocalPositionAndRotation(position, rotation);
        atmosphereObject.transform.localScale =
            Vector3.one * definition.Diameter * definition.AtmosphereRadiusRatio;
        Collider atmosphereCollider = atmosphereObject.GetComponent<Collider>();
        if (atmosphereCollider != null)
            DestroySceneObject(atmosphereCollider);
        PlanetAtmosphereBinding atmosphereBinding =
            atmosphereObject.AddComponent<PlanetAtmosphereBinding>();
        float planetRadius = definition.Diameter / 2f;
        atmosphereBinding.Configure(
            _planetAtmosphereShaderName,
            planetRadius,
            planetRadius * definition.AtmosphereRadiusRatio,
            sunDirection
        );
    }

    /// <summary>
    /// Loads every packaged model and texture required by authored map objects.
    /// </summary>
    /// <param name="battleMap">The map containing object definitions and packaged assets.</param>
    /// <param name="cancellationToken">Cancels loading when the renderer is rebuilt or destroyed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RenderEmbeddedObjectsAsync(
        BattleMap battleMap,
        CancellationToken cancellationToken
    )
    {
        foreach (BattleMapPlanet planet in battleMap.GetPlanets())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RenderPlanetAsync(battleMap, planet, cancellationToken);
        }

        await RenderDebrisFieldsAsync(battleMap, cancellationToken);
    }

    /// <summary>
    /// Creates one deterministic camera-relative particle starfield.
    /// </summary>
    /// <param name="definition">The starfield to create.</param>
    private void RenderStarfield(BattleMapStarfield definition)
    {
        GameObject starsObject = new GameObject(definition.InstanceID);
        starsObject.transform.SetParent(transform, false);
        cameraRelativeRoots.Add(starsObject.transform);

        ParticleSystem stars = starsObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = stars.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = definition.Count;
        main.startLifetime = 100000f;
        ParticleSystem.EmissionModule emission = stars.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = stars.shape;
        shape.enabled = false;
        stars.Play(true);
        stars.Pause(true);

        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[definition.Count];
        Random.State previousRandomState = Random.state;
        try
        {
            Random.InitState(definition.Seed);
            for (int index = 0; index < particles.Length; index++)
            {
                Vector3 direction = Random.onUnitSphere;
                float radius = Mathf.Lerp(
                    definition.InnerRadius,
                    definition.OuterRadius,
                    Random.value
                );
                float brightness = Mathf.Lerp(
                    definition.MinimumBrightness,
                    definition.MaximumBrightness,
                    Random.value
                );
                BattleMapColor authoredColor =
                    Random.value < definition.SecondaryColorProbability
                        ? definition.SecondaryColor
                        : definition.PrimaryColor;
                Color color = ToUnityColor(authoredColor);
                color.a *= brightness;
                particles[index] = new ParticleSystem.Particle
                {
                    position = direction * radius,
                    startColor = color,
                    startSize = Mathf.Lerp(
                        definition.MinimumSize,
                        definition.MaximumSize,
                        Mathf.Pow(Random.value, 4f)
                    ),
                    startLifetime = 100000f,
                    remainingLifetime = 100000f,
                };
            }
        }
        finally
        {
            Random.state = previousRandomState;
        }

        stars.SetParticles(particles, particles.Length);
        ParticleSystemRenderer renderer = stars.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.sharedMaterial = GetStarMaterial();
    }

    /// <summary>
    /// Loads and instantiates every deterministic embedded debris field in the map.
    /// </summary>
    /// <param name="battleMap">The map containing field definitions and packaged models.</param>
    /// <param name="cancellationToken">Cancels loading when the renderer is rebuilt or destroyed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RenderDebrisFieldsAsync(
        BattleMap battleMap,
        CancellationToken cancellationToken
    )
    {
        foreach (BattleMapDebrisField debrisField in battleMap.GetDebrisFields())
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] modelBytes = GetRequiredEmbeddedAsset(battleMap, debrisField.ModelAssetPath);

            ContentModelResource resource = await ContentModelLoader.LoadResourceAsync(
                modelBytes,
                debrisField.ModelAssetPath,
                cancellationToken
            );
            embeddedModelResources.Add(resource);
            GameObject fieldObject = new GameObject(debrisField.InstanceID);
            fieldObject.transform.SetParent(transform, false);
            IReadOnlyList<(Vector3 Position, Quaternion Rotation, float Scale)> placements =
                CreateDebrisPlacements(debrisField);
            for (int index = 0; index < placements.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (Vector3 position, Quaternion rotation, float scale) = placements[index];
                GameObject debrisObject = new GameObject($"Debris {index + 1:000}");
                debrisObject.transform.SetParent(fieldObject.transform, false);
                debrisObject.transform.SetLocalPositionAndRotation(position, rotation);
                debrisObject.transform.localScale = Vector3.one * scale;
                ContentModelInstance instance = await resource.InstantiateAsync(
                    debrisObject.transform,
                    cancellationToken
                );
                embeddedModelInstances.Add(instance);
            }
        }
    }

    /// <summary>
    /// Reproduces the authored cell distribution and retains only debris visible within its fade range.
    /// </summary>
    /// <param name="definition">The debris field definition.</param>
    /// <returns>The deterministic visible debris placements.</returns>
    private static IReadOnlyList<(
        Vector3 Position,
        Quaternion Rotation,
        float Scale
    )> CreateDebrisPlacements(BattleMapDebrisField definition)
    {
        List<(Vector3 Position, Quaternion Rotation, float Scale)> placements =
            new List<(Vector3 Position, Quaternion Rotation, float Scale)>();
        long cellSize = (long)Math.Ceiling(definition.HideDistance / definition.CellCount);
        float probability = definition.DebrisCountTarget / (cellSize * cellSize * cellSize);
        float cellMinimum = cellSize * (0.5f - definition.CellNoise);
        float cellMaximum = cellSize * (0.5f + definition.CellNoise);
        Random.State previousRandomState = Random.state;
        try
        {
            for (int z = -definition.CellCount; z <= definition.CellCount; z++)
            for (int y = -definition.CellCount; y <= definition.CellCount; y++)
            for (int x = -definition.CellCount; x <= definition.CellCount; x++)
            {
                Random.InitState(GetDebrisCellSeed(definition.Seed, x, y, z));
                if (Random.value >= probability)
                    continue;

                Vector3 position = new Vector3(
                    x * cellSize + Random.Range(cellMinimum, cellMaximum),
                    y * cellSize + Random.Range(cellMinimum, cellMaximum),
                    z * cellSize + Random.Range(cellMinimum, cellMaximum)
                );
                _ = Random.Range(0, 1);
                Quaternion rotation = definition.RandomRotation
                    ? Random.rotation
                    : Quaternion.identity;
                float scale = Mathf.Lerp(
                    definition.MinimumScale,
                    definition.MaximumScale,
                    ApplyScaleBias(Random.value, definition.ScaleBias)
                );
                float visibility = GetDebrisVisibility(position.magnitude, definition);
                if (visibility <= 0f)
                    continue;
                placements.Add((position, rotation, scale * visibility));
            }
        }
        finally
        {
            Random.state = previousRandomState;
        }

        return placements;
    }

    /// <summary>
    /// Hashes one debris-grid cell into the deterministic random seed used for that cell.
    /// </summary>
    /// <param name="seed">The authored field seed.</param>
    /// <param name="x">The cell X coordinate.</param>
    /// <param name="y">The cell Y coordinate.</param>
    /// <param name="z">The cell Z coordinate.</param>
    /// <returns>The cell-specific random seed.</returns>
    private static int GetDebrisCellSeed(int seed, int x, int y, int z)
    {
        const int multiplier = 1103515245;
        const int increment = 12345;
        seed = unchecked((int)(multiplier * (seed + (long)x) + increment)) % int.MaxValue;
        seed = unchecked((int)(multiplier * (seed + (long)y) + increment)) % int.MaxValue;
        return unchecked((int)(multiplier * (seed + (long)z) + increment)) % int.MaxValue;
    }

    /// <summary>
    /// Applies the authored bias that controls the debris scale distribution.
    /// </summary>
    /// <param name="value">The uniformly distributed source value.</param>
    /// <param name="bias">The authored scale bias.</param>
    /// <returns>The biased scale interpolation value.</returns>
    private static float ApplyScaleBias(float value, float bias)
    {
        return bias >= 0f ? Mathf.Pow(value, bias) : 1f - Mathf.Pow(1f - value, -bias);
    }

    /// <summary>
    /// Calculates the scale fade applied at one distance from the field center.
    /// </summary>
    /// <param name="distance">The distance from the field center.</param>
    /// <param name="definition">The debris field definition.</param>
    /// <returns>The visible scale multiplier, or zero outside the hide distance.</returns>
    private static float GetDebrisVisibility(float distance, BattleMapDebrisField definition)
    {
        if (distance >= definition.HideDistance)
            return 0f;
        if (distance <= definition.ShowDistance)
            return 1f;
        return Mathf.Max(
            Mathf.InverseLerp(definition.HideDistance, definition.ShowDistance, distance),
            0.001f
        );
    }

    /// <summary>
    /// Removes presentation objects and transient assets from a previous render.
    /// </summary>
    private void Clear()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
        DisposeEmbeddedModels();
        DisposeEmbeddedTextures();
        cameraRelativeRoots.Clear();
        for (int childIndex = transform.childCount - 1; childIndex >= 0; childIndex--)
            DestroySceneObject(transform.GetChild(childIndex).gameObject);
        DestroyOwnedAsset(starMaterial);
        DestroyOwnedAsset(starTexture);
        starMaterial = null;
        starTexture = null;
        ready = Task.CompletedTask;
    }

    /// <summary>
    /// Releases instantiated embedded models before their shared parsed resources.
    /// </summary>
    private void DisposeEmbeddedModels()
    {
        foreach (ContentModelInstance instance in embeddedModelInstances)
            instance.Dispose();
        embeddedModelInstances.Clear();
        foreach (ContentModelResource resource in embeddedModelResources)
            resource.Dispose();
        embeddedModelResources.Clear();
    }

    /// <summary>
    /// Releases textures decoded from the active map package.
    /// </summary>
    private void DisposeEmbeddedTextures()
    {
        foreach (Texture2D texture in embeddedTextures)
            DestroyOwnedAsset(texture);
        embeddedTextures.Clear();
    }

    /// <summary>
    /// Retrieves a required asset from the active map package.
    /// </summary>
    /// <param name="battleMap">The map that owns the packaged assets.</param>
    /// <param name="assetPath">The asset path relative to the package assets directory.</param>
    /// <returns>The packaged asset bytes.</returns>
    private static byte[] GetRequiredEmbeddedAsset(BattleMap battleMap, string assetPath)
    {
        if (battleMap.TryGetEmbeddedAsset(assetPath, out byte[] bytes))
            return bytes;
        throw new InvalidOperationException($"Battle-map asset is missing: {assetPath}");
    }

    /// <summary>
    /// Decodes one packaged image into a runtime-owned texture.
    /// </summary>
    /// <param name="bytes">The complete encoded image payload.</param>
    /// <param name="assetPath">The diagnostic package-relative asset path.</param>
    /// <returns>The decoded texture.</returns>
    private static Texture2D CreateEmbeddedTexture(byte[] bytes, string assetPath)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = assetPath,
            hideFlags = HideFlags.DontSave,
        };
        if (ImageConversion.LoadImage(texture, bytes, true))
            return texture;

        DestroyOwnedAsset(texture);
        throw new InvalidOperationException($"Battle-map image is invalid: {assetPath}");
    }

    /// <summary>
    /// Places all camera-relative objects at the current tactical camera position.
    /// </summary>
    private void PositionCameraRelativeRoots()
    {
        if (battleCamera == null)
            return;
        foreach (Transform cameraRelativeRoot in cameraRelativeRoots)
        {
            if (cameraRelativeRoot != null)
                cameraRelativeRoot.position = battleCamera.transform.position;
        }
    }

    /// <summary>
    /// Creates or returns the additive material shared by map starfields.
    /// </summary>
    /// <returns>The starfield material.</returns>
    private Material GetStarMaterial()
    {
        if (starMaterial != null)
            return starMaterial;

        Shader shader = Shader.Find(_starShaderName);
        if (shader == null)
            throw new InvalidOperationException($"Shader not found: {_starShaderName}");
        starTexture = CreateRadialGlowTexture();
        starMaterial = new Material(shader)
        {
            name = "Tactical Stars",
            mainTexture = starTexture,
            hideFlags = HideFlags.DontSave,
        };
        return starMaterial;
    }

    /// <summary>
    /// Creates the soft radial sprite used by every star particle.
    /// </summary>
    /// <returns>The generated glow texture.</returns>
    private static Texture2D CreateRadialGlowTexture()
    {
        const int textureSize = 32;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "Procedural Radial Glow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        Color[] pixels = new Color[textureSize * textureSize];
        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / center.x;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.4f);
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    /// <summary>
    /// Converts a data-only battle-map vector to a Unity vector.
    /// </summary>
    /// <param name="value">The authored vector.</param>
    /// <returns>The corresponding Unity vector.</returns>
    private static Vector3 ToUnityVector(BattleMapVector3 value)
    {
        return value == null ? Vector3.zero : new Vector3(value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Converts a data-only battle-map color to a Unity color.
    /// </summary>
    /// <param name="value">The authored color.</param>
    /// <returns>The corresponding Unity color.</returns>
    private static Color ToUnityColor(BattleMapColor value)
    {
        return value == null
            ? Color.black
            : new Color(value.Red, value.Green, value.Blue, value.Alpha);
    }

    /// <summary>
    /// Destroys one generated scene object using the active Unity lifetime mode.
    /// </summary>
    /// <param name="sceneObject">The generated object or component.</param>
    private static void DestroySceneObject(UnityEngine.Object sceneObject)
    {
        if (sceneObject == null)
            return;
        if (Application.isPlaying)
            Destroy(sceneObject);
        else
            DestroyImmediate(sceneObject);
    }

    /// <summary>
    /// Destroys one transient asset using the active Unity lifetime mode.
    /// </summary>
    /// <param name="asset">The transient material or texture.</param>
    private static void DestroyOwnedAsset(UnityEngine.Object asset)
    {
        DestroySceneObject(asset);
    }
}
