using OngekiFumenEditor.Base;
using OngekiFumenEditor.Parser.DefaultImpl.Ogkr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.Ogkr
{
	public interface ICommandFormatter
	{
		public Type FormatTargetType { get; }
		public string Section { get; }
		public (string, int) Format(object command, OngekiFumen refFumen);
	}
}
