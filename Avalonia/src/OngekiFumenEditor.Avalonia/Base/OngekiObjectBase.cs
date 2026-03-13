using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.Attributes;

namespace OngekiFumenEditor.Avalonia.Base
{
	public abstract class OngekiObjectBase : ObservableObject
	{
		private static int ID_GEN = 0;

		[ObjectPropertyBrowserReadOnly]
		[LocalizableObjectPropertyBrowserAlias(nameof(Resources.ObjectId))]
		public int Id { get; init; } = ID_GEN++;

		[ObjectPropertyBrowserHide]
		public abstract string IDShortName { get; }

		[ObjectPropertyBrowserHide]
		public string Name => GetType().GetTypeName();

		public override string ToString() => $"{{{IDShortName}}} OID[{Id}]";

		private string tag = string.Empty;
		/// <summary>
		/// 
		/// </summary>
		[ObjectPropertyBrowserTipText("ObjectTag")]
		public string Tag
		{
			get => tag;
			set => SetProperty(ref tag, value);
		}

		/// <summary>
		/// 复制物件参数和内容
		/// </summary>
		/// <param name="fromObj">复制源，本对象的仿制目标</param>
		public abstract void Copy(OngekiObjectBase fromObj);

		public OngekiObjectBase CopyNew()
		{
			var newObj = CacheLambdaActivator.CreateInstance(GetType()) as OngekiObjectBase;
			newObj.Copy(this);
			return newObj;
		}
	}
}
