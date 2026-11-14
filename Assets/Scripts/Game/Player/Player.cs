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

        /// <summary>
        /// Gets or sets the presentation name supplied by the current authenticated session.
        /// </summary>
        [PersistableIgnore]
        public string DisplayName { get; set; }
        public string FactionID { get; set; }
        public PlayerControllerType ControllerType { get; set; }
        public TickSpeed? RequestedGameSpeed { get; set; }

        /// <summary>
        /// Gets or sets the current runtime's version of this player's speed selection.
        /// </summary>
        [PersistableIgnore]
        public long GameSpeedSelectionRevision { get; set; }
        public PlayerUIState UIState { get; set; } = new PlayerUIState();
    }
}
