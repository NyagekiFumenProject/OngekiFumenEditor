using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser
{
    public class CommandArgs
    {
        private static readonly char[] SplitEmptyCharArray = new[] { ' ', '\t' };

        private string line = string.Empty;
        private Dictionary<Type, Array> cacheDataArray = new Dictionary<Type, Array>();

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

            var converter = TypeDescriptor.GetConverter(type);
            var arr = line.Trim().Split(SplitEmptyCharArray, StringSplitOptions.RemoveEmptyEntries).Skip(type == typeof(string) ? 0 : 1).Select(x =>
                {
                    if (converter.IsValid(x))
                        return (T)converter.ConvertFromString(x);
                    return default;
                });
            var a = (type == typeof(string) ? arr : arr.Prepend(default)).ToArray();
            cacheDataArray[type] = a;
            return a;
        }
    }
}
