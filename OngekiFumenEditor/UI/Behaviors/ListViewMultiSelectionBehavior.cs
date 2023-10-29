using Microsoft.Xaml.Behaviors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.UI.Behaviors
{
	public class ListViewMultiSelectionBehavior : Behavior<ListView>
	{
		protected override void OnAttached()
		{
			base.OnAttached();
			if (SelectedItems != null)
			{
				AssociatedObject.SelectedItems.Clear();
				foreach (var item in SelectedItems)
				{
					AssociatedObject.SelectedItems.Add(item);
				}
			}
		}

		public IList SelectedItems
		{
			get { return (IList)GetValue(SelectedItemsProperty); }
			set { SetValue(SelectedItemsProperty, value); }
		}

		public static readonly DependencyProperty SelectedItemsProperty =
			DependencyProperty.Register("SelectedItems", typeof(IList), typeof(ListViewMultiSelectionBehavior), new UIPropertyMetadata(null, SelectedItemsChanged));

		private static void SelectedItemsChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
		{
			var behavior = o as ListViewMultiSelectionBehavior;
			if (behavior == null)
				return;

			var oldValue = e.OldValue as INotifyCollectionChanged;
			var newValue = e.NewValue as INotifyCollectionChanged;

			if (oldValue != null)
			{
				oldValue.CollectionChanged -= behavior.SourceCollectionChanged;
				behavior.AssociatedObject.SelectionChanged -= behavior.ListViewSelectionChanged;
			}
			if (newValue != null)
			{
				behavior.AssociatedObject.SelectedItems.Clear();
				foreach (var item in (IEnumerable)newValue)
				{
					behavior.AssociatedObject.SelectedItems.Add(item);
				}

				behavior.AssociatedObject.SelectionChanged += behavior.ListViewSelectionChanged;
				newValue.CollectionChanged += behavior.SourceCollectionChanged;
			}
		}

		private bool _isUpdatingTarget;
		private bool _isUpdatingSource;

		void SourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (_isUpdatingSource)
				return;

			try
			{
				_isUpdatingTarget = true;

				if (e.OldItems != null)
				{
					foreach (var item in e.OldItems)
					{
						AssociatedObject.SelectedItems.Remove(item);
					}
				}

				if (e.NewItems != null)
				{
					foreach (var item in e.NewItems)
					{
						AssociatedObject.SelectedItems.Add(item);
					}
				}

				if (e.Action == NotifyCollectionChangedAction.Reset)
				{
					AssociatedObject.SelectedItems.Clear();
				}
			}
			finally
			{
				_isUpdatingTarget = false;
			}
		}

		private void ListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_isUpdatingTarget)
				return;

			var selectedItems = this.SelectedItems;
			if (selectedItems == null)
				return;
			
			//这里存在一个问题，就是OriginalSource可能是其中的combobox变更
			if (e.OriginalSource != e.Source)
				return;

			try
			{
				_isUpdatingSource = true;

				foreach (var item in e.RemovedItems)
				{
					selectedItems.Remove(item);
				}

				foreach (var item in e.AddedItems)
				{
					selectedItems.Add(item);
				}
			}
			finally
			{
				_isUpdatingSource = false;
			}
		}
	}
}
