using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Base
{
	public abstract class OngekiObjectBase : ObservableObject
	{
		private static int ID_GEN = 0;

		[ObjectPropertyBrowserReadOnly]
		[LocalizableObjectPropertyBrowserAlias(nameof(Lang.ObjectId))]
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
		/// 婢跺秴鍩楅悧鈺€娆㈤崣鍌涙殶閸滃苯鍞寸€?
		/// </summary>
		/// <param name="fromObj">婢跺秴鍩楀┃鎰剁礉閺堫剙顕挒锛勬畱娴犲灝鍩楅惄顔界垼</param>
		public abstract void Copy(OngekiObjectBase fromObj);

		public OngekiObjectBase CopyNew()
		{
			var newObj = CacheLambdaActivator.CreateInstance(GetType()) as OngekiObjectBase;
			newObj.Copy(this);
			return newObj;
		}
	}
}


