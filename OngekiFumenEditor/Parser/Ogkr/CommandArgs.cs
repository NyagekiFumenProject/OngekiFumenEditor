using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace OngekiFumenEditor.Parser.Ogkr
{
    public class CommandArgs
    {
        private static readonly char[] SplitEmptyCharArray = new[] {/* ' ',*/ '\t' };
        private static readonly Dictionary<Type, TypeConverter> staticConverterCache = new();

        private string line = string.Empty;
        private Dictionary<Type, Array> cacheDataArray = new Dictionary<Type, Array>();
        private Dictionary<Type, IArgValueConverter> converters = new Dictionary<Type, IArgValueConverter>();

        public CommandArgs()
        {
            converters = IoC.GetAll<IArgValueConverter>().ToDictionary(x => x.ConvertType);
        }

        public string Line
        {
            get
            {
                return line;
            }
            set
            {
                cacheDataArray.Clear();
                line = value;
            }
        }

        public T GetData<T>(int index)
        {
            return GetDataArray<T>().ElementAtOrDefault(index);
        }

        public T[] GetDataArray<T>()
        {
            var type = typeof(T);
            if (cacheDataArray.TryGetValue(type, out var array))
                return (T[])array;

            var inputs = line.Trim().Split(SplitEmptyCharArray);
            T[] arr;

            if (converters.TryGetValue(type, out var argConverter))
            {
                arr = argConverter.Parser(inputs).OfType<T>().ToArray();
            }
            else
            {
                arr = new T[inputs.Length];

                if (type == typeof(int))
                {
                    for (var i = 0; i < inputs.Length; i++)
                        if (int.TryParse(inputs[i].AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                            arr[i] = (T)(object)v;
                }
                else if (type == typeof(float))
                {
                    for (var i = 0; i < inputs.Length; i++)
                        if (float.TryParse(inputs[i].AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                            arr[i] = (T)(object)v;
                }
                else if (type == typeof(double))
                {
                    for (var i = 0; i < inputs.Length; i++)
                        if (double.TryParse(inputs[i].AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                            arr[i] = (T)(object)v;
                }
                else if (type == typeof(string))
                {
                    Array.Copy(inputs, arr, inputs.Length);
                }
                else
                {
                    var converter = GetCachedConverter(type);
                    for (var i = 0; i < inputs.Length; i++)
                    {
                        var x = inputs[i];
                        if (converter.IsValid(x))
                            arr[i] = (T)converter.ConvertFromString(x);
                    }
                }
            }

            cacheDataArray[type] = arr;
            return arr;
        }

        private static TypeConverter GetCachedConverter(Type type)
        {
            if (!staticConverterCache.TryGetValue(type, out var c))
                staticConverterCache[type] = c = TypeDescriptor.GetConverter(type);
            return c;
        }
    }
}
