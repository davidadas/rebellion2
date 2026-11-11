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
public sealed class TacticalCombatRenderer : MonoBehaviour, ITacticalCombatArrivalPresentation
{
    [SerializeField]
    private Camera _battleCamera;

    [SerializeField]
    private Transform _modelRoot;

    [SerializeField]
    private Shader _fighterShader;
    private readonly List<Visual> _visuals = new();
    private readonly Dictionary<int, Visual> _visualsByIndex = new();
    private readonly HashSet<TacticalCombatUnit> _yellowBoxes = new();
    private readonly HashSet<TacticalCombatUnit> _factionBoxes = new();
    private readonly HashSet<TacticalCombatUnit> _whiteBoxes = new();
    private readonly bool[] _factionHighlight = new bool[2];
    private TacticalCombatSession _session;
    private readonly TacticalCombatMeshPicker _picker = new();
    private readonly List<ContentModelInstance> _instances = new();
    private readonly Dictionary<Texture2D, Material> _fighterMaterials = new();
    private RenderTexture _renderTarget;
    private Mesh _billboardMesh;
    private Transform _planet;
    private TacticalCombatEffects _effects;
    private TacticalCombatImpactEffects _impacts;
    private TacticalCombatNavigationRenderer _navigation;
    private TacticalCombatHolocube _holocube;
    private readonly Dictionary<Material, Material> _environmentMaterials = new();
    private readonly Dictionary<string, Texture2D> _environmentTextures = new(
        StringComparer.Ordinal
    );
    private TacticalCombatSceneryRandom _sceneryRandom;
    private TacticalCombatCameraState _cameraState;
    private float _field => _cameraState.Field;
    private float _distance;
    private bool _showPyro;
    private bool _highDetail;
    private TacticalCombatDetailProjection _detailProjection;
    public Camera Camera => _battleCamera;
    public RenderTexture Target => _renderTarget;
    public bool IsPresentingArrival { get; private set; }
    public event Action<bool> ArrivalStrobeChanged;

    /// <summary>
    /// Loads required content into an isolated scene without advancing battle time.
    /// </summary>
    /// <param name="session">The deployed encounter.</param>
    /// <param name="models">The shared parsed model cache.</param>
    /// <param name="assets">The active pack's addressed textures.</param>
    /// <param name="cancellationToken">The owning screen's cancellation token.</param>
    /// <param name="videoSettings">The encounter's video settings, or null to use their authored defaults for isolated presentation.</param>
    /// <returns>The asynchronous presentation preparation.</returns>
    public Task PrepareAsync(
        TacticalCombatSession session,
        ContentModelCache models,
        ContentAssets assets,
        CancellationToken cancellationToken,
        UserVideoSettings videoSettings = null
    ) =>
        PrepareAsync(
            session,
            models,
            assets,
            cancellationToken,
            new TacticalCombatRenderSettings(videoSettings ?? new UserVideoSettings())
        );

    /// <summary>
    /// Loads battle resources using the immutable video options captured at encounter entry.
    /// </summary>
    /// <param name="session">The deployed encounter.</param>
    /// <param name="models">The shared parsed model cache.</param>
    /// <param name="assets">The active pack's addressed textures.</param>
    /// <param name="cancellationToken">The owning screen's cancellation token.</param>
    /// <param name="videoSettings">The encounter's captured video options.</param>
    /// <returns>The asynchronous presentation preparation.</returns>
    public async Task PrepareAsync(
        TacticalCombatSession session,
        ContentModelCache models,
        ContentAssets assets,
        CancellationToken cancellationToken,
        TacticalCombatRenderSettings videoSettings
    )
    {
        if (_battleCamera == null || _modelRoot == null || _fighterShader == null)
            throw new MissingReferenceException($"{name} is missing battle rendering references.");
        _session = session;
        _showPyro = videoSettings.ShowPyro;
        _highDetail = videoSettings.HighDetail;
        _sceneryRandom =
            session.SceneryRandom
            ?? throw new InvalidOperationException(
                "The tactical encounter has no scenery random stream."
            );
        _distance = session.BattleDepth * 1.7f;
        _cameraState = new TacticalCombatCameraState(
            _distance,
            session.GetSide(session.PlayerSide).FactionIndex
        );
        _effects = new TacticalCombatEffects(_modelRoot, _fighterShader, session.BattleDepth);
        _impacts = new TacticalCombatImpactEffects(
            _modelRoot,
            _battleCamera,
            _fighterShader,
            _showPyro
                ? session.Definition.Effects
                : Array.Empty<Rebellion.Game.SpaceCombat.TacticalCombatEffectDefinition>(),
            assets
        );
        _navigation = new TacticalCombatNavigationRenderer(
            _modelRoot,
            _fighterShader,
            session.Navigation
        );
        _holocube = new TacticalCombatHolocube(
            _modelRoot,
            _fighterShader,
            session.BattleDepth,
            videoSettings.ShowHolocube
        );
        UpdateFieldOfView();
        PrepareEnvironment(session, assets, videoSettings.ShowPlanet);
        _battleCamera.farClipPlane = _distance * 2.5f;
        _battleCamera.nearClipPlane = 1;
        foreach (TacticalCombatUnit unit in session.Units)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameObject root = new GameObject(unit.InstanceID);
            root.transform.SetParent(_modelRoot, false);
            Visual visual = new Visual(unit, root);
            _visuals.Add(visual);
            _visualsByIndex.Add(unit.Index, visual);
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
            if (!unit.IsBattleStation && !unit.IsFighter)
            {
                _picker.Register(root, unit);
                visual.SelectionBox = new TacticalCombatSelectionBox(
                    root.transform,
                    visual.ModelDetails[Math.Min(1, visual.ModelDetails.Length - 1)],
                    _fighterShader,
                    session.GetSide(unit.Side).FactionIndex
                );
            }
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
    /// <param name="unit">The impacted encounter participant.</param>
    /// <param name="weapon">The impacting weapon.</param>
    /// <param name="impact">The affected surface or destruction transition.</param>
    public void ShowImpact(
        TacticalCombatUnit unit,
        TacticalWeaponType weapon,
        TacticalCombatImpactKind impact
    )
    {
        if (_showPyro)
            _impacts.ShowImpact(unit, weapon, impact);
    }

    /// <summary>
    /// Begins the capital model's authored destruction sequence.
    /// </summary>
    /// <param name="unit">The destroyed encounter participant.</param>
    public void ShowDestruction(TacticalCombatUnit unit)
    {
        if (_showPyro)
            _impacts.ShowDestruction(unit);
    }

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
    /// <param name="showPlanet">Whether the planet image and its scenery-position draws are enabled.</param>
    private void PrepareEnvironment(
        TacticalCombatSession session,
        ContentAssets assets,
        bool showPlanet
    )
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
        if (!showPlanet || string.IsNullOrWhiteSpace(environment.PlanetImagePath))
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
    /// Places background scenery using the encounter-owned sequence and shared battle seed boundary.
    /// </summary>
    /// <param name="depth">The battle's deployment span.</param>
    /// <param name="horizontal">The horizontal position in multiples of the span.</param>
    /// <param name="spread">The vertical and depth spread in multiples of the span.</param>
    /// <returns>The encounter-local scenery position.</returns>
    private Vector3 GetSceneryPosition(float depth, float horizontal, float spread) =>
        ToUnity(_sceneryRandom.NextPosition(depth, horizontal, spread));

    /// <summary>
    /// Converts the viewport's near-plane half extent into the camera's vertical angle.
    /// </summary>
    private void UpdateFieldOfView()
    {
        if (_detailProjection?.Field.Equals(_field) != true)
            _detailProjection = new TacticalCombatDetailProjection(_field);
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
        int color = 4;
        if (visual.Unit.Side == session.PlayerSide)
        {
            TacticalCombatGroup requested = session.GetRequestedGroup(visual.Unit);
            color = requested?.IsFighterGroup == true ? requested.FighterCategory : 0;
        }
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
        UpdateCamera();
        RefreshSelectionBoxes();
        if (_planet != null)
            _planet.rotation = _battleCamera.transform.rotation;
        foreach (Visual visual in _visuals)
            RefreshVisual(visual);
    }

    /// <summary>
    /// Retains arrival transforms until the final snap and projects its pause-control state.
    /// </summary>
    /// <param name="active">Whether arrival presentation owns the affected transforms.</param>
    public void SetPauseStrobe(bool active)
    {
        IsPresentingArrival = active;
        if (!active)
            foreach (Visual visual in _visuals)
            {
                visual.ArrivalPosition = null;
                visual.ArrivalDirection = null;
                visual.ArrivalVisible = false;
            }
        ArrivalStrobeChanged?.Invoke(active);
    }

    /// <summary>
    /// Restores an addressed arrival member's base presentation before its first transform.
    /// </summary>
    /// <param name="unitIndex">The battle registration identifier.</param>
    public void Reset(int unitIndex)
    {
        if (!_visualsByIndex.TryGetValue(unitIndex, out Visual visual))
            return;
        visual.ArrivalVisible = true;
        visual.Root.SetActive(true);
        visual.SelectionBox?.SetState(1, false);
    }

    /// <summary>
    /// Applies an encounter-local arrival transform without changing simulation state.
    /// </summary>
    /// <param name="unitIndex">The battle registration identifier.</param>
    /// <param name="position">The presentation position.</param>
    /// <param name="direction">The participant's presentation direction.</param>
    public void SetTransform(
        int unitIndex,
        System.Numerics.Vector4 position,
        System.Numerics.Vector4 direction
    )
    {
        if (!_visualsByIndex.TryGetValue(unitIndex, out Visual visual))
            return;
        visual.ArrivalPosition = new Vector3(position.X, position.Y, position.Z);
        visual.ArrivalDirection = new Vector3(direction.X, direction.Y, direction.Z);
        ApplyTransform(visual);
    }

    /// <summary>
    /// Refreshes an addressed member's current visibility, selection state, and detail level.
    /// </summary>
    /// <param name="unitIndex">The battle registration identifier.</param>
    public void Refresh(int unitIndex)
    {
        if (_visualsByIndex.TryGetValue(unitIndex, out Visual visual))
            RefreshVisual(visual);
    }

    /// <summary>
    /// Projects one participant's owned presentation state without enumerating its hierarchy.
    /// </summary>
    /// <param name="visual">The participant's presentation.</param>
    private void RefreshVisual(Visual visual)
    {
        TacticalCombatUnit unit = visual.Unit;
        bool visible =
            visual.ArrivalVisible
            || _cameraState.IsCategoryView && _cameraState.TrackedObjectIndex == unit.Index
            || unit.Systems.IsAlive
                && unit.Status
                    is TacticalCombatUnitStatus.Active
                        or TacticalCombatUnitStatus.PreparingHyperspace
                        or TacticalCombatUnitStatus.EnteringHyperspace
                        or TacticalCombatUnitStatus.UnderConstruction
            || _impacts.KeepsModelVisible(unit);
        visual.Root.SetActive(visible);
        if (visual.SelectionBox != null)
        {
            int state =
                !unit.IsEngaged ? 1
                : _yellowBoxes.Contains(unit) ? 2
                : _whiteBoxes.Contains(unit) ? 4
                : _factionBoxes.Contains(unit)
                || _factionHighlight[_session.GetSide(unit.Side).FactionIndex]
                    ? 3
                : 1;
            visual.SelectionBox.SetState(state, _cameraState.TrackedObjectIndex == unit.Index);
        }
        if (!visible)
            return;
        ApplyTransform(visual);
        if (visual.Billboard != null)
        {
            visual.Billboard.rotation = _battleCamera.transform.rotation;
            if (unit.IsFighter)
                UpdateFighter(visual);
        }
        else if (visual.ModelDetails != null)
            UpdateModelDetail(visual);
    }

    /// <summary>
    /// Chooses the arrival transform while it is owned, or the participant's ordinary pose.
    /// </summary>
    /// <param name="visual">The participant's presentation.</param>
    private void ApplyTransform(Visual visual)
    {
        Vector3N position = visual.Unit.Position;
        Vector3N direction = visual.Unit.Heading;
        if (visual.Unit.Status == TacticalCombatUnitStatus.EnteringHyperspace)
        {
            TacticalCombatDeparturePose departure = TacticalCombatDeparturePose.Evaluate(
                visual.Unit.Index,
                visual.Unit.Definition.PresentationIndex,
                position,
                _session.GetSide(visual.Unit.Side).Forward,
                visual.Unit.HyperspaceMilliseconds
            );
            position = departure.Position;
            direction = departure.Heading;
        }
        visual.Root.transform.localPosition =
            visual.ArrivalPosition ?? visual.StationPosition ?? ToUnity(position);
        Vector3 heading = visual.ArrivalDirection ?? ToUnity(direction);
        if (heading.sqrMagnitude > 0)
            visual.Root.transform.localRotation = Quaternion.LookRotation(-heading, Vector3.up);
    }

    /// <summary>
    /// Changes one faction's capital bounding-box visibility independently of the command selection.
    /// </summary>
    /// <param name="factionIndex">The authored faction index.</param>
    /// <param name="enabled">Whether that faction's capital boxes are requested.</param>
    public void SetFactionHighlight(int factionIndex, bool enabled)
    {
        if ((uint)factionIndex >= 2)
            throw new ArgumentOutOfRangeException(nameof(factionIndex));
        _factionHighlight[factionIndex] = enabled;
    }

    /// <summary>
    /// Captures the current context's selection and ordered target colors once for the render frame.
    /// </summary>
    private void RefreshSelectionBoxes()
    {
        _yellowBoxes.Clear();
        _factionBoxes.Clear();
        _whiteBoxes.Clear();
        TacticalCombatSelection selection = _session.Selection;
        foreach (TacticalCombatUnit unit in selection.Highlights)
        {
            bool yellow =
                selection.Kind == TacticalCombatSelectionKind.MultipleObjects
                || selection.Kind
                    is TacticalCombatSelectionKind.SingleObject
                        or TacticalCombatSelectionKind.ObjectGroup
                    && ReferenceEquals(selection.PrimaryUnit, unit);
            (yellow ? _yellowBoxes : _factionBoxes).Add(unit);
        }
        bool first = true;
        foreach (TacticalCombatUnit target in selection.Targets)
        {
            if (!selection.IsDisplayObject(target))
                continue;
            if (first)
            {
                first = false;
                if (!target.IsFighter)
                    _whiteBoxes.Add(target);
            }
            else
                _factionBoxes.Add(target);
        }
    }

    /// <summary>
    /// Selects capital geometry from the camera-scaled depth thresholds while retaining its current level on exact boundaries.
    /// </summary>
    /// <param name="visual">The active capital visual.</param>
    private void UpdateModelDetail(Visual visual)
    {
        float distance = GetDetailDistance(visual);
        int selected = TacticalCombatDetailLevels.SelectCapital(
            distance,
            _field,
            _highDetail,
            visual.DetailLevel
        );
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
        int lod = _detailProjection.SelectFighter(
            GetDetailDistance(visual),
            _highDetail,
            visual.DetailLevel
        );
        visual.DetailLevel = lod;
        Texture2D texture = visual.FighterTextures[lod];
        if (!_fighterMaterials.TryGetValue(texture, out Material material))
        {
            material = new Material(_fighterShader) { mainTexture = texture };
            _fighterMaterials.Add(texture, material);
        }
        visual.FighterRenderer.sharedMaterial = material;
    }

    /// <summary>
    /// Projects the camera-relative position onto the camera's direction in the battle frame.
    /// </summary>
    /// <param name="visual">The participant's owned presentation.</param>
    /// <returns>The rounded nonnegative detail distance, retaining unordered values.</returns>
    private float GetDetailDistance(Visual visual)
    {
        Vector3 position = _battleCamera.transform.InverseTransformPoint(
            visual.Root.transform.position
        );
        Vector3 direction = _modelRoot.InverseTransformDirection(_battleCamera.transform.forward);
        double dot = (double)position.z * direction.z + (double)position.y * direction.y;
        dot += (double)position.x * direction.x;
        float distance = (float)dot;
        return !(dot >= 0) ? -distance : distance;
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
    /// Resolves a pointer press using the ordered pixel neighborhood associated with its button.
    /// </summary>
    /// <param name="point">The normalized viewport press.</param>
    /// <param name="secondary">Whether the secondary pointer button was pressed.</param>
    /// <param name="unit">Receives an object hit, or null for a navigation marker.</param>
    /// <param name="waypoint">Receives the addressed navigation marker.</param>
    /// <returns>Whether one of the ordered neighborhood rays hit an addressed object.</returns>
    public bool TryPickNeighborhood(
        Vector2 point,
        bool secondary,
        out TacticalCombatUnit unit,
        out Vector3N waypoint
    ) =>
        TacticalCombatPointerPicker.TryPick(
            point,
            new Vector2Int(_battleCamera.pixelWidth, _battleCamera.pixelHeight),
            secondary,
            TryPick,
            out unit,
            out waypoint
        );

    /// <summary>
    /// Applies one semantic camera operation independently of simulation pause.
    /// </summary>
    /// <param name="command">The requested primary-camera operation.</param>
    public void ApplyCameraCommand(TacticalCombatCameraCommand command)
    {
        _cameraState.Apply(command);
        UpdateFieldOfView();
        UpdateCamera();
    }

    /// <summary>
    /// Opens an occupied object or fighter category without changing its command selection.
    /// </summary>
    /// <param name="category">The object category from zero through seven, or fighter category from eight through eleven.</param>
    /// <returns>Whether the category has a registered owner.</returns>
    public bool ShowCategory(int category)
    {
        if ((uint)category >= 12)
            throw new ArgumentOutOfRangeException(nameof(category));
        if (GetCameraGroup(category) == null)
            return false;
        _cameraState.OpenCategory(category, () => ResolveCameraFocus(category));
        UpdateCamera();
        return true;
    }

    /// <summary>
    /// Resolves the current owner of one camera category independently of its prior slot contents.
    /// </summary>
    /// <param name="category">The camera category from zero through eleven.</param>
    /// <returns>The current category owner, or null for an empty slot.</returns>
    private TacticalCombatGroup GetCameraGroup(int category) =>
        category < 8
            ? _session.GetObjectGroup(_session.PlayerSide, category)
            : _session.GetFighterGroup(_session.PlayerSide, category - 8);

    /// <summary>
    /// Resolves applied members, route points, and targets against the screen's cached presentations.
    /// </summary>
    /// <param name="category">The live category slot.</param>
    /// <returns>The current close-up references, or null when no primary render object resolves.</returns>
    private TacticalCombatCameraFocus? ResolveCameraFocus(int category)
    {
        TacticalCombatGroup group = GetCameraGroup(category);
        if (group == null)
            return null;
        TacticalCombatUnit primary = group.PrimaryMember;
        if (category < 8 && _session.Selection.Kind == TacticalCombatSelectionKind.ObjectGroup)
            primary = _session.Selection.PrimaryUnit ?? primary;
        if (primary == null || !_visualsByIndex.TryGetValue(primary.Index, out Visual visual))
            return null;
        Vector3? secondaryPosition = null;
        TacticalCombatUnit secondary = null;
        if (group.PrimaryWaypoint is Vector3N waypoint)
        {
            if (_navigation.TryGetWorldPosition(waypoint, out Vector3 markerPosition))
                secondaryPosition = markerPosition;
        }
        else if (group.Targets.Count > 0)
            secondary = group.Targets[0];
        if (
            category >= 8
            && !group.PrimaryWaypoint.HasValue
            && secondary == null
            && group.Order == TacticalCombatOrder.Recover
            && group.Members.Count > 0
        )
            secondary = _session.GetUnit(group.Members[0].CarrierID);
        if (
            secondary != null
            && !ReferenceEquals(primary, secondary)
            && _visualsByIndex.TryGetValue(secondary.Index, out Visual secondaryVisual)
        )
        {
            ApplyTransform(secondaryVisual);
            secondaryPosition = GetCameraPosition(secondaryVisual);
        }
        ApplyTransform(visual);
        visual.Root.SetActive(true);
        visual.SelectionBox?.SetState(1, false);
        Vector3 span = visual.SelectionBox?.Bounds.size ?? Vector3.zero;
        return new TacticalCombatCameraFocus(
            primary.Index,
            GetCameraPosition(visual),
            primary.IsBattleStation ? _modelRoot.rotation : visual.Root.transform.rotation,
            span.x > span.y ? span.x : span.y,
            secondaryPosition
        );
    }

    /// <summary>
    /// Tracks the selected participant's live render position without translating the camera orbit.
    /// </summary>
    /// <param name="unit">The selected participant, or null to clear tracking.</param>
    public void Focus(TacticalCombatUnit unit)
    {
        if (unit == null || !_visualsByIndex.TryGetValue(unit.Index, out Visual visual))
            _cameraState.Track(null);
        else
        {
            _cameraState.Track(
                () =>
                {
                    ApplyTransform(visual);
                    return GetCameraPosition(visual);
                },
                unit.Index
            );
        }
        UpdateCamera();
    }

    /// <summary>
    /// Resolves the logical object frame independently of any offset background billboard.
    /// </summary>
    /// <param name="visual">The registered object's presentation.</param>
    /// <returns>The logical render frame's world position.</returns>
    private Vector3 GetCameraPosition(Visual visual) =>
        visual.Unit.IsBattleStation ? _modelRoot.position : visual.Root.transform.position;

    /// <summary>
    /// Aims the primary camera at an addressed navigation marker without moving its orbit.
    /// </summary>
    /// <param name="point">The marker's fixed encounter-local position.</param>
    public void Focus(Vector3N point)
    {
        _cameraState.Track(() => _modelRoot.TransformPoint(ToUnity(point)));
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
        _cameraState.GetPose(_modelRoot.position, out Vector3 position, out Quaternion rotation);
        _battleCamera.transform.SetPositionAndRotation(position, rotation);
        _battleCamera.nearClipPlane = _cameraState.NearPlane;
        UpdateFieldOfView();
    }

    /// <summary>
    /// Releases screen-owned models, materials, and GPU memory.
    /// </summary>
    private void OnDestroy()
    {
        _effects?.Dispose();
        _impacts?.Dispose();
        _navigation?.Dispose();
        _holocube?.Dispose();
        foreach (Visual visual in _visuals)
            visual.SelectionBox?.Dispose();
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
        public TacticalCombatSelectionBox SelectionBox { get; set; }
        public Vector3? StationPosition { get; set; }
        public Vector3? ArrivalPosition { get; set; }
        public Vector3? ArrivalDirection { get; set; }
        public bool ArrivalVisible { get; set; }

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
