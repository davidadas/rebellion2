using System.Collections.Generic;
using Rebellion.Util.Attributes;

namespace Rebellion.Generation
{
    /// <summary>
    /// Defines the rules used to generate a new game.
    /// This is the strongly-typed replacement for the old JSON config.
    /// </summary>
    [PersistableObject]
    public class GameGenerationConfig
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

        /// <summary>
        /// Post-seeding balance pass: HQ loyalty pinning and military-presence boosts.
        /// </summary>
        public BalanceSection Balance;
    }

    #region OFFICERS

    [PersistableObject]
    public class OfficerSection
    {
        public PlanetSizeProfile NumInitialOfficers;
    }

    #endregion

    #region GALAXY CLASSIFICATION

    [PersistableObject]
    public class GalaxyClassificationSection
    {
        public List<FactionSetup> FactionSetups;
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
        /// The faction the player chose. Null or empty means any (fallback).
        /// </summary>
        public string PlayerFactionID;

        /// <summary>
        /// The difficulty level this profile applies to. -1 means any (fallback).
        /// </summary>
        public int Difficulty = -1;

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

        /// <summary>
        /// Each garrison troop counters this much popular-support deficit when seeding
        /// uprising-prevention garrisons.
        /// </summary>
        public int SupportDeficitPerGarrisonTroop = 10;
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

    [PersistableObject]
    public class WeightedUnitEntry
    {
        public int CumulativeWeight;
        public List<UnitEntry> Units;
    }

    #endregion

    #region BALANCE

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

    [PersistableObject]
    public class PlanetSizeProfile
    {
        public int Small;
        public int Medium;
        public int Large;
    }

    [PersistableObject]
    public class IntRange
    {
        public int Min;
        public int Max;
    }

    #endregion
}
