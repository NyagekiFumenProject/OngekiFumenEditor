using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public class FadeStringEnum
    {
        public string Value { get; }
        public FadeStringEnum(string value) => Value = value;

        public static IEnumerable<FadeStringEnum> GetDefaultValues(Type type)
        {
            return type.GetProperties(BindingFlags.Static | BindingFlags.Public)
                    .Where(x => x.CanRead && !x.CanWrite)
                    .Select(x => x.GetValue(null))
                    .OfType<FadeStringEnum>();
        }

        public static IEnumerable<T> GetDefaultValues<T>() where T : FadeStringEnum => GetDefaultValues(typeof(T)).OfType<T>();
        
        public static IEnumerable<string> GetDefaultNames(Type type)
        {
            return type.GetProperties(BindingFlags.Static | BindingFlags.Public)
                    .Where(x => x.CanRead && !x.CanWrite)
                    .Select(x => x.Name);
        }

        public static IEnumerable<string> GetDefaultNames<T>() where T : FadeStringEnum => GetDefaultNames(typeof(T));

        public static implicit operator string(FadeStringEnum s)
        {
            return s.Value;
        }

        public static implicit operator FadeStringEnum(string s)
        {
            return new FadeStringEnum(s);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
