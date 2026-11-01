using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

/// <summary>
/// Identifies one authored XML block within a logical inspector page.
/// </summary>
public sealed class ContentEditorXmlScope
{
    public string Caption { get; }
    public string Text => Element.ToString();
    internal ContentPackEditorRecord Record { get; }
    internal XElement Element { get; }

    /// <summary>
    /// Binds a displayed block to its owning record without copying unrelated document contents.
    /// </summary>
    /// <param name="record">The owning definition.</param>
    /// <param name="element">The editable block.</param>
    /// <param name="caption">The block selector label.</param>
    internal ContentEditorXmlScope(ContentPackEditorRecord record, XElement element, string caption)
    {
        Record = record;
        Element = element;
        Caption = caption;
    }

    /// <summary>
    /// Finds the equivalent element in a staged copy using sibling indices, including repeated entries.
    /// </summary>
    /// <param name="source">The element in the current document.</param>
    /// <param name="copy">The staged document.</param>
    /// <returns>The equivalent staged element.</returns>
    internal static XElement FindInCopy(XElement source, XDocument copy)
    {
        int[] path = source
            .AncestorsAndSelf()
            .TakeWhile(element => element.Parent != null)
            .Reverse()
            .Select(element =>
                element.Parent.Elements().TakeWhile(sibling => sibling != element).Count()
            )
            .ToArray();
        XElement result = copy.Root;
        foreach (int index in path)
            result = result.Elements().ElementAt(index);
        return result;
    }

    /// <summary>
    /// Builds object-local blocks, or the smallest complete blocks belonging to a combined page.
    /// </summary>
    /// <param name="record">The selected object.</param>
    /// <param name="fields">The optional logical page's fields.</param>
    /// <returns>The independently editable blocks.</returns>
    internal static IReadOnlyList<ContentEditorXmlScope> Create(
        ContentPackEditorRecord record,
        IReadOnlyList<ContentPackEditorField> fields
    )
    {
        if (record == null)
            return Array.Empty<ContentEditorXmlScope>();
        if (fields == null)
            return new[] { new ContentEditorXmlScope(record, record.Element, record.Name) };
        List<ContentEditorXmlScope> result = new List<ContentEditorXmlScope>();
        foreach (
            IGrouping<ContentPackEditorRecord, ContentPackEditorField> group in fields.GroupBy(
                field => field.Record
            )
        )
        {
            ContentPackEditorRecord owner = group.Key;
            ContentPackEditorField[] all = owner.GetFields().ToArray();
            HashSet<XElement> selected = new HashSet<XElement>(
                group.Select(field => field.XmlElement)
            );
            Dictionary<XElement, int> total = new Dictionary<XElement, int>();
            Dictionary<XElement, int> included = new Dictionary<XElement, int>();
            foreach (ContentPackEditorField field in all)
            foreach (XElement ancestor in field.XmlElement.AncestorsAndSelf())
            {
                total.TryGetValue(ancestor, out int count);
                total[ancestor] = count + 1;
                if (selected.Contains(field.XmlElement))
                {
                    included.TryGetValue(ancestor, out int chosen);
                    included[ancestor] = chosen + 1;
                }
                if (ancestor == owner.Element)
                    break;
            }
            HashSet<XElement> blocks = new HashSet<XElement>();
            foreach (
                XElement element in selected.Where(element =>
                    element.Document == owner.Element.Document
                )
            )
            {
                XElement block = element;
                while (
                    block.Parent != null
                    && block != owner.Element
                    && total.TryGetValue(block.Parent, out int count)
                    && included.TryGetValue(block.Parent, out int chosen)
                    && count == chosen
                )
                    block = block.Parent;
                blocks.Add(block);
            }
            foreach (XElement block in blocks)
                result.Add(
                    new ContentEditorXmlScope(
                        owner,
                        block,
                        (
                            owner.Catalog.Faction == "Shared"
                                ? string.Empty
                                : owner.Catalog.Faction + " / "
                        )
                            + (
                                block == owner.Element
                                    ? owner.Kind == "Faction"
                                        ? owner.Element.Element("FactionDataPath") != null
                                            ? "Content files"
                                            : "Faction settings"
                                        : owner.Kind == "GameConfig"
                                            ? "Game rules"
                                            : owner.Name
                                    : string.Join(
                                        " / ",
                                        block
                                            .AncestorsAndSelf()
                                            .TakeWhile(element => element != owner.Element)
                                            .Reverse()
                                            .Select(element =>
                                                ContentPackEditorField.FormatName(
                                                    element.Name == "Entry"
                                                        ? element.Element("Key")?.Value ?? "Entry"
                                                        : element.Name.LocalName
                                                )
                                            )
                                    )
                            )
                    )
                );
        }
        return result;
    }
}
