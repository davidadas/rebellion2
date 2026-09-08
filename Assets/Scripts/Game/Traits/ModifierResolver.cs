using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

namespace Rebellion.Game.Traits
{
    /// <summary>
    /// Resolves content-authored modifiers against the current game state.
    /// Definitions remain passive data; this class owns targeting and stacking rules.
    /// </summary>
    public sealed class ModifierResolver
    {
        private readonly GameRoot _game;
        private readonly IReadOnlyDictionary<string, Trait> _traits;
        private readonly IReadOnlyDictionary<string, StatusEffect> _statusEffects;
        private readonly Func<ModifierType, ISceneNode, IEnumerable<Modifier>> _contextualModifiers;
        private int _sourceCacheTick = int.MinValue;
        private List<ISceneNode> _sourceCache;

        public ModifierResolver(
            GameRoot game,
            IEnumerable<Trait> traits,
            IEnumerable<StatusEffect> statusEffects,
            Func<ModifierType, ISceneNode, IEnumerable<Modifier>> contextualModifiers = null
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _traits = IndexByID(traits, trait => trait.ID, "trait");
            _statusEffects = IndexByID(
                statusEffects,
                statusEffect => statusEffect.ID,
                "status effect"
            );
            _contextualModifiers = contextualModifiers;
        }

        /// <summary>
        /// Applies every active modifier of the requested type to a value.
        /// Additions are applied first, multipliers second, and an override last.
        /// </summary>
        public decimal Resolve(ModifierType type, ISceneNode subject, decimal baseValue)
        {
            return Resolve(type, subject, baseValue, Enumerable.Empty<Modifier>());
        }

        /// <summary>
        /// Applies active authored modifiers and caller-supplied contextual modifiers.
        /// </summary>
        public decimal Resolve(
            ModifierType type,
            ISceneNode subject,
            decimal baseValue,
            IEnumerable<Modifier> contextualModifiers
        )
        {
            if (subject == null)
                throw new ArgumentNullException(nameof(subject));

            List<Modifier> modifiers = GetApplicableModifiers(type, subject)
                .Concat(_contextualModifiers?.Invoke(type, subject) ?? Enumerable.Empty<Modifier>())
                .Concat(contextualModifiers ?? Enumerable.Empty<Modifier>())
                .Where(modifier => modifier.Type == type)
                .ToList();
            decimal value = baseValue;
            foreach (
                Modifier modifier in modifiers.Where(m => m.Operation == ModifierOperation.Add)
            )
                value += modifier.Value;
            foreach (
                Modifier modifier in modifiers.Where(m => m.Operation == ModifierOperation.Multiply)
            )
                value *= modifier.Value;

            Modifier overriding = modifiers.LastOrDefault(m =>
                m.Operation == ModifierOperation.Override
            );
            return overriding?.Value ?? value;
        }

        /// <summary>
        /// Resolves an integer value using midpoint rounding away from zero.
        /// </summary>
        public int Resolve(ModifierType type, ISceneNode subject, int baseValue)
        {
            return checked(
                (int)
                    decimal.Round(
                        Resolve(type, subject, (decimal)baseValue),
                        0,
                        MidpointRounding.AwayFromZero
                    )
            );
        }

        public bool HasStatusEffect(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && _statusEffects.ContainsKey(id);
        }

        /// <summary>
        /// Invalidates cached modifier sources after traits, effects, or scene membership change.
        /// </summary>
        public void Invalidate()
        {
            _sourceCache = null;
            _sourceCacheTick = int.MinValue;
        }

        /// <summary>
        /// Removes status effects whose exclusive expiration tick has been reached.
        /// </summary>
        public void RemoveExpiredStatusEffects()
        {
            foreach (ISceneNode node in GetSceneNodes())
            {
                if (!(node is IStatusEffectTarget target) || target.ActiveStatusEffects == null)
                    continue;

                foreach (
                    string id in target
                        .ActiveStatusEffects.Where(pair =>
                            pair.Value.HasValue && pair.Value.Value <= _game.CurrentTick
                        )
                        .Select(pair => pair.Key)
                        .ToList()
                )
                {
                    target.ActiveStatusEffects.Remove(id);
                }
            }
            Invalidate();
        }

        private IEnumerable<Modifier> GetApplicableModifiers(ModifierType type, ISceneNode subject)
        {
            foreach (ISceneNode source in GetModifierSources())
            {
                foreach (Modifier modifier in GetSourceModifiers(source))
                {
                    if (
                        modifier.Type == type
                        && MatchesRecipient(modifier.Recipient, subject)
                        && MatchesTarget(modifier, source, subject)
                    )
                    {
                        yield return modifier;
                    }
                }
            }
        }

        private IEnumerable<Modifier> GetSourceModifiers(ISceneNode source)
        {
            if (source is ITraitTarget traitTarget && traitTarget.TraitIDs != null)
            {
                foreach (string id in traitTarget.TraitIDs)
                {
                    if (_traits.TryGetValue(id, out Trait trait) && trait.Effects != null)
                    {
                        foreach (Modifier modifier in trait.Effects)
                            yield return modifier;
                    }
                }
            }

            if (
                !(source is IStatusEffectTarget statusTarget)
                || statusTarget.ActiveStatusEffects == null
            )
                yield break;

            foreach (KeyValuePair<string, int?> applied in statusTarget.ActiveStatusEffects)
            {
                if (applied.Value.HasValue && applied.Value.Value <= _game.CurrentTick)
                    continue;
                if (
                    _statusEffects.TryGetValue(applied.Key, out StatusEffect statusEffect)
                    && statusEffect.Modifiers != null
                )
                {
                    foreach (Modifier modifier in statusEffect.Modifiers)
                        yield return modifier;
                }
            }
        }

        private static bool MatchesRecipient(ModifierRecipient recipient, ISceneNode subject)
        {
            return recipient switch
            {
                ModifierRecipient.Any => true,
                ModifierRecipient.Officer => subject is Officer,
                ModifierRecipient.CapitalShip => subject is CapitalShip,
                ModifierRecipient.Fleet => subject is Fleet,
                ModifierRecipient.Planet => subject is Planet,
                ModifierRecipient.Building => subject is Building,
                _ => false,
            };
        }

        private static bool MatchesTarget(Modifier modifier, ISceneNode source, ISceneNode subject)
        {
            if (modifier.Target == ModifierTarget.Faction)
            {
                string subjectOwnerID = subject.GetOwnerInstanceID();
                string targetOwnerID = string.IsNullOrWhiteSpace(modifier.TargetID)
                    ? source.GetOwnerInstanceID()
                    : modifier.TargetID;
                return !string.IsNullOrEmpty(targetOwnerID) && subjectOwnerID == targetOwnerID;
            }

            ISceneNode target = GetScopeNode(modifier.Target, source);
            ISceneNode subjectScope = GetScopeNode(modifier.Target, subject);
            if (!string.IsNullOrWhiteSpace(modifier.TargetID))
                return subjectScope?.InstanceID == modifier.TargetID;

            if (modifier.Target == ModifierTarget.MovementGroup)
            {
                string sourceGroup = GetMovement(source)?.MovementGroupID;
                string subjectGroup = GetMovement(subject)?.MovementGroupID;
                return !string.IsNullOrEmpty(sourceGroup) && sourceGroup == subjectGroup;
            }

            return target != null
                && subjectScope != null
                && target.InstanceID == subjectScope.InstanceID;
        }

        private static ISceneNode GetScopeNode(ModifierTarget target, ISceneNode node)
        {
            return target switch
            {
                ModifierTarget.Self => node,
                ModifierTarget.Faction => null,
                ModifierTarget.Planet => node as Planet ?? node.GetParentOfType<Planet>(),
                ModifierTarget.Sector => node as PlanetSector
                    ?? node.GetParentOfType<PlanetSector>(),
                ModifierTarget.MovementGroup => node,
                _ => null,
            };
        }

        private static MovementState GetMovement(ISceneNode node)
        {
            return node is IMovable movable ? movable.GetTransitMovement() : null;
        }

        private List<ISceneNode> GetSceneNodes()
        {
            List<ISceneNode> nodes = new List<ISceneNode>();
            _game.GetGalaxyMap().Traverse(nodes.Add);
            return nodes;
        }

        private IReadOnlyList<ISceneNode> GetModifierSources()
        {
            if (_sourceCache != null && _sourceCacheTick == _game.CurrentTick)
                return _sourceCache;

            _sourceCache = GetSceneNodes()
                .Where(node =>
                    node is ITraitTarget traitTarget && traitTarget.TraitIDs?.Count > 0
                    || node is IStatusEffectTarget statusTarget
                        && statusTarget.ActiveStatusEffects?.Count > 0
                )
                .ToList();
            _sourceCacheTick = _game.CurrentTick;
            return _sourceCache;
        }

        private static IReadOnlyDictionary<string, T> IndexByID<T>(
            IEnumerable<T> values,
            Func<T, string> getID,
            string label
        )
        {
            Dictionary<string, T> indexed = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T value in values ?? Enumerable.Empty<T>())
            {
                string id = getID(value);
                if (string.IsNullOrWhiteSpace(id))
                    throw new InvalidOperationException($"A {label} has no ID.");
                if (!indexed.TryAdd(id, value))
                    throw new InvalidOperationException($"Duplicate {label} ID '{id}'.");
            }
            return indexed;
        }
    }
}
