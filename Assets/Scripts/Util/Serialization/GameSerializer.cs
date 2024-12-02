using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

/// <summary>
/// Handles serialization and deserialization of game objects to and from XML.
/// </summary>
public class GameSerializer
{
    private readonly Type type;
    private readonly GameSerializerSettings settings;

    /// <summary>
    /// Initializes a new instance of the GameSerializer class.
    /// </summary>
    /// <param name="type">The type of object to be serialized or deserialized.</param>
    /// <param name="settings">Optional settings for the serializer.</param>
    public GameSerializer(Type type, GameSerializerSettings settings = null)
    {
        this.type = type;
        this.settings = settings ?? new GameSerializerSettings();
    }

    /// <summary>
    /// Serializes an object to XML and writes it to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to write the serialized object to.</param>
    /// <param name="obj">The object to serialize.</param>
    public void Serialize(Stream stream, object obj)
    {
        using (XmlWriter writer = XmlWriter.Create(stream, settings.CreateWriterSettings()))
        {
            SerializeToXmlWriter(writer, obj);
        }
    }

    /// <summary>
    /// Serializes an object to XML and writes it using the specified TextWriter.
    /// </summary>
    /// <param name="textWriter">The TextWriter to write the serialized object to.</param>
    /// <param name="obj">The object to serialize.</param>
    public void Serialize(TextWriter textWriter, object obj)
    {
        using (XmlWriter writer = XmlWriter.Create(textWriter, settings.CreateWriterSettings()))
        {
            SerializeToXmlWriter(writer, obj);
        }
    }

    /// <summary>
    /// Deserializes an object from XML read from the specified stream.
    /// </summary>
    /// <param name="stream">The stream to read the serialized object from.</param>
    /// <returns>The deserialized object.</returns>
    public object Deserialize(Stream stream)
    {
        using (XmlReader reader = XmlReader.Create(stream, settings.CreateReaderSettings()))
        {
            return DeserializeFromXmlReader(reader);
        }
    }

    /// <summary>
    /// Deserializes an object from XML read using the specified TextReader.
    /// </summary>
    /// <param name="textReader">The TextReader to read the serialized object from.</param>
    /// <returns>The deserialized object.</returns>
    public object Deserialize(TextReader textReader)
    {
        using (XmlReader reader = XmlReader.Create(textReader, settings.CreateReaderSettings()))
        {
            return DeserializeFromXmlReader(reader);
        }
    }

    /// <summary>
    /// Helper method to serialize an object to XML using an XmlWriter.
    /// </summary>
    /// <param name="writer">The XmlWriter to use for serialization.</param>
    /// <param name="obj">The object to serialize.</param>
    private void SerializeToXmlWriter(XmlWriter writer, object obj)
    {
        writer.WriteStartDocument();
        XmlSerializer.WriteValue(obj, writer, settings.RootName);
        writer.WriteEndDocument();
    }

    /// <summary>
    /// Helper method to deserialize an object from XML using an XmlReader.
    /// </summary>
    /// <param name="reader">The XmlReader to use for deserialization.</param>
    /// <returns>The deserialized object.</returns>
    private object DeserializeFromXmlReader(XmlReader reader)
    {
        reader.MoveToContent();
        return XmlDeserializer.ReadValue(type, reader);
    }
}

/// <summary>
/// Provides methods for writing objects to XML.
/// </summary>
static class XmlSerializer
{
    /// <summary>
    /// Writes an object to XML.
    /// </summary>
    /// <param name="obj">The object to write.</param>
    /// <param name="writer">The XmlWriter to use.</param>
    /// <param name="name">The optional name for the XML element.</param>
    public static void WriteValue(object obj, XmlWriter writer, string name = null)
    {
        if (obj == null)
        {
            return;
        }

        Type objType = obj.GetType();

        if (TypeHelper.IsPrimitive(objType))
        {
            WritePrimitive(obj, writer, name);
        }
        else if (objType.IsEnum)
        {
            WriteEnum(obj, writer, name);
        }
        else if (obj is IDictionary dictionary)
        {
            WriteDictionary(dictionary, writer, name);
        }
        else if (obj is IEnumerable enumerable)
        {
            WriteCollection(enumerable, writer, name);
        }
        else if (TypeHelper.HasAttribute<PersistableObjectAttribute>(objType))
        {
            WritePersistable(obj, writer, name);
        }
        else
        {
            throw new ArgumentException($"Object type {objType} not supported.");
        }
    }

    /// <summary>
    /// Writes a primitive value to XML.
    /// </summary>
    /// <param name="obj">The primitive object to write.</param>
    /// <param name="writer">The XmlWriter to use.</param>
    /// <param name="key">The optional key for the XML element.</param>
    private static void WritePrimitive(object obj, XmlWriter writer, string key = null)
    {
        if (key != null)
        {
            writer.WriteElementString(key, obj.ToString());
        }
        else
        {
            writer.WriteValue(obj);
        }
    }

    /// <summary>
    /// Writes an enum value to XML.
    /// </summary>
    /// <param name="obj">The enum object to write.</param>
    /// <param name="writer">The XmlWriter to use.</param>
    /// <param name="key">The optional key for the XML element.</param>
    private static void WriteEnum(object obj, XmlWriter writer, string key = null)
    {
        if (key != null)
        {
            writer.WriteElementString(key, obj.ToString());
        }
        else
        {
            writer.WriteString(obj.ToString());
        }
    }

    /// <summary>
    /// Writes a dictionary to XML.
    /// </summary>
    /// <param name="dictionary">The dictionary to write.</param>
    /// <param name="writer">The XmlWriter to use.</param>
    /// <param name="name">The optional name for the XML element.</param>
    private static void WriteDictionary(
        IDictionary dictionary,
        XmlWriter writer,
        string name = null
    )
    {
        Type elementType = dictionary.GetType().GetGenericArguments().FirstOrDefault();
        string elementName = name ?? $"DictionaryOf{elementType.Name}";

        writer.WriteStartElement(elementName);
        foreach (object key in dictionary.Keys)
        {
            writer.WriteStartElement("Entry");
            writer.WriteStartElement("Key");
            WriteValue(key, writer);
            writer.WriteEndElement();
            writer.WriteStartElement("Value");
            WriteValue(dictionary[key], writer);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    /// <summary>
    /// Writes a collection to XML.
    /// </summary>
    /// <param name="collection">The collection to write.</param>
    /// <param name="writer">The XmlWriter to use.</param>
    /// <param name="name">The optional name for the XML element.</param>
    private static void WriteCollection(
        IEnumerable collection,
        XmlWriter writer,
        string name = null
    )
    {
        Type elementType =
            collection.GetType().GetGenericArguments().FirstOrDefault()
            ?? collection.GetType().GetElementType();
        string elementName = name ?? $"ArrayOf{elementType.Name}";

        writer.WriteStartElement(elementName);
        foreach (object item in collection)
        {
            WriteValue(item, writer, item.GetType().Name);
        }
        writer.WriteEndElement();
    }

    /// <summary>
    /// Writes a persistable object to XML.
    /// </summary>
    /// <param name="obj">The persistable object to write.</param>
    /// <param name="writer">The XmlWriter to use.</param>
    /// <param name="key">The optional key for the XML element.</param>
    private static void WritePersistable(object obj, XmlWriter writer, string key = null)
    {
        Type objType = obj.GetType();
        IEnumerable<MemberInfo> members = ReflectionHelper.GetPersistableMembers(
            objType,
            ReflectionHelper.OperationType.Write
        );
        IEnumerable<MemberInfo> attributes = ReflectionHelper.GetPersistableAttributes(
            objType,
            ReflectionHelper.OperationType.Write
        );
        writer.WriteStartElement(key ?? objType.Name);

        foreach (MemberInfo attribute in attributes)
        {
            PersistableAttributeAttribute persistableAttr = (PersistableAttributeAttribute)
                Attribute.GetCustomAttribute(attribute, typeof(PersistableAttributeAttribute));
            object attributeValue = ReflectionHelper.GetMemberValue(attribute, obj);

            if (attributeValue != null)
            {
                string stringValue = TypeHelper.ConvertToString(attributeValue);
                string attributeName = !string.IsNullOrEmpty(persistableAttr.Name)
                    ? persistableAttr.Name
                    : attribute.Name;
                writer.WriteAttributeString(attributeName, stringValue);
            }
        }

        foreach (MemberInfo member in members)
        {
            object value = ReflectionHelper.GetMemberValue(member, obj);

            if (value != null)
            {
                string elementName = GetElementName(member, value);
                WriteValue(value, writer, elementName);
            }
        }
        writer.WriteEndElement();
    }

    /// <summary>
    /// Determines the element name for a member based on its attributes and value type.
    /// </summary>
    /// <param name="member">The MemberInfo of the member.</param>
    /// <param name="value">The value of the member.</param>
    /// <returns>The determined element name.</returns>
    private static string GetElementName(MemberInfo member, object value)
    {
        Type valueType = value.GetType();
        PersistableIncludeAttribute[] includeAttributes = (PersistableIncludeAttribute[])
            Attribute.GetCustomAttributes(member, typeof(PersistableIncludeAttribute));

        foreach (PersistableIncludeAttribute includeAttr in includeAttributes)
        {
            if (includeAttr.PersistableType.IsAssignableFrom(valueType))
            {
                return includeAttr.PersistableType.Name;
            }
        }

        PersistableMemberAttribute memberAttr = (PersistableMemberAttribute)
            Attribute.GetCustomAttribute(member, typeof(PersistableMemberAttribute));

        if (memberAttr != null)
        {
            if (!string.IsNullOrEmpty(memberAttr.Name))
            {
                return memberAttr.Name;
            }
            if (memberAttr.UseTypeName)
            {
                return valueType.Name;
            }
        }

        return member.Name;
    }
}

/// <summary>
/// Provides methods for reading objects from XML.
/// </summary>
static class XmlDeserializer
{
    /// <summary>
    /// Reads an object from XML.
    /// </summary>
    /// <param name="objType">The type of object to read.</param>
    /// <param name="reader">The XmlReader to use.</param>
    /// <param name="attributes">Optional attributes for the object.</param>
    /// <returns>The read object.</returns>
    public static object ReadValue(
        Type objType,
        XmlReader reader,
        PersistableAttributes attributes = default
    )
    {
        if (TypeHelper.IsPrimitive(objType))
        {
            return ReadPrimitive(objType, reader);
        }
        if (objType.IsEnum)
        {
            return ReadEnum(objType, reader);
        }
        if (reader.NodeType == XmlNodeType.Element)
        {
            if (TypeHelper.IsDictionary(objType))
            {
                return ReadDictionary(objType, reader);
            }
            else if (TypeHelper.IsEnumerable(objType))
            {
                return ReadCollection(objType, reader);
            }
            else if (TypeHelper.HasAttribute<PersistableObjectAttribute>(objType))
            {
                return ReadPersistable(objType, reader, attributes);
            }
            else
            {
                throw new ArgumentException($"Object type {objType.Name} not supported.");
            }
        }
        throw new ArgumentException(
            $"Invalid XML format. Expected Element or Text, got {reader.NodeType}."
        );
    }

    /// <summary>
    /// Reads a primitive value from XML.
    /// </summary>
    /// <param name="type">The type of primitive to read.</param>
    /// <param name="reader">The XmlReader to use.</param>
    /// <returns>The read primitive value.</returns>
    private static object ReadPrimitive(Type type, XmlReader reader)
    {
        string content;
        if (reader.NodeType == XmlNodeType.Element)
        {
            content = reader.ReadElementContentAsString();
        }
        else if (reader.NodeType == XmlNodeType.Attribute)
        {
            content = reader.Value;
        }
        else
        {
            content = reader.ReadContentAsString();
        }
        return TypeHelper.ConvertToPrimitive(content, type);
    }

    /// <summary>
    /// Reads an enum value from XML.
    /// </summary>
    /// <param name="type">The type of enum to read.</param>
    /// <param name="reader">The XmlReader to use.</param>
    /// <returns>The read enum value.</returns>
    private static object ReadEnum(Type type, XmlReader reader)
    {
        string content =
            reader.NodeType == XmlNodeType.Element
                ? reader.ReadElementContentAsString()
                : reader.ReadContentAsString();
        return Enum.Parse(type, content);
    }

    /// <summary>
    /// Reads a dictionary from XML.
    /// </summary>
    /// <param name="type">The type of dictionary to read.</param>
    /// <param name="reader">The XmlReader to use.</param>
    /// <returns>The read dictionary.</returns>
    private static object ReadDictionary(Type type, XmlReader reader)
    {
        Type[] keyValueType = type.GetGenericArguments();
        Type keyType = keyValueType[0];
        Type valueType = keyValueType[1];

        IDictionary dictionary = (IDictionary)Activator.CreateInstance(type);
        int startDepth = reader.Depth;

        reader.ReadStartElement();
        while (reader.Depth > startDepth)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "Entry")
            {
                reader.ReadStartElement("Entry");
                reader.ReadStartElement("Key");
                object objKey = ReadValue(keyType, reader);
                reader.ReadEndElement();
                reader.ReadStartElement("Value");
                object objValue = ReadValue(valueType, reader);
                reader.ReadEndElement();

                dictionary.Add(objKey, objValue);
            }
            else if (!reader.Read())
            {
                break;
            }
        }
        reader.ReadEndElement();
        return dictionary;
    }

    /// <summary>
    /// Reads a collection from XML.
    /// </summary>
    /// <param name="type">The type of collection to read.</param>
    /// <param name="reader">The XmlReader to use.</param>
    /// <returns>The read collection.</returns>
    private static object ReadCollection(Type type, XmlReader reader)
    {
        Type elementType = type.IsArray
            ? type.GetElementType()
            : type.GetGenericArguments().FirstOrDefault();

        if (type.IsArray)
        {
            List<object> tempList = new List<object>();
            ReadCollectionElements(reader, elementType, tempList);
            Array array = Array.CreateInstance(elementType, tempList.Count);
            for (int i = 0; i < tempList.Count; i++)
            {
                array.SetValue(tempList[i], i);
            }
            return array;
        }
        else
        {
            IList collection = (IList)Activator.CreateInstance(type);
            ReadCollectionElements(reader, elementType, collection);
            return collection;
        }
    }

    /// <summary>
    /// Reads collection elements from XML and adds them to the provided list.
    /// </summary>
    /// <param name="reader">The XmlReader to use.</param>
    /// <param name="elementType">The type of elements in the collection.</param>
    /// <param name="collection">The collection to add elements to.</param>
    private static void ReadCollectionElements(XmlReader reader, Type elementType, IList collection)
    {
        int startDepth = reader.Depth;
        IDictionary<string, Type> persistableMap = ReflectionHelper.GetPersistableObjectMap();

        reader.ReadStartElement();
        while (reader.Depth > startDepth)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                string elementName = reader.Name;
                Type actualElementType = persistableMap.ContainsKey(elementName)
                    ? persistableMap[elementName]
                    : elementType;
                object value = ReadValue(actualElementType, reader);
                collection.Add(value);
            }
            else if (!reader.Read())
            {
                break;
            }
        }
        reader.ReadEndElement();
    }

    /// <summary>
    /// Reads a persistable object from XML.
    /// </summary>
    /// <param name="objType">The type of persistable object to read.</param>
    /// <param name="reader">The XmlReader to use.</param>
    /// <param name="objAttributes">Optional attributes for the object.</param>
    /// <returns>The read persistable object.</returns>
    private static object ReadPersistable(
        Type objType,
        XmlReader reader,
        PersistableAttributes objAttributes = default
    )
    {
        string actualTypeName = reader.Name;
        Type actualType = objType;

        if (objType.IsInterface || objType.IsAbstract)
        {
            actualType = ResolveActualType(objType, actualTypeName, objAttributes);
        }

        object obj = Activator.CreateInstance(actualType);

        IDictionary<string, MemberInfo> attributes = ReflectionHelper.GetPersistableAttributeMap(
            actualType,
            ReflectionHelper.OperationType.Read
        );

        IEnumerable<MemberInfo> members = ReflectionHelper.GetPersistableMembers(
            actualType,
            ReflectionHelper.OperationType.Read
        );

        if (reader.HasAttributes)
        {
            ReadAttributes(reader, attributes, obj);
        }

        if (reader.IsEmptyElement)
        {
            reader.Read();
            return obj;
        }

        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                string elementName = reader.Name;
                MemberInfo member = FindMemberForElement(members, reader.Name);

                if (member != null)
                {
                    object value = ReadMember(member, reader);
                    ReflectionHelper.SetMemberValue(member, obj, value);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unknown element '{elementName}' encountered while deserializing {objType.Name}."
                    );
                }
            }
            else
            {
                reader.Read();
            }
        }
        reader.ReadEndElement();
        return obj;
    }

    /// <summary>
    /// Resolves the actual type for abstract or interface types.
    /// </summary>
    /// <param name="objType">The original object type.</param>
    /// <param name="actualTypeName">The name of the actual type from XML.</param>
    /// <param name="objAttributes">The persistable attributes.</param>
    /// <returns>The resolved actual type.</returns>
    private static Type ResolveActualType(
        Type objType,
        string actualTypeName,
        PersistableAttributes objAttributes
    )
    {
        if (objAttributes.IncludeAttributes != null)
        {
            foreach (PersistableIncludeAttribute includeAttr in objAttributes.IncludeAttributes)
            {
                if (includeAttr.PersistableType.Name == actualTypeName)
                {
                    return includeAttr.PersistableType;
                }
            }
        }

        Type resolvedType = ReflectionHelper.GetTypeByName(actualTypeName) ?? objType;

        if (!objType.IsAssignableFrom(resolvedType))
        {
            throw new InvalidOperationException(
                $"Could not find a valid type to instantiate for {objType.Name}. XML element: {actualTypeName}"
            );
        }

        return resolvedType;
    }

    /// <summary>
    /// Reads attributes from XML and sets them on the object.
    /// </summary>
    /// <param name="reader">The XmlReader to use.</param>
    /// <param name="attributes">The dictionary of persistable attributes.</param>
    /// <param name="obj">The object to set attributes on.</param>
    private static void ReadAttributes(
        XmlReader reader,
        IDictionary<string, MemberInfo> attributes,
        object obj
    )
    {
        for (int i = 0; i < reader.AttributeCount; i++)
        {
            reader.MoveToAttribute(i);
            if (attributes.TryGetValue(reader.Name, out MemberInfo attribute))
            {
                Type attributeType = ReflectionHelper.GetMemberType(attribute);
                object value = TypeHelper.ConvertToPrimitive(reader.Value, attributeType);
                ReflectionHelper.SetMemberValue(attribute, obj, value);
            }
        }
        reader.MoveToElement();
    }

    /// <summary>
    /// Finds the member that corresponds to the given XML element.
    /// </summary>
    /// <param name="members">The collection of members to search.</param>
    /// <param name="elementName">The name of the XML element.</param>
    /// <returns>The MemberInfo that corresponds to the XML element, or null if not found.</returns>
    private static MemberInfo FindMemberForElement(
        IEnumerable<MemberInfo> members,
        string elementName
    )
    {
        foreach (MemberInfo member in members)
        {
            PersistableIncludeAttribute[] typeAttributes = (PersistableIncludeAttribute[])
                Attribute.GetCustomAttributes(member, typeof(PersistableIncludeAttribute));

            foreach (PersistableIncludeAttribute typeAttr in typeAttributes)
            {
                if (typeAttr.PersistableType.Name == elementName)
                {
                    return member;
                }
            }

            PersistableMemberAttribute memberAttr = (PersistableMemberAttribute)
                Attribute.GetCustomAttribute(member, typeof(PersistableMemberAttribute));
            if (memberAttr != null)
            {
                if (!string.IsNullOrEmpty(memberAttr.Name) && memberAttr.Name == elementName)
                {
                    return member;
                }
                if (memberAttr.UseTypeName)
                {
                    Type memberType = ReflectionHelper.GetMemberType(member);
                    if (memberType.Name == elementName)
                    {
                        return member;
                    }
                }
            }

            if (member.Name == elementName)
            {
                return member;
            }
        }
        return null;
    }

    /// <summary>
    /// Reads a member of a persistable object from XML.
    /// </summary>
    /// <param name="info">The MemberInfo of the member to read.</param>
    /// <param name="reader">The XmlReader to use.</param>
    /// <returns>The read member value.</returns>
    private static object ReadMember(MemberInfo info, XmlReader reader)
    {
        Type type = ReflectionHelper.GetMemberType(info);

        if (reader.IsEmptyElement)
        {
            reader.Read();
            return null;
        }

        PersistableAttributes attributes = new PersistableAttributes
        {
            IncludeAttributes = (PersistableIncludeAttribute[])
                Attribute.GetCustomAttributes(info, typeof(PersistableIncludeAttribute)),
            MemberAttribute = (PersistableMemberAttribute)
                Attribute.GetCustomAttribute(info, typeof(PersistableMemberAttribute)),
            AttributeAttributes = (PersistableAttributeAttribute[])
                Attribute.GetCustomAttributes(info, typeof(PersistableAttributeAttribute)),
        };

        return ReadValue(type, reader, attributes);
    }
}

/// <summary>
/// Provides helper methods for reflection-related operations.
/// </summary>
static class ReflectionHelper
{
    private const BindingFlags CommonBindingFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public enum OperationType
    {
        Write,
        Read,
    };

    /// <summary>
    /// Gets all persistable members of a given type.
    /// </summary>
    /// <param name="classType">The type to get members from.</param>
    /// <param name="operationType">The type of operation to get members for.</param>
    /// <returns>An enumerable of persistable MemberInfo objects.</returns>
    public static IEnumerable<MemberInfo> GetPersistableMembers(
        Type classType,
        OperationType operationType
    )
    {
        bool IsPersistable(MemberInfo member)
        {
            bool isPublicField = member is FieldInfo field && field.IsPublic;
            bool isPublicProperty =
                member is PropertyInfo property && property.GetMethod?.IsPublic == true;
            bool hasPersistableAttribute = member
                .GetCustomAttributes(typeof(PersistableMemberAttribute), true)
                .Any();
            bool hasIgnoreAttribute = member
                .GetCustomAttributes(typeof(PersistableIgnoreAttribute), true)
                .Any();
            bool hasAlternativePersistableAttribute = member
                .GetCustomAttributes(typeof(PersistableAttributeAttribute), true)
                .Any();

            if (operationType == OperationType.Write)
            {
                return (isPublicField || isPublicProperty || hasPersistableAttribute)
                    && !hasIgnoreAttribute
                    && !hasAlternativePersistableAttribute;
            }

            return (isPublicField || isPublicProperty || hasPersistableAttribute)
                && !hasAlternativePersistableAttribute;
        }

        IEnumerable<MemberInfo> fields = classType
            .GetFields(CommonBindingFlags)
            .Where(IsPersistable)
            .Cast<MemberInfo>();

        IEnumerable<MemberInfo> properties = classType
            .GetProperties(CommonBindingFlags)
            .Where(property => property.CanRead && property.CanWrite)
            .Where(IsPersistable)
            .Cast<MemberInfo>();

        return fields.Concat(properties);
    }

    /// <summary>
    /// Gets all persistable attributes of a given type.
    /// </summary>
    /// <param name="classType">The type to get attributes from.</param>
    /// <param name="operationType">The type of operation to get attributes for.</param>
    /// <returns>An enumerable of MemberInfo objects representing persistable attributes.</returns>
    public static IEnumerable<MemberInfo> GetPersistableAttributes(
        Type classType,
        OperationType operationType
    )
    {
        bool IsPersistableAttribute(MemberInfo member)
        {
            bool hasPersistableAttribute = member
                .GetCustomAttributes(typeof(PersistableAttributeAttribute), true)
                .Any();
            bool hasIgnoreAttribute = member
                .GetCustomAttributes(typeof(PersistableIgnoreAttribute), true)
                .Any();

            if (operationType == OperationType.Write)
            {
                return hasPersistableAttribute && !hasIgnoreAttribute;
            }

            return hasPersistableAttribute;
        }

        IEnumerable<MemberInfo> fields = classType
            .GetFields(CommonBindingFlags)
            .Where(IsPersistableAttribute)
            .Cast<MemberInfo>();

        IEnumerable<MemberInfo> properties = classType
            .GetProperties(CommonBindingFlags)
            .Where(property => property.CanRead && property.CanWrite)
            .Where(IsPersistableAttribute)
            .Cast<MemberInfo>();

        return fields.Concat(properties);
    }

    /// <summary>
    /// Gets a dictionary mapping field names to their MemberInfo for all persistable fields of a type.
    /// </summary>
    /// <param name="classType">The type to get the field map for.</param>
    /// <param name="operationType">The type of operation to get fields for.</param>
    /// <returns>A dictionary mapping field names to MemberInfo objects.</returns>
    public static IDictionary<string, MemberInfo> GetPersistableMemberMap(
        Type classType,
        OperationType operationType
    )
    {
        return GetPersistableMembers(classType, operationType)
            .ToDictionary(
                member =>
                    (
                        (PersistableMemberAttribute)
                            Attribute.GetCustomAttribute(member, typeof(PersistableMemberAttribute))
                    )?.Name ?? member.Name,
                member => member
            );
    }

    /// <summary>
    /// Gets a dictionary mapping attribute names to their MemberInfo for all persistable attributes of a type.
    /// </summary>
    /// <param name="classType">The type to get the attribute map for.</param>
    /// <param name="operationType">The type of operation to get attributes for.</param>
    /// <returns>A dictionary mapping attribute names to MemberInfo objects.</returns>
    public static IDictionary<string, MemberInfo> GetPersistableAttributeMap(
        Type classType,
        OperationType operationType
    )
    {
        Dictionary<string, MemberInfo> attributeMap = new Dictionary<string, MemberInfo>();
        Type currentType = classType;

        while (currentType != null)
        {
            foreach (MemberInfo member in GetPersistableAttributes(currentType, operationType))
            {
                PersistableAttributeAttribute attr = (PersistableAttributeAttribute)
                    Attribute.GetCustomAttribute(member, typeof(PersistableAttributeAttribute));
                string key = attr?.Name ?? member.Name;

                if (!attributeMap.ContainsKey(key))
                {
                    attributeMap[key] = member;
                }
            }

            currentType = currentType.BaseType;
        }

        return attributeMap;
    }

    /// <summary>
    /// Gets a dictionary mapping type names to Type objects for all persistable types in the current AppDomain.
    /// </summary>
    /// <returns>A dictionary mapping type names to Type objects.</returns>
    public static IDictionary<string, Type> GetPersistableObjectMap()
    {
        return Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(type => Attribute.IsDefined(type, typeof(PersistableObjectAttribute)))
            .ToDictionary(
                type =>
                    (
                        (PersistableObjectAttribute)
                            Attribute.GetCustomAttribute(type, typeof(PersistableObjectAttribute))
                    )?.Name ?? type.Name,
                type => type
            );
    }

    /// <summary>
    /// Gets the value of a member from an object.
    /// </summary>
    /// <param name="member">The MemberInfo of the member to get the value from.</param>
    /// <param name="obj">The object to get the member value from.</param>
    /// <returns>The value of the member.</returns>
    public static object GetMemberValue(MemberInfo member, object obj)
    {
        switch (member)
        {
            case FieldInfo field:
                return field.GetValue(obj);
            case PropertyInfo property:
                return property.GetValue(obj);
            default:
                throw new ArgumentException($"Unsupported member type: {member.GetType()}");
        }
    }

    /// <summary>
    /// Sets the value of a member on an object.
    /// </summary>
    /// <param name="member">The MemberInfo of the member to set the value on.</param>
    /// <param name="obj">The object to set the member value on.</param>
    /// <param name="value">The value to set.</param>
    public static void SetMemberValue(MemberInfo member, object obj, object value)
    {
        if (value == null)
        {
            return;
        }

        switch (member)
        {
            case FieldInfo field:
                field.SetValue(obj, value);
                break;
            case PropertyInfo property:
                property.SetValue(obj, value);
                break;
            default:
                throw new ArgumentException($"Unsupported member type: {member.GetType()}");
        }
    }

    /// <summary>
    /// Gets the type of a member.
    /// </summary>
    /// <param name="info">The MemberInfo to get the type from.</param>
    /// <returns>The Type of the member.</returns>
    public static Type GetMemberType(MemberInfo info)
    {
        switch (info)
        {
            case FieldInfo field:
                return field.FieldType;
            case PropertyInfo property:
                return property.PropertyType;
            default:
                throw new ArgumentException($"Unsupported member type: {info.GetType()}");
        }
    }

    /// <summary>
    /// Returns a type given the type name.
    /// </summary>
    /// <param name="typeName">The name of the type to get.</param>
    /// <returns>The Type object for the given type name.</returns>
    public static Type GetTypeByName(string typeName)
    {
        Assembly currentAssembly = Assembly.GetExecutingAssembly();
        Type foundType = currentAssembly.GetType(typeName);

        if (foundType == null)
        {
            foundType = AppDomain
                .CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(typeName))
                .FirstOrDefault(t => t != null);
        }

        return foundType;
    }
}

/// <summary>
/// Specifies that a class is persistable and can be serialized to XML.
/// </summary>
struct PersistableAttributes
{
    public PersistableIncludeAttribute[] IncludeAttributes;
    public PersistableMemberAttribute MemberAttribute;
    public PersistableAttributeAttribute[] AttributeAttributes;
}
