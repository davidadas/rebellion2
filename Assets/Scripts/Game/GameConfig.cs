using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.Util.Random;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Runtime configuration loaded from the game config XML.
    /// </summary>
    [PersistableObject]
    public class GameConfig
    {
        public Dictionary<GameDifficulty, DifficultyModifiers> DifficultyModifiers { get; set; } =
            new Dictionary<GameDifficulty, DifficultyModifiers>();

        public AIConfig AI { get; set; } = new AIConfig();

        public MovementConfig Movement { get; set; } = new MovementConfig();

        public ProductionConfig Production { get; set; } = new ProductionConfig();

        public SmugglingConfig Smuggling { get; set; } = new SmugglingConfig();

        public PlanetConfig Planet { get; set; } = new PlanetConfig();

        public CombatConfig Combat { get; set; } = new CombatConfig();

        public UprisingConfig Uprising { get; set; } = new UprisingConfig();

        public SupportShiftConfig SupportShift { get; set; } = new SupportShiftConfig();

        public BlockadeConfig Blockade { get; set; } = new BlockadeConfig();

        public JediConfig Jedi { get; set; } = new JediConfig();

        public DuelResolutionConfig DuelResolution { get; set; } = new DuelResolutionConfig();

        public ResearchConfig Research { get; set; } = new ResearchConfig();

        public AssassinationConfig Assassination { get; set; } = new AssassinationConfig();

        public RecoveryConfig Recovery { get; set; } = new RecoveryConfig();

        public CaptiveConfig Captive { get; set; } = new CaptiveConfig();

        public OfficerLoyaltyConfig OfficerLoyalty { get; set; } = new OfficerLoyaltyConfig();

        public GameSpeedConfig GameSpeed { get; set; } = new GameSpeedConfig();

        public MessageConfig Messages { get; set; } = new MessageConfig();

        public EspionageConfig Espionage { get; set; } = new EspionageConfig();

        public ProbabilityTablesConfig ProbabilityTables { get; set; } =
            new ProbabilityTablesConfig();

        /// <summary>
        /// AI decision-making and mission dispatch.
        /// </summary>
        [PersistableObject]
        public class AIConfig
        {
            public int TickInterval { get; set; }

            public bool EnablePlanetaryAssaults { get; set; }

            public int DiplomacyMinimumSkill { get; set; }

            public int RecruitmentMinimumLeadership { get; set; }

            public AIMissionTablesConfig MissionTables { get; set; }

            public AIMissionPlanningConfig MissionPlanning { get; set; }

            public GarrisonConfig Garrison { get; set; }

            public int DeploymentGateLow { get; set; }

            public int DeploymentGateHigh { get; set; }

            public AISelectionConfig Selection { get; set; }

            public AINonCapitalSummaryConfig NonCapitalSummary { get; set; }

            public AIInfrastructureConfig Infrastructure { get; set; }

            public AIFleetDeploymentConfig FleetDeployment { get; set; }
        }

        /// <summary>
        /// Shapes supported by normalized AI utility considerations.
        /// </summary>
        public enum AIResponseCurveShape
        {
            Linear,
            Power,
            SmoothStep,
            Logistic,
        }

        /// <summary>
        /// Configuration for mapping a normalized input to normalized utility.
        /// </summary>
        [PersistableObject]
        public class AIResponseCurveConfig
        {
            public AIResponseCurveShape Shape { get; set; }

            public double Exponent { get; set; }

            public double Midpoint { get; set; }

            public double Steepness { get; set; }
        }

        /// <summary>
        /// Configuration for one normalized and weighted AI consideration.
        /// </summary>
        [PersistableObject]
        public class AIConsiderationConfig
        {
            public double Weight { get; set; }

            public double SaturationValue { get; set; }

            public AIResponseCurveConfig Curve { get; set; }
        }

        /// <summary>
        /// Mission planning priorities and intelligence freshness settings.
        /// </summary>
        [PersistableObject]
        public class AIMissionPlanningConfig
        {
            public int RetainedAlternativesPerMission { get; set; }

            public int EspionageRefreshIntervalTicks { get; set; }

            public int HostileMissionMaximumIntelAgeTicks { get; set; }

            public int MaximumJediTrainingStudents { get; set; }

            public double MinimumMissionScore { get; set; }

            public int MinimumUprisingMissionSuccessPercent { get; set; }

            public int MaximumOfficerMissionLossProbability { get; set; }

            public int HostileMissionIntelAgeFoilPenaltyPerRefreshInterval { get; set; }

            public int MaximumUnprotectedOfficerMissionFoilProbability { get; set; }

            public AIMissionUtilityConfig Utility { get; set; }
        }

        /// <summary>
        /// Utility considerations used to rank mission proposals.
        /// </summary>
        [PersistableObject]
        public class AIMissionUtilityConfig
        {
            public AIMissionObjectiveUtilityConfig Objective { get; set; }

            public AIMissionPriorityUtilityConfig Priority { get; set; }

            public AISabotageUtilityConfig Sabotage { get; set; }

            public AIDiplomacyUtilityConfig Diplomacy { get; set; }

            public AIOfficerTargetUtilityConfig OfficerTarget { get; set; }
        }

        /// <summary>
        /// General mission value and risk considerations.
        /// </summary>
        [PersistableObject]
        public class AIMissionObjectiveUtilityConfig
        {
            public AIConsiderationConfig Success { get; set; }

            public AIConsiderationConfig FoilRisk { get; set; }

            public AIConsiderationConfig TravelCost { get; set; }

            public AIConsiderationConfig OfficerRisk { get; set; }

            public AIConsiderationConfig IntelAge { get; set; }

            public AIConsiderationConfig AttackPreparationIntel { get; set; }

            public AIConsiderationConfig TrainingValue { get; set; }
        }

        /// <summary>
        /// Mission-type strategic priority considerations.
        /// </summary>
        [PersistableObject]
        public class AIMissionPriorityUtilityConfig
        {
            public AIConsiderationConfig Reconnaissance { get; set; }

            public AIConsiderationConfig Recruitment { get; set; }

            public AIConsiderationConfig Rescue { get; set; }

            public AIConsiderationConfig SubdueUprising { get; set; }

            public AIConsiderationConfig Research { get; set; }

            public AIConsiderationConfig JediTraining { get; set; }

            public AIConsiderationConfig Espionage { get; set; }

            public AIConsiderationConfig Diplomacy { get; set; }
        }

        /// <summary>
        /// Strategic value considerations for sabotage targets.
        /// </summary>
        [PersistableObject]
        public class AISabotageUtilityConfig
        {
            public AIConsiderationConfig Infrastructure { get; set; }

            public AIConsiderationConfig Defense { get; set; }

            public AIConsiderationConfig Shield { get; set; }

            public AIConsiderationConfig AttackTarget { get; set; }

            public AIConsiderationConfig AttackDefense { get; set; }

            public AIConsiderationConfig FavoredSupportRegiment { get; set; }

            public AIConsiderationConfig GarrisonRegiment { get; set; }

            public AIConsiderationConfig GarrisonStarfighter { get; set; }

            public AIConsiderationConfig OtherUnit { get; set; }
        }

        /// <summary>
        /// Strategic value considerations for diplomacy targets.
        /// </summary>
        [PersistableObject]
        public class AIDiplomacyUtilityConfig
        {
            public AIConsiderationConfig SupportDeficit { get; set; }

            public AIConsiderationConfig CoreWorld { get; set; }

            public AIConsiderationConfig ConstructionFacility { get; set; }

            public AIConsiderationConfig Shipyard { get; set; }

            public AIConsiderationConfig TrainingFacility { get; set; }

            public AIConsiderationConfig ResourceNode { get; set; }

            public AIConsiderationConfig SectorSupportRisk { get; set; }
        }

        /// <summary>
        /// Strategic value considerations for hostile officer targets.
        /// </summary>
        [PersistableObject]
        public class AIOfficerTargetUtilityConfig
        {
            public AIConsiderationConfig Combat { get; set; }
            public AIConsiderationConfig Espionage { get; set; }
            public AIConsiderationConfig Diplomacy { get; set; }
            public AIConsiderationConfig Leadership { get; set; }
            public AIConsiderationConfig ShipResearch { get; set; }
            public AIConsiderationConfig FacilityResearch { get; set; }
            public AIConsiderationConfig TroopResearch { get; set; }
        }

        /// <summary>
        /// Fleet attack and deployment scoring settings.
        /// </summary>
        [PersistableObject]
        public class AIFleetDeploymentConfig
        {
            public int MinimumBattleFleetCount { get; set; }

            public int PlanetsPerBattleFleet { get; set; }

            public int MinimumAttackStrength { get; set; }

            public int MinimumMobileCombatStrength { get; set; }

            public int MobileCombatStrengthPerPlanet { get; set; }

            public int MinimumDefenseStrength { get; set; }

            public int HeadquartersDefenseCombatPercent { get; set; }

            public int MinimumPlanetaryAssaultRegimentCount { get; set; }

            public int MinimumPlanetaryAssaultSuccessPercent { get; set; }

            public int AttackStrengthPercentOfDefense { get; set; }

            public int AttackStrengthPercentOfStrongestHostileFleet { get; set; }

            public int StaleIntelMaximumAttackStrengthPercent { get; set; }

            public int StaleIntelReserveSaturationIntervals { get; set; }

            public double AttackReadinessFloorWeight { get; set; }

            public AIAttackUtilityConfig AttackUtility { get; set; }

            public int ExposedSectorMinimumOwnedPresencePercent { get; set; }

            public AIDefenseUtilityConfig DefenseUtility { get; set; }

            public AIDefenseAllocationUtilityConfig DefenseAllocationUtility { get; set; }

            public AIColonizationUtilityConfig ColonizationUtility { get; set; }

            public AIColonizationTargetUtilityConfig ColonizationTargetUtility { get; set; }

            public int ColonizationFleetTargetCount { get; set; }

            public int ColonizationFleetMinimumRegimentCount { get; set; }

            public int ColonizationFleetMaximumRegimentCount { get; set; }
        }

        /// <summary>
        /// Utility considerations used to value fleet attacks and attack-fleet reinforcement.
        /// </summary>
        [PersistableObject]
        public class AIAttackUtilityConfig
        {
            public AIConsiderationConfig StrategicValue { get; set; }

            public AIConsiderationConfig SectorSupport { get; set; }

            public AIConsiderationConfig SystemPresence { get; set; }

            public AIConsiderationConfig Readiness { get; set; }

            public AIConsiderationConfig Ready { get; set; }

            public AIConsiderationConfig CaptureViability { get; set; }

            public AIConsiderationConfig TravelEfficiency { get; set; }

            public AIConsiderationConfig ExpectedLossRisk { get; set; }

            public AIConsiderationConfig OpportunityCost { get; set; }

            public AIConsiderationConfig IntelAgeRisk { get; set; }

            public AIConsiderationConfig ExistingOrder { get; set; }

            public AIConsiderationConfig Headquarters { get; set; }

            public AIConsiderationConfig OrbitalAdvantage { get; set; }

            public AIConsiderationConfig ExposedBombardment { get; set; }
        }

        /// <summary>
        /// Utility considerations used to value colonization fleets and destinations.
        /// </summary>
        [PersistableObject]
        public class AIColonizationUtilityConfig
        {
            public AIConsiderationConfig Base { get; set; }

            public AIConsiderationConfig StrategicValue { get; set; }

            public AIConsiderationConfig TravelEfficiency { get; set; }

            public AIConsiderationConfig AnchorProximity { get; set; }

            public AIConsiderationConfig Ready { get; set; }

            public AIConsiderationConfig OpportunityCost { get; set; }

            public AIConsiderationConfig ExistingOrder { get; set; }
        }

        /// <summary>
        /// Utility considerations used to choose a colony within an assigned system.
        /// </summary>
        [PersistableObject]
        public class AIColonizationTargetUtilityConfig
        {
            public AIConsiderationConfig Energy { get; set; }

            public AIConsiderationConfig Resources { get; set; }
        }

        /// <summary>
        /// Utility considerations used to value fleet defense.
        /// </summary>
        [PersistableObject]
        public class AIDefenseUtilityConfig
        {
            public AIConsiderationConfig Base { get; set; }

            public AIConsiderationConfig SectorRisk { get; set; }
        }

        /// <summary>
        /// Utility considerations used to allocate defense fleets among valid assignments.
        /// </summary>
        [PersistableObject]
        public class AIDefenseAllocationUtilityConfig
        {
            public AIConsiderationConfig SectorRisk { get; set; }

            public AIConsiderationConfig StrategicValue { get; set; }

            public AIConsiderationConfig DefenseNeed { get; set; }

            public AIConsiderationConfig TravelEfficiency { get; set; }

            public AIConsiderationConfig ForceEfficiency { get; set; }

            public AIConsiderationConfig ReinforcementNeed { get; set; }
        }

        /// <summary>
        /// AI manufacturing selection weights and limits.
        /// </summary>
        [PersistableObject]
        public class AISelectionConfig
        {
            public float MinimumSelectableScore { get; set; }

            public AIConsiderationConfig DemandUtility { get; set; }
            public int PreferredStarfighterTypeCountPerFleet { get; set; }
            public int PreferredRegimentTypeCountPerDestination { get; set; }
            public AITechnologySelectionUtilityConfig TechnologyUtility { get; set; }
            public int RefinedMaterialReservePercent { get; set; }
            public int RefinedMaterialEconomyWarningPercent { get; set; }
            public int RefinedMaterialCommitmentHorizonTicks { get; set; }
            public int MaintenanceHeadroomReserve { get; set; }
            public int MaintenanceHeadroomTarget { get; set; }
            public AIProductionUtilityConfig ProductionUtility { get; set; }
        }

        /// <summary>
        /// Utility considerations used to choose a unit technology for production.
        /// </summary>
        [PersistableObject]
        public class AITechnologySelectionUtilityConfig
        {
            public AIBuildingSelectionUtilityConfig Building { get; set; }

            public AIStarfighterSelectionUtilityConfig Starfighter { get; set; }

            public AIRegimentSelectionUtilityConfig Regiment { get; set; }

            public AISpecialForcesSelectionUtilityConfig SpecialForces { get; set; }

            public AIConsiderationConfig DuplicateCost { get; set; }
        }

        /// <summary>
        /// Utility considerations used to choose a building technology.
        /// </summary>
        [PersistableObject]
        public class AIBuildingSelectionUtilityConfig
        {
            public AIConsiderationConfig Capability { get; set; }
        }

        /// <summary>
        /// Utility considerations used to choose a starfighter technology.
        /// </summary>
        [PersistableObject]
        public class AIStarfighterSelectionUtilityConfig
        {
            public AIConsiderationConfig Laser { get; set; }
            public AIConsiderationConfig Ion { get; set; }
            public AIConsiderationConfig Torpedo { get; set; }
            public AIConsiderationConfig MissingIon { get; set; }
            public AIConsiderationConfig MissingTorpedo { get; set; }
            public AIConsiderationConfig PlanetDefenseEfficiency { get; set; }
        }

        /// <summary>
        /// Utility considerations used to choose a regiment technology.
        /// </summary>
        [PersistableObject]
        public class AIRegimentSelectionUtilityConfig
        {
            public AIConsiderationConfig Attack { get; set; }
            public AIConsiderationConfig Defense { get; set; }
            public AIConsiderationConfig BombardmentDefense { get; set; }
            public AIConsiderationConfig Base { get; set; }
            public AIConsiderationConfig MaintenanceCost { get; set; }
        }

        /// <summary>
        /// Utility considerations used to choose a special-forces technology.
        /// </summary>
        [PersistableObject]
        public class AISpecialForcesSelectionUtilityConfig
        {
            public AIConsiderationConfig BuildEfficiency { get; set; }
        }

        /// <summary>
        /// Utility costs applied when ranking production proposals.
        /// </summary>
        [PersistableObject]
        public class AIProductionUtilityConfig
        {
            public AIConsiderationConfig TravelCost { get; set; }

            public AIConsiderationConfig ColonyFoundation { get; set; }

            public AIConsiderationConfig HeadroomRisk { get; set; }

            public AIConsiderationConfig Shortfall { get; set; }
        }

        /// <summary>
        /// AI infrastructure demand settings.
        /// </summary>
        [PersistableObject]
        public class AIInfrastructureConfig
        {
            public int PlanetsPerConstructionFacility { get; set; }
            public int MinimumConstructionFacilityLanes { get; set; }
            public int ConstructionFacilityTargetClearTicks { get; set; }
            public int ShipyardTargetClearTicks { get; set; }
            public int FleetProductionMinimumShipyardCount { get; set; }
            public int TrainingFacilityTargetClearTicks { get; set; }
            public int PlanetsPerShipyard { get; set; }
            public int PlanetsPerTrainingFacility { get; set; }
            public int TrainingDemandsPerFacility { get; set; }
            public int ConstructionFacilityPortfolioPercent { get; set; }
            public int ShipyardPortfolioPercent { get; set; }
            public int TrainingFacilityPortfolioPercent { get; set; }
            public int StaticDefensePortfolioPercent { get; set; }
            public int ManufacturingFacilityBaseDemandPercent { get; set; }
            public int ConstructionFacilityDemandPercent { get; set; }
            public int ShipyardDemandPercent { get; set; }
            public int TrainingFacilityDemandPercent { get; set; }
            public int FacilitySectorHubTargetCount { get; set; }
            public int ShipyardSectorHubTargetCount { get; set; }
            public int FacilitySectorHubMaximumCount { get; set; }
            public int FacilityPlanetsPerSector { get; set; }
            public int FacilitySectorSecondaryTargetCount { get; set; }
            public AIInfrastructurePlacementUtilityConfig PlacementUtility { get; set; }
            public AIInfrastructureAllocationUtilityConfig AllocationUtility { get; set; }
            public int ProductionFacilityMaintenanceAllocationPercent { get; set; }
            public int ProductionFacilityInvestmentHorizonTicks { get; set; }
            public int FacilityConstructionLaneReserve { get; set; }
            public int ProductionQueueTargetPlanningIntervals { get; set; }
            public int ProductionFacilityUpgradeMinimumRemainingCount { get; set; }
            public int ProductionFacilityUpgradeDemandPercent { get; set; }
            public int FleetCapitalShipDemandPercent { get; set; }
            public int FleetStarfighterDemandPercent { get; set; }
            public int FleetRegimentDemandPercent { get; set; }
            public int FleetSeedCapitalShipDemandPercent { get; set; }
            public int ColonizationFleetDemandPercent { get; set; }
            public int SpecialForcesDemandPercent { get; set; }

            public int SpecialForcesMissionCoveragePercent { get; set; }

            public int StarfighterParentFillPercent { get; set; }
            public int StarfighterLocalReservePercent { get; set; }
            public int AssaultRegimentLoadPercent { get; set; }
            public int GarrisonRegimentReservePercent { get; set; }
            public int PlanetaryStarfighterDemandPercent { get; set; }
            public int IdleShipyardFighterReserveCount { get; set; }
            public int IdleShipyardFighterDemandPercent { get; set; }
            public int PlanetaryWeaponTargetCount { get; set; }
            public int PlanetaryDefenseSurplusBatchSize { get; set; }
            public int PlanetaryShieldDemandPercent { get; set; }
            public int PlanetaryWeaponDemandPercent { get; set; }
            public int PlanetaryGarrisonDemandPercent { get; set; }
            public int PlanetaryDefenseMaintenanceReservePercent { get; set; }
            public int EconomyDefaultBatchSize { get; set; }
            public int EconomyDemandPercent { get; set; }
            public int EconomySevereDemandPercent { get; set; }
            public int EconomySevereDeficitPercent { get; set; }
            public int EconomyCompetingNeedSlotReserve { get; set; }
            public int FleetFinalReadinessGateUnitCount { get; set; }
            public AIProductionDemandUtilityConfig DemandUtility { get; set; }
            public AIFleetProductionAllocationUtilityConfig FleetAllocationUtility { get; set; }
        }

        /// <summary>
        /// Utility considerations used to route production among eligible fleets.
        /// </summary>
        [PersistableObject]
        public class AIFleetProductionAllocationUtilityConfig
        {
            public AIConsiderationConfig AttackReadiness { get; set; }
            public AIConsiderationConfig AttackRequirements { get; set; }
            public AIConsiderationConfig SystemPresence { get; set; }
            public AIConsiderationConfig Headquarters { get; set; }
            public AIConsiderationConfig TargetValue { get; set; }
            public AIConsiderationConfig ColonyRegiments { get; set; }
            public AIConsiderationConfig ColonyCapacity { get; set; }
            public AIConsiderationConfig AssemblyWeakness { get; set; }
            public AIConsiderationConfig AssemblyCapacityNeed { get; set; }
        }

        /// <summary>
        /// Utility considerations that determine relative production-demand pressure.
        /// </summary>
        [PersistableObject]
        public class AIProductionDemandUtilityConfig
        {
            public AIConsiderationConfig Deficit { get; set; }
            public AIConsiderationConfig TrainingBacklog { get; set; }
            public AIConsiderationConfig SectorCoverage { get; set; }
            public AIConsiderationConfig PrimaryHub { get; set; }
            public AIConsiderationConfig FacilityBalance { get; set; }
            public AIConsiderationConfig FacilityInvestment { get; set; }
            public AIConsiderationConfig ColonyFoundation { get; set; }
            public AIConsiderationConfig FacilityPortfolio { get; set; }
            public AIConsiderationConfig UpgradeValue { get; set; }
            public AIConsiderationConfig UpgradeHeadquarters { get; set; }
            public AIConsiderationConfig ResourceShortage { get; set; }
            public AIConsiderationConfig MaintenanceShortfall { get; set; }
            public AIConsiderationConfig MaintenanceReserve { get; set; }
            public AIConsiderationConfig DefenseDeficit { get; set; }
            public AIConsiderationConfig DefenseValue { get; set; }
            public AIConsiderationConfig DefenseHeadquarters { get; set; }
            public AIConsiderationConfig DefenseThreat { get; set; }
            public AIConsiderationConfig ShieldSupport { get; set; }
            public AIConsiderationConfig ShieldSectorRisk { get; set; }
            public AIConsiderationConfig FleetTargetValue { get; set; }
            public AIConsiderationConfig AttackReinforcement { get; set; }
            public AIConsiderationConfig FleetReadiness { get; set; }
            public AIConsiderationConfig FinalReadiness { get; set; }
            public AIConsiderationConfig StarfighterFill { get; set; }
        }

        /// <summary>
        /// Utility considerations used to rank production-facility destinations.
        /// </summary>
        [PersistableObject]
        public class AIInfrastructurePlacementUtilityConfig
        {
            public AIConsiderationConfig SystemCoverage { get; set; }

            public AIConsiderationConfig ExistingHub { get; set; }

            public AIConsiderationConfig ConstructionHub { get; set; }

            public AIConsiderationConfig SecondTrainingFacility { get; set; }

            public AIConsiderationConfig AvailableEnergy { get; set; }

            public AIConsiderationConfig PlanetValue { get; set; }

            public AIConsiderationConfig SystemSecurity { get; set; }

            public AIConsiderationConfig DemandProximity { get; set; }

            public AIConsiderationConfig ResourceOpportunityCost { get; set; }
        }

        /// <summary>
        /// Legacy serialized hub-allocation tuning retained for configuration compatibility.
        /// Infrastructure planning no longer consumes these values.
        /// </summary>
        [PersistableObject]
        public class AIInfrastructureAllocationUtilityConfig
        {
            public AIConsiderationConfig ExistingFacilities { get; set; }

            public AIConsiderationConfig UnassignedHub { get; set; }

            public AIConsiderationConfig FeasibleCapacity { get; set; }

            public AIConsiderationConfig StrategicValue { get; set; }
        }

        /// <summary>
        /// AI non-capital production summary thresholds.
        /// </summary>
        [PersistableObject]
        public class AINonCapitalSummaryConfig
        {
            public int MajorThreatWeaponDefenseCapacity { get; set; }

            public int MajorThreatShieldDefenseCapacity { get; set; }

            public int StrategicThreatShieldDefenseCapacity { get; set; }

            public int StrategicThreatWeaponDefenseCapacity { get; set; }

            public int MinimumShieldDefenseCapacity { get; set; }

            public int SupportThreatWeaponDefenseCapacity { get; set; }

            public float SubtypeSelectorRatioThreshold { get; set; }

            public int SupportThreshold { get; set; }

            public int SupportDivisor { get; set; }

            public int SupportUrgencyCap { get; set; }

            public int BaseRequirementDefault { get; set; }

            public int BaseRequirementInfrastructure { get; set; }

            public int BaseRequirementHeadquarters { get; set; }

            public int StarfighterRequirementDefault { get; set; }

            public int StarfighterRequirementInfrastructure { get; set; }

            public int StarfighterRequirementHeadquarters { get; set; }

            public int UnthreatenedInfrastructureStarfighterBaselinePercent { get; set; }

            public int InteriorStarfighterBaselinePercent { get; set; }

            public bool RequireStaticDefenseBeforeStarfighters { get; set; }
        }

        /// <summary>
        /// Garrison troop requirements based on popular support.
        /// </summary>
        [PersistableObject]
        public class GarrisonConfig
        {
            public int SupportThreshold { get; set; }

            public int GarrisonDivisor { get; set; }

            public int UprisingMultiplier { get; set; }

            public int FleetLoadingDeficitThreshold { get; set; }

            public int InteriorCaptureFloorPercent { get; set; }
        }

        /// <summary>
        /// Uprising rolls, table lookups, and resolution.
        /// </summary>
        [PersistableObject]
        public class UprisingConfig
        {
            public int DiceRange { get; set; }

            public int DiceAddend { get; set; }

            public int MissionLeadershipDivisor { get; set; }

            public int InciteMissionSupportShift { get; set; }

            public int SubdueOwnedSupportBase { get; set; }

            public int SubdueOwnedSupportRange { get; set; }

            public int SubdueNeutralSupportBase { get; set; }

            public int SubdueNeutralSupportRange { get; set; }

            public Dictionary<int, int> PrimaryConsequenceTable { get; set; } =
                new Dictionary<int, int>();

            public Dictionary<int, int> SecondaryConsequenceTable { get; set; } =
                new Dictionary<int, int>();

            public int ControllerSupportShift { get; set; }

            public int ActiveSupportDriftMinTicks { get; set; }

            public int ActiveSupportDriftMaxTicks { get; set; }

            public int IncidentPulseMinTicks { get; set; }

            public int IncidentPulseMaxTicks { get; set; }

            public int ClearUprisingMinTicks { get; set; }

            public int ClearUprisingMaxTicks { get; set; }
        }

        /// <summary>
        /// Periodic popular support shifts and hostile force penalties.
        /// </summary>
        [PersistableObject]
        public class SupportShiftConfig
        {
            public int FleetPenalty { get; set; }

            public int FighterPenalty { get; set; }

            public int TroopPenalty { get; set; }

            public int OwnershipTransferThreshold { get; set; }

            public int WeakSupportPenaltyDivisor { get; set; }

            public int GarrisonRemovalSupportShift { get; set; }

            public int ControlChangeSupportShift { get; set; }

            public int BlockadeMatchShift { get; set; }

            public int BlockadeOpposeShift { get; set; }

            public int BlockadeMatchShiftIntervalTicks { get; set; }

            public int BlockadeOpposeShiftIntervalTicks { get; set; }

            public int DiplomacyOwnedPlanetSupportBase { get; set; }

            public int DiplomacyOwnedPlanetSupportRange { get; set; }

            public int DiplomacyNeutralPlanetSupportBase { get; set; }

            public int DiplomacyNeutralPlanetSupportRange { get; set; }
        }

        /// <summary>
        /// Fleet transit time and hyperdrive parameters.
        /// </summary>
        [PersistableObject]
        public class MovementConfig
        {
            public double DistanceScale { get; set; }

            public int MinTransitTicks { get; set; }

            public int SameSectorMinTransitTicks { get; set; }

            public int DefaultFighterHyperdrive { get; set; }
        }

        /// <summary>
        /// Manufacturing and production.
        /// </summary>
        [PersistableObject]
        public class ProductionConfig
        {
            public int MaintenanceShortfallAutoscrapInterval { get; set; }

            public int ScrapRefundDivisor { get; set; }

            public int ResourceMaintenanceLoadPercent { get; set; }

            public int ResourceCollectionBasePercent { get; set; }

            public int ResourceStartupBasePercent { get; set; }

            public int ResourceStartupRandomPercent { get; set; }
        }

        /// <summary>
        /// System smuggling calculation and resource-redirection rules.
        /// </summary>
        [PersistableObject]
        public class SmugglingConfig
        {
            public Dictionary<int, int> LossPercentByMinimumSupport { get; set; } =
                new Dictionary<int, int>();

            public int CapitalShipSuppression { get; set; } = 10;

            public int StarfighterSuppression { get; set; } = 5;

            public int RegimentSuppression { get; set; } = 2;
        }

        /// <summary>
        /// Per-planet generation limits.
        /// </summary>
        [PersistableObject]
        public class PlanetConfig
        {
            public int MaxEnergy { get; set; }

            public int MaxRawMaterials { get; set; }
        }

        /// <summary>
        /// Combat resolution parameters for space combat, assault, and bombardment.
        /// </summary>
        [PersistableObject]
        public class CombatConfig
        {
            public BombardmentConfig Bombardment { get; set; } = new BombardmentConfig();

            public PlanetaryAssaultConfig PlanetaryAssault { get; set; } =
                new PlanetaryAssaultConfig();

            public SpaceCombatConfig SpaceCombat { get; set; } = new SpaceCombatConfig();
        }

        /// <summary>
        /// Orbital bombardment resolution parameters.
        /// </summary>
        [PersistableObject]
        public class BombardmentConfig
        {
            public int AttackerLeadershipDivisor { get; set; }
            public int DefenderLeadershipDivisor { get; set; }
            public int ShieldStrengthDivisor { get; set; }
            public int StrikeRollMinimum { get; set; }
            public int StrikeRollMaximum { get; set; }
            public int EnergyResistance { get; set; }
            public int AllocatedEnergyResistance { get; set; }
            public int HeadquartersResistance { get; set; }
            public int CivilianSupportPenalty { get; set; }
            public int DestroyPlanetPersonnelInjuryPercent { get; set; }
            public int DestroyPlanetMinorPersonnelDeathPercent { get; set; }
            public int DestroyPlanetCoreSupportPenalty { get; set; }
            public int DestroyPlanetOuterRimSupportPenalty { get; set; }
            public int DestroyPlanetOuterRimSupportThreshold { get; set; }
        }

        /// <summary>
        /// Planetary assault resolution parameters.
        /// </summary>
        [PersistableObject]
        public class PlanetaryAssaultConfig
        {
            public int PersonnelDivisor { get; set; }
            public int ShieldGeneratorLimit { get; set; }
            public int DefenseFireDivisor { get; set; }
            public int CollateralDamagePercent { get; set; }
            public int GeneralLeadershipDivisor { get; set; }
            public int ContestRollMaximum { get; set; }
            public int DefenderWinsMaximum { get; set; }
            public int AttackerWinsMinimum { get; set; }
            public int CaptureGarrisonCount { get; set; }
        }

        /// <summary>
        /// Automatic space-combat resolution parameters.
        /// </summary>
        [PersistableObject]
        public class SpaceCombatConfig
        {
            public double LaserCannonCapitalDamageMultiplier { get; set; }

            public double AutoResolveFighterWeaponRechargeMultiplier { get; set; }

            public int AutoResolveMaximumIterations { get; set; }

            public int AutoResolveStagnationIterations { get; set; }

            public double AutoResolveRetreatStrengthRatio { get; set; }

            public double AutoResolveMinimumManeuverRatio { get; set; }

            public int AutoResolveTargetScanDivisor { get; set; }

            public double AutoResolveStartingDistance { get; set; }

            public double AutoResolveWithdrawalDistance { get; set; }

            public double AutoResolveComponentDamageInterval { get; set; }

            public int AutoResolveComponentDamageRollMaximum { get; set; }

            public int AutoResolveComponentDelayMinimum { get; set; }

            public int AutoResolveComponentDelayMaximum { get; set; }

            public int AutoResolveComponentDelayRecovery { get; set; }
        }

        /// <summary>
        /// Manufacturing and evacuation penalties during blockades.
        /// </summary>
        [PersistableObject]
        public class BlockadeConfig
        {
            public int CapitalShipProductionPenaltyPercent { get; set; }

            public int FighterProductionPenaltyPercent { get; set; }

            public int EvacuationLossPercent { get; set; }
        }

        /// <summary>
        /// Force tier advancement and detection thresholds.
        /// </summary>
        [PersistableObject]
        public class JediConfig
        {
            public int DiscoveringForceUserThreshold { get; set; }

            public int ForceQualifiedThreshold { get; set; }

            public int FastHealThreshold { get; set; }

            public int ForceGrowthPerMission { get; set; }

            public int TrainingCatchUpPercent { get; set; }

            public int EncounterProbabilityOffset { get; set; }

            public Dictionary<int, int> RankLabelByMinimumForceRank { get; set; } =
                new Dictionary<int, int>();

            /// <summary>
            /// Resolves the label for a numeric Force rank using the greatest configured threshold
            /// that does not exceed the supplied rank.
            /// </summary>
            /// <param name="forceRank">The numeric rank to classify.</param>
            /// <returns>The configured label active at that rank.</returns>
            public ForceRankLabel GetRankLabel(int forceRank)
            {
                return (ForceRankLabel)
                    new ProbabilityTable(RankLabelByMinimumForceRank).Lookup(forceRank);
            }

            /// <summary>
            /// Returns the lowest configured threshold assigned to a label, or
            /// <see cref="int.MaxValue"/> when the label is not authored.
            /// </summary>
            /// <param name="label">The configured label whose first threshold is requested.</param>
            /// <returns>The label's minimum numeric Force rank.</returns>
            public int GetMinimumRank(ForceRankLabel label)
            {
                return RankLabelByMinimumForceRank
                    .Where(entry => entry.Value == (int)label)
                    .Select(entry => entry.Key)
                    .DefaultIfEmpty(int.MaxValue)
                    .Min();
            }
        }

        /// <summary>
        /// Linked-officer capture, injury, and combat-growth rules.
        /// </summary>
        [PersistableObject]
        public class DuelResolutionConfig
        {
            public Dictionary<int, int> CombatCaptureAvoidance { get; set; } =
                new Dictionary<int, int>();

            public int CaptureEvasionInjuryBaseChance { get; set; } = 100;

            public int MinimumInjuryChance { get; set; } = 1;

            public int InjuryBase { get; set; } = 1;

            public int InjurySecondaryRollMaximum { get; set; } = 29;

            public int CombatReward { get; set; } = 1;
        }

        /// <summary>
        /// Research advancement and officer research mechanics.
        /// </summary>
        [PersistableObject]
        public class ResearchConfig
        {
            public int BaseResearchPoints { get; set; }

            public int ResearchDiceRange { get; set; }

            public int RefreshIntervalBase { get; set; }

            public int RefreshIntervalSpread { get; set; }
        }

        /// <summary>
        /// Assassination mission outcome parameters.
        /// </summary>
        [PersistableObject]
        public class AssassinationConfig
        {
            public int BaseInjury { get; set; }

            public int PrimaryInjuryRange { get; set; }

            public int SecondaryInjuryRange { get; set; }

            public int KillProbability { get; set; }
        }

        /// <summary>
        /// Officer healing and ship/fighter repair rates.
        /// </summary>
        [PersistableObject]
        public class RecoveryConfig
        {
            public int MaxInjuryPoints { get; set; }

            public int FastHealAmount { get; set; }

            public int NormalHealAmount { get; set; }

            public int FastRepairAmount { get; set; }

            public int NormalRepairAmount { get; set; }

            public int FastReplacementAmount { get; set; }

            public int NormalReplacementAmount { get; set; }
        }

        /// <summary>
        /// Captive escape probability and loyalty effects.
        /// </summary>
        [PersistableObject]
        public class CaptiveConfig
        {
            public TickRangeConfig EscapeAttemptInterval { get; set; } = new TickRangeConfig();

            public Dictionary<int, int> EscapeTable { get; set; } = new Dictionary<int, int>();

            public int EscapeLoyaltyShift { get; set; }
        }

        /// <summary>
        /// Inclusive tick bounds for scheduling recurring activity.
        /// </summary>
        [PersistableObject]
        public class TickRangeConfig
        {
            public int Minimum { get; set; }

            public int Maximum { get; set; }
        }

        /// <summary>
        /// Officer loyalty reactions to strategic control changes.
        /// </summary>
        [PersistableObject]
        public class OfficerLoyaltyConfig
        {
            public RandomRangeConfig PlanetAcquisitionLoyaltyShift { get; set; } =
                new RandomRangeConfig();
        }

        /// <summary>
        /// An authored inclusive integer range.
        /// </summary>
        [PersistableObject]
        public class RandomRangeConfig
        {
            public int Minimum { get; set; }
            public int Maximum { get; set; }
        }

        /// <summary>
        /// Game tick interval presets.
        /// </summary>
        [PersistableObject]
        public class GameSpeedConfig
        {
            public float FastTickIntervalSeconds { get; set; } = 1f;

            public float MediumTickIntervalSeconds { get; set; } = 10f;

            public float SlowTickIntervalSeconds { get; set; } = 60f;

            public float VerySlowTickIntervalSeconds { get; set; } = 120f;
        }

        /// <summary>
        /// Message retention settings.
        /// </summary>
        [PersistableObject]
        public class MessageConfig
        {
            public int RetentionTicks { get; set; }
        }

        /// <summary>
        /// Controls the additional sector intelligence granted by successful espionage.
        /// </summary>
        [PersistableObject]
        public class EspionageConfig
        {
            public RandomCountConfig CoreSectorBonus { get; set; } = new RandomCountConfig();

            public RandomCountConfig HeadquartersBonus { get; set; } = new RandomCountConfig();
        }

        /// <summary>
        /// Defines a count as a fixed minimum plus a random value below the spread.
        /// </summary>
        [PersistableObject]
        public class RandomCountConfig
        {
            public int Base { get; set; }

            public int Spread { get; set; }
        }

        /// <summary>
        /// AI mission dispatch probability tables. Each table maps a per-mission score to a dispatch probability.
        /// </summary>
        [PersistableObject]
        public class AIMissionTablesConfig
        {
            public Dictionary<int, int> Reconnaissance { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Diplomacy { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> SubdueUprising { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Espionage { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> InciteUprising { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Rescue { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Sabotage { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Abduction { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Assassination { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Recruitment { get; set; } = new Dictionary<int, int>();
        }

        /// <summary>
        /// Probability tables grouped by data type.
        /// </summary>
        [PersistableObject]
        public class ProbabilityTablesConfig
        {
            public Dictionary<int, int> UprisingStart { get; set; } = new Dictionary<int, int>();

            public MissionProbabilityTablesConfig Mission { get; set; } =
                new MissionProbabilityTablesConfig();
        }

        /// <summary>
        /// Per-mission success probability tables and tick ranges.
        /// </summary>
        [PersistableObject]
        public class MissionProbabilityTablesConfig
        {
            public Dictionary<int, int> Abduction { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Assassination { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> PlanetaryDecoy { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> FleetDecoy { get; set; } = new Dictionary<int, int>();

            public int DecoyDefenderScalingPercent { get; set; }

            public int FoilDefenderScalingPercent { get; set; }

            public int FoilFlatScoreAdjustment { get; set; }

            public Dictionary<int, int> Diplomacy { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Espionage { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Foil { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Evasion { get; set; } = new Dictionary<int, int>();

            public int DefaultSuccessProbability { get; set; } = 50;

            public int DefaultEvasionProbability { get; set; } = 50;

            public Dictionary<int, int> InciteUprising { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Recruitment { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Rescue { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Sabotage { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> SubdueUprising { get; set; } = new Dictionary<int, int>();

            public MissionTickRangesConfig TickRanges { get; set; } = new MissionTickRangesConfig();

            /// <summary>
            /// Returns the success probability table for the given mission config key, or null.
            /// </summary>
            /// <param name="key">Mission config key.</param>
            /// <returns>The matching success table, or null.</returns>
            public Dictionary<int, int> GetSuccessTable(string key)
            {
                return key switch
                {
                    "Abduction" => Abduction,
                    "Assassination" => Assassination,
                    "Diplomacy" => Diplomacy,
                    "Espionage" => Espionage,
                    "InciteUprising" => InciteUprising,
                    "Recruitment" => Recruitment,
                    "Rescue" => Rescue,
                    "Sabotage" => Sabotage,
                    "SubdueUprising" => SubdueUprising,
                    _ => null,
                };
            }

            /// <summary>
            /// Resolves a score through a named mission probability table.
            /// </summary>
            /// <param name="key">The key.</param>
            /// <param name="score">The score.</param>
            /// <returns>The requested success probability.</returns>
            public int GetSuccessProbability(string key, int score)
            {
                Dictionary<int, int> table = GetSuccessTable(key);
                if (table == null || table.Count == 0)
                    return DefaultSuccessProbability;

                return new ProbabilityTable(table).Lookup(score);
            }
        }

        /// <summary>
        /// Mission tick range: minimum ticks before execution plus a random spread.
        /// </summary>
        [PersistableObject]
        public class MissionTickConfig
        {
            public int Base { get; set; }

            public int Spread { get; set; }
        }

        /// <summary>
        /// Per-mission tick ranges.
        /// </summary>
        [PersistableObject]
        public class MissionTickRangesConfig
        {
            public MissionTickConfig Abduction { get; set; } = new MissionTickConfig();

            public MissionTickConfig Assassination { get; set; } = new MissionTickConfig();

            public MissionTickConfig Diplomacy { get; set; } = new MissionTickConfig();

            public MissionTickConfig Espionage { get; set; } = new MissionTickConfig();

            public MissionTickConfig InciteUprising { get; set; } = new MissionTickConfig();

            public MissionTickConfig Reconnaissance { get; set; } = new MissionTickConfig();

            public MissionTickConfig Recruitment { get; set; } = new MissionTickConfig();

            public MissionTickConfig Rescue { get; set; } = new MissionTickConfig();

            public MissionTickConfig Sabotage { get; set; } = new MissionTickConfig();

            public MissionTickConfig SubdueUprising { get; set; } = new MissionTickConfig();

            public MissionTickConfig Research { get; set; } = new MissionTickConfig();

            public MissionTickConfig JediTraining { get; set; } = new MissionTickConfig();

            /// <summary>
            /// Returns the tick config for the given mission config key, or null.
            /// </summary>
            /// <param name="key">Mission config key.</param>
            /// <returns>The matching tick config, or null.</returns>
            public MissionTickConfig GetTickConfig(string key)
            {
                return key switch
                {
                    "Abduction" => Abduction,
                    "Assassination" => Assassination,
                    "Diplomacy" => Diplomacy,
                    "Espionage" => Espionage,
                    "InciteUprising" => InciteUprising,
                    "Reconnaissance" => Reconnaissance,
                    "Recruitment" => Recruitment,
                    "Rescue" => Rescue,
                    "Sabotage" => Sabotage,
                    "SubdueUprising" => SubdueUprising,
                    "Research" => Research,
                    "JediTraining" => JediTraining,
                    _ => null,
                };
            }
        }
    }
}
