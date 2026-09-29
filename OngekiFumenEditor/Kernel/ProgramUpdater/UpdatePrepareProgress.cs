namespace OngekiFumenEditor.Kernel.ProgramUpdater
{
    public enum UpdatePrepareStep
    {
        Downloading,
        Extracting,
    }

    /// <param name="Progress">0~1；总量未知时为 -1。</param>
    /// <param name="BytesPerSecond">下载速度（字节/秒）；不适用时为 0。</param>
    public readonly record struct UpdatePrepareProgress(UpdatePrepareStep Step, double Progress, double BytesPerSecond = 0);
}
