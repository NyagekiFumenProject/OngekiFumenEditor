using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace OngekiFumenEditor.Parser
{
    public static class ParserUtils
    {
        public static readonly char[] SplitEmptyCharArray = new[] { ' ', '\t' };

        private static readonly Dictionary<Type, TypeConverter> converterCache = new();

        public static string[] SplitEmptyChar(string line)
        {
            return line.Trim().Split(SplitEmptyCharArray, StringSplitOptions.RemoveEmptyEntries);
        }

        public static T[] GetDataArray<T>(string line)
        {
            var parts = SplitEmptyChar(line);
            if (parts.Length <= 1)
                return Array.Empty<T>();

            var result = new T[parts.Length - 1];
            var type = typeof(T);

            if (type == typeof(int))
            {
                for (var i = 1; i < parts.Length; i++)
                    if (int.TryParse(parts[i].AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        result[i - 1] = (T)(object)v;
            }
            else if (type == typeof(float))
            {
                for (var i = 1; i < parts.Length; i++)
                    if (float.TryParse(parts[i].AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        result[i - 1] = (T)(object)v;
            }
            else if (type == typeof(double))
            {
                for (var i = 1; i < parts.Length; i++)
                    if (double.TryParse(parts[i].AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        result[i - 1] = (T)(object)v;
            }
            else if (type == typeof(string))
            {
                Array.Copy(parts, 1, result, 0, parts.Length - 1);
            }
            else
            {
                var converter = GetCachedConverter(type);
                for (var i = 1; i < parts.Length; i++)
                {
                    var x = parts[i];
                    if (converter.IsValid(x))
                        result[i - 1] = (T)converter.ConvertFromString(x);
                }
            }

            return result;
        }

        private static TypeConverter GetCachedConverter(Type type)
        {
            if (!converterCache.TryGetValue(type, out var c))
                converterCache[type] = c = TypeDescriptor.GetConverter(type);
            return c;
        }
    }
}
