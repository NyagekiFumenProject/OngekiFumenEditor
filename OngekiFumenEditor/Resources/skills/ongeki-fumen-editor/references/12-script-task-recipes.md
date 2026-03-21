# Script Task Recipes

Use these recipes to translate common user requests into scripts with minimal source-code changes.

When the recipe depends on picking the right object class, coordinate helper, or runtime-safe undo shape, cross-check `13-script-object-type-index.md`, `14-time-grid-xgrid-and-navigation.md`, and `15-runtime-script-safety-and-undo-patterns.md`.

## Table of Contents

1. Recipe 1: Read-Only Report
2. Recipe 2: Add Objects by Audio Time
3. Recipe 3: Batch Modify Existing Objects
4. Recipe 4: Build a Connectable Lane Chain
5. Recipe 5: Navigate After a Transformation
6. Recipe 6: Choose Between Legacy and Runtime Automation

## Recipe 1: Read-Only Report

User intent:

* count objects
* inspect paths
* summarize current editor state

Preferred path:

* For runtime automation, prefer `editor.get_current` or `editor.get_current_summary` instead of a script.
* For legacy `.nyagekiScript`, use:

```csharp
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;

var editor = ScriptArgs.TargetEditor;
if (editor is null)
    return new { success = false, reason = "No target editor." };

return new
{
    editor.DisplayName,
    editor.FilePath,
    tapCount = editor.Fumen.Taps.Count,
    holdCount = editor.Fumen.Holds.Count,
    laneCount = editor.Fumen.Lanes.Count,
    currentTGrid = editor.GetCurrentTGrid()?.ToString()
};
```

## Recipe 2: Add Objects by Audio Time

User intent:

* place notes at specific timestamps
* generate objects from external timing data already supplied in the prompt

Pattern:

```csharp
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System;
using System.Collections.Generic;

var specs = new[]
{
    new { Ms = 1000.0, X = -4f },
    new { Ms = 1500.0, X = 0f },
    new { Ms = 2000.0, X = 4f },
};

var created = new List<Tap>();

ScriptArgs.TargetEditor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create(
        "Add taps from audio times",
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            var fumen = editor.Fumen;

            if (created.Count == 0)
            {
                foreach (var spec in specs)
                {
                    created.Add(new Tap
                    {
                        TGrid = TGridCalculator.ConvertAudioTimeToTGrid(TimeSpan.FromMilliseconds(spec.Ms), editor),
                        XGrid = new XGrid(spec.X, 0)
                    });
                }
            }

            foreach (var tap in created)
                fumen.AddObject(tap);

            editor.ScrollTo(created[0].TGrid);
        },
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            foreach (var tap in created)
                editor.Fumen.RemoveObject(tap);
        }));

return new { created = created.Count };
```

## Recipe 3: Batch Modify Existing Objects

User intent:

* shift notes
* toggle flags such as `IsCritical`
* recolor or relink existing objects

Pattern:

```csharp
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System.Collections.Generic;
using System.Linq;

var touched = new List<(Tap tap, bool before)>();

ScriptArgs.TargetEditor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create(
        "Batch toggle tap critical",
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            if (touched.Count == 0)
                touched.AddRange(editor.Fumen.Taps.Select(x => (x, x.IsCritical)));

            foreach (var item in touched)
                item.tap.IsCritical = true;
        },
        () =>
        {
            foreach (var item in touched)
                item.tap.IsCritical = item.before;
        }));

return new { changed = touched.Count };
```

## Recipe 4: Build a Connectable Lane Chain

User intent:

* generate lane paths
* import sampled path points into a lane family

Pattern:

```csharp
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System.Collections.Generic;

var points = new[]
{
    new { T = new TGrid(4, 0), X = new XGrid(-8, 0) },
    new { T = new TGrid(8, 0), X = new XGrid(0, 0) },
    new { T = new TGrid(12, 0), X = new XGrid(8, 0) },
};

ColorfulLaneStart start = null;

ScriptArgs.TargetEditor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create(
        "Add lane chain",
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            if (start is null)
            {
                start = new ColorfulLaneStart
                {
                    TGrid = points[0].T,
                    XGrid = points[0].X
                };

                foreach (var point in points[1..])
                {
                    start.AddChildObject(new ColorfulLaneNext
                    {
                        TGrid = point.T,
                        XGrid = point.X
                    });
                }
            }

            editor.Fumen.AddObject(start);
        },
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            editor.Fumen.RemoveObject(start);
        }));

return new { created = points.Length };
```

## Recipe 5: Navigate After a Transformation

User intent:

* move the editor viewport to the changed region
* inspect the result immediately

Pattern:

```csharp
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;

var target = new TGrid(32, 0);

ScriptArgs.TargetEditor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create(
        "Scroll to target",
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            editor.ScrollTo(target);
        },
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            editor.ScrollTo(target);
        }));

return new { scrolledTo = target.ToString() };
```

## Recipe 6: Choose Between Legacy and Runtime Automation

* Need `IoC.Get(...)`, external files, or ad hoc IDE editing:
  Use the legacy `.nyagekiScript` surface.
* Need assistant-driven execution through MCP with a structured result:
  Use runtime automation and stay within the portable subset.
* Need reusable editor behavior:
  Patch source instead of scripting.
