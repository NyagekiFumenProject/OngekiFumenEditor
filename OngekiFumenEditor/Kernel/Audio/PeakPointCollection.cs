using OngekiFumenEditor.Base.Collections.Base;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio
{
	public class PeakPointCollection : SortableCollection<PeakPoint, TimeSpan>
	{
		private readonly SampleInfo sampleInfo;

		public PeakPointCollection(SampleInfo sampleInfo) : base(x => x.Time)
		{
			this.sampleInfo = sampleInfo;
		}

		public static double EvalGaussian(float x, float sigma)
		{
			const double inv_sqrt_2_pi = 0.39894;
			return inv_sqrt_2_pi * Math.Exp(-0.5 * x * x / (sigma * sigma)) / sigma;
		}

		public Task<PeakPointCollection> GenerateSimplfiedAsync(int pointsPerGeneratedPoint, CancellationToken cancellationToken)
		{
			return Task.Run(() =>
			{
				var kernelWidth = pointsPerGeneratedPoint * 3 + 1;
				float[] filter = new float[kernelWidth + 1];

				for (int i = 0; i < filter.Length; ++i)
					filter[i] = (float)EvalGaussian(i, pointsPerGeneratedPoint);

				var originalPointIndex = 0f;
				var generatedPointIndex = 0;

				var newCollection = new PeakPointCollection(sampleInfo);
				var channels = sampleInfo.Channels;

				newCollection.BeginBatchAction();
				{
					while (originalPointIndex < Count)
					{
						if (cancellationToken.IsCancellationRequested)
							return null;

						int startIndex = (int)originalPointIndex - kernelWidth;
						int endIndex = (int)originalPointIndex + kernelWidth;

						var origPeakPoint = this[(int)originalPointIndex];

						var point = new PeakPoint(origPeakPoint.Time, new float[channels]);
						var totalWeight = 0f;

						for (int j = startIndex; j < endIndex; j++)
						{
							if (j < 0 || j >= Count)
								continue;

							float weight = filter[Math.Abs(j - startIndex - kernelWidth)];
							totalWeight += weight;

							for (int c = 0; c < channels; c++)
								point.Amplitudes[c] += weight * this[j].Amplitudes[c];
						}

						if (totalWeight > 0)
						{
							// Means
							for (int c = 0; c < channels; c++)
								point.Amplitudes[c] /= totalWeight;
						}

						newCollection.Add(point);

						generatedPointIndex += 1;
						originalPointIndex = generatedPointIndex * pointsPerGeneratedPoint;
					}
				}
				newCollection.EndBatchAction();

				return newCollection;
			}, cancellationToken);
		}
	}
}
