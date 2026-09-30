// Suite 20 — the core object CRUD contract.
//
// editor.add_object / editor.modify_object / editor.remove_object are the tools every
// other feature is built on, so this suite pins:
//   * which families can be created, and what each one requires
//   * the failure codes for bad input (so callers get actionable errors)
//   * that a created object is really queryable through the DTO contract
//   * a full apply -> undo -> redo -> undo cycle for each of the three tools

import { verifyUndoRedo } from '../lib/history.mjs';
import { splitTotalGrid, totalGridResolution } from '../lib/env.mjs';

export default {
  name: '20-object-crud',
  description: 'add_object / modify_object / remove_object + undo/redo',

  async run(ctx) {
    const api = ctx.api;
    const editorId = ctx.editorId;
    ctx.cover('editor.add_object', 'editor.modify_object', 'editor.remove_object');

    const findById = async (family, objectId) =>
      (await api.family(editorId, family)).find((o) => o.id === objectId);

    const taps = await api.family(editorId, 'tap');
    ctx.check('the seed chart has taps to anchor the suite on', taps.length > 0, `count=${taps.length}`);
    const anchor = taps[0].tGrid.totalGrid;
    const T = (offset) => splitTotalGrid(anchor + offset * totalGridResolution);

    // a valid bullet pallete id from the seed chart (bullets must reference one)
    const palletes = await api.must(await api.queryBulletPallete({ editorId, limit: 1 }), 'query palletes');
    const palleteStrId = palletes.palletes?.[0]?.strId;
    ctx.check('the seed chart ships a bullet pallete to reference', !!palleteStrId, `strId=${palleteStrId}`);

    // ---------------- creation ----------------
    ctx.section('editor.add_object — creatable families');

    const creations = [
      ['tap', { ...T(1), xGridUnit: 0, xGridGrid: 0 }],
      ['flick', { ...T(1), xGridUnit: 0, xGridGrid: 0, direction: 'left' }],
      ['bell', { ...T(1), xGridUnit: 0, xGridGrid: 0 }],
      ['comment', { ...T(1), content: 'mcp-e2e' }],
      ['clickse', { ...T(1) }],
      ['bpm', { ...T(1), bpm: 123.45 }],
      ['meter', { ...T(1), meterBunShi: 3, meterBunbo: 4 }],
      ['enemy', { ...T(1) }],
      ['lane', { ...T(1), xGridUnit: 0, xGridGrid: 0, laneType: 'center' }],
      ['beam', { ...T(1), xGridUnit: 0, xGridGrid: 0 }],
      ['bullet', { ...T(1), xGridUnit: 0, xGridGrid: 0, bulletPalleteStrId: palleteStrId }],
      ['hold', { ...T(1), xGridUnit: 0, xGridGrid: 0 }],
      ['soflan', { ...T(1), soflanType: 'keyframe' }],
    ];

    const created = new Map();
    for (const [family, args] of creations) {
      const response = await api.addObject({ editorId, objectType: family, ...args });
      const payload = response.payload ?? {};
      ctx.check(`add_object('${family}') succeeds`, payload.success === true,
        `code=${payload.errorCode} msg=${String(payload.errorMessage ?? '').slice(0, 120)}`);
      if (payload.success !== true) continue;

      ctx.hasKeys(`add_object('${family}') response shape`, payload, ['success', 'editorId', 'objectType', 'objectId', 'applied', 'queued']);
      ctx.equal(`add_object('${family}') echoes the family`, payload.objectType, family);
      ctx.equal(`add_object('${family}') applies immediately outside a scope`, payload.applied, true);
      ctx.equal(`add_object('${family}') is not queued outside a scope`, payload.queued, false);

      created.set(family, payload.objectId);

      const dto = await findById(family === 'lane' || family === 'beam' ? family : family, payload.objectId);
      ctx.check(`the created ${family} is visible through query_object`, !!dto, `objectId=${payload.objectId}`);
      if (dto) {
        ctx.equal(`the ${family} DTO reports the requested TGrid`, dto.tGrid.totalGrid, T(1).tGridUnit * totalGridResolution + T(1).tGridGrid);
      }
    }

    // ---------------- creation failures ----------------
    ctx.section('editor.add_object — rejected input');

    ctx.fails('add_object rejects an unknown family',
      await api.addObject({ editorId, objectType: 'nope', ...T(1) }), 'UNSUPPORTED_OBJECT_TYPE');

    ctx.fails('add_object(bullet) without bulletPalleteStrId',
      await api.addObject({ editorId, objectType: 'bullet', ...T(2), xGridUnit: 0, xGridGrid: 0 }), 'MISSING_BULLET_PALLETE');

    // a non-numeric StrID used to escape as an unhandled exception; it must report a code
    ctx.fails('add_object(bullet) with a non-numeric bulletPalleteStrId',
      await api.addObject({ editorId, objectType: 'bullet', ...T(2), xGridUnit: 0, xGridGrid: 0, bulletPalleteStrId: 'does-not-exist' }), 'PALLETE_NOT_FOUND');
    ctx.fails('add_object(bullet) with an unknown numeric bulletPalleteStrId',
      await api.addObject({ editorId, objectType: 'bullet', ...T(2), xGridUnit: 0, xGridGrid: 0, bulletPalleteStrId: '99999' }), 'PALLETE_NOT_FOUND');
    ctx.fails('add_object(bullet) with a StrID that only collides numerically',
      await api.addObject({ editorId, objectType: 'bullet', ...T(2), xGridUnit: 0, xGridGrid: 0, bulletPalleteStrId: '!!!!' }), 'PALLETE_NOT_FOUND');

    ctx.fails('add_object(lane) with an unknown laneType',
      await api.addObject({ editorId, objectType: 'lane', ...T(2), xGridUnit: 0, xGridGrid: 0, laneType: 'nowhere' }), 'INVALID_ARGUMENT');

    ctx.fails('add_object into a missing editor',
      await api.addObject({ editorId: 'editor-missing', objectType: 'tap', ...T(2) }), 'EDITOR_NOT_FOUND');

    // optimistic concurrency
    ctx.fails('add_object rejects a stale expectedEditorId',
      await api.addObject({ editorId, expectedEditorId: 'editor-somebody-else', objectType: 'tap', ...T(2) }), 'EDITOR_CHANGED');

    // ---------------- modify ----------------
    ctx.section('editor.modify_object');

    const moveTarget = created.get('tap');
    ctx.check('a tap is available to modify', Number.isInteger(moveTarget), `objectId=${moveTarget}`);

    const moveTo = T(3);
    const moved = await api.modifyObject({ editorId, objectId: moveTarget, propertyName: 'tGridUnit', newValue: String(moveTo.tGridUnit) });
    ctx.ok('modify_object moves a tap on TGrid', moved);
    ctx.check('modify_object reports the previous value', (moved.payload ?? {}).oldValue !== undefined, `oldValue=${(moved.payload ?? {}).oldValue}`);

    const afterMove = await findById('tap', moveTarget);
    ctx.equal('the move is visible through query_object', afterMove?.tGrid.unit, moveTo.tGridUnit);

    const movedX = await api.modifyObject({ editorId, objectId: moveTarget, propertyName: 'xGridGrid', newValue: '96' });
    ctx.ok('modify_object changes XGrid', movedX);
    const afterX = await findById('tap', moveTarget);
    ctx.equal('the XGrid change is visible', afterX?.xGrid.grid, 96);

    ctx.fails('modify_object rejects an unknown property',
      await api.modifyObject({ editorId, objectId: moveTarget, propertyName: 'colour', newValue: '1' }), 'UNSUPPORTED_PROPERTY');

    ctx.fails('modify_object rejects an unknown objectId',
      await api.modifyObject({ editorId, objectId: 2147483000, propertyName: 'tGridUnit', newValue: '1' }), 'OBJECT_NOT_FOUND');

    ctx.fails('modify_object rejects an unparsable value',
      await api.modifyObject({ editorId, objectId: moveTarget, propertyName: 'tGridUnit', newValue: 'abc' }), 'INVALID_ARGUMENT');

    // cross-family property misuse must be rejected, not silently ignored
    ctx.fails('modify_object rejects widthId on a tap object',
      await api.modifyObject({ editorId, objectId: moveTarget, propertyName: 'widthId', newValue: '3' }), 'UNSUPPORTED_PROPERTY');
    ctx.fails('modify_object rejects blockDirection on a tap object',
      await api.modifyObject({ editorId, objectId: moveTarget, propertyName: 'blockDirection', newValue: 'left' }), 'UNSUPPORTED_PROPERTY');

    // ---------------- remove ----------------
    ctx.section('editor.remove_object');

    const doomed = await api.addObject({ editorId, objectType: 'tap', ...T(5), xGridUnit: 0, xGridGrid: 0 });
    ctx.ok('a tap for the removal case is created', doomed);
    const doomedId = doomed.payload.objectId;

    ctx.fails('remove_object rejects an unknown objectId',
      await api.removeObject({ editorId, objectId: 2147483000 }), 'OBJECT_NOT_FOUND');

    const removed = await api.removeObject({ editorId, objectId: doomedId });
    ctx.ok('remove_object removes the tap', removed);
    ctx.check('the tap is gone after removal', (await findById('tap', doomedId)) === undefined);

    // ---------------- undo / redo cycles ----------------
    ctx.section('undo / redo cycles');

    // add_object
    let addId = null;
    await verifyUndoRedo(ctx, {
      label: 'add_object(tap)',
      mutate: async () => {
        const response = await api.addObject({ editorId, objectType: 'tap', ...T(7), xGridUnit: 0, xGridGrid: 0 });
        addId = response.payload?.objectId;
        return response;
      },
      applied: async () => !!(await findById('tap', addId)),
      reverted: async () => (await findById('tap', addId)) === undefined,
      undoName: /Add tap/i,
      redoName: /Add tap/i,
    });

    // modify_object
    const modifyBase = await api.addObject({ editorId, objectType: 'tap', ...T(9), xGridUnit: 0, xGridGrid: 0 });
    ctx.ok('a tap for the modify cycle is created', modifyBase);
    const modifyId = modifyBase.payload.objectId;
    await verifyUndoRedo(ctx, {
      label: 'modify_object(tGridUnit)',
      mutate: () => api.modifyObject({ editorId, objectId: modifyId, propertyName: 'tGridUnit', newValue: String(T(11).tGridUnit) }),
      applied: async () => (await findById('tap', modifyId))?.tGrid.unit === T(11).tGridUnit,
      reverted: async () => (await findById('tap', modifyId))?.tGrid.unit === T(9).tGridUnit,
    });

    // remove_object — needs a dedicated victim, and one that currently exists
    const victim = await api.addObject({ editorId, objectType: 'tap', ...T(13), xGridUnit: 0, xGridGrid: 0 });
    ctx.ok('a tap for the removal cycle is created', victim);
    const victimId = victim.payload.objectId;

    await verifyUndoRedo(ctx, {
      label: 'remove_object(tap)',
      mutate: () => api.removeObject({ editorId, objectId: victimId }),
      applied: async () => (await findById('tap', victimId)) === undefined,
      reverted: async () => !!(await findById('tap', victimId)),
      undoName: /Remove tap|Delete tap/i,
    });

    // leave the chart as we found it: undo the leftover fixtures
    const leftovers = [addId, modifyId, victimId, ...created.values()].filter((id) => Number.isInteger(id));
    for (const id of leftovers) {
      await api.removeObject({ editorId, objectId: id });
    }
  },
};
