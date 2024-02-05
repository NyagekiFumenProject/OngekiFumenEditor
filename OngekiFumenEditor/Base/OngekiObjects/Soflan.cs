using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects
{
	public class Soflan : OngekiTimelineObjectBase, IDurationSoflan
	{
		public class SoflanEndIndicator : OngekiTimelineObjectBase
		{
			public SoflanEndIndicator()
			{
				TGrid = null;
			}

			public override string IDShortName => "[SFL_End]";

			public Soflan RefSoflan { get; internal protected set; }

			public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => IDisplayableObject.EmptyDisplayable;

			public override TGrid TGrid
			{
				get => base.TGrid is null ? RefSoflan.TGrid.CopyNew() : base.TGrid;
				set => base.TGrid = value is not null ? MathUtils.Max(value, RefSoflan.TGrid.CopyNew()) : value;
			}

			public override string ToString() => $"{base.ToString()}";
		}

		protected IDisplayableObject[] displayables;

		public Soflan()
		{
			EndIndicator = new SoflanEndIndicator() { RefSoflan = this };
			EndIndicator.PropertyChanged += EndIndicator_PropertyChanged;
			displayables = new IDisplayableObject[] { this, EndIndicator };
		}

		public override TGrid TGrid
		{
			get => base.TGrid;
			set
			{
				base.TGrid = value;
				if (value is not null)
					EndIndicator.TGrid = MathUtils.Max(value.CopyNew(), EndIndicator.TGrid);
			}
		}

		private void EndIndicator_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(TGrid):
					NotifyOfPropertyChange(nameof(EndTGrid));
					break;
				default:
					NotifyOfPropertyChange(nameof(EndIndicator));
					break;
			}
		}

		public override string IDShortName => $"SFL";

		public SoflanEndIndicator EndIndicator { get; protected set; }

		public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => displayables;

		private float speed = 1;
		public float Speed
		{
			get => speed;
			set => Set(ref speed, value);
		}

		private bool applySpeedInDesignMode = false;
		public bool ApplySpeedInDesignMode
		{
			get => applySpeedInDesignMode;
			set => Set(ref applySpeedInDesignMode, value);
		}

		public float SpeedInEditor => ApplySpeedInDesignMode ? speed : 1;

		public TGrid EndTGrid
		{
			get => EndIndicator.TGrid;
			set => EndIndicator.TGrid = value;
		}

		public int GridLength => EndIndicator.TGrid.TotalGrid - TGrid.TotalGrid;

		public override string ToString() => $"{base.ToString()} Speed[{speed}x]";

		public override bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
		{
			if (maxVisibleTGrid < TGrid)
				return false;

			if (EndIndicator.TGrid < minVisibleTGrid)
				return false;

			return true;
		}

        public override void Copy(OngekiObjectBase fromObj)
        {
            base.Copy(fromObj);

			if (fromObj is not Soflan soflan)
				return;

            Speed = soflan.Speed;
            ApplySpeedInDesignMode = soflan.ApplySpeedInDesignMode;
        }

        public virtual void CopyEntire(Soflan from)
		{
			Copy(from);

			EndIndicator.Copy(from.EndIndicator);
		}

		public virtual IEnumerable<IKeyframeSoflan> GenerateKeyframeSoflans()
		{
			yield return new KeyframeSoflan()
			{
				TGrid = TGrid,
				Speed = Speed,
				ApplySpeedInDesignMode = ApplySpeedInDesignMode
			};
			yield return new KeyframeSoflan()
			{
				TGrid = EndTGrid,
				Speed = 1,
				ApplySpeedInDesignMode = ApplySpeedInDesignMode
			};
		}

		public virtual float CalculateSpeed(TGrid tGrid)
		{
			if (TGrid <= tGrid && tGrid <= EndTGrid)
				return Speed;
			return 1;
		}
	}
}
