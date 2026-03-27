using System;
using System.Drawing;
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

        /// <summary>
        /// Used to determine whether this IMovable can be moved.
        /// </summary>
        /// <returns>True if the IMovable can be moved, false otherwise.</returns>
        bool IsMovable();

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        bool CanBlockade()
        {
            return false;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        bool IgnoresBlockade()
        {
            return false;
        }
    }
}
