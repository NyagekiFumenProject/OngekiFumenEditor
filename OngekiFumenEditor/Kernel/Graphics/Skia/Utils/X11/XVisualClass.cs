using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Utils.X11
{
	internal enum XVisualClass : int {
		StaticGray = 0,
		GrayScale = 1,
		StaticColor = 2,
		PseudoColor = 3,
		TrueColor = 4,
		DirectColor = 5
	}
}
