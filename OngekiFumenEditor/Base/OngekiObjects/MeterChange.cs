namespace OngekiFumenEditor.Base.OngekiObjects
{
	public class MeterChange : OngekiTimelineObjectBase
	{

		private int bunShi = 4;
		//分子(numerator)
		public int BunShi
		{
			get { return bunShi; }
			set
			{
				bunShi = value;
				NotifyOfPropertyChange(() => BunShi);
			}
		}

		private int bunbo = 4;
		//分母(denominator)
		public int Bunbo
		{
			get { return bunbo; }
			set
			{
				bunbo = value;
				NotifyOfPropertyChange(() => Bunbo);
			}
		}

		public static string CommandName => "MET";
		public override string IDShortName => CommandName;

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not MeterChange from)
				return;

			Bunbo = from.Bunbo;
			BunShi = from.BunShi;
		}

		public override string ToString() => $"{base.ToString()} Bunshi/Bunbo[{BunShi}/{Bunbo}]";
	}
}
