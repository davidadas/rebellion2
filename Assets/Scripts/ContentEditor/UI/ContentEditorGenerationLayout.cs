using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Util.Serialization;

/// <summary>
/// Groups starting setup around placements, fleets, resources, and faction rules.
/// </summary>
internal static class ContentEditorGenerationLayout
{
    /// <summary>
    /// Gets the tasks within one starting-setup tab.
    /// </summary>
    /// <param name="section">The selected tab.</param>
    /// <returns>The task captions and their serialized paths.</returns>
    internal static IReadOnlyList<(string Caption, string[] Path)> Groups(string section) =>
        section switch
        {
            "Galaxy" => new[]
            {
                ("Faction setup", new[] { "GalaxyClassification", "FactionSetups" }),
                ("Planet ownership", new[] { "GalaxyClassification", "FactionBuckets" }),
            },
            "Officers" => new[] { ("Starting officers", new[] { "Officers", "StartingOfficers" }) },
            "Buildings" => new[]
            {
                ("Headquarters buildings", new[] { "FacilityGeneration", "HQLoadouts" }),
                ("Core building choices", new[] { "FacilityGeneration", "CoreFacilityTable" }),
                ("Rim building choices", new[] { "FacilityGeneration", "RimFacilityTable" }),
                ("Building rules", new[] { "FacilityGeneration" }),
            },
            "Units" => new[]
            {
                ("Starting units", new[] { "UnitDeployment", "FixedGarrisons" }),
                ("Starting fleets", new[] { "UnitDeployment", "FixedFleets" }),
                ("Random starting forces", new[] { "UnitDeployment", "FactionBudgets" }),
                ("Garrison rules", new[] { "UnitDeployment" }),
            },
            "Resources" => new[] { ("Resource profiles", new[] { "PlanetResources", "Profiles" }) },
            "Support" => new[] { ("Popular support", new[] { "PlanetSupport" }) },
            _ => new[] { ("Balance", new[] { "Balance" }) },
        };

    /// <summary>
    /// Names a collection by its editing purpose.
    /// </summary>
    /// <param name="name">The serialized collection member.</param>
    /// <returns>The readable collection caption.</returns>
    internal static string Caption(string name) =>
        name switch
        {
            "FixedGarrisons" => "Placements",
            "FixedFleets" => "Fleets",
            "ShipEntries" => "Ships",
            "FactionSetups" => "Factions",
            "StartingPlanets" => "Starting planets",
            "FactionBuckets" => "Planet ownership",
            "Overrides" => "Difficulty overrides",
            "HQLoadouts" => "Headquarters buildings",
            "CoreFacilityTable" => "Core building choices",
            "RimFacilityTable" => "Rim building choices",
            "FactionBudgets" => "Random starting forces",
            "UnitTable" => "Unit groups",
            "Bonuses" => "Additional forces",
            "Profiles" => "Resource profiles",
            "BudgetLevels" => "Budget by galaxy size",
            "FactionModifiers" => "Faction settings",
            _ => ContentPackEditorField.FormatName(name),
        };

    /// <summary>
    /// Names the collection's explicit creation action.
    /// </summary>
    /// <param name="name">The collection member.</param>
    /// <returns>The singular creation action.</returns>
    internal static string AddCaption(string name) =>
        name switch
        {
            "FixedGarrisons" => "Add starting units",
            "FixedFleets" => "Add starting fleet",
            "ShipEntries" => "Add ship",
            "StartingOfficers" => "Add starting officer",
            "StartingPlanets" => "Add starting planet",
            "HQLoadouts" => "Add building placement",
            "CoreFacilityTable" or "RimFacilityTable" => "Add building choice",
            "UnitTable" => "Add unit group",
            "Bonuses" => "Add additional forces",
            "BudgetLevels" => "Add galaxy size",
            "Overrides" => "Add difficulty override",
            "Profiles" => "Add resource profile",
            _ => "Add faction setup",
        };

    /// <summary>
    /// Finds immediate object collections, traversing ordinary nested settings but not list entries.
    /// </summary>
    /// <param name="project">The owning project.</param>
    /// <param name="record">The setup definition.</param>
    /// <param name="owner">The displayed object.</param>
    /// <returns>The editable child collections.</returns>
    internal static IReadOnlyList<ContentPackEditorCollection> Collections(
        ContentPackEditorProject project,
        ContentPackEditorRecord record,
        XElement owner
    )
    {
        List<ContentPackEditorCollection> result = new();
        Type type =
            owner == record.Element
                ? ContentPackEditorFieldSchema.GetRecordType(owner)
                : ContentPackEditorFieldSchema.GetValueType(record.Element, owner);
        foreach (
            var property in type.GetProperties()
                .Where(property =>
                    SerializationMetadata.GetElementType(type, property.Name)
                    == property.PropertyType
                )
        )
        {
            Type itemType = ContentPackEditorFieldSchema.GetItemType(property.PropertyType);
            if (itemType != null && !TypeHelper.IsScalar(itemType))
                result.Add(project.GetCollection(record, owner, property.Name));
            else if (
                itemType == null
                && !TypeHelper.IsScalar(property.PropertyType)
                && !TypeHelper.IsDictionary(property.PropertyType)
                && owner.Element(property.Name) is XElement child
            )
                result.AddRange(Collections(project, record, child));
        }
        return result;
    }

    /// <summary>
    /// Names an entry from its officer, faction, planet, or unit reference.
    /// </summary>
    /// <param name="project">The source of readable definition names.</param>
    /// <param name="entry">The selected setup entry.</param>
    /// <param name="index">The fallback ordinal.</param>
    /// <returns>The readable entry label.</returns>
    internal static string EntryCaption(ContentPackEditorProject project, XElement entry, int index)
    {
        List<string> names = new();
        if (entry.Name.LocalName == "FixedFleet")
            names.Add("Fleet " + (index + 1));
        if (entry.Name.LocalName == "WeightedUnitEntry")
            names.Add("Group " + (index + 1));
        if ((bool?)entry.Element("IsHeadquarters") == true)
            names.Add("Headquarters");
        if ((bool?)entry.Element("PickFromRim") == true)
            names.Add("Random Outer Rim planet");
        var records = project
            .GetCatalogs()
            .SelectMany(catalog =>
                catalog.Records.Concat(catalog.Records.SelectMany(record => record.Planets))
            )
            .ToArray();
        foreach (
            string member in new[] { "FactionID", "OfficerInstanceID", "PlanetTypeID", "TypeID" }
        )
        {
            string value = entry.Element(member)?.Value;
            if (string.IsNullOrWhiteSpace(value))
                continue;
            names.Add(
                value == Rebellion.Game.GameGenerationConfig.FactionHqSentinel
                    ? "Headquarters"
                    : records
                        .FirstOrDefault(record =>
                            record.ID == value
                            || record.Element.Element("InstanceID")?.Value == value
                        )
                        ?.Name
                        ?? "Missing: " + value
            );
        }
        if (
            entry.Element("GalaxySize") is XElement size
            && int.TryParse(size.Value, out int number)
            && Enum.IsDefined(typeof(Rebellion.Game.GameSize), number)
        )
            names.Add(((Rebellion.Game.GameSize)number).ToString());
        foreach (
            string member in new[] { "Availability", "Difficulty", "ControllerType", "Category" }
        )
            if (entry.Element(member) is XElement value)
                names.Add(ContentPackEditorField.FormatName(value.Value));
        if (entry.Name.LocalName == "WeightedFacilityEntry" && entry.Element("TypeID") == null)
            names.Add("No building");
        if (entry.Element("CumulativeWeight") is XElement weight)
            names.Add("through " + weight.Value + "%");
        if (names.Count == 0)
            names.Add("Entry " + (index + 1));
        if (entry.Element("Count") is XElement count)
            names.Add("× " + count.Value);
        return string.Join(" — ", names);
    }
}
