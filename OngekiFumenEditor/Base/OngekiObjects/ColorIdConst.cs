using System.Collections.Generic;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.OngekiObjects
{
	public static class ColorIdConst
	{
		public static ColorId Akari { get; } = new ColorId()
		{
			Id = 0,
			Name = nameof(Akari),
			Color = Color.FromArgb(255, 255, 153, 158)
		};

		public static ColorId Yuzu { get; } = new ColorId()
		{
			Id = 1,
			Name = nameof(Yuzu),
			Color = Color.FromArgb(255, 255, 234, 115)
		};

		public static ColorId Rio { get; } = new ColorId()
		{
			Id = 2,
			Name = nameof(Rio),
			Color = Color.FromArgb(255, 141, 92, 224)
		};

		public static ColorId Riku { get; } = new ColorId()
		{
			Id = 3,
			Name = nameof(Riku),
			Color = Color.FromArgb(255, 255, 113, 217)
		};

		public static ColorId Tsubaki { get; } = new ColorId()
		{
			Id = 4,
			Name = nameof(Tsubaki),
			Color = Color.FromArgb(255, 80, 191, 163)
		};

		public static ColorId Ayaka { get; } = new ColorId()
		{
			Id = 5,
			Name = nameof(Ayaka),
			Color = Color.FromArgb(255, 209, 105, 237)
		};

		public static ColorId Kaede { get; } = new ColorId()
		{
			Id = 6,
			Name = nameof(Kaede),
			Color = Color.FromArgb(255, 72, 72, 120)
		};

		public static ColorId Saki { get; } = new ColorId()
		{
			Id = 7,
			Name = nameof(Saki),
			Color = Color.FromArgb(255, 206, 209, 217)
		};

		public static ColorId Koboshi { get; } = new ColorId()
		{
			Id = 8,
			Name = nameof(Koboshi),
			Color = Color.FromArgb(255, 148, 244, 83)
		};

		public static ColorId Alice { get; } = new ColorId()
		{
			Id = 9,
			Name = nameof(Alice),
			Color = Color.FromArgb(255, 186, 244, 254)
		};

		public static ColorId Mia { get; } = new ColorId()
		{
			Id = 10,
			Name = nameof(Mia),
			Color = Color.FromArgb(255, 254, 186, 212)
		};

		public static ColorId Chinatsu { get; } = new ColorId()
		{
			Id = 11,
			Name = nameof(Chinatsu),
			Color = Color.FromArgb(255, 255, 212, 39)
		};

		public static ColorId Tsumugi { get; } = new ColorId()
		{
			Id = 12,
			Name = nameof(Tsumugi),
			Color = Color.FromArgb(255, 79, 155, 171)
		};

		public static ColorId Setsuna { get; } = new ColorId()
		{
			Id = 13,
			Name = nameof(Setsuna),
			Color = Color.FromArgb(255, 96, 74, 163)
		};

		public static ColorId Brown { get; } = new ColorId()
		{
			Id = 14,
			Name = nameof(Brown),
			Color = Color.FromArgb(255, 165, 42, 42)
		};

		public static ColorId Haruna { get; } = new ColorId()
		{
			Id = 15,
			Name = nameof(Haruna),
			Color = Color.FromArgb(255, 254, 242, 244)
		};

		public static ColorId Black { get; } = new ColorId()
		{
			Id = 16,
			Name = nameof(Black),
			Color = Color.FromArgb(255, 0, 0, 0)
		};

		public static ColorId Akane { get; } = new ColorId()
		{
			Id = 17,
			Name = nameof(Akane),
			Color = Color.FromArgb(255, 204, 0, 0)
		};

		public static ColorId LaneG { get; } = new ColorId()
		{
			Id = 18,
			Name = nameof(LaneG),
			Color = Color.FromArgb(255, 0, 255, 0)
		};

		public static ColorId Aoi { get; } = new ColorId()
		{
			Id = 19,
			Name = nameof(Aoi),
			Color = Color.FromArgb(255, 71, 145, 255)
		};

		public static ColorId LaneRed { get; } = new ColorId()
		{
			Id = 1020,
			Name = nameof(LaneRed),
			Color = Color.FromArgb(255, 255, 0, 0)
		};

		public static ColorId LaneGreen { get; } = new ColorId()
		{
			Id = 1021,
			Name = nameof(LaneGreen),
			Color = Color.FromArgb(255, 0, 255, 0)
		};

		public static ColorId LaneBlue { get; } = new ColorId()
		{
			Id = 1022,
			Name = nameof(LaneBlue),
			Color = Color.FromArgb(255, 0, 0, 255)
		};


		public static IEnumerable<ColorId> AllColors { get; } = new[]
		{
			Akari,
			Yuzu,
			Rio,
			Riku,
			Tsubaki,
			Ayaka,
			Kaede,
			Saki,
			Koboshi,
			Alice,
			Mia,
			Chinatsu,
			Tsumugi,
			Setsuna,
			Brown,
			Haruna,
			Black,
			Akane,
			Aoi,
			LaneRed,
			LaneGreen,LaneG,
			LaneBlue,
		};
	}
}
