using Rebellion.SceneGraph;

namespace Rebellion.Game
{
    public enum MovementStatus
    {
        Idle,
        InTransit,
    }

    /// <summary>
    /// An interface for scene nodes/units that can be moved within the GalaxyMap.
    /// GetPosition/SetPosition provided via extension methods in Rebellion.Util.Extensions.
    /// </summary>
    public interface IMovable : ISceneNode
    {
        MovementState Movement { get; set; }

        bool IsMovable();

        bool CanBlockade() => false;

        bool IgnoresBlockade() => false;
    }
}
