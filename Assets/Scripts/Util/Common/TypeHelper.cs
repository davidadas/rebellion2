using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rebellion.Util.Common
{
    /// <summary>
    /// Provides helper methods for type-related operations.
    /// </summary>
    public static class TypeHelper
    {
        /// <summary>
        /// Determines whether the specified type is a scalar type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is scalar, otherwise false.</returns>
        public static bool IsScalar(Type type)
        {
            return type == typeof(string)
                || type == typeof(bool)
                || type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal)
                || type == typeof(char)
                || type.IsEnum
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(TimeSpan)
                || type == typeof(Guid);
        }

        /// <summary>
        /// Determines whether the specified type stores values directly.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is a value type or string; otherwise false.</returns>
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
                .Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                );
        }

        /// <summary>
        /// Determines whether the specified type is a class.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is a class; otherwise, false.</returns>
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
        /// Converts a string to a scalar type.
        /// </summary>
        /// <param name="content">The string to convert.</param>
        /// <param name="targetType">The target scalar type.</param>
        /// <returns>The converted scalar value.</returns>
        public static object ConvertToScalar(string content, Type targetType)
        {
            if (targetType.IsEnum)
                return Enum.Parse(targetType, content);
            if (targetType == typeof(string))
                return content;
            if (targetType == typeof(bool))
                return bool.Parse(content);
            if (targetType == typeof(byte))
                return byte.Parse(content, CultureInfo.InvariantCulture);
            if (targetType == typeof(sbyte))
                return sbyte.Parse(content, CultureInfo.InvariantCulture);
            if (targetType == typeof(short))
                return short.Parse(content, CultureInfo.InvariantCulture);
            if (targetType == typeof(ushort))
                return ushort.Parse(content, CultureInfo.InvariantCulture);
            if (targetType == typeof(int))
                return int.Parse(content, CultureInfo.InvariantCulture);
            if (targetType == typeof(uint))
                return uint.Parse(content, CultureInfo.InvariantCulture);
            if (targetType == typeof(long))
                return long.Parse(content, CultureInfo.InvariantCulture);
            if (targetType == typeof(ulong))
                return ulong.Parse(content, CultureInfo.InvariantCulture);
            if (targetType == typeof(float))
                return float.Parse(content, CultureInfo.InvariantCulture);
            if (targetType == typeof(double))
                return double.Parse(content, CultureInfo.InvariantCulture);
            if (targetType == typeof(decimal))
                return decimal.Parse(content, CultureInfo.InvariantCulture);
            if (targetType == typeof(char))
                return char.Parse(content);
            if (targetType == typeof(DateTime))
                return DateTime.Parse(content, null, DateTimeStyles.RoundtripKind);
            if (targetType == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(content, null, DateTimeStyles.RoundtripKind);
            if (targetType == typeof(TimeSpan))
                return TimeSpan.ParseExact(content, "c", CultureInfo.InvariantCulture);
            if (targetType == typeof(Guid))
                return Guid.Parse(content);

            throw new ArgumentException($"Unsupported scalar type: {targetType.FullName}");
        }

        /// <summary>
        /// Converts the specified value to its string representation.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A string representation of the value.</returns>
        public static string ConvertScalarToString(object value)
        {
            if (value == null)
                return string.Empty;

            Type type = value.GetType();

            if (type.IsEnum)
                return value.ToString();
            if (type == typeof(string))
                return (string)value;
            if (type == typeof(bool))
                return value.ToString();
            if (type == typeof(char))
                return value.ToString();
            if (type == typeof(DateTime))
                return ((DateTime)value).ToString("o");
            if (type == typeof(DateTimeOffset))
                return ((DateTimeOffset)value).ToString("o");
            if (type == typeof(TimeSpan))
                return ((TimeSpan)value).ToString("c");
            if (type == typeof(Guid))
                return ((Guid)value).ToString("D");
            if (IsScalar(type) && value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            throw new ArgumentException($"Unsupported scalar type: {type.FullName}");
        }
    }
}
