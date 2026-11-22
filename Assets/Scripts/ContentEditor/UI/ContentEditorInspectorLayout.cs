using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;

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
    /// Includes optional unit authoring controls without adding XML until they are edited.
    /// </summary>
    /// <param name="record">The selected entry.</param>
    /// <returns>The complete form field collection.</returns>
    internal static IReadOnlyList<ContentPackEditorField> GetFields(ContentPackEditorRecord record)
    {
        List<ContentPackEditorField> fields = record.GetFields().ToList();
        if (record.Kind == "Building")
            fields.RemoveAll(field =>
                field.MemberName
                    is nameof(Building.ProductionType)
                        or nameof(Building.WeaponStrength)
            );
        foreach (string member in ContentEditorUnitArtworkLayout.GetMembers(record.Kind))
            if (!fields.Any(field => field.MemberName == member))
                fields.Add(record.GetField(member));
        if (record.Kind == "Faction" && record.Element.Element("FactionDataPath") == null)
            foreach (string member in new[] { "HQInstanceID", "DisallowedMissionTypeIDs" })
                if (!fields.Any(field => field.MemberName == member))
                    fields.Add(record.GetField(member));
        if (record.Kind == "Starfighter")
            foreach (string member in new[] { "Roles", "FormationPriority" })
            {
                fields.RemoveAll(field => field.MemberName == member);
                fields.Add(record.GetField(member));
            }
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
        if (record.Kind == "Trait")
            foreach (
                string member in new[]
                {
                    "LeadershipBonusPercent",
                    "PersonnelTravelTimePercent",
                    "SharesPersonnelTravelTimeModifier",
                }
            )
                if (!fields.Any(field => field.MemberName == member))
                    fields.Add(record.GetField(member));
        return fields;
    }

    /// <summary>
    /// Gets a stable section order without reordering fields inside their authored group.
    /// </summary>
    /// <param name="field">The field to place.</param>
    /// <returns>The section order.</returns>
    internal static int GetOrder(ContentPackEditorField field)
    {
        return Array.IndexOf(_groups, ContentPackEditorFieldMetadata.GetInspectorGroup(field));
    }
}
