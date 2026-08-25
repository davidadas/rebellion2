using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Assigns an explicitly selected value to an event-local binding name.
    /// </summary>
    [PersistableObject(Name = "Bind")]
    public sealed class GameEventBinding
    {
        // Binding Configuration.
        [PersistableAttribute]
        public string Argument { get; set; }

        [PersistableAttribute]
        public string As { get; set; }

        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Resolves the selected scene node and adds it to the evaluation context.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random number provider used by selectors.</param>
        /// <param name="context">The event evaluation context receiving the binding.</param>
        internal void Bind(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        )
        {
            if (Selectors.Count != 1)
                throw new InvalidOperationException(
                    $"Selection binding '{As}' requires exactly one selector."
                );

            ISceneNode[] values = Selectors[0].Select(game, provider, context).Distinct().ToArray();
            if (values.Length != 1)
                throw new InvalidOperationException(
                    $"Selection binding '{As}' must resolve exactly one object but resolved {values.Length}."
                );
            context.Bind(As, values[0]);
        }
    }
}
