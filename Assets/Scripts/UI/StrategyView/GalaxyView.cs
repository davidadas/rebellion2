using System;
using Rebellion.Game;
using UnityEngine;

/// <summary>
/// Responsible for rendering and handling interaction
/// for all PlanetSystem icons on the galaxy map.
/// </summary>
public sealed class GalaxyView : MonoBehaviour
{
    private GalaxyMap galaxyMap;
    private UIContext context;

    public event Action<PlanetSystem> OnSystemSelected;
    public event Action<PlanetSystem> OnSystemOpened;

    /// <summary>
    /// Initializes the map view with galaxy data.
    /// </summary>
    public void Initialize(GalaxyMap galaxyMap, UIContext context)
    {
        if (galaxyMap == null)
            throw new ArgumentNullException(nameof(galaxyMap));

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        this.galaxyMap = galaxyMap;
        this.context = context;

        Clear();
        BuildSystems();
    }

    /// <summary>
    /// Removes all existing system icons.
    /// </summary>
    private void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// Builds all planet systems in the galaxy.
    /// </summary>
    private void BuildSystems()
    {
        foreach (PlanetSystem system in galaxyMap.GetChildren())
        {
            CreateSystem(system);
        }
    }

    /// <summary>
    /// Creates a single system icon and wires interaction.
    /// </summary>
    private void CreateSystem(PlanetSystem system)
    {
        GameObject go = new GameObject(system.DisplayName);
        go.transform.SetParent(transform, false);

        PlanetSystemIcon icon = go.AddComponent<PlanetSystemIcon>();
        icon.Initialize(system, context, transform);

        icon.OnClicked += HandleSystemClicked;
        icon.OnDoubleClicked += HandleSystemDoubleClicked;
    }

    /// <summary>
    /// Single click selects system.
    /// </summary>
    private void HandleSystemClicked(PlanetSystem system)
    {
        OnSystemSelected?.Invoke(system);
    }

    /// <summary>
    /// Double click opens system panel.
    /// </summary>
    private void HandleSystemDoubleClicked(PlanetSystem system)
    {
        OnSystemOpened?.Invoke(system);
    }
}
