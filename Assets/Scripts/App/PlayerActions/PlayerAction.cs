using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.App.PlayerActions
{
    /// <summary>
    /// Identifies an explicitly supported player request independently of its delivery mechanism.
    /// </summary>
    public enum PlayerActionKind
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
    public abstract class PlayerAction
    {
        public abstract PlayerActionKind Kind { get; }
    }

    /// <summary>
    /// Describes a request concerning a complete ordered selection of persistent scene identities.
    /// </summary>
    public abstract class SelectionPlayerAction : PlayerAction
    {
        public string[] ItemIDs { get; set; }
    }

    /// <summary>
    /// Requests movement of a selected group to a registered container.
    /// </summary>
    public sealed class MovePlayerAction : SelectionPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.Move;
        public string DestinationID { get; set; }
    }

    /// <summary>
    /// Requests relocation of an owned headquarters and any accompanying units.
    /// </summary>
    public sealed class RelocateHeadquartersPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.RelocateHeadquarters;
        public string HeadquartersID { get; set; }
        public string DestinationPlanetID { get; set; }
        public string[] AccompanyingUnitIDs { get; set; }
    }

    /// <summary>
    /// Commits an ordered fleet waypoint plan.
    /// </summary>
    public sealed class SetFleetRoutePlayerAction : SelectionPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.SetFleetRoute;
        public string[] WaypointPlanetIDs { get; set; }
    }

    /// <summary>
    /// Clears queued fleet continuation while preserving any current movement leg.
    /// </summary>
    public sealed class ClearFleetRoutePlayerAction : SelectionPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.ClearFleetRoute;
    }

    /// <summary>
    /// Requests scrapping of a complete owned manufacturable selection.
    /// </summary>
    public sealed class ScrapPlayerAction : SelectionPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.Scrap;
    }

    /// <summary>
    /// Cancels selected production items that remain eligible for cancellation.
    /// </summary>
    public sealed class CancelManufacturingPlayerAction : SelectionPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.CancelManufacturing;
    }

    /// <summary>
    /// Requests retirement of a complete owned personnel selection.
    /// </summary>
    public sealed class RetirePersonnelPlayerAction : SelectionPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.RetirePersonnel;
    }

    /// <summary>
    /// Requests interruption of a complete selection of owned working missions.
    /// </summary>
    public sealed class AbortMissionsPlayerAction : SelectionPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.AbortMissions;
    }

    /// <summary>
    /// Starts a mission using owned participants and the target state observed while planning it.
    /// </summary>
    public sealed class StartMissionPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.StartMission;
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
    public sealed class CreateFleetPlayerAction : SelectionPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.CreateFleet;
    }

    /// <summary>
    /// Requests bombardment and a report limited to the supplied visible target state.
    /// </summary>
    public sealed class BombardPlayerAction : SelectionPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.Bombard;
        public Planet ObservedTarget { get; set; }
        public BombardmentType Type { get; set; }
    }

    /// <summary>
    /// Requests planetary assault and a report limited to the supplied visible target state.
    /// </summary>
    public sealed class AssaultPlayerAction : SelectionPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.Assault;
        public Planet ObservedTarget { get; set; }
    }

    /// <summary>
    /// Requests an owned officer's command appointment or resignation.
    /// </summary>
    public sealed class SetOfficerRankPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.SetOfficerRank;
        public string OfficerID { get; set; }
        public OfficerRank Rank { get; set; }
    }

    /// <summary>
    /// Renames an owned fleet or capital ship.
    /// </summary>
    public sealed class RenamePlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.Rename;
        public string EntityID { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Toggles one advisor setting for the issuing faction.
    /// </summary>
    public sealed class ToggleAdvisorOptionPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.ToggleAdvisorOption;
        public PlayerAdvisorOption Option { get; set; }
    }

    /// <summary>
    /// Describes a request concerning messages delivered to the issuing faction.
    /// </summary>
    public abstract class MessagesPlayerAction : PlayerAction
    {
        public string[] MessageIDs { get; set; }
    }

    /// <summary>
    /// Marks selected delivered messages as read.
    /// </summary>
    public sealed class ReadMessagesPlayerAction : MessagesPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.ReadMessages;
    }

    /// <summary>
    /// Marks every message delivered to the issuing faction as read.
    /// </summary>
    public sealed class MarkAllMessagesReadPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.MarkAllMessagesRead;
    }

    /// <summary>
    /// Removes selected messages from the issuing faction's message buckets.
    /// </summary>
    public sealed class RemoveMessagesPlayerAction : MessagesPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.RemoveMessages;
    }

    /// <summary>
    /// Acknowledges presented message notifications without changing read state.
    /// </summary>
    public sealed class AcknowledgeMessagesPlayerAction : MessagesPlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.AcknowledgeMessages;
    }

    /// <summary>
    /// Toggles advisor notifications for one message category.
    /// </summary>
    public sealed class ToggleMessageNotificationPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.ToggleMessageNotification;
        public MessageType? MessageType { get; set; }
    }

    /// <summary>
    /// Consumes a delivered report's one-shot mission continuation choice.
    /// </summary>
    public sealed class ConsumeMissionContinuationPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.ConsumeMissionContinuation;
        public string MessageID { get; set; }
    }

    /// <summary>
    /// Chooses resolution or withdrawal for the displayed pending encounter.
    /// </summary>
    public sealed class ResolveCombatPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.ResolveCombat;
        public string PlanetID { get; set; }
        public int Tick { get; set; }
        public bool Retreat { get; set; }
    }

    /// <summary>
    /// Requests a shared simulation speed.
    /// </summary>
    public sealed class SetSpeedPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.SetSpeed;
        public TickSpeed Speed { get; set; }
    }

    /// <summary>
    /// Accepts or declines one displayed speed proposal without trusting a stale interface selection.
    /// </summary>
    public sealed class RespondToSpeedPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.RespondToSpeed;
        public string RequestingFactionID { get; set; }
        public TickSpeed Speed { get; set; }
        public long Revision { get; set; }
        public bool Accept { get; set; }
    }

    /// <summary>
    /// Sets the product and unfinished quantity of an owned production lane.
    /// </summary>
    public sealed class SetManufacturingOrderPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.SetManufacturingOrder;
        public string ProducerPlanetID { get; set; }
        public string DestinationID { get; set; }
        public string TemplateTypeID { get; set; }
        public ManufacturingType ManufacturingType { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Changes delivery for an owned production lane.
    /// </summary>
    public sealed class RetargetManufacturingPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.RetargetManufacturing;
        public string ProducerPlanetID { get; set; }
        public string DestinationID { get; set; }
        public ManufacturingType ManufacturingType { get; set; }
    }

    /// <summary>
    /// Toggles reservation of an owned production lane for manual orders.
    /// </summary>
    public sealed class ToggleManufacturingReservationPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.ToggleManufacturingReservation;
        public string ProducerPlanetID { get; set; }
        public ManufacturingType ManufacturingType { get; set; }
    }
}
