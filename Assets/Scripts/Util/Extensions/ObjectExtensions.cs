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
            // Return null if the source is null
            if (source == null)
                return null;

            Type type = source.GetType();

            // For value types and strings, return the source directly
            if (type.IsValueType || type == typeof(string))
                return source;

            // Create a new instance of the object
            T result = (T)Activator.CreateInstance(type);

            // Get all fields, including private and inherited
            foreach (FieldInfo field in GetAllFields(type))
            {
                // Skip fields marked with CloneIgnore attribute
                if (Attribute.IsDefined(field, typeof(CloneIgnoreAttribute)))
                    continue;

                // Copy the field value directly (shallow copy)
                object fieldValue = field.GetValue(source);
                field.SetValue(result, fieldValue);
            }

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
            // Return null if the source is null
            if (source == null)
                return null;

            // Perform deep copy
            return (T)DeepCopyObject(source);
        }

        /// <summary>
        /// Recursively creates a deep copy of an object.
        /// </summary>
        /// <param name="source">The object to be deep copied.</param>
        /// <returns>A deep copy of the source object.</returns>
        private static object DeepCopyObject(object source)
        {
            Type type = source.GetType();

            // For value types and strings, return the source directly
            if (type.IsValueType || type == typeof(string))
                return source;

            // Create a new instance of the object
            object result = Activator.CreateInstance(type);

            // Get all fields, including private and inherited
            foreach (FieldInfo field in GetAllFields(type))
            {
                // Skip fields marked with CloneIgnore attribute
                if (Attribute.IsDefined(field, typeof(CloneIgnoreAttribute)))
                    continue;

                object fieldValue = field.GetValue(source);

                // Skip null fields
                if (fieldValue == null)
                    continue;

                // Recursively copy the field value
                field.SetValue(result, DeepCopyField(fieldValue));
            }

            return result;
        }

        /// <summary>
        /// Creates a deep copy of a field value.
        /// </summary>
        /// <param name="fieldValue">The field value to be deep copied.</param>
        /// <returns>A deep copy of the field value.</returns>
        private static object DeepCopyField(object fieldValue)
        {
            Type fieldType = fieldValue.GetType();

            // For value types and strings, return the value directly
            if (fieldType.IsValueType || fieldType == typeof(string))
            {
                return fieldValue;
            }
            // For collections, use the DeepCopyCollection method
            else if (typeof(IEnumerable).IsAssignableFrom(fieldType))
            {
                return DeepCopyCollection(fieldValue);
            }
            // For other reference types, recursively deep copy
            else
            {
                return DeepCopyObject(fieldValue);
            }
        }

        /// <summary>
        /// Creates a deep copy of a collection.
        /// </summary>
        /// <param name="collection">The collection to be deep copied.</param>
        /// <returns>A deep copy of the collection.</returns>
        private static object DeepCopyCollection(object collection)
        {
            Type type = collection.GetType();

            // Handle arrays
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
            // Handle other collection types
            else
            {
                IList destinationCollection = (IList)Activator.CreateInstance(type);
                foreach (object item in (IEnumerable)collection)
                {
                    destinationCollection.Add(DeepCopyField(item));
                }
                return destinationCollection;
            }
        }

        /// <summary>
        /// Gets all fields for a given type, including inherited fields.
        /// </summary>
        /// <param name="t">The type to get fields for.</param>
        /// <returns>An IEnumerable of FieldInfo objects representing all fields in the type hierarchy.</returns>
        private static IEnumerable<FieldInfo> GetAllFields(Type t)
        {
            if (t == null)
                return Enumerable.Empty<FieldInfo>();

            // Get fields of the current type and recursively get fields of the base type
            return t.GetFields(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly
                )
                .Concat(GetAllFields(t.BaseType));
        }
    }
}
