# Common Object Property Index

Use this page after choosing an object family but before writing the script. It focuses on the properties that usually matter in user-facing operations.

## Timeline And Metadata Objects

* `BPMChange`:
  set `TGrid` and `BPM`.
  use `LengthConvertToOffset(...)` when converting millisecond lengths into grid offsets inside the current BPM segment.

* `MeterChange`:
  set `TGrid`, `BunShi`, and `Bunbo`.
  use when the user talks about measure structure rather than speed.

* `Soflan`:
  set `TGrid`, `EndTGrid`, `Speed`, `SoflanGroup`, and `ApplySpeedInDesignMode`.
  `SpeedInEditor` reflects whether design mode should respect the speed.
  `EndIndicator` is the paired duration endpoint; do not treat it as an unrelated object.

* `ClickSE`:
  only needs `TGrid` for most scripts.

* `EnemySet`:
  set `TGrid` and `TagTblValue`.
  `TagTblValue` is the wave selector: `Wave1`, `Wave2`, or `Boss`.

* `Comment`:
  lives under `OngekiFumenEditor.Base.EditorObjects`.
  set `TGrid` and `Content`.

* `LaneBlockArea`:
  use `TGrid`, the paired end indicator, and the direction-related fields when the user describes block spans rather than point events.

## Notes

* `Tap`:
  common fields are `TGrid`, `XGrid`, `IsCritical`, and `ReferenceLaneStart`.
  `ReferenceLaneStrId` is derived from the lane start.
  `ReferenceLaneStrIdManualSet` is a property-browser convenience, not the first-choice script API.

* `Hold`:
  common fields are `TGrid`, `XGrid`, `IsCritical`, and `ReferenceLaneStart`.
  `EndTGrid` is derived from the linked `HoldEnd`.
  create or update the endpoint with `SetHoldEnd(...)`.

* `HoldEnd`:
  usually only needs `TGrid`.
  its horizontal position is redocked from the parent hold and lane relationship.

* `Flick`:
  common fields are `TGrid`, `XGrid`, `Direction`, and `IsCritical`.
  `Direction.Left` is `1` and `Direction.Right` is `-1`.
  mirror-style operations often need to flip `Direction` as well as position.

## Projectile Objects

* `BulletPallete`:
  common fields are `StrID`, `EditorName`, `ShooterValue`, `TargetValue`, `PlaceOffset`, `RandomOffsetRange`, `SizeValue`, `TypeValue`, and `Speed`.

* `Bullet`:
  common fields are `TGrid`, `XGrid`, `ReferenceBulletPallete`, and `BulletDamageTypeValue`.
  `Speed`, `PlaceOffset`, `TargetValue`, `ShooterValue`, `SizeValue`, `TypeValue`, and `RandomOffsetRange` each store their own configured value. `ReferenceBulletPallete` is nullable; when set, the palette's parameters override these configured values when the palette is not null.

* `Bell`:
  common fields are `TGrid`, `XGrid`, and `ReferenceBulletPallete`.
  same override behavior as `Bullet`: `Speed`, `PlaceOffset`, `TargetValue`, `ShooterValue`, and `SizeValue` are overridden by the palette's parameters while `ReferenceBulletPallete` is set.

## Connectable Paths

* `ConnectableStartObject`:
  common fields are `TGrid`, `XGrid`, `RecordId`, `CurveInterpolaterFactory`, and `Children`.
  use `CreateChildObject()`, `AddChildObject(...)`, or `InsertChildObject(...)` to grow the chain.
  use `CalulateXGrid(tGrid)` to sample the path at a specific timeline position.

* `ConnectableChildObjectBase`:
  common fields are `TGrid`, `XGrid`, `CurvePrecision`, `CurveInterpolaterFactory`, and `PathControls`.
  `PrevObject` and `ReferenceStartObject` are relationship fields and should normally be set through `AddChildObject(...)` instead of manual assignment.
  `IsCurvePath`, `IsVaildPath`, `CheckCurveVaild()`, and `CalulateXGrid(...)` are the most useful inspection helpers.

* lane and wall families:
  starts and children live under `OngekiFumenEditor.Base.OngekiObjects.Lane`.
  `LaneStartBase` adds `IsTransparent`.

* `BeamStart` and `BeamNext`:
  common fields are `WidthId` and `ObliqueSourceXGridOffset`.
  `ObliqueSourceXGridOffset` being non-null makes the beam oblique.

* SVG prefabs:
  live under `Base/EditorObjects/Svg/`.
  common concerns are `TGrid`, `XGrid`, and `CurveInterpolaterFactory` when the prefab behaves like a path-driven editor helper.

## Property-Setting Heuristics

* If the object is lane-docked, set `ReferenceLaneStart` and let the editor maintain the relationship.
* If the object has a paired endpoint or child chain, build that relationship first, then add the objects to `fumen`.
* If the object uses a palette reference, ensure the palette exists before assignment.
* If the object exposes a `ManualSet` or property-browser convenience field, treat that as a last-resort interop surface rather than the primary script API.
* If the property looks like a UI helper or property-browser convenience field, prefer the underlying model reference instead.
