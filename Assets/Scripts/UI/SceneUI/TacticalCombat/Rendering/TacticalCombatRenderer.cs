using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.UI;
using UnityEngine;
using TacticalCombatEffectDefinition = global::Rebellion.Content.TacticalCombatEffectDefinition;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tactical.Rendering
{
    /// <summary>
    /// Owns tactical model instances, fighter billboards, and the isolated battle camera.
    /// </summary>
    public sealed partial class TacticalCombatRenderer
        : MonoBehaviour,
            ITacticalCombatArrivalPresentation
    {
        [SerializeField]
        private Camera _battleCamera;

        [SerializeField]
        private Transform _modelRoot;

        [SerializeField]
        private Shader _fighterShader;
        private readonly List<Visual> _visuals = new();
        private readonly Dictionary<int, Visual> _visualsByIndex = new();
        private readonly HashSet<CombatUnit> _yellowBoxes = new();
        private readonly HashSet<CombatUnit> _factionBoxes = new();
        private readonly HashSet<CombatUnit> _whiteBoxes = new();
        private readonly bool[] _factionHighlight = new bool[2];
        private TacticalCombatSession _session;
        private Dictionary<string, TacticalCombatUnitDefinition> _unitDefinitions;
        private ITacticalCombatPresentationTimeline _replicaPlayback;
        private readonly TacticalCombatMeshPicker _picker = new();
        private readonly List<ContentModelInstance> _instances = new();
        private readonly Dictionary<Texture2D, Material> _fighterMaterials = new();
        private RenderTexture _renderTarget;
        private Mesh _billboardMesh;
        private Mesh _contactMesh;
        private Transform _planet;
        private TacticalCombatEffects _effects;
        private TacticalCombatImpactEffects _impacts;
        private TacticalCombatNavigationRenderer _navigation;
        private TacticalCombatHolocube _holocube;
        private readonly Dictionary<Material, Material> _environmentMaterials = new();
        private readonly Dictionary<string, Texture2D> _environmentTextures = new(
            StringComparer.Ordinal
        );
        private ITacticalCombatSceneryRandom _sceneryRandom;
        private TacticalCombatCameraState _cameraState;
        private float _field => _cameraState.Field;
        private float _distance;
        private bool _showPyro;
        private bool _highDetail;
        private TacticalCombatDetailProjection _detailProjection;
        public Camera Camera => _battleCamera;
        public RenderTexture Target => _renderTarget;
        public bool IsPresentingArrival { get; private set; }
        public event Action<bool> ArrivalPresentationChanged;

        /// <summary>
        /// Loads required content into an isolated scene without advancing battle time.
        /// </summary>
        /// <param name="session">The deployed encounter.</param>
        /// <param name="models">The shared parsed model cache.</param>
        /// <param name="assets">The active pack's addressed textures.</param>
        /// <param name="cancellationToken">The owning screen's cancellation token.</param>
        /// <param name="videoSettings">The encounter's video settings, or null to use their authored defaults for isolated presentation.</param>
        /// <param name="replicaPlayback">The connection-owned replica timeline, or null for an authoritative encounter.</param>
        /// <returns>The asynchronous presentation preparation.</returns>
        public Task PrepareAsync(
            TacticalCombatSession session,
            ContentModelCache models,
            ContentAssets assets,
            CancellationToken cancellationToken,
            UserVideoSettings videoSettings = null,
            ITacticalCombatPresentationTimeline replicaPlayback = null
        ) =>
            PrepareAsync(
                session,
                models,
                assets,
                cancellationToken,
                new TacticalCombatRenderSettings(videoSettings ?? new UserVideoSettings()),
                replicaPlayback
            );

        /// <summary>
        /// Loads battle resources using the immutable video options captured at encounter entry.
        /// </summary>
        /// <param name="session">The deployed encounter.</param>
        /// <param name="models">The shared parsed model cache.</param>
        /// <param name="assets">The active pack's addressed textures.</param>
        /// <param name="cancellationToken">The owning screen's cancellation token.</param>
        /// <param name="videoSettings">The encounter's captured video options.</param>
        /// <param name="replicaPlayback">The connection-owned replica timeline, or null for an authoritative encounter.</param>
        /// <returns>The asynchronous presentation preparation.</returns>
        public async Task PrepareAsync(
            TacticalCombatSession session,
            ContentModelCache models,
            ContentAssets assets,
            CancellationToken cancellationToken,
            TacticalCombatRenderSettings videoSettings,
            ITacticalCombatPresentationTimeline replicaPlayback = null
        )
        {
            if (_battleCamera == null || _modelRoot == null || _fighterShader == null)
                throw new MissingReferenceException(
                    $"{name} is missing battle rendering references."
                );
            if (
                session.IsReplica != (replicaPlayback != null)
                || replicaPlayback != null && !ReferenceEquals(replicaPlayback.Session, session)
            )
                throw new ArgumentException(
                    "The renderer requires the active encounter's replica playback owner.",
                    nameof(replicaPlayback)
                );
            _session = session;
            _unitDefinitions = session.Definition.Units.ToDictionary(
                unit => unit.UnitTypeID,
                StringComparer.Ordinal
            );
            _replicaPlayback = replicaPlayback;
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
            _effects = new TacticalCombatEffects(
                _modelRoot,
                _fighterShader,
                session.BattleDepth,
                GetPresentationPosition
            );
            _impacts = new TacticalCombatImpactEffects(
                _modelRoot,
                _battleCamera,
                _fighterShader,
                _showPyro
                    ? session.Definition.Effects
                    : Array.Empty<TacticalCombatEffectDefinition>(),
                assets,
                GetPresentationPosition,
                GetCloseUpPrimary
            );
            _session.TractorSourcesChanged += HandleTractorSourcesChanged;
            _session.GravityWellPresentationRequested += HandleGravityWell;
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
            foreach (CombatUnit unit in session.Units)
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
                else if (
                    GetUnitDefinition(unit) is { } definition
                    && !string.IsNullOrWhiteSpace(definition.ModelPath)
                )
                {
                    IReadOnlyList<string> paths =
                        definition.ModelDetailPaths.Count > 0
                            ? definition.ModelDetailPaths
                            : new[] { definition.ModelPath };
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
                        instance.ModelRoot.localScale = Vector3.one * definition.ModelScale;
                        visual.ModelDetails[index] = detail.transform;
                    }
                }
                else
                    CreateCapitalPlaceholder(visual);
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
                    if (unit.HasTractorSources)
                        HandleTractorSourcesChanged(unit, true);
                }
            }
            UpdateCamera();
            RenderUnits();
        }

        /// <summary>
        /// Finds the authored presentation definition for a participant.
        /// </summary>
        /// <param name="unit">The participant being rendered.</param>
        /// <returns>The matching definition, or null when the type has no definition.</returns>
        private TacticalCombatUnitDefinition GetUnitDefinition(CombatUnit unit) =>
            _unitDefinitions.GetValueOrDefault(unit.TypeID ?? string.Empty);

        /// <summary>
        /// Releases screen-owned models, materials, and GPU memory.
        /// </summary>
        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.TractorSourcesChanged -= HandleTractorSourcesChanged;
                _session.GravityWellPresentationRequested -= HandleGravityWell;
            }
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
            if (_contactMesh != null)
                Destroy(_contactMesh);
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
            public CombatUnit Unit { get; }
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
            public Visual(CombatUnit unit, GameObject root)
            {
                Unit = unit;
                Root = root;
            }
        }
    }
}
