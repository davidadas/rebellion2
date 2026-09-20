using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Commands
{
    /// <summary>
    /// Base command describing authoritative gameplay work that has not happened yet.
    /// </summary>
    public abstract class GameCommand
    {
        public int Tick { get; set; }
        public string SourceEventInstanceID { get; set; }
    }

    /// <summary>
    /// Requests a validated ownership transition for planets or units.
    /// </summary>
    public sealed class OwnershipChangeCommand : GameCommand
    {
        public Faction NewOwner { get; set; }
        public List<Planet> Planets { get; set; } = new List<Planet>();
        public List<ISceneNode> Units { get; set; } = new List<ISceneNode>();
    }

    /// <summary>
    /// Requests authoritative resolution of a linked-officer encounter.
    /// </summary>
    public sealed class DuelCommand : GameCommand
    {
        public Officer EncounteredOfficer { get; set; }
        public Officer OpposingOfficer { get; set; }
        public string ImagePath { get; set; }
        public string AudioPath { get; set; }
    }

    /// <summary>
    /// Requests delivery of an authored faction message.
    /// </summary>
    public sealed class DeliverMessageCommand : GameCommand
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
        public string MissionInstanceID { get; set; }
        public Message Message { get; set; }
    }

    /// <summary>
    /// Requests movement through the authoritative movement system.
    /// </summary>
    public sealed class MoveUnitsCommand : GameCommand
    {
        public List<IMovable> Units { get; set; } = new List<IMovable>();
        public List<ContainerNode> Destinations { get; set; } = new List<ContainerNode>();
    }

    /// <summary>
    /// Requests immediate placement without transit.
    /// </summary>
    public sealed class PlaceUnitsCommand : GameCommand
    {
        public List<IMovable> Units { get; set; } = new List<IMovable>();
        public List<ContainerNode> Destinations { get; set; } = new List<ContainerNode>();
    }

    /// <summary>
    /// Applies an authored capture state to selected officers.
    /// </summary>
    public sealed class SetCaptureStatusCommand : GameCommand
    {
        public List<Officer> Officers { get; set; } = new List<Officer>();
        public bool IsCaptured { get; set; }
        public string CaptorFactionInstanceID { get; set; }
        public bool CanEscape { get; set; }
    }

    /// <summary>
    /// Sets one faction's popular support on a planet to an absolute value.
    /// </summary>
    public sealed class SetPopularSupportCommand : GameCommand
    {
        public Planet Planet { get; set; }
        public Faction Faction { get; set; }
        public int Support { get; set; }
    }

    /// <summary>
    /// Starts one or more manufacturing orders.
    /// </summary>
    public sealed class StartManufacturingCommand : GameCommand
    {
        public Planet Producer { get; set; }
        public IManufacturable Template { get; set; }
        public ISceneNode Destination { get; set; }
        public int Count { get; set; }
        public string OwnerInstanceID { get; set; }
    }

    /// <summary>
    /// Enqueues one already-created unit for manufacturing and delivery.
    /// </summary>
    public sealed class EnqueueManufacturingCommand : GameCommand
    {
        public Planet Producer { get; set; }
        public IManufacturable Unit { get; set; }
        public ISceneNode Destination { get; set; }
        public bool IgnoreCost { get; set; }
    }

    /// <summary>
    /// Changes the destination for one manufacturing lane.
    /// </summary>
    public sealed class RetargetManufacturingCommand : GameCommand
    {
        public Planet Producer { get; set; }
        public ManufacturingType Type { get; set; }
        public ContainerNode Destination { get; set; }
        public string OwnerInstanceID { get; set; }
    }

    /// <summary>
    /// Cancels selected manufacturing orders.
    /// </summary>
    public sealed class CancelManufacturingCommand : GameCommand
    {
        public List<IManufacturable> Items { get; set; } = new List<IManufacturable>();
        public string OwnerInstanceID { get; set; }
    }

    /// <summary>
    /// Scraps selected manufactured units.
    /// </summary>
    public sealed class ScrapUnitsCommand : GameCommand
    {
        public List<IManufacturable> Items { get; set; } = new List<IManufacturable>();
        public string OwnerInstanceID { get; set; }
    }

    /// <summary>
    /// Retires selected personnel.
    /// </summary>
    public sealed class RetirePersonnelCommand : GameCommand
    {
        public List<ISceneNode> Personnel { get; set; } = new List<ISceneNode>();
        public string OwnerInstanceID { get; set; }
    }

    /// <summary>
    /// Moves a selected group to one destination.
    /// </summary>
    public sealed class MoveSelectionCommand : GameCommand
    {
        public List<ISceneNode> Items { get; set; } = new List<ISceneNode>();
        public ContainerNode Destination { get; set; }
        public string OwnerInstanceID { get; set; }
    }

    /// <summary>
    /// Evacuates one movable unit to the nearest friendly planet.
    /// </summary>
    public sealed class EvacuateUnitCommand : GameCommand
    {
        public IMovable Unit { get; set; }
    }

    /// <summary>
    /// Relocates a faction headquarters.
    /// </summary>
    public sealed class RelocateHeadquartersCommand : GameCommand
    {
        public Building Headquarters { get; set; }
        public Planet Destination { get; set; }
    }

    /// <summary>
    /// Assigns an ordered waypoint route to selected fleets.
    /// </summary>
    public sealed class SetFleetWaypointsCommand : GameCommand
    {
        public List<ISceneNode> Items { get; set; } = new List<ISceneNode>();
        public List<string> PlanetInstanceIDs { get; set; } = new List<string>();
        public string OwnerInstanceID { get; set; }
    }

    /// <summary>
    /// Clears queued waypoints from selected fleets.
    /// </summary>
    public sealed class ClearFleetWaypointsCommand : GameCommand
    {
        public List<ISceneNode> Items { get; set; } = new List<ISceneNode>();
        public string OwnerInstanceID { get; set; }
    }

    /// <summary>
    /// Creates a fleet from stationary capital ships.
    /// </summary>
    public sealed class CreateFleetCommand : GameCommand
    {
        public List<CapitalShip> Ships { get; set; } = new List<CapitalShip>();
        public string OwnerInstanceID { get; set; }
    }

    /// <summary>
    /// Executes an orbital bombardment.
    /// </summary>
    public sealed class BombardCommand : GameCommand
    {
        public List<Fleet> Fleets { get; set; } = new List<Fleet>();
        public Planet Planet { get; set; }
        public BombardmentType Type { get; set; }
    }

    /// <summary>
    /// Executes a planetary assault.
    /// </summary>
    public sealed class AssaultPlanetCommand : GameCommand
    {
        public List<Fleet> Fleets { get; set; } = new List<Fleet>();
        public Planet Planet { get; set; }
    }

    /// <summary>
    /// Creates and begins a mission.
    /// </summary>
    public sealed class InitiateMissionCommand : GameCommand
    {
        public MissionContext Context { get; set; }
    }

    /// <summary>
    /// Aborts one active mission.
    /// </summary>
    public sealed class AbortMissionCommand : GameCommand
    {
        public string MissionInstanceID { get; set; }
    }
}
