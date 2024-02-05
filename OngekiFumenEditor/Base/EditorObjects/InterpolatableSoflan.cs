using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects
{
	public class InterpolatableSoflan : Soflan
	{
		public override string IDShortName => "[INTP_SFL]";

		public class InterpolatableSoflanIndicator : SoflanEndIndicator
		{
			private float speed = 1;
			public float Speed
			{
				get => speed;
				set => Set(ref speed, value);
			}

			public override string IDShortName => "[INTP_SFL_End]";

			public override void Copy(OngekiObjectBase from)
			{
				base.Copy(from);

				if (from is not InterpolatableSoflanIndicator f)
					return;
				Speed = f.Speed;
			}
		}

		public InterpolatableSoflan() : base()
		{
			EndIndicator = new InterpolatableSoflanIndicator() { RefSoflan = this };
			EndIndicator.PropertyChanged += EndIndicator_PropertyChanged;
			displayables = new IDisplayableObject[] { this, EndIndicator };
		}

		private void EndIndicator_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(Speed):
					NotifyOfPropertyChange(nameof(Speed));
					break;
				case nameof(TGrid):
					NotifyOfPropertyChange(nameof(EndTGrid));
					break;
				default:
					NotifyOfPropertyChange(nameof(EndIndicator));
					break;
			}
		}

		public override void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
		{
			switch (propertyName)
			{
				case nameof(Speed):
				case nameof(TGrid):
				case nameof(InterpolateCountPerResT):
				case nameof(EndTGrid):
				case nameof(ApplySpeedInDesignMode):
				case nameof(Easing):
					cachedValid = false;
					break;
				default:
					break;
			}
			base.NotifyOfPropertyChange(propertyName);
		}

		private EasingTypes easing = EasingTypes.None;
		public EasingTypes Easing
		{
			get => easing;
			set => Set(ref easing, value);
		}

		private int interpolateCountPerResT = 16;
		public int InterpolateCountPerResT
		{
			get => interpolateCountPerResT;
			set => Set(ref interpolateCountPerResT, value);
		}

		public override string ToString() => $"{base.ToString()} --> EndSpeed[{((InterpolatableSoflanIndicator)EndIndicator)?.Speed}x]";

        public override void Copy(OngekiObjectBase fromObj)
        {
            base.Copy(fromObj);

            if (fromObj is not InterpolatableSoflan soflan)
                return;

            Speed = soflan.Speed;
            ApplySpeedInDesignMode = soflan.ApplySpeedInDesignMode;
            Easing = soflan.Easing;
        }

		bool cachedValid = false;
		List<IKeyframeSoflan> cachedInterpolatedSoflans = new();

		public void UpdateCachedInterpolatedSoflans()
		{
			cachedInterpolatedSoflans.Clear();

			var fromTotalGrid = TGrid.TotalGrid;
			var toTotalGrid = EndTGrid.TotalGrid;

			var fromSpeed = Speed;
			var toSpeed = (EndIndicator as InterpolatableSoflanIndicator).Speed;

			if (fromSpeed == toSpeed || fromTotalGrid == toTotalGrid)
			{
				cachedInterpolatedSoflans.Add(new KeyframeSoflan()
				{
					TGrid = new TGrid(0, toTotalGrid),
					Speed = toSpeed,
					ApplySpeedInDesignMode = ApplySpeedInDesignMode
				});
			}
			else
			{
				var stepGridLength = (int)(TGrid.DEFAULT_RES_T / InterpolateCountPerResT);

				for (var curGrid = fromTotalGrid; curGrid < toTotalGrid; curGrid += stepGridLength)
				{
					var nextGrid = Math.Min(curGrid + stepGridLength, toTotalGrid);

					var normalized = nextGrid == toTotalGrid ? 1 : (curGrid - fromTotalGrid) * 1.0d / (toTotalGrid - fromTotalGrid);
					var transformed = (float)Interpolation.ApplyEasing(Easing, normalized);

					var speed = fromSpeed + transformed * (toSpeed - fromSpeed);

					cachedInterpolatedSoflans.Add(new KeyframeSoflan()
					{
						TGrid = new TGrid(0, curGrid),
						Speed = speed,
						ApplySpeedInDesignMode = ApplySpeedInDesignMode
					});
				}
			}
			cachedValid = true;
		}

		public override IEnumerable<IKeyframeSoflan> GenerateKeyframeSoflans()
		{
			if (!cachedValid)
				UpdateCachedInterpolatedSoflans();
			return cachedInterpolatedSoflans;
		}

		public override float CalculateSpeed(TGrid t)
		{
			var list = GenerateKeyframeSoflans();
			var r = ((IList<IKeyframeSoflan>)list).LastOrDefaultByBinarySearch(t, x => x.TGrid);
			return r?.Speed ?? 1;
		}
	}
}
