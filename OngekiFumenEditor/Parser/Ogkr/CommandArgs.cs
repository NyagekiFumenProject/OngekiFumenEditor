using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Parser.Ogkr
{
    public class CommandArgs
    {
        private static readonly char[] SplitEmptyCharArray = new[] {/* ' ',*/ '\t' };
        private static readonly Dictionary<Type, TypeConverter> staticConverterCache = new();

        private string line = string.Empty;
        private string[] cachedInputs;
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
                cachedInputs = null;
                line = value;
            }
        }

        public T GetData<T>(int index)
        {
            var arr = GetDataArray<T>();
            return (uint)index < (uint)arr.Length ? arr[index] : default;
        }

        public T[] GetDataArray<T>()
        {
            var type = typeof(T);
            if (cacheDataArray.TryGetValue(type, out var array))
                return (T[])array;

            var inputs = GetInputs();
            T[] arr;

            if (converters.TryGetValue(type, out var argConverter))
            {
                // 内置 converter 对每个输入返回一个值；这里直接消费一次，不再经过
                // OfType<T>().ToArray() 的两层迭代器和中间数组。
                arr = new T[inputs.Length];
                var count = 0;
                foreach (var value in argConverter.Parser(inputs))
                {
                    if (value is not T typed)
                        continue;

                    if (count == arr.Length)
                        Array.Resize(ref arr, Math.Max(4, arr.Length * 2));

                    arr[count++] = typed;
                }

                if (count != arr.Length)
                    Array.Resize(ref arr, count);
            }
            else
            {
                // 无 MEF 环境（或自定义类型未注册 converter）时的后备；
                // 固定使用不变区域设置，避免小数点随系统区域变化。
                arr = new T[inputs.Length];
                var converter = GetCachedConverter(type);
                for (var i = 0; i < inputs.Length; i++)
                {
                    var x = inputs[i];
                    if (converter.IsValid(x))
                        arr[i] = (T)converter.ConvertFromInvariantString(x);
                }
            }

            cacheDataArray[type] = arr;
            return arr;
        }

        private string[] GetInputs()
        {
            return cachedInputs ??= line.Trim().Split(SplitEmptyCharArray);
        }

        private static TypeConverter GetCachedConverter(Type type)
        {
            if (!staticConverterCache.TryGetValue(type, out var c))
                staticConverterCache[type] = c = TypeDescriptor.GetConverter(type);
            return c;
        }
    }
}
