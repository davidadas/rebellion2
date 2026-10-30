using System.Collections.Generic;

/// <summary>
/// Supplies authoring guidance for serialized fields without changing their data names.
/// </summary>
internal static class ContentEditorFieldDocumentation
{
    private static readonly IReadOnlyDictionary<string, string> _descriptions = new Dictionary<
        string,
        string
    >
    {
        ["TypeID"] =
            "Identifies this definition. Other definitions and events use this ID to reference it.",
        ["InstanceID"] =
            "Identifies this specific character, planet, or faction. Use a unique ID within the pack.",
        ["HQInstanceID"] = "The planet instance ID identifying this faction's headquarters.",
        ["DisallowedMissionTypeIDs"] = "Checked missions cannot be started by this faction.",
        ["GarrisonTroopTypeID"] =
            "The regiment type used when this faction creates automatic garrisons.",
        ["RecruitingFactionInstanceIDs"] = "Factions allowed to recruit this officer.",
        ["ManufacturingFactionInstanceIDs"] = "Factions allowed to build this unit or facility.",
        ["AllowedRanks"] = "Command assignments this officer is eligible to hold.",
        ["NamePoolID"] =
            "Ships use the pool matching their Ship name pool ID. Each name is used in list order.",
        ["FallbackNamePoolID"] = "The pool to use after this pool runs out of unused names.",
        ["ShipNamePoolID"] = "The faction ship-name pool used for this ship design.",
        ["NextNameIndex"] = "The next name to use from this list. Zero starts with the first name.",
        ["SmallDisplayImagePath"] =
            "Compact artwork used in unit lists and other small-card views.",
        ["FleetListIconImagePath"] =
            "The compact fleet image used in fleet lists and the idle bar.",
        ["FleetTileImagePath"] = "The larger fleet tile image.",
        ["FleetConstructionSmallImagePath"] = "The compact icon for ships under construction.",
        ["FleetConstructionLargeImagePath"] = "The larger image for ships under construction.",
        ["Traits"] =
            "Starting traits assigned to this officer. Definitions are edited under Officer traits.",
        ["CanBetray"] = "Allows this officer's loyalty checks to betray a mission.",
        ["Loyalty"] =
            "The officer's loyalty rating. Lower loyalty increases the risk of mission betrayal.",
        ["IsRecruitable"] = "Whether this officer can become available through recruitment.",
        ["IsMain"] =
            "Marks this officer as a main character, affecting character-specific mission and injury rules.",
        ["IsKnownJedi"] = "Whether the character's Force abilities have been revealed.",
        ["IsJediTrainer"] = "Whether this character can train other Force users.",
        ["GrowsForceOnMission"] =
            "Allows eligible missions to increase this character's Force abilities.",
        ["JediLevel"] = "The character's starting Force rating.",
        ["JediProbability"] =
            "The percentage chance that the character starts with Force abilities.",
        ["ConstructionCost"] = "Refined materials needed to build one unit.",
        ["MaintenanceCost"] = "The maintenance capacity consumed by one unit.",
        ["ResearchOrder"] = "Determines the unit's place in the research progression.",
        ["ResearchDifficulty"] = "Research points required to unlock this design.",
        ["BaseBuildSpeed"] =
            "The base value used by this unit's manufacturing-progress calculation.",
        ["Hyperdrive"] =
            "Controls hyperspace travel duration together with distance and movement rules.",
        ["DistanceDivisor"] =
            "Divides travel distance when calculating transit duration. Larger values shorten trips.",
        ["MinTransitTicks"] = "The minimum number of turns for a trip between different sectors.",
        ["SameSectorMinTransitTicks"] = "The minimum number of turns for a trip within one sector.",
        ["PositionX"] = "The horizontal map coordinate. Dragging the map item updates this value.",
        ["PositionY"] = "The vertical map coordinate. Dragging the map item updates this value.",
        ["IsColonized"] = "Whether the planet is marked as colonized in this definition.",
        ["Visibility"] = "The smallest galaxy size in which this sector is available.",
        ["SectorType"] = "Classifies the sector as Core or Outer Rim for world generation.",
        ["Importance"] = "The sector's strategic importance classification.",
        ["MissionSuccessChancePoints"] =
            "Percentage points added to mission success chances for the affected difficulty.",
        ["MissionFoilChancePoints"] =
            "Percentage points added to the chance of missions being foiled for the affected difficulty.",
        ["MissionExecutionSpeedIncreasePercent"] =
            "The mission execution speed bonus for the affected difficulty.",
        ["PersonnelTravelTimePercent"] =
            "The percentage of normal personnel travel time used when this trait applies.",
        ["SharesPersonnelTravelTimeModifier"] =
            "Applies this trait's travel-time modifier to accompanying personnel.",
        ["LeadershipBonus"] = "Leadership points added while this trait is active.",
        ["BasePackID"] = "The installed pack that supplies files not overridden by this pack.",
        ["Version"] =
            "This content pack's version, used to identify installed updates and save requirements.",
    };

    /// <summary>
    /// Gets verified guidance for one field, including common media and variance controls.
    /// </summary>
    /// <param name="field">The authored field.</param>
    /// <returns>The help text, or an empty string for undocumented extensions.</returns>
    internal static string GetDescription(ContentPackEditorField field)
    {
        if (_descriptions.TryGetValue(field.MemberName, out string text))
            return text;
        if (field.MemberName.EndsWith("Variance", System.StringComparison.Ordinal))
            return "The random starting variation applied to this rating during world generation.";
        if (field.IsMedia)
            return field.IsMediaDirectory
                ? "Select a content folder. Its supported files are discovered automatically, including inherited files."
                : "A Pack/ or Application/ asset address. Use Import to copy a file into this pack.";
        return string.Empty;
    }
}
