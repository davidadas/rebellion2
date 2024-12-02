using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ICollectionExtensions;
using IEnumerableExtensions;
using ObjectExtensions;

/// <summary>
/// Responsible for generating and deploying starfighters to the scene graph.
/// </summary>
public class StarfighterGenerator : UnitGenerator<Starfighter>
{
    /// <summary>
    /// Default constructor, constructs a StarfighterGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="resourceManager">The resource manager from which to load game data.</param>
    public StarfighterGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    /// Selects the starfighters that can be created in the game.
    /// </summary>
    /// <param name="starfighters">The available starfighters.</param>
    /// <returns>An array of all starfighters.</returns>
    public override Starfighter[] SelectUnits(Starfighter[] starfighters)
    {
        return starfighters
            .Where(starfighter =>
                starfighter.RequiredResearchLevel <= GetGameSummary().StartingResearchLevel
            )
            .ToArray();
    }

    /// <summary>
    /// Decorates the starfighters with additional properties or behavior.
    /// </summary>
    /// <param name="starfighters">The starfighters to decorate.</param>
    /// <returns>The decorated starfighters.</returns>
    public override Starfighter[] DecorateUnits(Starfighter[] starfighters)
    {
        // No op.
        return starfighters;
    }

    /// <summary>
    /// Deploys starfighters to the specified planet systems.
    /// </summary>
    /// <param name="starfighters">The starfighters to deploy.</param>
    /// <param name="destinations">The planet systems to deploy the starfighters to.</param>
    /// <returns>The deployed starfighters.</returns>
    public override Starfighter[] DeployUnits(
        Starfighter[] starfighters,
        PlanetSystem[] destinations
    )
    {
        List<Starfighter> deployedStarfighters = new List<Starfighter>();
        return deployedStarfighters.ToArray();
    }
}
