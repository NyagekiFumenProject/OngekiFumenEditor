using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums
{
	public enum Shooter
	{
		/// <summary>
		/// 从玩家头顶位置
		/// </summary>
		TargetHead = 0,
		/// <summary>
		/// 从敌人位置
		/// </summary>
		Enemy = 1,
		/// <summary>
		/// 谱面中心(?)
		/// </summary>
		Center = 2,
	}
}
