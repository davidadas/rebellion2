using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Defines the persisted output and state-specific artwork fields shown for each unit.
/// </summary>
internal static class ContentEditorUnitArtworkLayout
{
    internal const string BackgroundMember = "BackgroundImagePath";

    private static readonly IReadOnlyList<string> _officerGenerationSources = new[]
    {
        "MessageImagePath",
        "DisplayImagePath",
        "EncyclopediaImagePath",
        "SmallDisplayImagePath",
    };
    private static readonly IReadOnlyList<string> _unitGenerationSources = new[]
    {
        "DisplayImagePath",
        "MessageImagePath",
        "EncyclopediaImagePath",
        "BattleResultImagePath",
        "SmallDisplayImagePath",
    };
    private static readonly IReadOnlyList<string> _headquartersIconMembers = new[]
    {
        "DisplayImagePath",
        "SmallDisplayImagePath",
        "InTransitSmallImagePath",
    };

    private static readonly IReadOnlyDictionary<string, string[]> _membersByKind = new Dictionary<
        string,
        string[]
    >(StringComparer.Ordinal)
    {
        ["Officer"] = new[]
        {
            BackgroundMember,
            "DisplayImagePath",
            "SmallDisplayImagePath",
            "MessageImagePath",
            "EncyclopediaImagePath",
            "InTransitSmallImagePath",
            "InTransitImagePath",
            "InjuredImagePath",
            "CapturedOverlayImagePath",
        },
        ["SpecialForce"] = new[]
        {
            "DisplayImagePath",
            "SmallDisplayImagePath",
            "MessageImagePath",
            "EncyclopediaImagePath",
            "InTransitImagePath",
            "InTransitSmallImagePath",
        },
        ["Regiment"] = new[]
        {
            "DisplayImagePath",
            "SmallDisplayImagePath",
            "MessageImagePath",
            "EncyclopediaImagePath",
            "InTransitSmallImagePath",
        },
        ["Building"] = new[]
        {
            "DisplayImagePath",
            "SmallDisplayImagePath",
            "MessageImagePath",
            "EncyclopediaImagePath",
            "InTransitSmallImagePath",
        },
        ["CapitalShip"] = ShipMembers(),
        ["Starfighter"] = ShipMembers(),
    };

    /// <summary>
    /// Gets every artwork field supported by one unit type in workflow order.
    /// </summary>
    /// <param name="kind">The serialized unit type.</param>
    /// <returns>The ordered member names, or an empty collection for non-units.</returns>
    internal static IReadOnlyList<string> GetMembers(string kind) =>
        kind != null && _membersByKind.TryGetValue(kind, out string[] members)
            ? members
            : Array.Empty<string>();

    /// <summary>
    /// Gets the artwork fields supported by one concrete unit record.
    /// </summary>
    /// <param name="record">The unit whose artwork workflow is being rendered.</param>
    /// <returns>The ordered member names for the record's actual presentation.</returns>
    internal static IReadOnlyList<string> GetMembers(ContentPackEditorRecord record) =>
        IsHeadquartersIcon(record) ? _headquartersIconMembers : GetMembers(record?.Kind);

    /// <summary>
    /// Reports whether one unit member belongs in its concrete artwork workflow.
    /// </summary>
    /// <param name="record">The unit whose artwork workflow is being rendered.</param>
    /// <param name="member">The serialized image member.</param>
    /// <returns>True when the member has an established use for the unit.</returns>
    internal static bool SupportsMember(ContentPackEditorRecord record, string member) =>
        GetMembers(record).Contains(member);

    /// <summary>
    /// Reports whether a unit uses one complete icon instead of crop-based unit artwork.
    /// </summary>
    /// <param name="record">The unit whose artwork workflow is being rendered.</param>
    /// <returns>True for the headquarters icon record.</returns>
    internal static bool IsHeadquartersIcon(ContentPackEditorRecord record) =>
        record?.Kind == "Building"
        && string.Equals(
            record.Element.Element("BuildingType")?.Value,
            "Headquarters",
            StringComparison.Ordinal
        );

    /// <summary>
    /// Reports whether standard rescaled output cuts apply to one concrete unit.
    /// </summary>
    /// <param name="record">The unit whose artwork workflow is being rendered.</param>
    /// <returns>False for icon-only records and true for ordinary unit artwork.</returns>
    internal static bool SupportsStandardCuts(ContentPackEditorRecord record) =>
        !IsHeadquartersIcon(record);

    /// <summary>
    /// Checks whether a field can supply pixels for a standard artwork cut.
    /// </summary>
    /// <param name="field">The unit image field being evaluated.</param>
    /// <returns>True for an ordinary fixed-size image in the unit's artwork workflow.</returns>
    internal static bool IsCutSource(ContentPackEditorField field) =>
        SupportsStandardCuts(field.Record)
        && SupportsMember(field.Record, field.MemberName)
        && field.MemberName != BackgroundMember
        && !IsStateSpecific(field.MemberName)
        && ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(field, out _);

    /// <summary>
    /// Checks whether a field can receive generated artwork.
    /// </summary>
    /// <param name="field">The unit image field being evaluated.</param>
    /// <returns>True for standard cut destinations, excluding ship illustrations.</returns>
    internal static bool IsCutTarget(ContentPackEditorField field) =>
        IsCutSource(field)
        && (field.Record.Kind is not ("CapitalShip" or "Starfighter") || !IsIllustration(field));

    /// <summary>
    /// Identifies the large report and encyclopedia illustrations for a unit.
    /// </summary>
    /// <param name="field">The artwork field being evaluated.</param>
    /// <returns>Whether the field belongs in the illustrations gallery.</returns>
    internal static bool IsIllustration(ContentPackEditorField field) =>
        field.MemberName is "MessageImagePath" or "EncyclopediaImagePath";

    /// <summary>
    /// Finds the output size when two artwork slots support a cut at the source's resolution.
    /// </summary>
    /// <param name="source">The unit image supplying pixels.</param>
    /// <param name="target">The unit image slot receiving the cut.</param>
    /// <param name="sourceSize">The decoded width and height of the source image.</param>
    /// <param name="dimensions">The pixel dimensions of the eligible output.</param>
    /// <returns>True when the target can be generated at the source resolution.</returns>
    internal static bool TryGetCutDimensions(
        ContentPackEditorField source,
        ContentPackEditorField target,
        Vector2Int sourceSize,
        out Vector2Int dimensions
    )
    {
        dimensions = default;
        return target.Record.Element == source.Record.Element
            && target.MemberName != source.MemberName
            && IsCutSource(source)
            && IsCutTarget(target)
            && ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(target, out dimensions)
            && ContentEditorArtworkCutGenerator.CanFitSource(sourceSize, dimensions);
    }

    /// <summary>
    /// Gets the stable workflow position of one artwork member.
    /// </summary>
    /// <param name="kind">The serialized unit type.</param>
    /// <param name="member">The serialized image member.</param>
    /// <returns>The configured position, with extension fields placed last.</returns>
    internal static int GetOrder(string kind, string member)
    {
        IReadOnlyList<string> members = GetMembers(kind);
        for (int index = 0; index < members.Count; index++)
            if (string.Equals(members[index], member, StringComparison.Ordinal))
                return index;
        return int.MaxValue;
    }

    /// <summary>
    /// Gets the canonical source preference for deterministic standard artwork cuts.
    /// </summary>
    /// <param name="kind">The serialized unit type.</param>
    /// <returns>The ordered source members, from the established transparent source to fallbacks.</returns>
    internal static IReadOnlyList<string> GetGenerationSourceMembers(string kind) =>
        kind == "Officer" ? _officerGenerationSources : _unitGenerationSources;

    /// <summary>
    /// Reports whether an output requires separately authored state artwork instead of a normal cut.
    /// </summary>
    /// <param name="member">The serialized image member.</param>
    /// <returns>True for travel, damage, injury, and captured-state artwork.</returns>
    internal static bool IsStateSpecific(string member) =>
        member != null
        && (
            member.Contains("InTransit", StringComparison.Ordinal)
            || member.Contains("Damaged", StringComparison.Ordinal)
            || member is "InjuredImagePath" or "CapturedOverlayImagePath"
        );

    /// <summary>
    /// Creates the common ordered artwork members used by capital ships and starfighters.
    /// </summary>
    /// <returns>The independent member array assigned to one unit type.</returns>
    private static string[] ShipMembers() =>
        new[]
        {
            BackgroundMember,
            "DisplayImagePath",
            "SmallDisplayImagePath",
            "BattleResultImagePath",
            "MessageImagePath",
            "EncyclopediaImagePath",
            "InTransitImagePath",
            "InTransitSmallImagePath",
            "BattleResultInTransitImagePath",
            "DamagedImagePath",
            "DamagedSmallImagePath",
            "BattleResultDamagedImagePath",
        };
}
