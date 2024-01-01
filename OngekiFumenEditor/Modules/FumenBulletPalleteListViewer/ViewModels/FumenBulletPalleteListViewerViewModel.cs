using AngleSharp.Common;
using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Views;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenMetaInfoBrowser.ViewModels
{
	[Export(typeof(IFumenBulletPalleteListViewer))]
	public class FumenBulletPalleteListViewerViewModel : Tool, IFumenBulletPalleteListViewer
	{
		public FumenBulletPalleteListViewerViewModel()
		{
			DisplayName = Resources.FumenBulletPalleteListViewer;
			IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
			Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
		}

		private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
		{
			Editor = @new;
		}

		private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(FumenVisualEditorViewModel.Fumen))
			{
				if (Editor?.Fumen is not null)
					DataView = CollectionViewSource.GetDefaultView(Editor.Fumen.BulletPalleteList);
				else
					DataView = null;
			}
		}

		public override PaneLocation PreferredLocation => PaneLocation.Bottom;

		private string filter;
		public string Filter
		{
			get => filter;
			set => Set(ref filter, value);
		}

		private FumenVisualEditorViewModel editor;
		public FumenVisualEditorViewModel Editor
		{
			get
			{
				return editor;
			}
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(editor, value, OnEditorPropertyChanged);
				Set(ref editor, value);
				NotifyOfPropertyChange(() => IsEnable);

				if (Editor?.Fumen is not null)
					DataView = CollectionViewSource.GetDefaultView(Editor.Fumen.BulletPalleteList);
				else
					DataView = null;

				DataView?.Refresh();
			}
		}

		public bool IsEnable => Editor?.Fumen is not null;

		public ObservableCollection<BulletPallete> SelectedItems { get; } = new();

		private ICollectionView dataView;
		public ICollectionView DataView
		{
			get => dataView;
			set
			{
				Set(ref dataView, value);
				OnRefreshFilter();
			}
		}

		public void OnCreateNew()
		{
			var plattele = new BulletPallete();
			Editor.Fumen.AddObject(plattele);
			DataView?.Refresh();
		}

		public void OnDeleteSelecting(FumenBulletPalleteListViewerView e)
		{
			foreach (var item in SelectedItems.ToArray())
				Editor.Fumen.RemoveObject(item);
			DataView?.Refresh();
		}

		private bool _draggingItem;
		private Point _mouseStartPosition;
		private BulletPallete _selecting;

		public void OnMouseMoveAndDragNewBullet(ActionExecutionContext e)
		{
			if (!_draggingItem)
				return;
			var arg = e.EventArgs as MouseEventArgs;

			Point mousePosition = arg.GetPosition(null);
			Vector diff = _mouseStartPosition - mousePosition;

			if (arg.LeftButton == MouseButtonState.Pressed &&
				(Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
				Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
			{
				var dragData = new DataObject(ToolboxDragDrop.DataFormat, new OngekiObjectDropParam(() =>
				{
					var bullet = new Bullet()
					{
						ReferenceBulletPallete = _selecting
					};
					return bullet;
				}));
				DragDrop.DoDragDrop(e.Source, dragData, DragDropEffects.Move);
				_draggingItem = false;
			}
		}

		public void OnMouseMoveAndDragNewBell(ActionExecutionContext e)
		{
			if (!_draggingItem)
				return;
			var arg = e.EventArgs as MouseEventArgs;

			Point mousePosition = arg.GetPosition(null);
			Vector diff = _mouseStartPosition - mousePosition;

			if (arg.LeftButton == MouseButtonState.Pressed &&
				(Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
				Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
			{
				var dragData = new DataObject(ToolboxDragDrop.DataFormat, new OngekiObjectDropParam(() =>
				{
					var bullet = new Bell()
					{
						ReferenceBulletPallete = _selecting
					};
					return bullet;
				}));
				DragDrop.DoDragDrop(e.Source, dragData, DragDropEffects.Move);
				_draggingItem = false;
			}
		}

		public void OnMouseLeftButtonDown(ActionExecutionContext e)
		{
			var arg = e.EventArgs as MouseEventArgs;
			if ((arg.LeftButton != MouseButtonState.Pressed) || e.Source?.DataContext is not BulletPallete pallete)
				return;

			_mouseStartPosition = arg.GetPosition(null);
			_selecting = pallete;
			_draggingItem = true;
		}

		public void OnCopyNewBPL(ActionExecutionContext e)
		{
			var arg = e.EventArgs as MouseEventArgs;
			if ((arg.LeftButton != MouseButtonState.Pressed) || e.Source?.DataContext is not BulletPallete pallete)
				return;

			var cpBPL = new BulletPallete();
			cpBPL.Copy(pallete);
			cpBPL.StrID = null;

			Editor.Fumen.AddObject(cpBPL);
			DataView?.Refresh();
		}

		public void OnChangeEditorAxuiliaryLineColor(ActionExecutionContext e)
		{
			if (e.Source?.DataContext is not BulletPallete pallete)
				return;

			var dialog = new CommonColorPicker(() => pallete.EditorAxuiliaryLineColor, color => pallete.EditorAxuiliaryLineColor = color, Resources.ChangeAxuiliaryLineColor.Format(pallete.StrID));
			dialog.Show();
		}

		public void OnItemDoubleClick(BulletPallete e)
		{
			if (e is null)
				return;

			Editor.TryCancelAllObjectSelecting();
			Editor.Fumen.Bells
				.OfType<IBulletPalleteReferencable>()
				.Concat(Editor.Fumen.Bullets)
				.Where(x => x.ReferenceBulletPallete == e)
				.OfType<ISelectableObject>()
				.ForEach(x => x.IsSelected = true);

			IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
		}

		public void OnRefreshFilter()
		{
			if (DataView is ICollectionView view)
			{
				view.Filter = string.IsNullOrWhiteSpace(Filter) ? null : (r) =>
				{
					if (r is BulletPallete p)
						return p.ToString().Contains(Filter, StringComparison.InvariantCultureIgnoreCase);
					return false;
				};
			}
		}
	}
}
