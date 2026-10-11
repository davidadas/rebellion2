using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.SceneGraph;
using Rebellion.Simulation.Tactical;
using UnityEngine;

/// <summary>
/// Composes the Unity tactical scene with the active battle and its authored map definition.
/// </summary>
public sealed class TacticalViewController : MonoBehaviour
{
    [SerializeField]
    private Camera battleCamera;

    [SerializeField]
    private Transform battleRoot;

    [SerializeField]
    private Transform mapRoot;

    [SerializeField]
    private Rect initialFleetViewport = new Rect(0.05f, 0.05f, 0.9f, 0.9f);

    private readonly List<ContentModelInstance> modelInstances = new List<ContentModelInstance>();
    private CancellationTokenSource modelLoading;

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
    }

    /// <summary>
    /// Resolves the active tactical battle after the scene becomes active.
    /// </summary>
    private async void Start()
    {
        try
        {
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
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
            await InitializeAsync(
                activeBattle,
                battleMap,
                bootstrap.GetContentModelCache(),
                viewerFactionInstanceID,
                game == null
            );
            if (this == null)
                return;
            ContentBindings.Apply(mapRoot.gameObject, bootstrap.GetContentAssets());
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
        foreach (KeyValuePair<CombatUnit, Transform> presentation in presentations)
        {
            presentation.Value.localPosition = ToUnityVector(presentation.Key.Position);
            Vector3 forward = ToUnityVector(presentation.Key.Forward);
            if (forward.sqrMagnitude > Mathf.Epsilon)
                presentation.Value.localRotation = Quaternion.LookRotation(forward.normalized);
        }
        ConfigureCamera(viewerFactionInstanceID);
        battleRoot.gameObject.SetActive(true);
    }

    /// <summary>Releases scene instances and cancels model loads when the tactical view closes.</summary>
    private void OnDestroy()
    {
        ReleaseModels();
    }

    /// <summary>Loads and centers a combatant's Unity model with its intentional uniform scale.</summary>
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
    }

    /// <summary>Releases only the instantiated models owned by this tactical view.</summary>
    private void ReleaseModels()
    {
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

    /// <summary>Returns absolute component magnitudes for rotated hull dimensions.</summary>
    /// <param name="value">The rotated axis.</param>
    /// <returns>The absolute axis.</returns>
    private static Vector3 Abs(Vector3 value) =>
        new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

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
