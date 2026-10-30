using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

/// <summary>
/// Edits one named unit, map item, event, or configuration record.
/// </summary>
public sealed class ContentPackEditorRecord
{
    private readonly Dictionary<string, ContentPackEditorField> pendingRatings = new Dictionary<
        string,
        ContentPackEditorField
    >(StringComparer.Ordinal);
    internal XElement Element { get; }
    public ContentPackEditorCatalog Catalog { get; }
    public string Name =>
        Element.Element("DisplayName")?.Value ?? Element.Element("Name")?.Value ?? ID;
    public string ID =>
        Element.Element("TypeID")?.Value
        ?? Element.Element("InstanceID")?.Value
        ?? Element.Element("ID")?.Value
        ?? Element.Attribute("ID")?.Value
        ?? Element.Name.LocalName;
    public string Kind => Element.Name.LocalName;
    public string Key =>
        Catalog.Document.Address
        + "#"
        + (
            ContentPackEditorCatalog.GetIdentityValues(Element).Any()
                ? ID
                : Kind + "[" + Element.ElementsBeforeSelf(Element.Name).Count() + "]"
        );
    public string ImageAddress =>
        new[]
        {
            Element.Element("EncyclopediaImagePath")?.Value,
            Element.Element("DisplayImagePath")?.Value,
            Element.Element("ImagePath")?.Value,
            Element.Element("PlanetIconPath")?.Value,
        }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    public int X => ContentPackEditorCatalog.ReadCoordinate(Element, "PositionX");
    public int Y => ContentPackEditorCatalog.ReadCoordinate(Element, "PositionY");
    public IReadOnlyList<ContentPackEditorRecord> Planets =>
        (Element.Element("Planets")?.Elements("Planet") ?? Enumerable.Empty<XElement>())
            .Select(element => new ContentPackEditorRecord(Catalog, element))
            .ToArray();

    /// <summary>
    /// Wraps an existing record in its owning working catalog.
    /// </summary>
    /// <param name="catalog">The owning catalog.</param>
    /// <param name="element">The authored record.</param>
    internal ContentPackEditorRecord(ContentPackEditorCatalog catalog, XElement element)
    {
        Catalog = catalog;
        Element = element;
    }

    /// <summary>
    /// Projects editable members into grouped fields while retaining their XML nodes.
    /// </summary>
    /// <returns>The editable field collection.</returns>
    public IReadOnlyList<ContentPackEditorField> GetFields()
    {
        return Element
            .Attributes()
            .Select(attribute => new ContentPackEditorField(
                this,
                Element,
                ContentPackEditorField.FormatName(attribute.Name.LocalName),
                attribute: attribute
            ))
            .Concat(
                Element
                    .Elements()
                    .Where(element => element.Name.LocalName != "Planets")
                    .SelectMany(element =>
                        CreateFields(element, string.Empty, Array.Empty<string>())
                    )
            )
            .ToArray();
    }

    /// <summary>
    /// Gets an authored member or an uncommitted optional member with its declared default value.
    /// </summary>
    /// <param name="memberName">The serialized member name.</param>
    /// <returns>The editable member, which is attached only when its value changes.</returns>
    public ContentPackEditorField GetField(string memberName)
    {
        XElement element = Element.Element(memberName);
        if (element == null)
        {
            element = new XElement(memberName);
            Type type = ContentPackEditorFieldSchema.GetValueType(Element, element);
            if (type == null)
                throw new InvalidOperationException(
                    "This record has no editable member named " + memberName + "."
                );
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (TypeHelper.IsScalar(type) && type != typeof(string))
                element.Value =
                    type == typeof(bool)
                        ? "false"
                        : Convert.ToString(
                            Activator.CreateInstance(type),
                            CultureInfo.InvariantCulture
                        );
        }
        return new ContentPackEditorField(
            this,
            element,
            ContentPackEditorField.FormatName(memberName)
        );
    }

    /// <summary>
    /// Gets a skill rating, treating an absent dictionary entry as zero until it is edited.
    /// </summary>
    /// <param name="skill">The serialized skill name.</param>
    /// <returns>The reversible rating field.</returns>
    public ContentPackEditorField GetRatingField(string skill)
    {
        if (pendingRatings.TryGetValue(skill, out ContentPackEditorField pending))
            return pending;
        XElement ratings = Element.Element("Ratings") ?? new XElement("Ratings");
        XElement entry =
            ratings.Elements("Entry").FirstOrDefault(item => item.Element("Key")?.Value == skill)
            ?? new XElement("Entry", new XElement("Key", skill), new XElement("Value", 0));
        ContentPackEditorField field = new ContentPackEditorField(
            this,
            entry.Element("Value"),
            ContentPackEditorField.FormatName(skill),
            valueTypeOverride: typeof(int),
            groupOverride: "Ratings",
            attach: () =>
            {
                XElement target = Element.Element("Ratings");
                if (target == null)
                {
                    target = ratings;
                    Element.Add(target);
                }
                if (entry.Parent == null)
                    target.Add(entry);
            }
        );
        pendingRatings.Add(skill, field);
        return field;
    }

    /// <summary>
    /// Recursively projects scalar and collection fields with readable dictionary keys.
    /// </summary>
    /// <param name="element">The member being projected.</param>
    /// <param name="prefix">The readable parent path.</param>
    /// <param name="sections">The containing form sections.</param>
    /// <param name="sectionCaption">The optional collection item's identifying heading.</param>
    /// <returns>The descendant editable fields.</returns>
    private IEnumerable<ContentPackEditorField> CreateFields(
        XElement element,
        string prefix,
        IReadOnlyList<string> sections,
        string sectionCaption = null
    )
    {
        string name = element.Name.LocalName;
        string label = ContentPackEditorField.FormatName(name);
        string caption = prefix + label;
        IReadOnlyList<string> childrenSections =
            name == "Value" && element.Parent?.Element("Key") != null
            || name.StartsWith("ArrayOf", StringComparison.Ordinal)
                ? sections
                : sections.Append(sectionCaption ?? label).ToArray();
        Type declaredType = ContentPackEditorFieldSchema.GetValueType(Element, element);
        Type declaredItemType = ContentPackEditorFieldSchema.GetItemType(declaredType);
        foreach (XAttribute attribute in element.Attributes())
            yield return new ContentPackEditorField(
                this,
                element,
                caption + " / " + ContentPackEditorField.FormatName(attribute.Name.LocalName),
                attribute: attribute,
                caption: ContentPackEditorField.FormatName(attribute.Name.LocalName),
                sectionPath: childrenSections
            );
        if (!element.HasElements)
        {
            if (declaredItemType != null && TypeHelper.IsScalar(declaredItemType))
            {
                yield return new ContentPackEditorField(
                    this,
                    element,
                    caption,
                    true,
                    caption: label,
                    sectionPath: sections
                );
                yield break;
            }
            if (declaredItemType != null)
                yield break;
            if (!element.HasAttributes || !string.IsNullOrEmpty(element.Value))
                yield return new ContentPackEditorField(
                    this,
                    element,
                    caption,
                    caption: label,
                    sectionPath: sections
                );
            yield break;
        }
        if (
            (
                declaredType == null
                || declaredItemType != null && TypeHelper.IsScalar(declaredItemType)
            )
            && element
                .Elements()
                .All(child =>
                    !child.HasElements
                    && !child.HasAttributes
                    && child.Name == element.Elements().First().Name
                )
        )
        {
            yield return new ContentPackEditorField(
                this,
                element,
                caption,
                true,
                caption: label,
                sectionPath: sections
            );
            yield break;
        }
        Dictionary<XName, int> counts = element
            .Elements()
            .GroupBy(child => child.Name)
            .ToDictionary(group => group.Key, group => group.Count());
        Dictionary<XName, int> indices = new Dictionary<XName, int>();
        foreach (XElement child in element.Elements())
        {
            indices.TryGetValue(child.Name, out int index);
            indices[child.Name] = ++index;
            if (child.Name.LocalName == "Entry" && child.Element("Key") != null)
            {
                XElement value = child.Element("Value");
                string key = child.Element("Key").Value;
                if (value == null)
                    continue;
                if (!value.HasElements)
                    yield return new ContentPackEditorField(
                        this,
                        value,
                        key,
                        sectionPath: childrenSections
                    );
                else if (declaredType == typeof(Dictionary<PrimaryWeaponType, int[]>))
                {
                    string[] arcs = CapitalShip
                        .PrimaryWeaponArcs.Select(arc => arc.ToString())
                        .Append("Range")
                        .ToArray();
                    int arcIndex = 0;
                    foreach (XElement number in value.Descendants("int"))
                    {
                        string arc =
                            arcIndex < arcs.Length
                                ? arcs[arcIndex++]
                                : (++arcIndex).ToString(CultureInfo.InvariantCulture);
                        yield return new ContentPackEditorField(
                            this,
                            number,
                            key + " / " + arc,
                            caption: arc,
                            sectionPath: childrenSections.Append(key).ToArray()
                        );
                    }
                }
                else
                    foreach (
                        ContentPackEditorField field in CreateFields(
                            value,
                            key + " / ",
                            childrenSections.Append(key).ToArray()
                        )
                    )
                        yield return field;
            }
            else
                foreach (
                    ContentPackEditorField field in CreateFields(
                        child,
                        caption + " / ",
                        childrenSections,
                        counts[child.Name] > 1
                        || declaredItemType != null
                        || child.Name.LocalName is "Message" or "NamePool"
                            ? CollectionCaption(child, index)
                            : null
                    )
                )
                    yield return field;
        }
    }

    /// <summary>
    /// Names collection entries by their authored identity instead of their serializer index.
    /// </summary>
    /// <param name="element">The collection entry.</param>
    /// <param name="index">The fallback one-based position.</param>
    /// <returns>The readable section heading.</returns>
    private static string CollectionCaption(XElement element, int index)
    {
        if (
            element.Name.LocalName == "NamePool"
            && !string.IsNullOrWhiteSpace(element.Element("NamePoolID")?.Value)
        )
            return "Pool " + element.Element("NamePoolID").Value;
        if (
            element.Name.LocalName == "Message"
            && !string.IsNullOrWhiteSpace(element.Element("Title")?.Value)
        )
            return element.Element("Title").Value;
        return ContentPackEditorField.FormatName(element.Name.LocalName) + " " + index;
    }
}
