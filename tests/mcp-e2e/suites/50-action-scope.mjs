// Suite 50 — action scopes and history navigation.
//
// editor.begin_action / editor.end_action let a caller batch several mutations into ONE
// undo entry. This suite pins the batching contract (queue -> apply as one entry),
// the discard path, the all-or-nothing rollback, and the unscoped undo/redo tools.

import { verifyUndoRedo, verifyActionScopeUndoRedo } from '../lib/history.mjs';
import { splitTotalGrid, totalGridResolution } from '../lib/env.mjs';

export default {
  name: '50-action-scope',
  description: 'begin_action / end_action / undo / redo',

  async run(ctx) {
    const api = ctx.api;
    const editorId = ctx.editorId;
    ctx.cover('editor.begin_action', 'editor.end_action', 'editor.undo', 'editor.redo', 'editor.add_object');
    ctx.coverUndoRedo('editor.undo');
    ctx.coverUndoRedo('editor.redo');

    const taps = await api.family(editorId, 'tap');
    const base = taps[0].tGrid.totalGrid;
    const T = (n) => splitTotalGrid(base + n * totalGridResolution);
    const add = (objectType, args = {}) => api.addObject({ editorId, objectType, ...args });

    const countTaps = async () => (await api.family(editorId, 'tap')).length;

    // ---------------- scope state machine ----------------
    ctx.section('scope state machine');

    ctx.fails('end_action without an open scope is rejected',
      await api.endAction({ editorId }), 'NO_OPEN_SCOPE');

    const begin = await api.beginAction({ editorId });
    ctx.ok('begin_action opens a scope', begin);
    ctx.hasKeys('begin_action response shape', begin.payload, ['success', 'editorId', 'scope', 'identityKey']);
    ctx.equal('the scope reports itself open', begin.payload.scope, 'open');
    ctx.check('begin_action reports the identity key it opened the scope for',
      typeof begin.payload.identityKey === 'string' && begin.payload.identityKey.length > 0,
      `identityKey=${begin.payload.identityKey}`);

    ctx.fails('a second begin_action on the same editor is rejected',
      await api.beginAction({ editorId }), 'SCOPE_ALREADY_OPEN');

    await api.endAction({ editorId, name: 'e2e close-empty' });

    // ---------------- queued batch applies as one entry ----------------
    ctx.section('queued batch applies as one undo entry');

    const tapsBefore = await countTaps();
    let firstId = null;
    let secondId = null;

    await verifyActionScopeUndoRedo(ctx, {
      label: 'two queued taps',
      mutate: async () => {
        const one = await add('tap', { ...T(20), xGridUnit: 0, xGridGrid: 0 });
        const two = await add('tap', { ...T(21), xGridUnit: 0, xGridGrid: 0 });
        firstId = one.payload?.objectId;
        secondId = two.payload?.objectId;
        return [one, two];
      },
      applied: async () => (await countTaps()) === tapsBefore + 2,
      reverted: async () => (await countTaps()) === tapsBefore,
    });

    ctx.check('the batch really created two objects', Number.isInteger(firstId) && Number.isInteger(secondId));

    // ---------------- discard ----------------
    ctx.section('discard drops the batch');

    await api.beginAction({ editorId });
    const queued = await add('tap', { ...T(22), xGridUnit: 0, xGridGrid: 0 });
    ctx.check('a mutation is queued while the scope is open',
      queued.payload?.queued === true && queued.payload?.applied === false,
      `queued=${queued.payload?.queued} applied=${queued.payload?.applied}`);
    const beforeDiscard = await countTaps();
    const discard = await api.endAction({ editorId, name: 'e2e discarded', discard: true });
    ctx.ok('end_action(discard) succeeds', discard);
    ctx.equal('the discarded batch was not applied', discard.payload?.applied, false);
    ctx.equal('the chart is unchanged after discarding', await countTaps(), beforeDiscard);

    // ---------------- an invalid queued operation is rejected, not half-applied ----------------
    ctx.section('an invalid operation inside a scope is rejected up front');

    const beforeRejected = await countTaps();
    await api.beginAction({ editorId });
    const good = await add('tap', { ...T(23), xGridUnit: 0, xGridGrid: 0 });
    ctx.check('the valid mutation is queued', good.payload?.queued === true && good.payload?.applied === false,
      `queued=${good.payload?.queued} applied=${good.payload?.applied}`);

    const bad = await add('lane', { ...T(24), xGridUnit: 0, xGridGrid: 0, laneType: 'not-a-lane-type' });
    ctx.fails('an invalid mutation is rejected immediately, even inside a scope', bad, 'INVALID_ARGUMENT');

    const rejected = await api.endAction({ editorId, name: 'e2e batch with one bad op' });
    ctx.ok('the batch of still-valid operations applies', rejected);
    ctx.check('only the valid operation is in the batch',
      rejected.payload?.outcomeCount === 1 && rejected.payload?.failedCount === 0,
      `outcomeCount=${rejected.payload?.outcomeCount} failedCount=${rejected.payload?.failedCount}`);
    ctx.equal('the valid operation landed', await countTaps(), beforeRejected + 1);
    ctx.check('no lane with an invalid lane type was created',
      !(await api.family(editorId, 'lane')).some((l) => l.tGrid.totalGrid === T(24).tGridUnit * totalGridResolution + T(24).tGridGrid),
      `lanes=${(await api.family(editorId, 'lane')).length}`);

    // the rejected batch is still a single history entry
    const rejectedUndo = await api.undo({ editorId });
    ctx.ok('the surviving batch is undoable as one entry', rejectedUndo);
    ctx.equal('undoing the batch removes its single object', await countTaps(), beforeRejected);
    await api.redo({ editorId });
    await api.undo({ editorId });

    // ---------------- undo / redo tools themselves ----------------
    ctx.section('editor.undo / editor.redo');

    // Seed exactly one entry so the assertions below do not depend on how many actions the
    // earlier sections happened to leave on the stack (the invalid-op section ends reverted).
    const seedTaps = await countTaps();
    const seed = await add('tap', { ...T(25), xGridUnit: 0, xGridGrid: 0 });
    ctx.ok('history probe: one undoable entry was seeded', seed);

    const undoResponse = await api.undo({ editorId });
    ctx.ok('undo succeeds', undoResponse);
    ctx.hasKeys('undo response shape', undoResponse.payload, ['success', 'editorId', 'undone', 'undoCount', 'redoCount']);
    ctx.equal('undo removed the seeded object', await countTaps(), seedTaps);

    const redoResponse = await api.redo({ editorId });
    ctx.ok('redo succeeds', redoResponse);
    ctx.hasKeys('redo response shape', redoResponse.payload, ['success', 'editorId', 'redone', 'undoCount', 'redoCount', 'nextUndoName']);
    ctx.equal('redo restored the seeded object', await countTaps(), seedTaps + 1);

    ctx.check('undo then redo returns to the same depth',
      redoResponse.payload.undoCount === undoResponse.payload.undoCount + 1 && redoResponse.payload.redoCount === 0,
      `undo=${redoResponse.payload.undoCount} redo=${redoResponse.payload.redoCount}`);

    ctx.fails('undo against a stale expectedEditorId is rejected',
      await api.undo({ editorId, expectedEditorId: 'editor-not-this-one' }), 'EDITOR_CHANGED');
    ctx.fails('redo against a stale expectedEditorId is rejected',
      await api.redo({ editorId, expectedEditorId: 'editor-not-this-one' }), 'EDITOR_CHANGED');

    ctx.check('undo/redo expose requireConfirmation in their schema (host-side gate present)',
      (await ctx.client.listTools()).tools.filter((t) => ['editor.undo', 'editor.redo'].includes(t.name))
        .every((t) => 'requireConfirmation' in (t.inputSchema?.properties ?? {})));

    // clean up the seeded object. Note a reverted history always keeps one entry on the redo
    // stack — that is expected, and later suites establish their own baselines.
    await api.undo({ editorId });
    ctx.equal('history probe: the seeded object was removed again', await countTaps(), seedTaps);
  },
};
