using Gekimini.Avalonia.Modules.Window.ViewModels;
using OngekiFumenEditor.Avalonia.Base;
using CommunityToolkit.Mvvm.Input;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels.Dialog
{
	public partial class BrushTGridRangeDialogViewModel : WindowViewModelBase
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

		[RelayCommand]
		private async Task CancelAsync()
		{
			await TryCloseAsync(false);
		}

		[RelayCommand]
		private async Task ConfirmAsync()
		{
			await TryCloseAsync(true);
		}
	}
}

