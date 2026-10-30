using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

/// <summary>
/// Resolves editor field types from the same member metadata used by content deserialization.
/// </summary>
internal static class ContentPackEditorFieldSchema
{
    internal static readonly string[] MissionIDs =
    {
        MissionTypeIDs.Abduction,
        MissionTypeIDs.Assassination,
        MissionTypeIDs.Diplomacy,
        MissionTypeIDs.Espionage,
        MissionTypeIDs.InciteUprising,
        MissionTypeIDs.JediTraining,
        MissionTypeIDs.Reconnaissance,
        MissionTypeIDs.Recruitment,
        MissionTypeIDs.Rescue,
        MissionTypeIDs.Research,
        MissionTypeIDs.Sabotage,
        MissionTypeIDs.SubdueUprising,
    };

    /// <summary>
    /// Resolves the concrete data type represented by an authored record.
    /// </summary>
    /// <param name="record">The record root.</param>
    /// <returns>The known type, or null for an unregistered extension.</returns>
    internal static Type GetRecordType(XElement record)
    {
        Type definition = record.Name.LocalName switch
        {
            "ContentPack" => typeof(ContentPackDefinition),
            "Faction" when record.Element("FactionDataPath") != null =>
                typeof(ContentFactionDefinition),
            "Scenario" => typeof(ContentScenarioDefinition),
            "SpecialForce" => typeof(SpecialForces),
            _ => null,
        };
        if (definition != null)
            return definition;
        IReadOnlyList<Type> types = ReflectionHelper.GetPersistableTypes(record.Name.LocalName);
        return types.Count == 1 ? types[0] : null;
    }

    /// <summary>
    /// Resolves one element's serialized value type through its owning members and collections.
    /// </summary>
    /// <param name="record">The record root.</param>
    /// <param name="element">The element to inspect.</param>
    /// <param name="attribute">The optional XML attribute being edited.</param>
    /// <returns>The known member type, or null for an unregistered extension.</returns>
    internal static Type GetValueType(
        XElement record,
        XElement element,
        XAttribute attribute = null
    )
    {
        Type current = GetRecordType(record);
        if (current == null)
            return null;
        foreach (
            XElement node in element.AncestorsAndSelf().TakeWhile(node => node != record).Reverse()
        )
        {
            current = GetChildType(current, node);
            if (current == null)
                return null;
        }
        if (attribute == null)
            return current;
        return ReflectionHelper
            .GetPersistableAttributeMap(current, ReflectionHelper.OperationType.Read)
            .TryGetValue(attribute.Name.LocalName, out MemberInfo member)
            ? ReflectionHelper.GetMemberType(member)
            : null;
    }

    /// <summary>
    /// Resolves the item type of an array or generic collection.
    /// </summary>
    /// <param name="type">The collection type.</param>
    /// <returns>The item type, or null for a scalar or dictionary.</returns>
    internal static Type GetItemType(Type type)
    {
        if (type == null || !TypeHelper.IsEnumerable(type) || TypeHelper.IsDictionary(type))
            return null;
        return type.IsArray ? type.GetElementType() : type.GetGenericArguments().FirstOrDefault();
    }

    /// <summary>
    /// Gets the XML item name declared for one scalar collection member.
    /// </summary>
    /// <param name="record">The record root.</param>
    /// <param name="element">The collection element.</param>
    /// <returns>The declared item name, or null when the collection type is unknown.</returns>
    internal static XName GetItemName(XElement record, XElement element)
    {
        XElement parent = element.Parent ?? record;
        Type parentType = parent == record ? GetRecordType(record) : GetValueType(record, parent);
        if (
            parentType != null
            && ReflectionHelper
                .GetElementMemberLookup(parentType)
                .TryGetValue(element.Name.LocalName, out MemberInfo member)
        )
        {
            string itemName =
                member.GetCustomAttribute<PersistableCollectionItemAttribute>()?.Name
                ?? member.GetCustomAttribute<XmlArrayItemAttribute>()?.ElementName;
            if (!string.IsNullOrWhiteSpace(itemName))
                return itemName;
        }
        Type itemType = GetItemType(GetValueType(record, element));
        return itemType == null ? null : ReflectionHelper.GetPersistableElementName(itemType);
    }

    /// <summary>
    /// Validates a scalar through the content serializer's conversions and enum members.
    /// </summary>
    /// <param name="value">The proposed value.</param>
    /// <param name="type">The declared scalar type.</param>
    internal static void ValidateScalar(string value, Type type)
    {
        if (type == null || !TypeHelper.IsScalar(type))
            return;
        try
        {
            object parsed = TypeHelper.ConvertToScalar(value, type);
            if (type.IsEnum && !Enum.IsDefined(type, parsed))
                throw new ArgumentException("Choose a listed value.");
        }
        catch (Exception exception)
            when (exception is FormatException or OverflowException or ArgumentException)
        {
            throw new ArgumentException(
                type.IsEnum ? "Choose a listed value." : "Enter a valid value.",
                nameof(value),
                exception
            );
        }
    }

    /// <summary>
    /// Resolves one member, dictionary component, collection item, or polymorphic wrapper.
    /// </summary>
    /// <param name="parentType">The containing value type.</param>
    /// <param name="element">The authored child.</param>
    /// <returns>The resolved child type, or null when unknown.</returns>
    private static Type GetChildType(Type parentType, XElement element)
    {
        parentType = Nullable.GetUnderlyingType(parentType) ?? parentType;
        if (TypeHelper.IsDictionary(parentType))
            return typeof(KeyValuePair<,>).MakeGenericType(parentType.GetGenericArguments());
        if (
            parentType.IsGenericType
            && parentType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
        )
            return element.Name.LocalName switch
            {
                "Key" => parentType.GetGenericArguments()[0],
                "Value" => parentType.GetGenericArguments()[1],
                _ => null,
            };
        Type itemType = GetItemType(parentType);
        if (itemType != null)
            return element.Name.LocalName.StartsWith("ArrayOf", StringComparison.Ordinal)
                ? parentType
                : itemType;
        if (
            ReflectionHelper
                .GetElementMemberLookup(parentType)
                .TryGetValue(element.Name.LocalName, out MemberInfo member)
        )
            return ReflectionHelper.GetMemberType(member);
        if (element.Name.LocalName == parentType.Name)
            return parentType;
        return ReflectionHelper
            .GetPersistableTypes(element.Name.LocalName)
            .SingleOrDefault(type => parentType.IsAssignableFrom(type));
    }
}
