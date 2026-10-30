using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Missions;

/// <summary>
/// Supplies authoring guidance for serialized fields without changing their data names.
/// </summary>
internal static class ContentPackEditorFieldMetadata
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
        ["AllowedMissionTypeIDs"] =
            "Checked missions can be performed by this special forces unit.",
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

    private static readonly IReadOnlyDictionary<string, string> _missionCaptions =
        ContentPackEditorFieldSchema.MissionIDs.ToDictionary(
            mission => mission,
            ContentPackEditorField.FormatName,
            StringComparer.Ordinal
        );

    /// <summary>
    /// Gets verified guidance for one field, including common media and variance controls.
    /// </summary>
    /// <param name="field">The authored field.</param>
    /// <returns>The help text, or an empty string for undocumented extensions.</returns>
    internal static string GetDescription(ContentPackEditorField field)
    {
        if (
            field.Record.Kind == "GameGenerationConfig"
            && field.MemberName is "Random" or "Random1" or "Random2"
        )
            return "Adds an independent random amount from zero through this value, including both endpoints.";
        if (field.Record.Kind == "GameGenerationConfig" && field.MemberName == "Base")
            return "The starting amount before random additions and configured limits are applied.";
        if (field.MemberName == "PersonnelTravelTimePercent")
            return "Percentage of normal personnel travel time: 100 is unchanged; 50 halves the duration.";
        if (field.MemberName == "LeadershipBonusPercent")
            return "Percentage added to the officer's base leadership while the trait is active.";
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

    /// <summary>
    /// Gets the field label from the shared authoring metadata.
    /// </summary>
    /// <param name="field">The authored member.</param>
    /// <param name="fallback">The descriptive label supplied by its containing section.</param>
    /// <returns>The label to display.</returns>
    internal static string GetCaption(ContentPackEditorField field, string fallback)
    {
        return field.MemberName switch
        {
            "DisallowedMissionTypeIDs" => "Disabled missions",
            "AllowedMissionTypeIDs" => "Allowed missions",
            "MineTypeID" => "Mine",
            "RecruitingFactionInstanceIDs" => "Recruitable by",
            "ManufacturingFactionInstanceIDs" => "Buildable by",
            "GarrisonTroopTypeID" => "Garrison troop",
            "OfficerInstanceID" => "Officer",
            "PlanetTypeID" => "Planet",
            "DestinationTypeID" => "Destination",
            "FactionID" or "FactionInstanceID" => "Faction",
            "OwnerInstanceID" => "Owner",
            "IsMain" => "Main character",
            "IsRecruitable" => "Recruitable",
            "IsKnownJedi" => "Known Force user",
            "IsJediTrainer" => "Can train Force users",
            "SharesPersonnelTravelTimeModifier" => "Share travel modifier with companions",
            "LeadershipBonusPercent" => "Leadership bonus (%)",
            "PersonnelTravelTimePercent" => "Personnel travel time (%)",
            "NamePoolID" => "Pool ID",
            "FallbackNamePoolID" => "Fallback pool",
            "Names" when field.XmlElement.Parent?.Name.LocalName == "NamePool" => "Ship names",
            _ => fallback,
        };
    }

    /// <summary>
    /// Classifies a serialized asset field for file selection, previews, and validation.
    /// </summary>
    /// <param name="field">The authored member.</param>
    /// <returns>The supported media category.</returns>
    internal static ContentPackEditorMediaType GetMediaType(ContentPackEditorField field)
    {
        string ancestry = string.Join(
            "/",
            field.XmlElement.AncestorsAndSelf().Select(parent => parent.Name.LocalName)
        );
        bool referenceKey =
            field.MemberName.EndsWith("Key", StringComparison.Ordinal)
            || field.MemberName.EndsWith("Keys", StringComparison.Ordinal);
        bool assetValue =
            field.MemberName.EndsWith("Path", StringComparison.Ordinal)
            || field.MemberName.EndsWith("Address", StringComparison.Ordinal)
            || field.Values.Any(value =>
                value.StartsWith("Pack/", StringComparison.Ordinal)
                || value.StartsWith("Application/", StringComparison.Ordinal)
            )
            || field.IsList
                && (
                    ancestry.Contains("Voice", StringComparison.Ordinal)
                    || ancestry.Contains("Music", StringComparison.Ordinal)
                    || ancestry.Contains("Audio", StringComparison.Ordinal)
                    || ancestry.Contains("Texture", StringComparison.Ordinal)
                );
        return field.IsBoolean
            || field.IsNumber
            || field.Choices.Count > 0
            || referenceKey
            || !assetValue
                ? ContentPackEditorMediaType.None
            : ancestry.Contains("Voice", StringComparison.Ordinal)
            || ancestry.Contains("Music", StringComparison.Ordinal)
            || ancestry.Contains("Audio", StringComparison.Ordinal)
            || ancestry.Contains("SoundPath", StringComparison.Ordinal)
                ? ContentPackEditorMediaType.Audio
            : ancestry.Contains("Model", StringComparison.Ordinal)
                ? ContentPackEditorMediaType.Model
            : ancestry.Contains("Video", StringComparison.Ordinal)
            || ancestry.Contains("Cutscene", StringComparison.Ordinal)
                ? ContentPackEditorMediaType.Video
            : ancestry.Contains("Image", StringComparison.Ordinal)
            || ancestry.Contains("IconPath", StringComparison.Ordinal)
            || ancestry.Contains("Texture", StringComparison.Ordinal)
                ? ContentPackEditorMediaType.Image
            : ContentPackEditorMediaType.None;
    }

    /// <summary>
    /// Selects the form section for the authored value.
    /// </summary>
    /// <param name="field">The authored member.</param>
    /// <returns>The form section.</returns>
    internal static string GetSection(ContentPackEditorField field)
    {
        return field.MediaType != ContentPackEditorMediaType.None ? "Artwork and audio"
            : field.IsMultiline
            || field
                .XmlElement.AncestorsAndSelf()
                .Any(parent =>
                    parent.Name.LocalName.StartsWith("Encyclopedia", StringComparison.Ordinal)
                )
                ? "Descriptions"
            : "Properties";
    }

    /// <summary>
    /// Groups known authoring concerns while retaining descriptive headings for configuration.
    /// </summary>
    /// <param name="field">The field to classify.</param>
    /// <returns>The group heading.</returns>
    internal static string GetInspectorGroup(ContentPackEditorField field)
    {
        if (field.MediaType == ContentPackEditorMediaType.Audio)
            return "Audio";
        if (field.IsMedia)
            return "Artwork";
        string member = field.Group;
        if (member.Contains("Description", StringComparison.Ordinal))
            return "Description";
        if (member is "PositionX" or "PositionY")
            return "Coordinates";
        if (member is "ResearchOrder" or "ResearchDifficulty")
            return "Production";
        if (
            member
            is "DisplayName"
                or "Name"
                or "TypeID"
                or "InstanceID"
                or "ID"
                or "OwnerID"
                or "OwnerInstanceID"
                or "IsMain"
                or "IsRecruitable"
                or "RecruitingFactionInstanceIDs"
        )
            return "Identity";
        if (
            member == "Ratings"
            || member.Contains("Research", StringComparison.Ordinal)
            || member
                is "DiplomacyVariance"
                    or "EspionageVariance"
                    or "CombatVariance"
                    or "LeadershipVariance"
        )
            return "Skills";
        if (member.StartsWith("Loyalty", StringComparison.Ordinal) || member == "CanBetray")
            return "Loyalty";
        if (
            member.Contains("Jedi", StringComparison.Ordinal)
            || member.Contains("Force", StringComparison.Ordinal)
                && member != "RequiredSpecialForces"
        )
            return "Force";
        if (member == "Traits")
            return "Identity";
        if (member.StartsWith("Allowed", StringComparison.Ordinal))
            return "Command and missions";
        if (
            member.Contains("Cost", StringComparison.Ordinal)
            || member.Contains("Build", StringComparison.Ordinal)
            || member.Contains("Maintenance", StringComparison.Ordinal)
            || member.Contains("Energy", StringComparison.Ordinal)
            || member.Contains("Production", StringComparison.Ordinal)
            || member is "ManufacturingFactionInstanceIDs" or "ProcessRate"
        )
            return "Production";
        if (
            member.Contains("Speed", StringComparison.Ordinal)
            || member.Contains("Hyperdrive", StringComparison.Ordinal)
            || member is "Agility" or "Maneuverability"
        )
            return "Movement";
        if (
            member.Contains("Weapon", StringComparison.Ordinal)
            || member.Contains("Attack", StringComparison.Ordinal)
            || member.Contains("Bombard", StringComparison.Ordinal)
            || member
                is "LaserCannon"
                    or "IonCannon"
                    or "Torpedoes"
                    or "LaserRange"
                    or "IonRange"
                    or "TorpedoRange"
            || member.StartsWith("TractorBeam", StringComparison.Ordinal)
        )
            return "Weapons";
        if (
            member.Contains("Hull", StringComparison.Ordinal)
            || member.Contains("Shield", StringComparison.Ordinal)
            || member.Contains("Defense", StringComparison.Ordinal)
        )
            return "Durability";
        if (
            member.Contains("Capacity", StringComparison.Ordinal)
            || member.EndsWith("SquadronSize", StringComparison.Ordinal)
        )
            return "Capacity";
        return "Properties";
    }

    /// <summary>
    /// Identifies mission settings without moving AI planning or unrelated combat settings.
    /// </summary>
    /// <param name="field">The projected rules property.</param>
    /// <returns>Whether the field is part of mission execution rules.</returns>
    internal static bool IsMissionField(ContentPackEditorField field)
    {
        return field.Group is "Research" or "Assassination" or "Espionage"
            || field.Group == "ProbabilityTables" && field.SectionPath.Contains("Mission")
            || field.Group == "Uprising"
                && (
                    field.MemberName.StartsWith("Subdue", StringComparison.Ordinal)
                    || field.MemberName is "InciteMissionSupportShift" or "MissionLeadershipDivisor"
                )
            || field.Group == "Jedi"
                && field.MemberName
                    is "TrainingCatchUpPercent"
                        or "ForceGrowthPerMission"
                        or "MissionParticipantEncounterMinimum"
                        or "MissionDefenderEncounterMinimum"
            || field.Group == "DifficultyModifiers"
                && (
                    field.MemberName.StartsWith("Mission", StringComparison.Ordinal)
                    || field.SectionPath.Any(section =>
                        section.StartsWith("Mission", StringComparison.Ordinal)
                    )
                );
    }

    /// <summary>
    /// Finds the mission owning one rule, leaving shared mission tables in their own section.
    /// </summary>
    /// <param name="field">The projected mission rule.</param>
    /// <returns>The mission identifier, or null for shared mission rules.</returns>
    internal static string MissionFor(ContentPackEditorField field)
    {
        if (field.Group == "Uprising")
            return field.MemberName.StartsWith("Subdue", StringComparison.Ordinal)
                    ? MissionTypeIDs.SubdueUprising
                : field.MemberName == "InciteMissionSupportShift" ? MissionTypeIDs.InciteUprising
                : null;
        if (field.Group == "Jedi" && field.MemberName == "TrainingCatchUpPercent")
            return MissionTypeIDs.JediTraining;
        return _missionCaptions
            .FirstOrDefault(mission =>
                field.Group == mission.Key
                || field.SectionPath.Contains(mission.Value)
                || field.Group == "ProbabilityTables" && field.MemberName == mission.Key
            )
            .Key;
    }

    /// <summary>
    /// Groups shared game rules into focused authoring pages.
    /// </summary>
    /// <param name="field">The rule property.</param>
    /// <returns>The readable category.</returns>
    internal static string RuleCategory(ContentPackEditorField field) =>
        field.Group switch
        {
            "DifficultyModifiers" => "Difficulty",
            "ProbabilityTables" => field.SectionPath.Contains("Mission")
                ? "Shared mission rules"
                : "Uprisings",
            "Jedi" or "DuelResolution" => "Force and duels",
            "SupportShift" => "Popular support",
            "Captive" => "Prisoners",
            "Planet" => "Planets",
            "Blockade" => "Blockades",
            "Uprising" => "Uprisings",
            _ => ContentPackEditorField.FormatName(field.Group),
        };
}
