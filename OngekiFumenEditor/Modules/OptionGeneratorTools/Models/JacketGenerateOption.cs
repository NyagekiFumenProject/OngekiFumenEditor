using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models
{
    public class JacketGenerateOption : PropertyChangedBase
    {
        private int musicId;
        public int MusicId
        {
            get => musicId; set => Set(ref musicId, value);
        }

        private string outputAssetbundleFilePath;
        public string OutputAssetbundleFilePath
        {
            get => outputAssetbundleFilePath; set => Set(ref outputAssetbundleFilePath, value);
        }

        private string outputImageFilePath;
        public string OutputImageFilePath
        {
            get => outputImageFilePath; set => Set(ref outputImageFilePath, value);
        }

        private int width;
        public int Width
        {
            get => width; set => Set(ref width, value);
        }

        private int height;
        public int Height
        {
            get => height; set => Set(ref height, value);
        }

        private int widthSmall;
        public int WidthSmall
        {
            get => widthSmall; set => Set(ref widthSmall, value);
        }

        private int heightSmall;
        public int HeightSmall
        {
            get => heightSmall; set => Set(ref heightSmall, value);
        }

        private bool updateAssetBytesFile;
        public bool UpdateAssetBytesFile
        {
            get => updateAssetBytesFile; set => Set(ref updateAssetBytesFile, value);
        }
    }
}
