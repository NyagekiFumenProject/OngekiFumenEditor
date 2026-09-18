# Script Object Type Index

Use this page when a script needs to create, inspect, or batch-edit chart objects and you need to choose the correct runtime type quickly.

## Core Rule

Prefer `fumen.AddObject(obj)` and `fumen.RemoveObject(obj)` over mutating `fumen.Taps`, `fumen.Lanes`, `fumen.Bullets`, or other collections directly. `OngekiFumen` routes each object to the correct backing collection and also performs relationship maintenance for lanes, beams, hold ends, soflan groups, and dockable relocation.

## Common Base Shapes

* `OngekiTimelineObjectBase`: has `TGrid`, but no `XGrid`. Use for timing markers such as `BPMChange`, `MeterChange`, `ClickSE`, `Comment`, and `EnemySet`.
* `OngekiMovableObjectBase`: has both `TGrid` and `XGrid`. Use for objects placed on the playfield, such as `Tap`, `Hold`, `Flick`, `Bullet`, and `Bell`.
* `ILaneDockableChangable`: lane-docked movable objects whose logical home is a lane start object. `Tap` and `Hold` are the most common examples. Set `ReferenceLaneStart` when the request says "on this lane", then let the editor relocate or redock as needed.
* `ConnectableStartObject` and `ConnectableChildObjectBase`: segmented path objects such as lanes, walls, and beams. Build them as a start plus ordered children, not as unrelated points.

## High-Frequency Object Families

* Timing and structure:
  `BPMChange` uses `TGrid` and `BPM`.
  `MeterChange` uses `TGrid`, `BunShi`, and `Bunbo`.
  `Soflan` uses `TGrid`, `EndTGrid`, `Speed`, `SoflanGroup`, and `ApplySpeedInDesignMode`.
  `ClickSE`, `Comment`, and `EnemySet` are timeline markers with `TGrid`.
  `LaneBlockArea` is also a duration-style timeline object with an end indicator rather than an isolated point.

* Tap, hold, and flick notes:
  `Tap` uses `TGrid`, `XGrid`, `IsCritical`, and optional `ReferenceLaneStart`.
  `Hold` uses `TGrid`, `XGrid`, `IsCritical`, optional `ReferenceLaneStart`, and a linked `HoldEnd`.
  `HoldEnd` is not a standalone gameplay object. Create it as the end of a `Hold` and connect it with `hold.SetHoldEnd(end)`.
  `Flick` uses `TGrid`, `XGrid`, `Direction`, and `IsCritical`.

* Projectiles:
  `BulletPallete` defines reusable projectile settings and must exist before a `Bullet` or `Bell` can reference it.
  `Bullet` uses `TGrid`, `XGrid`, `ReferenceBulletPallete`, `BulletDamageTypeValue`, and optionally local projectile settings when not delegated to the palette.
  `Bell` also uses `TGrid`, `XGrid`, and `ReferenceBulletPallete`, but its projectile type is fixed to bell behavior.
  `Bullet` and `Bell` runtime classes live under `Base/OngekiObjects/Projectiles/`, while `BulletPallete` lives directly under `Base/OngekiObjects/`.

* Connectable objects:
  Lane starts live under `OngekiFumenEditor.Base.OngekiObjects.Lane`, for example `LaneLeftStart`, `LaneCenterStart`, `LaneRightStart`, `ColorfulLaneStart`, `WallLeftStart`, `WallRightStart`, and `EnemyLaneStart`.
  Their children use paired `*Next` types such as `LaneLeftNext`, `WallRightNext`, or `ColorfulLaneNext`.
  Beam paths live under `OngekiFumenEditor.Base.OngekiObjects.Beam` with `BeamStart` and `BeamNext`.

* Editor-only objects:
  `Comment` lives under `Base/EditorObjects/`.
  SVG prefabs live under `Base/EditorObjects/Svg/` and behave like movable editor-side objects rather than gameplay notes.
  soflan-group wrap items and related display helpers are editor-facing structures, not raw chart events.

## Relationship-Building Patterns

### Hold

```csharp
var hold = new Hold
{
    TGrid = new TGrid(8, 0),
    XGrid = new XGrid(0, 0),
    IsCritical = true,
    ReferenceLaneStart = targetLane,
};

var holdEnd = new HoldEnd
{
    TGrid = new TGrid(12, 0),
};

hold.SetHoldEnd(holdEnd);

fumen.AddObject(hold);
fumen.AddObject(holdEnd);
```

### Lane / wall / beam chain

```csharp
var start = new LaneLeftStart
{
    TGrid = new TGrid(4, 0),
    XGrid = XGrid.FromTotalUnit(-3),
};

var next1 = start.CreateChildObject();
next1.TGrid = new TGrid(8, 0);
next1.XGrid = XGrid.FromTotalUnit(-2);

var next2 = start.CreateChildObject();
next2.TGrid = new TGrid(12, 0);
next2.XGrid = XGrid.FromTotalUnit(-1);

start.AddChildObject(next1);
start.AddChildObject(next2);

fumen.AddObject(start);
fumen.AddObject(next1);
fumen.AddObject(next2);
```

`AddChildObject(...)` or `InsertChildObject(...)` sets `PrevObject`, `ReferenceStartObject`, and shared `RecordId` wiring. Do not assign those links by hand unless you are intentionally reproducing editor internals.

## Which Collection Receives What

`OngekiFumen.AddObject(...)` dispatches by runtime type:

* `Tap` -> `fumen.Taps`
* `Hold` -> `fumen.Holds`
* `Flick` -> `fumen.Flicks`
* `Bullet` -> `fumen.Bullets`
* `Bell` -> `fumen.Bells`
* `BPMChange` -> `fumen.BpmList`
* `MeterChange` -> `fumen.MeterChanges`
* `Soflan` -> `fumen.SoflansMap`
* `BulletPallete` -> `fumen.BulletPalleteList`
* lane starts and lane children -> `fumen.Lanes`
* beam start and beam child objects -> `fumen.Beams`

If a requested object is missing from that list, inspect `OngekiFumen.AddObject(...)` before inventing a new insertion path.

## Selection Heuristics For Models

* If the user describes musical time, reach for `BPMChange`, `MeterChange`, `Soflan`, or `TGridCalculator` before editing raw Y positions.
* If the user says "place on lane", choose a lane-docked object such as `Tap` or `Hold` and set `ReferenceLaneStart`.
* If the user says "draw a path", "generate a wall", or "make a curve", choose a `ConnectableStartObject` family and build children with `AddChildObject(...)`.
* If the user says "bullets using palette X", make sure the palette object exists and assign `ReferenceBulletPallete`.
* If the user says "editor note", "annotation", or "svg helper", check `Base/EditorObjects/` before forcing the task into a gameplay object family.
