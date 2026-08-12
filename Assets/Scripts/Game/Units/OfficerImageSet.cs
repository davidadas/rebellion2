using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Groups the image assets that define an officer's current visual identity.
    /// </summary>
    [PersistableObject]
    public sealed class OfficerImageSet
    {
        public string DisplayImagePath { get; set; }
        public string SmallDisplayImagePath { get; set; }
        public string MessageImagePath { get; set; }
        public string EncyclopediaImagePath { get; set; }

        public void MergeFrom(OfficerImageSet authored)
        {
            if (authored == null)
                return;
            DisplayImagePath = authored.DisplayImagePath ?? DisplayImagePath;
            SmallDisplayImagePath = authored.SmallDisplayImagePath ?? SmallDisplayImagePath;
            MessageImagePath = authored.MessageImagePath ?? MessageImagePath;
            EncyclopediaImagePath = authored.EncyclopediaImagePath ?? EncyclopediaImagePath;
        }
    }
}
