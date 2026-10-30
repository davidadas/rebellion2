using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Arranges authored fields around identity, skills, production, and media tasks.
/// </summary>
internal static class ContentEditorInspectorLayout
{
    private static readonly string[] _groups =
    {
        "Identity",
        "Skills",
        "Command and missions",
        "Loyalty",
        "Force",
        "Production",
        "Movement",
        "Durability",
        "Weapons",
        "Capacity",
        "Coordinates",
        "Description",
        "Artwork",
        "Audio",
        "Properties",
    };

    /// <summary>
    /// Includes optional officer authoring controls without adding XML until they are edited.
    /// </summary>
    /// <param name="record">The selected entry.</param>
    /// <returns>The complete form field collection.</returns>
    internal static IReadOnlyList<ContentPackEditorField> GetFields(ContentPackEditorRecord record)
    {
        List<ContentPackEditorField> fields = record.GetFields().ToList();
        if (
            ContentEditorUnitFactory.CanCreate(record.Kind)
            && !fields.Any(field => field.MemberName == "SmallDisplayImagePath")
        )
            fields.Add(record.GetField("SmallDisplayImagePath"));
        if (record.Kind == "Faction" && record.Element.Element("FactionDataPath") == null)
            foreach (string member in new[] { "HQInstanceID", "DisallowedMissionTypeIDs" })
                if (!fields.Any(field => field.MemberName == member))
                    fields.Add(record.GetField(member));
        if (record.Kind == "Officer")
        {
            foreach (string skill in new[] { "Diplomacy", "Espionage", "Combat", "Leadership" })
                if (!fields.Any(field => field.Group == "Ratings" && field.Name == skill))
                    fields.Add(record.GetRatingField(skill));
            string[] optional =
            {
                "AllowedRanks",
                "Traits",
                "RecruitingFactionInstanceIDs",
                "IsMain",
                "IsRecruitable",
                "CanBetray",
                "Loyalty",
                "LoyaltyVariance",
                "DiplomacyVariance",
                "EspionageVariance",
                "CombatVariance",
                "LeadershipVariance",
                "ShipResearch",
                "ShipResearchVariance",
                "TroopResearch",
                "TroopResearchVariance",
                "FacilityResearch",
                "FacilityResearchVariance",
                "JediProbability",
                "JediLevel",
                "JediLevelVariance",
                "IsKnownJedi",
                "IsJediTrainer",
                "GrowsForceOnMission",
            };
            foreach (string member in optional)
                if (!fields.Any(field => field.Group == member))
                    fields.Add(record.GetField(member));
        }
        return fields;
    }

    /// <summary>
    /// Gets a stable section order without reordering fields inside their authored group.
    /// </summary>
    /// <param name="field">The field to place.</param>
    /// <returns>The section order.</returns>
    internal static int GetOrder(ContentPackEditorField field)
    {
        return Array.IndexOf(_groups, GetGroup(field));
    }

    /// <summary>
    /// Groups known authoring concerns while retaining descriptive headings for configuration.
    /// </summary>
    /// <param name="field">The field to classify.</param>
    /// <returns>The group heading.</returns>
    internal static string GetGroup(ContentPackEditorField field)
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
}
