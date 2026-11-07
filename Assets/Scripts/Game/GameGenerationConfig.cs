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
        public GameGenerationConfig CreateCopy() =>
            new GameGenerationConfig
            {
                Officers = Officers?.CreateCopy(),
                GalaxyClassification = GalaxyClassification?.CreateCopy(),
                PlanetResources = PlanetResources?.CreateCopy(),
                PlanetSupport = PlanetSupport?.CreateCopy(),
                FacilityGeneration = FacilityGeneration?.CreateCopy(),
                UnitDeployment = UnitDeployment?.CreateCopy(),
                Balance = Balance?.CreateCopy(),
                DisabledEventCategories = DisabledEventCategories?.ToList(),
                FactionModifiers = FactionModifiers?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public FactionModifierConfig CreateCopy() =>
            new FactionModifierConfig { FactionID = FactionID, Settings = Settings?.CreateCopy() };
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
        public OfficerSection CreateCopy() =>
            new OfficerSection
            {
                StartingOfficers = StartingOfficers?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public StartingOfficerRule CreateCopy() =>
            new StartingOfficerRule
            {
                OfficerInstanceID = OfficerInstanceID,
                GalaxySizes = GalaxySizes?.ToList(),
                DestinationTypeID = DestinationTypeID,
                DestinationInstanceID = DestinationInstanceID,
            };
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
        public GalaxyClassificationSection CreateCopy() =>
            new GalaxyClassificationSection
            {
                FactionSetups = FactionSetups?.Select(item => item?.CreateCopy()).ToList(),
                FactionBuckets = FactionBuckets?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public FactionSetup CreateCopy() =>
            new FactionSetup
            {
                FactionID = FactionID,
                StartingResearchLevel = StartingResearchLevel,
                NumStartingOfficers = NumStartingOfficers?.CreateCopy(),
                StartingPlanets = StartingPlanets?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public StartingPlanet CreateCopy() =>
            new StartingPlanet
            {
                PlanetTypeID = PlanetTypeID,
                IsHeadquarters = IsHeadquarters,
                Loyalty = Loyalty,
                PickFromRim = PickFromRim,
                VisibleToFactionIDs = VisibleToFactionIDs?.ToList(),
            };
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
        public FactionBucketConfig CreateCopy() =>
            new FactionBucketConfig
            {
                FactionID = FactionID,
                StrongPct = StrongPct,
                WeakPct = WeakPct,
                Overrides = Overrides?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public FactionBucketOverride CreateCopy() =>
            new FactionBucketOverride
            {
                Difficulty = Difficulty,
                ControllerType = ControllerType,
                StrongPct = StrongPct,
                WeakPct = WeakPct,
            };
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
        public PlanetResourcesSection CreateCopy() =>
            new PlanetResourcesSection
            {
                Profiles = Profiles?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public PlanetResourceProfile CreateCopy() =>
            new PlanetResourceProfile
            {
                Availability = Availability,
                CoreEnergy = CoreEnergy?.CreateCopy(),
                RimEnergy = RimEnergy?.CreateCopy(),
                CoreRawMaterials = CoreRawMaterials?.CreateCopy(),
                RimRawMaterials = RimRawMaterials?.CreateCopy(),
                EnergyMin = EnergyMin,
                EnergyMax = EnergyMax,
                RawMaterialsMin = RawMaterialsMin,
                RawMaterialsMax = RawMaterialsMax,
                RimColonizationPct = RimColonizationPct,
            };
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
        public DiceFormula CreateCopy() =>
            new DiceFormula
            {
                Base = Base,
                Random1 = Random1,
                Random2 = Random2,
            };
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
        public PlanetSupportSection CreateCopy() =>
            new PlanetSupportSection
            {
                Strong = Strong?.CreateCopy(),
                Weak = Weak?.CreateCopy(),
                Neutral = Neutral?.CreateCopy(),
                RimSupportRandom = RimSupportRandom,
            };
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
        public SupportFormula CreateCopy() => new SupportFormula { Base = Base, Random = Random };
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
        public FacilityGenerationSection CreateCopy() =>
            new FacilityGenerationSection
            {
                CoreMineMultiplier = CoreMineMultiplier,
                RimMineMultiplier = RimMineMultiplier,
                MineTypeID = MineTypeID,
                FacilityTableRollMin = FacilityTableRollMin,
                FacilityTableRollMaxExclusive = FacilityTableRollMaxExclusive,
                CoreFacilityTable = CoreFacilityTable?.Select(item => item?.CreateCopy()).ToList(),
                RimFacilityTable = RimFacilityTable?.Select(item => item?.CreateCopy()).ToList(),
                HQLoadouts = HQLoadouts?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public WeightedFacilityEntry CreateCopy() =>
            new WeightedFacilityEntry { CumulativeWeight = CumulativeWeight, TypeID = TypeID };
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
        public HQFacilityLoadout CreateCopy() =>
            new HQFacilityLoadout
            {
                PlanetTypeID = PlanetTypeID,
                FactionID = FactionID,
                FacilityTypeIDs = FacilityTypeIDs?.ToList(),
            };
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
        public UnitDeploymentSection CreateCopy() =>
            new UnitDeploymentSection
            {
                UprisingPreventionThreshold = UprisingPreventionThreshold,
                SupportDeficitPerGarrisonTroop = SupportDeficitPerGarrisonTroop,
                FixedGarrisons = FixedGarrisons?.Select(item => item?.CreateCopy()).ToList(),
                FixedFleets = FixedFleets?.Select(item => item?.CreateCopy()).ToList(),
                FactionBudgets = FactionBudgets?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public FixedGarrison CreateCopy() =>
            new FixedGarrison
            {
                PlanetTypeID = PlanetTypeID,
                FactionID = FactionID,
                Units = Units?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public FixedFleet CreateCopy() =>
            new FixedFleet
            {
                PlanetTypeID = PlanetTypeID,
                TargetPlanets = TargetPlanets?.ToList(),
                FactionID = FactionID,
                SpawnChancePct = SpawnChancePct,
                ShipEntries = ShipEntries?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public FixedFleetShip CreateCopy() =>
            new FixedFleetShip
            {
                TypeID = TypeID,
                Count = Count,
                Cargo = Cargo?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public UnitEntry CreateCopy() => new UnitEntry { TypeID = TypeID, Count = Count };
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
        public FactionBudget CreateCopy() =>
            new FactionBudget
            {
                FactionID = FactionID,
                BudgetLevels = BudgetLevels?.Select(item => item?.CreateCopy()).ToList(),
                UnitTable = UnitTable?.Select(item => item?.CreateCopy()).ToList(),
                Bonuses = Bonuses?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public StartingUnitBudgetBonus CreateCopy() =>
            new StartingUnitBudgetBonus
            {
                Difficulty = Difficulty,
                AIOnly = AIOnly,
                Category = Category,
                BudgetLevels = BudgetLevels?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public BudgetLevel CreateCopy() =>
            new BudgetLevel { GalaxySize = GalaxySize, Percentage = Percentage };
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
        public WeightedUnitEntry CreateCopy() =>
            new WeightedUnitEntry
            {
                Weight = Weight,
                Units = Units?.Select(item => item?.CreateCopy()).ToList(),
            };
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
        public BalanceSection CreateCopy() =>
            new BalanceSection
            {
                SupportBoostPerUnit = SupportBoostPerUnit,
                MaxMilitaryPresenceBoost = MaxMilitaryPresenceBoost,
            };
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
        public PlanetSizeProfile CreateCopy() =>
            new PlanetSizeProfile
            {
                Small = Small,
                Medium = Medium,
                Large = Large,
            };
    }

    #endregion
}
