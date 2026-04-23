using System.Collections.Generic;
using Rebellion.Util.Attributes;

namespace Rebellion.Generation
{
    /// <summary>
    /// Defines the rules used to generate a new game.
    /// This is the strongly-typed replacement for the old JSON config.
    /// </summary>
    [PersistableObject]
    public class GameGenerationRules
    {
        /// <summary>
        /// Sentinel value for PlanetInstanceID fields (HQFacilityLoadout, FixedGarrison)
        /// that should resolve to the faction's dynamically-assigned HQ at runtime.
        /// </summary>
        public const string FactionHqSentinel = "FACTION_HQ";

        /// <summary>
        /// Rules related to officer generation, including initial officer counts.
        /// </summary>
        public OfficerSection Officers;

        /// <summary>
        /// Galaxy classification rules: faction bucket percentages per difficulty.
        /// </summary>
        public GalaxyClassificationSection GalaxyClassification;

        /// <summary>
        /// Planet resource dice formulas (energy, raw materials) per system type.
        /// </summary>
        public SystemResourcesSection SystemResources;

        /// <summary>
        /// Popular support formulas per faction bucket.
        /// </summary>
        public SystemSupportSection SystemSupport;

        /// <summary>
        /// Facility seeding: weighted tables and mine probability multipliers.
        /// </summary>
        public FacilityGenerationSection FacilityGeneration;

        /// <summary>
        /// Unit deployment: fixed garrisons, fleets, and budget-based deployment.
        /// </summary>
        public UnitDeploymentSection UnitDeployment;
    }

    #region OFFICERS

    /// <summary>
    /// Officer generation rules.
    /// </summary>
    [PersistableObject]
    public class OfficerSection
    {
        /// <summary>
        /// Number of initial officers keyed by galaxy size.
        /// </summary>
        public PlanetSizeProfile NumInitialOfficers;
    }

    #endregion

    #region GALAXY CLASSIFICATION

    [PersistableObject]
    public class GalaxyClassificationSection
    {
        /// <summary>
        /// Per-faction setup: starting planets, garrison troop types, etc.
        /// </summary>
        public List<FactionSetup> FactionSetups;

        /// <summary>
        /// Bucket percentages keyed by difficulty profile name (e.g. "Easy_Alliance").
        /// </summary>
        public List<DifficultyProfile> Profiles;
    }

    [PersistableObject]
    public class FactionSetup
    {
        public string FactionID;
        public string GarrisonTroopTypeID;
        public List<StartingPlanet> StartingPlanets;
    }

    [PersistableObject]
    public class StartingPlanet
    {
        public string PlanetInstanceID;
        public bool IsHeadquarters;
        public int Loyalty = 100;
        public bool PickFromRim;
        public List<string> VisibleToFactionIDs;
    }

    [PersistableObject]
    public class DifficultyProfile
    {
        public string Name;

        /// <summary>
        /// Which faction the player chose. Null or empty means any (fallback).
        /// Original SDPRTB param 7680/7681 values vary by player faction.
        /// </summary>
        public string PlayerFactionID;

        /// <summary>
        /// Difficulty level: 0=Easy, 1=Medium, 2=Hard. -1 means any (fallback).
        /// </summary>
        public int Difficulty = -1;

        /// <summary>
        /// Per-faction bucket size percentages.
        /// </summary>
        public List<FactionBucketConfig> FactionBuckets;
    }

    [PersistableObject]
    public class FactionBucketConfig
    {
        public string FactionID;
        public int StrongPct;
        public int WeakPct;
    }

    #endregion

    #region SYSTEM RESOURCES

    [PersistableObject]
    public class SystemResourcesSection
    {
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

    [PersistableObject]
    public class DiceFormula
    {
        public int Base;
        public int Random1;
        public int Random2;
    }

    #endregion

    #region SYSTEM SUPPORT

    [PersistableObject]
    public class SystemSupportSection
    {
        public SupportFormula Strong;
        public SupportFormula Weak;
        public SupportFormula Neutral;
        public int RimSupportRandom;
    }

    [PersistableObject]
    public class SupportFormula
    {
        public int Base;
        public int Random;
    }

    #endregion

    #region FACILITY GENERATION

    [PersistableObject]
    public class FacilityGenerationSection
    {
        public int CoreMineMultiplier;
        public int RimMineMultiplier;
        public string MineTypeID;
        public List<WeightedFacilityEntry> CoreFacilityTable;
        public List<WeightedFacilityEntry> RimFacilityTable;
        public List<HQFacilityLoadout> HQLoadouts;
    }

    [PersistableObject]
    public class WeightedFacilityEntry
    {
        public int CumulativeWeight;
        public string TypeID;
    }

    [PersistableObject]
    public class HQFacilityLoadout
    {
        public string PlanetInstanceID;
        public string FactionID;
        public List<string> FacilityTypeIDs;
    }

    #endregion

    #region UNIT DEPLOYMENT

    [PersistableObject]
    public class UnitDeploymentSection
    {
        public int UprisingPreventionThreshold;
        public List<FixedGarrison> FixedGarrisons;
        public List<FixedFleet> FixedFleets;
        public List<FactionBudget> FactionBudgets;
    }

    [PersistableObject]
    public class FixedGarrison
    {
        public string PlanetInstanceID;
        public string FactionID;
        public List<UnitEntry> Units;
    }

    [PersistableObject]
    public class FixedFleet
    {
        public string PlanetInstanceID;
        public string FactionID;
        public int SpawnChancePct;
        public List<UnitEntry> Ships;
        public List<UnitEntry> Cargo;
    }

    [PersistableObject]
    public class UnitEntry
    {
        public string TypeID;
        public int Count;
    }

    [PersistableObject]
    public class FactionBudget
    {
        public string FactionID;
        public List<BudgetLevel> BudgetLevels;
        public List<WeightedUnitEntry> UnitTable;
    }

    [PersistableObject]
    public class BudgetLevel
    {
        public int GalaxySize;

        /// <summary>
        /// Difficulty level: 0=Easy, 1=Medium, 2=Hard. -1 means any difficulty (fallback).
        /// From SDPRTB params 5168-5170: Easy gives equal budgets, Medium/Hard give
        /// the AI faction a higher budget than the player faction.
        /// </summary>
        public int Difficulty = -1;

        /// <summary>
        /// If true, this entry applies when the faction is AI-controlled.
        /// If false, applies when the faction is the player's faction (or on Easy where both are equal).
        /// </summary>
        public bool IsAI;
        public int Percentage;
    }

    [PersistableObject]
    public class WeightedUnitEntry
    {
        public int CumulativeWeight;
        public List<UnitEntry> Units;
    }

    #endregion

    #region SHARED

    /// <summary>
    /// Simple mapping keyed by galaxy size.
    /// Used for NumInitialPlanets and NumInitialOfficers.
    /// </summary>
    [PersistableObject]
    public class PlanetSizeProfile
    {
        /// <summary>
        /// Value for a Small galaxy.
        /// </summary>
        public int Small;

        /// <summary>
        /// Value for a Medium galaxy.
        /// </summary>
        public int Medium;

        /// <summary>
        /// Value for a Large galaxy.
        /// </summary>
        public int Large;
    }

    /// <summary>
    /// Represents an inclusive integer min/max range.
    /// </summary>
    [PersistableObject]
    public class IntRange
    {
        /// <summary>
        /// Inclusive minimum value.
        /// </summary>
        public int Min;

        /// <summary>
        /// Inclusive maximum value.
        /// </summary>
        public int Max;
    }

    #endregion
}
