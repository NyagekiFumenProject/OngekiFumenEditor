using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Caliburn.Micro;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ToolboxItems
{
    public abstract class ElementViewModel : PropertyChangedBase
    {
        private double x;

        public double X
        {
            get { return x; }
            set
            {
                x = value;
                NotifyOfPropertyChange(() => X);
            }
        }

        private double y;

        public double Y
        {
            get { return y; }
            set
            {
                y = value;
                NotifyOfPropertyChange(() => Y);
            }
        }

        private string name;

        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                NotifyOfPropertyChange(() => Name);
            }
        }

        private bool isSelected;

        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                isSelected = value;
                NotifyOfPropertyChange(() => IsSelected);
            }
        }

        public virtual BitmapSource PreviewImage { get; set; }
    }
}