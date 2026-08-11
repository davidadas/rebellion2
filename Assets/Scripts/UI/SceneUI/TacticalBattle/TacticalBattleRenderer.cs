using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using UnityEngine;

/// <summary>
/// Owns the runtime visual objects that project a tactical battle into the scene.
/// </summary>
public sealed class TacticalBattleRenderer : MonoBehaviour
{
    private const float _capitalShipScale = 12f;
    private const float _closeSpritePixelsPerUnit = 2f;
    private const float _farSpritePixelsPerUnit = 1f;
    private const float _weaponEffectDuration = 0.18f;
    private const int _persistentEffectFrameCount = 8;
    private static readonly string[] ModelLods = { "close", "medium", "far" };
    private static readonly float[] LodScreenHeights = { 0.35f, 0.12f, 0.01f };
    private static readonly string[] FighterGroupColors = { "red", "blue", "green", "gold" };
    private readonly List<Transform> fighterBillboards = new List<Transform>();
    private readonly List<Material> navigationMaterials = new List<Material>();
    private readonly List<Material> shipHighlightMaterials = new List<Material>();
    private readonly List<ContentModelInstance> modelInstances = new List<ContentModelInstance>();
    private readonly List<GameObject> navigationSets = new List<GameObject>();
    private readonly List<Sprite> sprites = new List<Sprite>();
    private readonly List<TacticalUnitView> unitViews = new List<TacticalUnitView>();
    private readonly Dictionary<TacticalUnitState, TacticalUnitView> unitViewsByState =
        new Dictionary<TacticalUnitState, TacticalUnitView>();
    private Sprite[] gravityWellEffectFrames = Array.Empty<Sprite>();
    private Sprite[] blueBlastImpactFrames = Array.Empty<Sprite>();
    private Sprite[] blueNetImpactFrames = Array.Empty<Sprite>();
    private Sprite[] blueSpreadImpactFrames = Array.Empty<Sprite>();
    private Material navigationSelectedMaterial;
    private bool initialized;
    private bool unitSelectionEnabled;
    private TacticalBattleSession session;
    private Sprite[] orangeBlastImpactFrames = Array.Empty<Sprite>();
    private Sprite[] orangeDoubleBlastImpactFrames = Array.Empty<Sprite>();
    private Sprite[] orangeSplitImpactFrames = Array.Empty<Sprite>();
    private Sprite[] tractorLockEffectFrames = Array.Empty<Sprite>();

    /// <summary>
    /// Raised when the player selects a visible tactical waypoint marker.
    /// </summary>
    public event Action<TacticalNavPoint, bool> NavigationPointSelected;

    /// <summary>
    /// Raised when an enabled tactical targeting command selects a visible unit.
    /// </summary>
    public event Action<TacticalUnitState> UnitSelected;

    /// <summary>
    /// Gets whether a weapon or destruction effect still requires presentation time.
    /// </summary>
    public bool HasActiveCombatEffects =>
        GetComponentInChildren<TacticalCombatEffectView>(true) != null
        || GetComponentInChildren<TacticalOneShotEffectView>(true) != null
        || GetComponentInChildren<TacticalDestructionEffectView>(true) != null;

    /// <summary>
    /// Loads and creates all model-backed tactical units.
    /// </summary>
    /// <param name="session">The active tactical session.</param>
    /// <param name="modelCache">The application-owned model cache.</param>
    /// <param name="contentAssets">The active external content assets.</param>
    /// <param name="theme">The local faction's tactical presentation theme.</param>
    /// <param name="cancellationToken">Token that cancels scene initialization.</param>
    public async Task InitializeAsync(
        TacticalBattleSession session,
        ContentModelCache modelCache,
        IContentAssetSource contentAssets,
        TacticalBattleTheme theme,
        CancellationToken cancellationToken
    )
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));
        if (modelCache == null)
            throw new ArgumentNullException(nameof(modelCache));
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));
        if (theme == null)
            throw new ArgumentNullException(nameof(theme));
        if (string.IsNullOrWhiteSpace(theme.SharedEffectsRoot))
            throw new InvalidOperationException("A tactical shared-effects root is required.");
        if (initialized)
            throw new InvalidOperationException("Tactical presentation is already initialized.");

        this.session = session;
        gravityWellEffectFrames = LoadEffectFrames(
            contentAssets,
            $"{theme.SharedEffectsRoot}/GravityWell",
            _persistentEffectFrameCount
        );
        tractorLockEffectFrames = LoadEffectFrames(
            contentAssets,
            $"{theme.SharedEffectsRoot}/TractorLock",
            _persistentEffectFrameCount
        );
        string impactRoot = $"{theme.SharedEffectsRoot}/WeaponImpact";
        orangeSplitImpactFrames = LoadEffectFrames(contentAssets, $"{impactRoot}/OrangeSplit", 6);
        orangeBlastImpactFrames = LoadEffectFrames(contentAssets, $"{impactRoot}/OrangeBlast", 7);
        blueSpreadImpactFrames = LoadEffectFrames(contentAssets, $"{impactRoot}/BlueSpread", 6);
        blueNetImpactFrames = LoadEffectFrames(contentAssets, $"{impactRoot}/BlueNet", 16);
        blueBlastImpactFrames = LoadEffectFrames(contentAssets, $"{impactRoot}/BlueBlast", 7);
        orangeDoubleBlastImpactFrames = LoadEffectFrames(
            contentAssets,
            $"{impactRoot}/OrangeDoubleBlast",
            16
        );

        CapitalShip[] capitalShips = session
            .Units.Select(unit => unit.Unit)
            .OfType<CapitalShip>()
            .Where(ship => !string.IsNullOrWhiteSpace(ship.TacticalModelPath))
            .Distinct()
            .ToArray();
        string[] addresses = capitalShips
            .SelectMany(GetModelAddresses)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        await modelCache.PreloadAsync(addresses);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (TacticalUnitState unit in session.Units)
        {
            if (
                unit.Unit is not CapitalShip ship
                || string.IsNullOrWhiteSpace(ship.TacticalModelPath)
            )
                continue;

            await CreateCapitalShipAsync(unit, ship, modelCache, cancellationToken);
        }

        CreateFighterGroups(session, contentAssets);
        CreateNavigationGrid(session.NavigationGrid);

        initialized = true;
        Synchronize();
    }

    /// <summary>
    /// Applies the latest tactical transforms to every loaded unit.
    /// </summary>
    public void Synchronize()
    {
        foreach (TacticalUnitView unitView in unitViews)
            unitView.Synchronize(session.GetPresentationPosition(unitView.Unit));

        Camera battleCamera = Camera.main;
        if (battleCamera == null)
            return;

        foreach (Transform billboard in fighterBillboards)
            billboard.rotation = battleCamera.transform.rotation;
    }

    /// <summary>
    /// Creates transient weapon and destruction effects for completed simulation events.
    /// </summary>
    /// <param name="events">The tactical events in simulation order.</param>
    public void PresentEvents(IReadOnlyList<TacticalCombatEvent> events)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));

        foreach (TacticalCombatEvent combatEvent in events)
        {
            if (combatEvent.Kind == TacticalCombatEventKind.WeaponImpact)
                CreateWeaponEffect(combatEvent);
            else if (combatEvent.Kind == TacticalCombatEventKind.UnitDestroyed)
                CreateDestructionEffect(combatEvent);
            else if (combatEvent.Kind == TacticalCombatEventKind.SuperlaserFired)
                CreateSuperlaserEffect(combatEvent);
            else if (combatEvent.Kind == TacticalCombatEventKind.TractorLock)
                SetTractorLockEffect(combatEvent.Target, true);
            else if (combatEvent.Kind == TacticalCombatEventKind.TractorRelease)
                SetTractorLockEffect(combatEvent.Target, false);
        }
    }

    /// <summary>
    /// Creates one object-scaled pyrotechnic burst at a destroyed unit's final position.
    /// </summary>
    /// <param name="combatEvent">The unit-destruction event to present.</param>
    private void CreateDestructionEffect(TacticalCombatEvent combatEvent)
    {
        const float fallbackDiameter = 4f;
        float diameter =
            unitViewsByState.TryGetValue(combatEvent.Source, out TacticalUnitView unitView)
            && unitView.PresentationDiameter > 0f
                ? unitView.PresentationDiameter
                : fallbackDiameter;
        GameObject effect = new GameObject("Destruction Effect");
        effect.transform.SetParent(transform, false);
        effect.transform.localPosition = ToUnityVector(combatEvent.SourcePosition);
        effect.AddComponent<TacticalDestructionEffectView>().Initialize(diameter);
    }

    /// <summary>
    /// Creates the broad green source-to-target beam used by the Death Star superlaser.
    /// </summary>
    /// <param name="combatEvent">The resolved superlaser event.</param>
    private void CreateSuperlaserEffect(TacticalCombatEvent combatEvent)
    {
        GameObject effect = new GameObject("Superlaser Effect");
        effect.transform.SetParent(transform, false);
        LineRenderer line = effect.AddComponent<LineRenderer>();
        Material material = CreateEffectMaterial(new Color(0.15f, 1f, 0.1f));
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.startWidth = 0.9f;
        line.endWidth = 0.5f;
        line.SetPosition(0, ToUnityVector(combatEvent.SourcePosition));
        line.SetPosition(1, ToUnityVector(combatEvent.TargetPosition));
        effect.AddComponent<TacticalCombatEffectView>().Initialize(material, _weaponEffectDuration);
    }

    /// <summary>
    /// Selects the target animation for one resolved weapon impact.
    /// </summary>
    /// <param name="combatEvent">The resolved weapon-impact event.</param>
    /// <returns>The ordered impact frames, or an empty sequence when none is shown.</returns>
    private Sprite[] GetWeaponImpactFrames(TacticalCombatEvent combatEvent)
    {
        switch (combatEvent.WeaponType)
        {
            case TacticalWeaponType.LaserCannon:
                return combatEvent.ImpactState == TacticalImpactState.Destroyed
                    ? blueSpreadImpactFrames
                    : orangeSplitImpactFrames;
            case TacticalWeaponType.Turbolaser:
                if (combatEvent.Target.Kind == TacticalUnitKind.Fighters)
                    return blueBlastImpactFrames;

                return combatEvent.ImpactState == TacticalImpactState.Destroyed
                    ? blueNetImpactFrames
                    : orangeBlastImpactFrames;
            case TacticalWeaponType.IonCannon:
                return orangeDoubleBlastImpactFrames;
            case TacticalWeaponType.Torpedo:
                return Array.Empty<Sprite>();
            default:
                throw new ArgumentOutOfRangeException(nameof(combatEvent));
        }
    }

    /// <summary>
    /// Shows or hides one concentric tactical waypoint-marker set.
    /// </summary>
    /// <param name="setIndex">The zero-based internal shell index.</param>
    /// <param name="visible">Whether the marker set should be visible.</param>
    public void SetNavigationSetVisible(int setIndex, bool visible)
    {
        if (setIndex < 0 || setIndex >= navigationSets.Count)
            throw new ArgumentOutOfRangeException(nameof(setIndex));

        navigationSets[setIndex].SetActive(visible);
    }

    /// <summary>
    /// Applies the selected group's ordered route to the waypoint-marker materials.
    /// </summary>
    /// <param name="route">The selected group's active route.</param>
    public void SetNavigationRoute(IReadOnlyList<TacticalNavPoint> route)
    {
        if (route == null)
            throw new ArgumentNullException(nameof(route));

        foreach (
            TacticalNavigationMarker marker in GetComponentsInChildren<TacticalNavigationMarker>(
                true
            )
        )
        {
            MeshRenderer markerRenderer = marker.GetComponent<MeshRenderer>();
            markerRenderer.sharedMaterial =
                GetRouteIndex(route, marker.Point) > 0
                    ? navigationSelectedMaterial
                    : marker.NormalMaterial;
        }
    }

    /// <summary>
    /// Enables or disables selection of tactical units in the 3D battle display.
    /// </summary>
    /// <param name="enabled">Whether visible tactical units accept selection.</param>
    public void SetUnitSelectionEnabled(bool enabled)
    {
        unitSelectionEnabled = enabled;
    }

    /// <summary>
    /// Shows or hides the colored capital-ship boxes for one tactical side.
    /// </summary>
    /// <param name="side">The tactical side whose capital ships are affected.</param>
    /// <param name="color">The configured faction highlight color.</param>
    /// <param name="visible">Whether the boxes should be visible.</param>
    public void SetShipHighlights(TacticalBattleSide side, Color color, bool visible)
    {
        Material material = shipHighlightMaterials.FirstOrDefault(existing =>
            existing.color == color
        );
        if (material == null)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
                throw new InvalidOperationException(
                    "The tactical ship-highlight shader is unavailable."
                );

            material = new Material(shader) { color = color };
            shipHighlightMaterials.Add(material);
        }

        foreach (TacticalUnitView unitView in unitViews)
        {
            if (unitView.Unit.Side == side && unitView.Unit.Unit is CapitalShip)
                unitView.SetHighlighted(material, visible);
        }
    }

    /// <summary>
    /// Releases instantiated model hierarchies when the tactical scene closes.
    /// </summary>
    private void OnDestroy()
    {
        foreach (ContentModelInstance instance in modelInstances)
            instance.Dispose();

        modelInstances.Clear();
        foreach (Sprite sprite in sprites)
            Destroy(sprite);

        sprites.Clear();
        fighterBillboards.Clear();
        foreach (Material material in navigationMaterials)
            Destroy(material);

        foreach (Material material in shipHighlightMaterials)
            Destroy(material);

        navigationMaterials.Clear();
        shipHighlightMaterials.Clear();
        navigationSets.Clear();
        unitViews.Clear();
        unitViewsByState.Clear();
    }

    /// <summary>
    /// Creates one short source-to-target beam for a resolved weapon-family attack.
    /// </summary>
    /// <param name="combatEvent">The resolved weapon event.</param>
    private void CreateWeaponEffect(TacticalCombatEvent combatEvent)
    {
        GameObject effect = new GameObject($"{combatEvent.WeaponType} Effect");
        effect.transform.SetParent(transform, false);
        LineRenderer line = effect.AddComponent<LineRenderer>();
        Material material = CreateEffectMaterial(GetWeaponColor(combatEvent));
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.positionCount = 2;
        float width = GetWeaponWidth(combatEvent.WeaponType.Value);
        line.startWidth = width;
        line.endWidth = width;
        line.SetPosition(0, ToUnityVector(combatEvent.SourcePosition));
        line.SetPosition(1, ToUnityVector(combatEvent.TargetPosition));
        effect.AddComponent<TacticalCombatEffectView>().Initialize(material, _weaponEffectDuration);

        Sprite[] impactFrames = GetWeaponImpactFrames(combatEvent);
        if (
            impactFrames.Length > 0
            && unitViewsByState.TryGetValue(combatEvent.Target, out TacticalUnitView targetView)
        )
        {
            targetView.ShowWeaponImpact(
                impactFrames,
                transform.TransformPoint(ToUnityVector(combatEvent.SourcePosition))
            );
        }
    }

    /// <summary>
    /// Creates one transparent unlit material owned by a transient tactical effect.
    /// </summary>
    /// <param name="color">The effect color.</param>
    /// <returns>The owned effect material.</returns>
    private static Material CreateEffectMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            throw new InvalidOperationException("The tactical effect shader is unavailable.");

        return new Material(shader) { color = color };
    }

    /// <summary>
    /// Selects the tactical effect color for one weapon family and firing side.
    /// </summary>
    /// <param name="combatEvent">The weapon-impact event.</param>
    /// <returns>The corresponding effect color.</returns>
    private static Color GetWeaponColor(TacticalCombatEvent combatEvent)
    {
        if (combatEvent.WeaponType == TacticalWeaponType.IonCannon)
            return Color.blue;
        if (
            combatEvent.WeaponType == TacticalWeaponType.Torpedo
            && combatEvent.Source.Kind == TacticalUnitKind.Fighters
        )
        {
            return Color.white;
        }

        return combatEvent.Source.Side == TacticalBattleSide.Attacker ? Color.red : Color.green;
    }

    /// <summary>
    /// Selects the base beam width used by one tactical weapon family.
    /// </summary>
    /// <param name="weaponType">The weapon family being presented.</param>
    /// <returns>The beam width in tactical world units.</returns>
    private static float GetWeaponWidth(TacticalWeaponType weaponType)
    {
        return weaponType switch
        {
            TacticalWeaponType.LaserCannon => 0.5f,
            TacticalWeaponType.Turbolaser => 0.65f,
            TacticalWeaponType.IonCannon => 0.4f,
            TacticalWeaponType.Torpedo => 0.2f,
            _ => throw new ArgumentOutOfRangeException(nameof(weaponType)),
        };
    }

    /// <summary>
    /// Converts a simulation vector without coupling tactical state to Unity.
    /// </summary>
    /// <param name="value">The simulation vector.</param>
    /// <returns>The equivalent Unity vector.</returns>
    private static Vector3 ToUnityVector(System.Numerics.Vector3 value)
    {
        return new Vector3(value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Creates the four original concentric waypoint-marker shells.
    /// </summary>
    /// <param name="grid">The tactical waypoint lattice to present.</param>
    private void CreateNavigationGrid(TacticalNavigationGrid grid)
    {
        Material lowerMaterial = CreateNavigationMaterial(new Color(0f, 0.541f, 1f));
        Material centerMaterial = CreateNavigationMaterial(Color.red);
        Material upperMaterial = CreateNavigationMaterial(Color.white);
        navigationSelectedMaterial = CreateNavigationMaterial(Color.magenta);
        for (int setIndex = 0; setIndex < grid.SetCount; setIndex++)
        {
            GameObject setObject = new GameObject($"Navigation Set {setIndex + 1}");
            setObject.transform.SetParent(transform, false);
            foreach (TacticalNavPoint point in grid.GetPoints(setIndex))
            {
                Material material =
                    point.Y < 0f ? lowerMaterial
                    : point.Y > 0f ? upperMaterial
                    : centerMaterial;
                CreateNavigationMarker(setObject.transform, point, material);
            }

            setObject.SetActive(grid.IsVisible(setIndex));
            navigationSets.Add(setObject);
        }
    }

    /// <summary>
    /// Creates one compact waypoint marker at a lattice position.
    /// </summary>
    /// <param name="parent">The marker-set transform.</param>
    /// <param name="point">The waypoint represented by the marker.</param>
    /// <param name="material">The material for the waypoint's vertical layer.</param>
    private void CreateNavigationMarker(Transform parent, TacticalNavPoint point, Material material)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Waypoint";
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = new UnityEngine.Vector3(point.X, point.Y, point.Z);
        marker.transform.localScale = UnityEngine.Vector3.one * 0.8f;
        marker.GetComponent<MeshRenderer>().sharedMaterial = material;
        TacticalNavigationMarker interaction = marker.AddComponent<TacticalNavigationMarker>();
        interaction.Initialize(point, material);
        interaction.Selected += OnNavigationPointSelected;
    }

    /// <summary>
    /// Forwards one marker selection to the tactical scene controller.
    /// </summary>
    /// <param name="point">The selected waypoint.</param>
    /// <param name="editRoute">Whether the existing route should be edited.</param>
    private void OnNavigationPointSelected(TacticalNavPoint point, bool editRoute)
    {
        NavigationPointSelected?.Invoke(point, editRoute);
    }

    /// <summary>
    /// Finds a waypoint by reference within an ordered route.
    /// </summary>
    /// <param name="route">The route to search.</param>
    /// <param name="point">The waypoint to locate.</param>
    /// <returns>The route index, or -1 when absent.</returns>
    private static int GetRouteIndex(IReadOnlyList<TacticalNavPoint> route, TacticalNavPoint point)
    {
        for (int index = 0; index < route.Count; index++)
        {
            if (ReferenceEquals(route[index], point))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Creates and owns one unlit material used by a tactical waypoint layer.
    /// </summary>
    /// <param name="color">The source-defined marker color.</param>
    /// <returns>The shared waypoint material.</returns>
    private Material CreateNavigationMaterial(Color color)
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            throw new InvalidOperationException("The tactical waypoint shader is unavailable.");

        Material material = new Material(shader) { color = color };
        navigationMaterials.Add(material);
        return material;
    }

    /// <summary>
    /// Creates one capital ship with its close, medium, and far model resources.
    /// </summary>
    /// <param name="unit">The tactical unit being presented.</param>
    /// <param name="ship">The capital ship definition.</param>
    /// <param name="modelCache">The application-owned model cache.</param>
    /// <param name="cancellationToken">Token that cancels scene initialization.</param>
    private async Task CreateCapitalShipAsync(
        TacticalUnitState unit,
        CapitalShip ship,
        ContentModelCache modelCache,
        CancellationToken cancellationToken
    )
    {
        GameObject unitObject = new GameObject($"{ship.TypeID} Tactical Unit");
        unitObject.transform.SetParent(transform, false);
        unitObject.transform.localScale = Vector3.one * _capitalShipScale;
        TacticalUnitView unitView = unitObject.AddComponent<TacticalUnitView>();
        LOD[] lods = new LOD[ModelLods.Length];
        for (int index = 0; index < ModelLods.Length; index++)
        {
            string address = $"{ship.TacticalModelPath}/{ModelLods[index]}";
            GameObject lodRoot = new GameObject($"{ModelLods[index]} LOD");
            lodRoot.transform.SetParent(unitObject.transform, false);
            ContentModelInstance instance = await modelCache.InstantiateAsync(
                address,
                lodRoot.transform,
                cancellationToken
            );
            instance.ModelRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.ModelRoot.localScale = Vector3.one;
            modelInstances.Add(instance);
            lods[index] = new LOD(
                LodScreenHeights[index],
                lodRoot.GetComponentsInChildren<Renderer>(true)
            );
        }

        LODGroup lodGroup = unitObject.AddComponent<LODGroup>();
        lodGroup.fadeMode = LODFadeMode.None;
        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();
        SetCollisionExtents(unit, lods[0].renderers);
        unitView.Initialize(unit);
        ConfigureUnitSelection(unitView, lods[0].renderers);
        unitViews.Add(unitView);
        unitViewsByState.Add(unit, unitView);
    }

    /// <summary>
    /// Returns all model addresses required by one capital ship.
    /// </summary>
    /// <param name="ship">The ship whose model addresses to resolve.</param>
    /// <returns>The ordered close, medium, and far model addresses.</returns>
    private static IEnumerable<string> GetModelAddresses(CapitalShip ship)
    {
        return ModelLods.Select(lod => $"{ship.TacticalModelPath}/{lod}");
    }

    /// <summary>
    /// Creates the original close and far billboards for every fighter squadron.
    /// </summary>
    /// <param name="session">The active tactical session.</param>
    /// <param name="contentAssets">The active external content assets.</param>
    private void CreateFighterGroups(
        TacticalBattleSession session,
        IContentAssetSource contentAssets
    )
    {
        foreach (TacticalBattleSide side in Enum.GetValues(typeof(TacticalBattleSide)))
        {
            IReadOnlyList<TacticalShipGroup> groups = session.GetFighterGroups(side);
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                string color = FighterGroupColors[groupIndex];
                foreach (TacticalUnitState unit in groups[groupIndex].Units)
                    CreateFighter(unit, color, contentAssets);
            }
        }
    }

    /// <summary>
    /// Creates one color-coded fighter billboard with its close and far resources.
    /// </summary>
    /// <param name="unit">The fighter unit being presented.</param>
    /// <param name="color">The command-group color.</param>
    /// <param name="contentAssets">The active external content assets.</param>
    private void CreateFighter(
        TacticalUnitState unit,
        string color,
        IContentAssetSource contentAssets
    )
    {
        if (
            unit.Unit is not Starfighter fighters
            || string.IsNullOrWhiteSpace(fighters.TacticalSpritePath)
        )
            return;

        GameObject unitObject = new GameObject($"{fighters.TypeID} Tactical Unit");
        unitObject.transform.SetParent(transform, false);
        TacticalUnitView unitView = unitObject.AddComponent<TacticalUnitView>();
        LOD[] lods = new LOD[2];
        lods[0] = CreateFighterLod(
            unitObject.transform,
            contentAssets,
            $"{fighters.TacticalSpritePath}/{color}-close",
            "close",
            _closeSpritePixelsPerUnit,
            0.08f
        );
        lods[1] = CreateFighterLod(
            unitObject.transform,
            contentAssets,
            $"{fighters.TacticalSpritePath}/{color}-far",
            "far",
            _farSpritePixelsPerUnit,
            0.01f
        );

        LODGroup lodGroup = unitObject.AddComponent<LODGroup>();
        lodGroup.fadeMode = LODFadeMode.None;
        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();
        SetCollisionExtents(unit, lods[0].renderers);
        unitView.Initialize(unit);
        ConfigureUnitSelection(unitView, lods[0].renderers);
        unitViews.Add(unitView);
        unitViewsByState.Add(unit, unitView);
    }

    /// <summary>
    /// Creates one fighter sprite LOD from external content.
    /// </summary>
    /// <param name="parent">The fighter presentation root.</param>
    /// <param name="contentAssets">The active external content assets.</param>
    /// <param name="address">The sprite content address.</param>
    /// <param name="name">The LOD object name.</param>
    /// <param name="pixelsPerUnit">The resource pixels represented by one world unit.</param>
    /// <param name="screenHeight">The LOD screen-height threshold.</param>
    /// <returns>The configured fighter LOD.</returns>
    private LOD CreateFighterLod(
        Transform parent,
        IContentAssetSource contentAssets,
        string address,
        string name,
        float pixelsPerUnit,
        float screenHeight
    )
    {
        Texture2D texture = contentAssets.GetTexture(address);
        if (texture == null)
            throw new InvalidOperationException($"Tactical fighter sprite is missing: {address}");

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit
        );
        sprite.name = $"{name} tactical fighter sprite";
        sprites.Add(sprite);
        GameObject lodObject = new GameObject($"{name} LOD");
        lodObject.transform.SetParent(parent, false);
        SpriteRenderer renderer = lodObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        fighterBillboards.Add(lodObject.transform);
        return new LOD(screenHeight, new Renderer[] { renderer });
    }

    /// <summary>
    /// Measures one unit's close presentation and supplies its physical extents to the simulation.
    /// </summary>
    /// <param name="unit">The tactical unit receiving the measured extents.</param>
    /// <param name="renderers">The renderers that comprise the unit's close presentation.</param>
    private static void SetCollisionExtents(
        TacticalUnitState unit,
        IReadOnlyList<Renderer> renderers
    )
    {
        if (renderers == null || renderers.Count == 0)
            throw new InvalidOperationException(
                "A tactical unit has no close presentation bounds."
            );

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Count; index++)
            bounds.Encapsulate(renderers[index].bounds);

        float horizontalExtent = Mathf.Max(bounds.extents.x, bounds.extents.z);
        unit.SetCollisionExtents(horizontalExtent, bounds.extents.y);
    }

    /// <summary>
    /// Adds one presentation-bounds collider and forwards its selection through the renderer.
    /// </summary>
    /// <param name="unitView">The unit presentation that receives selection.</param>
    /// <param name="renderers">The close presentation used to calculate its bounds.</param>
    private void ConfigureUnitSelection(
        TacticalUnitView unitView,
        IReadOnlyList<Renderer> renderers
    )
    {
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Count; index++)
            bounds.Encapsulate(renderers[index].bounds);

        Transform root = unitView.transform;
        Vector3 scale = root.lossyScale;
        BoxCollider collider = unitView.gameObject.AddComponent<BoxCollider>();
        collider.center = root.InverseTransformPoint(bounds.center);
        collider.size = new Vector3(
            scale.x == 0f ? bounds.size.x : bounds.size.x / Mathf.Abs(scale.x),
            scale.y == 0f ? bounds.size.y : bounds.size.y / Mathf.Abs(scale.y),
            scale.z == 0f ? bounds.size.z : bounds.size.z / Mathf.Abs(scale.z)
        );
        if (unitView.Unit.Unit is CapitalShip)
            unitView.ConfigureHighlight(new Bounds(collider.center, collider.size));
        unitView.ConfigurePersistentEffects(
            tractorLockEffectFrames,
            gravityWellEffectFrames,
            new Bounds(collider.center, collider.size)
        );
        unitView.Selected += HandleUnitSelected;
    }

    /// <summary>
    /// Loads one ordered tactical effect sequence.
    /// </summary>
    /// <param name="contentAssets">The active external content assets.</param>
    /// <param name="rootAddress">The effect's content directory.</param>
    /// <param name="frameCount">The number of sequential frames to load.</param>
    /// <returns>The runtime sprites in playback order.</returns>
    private Sprite[] LoadEffectFrames(
        IContentAssetSource contentAssets,
        string rootAddress,
        int frameCount
    )
    {
        Sprite[] frames = new Sprite[frameCount];
        for (int index = 0; index < frames.Length; index++)
        {
            string address = $"{rootAddress}/frame-{index + 1}";
            Texture2D texture = contentAssets.GetTexture(address);
            if (texture == null)
                throw new InvalidOperationException($"Tactical effect frame is missing: {address}");

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width
            );
            sprite.name = $"{rootAddress} frame {index + 1}";
            sprites.Add(sprite);
            frames[index] = sprite;
        }

        return frames;
    }

    /// <summary>
    /// Applies one tractor-lock lifecycle event to the affected unit presentation.
    /// </summary>
    /// <param name="target">The unit gaining or losing a tractor lock.</param>
    /// <param name="active">Whether a new lock was established instead of released.</param>
    private void SetTractorLockEffect(TacticalUnitState target, bool active)
    {
        if (target != null && unitViewsByState.TryGetValue(target, out TacticalUnitView unitView))
            unitView.SetTractorLockActive(active);
    }

    /// <summary>
    /// Forwards world selection only while a tactical targeting command is active.
    /// </summary>
    /// <param name="unit">The selected tactical unit.</param>
    private void HandleUnitSelected(TacticalUnitState unit)
    {
        if (unitSelectionEnabled)
            UnitSelected?.Invoke(unit);
    }
}

/// <summary>
/// Converts pointer presses on one tactical waypoint marker into route-edit requests.
/// </summary>
internal sealed class TacticalNavigationMarker : MonoBehaviour
{
    private TacticalNavPoint point;

    /// <summary>
    /// Gets the represented waypoint.
    /// </summary>
    public TacticalNavPoint Point => point;

    /// <summary>
    /// Gets the marker's normal vertical-layer material.
    /// </summary>
    public Material NormalMaterial { get; private set; }

    /// <summary>
    /// Raised when the player selects this waypoint.
    /// </summary>
    public event Action<TacticalNavPoint, bool> Selected;

    /// <summary>
    /// Associates the rendered marker with its tactical waypoint.
    /// </summary>
    /// <param name="navigationPoint">The represented waypoint.</param>
    /// <param name="normalMaterial">The normal material for its vertical layer.</param>
    public void Initialize(TacticalNavPoint navigationPoint, Material normalMaterial)
    {
        point = navigationPoint ?? throw new ArgumentNullException(nameof(navigationPoint));
        NormalMaterial = normalMaterial
            ? normalMaterial
            : throw new ArgumentNullException(nameof(normalMaterial));
    }

    /// <summary>
    /// Requests replacement by default and list editing while either Control key is held.
    /// </summary>
    private void OnMouseDown()
    {
        if (point == null)
            return;

        bool editRoute = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        Selected?.Invoke(point, editRoute);
    }
}
