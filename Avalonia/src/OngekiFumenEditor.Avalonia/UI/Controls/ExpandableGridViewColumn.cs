//WPF is shit
using System.ComponentModel;
using Avalonia.Controls;

namespace OngekiFumenEditor.Avalonia.UI.Controls
{
	public class ExpandableGridViewColumn : GridViewColumn
	{
		public ExpandableGridViewColumn()
		{

		}

		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
		}
	}
}

