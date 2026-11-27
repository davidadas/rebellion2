using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Missions;
using UnityEngine;

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
            "Factions that receive full starting intelligence at this planet, even when other planets start unexplored.",
        [(typeof(StartingIntelligenceProfile), nameof(StartingIntelligenceProfile.Difficulty))] =
            "The difficulty that uses these starting visibility rules. Omitted difficulties use the default rules.",
        [(typeof(StartingIntelligenceProfile), nameof(StartingIntelligenceProfile.RevealAgents))] =
            "Reveals foreign officers and special forces on initially explored Core worlds. Passengers of hidden fleets stay hidden. Scenario visibility exceptions always apply.",
        [(typeof(StartingIntelligenceProfile), nameof(StartingIntelligenceProfile.RevealFleets))] =
            "Reveals foreign fleets on initially explored Core worlds. Scenario visibility exceptions always apply.",
        [
            (
                typeof(StartingIntelligenceProfile),
                nameof(StartingIntelligenceProfile.RevealBuildings)
            )
        ] =
            "Reveals foreign buildings on initially explored Core worlds. Scenario visibility exceptions always apply.",
        [
            (
                typeof(StartingIntelligenceProfile),
                nameof(StartingIntelligenceProfile.StartWithUnexploredPlanets)
            )
        ] =
            "Starts neutral and enemy planets unexplored, including Core worlds. Owned planets and scenario visibility exceptions remain known. Applies to every faction.",
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
        ["RefinementMultiplier"] =
            "Multiplies the raw and refined material supply available to this faction. A value of 1 leaves supply unchanged.",
        ["ResourceProcessingPointsPerFacility"] =
            "Base maintenance capacity from each operating mine and refinery pair. Difficulty can adjust the final capacity.",
        ["GarrisonEfficiency"] =
            "Retained for saved settings. Sector garrison reduction is controlled by each ship's sector suppression settings.",
        ["TroopEffectiveness"] =
            "Multiplies the weight of hostile troops when this faction evaluates popular support.",
        ["UprisingResistance"] =
            "Multiplies garrison strength during an uprising in sectors with an operational suppression ship.",
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
        ["DisplayImagePath"] = "Primary illustration shown on the large unit card.",
        ["MessageImagePath"] = "Illustration attached to reports about this unit.",
        ["EncyclopediaImagePath"] = "Illustration shown when the unit's encyclopedia entry opens.",
        ["BattleResultImagePath"] = "Unit artwork shown in battle results.",
        ["InTransitSmallImagePath"] = "Travel artwork for the unit's compact gameplay card.",
        ["InTransitImagePath"] = "Travel artwork for the large unit view.",
        ["BattleResultInTransitImagePath"] = "Travel layer shown for this unit in battle results.",
        ["DamagedSmallImagePath"] = "Damage overlay drawn on the compact unit card.",
        ["DamagedImagePath"] = "Damage overlay drawn on the large unit card.",
        ["BattleResultDamagedImagePath"] = "Damage overlay shown on this unit in battle results.",
        ["BackgroundImagePath"] =
            "Optional background override for this unit. Leave empty to retain the faction theme default.",
        ["OfficerBackgroundImagePath"] =
            "Optional theme background retained for officer artwork workflows.",
        ["CapitalShipBackgroundImagePath"] =
            "Optional theme background retained for capital-ship artwork workflows.",
        ["StarfighterBackgroundImagePath"] =
            "Optional theme background retained for starfighter artwork workflows.",
        ["PrimaryColorHex"] =
            "Faction identity color used for planet names, support, and strategy counters.",
        ["HighlightTextColorHex"] = "Text color used for selected and highlighted menu entries.",
        ["SelectionColorHex"] = "Color applied to shared selection frames across faction windows.",
        ["ActiveWindowTitleColorHex"] =
            "Color applied to the shared title mask while a window has focus.",
        ["InactiveWindowTitleColorHex"] =
            "Color applied to the shared title mask while a window does not have focus.",
        ["ActiveBuildingPaneColorHex"] = "Color applied to the active manufacturing-lane mask.",
        ["InactiveBuildingPaneColorHex"] = "Color applied to inactive manufacturing-lane masks.",
        ["PlanetMarkerColorHex"] = "Color applied to the faction's shared galaxy-map star masks.",
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
            "Allows a successful capture evasion during a linked officer encounter to increase this character's Force abilities.",
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
        ["HyperdriveCount"] =
            "The number of hyperdrive systems on each craft. Combat damage can disable them; losing all systems prevents independent retreat. This does not change travel speed.",
        ["Roles"] = "The combat tasks this unit design is intended to perform.",
        ["FormationPriority"] =
            "Sets a fixed place in the faction's formation order, including when this design is absent. Lower values come first. Leave blank to order present fighter types by roles, then type ID.",
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
            "This project's version, used to identify installed updates and save requirements.",
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
        if (IsAIDecisionInterval(field))
            return "Ticks between new AI assignments. Higher values slow decisions. Existing fleet orders continue every tick. Minimum: 1.";

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
        if (field.MemberName == nameof(FactionTheme.MainMenuMedallionModelPath))
            return "A GLB model displayed as this faction's rotating main-menu medallion. The model is centered and scaled to fit automatically.";
        if (
            field.MemberName
            is nameof(ContentPackDefinition.StartupIntroVideoPath)
                or nameof(ContentPackDefinition.OpeningCrawlVideoPath)
        )
            return "Leave empty to use the built-in video. Changes take effect at the next launch.";
        if (field.Record.Kind == "Regiment" && field.MemberName == "InTransitSmallImagePath")
            return "An optional effect drawn over this troop in the Defense window while it is in transit.";
        if (_descriptions.TryGetValue(field.MemberName, out string text))
        {
            string expected = GetExpectedImageDimensions(field);
            return expected == null ? text : "Expected size: " + expected + ". " + text;
        }
        if (field.MemberName.EndsWith("Variance", StringComparison.Ordinal))
            return "The random starting variation applied to this rating during world generation.";
        if (
            field.Record.Kind == "FactionTheme"
            && field.MediaType == ContentPackEditorMediaType.Image
        )
            return GetInterfaceImageDescription(field);
        return string.Empty;
    }

    /// <summary>
    /// Describes the authored visual state when one image belongs to a named interface control.
    /// </summary>
    /// <param name="field">The faction image slot.</param>
    /// <returns>Guidance for recognized control states.</returns>
    private static string GetInterfaceImageDescription(ContentPackEditorField field)
    {
        string control = field.SectionPath.LastOrDefault();
        if (string.IsNullOrWhiteSpace(control))
            return string.Empty;
        string state = field.MemberName switch
        {
            "UpImagePath" or "NormalImagePath" => "at rest",
            "DownImagePath" or "PressedImagePath" => "while the control is pressed",
            "HoverImagePath" => "while the pointer is over the control",
            "ActiveImagePath" or "SelectedImagePath" => "when selected",
            "InactiveImagePath" => "when unselected",
            "DisabledImagePath" => "when the control is unavailable",
            _ => null,
        };
        return state == null ? string.Empty : $"{control} artwork shown {state}.";
    }

    /// <summary>
    /// Gets the field label from the shared authoring metadata.
    /// </summary>
    /// <param name="field">The authored member.</param>
    /// <param name="fallback">The descriptive label supplied by its containing section.</param>
    /// <returns>The label to display.</returns>
    internal static string GetCaption(ContentPackEditorField field, string fallback)
    {
        if (IsAIDecisionInterval(field))
            return "Decision interval (ticks)";

        string caption = field.MemberName switch
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
            "ResourceProcessingPointsPerFacility" => "Maintenance per resource pair",
            "UnattributedSubject" => "Subject without a reporter",
            "UnattributedBody" => "Body without a reporter",
            "PersonnelReturningTemplate" => "Personnel returning",
            "PersonnelLostTemplate" => "Personnel lost",
            "DetailListHeaderTemplate" => "Additional systems heading",
            "DetailListItemTemplate" => "Additional system entry",
            "OfficerInstanceID" => "Officer",
            "PlanetTypeID" => "Planet",
            "DestinationTypeID" => "Destination",
            "FactionID" or "FactionInstanceID" => "Faction",
            "OwnerInstanceID" => "Owner",
            "IsMain" => "Main character",
            "GrowsForceOnMission" => "Grows Force after evasion",
            "ForceGrowthPerMission" => "Force growth per evasion",
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
            "BackgroundImagePath" => "Background override",
            nameof(FactionTheme.MainMenuMedallionModelPath) => "Main menu medallion",
            nameof(FactionTheme.IntroCutscenePath) => "Campaign intro",
            nameof(FactionTheme.VictoryCutscenePath) => "Victory",
            nameof(FactionTheme.DefeatCutscenePath) => "Defeat",
            nameof(FactionTheme.HeadquartersDestroyedCutscenePath) => "Headquarters destroyed",
            nameof(ContentPackDefinition.StartupIntroVideoPath) => "Startup intro",
            nameof(ContentPackDefinition.OpeningCrawlVideoPath) => "Opening crawl",
            "PrimaryColorHex" => "Primary faction color",
            "HighlightTextColorHex" => "Highlight text color",
            "SelectionColorHex" => "Selection frames",
            "ActiveWindowTitleColorHex" => "Window titles, active",
            "InactiveWindowTitleColorHex" => "Window titles, inactive",
            "ActiveBuildingPaneColorHex" => "Building pane, active",
            "InactiveBuildingPaneColorHex" => "Building pane, inactive",
            "PlanetMarkerColorHex" => "Galaxy star markers",
            _ => fallback,
        };
        if (field.MediaType == ContentPackEditorMediaType.Image)
        {
            if (caption.EndsWith(" path", StringComparison.OrdinalIgnoreCase))
                caption = caption[..^5];
            string expected = GetExpectedImageDimensions(field);
            if (expected != null)
                caption += " (" + expected + ")";
        }
        return caption;
    }

    /// <summary>
    /// Returns the established pixel dimensions for image fields with one fixed authored size.
    /// </summary>
    /// <param name="field">The image field being presented.</param>
    /// <returns>The width and height label, or null when the layout intentionally accepts several sizes.</returns>
    internal static string GetExpectedImageDimensions(ContentPackEditorField field)
    {
        if (field.MediaType != ContentPackEditorMediaType.Image)
            return null;
        string member = field.MemberName;
        string kind = field.Record.Kind;
        if (kind == "FactionTheme" && field.Group == nameof(FactionTheme.ArtworkDefaults))
            return "1800 × 900";
        if (ContentEditorUnitArtworkLayout.IsHeadquartersIcon(field.Record))
            return
                member is "DisplayImagePath" or "SmallDisplayImagePath" or "InTransitSmallImagePath"
                ? "77 × 77"
                : null;
        if (member is "MessageImagePath" or "EncyclopediaImagePath")
            return "1800 × 900";
        if (member is "CapturedOverlayImagePath" or "InjuredImagePath")
            return "275 × 113";
        if (kind == "Officer")
            return member switch
            {
                "BackgroundImagePath" => "1800 × 900",
                "DisplayImagePath" => "360 × 360",
                "SmallDisplayImagePath" => "275 × 113",
                _ => null,
            };
        if (kind == "SpecialForce")
            return member switch
            {
                "DisplayImagePath" => "549 × 225",
                "SmallDisplayImagePath" or "InTransitSmallImagePath" => "275 × 113",
                "InTransitImagePath" => "549 × 225",
                _ => null,
            };
        if (kind == "Regiment")
            return member switch
            {
                "DisplayImagePath" => "1373 × 563",
                "SmallDisplayImagePath" or "InTransitSmallImagePath" => "275 × 113",
                _ => null,
            };
        if (kind == "Building")
            return member switch
            {
                "DisplayImagePath" => "1373 × 563",
                "SmallDisplayImagePath"
                or "InTransitSmallImagePath"
                    when field.Record.Element.Element("BuildingType")?.Value
                        is "ConstructionFacility"
                            or "TrainingFacility"
                            or "Shipyard"
                            or "Mine"
                            or "Refinery" => "302 × 158",
                "SmallDisplayImagePath" or "InTransitSmallImagePath" => "275 × 113",
                _ => null,
            };
        if (kind is "CapitalShip" or "Starfighter")
            return member switch
            {
                "BackgroundImagePath" => "1800 × 900",
                "DisplayImagePath" => "1485 × 563",
                "SmallDisplayImagePath" or "InTransitSmallImagePath" or "DamagedSmallImagePath" =>
                    "297 × 113",
                "BattleResultImagePath"
                or "InTransitImagePath"
                or "BattleResultInTransitImagePath"
                or "DamagedImagePath"
                or "BattleResultDamagedImagePath" => "549 × 225",
                _ => null,
            };
        return member switch
        {
            "SavedGameSlotIconImagePath" => "117 × 86",
            "FleetListIconImagePath" => "297 × 113",
            "FleetListEnrouteIconImagePath" or "FleetListDamagedIconImagePath" => "275 × 113",
            "FleetListSelectionImagePath" => "329 × 212",
            "FleetDetailSelectionImagePath" => "513 × 167",
            "FleetConstructionSmallImagePath" => "275 × 113",
            "FleetConstructionLargeImagePath" => "545 × 221",
            "FleetStarfightersBadgeImagePath"
            or "FleetTroopsBadgeImagePath"
            or "FleetPersonnelBadgeImagePath" => "68 × 50",
            _ => null,
        };
    }

    /// <summary>
    /// Parses the established fixed output size for one image field.
    /// </summary>
    /// <param name="field">The image field whose authored output size is required.</param>
    /// <param name="dimensions">Receives the parsed width and height.</param>
    /// <returns>True when the field has one valid fixed output size.</returns>
    internal static bool TryGetExpectedImageDimensions(
        ContentPackEditorField field,
        out Vector2Int dimensions
    )
    {
        dimensions = default;
        string expected = GetExpectedImageDimensions(field);
        if (string.IsNullOrWhiteSpace(expected))
            return false;
        string[] values = expected.Split('×');
        if (
            values.Length != 2
            || !int.TryParse(values[0].Trim(), out int width)
            || !int.TryParse(values[1].Trim(), out int height)
            || width <= 0
            || height <= 0
        )
            return false;
        dimensions = new Vector2Int(width, height);
        return true;
    }

    /// <summary>
    /// Classifies a serialized asset field for file selection, previews, and validation.
    /// </summary>
    /// <param name="field">The authored member.</param>
    /// <returns>The supported media category.</returns>
    internal static ContentPackEditorMediaType GetMediaType(ContentPackEditorField field)
    {
        if (
            field.XmlElement.Parent?.Name.LocalName == nameof(GalaxyBackground.PlanetIcons)
            && field.XmlElement.Parent.Parent?.Name.LocalName
                == nameof(FactionTheme.GalaxyBackground)
            && field.MemberName
                is nameof(PlanetIcons.Small)
                    or nameof(PlanetIcons.Medium)
                    or nameof(PlanetIcons.Large)
                    or nameof(PlanetIcons.XL)
        )
            return ContentPackEditorMediaType.Image;

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
        if (member is "Traits" or "Roles")
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
                    or "FormationPriority"
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
            "AI" when IsAIDecisionInterval(field) => "AI decisions",
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

    /// <summary>
    /// Identifies difficulty-specific strategic decision intervals exposed by the rules editor.
    /// </summary>
    /// <param name="field">The authored rules field.</param>
    /// <returns>Whether the field belongs to the decision-interval table.</returns>
    internal static bool IsAIDecisionInterval(ContentPackEditorField field) =>
        field.Group == "AI"
        && field.MemberName == "Value"
        && field
            .XmlElement.Ancestors()
            .Any(element =>
                element.Name.LocalName == nameof(GameConfig.AIConfig.DecisionTickIntervals)
            );
}
