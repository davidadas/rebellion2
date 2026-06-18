using Rebellion.Game.Factions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
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
        SabotageStrike,
        ResearchComplete,
        ResearchExhausted,
        UprisingStarted,
        UprisingEnded,
        BlockadeInitiated,
        BlockadeDetected,
        EvacuationLosses,
        MaintenanceAutoscrap,
        SpaceBattle,
        Bombardment,
        PlanetaryAssault,
    }

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

    public enum MessagePlanetOwnership
    {
        None,
        Owned,
        Neutral,
    }

    public class MessageDefinition : BaseGameEntity
    {
        public MessageResultType ResultType { get; set; }
        public MessageResultOutcome Outcome { get; set; }
        public MessagePlanetOwnership PlanetOwnership { get; set; }
        public MessageType MessageType { get; set; }
        public BuildingType BuildingType { get; set; }
        public ManufacturingType ManufacturingType { get; set; }
        public ResearchDiscipline ResearchDiscipline { get; set; }
        public string TitleTemplate { get; set; }
        public string BodyTemplate { get; set; }
        public MessageImageMap ImageMap { get; set; }
    }
}
