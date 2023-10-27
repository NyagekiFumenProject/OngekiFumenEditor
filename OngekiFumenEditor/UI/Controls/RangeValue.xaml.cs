using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace OngekiFumenEditor.UI.Controls
{
	/// <summary>
	/// RangeValue.xaml 的交互逻辑
	/// </summary>
	public partial class RangeValue : UserControl, INotifyPropertyChanged
	{
		public string DisplayName
		{
			get { return (string)GetValue(DisplayNameProperty); }
			set { SetValue(DisplayNameProperty, value); }
		}

		// Using a DependencyProperty as the backing store for DisplayName.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty DisplayNameProperty =
			DependencyProperty.Register("DisplayName", typeof(string), typeof(RangeValue), new PropertyMetadata(""));


		public double MinValue
		{
			get { return (double)GetValue(MinValueProperty); }
			set { SetValue(MinValueProperty, value); }
		}

		// Using a DependencyProperty as the backing store for MinValue.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty MinValueProperty =
			DependencyProperty.Register("MinValue", typeof(double), typeof(RangeValue), new PropertyMetadata(0d));

		public double MaxValue
		{
			get { return (double)GetValue(MaxValueProperty); }
			set { SetValue(MaxValueProperty, value); }
		}

		// Using a DependencyProperty as the backing store for MinValue.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty MaxValueProperty =
			DependencyProperty.Register("MaxValue", typeof(double), typeof(RangeValue), new PropertyMetadata(100d));

		public double CurrentValue
		{
			get { return (double)GetValue(CurrentValueProperty); }
			set { SetValue(CurrentValueProperty, value); }
		}

		// Using a DependencyProperty as the backing store for CurrentValue.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty CurrentValueProperty =
			DependencyProperty.Register("CurrentValue", typeof(double), typeof(RangeValue), new PropertyMetadata(50d, (e, q) =>
			{
				((RangeValue)e).PropertyChanged?.Invoke(e, new(nameof(CurrentIntValue)));
			}));

		public double Step
		{
			get { return (double)GetValue(StepProperty); }
			set { SetValue(StepProperty, value); }
		}

		// Using a DependencyProperty as the backing store for Step.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty StepProperty =
			DependencyProperty.Register("Step", typeof(double), typeof(RangeValue), new PropertyMetadata(1d));

		public event PropertyChangedEventHandler PropertyChanged;

		public event DragCompletedEventHandler ThumbDragCompleted
		{
			add => mySlider.ThumbDragCompleted += value;
			remove => mySlider.ThumbDragCompleted -= value;
		}

		public int CurrentIntValue
		{
			get => (int)CurrentValue;
			set => CurrentValue = value;
		}

		public RangeValue()
		{
			InitializeComponent();

			shitBinding.DataContext = this;
		}
	}
}
