using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Presents the tactical battle's units as 3D models, rendered by an isolated perspective camera
/// into the arena image. Battle positions lie on the ground plane; the camera orbits, pans, and
/// zooms around a focus point on that plane, locked to the play space, and the 2D overlay
/// projects through the same camera.
/// </summary>
public sealed class TacticalBattleScene : MonoBehaviour
{
    internal const float ShipModelDiameterScale = 3f;
    internal const float FighterModelDiameterScale = 1.6f;
    internal const string ModelAddressSuffix = "/Models/model";
    internal const string CompositeShaderName = "Custom/PremultipliedTexture";
    internal const float MinimumDistance = 6f;
    internal const float MaximumDistance = 900f;
    public const float DefaultPitchDegrees = 50f;
    internal const float MinimumPitchDegrees = 12f;
    internal const float MaximumPitchDegrees = 85f;
    public const float FieldOfViewDegrees = 40f;
    internal const float SelectionLineWidth = 1.25f;

    private const float _nearClip = 0.3f;
    private const float _farClip = 4000f;
    private const float _zoomStepFactor = 0.85f;
    private const float _arenaFramingPadding = 1.15f;
    private const float _fleetFramingPadding = 1.6f;
    private const float _focusDistanceDiameters = 4f;
    private const float _viewportMargin = 0.05f;
    private const float _placeholderLength = 0.9f;
    private const float _placeholderWidth = 0.45f;
    private const float _placeholderHeight = 0.16f;
    private const float _hullSmoothness = 0.35f;
    private const string _smoothnessProperty = "_Smoothness";
    private const string _cullModeProperty = "_CullMode";
    private const string _cullModeForwardProperty = "_CullModeForward";
    private const string _doubleSidedEnableProperty = "_DoubleSidedEnable";
    private const string _doubleSidedKeyword = "_DOUBLESIDED_ON";
    private const int _maximumRenderSize = 4096;
    private const string _emissiveColorProperty = "_EmissiveColor";
    private const string _emissiveExposureWeightProperty = "_EmissiveExposureWeight";
    private const string _alphaCutoffEnableProperty = "_AlphaCutoffEnable";
    private const string _baseColorMapProperty = "_BaseColorMap";
    private const string _alphaCutoffProperty = "_AlphaCutoff";
    private const string _alphaTestKeyword = "_ALPHATEST_ON";
    private const int _particleTextureSize = 64;
    private const float _particleEdgeSoftness = 0.35f;
    private const float _particleAlphaCutoff = 0.5f;
    private const float _explosionFlashSize = 1.4f;
    private const float _explosionFlashLifetime = 0.25f;
    private const int _explosionFireCount = 28;
    private const float _explosionFireSize = 0.3f;
    private const float _explosionFireSpeed = 1.6f;
    private const float _explosionFireLifetime = 0.8f;
    private const int _explosionSmokeCount = 14;
    private const float _torpedoBlastDiameter = 3f;
    private const int _torpedoBlastFireCount = 8;
    private const int _torpedoBlastSmokeCount = 4;
    private const float _explosionSmokeSize = 0.5f;
    private const float _explosionSmokeSpeed = 0.6f;
    private const float _explosionSmokeLifetime = 1.4f;
    private const float _explosionSpread = 0.15f;
    private const float _tractorEmissionRate = 36f;
    private const float _tractorParticleSize = 0.08f;
    private const float _tractorInwardSpeed = 0.9f;
    private const float _tractorRadiusScale = 0.7f;
    private const float _tractorShellThickness = 0.1f;
    private const float _tractorEndSize = 0.2f;
    private const float _flashEmission = 3f;
    private const float _fireEmission = 2f;
    private const float _tractorEmission = 2f;
    private const float _gravityWellEmission = 2f;
    private const float _gravityWellEmissionRate = 48f;
    private const float _gravityWellRadiusScale = 1f;
    private const float _jumpArrivalSeconds = 1.6f;
    private const float _jumpDepartureSeconds = 1.2f;
    private const float _jumpDistanceScale = 1.5f;
    private const float _jumpStretch = 3f;
    private const float _superlaserBeamSeconds = 1.2f;
    private const float _superlaserBeamWidth = 3f;
    private const float _superlaserBeamRise = 0.15f;
    private const float _superlaserEmission = 6f;
    private const int _stationExplosionBursts = 12;
    private const float _stationExplosionSpread = 0.8f;
    private const float _stationExplosionBurstScale = 0.7f;
    private const float _stationViewPitchDegrees = 20f;
    private const float _stationViewDistanceRadii = 5f;
    private const int _selectionEdgeCount = 12;
    private const float _deckMarkerMinimumHeight = 0.5f;
    private const float _deckMarkerLineWidth = 1f;
    private const float _deckMarkerFootScale = 0.12f;
    private const float _deckMarkerDim = 0.45f;
    private const float _selectionPadding = 1.1f;
    private const float _dockedScale = 0.15f;
    private const float _selectionEmission = 1.5f;
    private static readonly Color _attackerPlaceholderColor = new Color(0.55f, 0.58f, 0.66f, 1f);
    private static readonly Color _defenderPlaceholderColor = new Color(0.64f, 0.6f, 0.52f, 1f);
    private static readonly Color _flashColor = new Color(1f, 0.95f, 0.8f, 1f);
    private static readonly Color _fireColor = new Color(1f, 0.45f, 0.1f, 1f);
    private static readonly Color _smokeColor = new Color(0.25f, 0.24f, 0.22f, 1f);
    private static readonly Color _tractorColor = new Color(0.3f, 0.6f, 1f, 1f);
    private static readonly Color _gravityWellColor = new Color(0.85f, 0.42f, 0.1f, 1f);
    private static readonly Color _superlaserColor = new Color(0.35f, 1f, 0.3f, 1f);

    [SerializeField]
    private Camera sceneCamera;

    [SerializeField]
    private Transform modelRoot;

    [SerializeField]
    private RawImage outputImage;

    [SerializeField]
    private Material litMaterial;

    [SerializeField]
    private Material particleMaterial;

    /// <summary>
    /// Gets the authored opaque lit material that placeholders, markers, beams and repaired hulls
    /// are cloned from, or null in a scene assembled without the rig.
    /// </summary>
    internal Material LitMaterial => litMaterial;

    /// <summary>
    /// Gets the authored alpha-cutout material that particle effects are cloned from, or null in
    /// a scene assembled without the rig.
    /// </summary>
    internal Material ParticleMaterial => particleMaterial;

    private readonly Dictionary<TacticalBattleUnit, UnitPresentation> _units =
        new Dictionary<TacticalBattleUnit, UnitPresentation>();
    private readonly HashSet<string> _reportedAddresses = new HashSet<string>();
    private readonly List<Material> _replacedMaterials = new List<Material>();
    private readonly List<ExplosionEffect> _explosions = new List<ExplosionEffect>();
    private readonly HashSet<TacticalShot> _torpedoesInFlight = new HashSet<TacticalShot>();
    private readonly List<TacticalShot> _landedTorpedoes = new List<TacticalShot>();
    private readonly Dictionary<TacticalBattleStation, UnitPresentation> _stations =
        new Dictionary<TacticalBattleStation, UnitPresentation>();
    private readonly List<SuperlaserBeam> _beams = new List<SuperlaserBeam>();
    private readonly List<SelectionBox> _selectionBoxes = new List<SelectionBox>();
    private readonly List<DeckMarker> _deckMarkers = new List<DeckMarker>();
    private readonly Dictionary<Color, Material> _selectionMaterials =
        new Dictionary<Color, Material>();
    private Material _flashMaterial;
    private Material _fireMaterial;
    private Material _smokeMaterial;
    private Material _tractorMaterial;
    private Material _gravityWellMaterial;
    private Material _superlaserMaterial;
    private Texture2D _particleTexture;
    private float _jumpDistance = 600f;
    private ContentModelCache _modelCache;
    private CancellationTokenSource _loading;
    private int _pendingModelLoads;
    private int _loadGeneration;
    private RenderTexture _renderTexture;
    private Material _attackerMaterial;
    private Material _defenderMaterial;
    private Material _compositeMaterial;
    private Vector3 _focus;
    private float _yawDegrees;
    private float _pitchDegrees = DefaultPitchDegrees;
    private float _distance = 120f;
    private float _distanceLimit = MaximumDistance;
    private Vector2 _panLimit = new Vector2(1000f, 1000f);
    private bool _viewInitialized;
    private int _arenaHeight = 1;

    /// <summary>
    /// Gets the number of units currently presented.
    /// </summary>
    internal int UnitCount => _units.Count;

    /// <summary>
    /// Gets the camera that renders the scene.
    /// </summary>
    internal Camera SceneCamera => sceneCamera;

    /// <summary>
    /// Gets the battle position the camera orbits around.
    /// </summary>
    internal Vector2 Focus => new Vector2(_focus.x, _focus.z);

    /// <summary>
    /// Gets the camera's rotation around the vertical axis, in degrees.
    /// </summary>
    internal float YawDegrees => _yawDegrees;

    /// <summary>
    /// Gets the camera's tilt below the horizon, in degrees.
    /// </summary>
    internal float PitchDegrees => _pitchDegrees;

    /// <summary>
    /// Gets the camera's distance from its focus, in battle units.
    /// </summary>
    internal float Distance => _distance;

    /// <summary>
    /// Gets the farthest the camera may back away, which frames the whole play space.
    /// </summary>
    internal float DistanceLimit => _distanceLimit;

    /// <summary>
    /// Gets whether any unit's model is still loading, so the battle can hold at its opening
    /// positions instead of starting on placeholders.
    /// </summary>
    internal bool HasPendingModelLoads => _pendingModelLoads > 0;

    /// <summary>
    /// Gets the number of selection boxes currently drawn.
    /// </summary>
    internal int SelectionBoxCount
    {
        get
        {
            int count = 0;
            foreach (SelectionBox box in _selectionBoxes)
            {
                if (box.Unit != null)
                    count++;
            }

            return count;
        }
    }

    /// <summary>
    /// Gets whether any explosion is still burning; the debug automation screenshots it.
    /// </summary>
    public bool HasActiveExplosions
    {
        get
        {
            foreach (ExplosionEffect effect in _explosions)
            {
                if (effect.IsAlive)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets whether any unit is shown held in a tractor beam; the debug automation screenshots
    /// it.
    /// </summary>
    public bool HasActiveTractorEffects
    {
        get
        {
            foreach (UnitPresentation presentation in _units.Values)
            {
                if (presentation.TractorActive)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets whether any ship on show is drawn projecting a gravity well; the debug automation
    /// screenshots it.
    /// </summary>
    public bool HasActiveGravityWellEffects
    {
        get
        {
            foreach (UnitPresentation presentation in _units.Values)
            {
                if (IsShowingGravityWell(presentation))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets whether a superlaser beam is being drawn; the debug automation screenshots it.
    /// </summary>
    public bool HasActiveSuperlaserBeams
    {
        get
        {
            foreach (SuperlaserBeam beam in _beams)
            {
                if (beam.Active)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets whether a battle station's model is on show; the debug automation watches for it to
    /// go.
    /// </summary>
    public bool HasVisibleStation
    {
        get
        {
            foreach (UnitPresentation presentation in _stations.Values)
            {
                if (presentation.Holder?.gameObject.activeSelf == true)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets the number of battle stations presented, whether or not they are still shown.
    /// </summary>
    internal int StationCount => _stations.Count;

    /// <summary>
    /// Returns the content address of a unit's 3D model, derived from its display image folder.
    /// </summary>
    /// <param name="unit">The unit whose model is wanted.</param>
    /// <returns>The model address, or null when the unit has no display image folder.</returns>
    internal static string GetModelAddress(TacticalBattleUnit unit)
    {
        return GetNodeModelAddress(unit?.Node);
    }

    /// <summary>
    /// Returns the content address of a game unit's 3D model, derived from its display image
    /// folder. It takes the strategic node so models can be preloaded before a battle exists.
    /// </summary>
    /// <param name="node">The capital ship or squadron whose model is wanted.</param>
    /// <returns>The model address, or null when the node has no display image folder.</returns>
    internal static string GetNodeModelAddress(ISceneNode node)
    {
        string imagePath = node?.GetDisplayImagePath();
        if (string.IsNullOrEmpty(imagePath))
            return null;

        int separator = imagePath.LastIndexOf('/');
        if (separator <= 0)
            return null;

        return imagePath.Substring(0, separator) + ModelAddressSuffix;
    }

    /// <summary>
    /// Returns the scene diameter a unit's model is scaled to. Capital ships are drawn well
    /// above their simulation radius so they read at battle distances; fighters stay small.
    /// </summary>
    /// <param name="unit">The unit to size.</param>
    /// <returns>The model diameter in scene units.</returns>
    internal static float GetModelDiameter(TacticalBattleUnit unit)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        float scale = unit.IsStarfighter ? FighterModelDiameterScale : ShipModelDiameterScale;
        return (float)unit.Radius * 2f * scale;
    }

    /// <summary>
    /// Converts a battle position into a scene position: on the battle plane, or at a height
    /// above it.
    /// </summary>
    /// <param name="x">The battle horizontal coordinate.</param>
    /// <param name="y">The battle vertical coordinate.</param>
    /// <param name="altitude">The height above the battle plane.</param>
    /// <returns>The local scene position.</returns>
    internal static Vector3 ToScenePosition(double x, double y, double altitude = 0)
    {
        return new Vector3((float)x, (float)altitude, (float)y);
    }

    /// <summary>
    /// Converts a unit's battle position and height into a scene position.
    /// </summary>
    /// <param name="unit">The unit to place.</param>
    /// <returns>The local scene position.</returns>
    internal static Vector3 ToScenePosition(TacticalBattleUnit unit)
    {
        return ToScenePosition(unit.X, unit.Y, unit.Altitude);
    }

    /// <summary>
    /// Converts a battle heading into a scene rotation whose forward lies on the ground plane.
    /// </summary>
    /// <param name="heading">The battle heading in radians.</param>
    /// <returns>The local scene rotation.</returns>
    internal static Quaternion ToSceneRotation(double heading)
    {
        Vector3 forward = new Vector3((float)Math.Cos(heading), 0f, (float)Math.Sin(heading));
        return Quaternion.LookRotation(forward, Vector3.up);
    }

    /// <summary>
    /// Prepares the scene for a new battle, discarding any previous presentation. The camera is
    /// framed on the battle during the first <see cref="Sync"/>.
    /// </summary>
    /// <param name="modelCache">The model cache to load unit models from, or null for placeholders only.</param>
    internal void Begin(ContentModelCache modelCache)
    {
        VerifyReferences();
        End();
        _modelCache = modelCache;
        _loading = new CancellationTokenSource();
        _viewInitialized = false;
        SetVisible(true);
    }

    /// <summary>
    /// Shows or hides the scene without discarding its units.
    /// </summary>
    /// <param name="visible">Whether the scene renders.</param>
    internal void SetVisible(bool visible)
    {
        VerifyReferences();
        gameObject.SetActive(visible);
        sceneCamera.enabled = visible;
    }

    /// <summary>
    /// Matches the render target to the arena, applies the camera, and poses every unit at its
    /// battle position.
    /// </summary>
    /// <param name="battle">The battle to present.</param>
    /// <param name="arena">The source-space arena bounds.</param>
    internal void Sync(TacticalSpaceBattle battle, RectInt arena)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        VerifyReferences();
        _loading ??= new CancellationTokenSource();
        EnsureRenderTarget(arena);
        _arenaHeight = Mathf.Max(arena.height, 1);
        bool opening = !_viewInitialized;
        if (opening)
            ResetView(battle);
        ApplyCamera();
        foreach (TacticalBattleUnit unit in battle.Units)
        {
            if (!_units.TryGetValue(unit, out UnitPresentation presentation))
            {
                presentation = CreatePresentation(unit);
                _units.Add(unit, presentation);
                // The attacking fleet drops out of hyperspace as the battle opens.
                if (
                    opening
                    && unit.Side == CombatSide.Attacker
                    && unit.IsAlive
                    && !unit.HasWithdrawn
                )
                    StartJump(presentation, JumpPhase.Arriving, unit);
            }

            bool departing = presentation.Jump == JumpPhase.Departing;
            bool visible = unit.IsAlive && !unit.IsOffField && (!unit.HasWithdrawn || departing);
            if (presentation.Holder.gameObject.activeSelf != visible)
                presentation.Holder.gameObject.SetActive(visible);
            if (!visible)
                continue;

            PoseHolder(unit, presentation);
            UpdateTractorEffect(unit, presentation);
            UpdateGravityWellEffect(unit, presentation);
        }

        foreach (TacticalBattleStation station in battle.Stations)
        {
            if (!_stations.TryGetValue(station, out UnitPresentation presentation))
            {
                presentation = CreateStationPresentation(station);
                _stations.Add(station, presentation);
            }

            if (presentation.Holder.gameObject.activeSelf != station.IsInBattle)
                presentation.Holder.gameObject.SetActive(station.IsInBattle);
        }

        SyncSuperlaserBeams();
        SyncTorpedoImpacts(battle.Shots);
    }

    /// <summary>
    /// Advances the hyperspace arrival and departure animations by elapsed real time.
    /// </summary>
    /// <param name="deltaTime">The elapsed seconds.</param>
    internal void Advance(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        foreach (UnitPresentation presentation in _units.Values)
        {
            if (presentation.Jump != JumpPhase.None)
                presentation.JumpElapsed += deltaTime;
        }

        foreach (SuperlaserBeam beam in _beams)
        {
            if (beam.Active)
                beam.Elapsed += deltaTime;
        }
    }

    /// <summary>
    /// Returns whether a unit is still flying its hyperspace arrival or departure.
    /// </summary>
    /// <param name="unit">The unit to inspect.</param>
    /// <returns>True while the unit's jump animation runs.</returns>
    internal bool IsJumping(TacticalBattleUnit unit)
    {
        return unit != null
            && _units.TryGetValue(unit, out UnitPresentation presentation)
            && presentation.Jump != JumpPhase.None;
    }

    /// <summary>
    /// Draws a wireframe box around each listed unit that is on show, in the color of the side
    /// it fights for, and hides the boxes left over from the previous frame. Call it after
    /// <see cref="Sync"/>, which places the hulls the boxes wrap.
    /// </summary>
    /// <param name="units">The units to box, without repeats; null boxes none.</param>
    /// <param name="attackerColor">The attacking side's color.</param>
    /// <param name="defenderColor">The defending side's color.</param>
    internal void ShowSelection(
        IReadOnlyList<TacticalBattleUnit> units,
        Color attackerColor,
        Color defenderColor
    )
    {
        VerifyReferences();
        int used = 0;
        int count = units?.Count ?? 0;
        for (int index = 0; index < count; index++)
        {
            TacticalBattleUnit unit = units[index];
            if (
                unit == null
                || !_units.TryGetValue(unit, out UnitPresentation presentation)
                || presentation.Holder?.gameObject.activeSelf != true
            )
                continue;

            if (used == _selectionBoxes.Count)
                _selectionBoxes.Add(CreateSelectionBox());
            SelectionBox box = _selectionBoxes[used];
            used++;
            Color color = unit.Side == CombatSide.Attacker ? attackerColor : defenderColor;
            color.a = 1f;
            box.Unit = unit;
            if (box.Material == null || box.Color != color)
            {
                box.Color = color;
                box.Material = GetSelectionMaterial(color);
                foreach (MeshRenderer edge in box.Edges)
                    edge.sharedMaterial = box.Material;
            }

            PoseSelectionBox(box, presentation);
            box.Root.gameObject.SetActive(true);
        }

        for (int index = used; index < _selectionBoxes.Count; index++)
        {
            _selectionBoxes[index].Unit = null;
            _selectionBoxes[index].Root.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Marks every capital ship that stands above or below the battle plane with a thin stem
    /// dropped to the plane and a small foot where it lands, in a dim shade of its side's
    /// color, so the player can tell a ship that is higher from one that is farther away. Call
    /// it after <see cref="Sync"/>, which places the hulls.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="attackerColor">The attacking side's color.</param>
    /// <param name="defenderColor">The defending side's color.</param>
    internal void ShowDeckMarkers(
        TacticalSpaceBattle battle,
        Color attackerColor,
        Color defenderColor
    )
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        VerifyReferences();
        int used = 0;
        foreach (TacticalBattleUnit unit in battle.Units)
        {
            // A ship flying its hyperspace jump is not at its place yet and gets no marker.
            if (
                unit.IsStarfighter
                || Math.Abs(unit.Altitude) < _deckMarkerMinimumHeight
                || !_units.TryGetValue(unit, out UnitPresentation presentation)
                || presentation.Holder?.gameObject.activeSelf != true
                || presentation.Jump != JumpPhase.None
            )
                continue;

            if (used == _deckMarkers.Count)
                _deckMarkers.Add(CreateDeckMarker());
            DeckMarker marker = _deckMarkers[used];
            used++;
            Color side = unit.Side == CombatSide.Attacker ? attackerColor : defenderColor;
            Color color = new Color(
                side.r * _deckMarkerDim,
                side.g * _deckMarkerDim,
                side.b * _deckMarkerDim,
                1f
            );
            marker.Unit = unit;
            if (marker.Material == null || marker.Color != color)
            {
                marker.Color = color;
                marker.Material = GetSelectionMaterial(color);
                marker.Stem.sharedMaterial = marker.Material;
                marker.Foot.sharedMaterial = marker.Material;
            }

            Vector3 top = presentation.Holder.localPosition;
            Vector3 foot = new Vector3(top.x, 0f, top.z);
            float width = GetLineWidth(modelRoot.TransformPoint(foot), _deckMarkerLineWidth);
            float footSize = presentation.Diameter * _deckMarkerFootScale;
            marker.Stem.transform.localPosition = (top + foot) * 0.5f;
            marker.Stem.transform.localScale = new Vector3(width, Mathf.Abs(top.y), width);
            marker.Foot.transform.localPosition = foot;
            marker.Foot.transform.localRotation = presentation.Holder.localRotation;
            marker.Foot.transform.localScale = new Vector3(footSize, width, footSize);
            marker.Root.gameObject.SetActive(true);
        }

        for (int index = used; index < _deckMarkers.Count; index++)
        {
            _deckMarkers[index].Unit = null;
            _deckMarkers[index].Root.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Returns the deck marker drawn under or over a unit.
    /// </summary>
    /// <param name="unit">The unit to find.</param>
    /// <param name="stem">Receives the stem between the hull and the battle plane.</param>
    /// <param name="foot">Receives the foot on the battle plane.</param>
    /// <returns>True when the unit is marked.</returns>
    internal bool TryGetDeckMarker(TacticalBattleUnit unit, out Transform stem, out Transform foot)
    {
        stem = null;
        foot = null;
        if (unit == null)
            return false;

        foreach (DeckMarker candidate in _deckMarkers)
        {
            if (candidate.Unit != unit)
                continue;

            stem = candidate.Stem.transform;
            foot = candidate.Foot.transform;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the selection box drawn around a unit.
    /// </summary>
    /// <param name="unit">The unit to find.</param>
    /// <param name="box">Receives the box's root transform, whose children are its edges.</param>
    /// <param name="color">Receives the box's color.</param>
    /// <returns>True when the unit is boxed.</returns>
    internal bool TryGetSelectionBox(TacticalBattleUnit unit, out Transform box, out Color color)
    {
        box = null;
        color = default;
        if (unit == null)
            return false;

        foreach (SelectionBox candidate in _selectionBoxes)
        {
            if (candidate.Unit != unit)
                continue;

            box = candidate.Root;
            color = candidate.Color;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Plays the scene effects for one update's worth of battle events: a burst of fire and
    /// debris where each unit was destroyed.
    /// </summary>
    /// <param name="events">The events raised since the previous update.</param>
    internal void Handle(IReadOnlyList<TacticalBattleEvent> events)
    {
        if (events == null || events.Count == 0 || modelRoot == null)
            return;

        foreach (TacticalBattleEvent battleEvent in events)
        {
            switch (battleEvent.Kind)
            {
                case TacticalBattleEventKind.UnitDestroyed:
                    PlayExplosion(battleEvent.Unit);
                    break;
                case TacticalBattleEventKind.UnitWithdrew:
                    StartDeparture(battleEvent.Unit);
                    break;
                case TacticalBattleEventKind.SuperlaserFired:
                    StartSuperlaserBeam(battleEvent.Station, battleEvent.Unit);
                    break;
                case TacticalBattleEventKind.StationDestroyed:
                    PlayStationExplosion(battleEvent.Station);
                    break;
            }
        }
    }

    /// <summary>
    /// Returns whether a unit is currently shown held in a tractor beam.
    /// </summary>
    /// <param name="unit">The unit to inspect.</param>
    /// <returns>True when the unit's tractor effect is active.</returns>
    internal bool HasTractorEffect(TacticalBattleUnit unit)
    {
        return unit != null
            && _units.TryGetValue(unit, out UnitPresentation presentation)
            && presentation.TractorActive;
    }

    /// <summary>
    /// Returns whether a unit is currently shown projecting a gravity well.
    /// </summary>
    /// <param name="unit">The unit to inspect.</param>
    /// <returns>True when the unit is on show with its gravity-well effect running.</returns>
    internal bool HasGravityWellEffect(TacticalBattleUnit unit)
    {
        return unit != null
            && _units.TryGetValue(unit, out UnitPresentation presentation)
            && IsShowingGravityWell(presentation);
    }

    /// <summary>
    /// Enumerates the rig-local positions of the explosions still burning.
    /// </summary>
    /// <returns>One position per live explosion.</returns>
    internal IEnumerable<Vector3> GetActiveExplosionPositions()
    {
        foreach (ExplosionEffect effect in _explosions)
        {
            if (effect.IsAlive)
                yield return effect.Root.localPosition;
        }
    }

    /// <summary>
    /// Locks the camera to the battle's play space and frames both fleets, looking from behind
    /// the player's side of the battle.
    /// </summary>
    /// <param name="battle">The battle to frame.</param>
    internal void ResetView(TacticalSpaceBattle battle)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        VerifyReferences();
        _panLimit = new Vector2((float)battle.HalfWidth, (float)battle.HalfHeight);
        float arenaRadius = Mathf.Sqrt(_panLimit.x * _panLimit.x + _panLimit.y * _panLimit.y);
        // A battle station stands off beyond the arena; let the camera back out far enough to
        // take it in along with the battlefield.
        float viewRadius = arenaRadius;
        foreach (TacticalBattleStation station in battle.Stations)
        {
            viewRadius = Mathf.Max(
                viewRadius,
                ToScenePosition(station.X, station.Y).magnitude + (float)station.Radius
            );
        }

        _distanceLimit = Mathf.Clamp(
            GetFramingDistance(viewRadius, _arenaFramingPadding),
            MinimumDistance,
            MaximumDistance
        );

        // Frame every unit so both fleets open on screen; look along the player's fleet.
        List<TacticalBattleUnit> framed = new List<TacticalBattleUnit>();
        List<TacticalBattleUnit> playerUnits = new List<TacticalBattleUnit>();
        foreach (TacticalBattleUnit unit in battle.Units)
        {
            if (!unit.IsAlive || unit.HasWithdrawn || unit.IsOffField)
                continue;
            framed.Add(unit);
            if (battle.PlayerSide != null && unit.Side == battle.PlayerSide.Value)
                playerUnits.Add(unit);
        }

        _jumpDistance = _jumpDistanceScale * 2f * arenaRadius;
        Vector3 centroid = Vector3.zero;
        foreach (TacticalBattleUnit unit in framed)
            centroid += ToScenePosition(unit.X, unit.Y);
        if (framed.Count > 0)
            centroid /= framed.Count;

        float radius = 0f;
        foreach (TacticalBattleUnit unit in framed)
        {
            float reach =
                Vector3.Distance(centroid, ToScenePosition(unit.X, unit.Y))
                + GetModelDiameter(unit);
            radius = Mathf.Max(radius, reach);
        }

        _focus = ClampFocus(centroid);
        _yawDegrees = playerUnits.Count == 0 ? 0f : GetYawBehind(playerUnits);
        _pitchDegrees = DefaultPitchDegrees;
        _distance = ClampDistance(GetFramingDistance(radius, _fleetFramingPadding));
        _viewInitialized = true;
        ApplyCamera();
    }

    /// <summary>
    /// Returns the camera yaw that looks along a fleet's average heading, from behind it.
    /// </summary>
    /// <param name="units">The fleet's units.</param>
    /// <returns>The yaw in degrees.</returns>
    private static float GetYawBehind(IReadOnlyList<TacticalBattleUnit> units)
    {
        double sin = 0;
        double cos = 0;
        foreach (TacticalBattleUnit unit in units)
        {
            sin += Math.Sin(unit.Heading);
            cos += Math.Cos(unit.Heading);
        }

        if (sin == 0 && cos == 0)
            return 90f;

        // A ship with heading h faces scene direction (cos h, 0, sin h); camera yaw y looks
        // along (sin y, 0, cos y), so the two agree when y = 90 - h.
        return 90f - (float)(Math.Atan2(sin, cos) * Mathf.Rad2Deg);
    }

    /// <summary>
    /// Backs the camera out to the play-space limit over the center of the arena, so every unit
    /// and effect is on screen; the debug automation frames its effect screenshots this way.
    /// </summary>
    public void FrameArena()
    {
        VerifyReferences();
        _focus = Vector3.zero;
        _distance = _distanceLimit;
        ApplyCamera();
    }

    /// <summary>
    /// Turns the camera to a battle station beyond the arena's edge: the focus goes to the edge
    /// nearest the station and the camera looks out across it, low enough to take the station in.
    /// </summary>
    /// <param name="station">The station to look at.</param>
    internal void LookToward(TacticalBattleStation station)
    {
        if (station == null)
            throw new ArgumentNullException(nameof(station));

        VerifyReferences();
        Vector3 position = ToScenePosition(station.X, station.Y);
        _focus = ClampFocus(position);
        Vector3 toStation = position - _focus;
        if (toStation.sqrMagnitude > 0.0001f)
            _yawDegrees = Mathf.Atan2(toStation.x, toStation.z) * Mathf.Rad2Deg;
        _pitchDegrees = _stationViewPitchDegrees;
        _distance = ClampDistance((float)station.Radius * _stationViewDistanceRadii);
        _viewInitialized = true;
        ApplyCamera();
    }

    /// <summary>
    /// Turns the camera to the first battle station presented; the debug automation frames its
    /// station screenshots this way.
    /// </summary>
    /// <returns>True when a station was framed.</returns>
    public bool FrameStation()
    {
        foreach (TacticalBattleStation station in _stations.Keys)
        {
            LookToward(station);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Closes the camera in on the first ship shown projecting a gravity well; the debug
    /// automation frames its screenshot of the effect this way.
    /// </summary>
    /// <returns>True when such a ship was framed.</returns>
    public bool FrameGravityWell()
    {
        foreach (KeyValuePair<TacticalBattleUnit, UnitPresentation> entry in _units)
        {
            if (!IsShowingGravityWell(entry.Value))
                continue;

            FocusOn(entry.Key);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Moves the camera focus to a unit and closes in on it.
    /// </summary>
    /// <param name="unit">The unit to focus on.</param>
    internal void FocusOn(TacticalBattleUnit unit)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        VerifyReferences();
        _focus = ClampFocus(ToScenePosition(unit));
        _distance = ClampDistance(GetModelDiameter(unit) * _focusDistanceDiameters);
        _viewInitialized = true;
        ApplyCamera();
    }

    /// <summary>
    /// Centers the camera on a unit without changing its distance or angle, so a followed unit
    /// stays in view as it moves.
    /// </summary>
    /// <param name="unit">The unit to keep centered.</param>
    internal void TrackUnit(TacticalBattleUnit unit)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        VerifyReferences();
        _focus = ClampFocus(ToScenePosition(unit));
        ApplyCamera();
    }

    /// <summary>
    /// Moves the camera toward or away from its focus.
    /// </summary>
    /// <param name="steps">Positive steps zoom in; negative steps zoom out.</param>
    internal void Zoom(float steps)
    {
        VerifyReferences();
        _distance = ClampDistance(_distance * Mathf.Pow(_zoomStepFactor, steps));
        ApplyCamera();
    }

    /// <summary>
    /// Orbits the view around its focus: turns it and tilts it between a low angle and nearly
    /// top-down, while the focus stays on the battle plane.
    /// </summary>
    /// <param name="yawDegrees">The turn to add, in degrees.</param>
    /// <param name="pitchDegrees">The tilt to add toward top-down, in degrees.</param>
    internal void Rotate(float yawDegrees, float pitchDegrees)
    {
        VerifyReferences();
        _yawDegrees = Mathf.Repeat(_yawDegrees + yawDegrees, 360f);
        _pitchDegrees = Mathf.Clamp(
            _pitchDegrees + pitchDegrees,
            MinimumPitchDegrees,
            MaximumPitchDegrees
        );
        ApplyCamera();
    }

    /// <summary>
    /// Slides the focus so the ground point under the pointer follows a drag, as far as the
    /// play space allows.
    /// </summary>
    /// <param name="arena">The source-space arena bounds.</param>
    /// <param name="fromSource">The source-space point where the drag was.</param>
    /// <param name="toSource">The source-space point where the drag is now.</param>
    internal void Pan(RectInt arena, Vector2 fromSource, Vector2 toSource)
    {
        VerifyReferences();
        if (
            !TryPickGround(arena, fromSource, out Vector2 from)
            || !TryPickGround(arena, toSource, out Vector2 to)
        )
            return;

        Vector2 delta = to - from;
        SetFocus(_focus - new Vector3(delta.x, 0f, delta.y));
    }

    /// <summary>
    /// Slides the focus across the ground plane relative to the camera's facing, scaled by the
    /// camera distance so panning covers similar screen distances at any zoom.
    /// </summary>
    /// <param name="screenDelta">
    /// The pan amount in camera-right and camera-forward fractions of the view.
    /// </param>
    internal void PanBy(Vector2 screenDelta)
    {
        VerifyReferences();
        Quaternion rotation = Quaternion.Euler(0f, _yawDegrees, 0f);
        Vector3 right = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;
        SetFocus(_focus + (right * screenDelta.x + forward * screenDelta.y) * _distance);
    }

    /// <summary>
    /// Projects a battle position into source space through the scene camera.
    /// </summary>
    /// <param name="x">The battle horizontal coordinate.</param>
    /// <param name="y">The battle vertical coordinate.</param>
    /// <param name="arena">The source-space arena bounds.</param>
    /// <param name="sourcePoint">Receives the source-space point.</param>
    /// <param name="altitude">The position's height above the battle plane.</param>
    /// <returns>True when the position lies in front of the camera.</returns>
    internal bool TryProject(
        double x,
        double y,
        RectInt arena,
        out Vector2 sourcePoint,
        double altitude = 0
    )
    {
        VerifyReferences();
        return TryProjectWorld(
            modelRoot.TransformPoint(ToScenePosition(x, y, altitude)),
            arena,
            out sourcePoint
        );
    }

    /// <summary>
    /// Projects a unit's position and on-screen model diameter through the scene camera.
    /// </summary>
    /// <param name="unit">The unit to project.</param>
    /// <param name="arena">The source-space arena bounds.</param>
    /// <param name="sourcePoint">Receives the source-space center.</param>
    /// <param name="diameter">Receives the source-space model diameter.</param>
    /// <returns>True when the unit is in front of the camera and within the arena.</returns>
    internal bool TryProjectUnit(
        TacticalBattleUnit unit,
        RectInt arena,
        out Vector2 sourcePoint,
        out float diameter
    )
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        VerifyReferences();
        diameter = 0f;
        // Follow the holder while a hyperspace jump carries it away from its battle position.
        Vector3 local =
            _units.TryGetValue(unit, out UnitPresentation presentation)
            && presentation.Holder?.gameObject.activeSelf == true
                ? presentation.Holder.localPosition
                : ToScenePosition(unit);
        Vector3 center = modelRoot.TransformPoint(local);
        if (!TryProjectWorld(center, arena, out sourcePoint))
            return false;

        float margin = _viewportMargin * arena.width;
        if (
            sourcePoint.x < arena.x - margin
            || sourcePoint.x > arena.xMax + margin
            || sourcePoint.y < arena.y - margin
            || sourcePoint.y > arena.yMax + margin
        )
            return false;

        Vector3 edge = center + sceneCamera.transform.right * (GetModelDiameter(unit) * 0.5f);
        if (!TryProjectWorld(edge, arena, out Vector2 edgePoint))
            return false;

        diameter = Mathf.Abs(edgePoint.x - sourcePoint.x) * 2f;
        return true;
    }

    /// <summary>
    /// Projects a battle station's position and on-screen diameter through the scene camera.
    /// Unlike a unit, a station is projected wherever it falls, since it stands beyond the arena.
    /// </summary>
    /// <param name="station">The station to project.</param>
    /// <param name="arena">The source-space arena bounds.</param>
    /// <param name="sourcePoint">Receives the source-space center.</param>
    /// <param name="diameter">Receives the source-space diameter.</param>
    /// <returns>True when the station is in front of the camera.</returns>
    internal bool TryProjectStation(
        TacticalBattleStation station,
        RectInt arena,
        out Vector2 sourcePoint,
        out float diameter
    )
    {
        if (station == null)
            throw new ArgumentNullException(nameof(station));

        VerifyReferences();
        diameter = 0f;
        Vector3 center = modelRoot.TransformPoint(ToScenePosition(station.X, station.Y));
        if (!TryProjectWorld(center, arena, out sourcePoint))
            return false;

        Vector3 edge = center + sceneCamera.transform.right * (float)station.Radius;
        if (!TryProjectWorld(edge, arena, out Vector2 edgePoint))
            return false;

        diameter = Mathf.Abs(edgePoint.x - sourcePoint.x) * 2f;
        return true;
    }

    /// <summary>
    /// Finds the battle position under a source-space point by casting onto the battle plane,
    /// or onto a deck at a height above it. Casting onto a ship's own deck gives the position
    /// that puts the ship itself under the pointer.
    /// </summary>
    /// <param name="arena">The source-space arena bounds.</param>
    /// <param name="sourcePoint">The source-space point.</param>
    /// <param name="battlePoint">Receives the battle position.</param>
    /// <param name="altitude">The height of the deck to cast onto.</param>
    /// <returns>True when the point looks down onto that deck.</returns>
    internal bool TryPickGround(
        RectInt arena,
        Vector2 sourcePoint,
        out Vector2 battlePoint,
        double altitude = 0
    )
    {
        VerifyReferences();
        battlePoint = default;
        if (arena.width <= 0 || arena.height <= 0)
            return false;

        Ray ray = sceneCamera.ViewportPointToRay(
            new Vector3(
                (sourcePoint.x - arena.x) / arena.width,
                1f - (sourcePoint.y - arena.y) / arena.height,
                0f
            )
        );
        Plane ground = new Plane(
            modelRoot.up,
            modelRoot.TransformPoint(new Vector3(0f, (float)altitude, 0f))
        );
        if (!ground.Raycast(ray, out float distance))
            return false;

        Vector3 local = modelRoot.InverseTransformPoint(ray.GetPoint(distance));
        battlePoint = new Vector2(local.x, local.z);
        return true;
    }

    /// <summary>
    /// Returns the scene transform presenting a unit.
    /// </summary>
    /// <param name="unit">The unit to find.</param>
    /// <param name="holder">Receives the unit's scene transform.</param>
    /// <returns>True when the unit is presented.</returns>
    internal bool TryGetUnitTransform(TacticalBattleUnit unit, out Transform holder)
    {
        holder = null;
        if (unit == null || !_units.TryGetValue(unit, out UnitPresentation presentation))
            return false;

        holder = presentation.Holder;
        return holder != null;
    }

    /// <summary>
    /// Discards every unit, cancels pending model loads, and stops rendering.
    /// </summary>
    internal void End()
    {
        ReleaseUnits();
        _torpedoesInFlight.Clear();
        if (sceneCamera != null)
            sceneCamera.enabled = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Writes the arena's current render target to a PNG and logs its alpha coverage, for
    /// debugging what the scene camera actually drew.
    /// </summary>
    /// <param name="path">The PNG path to write.</param>
    /// <returns>True when a render target existed and was written.</returns>
    public bool TryWriteDebugSnapshot(string path)
    {
        if (_renderTexture == null || string.IsNullOrEmpty(path))
            return false;

        RenderTexture previous = RenderTexture.active;
        Texture2D readback = new Texture2D(
            _renderTexture.width,
            _renderTexture.height,
            TextureFormat.RGBAFloat,
            false
        );
        try
        {
            RenderTexture.active = _renderTexture;
            readback.ReadPixels(new Rect(0, 0, _renderTexture.width, _renderTexture.height), 0, 0);
            readback.Apply();
            Color[] pixels = readback.GetPixels();
            int covered = 0;
            int lit = 0;
            float maxAlpha = 0f;
            float maxLuminance = 0f;
            foreach (Color pixel in pixels)
            {
                if (pixel.a > 0.05f)
                    covered++;
                float luminance = Mathf.Max(pixel.r, pixel.g, pixel.b);
                if (luminance > 0.02f)
                    lit++;
                maxAlpha = Mathf.Max(maxAlpha, pixel.a);
                maxLuminance = Mathf.Max(maxLuminance, luminance);
            }

            Debug.Log(
                $"Tactical battle snapshot {path}: {_renderTexture.width}x{_renderTexture.height} coveredAlpha={covered} litPixels={lit} maxAlpha={maxAlpha} maxLuminance={maxLuminance}"
            );
            Texture2D encoded = new Texture2D(
                _renderTexture.width,
                _renderTexture.height,
                TextureFormat.RGBA32,
                false
            );
            Color[] display = new Color[pixels.Length];
            for (int index = 0; index < pixels.Length; index++)
            {
                Color pixel = pixels[index];
                display[index] = new Color(
                    Mathf.Clamp01(pixel.r),
                    Mathf.Clamp01(pixel.g),
                    Mathf.Clamp01(pixel.b),
                    1f
                );
            }

            encoded.SetPixels(display);
            encoded.Apply();
            System.IO.File.WriteAllBytes(path, encoded.EncodeToPNG());
            DestroyObject(encoded);
            return true;
        }
        finally
        {
            RenderTexture.active = previous;
            DestroyObject(readback);
        }
    }

    /// <summary>
    /// Releases scene resources with the component.
    /// </summary>
    private void OnDestroy()
    {
        ReleaseUnits();
        DestroyMaterial(ref _attackerMaterial);
        DestroyMaterial(ref _defenderMaterial);
        DestroyMaterial(ref _compositeMaterial);
        DestroyMaterial(ref _flashMaterial);
        DestroyMaterial(ref _fireMaterial);
        DestroyMaterial(ref _smokeMaterial);
        DestroyMaterial(ref _tractorMaterial);
        DestroyMaterial(ref _gravityWellMaterial);
        DestroyMaterial(ref _superlaserMaterial);
        foreach (Material material in _selectionMaterials.Values)
        {
            if (material != null)
                DestroyObject(material);
        }

        _selectionMaterials.Clear();
        if (_particleTexture != null)
            DestroyObject(_particleTexture);
        _particleTexture = null;
    }

    /// <summary>
    /// Destroys a material the scene created and clears the reference.
    /// </summary>
    /// <param name="material">The material reference.</param>
    private static void DestroyMaterial(ref Material material)
    {
        if (material != null)
            DestroyObject(material);
        material = null;
    }

    /// <summary>
    /// Cancels pending model loads, destroys every unit, and releases the render target.
    /// </summary>
    private void ReleaseUnits()
    {
        _loading?.Cancel();
        _loading?.Dispose();
        _loading = null;
        _pendingModelLoads = 0;
        _loadGeneration++;
        foreach (UnitPresentation presentation in _units.Values)
        {
            presentation.Model?.Dispose();
            if (presentation.Holder != null)
                DestroyObject(presentation.Holder.gameObject);
        }

        _units.Clear();
        foreach (UnitPresentation presentation in _stations.Values)
        {
            presentation.Model?.Dispose();
            if (presentation.Holder != null)
                DestroyObject(presentation.Holder.gameObject);
        }

        _stations.Clear();
        foreach (SuperlaserBeam beam in _beams)
        {
            if (beam.Root != null)
                DestroyObject(beam.Root.gameObject);
        }

        _beams.Clear();
        foreach (SelectionBox box in _selectionBoxes)
        {
            if (box.Root != null)
                DestroyObject(box.Root.gameObject);
        }

        _selectionBoxes.Clear();
        foreach (DeckMarker marker in _deckMarkers)
        {
            if (marker.Root != null)
                DestroyObject(marker.Root.gameObject);
        }

        _deckMarkers.Clear();
        foreach (ExplosionEffect effect in _explosions)
        {
            if (effect.Root != null)
                DestroyObject(effect.Root.gameObject);
        }

        _explosions.Clear();
        foreach (Material material in _replacedMaterials)
        {
            if (material != null)
                DestroyObject(material);
        }

        _replacedMaterials.Clear();
        _modelCache = null;
        _viewInitialized = false;
        ReleaseRenderTarget();
    }

    /// <summary>
    /// Projects a world position into source space through the scene camera.
    /// </summary>
    /// <param name="world">The world position.</param>
    /// <param name="arena">The source-space arena bounds.</param>
    /// <param name="sourcePoint">Receives the source-space point.</param>
    /// <returns>True when the position lies in front of the camera.</returns>
    private bool TryProjectWorld(Vector3 world, RectInt arena, out Vector2 sourcePoint)
    {
        Vector3 viewport = sceneCamera.WorldToViewportPoint(world);
        sourcePoint = new Vector2(
            arena.x + viewport.x * arena.width,
            arena.y + (1f - viewport.y) * arena.height
        );
        return viewport.z > 0f;
    }

    /// <summary>
    /// Returns the camera distance that frames a radius with padding at the vertical field of view.
    /// </summary>
    /// <param name="radius">The radius to frame, in scene units.</param>
    /// <param name="padding">The multiplier applied to the radius.</param>
    /// <returns>The framing distance.</returns>
    private static float GetFramingDistance(float radius, float padding)
    {
        return radius * padding / Mathf.Tan(FieldOfViewDegrees * 0.5f * Mathf.Deg2Rad);
    }

    /// <summary>
    /// Clamps a focus to the play space and applies it.
    /// </summary>
    /// <param name="focus">The wanted focus in scene space.</param>
    private void SetFocus(Vector3 focus)
    {
        _focus = ClampFocus(focus);
        ApplyCamera();
    }

    /// <summary>
    /// Clamps a focus to the play space, keeping its height: the focus rides at the height of a
    /// unit the camera is on, and on the battle plane otherwise.
    /// </summary>
    /// <param name="focus">The wanted focus in scene space.</param>
    /// <returns>The clamped focus.</returns>
    private Vector3 ClampFocus(Vector3 focus)
    {
        return new Vector3(
            Mathf.Clamp(focus.x, -_panLimit.x, _panLimit.x),
            focus.y,
            Mathf.Clamp(focus.z, -_panLimit.y, _panLimit.y)
        );
    }

    /// <summary>
    /// Clamps a camera distance to the supported range for the current play space.
    /// </summary>
    /// <param name="distance">The wanted distance.</param>
    /// <returns>The clamped distance.</returns>
    private float ClampDistance(float distance)
    {
        if (float.IsNaN(distance) || float.IsInfinity(distance))
            return MinimumDistance;
        return Mathf.Clamp(distance, MinimumDistance, _distanceLimit);
    }

    /// <summary>
    /// Places the perspective camera at its orbit position around the focus.
    /// </summary>
    private void ApplyCamera()
    {
        sceneCamera.orthographic = false;
        sceneCamera.fieldOfView = FieldOfViewDegrees;
        sceneCamera.nearClipPlane = _nearClip;
        sceneCamera.farClipPlane = _farClip;
        Quaternion rotation = Quaternion.Euler(_pitchDegrees, _yawDegrees, 0f);
        sceneCamera.transform.localRotation = rotation;
        sceneCamera.transform.localPosition = _focus - rotation * Vector3.forward * _distance;
    }

    /// <summary>
    /// Creates the render target when missing or when the arena's pixel size changed.
    /// </summary>
    /// <param name="arena">The source-space arena bounds.</param>
    private void EnsureRenderTarget(RectInt arena)
    {
        float scaleFactor = outputImage.canvas != null ? outputImage.canvas.scaleFactor : 1f;
        int width = Mathf.Clamp(Mathf.RoundToInt(arena.width * scaleFactor), 1, _maximumRenderSize);
        int height = Mathf.Clamp(
            Mathf.RoundToInt(arena.height * scaleFactor),
            1,
            _maximumRenderSize
        );
        sceneCamera.aspect = width / (float)height;
        if (
            _renderTexture != null
            && _renderTexture.width == width
            && _renderTexture.height == height
        )
            return;

        ReleaseRenderTarget();
        _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
        {
            name = "Tactical Battle",
            antiAliasing = 1,
            useDynamicScale = false,
            useMipMap = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        _renderTexture.Create();
        sceneCamera.targetTexture = _renderTexture;
        outputImage.texture = _renderTexture;
        outputImage.material = GetCompositeMaterial();
        outputImage.color = Color.white;
        outputImage.enabled = true;
    }

    /// <summary>
    /// Returns the premultiplied-alpha material that composites the render target over the
    /// starfield, the same way the main menu shows its planet render. Falls back to the default
    /// UI material when the shader is unavailable.
    /// </summary>
    /// <returns>The composite material, or null for the default.</returns>
    private Material GetCompositeMaterial()
    {
        if (_compositeMaterial != null)
            return _compositeMaterial;

        Shader shader = Shader.Find(CompositeShaderName);
        if (shader == null)
            return null;

        _compositeMaterial = new Material(shader) { name = "Tactical Arena Composite" };
        return _compositeMaterial;
    }

    /// <summary>
    /// Releases the render target and clears the image that showed it.
    /// </summary>
    private void ReleaseRenderTarget()
    {
        if (sceneCamera != null)
            sceneCamera.targetTexture = null;
        if (outputImage != null)
        {
            outputImage.texture = null;
            outputImage.material = null;
            outputImage.enabled = false;
        }

        if (_renderTexture == null)
            return;

        _renderTexture.Release();
        DestroyObject(_renderTexture);
        _renderTexture = null;
    }

    /// <summary>
    /// Creates a unit's holder with a placeholder hull and starts loading its model.
    /// </summary>
    /// <param name="unit">The unit to present.</param>
    /// <returns>The new presentation.</returns>
    private UnitPresentation CreatePresentation(TacticalBattleUnit unit)
    {
        float diameter = GetModelDiameter(unit);
        return CreatePresentation(
            unit.DisplayName ?? "Unit",
            diameter,
            "Cube.fbx",
            new Vector3(
                diameter * _placeholderWidth,
                diameter * _placeholderHeight,
                diameter * _placeholderLength
            ),
            unit.Side,
            GetModelAddress(unit)
        );
    }

    /// <summary>
    /// Creates a battle station's holder beyond the arena's edge, facing the battlefield, with a
    /// placeholder sphere, and starts loading its model.
    /// </summary>
    /// <param name="station">The station to present.</param>
    /// <returns>The new presentation.</returns>
    private UnitPresentation CreateStationPresentation(TacticalBattleStation station)
    {
        float diameter = (float)station.Radius * 2f;
        UnitPresentation presentation = CreatePresentation(
            station.DisplayName ?? "Station",
            diameter,
            "Sphere.fbx",
            Vector3.one * diameter,
            station.Side,
            GetNodeModelAddress(station.Ship)
        );
        presentation.Holder.localPosition = ToScenePosition(station.X, station.Y);
        presentation.Holder.localRotation = ToSceneRotation(Math.Atan2(-station.Y, -station.X));
        return presentation;
    }

    /// <summary>
    /// Creates a holder with a placeholder shape and starts loading the model that replaces it.
    /// </summary>
    /// <param name="holderName">The holder object's name.</param>
    /// <param name="diameter">The scene diameter the model is scaled to.</param>
    /// <param name="placeholderMesh">The built-in mesh shown until the model loads.</param>
    /// <param name="placeholderScale">The placeholder's scale.</param>
    /// <param name="side">The side whose placeholder color is used.</param>
    /// <param name="address">The model content address, or null for a placeholder only.</param>
    /// <returns>The new presentation.</returns>
    private UnitPresentation CreatePresentation(
        string holderName,
        float diameter,
        string placeholderMesh,
        Vector3 placeholderScale,
        CombatSide side,
        string address
    )
    {
        GameObject holder = new GameObject(holderName);
        holder.layer = PresentationLayers.TacticalBattle;
        holder.transform.SetParent(modelRoot, false);

        GameObject placeholder = new GameObject(
            "Placeholder",
            typeof(MeshFilter),
            typeof(MeshRenderer)
        );
        placeholder.layer = PresentationLayers.TacticalBattle;
        placeholder.transform.SetParent(holder.transform, false);
        placeholder.transform.localScale = placeholderScale;
        placeholder.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(
            placeholderMesh
        );
        placeholder.GetComponent<MeshRenderer>().sharedMaterial = GetPlaceholderMaterial(side);

        // The model gets its own child so fitting measures the mesh alone, never the placeholder.
        GameObject modelParent = new GameObject("Model");
        modelParent.layer = PresentationLayers.TacticalBattle;
        modelParent.transform.SetParent(holder.transform, false);

        UnitPresentation presentation = new UnitPresentation(
            holder.transform,
            modelParent.transform,
            placeholder,
            diameter
        )
        {
            // Until the model loads, the selection box wraps the placeholder hull.
            LocalBounds = new Bounds(Vector3.zero, placeholderScale),
        };
        if (_modelCache != null && !string.IsNullOrEmpty(address))
        {
            _pendingModelLoads++;
            _ = LoadModelAsync(presentation, address, _loadGeneration, _loading.Token);
        }
        return presentation;
    }

    /// <summary>
    /// Flies a unit that has just left the battle out along its heading, accelerating into
    /// hyperspace; squadrons recovered by a carrier simply disappear aboard it.
    /// </summary>
    /// <param name="unit">The unit that withdrew.</param>
    private void StartDeparture(TacticalBattleUnit unit)
    {
        if (
            unit == null
            || !_units.TryGetValue(unit, out UnitPresentation presentation)
            || (unit.IsStarfighter && !unit.CanWithdrawIndependently)
        )
            return;

        StartJump(presentation, JumpPhase.Departing, unit);
        presentation.Holder.gameObject.SetActive(true);
    }

    /// <summary>
    /// Begins a hyperspace animation for a unit from its current battle position and heading.
    /// </summary>
    /// <param name="presentation">The unit's scene objects.</param>
    /// <param name="phase">Whether the unit arrives or departs.</param>
    /// <param name="unit">The unit being animated.</param>
    private static void StartJump(
        UnitPresentation presentation,
        JumpPhase phase,
        TacticalBattleUnit unit
    )
    {
        presentation.Jump = phase;
        presentation.JumpElapsed = 0f;
        presentation.JumpOrigin = ToScenePosition(unit);
        presentation.JumpForward = ToSceneRotation(unit.Heading) * Vector3.forward;
    }

    /// <summary>
    /// Places a unit's holder: at its battle position, or along its hyperspace path while an
    /// arrival decelerates into place or a departure accelerates away.
    /// </summary>
    /// <param name="unit">The unit being posed.</param>
    /// <param name="presentation">The unit's scene objects.</param>
    private void PoseHolder(TacticalBattleUnit unit, UnitPresentation presentation)
    {
        Transform holder = presentation.Holder;
        if (presentation.Jump == JumpPhase.None)
        {
            holder.localPosition = ToScenePosition(unit);
            holder.localRotation = ToSceneRotation(unit.Heading);
            holder.localScale = Vector3.one * GetDockingScale(unit);
            return;
        }

        bool arriving = presentation.Jump == JumpPhase.Arriving;
        float duration = arriving ? _jumpArrivalSeconds : _jumpDepartureSeconds;
        float progress = Mathf.Clamp01(presentation.JumpElapsed / duration);
        if (progress >= 1f)
        {
            presentation.Jump = JumpPhase.None;
            if (arriving)
                PoseHolder(unit, presentation);
            else
                holder.gameObject.SetActive(false);
            return;
        }

        // The hull covers the jump distance on a cubic ease: arrivals shed speed into place,
        // departures gather it, and the model stretches along the path with that speed.
        float remaining = arriving ? 1f - progress : progress;
        float offset = _jumpDistance * remaining * remaining * remaining;
        float stretch = 1f + _jumpStretch * remaining * remaining;
        Vector3 anchor = arriving ? ToScenePosition(unit) : presentation.JumpOrigin;
        Vector3 forward = presentation.JumpForward;
        holder.localPosition = anchor + forward * (arriving ? -offset : offset);
        holder.localRotation = Quaternion.LookRotation(forward, Vector3.up);
        holder.localScale = new Vector3(1f, 1f, stretch);
    }

    /// <summary>
    /// Returns the size a unit is drawn at: a squadron landing aboard a ship shrinks into its
    /// hangar over the seconds the landing takes, and everything else is drawn full size.
    /// </summary>
    /// <param name="unit">The unit being posed.</param>
    /// <returns>The scale applied to the unit's model.</returns>
    private static float GetDockingScale(TacticalBattleUnit unit)
    {
        return unit is TacticalStarfighterUnit squadron && squadron.DockingFraction > 0
            ? Mathf.Lerp(1f, _dockedScale, (float)squadron.DockingFraction)
            : 1f;
    }

    /// <summary>
    /// Starts or stops a capital ship's tractor-beam effect to match the simulation.
    /// </summary>
    /// <param name="unit">The unit being posed.</param>
    /// <param name="presentation">The unit's scene objects.</param>
    private void UpdateTractorEffect(TacticalBattleUnit unit, UnitPresentation presentation)
    {
        bool tractored = unit is TacticalCapitalShipUnit ship && ship.IsTractored;
        if (tractored == presentation.TractorActive)
            return;

        presentation.TractorActive = tractored;
        if (tractored)
        {
            presentation.TractorEffect ??= CreateFieldEffect(
                presentation,
                "TractorBeam",
                GetTractorMaterial(),
                _tractorRadiusScale,
                _tractorEmissionRate
            );
            presentation.TractorEffect.Play();
        }
        else if (presentation.TractorEffect != null)
        {
            presentation.TractorEffect.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// <summary>
    /// Starts or stops a ship's gravity-well effect to match the simulation: an orange-brown
    /// field falling in on every ship that is holding the enemy in the system, which is how the
    /// player sees what stands between a fleet and its retreat.
    /// </summary>
    /// <param name="unit">The unit being posed.</param>
    /// <param name="presentation">The unit's scene objects.</param>
    private void UpdateGravityWellEffect(TacticalBattleUnit unit, UnitPresentation presentation)
    {
        bool projecting = unit is TacticalCapitalShipUnit ship && ship.ProjectsGravityWell;
        if (projecting == presentation.GravityWellActive)
            return;

        presentation.GravityWellActive = projecting;
        if (projecting)
        {
            presentation.GravityWellEffect ??= CreateFieldEffect(
                presentation,
                "GravityWell",
                GetGravityWellMaterial(),
                _gravityWellRadiusScale,
                _gravityWellEmissionRate
            );
            presentation.GravityWellEffect.Play();
        }
        else if (presentation.GravityWellEffect != null)
        {
            presentation.GravityWellEffect.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// <summary>
    /// Returns whether a unit is on show with its gravity-well effect running.
    /// </summary>
    /// <param name="presentation">The unit's scene objects.</param>
    /// <returns>True when the effect is running on a visible hull.</returns>
    private static bool IsShowingGravityWell(UnitPresentation presentation)
    {
        return presentation.GravityWellActive && presentation.Holder?.gameObject.activeSelf == true;
    }

    /// <summary>
    /// Creates a particle field that falls in on a ship from a shell around it: blue for a
    /// tractor beam's hold, orange-brown for a gravity well.
    /// </summary>
    /// <param name="presentation">The ship's scene objects.</param>
    /// <param name="effectName">The effect object's name.</param>
    /// <param name="material">The particle material.</param>
    /// <param name="radiusScale">The shell's radius, in hull diameters.</param>
    /// <param name="emissionRate">The particles emitted each second.</param>
    /// <returns>The looping particle system, parented to the ship's holder.</returns>
    private ParticleSystem CreateFieldEffect(
        UnitPresentation presentation,
        string effectName,
        Material material,
        float radiusScale,
        float emissionRate
    )
    {
        GameObject effect = new GameObject(effectName, typeof(ParticleSystem));
        effect.layer = PresentationLayers.TacticalBattle;
        effect.transform.SetParent(presentation.Holder, false);
        ParticleSystem system = effect.GetComponent<ParticleSystem>();
        // A new system is already playing, and its duration cannot be set until it stops.
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        float radius = presentation.Diameter * radiusScale;
        float speed = presentation.Diameter * _tractorInwardSpeed;

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = radius / speed;
        main.startSpeed = -speed;
        main.startSize = presentation.Diameter * _tractorParticleSize;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 128;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        shape.radiusThickness = _tractorShellThickness;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            AnimationCurve.Linear(0f, 1f, 1f, _tractorEndSize)
        );

        ConfigureEffectRenderer(effect, material);
        return system;
    }

    /// <summary>
    /// Fires an explosion where a unit was destroyed, reusing a finished burst when one exists.
    /// </summary>
    /// <param name="unit">The destroyed unit.</param>
    private void PlayExplosion(TacticalBattleUnit unit)
    {
        PlayExplosion(
            ToScenePosition(unit),
            GetModelDiameter(unit),
            _explosionFireCount,
            _explosionSmokeCount
        );
    }

    /// <summary>
    /// Fires a burst at a scene position, reusing a finished burst when one exists.
    /// </summary>
    /// <param name="position">The burst center.</param>
    /// <param name="diameter">The burst's size, in scene units.</param>
    /// <param name="fireCount">The number of fire particles.</param>
    /// <param name="smokeCount">The number of smoke particles.</param>
    private void PlayExplosion(Vector3 position, float diameter, int fireCount, int smokeCount)
    {
        ExplosionEffect effect = null;
        foreach (ExplosionEffect candidate in _explosions)
        {
            if (candidate.Root != null && !candidate.IsAlive)
            {
                effect = candidate;
                break;
            }
        }

        if (effect == null)
        {
            effect = CreateExplosion();
            _explosions.Add(effect);
        }

        // The burst is authored at unit scale and the root scales it to the hull.
        effect.Root.localPosition = position;
        effect.Root.localScale = Vector3.one * diameter;
        effect.Root.gameObject.SetActive(true);
        effect.Emit(fireCount, smokeCount);
    }

    /// <summary>
    /// Starts drawing a superlaser beam from a station to the capital ship it fired at.
    /// </summary>
    /// <param name="station">The firing station.</param>
    /// <param name="target">The ship being destroyed.</param>
    private void StartSuperlaserBeam(TacticalBattleStation station, TacticalBattleUnit target)
    {
        if (station == null || target == null)
            return;

        SuperlaserBeam beam = null;
        foreach (SuperlaserBeam candidate in _beams)
        {
            if (candidate.Root != null && !candidate.Active)
            {
                beam = candidate;
                break;
            }
        }

        if (beam == null)
        {
            GameObject root = new GameObject(
                "SuperlaserBeam",
                typeof(MeshFilter),
                typeof(MeshRenderer)
            );
            root.layer = PresentationLayers.TacticalBattle;
            root.transform.SetParent(modelRoot, false);
            root.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(
                "Cube.fbx"
            );
            MeshRenderer beamRenderer = root.GetComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = GetSuperlaserMaterial();
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;
            beam = new SuperlaserBeam(root.transform);
            _beams.Add(beam);
        }

        beam.Station = station;
        beam.Target = target;
        beam.End = ToScenePosition(target);
        beam.Elapsed = 0f;
        beam.Active = true;
        beam.Root.gameObject.SetActive(true);
        PoseSuperlaserBeam(beam);
    }

    /// <summary>
    /// Poses every beam being drawn and retires those that have burned out.
    /// </summary>
    private void SyncSuperlaserBeams()
    {
        foreach (SuperlaserBeam beam in _beams)
        {
            if (!beam.Active)
                continue;

            if (beam.Elapsed >= _superlaserBeamSeconds || beam.Root == null)
            {
                beam.Active = false;
                if (beam.Root != null)
                    beam.Root.gameObject.SetActive(false);
                continue;
            }

            PoseSuperlaserBeam(beam);
        }
    }

    /// <summary>
    /// Stretches a beam from its station's surface to its target. The beam follows the ship
    /// while it lives, swells as it fires, and thins as it burns out.
    /// </summary>
    /// <param name="beam">The beam to pose.</param>
    private static void PoseSuperlaserBeam(SuperlaserBeam beam)
    {
        if (beam.Target.IsAlive && !beam.Target.HasWithdrawn)
            beam.End = ToScenePosition(beam.Target);

        float radius = (float)beam.Station.Radius;
        Vector3 center = ToScenePosition(beam.Station.X, beam.Station.Y);
        Vector3 direction = beam.End - center;
        float distance = direction.magnitude;
        if (distance <= radius)
        {
            beam.Root.gameObject.SetActive(false);
            return;
        }

        direction /= distance;
        Vector3 start = center + direction * radius;
        float progress = Mathf.Clamp01(beam.Elapsed / _superlaserBeamSeconds);
        float envelope =
            progress < _superlaserBeamRise
                ? progress / _superlaserBeamRise
                : 1f - (progress - _superlaserBeamRise) / (1f - _superlaserBeamRise);
        float width = Mathf.Max(_superlaserBeamWidth * envelope, 0.05f);
        beam.Root.localPosition = (start + beam.End) * 0.5f;
        beam.Root.localRotation = Quaternion.LookRotation(direction, Vector3.up);
        beam.Root.localScale = new Vector3(width, width, distance - radius);
    }

    /// <summary>
    /// Returns the superlaser beam material, creating it on first use: the pipeline's lit
    /// material glowing green whatever the scene exposure.
    /// </summary>
    /// <returns>The beam material.</returns>
    private Material GetSuperlaserMaterial()
    {
        if (_superlaserMaterial != null)
            return _superlaserMaterial;

        _superlaserMaterial = CreatePlaceholderMaterial(_superlaserColor);
        _superlaserMaterial.name = "Tactical Superlaser";
        if (_superlaserMaterial.HasProperty(_emissiveColorProperty))
            _superlaserMaterial.SetColor(
                _emissiveColorProperty,
                _superlaserColor * _superlaserEmission
            );
        if (_superlaserMaterial.HasProperty(_emissiveExposureWeightProperty))
            _superlaserMaterial.SetFloat(_emissiveExposureWeightProperty, 0f);
        return _superlaserMaterial;
    }

    /// <summary>
    /// Creates a selection box: twelve thin bars, one for each edge of a box, under one root.
    /// </summary>
    /// <returns>The new box, not yet posed or colored.</returns>
    private SelectionBox CreateSelectionBox()
    {
        GameObject root = new GameObject("SelectionBox");
        root.layer = PresentationLayers.TacticalBattle;
        root.transform.SetParent(modelRoot, false);
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        MeshRenderer[] edges = new MeshRenderer[_selectionEdgeCount];
        for (int index = 0; index < edges.Length; index++)
        {
            GameObject edge = new GameObject("Edge", typeof(MeshFilter), typeof(MeshRenderer));
            edge.layer = PresentationLayers.TacticalBattle;
            edge.transform.SetParent(root.transform, false);
            edge.GetComponent<MeshFilter>().sharedMesh = cube;
            MeshRenderer edgeRenderer = edge.GetComponent<MeshRenderer>();
            edgeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            edgeRenderer.receiveShadows = false;
            edges[index] = edgeRenderer;
        }

        return new SelectionBox(root.transform, edges);
    }

    /// <summary>
    /// Fits a selection box to a unit's hull where it is drawn. The lines are sized from the
    /// hull's distance to the camera, so they keep one width on screen however far the camera
    /// backs away.
    /// </summary>
    /// <param name="box">The box to pose.</param>
    /// <param name="presentation">The boxed unit's scene objects.</param>
    private void PoseSelectionBox(SelectionBox box, UnitPresentation presentation)
    {
        Transform holder = presentation.Holder;
        box.Root.localPosition = holder.localPosition;
        box.Root.localRotation = holder.localRotation;

        // A hyperspace jump stretches the hull along its path. The box takes the stretch in its
        // size rather than its scale, so its lines stay thin.
        Vector3 stretch = holder.localScale;
        Vector3 center = Vector3.Scale(presentation.LocalBounds.center, stretch);
        Vector3 half = Vector3.Scale(presentation.LocalBounds.extents, stretch) * _selectionPadding;
        float width = GetLineWidth(holder.position, SelectionLineWidth);
        for (int index = 0; index < _selectionEdgeCount; index++)
        {
            // Four edges run along each axis, one through each corner of the face across it.
            int axis = index / 4;
            int first = (axis + 1) % 3;
            int second = (axis + 2) % 3;
            Vector3 offset = Vector3.zero;
            offset[first] = (index & 1) == 0 ? -half[first] : half[first];
            offset[second] = (index & 2) == 0 ? -half[second] : half[second];
            Vector3 size = new Vector3(width, width, width);
            size[axis] = half[axis] * 2f + width;
            Transform edge = box.Edges[index].transform;
            edge.localPosition = center + offset;
            edge.localScale = size;
        }
    }

    /// <summary>
    /// Returns how thick a line must be in the scene to draw a given number of source pixels
    /// wide at a point's distance from the camera.
    /// </summary>
    /// <param name="worldPosition">The point the line passes.</param>
    /// <param name="sourcePixels">The wanted width on screen, in source pixels.</param>
    /// <returns>The thickness in scene units.</returns>
    private float GetLineWidth(Vector3 worldPosition, float sourcePixels)
    {
        Transform view = sceneCamera.transform;
        float depth = Mathf.Max(
            Vector3.Dot(worldPosition - view.position, view.forward),
            _nearClip
        );
        return sourcePixels
            * depth
            * 2f
            * Mathf.Tan(FieldOfViewDegrees * 0.5f * Mathf.Deg2Rad)
            / _arenaHeight;
    }

    /// <summary>
    /// Creates a deck marker: a thin stem and a flat foot, under one root.
    /// </summary>
    /// <returns>The new marker, not yet posed or colored.</returns>
    private DeckMarker CreateDeckMarker()
    {
        GameObject root = new GameObject("DeckMarker");
        root.layer = PresentationLayers.TacticalBattle;
        root.transform.SetParent(modelRoot, false);
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        MeshRenderer[] parts = new MeshRenderer[2];
        for (int index = 0; index < parts.Length; index++)
        {
            GameObject part = new GameObject(
                index == 0 ? "Stem" : "Foot",
                typeof(MeshFilter),
                typeof(MeshRenderer)
            );
            part.layer = PresentationLayers.TacticalBattle;
            part.transform.SetParent(root.transform, false);
            part.GetComponent<MeshFilter>().sharedMesh = cube;
            MeshRenderer partRenderer = part.GetComponent<MeshRenderer>();
            partRenderer.shadowCastingMode = ShadowCastingMode.Off;
            partRenderer.receiveShadows = false;
            parts[index] = partRenderer;
        }

        return new DeckMarker(root.transform, parts[0], parts[1]);
    }

    /// <summary>
    /// Returns the selection-box material for a color, creating it on first use: the pipeline's
    /// lit material, black and matte so the rig's lights add nothing, glowing in that color
    /// whatever the scene exposure.
    /// </summary>
    /// <param name="color">The box color.</param>
    /// <returns>The material shared by every box of that color.</returns>
    private Material GetSelectionMaterial(Color color)
    {
        if (_selectionMaterials.TryGetValue(color, out Material material) && material != null)
            return material;

        material = CreatePlaceholderMaterial(Color.black);
        material.name = "Tactical Selection";
        if (material.HasProperty(_emissiveColorProperty))
            material.SetColor(_emissiveColorProperty, color.linear * _selectionEmission);
        else
            material.color = color;
        if (material.HasProperty(_emissiveExposureWeightProperty))
            material.SetFloat(_emissiveExposureWeightProperty, 0f);
        if (material.HasProperty(_smoothnessProperty))
            material.SetFloat(_smoothnessProperty, 0f);
        _selectionMaterials[color] = material;
        return material;
    }

    /// <summary>
    /// Blows a battle station apart: a cluster of bursts scattered through its volume, each a
    /// good fraction of its size, rather than one burst stretched over the whole of it.
    /// </summary>
    /// <param name="station">The destroyed station.</param>
    private void PlayStationExplosion(TacticalBattleStation station)
    {
        float radius = (float)station.Radius;
        Vector3 center = ToScenePosition(station.X, station.Y);
        for (int index = 0; index < _stationExplosionBursts; index++)
        {
            PlayExplosion(
                center + UnityEngine.Random.insideUnitSphere * (radius * _stationExplosionSpread),
                radius * _stationExplosionBurstScale,
                _explosionFireCount,
                _explosionSmokeCount
            );
        }
    }

    /// <summary>
    /// Fires a small burst where each torpedo that was in flight last frame has now landed.
    /// </summary>
    /// <param name="shots">The battle's current shots.</param>
    private void SyncTorpedoImpacts(IReadOnlyList<TacticalShot> shots)
    {
        _landedTorpedoes.Clear();
        foreach (TacticalShot torpedo in _torpedoesInFlight)
        {
            bool inFlight = false;
            for (int index = 0; index < shots.Count && !inFlight; index++)
                inFlight = ReferenceEquals(shots[index], torpedo);
            if (!inFlight)
                _landedTorpedoes.Add(torpedo);
        }

        foreach (TacticalShot torpedo in _landedTorpedoes)
        {
            _torpedoesInFlight.Remove(torpedo);
            if (modelRoot != null)
            {
                PlayExplosion(
                    ToScenePosition(torpedo.EndX, torpedo.EndY, torpedo.EndAltitude),
                    _torpedoBlastDiameter,
                    _torpedoBlastFireCount,
                    _torpedoBlastSmokeCount
                );
            }
        }

        foreach (TacticalShot shot in shots)
        {
            if (shot.IsTorpedo)
                _torpedoesInFlight.Add(shot);
        }
    }

    /// <summary>
    /// Builds one pooled explosion: a flash, a burst of fire, and slower smoke.
    /// </summary>
    /// <returns>The new effect.</returns>
    private ExplosionEffect CreateExplosion()
    {
        GameObject root = new GameObject("Explosion");
        root.layer = PresentationLayers.TacticalBattle;
        root.transform.SetParent(modelRoot, false);
        ParticleSystem flash = CreateBurstSystem(
            root.transform,
            "Flash",
            GetFlashMaterial(),
            _explosionFlashSize,
            0f,
            _explosionFlashLifetime
        );
        ParticleSystem fire = CreateBurstSystem(
            root.transform,
            "Fire",
            GetFireMaterial(),
            _explosionFireSize,
            _explosionFireSpeed,
            _explosionFireLifetime
        );
        ParticleSystem smoke = CreateBurstSystem(
            root.transform,
            "Smoke",
            GetSmokeMaterial(),
            _explosionSmokeSize,
            _explosionSmokeSpeed,
            _explosionSmokeLifetime
        );
        return new ExplosionEffect(root.transform, flash, fire, smoke);
    }

    /// <summary>
    /// Creates a one-shot particle system that only emits on request and shrinks its particles
    /// away over their lifetime; sizes and speeds are in multiples of the unit's diameter.
    /// </summary>
    /// <param name="parent">The explosion root.</param>
    /// <param name="systemName">The object name.</param>
    /// <param name="material">The particle material.</param>
    /// <param name="size">The largest particle size.</param>
    /// <param name="speed">The fastest particle speed.</param>
    /// <param name="lifetime">The longest particle lifetime in seconds.</param>
    /// <returns>The configured system.</returns>
    private static ParticleSystem CreateBurstSystem(
        Transform parent,
        string systemName,
        Material material,
        float size,
        float speed,
        float lifetime
    )
    {
        GameObject effect = new GameObject(systemName, typeof(ParticleSystem));
        effect.layer = PresentationLayers.TacticalBattle;
        effect.transform.SetParent(parent, false);
        ParticleSystem system = effect.GetComponent<ParticleSystem>();
        // A new system is already playing, and its duration cannot be set until it stops.
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = lifetime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = _explosionSpread;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            AnimationCurve.Linear(0f, 1f, 1f, 0f)
        );

        ConfigureEffectRenderer(effect, material);
        return system;
    }

    /// <summary>
    /// Points a particle system's renderer at a material and keeps it out of the shadow passes.
    /// </summary>
    /// <param name="effect">The particle system's object.</param>
    /// <param name="material">The particle material.</param>
    private static void ConfigureEffectRenderer(GameObject effect, Material material)
    {
        ParticleSystemRenderer renderer = effect.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    /// <summary>
    /// Returns the explosion flash material, creating it on first use.
    /// </summary>
    /// <returns>The flash material.</returns>
    private Material GetFlashMaterial()
    {
        if (_flashMaterial == null)
            _flashMaterial = CreateEffectMaterial("Tactical Flash", _flashColor, _flashEmission);
        return _flashMaterial;
    }

    /// <summary>
    /// Returns the explosion fire material, creating it on first use.
    /// </summary>
    /// <returns>The fire material.</returns>
    private Material GetFireMaterial()
    {
        if (_fireMaterial == null)
            _fireMaterial = CreateEffectMaterial("Tactical Fire", _fireColor, _fireEmission);
        return _fireMaterial;
    }

    /// <summary>
    /// Returns the explosion smoke material, creating it on first use.
    /// </summary>
    /// <returns>The smoke material.</returns>
    private Material GetSmokeMaterial()
    {
        if (_smokeMaterial == null)
            _smokeMaterial = CreateEffectMaterial("Tactical Smoke", _smokeColor, 0f);
        return _smokeMaterial;
    }

    /// <summary>
    /// Returns the gravity-well particle material, creating it on first use.
    /// </summary>
    /// <returns>The gravity-well material.</returns>
    private Material GetGravityWellMaterial()
    {
        if (_gravityWellMaterial == null)
        {
            _gravityWellMaterial = CreateEffectMaterial(
                "Tactical Gravity Well",
                _gravityWellColor,
                _gravityWellEmission
            );
        }

        return _gravityWellMaterial;
    }

    /// <summary>
    /// Returns the tractor-beam particle material, creating it on first use.
    /// </summary>
    /// <returns>The tractor material.</returns>
    private Material GetTractorMaterial()
    {
        if (_tractorMaterial == null)
        {
            _tractorMaterial = CreateEffectMaterial(
                "Tactical Tractor Beam",
                _tractorColor,
                _tractorEmission
            );
        }

        return _tractorMaterial;
    }

    /// <summary>
    /// Creates a particle material in the active pipeline's lit shader: a soft disc, cut out by
    /// alpha clipping, that glows in a color independent of the scene exposure.
    /// </summary>
    /// <param name="materialName">The material name.</param>
    /// <param name="color">The particle color.</param>
    /// <param name="emission">The emissive intensity, or zero for an unlit dark particle.</param>
    /// <returns>The new material.</returns>
    private Material CreateEffectMaterial(string materialName, Color color, float emission)
    {
        // The particle material authored with the rig already has the alpha cutout, validated
        // in the editor, so a clone needs only its texture and colors. A scene assembled without
        // the rig sets the cutout up here, which the editor alone can do.
        bool authored = particleMaterial != null;
        Material material = authored
            ? new Material(particleMaterial) { color = color }
            : CreatePlaceholderMaterial(color);
        material.name = materialName;
        Texture2D particle = GetParticleTexture();
        material.mainTexture = particle;
        if (material.HasProperty(_baseColorMapProperty))
            material.SetTexture(_baseColorMapProperty, particle);
        if (material.HasProperty(_alphaCutoffProperty))
            material.SetFloat(_alphaCutoffProperty, _particleAlphaCutoff);
        if (!authored && material.HasProperty(_alphaCutoffEnableProperty))
        {
            material.SetFloat(_alphaCutoffEnableProperty, 1f);
            material.EnableKeyword(_alphaTestKeyword);
        }

        if (material.HasProperty(_emissiveColorProperty))
            material.SetColor(_emissiveColorProperty, color * emission);
        if (material.HasProperty(_emissiveExposureWeightProperty))
            material.SetFloat(_emissiveExposureWeightProperty, 0f);
        if (material.HasProperty(_smoothnessProperty))
            material.SetFloat(_smoothnessProperty, 0f);
        // HDRP honors the cutout only once the material's passes, keywords and render queue are
        // validated; set by hand alone, the particles draw as opaque squares.
        if (!authored && material.HasProperty(_alphaCutoffEnableProperty))
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
        return material;
    }

    /// <summary>
    /// Returns the shared soft-disc particle texture, drawing it on first use.
    /// </summary>
    /// <returns>A white texture whose alpha fades out toward the edge of a circle.</returns>
    private Texture2D GetParticleTexture()
    {
        if (_particleTexture != null)
            return _particleTexture;

        _particleTexture = new Texture2D(
            _particleTextureSize,
            _particleTextureSize,
            TextureFormat.RGBA32,
            false
        )
        {
            name = "Tactical Particle",
            wrapMode = TextureWrapMode.Clamp,
        };
        Color32[] pixels = new Color32[_particleTextureSize * _particleTextureSize];
        float center = (_particleTextureSize - 1) / 2f;
        for (int y = 0; y < _particleTextureSize; y++)
        {
            for (int x = 0; x < _particleTextureSize; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((1f - distance) / _particleEdgeSoftness);
                pixels[y * _particleTextureSize + x] = new Color32(
                    255,
                    255,
                    255,
                    (byte)Mathf.RoundToInt(alpha * 255f)
                );
            }
        }

        _particleTexture.SetPixels32(pixels);
        _particleTexture.Apply(false, true);
        return _particleTexture;
    }

    /// <summary>
    /// Returns the shared placeholder material for one side, creating it from the active render
    /// pipeline's default material on first use.
    /// </summary>
    /// <param name="side">The unit's side.</param>
    /// <returns>The side's placeholder material.</returns>
    private Material GetPlaceholderMaterial(CombatSide side)
    {
        if (side == CombatSide.Attacker)
        {
            if (_attackerMaterial == null)
                _attackerMaterial = CreatePlaceholderMaterial(_attackerPlaceholderColor);
            return _attackerMaterial;
        }

        if (_defenderMaterial == null)
            _defenderMaterial = CreatePlaceholderMaterial(_defenderPlaceholderColor);
        return _defenderMaterial;
    }

    /// <summary>
    /// Creates a tinted opaque material that the active render pipeline can draw.
    /// </summary>
    /// <param name="color">The tint.</param>
    /// <returns>The new material.</returns>
    private Material CreatePlaceholderMaterial(Color color)
    {
        Material template = FindLitTemplate();
        if (template == null)
            throw new MissingReferenceException($"{name}/LitMaterial is missing.");

        return new Material(template) { name = "Tactical Placeholder", color = color };
    }

    /// <summary>
    /// Returns the opaque lit material the battle's own materials are cloned from: the asset
    /// authored with the rig, which a player build carries along with its shader variants. A
    /// scene assembled without the rig, as tests do, falls back to the render pipeline's default
    /// material, which exists only in the editor.
    /// </summary>
    /// <returns>The template material, or null when neither is available.</returns>
    private Material FindLitTemplate()
    {
        if (litMaterial != null)
            return litMaterial;

        return GraphicsSettings.currentRenderPipeline != null
            ? GraphicsSettings.currentRenderPipeline.defaultMaterial
            : null;
    }

    /// <summary>
    /// Loads a unit's model and swaps it in for the placeholder once posed.
    /// </summary>
    /// <param name="presentation">The unit presentation receiving the model.</param>
    /// <param name="address">The model content address.</param>
    /// <param name="generation">The battle the load belongs to, for the pending-load count.</param>
    /// <param name="cancellationToken">Cancels the load when the battle ends.</param>
    /// <returns>A task completing when the model is shown or the load was abandoned.</returns>
    private async Task LoadModelAsync(
        UnitPresentation presentation,
        string address,
        int generation,
        CancellationToken cancellationToken
    )
    {
        ContentModelInstance instance = null;
        try
        {
            instance = await _modelCache.InstantiateAsync(
                address,
                presentation.ModelParent,
                cancellationToken
            );
            if (cancellationToken.IsCancellationRequested || presentation.ModelParent == null)
            {
                instance.Dispose();
                return;
            }

            presentation.Model = instance;
            PoseModel(presentation);
            PrepareModelMaterials(presentation.ModelParent);
            presentation.Placeholder.SetActive(false);
        }
        catch (OperationCanceledException)
        {
            instance?.Dispose();
        }
        catch (Exception exception)
        {
            instance?.Dispose();
            if (_reportedAddresses.Add(address))
                Debug.LogWarning(
                    $"Tactical battle model '{address}' is unavailable; using a placeholder. {exception.Message}"
                );
        }
        finally
        {
            // A load that outlives its battle must not count against the next one.
            if (generation == _loadGeneration)
                _pendingModelLoads = Math.Max(_pendingModelLoads - 1, 0);
        }
    }

    /// <summary>
    /// Fits a loaded model to its unit, assigns the scene layer, and measures the hull for the
    /// selection box.
    /// </summary>
    /// <param name="presentation">The presentation whose model was loaded.</param>
    private static void PoseModel(UnitPresentation presentation)
    {
        Transform holder = presentation.Holder;
        Transform model = presentation.ModelParent;
        SetLayerRecursively(model.gameObject, PresentationLayers.TacticalBattle);
        FitModel(holder, model, presentation.Diameter);
        if (TryGetLocalBounds(holder, model, out Bounds localBounds))
            presentation.LocalBounds = localBounds;
    }

    /// <summary>
    /// Scales a model so its longest side matches a diameter and centers it on its holder. The
    /// holder's own turn and stretch are set aside while the model is measured: a ship still
    /// flying its hyperspace arrival is stretched along its path, and a model fitted to that
    /// would come out a fraction of its size.
    /// </summary>
    /// <param name="holder">The transform positioned at the unit.</param>
    /// <param name="model">The model root under the holder.</param>
    /// <param name="diameter">The length the model's longest side is scaled to.</param>
    internal static void FitModel(Transform holder, Transform model, float diameter)
    {
        Quaternion holderRotation = holder.localRotation;
        Vector3 holderScale = holder.localScale;
        holder.localRotation = Quaternion.identity;
        holder.localScale = Vector3.one;
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        if (TryGetBounds(model, out Bounds bounds))
        {
            float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largestDimension > 0f)
                model.localScale = Vector3.one * (diameter / largestDimension);
            if (TryGetBounds(model, out bounds))
                model.position += holder.position - bounds.center;
        }

        holder.localRotation = holderRotation;
        holder.localScale = holderScale;
    }

    /// <summary>
    /// Makes a loaded model's materials render reliably in the arena. Transmissive or
    /// transparent materials become opaque lit copies that keep the base color texture, and every
    /// material renders double-sided: several capital-ship models were exported with the glTF
    /// volume extension and with triangle windings that single-sided culling discards entirely.
    /// </summary>
    /// <param name="model">The model root.</param>
    private void PrepareModelMaterials(Transform model)
    {
        Material template = FindLitTemplate();
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int index = 0; index < materials.Length; index++)
            {
                Material source = materials[index];
                if (source == null)
                    continue;

                if (template != null && source.renderQueue >= (int)RenderQueue.Transparent)
                {
                    Material opaque = new Material(template)
                    {
                        name = $"{source.name} (opaque)",
                        mainTexture = source.mainTexture,
                        color = Color.white,
                    };
                    if (opaque.HasProperty(_smoothnessProperty))
                        opaque.SetFloat(_smoothnessProperty, _hullSmoothness);
                    _replacedMaterials.Add(opaque);
                    materials[index] = opaque;
                    source = opaque;
                    changed = true;
                }

                ForceDoubleSided(source);
            }

            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    /// <summary>
    /// Disables back-face culling on a material using the HDRP double-sided conventions.
    /// </summary>
    /// <param name="material">The material to make double-sided.</param>
    private static void ForceDoubleSided(Material material)
    {
        if (material.HasProperty(_cullModeProperty))
            material.SetFloat(_cullModeProperty, (float)CullMode.Off);
        if (material.HasProperty(_cullModeForwardProperty))
            material.SetFloat(_cullModeForwardProperty, (float)CullMode.Off);
        if (material.HasProperty(_doubleSidedEnableProperty))
        {
            material.SetFloat(_doubleSidedEnableProperty, 1f);
            material.EnableKeyword(_doubleSidedKeyword);
        }
    }

    /// <summary>
    /// Computes the combined renderer bounds of a model.
    /// </summary>
    /// <param name="model">The model root.</param>
    /// <param name="bounds">Receives the combined bounds.</param>
    /// <returns>True when the model has renderers.</returns>
    private static bool TryGetBounds(Transform model, out Bounds bounds)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
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
    /// Measures a model's extent along its holder's own axes, so a box fitted to it turns with
    /// the hull instead of swelling to stay square with the world.
    /// </summary>
    /// <param name="holder">The transform whose local space the extent is measured in.</param>
    /// <param name="model">The model root under the holder.</param>
    /// <param name="bounds">Receives the extent in the holder's local space.</param>
    /// <returns>True when the model has renderers.</returns>
    internal static bool TryGetLocalBounds(Transform holder, Transform model, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Bounds mesh = renderer.localBounds;
            Vector3 minimum = mesh.min;
            Vector3 maximum = mesh.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = holder.InverseTransformPoint(
                    renderer.transform.TransformPoint(
                        new Vector3(
                            (corner & 1) == 0 ? minimum.x : maximum.x,
                            (corner & 2) == 0 ? minimum.y : maximum.y,
                            (corner & 4) == 0 ? minimum.z : maximum.z
                        )
                    )
                );
                if (found)
                    bounds.Encapsulate(point);
                else
                    bounds = new Bounds(point, Vector3.zero);
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Assigns a layer to an object and all of its descendants.
    /// </summary>
    /// <param name="target">The root object.</param>
    /// <param name="layer">The layer to assign.</param>
    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    /// <summary>
    /// Destroys an object immediately in edit mode and at frame end while playing.
    /// </summary>
    /// <param name="target">The object to destroy.</param>
    private static void DestroyObject(UnityEngine.Object target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    /// <summary>
    /// Verifies the authored references.
    /// </summary>
    private void VerifyReferences()
    {
        if (sceneCamera == null)
            throw new MissingReferenceException($"{name}/SceneCamera is missing.");
        if (modelRoot == null)
            throw new MissingReferenceException($"{name}/ModelRoot is missing.");
        if (outputImage == null)
            throw new MissingReferenceException($"{name}/OutputImage is missing.");
    }

    /// <summary>
    /// Tracks one unit's scene objects.
    /// </summary>
    private sealed class UnitPresentation
    {
        internal Transform Holder { get; }

        internal Transform ModelParent { get; }

        internal GameObject Placeholder { get; }

        internal float Diameter { get; }

        /// <summary>
        /// Gets or sets the hull's extent in the holder's local space, which the selection box
        /// wraps.
        /// </summary>
        internal Bounds LocalBounds { get; set; }

        internal ContentModelInstance Model { get; set; }

        internal ParticleSystem TractorEffect { get; set; }

        internal bool TractorActive { get; set; }

        internal ParticleSystem GravityWellEffect { get; set; }

        internal bool GravityWellActive { get; set; }

        internal JumpPhase Jump { get; set; }

        internal float JumpElapsed { get; set; }

        internal Vector3 JumpOrigin { get; set; }

        internal Vector3 JumpForward { get; set; }

        /// <summary>
        /// Creates the presentation for a unit holder.
        /// </summary>
        /// <param name="holder">The transform positioned at the unit.</param>
        /// <param name="modelParent">The holder child that receives and scales the model.</param>
        /// <param name="placeholder">The primitive shown until the model loads.</param>
        /// <param name="diameter">The target model diameter in scene units.</param>
        internal UnitPresentation(
            Transform holder,
            Transform modelParent,
            GameObject placeholder,
            float diameter
        )
        {
            Holder = holder;
            ModelParent = modelParent;
            Placeholder = placeholder;
            Diameter = diameter;
        }
    }

    /// <summary>
    /// Tracks one pooled superlaser beam.
    /// </summary>
    private sealed class SuperlaserBeam
    {
        internal Transform Root { get; }

        internal TacticalBattleStation Station { get; set; }

        internal TacticalBattleUnit Target { get; set; }

        internal Vector3 End { get; set; }

        internal float Elapsed { get; set; }

        internal bool Active { get; set; }

        /// <summary>
        /// Creates a beam around its stretched mesh.
        /// </summary>
        /// <param name="root">The beam's transform.</param>
        internal SuperlaserBeam(Transform root)
        {
            Root = root;
        }
    }

    /// <summary>
    /// Tracks one pooled deck marker.
    /// </summary>
    private sealed class DeckMarker
    {
        internal Transform Root { get; }

        internal MeshRenderer Stem { get; }

        internal MeshRenderer Foot { get; }

        internal TacticalBattleUnit Unit { get; set; }

        internal Color Color { get; set; }

        internal Material Material { get; set; }

        /// <summary>
        /// Creates a marker around its two parts.
        /// </summary>
        /// <param name="root">The marker's root transform.</param>
        /// <param name="stem">The bar between the hull and the battle plane.</param>
        /// <param name="foot">The flat mark on the battle plane.</param>
        internal DeckMarker(Transform root, MeshRenderer stem, MeshRenderer foot)
        {
            Root = root;
            Stem = stem;
            Foot = foot;
        }
    }

    /// <summary>
    /// Tracks one pooled selection box.
    /// </summary>
    private sealed class SelectionBox
    {
        internal Transform Root { get; }

        internal MeshRenderer[] Edges { get; }

        internal TacticalBattleUnit Unit { get; set; }

        internal Color Color { get; set; }

        internal Material Material { get; set; }

        /// <summary>
        /// Creates a box around its edges.
        /// </summary>
        /// <param name="root">The transform posed at the boxed hull.</param>
        /// <param name="edges">The twelve bars that draw the box's edges.</param>
        internal SelectionBox(Transform root, MeshRenderer[] edges)
        {
            Root = root;
            Edges = edges;
        }
    }

    /// <summary>
    /// Identifies the hyperspace animation a unit is flying.
    /// </summary>
    private enum JumpPhase
    {
        None,
        Arriving,
        Departing,
    }

    /// <summary>
    /// Tracks one pooled explosion's systems.
    /// </summary>
    private sealed class ExplosionEffect
    {
        private readonly ParticleSystem _flash;
        private readonly ParticleSystem _fire;
        private readonly ParticleSystem _smoke;

        internal Transform Root { get; }

        internal bool IsAlive =>
            Root != null && (IsBurning(_flash) || IsBurning(_fire) || IsBurning(_smoke));

        /// <summary>
        /// Creates the effect for one explosion root.
        /// </summary>
        /// <param name="root">The transform positioned and scaled at the destroyed unit.</param>
        /// <param name="flash">The single bright flash particle's system.</param>
        /// <param name="fire">The fast fire burst.</param>
        /// <param name="smoke">The slow smoke burst.</param>
        internal ExplosionEffect(
            Transform root,
            ParticleSystem flash,
            ParticleSystem fire,
            ParticleSystem smoke
        )
        {
            Root = root;
            _flash = flash;
            _fire = fire;
            _smoke = smoke;
        }

        /// <summary>
        /// Emits the flash, the fire, and the smoke at once.
        /// </summary>
        /// <param name="fireCount">The number of fire particles.</param>
        /// <param name="smokeCount">The number of smoke particles.</param>
        internal void Emit(int fireCount, int smokeCount)
        {
            _flash.Play(false);
            _fire.Play(false);
            _smoke.Play(false);
            _flash.Emit(1);
            _fire.Emit(fireCount);
            _smoke.Emit(smokeCount);
        }

        /// <summary>
        /// Returns whether a burst still has particles in flight.
        /// </summary>
        /// <param name="system">The burst system.</param>
        /// <returns>True while particles remain.</returns>
        private static bool IsBurning(ParticleSystem system)
        {
            return system != null && (system.particleCount > 0 || system.IsAlive(false));
        }
    }
}
