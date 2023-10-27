namespace OngekiFumenEditor.Kernel.Audio
{
	public partial interface ISamplePeak
	{
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
