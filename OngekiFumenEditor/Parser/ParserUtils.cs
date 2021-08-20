using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser
{
    public static class ParserUtils
    {
        public static readonly char[] SplitEmptyCharArray = new[] { ' ', '\t' };
        public static string[] SplitEmptyChar(string line) => line.Trim().Split(SplitEmptyCharArray, StringSplitOptions.RemoveEmptyEntries);
        public static T[] SplitData<T>(string line) => line.Trim().Split(SplitEmptyCharArray, StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(x => (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(x)).ToArray();
    }
}
