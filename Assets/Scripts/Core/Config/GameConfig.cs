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
[Serializable]
[PersistableObject]
public class GameConfig
{
    public AIConfig AI { get; set; } = new AIConfig();
    public MovementConfig Movement { get; set; } = new MovementConfig();
    public ProductionConfig Production { get; set; } = new ProductionConfig();
    public PlanetConfig Planet { get; set; } = new PlanetConfig();
    public CombatConfig Combat { get; set; } = new CombatConfig();
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
    [Serializable]
    [PersistableObject]
    public class AIConfig
    {
        /// <summary>Ticks between AI decision cycles (default: 7)</summary>
        public int TickInterval { get; set; } = 7;

        /// <summary>Mission dispatch probability tables (from original MSTB .DAT files)</summary>
        public AIMissionTablesConfig MissionTables { get; set; } = new AIMissionTablesConfig();
    }

    /// <summary>
    /// AI mission dispatch tables.
    /// Each table maps a score to a dispatch probability (0 = don't dispatch).
    /// Score formula: (officer_skill - planet_state) + officer_leadership_rank
    /// Source: DIPLMSTB_DAT, SUBDMSTB_DAT, ESPIMSTB_DAT, INCTMSTB_DAT, RESCMSTB_DAT
    /// </summary>
    [Serializable]
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
    [Serializable]
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
    [Serializable]
    [PersistableObject]
    public class ProductionConfig
    {
        /// <summary>Multiplier for raw to refined materials (default: 50)</summary>
        public int RefinementMultiplier { get; set; } = 50;
    }

    /// <summary>
    /// Planet mechanics configuration.
    /// Controls planet-specific calculations (distance formulas, popularity).
    /// </summary>
    [Serializable]
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
    /// Source: FUN_0055d120_scale_capital_ship_assault_fleet_strength
    /// </summary>
    [Serializable]
    [PersistableObject]
    public class CombatConfig
    {
        /// <summary>
        /// Personnel divisor for assault strength calculation (GENERAL_PARAM_1537).
        /// Formula: assault_strength = (personnel / divisor + 1) * fleet_combat_value
        /// Default: 10 (estimated from typical gameplay values)
        /// </summary>
        public int AssaultPersonnelDivisor { get; set; } = 10;
    }

    /// <summary>
    /// Victory system configuration.
    /// Controls when victory conditions can trigger.
    /// </summary>
    [Serializable]
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
    [Serializable]
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
    [Serializable]
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
    [Serializable]
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

    [Serializable]
    [PersistableObject]
    public class MissionTickConfig
    {
        public int Min { get; set; }
        public int Max { get; set; }
    }

    [Serializable]
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
