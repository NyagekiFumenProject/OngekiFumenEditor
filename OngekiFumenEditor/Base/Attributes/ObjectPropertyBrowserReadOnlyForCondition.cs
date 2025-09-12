using System;

namespace OngekiFumenEditor.Base.Attributes
{
    /// <summary>
    /// 钦定此属性在属性查看栏为只读
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public abstract class ObjectPropertyBrowserReadOnlyForCondition : Attribute
    {
        public abstract bool CheckIfReadOnly(object propOwner);
    }

    public class ObjectPropertyBrowserReadOnlyForCondition<T> : ObjectPropertyBrowserReadOnlyForCondition
    {
        private readonly Func<T, bool> condition;

        public ObjectPropertyBrowserReadOnlyForCondition(Func<T, bool> condition)
        {
            ArgumentNullException.ThrowIfNull(condition);
            this.condition = condition;
        }

        public override bool CheckIfReadOnly(object propOwner)
        {
            return propOwner is T t && condition(t);
        }
    }
}
