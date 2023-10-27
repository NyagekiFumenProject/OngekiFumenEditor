using System;

namespace OngekiFumenEditor.Kernel.Audio
{
	public record PeakPoint(TimeSpan Time, float[] Amplitudes) : IComparable<PeakPoint>
	{
		public int CompareTo(PeakPoint other)
		{
			return this.Time.CompareTo(other.Time);
		}

		public override string ToString()
			=> $"{Time} [{string.Join(", ", Amplitudes)}]";
	}
}
