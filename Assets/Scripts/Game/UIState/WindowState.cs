using Rebellion.Util.Serialization;

namespace Rebellion.Game.UIState
{
    /// <summary>
    /// Stores the common state required to restore one interface window.
    /// </summary>
    [PersistableObject]
    public sealed class WindowState
    {
        public string WindowTypeID { get; set; }
        public string TargetInstanceID { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ZOrder { get; set; }
    }
}
