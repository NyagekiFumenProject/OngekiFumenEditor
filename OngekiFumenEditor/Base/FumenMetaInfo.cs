using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public class FumenMetaInfo : ISerializable
    {
        public class BpmDef
        {
            public double First { get; set; } = 240;
            public double Common { get; set; } = 240;
            public double Minimum { get; set; } = 240;
            public double Maximum { get; set; } = 240;
        }

        public class MetDef
        {
            public int Bunbo { get; set; } = 4;
            public int Bunshi { get; set; } = 4;
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
        public BpmDef BpmDefinition { get; set; } = new BpmDef();
        public BPMChange FirstBpm { get; set; } = new BPMChange();

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

        public string Serialize(OngekiFumen fumenData)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"VERSION {Version.Major} {Version.Minor} {Version.Build}");
            sb.AppendLine($"CREATOR {Creator}");
            sb.AppendLine($"BPM_DEF {BpmDefinition.First} {BpmDefinition.Common} {BpmDefinition.Maximum} {BpmDefinition.Minimum}");
            sb.AppendLine($"MET_DEF {MeterDefinition.Bunshi} {MeterDefinition.Bunbo}");
            sb.AppendLine($"TRESOLUTION {TRESOLUTION}");
            sb.AppendLine($"XRESOLUTION {XRESOLUTION}");
            sb.AppendLine($"CLK_DEF {ClickDefinition}");
            sb.AppendLine($"PROGJUDGE_BPM {ProgJudgeBpm}");
            sb.AppendLine($"TUTORIAL {(Tutorial?1:0)}");
            sb.AppendLine($"BULLET_DAMAGE {BulletDamage:F3}");
            sb.AppendLine($"HARDBULLET_DAMAGE {HardBulletDamage:F3}");
            sb.AppendLine($"DANGERBULLET_DAMAGE {DangerBulletDamage:F3}");
            sb.AppendLine($"BEAM_DAMAGE {BeamDamage:F3}");

            return sb.ToString();
        }
    }
}
