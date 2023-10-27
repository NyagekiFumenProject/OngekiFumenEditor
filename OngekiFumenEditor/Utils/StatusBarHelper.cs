using Caliburn.Micro;
using Gemini.Modules.StatusBar.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Utils
{
	public static class StatusBarHelper
	{
		public class Notify : IDisposable
		{
			public Notify(string statusDescription)
			{
				StatusDescription = statusDescription;
			}

			public string StatusDescription { get; }

			public void Dispose()
			{
				EndStatus(this);
			}
		}

		private static List<Notify> currentStatusList = new List<Notify>();

		private static void UpdateStatusToStatusBar()
		{
			var firstStatus = currentStatusList?.FirstOrDefault();

			var descStr = firstStatus?.StatusDescription;
			if (IoC.Get<CommonStatusBar>().MainContentViewModel is StatusBarItemViewModel viewModel)
			{
				viewModel.Message = descStr ?? "";
			}
		}

		public static Notify BeginStatus(string statusDescription)
		{
			var notify = new Notify(statusDescription);
			currentStatusList.Add(notify);
			UpdateStatusToStatusBar();
			return notify;
		}

		public static void EndStatus(Notify notify)
		{
			if (currentStatusList.Remove(notify))
			{
				UpdateStatusToStatusBar();
			}
		}
	}
}
