using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// A <see cref="GameConditional"/> that is met when all child conditions are met.
    /// </summary>
    [PersistableObject(Name = "And")]
    public class AndConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public AndConditional()
            : base() { }

        /// <summary>
        /// Evaluates the AND composition: all child conditions must be met.
        /// </summary>
        /// <param name="game">The game state to evaluate against.</param>
        /// <returns>True if every child condition is met; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            return Conditionals.All(conditional => conditional.IsMet(game));
        }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return Conditionals.All(conditional => conditional.IsMet(game, triggerResult));
        }

        public override bool IsMet(GameRoot game, GameEventExecutionContext context) =>
            Conditionals.All(conditional => conditional.IsMet(game, context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when any child condition is met.
    /// </summary>
    [PersistableObject(Name = "Or")]
    public class OrConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public OrConditional()
            : base() { }

        /// <summary>
        /// Evaluates the OR composition: at least one child condition must be met.
        /// </summary>
        /// <param name="game">The game state to evaluate against.</param>
        /// <returns>True if any child condition is met; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            return Conditionals.Any(conditional => conditional.IsMet(game));
        }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return Conditionals.Any(conditional => conditional.IsMet(game, triggerResult));
        }

        public override bool IsMet(GameRoot game, GameEventExecutionContext context) =>
            Conditionals.Any(conditional => conditional.IsMet(game, context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when none of the child conditions are met.
    /// </summary>
    [PersistableObject(Name = "Not")]
    public class NotConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public NotConditional()
            : base() { }

        /// <summary>
        /// Evaluates the NOT composition: no child condition may be met.
        /// </summary>
        /// <param name="game">The game state to evaluate against.</param>
        /// <returns>True if every child condition is unmet; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            return Conditionals.All(conditional => !conditional.IsMet(game));
        }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return Conditionals.All(conditional => !conditional.IsMet(game, triggerResult));
        }

        public override bool IsMet(GameRoot game, GameEventExecutionContext context) =>
            Conditionals.All(conditional => !conditional.IsMet(game, context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when exactly one child condition is met.
    /// </summary>
    [PersistableObject(Name = "Xor")]
    public class XorConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public XorConditional()
            : base() { }

        /// <summary>
        /// Evaluates the XOR composition: exactly one child condition must be met.
        /// </summary>
        /// <param name="game">The game state to evaluate against.</param>
        /// <returns>True if precisely one child condition is met; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            return Conditionals.Count(conditional => conditional.IsMet(game)) == 1;
        }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return Conditionals.Count(conditional => conditional.IsMet(game, triggerResult)) == 1;
        }

        public override bool IsMet(GameRoot game, GameEventExecutionContext context) =>
            Conditionals.Count(conditional => conditional.IsMet(game, context)) == 1;
    }
}
