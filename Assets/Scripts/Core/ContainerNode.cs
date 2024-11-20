using System;
using System.Collections.Generic;

public abstract class ContainerNode : BaseSceneNode
{
    public override void Traverse(Action<ISceneNode> action)
    {
        action(this);

        foreach (var child in GetChildren())
        {
            child.Traverse(action);
        }
    }
}
