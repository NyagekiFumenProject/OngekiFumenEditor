namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;

internal readonly record struct WaveformViewport(
    float Width,
    float Height,
    TimeSpan FromTime,
    TimeSpan ToTime,
    double DurationMilliseconds,
    float CurrentTimeX)
{
    public float ProjectX(TimeSpan time)
    {
        return (float)(Width * ((time - FromTime).TotalMilliseconds / DurationMilliseconds) - Width / 2);
    }
}

internal static class WaveformGeometry
{
    public static bool TryCreateViewport(
        float width,
        float height,
        TimeSpan currentTime,
        float currentTimeXOffset,
        float durationMsPerPixel,
        out WaveformViewport viewport)
    {
        viewport = default;
        if (!float.IsFinite(width) || width <= 0
            || !float.IsFinite(height) || height <= 0
            || !float.IsFinite(durationMsPerPixel) || durationMsPerPixel <= 0)
        {
            return false;
        }

        var normalizedOffset = float.IsFinite(currentTimeXOffset)
            ? Math.Clamp(currentTimeXOffset, 0, width)
            : 0;
        var durationMilliseconds = width * (double)durationMsPerPixel;
        var offsetMilliseconds = normalizedOffset * (double)durationMsPerPixel;

        if (!double.IsFinite(durationMilliseconds) || durationMilliseconds <= 0
            || !double.IsFinite(offsetMilliseconds))
        {
            return false;
        }

        try
        {
            var fromTime = currentTime - TimeSpan.FromMilliseconds(offsetMilliseconds);
            var toTime = fromTime + TimeSpan.FromMilliseconds(durationMilliseconds);
            viewport = new(
                width,
                height,
                fromTime,
                toTime,
                durationMilliseconds,
                normalizedOffset - width / 2);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    public static bool TryGetVerticalExtents(
        float[] amplitudes,
        float height,
        float verticalScale,
        out float top,
        out float bottom)
    {
        top = 0;
        bottom = 0;
        if (amplitudes is null || amplitudes.Length == 0
            || !float.IsFinite(height) || height <= 0)
        {
            return false;
        }

        var scale = float.IsFinite(verticalScale) ? Math.Max(0, verticalScale) : 0;
        var left = NormalizeAmplitude(amplitudes[0], scale);
        var right = amplitudes.Length > 1
            ? NormalizeAmplitude(amplitudes[1], scale)
            : left;
        var halfHeight = height / 2;

        top = halfHeight * left;
        bottom = -halfHeight * right;
        return true;
    }

    private static float NormalizeAmplitude(float amplitude, float scale)
    {
        if (!float.IsFinite(amplitude))
            return 0;

        return Math.Clamp(Math.Abs(amplitude) * scale, 0, 1);
    }
}
