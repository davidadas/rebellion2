using System;
using Rebellion.Game.Galaxy;
using UnityEngine;

public sealed class MissionsPane : MonoBehaviour
{
    private Planet planet;
    private UIContext uiContext;

    public void Initialize(Planet planet, UIContext uiContext)
    {
        if (planet == null)
            throw new ArgumentNullException(nameof(planet));

        if (uiContext == null)
            throw new ArgumentNullException(nameof(uiContext));

        this.planet = planet;
        this.uiContext = uiContext;
    }

    /// <summary>
    /// Updates the planet rendered by this pane while preserving initialized UI state.
    /// </summary>
    /// <param name="planet">The replacement planet view.</param>
    public void SetPlanet(Planet planet)
    {
        if (planet == null)
            throw new ArgumentNullException(nameof(planet));

        this.planet = planet;
    }
}
