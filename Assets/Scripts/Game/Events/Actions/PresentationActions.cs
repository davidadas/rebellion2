using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    internal static class DisplayActionTargets
    {
        internal static List<BaseGameEntity> Resolve(
            string targetInstanceID,
            IEnumerable<GameEventSelector> selectors,
            GameActionContext context,
            string actionName
        )
        {
            IEnumerable<ISceneNode> selected = (
                selectors ?? Enumerable.Empty<GameEventSelector>()
            ).SelectMany(selector =>
                selector.Select(context.Game, context.Random, context.Activation)
            );
            if (!string.IsNullOrWhiteSpace(targetInstanceID))
            {
                ISceneNode target = context.Game.GetSceneNodeByInstanceID<ISceneNode>(
                    targetInstanceID
                );
                if (target == null)
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve target '{targetInstanceID}'."
                    );
                selected = new[] { target }.Concat(selected);
            }

            List<ISceneNode> resolved = selected
                .Where(node => node != null)
                .Select(node => context.Game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID))
                .Where(node => node != null)
                .Distinct()
                .ToList();
            if (resolved.Count == 0)
                throw new InvalidOperationException(
                    $"{actionName} requires a resolvable target or selector."
                );
            if (resolved.Any(node => node is not BaseGameEntity))
                throw new InvalidOperationException(
                    $"{actionName} selectors may return only game entities."
                );
            return resolved.Cast<BaseGameEntity>().ToList();
        }
    }

    [PersistableObject(Name = "SetDisplayName")]
    public sealed class SetDisplayNameAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableAttribute]
        public string Name { get; set; }

        [PersistableInlineCollection]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.Resolve(
                    TargetInstanceID,
                    Selectors,
                    context,
                    "SetDisplayName"
                )
            )
                target.DisplayName = Name;
            return new List<GameResult>();
        }
    }

    [PersistableObject(Name = "SetDisplayStatus")]
    public sealed class SetDisplayStatusAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableAttribute]
        public string Status { get; set; }

        [PersistableInlineCollection]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.Resolve(
                    TargetInstanceID,
                    Selectors,
                    context,
                    "SetDisplayStatus"
                )
            )
                target.DisplayStatus = Status;
            return new List<GameResult>();
        }
    }

    [PersistableObject(Name = "ClearDisplayStatus")]
    public sealed class ClearDisplayStatusAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableInlineCollection]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.Resolve(
                    TargetInstanceID,
                    Selectors,
                    context,
                    "ClearDisplayStatus"
                )
            )
                target.DisplayStatus = null;
            return new List<GameResult>();
        }
    }
}
