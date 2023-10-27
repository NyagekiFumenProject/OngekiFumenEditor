using Caliburn.Micro;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel;

namespace OngekiFumenEditor.Base
{
	public class FumenMetaInfo : PropertyChangedBase
	{
		public class BpmDef : PropertyChangedBase
		{
			private double first = 240;
			public double First
			{
				get => first;
				set => Set(ref first, value);
			}

			public double Common { get; set; } = 240;
			public double Minimum { get; set; } = 240;
			public double Maximum { get; set; } = 240;
		}

		public class MetDef
		{
			public int Bunbo { get; set; } = 4;
			public int Bunshi { get; set; } = 4;
		}

		public FumenMetaInfo()
		{
			BpmDefinition = BpmDefinition;
		}

		/// <summary>
		/// 版本号
		/// </summary>
		public Version Version { get; set; } = new Version(1, 0, 0);

		/// <summary>
		/// 谱面作者
		/// </summary>
		public string Creator { get; set; } = "";

		/// <summary>
		/// BPM定义信息
		/// </summary>
		private BpmDef bpmDefinition = new BpmDef();
		public BpmDef BpmDefinition
		{
			get => bpmDefinition;
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(bpmDefinition, value, OnBpmDefinitionPropChanged);
				Set(ref bpmDefinition, value);
			}
		}

		private void OnBpmDefinitionPropChanged(object sender, PropertyChangedEventArgs e)
		{
			NotifyOfPropertyChange(() => BpmDefinition);
		}

		/// <summary>
		/// 节拍信息
		/// </summary>
		public MetDef MeterDefinition { get; set; } = new MetDef();

		/// <summary>
		/// 物件时间轴长度基准值，用来参与物件的下落速度和物件之间的垂直距离计算
		/// </summary>
		public int TRESOLUTION { get; set; } = 1920;

		/// <summary>
		/// 物件水平位置宽度基准值，用来参与物件水平位置计算的
		/// </summary>
		public int XRESOLUTION { get; set; } = 4096;

		/// <summary>
		/// 初始节拍声音速度(就是开头几个节拍音效)?
		/// </summary>
		public int ClickDefinition { get; set; } = 1920;

		/// <summary>
		/// 是否为教程，没用到
		/// </summary>
		public bool Tutorial { get; set; } = false;

		/// <summary>
		/// (?)伤害
		/// </summary>
		public double BulletDamage { get; set; } = 1;

		/// <summary>
		/// (?)伤害
		/// </summary>
		public double HardBulletDamage { get; set; } = 2;

		/// <summary>
		/// (?)伤害
		/// </summary>
		public double DangerBulletDamage { get; set; } = 4;

		/// <summary>
		/// (?)伤害
		/// </summary>
		public double BeamDamage { get; set; } = 2;

		/// <summary>
		/// (?)Hold用到，貌似用来做判定
		/// </summary>
		public float ProgJudgeBpm { get; set; } = 240;
	}
}
