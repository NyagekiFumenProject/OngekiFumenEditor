using OngekiFumenEditor.Base;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions
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
