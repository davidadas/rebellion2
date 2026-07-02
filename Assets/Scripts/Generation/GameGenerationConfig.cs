using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Util.Serialization;

namespace Rebellion.Generation
{
    /// <summary>
    /// Defines the rules used to generate a new game.
    /// </summary>
    [PersistableObject]
    public class GameGenerationConfig
    {
        /// <summary>
        /// Planet type ID used by config entries that resolve to a faction headquarters.
        /// </summary>
        public const string FactionHqSentinel = "FACTION_HQ";

        /// <summary>Officer generation settings.</summary>
        public OfficerSection Officers;

        /// <summary>Galaxy ownership and faction placement settings.</summary>
        public GalaxyClassificationSection GalaxyClassification;

        /// <summary>Planet energy and raw material settings.</summary>
        public SystemResourcesSection SystemResources;

        /// <summary>Popular support generation settings.</summary>
        public SystemSupportSection SystemSupport;

        /// <summary>Starting facility placement settings.</summary>
        public FacilityGenerationSection FacilityGeneration;

        /// <summary>Starting unit placement settings.</summary>
        public UnitDeploymentSection UnitDeployment;

        /// <summary>Post-generation balance settings.</summary>
        public BalanceSection Balance;
    }

    #region OFFICERS

    /// <summary>
    /// Defines officer counts placed during game generation.
    /// </summary>
    [PersistableObject]
    public class OfficerSection
    {
        public PlanetSizeProfile NumInitialOfficers;
    }

    #endregion

    #region GALAXY CLASSIFICATION

    /// <summary>
    /// Defines faction setup and difficulty-specific ownership profiles.
    /// </summary>
    [PersistableObject]
    public class GalaxyClassificationSection
    {
        public List<FactionSetup> FactionSetups;
        public List<DifficultyProfile> Profiles;
    }

    /// <summary>
    /// Defines starting placement rules for one faction.
    /// </summary>
    [PersistableObject]
    public class FactionSetup
    {
        public string FactionID;
        public string GarrisonTroopTypeID;
        public List<StartingPlanet> StartingPlanets;
    }

    /// <summary>
    /// Defines a fixed or selectable starting planet for a faction.
    /// </summary>
    [PersistableObject]
    public class StartingPlanet
    {
        public string PlanetTypeID;
        public bool IsHeadquarters;
        public int Loyalty;
        public bool PickFromRim;
        public List<string> VisibleToFactionIDs;
    }

    /// <summary>
    /// Defines ownership distribution for a difficulty profile.
    /// </summary>
    [PersistableObject]
    public class DifficultyProfile
    {
        public string Name;

        /// <summary>
        /// The faction the player chose. Null or empty means any (fallback).
        /// </summary>
        public string PlayerFactionID;

        /// <summary>
        /// The difficulty level this profile applies to. -1 means any (fallback).
        /// </summary>
        public int Difficulty = -1;

        public List<FactionBucketConfig> FactionBuckets;
    }

    /// <summary>
    /// Defines how strongly one faction is represented in generated core systems.
    /// </summary>
    [PersistableObject]
    public class FactionBucketConfig
    {
        public string FactionID;
        public int StrongPct;
        public int WeakPct;
    }

    #endregion

    #region SYSTEM RESOURCES

    /// <summary>
    /// Defines resource profiles used by planet system generation.
    /// </summary>
    [PersistableObject]
    public class SystemResourcesSection
    {
        public List<SystemResourceProfile> Profiles;
    }

    /// <summary>
    /// Defines energy, raw material, and colonization settings for one resource profile.
    /// </summary>
    [PersistableObject]
    public class SystemResourceProfile
    {
        public GameResourceAvailability Availability;
        public DiceFormula CoreEnergy;
        public DiceFormula RimEnergy;
        public DiceFormula CoreRawMaterials;
        public DiceFormula RimRawMaterials;
        public int EnergyMin;
        public int EnergyMax;
        public int RawMaterialsMin;
        public int RawMaterialsMax;
        public int RimColonizationPct;
    }

    /// <summary>
    /// Defines a generated integer value with a base and two random terms.
    /// </summary>
    [PersistableObject]
    public class DiceFormula
    {
        public int Base;
        public int Random1;
        public int Random2;
    }

    #endregion

    #region SYSTEM SUPPORT

    /// <summary>
    /// Defines popular support generation settings for system ownership buckets.
    /// </summary>
    [PersistableObject]
    public class SystemSupportSection
    {
        public SupportFormula Strong;
        public SupportFormula Weak;
        public SupportFormula Neutral;
        public int RimSupportRandom;
    }

    /// <summary>
    /// Defines generated popular support with a base and random term.
    /// </summary>
    [PersistableObject]
    public class SupportFormula
    {
        public int Base;
        public int Random;
    }

    #endregion

    #region FACILITY GENERATION

    /// <summary>
    /// Defines starting building placement settings.
    /// </summary>
    [PersistableObject]
    public class FacilityGenerationSection
    {
        public int CoreMineMultiplier;
        public int RimMineMultiplier;
        public string MineTypeID;
        public int FacilityTableRollMin;
        public int FacilityTableRollMaxExclusive;
        public List<WeightedFacilityEntry> CoreFacilityTable;
        public List<WeightedFacilityEntry> RimFacilityTable;
        public List<HQFacilityLoadout> HQLoadouts;
    }

    /// <summary>
    /// Defines one weighted building entry.
    /// </summary>
    [PersistableObject]
    public class WeightedFacilityEntry
    {
        public int CumulativeWeight;
        public string TypeID;
    }

    /// <summary>
    /// Defines fixed starting buildings for a headquarters planet.
    /// </summary>
    [PersistableObject]
    public class HQFacilityLoadout
    {
        public string PlanetTypeID;
        public string FactionID;
        public List<string> FacilityTypeIDs;
    }

    #endregion

    #region UNIT DEPLOYMENT

    /// <summary>
    /// Defines starting unit placement settings.
    /// </summary>
    [PersistableObject]
    public class UnitDeploymentSection
    {
        public int UprisingPreventionThreshold;

        /// <summary>
        /// Each garrison troop counters this much popular-support deficit when seeding
        /// uprising-prevention garrisons.
        /// </summary>
        public int SupportDeficitPerGarrisonTroop = 10;
        public List<BudgetDifficultyMapping> BudgetDifficultyMappings;
        public List<FixedGarrison> FixedGarrisons;
        public List<FixedFleet> FixedFleets;
        public List<FactionBudget> FactionBudgets;
    }

    /// <summary>
    /// Maps a game difficulty to a unit deployment budget difficulty.
    /// </summary>
    [PersistableObject]
    public class BudgetDifficultyMapping
    {
        public int Difficulty;
        public int BudgetDifficulty;
    }

    /// <summary>
    /// Defines fixed starting ground units for one planet.
    /// </summary>
    [PersistableObject]
    public class FixedGarrison
    {
        public string PlanetTypeID;
        public string FactionID;
        public List<UnitEntry> Units;
    }

    /// <summary>
    /// Defines a fixed starting fleet for one planet.
    /// </summary>
    [PersistableObject]
    public class FixedFleet
    {
        public string PlanetTypeID;
        public List<string> TargetPlanets;
        public string FactionID;
        public int SpawnChancePct;
        public List<FixedFleetShip> ShipEntries;
    }

    [PersistableObject]
    public class FixedFleetShip
    {
        public string TypeID;
        public int Count;
        public List<UnitEntry> Cargo;
    }

    /// <summary>
    /// Defines a unit type and count.
    /// </summary>
    [PersistableObject]
    public class UnitEntry
    {
        public string TypeID;
        public int Count;
    }

    /// <summary>
    /// Defines maintenance-budget unit placement for one faction.
    /// </summary>
    [PersistableObject]
    public class FactionBudget
    {
        public string FactionID;
        public List<BudgetLevel> BudgetLevels;
        public List<WeightedUnitEntry> UnitTable;
    }

    /// <summary>
    /// Defines the budget percentage used for one galaxy size, difficulty, and controller type.
    /// </summary>
    [PersistableObject]
    public class BudgetLevel
    {
        public int GalaxySize;

        /// <summary>
        /// The difficulty level this budget level applies to. -1 means any (fallback).
        /// </summary>
        public int Difficulty = -1;

        /// <summary>
        /// If true, this entry applies when the faction is AI-controlled.
        /// If false, applies when the faction is the player's faction (or on Easy where both are equal).
        /// </summary>
        public bool IsAI;
        public int Percentage;
    }

    /// <summary>
    /// Defines one weighted unit bundle.
    /// </summary>
    [PersistableObject]
    public class WeightedUnitEntry
    {
        public int CumulativeWeight;
        public List<UnitEntry> Units;
    }

    #endregion

    #region BALANCE

    /// <summary>
    /// Defines post-generation support adjustment settings.
    /// </summary>
    [PersistableObject]
    public class BalanceSection
    {
        /// <summary>
        /// Each unit of military presence (regiment, fleet, or starfighter) boosts the
        /// owner's popular support by this many points.
        /// </summary>
        public int SupportBoostPerUnit = 2;

        /// <summary>
        /// Upper bound on the total support boost a single planet can receive from
        /// military presence, regardless of how many units are stationed there.
        /// </summary>
        public int MaxMilitaryPresenceBoost = 10;
    }

    #endregion

    #region SHARED

    /// <summary>
    /// Defines values by galaxy size.
    /// </summary>
    [PersistableObject]
    public class PlanetSizeProfile
    {
        public int Small;
        public int Medium;
        public int Large;
    }

    /// <summary>
    /// Defines an inclusive integer range.
    /// </summary>
    [PersistableObject]
    public class IntRange
    {
        public int Min;
        public int Max;
    }

    #endregion
}
