using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game
{
    /// <summary>
    /// Owns transitions between active scene-graph placement and retained off-map state.
    /// </summary>
    public sealed class UnitLifecycleService
    {
        private readonly GameRoot _game;

        public UnitLifecycleService(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        public void AddToVoid(ISceneNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (string.IsNullOrEmpty(node.OwnerInstanceID))
                throw new InvalidOperationException(
                    "Only faction-owned entities can enter a void pool."
                );

            Faction faction = _game.GetFactionByOwnerInstanceID(node.OwnerInstanceID);
            List<ISceneNode> pool = faction.VoidPool ??= new List<ISceneNode>();
            if (pool.Contains(node))
                return;
            if (node.GetParent() == null)
                throw new InvalidOperationException(
                    $"{node.GetDisplayName()} is not attached to the scene graph."
                );
            if (node is not IMovable)
                throw new InvalidOperationException(
                    $"{node.GetType().Name} cannot enter a void pool."
                );

            ISceneNode previousParent = node.GetParent();
            _game.DetachNode(node);
            try
            {
                pool.Add(node);
                node.Traverse(_game.AddSceneNodeByInstanceID);
            }
            catch
            {
                pool.Remove(node);
                node.Traverse(_game.RemoveSceneNodeByInstanceID);
                _game.AttachNode(node, previousParent);
                throw;
            }
        }

        public void SetStatus(ISceneNode node, VoidStatus? status, string displayText = null)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (!IsInVoid(node))
                throw new InvalidOperationException(
                    $"{node.GetDisplayName()} is not in a void pool."
                );
            ((IMovable)node).VoidState =
                status.HasValue || !string.IsNullOrWhiteSpace(displayText)
                    ? new VoidState { Status = status, DisplayText = displayText }
                    : null;
        }

        public void Initialize()
        {
            foreach (Faction faction in _game.Factions)
            {
                faction.VoidPool ??= new List<ISceneNode>();
                foreach (ISceneNode node in faction.VoidPool)
                {
                    node.SetParent(null);
                    node.Traverse(child =>
                    {
                        ISceneNode registered = _game.GetSceneNodeByInstanceID<ISceneNode>(
                            child.InstanceID
                        );
                        if (registered != null)
                        {
                            if (!ReferenceEquals(registered, child))
                                throw new InvalidOperationException(
                                    $"Duplicate scene node instance ID '{child.InstanceID}' exists in a void pool."
                                );
                            return;
                        }
                        _game.AddSceneNodeByInstanceID(child);
                    });
                }
            }
        }

        public void Activate(ISceneNode node, ContainerNode destination)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            Faction faction = _game.GetFactionByOwnerInstanceID(node.OwnerInstanceID);
            List<ISceneNode> pool = faction.VoidPool ??= new List<ISceneNode>();
            if (!pool.Contains(node))
                throw new InvalidOperationException(
                    $"{node.GetDisplayName()} is not in a void pool."
                );
            if (!destination.CanAcceptChild(node))
                throw new InvalidOperationException(
                    $"{destination.GetDisplayName()} cannot accept {node.GetDisplayName()}."
                );

            pool.Remove(node);
            node.Traverse(_game.RemoveSceneNodeByInstanceID);
            try
            {
                _game.AttachNode(node, destination);
                ((IMovable)node).VoidState = null;
            }
            catch
            {
                pool.Add(node);
                node.Traverse(_game.AddSceneNodeByInstanceID);
                throw;
            }
        }

        public bool IsInVoid(ISceneNode node)
        {
            for (ISceneNode current = node; current != null; current = current.GetParent())
            {
                if (_game.Factions.Any(faction => faction.VoidPool?.Contains(current) == true))
                    return true;
            }
            return false;
        }

        public void ChangeOwnership(ISceneNode node, string ownerInstanceId)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            Faction faction = _game.GetFactionByOwnerInstanceID(ownerInstanceId);
            Faction previousFaction = string.IsNullOrWhiteSpace(node.OwnerInstanceID)
                ? null
                : _game.GetFactionByOwnerInstanceID(node.OwnerInstanceID);
            bool isVoidRoot = previousFaction?.VoidPool?.Contains(node) == true;

            if (isVoidRoot)
                previousFaction.VoidPool.Remove(node);
            else
                _game.DeregsiterOwnedUnit(node);

            node.SetOwnerInstanceID(ownerInstanceId);
            if (isVoidRoot)
                (faction.VoidPool ??= new List<ISceneNode>()).Add(node);
            else
                faction.AddOwnedUnit(node);
        }
    }
}
