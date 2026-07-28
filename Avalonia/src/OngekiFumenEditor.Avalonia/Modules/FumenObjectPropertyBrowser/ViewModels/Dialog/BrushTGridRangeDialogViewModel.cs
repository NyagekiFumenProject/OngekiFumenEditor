using Gekimini.Avalonia.Modules.Window.ViewModels;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels.Dialog
{
	public class BrushTGridRangeDialogViewModel : WindowViewModelBase
    {
		private TGrid beginTGrid = new TGrid();
		private TGrid endTGrid = new TGrid();

		public TGrid BeginTGrid
		{
			get => beginTGrid;
			set => SetProperty(ref beginTGrid, value);
		}

		public TGrid EndTGrid
		{
			get => endTGrid;
			set => SetProperty(ref endTGrid, value);
		}

		public void OnCancelButtonClicked()
		{
			this.TryCloseAsync(false);
		}

		public void OnComfirmButtonClicked()
		{
			this.TryCloseAsync(true);
		}
	}
}

