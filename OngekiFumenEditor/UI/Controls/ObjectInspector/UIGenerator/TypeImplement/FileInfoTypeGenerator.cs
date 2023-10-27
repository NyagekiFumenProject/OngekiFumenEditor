using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator.TypeImplement
{
	[Export(typeof(ITypeUIGenerator))]
	public class FileInfoTypeGenerator : ITypeUIGenerator
	{
		public IEnumerable<Type> SupportTypes { get; } = new[] {
			typeof(FileInfo)
		};

		public UIElement Generate(IObjectPropertyAccessProxy wrapper) => ViewHelper.CreateViewByViewModelType(() => new FileInfoTypeUIViewModel(wrapper));
	}
}
