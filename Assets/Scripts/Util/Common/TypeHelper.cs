using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Provides helper methods for type-related operations.
/// </summary>
static class TypeHelper
{
    /// <summary>
    /// Determines whether the specified type is a primitive type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is primitive, otherwise false.</returns>
    public static bool IsPrimitive(Type type)
    {
        return type.IsPrimitive || type == typeof(string);
    }

    /// <summary>
    /// Determines whether the specified type is a numeric type.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsValueType(Type type)
    {
        return type.IsValueType || type == typeof(string);
    }

    /// <summary>
    /// Determines whether the specified type is a struct.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a struct, otherwise false.</returns>
    public static bool IsStruct(Type type)
    {
        return type.IsValueType && !type.IsEnum && !type.IsPrimitive;
    }

    /// <summary>
    /// Determines whether the specified type is enumerable.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is enumerable, otherwise false.</returns>
    public static bool IsEnumerable(Type type)
    {
        return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
    }

    /// <summary>
    /// Determines whether the specified type is a dictionary.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a dictionary, otherwise false.</returns>
    public static bool IsDictionary(Type type)
    {
        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            return true;
        }

        return type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsClass(Type type)
    {
        return type.IsClass;
    }

    /// <summary>
    /// Determines whether the specified type is a tuple.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a tuple, otherwise false.</returns>
    public static bool IsTuple(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        Type genericDefinition = type.GetGenericTypeDefinition();
        return genericDefinition == typeof(Tuple<>)
            || genericDefinition == typeof(Tuple<,>)
            || genericDefinition == typeof(Tuple<,,>)
            || genericDefinition == typeof(Tuple<,,,>)
            || genericDefinition == typeof(Tuple<,,,,>)
            || genericDefinition == typeof(Tuple<,,,,,>)
            || genericDefinition == typeof(Tuple<,,,,,,>)
            || genericDefinition == typeof(Tuple<,,,,,,,>);
    }

    /// <summary>
    /// Checks if the given type has the specified attribute.
    /// </summary>
    /// <typeparam name="TAttribute">The type of attribute to check for.</typeparam>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type has the attribute, false otherwise.</returns>
    public static bool HasAttribute<TAttribute>(Type type)
        where TAttribute : Attribute
    {
        return type.GetCustomAttributes(typeof(TAttribute), true).Length > 0;
    }

    /// <summary>
    /// Checks if the given type has the specified attribute.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="attributeType">The type of attribute to check for.</param>
    /// <returns>True if the type has the attribute, false otherwise.</returns>
    public static bool HasAttribute(Type type, Type attributeType)
    {
        if (!typeof(Attribute).IsAssignableFrom(attributeType))
        {
            throw new ArgumentException(
                "The provided type is not an Attribute",
                nameof(attributeType)
            );
        }
        return type.GetCustomAttributes(attributeType, true).Length > 0;
    }

    /// <summary>
    /// Converts a string to a primitive type.
    /// </summary>
    /// <param name="content">The string to convert.</param>
    /// <param name="targetType">The target primitive type.</param>
    /// <returns>The converted primitive value.</returns>
    public static object ConvertToPrimitive(string content, Type targetType)
    {
        if (targetType.IsEnum)
            return Enum.Parse(targetType, content);
        if (targetType == typeof(string))
            return content;
        if (targetType == typeof(int))
            return int.Parse(content);
        if (targetType == typeof(double))
            return double.Parse(content);
        if (targetType == typeof(bool))
            return bool.Parse(content);
        if (targetType == typeof(float))
            return float.Parse(content);
        if (targetType == typeof(long))
            return long.Parse(content);

        throw new ArgumentException($"Unsupported primitive type: {targetType.FullName}");
    }

    /// <summary>
    /// Converts the specified value to its string representation.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A string representation of the value.</returns>
    public static string ConvertToString(object value)
    {
        if (value == null)
            return string.Empty;

        Type type = value.GetType();

        if (type.IsEnum)
            return value.ToString();
        if (type == typeof(DateTime))
            return ((DateTime)value).ToString("o");
        if (type == typeof(DateTimeOffset))
            return ((DateTimeOffset)value).ToString("o");
        if (type == typeof(TimeSpan))
            return ((TimeSpan)value).ToString("c");

        return value.ToString();
    }
}
