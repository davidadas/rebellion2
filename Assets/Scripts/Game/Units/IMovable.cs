using Rebellion.Game.Movement;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// An interface for scene nodes/units that can be moved within the GalaxyMap.
    /// GetPosition/SetPosition provided via extension methods in Rebellion.Util.Extensions.
    /// </summary>
    public interface IMovable : ISceneNode
    {
        MovementState Movement { get; set; }

        /// <summary>
        /// Checks whether the movable condition is met.
        /// </summary>
        /// <returns>True when the movable condition is met; otherwise false.</returns>
        bool IsMovable();

        /// <summary>
        /// Checks whether the blockade condition is met.
        /// </summary>
        /// <returns>True when the blockade condition is met; otherwise false.</returns>
        bool CanBlockade() => false;

        /// <summary>
        /// Executes ignores blockade.
        /// </summary>
        /// <returns>True when the operation succeeds; otherwise false.</returns>
        bool IgnoresBlockade() => false;
    }
}
