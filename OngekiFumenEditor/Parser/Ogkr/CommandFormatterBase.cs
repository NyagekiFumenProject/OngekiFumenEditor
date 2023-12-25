using OngekiFumenEditor.Base;
using OngekiFumenEditor.Parser.Ogkr;
using System;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr
{
	public abstract class CommandFormatterBase<T> : ICommandFormatter
	{
		public virtual Type FormatTargetType => typeof(T);

		public abstract string Section { get; }

		public (string, int) Format(object command, OngekiFumen refFumen) => Format((T)command, refFumen);

		public abstract (string, int) Format(T command, OngekiFumen refFumen);
	}
}
