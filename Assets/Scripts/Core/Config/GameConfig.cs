using System;
using System.Collections.Generic;
using Rebellion.Util.Attributes;
using Rebellion.Util.Common;

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
    public PlanetConfig Planet { get; set; } = new PlanetConfig();
    public CombatConfig Combat { get; set; } = new CombatConfig();
    public UprisingConfig Uprising { get; set; } = new UprisingConfig();
    public SupportShiftConfig SupportShift { get; set; } = new SupportShiftConfig();
    public VictoryConfig Victory { get; set; } = new VictoryConfig();
    public JediConfig Jedi { get; set; } = new JediConfig();
    public ProbabilityTablesConfig ProbabilityTables { get; set; } = new ProbabilityTablesConfig();

    /// <summary>
    /// Validates configuration values for sanity.
    /// Throws InvalidOperationException if critical values are invalid.
    /// Call this after deserialization or before use.
    /// </summary>
    public void Validate()
    {
        // Movement validation
        if (Movement.DistanceScale <= 0)
        {
            throw new InvalidOperationException(
                "GameConfig.Movement.DistanceScale must be positive"
            );
        }
        if (Movement.MinTransitTicks < 0)
        {
            throw new InvalidOperationException(
                "GameConfig.Movement.MinTransitTicks cannot be negative"
            );
        }
        if (Movement.DefaultFighterHyperdrive <= 0)
        {
            throw new InvalidOperationException(
                "GameConfig.Movement.DefaultFighterHyperdrive must be positive"
            );
        }

        // AI validation
        if (AI.TickInterval <= 0)
        {
            throw new InvalidOperationException("GameConfig.AI.TickInterval must be positive");
        }

        // Production validation
        if (Production.RefinementMultiplier <= 0)
        {
            throw new InvalidOperationException(
                "GameConfig.Production.RefinementMultiplier must be positive"
            );
        }

        // Planet validation
        if (Planet.DistanceDivisor <= 0)
        {
            throw new InvalidOperationException(
                "GameConfig.Planet.DistanceDivisor must be positive"
            );
        }
        if (Planet.MaxPopularSupport <= 0)
        {
            throw new InvalidOperationException(
                "GameConfig.Planet.MaxPopularSupport must be positive"
            );
        }

        // Victory validation
        if (Victory.MinVictoryTick < 0)
        {
            throw new InvalidOperationException(
                "GameConfig.Victory.MinVictoryTick cannot be negative"
            );
        }
    }

    /// <summary>
    /// AI system configuration.
    /// Controls AI decision-making and mission dispatch.
    /// </summary>
    [PersistableObject]
    public class AIConfig
    {
        /// <summary>Ticks between AI decision cycles (default: 7)</summary>
        public int TickInterval { get; set; } = 7;

        /// <summary>Mission dispatch probability tables.</summary>
        public AIMissionTablesConfig MissionTables { get; set; } = new AIMissionTablesConfig();

        /// <summary>Garrison requirement parameters.</summary>
        public GarrisonConfig Garrison { get; set; } = new GarrisonConfig();

        /// <summary>Lower bound for fleet deployment probability gate (PARAM_1538).</summary>
        public int DeploymentGateLow { get; set; } = 20;

        /// <summary>Upper bound for fleet deployment probability gate (PARAM_1539).</summary>
        public int DeploymentGateHigh { get; set; } = 80;
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
        public int SupportThreshold { get; set; } = 60;

        /// <summary>
        /// Divisor for garrison calculation: ceil((threshold - support) / divisor).
        /// </summary>
        public int GarrisonDivisor { get; set; } = 10;

        /// <summary>
        /// Multiplier applied to garrison requirement during an uprising.
        /// </summary>
        public int UprisingMultiplier { get; set; } = 2;
    }

    /// <summary>
    /// Uprising system configuration.
    /// Controls dice rolls, table lookups, and garrison-based uprising resolution.
    /// </summary>
    [PersistableObject]
    public class UprisingConfig
    {
        /// <summary>Dice roll range (random 0..N). Default: 10 (rolls 0-9).</summary>
        public int DiceRange { get; set; } = 10;

        /// <summary>Addend to each dice roll. Default: 1 (so rolls are 1-9).</summary>
        public int DiceAddend { get; set; } = 1;

        /// <summary>
        /// UPRIS1 table: maps combined uprising score to result.
        /// Score >= 10 -> 2, >= 6 -> 1, >= 1 -> 0.
        /// </summary>
        public Dictionary<int, int> Upris1Table { get; set; } =
            new Dictionary<int, int>
            {
                { 1, 0 },
                { 6, 1 },
                { 10, 2 },
            };

        /// <summary>
        /// UPRIS2 table: maps combined uprising score to severity.
        /// Score >= 12 -> 5, >= 11 -> 4, >= 9 -> 3, >= 1 -> 0.
        /// </summary>
        public Dictionary<int, int> Upris2Table { get; set; } =
            new Dictionary<int, int>
            {
                { 1, 0 },
                { 9, 3 },
                { 11, 4 },
                { 12, 5 },
            };
    }

    /// <summary>
    /// Support shift configuration.
    /// Controls periodic popular support recovery and hostile force penalties.
    /// </summary>
    [PersistableObject]
    public class SupportShiftConfig
    {
        /// <summary>Support must be at or below this for shift to apply. Default: 40.</summary>
        public int ShiftThreshold { get; set; } = 40;

        /// <summary>Lower boundary of middle bracket. Default: 20.</summary>
        public int LowBracketCeiling { get; set; } = 20;

        /// <summary>Upper boundary of middle bracket. Default: 30.</summary>
        public int MidBracketCeiling { get; set; } = 30;

        /// <summary>Base shift for support 0-20. Default: 75.</summary>
        public int LowBracketShift { get; set; } = 75;

        /// <summary>Base shift for support 21-30. Default: 50.</summary>
        public int MidBracketShift { get; set; } = 50;

        /// <summary>Base shift for support 31-40. Default: 25.</summary>
        public int HighBracketShift { get; set; } = 25;

        /// <summary>Penalty per hostile fleet. Default: 10.</summary>
        public int FleetPenalty { get; set; } = 10;

        /// <summary>Penalty per hostile fighter squadron. Default: 5.</summary>
        public int FighterPenalty { get; set; } = 5;

        /// <summary>Penalty per (adjusted) hostile troop. Default: 2.</summary>
        public int TroopPenalty { get; set; } = 2;

        /// <summary>Support shift when blockading fleet matches popular support side. Default: 1.</summary>
        public int BlockadeMatchShift { get; set; } = 1;

        /// <summary>Support shift when blockading fleet opposes popular support side. Default: -1.</summary>
        public int BlockadeOpposeShift { get; set; } = -1;
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
        /// <summary>Distance scaling factor for transit time (default: 2)</summary>
        public int DistanceScale { get; set; } = 2;

        /// <summary>Minimum transit ticks regardless of distance (default: 10)</summary>
        public int MinTransitTicks { get; set; } = 10;

        /// <summary>Default hyperdrive rating for fighters/troops (default: 60)</summary>
        public int DefaultFighterHyperdrive { get; set; } = 60;
    }

    /// <summary>
    /// Production system configuration.
    /// Controls manufacturing and resource refinement.
    /// </summary>
    [PersistableObject]
    public class ProductionConfig
    {
        /// <summary>Multiplier for raw to refined materials (default: 50)</summary>
        public int RefinementMultiplier { get; set; } = 50;

        /// <summary>Production penalty per hostile capital ship during blockade (default: 5).</summary>
        public int BlockadeCapitalShipPenalty { get; set; } = 5;

        /// <summary>Production penalty per hostile fighter during blockade (default: 2).</summary>
        public int BlockadeFighterPenalty { get; set; } = 2;

        /// <summary>
        /// CSCRHT table: maps roll threshold to progress increment for capital ship production.
        /// Original game values: 0->2, 16->3, 31->4, 41->5, 46->6.
        /// </summary>
        public Dictionary<int, int> CapitalShipProgressTable { get; set; } =
            new Dictionary<int, int>
            {
                { 0, 2 },
                { 16, 3 },
                { 31, 4 },
                { 41, 5 },
                { 46, 6 },
            };

        /// <summary>Range for the CSCRHT progress roll (GENERAL_PARAM_2051, default: 50).</summary>
        public int CapitalShipProgressRollRange { get; set; } = 50;

        /// <summary>Threshold for the success check roll (GENERAL_PARAM_2050, default: 50).</summary>
        public int CapitalShipSuccessThreshold { get; set; } = 50;

        /// <summary>Range for the success check roll (GENERAL_PARAM_2049, default: 50).</summary>
        public int CapitalShipSuccessRollRange { get; set; } = 50;

        /// <summary>Popular support shift when a capital ship completes (default: 3).</summary>
        public int CapitalShipCompletionSupportShift { get; set; } = 3;

        /// <summary>Popular support shift when a building completes (default: 1).</summary>
        public int BuildingCompletionSupportShift { get; set; } = 1;

        /// <summary>Popular support shift when a troop completes (default: 1).</summary>
        public int TroopCompletionSupportShift { get; set; } = 1;
    }

    /// <summary>
    /// Planet mechanics configuration.
    /// Controls planet-specific calculations (distance formulas, popularity).
    /// </summary>
    [PersistableObject]
    public class PlanetConfig
    {
        /// <summary>Distance divisor for planet formulas (default: 5)</summary>
        public int DistanceDivisor { get; set; } = 5;

        /// <summary>Base value for distance calculations (default: 100)</summary>
        public int DistanceBase { get; set; } = 100;

        /// <summary>Maximum popular support value (default: 100)</summary>
        public int MaxPopularSupport { get; set; } = 100;
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
        public int AssaultPersonnelDivisor { get; set; } = 40;

        /// <summary>
        /// Dice roll threshold for a planetary assault to succeed (roll must be below this).
        /// </summary>
        public int AssaultSuccessThreshold { get; set; } = 50;

        /// <summary>
        /// Upper bound of the dice roll range for planetary assault success checks.
        /// </summary>
        public int AssaultRollRange { get; set; } = 100;
    }

    /// <summary>
    /// Victory system configuration.
    /// Controls when victory conditions can trigger.
    /// </summary>
    [PersistableObject]
    public class VictoryConfig
    {
        /// <summary>Minimum tick before victory can trigger (default: 200)</summary>
        public int MinVictoryTick { get; set; } = 200;
    }

    /// <summary>
    /// Jedi / Force training system configuration.
    /// Controls Force tier advancement and detection mechanics.
    /// </summary>
    [PersistableObject]
    public class JediConfig
    {
        /// <summary>XP required to advance from Aware to Training tier (default: 50)</summary>
        public int XpToTraining { get; set; } = 50;

        /// <summary>XP required to advance from Training to Experienced tier (default: 150)</summary>
        public int XpToExperienced { get; set; } = 150;

        /// <summary>Ticks between Jedi detection checks (default: 30)</summary>
        public int DetectionCheckInterval { get; set; } = 30;

        /// <summary>Detection probability per check for Aware tier (default: 0.05 = 5%)</summary>
        public double DetectProbAware { get; set; } = 0.05;

        /// <summary>Detection probability per check for Training tier (default: 0.15 = 15%)</summary>
        public double DetectProbTraining { get; set; } = 0.15;

        /// <summary>Detection probability per check for Experienced tier (default: 0.30 = 30%)</summary>
        public double DetectProbExperienced { get; set; } = 0.30;
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
    }

    [PersistableObject]
    public class MissionTickConfig
    {
        public int Min { get; set; }
        public int Max { get; set; }
    }

    [PersistableObject]
    public class MissionTickRangesConfig
    {
        public MissionTickConfig Abduction { get; set; } =
            new MissionTickConfig { Min = 15, Max = 20 };
        public MissionTickConfig Assassination { get; set; } =
            new MissionTickConfig { Min = 15, Max = 20 };
        public MissionTickConfig Diplomacy { get; set; } =
            new MissionTickConfig { Min = 5, Max = 10 };
        public MissionTickConfig Espionage { get; set; } =
            new MissionTickConfig { Min = 10, Max = 20 };
        public MissionTickConfig InciteUprising { get; set; } =
            new MissionTickConfig { Min = 10, Max = 20 };
        public MissionTickConfig Recruitment { get; set; } =
            new MissionTickConfig { Min = 15, Max = 20 };
        public MissionTickConfig Rescue { get; set; } =
            new MissionTickConfig { Min = 10, Max = 20 };
        public MissionTickConfig Sabotage { get; set; } =
            new MissionTickConfig { Min = 10, Max = 15 };
        public MissionTickConfig SubdueUprising { get; set; } =
            new MissionTickConfig { Min = 10, Max = 15 };
    }
}
