namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;

// The owning monitor serializes writers, snapshots and Clear under its sample lock.
internal sealed class PerformanceSampleWindow(int capacity)
{
    private readonly long[] values = new long[capacity];
    private int index;
    private int count;
    private long sum;

    public int Count => count;
    public double Average => count == 0 ? 0 : (double)sum / count;
    public long Current => count == 0 ? 0 : values[(index + values.Length - 1) % values.Length];
    public long Max
    {
        get
        {
            long max = 0;
            for (var i = 0; i < count; i++)
                max = Math.Max(max, values[i]);
            return max;
        }
    }

    public void Enqueue(long value)
    {
        if (count == values.Length)
            sum -= values[index];
        else
            count++;
        values[index] = value;
        sum += value;
        index = (index + 1) % values.Length;
    }

    public void Clear()
    {
        index = 0;
        count = 0;
        sum = 0;
    }
}
