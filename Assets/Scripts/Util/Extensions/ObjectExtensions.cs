using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rebellion.Util.Attributes;
using Rebellion.Util.Common;

namespace Rebellion.Util.Extensions
{
    public enum CloneMode
    {
        Normal,
        Full,
    }

    /// <summary>
    /// Provides extension methods for creating shallow and deep copies of objects,
    /// while respecting custom attributes such as CloneIgnore.
    /// </summary>
    public static class ObjectExtensions
    {
        /// <summary>
        /// Creates a deep copy of the given object, respecting CloneIgnore attributes.
        /// </summary>
        /// <typeparam name="T">The type of the object to be copied.</typeparam>
        /// <param name="source">The source object to be copied.</param>
        /// <param name="mode">The mode of cloning to be used.</param>
        /// <returns>A deep copy of the source object.</returns>
        public static T GetDeepCopy<T>(this T source, CloneMode mode = CloneMode.Normal)
            where T : class
        {
            if (source == null)
            {
                return null;
            }

            return (T)CopyValue(source, shallow: false, mode);
        }

        /// <summary>
        /// Creates a shallow copy of the given object, respecting CloneIgnore attributes.
        /// </summary>
        /// <typeparam name="T">The type of the object to be copied.</typeparam>
        /// <param name="source">The source object to be copied.</param>
        /// <param name="mode">The mode of cloning to be used.</param>
        /// <returns>A shallow copy of the source object.</returns>
        public static T GetShallowCopy<T>(this T source, CloneMode mode = CloneMode.Normal)
            where T : class
        {
            if (source == null)
            {
                return null;
            }

            return (T)CopyValue(source, shallow: true, mode);
        }

        /// <summary>
        /// Copies the value of an object, either shallowly or deeply.
        /// </summary>
        /// <param name="source">The source object to be copied.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <param name="mode">The mode of cloning to be used.</param>
        /// <returns>The copied object.</returns>
        private static object CopyValue(object source, bool shallow, CloneMode mode)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();

            if (TypeHelper.IsValueType(type))
            {
                if (TypeHelper.IsStruct(type))
                {
                    return CopyStruct(source, shallow, mode);
                }
                return source;
            }
            if (TypeHelper.IsDictionary(type))
            {
                return CopyDictionary((IDictionary)source, shallow, mode);
            }
            if (TypeHelper.IsEnumerable(type))
            {
                return CopyCollection((IEnumerable)source, shallow, mode);
            }
            if (TypeHelper.IsTuple(type))
            {
                return CopyTuple(source, type, shallow, mode);
            }
            if (TypeHelper.IsClass(type))
            {
                return CopyObject(source, shallow, mode);
            }

            throw new ArgumentException($"Type {type} not supported when copying objects.");
        }

        /// <summary>
        /// Creates a copy of a dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary to be copied.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <param name="mode">The mode of cloning to be used.</param>
        /// <returns>The copied dictionary.</returns>
        private static IDictionary CopyDictionary(
            IDictionary dictionary,
            bool shallow,
            CloneMode mode
        )
        {
            if (shallow)
            {
                return dictionary;
            }

            Type type = dictionary.GetType();
            IDictionary result = (IDictionary)Activator.CreateInstance(type);

            foreach (DictionaryEntry entry in dictionary)
            {
                object copiedKey = CopyValue(entry.Key, shallow, mode);
                object copiedValue = CopyValue(entry.Value, shallow, mode);
                result.Add(copiedKey, copiedValue);
            }

            return result;
        }

        /// <summary>
        /// Creates a copy of a collection.
        /// </summary>
        /// <param name="collection">The collection to be copied.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <param name="mode">The mode of cloning to be used.</param>
        /// <returns>The copied collection.</returns>
        private static object CopyCollection(IEnumerable collection, bool shallow, CloneMode mode)
        {
            if (shallow)
            {
                return collection;
            }

            Type type = collection.GetType();

            if (type.IsArray)
            {
                Array sourceArray = (Array)collection;
                Array destinationArray = Array.CreateInstance(
                    type.GetElementType(),
                    sourceArray.Length
                );

                for (int i = 0; i < sourceArray.Length; i++)
                {
                    destinationArray.SetValue(CopyValue(sourceArray.GetValue(i), shallow, mode), i);
                }

                return destinationArray;
            }

            Type elementType = type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
            Type listType = typeof(List<>).MakeGenericType(elementType);
            IList newList = (IList)Activator.CreateInstance(listType);

            foreach (object item in collection)
            {
                newList.Add(CopyValue(item, shallow, mode));
            }

            return newList;
        }

        /// <summary>
        /// Creates a copy of a struct.
        /// </summary>
        /// <param name="source">The struct to be copied.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <param name="mode">The mode of cloning to be used.</param>
        /// <returns>The copied struct.</returns>
        private static object CopyStruct(object source, bool shallow, CloneMode mode)
        {
            Type type = source.GetType();
            object result = Activator.CreateInstance(type);

            FieldInfo[] fields = type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            foreach (FieldInfo field in fields)
            {
                object fieldValue = field.GetValue(source);
                object copiedValue = shallow ? fieldValue : CopyValue(fieldValue, shallow, mode);
                field.SetValue(result, copiedValue);
            }

            return result;
        }

        /// <summary>
        /// Creates a copy of a tuple.
        /// </summary>
        /// <param name="source">The tuple to be copied.</param>
        /// <param name="tupleType">The type of the tuple.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <param name="mode">The mode of cloning to be used.</param>
        /// <returns>The copied tuple.</returns>
        private static object CopyTuple(object source, Type tupleType, bool shallow, CloneMode mode)
        {
            int tupleLength = tupleType.GetGenericArguments().Length;
            object[] values = new object[tupleLength];

            for (int i = 0; i < tupleLength; i++)
            {
                PropertyInfo itemProperty = tupleType.GetProperty($"Item{i + 1}");
                object itemValue = itemProperty.GetValue(source);
                values[i] = shallow ? itemValue : CopyValue(itemValue, shallow, mode);
            }

            return Activator.CreateInstance(tupleType, values);
        }

        /// <summary>
        /// Creates a copy of an object.
        /// </summary>
        /// <param name="source">The object to be copied.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <param name="mode">The mode of cloning to be used.</param>
        /// <returns>The copied object.</returns>
        private static object CopyObject(object source, bool shallow, CloneMode mode)
        {
            Type type = source.GetType();

            if (!type.GetConstructors().Any(c => c.GetParameters().Length == 0))
            {
                throw new InvalidOperationException(
                    $"Cannot copy object of type {type.FullName} as it does not have a parameterless constructor."
                );
            }

            object result = Activator.CreateInstance(type);

            foreach (FieldInfo field in GetAllFields(type))
            {
                if (
                    mode == CloneMode.Normal
                    && Attribute.IsDefined(field, typeof(CloneIgnoreAttribute))
                )
                {
                    SetDefaultFieldValue(result, field);
                    continue;
                }

                object fieldValue = field.GetValue(source);
                field.SetValue(result, shallow ? fieldValue : CopyValue(fieldValue, shallow, mode));
            }

            CopyProperties(type, source, result, shallow, mode);

            return result;
        }

        /// <summary>
        /// Copies properties from the source object to the target object, respecting CloneIgnore attributes.
        /// </summary>
        /// <param name="type">The type of the objects.</param>
        /// <param name="source">The source object.</param>
        /// <param name="target">The target object.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <param name="mode">The mode of cloning to be used.</param>
        private static void CopyProperties(
            Type type,
            object source,
            object target,
            bool shallow,
            CloneMode mode
        )
        {
            foreach (
                PropertyInfo property in type.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                )
            )
            {
                if (!property.CanRead || !property.CanWrite)
                {
                    continue;
                }

                if (
                    mode == CloneMode.Normal
                    && Attribute.IsDefined(property, typeof(CloneIgnoreAttribute))
                )
                {
                    object defaultValue = property.PropertyType.IsValueType
                        ? Activator.CreateInstance(property.PropertyType)
                        : null;

                    try
                    {
                        property.SetValue(target, defaultValue, null);
                    }
                    catch
                    {
                        continue;
                    }

                    continue;
                }

                try
                {
                    object propertyValue = property.GetValue(source, null);
                    property.SetValue(
                        target,
                        shallow ? propertyValue : CopyValue(propertyValue, shallow, mode),
                        null
                    );
                }
                catch
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// Retrieves all fields of a given type, including inherited fields.
        /// </summary>
        /// <param name="type">The type to get fields from.</param>
        /// <returns>An enumerable of FieldInfo objects.</returns>
        private static IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            return type == null
                ? Enumerable.Empty<FieldInfo>()
                : type.GetFields(
                        BindingFlags.Instance
                            | BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.DeclaredOnly
                    )
                    .Concat(GetAllFields(type.BaseType));
        }

        /// <summary>
        /// Sets a field's value to its default for value types or null for reference types.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="field">The field to set.</param>
        private static void SetDefaultFieldValue(object target, FieldInfo field)
        {
            object defaultValue = field.FieldType.IsValueType
                ? Activator.CreateInstance(field.FieldType)
                : null;
            field.SetValue(target, defaultValue);
        }
    }
}
