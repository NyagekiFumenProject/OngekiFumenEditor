using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Views.UI
{
	/// <summary>
	/// Toast.xaml 的交互逻辑
	/// </summary>
	public partial class Toast : UserControl
	{
		public void ShowMessage(string message, MessageType message_type = MessageType.Notify, uint show_time = 2000) => InternalShowMessage(message, message_type, show_time);

		public enum MessageType
		{
			Error,
			Warn,
			Notify
		}

		public SolidColorBrush TextColor
		{
			get { return (SolidColorBrush)GetValue(TextColorProperty); }
			set { SetValue(TextColorProperty, value); }
		}

		public static readonly DependencyProperty TextColorProperty =
			DependencyProperty.Register("TextColor", typeof(SolidColorBrush), typeof(Toast), new PropertyMetadata(new SolidColorBrush(Colors.White)));

		public string Message
		{
			get { return (string)GetValue(MessageProperty); }
			set { SetValue(MessageProperty, value); }
		}

		public static readonly DependencyProperty MessageProperty =
			DependencyProperty.Register("Message", typeof(string), typeof(Toast), new PropertyMetadata(""));

		private Storyboard sb;
		private DoubleAnimation animation;

		private Dictionary<MessageType, SolidColorBrush> Backgrounds = new Dictionary<MessageType, SolidColorBrush>()
		{
			{MessageType.Error,new SolidColorBrush(Colors.Red) },
			{MessageType.Warn,new SolidColorBrush(Colors.LightYellow) },
			{MessageType.Notify,new SolidColorBrush(Colors.White) },
		};

		public Toast()
		{
			InitializeComponent();

			ContentPanel.DataContext = this;

			sb = Resources["ShowAction"] as Storyboard;
			animation = sb.Children.FirstOrDefault(x => x.Name == "HideAnimation") as DoubleAnimation;
		}

		private void InternalShowMessage(string message, MessageType message_type = MessageType.Notify, uint show_time = 2000)
		{
			Dispatcher.Invoke(() =>
			{
				Message = message;
				animation.BeginTime = TimeSpan.FromMilliseconds(show_time);
				TextColor = Backgrounds[message_type];
				Log.LogDebug($"{message_type} {Message} ({animation.BeginTime})");
				sb.Begin();
			});
		}
	}
}
