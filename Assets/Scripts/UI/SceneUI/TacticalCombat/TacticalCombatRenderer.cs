using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Gameplay;
using UnityEngine;
using Vector3N = System.Numerics.Vector3;

/// <summary>
/// Owns tactical model instances, fighter billboards, and the isolated battle camera.
/// </summary>
public sealed class TacticalCombatRenderer : MonoBehaviour
{
    [SerializeField]
    private Camera _battleCamera;

    [SerializeField]
    private Transform _modelRoot;

    [SerializeField]
    private Shader _fighterShader;
    private readonly List<Visual> _visuals = new();
    private readonly TacticalCombatMeshPicker _picker = new();
    private readonly List<ContentModelInstance> _instances = new();
    private readonly Dictionary<Texture2D, Material> _fighterMaterials = new();
    private RenderTexture _renderTarget;
    private Mesh _billboardMesh;
    private Transform _planet;
    private TacticalCombatEffects _effects;
    private TacticalCombatImpactEffects _impacts;
    private TacticalCombatNavigationRenderer _navigation;
    private readonly Dictionary<Material, Material> _environmentMaterials = new();
    private readonly Dictionary<string, Texture2D> _environmentTextures = new(
        StringComparer.Ordinal
    );
    private readonly System.Random _sceneryRandom = new();
    private float _field = 0.2f;
    private Vector3 _focus;
    private float _distance;
    private float _yaw;
    private float _pitch = 30;
    public Camera Camera => _battleCamera;
    public RenderTexture Target => _renderTarget;

    /// <summary>
    /// Loads required content into an isolated scene without advancing battle time.
    /// </summary>
    /// <param name="session">The deployed encounter.</param>
    /// <param name="models">The shared parsed model cache.</param>
    /// <param name="assets">The active pack's addressed textures.</param>
    /// <param name="cancellationToken">The owning screen's cancellation token.</param>
    /// <returns>The asynchronous presentation preparation.</returns>
    public async Task PrepareAsync(
        TacticalCombatSession session,
        ContentModelCache models,
        ContentAssets assets,
        CancellationToken cancellationToken
    )
    {
        if (_battleCamera == null || _modelRoot == null || _fighterShader == null)
            throw new MissingReferenceException($"{name} is missing battle rendering references.");
        _distance = session.BattleDepth * 1.7f;
        _effects = new TacticalCombatEffects(_modelRoot, _fighterShader, session.BattleDepth);
        _impacts = new TacticalCombatImpactEffects(
            _modelRoot,
            _battleCamera,
            _fighterShader,
            session.Definition.Effects,
            assets
        );
        _navigation = new TacticalCombatNavigationRenderer(
            _modelRoot,
            _fighterShader,
            session.Navigation
        );
        _field = 0.2f;
        UpdateFieldOfView();
        PrepareEnvironment(session, assets);
        _battleCamera.farClipPlane = _distance * 2.5f;
        _battleCamera.nearClipPlane = 1;
        _yaw = session.GetSide(session.PlayerSide).FactionIndex == 0 ? -30 : 150;
        _focus = _modelRoot.position;
        foreach (TacticalCombatUnit unit in session.Units)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameObject root = new GameObject(unit.InstanceID);
            root.transform.SetParent(_modelRoot, false);
            Visual visual = new Visual(unit, root);
            _visuals.Add(visual);
            if (unit.IsFighter)
                CreateFighter(visual, assets, session);
            else if (unit.IsBattleStation)
                CreateStation(visual, assets, session.BattleDepth);
            else
            {
                if (string.IsNullOrWhiteSpace(unit.Definition?.ModelPath))
                    throw new InvalidOperationException(
                        $"The tactical model is missing for '{unit.TypeID}'."
                    );
                IReadOnlyList<string> paths =
                    unit.Definition.ModelDetailPaths.Count > 0
                        ? unit.Definition.ModelDetailPaths
                        : new[] { unit.Definition.ModelPath };
                visual.ModelDetails = new Transform[paths.Count];
                for (int index = 0; index < paths.Count; index++)
                {
                    GameObject detail = new GameObject("Detail " + index);
                    detail.transform.SetParent(root.transform, false);
                    ContentModelInstance instance = await models.InstantiateAsync(
                        paths[index],
                        detail.transform,
                        cancellationToken
                    );
                    _instances.Add(instance);
                    ApplyEnvironmentTextures(instance.ModelRoot);
                    instance.ModelRoot.localScale = Vector3.one * unit.Definition.ModelScale;
                    visual.ModelDetails[index] = detail.transform;
                }
            }
            SetLayer(root.transform, PresentationLayers.TacticalCombat);
            _picker.Register(root, unit);
        }
        UpdateCamera();
        RenderUnits();
    }

    /// <summary>
    /// Displays a discharge using the firing faction's authored beam color.
    /// </summary>
    /// <param name="shot">The applied discharge.</param>
    /// <param name="factionIndex">The firing faction's color index.</param>
    public void ShowShot(TacticalCombatShot shot, int factionIndex) =>
        _effects.ShowShot(shot, factionIndex);

    /// <summary>
    /// Advances visual effect timers independently of campaign and tactical simulation ticks.
    /// </summary>
    /// <param name="elapsedSeconds">The unscaled frame interval.</param>
    public void AdvanceEffects(float elapsedSeconds)
    {
        _effects.Advance(elapsedSeconds);
        _impacts.Advance(elapsedSeconds);
    }

    /// <summary>
    /// Displays the animation associated with one applied weapon impact.
    /// </summary>
    /// <param name="shot">The applied discharge.</param>
    /// <param name="impact">The affected surface or destruction transition.</param>
    public void ShowImpact(TacticalCombatShot shot, TacticalCombatImpactKind impact) =>
        _impacts.ShowImpact(shot, impact);

    /// <summary>
    /// Begins the capital model's authored destruction sequence.
    /// </summary>
    /// <param name="unit">The destroyed encounter participant.</param>
    public void ShowDestruction(TacticalCombatUnit unit) => _impacts.ShowDestruction(unit);

    /// <summary>
    /// Fires the station's superlaser from its rendered emitter position.
    /// </summary>
    /// <param name="station">The firing station.</param>
    /// <param name="target">The selected capital ship.</param>
    /// <param name="battleDepth">The battle's deployment span.</param>
    public void ShowSuperlaser(
        TacticalCombatUnit station,
        TacticalCombatUnit target,
        float battleDepth
    )
    {
        foreach (Visual visual in _visuals)
            if (ReferenceEquals(visual.Unit, station))
            {
                _effects.ShowSuperlaser(
                    (visual.StationPosition ?? ToUnity(station.Position))
                        + new Vector3(0, battleDepth * 0.11f, battleDepth * -0.09f),
                    target
                );
                return;
            }
    }

    /// <summary>
    /// Loads the selected planet's authored scenery and palette-dependent texture substitutions.
    /// </summary>
    /// <param name="session">The captured encounter environment.</param>
    /// <param name="assets">The addressed content source.</param>
    private void PrepareEnvironment(TacticalCombatSession session, ContentAssets assets)
    {
        Rebellion.Game.SpaceCombat.TacticalCombatEnvironmentDefinition environment =
            session.Environment;
        if (environment == null)
            return;
        foreach (
            Rebellion.Game.SpaceCombat.TacticalCombatTextureOverride replacement in environment.TextureOverrides
        )
            _environmentTextures.Add(
                replacement.TextureName,
                assets.GetTexture(replacement.ImagePath)
                    ?? throw new InvalidOperationException(
                        "A tactical environment texture is missing: " + replacement.ImagePath
                    )
            );
        if (string.IsNullOrWhiteSpace(environment.PlanetImagePath))
            return;
        Texture2D texture =
            assets.GetTexture(environment.PlanetImagePath)
            ?? throw new InvalidOperationException(
                "The tactical planet image is missing: " + environment.PlanetImagePath
            );
        GameObject planet = CreateBillboard();
        planet.name = "Planet";
        planet.transform.SetParent(_modelRoot, false);
        planet.transform.localPosition = GetSceneryPosition(session.BattleDepth, 2, 0.1f);
        planet.transform.localScale = Vector3.one * session.BattleDepth;
        Material material = new Material(_fighterShader) { mainTexture = texture };
        _fighterMaterials.Add(texture, material);
        planet.GetComponent<MeshRenderer>().sharedMaterial = material;
        SetLayer(planet.transform, PresentationLayers.TacticalCombat);
        _planet = planet.transform;
    }

    /// <summary>
    /// Creates renderer-owned material variants while preserving the shared model cache's materials.
    /// </summary>
    /// <param name="model">The instantiated model hierarchy.</param>
    private void ApplyEnvironmentTextures(Transform model)
    {
        foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int index = 0; index < materials.Length; index++)
            {
                Material original = materials[index];
                if (
                    original == null
                    || original.mainTexture == null
                    || !_environmentTextures.TryGetValue(
                        original.mainTexture.name,
                        out Texture2D texture
                    )
                )
                    continue;
                if (!_environmentMaterials.TryGetValue(original, out Material replacement))
                {
                    replacement = new Material(original) { mainTexture = texture };
                    _environmentMaterials.Add(original, replacement);
                }
                materials[index] = replacement;
                changed = true;
            }
            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    /// <summary>
    /// Places background scenery independently of the campaign's simulation random stream.
    /// </summary>
    /// <param name="depth">The battle's deployment span.</param>
    /// <param name="horizontal">The horizontal position in multiples of the span.</param>
    /// <param name="spread">The vertical and depth spread in multiples of the span.</param>
    /// <returns>The encounter-local scenery position.</returns>
    private Vector3 GetSceneryPosition(float depth, float horizontal, float spread)
    {
        float range = depth * spread;
        int width = (int)(range * 2);
        return new Vector3(
            depth * horizontal,
            _sceneryRandom.Next(Math.Max(width, 1)) - range,
            _sceneryRandom.Next(Math.Max(width, 1)) - range
        );
    }

    /// <summary>
    /// Converts the viewport's near-plane half extent into the camera's vertical angle.
    /// </summary>
    private void UpdateFieldOfView()
    {
        float vertical = _field / Mathf.Max(_battleCamera.aspect, 1);
        _battleCamera.fieldOfView = 2 * Mathf.Atan(vertical) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Creates one billboard with the pack-authored fighter sprite.
    /// </summary>
    /// <param name="visual">The fighter's owned visual.</param>
    /// <param name="assets">The addressed texture source.</param>
    /// <param name="session">The encounter used to select friendly group colors.</param>
    private void CreateFighter(Visual visual, ContentAssets assets, TacticalCombatSession session)
    {
        Rebellion.Game.SpaceCombat.TacticalCombatFighterSprites sprites =
            visual.Unit.Definition?.FighterSprites
            ?? throw new InvalidOperationException(
                $"Fighter sprites are missing for '{visual.Unit.TypeID}'."
            );
        int color =
            visual.Unit.Side == session.PlayerSide ? visual.Unit.Definition.FighterGroup : 4;
        visual.FighterTextures = new Texture2D[3];
        visual.FighterTextures[0] = assets.GetTexture(sprites.NearImages[color]);
        visual.FighterTextures[1] = assets.GetTexture(sprites.MiddleImages[color]);
        visual.FighterTextures[2] = assets.GetTexture(sprites.FarImages[color]);
        foreach (Texture2D texture in visual.FighterTextures)
            if (texture == null)
                throw new InvalidOperationException(
                    $"A fighter sprite is missing for '{visual.Unit.TypeID}'."
                );
        GameObject billboard = CreateBillboard();
        billboard.name = "Fighter";
        billboard.transform.SetParent(visual.Root.transform, false);
        billboard.transform.localScale = Vector3.one * 0.3f;
        visual.Billboard = billboard.transform;
        visual.FighterRenderer = billboard.GetComponent<MeshRenderer>();
        visual.FighterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        visual.FighterRenderer.receiveShadows = false;
    }

    /// <summary>
    /// Creates the station's complete or construction billboard using its shared tactical definition.
    /// </summary>
    /// <param name="visual">The owned station visual.</param>
    /// <param name="assets">The addressed texture source.</param>
    /// <param name="battleDepth">The encounter's deployment span.</param>
    private void CreateStation(Visual visual, ContentAssets assets, float battleDepth)
    {
        string path =
            visual.Unit.Status == TacticalCombatUnitStatus.UnderConstruction
                ? visual.Unit.Definition.ConstructionBillboardImagePath
                : visual.Unit.Definition.BillboardImagePath;
        Texture2D texture =
            assets.GetTexture(path)
            ?? throw new InvalidOperationException(
                $"The station image is missing for '{visual.Unit.TypeID}'."
            );
        Material material = new Material(_fighterShader) { mainTexture = texture };
        _fighterMaterials.Add(texture, material);
        GameObject billboard = CreateBillboard();
        billboard.transform.SetParent(visual.Root.transform, false);
        billboard.transform.localScale = Vector3.one * (battleDepth * 0.5f);
        billboard.GetComponent<MeshRenderer>().sharedMaterial = material;
        visual.Billboard = billboard.transform;
        visual.StationPosition = GetSceneryPosition(battleDepth, -2, 0.2f);
    }

    /// <summary>
    /// Creates a render-only billboard from the screen's shared quad geometry.
    /// </summary>
    /// <returns>A billboard with a mesh filter and renderer.</returns>
    private GameObject CreateBillboard()
    {
        if (_billboardMesh == null)
        {
            _billboardMesh = new Mesh
            {
                name = "Tactical billboard",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3(0.5f, -0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0),
                    new Vector3(0.5f, 0.5f, 0),
                },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
            };
            _billboardMesh.RecalculateBounds();
        }
        GameObject billboard = new GameObject(
            "Billboard",
            typeof(MeshFilter),
            typeof(MeshRenderer)
        );
        billboard.GetComponent<MeshFilter>().sharedMesh = _billboardMesh;
        return billboard;
    }

    /// <summary>
    /// Resizes the camera target only when the viewport's pixel size changes.
    /// </summary>
    /// <param name="width">The viewport width in pixels.</param>
    /// <param name="height">The viewport height in pixels.</param>
    public void Resize(int width, int height)
    {
        width = Mathf.Max(width, 1);
        height = Mathf.Max(height, 1);
        if (_renderTarget != null && _renderTarget.width == width && _renderTarget.height == height)
            return;
        ReleaseTarget();
        _renderTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "Tactical combat",
            antiAliasing = 1,
        };
        _renderTarget.Create();
        _battleCamera.targetTexture = _renderTarget;
        _battleCamera.aspect = (float)width / height;
        UpdateFieldOfView();
    }

    /// <summary>
    /// Applies current battle positions and presentation visibility.
    /// </summary>
    public void RenderUnits()
    {
        if (_planet != null)
            _planet.rotation = _battleCamera.transform.rotation;
        foreach (Visual visual in _visuals)
        {
            TacticalCombatUnit unit = visual.Unit;
            bool visible =
                unit.Systems.IsAlive
                    && unit.Status
                        is TacticalCombatUnitStatus.Active
                            or TacticalCombatUnitStatus.PreparingHyperspace
                            or TacticalCombatUnitStatus.EnteringHyperspace
                            or TacticalCombatUnitStatus.UnderConstruction
                || _impacts.KeepsModelVisible(unit);
            visual.Root.SetActive(visible);
            if (!visible)
                continue;
            visual.Root.transform.localPosition = visual.StationPosition ?? ToUnity(unit.Position);
            Vector3 heading = ToUnity(unit.Heading);
            if (heading.sqrMagnitude > 0)
                visual.Root.transform.localRotation = Quaternion.LookRotation(-heading, Vector3.up);
            if (visual.Billboard != null)
            {
                visual.Billboard.rotation = _battleCamera.transform.rotation;
                if (unit.IsFighter)
                    UpdateFighter(visual);
            }
            else if (visual.ModelDetails != null)
                UpdateModelDetail(visual);
        }
    }

    /// <summary>
    /// Selects capital geometry from the camera-scaled depth thresholds while retaining its current level on exact boundaries.
    /// </summary>
    /// <param name="visual">The active capital visual.</param>
    private void UpdateModelDetail(Visual visual)
    {
        float metric =
            Mathf.Abs(
                _battleCamera.transform.InverseTransformPoint(visual.Root.transform.position).z
            ) * _field;
        int selected =
            metric > 40 ? 2
            : metric > 15 && metric < 40 ? 1
            : metric < 15 ? 0
            : visual.DetailLevel;
        visual.DetailLevel = Mathf.Min(selected, visual.ModelDetails.Length - 1);
        for (int index = 0; index < visual.ModelDetails.Length; index++)
            visual.ModelDetails[index].gameObject.SetActive(index == visual.DetailLevel);
    }

    /// <summary>
    /// Selects the fighter's depth-dependent sprite and faces its billboard toward the camera.
    /// </summary>
    /// <param name="visual">The active fighter visual.</param>
    private void UpdateFighter(Visual visual)
    {
        visual.Billboard.rotation = _battleCamera.transform.rotation;
        float depth = Mathf.Abs(
            _battleCamera.transform.InverseTransformPoint(visual.Root.transform.position).z
        );
        float metric = Mathf.Tan(_field * 0.5f) * depth;
        int lod =
            metric < 5 ? 0
            : metric < 10 ? 1
            : 2;
        Texture2D texture = visual.FighterTextures[lod];
        if (!_fighterMaterials.TryGetValue(texture, out Material material))
        {
            material = new Material(_fighterShader) { mainTexture = texture };
            _fighterMaterials.Add(texture, material);
        }
        visual.FighterRenderer.sharedMaterial = material;
    }

    /// <summary>
    /// Toggles one of the encounter's four navigation rings.
    /// </summary>
    /// <param name="ring">The zero-based navigation ring.</param>
    public void ToggleNavigationRing(int ring) => _navigation.ToggleRing(ring);

    /// <summary>
    /// Projects requested routes for the current player selection.
    /// </summary>
    /// <param name="groups">The selected groups in encounter order.</param>
    public void UpdateNavigationSelection(IReadOnlyList<TacticalCombatGroup> groups) =>
        _navigation.UpdateSelection(groups);

    /// <summary>
    /// Resolves the nearest visible unit or navigation marker under the pointer.
    /// </summary>
    /// <param name="point">The normalized viewport point.</param>
    /// <param name="unit">Receives the selected participant, or null for a navigation marker.</param>
    /// <param name="waypoint">Receives the selected navigation position.</param>
    /// <returns>Whether a visible selectable object was intersected.</returns>
    public bool TryPick(Vector2 point, out TacticalCombatUnit unit, out Vector3N waypoint)
    {
        unit = null;
        waypoint = default;
        if (point.x <= 0 || point.y <= 0 || point.x >= 1 || point.y >= 1)
            return false;
        Ray ray = _battleCamera.ViewportPointToRay(point);
        float distance = _battleCamera.farClipPlane;
        unit = _picker.Pick(ray, ref distance);
        if (_navigation.TryPick(ray, distance, out waypoint))
        {
            unit = null;
            return true;
        }
        return unit != null;
    }

    /// <summary>
    /// Orbits around the current focus while keeping the camera outside its clipping limits.
    /// </summary>
    /// <param name="delta">The pointer displacement in screen pixels.</param>
    public void Orbit(Vector2 delta)
    {
        _yaw += delta.x * 0.25f;
        _pitch = Mathf.Clamp(_pitch - delta.y * 0.25f, -85, 85);
        UpdateCamera();
    }

    /// <summary>
    /// Pans the camera focus in the current screen plane.
    /// </summary>
    /// <param name="delta">The pointer displacement in screen pixels.</param>
    public void Pan(Vector2 delta)
    {
        _focus -=
            (_battleCamera.transform.right * delta.x + _battleCamera.transform.up * delta.y)
            * (_distance / Mathf.Max(_battleCamera.pixelHeight, 1));
        UpdateCamera();
    }

    /// <summary>
    /// Changes the viewport field without affecting tactical movement.
    /// </summary>
    /// <param name="steps">The signed wheel displacement.</param>
    public void Zoom(float steps)
    {
        float factor = steps >= 0 ? Mathf.Pow(0.9f, steps) : Mathf.Pow(1.1f, -steps);
        _field = Mathf.Clamp(_field * factor, 0.005f, 1.5f);
        UpdateFieldOfView();
        UpdateCamera();
    }

    /// <summary>
    /// Centers the view on a selected battle position.
    /// </summary>
    /// <param name="position">The encounter-local focus position.</param>
    public void Focus(Vector3N position)
    {
        _focus = _modelRoot.TransformPoint(ToUnity(position));
        UpdateCamera();
    }

    /// <summary>
    /// Projects an encounter-local point into normalized viewport coordinates.
    /// </summary>
    /// <param name="position">The encounter-local position.</param>
    /// <returns>The normalized projection and signed camera depth.</returns>
    public Vector3 Project(Vector3N position) =>
        _battleCamera.WorldToViewportPoint(_modelRoot.TransformPoint(ToUnity(position)));

    /// <summary>
    /// Updates the isolated camera's orbit transform.
    /// </summary>
    private void UpdateCamera()
    {
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);
        _battleCamera.transform.SetPositionAndRotation(
            _focus - rotation * Vector3.forward * _distance,
            rotation
        );
    }

    /// <summary>
    /// Releases screen-owned models, materials, and GPU memory.
    /// </summary>
    private void OnDestroy()
    {
        _effects?.Dispose();
        _impacts?.Dispose();
        _navigation?.Dispose();
        foreach (ContentModelInstance instance in _instances)
            instance.Dispose();
        _instances.Clear();
        foreach (Material material in _fighterMaterials.Values)
            Destroy(material);
        _fighterMaterials.Clear();
        foreach (Material material in _environmentMaterials.Values)
            Destroy(material);
        _environmentMaterials.Clear();
        _environmentTextures.Clear();
        ReleaseTarget();
        if (_billboardMesh != null)
            Destroy(_billboardMesh);
    }

    /// <summary>
    /// Detaches and releases the current camera target.
    /// </summary>
    private void ReleaseTarget()
    {
        if (_battleCamera != null)
            _battleCamera.targetTexture = null;
        if (_renderTarget == null)
            return;
        _renderTarget.Release();
        Destroy(_renderTarget);
        _renderTarget = null;
    }

    /// <summary>
    /// Converts a simulation vector at the rendering boundary.
    /// </summary>
    /// <param name="value">The simulation vector.</param>
    /// <returns>The Unity vector.</returns>
    private static Vector3 ToUnity(Vector3N value) => new Vector3(value.X, value.Y, value.Z);

    /// <summary>
    /// Assigns the isolated presentation layer once during model creation.
    /// </summary>
    /// <param name="root">The visual hierarchy.</param>
    /// <param name="layer">The reserved presentation layer.</param>
    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayer(child, layer);
    }

    private sealed class Visual
    {
        public TacticalCombatUnit Unit { get; }
        public GameObject Root { get; }
        public Transform Billboard { get; set; }
        public MeshRenderer FighterRenderer { get; set; }
        public Texture2D[] FighterTextures { get; set; }
        public Transform[] ModelDetails { get; set; }
        public int DetailLevel { get; set; } = 1;
        public Vector3? StationPosition { get; set; }

        /// <summary>
        /// Owns one unit's instantiated presentation.
        /// </summary>
        /// <param name="unit">The simulation unit.</param>
        /// <param name="root">The owned visual root.</param>
        public Visual(TacticalCombatUnit unit, GameObject root)
        {
            Unit = unit;
            Root = root;
        }
    }
}
