//WPF is shit
using System.ComponentModel;
using System.Windows.Controls;

namespace OngekiFumenEditor.UI.Controls
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
