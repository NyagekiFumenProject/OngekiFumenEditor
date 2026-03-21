# Chart Model and Editing Primitives

## `OngekiFumen` as the Aggregate Root

* `Base/OngekiFumen.cs` is the central in-memory chart model.
* It owns typed collections for BPM changes, meters, lanes, taps, holds, bullets, bells, beams, comments, click sounds, lane blocks, SVG prefabs, and soflan-related structures.
* `MetaInfo` is live and participates in setup logic such as first BPM and first meter synchronization.
* `Base/FumenMetaInfo.cs` is not just passive metadata. Property changes on `MetaInfo` update first-BPM and first-meter state through `OngekiFumen` change handlers.

## Initialization and Normalization

* `Setup()` recalculates default first BPM and first meter state from `MetaInfo`.
* `Setup()` also ensures soflan groups and default wrappers exist inside `IndividualSoflanAreaMap`.
* Parser output is not considered ready until `fumen.Setup()` has run.

## Object Routing

* `AddObject(...)` and `RemoveObject(...)` determine which collection owns each object type.
* Connectable lanes, beams, holds, hold ends, and soflan-related objects have special routing and relationship repair logic.
* `IndividualSoflanAreaMap` is part of the aggregate, not a separate editor cache. Adding or removing individual soflan areas also mutates that structure.
* Lane-like objects are not flat notes; they participate in connectable structures and dockable relocation behavior.

## Change Tracking

* `OngekiFumen` raises `ObjectModifiedChanged` and wires object property changes when objects are added.
* The editor document marks itself dirty from most object-property mutations, not just collection add/remove operations.
* `GetAllDisplayableObjects()` is a useful cross-check surface when a new object should show up in editor scans, summaries, or selection logic.

## Useful Families

* `Base/OngekiObjects/`: gameplay-facing chart objects.
* `Base/EditorObjects/`: editor-only helpers such as comments, SVG prefabs, lane-curve controls, and autoplay helpers.
* `Base/Collections/`: sorted collections, range helpers, and type-specific aggregate structures.
* Connectable and dockable behavior usually spans several families at once, so inspect base types before patching a single concrete class.

## Change Guidance

* When adding a new chart object type, update collection ownership, add/remove routing, and any required object-modified wiring together.
* When changing connectable or dockable behavior, inspect the connectable base types and relocation helpers instead of patching one subtype in isolation.
* If a change mutates `MetaInfo`, verify that first BPM and first meter behavior still match the chart root collections after `Setup()`.
* When counts or summaries matter to UI or MCP, update those surfaces at the same time as the model change.
