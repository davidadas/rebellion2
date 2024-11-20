using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ObjectExtensions
{
    /// <summary>
    /// Provides extension methods for copying objects.
    /// </summary>
    public static class ObjectExtensions
    {
        /// <summary>
        /// Creates a shallow copy of the given object, respecting CloneIgnore attributes.
        /// </summary>
        /// <typeparam name="T">The type of the object to be cloned. Must be a reference type.</typeparam>
        /// <param name="source">The object to be cloned.</param>
        /// <returns>A shallow copy of the source object, with fields/properties marked CloneIgnore set to null.</returns>
        public static T GetShallowCopy<T>(this T source)
            where T : class
        {
            if (source == null)
                return null;

            Type type = source.GetType();

            // For value types and strings, return the source directly.
            if (type.IsValueType || type == typeof(string))
                return source;

            T result = (T)Activator.CreateInstance(type);

            CopyFields(type, source, result);
            CopyProperties(type, source, result);

            return result;
        }

        /// <summary>
        /// Creates a deep copy of the given object, respecting CloneIgnore attributes.
        /// </summary>
        /// <typeparam name="T">The type of the object to be copied.</typeparam>
        /// <param name="source">The object to be copied.</param>
        /// <returns>A deep copy of the source object.</returns>
        public static T GetDeepCopy<T>(this T source)
            where T : class
        {
            if (source == null)
                return null;

            return (T)DeepCopyObject(source);
        }

        /// <summary>
        /// Copies fields from source to target, skipping fields with CloneIgnore attribute.
        /// </summary>
        private static void CopyFields(Type type, object source, object target)
        {
            foreach (FieldInfo field in GetAllFields(type))
            {
                if (Attribute.IsDefined(field, typeof(CloneIgnoreAttribute)))
                {
                    // Set ignored fields to their default value.
                    if (!field.FieldType.IsValueType)
                    {
                        field.SetValue(target, null);
                    }
                    else
                    {
                        field.SetValue(target, Activator.CreateInstance(field.FieldType));
                    }
                    continue;
                }

                // Copy the field value for fields not marked with CloneIgnore
                object value = field.GetValue(source);
                field.SetValue(target, value);
            }
        }

        /// <summary>
        /// Copies properties from source to target, skipping properties with CloneIgnore attribute.
        /// </summary>
        private static void CopyProperties(Type type, object source, object target)
        {
            foreach (
                PropertyInfo property in type.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                )
            )
            {
                if (Attribute.IsDefined(property, typeof(CloneIgnoreAttribute)))
                {
                    // Explicitly set the property to its default value if writable
                    if (property.CanWrite)
                    {
                        property.SetValue(target, default);
                    }
                    continue;
                }

                if (property.CanRead && property.CanWrite)
                {
                    object value = property.GetValue(source);
                    property.SetValue(target, value);
                }
            }
        }

        /// <summary>
        /// Recursively creates a deep copy of an object.
        /// </summary>
        private static object DeepCopyObject(object source)
        {
            Type type = source.GetType();

            if (type.IsValueType || type == typeof(string))
                return source;

            object result = Activator.CreateInstance(type);

            CopyFields(type, source, result);

            foreach (FieldInfo field in GetAllFields(type))
            {
                if (Attribute.IsDefined(field, typeof(CloneIgnoreAttribute)))
                    continue;

                object fieldValue = field.GetValue(source);
                if (fieldValue == null)
                    continue;

                field.SetValue(result, DeepCopyField(fieldValue));
            }

            return result;
        }

        /// <summary>
        /// Recursively creates a deep copy of a field value.
        /// </summary>
        private static object DeepCopyField(object fieldValue)
        {
            Type fieldType = fieldValue.GetType();

            if (fieldType.IsValueType || fieldType == typeof(string))
                return fieldValue;

            if (typeof(IEnumerable).IsAssignableFrom(fieldType))
                return DeepCopyCollection(fieldValue);

            return DeepCopyObject(fieldValue);
        }

        /// <summary>
        /// Creates a deep copy of a collection.
        /// </summary>
        private static object DeepCopyCollection(object collection)
        {
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
                    destinationArray.SetValue(DeepCopyField(sourceArray.GetValue(i)), i);
                }

                return destinationArray;
            }

            IList destinationCollection = (IList)Activator.CreateInstance(type);
            foreach (object item in (IEnumerable)collection)
            {
                destinationCollection.Add(DeepCopyField(item));
            }

            return destinationCollection;
        }

        /// <summary>
        /// Retrieves all fields of a given type, including inherited fields.
        /// </summary>
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
    }
}
