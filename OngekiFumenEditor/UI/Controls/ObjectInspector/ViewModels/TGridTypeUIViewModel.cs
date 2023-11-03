using OngekiFumenEditor.Base;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
	public class TGridTypeUIViewModel : CommonUIViewModelBase<TGrid>
	{
		private object cacheGrid = DependencyProperty.UnsetValue;
		public object Grid
		{
			get
			{
				var val = ProxyValue;
				if (val is TGrid tGrid)
					return tGrid.Grid;
				return cacheGrid;
			}
			set
			{
				if (int.TryParse(value?.ToString(), out var v))
				{
					cacheGrid = v;
					TryApplyValue(v, Unit);
					NotifyOfPropertyChange(() => Grid);
				}
			}
		}

		private object cacheUnit = DependencyProperty.UnsetValue;
		public object Unit
		{
			get
			{
				var val = ProxyValue;
				if (val is TGrid tGrid)
					return tGrid.Unit;
				return cacheUnit;
			}
			set
			{
				if (float.TryParse(value?.ToString(), out var v))
				{
					cacheUnit = v;
					TryApplyValue(Grid, v);
					NotifyOfPropertyChange(() => Unit);
				}
			}
		}

		private void TryApplyValue(object Grid, object Unit)
		{
			if (Grid is int grid && Unit is float unit)
			{
				var newVal = new TGrid(unit, grid);
				TypedProxyValue = newVal;
			}
		}

		public TGridTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
		{

		}

		public void SetNull()
		{
			var rollback = TypedProxyValue;
			try
			{
				TypedProxyValue = null;
			}
			catch (Exception e)
			{
				Log.LogError($"Can't set null for prop {PropertyInfo.DisplayPropertyName}: {e.Message}");
				TypedProxyValue = rollback;
			}
		}
	}
}
