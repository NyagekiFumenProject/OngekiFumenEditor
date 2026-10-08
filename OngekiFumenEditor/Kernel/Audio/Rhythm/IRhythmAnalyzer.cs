using System.Threading;

namespace OngekiFumenEditor.Kernel.Audio.Rhythm
{
    /// <summary>
    /// 对整个音频做一次「看得见节奏」的频谱分析：短时傅里叶 → 对数幅度 → 逐 bin 背景抑制 → 分频段谱通量 →
    /// 局部自适应归一化 → 一条 0–1 的节奏强度曲线。
    /// </summary>
    public interface IRhythmAnalyzer
    {
        /// <summary>
        /// 分析整段采样数据。数据格式不支持（非 32bit 浮点）、长度不足或取消时返回 null。
        /// 只在后台线程调用，不做 UI 交互。
        /// </summary>
        RhythmEnvelope Analyze(SampleData data, CancellationToken cancellationToken = default);
    }
}
