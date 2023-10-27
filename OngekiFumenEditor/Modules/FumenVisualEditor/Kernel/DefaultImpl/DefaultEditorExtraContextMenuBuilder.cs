using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl
{
	[Export(typeof(IEditorExtraContextMenuBuilder))]
	public class DefaultEditorExtraContextMenuBuilder : IEditorExtraContextMenuBuilder
	{
		public IEnumerable<FrameworkElement> BuildMenuItems(IEnumerable<IFumenVisualEditorExtraMenuItemHandler> registerHandlers, FumenVisualEditorViewModel targetEditor)
		{
			var menuItemRoots = new Dictionary<string, MenuItem>();

			MenuItem makePath(string[] pathes)
			{
				string getVaildName(string n) => "S_" + Math.Abs(n.GetHashCode());

				var rootName = pathes[0];
				var name = getVaildName(rootName);
				if (!menuItemRoots.TryGetValue(name, out var curMenuItem))
				{
					curMenuItem = new MenuItem() { Name = name, Header = rootName };
					menuItemRoots[name] = curMenuItem;
				}

				foreach (var part in pathes.Skip(1))
				{
					name = getVaildName(part);
					if (curMenuItem.Items.OfType<MenuItem>().FirstOrDefault(x => x.Name == name) is not MenuItem subMenuItem)
					{
						subMenuItem = new MenuItem() { Name = name, Header = part };
						curMenuItem.Items.Add(subMenuItem);
					}

					curMenuItem = subMenuItem;
				}

				return curMenuItem;
			}

			foreach (var handler in registerHandlers)
			{
				var menuItem = makePath(handler.RegisterMenuPath);
				if (menuItem is null)
				{
					Log.LogError($"{handler.GetType().Name}.RegisterMenuPath = null");
					continue;
				}

				menuItem.Click += (_, b) => handler.Handle(targetEditor, b);
			}

			return menuItemRoots.Values;
		}
	}
}
