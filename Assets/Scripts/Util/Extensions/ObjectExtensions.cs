using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ObjectExtensions
{
    public static class ObjectExtensions
    {
        /// <summary>
        /// Creates a shallow copy of the current object, excluding any properties or fields marked with the CloneIgnoreAttribute.
        /// </summary>
        /// <typeparam name="T">The type of the object to clone.</typeparam>
        /// <param name="obj">The object to clone.</param>
        /// <returns>A shallow copy of the current object, excluding any properties or fields marked with the CloneIgnoreAttribute.</returns>
        public static T CloneWithoutAttribute<T>(this T source)
            where T : new()
        {
            // Create a new instance of the target type.
            T target = new T();

            // Get all the properties of the source object.
            IEnumerable<PropertyInfo> properties = source.GetType().GetProperties();

            // Loop through each property and copy its value to the target object,
            // unless the property has the CloneIgnore attribute.
            foreach (PropertyInfo property in properties)
            {
                if (property.GetCustomAttribute<CloneIgnoreAttribute>() == null)
                {
                    // If the property is a collection, clone its elements and add them
                    // to the target collection.
                    if (typeof(IEnumerable<object>).IsAssignableFrom(property.PropertyType))
                    {
                        // Create a new collection of the same type as the source property.
                        object sourceCollection = property.GetValue(source);
                        object targetCollection = Activator.CreateInstance(property.PropertyType);

                        // Clone each element in the source collection and add it to the target collection.
                        foreach (object item in (IEnumerable<object>)sourceCollection)
                        {
                            MethodInfo cloneMethod = item.GetType().GetMethod("Clone");
                            if (cloneMethod != null)
                            {
                                targetCollection
                                    .GetType()
                                    .GetMethod("Add")
                                    .Invoke(
                                        targetCollection,
                                        new[] { cloneMethod.Invoke(item, null) }
                                    );
                            }
                            else
                            {
                                targetCollection
                                    .GetType()
                                    .GetMethod("Add")
                                    .Invoke(targetCollection, new[] { item });
                            }
                        }

                        // Set the cloned collection to the corresponding property of the target object.
                        property.SetValue(target, targetCollection);
                    }
                    else
                    {
                        // If the property is not a collection, copy its value to the target object.
                        object value = property.GetValue(source);
                        property.SetValue(target, value);
                    }
                }
            }

            // Get all the fields of the source object.
            IEnumerable<FieldInfo> fields = source.GetType().GetFields();

            // Loop through each field and copy its value to the target object,
            // unless the field has the CloneIgnore attribute.
            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttribute<CloneIgnoreAttribute>() == null)
                {
                    object value = field.GetValue(source);
                    field.SetValue(target, value);
                }
            }

            // Return the cloned object
            return target;
        }
    }
}
