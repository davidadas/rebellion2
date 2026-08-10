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
    private static readonly string[] ModelLods = { "close", "medium", "far" };
    private static readonly float[] LodScreenHeights = { 0.35f, 0.12f, 0.01f };
    private static readonly string[] FighterGroupColors = { "red", "blue", "green", "gold" };
    private readonly List<Transform> fighterBillboards = new List<Transform>();
    private readonly List<ContentModelInstance> modelInstances = new List<ContentModelInstance>();
    private readonly List<Sprite> sprites = new List<Sprite>();
    private readonly List<TacticalUnitView> unitViews = new List<TacticalUnitView>();
    private bool initialized;

    /// <summary>
    /// Loads and creates all model-backed tactical units.
    /// </summary>
    /// <param name="session">The active tactical session.</param>
    /// <param name="modelCache">The application-owned model cache.</param>
    /// <param name="contentAssets">The active external content assets.</param>
    /// <param name="cancellationToken">Token that cancels scene initialization.</param>
    public async Task InitializeAsync(
        TacticalBattleSession session,
        ContentModelCache modelCache,
        IContentAssetSource contentAssets,
        CancellationToken cancellationToken
    )
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));
        if (modelCache == null)
            throw new ArgumentNullException(nameof(modelCache));
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));
        if (initialized)
            throw new InvalidOperationException("Tactical presentation is already initialized.");

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

        initialized = true;
        Synchronize();
    }

    /// <summary>
    /// Applies the latest tactical transforms to every loaded unit.
    /// </summary>
    public void Synchronize()
    {
        foreach (TacticalUnitView unitView in unitViews)
            unitView.Synchronize();

        Camera battleCamera = Camera.main;
        if (battleCamera == null)
            return;

        foreach (Transform billboard in fighterBillboards)
            billboard.rotation = battleCamera.transform.rotation;
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
        unitViews.Clear();
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
        unitView.Initialize(unit);
        unitViews.Add(unitView);
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
        unitView.Initialize(unit);
        unitViews.Add(unitView);
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
}
