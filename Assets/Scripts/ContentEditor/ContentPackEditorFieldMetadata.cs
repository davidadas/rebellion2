using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Missions;

/// <summary>
/// Supplies authoring guidance for serialized fields without changing their data names.
/// </summary>
internal static class ContentPackEditorFieldMetadata
{
    private static readonly IReadOnlyDictionary<
        (Type Owner, string Member),
        string
    > _generationDescriptions = new Dictionary<(Type, string), string>
    {
        [(typeof(StartingOfficerRule), nameof(StartingOfficerRule.OfficerInstanceID))] =
            "The officer guaranteed to join the starting roster when this rule applies.",
        [(typeof(StartingOfficerRule), nameof(StartingOfficerRule.GalaxySizes))] =
            "Galaxy sizes where this officer is guaranteed. Leave all unchecked to use every size.",
        [(typeof(StartingOfficerRule), nameof(StartingOfficerRule.DestinationTypeID))] =
            "A planet type or faction headquarters where the officer starts. Leave both destinations unset to use a friendly starting location.",
        [(typeof(StartingOfficerRule), nameof(StartingOfficerRule.DestinationInstanceID))] =
            "A specific planet instance where the officer starts. Selecting this replaces the other destination.",
        [(typeof(FactionSetup), nameof(FactionSetup.FactionID))] =
            "The faction receiving these research, officer, and starting-planet settings.",
        [(typeof(FactionSetup), nameof(FactionSetup.StartingResearchLevel))] =
            "The highest starting research order unlocked for buildings, ships, and troops.",
        [(typeof(PlanetSizeProfile), nameof(PlanetSizeProfile.Small))] =
            "The number of officers selected for this faction in a small galaxy.",
        [(typeof(PlanetSizeProfile), nameof(PlanetSizeProfile.Medium))] =
            "The number of officers selected for this faction in a medium galaxy.",
        [(typeof(PlanetSizeProfile), nameof(PlanetSizeProfile.Large))] =
            "The number of officers selected for this faction in a large galaxy.",
        [(typeof(StartingPlanet), nameof(StartingPlanet.PlanetTypeID))] =
            "The specific planet assigned to this faction. This is ignored when a random Outer Rim planet is selected.",
        [(typeof(StartingPlanet), nameof(StartingPlanet.IsHeadquarters))] =
            "Marks this starting planet as the faction's headquarters.",
        [(typeof(StartingPlanet), nameof(StartingPlanet.Loyalty))] =
            "The percentage of starting popular support assigned to the owning faction on this planet.",
        [(typeof(StartingPlanet), nameof(StartingPlanet.PickFromRim))] =
            "Selects a random unused Outer Rim planet instead of the configured planet.",
        [(typeof(StartingPlanet), nameof(StartingPlanet.VisibleToFactionIDs))] =
            "Additional factions that know this planet at game start.",
        [(typeof(FactionBucketConfig), nameof(FactionBucketConfig.FactionID))] =
            "The faction assigned these shares of strong and weak core planets.",
        [(typeof(FactionBucketConfig), nameof(FactionBucketConfig.StrongPct))] =
            "The percentage of all core planets assigned to this faction's strong bucket. Fixed core starting planets count toward this share.",
        [(typeof(FactionBucketConfig), nameof(FactionBucketConfig.WeakPct))] =
            "The percentage of all core planets assigned to this faction's weak bucket.",
        [(typeof(FactionBucketOverride), nameof(FactionBucketOverride.Difficulty))] =
            "The difficulty where this ownership override applies.",
        [(typeof(FactionBucketOverride), nameof(FactionBucketOverride.ControllerType))] =
            "Whether this override applies when the faction is controlled by a player or the AI.",
        [(typeof(FactionBucketOverride), nameof(FactionBucketOverride.StrongPct))] =
            "The strong core-planet percentage used instead of the faction's default for this difficulty and controller.",
        [(typeof(FactionBucketOverride), nameof(FactionBucketOverride.WeakPct))] =
            "The weak core-planet percentage used instead of the faction's default for this difficulty and controller.",
        [(typeof(PlanetResourceProfile), nameof(PlanetResourceProfile.Availability))] =
            "The resource-availability setting that selects this profile for a new game.",
        [(typeof(PlanetResourceProfile), nameof(PlanetResourceProfile.EnergyMin))] =
            "The minimum energy capacity after a planet's energy roll.",
        [(typeof(PlanetResourceProfile), nameof(PlanetResourceProfile.EnergyMax))] =
            "The maximum energy capacity after a planet's energy roll.",
        [(typeof(PlanetResourceProfile), nameof(PlanetResourceProfile.RawMaterialsMin))] =
            "The minimum raw-material node count after a planet's resource roll.",
        [(typeof(PlanetResourceProfile), nameof(PlanetResourceProfile.RawMaterialsMax))] =
            "The maximum raw-material node count after a planet's resource roll. The result cannot exceed the planet's energy capacity.",
        [(typeof(PlanetResourceProfile), nameof(PlanetResourceProfile.RimColonizationPct))] =
            "The percentage chance that an uncolonized Outer Rim planet starts colonized.",
        [(typeof(PlanetSupportSection), nameof(PlanetSupportSection.RimSupportRandom))] =
            "Stored in starting setup data but not currently applied during planet generation.",
        [
            (
                typeof(FacilityGenerationSection),
                nameof(FacilityGenerationSection.CoreMineMultiplier)
            )
        ] =
            "Percentage points of mine placement chance per unfilled raw-material node on core planets.",
        [(typeof(FacilityGenerationSection), nameof(FacilityGenerationSection.RimMineMultiplier))] =
            "Percentage points of mine placement chance per unfilled raw-material node on Outer Rim planets.",
        [(typeof(FacilityGenerationSection), nameof(FacilityGenerationSection.MineTypeID))] =
            "The building created when a starting mine placement succeeds.",
        [
            (
                typeof(FacilityGenerationSection),
                nameof(FacilityGenerationSection.FacilityTableRollMin)
            )
        ] = "The inclusive minimum roll used by the core and Outer Rim building tables.",
        [
            (
                typeof(FacilityGenerationSection),
                nameof(FacilityGenerationSection.FacilityTableRollMaxExclusive)
            )
        ] = "The exclusive maximum roll used by the core and Outer Rim building tables.",
        [(typeof(WeightedFacilityEntry), nameof(WeightedFacilityEntry.CumulativeWeight))] =
            "The first roll value that selects this entry. It remains selected until the next entry's value.",
        [(typeof(WeightedFacilityEntry), nameof(WeightedFacilityEntry.TypeID))] =
            "The building selected for this roll range. Leave empty to create no building.",
        [(typeof(HQFacilityLoadout), nameof(HQFacilityLoadout.PlanetTypeID))] =
            "The planet receiving these fixed buildings. Faction headquarters resolves to the selected faction's headquarters.",
        [(typeof(HQFacilityLoadout), nameof(HQFacilityLoadout.FactionID))] =
            "The faction used to resolve a faction-headquarters destination.",
        [(typeof(HQFacilityLoadout), nameof(HQFacilityLoadout.FacilityTypeIDs))] =
            "The buildings and quantities placed on this planet at game start.",
        [
            (
                typeof(UnitDeploymentSection),
                nameof(UnitDeploymentSection.UprisingPreventionThreshold)
            )
        ] =
            "Owned colonized planets below this support percentage receive automatic garrison troops.",
        [
            (
                typeof(UnitDeploymentSection),
                nameof(UnitDeploymentSection.SupportDeficitPerGarrisonTroop)
            )
        ] =
            "Each automatic garrison troop covers this many percentage points below the uprising threshold.",
        [(typeof(FixedGarrison), nameof(FixedGarrison.PlanetTypeID))] =
            "The planet receiving these starting units. Faction headquarters resolves to this entry's faction headquarters.",
        [(typeof(FixedGarrison), nameof(FixedGarrison.FactionID))] =
            "The faction that owns the placed units and resolves a faction-headquarters destination.",
        [(typeof(FixedFleet), nameof(FixedFleet.PlanetTypeID))] =
            "The fleet's fixed destination. This is replaced when possible starting planets are configured.",
        [(typeof(FixedFleet), nameof(FixedFleet.TargetPlanets))] =
            "Possible fleet destinations. One is selected at random, replacing the fixed destination and spawn chance.",
        [(typeof(FixedFleet), nameof(FixedFleet.FactionID))] =
            "The faction that owns the fleet and resolves faction-headquarters destinations.",
        [(typeof(FixedFleet), nameof(FixedFleet.SpawnChancePct))] =
            "The percentage chance to create this fleet at its fixed destination. Possible starting planets bypass this roll.",
        [(typeof(FixedFleetShip), nameof(FixedFleetShip.TypeID))] =
            "The capital-ship design created for this starting fleet.",
        [(typeof(FixedFleetShip), nameof(FixedFleetShip.Count))] =
            "The number of this capital ship created for the fleet.",
        [(typeof(UnitEntry), nameof(UnitEntry.TypeID))] = "The unit design created by this entry.",
        [(typeof(UnitEntry), nameof(UnitEntry.Count))] =
            "The number of this unit created whenever the entry is deployed.",
        [(typeof(FactionBudget), nameof(FactionBudget.FactionID))] =
            "The faction receiving units from this maintenance-based starting budget.",
        [(typeof(StartingUnitBudgetBonus), nameof(StartingUnitBudgetBonus.Difficulty))] =
            "The difficulty where this additional starting-unit budget applies.",
        [(typeof(StartingUnitBudgetBonus), nameof(StartingUnitBudgetBonus.AIOnly))] =
            "Limits this additional budget to factions not controlled by the player.",
        [(typeof(StartingUnitBudgetBonus), nameof(StartingUnitBudgetBonus.Category))] =
            "The unit category eligible for this additional budget.",
        [(typeof(BudgetLevel), nameof(BudgetLevel.GalaxySize))] =
            "The galaxy size where this budget percentage applies.",
        [(typeof(BudgetLevel), nameof(BudgetLevel.Percentage))] =
            "The percentage of maintenance capacity remaining after fixed units that can fund this allocation.",
        [(typeof(WeightedUnitEntry), nameof(WeightedUnitEntry.Weight))] =
            "The relative chance of selecting this unit group for each budget roll.",
        [(typeof(BalanceSection), nameof(BalanceSection.SupportBoostPerUnit))] =
            "Stored in starting setup data but not currently applied during game generation.",
        [(typeof(BalanceSection), nameof(BalanceSection.MaxMilitaryPresenceBoost))] =
            "Stored in starting setup data but not currently applied during game generation.",
    };

    private static readonly IReadOnlyDictionary<string, string> _descriptions = new Dictionary<
        string,
        string
    >
    {
        ["GalaxySizes"] =
            "Apply this starting officer rule to the selected galaxy sizes. Leave all unchecked to use every size.",
        ["DestinationTypeID"] =
            "Choose a planet or faction headquarters. Leave both destinations unset to choose a friendly starting location.",
        ["DestinationInstanceID"] =
            "Choose a specific planet instance. Selecting this replaces the other destination.",
        ["FacilityTypeIDs"] =
            "Choose buildings and the quantity of each to place at this headquarters.",
        ["TargetPlanets"] =
            "Choose candidate starting planets. Weight controls how often each can be selected. This replaces a fixed planet.",
        ["VisibleToFactionIDs"] = "Factions that know this planet at game start.",
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
        if (field.Record.Kind == "GameGenerationConfig")
        {
            Type owner =
                field.Container == field.Record.Element
                    ? ContentPackEditorFieldSchema.GetRecordType(field.Record.Element)
                    : ContentPackEditorFieldSchema.GetValueType(
                        field.Record.Element,
                        field.Container
                    );
            if (
                owner != null
                && _generationDescriptions.TryGetValue(
                    (owner, field.MemberName),
                    out string generationText
                )
            )
                return generationText;
        }
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
            "GalaxySizes" => "Galaxy sizes (all if empty)",
            "FacilityTypeIDs" => "Buildings",
            "TargetPlanets" => "Possible starting planets",
            "VisibleToFactionIDs" => "Visible to factions",
            "DestinationInstanceID" => "Specific destination",
            "TypeID" when field.Container?.Name.LocalName == "WeightedFacilityEntry" => "Building",
            "TypeID" when field.Container?.Name.LocalName == "FixedFleetShip" => "Ship",
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
            "Bombardment" when field.Record.Kind == "Building" => "Bombardment resistance",
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
