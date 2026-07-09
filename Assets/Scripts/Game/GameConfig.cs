using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Runtime configuration loaded from the game config XML.
    /// </summary>
    [PersistableObject]
    public class GameConfig
    {
        /// <summary>AI scheduling, planning, and scoring settings.</summary>
        public AIConfig AI { get; set; } = new AIConfig();

        /// <summary>Movement timing settings.</summary>
        public MovementConfig Movement { get; set; } = new MovementConfig();

        /// <summary>Manufacturing and maintenance settings.</summary>
        public ProductionConfig Production { get; set; } = new ProductionConfig();

        /// <summary>Planet limit settings.</summary>
        public PlanetConfig Planet { get; set; } = new PlanetConfig();

        /// <summary>Combat resolution settings.</summary>
        public CombatConfig Combat { get; set; } = new CombatConfig();

        /// <summary>Uprising resolution settings.</summary>
        public UprisingConfig Uprising { get; set; } = new UprisingConfig();

        /// <summary>Popular support drift settings.</summary>
        public SupportShiftConfig SupportShift { get; set; } = new SupportShiftConfig();

        /// <summary>Blockade resolution settings.</summary>
        public BlockadeConfig Blockade { get; set; } = new BlockadeConfig();

        /// <summary>Victory condition settings.</summary>
        public VictoryConfig Victory { get; set; } = new VictoryConfig();

        /// <summary>Force progression settings.</summary>
        public JediConfig Jedi { get; set; } = new JediConfig();

        /// <summary>Research progression settings.</summary>
        public ResearchConfig Research { get; set; } = new ResearchConfig();

        /// <summary>Assassination outcome settings.</summary>
        public AssassinationConfig Assassination { get; set; } = new AssassinationConfig();

        /// <summary>Healing and repair settings.</summary>
        public RecoveryConfig Recovery { get; set; } = new RecoveryConfig();

        /// <summary>Captive escape settings.</summary>
        public CaptiveConfig Captive { get; set; } = new CaptiveConfig();

        /// <summary>Game tick speed settings.</summary>
        public GameSpeedConfig GameSpeed { get; set; } = new GameSpeedConfig();

        /// <summary>Shared probability table settings.</summary>
        public ProbabilityTablesConfig ProbabilityTables { get; set; } =
            new ProbabilityTablesConfig();

        /// <summary>
        /// AI decision-making and mission dispatch.
        /// </summary>
        [PersistableObject]
        public class AIConfig
        {
            /// <summary>Ticks between AI decision cycles.</summary>
            public int TickInterval { get; set; }

            /// <summary>Whether AI combat planning may queue planetary assaults.</summary>
            public bool EnablePlanetaryAssaults { get; set; } = true;

            /// <summary>Minimum diplomacy required for the AI to assign diplomacy.</summary>
            public int DiplomacyMinimumSkill { get; set; } = 69;

            /// <summary>Minimum leadership required for the AI to assign recruitment.</summary>
            public int RecruitmentMinimumLeadership { get; set; } = 80;

            /// <summary>Mission dispatch probability tables.</summary>
            public AIMissionTablesConfig MissionTables { get; set; } = new AIMissionTablesConfig();

            /// <summary>Mission proposal candidate limits.</summary>
            public AIMissionPlanningConfig MissionPlanning { get; set; } =
                new AIMissionPlanningConfig();

            /// <summary>Garrison requirement parameters.</summary>
            public GarrisonConfig Garrison { get; set; } = new GarrisonConfig();

            /// <summary>Lower bound for the fleet deployment probability gate.</summary>
            public int DeploymentGateLow { get; set; }

            /// <summary>Upper bound for the fleet deployment probability gate.</summary>
            public int DeploymentGateHigh { get; set; }

            /// <summary>Capital ship production pipeline parameters.</summary>
            public CapitalShipProductionConfig CapitalShipProduction { get; set; } =
                new CapitalShipProductionConfig();

            /// <summary>Unit selection parameters for AI manufacturing.</summary>
            public AISelectionConfig Selection { get; set; } = new AISelectionConfig();

            /// <summary>Non-capital strategic summary thresholds.</summary>
            public AINonCapitalSummaryConfig NonCapitalSummary { get; set; } =
                new AINonCapitalSummaryConfig();

            /// <summary>Infrastructure shortage parameters for AI manufacturing.</summary>
            public AIInfrastructureConfig Infrastructure { get; set; } =
                new AIInfrastructureConfig();

            /// <summary>Fleet attack and deployment scoring settings.</summary>
            public AIFleetDeploymentConfig FleetDeployment { get; set; } =
                new AIFleetDeploymentConfig();
        }

        /// <summary>
        /// Candidate limits used while generating mission proposals.
        /// </summary>
        [PersistableObject]
        public class AIMissionPlanningConfig
        {
            /// <summary>Maximum diplomacy mission planet candidates to consider.</summary>
            public int DiplomacyCandidatePlanetLimit { get; set; } = 8;

            /// <summary>Maximum research mission planet candidates to consider.</summary>
            public int ResearchCandidatePlanetLimit { get; set; } = 4;

            /// <summary>Maximum sabotage mission planet candidates to consider.</summary>
            public int SabotageCandidatePlanetLimit { get; set; } = 5;

            /// <summary>Maximum enemy officer candidates to consider.</summary>
            public int OfficerTargetCandidateLimit { get; set; } = 4;
        }

        /// <summary>
        /// Fleet attack and deployment scoring settings.
        /// </summary>
        [PersistableObject]
        public class AIFleetDeploymentConfig
        {
            /// <summary>Minimum friendly attack strength required before attacking.</summary>
            public int MinimumAttackStrength { get; set; }

            /// <summary>Minimum defensive strength a fleet should keep available.</summary>
            public int MinimumDefenseStrength { get; set; } = 1000;

            /// <summary>Minimum loaded regiments required before attempting planet capture.</summary>
            public int MinimumPlanetaryAssaultRegimentCount { get; set; } = 1;

            /// <summary>Required attack strength as a percent of planet defense.</summary>
            public int AttackStrengthPercentOfDefense { get; set; }

            /// <summary>Required attack strength as a percent of the strongest hostile fleet.</summary>
            public int AttackStrengthPercentOfStrongestHostileFleet { get; set; }

            /// <summary>Score weight for target strategic value.</summary>
            public int AttackStrategicValueWeight { get; set; } = 55;

            /// <summary>Score weight for fleet readiness.</summary>
            public int AttackReadinessWeight { get; set; } = 35;

            /// <summary>Score weight for capture viability.</summary>
            public int AttackCaptureViabilityWeight { get; set; } = 45;

            /// <summary>Score weight for travel efficiency.</summary>
            public int AttackTravelEfficiencyWeight { get; set; } = 20;

            /// <summary>Score penalty weight for expected losses.</summary>
            public int AttackExpectedLossPenaltyWeight { get; set; } = 50;

            /// <summary>Score penalty weight for committing a fleet to an attack.</summary>
            public int AttackOpportunityCostPenaltyWeight { get; set; } = 30;

            /// <summary>Score bonus for continuing an existing attack order.</summary>
            public int ExistingAttackOrderBonus { get; set; } = 25;

            /// <summary>Score bonus for attacking an enemy headquarters.</summary>
            public int HeadquartersAttackBonus { get; set; } = 45;
        }

        /// <summary>
        /// AI manufacturing selection weights and limits.
        /// </summary>
        [PersistableObject]
        public class AISelectionConfig
        {
            public float MinimumSelectableScore { get; set; }
            public int CandidatePoolSize { get; set; }
            public int RecentBuildHistoryLimit { get; set; }
            public int RepeatBuildPenaltyPerSelection { get; set; }
            public int LocalDuplicatePenaltyPerSelection { get; set; }
            public int MaxDuplicateCapitalTypePerFleet { get; set; }
            public int MaxDuplicateStarfighterTypePerFleet { get; set; }
            public int MaxDuplicateRegimentTypePerDestination { get; set; }
            public int PremiumCapitalConstructionCostThreshold { get; set; }
            public int MaxPremiumCapitalsPerFaction { get; set; }
            public int CapitalConstructionCostWeight { get; set; }
            public int CapitalMaintenanceCostWeight { get; set; }
            public int CapitalCombatWeight { get; set; }
            public int CapitalExcessCombatPenaltyWeight { get; set; } = 1;
            public int CapitalStarfighterCapacityWeight { get; set; }
            public int CapitalRegimentCapacityWeight { get; set; }
            public int CapitalBombardmentWeight { get; set; }
            public int CapitalGravityWellWeight { get; set; }
            public int CapitalEmptyFleetCombatBoost { get; set; }
            public int CapitalMissingStarfighterCapacityBoost { get; set; }
            public int CapitalMissingRegimentCapacityBoost { get; set; }
            public int CapitalMissingGravityWellBoost { get; set; }
            public int StarfighterEscortWeight { get; set; }
            public int StarfighterInterceptorWeight { get; set; }
            public int StarfighterBomberWeight { get; set; }
            public int StarfighterMissingInterceptorBoost { get; set; }
            public int StarfighterMissingBomberBoost { get; set; }
            public int RegimentDefenseWeight { get; set; }
            public int RegimentAttackWeight { get; set; }
            public int RegimentBombardmentDefenseWeight { get; set; }
            public int RegimentMaintenanceCostWeight { get; set; }
            public int RegimentGarrisonDefenseBoost { get; set; }
            public int RegimentFleetAttackBoost { get; set; }
            public int MinimumMaintenanceHeadroomAfterProduction { get; set; } = 200;
            public int MaintenanceHeadroomHardFloor { get; set; } = 0;
            public int MaintenanceHeadroomPenaltyWeight { get; set; }
            public int MaintenanceShortfallPenalty { get; set; }
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
            public int PlanetsPerShipyard { get; set; }
            public int PlanetsPerTrainingFacility { get; set; }
            public int ManufacturingFacilityBaseDemandPercent { get; set; }
            public int ConstructionFacilityDemandPercent { get; set; }
            public int ShipyardDemandPercent { get; set; }
            public int TrainingFacilityDemandPercent { get; set; }
            public int FleetCapitalShipDemandPercent { get; set; } = 80;
            public int FleetStarfighterDemandPercent { get; set; } = 50;
            public int FleetRegimentDemandPercent { get; set; } = 60;
            public int StarfighterParentFillPercent { get; set; } = 100;
            public int StarfighterLocalReservePercent { get; set; }
            public int AssaultRegimentLoadPercent { get; set; } = 100;
            public int GarrisonRegimentReservePercent { get; set; }
            public int MaxDefensePerPlanet { get; set; }
            public int EconomyDefaultBatchSize { get; set; } = 1;
            public int EconomyDemandPercent { get; set; } = 90;
            public int EconomySevereDemandPercent { get; set; } = 100;
            public int EconomySevereDeficitPercent { get; set; } = 25;
            public int EconomyCompetingNeedSlotReserve { get; set; } = 1;
            public int EconomyMaintenanceShortfallPressure { get; set; } = 40;
            public int EconomyMaintenanceReservePressure { get; set; } = 20;
            public int FleetTargetValuePressureWeight { get; set; } = 20;
            public int FleetReadinessPressureWeight { get; set; } = 35;
            public int FleetFinalReadinessGatePressure { get; set; } = 35;
            public int FleetFinalReadinessGateUnitCount { get; set; } = 2;
            public int FleetStarfighterFillPressureWeight { get; set; } = 20;
            public int FleetReinforcementTravelPenaltyWeight { get; set; } = 1;
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
        }

        /// <summary>
        /// Capital ship production pipeline parameters.
        /// </summary>
        [PersistableObject]
        public class CapitalShipProductionConfig
        {
            /// <summary>Personnel skill divisor for facility production contribution.</summary>
            public int FacilityPersonnelDivisor { get; set; }

            /// <summary>Lower bound of the strike target evaluation threshold.</summary>
            public int StrikeThresholdLow { get; set; }

            /// <summary>Upper bound of the strike target evaluation threshold.</summary>
            public int StrikeThresholdHigh { get; set; }

            /// <summary>Strike resistance for system energy targets.</summary>
            public int EnergyStrikeResistance { get; set; }

            /// <summary>Strike resistance for allocated energy targets.</summary>
            public int AllocatedEnergyStrikeResistance { get; set; }

            /// <summary>Popular support shift applied to the producing faction during a Death Star orbital strike.</summary>
            public int OrbitalStrikeSupportShift { get; set; }
        }

        /// <summary>
        /// Garrison troop requirements based on popular support.
        /// </summary>
        [PersistableObject]
        public class GarrisonConfig
        {
            /// <summary>Popular support threshold below which garrison troops are required.</summary>
            public int SupportThreshold { get; set; }

            /// <summary>Divisor for the garrison requirement calculation.</summary>
            public int GarrisonDivisor { get; set; }

            /// <summary>Multiplier applied to the garrison requirement during an uprising.</summary>
            public int UprisingMultiplier { get; set; }

            /// <summary>
            /// Maximum remaining planet garrison deficit that still allows loading regiments into fleets.
            /// </summary>
            public int FleetLoadingDeficitThreshold { get; set; }
        }

        /// <summary>
        /// Uprising rolls, table lookups, and resolution.
        /// </summary>
        [PersistableObject]
        public class UprisingConfig
        {
            /// <summary>Range for the uprising dice roll.</summary>
            public int DiceRange { get; set; }

            /// <summary>Addend applied to each uprising dice roll.</summary>
            public int DiceAddend { get; set; }

            /// <summary>Maps an uprising score to a property damage consequence code.</summary>
            public Dictionary<int, int> PrimaryConsequenceTable { get; set; } =
                new Dictionary<int, int>();

            /// <summary>Maps an uprising score to a personnel consequence code.</summary>
            public Dictionary<int, int> SecondaryConsequenceTable { get; set; } =
                new Dictionary<int, int>();

            /// <summary>Popular support shift applied to the controlling faction each uprising tick.</summary>
            public int ControllerSupportShift { get; set; }

            /// <summary>Divisor applied to weak uprising support penalties.</summary>
            public int WeakSupportPenaltyDivisor { get; set; } = 2;

            /// <summary>Minimum ticks between active-uprising support drift pulses.</summary>
            public int ActiveSupportDriftMinTicks { get; set; }

            /// <summary>Maximum ticks between active-uprising support drift pulses.</summary>
            public int ActiveSupportDriftMaxTicks { get; set; }

            /// <summary>Minimum ticks between active-uprising incident pulses.</summary>
            public int IncidentPulseMinTicks { get; set; }

            /// <summary>Maximum ticks between active-uprising incident pulses.</summary>
            public int IncidentPulseMaxTicks { get; set; }
        }

        /// <summary>
        /// Periodic popular support shifts and hostile force penalties.
        /// </summary>
        [PersistableObject]
        public class SupportShiftConfig
        {
            /// <summary>Support must be at or below this for a shift to apply.</summary>
            public int ShiftThreshold { get; set; }

            /// <summary>Lower boundary of the middle bracket.</summary>
            public int LowBracketCeiling { get; set; }

            /// <summary>Upper boundary of the middle bracket.</summary>
            public int MidBracketCeiling { get; set; }

            /// <summary>Base shift for support in the low bracket.</summary>
            public int LowBracketShift { get; set; }

            /// <summary>Base shift for support in the mid bracket.</summary>
            public int MidBracketShift { get; set; }

            /// <summary>Base shift for support in the high bracket.</summary>
            public int HighBracketShift { get; set; }

            /// <summary>Penalty per hostile fleet.</summary>
            public int FleetPenalty { get; set; }

            /// <summary>Penalty per hostile fighter squadron.</summary>
            public int FighterPenalty { get; set; }

            /// <summary>Penalty per hostile troop.</summary>
            public int TroopPenalty { get; set; }

            /// <summary>Popular support threshold above which a neutral planet transfers to the faction.</summary>
            public int OwnershipTransferThreshold { get; set; }

            /// <summary>Support shift when a blockading fleet matches the popular support side.</summary>
            public int BlockadeMatchShift { get; set; }

            /// <summary>Support shift when a blockading fleet opposes the popular support side.</summary>
            public int BlockadeOpposeShift { get; set; }

            /// <summary>Support shift applied after a successful diplomacy mission completes.</summary>
            public int DiplomacyCompletionSupportBonus { get; set; }

            /// <summary>Base support shift for successful diplomacy on an owned planet.</summary>
            public int DiplomacyOwnedPlanetSupportBase { get; set; }

            /// <summary>Random support shift range for successful diplomacy on an owned planet.</summary>
            public int DiplomacyOwnedPlanetSupportRange { get; set; }

            /// <summary>Base support shift for successful diplomacy on a neutral planet.</summary>
            public int DiplomacyNeutralPlanetSupportBase { get; set; }

            /// <summary>Random support shift range for successful diplomacy on a neutral planet.</summary>
            public int DiplomacyNeutralPlanetSupportRange { get; set; }
        }

        /// <summary>
        /// AI mission dispatch probability tables. Each table maps a per-mission score to a dispatch probability.
        /// </summary>
        [PersistableObject]
        public class AIMissionTablesConfig
        {
            /// <summary>Reconnaissance mission dispatch table.</summary>
            public Dictionary<int, int> Reconnaissance { get; set; } = new Dictionary<int, int>();

            /// <summary>Diplomacy mission dispatch table.</summary>
            public Dictionary<int, int> Diplomacy { get; set; } = new Dictionary<int, int>();

            /// <summary>SubdueUprising mission dispatch table.</summary>
            public Dictionary<int, int> SubdueUprising { get; set; } = new Dictionary<int, int>();

            /// <summary>Espionage mission dispatch table.</summary>
            public Dictionary<int, int> Espionage { get; set; } = new Dictionary<int, int>();

            /// <summary>InciteUprising mission dispatch table.</summary>
            public Dictionary<int, int> InciteUprising { get; set; } = new Dictionary<int, int>();

            /// <summary>Rescue mission dispatch table.</summary>
            public Dictionary<int, int> Rescue { get; set; } = new Dictionary<int, int>();

            /// <summary>Sabotage mission dispatch table.</summary>
            public Dictionary<int, int> Sabotage { get; set; } = new Dictionary<int, int>();

            /// <summary>Abduction mission dispatch table.</summary>
            public Dictionary<int, int> Abduction { get; set; } = new Dictionary<int, int>();

            /// <summary>Assassination mission dispatch table.</summary>
            public Dictionary<int, int> Assassination { get; set; } = new Dictionary<int, int>();

            /// <summary>Recruitment mission dispatch table.</summary>
            public Dictionary<int, int> Recruitment { get; set; } = new Dictionary<int, int>();
        }

        /// <summary>
        /// Fleet transit time and hyperdrive parameters.
        /// </summary>
        [PersistableObject]
        public class MovementConfig
        {
            /// <summary>Distance scaling factor for transit time.</summary>
            public double DistanceScale { get; set; }

            /// <summary>Minimum transit ticks regardless of distance.</summary>
            public int MinTransitTicks { get; set; }

            /// <summary>Minimum transit ticks for movement between planets in the same system.</summary>
            public int SameSystemMinTransitTicks { get; set; }

            /// <summary>Default hyperdrive rating for fighters and troops.</summary>
            public int DefaultFighterHyperdrive { get; set; }
        }

        /// <summary>
        /// Manufacturing and production.
        /// </summary>
        [PersistableObject]
        public class ProductionConfig
        {
            /// <summary>Ticks between maintenance shortfall auto-scrap attempts.</summary>
            public int MaintenanceShortfallAutoscrapInterval { get; set; }

            /// <summary>Production penalty per hostile capital ship during a blockade.</summary>
            public int BlockadeCapitalShipPenalty { get; set; }

            /// <summary>Production penalty per hostile fighter during a blockade.</summary>
            public int BlockadeFighterPenalty { get; set; }

            /// <summary>Maps a roll threshold to a progress increment for capital ship production.</summary>
            public Dictionary<int, int> CapitalShipProgressTable { get; set; } =
                new Dictionary<int, int>();

            /// <summary>Range for the capital ship progress roll.</summary>
            public int CapitalShipProgressRollRange { get; set; }

            /// <summary>Threshold for the capital ship success check roll.</summary>
            public int CapitalShipSuccessThreshold { get; set; }

            /// <summary>Range for the capital ship success check roll.</summary>
            public int CapitalShipSuccessRollRange { get; set; }

            /// <summary>Popular support shift applied when a capital ship completes.</summary>
            public int CapitalShipCompletionSupportShift { get; set; }

            /// <summary>Popular support shift applied when a building completes.</summary>
            public int BuildingCompletionSupportShift { get; set; }

            /// <summary>Popular support shift applied when a troop completes.</summary>
            public int TroopCompletionSupportShift { get; set; }
        }

        /// <summary>
        /// Per-planet generation limits.
        /// </summary>
        [PersistableObject]
        public class PlanetConfig
        {
            /// <summary>Maximum energy capacity per planet.</summary>
            public int MaxEnergy { get; set; }

            /// <summary>Maximum raw resource nodes per planet.</summary>
            public int MaxRawMaterials { get; set; }
        }

        /// <summary>
        /// Combat resolution parameters for assault, bombardment, and dogfights.
        /// </summary>
        [PersistableObject]
        public class CombatConfig
        {
            /// <summary>Personnel divisor for the assault strength calculation.</summary>
            public int AssaultPersonnelDivisor { get; set; }

            /// <summary>Lower bound of the bombardment strike resistance check.</summary>
            public int BombardmentStrikeThresholdLow { get; set; }

            /// <summary>Upper bound of the bombardment strike resistance check.</summary>
            public int BombardmentStrikeThresholdHigh { get; set; }

            /// <summary>Resistance value for the system energy bombardment lane.</summary>
            public int BombardmentEnergyResistance { get; set; }

            /// <summary>Minimum number of shield facilities required to block bombardment.</summary>
            public int BombardmentShieldBlockThreshold { get; set; }

            /// <summary>Divisor applied to a defense facility's production modifier when rolling its return fire chance.</summary>
            public int DefenseFacilityResponseDivisor { get; set; }

            /// <summary>Probability (out of 100) that a single planetary defense repeat trial succeeds.</summary>
            public int RepeatTrialProbability { get; set; }

            /// <summary>Divisor applied to commander skill during ground combat.</summary>
            public int GroundCombatCommanderDivisor { get; set; }

            /// <summary>Range for the ground combat contest roll.</summary>
            public int GroundCombatContestDiceRange { get; set; }

            /// <summary>Threshold at or below which the defender wins ground combat.</summary>
            public int GroundCombatDefenderWinsThreshold { get; set; }

            /// <summary>Threshold at or above which the attacker wins ground combat.</summary>
            public int GroundCombatAttackerWinsThreshold { get; set; }

            /// <summary>Percent variance applied symmetrically to each weapon damage roll.</summary>
            public int WeaponDamageVariancePercent { get; set; }

            /// <summary>Percent of each side's fighter squadrons that can be lost per dogfight round.</summary>
            public int FighterDogfightLossRatePercent { get; set; }

            /// <summary>Minimum percent of nominal fighter damage applied per strike.</summary>
            public int FighterDamageBasePercent { get; set; }

            /// <summary>Percent spread added to the minimum fighter damage.</summary>
            public int FighterDamageSpreadPercent { get; set; }
        }

        /// <summary>
        /// Evacuation losses when units depart blockaded planets.
        /// </summary>
        [PersistableObject]
        public class BlockadeConfig
        {
            /// <summary>Percent chance each regiment is destroyed when evacuating through a blockade.</summary>
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
            /// <summary>ForceRank threshold to enter the discovering-force-user state.</summary>
            public int DiscoveringForceUserThreshold { get; set; }

            /// <summary>ForceRank threshold for full Jedi qualification.</summary>
            public int ForceQualifiedThreshold { get; set; }

            /// <summary>ForceRank threshold for Force-based fast healing.</summary>
            public int FastHealThreshold { get; set; }

            /// <summary>ForceValue increment per successful mission.</summary>
            public int ForceGrowthPerMission { get; set; }

            /// <summary>Training catch-up range percentage.</summary>
            public int TrainingCatchUpPercent { get; set; }

            /// <summary>ForceRank threshold for Luke to learn his heritage.</summary>
            public int HeritageThreshold { get; set; }

            /// <summary>Percent bonus to ForceRank on Dagobah mission completion.</summary>
            public int DagobahCompletionBonusPercent { get; set; }

            /// <summary>Local force-user minimum rank for encounter eligibility.</summary>
            public int EncounterLocalMinRank { get; set; }

            /// <summary>Cross-side force-user minimum rank for encounter eligibility.</summary>
            public int EncounterCrossSideMinRank { get; set; }

            /// <summary>Offset applied to the encounter probability calculation.</summary>
            public int EncounterProbabilityOffset { get; set; }

            /// <summary>ForceRank threshold for the Novice label.</summary>
            public int RankLabelNovice { get; set; }

            /// <summary>ForceRank threshold for the Trainee label.</summary>
            public int RankLabelTrainee { get; set; }

            /// <summary>ForceRank threshold for the ForceStudent label.</summary>
            public int RankLabelForceStudent { get; set; }

            /// <summary>ForceRank threshold for the ForceKnight label.</summary>
            public int RankLabelForceKnight { get; set; }

            /// <summary>ForceRank threshold for the ForceMaster label.</summary>
            public int RankLabelForceMaster { get; set; }
        }

        /// <summary>
        /// Research advancement and officer research mechanics.
        /// </summary>
        [PersistableObject]
        public class ResearchConfig
        {
            /// <summary>Base research points awarded per successful research mission.</summary>
            public int BaseResearchPoints { get; set; }

            /// <summary>Random bonus range added to the base research points on success.</summary>
            public int ResearchDiceRange { get; set; }

            /// <summary>Base ticks between research capacity refresh pulses.</summary>
            public int RefreshIntervalBase { get; set; }

            /// <summary>Random spread added to the base interval on each pulse.</summary>
            public int RefreshIntervalSpread { get; set; }
        }

        /// <summary>
        /// Assassination mission outcome parameters.
        /// </summary>
        [PersistableObject]
        public class AssassinationConfig
        {
            /// <summary>Base injury always applied on a successful hit.</summary>
            public int BaseInjury { get; set; }

            /// <summary>Upper bound of the primary injury roll.</summary>
            public int PrimaryInjuryRange { get; set; }

            /// <summary>Upper bound of the secondary injury roll.</summary>
            public int SecondaryInjuryRange { get; set; }

            /// <summary>Probability that a hit kills the target outright.</summary>
            public int KillProbability { get; set; }
        }

        /// <summary>
        /// Officer healing and ship/fighter repair rates.
        /// </summary>
        [PersistableObject]
        public class RecoveryConfig
        {
            /// <summary>Maximum injury points an officer can accumulate.</summary>
            public int MaxInjuryPoints { get; set; }

            /// <summary>Injury points healed per tick for officers with FastHeal.</summary>
            public int FastHealAmount { get; set; }

            /// <summary>Injury points healed per tick for normal officers.</summary>
            public int NormalHealAmount { get; set; }

            /// <summary>Hull points repaired per tick for ships at a friendly shipyard.</summary>
            public int FastRepairAmount { get; set; }

            /// <summary>Hull points repaired per tick for ships not at a shipyard.</summary>
            public int NormalRepairAmount { get; set; }

            /// <summary>Fighters replaced per tick for squadrons at a friendly planet.</summary>
            public int FastReplacementAmount { get; set; }

            /// <summary>Fighters replaced per tick for squadrons not at a friendly planet.</summary>
            public int NormalReplacementAmount { get; set; }
        }

        /// <summary>
        /// Captive escape probability and loyalty effects.
        /// </summary>
        [PersistableObject]
        public class CaptiveConfig
        {
            /// <summary>Maps an officer-versus-defense score to escape probability.</summary>
            public Dictionary<int, int> EscapeTable { get; set; } = new Dictionary<int, int>();

            /// <summary>Loyalty shift applied on a successful escape.</summary>
            public int EscapeLoyaltyShift { get; set; }
        }

        /// <summary>
        /// Game tick interval presets.
        /// </summary>
        [PersistableObject]
        public class GameSpeedConfig
        {
            /// <summary>Tick interval for fast speed.</summary>
            public float FastTickIntervalSeconds { get; set; } = 1f;

            /// <summary>Tick interval for medium speed.</summary>
            public float MediumTickIntervalSeconds { get; set; } = 10f;

            /// <summary>Tick interval for slow speed.</summary>
            public float SlowTickIntervalSeconds { get; set; } = 60f;

            /// <summary>Tick interval for very slow speed.</summary>
            public float VerySlowTickIntervalSeconds { get; set; } = 120f;
        }

        /// <summary>
        /// Probability tables grouped by data type.
        /// </summary>
        [PersistableObject]
        public class ProbabilityTablesConfig
        {
            /// <summary>Uprising start probability table.</summary>
            public Dictionary<int, int> UprisingStart { get; set; } = new Dictionary<int, int>();

            /// <summary>Mission-related probability tables.</summary>
            public MissionProbabilityTablesConfig Mission { get; set; } =
                new MissionProbabilityTablesConfig();
        }

        /// <summary>
        /// Per-mission success probability tables and tick ranges.
        /// </summary>
        [PersistableObject]
        public class MissionProbabilityTablesConfig
        {
            /// <summary>Abduction success probability table.</summary>
            public Dictionary<int, int> Abduction { get; set; } = new Dictionary<int, int>();

            /// <summary>Assassination success probability table.</summary>
            public Dictionary<int, int> Assassination { get; set; } = new Dictionary<int, int>();

            /// <summary>Decoy probability table.</summary>
            public Dictionary<int, int> Decoy { get; set; } = new Dictionary<int, int>();

            /// <summary>Percentage of defender espionage subtracted from the decoy score.</summary>
            public int DecoyDefenderScalingPercent { get; set; }

            /// <summary>Percentage of defender espionage applied to mission foil checks.</summary>
            public int FoilDefenderScalingPercent { get; set; }

            /// <summary>Flat modifier applied to mission foil checks.</summary>
            public int FoilFlatScoreAdjustment { get; set; }

            /// <summary>Diplomacy success probability table.</summary>
            public Dictionary<int, int> Diplomacy { get; set; } = new Dictionary<int, int>();

            /// <summary>Death Star sabotage success probability table.</summary>
            public Dictionary<int, int> DeathStarSabotage { get; set; } =
                new Dictionary<int, int>();

            /// <summary>Espionage success probability table.</summary>
            public Dictionary<int, int> Espionage { get; set; } = new Dictionary<int, int>();

            /// <summary>Mission foil probability table.</summary>
            public Dictionary<int, int> Foil { get; set; } = new Dictionary<int, int>();

            /// <summary>Kill-or-capture outcome table.</summary>
            public Dictionary<int, int> KillOrCapture { get; set; } = new Dictionary<int, int>();

            /// <summary>Fallback mission success probability when a table has no match.</summary>
            public int DefaultSuccessProbability { get; set; } = 50;

            /// <summary>Fallback kill-or-capture probability when the table has no match.</summary>
            public int DefaultKillOrCaptureProbability { get; set; } = 50;

            /// <summary>Incite uprising success probability table.</summary>
            public Dictionary<int, int> InciteUprising { get; set; } = new Dictionary<int, int>();

            /// <summary>Recruitment success probability table.</summary>
            public Dictionary<int, int> Recruitment { get; set; } = new Dictionary<int, int>();

            /// <summary>Rescue success probability table.</summary>
            public Dictionary<int, int> Rescue { get; set; } = new Dictionary<int, int>();

            /// <summary>Sabotage success probability table.</summary>
            public Dictionary<int, int> Sabotage { get; set; } = new Dictionary<int, int>();

            /// <summary>Subdue uprising success probability table.</summary>
            public Dictionary<int, int> SubdueUprising { get; set; } = new Dictionary<int, int>();

            /// <summary>Per-mission tick ranges.</summary>
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
                    "DeathStarSabotage" => DeathStarSabotage,
                    "Espionage" => Espionage,
                    "InciteUprising" => InciteUprising,
                    "Recruitment" => Recruitment,
                    "Rescue" => Rescue,
                    "Sabotage" => Sabotage,
                    "SubdueUprising" => SubdueUprising,
                    _ => null,
                };
            }
        }

        /// <summary>
        /// Mission tick range: minimum ticks before execution plus a random spread.
        /// </summary>
        [PersistableObject]
        public class MissionTickConfig
        {
            /// <summary>Guaranteed minimum ticks before the mission executes.</summary>
            public int Base { get; set; }

            /// <summary>Random spread added to the base.</summary>
            public int Spread { get; set; }
        }

        /// <summary>
        /// Per-mission tick ranges.
        /// </summary>
        [PersistableObject]
        public class MissionTickRangesConfig
        {
            /// <summary>Tick range for Abduction.</summary>
            public MissionTickConfig Abduction { get; set; } = new MissionTickConfig();

            /// <summary>Tick range for Assassination.</summary>
            public MissionTickConfig Assassination { get; set; } = new MissionTickConfig();

            /// <summary>Tick range for Diplomacy.</summary>
            public MissionTickConfig Diplomacy { get; set; } = new MissionTickConfig();

            /// <summary>Tick range for Espionage.</summary>
            public MissionTickConfig Espionage { get; set; } = new MissionTickConfig();

            /// <summary>Tick range for InciteUprising.</summary>
            public MissionTickConfig InciteUprising { get; set; } = new MissionTickConfig();

            /// <summary>Tick range for Reconnaissance.</summary>
            public MissionTickConfig Reconnaissance { get; set; } = new MissionTickConfig();

            /// <summary>Tick range for Recruitment.</summary>
            public MissionTickConfig Recruitment { get; set; } = new MissionTickConfig();

            /// <summary>Tick range for Rescue.</summary>
            public MissionTickConfig Rescue { get; set; } = new MissionTickConfig();

            /// <summary>Tick range for Sabotage.</summary>
            public MissionTickConfig Sabotage { get; set; } = new MissionTickConfig();

            /// <summary>Tick range for SubdueUprising.</summary>
            public MissionTickConfig SubdueUprising { get; set; } = new MissionTickConfig();

            /// <summary>Tick range for Research.</summary>
            public MissionTickConfig Research { get; set; } = new MissionTickConfig();

            /// <summary>Tick range for JediTraining.</summary>
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
