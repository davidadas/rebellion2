using System;
using System.Collections.Generic;
using System.Linq;

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
    };
    private static readonly IReadOnlyList<string> _unitGenerationSources = new[]
    {
        "DisplayImagePath",
        "MessageImagePath",
        "EncyclopediaImagePath",
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
