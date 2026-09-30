// Suite 10 — read-only discovery: editor.get_current, editor.list_opened,
// editor.get_current_summary, editor.query_object.
//
// These four are the only tools an agent can use before deciding what to edit, so the
// suite pins their exact response shapes (two of them return bare values, not the usual
// `{success:...}` envelope) plus query_object's paging and filtering contract.

export default {
  name: '10-discovery',
  description: 'get_current / list_opened / get_current_summary / query_object',

  async run(ctx) {
    const api = ctx.api;
    const editorId = ctx.editorId;
    ctx.cover('editor.get_current', 'editor.list_opened', 'editor.get_current_summary', 'editor.query_object');

    // ---------------- editor.get_current ----------------
    ctx.section('editor.get_current');
    const current = (await api.getCurrent()).payload;
    ctx.check('get_current returns a bare editor object (no success envelope)', current && current.success === undefined);
    ctx.equal('get_current reports the editor we opened', current?.editorId, editorId);
    ctx.equal('get_current marks the editor active', current?.isActive, true);
    ctx.equal('get_current reports a clean chart right after opening', current?.isDirty, false);
    ctx.check('get_current carries the fumen path', String(current?.fumenPath).endsWith('seed.ogkr'), `fumenPath=${current?.fumenPath}`);
    ctx.hasKeys('get_current carries the per-family counters', current,
      ['laneCount', 'tapCount', 'holdCount', 'bellCount', 'bulletCount', 'bpmChangeCount', 'soflanCount']);

    // ---------------- editor.list_opened ----------------
    ctx.section('editor.list_opened');
    const opened = (await api.listOpened()).payload;
    ctx.check('list_opened returns a bare array (no success envelope)', Array.isArray(opened), `type=${typeof opened}`);
    ctx.equal('list_opened sees exactly the one seeded editor', opened?.length, 1);
    ctx.equal('list_opened reports the same editor id', opened?.[0]?.editorId, editorId);
    ctx.hasKeys('list_opened entries carry the same counters as get_current', opened?.[0],
      ['editorId', 'displayName', 'fumenPath', 'isDirty', 'isActive', 'tapCount', 'laneCount']);

    // ---------------- editor.get_current_summary ----------------
    ctx.section('editor.get_current_summary');
    const summary = (await api.getCurrentSummary()).payload;
    ctx.ok('get_current_summary succeeds', { payload: summary });
    ctx.equal('get_current_summary reports the editor id', summary?.editorId, editorId);
    ctx.check('get_current_summary nests the counters under "counts"', !!summary?.counts && typeof summary.counts === 'object');
    ctx.equal('summary tap count matches get_current', summary?.counts?.taps, current?.tapCount);
    ctx.equal('summary lane count matches get_current', summary?.counts?.lanes, current?.laneCount);
    ctx.equal('summary hold count matches get_current', summary?.counts?.holds, current?.holdCount);
    ctx.equal('summary bpm count matches get_current', summary?.counts?.bpmChanges, current?.bpmChangeCount);
    ctx.equal('summary soflan count matches get_current', summary?.counts?.soflans, current?.soflanCount);

    // ---------------- editor.query_object ----------------
    ctx.section('editor.query_object');

    const unknown = await api.queryObject({ editorId, objectType: 'not_a_family' });
    ctx.fails('query_object rejects an unknown family', unknown, 'UNSUPPORTED_OBJECT_TYPE');

    const badCursor = await api.queryObject({ editorId, objectType: 'tap', cursor: 'nonsense' });
    ctx.fails('query_object rejects a malformed cursor', badCursor, 'INVALID_CURSOR');

    const missingEditor = await api.queryObject({ editorId: 'editor-does-not-exist', objectType: 'tap' });
    ctx.fails('query_object rejects an unknown editorId', missingEditor, 'EDITOR_NOT_FOUND');

    // every queryable family must answer
    const families = ['tap', 'flick', 'hold', 'bell', 'bullet', 'comment', 'bpm', 'meter', 'clickse', 'enemy', 'lane', 'soflan',
      'lanenext', 'beam', 'beamnext', 'curvecontrol', 'isfarea', 'laneblock'];
    for (const family of families) {
      const response = await api.queryObject({ editorId, objectType: family, limit: 1 });
      const payload = response.payload ?? {};
      ctx.check(`query_object accepts family '${family}'`,
        payload.success === true && payload.objectType === family,
        `code=${payload.errorCode} objectType=${payload.objectType}`);
    }

    const allTaps = await api.queryObject({ editorId, objectType: 'tap', limit: 2000 });
    ctx.ok('query_object returns a full page of taps', allTaps);
    const allTapObjects = allTaps.payload.objects;
    ctx.check('the seed chart really has taps to work with', allTapObjects.length > 50, `count=${allTapObjects.length}`);
    ctx.equal('query_object echoes the normalized family name', allTaps.payload.objectType, 'tap');
    ctx.equal('query_object reports count === objects.length', allTaps.payload.count, allTapObjects.length);
    ctx.hasKeys('tap DTOs carry the documented fields', allTapObjects[0], ['id', 'tGrid', 'xGrid', 'isCritical', 'referenceLaneRecordId']);
    ctx.hasKeys('tGrid sub-document is unit/grid/totalGrid', allTapObjects[0]?.tGrid, ['unit', 'grid', 'totalGrid']);

    // ordering contract: TGrid is the only guaranteed sort key. Objects sharing a TGrid keep
    // the chart's internal order, which is stable but NOT sorted by id — do not assert on it.
    let tGridOrdered = true;
    for (let i = 1; i < allTapObjects.length; i++) {
      if (allTapObjects[i - 1].tGrid.totalGrid > allTapObjects[i].tGrid.totalGrid) { tGridOrdered = false; break; }
    }
    ctx.check('query_object orders by TGrid', tGridOrdered, `count=${allTapObjects.length}`);

    const tieCount = allTapObjects.filter((o, i) => i > 0 && allTapObjects[i - 1].tGrid.totalGrid === o.tGrid.totalGrid).length;
    ctx.check('objects sharing a TGrid are present (so the tie case is exercised)', tieCount > 0, `ties=${tieCount}`);

    // paging contract
    const page1 = await api.queryObject({ editorId, objectType: 'tap', limit: 5 });
    ctx.equal('a bounded query returns exactly `limit` objects', page1.payload.count, 5);
    ctx.equal('a bounded query flags truncation', page1.payload.truncated, true);
    ctx.check('a truncated query hands back a cursor', typeof page1.payload.nextCursor === 'string' && page1.payload.nextCursor.includes(':'), `nextCursor=${page1.payload.nextCursor}`);

    const page2 = await api.queryObject({ editorId, objectType: 'tap', limit: 5, cursor: page1.payload.nextCursor });
    ctx.equal('the next page returns another `limit` objects', page2.payload.count, 5);
    const overlap = page2.payload.objects.filter((o) => page1.payload.objects.some((p) => p.id === o.id));
    ctx.equal('paging does not repeat objects', overlap.length, 0);
    ctx.check('the second page continues after the first',
      page2.payload.objects[0].tGrid.totalGrid > page1.payload.objects[0].tGrid.totalGrid,
      `first=${page1.payload.objects[0].tGrid.totalGrid} next=${page2.payload.objects[0].tGrid.totalGrid}`);

    // walking every page must reproduce the single-shot result exactly, in order, once each
    const walked = [];
    let cursor;
    let pages = 0;
    do {
      const page = await api.queryObject({ editorId, objectType: 'tap', limit: 7, cursor });
      if (page.payload?.success !== true) { ctx.check('paging walk stays successful', false, `code=${page.payload?.errorCode}`); break; }
      walked.push(...page.payload.objects);
      cursor = page.payload.nextCursor;
      pages++;
    } while (cursor && pages < 500);

    ctx.equal('walking every page visits each tap exactly once', walked.length, allTapObjects.length);
    ctx.equal('walking every page yields the same order as one big query',
      walked.map((o) => o.id).join(','), allTapObjects.map((o) => o.id).join(','));
    ctx.check('paging needed more than one round trip', pages > 1, `pages=${pages}`);

    // limit clamping
    const clampedLow = await api.queryObject({ editorId, objectType: 'tap', limit: 0 });
    ctx.equal('limit is clamped up to 1', clampedLow.payload.count, 1);
    const clampedHigh = await api.queryObject({ editorId, objectType: 'tap', limit: 99999 });
    ctx.check('limit is clamped down to 2000', clampedHigh.payload.count <= 2000, `count=${clampedHigh.payload.count}`);

    // range filtering
    const middle = allTapObjects[Math.floor(allTapObjects.length / 2)].tGrid.totalGrid;
    const ranged = await api.queryObject({ editorId, objectType: 'tap', minTotalGrid: middle, limit: 2000 });
    const outOfRange = ranged.payload.objects.filter((o) => o.tGrid.totalGrid < middle);
    ctx.equal('minTotalGrid is honoured', outOfRange.length, 0);
    ctx.check('minTotalGrid actually narrows the result', ranged.payload.count < allTapObjects.length, `${ranged.payload.count} < ${allTapObjects.length}`);

    const bounded = await api.queryObject({ editorId, objectType: 'tap', minTotalGrid: middle, maxTotalGrid: middle , limit: 2000 });
    const outside = bounded.payload.objects.filter((o) => o.tGrid.totalGrid !== middle);
    ctx.equal('maxTotalGrid is honoured (inclusive bounds)', outside.length, 0);
  },
};
