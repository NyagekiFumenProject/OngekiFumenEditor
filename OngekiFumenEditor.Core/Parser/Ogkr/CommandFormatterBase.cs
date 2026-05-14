using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Parser.Ogkr;
using System;

namespace OngekiFumenEditor.Core.Parser.Ogkr
{
	public abstract class CommandFormatterBase<T> : ICommandFormatter
	{
		public virtual Type FormatTargetType => typeof(T);

		public abstract string Section { get; }

		public (string, int) Format(object command, OngekiFumen refFumen) => Format((T)command, refFumen);

		public abstract (string, int) Format(T command, OngekiFumen refFumen);
	}
}
