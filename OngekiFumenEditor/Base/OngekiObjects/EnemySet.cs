namespace OngekiFumenEditor.Base.OngekiObjects
{
	public class EnemySet : OngekiTimelineObjectBase
	{

		public enum WaveChangeConst
		{
			Wave1 = 0,
			Wave2 = 1,
			Boss = 2,
		}

		private WaveChangeConst tagTblValue = WaveChangeConst.Boss;
		public WaveChangeConst TagTblValue
		{
			get { return tagTblValue; }
			set
			{
				tagTblValue = value;
				NotifyOfPropertyChange(() => TagTblValue);
			}
		}

		public static string CommandName => "EST";
		public override string IDShortName => CommandName;

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not EnemySet fromSet)
				return;

			TagTblValue = fromSet.TagTblValue;
		}
	}
}
