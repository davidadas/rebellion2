using System.Collections.Generic;
using System.Linq;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// A <see cref="GameConditional"/> that is met when all child conditions are met.
    /// </summary>
    [PersistableObject(Name = "All")]
    public sealed class AllConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public AllConditional()
            : base() { }

        /// <summary>
        /// Evaluates the AND composition: all child conditions must be met.
        /// </summary>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if every child condition is met; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context) =>
            Conditionals.All(conditional => conditional.IsMet(context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when any child condition is met.
    /// </summary>
    [PersistableObject(Name = "Any")]
    public sealed class AnyConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public AnyConditional()
            : base() { }

        /// <summary>
        /// Evaluates the OR composition: at least one child condition must be met.
        /// </summary>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if any child condition is met; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context) =>
            Conditionals.Any(conditional => conditional.IsMet(context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when none of the child conditions are met.
    /// </summary>
    [PersistableObject(Name = "Not")]
    public sealed class NotConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public NotConditional()
            : base() { }

        /// <summary>
        /// Evaluates the NOT composition: no child condition may be met.
        /// </summary>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if every child condition is unmet; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context) =>
            Conditionals.All(conditional => !conditional.IsMet(context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when exactly one child condition is met.
    /// </summary>
    [PersistableObject(Name = "Xor")]
    public sealed class XorConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public XorConditional()
            : base() { }

        /// <summary>
        /// Evaluates the XOR composition: exactly one child condition must be met.
        /// </summary>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if precisely one child condition is met; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context) =>
            Conditionals.Count(conditional => conditional.IsMet(context)) == 1;
    }
}
