using Rebellion.Game.Factions;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Selects the game result category that can produce a message.
    /// </summary>
    public enum MessageResultType
    {
        None,
        FleetArrived,
        ShipsArrived,
        EmperorSeatOfPower,
        FacilityDeployed,
        ManufacturingIdle,
        MissionReport,
        EnemyMissionFoiled,
        OfficerRecruited,
        OfficerCaptured,
        OfficerReleased,
        OfficerInjured,
        OfficerRecovered,
        OfficerKilled,
        ForceGrowth,
        CapitalShipRepaired,
        StarfighterRepaired,
        SabotageStrike,
        ResearchComplete,
        ResearchExhausted,
        UprisingStarted,
        UprisingEnded,
        PlanetJoinedBySupport,
        BlockadeInitiated,
        BlockadeDetected,
        EvacuationLosses,
        MaintenanceAutoscrap,
        SpaceBattle,
        Bombardment,
        PlanetaryAssault,
    }

    /// <summary>
    /// Selects the result outcome variant that can produce a message.
    /// </summary>
    public enum MessageResultOutcome
    {
        None,
        Success,
        Failed,
        Foiled,
        Victory,
        Defeat,
        Stalemate,
        NoLosses,
        TargetLosses,
        AttackerLosses,
    }

    /// <summary>
    /// Selects the planet ownership variant that can produce a message.
    /// </summary>
    public enum MessagePlanetOwnership
    {
        None,
        Owned,
        Neutral,
    }

    /// <summary>
    /// Defines the templates, selectors, and image map for one generated message.
    /// </summary>
    public class MessageDefinition : BaseGameEntity
    {
        public MessageResultType ResultType { get; set; }
        public MessageResultOutcome Outcome { get; set; }
        public MessagePlanetOwnership PlanetOwnership { get; set; }
        public MessageType MessageType { get; set; }
        public MissionType MissionType { get; set; }
        public MissionReportDetail MissionReportDetail { get; set; }
        public BuildingType BuildingType { get; set; }
        public ManufacturingType ManufacturingType { get; set; }
        public ResearchDiscipline ResearchDiscipline { get; set; }
        public string TitleTemplate { get; set; }
        public string BodyTemplate { get; set; }
        public string ImageKey { get; set; }
        public MessageImageMap ImageMap { get; set; }
    }
}
