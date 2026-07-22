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
        public AIConfig AI { get; set; } = new AIConfig();

        public MovementConfig Movement { get; set; } = new MovementConfig();

        public ProductionConfig Production { get; set; } = new ProductionConfig();

        public PlanetConfig Planet { get; set; } = new PlanetConfig();

        public CombatConfig Combat { get; set; } = new CombatConfig();

        public UprisingConfig Uprising { get; set; } = new UprisingConfig();

        public SupportShiftConfig SupportShift { get; set; } = new SupportShiftConfig();

        public BlockadeConfig Blockade { get; set; } = new BlockadeConfig();

        public VictoryConfig Victory { get; set; } = new VictoryConfig();

        public JediConfig Jedi { get; set; } = new JediConfig();

        public ResearchConfig Research { get; set; } = new ResearchConfig();

        public AssassinationConfig Assassination { get; set; } = new AssassinationConfig();

        public RecoveryConfig Recovery { get; set; } = new RecoveryConfig();

        public CaptiveConfig Captive { get; set; } = new CaptiveConfig();

        public GameSpeedConfig GameSpeed { get; set; } = new GameSpeedConfig();

        public MessageConfig Messages { get; set; } = new MessageConfig();

        public ProbabilityTablesConfig ProbabilityTables { get; set; } =
            new ProbabilityTablesConfig();

        /// <summary>
        /// AI decision-making and mission dispatch.
        /// </summary>
        [PersistableObject]
        public class AIConfig
        {
            public int TickInterval { get; set; }

            public bool EnablePlanetaryAssaults { get; set; } = true;

            public int DiplomacyMinimumSkill { get; set; } = 69;

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
        /// Candidate limits used while generating mission proposals.
        /// </summary>
        [PersistableObject]
        public class AIMissionPlanningConfig
        {
            public int DiplomacyCandidatePlanetLimit { get; set; } = 8;

            public int ResearchCandidatePlanetLimit { get; set; } = 4;

            public int SabotageCandidatePlanetLimit { get; set; } = 5;

            public int OfficerTargetCandidateLimit { get; set; } = 4;
        }

        /// <summary>
        /// Fleet attack and deployment scoring settings.
        /// </summary>
        [PersistableObject]
        public class AIFleetDeploymentConfig
        {
            public int MinimumAttackStrength { get; set; }

            public int MinimumDefenseStrength { get; set; } = 1000;

            public int MinimumPlanetaryAssaultRegimentCount { get; set; } = 1;

            public int AttackStrengthPercentOfDefense { get; set; }

            public int AttackStrengthPercentOfStrongestHostileFleet { get; set; }

            public int AttackStrategicValueWeight { get; set; } = 55;

            public int AttackReadinessWeight { get; set; } = 35;

            public int AttackCaptureViabilityWeight { get; set; } = 45;

            public int AttackTravelEfficiencyWeight { get; set; } = 20;

            public int AttackExpectedLossPenaltyWeight { get; set; } = 50;

            public int AttackOpportunityCostPenaltyWeight { get; set; } = 30;

            public int ExistingAttackOrderBonus { get; set; } = 25;

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
        /// Garrison troop requirements based on popular support.
        /// </summary>
        [PersistableObject]
        public class GarrisonConfig
        {
            public int SupportThreshold { get; set; }

            public int GarrisonDivisor { get; set; }

            public int UprisingMultiplier { get; set; }

            public int FleetLoadingDeficitThreshold { get; set; }
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

            public string ResistanceRegimentTypeID { get; set; }

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
            public int ShiftThreshold { get; set; }

            public int LowBracketCeiling { get; set; }

            public int MidBracketCeiling { get; set; }

            public int LowBracketShift { get; set; }

            public int MidBracketShift { get; set; }

            public int HighBracketShift { get; set; }

            public int FleetPenalty { get; set; }

            public int FighterPenalty { get; set; }

            public int TroopPenalty { get; set; }

            public int OwnershipTransferThreshold { get; set; }

            public int WeakSupportPenaltyDivisor { get; set; }

            public int GarrisonRemovalSupportShift { get; set; }

            public int ControlChangeSupportShift { get; set; }

            public int BlockadeMatchShift { get; set; }

            public int BlockadeOpposeShift { get; set; }

            public int DiplomacyCompletionSupportBonus { get; set; }

            public int DiplomacyOwnedPlanetSupportBase { get; set; }

            public int DiplomacyOwnedPlanetSupportRange { get; set; }

            public int DiplomacyNeutralPlanetSupportBase { get; set; }

            public int DiplomacyNeutralPlanetSupportRange { get; set; }
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
        /// Fleet transit time and hyperdrive parameters.
        /// </summary>
        [PersistableObject]
        public class MovementConfig
        {
            public double DistanceScale { get; set; }

            public int MinTransitTicks { get; set; }

            public int SameSystemMinTransitTicks { get; set; }

            public int DefaultFighterHyperdrive { get; set; }
        }

        /// <summary>
        /// Manufacturing and production.
        /// </summary>
        [PersistableObject]
        public class ProductionConfig
        {
            public int MaintenanceShortfallAutoscrapInterval { get; set; }

            public int ResourceMaintenanceLoadPercent { get; set; }

            public int ResourceCollectionBasePercent { get; set; }

            public int ResourceStartupBasePercent { get; set; }

            public int ResourceStartupRandomPercent { get; set; }
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
        /// Combat resolution parameters for assault, bombardment, and dogfights.
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
            public int StrikeRollMinimum { get; set; }
            public int StrikeRollMaximum { get; set; }
            public int EnergyResistance { get; set; }
            public int AllocatedEnergyResistance { get; set; }
            public int HeadquartersResistance { get; set; }
            public int CivilianSupportPenalty { get; set; }
            public int CivilianCoreAllianceSupportPenalty { get; set; }
            public int CivilianCoreEmpireSupportPenalty { get; set; }
            public int CivilianOuterRimAllianceSupportPenalty { get; set; }
            public int CivilianOuterRimEmpireSupportPenalty { get; set; }
            public int DestroySystemPersonnelInjuryPercent { get; set; }
            public int DestroySystemMinorPersonnelDeathPercent { get; set; }
            public int DestroySystemCoreSupportPenalty { get; set; }
            public int DestroySystemOuterRimSupportPenalty { get; set; }
            public int DestroySystemOuterRimSupportThreshold { get; set; }
            public List<string> PlanetDestroyingCapitalShipTypeIDs { get; set; } =
                new List<string>();
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
        /// Space combat damage and loss parameters.
        /// </summary>
        [PersistableObject]
        public class SpaceCombatConfig
        {
            public int FighterTacticalDurability { get; set; }

            public int WeaponDamageVariancePercent { get; set; }

            public int FighterDogfightLossRatePercent { get; set; }

            public int FighterDamageBasePercent { get; set; }

            public int FighterDamageSpreadPercent { get; set; }
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

            public int HeritageThreshold { get; set; }

            public int DagobahCompletionBonusPercent { get; set; }

            public int EncounterLocalMinRank { get; set; }

            public int EncounterCrossSideMinRank { get; set; }

            public int EncounterProbabilityOffset { get; set; }

            public int RankLabelNovice { get; set; }

            public int RankLabelTrainee { get; set; }

            public int RankLabelForceStudent { get; set; }

            public int RankLabelForceKnight { get; set; }

            public int RankLabelForceMaster { get; set; }
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
            public int RetentionTicks { get; set; } = 300;
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

            public Dictionary<int, int> Decoy { get; set; } = new Dictionary<int, int>();

            public int DecoyDefenderScalingPercent { get; set; }

            public int FoilDefenderScalingPercent { get; set; }

            public int FoilFlatScoreAdjustment { get; set; }

            public Dictionary<int, int> Diplomacy { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> DeathStarSabotage { get; set; } =
                new Dictionary<int, int>();

            public Dictionary<int, int> Espionage { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> Foil { get; set; } = new Dictionary<int, int>();

            public Dictionary<int, int> KillOrCapture { get; set; } = new Dictionary<int, int>();

            public int DefaultSuccessProbability { get; set; } = 50;

            public int DefaultKillOrCaptureProbability { get; set; } = 50;

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
