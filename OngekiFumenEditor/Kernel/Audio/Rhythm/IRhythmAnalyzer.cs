using System.Threading;

namespace OngekiFumenEditor.Kernel.Audio.Rhythm
{
    /// <summary>
    /// 对整个音频做一次「看得见节奏」的频谱分析：短时傅里叶 → 对数幅度 → 滞后频谱邻域差分 → 分频段谱通量 →
    /// 窄高斯平滑与局部自适应归一化 → 一条 0–1 的节奏强度曲线。
    /// </summary>
    public interface IRhythmAnalyzer
    {
        /// <summary>
        /// 数据格式不支持（非 32bit 浮点）或长度不足时返回 null；取消时抛出 OperationCanceledException。
        /// 只在后台线程调用，不做 UI 交互。
        /// </summary>
        RhythmEnvelope Analyze(SampleData data, CancellationToken cancellationToken = default);
    }
}
