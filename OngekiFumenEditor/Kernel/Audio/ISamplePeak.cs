using OngekiFumenEditor.Base.Collections.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio
{
    public interface ISamplePeak
    {
        public class PeakPointCollection : SortableCollection<PeakPoint, TimeSpan>
        {
            public PeakPointCollection() : base(x => x.Time)
            {

            }
        }

        public record PeakPoint(TimeSpan Time, float[] Amplitudes) : IComparable<PeakPoint>
        {
            public int CompareTo(PeakPoint other)
            {
                return this.Time.CompareTo(other.Time);
            }

            public override string ToString()
                => $"{Time} [{string.Join(", ", Amplitudes)}]";
        }

        /// <summary>
        /// 获取波峰数据
        /// </summary>
        /// <param name="data">采样数据</param>
        /// <param name="startTime">需要计算采样的开始时间</param>
        /// <param name="endTime">需要计算采样的结束时间</param>
        /// <returns>x归一</returns>
        PeakPointCollection GetPeakValues(SampleData data);
    }
}
