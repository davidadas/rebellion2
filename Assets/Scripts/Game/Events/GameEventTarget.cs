using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects the single scene node targeted by one event activation.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventTarget
    {
        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Resolves the authored selector and requires exactly one target.
        /// </summary>
        public ISceneNode Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context = null
        )
        {
            if (Selectors.Count != 1)
                throw new InvalidOperationException("Target requires exactly one selector.");

            ISceneNode[] targets = Selectors[0]
                .Select(game, provider, context)
                .Distinct()
                .ToArray();
            if (targets.Length != 1)
                throw new InvalidOperationException(
                    $"Target selector must resolve exactly one object but resolved {targets.Length}."
                );
            return targets[0];
        }
    }
}
