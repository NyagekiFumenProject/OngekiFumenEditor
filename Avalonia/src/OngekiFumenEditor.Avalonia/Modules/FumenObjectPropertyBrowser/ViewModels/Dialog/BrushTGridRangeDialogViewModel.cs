using Gekimini.Avalonia.Modules.Window.ViewModels;
using OngekiFumenEditor.Avalonia.Base;
using CommunityToolkit.Mvvm.Input;

using OngekiFumenEditor.Avalonia.Utils;
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
			Log.LogInfo("BrushTGridRange dialog cancelled by user.");
			await TryCloseAsync(false);
		}

		[RelayCommand]
		private async Task ConfirmAsync()
		{
			Log.LogInfo($"BrushTGridRange dialog confirmed by user (begin={BeginTGrid?.Unit}:{BeginTGrid?.Grid}, end={EndTGrid?.Unit}:{EndTGrid?.Grid}).");
			await TryCloseAsync(true);
		}
	}
}

