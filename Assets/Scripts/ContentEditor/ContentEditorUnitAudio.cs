using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

/// <summary>
/// Exposes every supported officer response without writing absent voice categories until edited.
/// </summary>
internal static class ContentEditorUnitAudio
{
    /// <summary>
    /// Projects the runtime voice categories as file-list fields over one shared optional owner.
    /// </summary>
    /// <param name="record">The officer being edited.</param>
    /// <returns>The supported voice fields, or no fields for other unit types.</returns>
    internal static IReadOnlyList<ContentPackEditorField> GetFields(ContentPackEditorRecord record)
    {
        if (record.Kind != nameof(Officer))
            return Array.Empty<ContentPackEditorField>();
        XElement voice =
            record.Element.Element(nameof(Officer.VoiceSet))
            ?? new XElement(nameof(Officer.VoiceSet));
        return ((OfficerVoiceLineType[])Enum.GetValues(typeof(OfficerVoiceLineType)))
            .Select(type => CreateField(record, voice, type.ToString()))
            .ToArray();
    }

    /// <summary>
    /// Creates an independently editable response while keeping its containing voice set shared.
    /// </summary>
    /// <param name="record">The owning officer.</param>
    /// <param name="voice">The existing or detached voice set.</param>
    /// <param name="member">The serialized response name.</param>
    /// <returns>The file-list field used by imports, removal, inheritance, and undo.</returns>
    private static ContentPackEditorField CreateField(
        ContentPackEditorRecord record,
        XElement voice,
        string member
    )
    {
        XElement category = voice.Element(member) ?? new XElement(member);
        return new ContentPackEditorField(
            record,
            category,
            nameof(Officer.VoiceSet) + " / " + member,
            caption: ContentPackEditorField.FormatName(member),
            attach: () =>
            {
                if (voice.Parent == null)
                    record.Element.Add(voice);
                if (category.Parent == null)
                    voice.Add(category);
            },
            valueTypeOverride: SerializationMetadata.GetElementType(
                typeof(OfficerVoiceSet),
                member
            ),
            parentOverride: voice,
            groupOverride: nameof(Officer.VoiceSet),
            itemNameOverride: SerializationMetadata.GetCollectionItemName(
                typeof(OfficerVoiceSet),
                member
            )
        );
    }
}
