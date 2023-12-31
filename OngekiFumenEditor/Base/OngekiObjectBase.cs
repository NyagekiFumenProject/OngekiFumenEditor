using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Utils;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Base
{
	public abstract class OngekiObjectBase : PropertyChangedBase
	{
		private static int ID_GEN = 0;

		[ObjectPropertyBrowserReadOnly]
		public int Id { get; init; } = ID_GEN++;

		[ObjectPropertyBrowserHide]
		public abstract string IDShortName { get; }

		[ObjectPropertyBrowserHide]
		public string Name => GetType().GetTypeName();

		public override string ToString() => $"{{{IDShortName}}} OID[{Id}]";

		[ObjectPropertyBrowserHide]
		public override bool IsNotifying
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => base.IsNotifying;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => base.IsNotifying = value;
		}

		private string tag = string.Empty;
		/// <summary>
		/// 
		/// </summary>
		[ObjectPropertyBrowserTipText("ObjectTag")]
		public string Tag
		{
			get => tag;
			set => Set(ref tag, value);
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
