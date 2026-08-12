using System;
using System.Collections.Generic;
using Rebellion.SceneGraph;

namespace Rebellion.Game
{
    /// <summary>
    /// Retains faction-owned scene nodes that are temporarily outside the active galaxy.
    /// </summary>
    public sealed class VoidPool : ContainerNode
    {
        public List<ISceneNode> Nodes { get; set; } = new List<ISceneNode>();

        public override bool CanAcceptChild(ISceneNode child)
        {
            return child != null && child.OwnerInstanceID == OwnerInstanceID;
        }

        public override void AddChild(ISceneNode child)
        {
            if (!CanAcceptChild(child))
                throw new InvalidOperationException(
                    $"Void pool '{InstanceID}' cannot retain '{child?.InstanceID}'."
                );

            Nodes.Add(child);
        }

        public override void RemoveChild(ISceneNode child)
        {
            Nodes.Remove(child);
        }

        public override IEnumerable<ISceneNode> GetChildren()
        {
            return Nodes.ToArray();
        }
    }
}
