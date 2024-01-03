using MahApps.Metro.Controls;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OngekiFumenEditor.UI.Dialogs
{
	/// <summary>
	/// ExceptionTermWindow.xaml 的交互逻辑
	/// </summary>
	public partial class ExceptionTermWindow : MetroWindow
	{
		public string ExceptionMessage { get; init; }
		public string[] RescueFolderPaths { get; init; }
		public string LogFile { get; init; }
		public string DumpFile { get; init; }

		public string ProgramVersion => FileVersionInfo.GetVersionInfo(typeof(AppBootstrapper).Assembly.Location).ProductVersion;

		public ExceptionTermWindow(string exceptionMessage, string[] rescueFolderPaths, string logFile, string dumpFile)
		{
			ExceptionMessage = exceptionMessage;
			RescueFolderPaths = rescueFolderPaths;
			LogFile = logFile;
			DumpFile = dumpFile;

			InitializeComponent();
			DataContext = this;
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void Hyperlink_Click(object sender, RoutedEventArgs e)
		{
			if ((sender switch
			{
				FrameworkElement f => f.DataContext,
				FrameworkContentElement f => f.DataContext,
				_ => default
			}) is string path)
				ProcessUtils.OpenExplorerToBrowser(path);
		}
	}
}
