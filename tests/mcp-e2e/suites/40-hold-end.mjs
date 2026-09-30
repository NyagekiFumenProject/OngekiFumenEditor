// Suite 40 — hold ends.
//
// A hold starts life as a single point; `editor.create_hold_end` gives it length and
// `editor.remove_hold_end` takes the length away again. Both are single undoable actions,
// and both must refuse the wrong kind of target rather than corrupt the chart.
//
// Each scenario gets its own hold so the suite never depends on the order the sections run in.

import { verifyUndoRedo } from '../lib/history.mjs';
import { splitTotalGrid, totalGridResolution } from '../lib/env.mjs';

export default {
  name: '40-hold-end',
  description: 'create_hold_end / remove_hold_end + undo/redo',

  async run(ctx) {
    const api = ctx.api;
    const editorId = ctx.editorId;
    ctx.cover('editor.create_hold_end', 'editor.remove_hold_end', 'editor.add_object', 'editor.remove_object');

    const findHold = async (id) => (await api.family(editorId, 'hold')).find((h) => h.id === id);
    const totalOf = (t) => t.tGridUnit * totalGridResolution + t.tGridGrid;

    const taps = await api.family(editorId, 'tap');
    const base = taps[0].tGrid.totalGrid;
    const T = (n) => splitTotalGrid(base + n * totalGridResolution);

    /** A fresh hold with no end, placed at its own TGrid. */
    const newHold = async (n) => {
      const response = await api.addObject({ editorId, objectType: 'hold', ...T(n), xGridUnit: 0, xGridGrid: 0 });
      ctx.ok(`a hold at T[${n}] is created`, response);
      return response.payload.objectId;
    };

    const cleanup = [];

    // ---------------- the DTO only reports an end once there is one ----------------
    ctx.section('hold DTO');

    const holdA = await newHold(1);
    cleanup.push(holdA);
    const fresh = await findHold(holdA);
    ctx.check('a hold DTO always exposes hasHoldEnd', fresh && 'hasHoldEnd' in fresh);
    ctx.equal('a new hold has no end yet', fresh.hasHoldEnd, false);
    ctx.equal('a hold without an end omits endTGrid', fresh.endTGrid, undefined);

    // ---------------- create_hold_end ----------------
    ctx.section('editor.create_hold_end');

    ctx.fails('create_hold_end on an unknown object id is rejected',
      await api.createHoldEnd({ holdObjectId: 2147483000, ...T(3) }), 'OBJECT_NOT_FOUND');

    const tapId = (await api.addObject({ editorId, objectType: 'tap', ...T(2), xGridUnit: 0, xGridGrid: 0 })).payload.objectId;
    cleanup.push(tapId);
    ctx.fails('create_hold_end on a tap is rejected',
      await api.createHoldEnd({ holdObjectId: tapId, ...T(3) }), 'NOT_A_HOLD');

    const endA = T(3);
    const created = await api.createHoldEnd({ holdObjectId: holdA, ...endA, xGridUnit: 0, xGridGrid: 0 });
    ctx.ok('create_hold_end attaches the end', created);
    ctx.hasKeys('create_hold_end response shape', created.payload,
      ['success', 'editorId', 'holdObjectId', 'holdEndObjectId', 'applied', 'queued']);
    ctx.equal('create_hold_end reports it applied', created.payload.applied, true);

    const withEnd = await findHold(holdA);
    ctx.equal('the hold now reports an end', withEnd.hasHoldEnd, true);
    ctx.equal('the hold end landed on the requested TGrid', withEnd.endTGrid?.totalGrid, totalOf(endA));
    ctx.hasKeys('the ended hold DTO now exposes endTGrid', withEnd, ['endTGrid']);

    ctx.fails('create_hold_end refuses a hold that already has an end',
      await api.createHoldEnd({ holdObjectId: holdA, ...T(5) }), 'HOLD_END_ALREADY_EXISTS');
    ctx.equal('the refusal left the original end in place', (await findHold(holdA))?.endTGrid?.totalGrid, totalOf(endA));

    // ---------------- create_hold_end undo / redo ----------------
    ctx.section('create_hold_end undo / redo');

    const holdB = await newHold(6);
    cleanup.push(holdB);
    const endB = T(7);
    await verifyUndoRedo(ctx, {
      label: 'create_hold_end',
      mutate: () => api.createHoldEnd({ holdObjectId: holdB, ...endB, xGridUnit: 0, xGridGrid: 0 }),
      applied: async () => (await findHold(holdB))?.endTGrid?.totalGrid === totalOf(endB),
      reverted: async () => (await findHold(holdB))?.hasHoldEnd === false,
    });

    // ---------------- remove_hold_end ----------------
    ctx.section('editor.remove_hold_end');

    ctx.fails('remove_hold_end on an unknown object id is rejected',
      await api.removeHoldEnd({ holdObjectId: 2147483000 }), 'OBJECT_NOT_FOUND');
    ctx.fails('remove_hold_end on a tap is rejected',
      await api.removeHoldEnd({ holdObjectId: tapId }), 'NOT_A_HOLD');

    const holdC = await newHold(9);
    cleanup.push(holdC);
    ctx.fails('remove_hold_end on a zero-length hold is rejected',
      await api.removeHoldEnd({ holdObjectId: holdC }), 'HOLD_END_NOT_FOUND');

    const removed = await api.removeHoldEnd({ holdObjectId: holdA });
    ctx.ok('remove_hold_end detaches the end', removed);
    ctx.hasKeys('remove_hold_end response shape', removed.payload,
      ['success', 'editorId', 'holdObjectId', 'removedHoldEndObjectId', 'applied', 'queued']);
    ctx.equal('the hold is zero-length again', (await findHold(holdA))?.hasHoldEnd, false);
    ctx.equal('removing twice in a row is refused',
      (await api.removeHoldEnd({ holdObjectId: holdA })).payload?.errorCode, 'HOLD_END_NOT_FOUND');

    // ---------------- remove_hold_end undo / redo ----------------
    ctx.section('remove_hold_end undo / redo');

    const holdD = await newHold(11);
    cleanup.push(holdD);
    const endD = T(12);
    await api.createHoldEnd({ holdObjectId: holdD, ...endD, xGridUnit: 0, xGridGrid: 0 });
    ctx.equal('the hold for the removal cycle starts with an end', (await findHold(holdD))?.hasHoldEnd, true);

    await verifyUndoRedo(ctx, {
      label: 'remove_hold_end',
      mutate: () => api.removeHoldEnd({ holdObjectId: holdD }),
      applied: async () => (await findHold(holdD))?.hasHoldEnd === false,
      reverted: async () => (await findHold(holdD))?.endTGrid?.totalGrid === totalOf(endD),
    });

    // ---------------- the end can also be moved like any other property ----------------
    ctx.section('the end is editable through modify_object');

    const moved = await api.modifyObject({ editorId, objectId: holdD, propertyName: 'endTGridUnit', newValue: String(T(14).tGridUnit) });
    ctx.ok('endTGridUnit is writable', moved);
    ctx.equal('the moved end is visible', (await findHold(holdD))?.endTGrid?.unit, T(14).tGridUnit);

    await verifyUndoRedo(ctx, {
      label: 'modify_object(hold.endTGridUnit)',
      mutate: () => api.modifyObject({ editorId, objectId: holdD, propertyName: 'endTGridUnit', newValue: String(T(16).tGridUnit) }),
      applied: async () => (await findHold(holdD))?.endTGrid?.unit === T(16).tGridUnit,
      reverted: async () => (await findHold(holdD))?.endTGrid?.unit === T(14).tGridUnit,
    });

    const noEndWrite = await api.modifyObject({ editorId, objectId: holdC, propertyName: 'endTGridUnit', newValue: '10' });
    ctx.check('writing endTGridUnit on an end-less hold does not silently succeed',
      noEndWrite.payload?.success === false,
      `success=${noEndWrite.payload?.success} code=${noEndWrite.payload?.errorCode}`);

    // ---------------- cleanup ----------------
    for (const objectId of cleanup) await api.removeObject({ editorId, objectId });
  },
};
