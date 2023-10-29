using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums
{
	public enum Target
	{
		/// <summary>
		/// 射向玩家位置
		/// </summary>
		Player = 0,
		/// <summary>
		/// 射向对应位置，具体看使用的BLT指令的xUnit值
		/// </summary>
		FixField = 1
	}
}
