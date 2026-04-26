using System;
using System.Collections.Generic;
using Rebellion.Util.Attributes;

/// <summary>
/// Runtime simulation configuration.
/// Loaded from Resources/Configs/GameConfig.xml on every game start/load.
/// Changes to this file apply to ALL saves (NOT frozen into save files).
///
/// DO NOT serialize this with GameSummary - it should reload from XML every time.
/// Player choices belong in GameSummary.
/// New game setup belongs in GameGenerationRules.xml.
/// </summary>
[PersistableObject]
public class GameConfig
{
    public AIConfig AI { get; set; } = new AIConfig();
    public MovementConfig Movement { get; set; } = new MovementConfig();
    public ProductionConfig Production { get; set; } = new ProductionConfig();
    public ResourceRebalanceConfig ResourceRebalance { get; set; } = new ResourceRebalanceConfig();
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
    public ProbabilityTablesConfig ProbabilityTables { get; set; } = new ProbabilityTablesConfig();

    /// <summary>
    /// AI system configuration.
    /// Controls AI decision-making and mission dispatch.
    /// </summary>
    [PersistableObject]
    public class AIConfig
    {
        /// <summary>Ticks between AI decision cycles.</summary>
        public int TickInterval { get; set; }

        /// <summary>Mission dispatch probability tables.</summary>
        public AIMissionTablesConfig MissionTables { get; set; } = new AIMissionTablesConfig();

        /// <summary>Garrison requirement parameters.</summary>
        public GarrisonConfig Garrison { get; set; } = new GarrisonConfig();

        /// <summary>Lower bound for fleet deployment probability gate.</summary>
        public int DeploymentGateLow { get; set; }

        /// <summary>Upper bound for fleet deployment probability gate.</summary>
        public int DeploymentGateHigh { get; set; }

        /// <summary>Capital ship production pipeline parameters.</summary>
        public CapitalShipProductionConfig CapitalShipProduction { get; set; } =
            new CapitalShipProductionConfig();

        /// <summary>Unit selection parameters for AI manufacturing.</summary>
        public AISelectionConfig Selection { get; set; } = new AISelectionConfig();

        /// <summary>Infrastructure shortage parameters for AI manufacturing.</summary>
        public AIInfrastructureConfig Infrastructure { get; set; } = new AIInfrastructureConfig();
    }

    [PersistableObject]
    public class AISelectionConfig
    {
        public int CandidatePoolSize { get; set; }
        public int RecentBuildHistoryLimit { get; set; }
        public int RepeatBuildPenaltyPerSelection { get; set; }
        public int LocalDuplicatePenaltyPerSelection { get; set; }
        public int MaxFleetCapitalBeforeSplit { get; set; }
        public int MaxDuplicateCapitalTypePerFleet { get; set; }
        public int MaxDuplicateStarfighterTypePerFleet { get; set; }
        public int MaxDuplicateRegimentTypePerDestination { get; set; }
        public int PremiumCapitalConstructionCostThreshold { get; set; }
        public int MaxPremiumCapitalsPerFaction { get; set; }
        public int CapitalConstructionCostWeight { get; set; }
        public int CapitalMaintenanceCostWeight { get; set; }
        public int CapitalCombatWeight { get; set; }
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
        public int RegimentGarrisonDefenseBoost { get; set; }
        public int RegimentFleetAttackBoost { get; set; }
    }

    [PersistableObject]
    public class AIInfrastructureConfig
    {
        public int PlanetsPerConstructionFacility { get; set; }
        public int PlanetsPerShipyard { get; set; }
        public int PlanetsPerTrainingFacility { get; set; }
        public int MaxDefensePerPlanet { get; set; }
    }

    /// <summary>
    /// AI capital ship production pipeline configuration.
    /// Controls KDY/LNR facility contribution scaling and strike target evaluation.
    /// </summary>
    [PersistableObject]
    public class CapitalShipProductionConfig
    {
        /// <summary>
        /// Personnel skill divisor for facility production contribution.
        /// Formula: (personnel_skill / divisor + 1) * facility_production_modifier.
        /// </summary>
        public int FacilityPersonnelDivisor { get; set; }

        /// <summary>
        /// Low threshold for strike target evaluation.
        /// Planets with energy below this are not considered strike targets.
        /// </summary>
        public int StrikeThresholdLow { get; set; }

        /// <summary>
        /// High threshold for strike target evaluation.
        /// Planets with energy at or above this are prioritized as strike targets.
        /// </summary>
        public int StrikeThresholdHigh { get; set; }

        /// <summary>
        /// Energy resistance value for strike evaluation on unallocated energy planets.
        /// </summary>
        public int EnergyStrikeResistance { get; set; }

        /// <summary>
        /// Energy resistance value for strike evaluation on planets with allocated energy.
        /// </summary>
        public int AllocatedEnergyStrikeResistance { get; set; }

        /// <summary>
        /// Support shift applied to PRODUCING faction's own popular support (Death Star only).
        /// Negative = military disruption at construction site.
        /// Only applied when enableFinalizePackage is set (Death Star found in setup stage).
        /// </summary>
        public int OrbitalStrikeSupportShift { get; set; }
    }

    /// <summary>
    /// Garrison requirement configuration.
    /// Controls how many troops a planet needs based on popular support.
    /// </summary>
    [PersistableObject]
    public class GarrisonConfig
    {
        /// <summary>
        /// Popular support threshold below which garrison troops are required.
        /// </summary>
        public int SupportThreshold { get; set; }

        /// <summary>
        /// Divisor for garrison calculation: ceil((threshold - support) / divisor).
        /// </summary>
        public int GarrisonDivisor { get; set; }

        /// <summary>
        /// Multiplier applied to garrison requirement during an uprising.
        /// </summary>
        public int UprisingMultiplier { get; set; }

        /// <summary>
        /// Maximum remaining planet garrison deficit that still allows loading regiments into fleets.
        /// </summary>
        public int FleetLoadingDeficitThreshold { get; set; }
    }

    /// <summary>
    /// Uprising system configuration.
    /// Controls dice rolls, table lookups, and garrison-based uprising resolution.
    /// </summary>
    [PersistableObject]
    public class UprisingConfig
    {
        /// <summary>Dice roll range (random 0..N).</summary>
        public int DiceRange { get; set; }

        /// <summary>Addend to each dice roll.</summary>
        public int DiceAddend { get; set; }

        /// <summary>Maps uprising score to a property damage consequence code.</summary>
        public Dictionary<int, int> PrimaryConsequenceTable { get; set; } =
            new Dictionary<int, int>();

        /// <summary>Maps uprising score to a personnel consequence code.</summary>
        public Dictionary<int, int> SecondaryConsequenceTable { get; set; } =
            new Dictionary<int, int>();

        /// <summary>
        /// Popular support shift applied to the controlling faction each uprising resolution tick.
        /// Negative = support drops during an uprising.
        /// </summary>
        public int ControllerSupportShift { get; set; }

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
    /// Support shift configuration.
    /// Controls periodic popular support recovery and hostile force penalties.
    /// </summary>
    [PersistableObject]
    public class SupportShiftConfig
    {
        /// <summary>Support must be at or below this for shift to apply.</summary>
        public int ShiftThreshold { get; set; }

        /// <summary>Lower boundary of middle bracket.</summary>
        public int LowBracketCeiling { get; set; }

        /// <summary>Upper boundary of middle bracket.</summary>
        public int MidBracketCeiling { get; set; }

        /// <summary>Base shift for support in low bracket.</summary>
        public int LowBracketShift { get; set; }

        /// <summary>Base shift for support in mid bracket.</summary>
        public int MidBracketShift { get; set; }

        /// <summary>Base shift for support in high bracket.</summary>
        public int HighBracketShift { get; set; }

        /// <summary>Penalty per hostile fleet.</summary>
        public int FleetPenalty { get; set; }

        /// <summary>Penalty per hostile fighter squadron.</summary>
        public int FighterPenalty { get; set; }

        /// <summary>Penalty per (adjusted) hostile troop.</summary>
        public int TroopPenalty { get; set; }

        /// <summary>Popular support threshold above which a neutral planet transfers to the faction.</summary>
        public int OwnershipTransferThreshold { get; set; }

        /// <summary>Support shift when blockading fleet matches popular support side.</summary>
        public int BlockadeMatchShift { get; set; }

        /// <summary>Support shift when blockading fleet opposes popular support side.</summary>
        public int BlockadeOpposeShift { get; set; }
    }

    /// <summary>
    /// AI mission dispatch tables.
    /// Each table maps a score to a dispatch probability (0 = don't dispatch).
    /// Score formula: (officer_skill - planet_state) + officer_leadership_rank
    /// Source: DIPLMSTB_DAT, SUBDMSTB_DAT, ESPIMSTB_DAT, INCTMSTB_DAT, RESCMSTB_DAT
    /// </summary>
    [PersistableObject]
    public class AIMissionTablesConfig
    {
        /// <summary>Diplomacy dispatch table (DIPLMSTB_DAT). Score = (diplomacy - popular_support) + rank</summary>
        public Dictionary<int, int> Diplomacy { get; set; } = new Dictionary<int, int>();

        /// <summary>SubdueUprising dispatch table (SUBDMSTB_DAT). Score = (combat - popular_support) + rank</summary>
        public Dictionary<int, int> SubdueUprising { get; set; } = new Dictionary<int, int>();

        /// <summary>Espionage dispatch table (ESPIMSTB_DAT). Score = officer.espionage</summary>
        public Dictionary<int, int> Espionage { get; set; } = new Dictionary<int, int>();

        /// <summary>InciteUprising dispatch table (INCTMSTB_DAT). Score = (espionage - enemy_support) - enemy_strength</summary>
        public Dictionary<int, int> InciteUprising { get; set; } = new Dictionary<int, int>();

        /// <summary>Rescue dispatch table (RESCMSTB_DAT). Score = captured_officer.combat</summary>
        public Dictionary<int, int> Rescue { get; set; } = new Dictionary<int, int>();

        /// <summary>Sabotage dispatch table (SBTGMSTB_DAT). Score = (attacker.espionage + defender.combat) / 2</summary>
        public Dictionary<int, int> Sabotage { get; set; } = new Dictionary<int, int>();

        /// <summary>Abduction dispatch table (ABDCMSTB_DAT). Score = attacker.combat - target.combat</summary>
        public Dictionary<int, int> Abduction { get; set; } = new Dictionary<int, int>();

        /// <summary>Assassination dispatch table (ASSNMSTB_DAT). Score = attacker.combat - target.combat</summary>
        public Dictionary<int, int> Assassination { get; set; } = new Dictionary<int, int>();

        /// <summary>Recruitment dispatch table (RCRTMSTB_DAT). Score = officer.diplomacy</summary>
        public Dictionary<int, int> Recruitment { get; set; } = new Dictionary<int, int>();
    }

    /// <summary>
    /// Movement system configuration.
    /// Controls fleet transit time and hyperdrive calculations.
    /// </summary>
    [PersistableObject]
    public class MovementConfig
    {
        /// <summary>Distance scaling factor for transit time.</summary>
        public int DistanceScale { get; set; }

        /// <summary>Minimum transit ticks regardless of distance.</summary>
        public int MinTransitTicks { get; set; }

        /// <summary>Default hyperdrive rating for fighters/troops.</summary>
        public int DefaultFighterHyperdrive { get; set; }
    }

    /// <summary>
    /// Production system configuration.
    /// Controls manufacturing and resource refinement.
    /// </summary>
    [PersistableObject]
    public class ProductionConfig
    {
        /// <summary>Multiplier for raw to refined materials.</summary>
        public int RefinementMultiplier { get; set; }

        /// <summary>Ticks between maintenance shortfall auto-scrap attempts.</summary>
        public int MaintenanceShortfallAutoscrapInterval { get; set; }

        /// <summary>Production penalty per hostile capital ship during blockade.</summary>
        public int BlockadeCapitalShipPenalty { get; set; }

        /// <summary>Production penalty per hostile fighter during blockade.</summary>
        public int BlockadeFighterPenalty { get; set; }

        /// <summary>
        /// CSCRHT table: maps roll threshold to progress increment for capital ship production.
        /// </summary>
        public Dictionary<int, int> CapitalShipProgressTable { get; set; } =
            new Dictionary<int, int>();

        /// <summary>Range for the CSCRHT progress roll.</summary>
        public int CapitalShipProgressRollRange { get; set; }

        /// <summary>Threshold for the success check roll.</summary>
        public int CapitalShipSuccessThreshold { get; set; }

        /// <summary>Range for the success check roll.</summary>
        public int CapitalShipSuccessRollRange { get; set; }

        /// <summary>Popular support shift when a capital ship completes.</summary>
        public int CapitalShipCompletionSupportShift { get; set; }

        /// <summary>Popular support shift when a building completes.</summary>
        public int BuildingCompletionSupportShift { get; set; }

        /// <summary>Popular support shift when a troop completes.</summary>
        public int TroopCompletionSupportShift { get; set; }
    }

    /// <summary>
    /// Planet mechanics configuration.
    /// Controls planet-specific calculations (distance formulas, popularity).
    /// </summary>
    [PersistableObject]
    public class PlanetConfig
    {
        /// <summary>Distance divisor for planet formulas.</summary>
        public int DistanceDivisor { get; set; }

        /// <summary>Base value for distance calculations.</summary>
        public int DistanceBase { get; set; }

        /// <summary>Maximum popular support value.</summary>
        public int MaxPopularSupport { get; set; }
    }

    /// <summary>
    /// Combat system configuration.
    /// Controls fleet assault strength calculations and combat resolution.
    /// </summary>
    [PersistableObject]
    public class CombatConfig
    {
        /// <summary>
        /// Personnel divisor for assault strength calculation.
        /// Formula: assault_strength = (personnel / divisor + 1) * fleet_combat_value.
        /// </summary>
        public int AssaultPersonnelDivisor { get; set; }

        /// <summary>
        /// Low threshold for bombardment strike resistance check.
        /// Strike hits if target resistance is less than random(Low, High).
        /// </summary>
        public int BombardmentStrikeThresholdLow { get; set; }

        /// <summary>
        /// High threshold for bombardment strike resistance check.
        /// </summary>
        public int BombardmentStrikeThresholdHigh { get; set; }

        /// <summary>
        /// Resistance value for the system energy bombardment lane.
        /// </summary>
        public int BombardmentEnergyResistance { get; set; }

        /// <summary>
        /// Minimum number of Shield-class defense facilities required to block bombardment.
        /// </summary>
        public int BombardmentShieldBlockThreshold { get; set; }

        /// <summary>
        /// Divisor applied to a defense facility's ProductionModifier when rolling its chance
        /// to fire back at an attacking ship during stage 2 of planetary defense combat.
        /// </summary>
        public int DefenseFacilityResponseDivisor { get; set; }

        /// <summary>
        /// Probability (out of 100) that a single repeat trial succeeds during stage 4
        /// of planetary defense combat.
        /// </summary>
        public int RepeatTrialProbability { get; set; }

        public int GroundCombatCommanderDivisor { get; set; }

        public int GroundCombatContestDiceRange { get; set; }

        public int GroundCombatDefenderWinsThreshold { get; set; }

        public int GroundCombatAttackerWinsThreshold { get; set; }

        public int WeaponDamageVariancePercent { get; set; }

        public int FighterDogfightLossRatePercent { get; set; }

        public int FighterDamageBasePercent { get; set; }

        public int FighterDamageSpreadPercent { get; set; }
    }

    /// <summary>
    /// Blockade system configuration.
    /// Controls evacuation losses when units depart blockaded planets.
    /// </summary>
    [PersistableObject]
    public class BlockadeConfig
    {
        /// <summary>Percent chance each regiment is destroyed when evacuating through a blockade.</summary>
        public int EvacuationLossPercent { get; set; }
    }

    /// <summary>
    /// Victory system configuration.
    /// </summary>
    [PersistableObject]
    public class VictoryConfig { }

    /// <summary>
    /// Jedi / Force training system configuration.
    /// Controls Force tier advancement and detection mechanics.
    /// </summary>
    [PersistableObject]
    public class JediConfig
    {
        /// <summary>ForceRank threshold to enter "discovering force user" state.</summary>
        public int DiscoveringForceUserThreshold { get; set; }

        /// <summary>ForceRank threshold for full Jedi qualification.</summary>
        public int ForceQualifiedThreshold { get; set; }

        /// <summary>ForceRank threshold for Force-based fast healing.</summary>
        public int FastHealThreshold { get; set; }

        /// <summary>ForceValue increment per successful mission.</summary>
        public int ForceGrowthPerMission { get; set; }

        /// <summary>Percent of rank gap used as catch-up range in training.</summary>
        public int TrainingCatchUpPercent { get; set; }

        /// <summary>ForceRank threshold for Luke to learn heritage.</summary>
        public int HeritageThreshold { get; set; }

        /// <summary>Percent bonus to ForceRank on Dagobah mission completion.</summary>
        public int DagobahCompletionBonusPercent { get; set; }

        /// <summary>Local force user minimum rank for encounter eligibility.</summary>
        public int EncounterLocalMinRank { get; set; }

        /// <summary>Cross-side force user minimum rank for encounter eligibility.</summary>
        public int EncounterCrossSideMinRank { get; set; }

        /// <summary>Offset applied to encounter probability calculation.</summary>
        public int EncounterProbabilityOffset { get; set; }

        /// <summary>ForceRank threshold for Novice label.</summary>
        public int RankLabelNovice { get; set; }

        /// <summary>ForceRank threshold for Trainee label.</summary>
        public int RankLabelTrainee { get; set; }

        /// <summary>ForceRank threshold for ForceStudent label.</summary>
        public int RankLabelForceStudent { get; set; }

        /// <summary>ForceRank threshold for ForceKnight label.</summary>
        public int RankLabelForceKnight { get; set; }

        /// <summary>ForceRank threshold for ForceMaster label.</summary>
        public int RankLabelForceMaster { get; set; }
    }

    /// <summary>
    /// Research system configuration.
    /// Controls technology advancement rates and officer research mechanics.
    /// </summary>
    [Serializable]
    [PersistableObject]
    public class ResearchConfig
    {
        /// <summary>Base research points awarded per successful research mission.</summary>
        public int BaseResearchPoints { get; set; }

        /// <summary>Random bonus range: award random(0, DiceRange) extra points on success.</summary>
        public int ResearchDiceRange { get; set; }
    }

    [PersistableObject]
    public class AssassinationConfig
    {
        public int BaseInjury { get; set; }
        public int PrimaryInjuryRange { get; set; }
        public int SecondaryInjuryRange { get; set; }
        public int KillProbability { get; set; }
    }

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

    [PersistableObject]
    public class CaptiveConfig
    {
        public Dictionary<int, int> EscapeTable { get; set; } = new Dictionary<int, int>();
        public int EscapeLoyaltyShift { get; set; }
    }

    /// <summary>
    /// Probability tables configuration.
    /// All probability tables grouped by data type (not usage).
    /// </summary>
    [PersistableObject]
    public class ProbabilityTablesConfig
    {
        /// <summary>Non-mission probability tables</summary>
        public Dictionary<int, int> UprisingStart { get; set; } = new Dictionary<int, int>();

        /// <summary>Mission-related probability tables</summary>
        public MissionProbabilityTablesConfig Mission { get; set; } =
            new MissionProbabilityTablesConfig();
    }

    /// <summary>
    /// Mission-specific probability tables.
    /// </summary>
    [PersistableObject]
    public class MissionProbabilityTablesConfig
    {
        public Dictionary<int, int> Abduction { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Assassination { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Decoy { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Diplomacy { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> DeathStarSabotage { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Espionage { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Foil { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> InciteUprising { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Recruitment { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Rescue { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Sabotage { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> SubdueUprising { get; set; } = new Dictionary<int, int>();
        public MissionTickRangesConfig TickRanges { get; set; } = new MissionTickRangesConfig();

        /// <summary>
        /// Returns the success probability table for the given mission config key, or null.
        /// </summary>
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

    [PersistableObject]
    public class MissionTickConfig
    {
        /// <summary>Guaranteed minimum ticks before mission executes.</summary>
        public int Base { get; set; }

        /// <summary>Random spread added to base: total = Base + random(0, Spread) inclusive.</summary>
        public int Spread { get; set; }
    }

    [PersistableObject]
    public class MissionTickRangesConfig
    {
        public MissionTickConfig Abduction { get; set; } = new MissionTickConfig();
        public MissionTickConfig Assassination { get; set; } = new MissionTickConfig();
        public MissionTickConfig Diplomacy { get; set; } = new MissionTickConfig();
        public MissionTickConfig Espionage { get; set; } = new MissionTickConfig();
        public MissionTickConfig InciteUprising { get; set; } = new MissionTickConfig();
        public MissionTickConfig Recruitment { get; set; } = new MissionTickConfig();
        public MissionTickConfig Rescue { get; set; } = new MissionTickConfig();
        public MissionTickConfig Sabotage { get; set; } = new MissionTickConfig();
        public MissionTickConfig SubdueUprising { get; set; } = new MissionTickConfig();
        public MissionTickConfig Research { get; set; } = new MissionTickConfig();
        public MissionTickConfig JediTraining { get; set; } = new MissionTickConfig();

        /// <summary>
        /// Returns the tick config for the given mission config key, or null.
        /// </summary>
        /// <param name="key">Mission config key name (e.g. "Diplomacy").</param>
        /// <returns>The matching MissionTickConfig, or null.</returns>
        public MissionTickConfig GetTickConfig(string key)
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
                "Research" => Research,
                "JediTraining" => JediTraining,
                _ => null,
            };
        }
    }

    /// <summary>
    /// Resource rebalance configuration.
    /// Controls periodic resource decay, facility suspension, and resource random walk.
    /// </summary>
    [PersistableObject]
    public class ResourceRebalanceConfig
    {
        /// <summary>Probability multiplier for per-unit resource decay. Default: 5.</summary>
        public int DecayMultiplier { get; set; }

        /// <summary>Base delay for rebalance timer in ticks.</summary>
        public int RebalanceTimerBase { get; set; }

        /// <summary>Random spread added to rebalance timer. Range: 0..spread-1.</summary>
        public int RebalanceTimerSpread { get; set; }

        /// <summary>Base delay for resource walk timer in ticks.</summary>
        public int ResourceWalkTimerBase { get; set; }

        /// <summary>Random spread for resource walk timer.</summary>
        public int ResourceWalkTimerSpread { get; set; }

        /// <summary>Maximum energy value per planet.</summary>
        public int MaxEnergy { get; set; }

        /// <summary>Maximum raw materials value per planet.</summary>
        public int MaxRawMaterials { get; set; }
    }
}
