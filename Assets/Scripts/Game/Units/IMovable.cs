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
        /// Gets whether this unit is currently eligible to move.
        /// </summary>
        /// <returns>True when this unit can move; otherwise false.</returns>
        bool IsMovable();

        /// <summary>
        /// Gets whether this unit can impose a blockade.
        /// </summary>
        /// <returns>True when this unit can impose a blockade; otherwise false.</returns>
        bool CanBlockade() => false;

        /// <summary>
        /// Gets whether this unit can move despite a blockade.
        /// </summary>
        /// <returns>True when blockades do not prevent this unit from moving; otherwise false.</returns>
        bool IgnoresBlockade() => false;
    }
}
