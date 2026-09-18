# Time Grid, X Grid, And Navigation

Use this page when a script needs to convert between audio time, `TGrid`, `XGrid`, viewport position, or lane-relative placement.

## Mental Model

* `TGrid` is the vertical musical timeline. Its radix is `1920`, so one unit is `1920` sub-grids.
* `XGrid` is the horizontal lane space. Its radix is `4096`.
* `GridOffset` is a relative delta, not an absolute position. Use it for `tGrid + new GridOffset(...)` or `xGrid + new GridOffset(...)`.
* `TotalGrid` is the normalized integer representation that supports ordering and interpolation.

## Safe Constructors

* `new TGrid(unit, grid)` and `new XGrid(unit, grid)` are fine when you already know normalized values.
* `TGrid.FromTotalGrid(...)` and `XGrid.FromTotalGrid(...)` are safer when you computed a flattened integer grid.
* `TGrid.FromTotalUnit(...)` and `XGrid.FromTotalUnit(...)` are useful when you have a fractional unit value.
* Call `NormalizeSelf()` only when you manually edited `Unit` or `Grid` and need canonical values.

## Convert Between Audio Time And TGrid

Use `TGridCalculator` for music-aware conversions. It already consults the current BPM list.

```csharp
var tGrid = TGridCalculator.ConvertAudioTimeToTGrid(TimeSpan.FromSeconds(42), editor);
var audioTime = TGridCalculator.ConvertTGridToAudioTime(new TGrid(16, 0), editor);
```

Choose this path when the user request is phrased as seconds, milliseconds, bars, beats, or playback time.

## Convert Between TGrid And Y

There are two different vertical projections:

* Design mode:
  use `ConvertYToTGrid_DesignMode(...)`, `ConvertTGridToY_DesignMode(...)`, `ConvertAudioTimeToY_DesignMode(...)`
* Preview mode:
  use `ConvertYToTGrid_PreviewMode(...)`, `ConvertTGridToY_PreviewMode(...)`, `ConvertAudioTimeToY_PreviewMode(...)`

Do not mix them. Preview mode can have multiple visible ranges at the same canvas Y because of soflan projection, while design mode is the straightforward editor placement projection.

## Beats And Meter Helpers

Use these helpers when the request is quantized to measure or beat lines:

* `TGridCalculator.GetCurrentTimeSignature(...)`
* `TGridCalculator.GetVisbleTimelines_DesignMode(...)`
* `TGridCalculator.GetVisbleTimelines_PreviewMode(...)`
* `TGridCalculator.TryPickClosestBeatTime(...)`
* `TGridCalculator.TryPickMagneticBeatTime(...)`

This is better than hand-rolling meter math with `BunShi`, `Bunbo`, and `ResT` unless the script needs a custom quantizer.

## Editor Navigation APIs

`FumenVisualEditorViewModel` exposes simple navigation entry points:

* `editor.GetCurrentTGrid()`
* `editor.ScrollTo(TGrid)`
* `editor.ScrollTo(TimeSpan)`
* `editor.ScrollTo(ITimelineObject)`
* `editor.CurrentPlayTime`

Use `ScrollTo(...)` after a mutation when the user expects the viewport to focus the new or changed material.

## Lane-Relative X Placement

For lane-docked objects, avoid inventing horizontal coordinates if a lane reference exists.

* `Tap` and `Hold` can stay lane-relative by setting `ReferenceLaneStart`.
* `ConnectableStartObject.CalulateXGrid(tGrid)` gives the interpolated lane X at a timeline position.
* `ConnectableChildObjectBase.CalulateXGrid(tGrid)` gives the interpolated X within a path segment.

Example:

```csharp
var laneX = targetLane.CalulateXGrid(targetTGrid) ?? XGrid.Zero;
var tap = new Tap
{
    TGrid = targetTGrid,
    XGrid = laneX,
    ReferenceLaneStart = targetLane,
};
```

## Common Decision Rules

* If the request is "at 01:23.450", convert from `TimeSpan` to `TGrid`.
* If the request is "at bar 12 beat 3", work in `TGrid` and meter helpers.
* If the request is "move what is currently selected into view", use `editor.ScrollTo(...)`.
* If the request is "place this note on the current lane path", derive X from the lane or set `ReferenceLaneStart`.
* Only use raw Y coordinates when the task is explicitly about viewport interaction or screen-space picking.

## Minimal Example

```csharp
var targetTime = TimeSpan.FromMilliseconds(27500);
var targetTGrid = TGridCalculator.ConvertAudioTimeToTGrid(targetTime, editor);
var snapped = TGridCalculator.TryPickClosestBeatTime(
    (float)TGridCalculator.ConvertTGridToY_DesignMode(targetTGrid, editor),
    editor);

editor.ScrollTo(snapped.tGrid);
```

This pattern is useful when the request starts from audio time, then wants the final object aligned to the nearest visible beat line.
