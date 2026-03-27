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
    public VictoryConfig Victory { get; set; } = new VictoryConfig();
    public JediConfig Jedi { get; set; } = new JediConfig();
    public ProbabilityTablesConfig ProbabilityTables { get; set; } = new ProbabilityTablesConfig();

    /// <summary>
    /// Validates configuration values for sanity.
    /// Throws GameException if critical values are invalid.
    /// Call this after deserialization or before use.
    /// </summary>
    public void Validate()
    {
        // Movement validation
        if (Movement.DistanceScale <= 0)
        {
            throw new GameException("GameConfig.Movement.DistanceScale must be positive");
        }
        if (Movement.MinTransitTicks < 0)
        {
            throw new GameException("GameConfig.Movement.MinTransitTicks cannot be negative");
        }
        if (Movement.DefaultFighterHyperdrive <= 0)
        {
            throw new GameException(
                "GameConfig.Movement.DefaultFighterHyperdrive must be positive"
            );
        }

        // AI validation
        if (AI.TickInterval <= 0)
        {
            throw new GameException("GameConfig.AI.TickInterval must be positive");
        }
        if (AI.MaxAttackFronts < 0)
        {
            throw new GameException("GameConfig.AI.MaxAttackFronts cannot be negative");
        }

        // Production validation
        if (Production.RefinementMultiplier <= 0)
        {
            throw new GameException("GameConfig.Production.RefinementMultiplier must be positive");
        }

        // Planet validation
        if (Planet.DistanceDivisor <= 0)
        {
            throw new GameException("GameConfig.Planet.DistanceDivisor must be positive");
        }
        if (Planet.MaxPopularSupport <= 0)
        {
            throw new GameException("GameConfig.Planet.MaxPopularSupport must be positive");
        }

        // Victory validation
        if (Victory.MinVictoryTick < 0)
        {
            throw new GameException("GameConfig.Victory.MinVictoryTick cannot be negative");
        }
    }

    /// <summary>
    /// AI system configuration.
    /// Controls AI decision-making, targeting, and mission dispatch.
    /// </summary>
    [Serializable]
    [PersistableObject]
    public class AIConfig
    {
        /// <summary>Ticks between AI decision cycles (default: 7)</summary>
        public int TickInterval { get; set; } = 7;

        /// <summary>Minimum skill required for diplomacy missions (default: 60)</summary>
        public int DiplomacySkillThreshold { get; set; } = 60;

        /// <summary>Target popularity cap for diplomacy missions (default: 0.8)</summary>
        public float DiplomacyTargetPopularityCap { get; set; } = 0.8f;

        /// <summary>Minimum skill required for espionage missions (default: 30)</summary>
        public int EspionageSkillThreshold { get; set; } = 30;

        /// <summary>Minimum success probability for covert missions (default: 0.5)</summary>
        public double CovertMinSuccessProbability { get; set; } = 0.5;

        /// <summary>Maximum simultaneous attack fronts (default: 3)</summary>
        public int MaxAttackFronts { get; set; } = 3;

        /// <summary>Ticks before AI can attack same system again (default: 100)</summary>
        public float BattleCooldownTicks { get; set; } = 100f;

        /// <summary>Distance divisor for proximity scoring (default: 100)</summary>
        public float ProximityDivisor { get; set; } = 100f;

        /// <summary>Weight factor for target weakness in scoring (default: 0.30)</summary>
        public float WeightWeakness { get; set; } = 0.30f;

        /// <summary>Weight factor for target proximity in scoring (default: 0.30)</summary>
        public float WeightProximity { get; set; } = 0.30f;

        /// <summary>Weight factor for deconfliction in scoring (default: 0.25)</summary>
        public float WeightDeconfliction { get; set; } = 0.25f;

        /// <summary>Weight factor for battle freshness in scoring (default: 0.15)</summary>
        public float WeightFreshness { get; set; } = 0.15f;

        /// <summary>Popularity threshold for covert ops targeting (default: 0.3)</summary>
        public float CovertTargetPopularityThreshold { get; set; } = 0.3f;
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
        public Dictionary<int, int> Diplomacy { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> DeathStarSabotage { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Espionage { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> InciteUprising { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Recruitment { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Rescue { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> Sabotage { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> SubdueUprising { get; set; } = new Dictionary<int, int>();
    }
}
