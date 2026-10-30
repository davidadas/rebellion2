using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.PlayerRequests
{
    /// <summary>
    /// Identifies an explicitly supported player request independently of its delivery mechanism.
    /// </summary>
    public enum PlayerRequestKind
    {
        Move,
        RelocateHeadquarters,
        SetFleetRoute,
        ClearFleetRoute,
        Scrap,
        CancelManufacturing,
        RetirePersonnel,
        AbortMissions,
        StartMission,
        CreateFleet,
        Bombard,
        Assault,
        SetOfficerRank,
        Rename,
        ToggleAdvisorOption,
        ReadMessages,
        MarkAllMessagesRead,
        RemoveMessages,
        AcknowledgeMessages,
        ToggleMessageNotification,
        ConsumeMissionContinuation,
        ResolveCombat,
        SetSpeed,
        SetManufacturingOrder,
        RetargetManufacturing,
        ToggleManufacturingReservation,
        SendPeerChat,
        RespondToSpeed,
    }

    /// <summary>
    /// Identifies a persisted advisor setting that the owning player may toggle.
    /// </summary>
    public enum PlayerAdvisorOption
    {
        ManageGarrisons,
        ManageProduction,
        ManageNaming,
        TranslateCounterpart,
        AgentAdvice,
    }

    /// <summary>
    /// Describes a player request without carrying or claiming the issuing player's identity.
    /// </summary>
    public abstract class PlayerRequest
    {
        public abstract PlayerRequestKind Kind { get; }
    }

    /// <summary>
    /// Describes a request concerning a complete ordered selection of persistent scene identities.
    /// </summary>
    public abstract class SelectionPlayerRequest : PlayerRequest
    {
        public string[] ItemIDs { get; set; }
    }

    /// <summary>
    /// Requests movement of a selected group to a registered container.
    /// </summary>
    public sealed class MoveRequest : SelectionPlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.Move;
        public string DestinationID { get; set; }
    }

    /// <summary>
    /// Requests relocation of an owned headquarters and any accompanying units.
    /// </summary>
    public sealed class RelocateHeadquartersRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.RelocateHeadquarters;
        public string HeadquartersID { get; set; }
        public string DestinationPlanetID { get; set; }
        public string[] AccompanyingUnitIDs { get; set; }
    }

    /// <summary>
    /// Commits an ordered fleet waypoint plan.
    /// </summary>
    public sealed class SetFleetRouteRequest : SelectionPlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.SetFleetRoute;
        public string[] WaypointPlanetIDs { get; set; }
    }

    /// <summary>
    /// Clears queued fleet continuation while preserving any current movement leg.
    /// </summary>
    public sealed class ClearFleetRouteRequest : SelectionPlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.ClearFleetRoute;
    }

    /// <summary>
    /// Requests scrapping of a complete owned manufacturable selection.
    /// </summary>
    public sealed class ScrapRequest : SelectionPlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.Scrap;
    }

    /// <summary>
    /// Cancels selected production items that remain eligible for cancellation.
    /// </summary>
    public sealed class CancelManufacturingRequest : SelectionPlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.CancelManufacturing;
    }

    /// <summary>
    /// Requests retirement of a complete owned personnel selection.
    /// </summary>
    public sealed class RetirePersonnelRequest : SelectionPlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.RetirePersonnel;
    }

    /// <summary>
    /// Requests interruption of a complete selection of owned working missions.
    /// </summary>
    public sealed class AbortMissionsRequest : SelectionPlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.AbortMissions;
    }

    /// <summary>
    /// Starts a mission using owned participants and the target state observed while planning it.
    /// </summary>
    public sealed class StartMissionRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.StartMission;
        public string MissionTypeID { get; set; }
        public Planet ObservedLocation { get; set; }
        public ISceneNode ObservedTarget { get; set; }
        public string[] MainParticipantIDs { get; set; }
        public string[] DecoyParticipantIDs { get; set; }
        public ResearchDiscipline? Discipline { get; set; }
        public bool IsRepeating { get; set; }
    }

    /// <summary>
    /// Forms a fleet from selected owned capital ships.
    /// </summary>
    public sealed class CreateFleetRequest : SelectionPlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.CreateFleet;
    }

    /// <summary>
    /// Requests bombardment and a report limited to the supplied visible target state.
    /// </summary>
    public sealed class BombardRequest : SelectionPlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.Bombard;
        public Planet ObservedTarget { get; set; }
        public BombardmentType Type { get; set; }
    }

    /// <summary>
    /// Requests planetary assault and a report limited to the supplied visible target state.
    /// </summary>
    public sealed class AssaultRequest : SelectionPlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.Assault;
        public Planet ObservedTarget { get; set; }
    }

    /// <summary>
    /// Requests an owned officer's command appointment or resignation.
    /// </summary>
    public sealed class SetOfficerRankRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.SetOfficerRank;
        public string OfficerID { get; set; }
        public OfficerRank Rank { get; set; }
    }

    /// <summary>
    /// Renames an owned fleet or capital ship.
    /// </summary>
    public sealed class RenameRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.Rename;
        public string EntityID { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Toggles one advisor setting for the issuing faction.
    /// </summary>
    public sealed class ToggleAdvisorOptionRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.ToggleAdvisorOption;
        public PlayerAdvisorOption Option { get; set; }
    }

    /// <summary>
    /// Describes a request concerning messages delivered to the issuing faction.
    /// </summary>
    public abstract class MessagesRequest : PlayerRequest
    {
        public string[] MessageIDs { get; set; }
    }

    /// <summary>
    /// Marks selected delivered messages as read.
    /// </summary>
    public sealed class ReadMessagesRequest : MessagesRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.ReadMessages;
    }

    /// <summary>
    /// Marks every message delivered to the issuing faction as read.
    /// </summary>
    public sealed class MarkAllMessagesReadRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.MarkAllMessagesRead;
    }

    /// <summary>
    /// Removes selected messages from the issuing faction's message buckets.
    /// </summary>
    public sealed class RemoveMessagesRequest : MessagesRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.RemoveMessages;
    }

    /// <summary>
    /// Acknowledges presented message notifications without changing read state.
    /// </summary>
    public sealed class AcknowledgeMessagesRequest : MessagesRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.AcknowledgeMessages;
    }

    /// <summary>
    /// Toggles advisor notifications for one message category.
    /// </summary>
    public sealed class ToggleMessageNotificationRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.ToggleMessageNotification;
        public MessageType? MessageType { get; set; }
    }

    /// <summary>
    /// Consumes a delivered report's one-shot mission continuation choice.
    /// </summary>
    public sealed class ConsumeMissionContinuationRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.ConsumeMissionContinuation;
        public string MessageID { get; set; }
    }

    /// <summary>
    /// Chooses resolution or withdrawal for the displayed pending encounter.
    /// </summary>
    public sealed class ResolveCombatRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.ResolveCombat;
        public string PlanetID { get; set; }
        public int Tick { get; set; }
        public bool Retreat { get; set; }
    }

    /// <summary>
    /// Requests a shared simulation speed.
    /// </summary>
    public sealed class SetSpeedRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.SetSpeed;
        public TickSpeed Speed { get; set; }
    }

    /// <summary>
    /// Accepts or declines one displayed speed proposal without trusting a stale interface selection.
    /// </summary>
    public sealed class RespondToSpeedRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.RespondToSpeed;
        public string RequestingFactionID { get; set; }
        public TickSpeed Speed { get; set; }
        public long Revision { get; set; }
        public bool Accept { get; set; }
    }

    /// <summary>
    /// Sets the product and unfinished quantity of an owned production lane.
    /// </summary>
    public sealed class SetManufacturingOrderRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.SetManufacturingOrder;
        public string ProducerPlanetID { get; set; }
        public string DestinationID { get; set; }
        public string TemplateTypeID { get; set; }
        public ManufacturingType ManufacturingType { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Changes delivery for an owned production lane.
    /// </summary>
    public sealed class RetargetManufacturingRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.RetargetManufacturing;
        public string ProducerPlanetID { get; set; }
        public string DestinationID { get; set; }
        public ManufacturingType ManufacturingType { get; set; }
    }

    /// <summary>
    /// Toggles reservation of an owned production lane for manual orders.
    /// </summary>
    public sealed class ToggleManufacturingReservationRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.ToggleManufacturingReservation;
        public string ProducerPlanetID { get; set; }
        public ManufacturingType ManufacturingType { get; set; }
    }
}
