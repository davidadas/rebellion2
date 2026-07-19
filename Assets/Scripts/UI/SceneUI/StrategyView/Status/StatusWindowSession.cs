using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;

/// <summary>
/// Owns stable target identity and local state for one status window.
/// </summary>
internal sealed class StatusWindowSession
{
    private readonly Func<string, ISceneNode> findVisibleNode;
    private readonly string itemInstanceId;
    private readonly string planetInstanceId;
    private readonly bool rebindVisibleItem;
    private readonly ISceneNode staticItem;

    /// <summary>
    /// Creates one status-window session.
    /// </summary>
    /// <param name="window">The owning status window.</param>
    /// <param name="target">The displayed status target.</param>
    /// <param name="infoDisabled">Whether Encyclopedia navigation is unavailable.</param>
    /// <param name="findVisibleNode">Resolves a node from the current visible galaxy snapshot.</param>
    public StatusWindowSession(
        UIWindow window,
        StrategyStatusTarget target,
        bool infoDisabled,
        Func<string, ISceneNode> findVisibleNode
    )
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        this.findVisibleNode =
            findVisibleNode ?? throw new ArgumentNullException(nameof(findVisibleNode));
        InfoDisabled = infoDisabled;
        itemInstanceId = target.Item?.InstanceID;
        planetInstanceId = target.Planet?.Planet?.InstanceID ?? (target.Item as Planet)?.InstanceID;
        rebindVisibleItem =
            !string.IsNullOrEmpty(itemInstanceId) && findVisibleNode(itemInstanceId) != null;
        staticItem = rebindVisibleItem ? null : target.Item;
    }

    public bool InfoDisabled { get; }

    public StrategyStatusTarget Target { get; private set; }

    public UIWindow Window { get; }

    /// <summary>
    /// Rebinds snapshot-backed target references while preserving static template targets.
    /// </summary>
    /// <param name="sectors">The refreshed visible sectors.</param>
    /// <returns>True when every required snapshot-backed target still exists.</returns>
    public bool Reconcile(IReadOnlyList<GalaxyMapSector> sectors)
    {
        if (sectors == null)
            throw new ArgumentNullException(nameof(sectors));

        GalaxyMapPlanet planet = FindPlanet(sectors, planetInstanceId);
        if (!string.IsNullOrEmpty(planetInstanceId) && planet == null)
            return false;

        ISceneNode item = rebindVisibleItem ? findVisibleNode(itemInstanceId) : staticItem;
        if (rebindVisibleItem && item == null)
            return false;

        Target = new StrategyStatusTarget(planet, item, Target.ManufacturingType);
        return true;
    }

    /// <summary>
    /// Finds a visible strategy planet by stable scene-node identity.
    /// </summary>
    /// <param name="sectors">The current visible sectors.</param>
    /// <param name="instanceId">The requested planet identifier.</param>
    /// <returns>The refreshed planet projection, or null.</returns>
    private static GalaxyMapPlanet FindPlanet(
        IReadOnlyList<GalaxyMapSector> sectors,
        string instanceId
    )
    {
        if (string.IsNullOrEmpty(instanceId))
            return null;

        foreach (GalaxyMapSector sector in sectors)
        {
            foreach (GalaxyMapPlanet planet in sector?.Planets ?? Array.Empty<GalaxyMapPlanet>())
            {
                if (string.Equals(planet?.Planet?.InstanceID, instanceId, StringComparison.Ordinal))
                    return planet;
            }
        }

        return null;
    }
}
