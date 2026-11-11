using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Records the authenticated participants and faction ownership of a saved multiplayer campaign.
    /// </summary>
    [PersistableObject]
    public sealed class MultiplayerCampaignIdentity
    {
        public string HostUserID { get; set; }
        public string GuestUserID { get; set; }
        public string HostFactionID { get; set; }
        public string GuestFactionID { get; set; }
    }
}
