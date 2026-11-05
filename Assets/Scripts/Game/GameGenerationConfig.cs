using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.GameEvents;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Defines the rules used to generate a new game.
    /// </summary>
    [PersistableObject]
    public class GameGenerationConfig
    {
        public const string FactionHqSentinel = "FACTION_HQ";

        public OfficerSection Officers { get; set; }

        public GalaxyClassificationSection GalaxyClassification { get; set; }

        public PlanetResourcesSection PlanetResources { get; set; }

        public PlanetSupportSection PlanetSupport { get; set; }

        public FacilityGenerationSection FacilityGeneration { get; set; }

        public UnitDeploymentSection UnitDeployment { get; set; }

        public BalanceSection Balance { get; set; }

        public List<GameEventCategory> DisabledEventCategories { get; set; } =
            new List<GameEventCategory>();

        public List<FactionModifierConfig> FactionModifiers { get; set; } =
            new List<FactionModifierConfig>();

        /// <summary>
        /// Returns whether one classified event category is enabled by these generation settings.
        /// </summary>
        /// <param name="category">The category to inspect.</param>
        /// <returns>True when events in the category should be included.</returns>
        public bool IsEventCategoryEnabled(GameEventCategory category)
        {
            return category == GameEventCategory.None
                || DisabledEventCategories?.Contains(category) != true;
        }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public GameGenerationConfig CreateCopy()
        {
            GameGenerationConfig copy = (GameGenerationConfig)MemberwiseClone();
            copy.Officers = Officers?.CreateCopy();
            copy.GalaxyClassification = GalaxyClassification?.CreateCopy();
            copy.PlanetResources = PlanetResources?.CreateCopy();
            copy.PlanetSupport = PlanetSupport?.CreateCopy();
            copy.FacilityGeneration = FacilityGeneration?.CreateCopy();
            copy.UnitDeployment = UnitDeployment?.CreateCopy();
            copy.Balance = Balance?.CreateCopy();
            copy.DisabledEventCategories = DisabledEventCategories?.ToList();
            copy.FactionModifiers = FactionModifiers?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines launch-specific settings for one faction.
    /// </summary>
    [PersistableObject]
    public class FactionModifierConfig
    {
        public string FactionID { get; set; }
        public FactionSettings Settings { get; set; } = new FactionSettings();

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public FactionModifierConfig CreateCopy()
        {
            FactionModifierConfig copy = (FactionModifierConfig)MemberwiseClone();
            copy.Settings = Settings?.CreateCopy();
            return copy;
        }
    }

    #region OFFICERS

    /// <summary>
    /// Defines officer counts placed during game generation.
    /// </summary>
    [PersistableObject]
    public class OfficerSection
    {
        public List<StartingOfficerRule> StartingOfficers { get; set; } =
            new List<StartingOfficerRule>();

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public OfficerSection CreateCopy()
        {
            OfficerSection copy = (OfficerSection)MemberwiseClone();
            copy.StartingOfficers = StartingOfficers?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines a guaranteed starting officer, the galaxy sizes where the rule applies, and an
    /// optional fixed deployment destination.
    /// </summary>
    [PersistableObject]
    public class StartingOfficerRule
    {
        public string OfficerInstanceID { get; set; }

        public List<GameSize> GalaxySizes { get; set; } = new List<GameSize>();

        public string DestinationTypeID { get; set; }

        public string DestinationInstanceID { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public StartingOfficerRule CreateCopy()
        {
            StartingOfficerRule copy = (StartingOfficerRule)MemberwiseClone();
            copy.GalaxySizes = GalaxySizes?.ToList();
            return copy;
        }
    }

    #endregion

    #region GALAXY CLASSIFICATION

    /// <summary>
    /// Defines faction setup and starting ownership distribution.
    /// </summary>
    [PersistableObject]
    public class GalaxyClassificationSection
    {
        public List<FactionSetup> FactionSetups { get; set; }
        public List<FactionBucketConfig> FactionBuckets { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public GalaxyClassificationSection CreateCopy()
        {
            GalaxyClassificationSection copy = (GalaxyClassificationSection)MemberwiseClone();
            copy.FactionSetups = FactionSetups?.Select(item => item?.CreateCopy()).ToList();
            copy.FactionBuckets = FactionBuckets?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines starting placement rules for one faction.
    /// </summary>
    [PersistableObject]
    public class FactionSetup
    {
        public string FactionID { get; set; }

        public int StartingResearchLevel { get; set; }

        public PlanetSizeProfile NumStartingOfficers { get; set; }

        public List<StartingPlanet> StartingPlanets { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public FactionSetup CreateCopy()
        {
            FactionSetup copy = (FactionSetup)MemberwiseClone();
            copy.NumStartingOfficers = NumStartingOfficers?.CreateCopy();
            copy.StartingPlanets = StartingPlanets?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines a fixed or selectable starting planet for a faction.
    /// </summary>
    [PersistableObject]
    public class StartingPlanet
    {
        public string PlanetTypeID { get; set; }
        public bool IsHeadquarters { get; set; }
        public int Loyalty { get; set; }
        public bool PickFromRim { get; set; }
        public List<string> VisibleToFactionIDs { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public StartingPlanet CreateCopy()
        {
            StartingPlanet copy = (StartingPlanet)MemberwiseClone();
            copy.VisibleToFactionIDs = VisibleToFactionIDs?.ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines how strongly one faction is represented in generated core sectors.
    /// </summary>
    [PersistableObject]
    public class FactionBucketConfig
    {
        public string FactionID { get; set; }
        public int StrongPct { get; set; }
        public int WeakPct { get; set; }
        public List<FactionBucketOverride> Overrides { get; set; } =
            new List<FactionBucketOverride>();

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public FactionBucketConfig CreateCopy()
        {
            FactionBucketConfig copy = (FactionBucketConfig)MemberwiseClone();
            copy.Overrides = Overrides?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Overrides one faction's ownership percentages for a difficulty and controller role.
    /// </summary>
    [PersistableObject]
    public class FactionBucketOverride
    {
        public GameDifficulty Difficulty { get; set; }
        public PlayerControllerType ControllerType { get; set; }
        public int StrongPct { get; set; }
        public int WeakPct { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public FactionBucketOverride CreateCopy() => (FactionBucketOverride)MemberwiseClone();
    }

    #endregion

    #region PLANET RESOURCES

    /// <summary>
    /// Defines resource profiles used by planet sector generation.
    /// </summary>
    [PersistableObject]
    public class PlanetResourcesSection
    {
        public List<PlanetResourceProfile> Profiles { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public PlanetResourcesSection CreateCopy()
        {
            PlanetResourcesSection copy = (PlanetResourcesSection)MemberwiseClone();
            copy.Profiles = Profiles?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines energy, raw material, and colonization settings for one resource profile.
    /// </summary>
    [PersistableObject]
    public class PlanetResourceProfile
    {
        public GameResourceAvailability Availability { get; set; }
        public DiceFormula CoreEnergy { get; set; }
        public DiceFormula RimEnergy { get; set; }
        public DiceFormula CoreRawMaterials { get; set; }
        public DiceFormula RimRawMaterials { get; set; }
        public int EnergyMin { get; set; }
        public int EnergyMax { get; set; }
        public int RawMaterialsMin { get; set; }
        public int RawMaterialsMax { get; set; }
        public int RimColonizationPct { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public PlanetResourceProfile CreateCopy()
        {
            PlanetResourceProfile copy = (PlanetResourceProfile)MemberwiseClone();
            copy.CoreEnergy = CoreEnergy?.CreateCopy();
            copy.RimEnergy = RimEnergy?.CreateCopy();
            copy.CoreRawMaterials = CoreRawMaterials?.CreateCopy();
            copy.RimRawMaterials = RimRawMaterials?.CreateCopy();
            return copy;
        }
    }

    /// <summary>
    /// Defines a generated integer value with a base and two random terms.
    /// </summary>
    [PersistableObject]
    public class DiceFormula
    {
        public int Base { get; set; }
        public int Random1 { get; set; }
        public int Random2 { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public DiceFormula CreateCopy() => (DiceFormula)MemberwiseClone();
    }

    #endregion

    #region PLANET SUPPORT

    /// <summary>
    /// Defines popular support generation settings for planet ownership buckets.
    /// </summary>
    [PersistableObject]
    public class PlanetSupportSection
    {
        public SupportFormula Strong { get; set; }
        public SupportFormula Weak { get; set; }
        public SupportFormula Neutral { get; set; }
        public int RimSupportRandom { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public PlanetSupportSection CreateCopy()
        {
            PlanetSupportSection copy = (PlanetSupportSection)MemberwiseClone();
            copy.Strong = Strong?.CreateCopy();
            copy.Weak = Weak?.CreateCopy();
            copy.Neutral = Neutral?.CreateCopy();
            return copy;
        }
    }

    /// <summary>
    /// Defines generated popular support with a base and random term.
    /// </summary>
    [PersistableObject]
    public class SupportFormula
    {
        public int Base { get; set; }
        public int Random { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public SupportFormula CreateCopy() => (SupportFormula)MemberwiseClone();
    }

    #endregion

    #region FACILITY GENERATION

    /// <summary>
    /// Defines starting building placement settings.
    /// </summary>
    [PersistableObject]
    public class FacilityGenerationSection
    {
        public int CoreMineMultiplier { get; set; }
        public int RimMineMultiplier { get; set; }
        public string MineTypeID { get; set; }
        public int FacilityTableRollMin { get; set; }
        public int FacilityTableRollMaxExclusive { get; set; }
        public List<WeightedFacilityEntry> CoreFacilityTable { get; set; }
        public List<WeightedFacilityEntry> RimFacilityTable { get; set; }
        public List<HQFacilityLoadout> HQLoadouts { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public FacilityGenerationSection CreateCopy()
        {
            FacilityGenerationSection copy = (FacilityGenerationSection)MemberwiseClone();
            copy.CoreFacilityTable = CoreFacilityTable?.Select(item => item?.CreateCopy()).ToList();
            copy.RimFacilityTable = RimFacilityTable?.Select(item => item?.CreateCopy()).ToList();
            copy.HQLoadouts = HQLoadouts?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines one weighted building entry.
    /// </summary>
    [PersistableObject]
    public class WeightedFacilityEntry
    {
        public int CumulativeWeight { get; set; }
        public string TypeID { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public WeightedFacilityEntry CreateCopy() => (WeightedFacilityEntry)MemberwiseClone();
    }

    /// <summary>
    /// Defines fixed starting buildings for a headquarters planet.
    /// </summary>
    [PersistableObject]
    public class HQFacilityLoadout
    {
        public string PlanetTypeID { get; set; }
        public string FactionID { get; set; }
        public List<string> FacilityTypeIDs { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public HQFacilityLoadout CreateCopy()
        {
            HQFacilityLoadout copy = (HQFacilityLoadout)MemberwiseClone();
            copy.FacilityTypeIDs = FacilityTypeIDs?.ToList();
            return copy;
        }
    }

    #endregion

    #region UNIT DEPLOYMENT

    /// <summary>
    /// Defines starting unit placement settings.
    /// </summary>
    [PersistableObject]
    public class UnitDeploymentSection
    {
        public int UprisingPreventionThreshold { get; set; }

        public int SupportDeficitPerGarrisonTroop { get; set; } = 10;
        public List<FixedGarrison> FixedGarrisons { get; set; }
        public List<FixedFleet> FixedFleets { get; set; }
        public List<FactionBudget> FactionBudgets { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public UnitDeploymentSection CreateCopy()
        {
            UnitDeploymentSection copy = (UnitDeploymentSection)MemberwiseClone();
            copy.FixedGarrisons = FixedGarrisons?.Select(item => item?.CreateCopy()).ToList();
            copy.FixedFleets = FixedFleets?.Select(item => item?.CreateCopy()).ToList();
            copy.FactionBudgets = FactionBudgets?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines fixed starting ground units for one planet.
    /// </summary>
    [PersistableObject]
    public class FixedGarrison
    {
        public string PlanetTypeID { get; set; }
        public string FactionID { get; set; }
        public List<UnitEntry> Units { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public FixedGarrison CreateCopy()
        {
            FixedGarrison copy = (FixedGarrison)MemberwiseClone();
            copy.Units = Units?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines a fixed starting fleet for one planet.
    /// </summary>
    [PersistableObject]
    public class FixedFleet
    {
        public string PlanetTypeID { get; set; }
        public List<string> TargetPlanets { get; set; }
        public string FactionID { get; set; }
        public int SpawnChancePct { get; set; }
        public List<FixedFleetShip> ShipEntries { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public FixedFleet CreateCopy()
        {
            FixedFleet copy = (FixedFleet)MemberwiseClone();
            copy.TargetPlanets = TargetPlanets?.ToList();
            copy.ShipEntries = ShipEntries?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines one capital ship entry and its starting cargo.
    /// </summary>
    [PersistableObject]
    public class FixedFleetShip
    {
        public string TypeID { get; set; }
        public int Count { get; set; }
        public List<UnitEntry> Cargo { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public FixedFleetShip CreateCopy()
        {
            FixedFleetShip copy = (FixedFleetShip)MemberwiseClone();
            copy.Cargo = Cargo?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines a unit type and count.
    /// </summary>
    [PersistableObject]
    public class UnitEntry
    {
        public string TypeID { get; set; }
        public int Count { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public UnitEntry CreateCopy() => (UnitEntry)MemberwiseClone();
    }

    /// <summary>
    /// Defines maintenance-budget unit placement for one faction.
    /// </summary>
    [PersistableObject]
    public class FactionBudget
    {
        public string FactionID { get; set; }
        public List<BudgetLevel> BudgetLevels { get; set; }
        public List<WeightedUnitEntry> UnitTable { get; set; }
        public List<StartingUnitBudgetBonus> Bonuses { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public FactionBudget CreateCopy()
        {
            FactionBudget copy = (FactionBudget)MemberwiseClone();
            copy.BudgetLevels = BudgetLevels?.Select(item => item?.CreateCopy()).ToList();
            copy.UnitTable = UnitTable?.Select(item => item?.CreateCopy()).ToList();
            copy.Bonuses = Bonuses?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Identifies the unit category eligible for an additional starting budget.
    /// </summary>
    public enum StartingUnitBudgetCategory
    {
        Starfighter,
        Regiment,
    }

    /// <summary>
    /// Defines an additional starting-unit allocation for one difficulty and controller role.
    /// </summary>
    [PersistableObject]
    public class StartingUnitBudgetBonus
    {
        public GameDifficulty Difficulty { get; set; }
        public bool AIOnly { get; set; }
        public StartingUnitBudgetCategory Category { get; set; }
        public List<BudgetLevel> BudgetLevels { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public StartingUnitBudgetBonus CreateCopy()
        {
            StartingUnitBudgetBonus copy = (StartingUnitBudgetBonus)MemberwiseClone();
            copy.BudgetLevels = BudgetLevels?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Defines the budget percentage used for one galaxy size.
    /// </summary>
    [PersistableObject]
    public class BudgetLevel
    {
        public int GalaxySize { get; set; }
        public int Percentage { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public BudgetLevel CreateCopy() => (BudgetLevel)MemberwiseClone();
    }

    /// <summary>
    /// Defines one weighted unit bundle.
    /// </summary>
    [PersistableObject]
    public class WeightedUnitEntry
    {
        public int Weight { get; set; }
        public List<UnitEntry> Units { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public WeightedUnitEntry CreateCopy()
        {
            WeightedUnitEntry copy = (WeightedUnitEntry)MemberwiseClone();
            copy.Units = Units?.Select(item => item?.CreateCopy()).ToList();
            return copy;
        }
    }

    #endregion

    #region BALANCE

    /// <summary>
    /// Stores support adjustment values in serialized generation settings.
    /// </summary>
    [PersistableObject]
    public class BalanceSection
    {
        public int SupportBoostPerUnit { get; set; } = 2;

        public int MaxMilitaryPresenceBoost { get; set; } = 10;

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public BalanceSection CreateCopy() => (BalanceSection)MemberwiseClone();
    }

    #endregion

    #region SHARED

    /// <summary>
    /// Defines values by galaxy size.
    /// </summary>
    [PersistableObject]
    public class PlanetSizeProfile
    {
        public int Small { get; set; }
        public int Medium { get; set; }
        public int Large { get; set; }

        /// <summary>
        /// Creates an independent snapshot of these settings.
        /// </summary>
        /// <returns>A copy retaining all values and owning its mutable settings and collections.</returns>
        public PlanetSizeProfile CreateCopy() => (PlanetSizeProfile)MemberwiseClone();
    }

    #endregion
}
