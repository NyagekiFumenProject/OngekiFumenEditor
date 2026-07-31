namespace OngekiFumenEditor.Avalonia.Base
{
	public class RangeValue : ObservableObject
	{
		private float minValue;
		private float maxValue;

		public float MaxValue
		{
			get => IsLimitInt ? (int)maxValue : maxValue;
			set
			{
				SetProperty(ref maxValue, value);
				CurrentValue = CurrentValue;
			}
		}

		public float MinValue
		{
			get => IsLimitInt ? (int)minValue : minValue;
			set
			{
				SetProperty(ref minValue, value);
				CurrentValue = CurrentValue;
			}
		}

		private bool isLimitInt = false;
		public bool IsLimitInt
		{
			get => isLimitInt;
			set
			{
				SetProperty(ref isLimitInt, value);
				OnPropertyChanged(nameof(MinValue));
				OnPropertyChanged(nameof(MaxValue));
				OnPropertyChanged(nameof(CurrentValue));
				OnPropertyChanged(nameof(ValuePercent));
			}
		}

		private float currentValue;
		public float CurrentValue
		{
			get => IsLimitInt ? (int)currentValue : currentValue;
			set
			{
				SetProperty(ref currentValue, Math.Min(MaxValue, Math.Max(MinValue, value)));
				OnPropertyChanged(() => ValuePercent);
			}
		}

		public float ValuePercent => (CurrentValue - MinValue) / (MaxValue - MinValue);

		public override string ToString() => $"Min:{MinValue:F2} Max:{MaxValue:F2} Value:{CurrentValue:F2}({ValuePercent * 100:F2}%) IsLimitInt:{IsLimitInt}";

		public static RangeValue Create(int min, int max, int initVal = 0) => new RangeValue()
		{
			MaxValue = max,
			MinValue = min,
			CurrentValue = initVal,
			IsLimitInt = true
		};

		public static RangeValue Create(float min, float max, float initVal = 0) => new RangeValue()
		{
			MaxValue = max,
			MinValue = min,
			CurrentValue = initVal,
			IsLimitInt = false
		};

		public static RangeValue CreateNormalized(float initVal = 0) => new RangeValue()
		{
			MaxValue = 1,
			MinValue = 0,
			CurrentValue = initVal,
			IsLimitInt = false
		};
	}
}

