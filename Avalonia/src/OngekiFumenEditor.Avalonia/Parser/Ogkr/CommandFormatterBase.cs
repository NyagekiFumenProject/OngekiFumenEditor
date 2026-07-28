using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Parser.Ogkr;
using System;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr
{
	public abstract class CommandFormatterBase<T> : ICommandFormatter
	{
		public virtual Type FormatTargetType => typeof(T);

		public abstract string Section { get; }

		public (string, int) Format(object command, OngekiFumen refFumen) => Format((T)command, refFumen);

		public abstract (string, int) Format(T command, OngekiFumen refFumen);
	}
}


