// Suite 70 — chart meta info and the built-in check rules.
//
// editor.set_metainfo is a normal undoable action; editor.check is the read-only companion
// the skill documentation tells agents to run after every mutation. The two belong
// together: `HeaderConstMismatch` is a deterministic check rule that reacts to meta info,
// which makes it the observability hook this suite uses to prove a meta info write (and
// its undo) really landed.

import { verifyUndoRedo } from '../lib/history.mjs';
import { splitTotalGrid, totalGridResolution } from '../lib/env.mjs';

const RULE = 'HeaderConstMismatch';

export default {
  name: '70-metainfo-check',
  description: 'set_metainfo / check + undo/redo',

  async run(ctx) {
    const api = ctx.api;
    const editorId = ctx.editorId;
    ctx.cover('editor.set_metainfo', 'editor.check');

    /** Count how many HeaderConstMismatch errors the chart currently has. */
    const headerMismatches = async () => {
      const payload = await api.must(await api.check({ editorId, limit: 1000 }), 'run check');
      return payload.results.filter((r) => String(r.ruleName).includes(RULE)).length;
    };

    // ---------------- editor.check contract ----------------
    ctx.section('editor.check');

    const check = await api.check({ editorId, limit: 200 });
    const payload = check.payload ?? {};
    ctx.ok('editor.check succeeds', check);
    ctx.hasKeys('check response shape', payload,
      ['success', 'editorId', 'ruleCount', 'minSeverity', 'total', 'errorCount', 'problemCount', 'suggestCount', 'matched', 'returned', 'truncated', 'results', 'ruleFailures']);

    // WallConflictCheckRule ships without [Export] and is therefore a dead rule: 22, not 23.
    ctx.equal('the host runs the expected number of check rules', payload.ruleCount, 22);
    ctx.isArray('every rule that ran reports back', payload.ruleFailures, {});
    ctx.equal('no rule blew up', payload.ruleFailures.length, 0);
    ctx.equal('total equals the number of matched results when nothing is truncated', payload.total >= payload.matched, true);
    ctx.check('severity buckets add up to the total',
      payload.errorCount + payload.problemCount + payload.suggestCount === payload.total,
      `${payload.errorCount}+${payload.problemCount}+${payload.suggestCount} vs ${payload.total}`);

    if (payload.results.length > 0) {
      const first = payload.results[0];
      ctx.hasKeys('check results carry ruleName / severity / description / location', first, ['ruleName', 'severity', 'description', 'location']);
      const rank = { error: 0, problem: 1, suggest: 2 };
      let ordered = true;
      for (let i = 1; i < payload.results.length; i++) {
        if (rank[payload.results[i - 1].severity] > rank[payload.results[i].severity]) { ordered = false; break; }
      }
      ctx.check('results are ordered by severity (error -> problem -> suggest)', ordered);
    }

    const limited = await api.check({ editorId, limit: 3 });
    ctx.equal('limit bounds the returned results', limited.payload.results.length, 3);
    ctx.equal('a bounded check reports the truncation', limited.payload.truncated, true);
    ctx.check('a bounded check still reports the full match count', limited.payload.matched >= limited.payload.results.length,
      `matched=${limited.payload.matched} returned=${limited.payload.returned}`);

    const errorsOnly = await api.check({ editorId, minSeverity: 'error' });
    ctx.check('minSeverity=error filters out lower severities',
      errorsOnly.payload.results.every((r) => r.severity === 'error'),
      `severities=${[...new Set(errorsOnly.payload.results.map((r) => r.severity))]}`);
    ctx.equal('minSeverity=error reports only error severity',
      errorsOnly.payload.minSeverity, 'error');
    ctx.equal('minSeverity filters suggestions out of the counts', errorsOnly.payload.suggestCount, 0);

    ctx.fails('an unknown minSeverity is rejected',
      await api.check({ editorId, minSeverity: 'catastrophe' }), 'INVALID_ARGUMENT');

    // editor.check is read-only. A new history entry would discard the redo stack, so the
    // strict observable is: undo once, run check, then redo — the redo must still be there.
    const beforeCheck = await api.undo({ editorId });
    ctx.check('a redo entry exists before running check', beforeCheck.payload?.redoCount === 1,
      `redoCount=${beforeCheck.payload?.redoCount}`);

    const afterCheck = await api.check({ editorId, limit: 5 });
    ctx.ok('checking the chart still works after an undo', afterCheck);

    const afterRedo = await api.redo({ editorId });
    ctx.check('editor.check did not discard the redo stack',
      typeof afterRedo.payload?.redone === 'string' && afterRedo.payload.redone.length > 0,
      `redone=${afterRedo.payload?.redone} redoCount=${afterRedo.payload?.redoCount}`);

    // ---------------- set_metainfo contract ----------------
    ctx.section('editor.set_metainfo');

    const baselineMismatches = await headerMismatches();
    ctx.check('the header-mismatch baseline was measured', baselineMismatches >= 0, `baseline=${baselineMismatches}`);

    ctx.fails('set_metainfo rejects an unknown field',
      await api.setMetainfo({ editorId, metainfoName: 'favouriteColour', newValue: 'blue' }), 'UNSUPPORTED_METAINFO');

    ctx.fails('set_metainfo rejects an unparsable value',
      await api.setMetainfo({ editorId, metainfoName: 'tResolution', newValue: 'not-a-number' }), 'INVALID_ARGUMENT');

    ctx.fails('set_metainfo rejects a bad boolean',
      await api.setMetainfo({ editorId, metainfoName: 'tutorial', newValue: 'maybe' }), 'INVALID_ARGUMENT');

    const creatorWrite = await api.setMetainfo({ editorId, metainfoName: 'creator', newValue: 'MCP-E2E' });
    ctx.ok('set_metainfo writes a string field', creatorWrite);
    ctx.hasKeys('set_metainfo response shape', creatorWrite.payload,
      ['success', 'editorId', 'metainfoName', 'valueType', 'oldValue', 'newValue', 'applied', 'queued']);
    ctx.equal('the response echoes the field name', creatorWrite.payload.metainfoName, 'creator');
    ctx.equal('the response echoes the new value', creatorWrite.payload.newValue, 'MCP-E2E');
    ctx.equal('the response reports the value type', creatorWrite.payload.valueType, 'string');
    ctx.equal('the write applied immediately outside a scope', creatorWrite.payload.applied, true);

    // ---------------- every documented field is writable ----------------
    ctx.section('every documented meta info field is writable');

    const fieldValues = {
      creator: 'MCP-E2E',
      version: '1.0.0',
      bpmFirst: '175.5',
      bpmCommon: '180',
      bpmMinimum: '120',
      bpmMaximum: '240',
      meterBunshi: '3',
      meterBunbo: '4',
      tResolution: '1920',
      xResolution: '1000',
      clickDefinition: '1',
      tutorial: 'true',
      bulletDamage: '1',
      hardBulletDamage: '2',
      dangerBulletDamage: '3',
      beamDamage: '4',
      progJudgeBpm: '180',
    };

    // Batch them through an action scope and discard, so this stays a pure contract probe.
    const begin = await api.beginAction({ editorId });
    ctx.ok('a scope is opened for the field sweep', begin);
    for (const [metainfoName, newValue] of Object.entries(fieldValues)) {
      const response = await api.setMetainfo({ editorId, metainfoName, newValue });
      const body = response.payload ?? {};
      ctx.check(`set_metainfo accepts '${metainfoName}'`,
        body.success === true && body.queued === true,
        `code=${body.errorCode} msg=${String(body.errorMessage ?? '').slice(0, 130)}`);
    }
    const discard = await api.endAction({ editorId, name: 'e2e metainfo sweep', discard: true });
    ctx.ok('the field sweep is discarded without applying', discard);
    ctx.equal('the sweep left the chart untouched', discard.payload?.applied, false);

    // ---------------- derived synchronisation ----------------
    ctx.section('derived values stay in sync');

    const bpmBefore = (await api.family(editorId, 'bpm'))[0];
    ctx.check('the chart exposes a first bpm entry', !!bpmBefore, `first=${JSON.stringify(bpmBefore)}`);
    ctx.check('the bpm DTO reports the BPM value', typeof bpmBefore?.bpm === 'number', `bpm=${bpmBefore?.bpm}`);

    await api.setMetainfo({ editorId, metainfoName: 'bpmFirst', newValue: '175.5' });
    const bpmAfter = (await api.family(editorId, 'bpm'))[0];
    ctx.equal('bpmFirst also updates the chart\'s first BPM', bpmAfter?.bpm, 175.5);
    await api.undo({ editorId });
    ctx.equal('undo restores the original first BPM', (await api.family(editorId, 'bpm'))[0]?.bpm, bpmBefore?.bpm);

    const meterBefore = (await api.family(editorId, 'meter'))[0];
    ctx.check('the chart exposes a first meter entry', !!meterBefore, `first=${JSON.stringify(meterBefore)}`);

    await api.setMetainfo({ editorId, metainfoName: 'meterBunshi', newValue: '3' });
    await api.setMetainfo({ editorId, metainfoName: 'meterBunbo', newValue: '4' });
    const meterAfter = (await api.family(editorId, 'meter'))[0];
    ctx.check('meterBunshi also updates the chart\'s first meter',
      meterAfter?.meterBunShi === 3 && meterAfter?.meterBunbo === 4,
      `meterBunShi=${meterAfter?.meterBunShi} meterBunbo=${meterAfter?.meterBunbo}`);
    await api.undo({ editorId });
    await api.undo({ editorId });
    const meterRestored = (await api.family(editorId, 'meter'))[0];
    ctx.check('undo restores the original first meter',
      meterRestored?.meterBunShi === meterBefore?.meterBunShi && meterRestored?.meterBunbo === meterBefore?.meterBunbo,
      `meterBunShi=${meterRestored?.meterBunShi} meterBunbo=${meterRestored?.meterBunbo}`);

    // ---------------- a meta info write is observable through a check rule ----------------
    ctx.section('a meta info write is observable through editor.check');

    await verifyUndoRedo(ctx, {
      label: 'set_metainfo(xResolution)',
      mutate: () => api.setMetainfo({ editorId, metainfoName: 'xResolution', newValue: '1000' }),
      applied: async () => (await headerMismatches()) === baselineMismatches + 1,
      reverted: async () => (await headerMismatches()) === baselineMismatches,
    });

    ctx.equal('the chart is back to its baseline mismatch count', await headerMismatches(), baselineMismatches);

    // and the xResolution range must survive a check call without drift
    const stable = await api.check({ editorId, limit: 1000 });
    ctx.check('editor.check is stable across repeated calls',
      stable.payload.results.filter((r) => String(r.ruleName).includes(RULE)).length === baselineMismatches,
      `count=${stable.payload.results.filter((r) => String(r.ruleName).includes(RULE)).length}`);

    // ---------------- get_metainfo read-back ----------------
    ctx.section('editor.get_metainfo');
    ctx.cover('editor.get_metainfo');

    ctx.fails('get_metainfo rejects an unknown field',
      await api.getMetainfo({ editorId, metainfoName: 'favouriteColour' }), 'UNSUPPORTED_METAINFO');

    // read-back for a write: set_metainfo reports oldValue, get_metainfo must report the new value
    await api.setMetainfo({ editorId, metainfoName: 'creator', newValue: 'MCP-GET-E2E' });
    const single = await api.getMetainfo({ editorId, metainfoName: 'creator' });
    ctx.ok('get_metainfo reads a single field', single);
    ctx.hasKeys('get_metainfo single-field response shape', single.payload, ['success', 'editorId', 'metainfoName', 'valueType', 'value']);
    ctx.equal('the single-field read echoes the field name', single.payload.metainfoName, 'creator');
    ctx.equal('the single-field read reports the value type', single.payload.valueType, 'string');
    ctx.equal('the single-field read returns what set_metainfo wrote', single.payload.value, 'MCP-GET-E2E');

    const all = await api.getMetainfo({ editorId });
    ctx.ok('get_metainfo reads every field when the name is omitted', all);
    ctx.hasKeys('get_metainfo all-fields response shape', all.payload, ['success', 'editorId', 'fields']);
    ctx.isArray('get_metainfo returns one entry per supported field', all.payload.fields, { length: 17 });
    ctx.check('every field entry carries name/valueType/value',
      all.payload.fields.every((f) => typeof f.name === 'string' && typeof f.valueType === 'string' && typeof f.value === 'string'));
    const byName = new Map(all.payload.fields.map((f) => [f.name, f]));
    ctx.equal('the all-fields read sees the creator write', byName.get('creator')?.value, 'MCP-GET-E2E');

    // cross-check one field against a different tool: bpmFirst is the chart's first BPM entry
    const firstBpm = (await api.family(editorId, 'bpm'))[0];
    ctx.equal('the reported bpmFirst matches the chart\'s first BPM entry', Number(byName.get('bpmFirst')?.value), firstBpm?.bpm);

    // read-only proof: a read between an undo and its redo must consume neither stack
    const undoAfterSet = await api.undo({ editorId });
    ctx.ok('the creator write is undoable', undoAfterSet);
    const readAfterUndo = await api.getMetainfo({ editorId, metainfoName: 'creator' });
    ctx.equal('the read reflects the undo', readAfterUndo.payload?.value, 'MCP-E2E');
    const redoAfterRead = await api.redo({ editorId });
    ctx.check('a read does not consume the redo stack',
      typeof redoAfterRead.payload?.redone === 'string' && /creator/i.test(redoAfterRead.payload.redone),
      `redone=${redoAfterRead.payload?.redone}`);
    ctx.equal('the read returns the redone value', (await api.getMetainfo({ editorId, metainfoName: 'creator' })).payload?.value, 'MCP-GET-E2E');
    await api.undo({ editorId });

    // ---------------- horizontal lane segments accept an X interval ----------------
    // A horizontal segment — every point shares one TGrid and only X moves; the WLS/WLE pair of a
    // wall that exists for a single instant is the real-world shape — covers an X *interval* at
    // that moment, so an object docked anywhere inside it sits on the lane. WrongLocation used to
    // compare against the single X the interpolation picks for a zero-length segment (the start
    // key), which reported a tap resting on the end key as misplaced.
    ctx.section('WrongLocation accepts objects sitting on a horizontal lane segment');

    const horizontalTGrid = 61 * totalGridResolution;
    const atHorizontal = splitTotalGrid(horizontalTGrid);

    const wall = await api.must(await api.addObject({
      editorId, objectType: 'lane', laneType: 'wallRight',
      ...atHorizontal, xGridUnit: 22, xGridGrid: 0,
    }), 'create the wall lane');
    const wallRecordId = (await api.family(editorId, 'lane')).find((lane) => lane.id === wall.objectId)?.recordId;
    ctx.check('the new wall lane reports its RecordId', Number.isInteger(wallRecordId), `recordId=${wallRecordId}`);

    await api.must(await api.addObject({
      editorId, objectType: 'lanenext', parentRecordId: wallRecordId,
      ...atHorizontal, xGridUnit: 20, xGridGrid: 0,
    }), 'add a zero-length segment (same TGrid as the start, different X)');

    const dockedTap = async (xGridUnit) => api.must(await api.addObject({
      editorId, objectType: 'tap', referenceLaneRecordId: wallRecordId,
      ...atHorizontal, xGridUnit, xGridGrid: 0,
    }), `dock a tap at x=${xGridUnit}`);

    const endKeyTap = await dockedTap(20);   // the segment's end key
    const insideTap = await dockedTap(21);   // inside the segment's X interval
    const outsideTap = await dockedTap(24);  // beyond the interval (and its 1 unit tolerance)

    const wrongLocations = (await api.must(await api.check({ editorId, limit: 1000 }), 'run check'))
      .results.filter((result) => result.ruleName === 'WrongLocation');
    const isWrongLocation = (objectId) => wrongLocations.some((result) => Number(result.objectId) === objectId);

    ctx.equal('a tap on the segment end key is not reported', isWrongLocation(endKeyTap.objectId), false);
    ctx.equal('a tap inside the segment X interval is not reported', isWrongLocation(insideTap.objectId), false);
    ctx.equal('a tap outside the segment X interval is still reported', isWrongLocation(outsideTap.objectId), true);

    for (let undone = 0; undone < 5; undone++)
      await api.undo({ editorId });
    const leftover = (await api.family(editorId, 'tap', { minTotalGrid: horizontalTGrid, maxTotalGrid: horizontalTGrid }))
      .filter((tap) => tap.xGrid && [20, 21, 24].includes(Number(tap.xGrid.unit)));
    ctx.equal('the section leaves no taps behind', leftover.length, 0);

    // ---------------- ObjectTimelineNotAligned: lattice test, not "1/n only" ----------------
    // The rule used to accept only 1/n and (n-1)/n fractions of a beat, computed in doubles — which
    // also rejected exact triplets once the beat index grew (1/3 -> 3.0000000000000426). It now
    // accepts any offset whose gcd with the beat length reaches 10 grids (4/4) or 1/48 of the beat.
    // These taps live in the seeded chart's first segment (MET_DEF 4/4, start T[0,0], before the
    // chart's first MET change), so the offset inside a beat is exactly the grid value.
    ctx.section('ObjectTimelineNotAligned accepts regular grid points, still flags off-grid ones');

    const tripleTap = await api.must(await api.addObject({
      editorId, objectType: 'tap', ...splitTotalGrid(80), xGridUnit: 0, xGridGrid: 0,
    }), 'add a 1/6-beat (triplet) tap');
    const threeEighthsTap = await api.must(await api.addObject({
      editorId, objectType: 'tap', ...splitTotalGrid(180), xGridUnit: 0, xGridGrid: 0,
    }), 'add a 3/8-beat tap');
    const offGridTap = await api.must(await api.addObject({
      editorId, objectType: 'tap', ...splitTotalGrid(5), xGridUnit: 0, xGridGrid: 0,
    }), 'add a 5-grid tap');

    const notAligned = (await api.must(await api.check({ editorId, limit: 1000 }), 'run check'))
      .results.filter((result) => result.ruleName === 'ObjectTimelineNotAligned');
    const isNotAligned = (objectId) => notAligned.some((result) => Number(result.objectId) === objectId);

    ctx.equal('a 1/6-beat (triplet) position is not reported', isNotAligned(tripleTap.objectId), false);
    ctx.equal('a 3/8-beat position is not reported', isNotAligned(threeEighthsTap.objectId), false);
    ctx.equal('a 5-grid drag is still reported', isNotAligned(offGridTap.objectId), true);

    for (let undone = 0; undone < 3; undone++)
      await api.undo({ editorId });
    const alignLeftover = (await api.family(editorId, 'tap', { minTotalGrid: 0, maxTotalGrid: 479 }))
      .filter((tap) => [5, 80, 180].includes(Number(tap.tGrid?.grid)));
    ctx.equal('the lattice section leaves no taps behind', alignLeftover.length, 0);
  },
};
