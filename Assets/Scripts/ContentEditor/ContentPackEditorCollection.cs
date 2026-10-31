using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Util.Serialization;

/// <summary>
/// Describes a typed object collection, including an absent collection awaiting its first edit.
/// </summary>
public sealed class ContentPackEditorCollection
{
    private readonly Action attach;
    internal ContentPackEditorRecord Record { get; }
    internal XElement Element { get; }
    internal XElement Owner { get; }
    internal Type ItemType { get; }
    public string Name => Element.Name.LocalName;
    public string Key { get; }
    internal IReadOnlyList<XElement> Entries => Element.Elements().ToArray();

    /// <summary>
    /// Resolves a collection path without adding missing containers to the source document.
    /// </summary>
    /// <param name="record">The owning definition.</param>
    /// <param name="owner">The attached object containing the path.</param>
    /// <param name="path">The serialized member path.</param>
    internal ContentPackEditorCollection(
        ContentPackEditorRecord record,
        XElement owner,
        params string[] path
    )
    {
        Record = record;
        Owner = owner;
        XElement current = owner;
        Type type =
            owner == record.Element
                ? ContentPackEditorFieldSchema.GetRecordType(owner)
                : ContentPackEditorFieldSchema.GetValueType(record.Element, owner);
        XElement firstMissing = null;
        XElement attachTo = null;
        foreach (string member in path)
        {
            type =
                SerializationMetadata.GetElementType(type, member)
                ?? throw new ArgumentException("Unknown collection path.", nameof(path));
            XElement child = current.Element(member);
            if (child == null)
            {
                child = new XElement(member);
                if (firstMissing == null)
                {
                    firstMissing = child;
                    attachTo = current;
                }
                else
                    current.Add(child);
            }
            current = child;
        }
        Element = current;
        ItemType =
            ContentPackEditorFieldSchema.GetItemType(type)
            ?? throw new ArgumentException("Choose a collection.", nameof(path));
        if (TypeHelper.IsScalar(ItemType))
            throw new ArgumentException("Choose an object collection.", nameof(path));
        attach = () =>
        {
            if (firstMissing != null && firstMissing.Parent == null)
                ContentEditorGenerationFactory.AttachMember(
                    attachTo,
                    firstMissing,
                    attachTo == record.Element
                        ? ContentPackEditorFieldSchema.GetRecordType(attachTo)
                        : ContentPackEditorFieldSchema.GetValueType(record.Element, attachTo)
                );
        };
        Key = string.Join(
            "/",
            owner
                .AncestorsAndSelf()
                .TakeWhile(element => element != record.Element)
                .Reverse()
                .Select(element =>
                    element.Name.LocalName
                    + "["
                    + element.ElementsBeforeSelf(element.Name).Count()
                    + "]"
                )
                .Concat(path)
        );
    }

    /// <summary>
    /// Attaches pending containers only inside an explicit project edit.
    /// </summary>
    internal void Attach() => attach();
}
