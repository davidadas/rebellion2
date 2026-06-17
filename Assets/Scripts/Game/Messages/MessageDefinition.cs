using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
    public class MessageDefinition : BaseGameEntity
    {
        public MessageType MessageType { get; set; }
        public BuildingType BuildingType { get; set; }
        public string TitleTemplate { get; set; }
        public string BodyTemplate { get; set; }
        public MessageImageMap ImageMap { get; set; }
    }
}
