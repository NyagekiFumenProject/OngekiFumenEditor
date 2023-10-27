using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.UI.Controls
{
	public class ColumnExpandableListView : ListView
	{
		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);
		}

		protected override DependencyObject GetContainerForItemOverride()
		{
			var r = base.GetContainerForItemOverride();
			return r;
		}

		protected override void OnItemContainerStyleChanged(Style oldItemContainerStyle, Style newItemContainerStyle)
		{
			base.OnItemContainerStyleChanged(oldItemContainerStyle, newItemContainerStyle);
		}
	}
}
