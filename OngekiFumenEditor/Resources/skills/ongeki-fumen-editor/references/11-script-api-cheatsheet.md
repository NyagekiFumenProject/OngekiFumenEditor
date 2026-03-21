# Script API Cheatsheet

Use this page to write scripts quickly against the editor's existing runtime surface.

If you still need help choosing object families, time/grid conversion helpers, or runtime-safe mutation structure, also read `13-script-object-type-index.md`, `14-time-grid-xgrid-and-navigation.md`, and `15-runtime-script-safety-and-undo-patterns.md`.

## Table of Contents

1. Minimal Imports
2. Primary Live Object
3. Logging and Return Values
4. Core Chart APIs
5. Grid and Time Helpers
6. Connectable Objects
7. Undoable Mutation Pattern
8. Legacy-Only Service Access

## Minimal Imports

For portable scripts that should work well with runtime automation, start from:

```csharp
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
```

For legacy in-app scripts only, you may additionally use service-locator access such as:

```csharp
using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
```

Do not rely on those extra imports for runtime automation scripts.

## Primary Live Object

Legacy in-app scripts may read the editor directly:

```csharp
var editor = ScriptArgs.TargetEditor;
if (editor is null)
    return;
```

Portable runtime-automation scripts should enter through:

```csharp
ScriptArgs.TargetEditor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create(
        "Task name",
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            // work here
        },
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            // undo here
        }));
```

Useful members on `editor`:

* `editor.Fumen`: the in-memory `OngekiFumen`
* `editor.Setting`: editor-local display and behavior settings
* `editor.EditorProjectData`: project model, including audio and fumen paths
* `editor.UndoRedoManager`: undoable mutation entry
* `editor.GetCurrentTGrid()`: current timeline position
* `editor.ScrollTo(TGrid)`, `editor.ScrollTo(TimeSpan)`, `editor.ScrollTo(ITimelineObject)`: viewport navigation
* `editor.RecalculateTotalDurationHeight()`: recalculate scrollable editor height after duration-shaping changes

## Logging and Return Values

Use logging when the user needs human-readable progress:

```csharp
Log.LogInfo("message");
Log.LogDebug("debug");
Log.LogError("error");
```

For runtime automation, also return a serializable result:

```csharp
return new { created = 3, skipped = 1 };
```

Good return types:

* `null`
* `string`
* numbers and booleans
* arrays and lists of primitive/serializable values
* anonymous objects

Avoid returning:

* `FumenVisualEditorViewModel`
* `OngekiFumen`
* WPF objects
* other complex runtime objects

## Core Chart APIs

Useful members on `var fumen = editor.Fumen;`:

* `fumen.AddObject(obj)`
* `fumen.RemoveObject(obj)`
* `fumen.AddObjects(seq)`
* `fumen.RemoveObjects(seq)`
* `fumen.GetAllDisplayableObjects()`
* typed collections such as `fumen.Taps`, `fumen.Holds`, `fumen.Lanes`, `fumen.Bullets`, `fumen.Bells`, `fumen.BpmList`, `fumen.MeterChanges`

Typical legacy-script read-only query:

```csharp
var taps = editor.Fumen.Taps.Count;
var holds = editor.Fumen.Holds.Count;
return new { taps, holds };
```

For runtime automation, prefer MCP editor tools for pure queries. The current security policy is mutation-oriented.

## Grid and Time Helpers

Create positions directly:

```csharp
var t = new TGrid(12, 960);
var x = new XGrid(-4, 0);
x.NormalizeSelf();
```

Convert between audio time and chart time:

```csharp
var t = TGridCalculator.ConvertAudioTimeToTGrid(TimeSpan.FromSeconds(10), editor);
var audio = TGridCalculator.ConvertTGridToAudioTime(t, editor);
```

Useful types:

* `TGrid`
* `XGrid`
* `GridOffset`

## Connectable Objects

For lane- or beam-like chains, build the start object first and then append children:

```csharp
using OngekiFumenEditor.Base.OngekiObjects.Lane;

var start = new ColorfulLaneStart();
start.TGrid = new TGrid(4, 0);
start.XGrid = new XGrid(0, 0);

var next = new ColorfulLaneNext();
next.TGrid = new TGrid(8, 0);
next.XGrid = new XGrid(2, 0);

start.AddChildObject(next);
```

Useful APIs:

* `ConnectableStartObject.AddChildObject(child)`
* `ConnectableStartObject.InsertChildObject(...)`
* `ConnectableStartObject.RemoveChildObject(child)`
* child `PathControls` for curve editing when needed

## Undoable Mutation Pattern

Portable mutation scripts should follow this shape:

```csharp
var created = new List<Tap>();

ScriptArgs.TargetEditor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create(
        "Add taps",
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            var fumen = editor.Fumen;

            if (created.Count == 0)
            {
                created.Add(new Tap { TGrid = new TGrid(4, 0), XGrid = new XGrid(0, 0) });
                created.Add(new Tap { TGrid = new TGrid(8, 0), XGrid = new XGrid(2, 0) });
            }

            foreach (var tap in created)
                fumen.AddObject(tap);
        },
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            var fumen = editor.Fumen;

            foreach (var tap in created)
                fumen.RemoveObject(tap);
        }));

return new { created = created.Count };
```

## Legacy-Only Service Access

In `.nyagekiScript` documents, the template demonstrates:

```csharp
var editorManager = IoC.Get<IEditorDocumentManager>();
```

Use this only when you intentionally target the legacy in-app script executor. Runtime automation blocks this pattern.
