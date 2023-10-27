using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator.TypeImplement
{
	[Export(typeof(ITypeUIGenerator))]
	public class BaseValueTypeGenerator : ITypeUIGenerator
	{
		public IEnumerable<Type> SupportTypes { get; } = new[] {
			typeof(int),
			typeof(long),
			typeof(short),

			typeof(uint),
			typeof(ulong),
			typeof(ushort),

			typeof(string),
			typeof(float),
			typeof(double),

            //nullable
            typeof(int?),
			typeof(long?),
			typeof(short?),

			typeof(uint?),
			typeof(ulong?),
			typeof(ushort?),

			typeof(float?),
			typeof(double?),
		};

		public UIElement Generate(IObjectPropertyAccessProxy wrapper) => ViewHelper.CreateViewByViewModelType(() => new BaseValueTypeUIViewModel(wrapper));
	}
}
