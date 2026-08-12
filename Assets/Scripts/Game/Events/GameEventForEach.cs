using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Defines the selected objects that receive independent event activation contexts.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventForEach
    {
        [PersistableInlineCollection]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public IReadOnlyList<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context = null
        )
        {
            if (Selectors.Count != 1)
                throw new InvalidOperationException("ForEach requires exactly one selector.");

            return Selectors[0]
                .Select(game, provider, context)
                .Distinct()
                .OrderBy(node => node.InstanceID, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
