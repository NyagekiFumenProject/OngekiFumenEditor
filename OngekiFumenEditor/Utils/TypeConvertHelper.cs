using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace OngekiFumenEditor.Utils
{
    internal static class TypeConvertHelper
    {
        public static object? ConvertFromString(Type type, string? value)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            // null 处理
            if (value == null)
            {
                if (IsNullable(type))
                    return null;

                return Activator.CreateInstance(type);
            }

            // Nullable<T>
            Type actualType = Nullable.GetUnderlyingType(type) ?? type;

            // string
            if (actualType == typeof(string))
                return value;

            // enum
            if (actualType.IsEnum)
                return Enum.Parse(actualType, value, true);

            // TypeConverter
            TypeConverter converter = TypeDescriptor.GetConverter(actualType);

            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                return converter.ConvertFromInvariantString(value);
            }

            // fallback
            return Convert.ChangeType(
                value,
                actualType,
                CultureInfo.InvariantCulture);
        }

        private static bool IsNullable(Type type)
        {
            return !type.IsValueType
                   || Nullable.GetUnderlyingType(type) != null;
        }
    }
}
