using Caliburn.Micro;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OngekiFumenEditor.UI.ValueConverters
{
	public class AsyncImageLoadConverter : IValueConverter
	{
		private class Wrapper : PropertyChangedBase
		{
			private static ImageSource defaultImage;

			static Wrapper()
			{
				var image = new BitmapImage();
				image.BeginInit();
				image.StreamSource = new MemoryStream(System.Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII="));
				image.EndInit();
				image.Freeze();

				defaultImage = image;
			}

			private ImageSource bitmap = defaultImage;
			public ImageSource Bitmap
			{
				get => bitmap;
				set => Set(ref bitmap, value);
			}
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is not string path)
				return default;

			var obj = new Wrapper();
			GenerateRequestImageTask(path, obj);
			return obj;
		}

		private async void GenerateRequestImageTask(string path, Wrapper obj)
		{
			if (Application.Current?.MainWindow is null || DesignerProperties.GetIsInDesignMode(Application.Current.MainWindow))
				return;

			var data = await LoadImage(path);
			if ((data?.Length ?? 0) == 0)
				return;

			var imageSource = new BitmapImage();
			using (MemoryStream stream = new MemoryStream(data))
			{
				imageSource.BeginInit();
				imageSource.CacheOption = BitmapCacheOption.OnLoad;
				imageSource.StreamSource = stream;
				imageSource.EndInit();

				imageSource.Freeze();
			}
			obj.Bitmap = imageSource;
		}

		protected async virtual Task<byte[]> LoadImage(string path)
		{
			return await IoC.Get<ImageLoader>().LoadImage(path, CancellationToken.None);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
