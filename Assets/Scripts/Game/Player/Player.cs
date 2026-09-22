using Rebellion.Game.UIState;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Represents one participant in a game and the faction that participant controls.
    /// </summary>
    [PersistableObject]
    public sealed class Player
    {
        public string PlayerID { get; set; }
        public string FactionID { get; set; }
        public PlayerControllerType ControllerType { get; set; }
        public PlayerUIState UIState { get; set; } = new PlayerUIState();
    }
}
