using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ArgProcesser.Attributes
{
    public abstract class OptionBindingAttrbuteBase : Attribute
    {
        public OptionBindingAttrbuteBase(string name, string description, object defaultValue, Type type)
        {
            Name = name;
            Description = description;
            DefaultValue = defaultValue;
            Type = type;
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public object DefaultValue { get; set; }
        public Type Type { get; }
        public bool Require { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class OptionBindingAttrbute<T> : OptionBindingAttrbuteBase
    {
        public OptionBindingAttrbute(string name, string description, T defaultValue) : base(name, description, defaultValue, typeof(T))
        {

        }
    }
}
