using Rebellion.Util.Serialization;

namespace Rebellion.Game.UIState
{
    /// <summary>
    /// Stores the common state required to restore one interface window.
    /// </summary>
    [PersistableObject]
    public sealed class WindowState
    {
        [PersistableMember(Name = "WindowTypeID")]
        private string _windowTypeID;

        [PersistableMember(Name = "TargetInstanceID")]
        private string _targetInstanceID;

        [PersistableMember(Name = "X")]
        private int _x;

        [PersistableMember(Name = "Y")]
        private int _y;

        [PersistableMember(Name = "Width")]
        private int _width;

        [PersistableMember(Name = "Height")]
        private int _height;

        [PersistableMember(Name = "ZOrder")]
        private int _zOrder;

        /// <summary>
        /// Creates an empty window state for deserialization.
        /// </summary>
        public WindowState() { }

        /// <summary>
        /// Creates the complete persisted state for one interface window.
        /// </summary>
        /// <param name="windowTypeID">The stable window type identifier.</param>
        /// <param name="targetInstanceID">The represented game-object identifier.</param>
        /// <param name="x">The horizontal window position.</param>
        /// <param name="y">The vertical window position.</param>
        /// <param name="width">The window width.</param>
        /// <param name="height">The window height.</param>
        /// <param name="zOrder">The window stacking position.</param>
        public WindowState(
            string windowTypeID,
            string targetInstanceID,
            int x,
            int y,
            int width,
            int height,
            int zOrder
        )
        {
            _windowTypeID = windowTypeID;
            _targetInstanceID = targetInstanceID;
            _x = x;
            _y = y;
            _width = width;
            _height = height;
            _zOrder = zOrder;
        }

        /// <summary>Returns the stable window type identifier.</summary>
        /// <returns>The stable window type identifier.</returns>
        public string GetWindowTypeID() => _windowTypeID;

        /// <summary>Returns the represented game-object identifier.</summary>
        /// <returns>The represented game-object identifier.</returns>
        public string GetTargetInstanceID() => _targetInstanceID;

        /// <summary>Returns the horizontal window position.</summary>
        /// <returns>The horizontal window position.</returns>
        public int GetX() => _x;

        /// <summary>Returns the vertical window position.</summary>
        /// <returns>The vertical window position.</returns>
        public int GetY() => _y;

        /// <summary>Returns the saved window width.</summary>
        /// <returns>The saved window width.</returns>
        public int GetWidth() => _width;

        /// <summary>Returns the saved window height.</summary>
        /// <returns>The saved window height.</returns>
        public int GetHeight() => _height;

        /// <summary>Returns the window stacking position.</summary>
        /// <returns>The window stacking position.</returns>
        public int GetZOrder() => _zOrder;
    }
}
