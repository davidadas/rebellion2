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
        public RollInteger RollInteger { get; set; }
        public RollDouble RollDouble { get; set; }

        /// <summary>
        /// Resolves the authored source and stores its value in the evaluation context.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random number provider used by selectors and rolls.</param>
        /// <param name="context">The event evaluation context that receives the binding.</param>
        internal void Bind(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        )
        {
            int modeCount =
                (Selectors.Count > 0 ? 1 : 0)
                + (RollInteger != null ? 1 : 0)
                + (RollDouble != null ? 1 : 0);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    $"Binding '{As}' requires exactly one selector, RollInteger, or RollDouble."
                );

            if (RollInteger != null)
            {
                context.Bind(As, RollInteger.Roll(provider));
                return;
            }
            if (RollDouble != null)
            {
                context.Bind(As, RollDouble.Roll(provider));
                return;
            }

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
