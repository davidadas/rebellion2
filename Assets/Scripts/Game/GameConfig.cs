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
    public VictoryConfig Victory { get; set; } = new VictoryConfig();
    public JediConfig Jedi { get; set; } = new JediConfig();
    public ResearchConfig Research { get; set; } = new ResearchConfig();
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

        /// <summary>Lower bound for fleet deployment probability gate (PARAM_1538).</summary>
        public int DeploymentGateLow { get; set; }

        /// <summary>Upper bound for fleet deployment probability gate (PARAM_1539).</summary>
        public int DeploymentGateHigh { get; set; }

        /// <summary>Capital ship production pipeline parameters.</summary>
        public CapitalShipProductionConfig CapitalShipProduction { get; set; } =
            new CapitalShipProductionConfig();
    }

    /// <summary>
    /// AI capital ship production pipeline configuration.
    /// Controls KDY/LNR facility contribution scaling and strike target evaluation.
    /// Source: GNPRTB params 1537, and strike evaluation constants from disassembly.
    /// </summary>
    [PersistableObject]
    public class CapitalShipProductionConfig
    {
        /// <summary>
        /// Personnel skill divisor for facility production contribution.
        /// Formula: (personnel_skill / divisor + 1) * facility_production_modifier.
        /// Source: GNPR param 1537 (same value as Combat.AssaultPersonnelDivisor).
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

        /// <summary>
        /// UPRIS1 table: maps combined uprising score to result.
        /// </summary>
        public Dictionary<int, int> Upris1Table { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// UPRIS2 table: maps combined uprising score to severity.
        /// </summary>
        public Dictionary<int, int> Upris2Table { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// Popular support shift applied to the controlling faction each uprising resolution tick.
        /// Negative = support drops during an uprising.
        /// </summary>
        public int ControllerSupportShift { get; set; }
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
        /// Dice roll threshold for a planetary assault to succeed (roll must be below this).
        /// </summary>
        public int AssaultSuccessThreshold { get; set; }

        /// <summary>
        /// Upper bound of the dice roll range for planetary assault success checks.
        /// </summary>
        public int AssaultRollRange { get; set; }

        /// <summary>
        /// Low threshold for bombardment strike resistance check.
        /// Strike hits if target resistance is less than random(Low, High).
        /// Source: GENERAL_PARAM 1538.
        /// </summary>
        public int BombardmentStrikeThresholdLow { get; set; }

        /// <summary>
        /// High threshold for bombardment strike resistance check.
        /// Source: GENERAL_PARAM 1539.
        /// </summary>
        public int BombardmentStrikeThresholdHigh { get; set; }

        /// <summary>
        /// Resistance value for the system energy bombardment lane.
        /// Source: GENERAL_PARAM 1540.
        /// </summary>
        public int BombardmentEnergyResistance { get; set; }

        /// <summary>
        /// Minimum number of Shield-class defense facilities required to block bombardment.
        /// </summary>
        public int BombardmentShieldBlockThreshold { get; set; }
    }

    /// <summary>
    /// Victory system configuration.
    /// Controls when victory conditions can trigger.
    /// </summary>
    [PersistableObject]
    public class VictoryConfig
    {
        /// <summary>Minimum tick before victory can trigger (default: 200)</summary>
        public int MinVictoryTick { get; set; }
    }

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
    /// Source: GENERAL_PARAM 7712-7720.
    /// </summary>
    [PersistableObject]
    public class ResourceRebalanceConfig
    {
        /// <summary>Probability multiplier for per-unit resource decay (PARAM_7715). Default: 5.</summary>
        public int DecayMultiplier { get; set; }

        /// <summary>Base delay for rebalance timer in ticks (PARAM_7717).</summary>
        public int RebalanceTimerBase { get; set; }

        /// <summary>Random spread added to rebalance timer (PARAM_7718). Range: 0..spread-1.</summary>
        public int RebalanceTimerSpread { get; set; }

        /// <summary>Base delay for resource walk timer in ticks (PARAM_7719).</summary>
        public int ResourceWalkTimerBase { get; set; }

        /// <summary>Random spread for resource walk timer (PARAM_7720).</summary>
        public int ResourceWalkTimerSpread { get; set; }

        /// <summary>Maximum energy value per planet (PARAM_7714).</summary>
        public int MaxEnergy { get; set; }

        /// <summary>Maximum raw materials value per planet (PARAM_7712).</summary>
        public int MaxRawMaterials { get; set; }
    }
}
