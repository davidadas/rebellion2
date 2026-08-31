using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Resolves one typed scalar value for an event-local binding.
    /// </summary>
    [PersistableObject]
    public abstract class GameEventBindingSource
    {
        /// <summary>
        /// Gets the scalar type produced by this source.
        /// </summary>
        internal abstract Type ValueType { get; }

        /// <summary>
        /// Resolves the scalar value from the current game and evaluation context.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random provider available to nested selectors.</param>
        /// <param name="context">The current event evaluation context.</param>
        /// <returns>The resolved scalar value.</returns>
        internal abstract object Resolve(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        );
    }

    /// <summary>
    /// Resolves one officer's effective authored rating.
    /// </summary>
    [PersistableObject(Name = "OfficerRating")]
    public sealed class OfficerRatingBindingSource : GameEventBindingSource
    {
        // Officer.
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string OfficerBinding { get; set; }

        // Rating.
        [PersistableAttribute]
        public OfficerRating Rating { get; set; }

        internal override Type ValueType => typeof(int);

        /// <summary>
        /// Resolves the selected officer's effective rating.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random provider available to binding sources.</param>
        /// <param name="context">The current event evaluation context.</param>
        /// <returns>The officer's effective rating.</returns>
        internal override object Resolve(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        )
        {
            bool hasInstanceID = !string.IsNullOrWhiteSpace(OfficerInstanceID);
            bool hasBinding = !string.IsNullOrWhiteSpace(OfficerBinding);
            if (hasInstanceID == hasBinding)
                throw new InvalidOperationException(
                    "OfficerRating requires exactly one OfficerInstanceID or OfficerBinding."
                );
            Officer officer = hasBinding
                ? context?.GetBindingReference<Officer>(OfficerBinding)
                : game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException("OfficerRating could not resolve its officer.");
            return officer.GetEffectiveRating(Rating);
        }
    }

    /// <summary>
    /// Resolves one officer's current effective Force value.
    /// </summary>
    [PersistableObject(Name = "OfficerForce")]
    public sealed class OfficerForceBindingSource : GameEventBindingSource
    {
        // Officer.
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string OfficerBinding { get; set; }

        internal override Type ValueType => typeof(int);

        /// <summary>
        /// Resolves the selected officer's current Force rank.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random provider available to binding sources.</param>
        /// <param name="context">The current event evaluation context.</param>
        /// <returns>The officer's current Force rank.</returns>
        internal override object Resolve(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        )
        {
            bool hasInstanceID = !string.IsNullOrWhiteSpace(OfficerInstanceID);
            bool hasBinding = !string.IsNullOrWhiteSpace(OfficerBinding);
            if (hasInstanceID == hasBinding)
                throw new InvalidOperationException(
                    "OfficerForce requires exactly one OfficerInstanceID or OfficerBinding."
                );
            Officer officer = hasBinding
                ? context?.GetBindingReference<Officer>(OfficerBinding)
                : game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException("OfficerForce could not resolve its officer.");
            return officer.ForceRank;
        }
    }

    /// <summary>
    /// Resolves one authored statistic from a planet.
    /// </summary>
    [PersistableObject(Name = "PlanetStat")]
    public sealed class PlanetStatBindingSource : GameEventBindingSource
    {
        // Planet.
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        // Statistic.
        [PersistableAttribute]
        public PlanetStat Stat { get; set; }

        internal override Type ValueType => typeof(int);

        /// <summary>
        /// Resolves the selected planet's configured statistic.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random provider available to binding sources.</param>
        /// <param name="context">The current event evaluation context.</param>
        /// <returns>The planet statistic value.</returns>
        internal override object Resolve(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        )
        {
            bool hasInstanceID = !string.IsNullOrWhiteSpace(PlanetInstanceID);
            bool hasBinding = !string.IsNullOrWhiteSpace(PlanetBinding);
            if (hasInstanceID == hasBinding)
                throw new InvalidOperationException(
                    "PlanetStat requires exactly one PlanetInstanceID or PlanetBinding."
                );
            Planet planet = hasBinding
                ? context?.GetBindingReference<Planet>(PlanetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);
            if (planet == null)
                throw new InvalidOperationException("PlanetStat could not resolve its planet.");
            return planet.GetStatValue(Stat);
        }
    }

    /// <summary>
    /// Resolves the number of distinct scene nodes returned by authored selectors.
    /// </summary>
    [PersistableObject(Name = "SelectionCount")]
    public sealed class SelectionCountBindingSource : GameEventBindingSource
    {
        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        internal override Type ValueType => typeof(int);

        /// <summary>
        /// Counts the distinct scene nodes returned by the configured selectors.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random provider used by the selectors.</param>
        /// <param name="context">The current event evaluation context.</param>
        /// <returns>The number of distinct selected scene nodes.</returns>
        internal override object Resolve(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        )
        {
            if (Selectors.Count == 0)
                throw new InvalidOperationException(
                    "SelectionCount requires at least one selector."
                );
            return Selectors
                .SelectMany(selector => selector.Select(game, provider, context))
                .Distinct()
                .Count();
        }
    }

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

        [PersistableInlineCollection]
        public List<GameEventBindingSource> Sources { get; set; } =
            new List<GameEventBindingSource>();

        /// <summary>
        /// Gets the value type exposed by the configured binding source.
        /// </summary>
        /// <returns>The bound value type.</returns>
        internal Type GetValueType()
        {
            if (RollInteger != null)
                return typeof(int);
            if (RollDouble != null)
                return typeof(double);
            if (Sources.Count == 1)
                return Sources[0].ValueType;
            return typeof(ISceneNode);
        }

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
                + (RollDouble != null ? 1 : 0)
                + (Sources.Count > 0 ? 1 : 0);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    $"Binding '{As}' requires exactly one From, RollInteger, RollDouble, or typed value source."
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
            if (Sources.Count > 0)
            {
                if (Sources.Count != 1)
                    throw new InvalidOperationException(
                        $"Binding '{As}' requires exactly one typed value source."
                    );
                context.Bind(As, Sources[0].Resolve(game, provider, context));
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
