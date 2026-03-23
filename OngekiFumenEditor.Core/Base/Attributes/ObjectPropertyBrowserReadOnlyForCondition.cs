using System;

namespace OngekiFumenEditor.Core.Base.Attributes
{
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
            if (condition is null)
                throw new ArgumentNullException(nameof(condition));
            this.condition = condition;
        }

        public override bool CheckIfReadOnly(object propOwner)
        {
            return propOwner is T t && condition(t);
        }
    }
}
