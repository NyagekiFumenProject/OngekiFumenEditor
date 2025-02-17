using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Kernel.CurveInterpolater;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
	public abstract class ConnectableChildObjectBase : ConnectableObjectBase
	{
		public override LaneType LaneType => ReferenceStartObject?.LaneType ?? default;

		public bool IsEndObject => NextObject is null;

		private float curvePrecision = 0.025f;
		
		[LocalizableObjectPropertyBrowserAlias(nameof(Resources.CurvePrecisionLabel))]
		public float CurvePrecision
		{
			get => curvePrecision;
			set => Set(ref curvePrecision, value <= 0 ? 0.01f : value);
		}

		private ICurveInterpolaterFactory curveInterpolaterFactory = XGridLimitedCurveInterpolaterFactory.Default;
		
		[LocalizableObjectPropertyBrowserAlias(nameof(Resources.CurveInterpolatorFactoryLabel))]
		public ICurveInterpolaterFactory CurveInterpolaterFactory
		{
			get => curveInterpolaterFactory;
			set => Set(ref curveInterpolaterFactory, value);
		}

		public bool IsAnyControlSelecting => PathControls.Any(x => x.IsSelected);

		private ConnectableObjectBase prevObject;
		public ConnectableObjectBase PrevObject
		{
			get => prevObject;
			set
			{
				if (prevObject is not null)
					prevObject.NextObject = default;
				Set(ref prevObject, value);
				if (prevObject is not null)
					prevObject.NextObject = this;
				NotifyRefreshPaths();
			}
		}

		private ConnectableStartObject referenceStartObject;
		public override ConnectableStartObject ReferenceStartObject => referenceStartObject;

		private int recordId = int.MinValue;
		public override int RecordId { get => ReferenceStartObject?.RecordId ?? recordId; set => Set(ref recordId, value); }

		private List<LaneCurvePathControlObject> pathControls = new();
		public IReadOnlyList<LaneCurvePathControlObject> PathControls => pathControls;

		public bool IsCurvePath => PathControls.Count > 0;
		public bool IsVaildPath
		{
			get
			{
				if (cacheGeneratedPath is null)
					RegeneratePaths();

				return cachedIsVaild;
			}
		}

		private bool cachedIsVaild = false;
		private List<(Vector2 pos, bool isVaild)> cacheGeneratedPath = default;

		public void SetReferenceStartObject(ConnectableStartObject refStart)
		{
			referenceStartObject = refStart;
		}

		public void AddControlObject(LaneCurvePathControlObject controlObj)
		{
			InsertControlObject(PathControls.Count, controlObj);
		}

		public void InsertControlObject(int index, LaneCurvePathControlObject controlObj)
		{
#if DEBUG
			if (controlObj.RefCurveObject is not null)
				throw new Exception("controlObj is using");
#endif

			pathControls.Insert(index, controlObj);
			for (int i = index; i < pathControls.Count; i++)
				pathControls[i].Index = i;
			controlObj.PropertyChanged += ControlObj_PropertyChanged;
			controlObj.RefCurveObject = this;
			NotifyRefreshPaths();
			NotifyOfPropertyChange(() => PathControls);
		}

		private void ControlObj_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(IsSelected):
					NotifyOfPropertyChange(() => IsAnyControlSelecting);
					break;
				case nameof(TGrid):
				case nameof(XGrid):
					NotifyRefreshPaths();
					NotifyOfPropertyChange(e.PropertyName);
					break;
				default:
					break;
			}
		}

		internal void NotifyRefreshPaths()
		{
			ObjectPool<List<(Vector2 pos, bool isVaild)>>.Return(cacheGeneratedPath);
			cacheGeneratedPath = default;
			cachedIsVaild = default;
		}

		private void RegeneratePaths()
		{
			if (cacheGeneratedPath is null)
				cacheGeneratedPath = ObjectPool<List<(Vector2 pos, bool isVaild)>>.Get();
			cacheGeneratedPath.Clear();

			var isVaild = true;
			foreach (var p in GenerateConnectionPaths())
			{
				cacheGeneratedPath.Add(p);
				isVaild = isVaild && p.isVaild;
			}

			cachedIsVaild = isVaild;
		}

		public void RemoveControlObject(LaneCurvePathControlObject controlObj)
		{
			if (pathControls.Remove(controlObj))
			{
				controlObj.RefCurveObject = null;
				controlObj.PropertyChanged -= ControlObj_PropertyChanged;
				NotifyRefreshPaths();
				NotifyOfPropertyChange(() => PathControls);
			}
		}

		public IEnumerable<Vector2> GridBasePoints => PathControls
			.AsEnumerable<OngekiMovableObjectBase>()
			.Prepend(PrevObject)
			.Append(this)
			.OfType<OngekiMovableObjectBase>()
			.Select(x => new Vector2(x.XGrid.TotalGrid, x.TGrid.TotalGrid));

		public IEnumerable<(Vector2 pos, bool isVaild)> GenerateConnectionPaths()
		{
			int calcSign(Vector2 a, Vector2 b)
			{
				if (a.Y == b.Y)
					return 1;

				return Math.Sign(b.Y - a.Y);
			}

			using var d = GridBasePoints.ToListWithObjectPool(out var points);
			if (points.Count <= 2)
			{
				var fromP = points[0];
				var toP = points[1];
				yield return (fromP, true);
				yield return (toP, toP.Y >= fromP.Y);
				yield break;
			}

			var prevPos = points[0];
			var prevSign = 0;
			var step = CurvePrecision;
			var isVaild = true;

			var t = 0f;
			while (true)
			{
				var curP = BezierCurve.CalculatePoint(points, t);
				var sign = calcSign(prevPos, curP);

				if (prevSign != sign && prevSign != 0)
					isVaild = isVaild && false;

				prevPos = curP;
				prevSign = sign;

				yield return (curP, isVaild);

				if (t >= 1)
					break;

				t = MathF.Min(1, t + step);
			}
		}

		public IReadOnlyList<(Vector2 pos, bool isVaild)> GetConnectionPaths()
		{
			if (cacheGeneratedPath is null)
				RegeneratePaths();

			return cacheGeneratedPath;
		}

		public double? CalulateXGridTotalGrid(double totalTGrid)
		{
			if (PathControls.Count > 0)
			{
				Vector2? prevVec2 = null;

				foreach ((var gridVec2, var isVaild) in GetConnectionPaths())
				{
					if (!isVaild)
						return default;

					if (totalTGrid <= gridVec2.Y)
					{
						prevVec2 = prevVec2 ?? gridVec2;

						var fromXGrid = prevVec2.Value.X;
						var fromTGrid = prevVec2.Value.Y;
						var toTGrid = gridVec2.Y;
						var toXGrid = gridVec2.X;

						var xTotalGrid = MathUtils.CalculateXFromTwoPointFormFormula(totalTGrid, fromXGrid, fromTGrid, toXGrid, toTGrid);

						//Log.LogDebug($"fromXGrid:{fromXGrid} fromTGrid:{fromTGrid} fromTGrid:{fromTGrid} fromTGrid:{fromTGrid} tGrid:{tGrid} -> {xGrid}");
						return xTotalGrid;
					}

					prevVec2 = gridVec2;
				}

				return default;
			}
			else
			{
				//就在当前[prev,cur]范围内，那么就插值计算咯
				var xGrid = MathUtils.CalculateXFromTwoPointFormFormula(totalTGrid, PrevObject.XGrid.TotalGrid, PrevObject.TGrid.TotalGrid, XGrid.TotalGrid, TGrid.TotalGrid);
				return xGrid;
			}
		}

		public XGrid CalulateXGrid(TGrid tGrid)
		{
			if (CalulateXGridTotalGrid(tGrid.TotalGrid) is not double totalGrid)
				return default;
			var xGrid = new XGrid(0, (int)totalGrid);
			xGrid?.NormalizeSelf();
			return xGrid;
		}

		public bool CheckCurveVaild()
		{
			return GetConnectionPaths().All(x => x.isVaild);
		}

		public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
		{
			return PathControls.AsEnumerable<IDisplayableObject>().Append(this);
		}

		public override bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
		{
			return base.CheckVisiable(minVisibleTGrid, maxVisibleTGrid) || (TGrid > maxVisibleTGrid && PrevObject is not null && PrevObject.TGrid < minVisibleTGrid);
		}

		public override string ToString() => $"{base.ToString()} {(PathControls.Count > 0 ? $"CurveCount[{PathControls.Count}]" : string.Empty)} RefStart[{ReferenceStartObject}]";

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not ConnectableChildObjectBase from)
				return;

			RecordId = -Math.Abs(from.RecordId);
			SetReferenceStartObject(null);
			PrevObject = null;
			CurvePrecision = from.CurvePrecision;
			CurveInterpolaterFactory = from.CurveInterpolaterFactory;
			foreach (var cp in from.PathControls)
			{
				var newCP = new LaneCurvePathControlObject();
				newCP.Copy(cp);
				AddControlObject(newCP);
			}
		}

		public IEnumerable<ConnectableChildObjectBase> InterpolateCurveChildren(ICurveInterpolaterFactory factory = default)
		{
			var to = ReferenceStartObject.Children.FindNextOrDefault(this);
			var itor = (factory ?? CurveInterpolaterFactory).CreateInterpolaterForRange(this, to);

			while (true)
			{
				if (itor.EnumerateNext() is not CurvePoint point)
					break;

				var newNext = ReferenceStartObject.CreateChildObject();

				newNext.Copy(this);
				foreach (var ctrl in newNext.PathControls.ToArray())
					newNext.RemoveControlObject(ctrl);

				newNext.TGrid = point.TGrid;
				newNext.XGrid = point.XGrid;

				yield return newNext;
			}
		}
	}
}
