public class GameLeaf : GameNode
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="childNode"></param>
    protected override void AddChildNode(GameNode childNode)
    {
        throw new SceneException(
            this,
            childNode,
            SceneExceptionType.Insertion,
            "Accessor is a leaf node."
        );
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="childNode"></param>
    protected override void RemoveChildNode(GameNode childNode)
    {
        throw new SceneException(
            this,
            childNode,
            SceneExceptionType.Deletion,
            "Accessor is a leaf node."
        );
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        // Leaf node.
        return new GameNode[] { };
    }
}
