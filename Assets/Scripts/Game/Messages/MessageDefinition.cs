using System.Collections.Generic;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

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
        PersonnelArrived,
        PersonnelArrivedByOfficer,
        PersonnelArrivedByOfficerWithCompany,
        EmperorSeatOfPower,
        FacilityDeployed,
        CapitalShipDeployed,
        DeathStarDeployed,
        StarfighterDeployed,
        RegimentDeployed,
        FacilityLost,
        SmugglingLosses,
        SmugglingLossesEnded,
        SmugglingBenefits,
        SmugglingBenefitsEnded,
        ManufacturingIdle,
        MissionReport,
        EnemyMissionFoiled,
        OfficerRecruited,
        OfficerCaptured,
        EnemyOfficerCaptured,
        OfficerReleased,
        OfficerInjured,
        OfficerRecovered,
        OfficerKilled,
        TraitorDiscovered,
        ForceGrowth,
        ForceUserDiscovered,
        ForceUserDiscoveredByStudent,
        ForceAbilityRevealed,
        DagobahCompleted,
        HeritageRevealed,
        CapitalShipRepaired,
        StarfighterRepaired,
        SabotageStrike,
        ResearchComplete,
        ResearchExhausted,
        NearUprising,
        UprisingStarted,
        UprisingEnded,
        PlanetJoinedBySupport,
        PlanetJoinedEnemyBySupport,
        PlanetDeclaredNeutralityBySupport,
        PlanetCaptured,
        HeadquartersDestroyed,
        NaturalDisaster,
        NewResources,
        ResourcesDepleted,
        BlockadeInitiated,
        BlockadeDetected,
        EvacuationLosses,
        MaintenanceAutoscrap,
        RecruitmentExhausted,
        SpaceBattle,
        Bombardment,
        PlanetaryAssault,
        OfficerAssassinated,
        UnitsArrived,
        HeadquartersArrived,
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
    /// Defines the original situation and fleet-disposition text used to assemble a space battle
    /// report. Keeping these fragments in content allows total-conversion packs to replace the
    /// complete battle vocabulary without changing message routing code.
    /// </summary>
    [PersistableObject]
    public class SpaceBattleNarrativeTemplates
    {
        public string VictoryHeadline { get; set; }
        public string DefeatHeadline { get; set; }
        public string StalemateHeadline { get; set; }
        public string NeutralVictory { get; set; }
        public string NeutralDefeat { get; set; }
        public string SuccessfullyDefended { get; set; }
        public string BlockadeEstablished { get; set; }
        public string AttackFailed { get; set; }
        public string BlockadeMaintained { get; set; }
        public string BlockadeBroken { get; set; }
        public string NoVictor { get; set; }
        public string FleetDestroyed { get; set; }
        public string FleetWithdrawn { get; set; }
        public string FleetWithdrawnTo { get; set; }
        public string AllShipsDestroyed { get; set; }
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
        public string MissionTypeID { get; set; }
        public MissionCompletionReason MissionCompletionReason { get; set; }
        public BuildingType BuildingType { get; set; }
        public ManufacturingType ManufacturingType { get; set; }
        public ResearchDiscipline ResearchDiscipline { get; set; }
        public string PlanetInstanceID { get; set; }
        public string PreviousOwnerInstanceID { get; set; }
        public string NewOwnerInstanceID { get; set; }
        public string FactionInstanceID { get; set; }
        public string GameObjectTypeID { get; set; }
        public PlanetStatType PlanetStat { get; set; }
        public bool HasDestroyedObjects { get; set; }
        public bool PlanetDestroyed { get; set; }
        public string TitleTemplate { get; set; }
        public string BodyTemplate { get; set; }
        public string DetailListHeaderTemplate { get; set; }
        public string DetailListItemTemplate { get; set; }
        public SpaceBattleNarrativeTemplates SpaceBattleNarrative { get; set; }
        public bool ShowOfficerOverlay { get; set; }
        public string ImageKey { get; set; }
        public string ImagePath { get; set; }
        public Dictionary<string, string> ImagePaths { get; set; } =
            new Dictionary<string, string>();
        public string VoicePath { get; set; }
        public Dictionary<string, string> VoicePaths { get; set; } =
            new Dictionary<string, string>();
    }
}
