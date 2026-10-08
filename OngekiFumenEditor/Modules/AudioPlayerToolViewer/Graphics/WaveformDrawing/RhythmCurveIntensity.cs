namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing
{
    /// <summary>
    /// 节奏曲线的显示强度档位（对象检查器里以下拉框选择）。
    /// <para>数值会写进用户设置，<b>不要改动既有编号</b>，新增档位往后加。</para>
    /// </summary>
    public enum RhythmCurveIntensity
    {
        /// <summary>不做色调映射：曲线的原始观感（色调映射上线前的样子）。</summary>
        Default = 0,

        /// <summary>默认档：轻度强调 + γ 压缩，峰谷对比约翻倍。</summary>
        Enhanced = 1,

        /// <summary>更强一档：强调与 γ 都再上一档，曲线更"只有峰"。</summary>
        Strong = 2,
    }
}
