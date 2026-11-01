using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Rebellion.Util.Serialization;

/// <summary>
/// Validates and changes one authored member through a form control.
/// </summary>
public sealed class ContentPackEditorField
{
    private readonly ContentPackEditorRecord record;
    private readonly XElement element;
    private readonly bool list;
    private readonly XName itemName;
    private readonly bool numeric;
    private readonly XAttribute attribute;
    private readonly Type valueType;
    private readonly string groupOverride;
    private readonly Action attach;
    private readonly Action detach;
    private readonly XElement parentOverride;

    public string Name { get; }
    public string Caption { get; }
    public string HelpText => ContentPackEditorFieldMetadata.GetDescription(this);
    public IReadOnlyList<string> SectionPath { get; }
    public string Value =>
        attribute?.Value
        ?? (
            list
                ? string.Join(", ", element.Elements().Select(child => child.Value))
                : element.Value
        );
    public bool IsBoolean { get; }
    public bool IsNumber => numeric;
    public bool IsList => list;
    public ContentPackEditorDocument Document => record.Catalog.Document;
    public string MemberName => attribute?.Name.LocalName ?? element.Name.LocalName;
    public bool IsFactionReference =>
        MemberName
            is "RecruitingFactionInstanceIDs"
                or "ManufacturingFactionInstanceIDs"
                or "VisibleToFactionIDs";
    public bool IsTraitReference => record.Kind == "Officer" && MemberName == "Traits";
    public bool IsMediaDirectory =>
        IsMedia
        && (
            MemberName.EndsWith("Root", StringComparison.Ordinal)
            || MemberName.EndsWith("Directories", StringComparison.Ordinal)
        );
    internal ContentPackEditorRecord Record => record;
    internal XElement XmlElement => element;
    internal XElement Container => parentOverride ?? element.Parent;
    internal bool IsIdentity =>
        ContentPackEditorCatalog.IsIdentityName(MemberName)
        && (attribute != null ? element == record.Element : element.Parent == record.Element);
    internal string MediaRoot =>
        MediaType == ContentPackEditorMediaType.Audio && MemberName == "AudioOptions"
            ? element
                .Ancestors()
                .Select(parent => parent.Element("AudioRoot")?.Value)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            : null;
    public string Group =>
        groupOverride
        ?? element
            .AncestorsAndSelf()
            .TakeWhile(parent => parent != record.Element)
            .LastOrDefault()
            ?.Name.LocalName
        ?? MemberName;
    public IReadOnlyList<string> Choices =>
        MemberName is "DisallowedMissionTypeIDs" or "AllowedMissionTypeIDs"
            ? ContentPackEditorFieldSchema.MissionIDs
        : (list ? ContentPackEditorFieldSchema.GetItemType(valueType) : valueType)?.IsEnum == true
            ? Enum.GetNames(list ? ContentPackEditorFieldSchema.GetItemType(valueType) : valueType)
        : Array.Empty<string>();
    public IReadOnlyList<string> Values =>
        list ? element.Elements().Select(child => child.Value).ToArray() : new[] { Value };
    public bool IsMedia => MediaType != ContentPackEditorMediaType.None;
    public ContentPackEditorMediaType MediaType { get; }
    public string[] MediaExtensions =>
        MediaType == ContentPackEditorMediaType.Audio ? new[] { ".wav", ".ogg", ".mp3" }
        : MediaType == ContentPackEditorMediaType.Model ? new[] { ".glb" }
        : MediaType == ContentPackEditorMediaType.Video ? new[] { ".mp4", ".webm" }
        : new[] { ".png", ".jpg", ".jpeg" };
    public bool IsMultiline =>
        MemberName == "Body"
        || element.Name.LocalName.Contains("Description", StringComparison.Ordinal)
        || Value.Contains('\n');
    public string Section { get; }

    /// <summary>
    /// Resolves a media value using its declared root when the format stores a clip name.
    /// </summary>
    /// <param name="value">The authored media value.</param>
    /// <returns>The content address used by the game.</returns>
    public string GetMediaAddress(string value)
    {
        return string.IsNullOrWhiteSpace(MediaRoot) ? value : MediaRoot.TrimEnd('/') + "/" + value;
    }

    /// <summary>
    /// Creates a form field over an existing XML member.
    /// </summary>
    /// <param name="record">The owning record.</param>
    /// <param name="element">The editable XML member.</param>
    /// <param name="name">The readable field label.</param>
    /// <param name="list">Whether the field edits a comma-separated scalar list.</param>
    /// <param name="attribute">The XML attribute edited by this field, when applicable.</param>
    /// <param name="attach">The optional delayed attachment of a new nested member.</param>
    /// <param name="valueTypeOverride">The declared scalar type for a new detached dictionary value.</param>
    /// <param name="groupOverride">The declared section for a new detached dictionary value.</param>
    /// <param name="caption">The short label displayed beside the value.</param>
    /// <param name="sectionPath">The containing form headings, separate from the field label.</param>
    /// <param name="detach">The optional removal of a collection entry when its value is cleared.</param>
    /// <param name="itemNameOverride">The serialized item name for a detached optional collection.</param>
    /// <param name="parentOverride">The containing object for an absent optional member.</param>
    internal ContentPackEditorField(
        ContentPackEditorRecord record,
        XElement element,
        string name,
        bool list = false,
        XAttribute attribute = null,
        Action attach = null,
        Type valueTypeOverride = null,
        string groupOverride = null,
        string caption = null,
        IReadOnlyList<string> sectionPath = null,
        Action detach = null,
        XName itemNameOverride = null,
        XElement parentOverride = null
    )
    {
        this.record = record;
        this.element = element;
        this.attribute = attribute;
        this.attach = attach;
        this.detach = detach;
        this.parentOverride = parentOverride;
        this.groupOverride = groupOverride;
        valueType =
            valueTypeOverride
            ?? ContentPackEditorFieldSchema.GetValueType(record.Element, element, attribute);
        valueType = Nullable.GetUnderlyingType(valueType ?? typeof(string)) ?? valueType;
        Type itemType = ContentPackEditorFieldSchema.GetItemType(valueType);
        this.list = list || itemType != null && TypeHelper.IsScalar(itemType);
        itemName =
            itemNameOverride
            ?? element.Elements().FirstOrDefault()?.Name
            ?? ContentPackEditorFieldSchema.GetItemName(record.Element, element);
        Name = name;
        Caption = ContentPackEditorFieldMetadata.GetCaption(this, caption ?? name);
        SectionPath = sectionPath ?? Array.Empty<string>();
        IsBoolean =
            !this.list
            && (valueType == typeof(bool) || valueType == null && bool.TryParse(Value, out _));
        numeric =
            !this.list
            && !IsBoolean
            && (
                valueType == null
                    ? decimal.TryParse(
                        Value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out _
                    )
                    : valueType.IsPrimitive && valueType != typeof(char)
                        || valueType == typeof(decimal)
            );
        MediaType = ContentPackEditorFieldMetadata.GetMediaType(this);
        Section = ContentPackEditorFieldMetadata.GetSection(this);
    }

    /// <summary>
    /// Checks scalar values using the declared serializer type before changing a document.
    /// </summary>
    /// <param name="values">The proposed scalar or list values.</param>
    internal void ValidateValues(IEnumerable<string> values)
    {
        Type type = list ? ContentPackEditorFieldSchema.GetItemType(valueType) : valueType;
        foreach (string value in values)
        {
            if (value.Length == 0 && detach != null)
                continue;
            ContentPackEditorFieldSchema.ValidateScalar(value, type);
            if (IsIdentity && string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("ID required.");
        }
    }

    /// <summary>
    /// Adds an imported media address to this member or its authored media list.
    /// </summary>
    /// <param name="address">The imported pack address.</param>
    internal void SetImportedAsset(string address)
    {
        if (list)
            SetListValues(Values.Append(address));
        else
            SetValue(address);
    }

    /// <summary>
    /// Replaces a scalar collection without treating punctuation inside an item as a separator.
    /// </summary>
    /// <param name="values">The complete ordered item collection.</param>
    internal void SetListValues(IEnumerable<string> values)
    {
        if (!list || itemName == null)
            throw new InvalidOperationException(
                "This member is not a supported scalar collection."
            );
        string[] replacement = values.ToArray();
        Type itemType = ContentPackEditorFieldSchema.GetItemType(valueType);
        foreach (string value in replacement)
            ContentPackEditorFieldSchema.ValidateScalar(value, itemType);
        if (Values.SequenceEqual(replacement))
            return;
        if (replacement.Length == 0 && detach != null)
        {
            detach();
            element.RemoveNodes();
            record.Catalog.Commit();
            return;
        }
        AttachOptionalMember();
        element.ReplaceNodes(replacement.Select(value => new XElement(itemName, value)));
        record.Catalog.Commit();
    }

    /// <summary>
    /// Applies a validated form value and marks only its source catalog as changed.
    /// </summary>
    /// <param name="value">The user-authored value.</param>
    internal void SetValue(string value)
    {
        value ??= string.Empty;
        if (value == Value)
            return;
        if (value.Length == 0 && detach != null)
        {
            detach();
            element.Value = string.Empty;
            record.Catalog.Commit();
            return;
        }
        if (list)
        {
            SetListValues(
                value.Split(',').Select(item => item.Trim()).Where(item => item.Length > 0)
            );
            return;
        }
        ContentPackEditorFieldSchema.ValidateScalar(value, valueType);
        if (IsBoolean && !bool.TryParse(value, out _))
            throw new ArgumentException("Enter true or false.", nameof(value));
        if (
            numeric
            && !decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
        )
            throw new ArgumentException("Enter a number.", nameof(value));
        if (attribute != null)
        {
            if (
                element == record.Element
                && ContentPackEditorCatalog.IsIdentityName(attribute.Name.LocalName)
            )
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("ID required.", nameof(value));
                if (
                    record.Catalog.Records.Any(candidate =>
                        candidate.Element != record.Element
                        && candidate.Element.Attribute(attribute.Name)?.Value == value
                    )
                )
                    throw new ArgumentException("ID already in use.", nameof(value));
            }
            attribute.Value = value;
            record.Catalog.Commit();
            return;
        }
        if (
            element.Parent == record.Element
            && element.Name.LocalName is "TypeID" or "InstanceID" or "ID"
        )
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("ID required.", nameof(value));
            if (
                element
                    .Document.Descendants(element.Name)
                    .Any(candidate => candidate != element && candidate.Value == value)
            )
                throw new ArgumentException("ID already in use.", nameof(value));
        }
        AttachOptionalMember();
        if ((element.Name.LocalName is "PositionX" or "PositionY") && record.Catalog.IsMap)
        {
            if (
                !int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int coordinate
                )
            )
                throw new ArgumentException("Enter a whole-number coordinate.", nameof(value));
            record.Catalog.Move(
                record,
                element.Name.LocalName == "PositionX" ? coordinate : record.X,
                element.Name.LocalName == "PositionY" ? coordinate : record.Y
            );
            return;
        }
        else
            element.Value = value;
        record.Catalog.Commit();
    }

    /// <summary>
    /// Adds an absent optional member only after an actual edit has been validated.
    /// </summary>
    private void AttachOptionalMember()
    {
        attach?.Invoke();
        if (element != record.Element && element.Parent == null)
            record.Element.Add(element);
    }

    /// <summary>
    /// Converts member names into short sentence-case labels.
    /// </summary>
    /// <param name="name">The serialized member name.</param>
    /// <returns>The readable caption.</returns>
    internal static string FormatName(string name)
    {
        if (name == "HQInstanceID")
            return "HQ Instance ID";
        name = name.Replace("IDs", "Ids", StringComparison.Ordinal);
        string words = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1 $2");
        words = Regex.Replace(words, "([a-z0-9])([A-Z])", "$1 $2");
        string caption =
            words.Length == 0
                ? words
                : char.ToUpperInvariant(words[0]) + words[1..].ToLowerInvariant();
        return Regex.Replace(
            caption,
            @"\b(hq|ids?|ai|ui|xml|x|y|z)\b",
            match =>
                match.Value.Equals("ids", StringComparison.OrdinalIgnoreCase)
                    ? "IDs"
                    : match.Value.ToUpperInvariant(),
            RegexOptions.IgnoreCase
        );
    }
}
