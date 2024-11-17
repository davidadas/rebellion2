using System;
using System.Collections.Generic;

public class SceneNodeContainer<TSceneNode>
    where TSceneNode : SceneNode
{
    IEnumerable<TSceneNode> SceneNodes { get; set; }
}
