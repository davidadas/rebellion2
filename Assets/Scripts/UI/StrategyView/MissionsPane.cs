using System;
using Rebellion.Game;
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
}
