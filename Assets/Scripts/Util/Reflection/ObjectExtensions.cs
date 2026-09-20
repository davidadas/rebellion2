using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Rebellion.Util.Reflection
{
    /// <summary>
    /// Provides extension methods for creating shallow and deep copies of ordinary data objects.
    /// </summary>
    public static class ObjectExtensions
    {
        /// <summary>
        /// Creates a deep copy of the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be copied.</typeparam>
        /// <param name="source">The source object to be copied.</param>
        /// <returns>A deep copy of the source object.</returns>
        public static T GetDeepCopy<T>(this T source)
            where T : class
        {
            if (source == null)
            {
                return null;
            }

            return (T)CopyValue(source, shallow: false);
        }

        /// <summary>
        /// Creates a shallow copy of the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be copied.</typeparam>
        /// <param name="source">The source object to be copied.</param>
        /// <returns>A shallow copy of the source object.</returns>
        public static T GetShallowCopy<T>(this T source)
            where T : class
        {
            if (source == null)
            {
                return null;
            }

            return (T)CopyValue(source, shallow: true);
        }

        /// <summary>
        /// Copies the value of an object, either shallowly or deeply.
        /// </summary>
        /// <param name="source">The source object to be copied.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <returns>The copied object.</returns>
        private static object CopyValue(object source, bool shallow)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();

            if (source is Type)
            {
                return source;
            }

            if (IsValueType(type))
            {
                if (IsStruct(type))
                {
                    return CopyStruct(source, shallow);
                }
                return source;
            }
            if (IsDictionary(type))
            {
                return CopyDictionary((IDictionary)source, shallow);
            }
            if (IsEnumerable(type))
            {
                return CopyCollection((IEnumerable)source, shallow);
            }
            if (IsTuple(type))
            {
                return CopyTuple(source, type, shallow);
            }
            if (type.IsClass)
            {
                return CopyObject(source, shallow);
            }

            throw new ArgumentException($"Type {type} not supported when copying objects.");
        }

        /// <summary>
        /// Creates a copy of a dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary to be copied.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <returns>The copied dictionary.</returns>
        private static IDictionary CopyDictionary(IDictionary dictionary, bool shallow)
        {
            if (shallow)
            {
                return dictionary;
            }

            Type type = dictionary.GetType();
            IDictionary result = (IDictionary)Activator.CreateInstance(type);

            foreach (DictionaryEntry entry in dictionary)
            {
                object copiedKey = CopyValue(entry.Key, shallow);
                object copiedValue = CopyValue(entry.Value, shallow);
                result.Add(copiedKey, copiedValue);
            }

            return result;
        }

        /// <summary>
        /// Creates a copy of a collection.
        /// </summary>
        /// <param name="collection">The collection to be copied.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <returns>The copied collection.</returns>
        private static object CopyCollection(IEnumerable collection, bool shallow)
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
                    destinationArray.SetValue(CopyValue(sourceArray.GetValue(i), shallow), i);
                }

                return destinationArray;
            }

            Type elementType = type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
            Type listType = typeof(List<>).MakeGenericType(elementType);
            IList newList = (IList)Activator.CreateInstance(listType);

            foreach (object item in collection)
            {
                newList.Add(CopyValue(item, shallow));
            }

            return newList;
        }

        /// <summary>
        /// Creates a copy of a struct.
        /// </summary>
        /// <param name="source">The struct to be copied.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <returns>The copied struct.</returns>
        private static object CopyStruct(object source, bool shallow)
        {
            Type type = source.GetType();
            object result = Activator.CreateInstance(type);

            FieldInfo[] fields = type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            foreach (FieldInfo field in fields)
            {
                object fieldValue = field.GetValue(source);
                object copiedValue = shallow ? fieldValue : CopyValue(fieldValue, shallow);
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
        /// <returns>The copied tuple.</returns>
        private static object CopyTuple(object source, Type tupleType, bool shallow)
        {
            int tupleLength = tupleType.GetGenericArguments().Length;
            object[] values = new object[tupleLength];

            for (int i = 0; i < tupleLength; i++)
            {
                PropertyInfo itemProperty = tupleType.GetProperty($"Item{i + 1}");
                object itemValue = itemProperty.GetValue(source);
                values[i] = shallow ? itemValue : CopyValue(itemValue, shallow);
            }

            return Activator.CreateInstance(tupleType, values);
        }

        /// <summary>
        /// Creates a copy of an object.
        /// </summary>
        /// <param name="source">The object to be copied.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        /// <returns>The copied object.</returns>
        private static object CopyObject(object source, bool shallow)
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
                object fieldValue = field.GetValue(source);
                field.SetValue(result, shallow ? fieldValue : CopyValue(fieldValue, shallow));
            }

            CopyProperties(type, source, result, shallow);

            return result;
        }

        /// <summary>
        /// Copies properties from the source object to the target object.
        /// </summary>
        /// <param name="type">The type of the objects.</param>
        /// <param name="source">The source object.</param>
        /// <param name="target">The target object.</param>
        /// <param name="shallow">Whether to perform a shallow copy.</param>
        private static void CopyProperties(Type type, object source, object target, bool shallow)
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

                try
                {
                    object propertyValue = property.GetValue(source, null);
                    property.SetValue(
                        target,
                        shallow ? propertyValue : CopyValue(propertyValue, shallow),
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
        /// Returns whether a type stores values directly during cloning.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>True for value types and strings.</returns>
        private static bool IsValueType(Type type)
        {
            return type.IsValueType || type == typeof(string);
        }

        /// <summary>
        /// Returns whether a type is a non-primitive, non-enum value type.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>True when the type is a struct.</returns>
        private static bool IsStruct(Type type)
        {
            return type.IsValueType && !type.IsEnum && !type.IsPrimitive;
        }

        /// <summary>
        /// Returns whether a type represents an enumerable object rather than a string.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>True when the type is enumerable.</returns>
        private static bool IsEnumerable(Type type)
        {
            return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
        }

        /// <summary>
        /// Returns whether a type implements a dictionary contract.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>True when the type is a dictionary.</returns>
        private static bool IsDictionary(Type type)
        {
            return typeof(IDictionary).IsAssignableFrom(type)
                || type.GetInterfaces()
                    .Any(interfaceType =>
                        interfaceType.IsGenericType
                        && interfaceType.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                    );
        }

        /// <summary>
        /// Returns whether a type is a reference-tuple type.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>True when the type is a tuple.</returns>
        private static bool IsTuple(Type type)
        {
            if (!type.IsGenericType)
                return false;

            Type definition = type.GetGenericTypeDefinition();
            return definition == typeof(Tuple<>)
                || definition == typeof(Tuple<,>)
                || definition == typeof(Tuple<,,>)
                || definition == typeof(Tuple<,,,>)
                || definition == typeof(Tuple<,,,,>)
                || definition == typeof(Tuple<,,,,,>)
                || definition == typeof(Tuple<,,,,,,>)
                || definition == typeof(Tuple<,,,,,,,>);
        }
    }
}
