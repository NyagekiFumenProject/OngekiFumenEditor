// Suite 80 — viewport navigation and runtime automation scripts.
//
// editor.scroll_to must not disturb history. The script.* tools are the opposite: they run
// arbitrary C# through Roslyn behind a security policy. Two rules shape every script here:
//   1. no reflection / IO / network / DI tokens (`System.IO.File`, `IoC.Get`, ...)
//   2. EVERY script — even a read-only one — must contain an inline
//      `UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(name, redo, undo))`
//      or it is refused with SECURITY_CHECK_FAILED.

import { verifyUndoRedo } from '../lib/history.mjs';
import { splitTotalGrid, totalGridResolution, parseTGridTotal } from '../lib/env.mjs';

const IMPORTS = `using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System.Linq;`;

/**
 * Adds a tap through the allowed undoable shape.
 *
 * This deliberately re-reads `ScriptArgs.TargetEditor` *inside* the queued lambdas instead of
 * capturing a local at the top: that is the documented "reacquire" style, and the lambdas run
 * long after the script body returned (the host only executes the combined action once
 * `Execute` is done, and `editor.undo` may undo it much later still). It is the regression
 * guard for script queued actions remaining resolvable outside the executor's registration
 * window — if the host ever forgets the target editor, the undo below throws instead of
 * removing the tap.
 */
const mutatingScript = `${IMPORTS}

var editor = ScriptArgs.TargetEditor;
Tap tap = null;

editor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create(
        "MCP e2e scripted tap",
        () =>
        {
            var target = ScriptArgs.TargetEditor;
            tap ??= new Tap { TGrid = new TGrid(4000, 0), XGrid = XGrid.Zero };
            target.Fumen.AddObject(tap);
        },
        () =>
        {
            var target = ScriptArgs.TargetEditor;
            if (tap is not null)
                target.Fumen.RemoveObject(tap);
        }));

return new { added = tap?.Id ?? 0 };`;

/**
 * Read-only reporting script. It still has to carry the mutation shape (the policy is
 * token-based, not flow-based), so it registers a no-op action and only reads the chart.
 */
const reportingScript = `${IMPORTS}

var editor = ScriptArgs.TargetEditor;
var taps = editor.Fumen.Taps.Count();
var lanes = editor.Fumen.Lanes.Count();

editor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create("MCP e2e read-only probe", () => { }, () => { }));

return new { taps, lanes };`;

/** Violates two documented token rules. */
const forbiddenScript = `${IMPORTS}

var injected = Caliburn.Micro.IoC.Get<object>()?.ToString();
var text = System.IO.File.ReadAllText("C:/windows/win.ini");
return new { injected, text };`;

/** Mutates chart state without the required undoable wrapper. */
const unsafeMutationScript = `${IMPORTS}

ScriptArgs.TargetEditor.Fumen.AddObject(new Tap { TGrid = new TGrid(3000, 0), XGrid = XGrid.Zero });
return new { done = true };`;

/** Carries the required shape but does not compile. */
const brokenScript = `${IMPORTS}

var editor = ScriptArgs.TargetEditor;
editor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create("MCP e2e broken", () => { }, () => { }));
var broken = ;`;

export default {
  name: '80-scroll-script',
  description: 'scroll_to / compile / run_editor / run_current_editor / get_last_result',

  async run(ctx) {
    const api = ctx.api;
    const editorId = ctx.editorId;
    ctx.cover('editor.scroll_to', 'script.compile', 'script.run_editor', 'script.run_current_editor', 'script.get_last_result');

    const taps = await api.family(editorId, 'tap');
    const base = taps[0].tGrid.totalGrid;
    const T = (n) => splitTotalGrid(base + n * totalGridResolution);
    const totalOf = (t) => t.tGridUnit * totalGridResolution + t.tGridGrid;

    // ---------------- editor.scroll_to ----------------
    ctx.section('editor.scroll_to');

    // A scroll target is clamped to the loaded audio duration, so probe the reachable ceiling
    // first and pick two distinct positions strictly inside it — a fixed fraction of the
    // chart would collapse both targets onto the clamped end and hide a broken viewport.
    const ceilingProbe = await api.scrollTo({ editorId, tGridUnit: 100000, tGridGrid: 0 });
    ctx.ok('scroll_to clamps a far-out target instead of failing', ceilingProbe);
    const ceiling = parseTGridTotal(ceilingProbe.payload?.viewport);
    ctx.check('the reachable ceiling is reported as a TGrid',
      Number.isInteger(ceiling) && ceiling > 0, `viewport=${ceilingProbe.payload?.viewport}`);

    const targetA = splitTotalGrid(Math.floor(ceiling * 0.2));
    const targetB = splitTotalGrid(Math.floor(ceiling * 0.7));

    const scrolled = await api.scrollTo({ editorId, ...targetA });
    ctx.ok('scroll_to succeeds', scrolled);
    ctx.hasKeys('scroll_to response shape', scrolled.payload, ['success', 'editorId', 'target', 'viewport', 'isPreviewMode']);
    ctx.equal('the reported target echoes the requested TGrid',
      scrolled.payload.target, `T[${targetA.tGridUnit},${targetA.tGridGrid}]`);
    ctx.equal('the editor is not in preview mode', scrolled.payload.isPreviewMode, false);
    ctx.check('the reported viewport is a TGrid string', parseTGridTotal(scrolled.payload.viewport) !== null,
      `viewport=${scrolled.payload.viewport}`);
    ctx.check('the viewport landed on the requested position',
      Math.abs(parseTGridTotal(scrolled.payload.viewport) - totalOf(targetA)) <= 8,
      `viewport=${scrolled.payload.viewport} target=${scrolled.payload.target}`);

    const scrolledFar = await api.scrollTo({ editorId, ...targetB });
    ctx.ok('scrolling elsewhere succeeds', scrolledFar);
    ctx.check('scrolling elsewhere moves the viewport',
      scrolledFar.payload.viewport !== scrolled.payload.viewport,
      `${scrolled.payload.viewport} -> ${scrolledFar.payload.viewport}`);

    // scrolling must not touch history: park one redoable entry, scroll, then redo it
    const probeSeed = await api.addObject({ editorId, objectType: 'tap', ...T(12), xGridUnit: 0, xGridGrid: 0 });
    ctx.ok('history probe: one undoable entry was seeded', probeSeed);
    const probeUndo = await api.undo({ editorId });
    ctx.ok('history probe: undo succeeds', probeUndo);
    ctx.check('history probe: the entry is redoable', (probeUndo.payload?.redoCount ?? 0) >= 1,
      `redoCount=${probeUndo.payload?.redoCount}`);
    await api.scrollTo({ editorId, ...targetB });
    const probeRedo = await api.redo({ editorId });
    ctx.ok('scroll_to did not clear the redo stack (scrolling is history-neutral)', probeRedo);
    ctx.equal('the parked entry was consumed by the redo', probeRedo.payload?.redoCount, 0);
    await api.undo({ editorId });
    ctx.equal('history probe: the seeded tap was removed again',
      (await api.family(editorId, 'tap')).length, taps.length);

    ctx.fails('scroll_to rejects an unknown editorId',
      await api.scrollTo({ editorId: 'editor-does-not-exist', ...targetA }), 'EDITOR_NOT_FOUND');
    ctx.fails('scroll_to rejects a stale expectedEditorId',
      await api.scrollTo({ editorId, expectedEditorId: 'editor-not-this-one', ...targetA }), 'EDITOR_CHANGED');

    // ---------------- script.compile ----------------
    ctx.section('script.compile');

    const compiledOk = await api.compileScript({ scriptText: mutatingScript });
    ctx.ok('a well-formed mutating script compiles', compiledOk);
    ctx.hasKeys('compile response shape', compiledOk.payload, ['success', 'diagnostics', 'securityIssues']);
    ctx.isArray('a clean compile reports no security issues', compiledOk.payload.securityIssues, { length: 0 });
    ctx.isArray('a clean compile reports no diagnostics', compiledOk.payload.diagnostics, { length: 0 });

    const compiledReporting = await api.compileScript({ scriptText: reportingScript });
    ctx.ok('a read-only script that carries the mutation shape compiles', compiledReporting,
      `issues=${JSON.stringify(compiledReporting.payload?.securityIssues)}`);

    const blockedMutation = await api.compileScript({ scriptText: unsafeMutationScript });
    ctx.check('a mutation without the undoable wrapper is blocked',
      blockedMutation.payload?.success === false && blockedMutation.payload.securityIssues.length > 0,
      `success=${blockedMutation.payload?.success}`);
    ctx.check('the block explains the required undo pattern',
      (blockedMutation.payload?.securityIssues ?? []).join(' ').includes('UndoRedoManager.ExecuteAction'),
      JSON.stringify(blockedMutation.payload?.securityIssues ?? []).slice(0, 200));

    const blockedForbidden = await api.compileScript({ scriptText: forbiddenScript });
    ctx.check('the security policy blocks forbidden API tokens',
      blockedForbidden.payload?.success === false && blockedForbidden.payload.securityIssues.length > 0,
      `issues=${JSON.stringify(blockedForbidden.payload?.securityIssues ?? []).slice(0, 200)}`);
    const issues = (blockedForbidden.payload?.securityIssues ?? []).join(' | ');
    for (const token of ['IoC.Get', 'System.IO.File']) {
      ctx.check(`the block report names '${token}'`, issues.includes(token), issues.slice(0, 220));
    }

    const brokenCompile = await api.compileScript({ scriptText: brokenScript });
    ctx.check('a script that does not compile is rejected',
      brokenCompile.payload?.success === false, `success=${brokenCompile.payload?.success}`);
    ctx.check('compile diagnostics are reported for a syntax error',
      Array.isArray(brokenCompile.payload?.diagnostics) && brokenCompile.payload.diagnostics.length > 0,
      `diagnostics=${brokenCompile.payload?.diagnostics?.length} issues=${JSON.stringify(brokenCompile.payload?.securityIssues ?? [])}`);

    const unchecked = await api.compileScript({ scriptText: forbiddenScript, enableSecurityCheck: false });
    ctx.check('enableSecurityCheck=false skips the token scan',
      (unchecked.payload?.securityIssues ?? []).length === 0,
      `issues=${JSON.stringify(unchecked.payload?.securityIssues ?? [])}`);

    // ---------------- script.run_editor ----------------
    ctx.section('script.run_editor');

    const readRun = await api.runScript({ editorId, scriptText: reportingScript });
    ctx.ok('a read-only script runs against an explicit editor', readRun);
    ctx.hasKeys('run response shape', readRun.payload, ['success', 'editorId', 'transactionName', 'logs', 'diagnostics']);
    ctx.check('the returned value round-trips as JSON',
      readRun.payload.result !== null && typeof readRun.payload.result === 'object',
      `result=${JSON.stringify(readRun.payload.result)?.slice(0, 120)}`);
    ctx.equal('the script observed the same tap count as query_object',
      readRun.payload.result?.taps, taps.length);

    ctx.fails('script.run_editor refuses a security-violating script at run time',
      await api.runScript({ editorId, scriptText: unsafeMutationScript }), 'SECURITY_CHECK_FAILED');

    ctx.fails('script.run_editor rejects an unknown editorId',
      await api.runScript({ editorId: 'editor-does-not-exist', scriptText: reportingScript }), 'EDITOR_NOT_FOUND');

    const runLogs = await api.runScript({ editorId, scriptText: brokenScript });
    ctx.check('a script that does not compile does not run',
      runLogs.payload?.success === false, `success=${runLogs.payload?.success}`);
    ctx.equal('a failed build reports no return value', runLogs.payload?.result, null);

    // a scripted mutation is undoable exactly like a tool-driven one
    const tapsBefore = (await api.family(editorId, 'tap')).length;
    await verifyUndoRedo(ctx, {
      label: 'script.run_editor (mutation)',
      mutate: () => api.runScript({
        editorId, scriptText: mutatingScript, transactionName: 'MCP e2e scripted tap',
      }),
      applied: async () => (await api.family(editorId, 'tap')).length === tapsBefore + 1,
      reverted: async () => (await api.family(editorId, 'tap')).length === tapsBefore,
      // the queued action is folded into the run's transaction, so the entry is named after it
      undoName: /MCP e2e scripted tap/i,
    });
    ctx.equal('the scripted mutation left no trace', (await api.family(editorId, 'tap')).length, tapsBefore);

    const wrapped = await api.runScript({
      editorId, scriptText: reportingScript, wrapUndoTransaction: true, transactionName: 'e2e wrapped script',
    });
    ctx.ok('wrapUndoTransaction is accepted', wrapped);
    ctx.equal('the wrapped run reports the transaction name it was given',
      wrapped.payload?.transactionName, 'e2e wrapped script');
    ctx.equal('the wrapped run still reports its value', wrapped.payload?.result?.taps, taps.length);

    // a script rejected before execution is still the cached last result
    ctx.fails('script.run_editor refuses a stale expectedEditorId',
      await api.runScript({ editorId, expectedEditorId: 'editor-not-this-one', scriptText: reportingScript }), 'EDITOR_CHANGED');

    // ---------------- script.run_current_editor ----------------
    ctx.section('script.run_current_editor');

    const currentRun = await api.runScriptOnCurrent({ scriptText: reportingScript });
    ctx.ok('a script runs against the active editor without naming it', currentRun);
    ctx.equal('the current-editor run reports the same result',
      currentRun.payload?.result?.taps, readRun.payload?.result?.taps);

    ctx.fails('script.run_current_editor refuses a stale expectedEditorId',
      await api.runScriptOnCurrent({ scriptText: reportingScript, expectedEditorId: 'editor-not-this-one' }), 'EDITOR_CHANGED');

    const currentTaps = (await api.family(editorId, 'tap')).length;
    await verifyUndoRedo(ctx, {
      label: 'script.run_current_editor (mutation)',
      mutate: () => api.runScriptOnCurrent({
        scriptText: mutatingScript, transactionName: 'MCP e2e current scripted tap',
      }),
      applied: async () => (await api.family(editorId, 'tap')).length === currentTaps + 1,
      reverted: async () => (await api.family(editorId, 'tap')).length === currentTaps,
      undoName: /MCP e2e current scripted tap/i,
      tool: 'script.run_current_editor',
    });
    ctx.equal('the current-editor scripted mutation left no trace',
      (await api.family(editorId, 'tap')).length, currentTaps);

    // ---------------- script.get_last_result ----------------
    ctx.section('script.get_last_result');

    const finalRun = await api.runScript({ editorId, scriptText: reportingScript });
    ctx.ok('a final reporting run succeeds', finalRun);

    const last = await api.getLastScriptResult();
    ctx.ok('script.get_last_result returns the cached run', last);
    ctx.hasKeys('last-result shape', last.payload, ['success', 'editorId', 'transactionName', 'logs', 'diagnostics']);
    ctx.equal('the cached result belongs to the most recent run', last.payload.editorId, editorId);
    ctx.equal('the cached result is the one just executed',
      last.payload.result?.taps, finalRun.payload.result?.taps);
    ctx.equal('the cached result reports the same chart as query_object',
      last.payload.result?.taps, taps.length);
  },
};
