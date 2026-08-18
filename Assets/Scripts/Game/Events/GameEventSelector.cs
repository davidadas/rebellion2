using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
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
        internal abstract IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        );

        /// <summary>
        /// Returns registered nodes that remain attached to active gameplay containment.
        /// </summary>
        protected static IEnumerable<T> Active<T>(GameRoot game)
            where T : class, ISceneNode
        {
            return game.GetRegisteredSceneNodesByType<T>()
                .Where(node => node.GetParent() != null);
        }

        /// <summary>
        /// Returns whether a node is located at the explicitly named or bound planet.
        /// </summary>
        protected static bool MatchesLocation(
            ISceneNode node,
            GameEventExecutionContext context,
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
