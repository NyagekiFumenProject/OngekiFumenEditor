using OngekiFumenEditor.Avalonia.Base;
using System;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions
{
	public class OngekiObjectDropParam : EditorAddObjectDropAction
	{
		private readonly Func<OngekiObjectBase> lazyLoadFunc;

		public OngekiObjectDropParam(Func<OngekiObjectBase> lazyLoadFunc)
		{
			this.lazyLoadFunc = lazyLoadFunc;
		}

		protected override OngekiObjectBase GetDisplayObject()
		{
			return lazyLoadFunc();
		}
	}
}


