using System.Collections.Generic;
using Rebellion.Game.Advisor;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Requests
{
    /// <summary>
    /// Base command describing authoritative gameplay work that has not happened yet.
    /// </summary>
    public abstract class GameRequest
    {
        public int Tick { get; set; }
        public string SourceEventInstanceID { get; set; }
        public string MissionInstanceID { get; set; }
    }

    /// <summary>
    /// Requests a validated ownership transition for planets or units.
    /// </summary>
    public sealed class OwnershipChangeRequest : GameRequest
    {
        public Faction NewOwner { get; set; }
        public List<Planet> Planets { get; set; } = new List<Planet>();
        public List<ISceneNode> Units { get; set; } = new List<ISceneNode>();
    }

    /// <summary>
    /// Requests authoritative resolution of a linked-officer encounter.
    /// </summary>
    public sealed class DuelRequest : GameRequest
    {
        public Officer EncounteredOfficer { get; set; }
        public Officer OpposingOfficer { get; set; }
        public string ImagePath { get; set; }
        public string AudioPath { get; set; }
    }

    /// <summary>
    /// Requests delivery of an authored faction message.
    /// </summary>
    public sealed class MessageDeliveryRequest : GameRequest
    {
        public Faction Recipient { get; set; }
        public MessageResultType ResultType { get; set; }
        public ISceneNode SubjectNode { get; set; }
        public ISceneNode RelatedSubjectNode { get; set; }
        public Planet Location { get; set; }
        public MessageType MessageType { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string BackgroundImageKey { get; set; }
        public string BackgroundImagePath { get; set; }
        public string OverlayImagePath { get; set; }
        public string BackgroundAudioPath { get; set; }
        public string OfficerVoicePath { get; set; }
        public AdvisorNotification AdvisorNotification { get; set; }
        public AdvisorNotificationType NotificationType { get; set; }
        public AdvisorSubjectNotification AdvisorSubjectNotification { get; set; }
        public string AdvisorSubjectTypeID { get; set; }
        public string EventLocationInstanceID { get; set; }
        public string NavigationTargetInstanceID { get; set; }
        public string NavigationSecondaryTargetInstanceID { get; set; }
    }

    /// <summary>
    /// Requests movement through the authoritative movement system.
    /// </summary>
    public sealed class UnitMovementRequest : GameRequest
    {
        public List<IMovable> Units { get; set; } = new List<IMovable>();
        public List<ContainerNode> Destinations { get; set; } = new List<ContainerNode>();
    }

    /// <summary>
    /// Requests immediate placement without transit.
    /// </summary>
    public sealed class UnitPlacementRequest : GameRequest
    {
        public List<IMovable> Units { get; set; } = new List<IMovable>();
        public List<ContainerNode> Destinations { get; set; } = new List<ContainerNode>();
    }
}
