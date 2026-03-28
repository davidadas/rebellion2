namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Exception thrown when a scene node access violation occurs between nodes whose owners do not match.
    /// </summary>
    public class SceneAccessException : SceneException
    {
        public ISceneNode Accessor { get; }
        public ISceneNode Accessee { get; }

        public SceneAccessException(ISceneNode accessor, ISceneNode accessee)
            : base(
                $"Cannot add \"{accessor.GetDisplayName()}\" to \"{accessee.GetDisplayName()}\". Owners do not match."
            )
        {
            Accessor = accessor;
            Accessee = accessee;
        }
    }
}
