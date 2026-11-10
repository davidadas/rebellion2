using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Rebellion.Game;
using Rebellion.Game.Messages;
using Rebellion.Util.Serialization;

/// <summary>
/// Composes explicit setting overrides while preserving legacy whole-document replacements.
/// Identified records merge by identity; ordered and unidentified collections replace as a unit.
/// </summary>
public static class ContentXmlOverrides
{
    private const string _inherit = "Inherit";
    private const string _remove = "Remove";
    private const string _removeAttributes = "RemoveAttributes";

    /// <summary>
    /// Reads the content layers in base-to-override order.
    /// </summary>
    /// <param name="resolver">The content path owner.</param>
    /// <param name="address">The logical XML address.</param>
    /// <param name="defaults">Optional application defaults beneath pack-owned rules.</param>
    /// <returns>The resolved XML, without authoring directives.</returns>
    public static string Load(ContentFileResolver resolver, string address, string defaults = null)
    {
        IReadOnlyList<string> layers = resolver.ResolveFileLayers(address);
        if (layers.Count == 0)
            throw new FileNotFoundException("Content document not found: " + address);
        string result = defaults;
        List<string> overlays = new List<string>();
        foreach (string path in layers.Reverse())
        {
            string authored = File.ReadAllText(path);
            if (IsOverlay(authored))
                overlays.Add(authored);
            else
            {
                result = defaults == null ? authored : MergeRules(defaults, authored);
                break;
            }
        }
        foreach (string overlay in overlays.AsEnumerable().Reverse())
            result = Merge(result, overlay);
        return result ?? throw new FileNotFoundException("Content document not found: " + address);
    }

    /// <summary>
    /// Checks whether a document explicitly requests setting inheritance.
    /// </summary>
    /// <param name="text">The authored XML.</param>
    /// <returns>True for an explicit setting overlay.</returns>
    public static bool IsOverlay(string text) =>
        text != null && (bool?)XElement.Parse(text).Attribute(_inherit) == true;

    /// <summary>
    /// Resolves either legacy sparse rules or explicit setting directives over their defaults.
    /// </summary>
    /// <param name="defaults">The applicable resolved rules defaults.</param>
    /// <param name="authored">The authored rules layer.</param>
    /// <returns>The resolved rules without authoring directives.</returns>
    public static string MergeRules(string defaults, string authored)
    {
        if (IsOverlay(authored))
            return Merge(defaults, authored);
        XmlDocument baseline = new XmlDocument();
        baseline.LoadXml(defaults);
        XmlDocument overlay = new XmlDocument();
        overlay.LoadXml(authored);
        return new GameSerializer(typeof(GameConfig)).Compose(baseline, overlay).OuterXml;
    }

    /// <summary>
    /// Applies one sparse document over its resolved base.
    /// </summary>
    /// <param name="defaults">The inherited XML.</param>
    /// <param name="overlay">The authored setting overlay.</param>
    /// <returns>The composed XML.</returns>
    public static string Merge(string defaults, string overlay)
    {
        if (defaults == null)
            throw new InvalidDataException("A setting override requires an inherited document.");
        XElement target = XElement.Parse(defaults);
        XElement source = XElement.Parse(overlay);
        if (target.Name != source.Name)
            throw new InvalidDataException("A setting override must keep its inherited root name.");
        if (IsEmpty(overlay))
            return defaults;
        MergeElement(target, source);
        return target.ToString();
    }

    /// <summary>
    /// Creates an empty overlay without copying inherited settings.
    /// </summary>
    /// <param name="defaults">The inherited XML.</param>
    /// <returns>The empty authored overlay.</returns>
    public static string Create(string defaults) =>
        new XElement(XElement.Parse(defaults).Name, new XAttribute(_inherit, true)).ToString();

    /// <summary>
    /// Records edits without materializing inherited values into the authored layer.
    /// </summary>
    /// <param name="defaults">The resolved inherited XML.</param>
    /// <param name="effective">The edited displayed XML.</param>
    /// <param name="previous">The preceding authored layer, including explicitly pinned values.</param>
    /// <returns>The sparse authored XML.</returns>
    public static string Update(string defaults, string effective, string previous)
    {
        XElement baseline = XElement.Parse(defaults);
        XElement edited = XElement.Parse(effective);
        XElement authored = previous == null ? null : XElement.Parse(previous);
        if (IsAtomicCollection(baseline))
            return XNode.DeepEquals(baseline, edited) && (previous == null || IsEmpty(previous))
                ? Create(defaults)
                : edited.ToString();
        XElement result = Difference(baseline, edited, authored) ?? new XElement(baseline.Name);
        result.SetAttributeValue(_inherit, true);
        return result.ToString();
    }

    /// <summary>
    /// Checks whether an overlay contains any authored values or record removals.
    /// </summary>
    /// <param name="text">The authored XML.</param>
    /// <returns>True when only the inheritance directive remains.</returns>
    public static bool IsEmpty(string text)
    {
        XElement root = XElement.Parse(text);
        return !root.Elements().Any()
            && !root.Attributes().Any(attribute => attribute.Name != _inherit)
            && string.IsNullOrWhiteSpace(root.Value);
    }

    /// <summary>
    /// Checks whether a setting can be reset independently of an ordered list.
    /// </summary>
    /// <param name="element">The displayed setting.</param>
    /// <returns>True outside the contents of an atomic collection.</returns>
    public static bool CanResetSetting(XElement element) =>
        !element.Ancestors().Any(IsAtomicCollection);

    /// <summary>
    /// Locates an effective member in another version of the same document.
    /// </summary>
    /// <param name="source">The displayed member.</param>
    /// <param name="root">The alternate document root.</param>
    /// <returns>The matching member, or null when that layer omits it.</returns>
    public static XElement FindEquivalent(XElement source, XElement root)
    {
        if (source == null || root == null)
            return null;
        XElement[] path = source.AncestorsAndSelf().Reverse().ToArray();
        if (path[0].Name != root.Name)
            return null;
        XElement current = root;
        foreach (XElement step in path.Skip(1))
        {
            current = FindChild(current, step);
            if (current == null)
                break;
        }
        return current;
    }

    /// <summary>
    /// Checks whether an authored layer owns a displayed scalar or collection.
    /// </summary>
    /// <param name="text">The authored overlay.</param>
    /// <param name="element">The displayed member.</param>
    /// <param name="attribute">The edited attribute name, when applicable.</param>
    /// <returns>True when the value is explicitly authored.</returns>
    public static bool IsOverridden(string text, XElement element, XName attribute = null) =>
        IsOverridden(XElement.Parse(text), element, attribute);

    /// <summary>
    /// Checks ownership using an already parsed authored layer.
    /// </summary>
    /// <param name="root">The authored layer root.</param>
    /// <param name="element">The displayed member.</param>
    /// <param name="attribute">The edited attribute name, when applicable.</param>
    /// <returns>True when the member is explicitly authored.</returns>
    public static bool IsOverridden(XElement root, XElement element, XName attribute = null)
    {
        XElement value = FindEquivalent(element, root);
        return value != null
            && (
                attribute != null
                    ? value.Attribute(attribute) != null
                        || RemovedAttributes(value).Contains(attribute)
                    : !IsIdentity(element)
            );
    }

    /// <summary>
    /// Removes one authored setting and prunes empty override branches.
    /// </summary>
    /// <param name="text">The authored overlay.</param>
    /// <param name="element">The displayed member being reset.</param>
    /// <param name="attribute">The edited attribute name, when applicable.</param>
    /// <returns>The resulting authored overlay.</returns>
    public static string Reset(string text, XElement element, XName attribute = null)
    {
        XElement root = XElement.Parse(text);
        XElement target = FindEquivalent(element, root);
        if (target == null)
            return text;
        XElement parent = target.Parent;
        if (attribute == null)
            target.Remove();
        else
        {
            target.Attribute(attribute)?.Remove();
            XName[] remaining = RemovedAttributes(target)
                .Where(name => name != attribute)
                .ToArray();
            target.SetAttributeValue(
                _removeAttributes,
                remaining.Length == 0
                    ? null
                    : string.Join(" ", remaining.Select(name => name.ToString()))
            );
            parent = target;
        }
        while (parent != null && parent != root && HasOnlyIdentity(parent))
        {
            XElement next = parent.Parent;
            parent.Remove();
            parent = next;
        }
        return root.ToString();
    }

    /// <summary>
    /// Applies one branch using presence rather than deserialized default values.
    /// </summary>
    /// <param name="target">The inherited branch to update.</param>
    /// <param name="source">The explicit authored branch.</param>
    private static void MergeElement(XElement target, XElement source)
    {
        Type type = ElementType(target);
        foreach (XName name in RemovedAttributes(source))
            target.Attribute(name)?.Remove();
        foreach (XAttribute attribute in source.Attributes().Where(IsValueAttribute))
        {
            string member = SerializationMetadata.GetMemberIdentity(type, attribute.Name.LocalName);
            if (member != null)
            {
                foreach (
                    XElement child in target
                        .Elements()
                        .Where(child =>
                            SerializationMetadata.GetMemberIdentity(type, child.Name.LocalName)
                            == member
                        )
                        .ToArray()
                )
                    child.Remove();
                foreach (
                    XAttribute existing in target
                        .Attributes()
                        .Where(existing =>
                            existing.Name != attribute.Name
                            && SerializationMetadata.GetMemberIdentity(
                                type,
                                existing.Name.LocalName
                            ) == member
                        )
                        .ToArray()
                )
                    existing.Remove();
            }
            target.SetAttributeValue(attribute.Name, attribute.Value);
        }
        if (!source.HasElements && !target.HasElements)
        {
            target.ReplaceNodes(source.Nodes().Select(CloneNode));
            return;
        }
        if (IsAtomicCollection(target))
        {
            target.ReplaceNodes(source.Nodes().Select(CloneNode));
            return;
        }
        foreach (XElement child in source.Elements())
        {
            string member = SerializationMetadata.GetMemberIdentity(type, child.Name.LocalName);
            if (member != null)
                foreach (
                    XAttribute attribute in target
                        .Attributes()
                        .Where(attribute =>
                            SerializationMetadata.GetMemberIdentity(type, attribute.Name.LocalName)
                            == member
                        )
                        .ToArray()
                )
                    attribute.Remove();
            XElement existing = FindChild(target, child);
            if ((bool?)child.Attribute(_remove) == true)
            {
                existing?.Remove();
                continue;
            }
            if (existing == null)
                target.Add(new XElement(child));
            else
                MergeElement(existing, child);
        }
    }

    /// <summary>
    /// Builds the authored differences while retaining previously explicit settings.
    /// </summary>
    /// <param name="baseline">The inherited branch.</param>
    /// <param name="edited">The displayed edited branch.</param>
    /// <param name="previous">The preceding authored branch.</param>
    /// <returns>The authored branch, or null for an entirely inherited branch.</returns>
    private static XElement Difference(XElement baseline, XElement edited, XElement previous)
    {
        if (baseline == null)
            return new XElement(edited);
        if (edited == null)
        {
            XElement removed = IdentityElement(baseline);
            removed.SetAttributeValue(_remove, true);
            return removed;
        }
        if (IsAtomicCollection(baseline) || !baseline.HasElements && !edited.HasElements)
            return !XNode.DeepEquals(baseline, edited) || previous != null
                ? WithElementRemovals(
                    edited,
                    previous,
                    WithAttributeRemovals(baseline, edited, previous, new XElement(edited))
                )
                : null;
        XElement result = IdentityElement(edited);
        foreach (XAttribute attribute in edited.Attributes().Where(IsValueAttribute))
            if (
                attribute.Value != (string)baseline.Attribute(attribute.Name)
                || previous?.Attribute(attribute.Name) != null
            )
                result.SetAttributeValue(attribute.Name, attribute.Value);
        foreach (XElement child in edited.Elements())
        {
            if (IsIdentity(child))
                continue;
            XElement difference = Difference(
                FindChild(baseline, child),
                child,
                previous == null ? null : FindChild(previous, child)
            );
            if (difference != null)
                result.Add(difference);
        }
        foreach (XElement child in baseline.Elements())
            if (FindChild(edited, child) == null)
                result.Add(Difference(child, null, null));
        WithAttributeRemovals(baseline, edited, previous, result);
        WithElementRemovals(edited, previous, result);
        return HasOnlyIdentity(result) ? null : result;
    }

    /// <summary>
    /// Retains explicit deletions when a base update also omits the removed member.
    /// </summary>
    /// <param name="edited">The displayed branch after editing.</param>
    /// <param name="previous">The preceding authored branch.</param>
    /// <param name="result">The sparse branch being produced.</param>
    /// <returns>The branch including still-authored deletions.</returns>
    private static XElement WithElementRemovals(XElement edited, XElement previous, XElement result)
    {
        if (previous != null)
            foreach (
                XElement removal in previous
                    .Elements()
                    .Where(child => (bool?)child.Attribute(_remove) == true)
            )
                if (FindChild(edited, removal) == null && FindChild(result, removal) == null)
                    result.Add(new XElement(removal));
        return result;
    }

    /// <summary>
    /// Preserves explicit attribute deletion separately from inheritance.
    /// </summary>
    /// <param name="baseline">The inherited attributes.</param>
    /// <param name="edited">The displayed edited element.</param>
    /// <param name="previous">The preceding authored element.</param>
    /// <param name="result">The sparse element being produced.</param>
    /// <returns>The element including explicit attribute removals.</returns>
    private static XElement WithAttributeRemovals(
        XElement baseline,
        XElement edited,
        XElement previous,
        XElement result
    )
    {
        XName[] removed = baseline
            .Attributes()
            .Where(IsValueAttribute)
            .Select(attribute => attribute.Name)
            .Concat(previous == null ? Enumerable.Empty<XName>() : RemovedAttributes(previous))
            .Where(name => edited.Attribute(name) == null)
            .Distinct()
            .ToArray();
        result.SetAttributeValue(
            _removeAttributes,
            removed.Length == 0 ? null : string.Join(" ", removed.Select(name => name.ToString()))
        );
        return result;
    }

    /// <summary>
    /// Reads the names of attributes explicitly removed by one overlay.
    /// </summary>
    /// <param name="element">The authored element.</param>
    /// <returns>The removed attribute names.</returns>
    private static IEnumerable<XName> RemovedAttributes(XElement element) =>
        ((string)element.Attribute(_removeAttributes) ?? string.Empty)
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(XName.Get);

    /// <summary>
    /// Matches identified collection records or singleton object members.
    /// </summary>
    /// <param name="parent">The branch to search.</param>
    /// <param name="source">The requested member or record.</param>
    /// <returns>The matching child, or null.</returns>
    private static XElement FindChild(XElement parent, XElement source)
    {
        string key = Identity(source);
        Type type = ElementType(parent);
        string member = SerializationMetadata.GetMemberIdentity(type, source.Name.LocalName);
        IEnumerable<XElement> candidates = parent
            .Elements()
            .Where(child =>
                child.Name == source.Name
                || member != null
                    && SerializationMetadata.GetMemberIdentity(type, child.Name.LocalName) == member
            );
        if (key != null)
            return candidates.SingleOrDefault(child => Identity(child) == key);
        int index = source.ElementsBeforeSelf(source.Name).Count();
        return candidates.ElementAtOrDefault(index);
    }

    /// <summary>
    /// Recognizes the stable identities already used by content catalogs.
    /// </summary>
    /// <param name="element">The record to inspect.</param>
    /// <returns>The identity member name, or null for an ordered item.</returns>
    private static XName IdentityName(XElement element)
    {
        string name = IdentityMemberName(element.Name.LocalName);
        return name != null && (element.Element(name) != null || element.Attribute(name) != null)
            ? name
            : null;
    }

    /// <summary>
    /// Identifies the stable selector declared by a supported record kind even in empty lists.
    /// </summary>
    /// <param name="elementName">The serialized record name.</param>
    /// <returns>The stable selector name, or null for ordered records.</returns>
    private static string IdentityMemberName(string elementName) =>
        elementName switch
        {
            "Officer"
            or "CapitalShip"
            or "Starfighter"
            or "Regiment"
            or "SpecialForce"
            or "Building"
            or "PlanetSector"
            or "Unit" => "TypeID",
            "Planet" or "GameEvent" or "Faction" => "InstanceID",
            "FactionTheme" => "FactionInstanceID",
            "Trait" => "ID",
            "Entry" => "Key",
            _ => null,
        };

    /// <summary>
    /// Reads one record's stable identity.
    /// </summary>
    /// <param name="element">The record to inspect.</param>
    /// <returns>The identity, or null for an ordered item.</returns>
    private static string Identity(XElement element)
    {
        XName name = IdentityName(element);
        return name == null
            ? null
            : (string)element.Element(name) ?? (string)element.Attribute(name);
    }

    /// <summary>
    /// Creates the identity-only selector for an inherited record.
    /// </summary>
    /// <param name="element">The identified record.</param>
    /// <returns>The record selector.</returns>
    private static XElement IdentityElement(XElement element)
    {
        XElement result = new XElement(element.Name);
        XName key = IdentityName(element);
        if (key != null)
            if (element.Element(key) is XElement value)
                result.Add(new XElement(value));
            else
                result.SetAttributeValue(key, (string)element.Attribute(key));
        return result;
    }

    /// <summary>
    /// Checks whether a member is its containing record's identity selector.
    /// </summary>
    /// <param name="element">The member to inspect.</param>
    /// <returns>True for an identity member.</returns>
    private static bool IsIdentity(XElement element) =>
        element.Parent != null && IdentityName(element.Parent) == element.Name;

    /// <summary>
    /// Checks whether a sparse branch carries only structural identity.
    /// </summary>
    /// <param name="element">The branch to inspect.</param>
    /// <returns>True when no setting or removal remains.</returns>
    private static bool HasOnlyIdentity(XElement element) =>
        !element
            .Attributes()
            .Any(attribute => attribute.Name != _inherit && attribute.Name != IdentityName(element))
        && element.Elements().All(IsIdentity)
        && (element.HasElements || string.IsNullOrWhiteSpace(element.Value));

    /// <summary>
    /// Preserves explicit ordered-list replacement semantics, including empty lists.
    /// </summary>
    /// <param name="element">The inherited element.</param>
    /// <returns>True for a collection without stable record identities.</returns>
    private static bool IsAtomicCollection(XElement element)
    {
        XElement[] children = element.Elements().ToArray();
        if (children.Length > 0 && children.All(child => Identity(child) != null))
            return false;
        Type type = ElementType(element);
        Type itemType = SerializationMetadata.GetCollectionItemType(type);
        if (
            type != null && TypeHelper.IsDictionary(type)
            || itemType != null
                && IdentityMemberName(SerializationMetadata.GetElementName(itemType)) != null
        )
            return false;
        return type != null && TypeHelper.IsEnumerable(type)
            || children.GroupBy(child => child.Name).Any(group => group.Count() > 1);
    }

    /// <summary>
    /// Resolves declared collection types through serializer metadata.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>The declared type, or null for an unknown extension.</returns>
    private static Type ElementType(XElement element)
    {
        if (element.Parent == null)
            return element.Name.LocalName switch
            {
                "Scenario" => typeof(ContentScenarioDefinition),
                "ContentPreload" => typeof(ContentPreloadManifest),
                "MessageDefinitions" => typeof(MessageDefinition[]),
                "Faction" when element.Element("FactionDataPath") != null =>
                    typeof(ContentFactionDefinition),
                _ => RegisteredType(element.Name.LocalName),
            };
        Type parent = ElementType(element.Parent);
        if (parent != null)
        {
            Type item = SerializationMetadata.GetCollectionItemType(parent);
            if (item != null)
                return item;
            Type member = SerializationMetadata.GetElementType(parent, element.Name.LocalName);
            if (member != null)
                return member;
        }
        return RegisteredType(element.Name.LocalName);
    }

    /// <summary>
    /// Resolves an unambiguous registered object name.
    /// </summary>
    /// <param name="name">The serialized object name.</param>
    /// <returns>The unique registered type, or null.</returns>
    private static Type RegisteredType(string name)
    {
        IReadOnlyList<Type> types = SerializationMetadata.GetTypes(name);
        return types.Count == 1 ? types[0] : null;
    }

    /// <summary>
    /// Distinguishes data attributes from overlay instructions.
    /// </summary>
    /// <param name="attribute">The authored attribute.</param>
    /// <returns>True for a data attribute.</returns>
    private static bool IsValueAttribute(XAttribute attribute) =>
        !attribute.IsNamespaceDeclaration
        && attribute.Name != _inherit
        && attribute.Name != _remove
        && attribute.Name != _removeAttributes;

    /// <summary>
    /// Copies one XML node while preserving ordered collection contents.
    /// </summary>
    /// <param name="node">The source node.</param>
    /// <returns>An independent node.</returns>
    private static XNode CloneNode(XNode node) =>
        node switch
        {
            XElement element => new XElement(element),
            XComment comment => new XComment(comment.Value),
            XCData data => new XCData(data.Value),
            XText text => new XText(text.Value),
            XProcessingInstruction instruction => new XProcessingInstruction(instruction),
            _ => throw new InvalidDataException("Unsupported node in a setting override."),
        };
}
