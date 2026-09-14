using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.Util.Common;
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

        public VictoryConfig Victory { get; set; } = new VictoryConfig();

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
            public int TickInterval { get; set; } = 1;

            public bool EnablePlanetaryAssaults { get; set; } = true;

            public int DiplomacyMinimumSkill { get; set; } = 50;

            public int RecruitmentMinimumLeadership { get; set; } = 80;

            public AIMissionTablesConfig MissionTables { get; set; } = new AIMissionTablesConfig();

            public AIMissionPlanningConfig MissionPlanning { get; set; } =
                new AIMissionPlanningConfig();

            public GarrisonConfig Garrison { get; set; } = new GarrisonConfig();

            public int DeploymentGateLow { get; set; }

            public int DeploymentGateHigh { get; set; }

            public AISelectionConfig Selection { get; set; } = new AISelectionConfig();

            public AINonCapitalSummaryConfig NonCapitalSummary { get; set; } =
                new AINonCapitalSummaryConfig();

            public AIInfrastructureConfig Infrastructure { get; set; } =
                new AIInfrastructureConfig();

            public AIFleetDeploymentConfig FleetDeployment { get; set; } =
                new AIFleetDeploymentConfig();
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
            public AIResponseCurveShape Shape { get; set; } = AIResponseCurveShape.Linear;

            public double Exponent { get; set; } = 1;

            public double Midpoint { get; set; } = 0.5;

            public double Steepness { get; set; } = 10;
        }

        /// <summary>
        /// Configuration for one normalized and weighted AI consideration.
        /// </summary>
        [PersistableObject]
        public class AIConsiderationConfig
        {
            public double Weight { get; set; }

            public double InputMaximum { get; set; } = 1;

            public AIResponseCurveConfig Curve { get; set; } = new AIResponseCurveConfig();
        }

        /// <summary>
        /// Mission planning priorities and intelligence freshness settings.
        /// </summary>
        [PersistableObject]
        public class AIMissionPlanningConfig
        {
            public int RetainedAlternativesPerMission { get; set; } = 3;

            public int EspionageRefreshIntervalTicks { get; set; } = 20;

            public int HostileMissionMaximumIntelAgeTicks { get; set; } = 40;

            public int MaximumJediTrainingStudents { get; set; } = 3;

            public int MinimumMissionScore { get; set; } = 20;

            public int MinimumUprisingMissionSuccessPercent { get; set; } = 20;

            public int MaximumOfficerMissionLossProbability { get; set; } = 20;

            public int HostileMissionIntelAgeFoilPenaltyPerRefreshInterval { get; set; } = 5;

            public int MaximumUnprotectedOfficerMissionFoilProbability { get; set; } = 20;

            public AIMissionUtilityConfig Utility { get; set; } = new AIMissionUtilityConfig();
        }

        /// <summary>
        /// Utility considerations used to rank mission proposals.
        /// </summary>
        [PersistableObject]
        public class AIMissionUtilityConfig
        {
            public AIMissionObjectiveUtilityConfig Objective { get; set; } =
                new AIMissionObjectiveUtilityConfig();

            public AIMissionPriorityUtilityConfig Priority { get; set; } =
                new AIMissionPriorityUtilityConfig();

            public AISabotageUtilityConfig Sabotage { get; set; } = new AISabotageUtilityConfig();

            public AIDiplomacyUtilityConfig Diplomacy { get; set; } =
                new AIDiplomacyUtilityConfig();

            public AIOfficerTargetUtilityConfig OfficerTarget { get; set; } =
                new AIOfficerTargetUtilityConfig();
        }

        /// <summary>
        /// General mission value and risk considerations.
        /// </summary>
        [PersistableObject]
        public class AIMissionObjectiveUtilityConfig
        {
            public AIConsiderationConfig Success { get; set; } = Weighted(100, 100);

            public AIConsiderationConfig FoilRisk { get; set; } = Weighted(100, 100);

            public AIConsiderationConfig TravelCost { get; set; } = Weighted(100, 100);

            public AIConsiderationConfig OfficerRisk { get; set; } = Weighted(100);

            public AIConsiderationConfig IntelAge { get; set; } = Weighted(1000, 1000);

            public AIConsiderationConfig TrainingValue { get; set; } = Weighted(300, 300);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// Mission-type strategic priority considerations.
        /// </summary>
        [PersistableObject]
        public class AIMissionPriorityUtilityConfig
        {
            public AIConsiderationConfig Reconnaissance { get; set; } = Weighted(50);

            public AIConsiderationConfig Recruitment { get; set; } = Weighted(80);

            public AIConsiderationConfig Rescue { get; set; } = Weighted(120);

            public AIConsiderationConfig SubdueUprising { get; set; } = Weighted(120);

            public AIConsiderationConfig Research { get; set; } = Weighted(50);

            public AIConsiderationConfig JediTraining { get; set; } = Weighted(80);

            public AIConsiderationConfig Espionage { get; set; } = Weighted(50);

            public AIConsiderationConfig Diplomacy { get; set; } = Weighted(30);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };
        }

        /// <summary>
        /// Strategic value considerations for sabotage targets.
        /// </summary>
        [PersistableObject]
        public class AISabotageUtilityConfig
        {
            public AIConsiderationConfig Infrastructure { get; set; } = Weighted(0);

            public AIConsiderationConfig Defense { get; set; } = Weighted(125);

            public AIConsiderationConfig Shield { get; set; } = Weighted(150);

            public AIConsiderationConfig AttackTarget { get; set; } = Weighted(150);

            public AIConsiderationConfig AttackDefense { get; set; } = Weighted(200);

            public AIConsiderationConfig FavoredSupportRegiment { get; set; } = Weighted(50);

            public AIConsiderationConfig GarrisonRegiment { get; set; } = Weighted(100);

            public AIConsiderationConfig GarrisonStarfighter { get; set; } = Weighted(75);

            public AIConsiderationConfig OtherUnit { get; set; } = Weighted(25);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };
        }

        /// <summary>
        /// Strategic value considerations for diplomacy targets.
        /// </summary>
        [PersistableObject]
        public class AIDiplomacyUtilityConfig
        {
            public AIConsiderationConfig SupportDeficit { get; set; } = Weighted(100, 100);

            public AIConsiderationConfig CoreWorld { get; set; } = Weighted(1000);

            public AIConsiderationConfig ConstructionFacility { get; set; } = Weighted(250, 10);

            public AIConsiderationConfig Shipyard { get; set; } = Weighted(200, 10);

            public AIConsiderationConfig TrainingFacility { get; set; } = Weighted(50, 10);

            public AIConsiderationConfig ResourceNode { get; set; } = Weighted(75, 15);

            public AIConsiderationConfig SectorSupportRisk { get; set; } = Weighted(250, 10);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// Strategic value considerations for hostile officer targets.
        /// </summary>
        [PersistableObject]
        public class AIOfficerTargetUtilityConfig
        {
            public AIConsiderationConfig Combat { get; set; } = Weighted(300, 300);
            public AIConsiderationConfig Espionage { get; set; } = Weighted(300, 300);
            public AIConsiderationConfig Diplomacy { get; set; } = Weighted(300, 300);
            public AIConsiderationConfig Leadership { get; set; } = Weighted(300, 300);
            public AIConsiderationConfig ShipResearch { get; set; } = Weighted(300, 300);
            public AIConsiderationConfig FacilityResearch { get; set; } = Weighted(300, 300);
            public AIConsiderationConfig TroopResearch { get; set; } = Weighted(300, 300);

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// Fleet attack and deployment scoring settings.
        /// </summary>
        [PersistableObject]
        public class AIFleetDeploymentConfig
        {
            public int MinimumBattleFleetCount { get; set; } = 3;

            public int PlanetsPerBattleFleet { get; set; } = 15;

            public int MinimumAttackStrength { get; set; }

            public int MinimumMobileCombatStrength { get; set; }

            public int MobileCombatStrengthPerPlanet { get; set; }

            public int MinimumDefenseStrength { get; set; } = 1000;

            public int HeadquartersDefenseCombatPercent { get; set; } = 35;

            public int MinimumPlanetaryAssaultRegimentCount { get; set; } = 1;

            public int MinimumPlanetaryAssaultSuccessPercent { get; set; } = 55;

            public int AttackStrengthPercentOfDefense { get; set; }

            public int AttackStrengthPercentOfStrongestHostileFleet { get; set; }

            public double AttackReadinessFloorWeight { get; set; } = 4;

            public AIAttackUtilityConfig AttackUtility { get; set; } = new AIAttackUtilityConfig();

            public int ExposedSectorMinimumOwnedPresencePercent { get; set; } = 50;

            public AIDefenseUtilityConfig DefenseUtility { get; set; } =
                new AIDefenseUtilityConfig();

            public AIDefenseAllocationUtilityConfig DefenseAllocationUtility { get; set; } =
                new AIDefenseAllocationUtilityConfig();

            public AIColonizationUtilityConfig ColonizationUtility { get; set; } =
                new AIColonizationUtilityConfig();

            public int ColonizationFleetTargetCount { get; set; } = 2;

            public int ColonizationFleetMinimumRegimentCount { get; set; } = 2;

            public int ColonizationFleetMaximumRegimentCount { get; set; } = 4;
        }

        /// <summary>
        /// Utility considerations used to value fleet attacks and attack-fleet reinforcement.
        /// </summary>
        [PersistableObject]
        public class AIAttackUtilityConfig
        {
            public AIConsiderationConfig StrategicValue { get; set; } = Weighted(55);

            public AIConsiderationConfig SectorSupport { get; set; } = Weighted(300, 10);

            public AIConsiderationConfig SystemPresence { get; set; } = Weighted(30);

            public AIConsiderationConfig Readiness { get; set; } = Weighted(35);

            public AIConsiderationConfig Ready { get; set; } = Weighted(350);

            public AIConsiderationConfig CaptureViability { get; set; } = Weighted(45);

            public AIConsiderationConfig TravelEfficiency { get; set; } = Weighted(20);

            public AIConsiderationConfig ExpectedLossRisk { get; set; } = Weighted(50);

            public AIConsiderationConfig OpportunityCost { get; set; } = Weighted(30);

            public AIConsiderationConfig IntelAgeRisk { get; set; } = Weighted(2);

            public AIConsiderationConfig ExistingOrder { get; set; } = Weighted(300);

            public AIConsiderationConfig Headquarters { get; set; } = Weighted(45);

            public AIConsiderationConfig OrbitalAdvantage { get; set; } = Weighted(250);

            public AIConsiderationConfig ExposedBombardment { get; set; } = Weighted(100);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// Utility considerations used to value colonization fleets and destinations.
        /// </summary>
        [PersistableObject]
        public class AIColonizationUtilityConfig
        {
            public AIConsiderationConfig Base { get; set; } = Weighted(45);

            public AIConsiderationConfig StrategicValue { get; set; } = Weighted(20);

            public AIConsiderationConfig TravelEfficiency { get; set; } = Weighted(20);

            public AIConsiderationConfig Ready { get; set; } = Weighted(35);

            public AIConsiderationConfig OpportunityCost { get; set; } = Weighted(20);

            public AIConsiderationConfig ExistingOrder { get; set; } = Weighted(100);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };
        }

        /// <summary>
        /// Utility considerations used to value fleet defense.
        /// </summary>
        [PersistableObject]
        public class AIDefenseUtilityConfig
        {
            public AIConsiderationConfig Base { get; set; } = Weighted(1000);

            public AIConsiderationConfig SectorRisk { get; set; } = Weighted(300, 10);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// Utility considerations used to allocate defense fleets among valid assignments.
        /// </summary>
        [PersistableObject]
        public class AIDefenseAllocationUtilityConfig
        {
            public AIConsiderationConfig SectorRisk { get; set; } = Weighted(1000000, 10);

            public AIConsiderationConfig StrategicValue { get; set; } = Weighted(10000, 1000);

            public AIConsiderationConfig DefenseNeed { get; set; } = Weighted(1, 10000);

            public AIConsiderationConfig TravelEfficiency { get; set; } = Weighted(1000000);

            public AIConsiderationConfig ForceEfficiency { get; set; } = Weighted(1);

            public AIConsiderationConfig ReinforcementNeed { get; set; } =
                Weighted(int.MaxValue, int.MaxValue);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// AI manufacturing selection weights and limits.
        /// </summary>
        [PersistableObject]
        public class AISelectionConfig
        {
            public float MinimumSelectableScore { get; set; }
            public int PreferredStarfighterTypeCountPerFleet { get; set; }
            public int PreferredRegimentTypeCountPerDestination { get; set; }
            public AITechnologySelectionUtilityConfig TechnologyUtility { get; set; } =
                new AITechnologySelectionUtilityConfig();
            public int RefinedMaterialReservePercent { get; set; } = 20;
            public int RefinedMaterialEconomyWarningPercent { get; set; } = 40;
            public int RefinedMaterialCommitmentHorizonTicks { get; set; } = 25;
            public int MinimumMaintenanceHeadroomAfterProduction { get; set; } = 200;
            public int MaintenanceHeadroomHardFloor { get; set; } = 0;
            public AIProductionUtilityConfig ProductionUtility { get; set; } =
                new AIProductionUtilityConfig();
        }

        /// <summary>
        /// Utility considerations used to choose a unit technology for production.
        /// </summary>
        [PersistableObject]
        public class AITechnologySelectionUtilityConfig
        {
            public AIBuildingSelectionUtilityConfig Building { get; set; } =
                new AIBuildingSelectionUtilityConfig();

            public AIStarfighterSelectionUtilityConfig Starfighter { get; set; } =
                new AIStarfighterSelectionUtilityConfig();

            public AIRegimentSelectionUtilityConfig Regiment { get; set; } =
                new AIRegimentSelectionUtilityConfig();

            public AISpecialForcesSelectionUtilityConfig SpecialForces { get; set; } =
                new AISpecialForcesSelectionUtilityConfig();

            public AIConsiderationConfig DuplicateCost { get; set; } = Weighted(450, 10);

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// Utility considerations used to choose a building technology.
        /// </summary>
        [PersistableObject]
        public class AIBuildingSelectionUtilityConfig
        {
            public AIConsiderationConfig Capability { get; set; } = Weighted(1000, 1000);

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// Utility considerations used to choose a starfighter technology.
        /// </summary>
        [PersistableObject]
        public class AIStarfighterSelectionUtilityConfig
        {
            public AIConsiderationConfig Laser { get; set; } = Weighted(120, 20);
            public AIConsiderationConfig Ion { get; set; } = Weighted(140, 20);
            public AIConsiderationConfig Torpedo { get; set; } = Weighted(180, 20);
            public AIConsiderationConfig MissingIon { get; set; } = Weighted(60);
            public AIConsiderationConfig MissingTorpedo { get; set; } = Weighted(60);
            public AIConsiderationConfig PlanetDefenseEfficiency { get; set; } = Weighted(100, 100);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// Utility considerations used to choose a regiment technology.
        /// </summary>
        [PersistableObject]
        public class AIRegimentSelectionUtilityConfig
        {
            public AIConsiderationConfig Attack { get; set; } = Weighted(80, 10);
            public AIConsiderationConfig Defense { get; set; } = Weighted(80, 10);
            public AIConsiderationConfig BombardmentDefense { get; set; } = Weighted(60, 10);
            public AIConsiderationConfig Base { get; set; } = Weighted(50);
            public AIConsiderationConfig MaintenanceCost { get; set; } = Weighted(100, 10);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// Utility considerations used to choose a special-forces technology.
        /// </summary>
        [PersistableObject]
        public class AISpecialForcesSelectionUtilityConfig
        {
            public AIConsiderationConfig BuildEfficiency { get; set; } = Weighted(100, 100);

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// Utility costs applied when ranking production proposals.
        /// </summary>
        [PersistableObject]
        public class AIProductionUtilityConfig
        {
            public AIConsiderationConfig TravelCost { get; set; } = Weighted(100, 100);

            public AIConsiderationConfig HeadroomRisk { get; set; } = Weighted(0);

            public AIConsiderationConfig Shortfall { get; set; } = Weighted(0);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// AI infrastructure demand settings.
        /// </summary>
        [PersistableObject]
        public class AIInfrastructureConfig
        {
            public int PlanetsPerConstructionFacility { get; set; }
            public int MinimumConstructionFacilityLanes { get; set; } = 1;
            public int ConstructionFacilityTargetClearTicks { get; set; } = 80;
            public int ShipyardTargetClearTicks { get; set; } = 80;
            public int TrainingFacilityTargetClearTicks { get; set; } = 1;
            public int PlanetsPerShipyard { get; set; }
            public int PlanetsPerTrainingFacility { get; set; }
            public int TrainingDemandsPerFacility { get; set; } = 4;
            public int ManufacturingFacilityBaseDemandPercent { get; set; }
            public int ConstructionFacilityDemandPercent { get; set; }
            public int ShipyardDemandPercent { get; set; }
            public int TrainingFacilityDemandPercent { get; set; } = 100;
            public int FacilitySectorHubTargetCount { get; set; } = 5;
            public int ShipyardSectorHubTargetCount { get; set; } = 6;
            public int FacilitySectorHubMaximumCount { get; set; } = 7;
            public int FacilityPlanetsPerSector { get; set; } = 3;
            public int FacilitySectorSecondaryTargetCount { get; set; } = 3;
            public AIInfrastructurePlacementUtilityConfig PlacementUtility { get; set; } =
                new AIInfrastructurePlacementUtilityConfig();
            public int ProductionFacilityMaintenanceAllocationPercent { get; set; } = 30;
            public int ProductionFacilityInvestmentHorizonTicks { get; set; } = 70;
            public int FacilityConstructionLaneReserve { get; set; } = 1;
            public int ProductionQueueTargetPlanningIntervals { get; set; } = 1;
            public int ProductionFacilityUpgradeMinimumRemainingCount { get; set; } = 1;
            public int ProductionFacilityUpgradeDemandPercent { get; set; } = 65;
            public int FleetCapitalShipDemandPercent { get; set; } = 80;
            public int FleetStarfighterDemandPercent { get; set; } = 50;
            public int FleetRegimentDemandPercent { get; set; } = 60;
            public int FleetSeedCapitalShipDemandPercent { get; set; } = 95;
            public int ColonizationFleetDemandPercent { get; set; } = 110;
            public int SpecialForcesDemandPercent { get; set; } = 25;

            public int SpecialForcesMissionCoveragePercent { get; set; } = 10;

            public int StarfighterParentFillPercent { get; set; } = 100;
            public int StarfighterLocalReservePercent { get; set; }
            public int AssaultRegimentLoadPercent { get; set; } = 100;
            public int GarrisonRegimentReservePercent { get; set; }
            public int PlanetaryStarfighterDemandPercent { get; set; } = 40;
            public int IdleShipyardFighterReserveCount { get; set; } = 1;
            public int IdleShipyardFighterDemandPercent { get; set; } = 1;
            public int PlanetaryWeaponTargetCount { get; set; } = 1;
            public int PlanetaryDefenseSurplusBatchSize { get; set; } = 1;
            public int PlanetaryShieldDemandPercent { get; set; } = 45;
            public int PlanetaryWeaponDemandPercent { get; set; } = 35;
            public int PlanetaryGarrisonDemandPercent { get; set; } = 30;
            public int PlanetaryDefenseMaintenanceReservePercent { get; set; } = 10;
            public int EconomyDefaultBatchSize { get; set; } = 1;
            public int EconomyDemandPercent { get; set; } = 90;
            public int EconomySevereDemandPercent { get; set; } = 100;
            public int EconomySevereDeficitPercent { get; set; } = 25;
            public int EconomyCompetingNeedSlotReserve { get; set; } = 1;
            public int FleetFinalReadinessGateUnitCount { get; set; } = 2;
            public AIProductionDemandUtilityConfig DemandUtility { get; set; } =
                new AIProductionDemandUtilityConfig();
        }

        /// <summary>
        /// Utility considerations that determine relative production-demand pressure.
        /// </summary>
        [PersistableObject]
        public class AIProductionDemandUtilityConfig
        {
            public AIConsiderationConfig Deficit { get; set; } = Weighted(100);
            public AIConsiderationConfig TrainingBacklog { get; set; } = Weighted(5);
            public AIConsiderationConfig SectorCoverage { get; set; } = Weighted(100);
            public AIConsiderationConfig PrimaryHub { get; set; } = Weighted(50);
            public AIConsiderationConfig FacilityBalance { get; set; } = Weighted(100);
            public AIConsiderationConfig FacilityInvestment { get; set; } = Weighted(100);
            public AIConsiderationConfig UpgradeValue { get; set; } = Weighted(20);
            public AIConsiderationConfig UpgradeHeadquarters { get; set; } = Weighted(10);
            public AIConsiderationConfig ResourceShortage { get; set; } = Weighted(100);
            public AIConsiderationConfig MaintenanceShortfall { get; set; } = Weighted(40);
            public AIConsiderationConfig MaintenanceReserve { get; set; } = Weighted(20);
            public AIConsiderationConfig DefenseDeficit { get; set; } = Weighted(20);
            public AIConsiderationConfig DefenseValue { get; set; } = Weighted(25);
            public AIConsiderationConfig DefenseHeadquarters { get; set; } = Weighted(20);
            public AIConsiderationConfig DefenseThreat { get; set; } = Weighted(50);
            public AIConsiderationConfig ShieldSupport { get; set; } = Weighted(50);
            public AIConsiderationConfig ShieldSectorRisk { get; set; } = Weighted(500, 10);
            public AIConsiderationConfig FleetTargetValue { get; set; } = Weighted(20);
            public AIConsiderationConfig AttackReinforcement { get; set; } = Weighted(25);
            public AIConsiderationConfig FleetReadiness { get; set; } = Weighted(35);
            public AIConsiderationConfig FinalReadiness { get; set; } = Weighted(35);
            public AIConsiderationConfig StarfighterFill { get; set; } = Weighted(20);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };

            private static AIConsiderationConfig Weighted(double weight, double inputMaximum) =>
                new AIConsiderationConfig { Weight = weight, InputMaximum = inputMaximum };
        }

        /// <summary>
        /// Utility considerations used to rank production-facility destinations.
        /// </summary>
        [PersistableObject]
        public class AIInfrastructurePlacementUtilityConfig
        {
            public AIConsiderationConfig SystemCoverage { get; set; } = Weighted(50);

            public AIConsiderationConfig ExistingHub { get; set; } = Weighted(30);

            public AIConsiderationConfig ConstructionHub { get; set; } = Weighted(100);

            public AIConsiderationConfig SecondTrainingFacility { get; set; } = Weighted(125);

            public AIConsiderationConfig AvailableEnergy { get; set; } = Weighted(20);

            public AIConsiderationConfig PlanetValue { get; set; } = Weighted(15);

            public AIConsiderationConfig SystemSecurity { get; set; } = Weighted(20);

            public AIConsiderationConfig DemandProximity { get; set; } = Weighted(15);

            public AIConsiderationConfig ResourceOpportunityCost { get; set; } = Weighted(25);

            private static AIConsiderationConfig Weighted(double weight) =>
                new AIConsiderationConfig { Weight = weight };
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

            public int StarfighterRequirementInfrastructure { get; set; } = 12;

            public int StarfighterRequirementHeadquarters { get; set; }

            public int UnthreatenedInfrastructureStarfighterBaselinePercent { get; set; } = 50;

            public int InteriorStarfighterBaselinePercent { get; set; } = 100;

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

            public int InteriorCaptureFloorPercent { get; set; } = 100;
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
        /// Victory system parameters.
        /// </summary>
        [PersistableObject]
        public class VictoryConfig { }

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
            public Dictionary<int, int> EscapeTable { get; set; } = new Dictionary<int, int>();

            public int EscapeLoyaltyShift { get; set; }
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
