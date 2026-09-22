using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects registered scene nodes for an authored game-event operation.
    /// </summary>
    [PersistableObject]
    public abstract class GameEventSelector
    {
        /// <summary>
        /// Selects nodes from the current game state for one event activation.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="context">The context.</param>
        /// <returns>The selected value.</returns>
        internal abstract IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        );

        /// <summary>
        /// Returns registered nodes that remain attached to active gameplay containment.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <typeparam name="T">The active scene-node type to select.</typeparam>
        /// <returns>The attached, active nodes of the requested type.</returns>
        protected static IEnumerable<T> Active<T>(GameRoot game)
            where T : class, ISceneNode
        {
            return game.GetRegisteredSceneNodesByType<T>().Where(node => node.GetParent() != null);
        }

        /// <summary>
        /// Returns whether a node is located at the explicitly named or bound planet.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="context">The context.</param>
        /// <param name="planetInstanceID">The planet instance id.</param>
        /// <param name="planetBinding">The planet binding.</param>
        /// <returns>True when the value matches location; otherwise false.</returns>
        protected static bool MatchesLocation(
            ISceneNode node,
            GameEventEvaluationContext context,
            string planetInstanceID,
            string planetBinding
        )
        {
            Planet planet = !string.IsNullOrWhiteSpace(planetBinding)
                ? context?.GetBindingReference<Planet>(planetBinding)
                : null;
            string expected = planet?.InstanceID ?? planetInstanceID;
            return string.IsNullOrWhiteSpace(expected)
                || node is Planet selectedPlanet && selectedPlanet.InstanceID == expected
                || node.GetParentOfType<Planet>()?.InstanceID == expected;
        }
    }
}
