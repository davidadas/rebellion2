using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace Rebellion.Util.Serialization
{
    /// <summary>
    /// Describes the types and members recognized by the XML serializer without exposing its caches.
    /// </summary>
    public static class SerializationMetadata
    {
        /// <summary>
        /// Finds registered types using their serialized element name.
        /// </summary>
        /// <param name="elementName">The serialized object name.</param>
        /// <returns>An independent list of matching types.</returns>
        public static IReadOnlyList<Type> GetTypes(string elementName) =>
            ReflectionHelper.GetPersistableTypes(elementName).ToArray();

        /// <summary>
        /// Gets the serialized element name of one registered type.
        /// </summary>
        /// <param name="type">The serialized object type.</param>
        /// <returns>The declared XML element name.</returns>
        public static string GetElementName(Type type) =>
            ReflectionHelper.GetPersistableElementName(type);

        /// <summary>
        /// Gets the value type read from one named child element.
        /// </summary>
        /// <param name="type">The containing type.</param>
        /// <param name="elementName">The serialized child name.</param>
        /// <returns>The declared value type, or null for an unknown member.</returns>
        public static Type GetElementType(Type type, string elementName) =>
            ReflectionHelper
                .GetElementMemberLookup(type)
                .TryGetValue(elementName, out MemberInfo member)
                ? ReflectionHelper.GetMemberType(member)
                : null;

        /// <summary>
        /// Gets the value type read from one named attribute.
        /// </summary>
        /// <param name="type">The containing type.</param>
        /// <param name="attributeName">The serialized attribute name.</param>
        /// <returns>The declared value type, or null for an unknown attribute.</returns>
        public static Type GetAttributeType(Type type, string attributeName) =>
            ReflectionHelper
                .GetPersistableAttributeMap(type, ReflectionHelper.OperationType.Read)
                .TryGetValue(attributeName, out MemberInfo member)
                ? ReflectionHelper.GetMemberType(member)
                : null;

        /// <summary>
        /// Gets an explicitly declared item name for a collection member.
        /// </summary>
        /// <param name="type">The containing type.</param>
        /// <param name="elementName">The collection element name.</param>
        /// <returns>The declared item name, or null when the serializer uses a type-derived name.</returns>
        public static string GetCollectionItemName(Type type, string elementName)
        {
            if (
                !ReflectionHelper
                    .GetElementMemberLookup(type)
                    .TryGetValue(elementName, out MemberInfo member)
            )
                return null;
            return member.GetCustomAttribute<PersistableCollectionItemAttribute>()?.Name
                ?? member.GetCustomAttribute<XmlArrayItemAttribute>()?.ElementName;
        }

        /// <summary>
        /// Resolves the element type of a supported array or generic collection.
        /// </summary>
        /// <param name="type">The possible collection type.</param>
        /// <returns>The item type, or null for scalar and dictionary types.</returns>
        public static Type GetCollectionItemType(Type type)
        {
            if (type == null || !TypeHelper.IsEnumerable(type) || TypeHelper.IsDictionary(type))
                return null;
            if (type.IsArray)
                return type.GetElementType();
            return type.GetGenericArguments().FirstOrDefault()
                ?? type.GetInterfaces()
                    .Where(candidate =>
                        candidate.IsGenericType
                        && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                    )
                    .Select(candidate => candidate.GetGenericArguments()[0])
                    .FirstOrDefault();
        }
    }
}
