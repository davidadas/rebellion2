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
        /// Rules related to planet generation, including starting planets,
        /// resource allocation ranges, and colonization behavior.
        /// </summary>
        public PlanetSection Planets;

        /// <summary>
        /// Rules related to officer generation, including initial officer counts.
        /// </summary>
        public OfficerSection Officers;

        /// <summary>
        /// Rules related to starting building generation and frequencies.
        /// </summary>
        public BuildingSection Buildings;

        /// <summary>
        /// Rules related to initial capital ship placement.
        /// </summary>
        public CapitalShipSection CapitalShips;
    }

    #region PLANETS

    /// <summary>
    /// Planet generation rules.
    /// Matches the JSON structure where InitialColonizationRate lives under Planets.
    /// </summary>
    [PersistableObject]
    public class PlanetSection
    {
        /// <summary>
        /// Resource allocation ranges by availability level (Limited, Normal, Abundant),
        /// then by system type (CoreSystem, OuterRim).
        /// </summary>
        public ResourceAvailabilityProfile ResourceAvailability;

        /// <summary>
        /// Number of initial planets per faction (or per ruleset, depending on generator usage),
        /// keyed by galaxy size.
        /// </summary>
        public PlanetSizeProfile NumInitialPlanets;

        /// <summary>
        /// Chance that a non-core planet starts colonized.
        /// Core systems are typically forced colonized by generator logic.
        /// </summary>
        public double InitialColonizationRate;
    }

    /// <summary>
    /// Contains resource allocation profiles for each availability setting.
    /// </summary>
    [PersistableObject]
    public class ResourceAvailabilityProfile
    {
        /// <summary>
        /// Limited resources profile.
        /// </summary>
        public ResourceSystemProfile Limited;

        /// <summary>
        /// Normal resources profile.
        /// </summary>
        public ResourceSystemProfile Normal;

        /// <summary>
        /// Abundant resources profile.
        /// </summary>
        public ResourceSystemProfile Abundant;
    }

    /// <summary>
    /// Resource allocation ranges per system type.
    /// </summary>
    [PersistableObject]
    public class ResourceSystemProfile
    {
        /// <summary>
        /// Ranges used when the planet is in a CoreSystem.
        /// </summary>
        public ResourceRanges CoreSystem;

        /// <summary>
        /// Ranges used when the planet is in the OuterRim.
        /// </summary>
        public ResourceRanges OuterRim;
    }

    /// <summary>
    /// Resource-related ranges used when generating planet values.
    /// </summary>
    [PersistableObject]
    public class ResourceRanges
    {
        /// <summary>
        /// Inclusive min/max range for number of ground building slots.
        /// </summary>
        public IntRange GroundSlotRange;

        /// <summary>
        /// Inclusive min/max range for number of orbit building slots.
        /// </summary>
        public IntRange OrbitSlotRange;

        /// <summary>
        /// Inclusive min/max range for energy value (if used by your planet model).
        /// This exists because the original JSON includes EnergyRange under Limited/CoreSystem, etc.
        /// </summary>
        public IntRange EnergyRange;

        /// <summary>
        /// Inclusive min/max range for number of raw resource nodes.
        /// </summary>
        public IntRange ResourceRange;
    }

    #endregion

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

    #region BUILDINGS

    /// <summary>
    /// Building generation rules.
    /// </summary>
    [PersistableObject]
    public class BuildingSection
    {
        /// <summary>
        /// Starting building options and their frequencies.
        /// This replaces the old parallel arrays TypeIDs[] + Frequency[].
        /// </summary>
        public List<InitialBuildingEntry> InitialBuildings;
    }

    /// <summary>
    /// Represents one building type and how frequently it is selected.
    /// </summary>
    [PersistableObject]
    public class InitialBuildingEntry
    {
        /// <summary>
        /// The building type ID.
        /// </summary>
        public string TypeID;

        /// <summary>
        /// The selection probability for this building type.
        /// </summary>
        public double Frequency;
    }

    #endregion

    #region CAPITAL SHIPS

    /// <summary>
    /// Capital ship generation rules.
    /// </summary>
    [PersistableObject]
    public class CapitalShipSection
    {
        /// <summary>
        /// Initial capital ship placements keyed by galaxy size.
        /// </summary>
        public CapitalShipGalaxyProfile InitialCapitalShips;
    }

    /// <summary>
    /// Holds initial capital ship options for each galaxy size.
    /// </summary>
    [PersistableObject]
    public class CapitalShipGalaxyProfile
    {
        /// <summary>
        /// Capital ship options for a Small galaxy.
        /// </summary>
        public List<InitialCapitalShipOption> Small;

        /// <summary>
        /// Capital ship options for a Medium galaxy.
        /// </summary>
        public List<InitialCapitalShipOption> Medium;

        /// <summary>
        /// Capital ship options for a Large galaxy.
        /// </summary>
        public List<InitialCapitalShipOption> Large;
    }

    /// <summary>
    /// Represents one initial capital ship placement.
    /// </summary>
    [PersistableObject]
    public class InitialCapitalShipOption
    {
        /// <summary>
        /// The owning faction instance ID.
        /// </summary>
        public string OwnerInstanceID;

        /// <summary>
        /// The capital ship type ID.
        /// </summary>
        public string TypeID;

        /// <summary>
        /// Optional initial parent instance ID (example: HQ planet instance ID).
        /// If null or empty, generator logic can place it randomly among owned planets.
        /// </summary>
        public string InitialParentInstanceID;
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
