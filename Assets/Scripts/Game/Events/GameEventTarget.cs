using System;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects the concrete scene node against which an event executes.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventTarget
    {
        public PlanetTarget Planet { get; set; }
        public RandomPlanetsTarget RandomPlanets { get; set; }

        /// <summary>
        /// Resolves the single configured target selector.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The deterministic random source used by random selectors.</param>
        /// <returns>The selected scene node, or null when no eligible target exists.</returns>
        public ISceneNode Resolve(GameRoot game, IRandomNumberProvider provider)
        {
            if (Planet != null && RandomPlanets != null)
                throw new InvalidOperationException(
                    "An event can define only one target selector."
                );

            return Planet?.Resolve(game) ?? RandomPlanets?.Resolve(game, provider);
        }
    }

    /// <summary>
    /// Selects one authored planet by instance ID.
    /// </summary>
    [PersistableObject]
    public sealed class PlanetTarget
    {
        [PersistableAttribute]
        public string InstanceID { get; set; }

        /// <summary>
        /// Resolves the configured planet when it exists and has not been destroyed.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The eligible planet, or null.</returns>
        public Planet Resolve(GameRoot game)
        {
            Planet planet = game.GetSceneNodeByInstanceID<Planet>(InstanceID);
            return planet?.IsDestroyed == false ? planet : null;
        }
    }

    /// <summary>
    /// Selects random eligible planets from one type of planetary system.
    /// </summary>
    [PersistableObject]
    public sealed class RandomPlanetsTarget
    {
        [PersistableAttribute]
        public int Count { get; set; } = 1;

        [PersistableAttribute]
        public PlanetSystemType SystemType { get; set; } = PlanetSystemType.CoreSystem;

        /// <summary>
        /// Selects an eligible planet in stable instance-ID order.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The deterministic random source used for selection.</param>
        /// <returns>The selected planet, or null when no eligible planet exists.</returns>
        public Planet Resolve(GameRoot game, IRandomNumberProvider provider)
        {
            if (Count != 1)
                throw new InvalidOperationException(
                    "RandomPlanets currently supports exactly one target."
                );

            Planet[] candidates = game.GetGalaxyMap()
                .PlanetSystems.Where(system => system.SystemType == SystemType)
                .SelectMany(system => system.Planets)
                .Where(planet => !planet.IsDestroyed)
                .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .ToArray();
            return candidates.Length == 0
                ? null
                : candidates[provider.NextInt(0, candidates.Length)];
        }
    }
}
