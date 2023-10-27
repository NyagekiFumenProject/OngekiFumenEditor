using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.UI.Controls
{
	public class ObjectPoolItemsControl : ItemsControl
	{
		Dictionary<Type, List<DependencyObject>> cachedObject = new();
		private object dirtyPrepareCreateItem = default;

		protected override bool IsItemItsOwnContainerOverride(object item)
		{
			dirtyPrepareCreateItem = item;
			return base.IsItemItsOwnContainerOverride(item);
		}

		protected override DependencyObject GetContainerForItemOverride()
		{
			if (cachedObject.TryGetValue(dirtyPrepareCreateItem.GetType(), out var list) && list.Count > 0)
			{
				var obj = list[0];
				list.Remove(obj);
				//Log.LogDebug($"add {dirtyPrepareCreateItem.GetType().Name} -> ({(obj as ContentPresenter)?.Content?.GetType()?.Name})");
				return obj;
			}
			dirtyPrepareCreateItem = default;
			return base.GetContainerForItemOverride();
		}

		protected override void ClearContainerForItemOverride(DependencyObject element, object item)
		{
			var type = item.GetType();
			//Log.LogDebug($"remove {type.Name} -> ({(element as ContentPresenter)?.Content?.GetType()?.Name})");
			if (!cachedObject.TryGetValue(type, out var list))
			{
				list = new();
				cachedObject[type] = list;
			}
			list.Add(element);
		}

		public void ClearCacheViews() => cachedObject.Clear();
	}
}
