using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;

/// <summary>
/// Represents one configured Espionage report variant and its faction-specific text.
/// </summary>
internal sealed class ContentEditorMissionReport
{
    internal const string Section = "Report text";

    internal string Outcome { get; }
    internal string CompletionReason { get; }
    internal string Key => Outcome + "/" + CompletionReason;
    internal string Caption =>
        ContentPackEditorField.FormatName(Outcome)
        + (
            CompletionReason == "None"
                ? string.Empty
                : " (" + ContentPackEditorField.FormatName(CompletionReason) + ")"
        );
    internal ContentPackEditorRecord Shared { get; }
    internal ContentPackEditorRecord Custom { get; }
    internal IReadOnlyList<ContentPackEditorField> Fields { get; }

    /// <summary>
    /// Binds one shared or customized report to the text that the mission can display.
    /// </summary>
    /// <param name="outcome">The authored result outcome.</param>
    /// <param name="completionReason">The authored reason or None.</param>
    /// <param name="shared">The shared definition, when configured.</param>
    /// <param name="custom">The recipient-specific definition, when configured.</param>
    private ContentEditorMissionReport(
        string outcome,
        string completionReason,
        ContentPackEditorRecord shared,
        ContentPackEditorRecord custom
    )
    {
        Outcome = outcome;
        CompletionReason = completionReason;
        Shared = shared;
        Custom = custom;
        Fields =
            custom == null
                ? Array.Empty<ContentPackEditorField>()
                : new[]
                {
                    nameof(MessageDefinition.Subject),
                    nameof(MessageDefinition.Body),
                    nameof(MessageDefinition.UnattributedSubject),
                    nameof(MessageDefinition.UnattributedBody),
                    nameof(MessageDefinition.PersonnelReturningTemplate),
                    nameof(MessageDefinition.PersonnelLostTemplate),
                    nameof(MessageDefinition.DetailListHeaderTemplate),
                    nameof(MessageDefinition.DetailListItemTemplate),
                }
                    .Where(member =>
                        custom.Element.Element(member) != null
                        || shared?.Element.Element(member) != null
                    )
                    .Select(custom.GetField)
                    .ToArray();
    }

    /// <summary>
    /// Checks whether a definition uses a selector that can produce an Espionage report.
    /// </summary>
    /// <param name="record">The authored message definition.</param>
    /// <returns>Whether it matches the mission report lookup's Espionage selectors.</returns>
    internal static bool IsEspionageReport(ContentPackEditorRecord record) =>
        record.Kind == nameof(MessageDefinition)
        && (string)record.Element.Element(nameof(MessageDefinition.ResultType))
            == nameof(MessageResultType.MissionReport)
        && (string)record.Element.Element(nameof(MessageDefinition.MissionTypeID))
            == MissionTypeIDs.Espionage
        && (string)record.Element.Element(nameof(MessageDefinition.Outcome))
            is "Success"
                or "Failed"
                or "Foiled"
        && ReadSelector(record, nameof(MessageDefinition.PlanetOwnership)) == "None"
        && ReadSelector(record, nameof(MessageDefinition.BuildingType)) == "None"
        && ReadSelector(record, nameof(MessageDefinition.ManufacturingType)) == "None";

    /// <summary>
    /// Groups reports by the same outcome and reason used during gameplay.
    /// </summary>
    /// <param name="records">The linked report catalog records in lookup order.</param>
    /// <param name="factionID">The recipient faction instance ID.</param>
    /// <returns>The shared and local alternatives available to this faction.</returns>
    internal static IReadOnlyList<ContentEditorMissionReport> Create(
        IEnumerable<ContentPackEditorRecord> records,
        string factionID
    ) =>
        records
            .Where(IsEspionageReport)
            .Where(record => GetFactionID(record) == null || GetFactionID(record) == factionID)
            .GroupBy(record =>
                (
                    Outcome: ReadSelector(record, nameof(MessageDefinition.Outcome)),
                    Reason: ReadSelector(record, nameof(MessageDefinition.MissionCompletionReason))
                )
            )
            .Select(group => new ContentEditorMissionReport(
                group.Key.Outcome,
                group.Key.Reason,
                group.FirstOrDefault(record => GetFactionID(record) == null),
                group.FirstOrDefault(record => GetFactionID(record) != null)
            ))
            .OrderBy(report =>
                report.Outcome switch
                {
                    "Success" => 0,
                    "Failed" => 1,
                    _ => 2,
                }
            )
            .ThenBy(report => report.CompletionReason != "None")
            .ThenBy(report => report.Caption, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Reads the recipient selector, treating absent or blank values as shared.
    /// </summary>
    /// <param name="record">The report definition.</param>
    /// <returns>The authored recipient ID or null.</returns>
    internal static string GetFactionID(ContentPackEditorRecord record)
    {
        string id = (string)record.Element.Element(nameof(MessageDefinition.FactionInstanceID));
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    /// <summary>
    /// Reads a report filter using the serialized enum's default for omitted members.
    /// </summary>
    /// <param name="record">The report definition.</param>
    /// <param name="member">The serialized enum member.</param>
    /// <returns>The serialized selector value.</returns>
    private static string ReadSelector(ContentPackEditorRecord record, string member) =>
        (string)record.Element.Element(member) ?? "None";
}
