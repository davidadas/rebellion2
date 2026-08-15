using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    [PersistableObject]
    public abstract class GameEventSelector
    {
        internal abstract IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        );

        protected static IEnumerable<T> Active<T>(GameRoot game)
            where T : class, ISceneNode
        {
            return game.GetRegisteredSceneNodesByType<T>()
                .Where(node => node.GetParent() != null && !game.IsInVoid(node));
        }

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

        protected static bool MatchesActiveOrRecordedLocation(
            GameRoot game,
            ISceneNode node,
            GameEventExecutionContext context,
            string planetInstanceID,
            string planetBinding
        )
        {
            if (MatchesLocation(node, context, planetInstanceID, planetBinding))
                return true;
            if (!game.IsInVoid(node))
                return false;

            Planet planet = !string.IsNullOrWhiteSpace(planetBinding)
                ? context?.GetBindingReference<Planet>(planetBinding)
                : null;
            string expected = planet?.InstanceID ?? planetInstanceID;
            if (string.IsNullOrWhiteSpace(expected))
                return true;

            ISceneNode recordedParent = game.GetSceneNodeByInstanceID<ISceneNode>(
                node.LastParentInstanceID
            );
            return recordedParent is Planet recordedPlanet && recordedPlanet.InstanceID == expected
                || recordedParent?.GetParentOfType<Planet>()?.InstanceID == expected;
        }
    }
}
