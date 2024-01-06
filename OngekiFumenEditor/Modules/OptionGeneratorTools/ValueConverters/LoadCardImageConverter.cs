using Caliburn.Micro;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.ValueConverters
{
	class LoadCardImageConverter : IValueConverter
	{
		private class AsyncLoadImageTask : PropertyChangedBase
		{
			public AsyncLoadImageTask(string img)
			{
				LoadValue(img);
			}

			private async void LoadValue(string imgFilePath)
			{
				if (!File.Exists(imgFilePath))
					return;

				var imgData = await JacketGenerateWrapper.GetMainImageDataAsync(default, imgFilePath);

				AsyncValue = await Task.Run(() =>
				{
					using var image = Image.LoadPixelData<Rgba32>(imgData.Data, imgData.Width, imgData.Height);
					var memoryStream = new MemoryStream();
					image.Mutate(i => i.Flip(FlipMode.Vertical));
					image.SaveAsPng(memoryStream);
					memoryStream.Seek(0, SeekOrigin.Begin);

					var bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.StreamSource = memoryStream;
					bitmapImage.EndInit();

					bitmapImage.Freeze();

					return bitmapImage;
				});
				NotifyOfPropertyChange(() => AsyncValue);
			}

			public BitmapImage AsyncValue { get; private set; }
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return new AsyncLoadImageTask(value?.ToString());
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
