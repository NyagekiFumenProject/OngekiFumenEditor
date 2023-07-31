using Caliburn.Micro;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Runtime.InteropServices;
using System.IO;
using System.Buffers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;

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

                var imgData = await JacketGenerateWrapper.GetMainImageDataAsync(imgFilePath);

                AsyncValue = await Task.Run(() =>
                {
                    using var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(imgData.Data, imgData.Width, imgData.Height);
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
