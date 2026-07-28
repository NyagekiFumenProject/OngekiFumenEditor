using OngekiFumenEditor.Avalonia.Base.Collections.Base;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public class PeakPointCollection : SortableCollection<PeakPoint, TimeSpan>
{
    private readonly SampleInfo sampleInfo;

    public PeakPointCollection(SampleInfo sampleInfo) : base(x => x.Time)
    {
        this.sampleInfo = sampleInfo;
    }

    public static double EvalGaussian(float x, float sigma)
    {
        const double invSqrt2Pi = 0.39894;
        return invSqrt2Pi * Math.Exp(-0.5 * x * x / (sigma * sigma)) / sigma;
    }

    public Task<PeakPointCollection> GenerateSimplfiedAsync(int pointsPerGeneratedPoint, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var kernelWidth = pointsPerGeneratedPoint * 3 + 1;
            var filter = new float[kernelWidth + 1];

            for (var i = 0; i < filter.Length; ++i)
                filter[i] = (float)EvalGaussian(i, pointsPerGeneratedPoint);

            var originalPointIndex = 0f;
            var generatedPointIndex = 0;

            var newCollection = new PeakPointCollection(sampleInfo);
            var channels = sampleInfo.Channels;

            newCollection.BeginBatchAction();
            while (originalPointIndex < Count)
            {
                if (cancellationToken.IsCancellationRequested)
                    return default(PeakPointCollection);

                var startIndex = (int)originalPointIndex - kernelWidth;
                var endIndex = (int)originalPointIndex + kernelWidth;
                var origPeakPoint = this[(int)originalPointIndex];
                var point = new PeakPoint(origPeakPoint.Time, new float[channels]);
                var totalWeight = 0f;

                for (var j = startIndex; j < endIndex; j++)
                {
                    if (j < 0 || j >= Count)
                        continue;

                    var weight = filter[Math.Abs(j - startIndex - kernelWidth)];
                    totalWeight += weight;
                    for (var c = 0; c < channels; c++)
                        point.Amplitudes[c] += weight * this[j].Amplitudes[c];
                }

                if (totalWeight > 0)
                {
                    for (var c = 0; c < channels; c++)
                        point.Amplitudes[c] /= totalWeight;
                }

                newCollection.Add(point);
                generatedPointIndex += 1;
                originalPointIndex = generatedPointIndex * pointsPerGeneratedPoint;
            }

            newCollection.EndBatchAction();
            return newCollection;
        }, cancellationToken);
    }
}

