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

        public GameSpeedConfig GameSpeed { get; set; } = new GameSpeedConfig();

        public MessageConfig Messages { get; set; } = new MessageConfig();

        public EspionageConfig Espionage { get; set; } = new EspionageConfig();

        public ProbabilityTablesConfig ProbabilityTables { get; set; } =
            new ProbabilityTablesConfig();

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public GameConfig CreateCopy() =>
            new GameConfig
            {
                DifficultyModifiers = DifficultyModifiers?.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value?.CreateCopy()
                ),
                AI = AI?.CreateCopy(),
                Movement = Movement?.CreateCopy(),
                Production = Production?.CreateCopy(),
                Smuggling = Smuggling?.CreateCopy(),
                Planet = Planet?.CreateCopy(),
                Combat = Combat?.CreateCopy(),
                Uprising = Uprising?.CreateCopy(),
                SupportShift = SupportShift?.CreateCopy(),
                Blockade = Blockade?.CreateCopy(),
                Jedi = Jedi?.CreateCopy(),
                DuelResolution = DuelResolution?.CreateCopy(),
                Research = Research?.CreateCopy(),
                Assassination = Assassination?.CreateCopy(),
                Recovery = Recovery?.CreateCopy(),
                Captive = Captive?.CreateCopy(),
                GameSpeed = GameSpeed?.CreateCopy(),
                Messages = Messages?.CreateCopy(),
                Espionage = Espionage?.CreateCopy(),
                ProbabilityTables = ProbabilityTables?.CreateCopy(),
            };

        /// <summary>
        /// AI decision-making and mission dispatch.
        /// </summary>
        [PersistableObject]
        public class AIConfig
        {
            public int TickInterval { get; set; }

            public Dictionary<GameDifficulty, int> DecisionTickIntervals { get; set; } = new();

            public bool EnablePlanetaryAssaults { get; set; }

            public int DiplomacyMinimumSkill { get; set; }

            public int RecruitmentMinimumLeadership { get; set; }

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

            /// <summary>
            /// Returns the number of AI updates between strategic decisions for one difficulty.
            /// </summary>
            /// <param name="difficulty">The selected difficulty.</param>
            /// <returns>A positive interval, defaulting to every update when unspecified.</returns>
            public int GetDecisionTickInterval(GameDifficulty difficulty)
            {
                if (
                    DecisionTickIntervals != null
                    && DecisionTickIntervals.TryGetValue(difficulty, out int interval)
                )
                    return System.Math.Max(1, interval);
                return 1;
            }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIConfig CreateCopy() =>
                new AIConfig
                {
                    TickInterval = TickInterval,
                    DecisionTickIntervals = DecisionTickIntervals?.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value
                    ),
                    EnablePlanetaryAssaults = EnablePlanetaryAssaults,
                    DiplomacyMinimumSkill = DiplomacyMinimumSkill,
                    RecruitmentMinimumLeadership = RecruitmentMinimumLeadership,
                    MissionTables = MissionTables?.CreateCopy(),
                    MissionPlanning = MissionPlanning?.CreateCopy(),
                    Garrison = Garrison?.CreateCopy(),
                    DeploymentGateLow = DeploymentGateLow,
                    DeploymentGateHigh = DeploymentGateHigh,
                    Selection = Selection?.CreateCopy(),
                    NonCapitalSummary = NonCapitalSummary?.CreateCopy(),
                    Infrastructure = Infrastructure?.CreateCopy(),
                    FleetDeployment = FleetDeployment?.CreateCopy(),
                };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIResponseCurveConfig CreateCopy() =>
                new AIResponseCurveConfig
                {
                    Shape = Shape,
                    Exponent = Exponent,
                    Midpoint = Midpoint,
                    Steepness = Steepness,
                };
        }

        /// <summary>
        /// Configuration for one normalized and weighted AI consideration.
        /// </summary>
        [PersistableObject]
        public class AIConsiderationConfig
        {
            public double Weight { get; set; }

            public double SaturationValue { get; set; } = 1;

            public AIResponseCurveConfig Curve { get; set; } = new AIResponseCurveConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIConsiderationConfig CreateCopy() =>
                new AIConsiderationConfig
                {
                    Weight = Weight,
                    SaturationValue = SaturationValue,
                    Curve = Curve?.CreateCopy(),
                };
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

            public int MinimumSabotageSuccessPercent { get; set; }

            public int MaximumSabotageTeamSize { get; set; }

            public int MaximumSabotageMainParticipants { get; set; }

            public int MaximumSabotageDecoyParticipants { get; set; }

            public int SabotageTeamCandidateCount { get; set; }

            public int SabotageTeamAlternativesPerTarget { get; set; }

            public int MaximumOfficerMissionLossProbability { get; set; }

            public int EncounterCacheCapacity { get; set; }

            public int MissionDefenseIntelFullWeightTicks { get; set; }

            public int MissionDefenseFallbackGroundDetectorCount { get; set; }

            public int MissionDefenseFallbackOrbitalDetectorCount { get; set; }

            public int MissionDefenseFallbackDetectionRating { get; set; }

            public int MissionDefenseFallbackCommanderEspionage { get; set; }

            public int MissionDefenseFallbackCommanderCombat { get; set; }

            public int HostileMissionIntelAgeFoilPenaltyPerRefreshInterval { get; set; }

            public int MaximumUnprotectedOfficerMissionFoilProbability { get; set; }

            public AIMissionUtilityConfig Utility { get; set; } = new AIMissionUtilityConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIMissionPlanningConfig CreateCopy() =>
                new AIMissionPlanningConfig
                {
                    RetainedAlternativesPerMission = RetainedAlternativesPerMission,
                    EspionageRefreshIntervalTicks = EspionageRefreshIntervalTicks,
                    HostileMissionMaximumIntelAgeTicks = HostileMissionMaximumIntelAgeTicks,
                    MaximumJediTrainingStudents = MaximumJediTrainingStudents,
                    MinimumMissionScore = MinimumMissionScore,
                    MinimumUprisingMissionSuccessPercent = MinimumUprisingMissionSuccessPercent,
                    MinimumSabotageSuccessPercent = MinimumSabotageSuccessPercent,
                    MaximumSabotageTeamSize = MaximumSabotageTeamSize,
                    MaximumSabotageMainParticipants = MaximumSabotageMainParticipants,
                    MaximumSabotageDecoyParticipants = MaximumSabotageDecoyParticipants,
                    SabotageTeamCandidateCount = SabotageTeamCandidateCount,
                    SabotageTeamAlternativesPerTarget = SabotageTeamAlternativesPerTarget,
                    MaximumOfficerMissionLossProbability = MaximumOfficerMissionLossProbability,
                    EncounterCacheCapacity = EncounterCacheCapacity,
                    MissionDefenseIntelFullWeightTicks = MissionDefenseIntelFullWeightTicks,
                    MissionDefenseFallbackGroundDetectorCount =
                        MissionDefenseFallbackGroundDetectorCount,
                    MissionDefenseFallbackOrbitalDetectorCount =
                        MissionDefenseFallbackOrbitalDetectorCount,
                    MissionDefenseFallbackDetectionRating = MissionDefenseFallbackDetectionRating,
                    MissionDefenseFallbackCommanderEspionage =
                        MissionDefenseFallbackCommanderEspionage,
                    MissionDefenseFallbackCommanderCombat = MissionDefenseFallbackCommanderCombat,
                    HostileMissionIntelAgeFoilPenaltyPerRefreshInterval =
                        HostileMissionIntelAgeFoilPenaltyPerRefreshInterval,
                    MaximumUnprotectedOfficerMissionFoilProbability =
                        MaximumUnprotectedOfficerMissionFoilProbability,
                    Utility = Utility?.CreateCopy(),
                };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIMissionUtilityConfig CreateCopy() =>
                new AIMissionUtilityConfig
                {
                    Objective = Objective?.CreateCopy(),
                    Priority = Priority?.CreateCopy(),
                    Sabotage = Sabotage?.CreateCopy(),
                    Diplomacy = Diplomacy?.CreateCopy(),
                    OfficerTarget = OfficerTarget?.CreateCopy(),
                };
        }

        /// <summary>
        /// General mission value and risk considerations.
        /// </summary>
        [PersistableObject]
        public class AIMissionObjectiveUtilityConfig
        {
            public AIConsiderationConfig Success { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig FoilRisk { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig TravelCost { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig TravelTime { get; set; }

            public AIConsiderationConfig ExecutionDelay { get; set; }

            public AIConsiderationConfig ParticipantTravelTime { get; set; }

            public AIConsiderationConfig OfficerRisk { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig IntelAge { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig AttackPreparationIntel { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig BlockedAttackIntel { get; set; }

            public AIConsiderationConfig TrainingValue { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIMissionObjectiveUtilityConfig CreateCopy() =>
                new AIMissionObjectiveUtilityConfig
                {
                    Success = Success?.CreateCopy(),
                    FoilRisk = FoilRisk?.CreateCopy(),
                    TravelCost = TravelCost?.CreateCopy(),
                    TravelTime = TravelTime?.CreateCopy(),
                    ExecutionDelay = ExecutionDelay?.CreateCopy(),
                    ParticipantTravelTime = ParticipantTravelTime?.CreateCopy(),
                    OfficerRisk = OfficerRisk?.CreateCopy(),
                    IntelAge = IntelAge?.CreateCopy(),
                    AttackPreparationIntel = AttackPreparationIntel?.CreateCopy(),
                    BlockedAttackIntel = BlockedAttackIntel?.CreateCopy(),
                    TrainingValue = TrainingValue?.CreateCopy(),
                };
        }

        /// <summary>
        /// Mission-type strategic priority considerations.
        /// </summary>
        [PersistableObject]
        public class AIMissionPriorityUtilityConfig
        {
            public AIConsiderationConfig Reconnaissance { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Recruitment { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Rescue { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig SubdueUprising { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Research { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig JediTraining { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Espionage { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig MobileHeadquartersEspionage { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig ManufacturingEspionage { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig Diplomacy { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIMissionPriorityUtilityConfig CreateCopy() =>
                new AIMissionPriorityUtilityConfig
                {
                    Reconnaissance = Reconnaissance?.CreateCopy(),
                    Recruitment = Recruitment?.CreateCopy(),
                    Rescue = Rescue?.CreateCopy(),
                    SubdueUprising = SubdueUprising?.CreateCopy(),
                    Research = Research?.CreateCopy(),
                    JediTraining = JediTraining?.CreateCopy(),
                    Espionage = Espionage?.CreateCopy(),
                    MobileHeadquartersEspionage = MobileHeadquartersEspionage?.CreateCopy(),
                    ManufacturingEspionage = ManufacturingEspionage?.CreateCopy(),
                    Diplomacy = Diplomacy?.CreateCopy(),
                };
        }

        /// <summary>
        /// Strategic value considerations for sabotage targets.
        /// </summary>
        [PersistableObject]
        public class AISabotageUtilityConfig
        {
            public AIConsiderationConfig Infrastructure { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Defense { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Shield { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig AttackTarget { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig AttackDefense { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig FavoredSupportRegiment { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig GarrisonRegiment { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig GarrisonStarfighter { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig SpecialForces { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig OtherUnit { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AISabotageUtilityConfig CreateCopy() =>
                new AISabotageUtilityConfig
                {
                    Infrastructure = Infrastructure?.CreateCopy(),
                    Defense = Defense?.CreateCopy(),
                    Shield = Shield?.CreateCopy(),
                    AttackTarget = AttackTarget?.CreateCopy(),
                    AttackDefense = AttackDefense?.CreateCopy(),
                    FavoredSupportRegiment = FavoredSupportRegiment?.CreateCopy(),
                    GarrisonRegiment = GarrisonRegiment?.CreateCopy(),
                    GarrisonStarfighter = GarrisonStarfighter?.CreateCopy(),
                    SpecialForces = SpecialForces?.CreateCopy(),
                    OtherUnit = OtherUnit?.CreateCopy(),
                };
        }

        /// <summary>
        /// Strategic value considerations for diplomacy targets.
        /// </summary>
        [PersistableObject]
        public class AIDiplomacyUtilityConfig
        {
            public AIConsiderationConfig Competition { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig TravelTime { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig SupportDeficit { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig CoreWorld { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig HeadquartersSector { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig SystemPresence { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Concentration { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig IndustrialOpportunity { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig ConstructionFacility { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig Shipyard { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig TrainingFacility { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig ResourceNode { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig SectorSupportRisk { get; set; } =
                new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIDiplomacyUtilityConfig CreateCopy() =>
                new AIDiplomacyUtilityConfig
                {
                    Competition = Competition?.CreateCopy(),
                    TravelTime = TravelTime?.CreateCopy(),
                    SupportDeficit = SupportDeficit?.CreateCopy(),
                    CoreWorld = CoreWorld?.CreateCopy(),
                    HeadquartersSector = HeadquartersSector?.CreateCopy(),
                    SystemPresence = SystemPresence?.CreateCopy(),
                    Concentration = Concentration?.CreateCopy(),
                    IndustrialOpportunity = IndustrialOpportunity?.CreateCopy(),
                    ConstructionFacility = ConstructionFacility?.CreateCopy(),
                    Shipyard = Shipyard?.CreateCopy(),
                    TrainingFacility = TrainingFacility?.CreateCopy(),
                    ResourceNode = ResourceNode?.CreateCopy(),
                    SectorSupportRisk = SectorSupportRisk?.CreateCopy(),
                };
        }

        /// <summary>
        /// Strategic value considerations for hostile officer targets.
        /// </summary>
        [PersistableObject]
        public class AIOfficerTargetUtilityConfig
        {
            public AIConsiderationConfig Combat { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig Espionage { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig Diplomacy { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig Leadership { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig ShipResearch { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig FacilityResearch { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig TroopResearch { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIOfficerTargetUtilityConfig CreateCopy() =>
                new AIOfficerTargetUtilityConfig
                {
                    Combat = Combat?.CreateCopy(),
                    Espionage = Espionage?.CreateCopy(),
                    Diplomacy = Diplomacy?.CreateCopy(),
                    Leadership = Leadership?.CreateCopy(),
                    ShipResearch = ShipResearch?.CreateCopy(),
                    FacilityResearch = FacilityResearch?.CreateCopy(),
                    TroopResearch = TroopResearch?.CreateCopy(),
                };
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

            public int LocalAttackMinimumStrengthPercent { get; set; }

            public int LocalAttackMaximumIntelAgeTicks { get; set; }

            public int LocalAttackMaximumTravelTicks { get; set; }

            public int SupportOpportunityMinimumAttackStrengthPercent { get; set; }

            public int SupportOpportunityMaximumIntelAgeTicks { get; set; }

            public int SupportOpportunityMaximumTravelTicks { get; set; }

            public int MinimumMobileCombatStrength { get; set; }

            public int MobileCombatStrengthPerPlanet { get; set; }

            public int MinimumDefenseStrength { get; set; }

            public int HeadquartersDefenseCombatPercent { get; set; }

            public int HeadquartersDefensePercentOfStrongestHostileFleet { get; set; }

            public int MinimumPlanetaryAssaultRegimentCount { get; set; }

            public int MinimumPlanetaryAssaultSuccessPercent { get; set; }

            public int AttackStrengthPercentOfDefense { get; set; }

            public int AttackStrengthPercentOfStrongestHostileFleet { get; set; }

            public int AssaultFleetCompositionReservePercent { get; set; }

            public int StaleIntelMaximumAttackStrengthPercent { get; set; }

            public int StaleIntelReserveSaturationIntervals { get; set; }

            public int AttackIntelAgeGraceTicks { get; set; }

            public int HeadquartersAttackMaximumIntelAgeTicks { get; set; }

            public double AttackReadinessFloorWeight { get; set; }

            public AIAttackUtilityConfig AttackUtility { get; set; } = new AIAttackUtilityConfig();

            public AIOfficerCommandUtilityConfig OfficerCommandUtility { get; set; } =
                new AIOfficerCommandUtilityConfig();

            public int ExposedSectorMinimumOwnedPresencePercent { get; set; }

            public AIDefenseUtilityConfig DefenseUtility { get; set; } =
                new AIDefenseUtilityConfig();

            public AIDefenseAllocationUtilityConfig DefenseAllocationUtility { get; set; } =
                new AIDefenseAllocationUtilityConfig();

            public AIColonizationUtilityConfig ColonizationUtility { get; set; } =
                new AIColonizationUtilityConfig();

            public AIColonizationTargetUtilityConfig ColonizationTargetUtility { get; set; } =
                new AIColonizationTargetUtilityConfig();

            public int ColonizationFleetTargetCount { get; set; }

            public int ColonizationFleetMinimumRegimentCount { get; set; }

            public int ColonizationFleetMaximumRegimentCount { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIFleetDeploymentConfig CreateCopy() =>
                new AIFleetDeploymentConfig
                {
                    MinimumBattleFleetCount = MinimumBattleFleetCount,
                    PlanetsPerBattleFleet = PlanetsPerBattleFleet,
                    MinimumAttackStrength = MinimumAttackStrength,
                    LocalAttackMinimumStrengthPercent = LocalAttackMinimumStrengthPercent,
                    LocalAttackMaximumIntelAgeTicks = LocalAttackMaximumIntelAgeTicks,
                    LocalAttackMaximumTravelTicks = LocalAttackMaximumTravelTicks,
                    SupportOpportunityMinimumAttackStrengthPercent =
                        SupportOpportunityMinimumAttackStrengthPercent,
                    SupportOpportunityMaximumIntelAgeTicks = SupportOpportunityMaximumIntelAgeTicks,
                    SupportOpportunityMaximumTravelTicks = SupportOpportunityMaximumTravelTicks,
                    MinimumMobileCombatStrength = MinimumMobileCombatStrength,
                    MobileCombatStrengthPerPlanet = MobileCombatStrengthPerPlanet,
                    MinimumDefenseStrength = MinimumDefenseStrength,
                    HeadquartersDefenseCombatPercent = HeadquartersDefenseCombatPercent,
                    HeadquartersDefensePercentOfStrongestHostileFleet =
                        HeadquartersDefensePercentOfStrongestHostileFleet,
                    MinimumPlanetaryAssaultRegimentCount = MinimumPlanetaryAssaultRegimentCount,
                    MinimumPlanetaryAssaultSuccessPercent = MinimumPlanetaryAssaultSuccessPercent,
                    AttackStrengthPercentOfDefense = AttackStrengthPercentOfDefense,
                    AttackStrengthPercentOfStrongestHostileFleet =
                        AttackStrengthPercentOfStrongestHostileFleet,
                    AssaultFleetCompositionReservePercent = AssaultFleetCompositionReservePercent,
                    StaleIntelMaximumAttackStrengthPercent = StaleIntelMaximumAttackStrengthPercent,
                    StaleIntelReserveSaturationIntervals = StaleIntelReserveSaturationIntervals,
                    AttackIntelAgeGraceTicks = AttackIntelAgeGraceTicks,
                    HeadquartersAttackMaximumIntelAgeTicks = HeadquartersAttackMaximumIntelAgeTicks,
                    AttackReadinessFloorWeight = AttackReadinessFloorWeight,
                    AttackUtility = AttackUtility?.CreateCopy(),
                    OfficerCommandUtility = OfficerCommandUtility?.CreateCopy(),
                    ExposedSectorMinimumOwnedPresencePercent =
                        ExposedSectorMinimumOwnedPresencePercent,
                    DefenseUtility = DefenseUtility?.CreateCopy(),
                    DefenseAllocationUtility = DefenseAllocationUtility?.CreateCopy(),
                    ColonizationUtility = ColonizationUtility?.CreateCopy(),
                    ColonizationTargetUtility = ColonizationTargetUtility?.CreateCopy(),
                    ColonizationFleetTargetCount = ColonizationFleetTargetCount,
                    ColonizationFleetMinimumRegimentCount = ColonizationFleetMinimumRegimentCount,
                    ColonizationFleetMaximumRegimentCount = ColonizationFleetMaximumRegimentCount,
                };
        }

        /// <summary>
        /// Utility considerations used to allocate officers to attack fleets.
        /// </summary>
        [PersistableObject]
        public class AIOfficerCommandUtilityConfig
        {
            public AIConsiderationConfig ForceImprovement { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig ReadinessGain { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig Ready { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig StrategicValue { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig TravelCost { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIOfficerCommandUtilityConfig CreateCopy() =>
                new AIOfficerCommandUtilityConfig
                {
                    ForceImprovement = ForceImprovement?.CreateCopy(),
                    ReadinessGain = ReadinessGain?.CreateCopy(),
                    Ready = Ready?.CreateCopy(),
                    StrategicValue = StrategicValue?.CreateCopy(),
                    TravelCost = TravelCost?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility considerations used to value fleet attacks and attack-fleet reinforcement.
        /// </summary>
        [PersistableObject]
        public class AIAttackUtilityConfig
        {
            public AIConsiderationConfig TravelTime { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Gets or sets the cost of a longer journey when the fleet has a ready shorter attack.
            /// </summary>
            public AIConsiderationConfig TravelOpportunityCost { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig StrategicValue { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig SectorSupport { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig SystemPresence { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Readiness { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Ready { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig CaptureViability { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig TravelEfficiency { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig ExpectedLossRisk { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig OpportunityCost { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig IntelAgeRisk { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig ExistingOrder { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Headquarters { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig HeadquartersBuildupCost { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig OrbitalAdvantage { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig ExposedBombardment { get; set; } =
                new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIAttackUtilityConfig CreateCopy() =>
                new AIAttackUtilityConfig
                {
                    TravelTime = TravelTime?.CreateCopy(),
                    TravelOpportunityCost = TravelOpportunityCost?.CreateCopy(),
                    StrategicValue = StrategicValue?.CreateCopy(),
                    SectorSupport = SectorSupport?.CreateCopy(),
                    SystemPresence = SystemPresence?.CreateCopy(),
                    Readiness = Readiness?.CreateCopy(),
                    Ready = Ready?.CreateCopy(),
                    CaptureViability = CaptureViability?.CreateCopy(),
                    TravelEfficiency = TravelEfficiency?.CreateCopy(),
                    ExpectedLossRisk = ExpectedLossRisk?.CreateCopy(),
                    OpportunityCost = OpportunityCost?.CreateCopy(),
                    IntelAgeRisk = IntelAgeRisk?.CreateCopy(),
                    ExistingOrder = ExistingOrder?.CreateCopy(),
                    Headquarters = Headquarters?.CreateCopy(),
                    HeadquartersBuildupCost = HeadquartersBuildupCost?.CreateCopy(),
                    OrbitalAdvantage = OrbitalAdvantage?.CreateCopy(),
                    ExposedBombardment = ExposedBombardment?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility considerations used to value colonization fleets and destinations.
        /// </summary>
        [PersistableObject]
        public class AIColonizationUtilityConfig
        {
            public AIConsiderationConfig Base { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig StrategicValue { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig TravelEfficiency { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig AnchorProximity { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig Ready { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig OpportunityCost { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig ExistingOrder { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIColonizationUtilityConfig CreateCopy() =>
                new AIColonizationUtilityConfig
                {
                    Base = Base?.CreateCopy(),
                    StrategicValue = StrategicValue?.CreateCopy(),
                    TravelEfficiency = TravelEfficiency?.CreateCopy(),
                    AnchorProximity = AnchorProximity?.CreateCopy(),
                    Ready = Ready?.CreateCopy(),
                    OpportunityCost = OpportunityCost?.CreateCopy(),
                    ExistingOrder = ExistingOrder?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility considerations used to choose a colony within an assigned system.
        /// </summary>
        [PersistableObject]
        public class AIColonizationTargetUtilityConfig
        {
            public AIConsiderationConfig Energy { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Resources { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIColonizationTargetUtilityConfig CreateCopy() =>
                new AIColonizationTargetUtilityConfig
                {
                    Energy = Energy?.CreateCopy(),
                    Resources = Resources?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility considerations used to value fleet defense.
        /// </summary>
        [PersistableObject]
        public class AIDefenseUtilityConfig
        {
            public AIConsiderationConfig Base { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig SectorRisk { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig TravelTime { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig OpportunityCost { get; set; } =
                new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIDefenseUtilityConfig CreateCopy() =>
                new AIDefenseUtilityConfig
                {
                    Base = Base?.CreateCopy(),
                    SectorRisk = SectorRisk?.CreateCopy(),
                    TravelTime = TravelTime?.CreateCopy(),
                    OpportunityCost = OpportunityCost?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility considerations used to allocate defense fleets among valid assignments.
        /// </summary>
        [PersistableObject]
        public class AIDefenseAllocationUtilityConfig
        {
            public AIConsiderationConfig SectorRisk { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig StrategicValue { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig DefenseNeed { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig TravelEfficiency { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig ForceEfficiency { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig ReinforcementNeed { get; set; } =
                new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIDefenseAllocationUtilityConfig CreateCopy() =>
                new AIDefenseAllocationUtilityConfig
                {
                    SectorRisk = SectorRisk?.CreateCopy(),
                    StrategicValue = StrategicValue?.CreateCopy(),
                    DefenseNeed = DefenseNeed?.CreateCopy(),
                    TravelEfficiency = TravelEfficiency?.CreateCopy(),
                    ForceEfficiency = ForceEfficiency?.CreateCopy(),
                    ReinforcementNeed = ReinforcementNeed?.CreateCopy(),
                };
        }

        /// <summary>
        /// AI manufacturing selection weights and limits.
        /// </summary>
        [PersistableObject]
        public class AISelectionConfig
        {
            public float MinimumSelectableScore { get; set; }

            public AIConsiderationConfig DemandUtility { get; set; } = new AIConsiderationConfig();
            public int PreferredStarfighterTypeCountPerFleet { get; set; }
            public int PreferredRegimentTypeCountPerDestination { get; set; }
            public int PreferredInterdictionCountPerFleet { get; set; }
            public AITechnologySelectionUtilityConfig TechnologyUtility { get; set; } =
                new AITechnologySelectionUtilityConfig();
            public int RefinedMaterialReservePercent { get; set; }
            public int RefinedMaterialEconomyWarningPercent { get; set; }
            public int RefinedMaterialCommitmentHorizonTicks { get; set; }
            public int MaintenanceHeadroomReserve { get; set; }
            public int MaintenanceHeadroomTarget { get; set; }

            public AIMaintenanceRecoveryConfig MaintenanceRecovery { get; set; } =
                new AIMaintenanceRecoveryConfig();

            public int DiplomacyEconomyConfidencePercent { get; set; }
            public AIProductionUtilityConfig ProductionUtility { get; set; } =
                new AIProductionUtilityConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AISelectionConfig CreateCopy() =>
                new AISelectionConfig
                {
                    MinimumSelectableScore = MinimumSelectableScore,
                    DemandUtility = DemandUtility?.CreateCopy(),
                    PreferredStarfighterTypeCountPerFleet = PreferredStarfighterTypeCountPerFleet,
                    PreferredRegimentTypeCountPerDestination =
                        PreferredRegimentTypeCountPerDestination,
                    PreferredInterdictionCountPerFleet = PreferredInterdictionCountPerFleet,
                    TechnologyUtility = TechnologyUtility?.CreateCopy(),
                    RefinedMaterialReservePercent = RefinedMaterialReservePercent,
                    RefinedMaterialEconomyWarningPercent = RefinedMaterialEconomyWarningPercent,
                    RefinedMaterialCommitmentHorizonTicks = RefinedMaterialCommitmentHorizonTicks,
                    MaintenanceHeadroomReserve = MaintenanceHeadroomReserve,
                    MaintenanceHeadroomTarget = MaintenanceHeadroomTarget,
                    MaintenanceRecovery = MaintenanceRecovery?.CreateCopy(),
                    DiplomacyEconomyConfidencePercent = DiplomacyEconomyConfidencePercent,
                    ProductionUtility = ProductionUtility?.CreateCopy(),
                };
        }

        /// <summary>
        /// Controls deliberate retirement of maintenance commitments during a shortage.
        /// </summary>
        [PersistableObject]
        public class AIMaintenanceRecoveryConfig
        {
            public bool Enabled { get; set; }
            public int TargetHeadroom { get; set; }
            public double ReplacementCostWeight { get; set; }
            public double DefenseWeight { get; set; }
            public double AttackWeight { get; set; }
            public double GarrisonWeight { get; set; }
            public double AgentWeight { get; set; }
            public double ProductionWeight { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIMaintenanceRecoveryConfig CreateCopy() =>
                new AIMaintenanceRecoveryConfig
                {
                    Enabled = Enabled,
                    TargetHeadroom = TargetHeadroom,
                    ReplacementCostWeight = ReplacementCostWeight,
                    DefenseWeight = DefenseWeight,
                    AttackWeight = AttackWeight,
                    GarrisonWeight = GarrisonWeight,
                    AgentWeight = AgentWeight,
                    ProductionWeight = ProductionWeight,
                };
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

            public AIConsiderationConfig DuplicateCost { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AITechnologySelectionUtilityConfig CreateCopy() =>
                new AITechnologySelectionUtilityConfig
                {
                    Building = Building?.CreateCopy(),
                    Starfighter = Starfighter?.CreateCopy(),
                    Regiment = Regiment?.CreateCopy(),
                    SpecialForces = SpecialForces?.CreateCopy(),
                    DuplicateCost = DuplicateCost?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility considerations used to choose a building technology.
        /// </summary>
        [PersistableObject]
        public class AIBuildingSelectionUtilityConfig
        {
            public AIConsiderationConfig Capability { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIBuildingSelectionUtilityConfig CreateCopy() =>
                new AIBuildingSelectionUtilityConfig { Capability = Capability?.CreateCopy() };
        }

        /// <summary>
        /// Utility considerations used to choose a starfighter technology.
        /// </summary>
        [PersistableObject]
        public class AIStarfighterSelectionUtilityConfig
        {
            public AIConsiderationConfig Laser { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig Ion { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig Torpedo { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig MissingIon { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig MissingTorpedo { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig PlanetDefenseEfficiency { get; set; } =
                new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIStarfighterSelectionUtilityConfig CreateCopy() =>
                new AIStarfighterSelectionUtilityConfig
                {
                    Laser = Laser?.CreateCopy(),
                    Ion = Ion?.CreateCopy(),
                    Torpedo = Torpedo?.CreateCopy(),
                    MissingIon = MissingIon?.CreateCopy(),
                    MissingTorpedo = MissingTorpedo?.CreateCopy(),
                    PlanetDefenseEfficiency = PlanetDefenseEfficiency?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility considerations used to choose a regiment technology.
        /// </summary>
        [PersistableObject]
        public class AIRegimentSelectionUtilityConfig
        {
            public AIConsiderationConfig Attack { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig Defense { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig BombardmentDefense { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig Base { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig MaintenanceCost { get; set; } =
                new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIRegimentSelectionUtilityConfig CreateCopy() =>
                new AIRegimentSelectionUtilityConfig
                {
                    Attack = Attack?.CreateCopy(),
                    Defense = Defense?.CreateCopy(),
                    BombardmentDefense = BombardmentDefense?.CreateCopy(),
                    Base = Base?.CreateCopy(),
                    MaintenanceCost = MaintenanceCost?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility considerations used to choose a special-forces technology.
        /// </summary>
        [PersistableObject]
        public class AISpecialForcesSelectionUtilityConfig
        {
            public AIConsiderationConfig BuildEfficiency { get; set; } =
                new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AISpecialForcesSelectionUtilityConfig CreateCopy() =>
                new AISpecialForcesSelectionUtilityConfig
                {
                    BuildEfficiency = BuildEfficiency?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility costs applied when ranking production proposals.
        /// </summary>
        [PersistableObject]
        public class AIProductionUtilityConfig
        {
            public AIConsiderationConfig IndustrialDefense { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig FacilityAvailability { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig LocalConstructionGrowth { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig CompletionTime { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig InterruptionCost { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig TravelCost { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig ColonyFoundation { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig HeadquartersReinforcement { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig HeadroomRisk { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig Shortfall { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIProductionUtilityConfig CreateCopy() =>
                new AIProductionUtilityConfig
                {
                    IndustrialDefense = IndustrialDefense?.CreateCopy(),
                    FacilityAvailability = FacilityAvailability?.CreateCopy(),
                    LocalConstructionGrowth = LocalConstructionGrowth?.CreateCopy(),
                    CompletionTime = CompletionTime?.CreateCopy(),
                    InterruptionCost = InterruptionCost?.CreateCopy(),
                    TravelCost = TravelCost?.CreateCopy(),
                    ColonyFoundation = ColonyFoundation?.CreateCopy(),
                    HeadquartersReinforcement = HeadquartersReinforcement?.CreateCopy(),
                    HeadroomRisk = HeadroomRisk?.CreateCopy(),
                    Shortfall = Shortfall?.CreateCopy(),
                };
        }

        /// <summary>
        /// AI infrastructure demand settings.
        /// </summary>
        [PersistableObject]
        public class AIInfrastructureConfig
        {
            public int ConstructionFacilityTargetClearTicks { get; set; }
            public int ShipyardTargetClearTicks { get; set; }
            public int FleetProductionMinimumShipyardCount { get; set; }

            public int CapitalShipMaximumBuildTicks { get; set; }
            public int TrainingFacilityTargetClearTicks { get; set; }
            public int ConstructionFacilityPortfolioPercent { get; set; }
            public int ShipyardPortfolioPercent { get; set; }
            public int TrainingFacilityPortfolioPercent { get; set; }
            public int StaticDefensePortfolioPercent { get; set; }
            public int ManufacturingFacilityBaseDemandPercent { get; set; }
            public int ConstructionFacilityDemandPercent { get; set; }
            public int ShipyardDemandPercent { get; set; }
            public int TrainingFacilityDemandPercent { get; set; }
            public int ConstructionFacilityPrimaryHubTargetCount { get; set; }
            public int ConstructionFacilitySecondaryHubTargetCount { get; set; }
            public int ShipyardPrimaryHubTargetCount { get; set; }
            public int ShipyardSecondaryHubTargetCount { get; set; }
            public int TrainingFacilityHubTargetCount { get; set; }
            public AIInfrastructurePlacementUtilityConfig PlacementUtility { get; set; } =
                new AIInfrastructurePlacementUtilityConfig();
            public AIInfrastructureAllocationUtilityConfig AllocationUtility { get; set; } =
                new AIInfrastructureAllocationUtilityConfig();
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

            public int MobileHeadquartersEspionageDemandPercent { get; set; }

            public int ManufacturingEspionageDemandPercent { get; set; }

            public int ManufacturingInterceptionAgentCount { get; set; }

            public int ManufacturingInterceptionDemandPercent { get; set; }

            public int SpecialForcesMissionCoveragePercent { get; set; }

            public int SaboteursPerEnemyDefense { get; set; }

            public int SpiesPerEnemyPlanet { get; set; }

            public int StarfighterParentFillPercent { get; set; }
            public int StarfighterLocalReservePercent { get; set; }
            public int AssaultRegimentLoadPercent { get; set; }
            public int GarrisonRegimentReservePercent { get; set; }
            public int PlanetaryStarfighterDemandPercent { get; set; }
            public int IdleShipyardFighterReserveCount { get; set; }
            public int IdleShipyardFighterDemandPercent { get; set; }
            public int PlanetaryWeaponTargetCount { get; set; }

            public bool RequireEstablishedIndustryForStaticDefense { get; set; }
            public int PlanetaryDefenseSurplusBatchSize { get; set; }
            public int PlanetaryShieldDemandPercent { get; set; }

            public int IndustrialShieldTargetCount { get; set; }
            public int PlanetaryWeaponDemandPercent { get; set; }
            public int PlanetaryGarrisonDemandPercent { get; set; }
            public int PlanetaryDefenseMaintenanceReservePercent { get; set; }
            public int EconomyDefaultBatchSize { get; set; }
            public int EconomyDemandPercent { get; set; }
            public int EconomySevereDemandPercent { get; set; }
            public int EconomySevereDeficitPercent { get; set; }
            public int EconomyCompetingNeedSlotReserve { get; set; }
            public int FleetFinalReadinessGateUnitCount { get; set; }
            public AIProductionDemandUtilityConfig DemandUtility { get; set; } =
                new AIProductionDemandUtilityConfig();
            public AIFleetProductionAllocationUtilityConfig FleetAllocationUtility { get; set; } =
                new AIFleetProductionAllocationUtilityConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIInfrastructureConfig CreateCopy() =>
                new AIInfrastructureConfig
                {
                    ConstructionFacilityTargetClearTicks = ConstructionFacilityTargetClearTicks,
                    ShipyardTargetClearTicks = ShipyardTargetClearTicks,
                    FleetProductionMinimumShipyardCount = FleetProductionMinimumShipyardCount,
                    CapitalShipMaximumBuildTicks = CapitalShipMaximumBuildTicks,
                    TrainingFacilityTargetClearTicks = TrainingFacilityTargetClearTicks,
                    ConstructionFacilityPortfolioPercent = ConstructionFacilityPortfolioPercent,
                    ShipyardPortfolioPercent = ShipyardPortfolioPercent,
                    TrainingFacilityPortfolioPercent = TrainingFacilityPortfolioPercent,
                    StaticDefensePortfolioPercent = StaticDefensePortfolioPercent,
                    ManufacturingFacilityBaseDemandPercent = ManufacturingFacilityBaseDemandPercent,
                    ConstructionFacilityDemandPercent = ConstructionFacilityDemandPercent,
                    ShipyardDemandPercent = ShipyardDemandPercent,
                    TrainingFacilityDemandPercent = TrainingFacilityDemandPercent,
                    ConstructionFacilityPrimaryHubTargetCount =
                        ConstructionFacilityPrimaryHubTargetCount,
                    ConstructionFacilitySecondaryHubTargetCount =
                        ConstructionFacilitySecondaryHubTargetCount,
                    ShipyardPrimaryHubTargetCount = ShipyardPrimaryHubTargetCount,
                    ShipyardSecondaryHubTargetCount = ShipyardSecondaryHubTargetCount,
                    TrainingFacilityHubTargetCount = TrainingFacilityHubTargetCount,
                    PlacementUtility = PlacementUtility?.CreateCopy(),
                    AllocationUtility = AllocationUtility?.CreateCopy(),
                    ProductionFacilityMaintenanceAllocationPercent =
                        ProductionFacilityMaintenanceAllocationPercent,
                    ProductionFacilityInvestmentHorizonTicks =
                        ProductionFacilityInvestmentHorizonTicks,
                    FacilityConstructionLaneReserve = FacilityConstructionLaneReserve,
                    ProductionQueueTargetPlanningIntervals = ProductionQueueTargetPlanningIntervals,
                    ProductionFacilityUpgradeMinimumRemainingCount =
                        ProductionFacilityUpgradeMinimumRemainingCount,
                    ProductionFacilityUpgradeDemandPercent = ProductionFacilityUpgradeDemandPercent,
                    FleetCapitalShipDemandPercent = FleetCapitalShipDemandPercent,
                    FleetStarfighterDemandPercent = FleetStarfighterDemandPercent,
                    FleetRegimentDemandPercent = FleetRegimentDemandPercent,
                    FleetSeedCapitalShipDemandPercent = FleetSeedCapitalShipDemandPercent,
                    ColonizationFleetDemandPercent = ColonizationFleetDemandPercent,
                    SpecialForcesDemandPercent = SpecialForcesDemandPercent,
                    MobileHeadquartersEspionageDemandPercent =
                        MobileHeadquartersEspionageDemandPercent,
                    ManufacturingEspionageDemandPercent = ManufacturingEspionageDemandPercent,
                    ManufacturingInterceptionAgentCount = ManufacturingInterceptionAgentCount,
                    ManufacturingInterceptionDemandPercent = ManufacturingInterceptionDemandPercent,
                    SpecialForcesMissionCoveragePercent = SpecialForcesMissionCoveragePercent,
                    SaboteursPerEnemyDefense = SaboteursPerEnemyDefense,
                    SpiesPerEnemyPlanet = SpiesPerEnemyPlanet,
                    StarfighterParentFillPercent = StarfighterParentFillPercent,
                    StarfighterLocalReservePercent = StarfighterLocalReservePercent,
                    AssaultRegimentLoadPercent = AssaultRegimentLoadPercent,
                    GarrisonRegimentReservePercent = GarrisonRegimentReservePercent,
                    PlanetaryStarfighterDemandPercent = PlanetaryStarfighterDemandPercent,
                    IdleShipyardFighterReserveCount = IdleShipyardFighterReserveCount,
                    IdleShipyardFighterDemandPercent = IdleShipyardFighterDemandPercent,
                    PlanetaryWeaponTargetCount = PlanetaryWeaponTargetCount,
                    RequireEstablishedIndustryForStaticDefense =
                        RequireEstablishedIndustryForStaticDefense,
                    PlanetaryDefenseSurplusBatchSize = PlanetaryDefenseSurplusBatchSize,
                    PlanetaryShieldDemandPercent = PlanetaryShieldDemandPercent,
                    IndustrialShieldTargetCount = IndustrialShieldTargetCount,
                    PlanetaryWeaponDemandPercent = PlanetaryWeaponDemandPercent,
                    PlanetaryGarrisonDemandPercent = PlanetaryGarrisonDemandPercent,
                    PlanetaryDefenseMaintenanceReservePercent =
                        PlanetaryDefenseMaintenanceReservePercent,
                    EconomyDefaultBatchSize = EconomyDefaultBatchSize,
                    EconomyDemandPercent = EconomyDemandPercent,
                    EconomySevereDemandPercent = EconomySevereDemandPercent,
                    EconomySevereDeficitPercent = EconomySevereDeficitPercent,
                    EconomyCompetingNeedSlotReserve = EconomyCompetingNeedSlotReserve,
                    FleetFinalReadinessGateUnitCount = FleetFinalReadinessGateUnitCount,
                    DemandUtility = DemandUtility?.CreateCopy(),
                    FleetAllocationUtility = FleetAllocationUtility?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility considerations used to route production among eligible fleets.
        /// </summary>
        [PersistableObject]
        public class AIFleetProductionAllocationUtilityConfig
        {
            public AIConsiderationConfig AttackReadiness { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig AttackRequirements { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig SystemPresence { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig Headquarters { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig TargetValue { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig ColonyRegiments { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig ColonyCapacity { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig AssemblyWeakness { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig AssemblyCapacityNeed { get; set; } =
                new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIFleetProductionAllocationUtilityConfig CreateCopy() =>
                new AIFleetProductionAllocationUtilityConfig
                {
                    AttackReadiness = AttackReadiness?.CreateCopy(),
                    AttackRequirements = AttackRequirements?.CreateCopy(),
                    SystemPresence = SystemPresence?.CreateCopy(),
                    Headquarters = Headquarters?.CreateCopy(),
                    TargetValue = TargetValue?.CreateCopy(),
                    ColonyRegiments = ColonyRegiments?.CreateCopy(),
                    ColonyCapacity = ColonyCapacity?.CreateCopy(),
                    AssemblyWeakness = AssemblyWeakness?.CreateCopy(),
                    AssemblyCapacityNeed = AssemblyCapacityNeed?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility considerations that determine relative production-demand pressure.
        /// </summary>
        [PersistableObject]
        public class AIProductionDemandUtilityConfig
        {
            public AIConsiderationConfig Deficit { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig TrainingBacklog { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig SectorCoverage { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig PrimaryHub { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig FacilityBalance { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig FacilityInvestment { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig ColonyFoundation { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig FacilityPortfolio { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig UpgradeValue { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig UpgradeHeadquarters { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig ResourceShortage { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig MaintenanceShortfall { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig MaintenanceReserve { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig DefenseDeficit { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig DefenseValue { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig DefenseHeadquarters { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig DefenseThreat { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig ShieldSupport { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig ShieldSectorRisk { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig FleetTargetValue { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig AttackReinforcement { get; set; } =
                new AIConsiderationConfig();
            public AIConsiderationConfig FleetReadiness { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig FinalReadiness { get; set; } = new AIConsiderationConfig();
            public AIConsiderationConfig StarfighterFill { get; set; } =
                new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIProductionDemandUtilityConfig CreateCopy() =>
                new AIProductionDemandUtilityConfig
                {
                    Deficit = Deficit?.CreateCopy(),
                    TrainingBacklog = TrainingBacklog?.CreateCopy(),
                    SectorCoverage = SectorCoverage?.CreateCopy(),
                    PrimaryHub = PrimaryHub?.CreateCopy(),
                    FacilityBalance = FacilityBalance?.CreateCopy(),
                    FacilityInvestment = FacilityInvestment?.CreateCopy(),
                    ColonyFoundation = ColonyFoundation?.CreateCopy(),
                    FacilityPortfolio = FacilityPortfolio?.CreateCopy(),
                    UpgradeValue = UpgradeValue?.CreateCopy(),
                    UpgradeHeadquarters = UpgradeHeadquarters?.CreateCopy(),
                    ResourceShortage = ResourceShortage?.CreateCopy(),
                    MaintenanceShortfall = MaintenanceShortfall?.CreateCopy(),
                    MaintenanceReserve = MaintenanceReserve?.CreateCopy(),
                    DefenseDeficit = DefenseDeficit?.CreateCopy(),
                    DefenseValue = DefenseValue?.CreateCopy(),
                    DefenseHeadquarters = DefenseHeadquarters?.CreateCopy(),
                    DefenseThreat = DefenseThreat?.CreateCopy(),
                    ShieldSupport = ShieldSupport?.CreateCopy(),
                    ShieldSectorRisk = ShieldSectorRisk?.CreateCopy(),
                    FleetTargetValue = FleetTargetValue?.CreateCopy(),
                    AttackReinforcement = AttackReinforcement?.CreateCopy(),
                    FleetReadiness = FleetReadiness?.CreateCopy(),
                    FinalReadiness = FinalReadiness?.CreateCopy(),
                    StarfighterFill = StarfighterFill?.CreateCopy(),
                };
        }

        /// <summary>
        /// Utility considerations used to rank production-facility destinations.
        /// </summary>
        [PersistableObject]
        public class AIInfrastructurePlacementUtilityConfig
        {
            public AIConsiderationConfig SystemCoverage { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig ExistingHub { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig ConstructionHub { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig SecondTrainingFacility { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig AvailableEnergy { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig PlanetValue { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig SystemSecurity { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig DemandProximity { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig ResourceOpportunityCost { get; set; } =
                new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIInfrastructurePlacementUtilityConfig CreateCopy() =>
                new AIInfrastructurePlacementUtilityConfig
                {
                    SystemCoverage = SystemCoverage?.CreateCopy(),
                    ExistingHub = ExistingHub?.CreateCopy(),
                    ConstructionHub = ConstructionHub?.CreateCopy(),
                    SecondTrainingFacility = SecondTrainingFacility?.CreateCopy(),
                    AvailableEnergy = AvailableEnergy?.CreateCopy(),
                    PlanetValue = PlanetValue?.CreateCopy(),
                    SystemSecurity = SystemSecurity?.CreateCopy(),
                    DemandProximity = DemandProximity?.CreateCopy(),
                    ResourceOpportunityCost = ResourceOpportunityCost?.CreateCopy(),
                };
        }

        /// <summary>
        /// Legacy serialized hub-allocation tuning retained for configuration compatibility.
        /// Infrastructure planning no longer consumes these values.
        /// </summary>
        [PersistableObject]
        public class AIInfrastructureAllocationUtilityConfig
        {
            public AIConsiderationConfig ExistingFacilities { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig UnassignedHub { get; set; } = new AIConsiderationConfig();

            public AIConsiderationConfig FeasibleCapacity { get; set; } =
                new AIConsiderationConfig();

            public AIConsiderationConfig StrategicValue { get; set; } = new AIConsiderationConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIInfrastructureAllocationUtilityConfig CreateCopy() =>
                new AIInfrastructureAllocationUtilityConfig
                {
                    ExistingFacilities = ExistingFacilities?.CreateCopy(),
                    UnassignedHub = UnassignedHub?.CreateCopy(),
                    FeasibleCapacity = FeasibleCapacity?.CreateCopy(),
                    StrategicValue = StrategicValue?.CreateCopy(),
                };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AINonCapitalSummaryConfig CreateCopy() =>
                new AINonCapitalSummaryConfig
                {
                    MajorThreatWeaponDefenseCapacity = MajorThreatWeaponDefenseCapacity,
                    MajorThreatShieldDefenseCapacity = MajorThreatShieldDefenseCapacity,
                    StrategicThreatShieldDefenseCapacity = StrategicThreatShieldDefenseCapacity,
                    StrategicThreatWeaponDefenseCapacity = StrategicThreatWeaponDefenseCapacity,
                    MinimumShieldDefenseCapacity = MinimumShieldDefenseCapacity,
                    SupportThreatWeaponDefenseCapacity = SupportThreatWeaponDefenseCapacity,
                    SubtypeSelectorRatioThreshold = SubtypeSelectorRatioThreshold,
                    SupportThreshold = SupportThreshold,
                    SupportDivisor = SupportDivisor,
                    SupportUrgencyCap = SupportUrgencyCap,
                    BaseRequirementDefault = BaseRequirementDefault,
                    BaseRequirementInfrastructure = BaseRequirementInfrastructure,
                    BaseRequirementHeadquarters = BaseRequirementHeadquarters,
                    StarfighterRequirementDefault = StarfighterRequirementDefault,
                    StarfighterRequirementInfrastructure = StarfighterRequirementInfrastructure,
                    StarfighterRequirementHeadquarters = StarfighterRequirementHeadquarters,
                    UnthreatenedInfrastructureStarfighterBaselinePercent =
                        UnthreatenedInfrastructureStarfighterBaselinePercent,
                    InteriorStarfighterBaselinePercent = InteriorStarfighterBaselinePercent,
                    RequireStaticDefenseBeforeStarfighters = RequireStaticDefenseBeforeStarfighters,
                };
        }

        /// <summary>
        /// Garrison troop requirements based on popular support.
        /// </summary>
        [PersistableObject]
        public class GarrisonConfig
        {
            public int AutomatedOrderQuantity { get; set; }

            public int MinimumGarrisonTarget { get; set; }

            public int ResourceFacilitiesPerGarrison { get; set; }

            public int TieBreakRollMinimum { get; set; }

            public int TieBreakRollMaximum { get; set; }

            public int TieBreakReplacementThreshold { get; set; }

            public int SupportThreshold { get; set; }

            public int GarrisonDivisor { get; set; }

            public int UprisingMultiplier { get; set; }

            public int FleetLoadingDeficitThreshold { get; set; }

            public int InteriorCaptureFloorPercent { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public GarrisonConfig CreateCopy() =>
                new GarrisonConfig
                {
                    AutomatedOrderQuantity = AutomatedOrderQuantity,
                    MinimumGarrisonTarget = MinimumGarrisonTarget,
                    ResourceFacilitiesPerGarrison = ResourceFacilitiesPerGarrison,
                    TieBreakRollMinimum = TieBreakRollMinimum,
                    TieBreakRollMaximum = TieBreakRollMaximum,
                    TieBreakReplacementThreshold = TieBreakReplacementThreshold,
                    SupportThreshold = SupportThreshold,
                    GarrisonDivisor = GarrisonDivisor,
                    UprisingMultiplier = UprisingMultiplier,
                    FleetLoadingDeficitThreshold = FleetLoadingDeficitThreshold,
                    InteriorCaptureFloorPercent = InteriorCaptureFloorPercent,
                };
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

            /// <summary>
            /// Looks up the consequence at the highest satisfied threshold.
            /// </summary>
            /// <param name="table">The configured consequence table.</param>
            /// <param name="score">The incident score.</param>
            /// <returns>The matching consequence, or zero below the first threshold.</returns>
            internal static int GetConsequence(Dictionary<int, int> table, int score)
            {
                int consequence = 0;
                int matchedThreshold = int.MinValue;
                bool found = false;
                foreach (KeyValuePair<int, int> entry in table)
                {
                    if (entry.Key <= score && (!found || entry.Key > matchedThreshold))
                    {
                        matchedThreshold = entry.Key;
                        consequence = entry.Value;
                        found = true;
                    }
                }
                return consequence;
            }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public UprisingConfig CreateCopy() =>
                new UprisingConfig
                {
                    DiceRange = DiceRange,
                    DiceAddend = DiceAddend,
                    MissionLeadershipDivisor = MissionLeadershipDivisor,
                    InciteMissionSupportShift = InciteMissionSupportShift,
                    SubdueOwnedSupportBase = SubdueOwnedSupportBase,
                    SubdueOwnedSupportRange = SubdueOwnedSupportRange,
                    SubdueNeutralSupportBase = SubdueNeutralSupportBase,
                    SubdueNeutralSupportRange = SubdueNeutralSupportRange,
                    PrimaryConsequenceTable =
                        PrimaryConsequenceTable == null
                            ? null
                            : new Dictionary<int, int>(PrimaryConsequenceTable),
                    SecondaryConsequenceTable =
                        SecondaryConsequenceTable == null
                            ? null
                            : new Dictionary<int, int>(SecondaryConsequenceTable),
                    ControllerSupportShift = ControllerSupportShift,
                    ActiveSupportDriftMinTicks = ActiveSupportDriftMinTicks,
                    ActiveSupportDriftMaxTicks = ActiveSupportDriftMaxTicks,
                    IncidentPulseMinTicks = IncidentPulseMinTicks,
                    IncidentPulseMaxTicks = IncidentPulseMaxTicks,
                    ClearUprisingMinTicks = ClearUprisingMinTicks,
                    ClearUprisingMaxTicks = ClearUprisingMaxTicks,
                };
        }

        /// <summary>
        /// Popular support shifts and ownership-transfer settings.
        /// </summary>
        [PersistableObject]
        public class SupportShiftConfig
        {
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public SupportShiftConfig CreateCopy() =>
                new SupportShiftConfig
                {
                    OwnershipTransferThreshold = OwnershipTransferThreshold,
                    WeakSupportPenaltyDivisor = WeakSupportPenaltyDivisor,
                    GarrisonRemovalSupportShift = GarrisonRemovalSupportShift,
                    ControlChangeSupportShift = ControlChangeSupportShift,
                    BlockadeMatchShift = BlockadeMatchShift,
                    BlockadeOpposeShift = BlockadeOpposeShift,
                    BlockadeMatchShiftIntervalTicks = BlockadeMatchShiftIntervalTicks,
                    BlockadeOpposeShiftIntervalTicks = BlockadeOpposeShiftIntervalTicks,
                    DiplomacyOwnedPlanetSupportBase = DiplomacyOwnedPlanetSupportBase,
                    DiplomacyOwnedPlanetSupportRange = DiplomacyOwnedPlanetSupportRange,
                    DiplomacyNeutralPlanetSupportBase = DiplomacyNeutralPlanetSupportBase,
                    DiplomacyNeutralPlanetSupportRange = DiplomacyNeutralPlanetSupportRange,
                };
        }

        /// <summary>
        /// Fleet transit time and hyperdrive parameters.
        /// </summary>
        [PersistableObject]
        public class MovementConfig
        {
            public double DistanceDivisor { get; set; }

            public int MinTransitTicks { get; set; }

            public int SameSectorMinTransitTicks { get; set; }

            public int DefaultFighterHyperdrive { get; set; }

            public int DefaultPersonnelHyperdrive { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public MovementConfig CreateCopy() =>
                new MovementConfig
                {
                    DistanceDivisor = DistanceDivisor,
                    MinTransitTicks = MinTransitTicks,
                    SameSectorMinTransitTicks = SameSectorMinTransitTicks,
                    DefaultFighterHyperdrive = DefaultFighterHyperdrive,
                    DefaultPersonnelHyperdrive = DefaultPersonnelHyperdrive,
                };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public ProductionConfig CreateCopy() =>
                new ProductionConfig
                {
                    MaintenanceShortfallAutoscrapInterval = MaintenanceShortfallAutoscrapInterval,
                    ScrapRefundDivisor = ScrapRefundDivisor,
                    ResourceMaintenanceLoadPercent = ResourceMaintenanceLoadPercent,
                    ResourceCollectionBasePercent = ResourceCollectionBasePercent,
                    ResourceStartupBasePercent = ResourceStartupBasePercent,
                    ResourceStartupRandomPercent = ResourceStartupRandomPercent,
                };
        }

        /// <summary>
        /// System smuggling calculation and resource-redirection rules.
        /// </summary>
        [PersistableObject]
        public class SmugglingConfig
        {
            public Dictionary<int, int> LossPercentByMinimumSupport { get; set; } =
                new Dictionary<int, int>();

            public int CapitalShipSuppression { get; set; }

            public int StarfighterSuppression { get; set; }

            public int RegimentSuppression { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public SmugglingConfig CreateCopy() =>
                new SmugglingConfig
                {
                    LossPercentByMinimumSupport =
                        LossPercentByMinimumSupport == null
                            ? null
                            : new Dictionary<int, int>(LossPercentByMinimumSupport),
                    CapitalShipSuppression = CapitalShipSuppression,
                    StarfighterSuppression = StarfighterSuppression,
                    RegimentSuppression = RegimentSuppression,
                };
        }

        /// <summary>
        /// Per-planet generation limits.
        /// </summary>
        [PersistableObject]
        public class PlanetConfig
        {
            public int MaxEnergy { get; set; }

            public int MaxRawMaterials { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public PlanetConfig CreateCopy() =>
                new PlanetConfig { MaxEnergy = MaxEnergy, MaxRawMaterials = MaxRawMaterials };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public CombatConfig CreateCopy() =>
                new CombatConfig
                {
                    Bombardment = Bombardment?.CreateCopy(),
                    PlanetaryAssault = PlanetaryAssault?.CreateCopy(),
                    SpaceCombat = SpaceCombat?.CreateCopy(),
                };
        }

        /// <summary>
        /// Orbital bombardment resolution parameters.
        /// </summary>
        [PersistableObject]
        public class BombardmentConfig
        {
            public int AttackerLeadershipDivisor { get; set; }
            public int DefenderLeadershipDivisor { get; set; }
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public BombardmentConfig CreateCopy() =>
                new BombardmentConfig
                {
                    AttackerLeadershipDivisor = AttackerLeadershipDivisor,
                    DefenderLeadershipDivisor = DefenderLeadershipDivisor,
                    StrikeRollMinimum = StrikeRollMinimum,
                    StrikeRollMaximum = StrikeRollMaximum,
                    EnergyResistance = EnergyResistance,
                    AllocatedEnergyResistance = AllocatedEnergyResistance,
                    HeadquartersResistance = HeadquartersResistance,
                    CivilianSupportPenalty = CivilianSupportPenalty,
                    DestroyPlanetPersonnelInjuryPercent = DestroyPlanetPersonnelInjuryPercent,
                    DestroyPlanetMinorPersonnelDeathPercent =
                        DestroyPlanetMinorPersonnelDeathPercent,
                    DestroyPlanetCoreSupportPenalty = DestroyPlanetCoreSupportPenalty,
                    DestroyPlanetOuterRimSupportPenalty = DestroyPlanetOuterRimSupportPenalty,
                    DestroyPlanetOuterRimSupportThreshold = DestroyPlanetOuterRimSupportThreshold,
                };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public PlanetaryAssaultConfig CreateCopy() =>
                new PlanetaryAssaultConfig
                {
                    PersonnelDivisor = PersonnelDivisor,
                    ShieldGeneratorLimit = ShieldGeneratorLimit,
                    DefenseFireDivisor = DefenseFireDivisor,
                    CollateralDamagePercent = CollateralDamagePercent,
                    GeneralLeadershipDivisor = GeneralLeadershipDivisor,
                    ContestRollMaximum = ContestRollMaximum,
                    DefenderWinsMaximum = DefenderWinsMaximum,
                    AttackerWinsMinimum = AttackerWinsMinimum,
                    CaptureGarrisonCount = CaptureGarrisonCount,
                };
        }

        /// <summary>
        /// Automatic space-combat resolution parameters.
        /// </summary>
        [PersistableObject]
        public class SpaceCombatConfig
        {
            public int AdmiralLeadershipDivisor { get; set; }

            public int CommanderCombatDivisor { get; set; }

            public double LaserCannonCapitalDamageMultiplier { get; set; }

            public double AutoResolveWeaponDamagePerStrength { get; set; }

            public double AutoResolveTorpedoDamagePerStrength { get; set; }

            public double AutoResolveFighterHullPerCraft { get; set; }

            public double AutoResolveFighterShieldRechargePerCraft { get; set; }

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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public SpaceCombatConfig CreateCopy() =>
                new SpaceCombatConfig
                {
                    AdmiralLeadershipDivisor = AdmiralLeadershipDivisor,
                    CommanderCombatDivisor = CommanderCombatDivisor,
                    LaserCannonCapitalDamageMultiplier = LaserCannonCapitalDamageMultiplier,
                    AutoResolveWeaponDamagePerStrength = AutoResolveWeaponDamagePerStrength,
                    AutoResolveTorpedoDamagePerStrength = AutoResolveTorpedoDamagePerStrength,
                    AutoResolveFighterHullPerCraft = AutoResolveFighterHullPerCraft,
                    AutoResolveFighterShieldRechargePerCraft =
                        AutoResolveFighterShieldRechargePerCraft,
                    AutoResolveFighterWeaponRechargeMultiplier =
                        AutoResolveFighterWeaponRechargeMultiplier,
                    AutoResolveMaximumIterations = AutoResolveMaximumIterations,
                    AutoResolveStagnationIterations = AutoResolveStagnationIterations,
                    AutoResolveRetreatStrengthRatio = AutoResolveRetreatStrengthRatio,
                    AutoResolveMinimumManeuverRatio = AutoResolveMinimumManeuverRatio,
                    AutoResolveTargetScanDivisor = AutoResolveTargetScanDivisor,
                    AutoResolveStartingDistance = AutoResolveStartingDistance,
                    AutoResolveWithdrawalDistance = AutoResolveWithdrawalDistance,
                    AutoResolveComponentDamageInterval = AutoResolveComponentDamageInterval,
                    AutoResolveComponentDamageRollMaximum = AutoResolveComponentDamageRollMaximum,
                    AutoResolveComponentDelayMinimum = AutoResolveComponentDelayMinimum,
                    AutoResolveComponentDelayMaximum = AutoResolveComponentDelayMaximum,
                    AutoResolveComponentDelayRecovery = AutoResolveComponentDelayRecovery,
                };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public BlockadeConfig CreateCopy() =>
                new BlockadeConfig
                {
                    CapitalShipProductionPenaltyPercent = CapitalShipProductionPenaltyPercent,
                    FighterProductionPenaltyPercent = FighterProductionPenaltyPercent,
                    EvacuationLossPercent = EvacuationLossPercent,
                };
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

            public int MissionParticipantEncounterMinimum { get; set; }

            public int MissionDefenderEncounterMinimum { get; set; }

            /// <summary>
            /// Gets or sets the ordered major officers considered before random encounter candidates.
            /// </summary>
            public List<string> MissionEncounterPriorityOfficerInstanceIDs { get; set; } =
                new List<string>();

            public Dictionary<int, int> RankLabelByMinimumForceRank { get; set; } =
                new Dictionary<int, int>();

            /// <summary>
            /// Resolves the label for the unsigned 16-bit representation of a stored Force rank
            /// using the greatest configured threshold that does not exceed it.
            /// </summary>
            /// <param name="forceRank">The numeric rank to classify.</param>
            /// <returns>The configured label active at that rank.</returns>
            public ForceRankLabel GetRankLabel(int forceRank)
            {
                return (ForceRankLabel)
                    new ProbabilityTable(RankLabelByMinimumForceRank).Lookup(
                        unchecked((ushort)forceRank)
                    );
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public JediConfig CreateCopy() =>
                new JediConfig
                {
                    DiscoveringForceUserThreshold = DiscoveringForceUserThreshold,
                    ForceQualifiedThreshold = ForceQualifiedThreshold,
                    FastHealThreshold = FastHealThreshold,
                    ForceGrowthPerMission = ForceGrowthPerMission,
                    TrainingCatchUpPercent = TrainingCatchUpPercent,
                    EncounterProbabilityOffset = EncounterProbabilityOffset,
                    MissionParticipantEncounterMinimum = MissionParticipantEncounterMinimum,
                    MissionDefenderEncounterMinimum = MissionDefenderEncounterMinimum,
                    MissionEncounterPriorityOfficerInstanceIDs =
                        MissionEncounterPriorityOfficerInstanceIDs?.ToList(),
                    RankLabelByMinimumForceRank =
                        RankLabelByMinimumForceRank == null
                            ? null
                            : new Dictionary<int, int>(RankLabelByMinimumForceRank),
                };
        }

        /// <summary>
        /// Linked-officer capture, injury, and combat-growth rules.
        /// </summary>
        [PersistableObject]
        public class DuelResolutionConfig
        {
            public Dictionary<int, int> CombatCaptureAvoidance { get; set; } =
                new Dictionary<int, int>();

            public int CaptureEvasionInjuryBaseChance { get; set; }

            public int MinimumInjuryChance { get; set; }

            public int InjuryBase { get; set; }

            public int InjurySecondaryRollMaximum { get; set; }

            public int CombatReward { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public DuelResolutionConfig CreateCopy() =>
                new DuelResolutionConfig
                {
                    CombatCaptureAvoidance =
                        CombatCaptureAvoidance == null
                            ? null
                            : new Dictionary<int, int>(CombatCaptureAvoidance),
                    CaptureEvasionInjuryBaseChance = CaptureEvasionInjuryBaseChance,
                    MinimumInjuryChance = MinimumInjuryChance,
                    InjuryBase = InjuryBase,
                    InjurySecondaryRollMaximum = InjurySecondaryRollMaximum,
                    CombatReward = CombatReward,
                };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public ResearchConfig CreateCopy() =>
                new ResearchConfig
                {
                    BaseResearchPoints = BaseResearchPoints,
                    ResearchDiceRange = ResearchDiceRange,
                    RefreshIntervalBase = RefreshIntervalBase,
                    RefreshIntervalSpread = RefreshIntervalSpread,
                };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AssassinationConfig CreateCopy() =>
                new AssassinationConfig
                {
                    BaseInjury = BaseInjury,
                    PrimaryInjuryRange = PrimaryInjuryRange,
                    SecondaryInjuryRange = SecondaryInjuryRange,
                    KillProbability = KillProbability,
                };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public RecoveryConfig CreateCopy() =>
                new RecoveryConfig
                {
                    MaxInjuryPoints = MaxInjuryPoints,
                    FastHealAmount = FastHealAmount,
                    NormalHealAmount = NormalHealAmount,
                    FastRepairAmount = FastRepairAmount,
                    NormalRepairAmount = NormalRepairAmount,
                    FastReplacementAmount = FastReplacementAmount,
                    NormalReplacementAmount = NormalReplacementAmount,
                };
        }

        /// <summary>
        /// Captive escape timing and probability.
        /// </summary>
        [PersistableObject]
        public class CaptiveConfig
        {
            public TickRangeConfig EscapeAttemptInterval { get; set; } = new TickRangeConfig();

            public Dictionary<int, int> EscapeTable { get; set; } = new Dictionary<int, int>();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public CaptiveConfig CreateCopy() =>
                new CaptiveConfig
                {
                    EscapeAttemptInterval = EscapeAttemptInterval?.CreateCopy(),
                    EscapeTable =
                        EscapeTable == null ? null : new Dictionary<int, int>(EscapeTable),
                };
        }

        /// <summary>
        /// Inclusive tick bounds for scheduling recurring activity.
        /// </summary>
        [PersistableObject]
        public class TickRangeConfig
        {
            public int Minimum { get; set; }

            public int Maximum { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public TickRangeConfig CreateCopy() =>
                new TickRangeConfig { Minimum = Minimum, Maximum = Maximum };
        }

        /// <summary>
        /// Game tick interval presets.
        /// </summary>
        [PersistableObject]
        public class GameSpeedConfig
        {
            public double TickWorkBudgetMilliseconds { get; set; }

            public float FastTickIntervalSeconds { get; set; }

            public float MediumTickIntervalSeconds { get; set; }

            public float SlowTickIntervalSeconds { get; set; }

            public float VerySlowTickIntervalSeconds { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public GameSpeedConfig CreateCopy() =>
                new GameSpeedConfig
                {
                    TickWorkBudgetMilliseconds = TickWorkBudgetMilliseconds,
                    FastTickIntervalSeconds = FastTickIntervalSeconds,
                    MediumTickIntervalSeconds = MediumTickIntervalSeconds,
                    SlowTickIntervalSeconds = SlowTickIntervalSeconds,
                    VerySlowTickIntervalSeconds = VerySlowTickIntervalSeconds,
                };
        }

        /// <summary>
        /// Message retention settings.
        /// </summary>
        [PersistableObject]
        public class MessageConfig
        {
            public int RetentionTicks { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public MessageConfig CreateCopy() =>
                new MessageConfig { RetentionTicks = RetentionTicks };
        }

        /// <summary>
        /// Controls the additional sector intelligence granted by successful espionage.
        /// </summary>
        [PersistableObject]
        public class EspionageConfig
        {
            public RandomCountConfig CoreSectorBonus { get; set; } = new RandomCountConfig();

            public RandomCountConfig HeadquartersBonus { get; set; } = new RandomCountConfig();

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public EspionageConfig CreateCopy() =>
                new EspionageConfig
                {
                    CoreSectorBonus = CoreSectorBonus?.CreateCopy(),
                    HeadquartersBonus = HeadquartersBonus?.CreateCopy(),
                };
        }

        /// <summary>
        /// Defines a count as a fixed minimum plus a random value below the spread.
        /// </summary>
        [PersistableObject]
        public class RandomCountConfig
        {
            public int Base { get; set; }

            public int Spread { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public RandomCountConfig CreateCopy() =>
                new RandomCountConfig { Base = Base, Spread = Spread };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public AIMissionTablesConfig CreateCopy() =>
                new AIMissionTablesConfig
                {
                    Reconnaissance =
                        Reconnaissance == null ? null : new Dictionary<int, int>(Reconnaissance),
                    Diplomacy = Diplomacy == null ? null : new Dictionary<int, int>(Diplomacy),
                    SubdueUprising =
                        SubdueUprising == null ? null : new Dictionary<int, int>(SubdueUprising),
                    Espionage = Espionage == null ? null : new Dictionary<int, int>(Espionage),
                    InciteUprising =
                        InciteUprising == null ? null : new Dictionary<int, int>(InciteUprising),
                    Rescue = Rescue == null ? null : new Dictionary<int, int>(Rescue),
                    Sabotage = Sabotage == null ? null : new Dictionary<int, int>(Sabotage),
                    Abduction = Abduction == null ? null : new Dictionary<int, int>(Abduction),
                    Assassination =
                        Assassination == null ? null : new Dictionary<int, int>(Assassination),
                    Recruitment =
                        Recruitment == null ? null : new Dictionary<int, int>(Recruitment),
                };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public ProbabilityTablesConfig CreateCopy() =>
                new ProbabilityTablesConfig
                {
                    UprisingStart =
                        UprisingStart == null ? null : new Dictionary<int, int>(UprisingStart),
                    Mission = Mission?.CreateCopy(),
                };
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

            public int DefaultSuccessProbability { get; set; }

            public int DefaultEvasionProbability { get; set; }

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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public MissionProbabilityTablesConfig CreateCopy() =>
                new MissionProbabilityTablesConfig
                {
                    Abduction = Abduction == null ? null : new Dictionary<int, int>(Abduction),
                    Assassination =
                        Assassination == null ? null : new Dictionary<int, int>(Assassination),
                    PlanetaryDecoy =
                        PlanetaryDecoy == null ? null : new Dictionary<int, int>(PlanetaryDecoy),
                    FleetDecoy = FleetDecoy == null ? null : new Dictionary<int, int>(FleetDecoy),
                    DecoyDefenderScalingPercent = DecoyDefenderScalingPercent,
                    FoilDefenderScalingPercent = FoilDefenderScalingPercent,
                    FoilFlatScoreAdjustment = FoilFlatScoreAdjustment,
                    Diplomacy = Diplomacy == null ? null : new Dictionary<int, int>(Diplomacy),
                    Espionage = Espionage == null ? null : new Dictionary<int, int>(Espionage),
                    Foil = Foil == null ? null : new Dictionary<int, int>(Foil),
                    Evasion = Evasion == null ? null : new Dictionary<int, int>(Evasion),
                    DefaultSuccessProbability = DefaultSuccessProbability,
                    DefaultEvasionProbability = DefaultEvasionProbability,
                    InciteUprising =
                        InciteUprising == null ? null : new Dictionary<int, int>(InciteUprising),
                    Recruitment =
                        Recruitment == null ? null : new Dictionary<int, int>(Recruitment),
                    Rescue = Rescue == null ? null : new Dictionary<int, int>(Rescue),
                    Sabotage = Sabotage == null ? null : new Dictionary<int, int>(Sabotage),
                    SubdueUprising =
                        SubdueUprising == null ? null : new Dictionary<int, int>(SubdueUprising),
                    TickRanges = TickRanges?.CreateCopy(),
                };
        }

        /// <summary>
        /// Mission tick range: minimum ticks before execution plus a random spread.
        /// </summary>
        [PersistableObject]
        public class MissionTickConfig
        {
            public int Base { get; set; }

            public int Spread { get; set; }

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public MissionTickConfig CreateCopy() =>
                new MissionTickConfig { Base = Base, Spread = Spread };
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

            /// <summary>
            /// Creates an independent snapshot of these settings.
            /// </summary>
            /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
            public MissionTickRangesConfig CreateCopy() =>
                new MissionTickRangesConfig
                {
                    Abduction = Abduction?.CreateCopy(),
                    Assassination = Assassination?.CreateCopy(),
                    Diplomacy = Diplomacy?.CreateCopy(),
                    Espionage = Espionage?.CreateCopy(),
                    InciteUprising = InciteUprising?.CreateCopy(),
                    Reconnaissance = Reconnaissance?.CreateCopy(),
                    Recruitment = Recruitment?.CreateCopy(),
                    Rescue = Rescue?.CreateCopy(),
                    Sabotage = Sabotage?.CreateCopy(),
                    SubdueUprising = SubdueUprising?.CreateCopy(),
                    Research = Research?.CreateCopy(),
                    JediTraining = JediTraining?.CreateCopy(),
                };
        }
    }
}
