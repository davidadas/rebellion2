using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation.Tactical;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Composes the Unity tactical scene with the active battle and its authored map definition.
/// </summary>
public sealed class TacticalViewController : MonoBehaviour
{
    private const string _arrivalSoundPath = "Pack/Shared/Tactical/Audio/hyperspace-arrival.wav";
    private const string _launchSoundPath = "Pack/Shared/Tactical/Audio/fighter-launch.wav";

    [SerializeField]
    private Camera battleCamera;

    [SerializeField]
    private Transform battleRoot;

    [SerializeField]
    private Transform mapRoot;

    [SerializeField]
    private Rect initialFleetViewport = new Rect(0.05f, 0.24f, 0.9f, 0.71f);

    private readonly List<ContentModelInstance> modelInstances = new List<ContentModelInstance>();
    private CancellationTokenSource modelLoading;
    private readonly Dictionary<CombatUnit, Transform> combatantPresentations =
        new Dictionary<CombatUnit, Transform>();
    private TacticalSelectionController selectionController;
    private string viewerFactionInstanceID;
    private TacticalArrivalView arrivals;
    private TacticalCarrierOperations carrierOperations;
    private TacticalFlightAudio flightAudio;
    private readonly Dictionary<CombatUnit, System.Numerics.Vector3[][]> carrierPaths = new();

    /// <summary>
    /// Gets the active battle presented by this scene.
    /// </summary>
    public ActiveBattle ActiveBattle { get; private set; }

    /// <summary>
    /// Gets the authored map used by the active battle.
    /// </summary>
    public BattleMap BattleMap { get; private set; }

    /// <summary>
    /// Gets the camera owned by the tactical scene.
    /// </summary>
    public Camera BattleCamera => battleCamera;

    /// <summary>
    /// Gets the transform under which tactical presentation objects are created.
    /// </summary>
    public Transform BattleRoot => battleRoot;

    /// <summary>
    /// Gets the transform under which authored map presentation objects are created.
    /// </summary>
    public Transform MapRoot => mapRoot;

    /// <summary>
    /// Verifies the authored tactical-scene references.
    /// </summary>
    private void Awake()
    {
        if (battleCamera == null)
            throw new MissingReferenceException($"{name} is missing its battle camera.");
        if (battleRoot == null)
            throw new MissingReferenceException($"{name} is missing its battle root.");
        if (mapRoot == null)
            throw new MissingReferenceException($"{name} is missing its map root.");
        if (mapRoot.GetComponent<TacticalMapRenderer>() == null)
            throw new MissingComponentException($"{mapRoot.name} is missing its map renderer.");
        if (GetComponent<TacticalSelectionController>() == null)
            throw new MissingComponentException($"{name} is missing its selection controller.");
    }

    /// <summary>
    /// Advances battle movement and presents its saved positions while the scene is unpaused.
    /// </summary>
    private void Update()
    {
        flightAudio?.Advance(Time.unscaledDeltaTime);
        bool paused = GetComponent<TacticalMenuController>()?.IsOpen == true;
        if (arrivals?.Advance(Time.deltaTime, paused) == false)
        {
            arrivals.Dispose();
            arrivals = null;
        }
        if (paused)
            return;
        foreach (CombatUnit unit in combatantPresentations.Keys)
            if (
                (unit.GetUnit() is CapitalShip || unit.FlightState == FighterFlightState.Deployed)
                && arrivals?.IsArriving(unit) != true
            )
                TacticalMovement.Advance(unit, Time.deltaTime);
        if (arrivals == null)
            carrierOperations?.Advance(Time.deltaTime);
        foreach (KeyValuePair<CombatUnit, Transform> presentation in combatantPresentations)
        {
            if (arrivals?.IsArriving(presentation.Key) == true)
                continue;
            presentation.Value.gameObject.SetActive(IsVisible(presentation.Key));
            presentation.Value.localPosition = ToUnityVector(presentation.Key.Position);
            Vector3 forward = ToUnityVector(presentation.Key.Forward);
            if (forward.sqrMagnitude > Mathf.Epsilon)
                presentation.Value.localRotation = Quaternion.LookRotation(forward.normalized);
        }
    }

    /// <summary>
    /// Projects a world click onto the selected friendly formation's current horizontal plane.
    /// </summary>
    /// <param name="screenPosition">The clicked screen location.</param>
    private void RequestMove(Vector2 screenPosition)
    {
        float height = 0f;
        int count = 0;
        foreach (CombatUnit unit in selectionController.SelectedCombatants)
        {
            if (
                unit.HasRetreated
                || !string.Equals(
                    unit.GetUnit().OwnerInstanceID,
                    viewerFactionInstanceID,
                    StringComparison.Ordinal
                )
            )
                continue;
            height += unit.Position.Y;
            count++;
        }
        if (count == 0)
            return;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, height / count, 0f));
        Ray ray = battleCamera.ScreenPointToRay(screenPosition);
        if (!plane.Raycast(ray, out float distance))
            return;
        Vector3 destination = ray.GetPoint(distance);
        TacticalMovement.MoveTo(
            ActiveBattle,
            BattleMap.PlayableBounds,
            selectionController.SelectedCombatants,
            viewerFactionInstanceID,
            new BattleVector3
            {
                X = destination.x,
                Y = destination.y,
                Z = destination.z,
            }
        );
    }

    /// <summary>
    /// Stops pending movement for the selected friendly ships.
    /// </summary>
    private void RequestStop() =>
        TacticalMovement.Stop(
            ActiveBattle,
            selectionController.SelectedCombatants,
            viewerFactionInstanceID
        );

    /// <summary>
    /// Queues the selected friendly carrier's cargo or selected docked squadrons for departure.
    /// </summary>
    private void RequestLaunch() =>
        carrierOperations.Launch(selectionController.SelectedCombatants, viewerFactionInstanceID);

    /// <summary>
    /// Recalls fighters belonging to the selected friendly carriers or squadrons.
    /// </summary>
    private void RequestRecall() =>
        carrierOperations.Recall(selectionController.SelectedCombatants, viewerFactionInstanceID);

    /// <summary>
    /// Resolves the active tactical battle after the scene becomes active.
    /// </summary>
    private async void Start()
    {
        if (ActiveBattle != null)
            return;
        try
        {
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            GetComponent<TacticalMenuController>().Initialize(bootstrap);
            GameRuntime runtime = bootstrap.GetRuntime();
            GameRoot game = runtime?.GetActiveGame();
            ActiveBattle activeBattle =
                game == null ? TacticalBattleLaunchContext.ActiveBattle : game.GetActiveBattle();
            if (activeBattle == null)
                throw new InvalidOperationException("TacticalView requires an active battle.");

            GameDataCatalog gameData = bootstrap.GetContentPack().GameData;
            if (
                !gameData.TryGetBattleMap(activeBattle.BattleMapInstanceID, out BattleMap battleMap)
            )
            {
                throw new InvalidOperationException(
                    $"Active battle map '{activeBattle.BattleMapInstanceID}' is not loaded."
                );
            }

            string viewerFactionInstanceID = game?.GetPlayerFaction().InstanceID;
            string themeFaction =
                viewerFactionInstanceID
                ?? activeBattle.GetParticipants().FirstOrDefault()?.FactionInstanceID;
            FactionThemeLibrary themes = new FactionThemeLibrary(gameData.FactionThemes);
            FactionTheme theme = themes.GetTheme(themeFaction);
            AudioClip arrivalClip = null;
            AudioClip launchClip = await bootstrap
                .GetContentAssets()
                .LoadAudioAsync(_launchSoundPath);
            if (this == null)
                return;
            if (game == null && activeBattle.Kind == BattleKind.Space)
            {
                arrivalClip = await bootstrap.GetContentAssets().LoadAudioAsync(_arrivalSoundPath);
                if (this == null)
                    return;
            }
            await InitializeAsync(
                activeBattle,
                battleMap,
                bootstrap.GetContentModelCache(),
                viewerFactionInstanceID,
                game == null
            );
            if (this == null)
                return;
            flightAudio = new TacticalFlightAudio(
                transform,
                launchClip,
                bootstrap.GetAudioManager()
            );
            carrierOperations.SquadronLaunched += flightAudio.PlayLaunch;
            GetComponentInChildren<TacticalSelectionView>()
                .ApplyTheme(theme.BattleHud, bootstrap.GetContentAssets());
            ContentBindings.Apply(mapRoot.gameObject, bootstrap.GetContentAssets());
            if (arrivalClip != null)
                arrivals = new TacticalArrivalView(
                    transform,
                    combatantPresentations,
                    arrivalClip,
                    bootstrap.GetAudioManager()
                );
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// Loads one battle's models, measures their rendered geometry, and prepares its presentation.
    /// </summary>
    /// <param name="activeBattle">The persistent tactical-battle state.</param>
    /// <param name="battleMap">The authored map selected for the battle.</param>
    /// <param name="models">The existing cache owning the parsed models used by this scene.</param>
    /// <param name="viewerFactionInstanceID">
    /// The faction whose authored starting camera should be used, or null to use the first participant.
    /// </param>
    /// <param name="deployFleet">Whether this is a new battle requiring initial deployment; false preserves saved poses.</param>
    /// <returns>A task completing after models, deployment, and camera framing are ready.</returns>
    public async Task InitializeAsync(
        ActiveBattle activeBattle,
        BattleMap battleMap,
        ContentModelCache models,
        string viewerFactionInstanceID = null,
        bool deployFleet = false
    )
    {
        if (activeBattle == null)
            throw new ArgumentNullException(nameof(activeBattle));
        if (battleMap == null)
            throw new ArgumentNullException(nameof(battleMap));
        if (models == null)
            throw new ArgumentNullException(nameof(models));
        if (
            !string.Equals(
                activeBattle.BattleMapInstanceID,
                battleMap.InstanceID,
                StringComparison.Ordinal
            )
        )
        {
            throw new ArgumentException(
                "The authored map does not match the active battle map identifier.",
                nameof(battleMap)
            );
        }
        if (activeBattle.Kind != battleMap.Kind)
        {
            throw new ArgumentException(
                "The authored map kind does not match the active battle kind.",
                nameof(battleMap)
            );
        }

        ActiveBattle = activeBattle;
        BattleMap = battleMap;
        mapRoot.GetComponent<TacticalMapRenderer>().Render(battleMap, battleCamera);
        Dictionary<CombatUnit, Transform> presentations = RenderCombatants();
        battleRoot.gameObject.SetActive(false);
        await Task.WhenAll(
            presentations
                .Select(presentation =>
                    LoadCombatantModelAsync(
                        presentation.Key,
                        presentation.Value,
                        models,
                        modelLoading.Token
                    )
                )
                .Append(mapRoot.GetComponent<TacticalMapRenderer>().Ready)
        );
        if (this == null)
            return;
        foreach (KeyValuePair<CombatUnit, Transform> presentation in presentations)
        {
            if (
                !ContentModelInstance.TryGetBounds(
                    presentation.Value,
                    presentation.Value,
                    out Bounds bounds
                )
            )
                throw new InvalidOperationException(
                    $"Tactical unit '{presentation.Key.GetUnit().TypeID}' has no loaded model geometry."
                );
            presentation.Key.GetUnit().ModelSize = new ModelDimensions
            {
                Width = bounds.size.x,
                Height = bounds.size.y,
                Depth = bounds.size.z,
            };
        }
        if (deployFleet)
            FleetDeploymentPlanner.Deploy(activeBattle, battleMap);
        carrierOperations = new TacticalCarrierOperations(activeBattle, carrierPaths);
        foreach (KeyValuePair<CombatUnit, Transform> presentation in presentations)
        {
            presentation.Value.gameObject.SetActive(IsVisible(presentation.Key));
            presentation.Value.localPosition = ToUnityVector(presentation.Key.Position);
            Vector3 forward = ToUnityVector(presentation.Key.Forward);
            if (forward.sqrMagnitude > Mathf.Epsilon)
                presentation.Value.localRotation = Quaternion.LookRotation(forward.normalized);
        }
        ConfigureCamera(viewerFactionInstanceID);
        battleRoot.gameObject.SetActive(true);
        GetComponent<TacticalSelectionController>().Initialize(battleCamera, presentations);
        this.viewerFactionInstanceID =
            viewerFactionInstanceID
            ?? activeBattle.GetParticipants().FirstOrDefault()?.FactionInstanceID;
        selectionController = GetComponent<TacticalSelectionController>();
        selectionController.MoveRequested -= RequestMove;
        selectionController.StopRequested -= RequestStop;
        selectionController.MoveRequested += RequestMove;
        selectionController.StopRequested += RequestStop;
        selectionController.LaunchRequested -= RequestLaunch;
        selectionController.RecallRequested -= RequestRecall;
        selectionController.DockRequested -= RequestRecall;
        selectionController.LaunchRequested += RequestLaunch;
        selectionController.RecallRequested += RequestRecall;
        selectionController.DockRequested += RequestRecall;
        foreach (KeyValuePair<CombatUnit, Transform> presentation in presentations)
            combatantPresentations.Add(presentation.Key, presentation.Value);
        GetComponentInChildren<TacticalSelectionView>()
            .BindBattle(
                presentations,
                battleMap.PlayableBounds,
                battleCamera,
                viewerFactionInstanceID
                    ?? activeBattle.GetParticipants().FirstOrDefault()?.FactionInstanceID,
                GetComponent<TacticalSelectionController>().SelectedCombatants
            );
        GetComponentInChildren<TacticalSelectionView>()
            .BindCarrierOperations(carrierOperations, this.viewerFactionInstanceID);
    }

    /// <summary>
    /// Releases scene instances and cancels model loads when the tactical view closes.
    /// </summary>
    private void OnDestroy()
    {
        if (selectionController)
        {
            selectionController.MoveRequested -= RequestMove;
            selectionController.StopRequested -= RequestStop;
            selectionController.LaunchRequested -= RequestLaunch;
            selectionController.RecallRequested -= RequestRecall;
            selectionController.DockRequested -= RequestRecall;
        }
        ReleaseModels();
    }

    /// <summary>
    /// Loads and centers a combatant's Unity model with its intentional uniform scale.
    /// </summary>
    /// <param name="combatant">The unit whose model is loaded.</param>
    /// <param name="presentation">The root receiving the model hierarchy.</param>
    /// <param name="models">The application-owned model cache.</param>
    /// <param name="cancellationToken">Cancels loading when the scene is closed or rebound.</param>
    /// <returns>A task completing when the model has been posed.</returns>
    private async Task LoadCombatantModelAsync(
        CombatUnit combatant,
        Transform presentation,
        ContentModelCache models,
        CancellationToken cancellationToken
    )
    {
        float scale = combatant.GetUnit().ModelScale;
        if (!(scale > 0f) || float.IsInfinity(scale))
            throw new InvalidOperationException(
                "Tactical model scale must be finite and positive."
            );
        ContentModelInstance instance = await models.InstantiateAsync(
            combatant.GetUnit().ModelPath,
            presentation,
            cancellationToken
        );
        if (cancellationToken.IsCancellationRequested)
        {
            instance.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        modelInstances.Add(instance);
        instance.ApplyPose(presentation, scale, Vector3.zero, false, false, true, -1);
        if (combatant.GetUnit() is CapitalShip carrier)
        {
            List<System.Numerics.Vector3[]> paths = new();
            foreach (StarfighterBay bay in carrier.GetComponents().OfType<StarfighterBay>())
            {
                if (bay.FlightPath.Count == 0)
                    continue;
                Transform node =
                    bay.ModelNodePath == "." || bay.ModelNodePath == instance.ModelRoot.name
                        ? instance.ModelRoot
                        : instance.ModelRoot.Find(bay.ModelNodePath);
                if (!node)
                    throw new InvalidOperationException(
                        $"Carrier '{carrier.TypeID}' has an unknown bay model node '{bay.ModelNodePath}'."
                    );
                System.Numerics.Vector3[] path = new System.Numerics.Vector3[bay.FlightPath.Count];
                for (int index = 0; index < path.Length; index++)
                {
                    StarfighterBayWaypoint point = bay.FlightPath[index];
                    Vector3 local = presentation.InverseTransformPoint(
                        node.TransformPoint(new Vector3(-point.X, point.Y, point.Z))
                    );
                    path[index] = new System.Numerics.Vector3(local.x, local.y, local.z);
                }
                paths.Add(path);
            }
            carrierPaths.Add(combatant, paths.ToArray());
        }
    }

    /// <summary>
    /// Releases only the instantiated models owned by this tactical view.
    /// </summary>
    private void ReleaseModels()
    {
        if (carrierOperations != null && flightAudio != null)
            carrierOperations.SquadronLaunched -= flightAudio.PlayLaunch;
        flightAudio?.Dispose();
        flightAudio = null;
        carrierOperations = null;
        carrierPaths.Clear();
        arrivals?.Dispose();
        arrivals = null;
        combatantPresentations.Clear();
        modelLoading?.Cancel();
        modelLoading?.Dispose();
        modelLoading = null;
        foreach (ContentModelInstance instance in modelInstances)
            instance.Dispose();
        modelInstances.Clear();
    }

    /// <summary>
    /// Applies the camera start authored for the viewing participant.
    /// </summary>
    /// <param name="viewerFactionInstanceID">The faction viewing the battle, when known.</param>
    private void ConfigureCamera(string viewerFactionInstanceID)
    {
        BattleParticipant viewer =
            ActiveBattle
                .GetParticipants()
                .FirstOrDefault(participant =>
                    string.Equals(
                        participant.FactionInstanceID,
                        viewerFactionInstanceID,
                        StringComparison.Ordinal
                    )
                )
            ?? ActiveBattle.GetParticipants().FirstOrDefault();
        BattleMapCameraStart cameraStart =
            BattleMap
                .GetCameraStarts()
                .FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.ParticipantSlotID,
                        viewer?.BattleMapSlotID,
                        StringComparison.Ordinal
                    )
                )
            ?? BattleMap.GetCameraStarts().FirstOrDefault();
        if (cameraStart == null)
            return;

        battleCamera.transform.SetPositionAndRotation(
            ToUnityVector(cameraStart.Position),
            Quaternion.Euler(ToUnityVector(cameraStart.Rotation))
        );
        battleCamera.fieldOfView = cameraStart.FieldOfView;
        battleCamera.nearClipPlane = 0.1f;
        battleCamera.farClipPlane = GetRequiredFarClipPlane(cameraStart.Position);
        battleCamera.clearFlags = CameraClearFlags.SolidColor;
        battleCamera.backgroundColor = ToUnityColor(BattleMap.Environment.BackgroundColor);
        battleCamera.allowHDR = true;
        HDAdditionalCameraData cameraData =
            battleCamera.GetComponent<HDAdditionalCameraData>()
            ?? battleCamera.gameObject.AddComponent<HDAdditionalCameraData>();
        cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        cameraData.backgroundColorHDR = battleCamera.backgroundColor.linear;
        TacticalViewCameraController cameraController =
            battleCamera.GetComponent<TacticalViewCameraController>();
        if (cameraController == null)
        {
            throw new MissingComponentException(
                $"{battleCamera.name} is missing its tactical camera controller."
            );
        }

        cameraController.Configure(BattleMap.PlayableBounds, ResolveCameraPivot(cameraStart));
        List<Bounds> hulls = new List<Bounds>();
        if (viewer != null)
        {
            foreach (CombatUnit combatant in viewer.GetCombatants())
            {
                if (!IsVisible(combatant))
                    continue;
                ModelDimensions size = combatant.GetUnit().ModelSize;
                Quaternion rotation = Quaternion.LookRotation(
                    ToUnityVector(combatant.Forward).normalized
                );
                Vector3 worldSize =
                    Abs(rotation * Vector3.right) * size.Width
                    + Abs(rotation * Vector3.up) * size.Height
                    + Abs(rotation * Vector3.forward) * size.Depth;
                hulls.Add(new Bounds(ToUnityVector(combatant.Position), worldSize));
            }
        }
        cameraController.Frame(hulls, initialFleetViewport);
    }

    /// <summary>
    /// Returns absolute component magnitudes for rotated hull dimensions.
    /// </summary>
    /// <param name="value">The rotated axis.</param>
    /// <returns>The absolute axis.</returns>
    private static Vector3 Abs(Vector3 value) =>
        new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

    /// <summary>
    /// Hides carried fighters until they physically begin leaving a bay.
    /// </summary>
    /// <param name="unit">The combatant to present.</param>
    /// <returns>Whether its hull is present in open space.</returns>
    private static bool IsVisible(CombatUnit unit) =>
        !unit.HasRetreated
        && unit.FlightState != FighterFlightState.Docked
        && unit.FlightState != FighterFlightState.Queued
        && !(unit.FlightState == FighterFlightState.Launching && unit.FlightDelay > 0f);

    /// <summary>
    /// Resolves the orbit pivot where the authored starting view crosses the participant's battle plane.
    /// </summary>
    /// <param name="cameraStart">The authored starting camera.</param>
    /// <returns>The initial orbit pivot inside the playable volume.</returns>
    private Vector3 ResolveCameraPivot(BattleMapCameraStart cameraStart)
    {
        BattleMapDeploymentRegion deploymentRegion = BattleMap
            .GetDeploymentRegions()
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.ParticipantSlotID,
                    cameraStart.ParticipantSlotID,
                    StringComparison.Ordinal
                )
            );
        BattleMapBounds pivotBounds = deploymentRegion?.Bounds ?? BattleMap.PlayableBounds;
        float pivotPlaneY = (pivotBounds.MinimumY + pivotBounds.MaximumY) * 0.5f;
        Vector3 cameraPosition = battleCamera.transform.position;
        Vector3 cameraForward = battleCamera.transform.forward;
        if (Mathf.Abs(cameraForward.y) > Mathf.Epsilon)
        {
            float intersectionDistance = (pivotPlaneY - cameraPosition.y) / cameraForward.y;
            if (intersectionDistance > 0f)
                return ClampToBounds(cameraPosition + cameraForward * intersectionDistance);
        }

        Vector3 boundsCenter = GetBoundsCenter(pivotBounds);
        float projectedDistance = Vector3.Dot(boundsCenter - cameraPosition, cameraForward);
        if (projectedDistance > 0f)
            return ClampToBounds(cameraPosition + cameraForward * projectedDistance);
        return ClampToBounds(boundsCenter);
    }

    /// <summary>
    /// Calculates the center of an authored battle volume.
    /// </summary>
    /// <param name="bounds">The authored bounds.</param>
    /// <returns>The volume center.</returns>
    private static Vector3 GetBoundsCenter(BattleMapBounds bounds)
    {
        return new Vector3(
            (bounds.MinimumX + bounds.MaximumX) * 0.5f,
            (bounds.MinimumY + bounds.MaximumY) * 0.5f,
            (bounds.MinimumZ + bounds.MaximumZ) * 0.5f
        );
    }

    /// <summary>
    /// Clamps a point to the active map's playable volume.
    /// </summary>
    /// <param name="point">The requested point.</param>
    /// <returns>The point inside the playable volume.</returns>
    private Vector3 ClampToBounds(Vector3 point)
    {
        BattleMapBounds bounds = BattleMap.PlayableBounds;
        return new Vector3(
            Mathf.Clamp(point.x, bounds.MinimumX, bounds.MaximumX),
            Mathf.Clamp(point.y, bounds.MinimumY, bounds.MaximumY),
            Mathf.Clamp(point.z, bounds.MinimumZ, bounds.MaximumZ)
        );
    }

    /// <summary>
    /// Rebuilds the unit-presentation hierarchy from the active battle snapshot.
    /// </summary>
    /// <returns>The presentation root for each combatant, used after its model finishes loading.</returns>
    private Dictionary<CombatUnit, Transform> RenderCombatants()
    {
        ClearBattleRoot();
        Dictionary<CombatUnit, Transform> presentations = new Dictionary<CombatUnit, Transform>();
        foreach (BattleParticipant participant in ActiveBattle.GetParticipants())
        {
            GameObject participantObject = new GameObject(
                $"Participant {participant.FactionInstanceID}"
            );
            participantObject.transform.SetParent(battleRoot, false);
            foreach (CombatUnit combatant in participant.GetCombatants())
                presentations.Add(
                    combatant,
                    RenderCombatant(combatant, participantObject.transform)
                );
        }
        return presentations;
    }

    /// <summary>
    /// Creates one presentation root for a combatant's model.
    /// </summary>
    /// <param name="combatant">The battle unit to present.</param>
    /// <param name="participantRoot">The owning participant's presentation root.</param>
    /// <returns>The combatant's presentation root.</returns>
    private Transform RenderCombatant(CombatUnit combatant, Transform participantRoot)
    {
        if (combatant == null)
            throw new InvalidOperationException("A battle participant contains a null combatant.");

        BaseSceneNode unit = combatant.GetUnit();
        if (unit == null)
            throw new InvalidOperationException("A combatant does not contain a battle unit.");

        string unitName =
            unit.DisplayName ?? unit.TypeID ?? combatant.SourceUnitInstanceID ?? "CombatUnit";
        GameObject unitObject = new GameObject(unitName);
        unitObject.transform.SetParent(participantRoot, false);
        unitObject.transform.localPosition = ToUnityVector(combatant.Position);

        Vector3 forward = ToUnityVector(combatant.Forward);
        if (forward.sqrMagnitude > Mathf.Epsilon)
            unitObject.transform.localRotation = Quaternion.LookRotation(forward.normalized);

        return unitObject.transform;
    }

    /// <summary>
    /// Removes presentation objects produced for a previous battle binding.
    /// </summary>
    private void ClearBattleRoot()
    {
        GetComponent<TacticalSelectionController>().Clear();
        ReleaseModels();
        modelLoading = new CancellationTokenSource();
        for (int childIndex = battleRoot.childCount - 1; childIndex >= 0; childIndex--)
        {
            GameObject child = battleRoot.GetChild(childIndex).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    /// <summary>
    /// Computes a far clipping plane that contains every corner of the playable volume.
    /// </summary>
    /// <param name="cameraPosition">The authored camera position.</param>
    /// <returns>A far clipping distance enclosing the full map.</returns>
    private float GetRequiredFarClipPlane(BattleMapVector3 cameraPosition)
    {
        BattleMapBounds bounds = BattleMap.PlayableBounds;
        float farthestDistance = 0f;
        foreach (float x in new[] { bounds.MinimumX, bounds.MaximumX })
        foreach (float y in new[] { bounds.MinimumY, bounds.MaximumY })
        foreach (float z in new[] { bounds.MinimumZ, bounds.MaximumZ })
        {
            float distance = Vector3.Distance(ToUnityVector(cameraPosition), new Vector3(x, y, z));
            farthestDistance = Mathf.Max(farthestDistance, distance);
        }

        return Mathf.Max(1000f, farthestDistance + 100f);
    }

    /// <summary>
    /// Converts a data-only battle vector into its Unity presentation value.
    /// </summary>
    /// <param name="value">The battle vector.</param>
    /// <returns>The corresponding Unity vector.</returns>
    private static Vector3 ToUnityVector(BattleVector3 value)
    {
        return value == null ? Vector3.zero : new Vector3(value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Converts a data-only map vector into its Unity presentation value.
    /// </summary>
    /// <param name="value">The battle-map vector.</param>
    /// <returns>The corresponding Unity vector.</returns>
    private static Vector3 ToUnityVector(BattleMapVector3 value)
    {
        return value == null ? Vector3.zero : new Vector3(value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Converts a data-only map color into its Unity presentation value.
    /// </summary>
    /// <param name="value">The battle-map color.</param>
    /// <returns>The corresponding Unity color.</returns>
    private static Color ToUnityColor(BattleMapColor value)
    {
        return value == null
            ? Color.black
            : new Color(value.Red, value.Green, value.Blue, value.Alpha);
    }
}
