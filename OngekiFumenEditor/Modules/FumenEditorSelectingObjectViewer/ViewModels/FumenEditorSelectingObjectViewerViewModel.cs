using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Data;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ViewModels
{
	[Export(typeof(IFumenEditorSelectingObjectViewer))]
	public class FumenEditorSelectingObjectViewerViewModel : Tool, IFumenEditorSelectingObjectViewer
	{
		private FumenVisualEditorViewModel editor;
		public FumenVisualEditorViewModel Editor
		{
			get => editor;
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(editor, value, OnEditorPropChanged);
				Set(ref editor, value);
				OnRefresh();
			}
		}

		private void OnEditorPropChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(FumenVisualEditorViewModel.SelectObjects):
					OnRefresh();
					break;
				default:
					break;
			}
		}

		public ObservableCollection<ISelectableObject> SelectedItems { get; } = new();

		private List<ISelectableObject> editorSelectObjects = new();

		public ICollectionView CollectionView => dataView;
		private ICollectionView dataView;

		private GridViewColumnHeader _lastHeaderClicked = null;
		private ListSortDirection _lastDirection = ListSortDirection.Ascending;

		public override PaneLocation PreferredLocation => PaneLocation.Bottom;

		public FumenEditorSelectingObjectViewerViewModel()
		{
			DisplayName = Resources.FumenEditorSelectingObjectViewer;
			IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;

			dataView = CollectionViewSource.GetDefaultView(editorSelectObjects);
			Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
		}

		private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
		{
			Editor = @new;
		}

		public void OnRefresh()
		{
			editorSelectObjects.Clear();
			editorSelectObjects.AddRange(Editor?.SelectObjects ?? Enumerable.Empty<ISelectableObject>());
			dataView.Refresh();
		}

		public void OnCancelItemSelectedObjects(ActionExecutionContext ctx)
		{
			foreach (var item in SelectedItems.ToArray())
				item.IsSelected = false;

			IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
		}

		public void OnItemSingleClick(OngekiObjectBase item)
		{
			if (Editor is null)
				return;

			IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor, item);
		}

		public void OnItemDoubleClick(OngekiObjectBase item)
		{
			if (Editor is null)
				return;

			if (item is ITimelineObject timelineObject)
				Editor.ScrollTo(timelineObject.TGrid);

			Editor.SelectObjects.Where(x => x != item).FilterNull().ForEach(x => x.IsSelected = false);
			IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
		}

		private void Sort(string sortBy, ListSortDirection direction)
		{
			dataView.SortDescriptions.Clear();
			dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
			dataView.Refresh();
		}

		public void SortColumn(ActionExecutionContext ctx)
		{
			var e = ctx.EventArgs as RoutedEventArgs;

			var headerClicked = e.OriginalSource as GridViewColumnHeader;
			ListSortDirection direction;

			if (headerClicked != null)
			{
				if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
				{
					if (headerClicked != _lastHeaderClicked)
					{
						direction = ListSortDirection.Ascending;
					}
					else
					{
						if (_lastDirection == ListSortDirection.Ascending)
						{
							direction = ListSortDirection.Descending;
						}
						else
						{
							direction = ListSortDirection.Ascending;
						}
					}

					var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
					var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

					Sort(sortBy, direction);

					// Remove arrow from previously sorted header
					if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
					{
						_lastHeaderClicked.Column.HeaderTemplate = null;
					}

					_lastHeaderClicked = headerClicked;
					_lastDirection = direction;
				}
			}
		}
	}
}
