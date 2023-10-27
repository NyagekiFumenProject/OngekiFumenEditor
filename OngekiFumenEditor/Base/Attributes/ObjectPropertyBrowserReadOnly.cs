using System;

namespace OngekiFumenEditor.Base.Attributes
{
	/// <summary>
	/// 钦定此属性在属性查看栏为只读
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
	public class ObjectPropertyBrowserReadOnly : Attribute
	{
	}
}
