using System;
using Rebellion.Game.Galaxy;

/// <summary>
/// Calculates energy values presented from a visible planet view.
/// </summary>
internal static class StrategyPlanetEnergy
{
    /// <summary>
    /// Gets the greatest known facility consumption from visible facilities and reserved energy.
    /// </summary>
    /// <param name="planet">The visible planet view.</param>
    /// <returns>The known number of consumed energy units.</returns>
    internal static int GetUsed(Planet planet)
    {
        if (planet == null)
            throw new ArgumentNullException(nameof(planet));

        return Math.Max(planet.AllocatedEnergy, planet.GetEnergyUsed());
    }

    /// <summary>
    /// Gets the energy capacity not consumed by visible or reserved facilities.
    /// </summary>
    /// <param name="planet">The visible planet view.</param>
    /// <returns>The known number of available energy units.</returns>
    internal static int GetAvailable(Planet planet)
    {
        if (planet == null)
            throw new ArgumentNullException(nameof(planet));

        return planet.IsDestroyed ? 0 : Math.Max(0, planet.EnergyCapacity - GetUsed(planet));
    }
}
