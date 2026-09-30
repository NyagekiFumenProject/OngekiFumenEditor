// Suite 60 — bullet palletes (BPL).
//
// A bullet pallete is a shared projectile template: bullets and bells reference it by
// StrID. That back-reference is what makes this family special — a pallete still in use
// must be refused on removal, and the query in single-StrID mode must report the objects
// that point at it.

import { verifyUndoRedo } from '../lib/history.mjs';

const UNUSED_PREFIX = 'e2e';

export default {
  name: '60-bullet-pallete',
  description: 'create / query / modify / remove_bullet_pallete + undo/redo',

  async run(ctx) {
    const api = ctx.api;
    const editorId = ctx.editorId;
    ctx.cover('editor.create_bullet_pallete', 'editor.query_bullet_pallete',
      'editor.modify_bullet_pallete', 'editor.remove_bullet_pallete', 'editor.add_object');

    const created = [];

    // ---------------- create ----------------
    ctx.section('editor.create_bullet_pallete');

    const auto = await api.createBulletPallete({
      editorId, editorName: `${UNUSED_PREFIX} auto`, shooter: 'Center', target: 'FixField',
      size: 'Normal', type: 'Circle', speed: 1.5,
    });
    ctx.ok('create_bullet_pallete without an explicit strId succeeds', auto);
    ctx.hasKeys('create response exposes the allocated StrID', auto.payload, ['success', 'editorId', 'strId']);
    ctx.check('the host allocated a non-empty StrID', typeof auto.payload.strId === 'string' && auto.payload.strId.length > 0, `strId=${auto.payload.strId}`);
    created.push(auto.payload.strId);

    const explicitStrId = '9001';
    const explicit = await api.createBulletPallete({
      editorId, strId: explicitStrId, editorName: `${UNUSED_PREFIX} explicit`, shooter: 'Enemy', target: 'Player',
      size: 'Large', type: 'Needle', speed: 2, placeOffset: 8, randomOffsetRange: 4,
    });
    ctx.ok('create_bullet_pallete with an explicit strId succeeds', explicit);
    ctx.equal('the requested StrID is honoured', explicit.payload.strId, explicitStrId);
    created.push(explicitStrId);

    ctx.fails('a duplicate StrID is rejected',
      await api.createBulletPallete({ editorId, strId: explicitStrId, editorName: 'dup', shooter: 'Center', target: 'FixField', size: 'Normal', type: 'Circle' }),
      'DUPLICATE_STR_ID');

    ctx.fails('an unknown bullet type is rejected',
      await api.createBulletPallete({ editorId, editorName: 'bad', shooter: 'Center', target: 'FixField', size: 'Normal', type: 'Default' }),
      'INVALID_ARGUMENT');

    ctx.fails('an unknown shooter is rejected',
      await api.createBulletPallete({ editorId, editorName: 'bad', shooter: 'Nobody', target: 'FixField', size: 'Normal', type: 'Circle' }),
      'INVALID_ARGUMENT');
    ctx.fails('an unknown target is rejected',
      await api.createBulletPallete({ editorId, editorName: 'bad', shooter: 'Center', target: 'Nowhere', size: 'Normal', type: 'Circle' }),
      'INVALID_ARGUMENT');
    ctx.fails('an unknown size is rejected',
      await api.createBulletPallete({ editorId, editorName: 'bad', shooter: 'Center', target: 'FixField', size: 'Huge', type: 'Circle' }),
      'INVALID_ARGUMENT');
    const sweep = [
      ...['Center', 'Enemy', 'TargetHead'].map((shooter) => [`shooter '${shooter}'`, { shooter, target: 'FixField', size: 'Normal', type: 'Circle' }]),
      ...['Player', 'FixField'].map((target) => [`target '${target}'`, { shooter: 'Center', target, size: 'Normal', type: 'Circle' }]),
      ...['Circle', 'Needle', 'Square'].map((type) => [`type '${type}'`, { shooter: 'Center', target: 'FixField', size: 'Normal', type }]),
      ...['Large', 'Normal'].map((size) => [`size '${size}'`, { shooter: 'Center', target: 'FixField', size, type: 'Circle' }]),
    ];
    for (const [label, args] of sweep) {
      const response = await api.createBulletPallete({ editorId, editorName: `${UNUSED_PREFIX} ${label}`, ...args });
      ctx.ok(`${label} is accepted`, response);
      if (response.payload?.success) created.push(response.payload.strId);
    }

    // creating a pallete is itself an undoable action
    const undoStrId = '9100';
    ctx.fails('the chosen StrID is free before the cycle',
      await api.queryBulletPallete({ editorId, strId: undoStrId }), 'PALLETE_NOT_FOUND');
    await verifyUndoRedo(ctx, {
      label: 'create_bullet_pallete',
      mutate: () => api.createBulletPallete({
        editorId, strId: undoStrId, editorName: `${UNUSED_PREFIX} undo`, shooter: 'Center', target: 'FixField', size: 'Normal', type: 'Circle',
      }),
      applied: async () => (await api.queryBulletPallete({ editorId, strId: undoStrId })).payload?.success === true,
      reverted: async () => (await api.queryBulletPallete({ editorId, strId: undoStrId })).payload?.errorCode === 'PALLETE_NOT_FOUND',
      tool: 'editor.create_bullet_pallete',
    });
    await api.removeBulletPallete({ editorId, strId: undoStrId });

    // ---------------- query ----------------
    ctx.section('editor.query_bullet_pallete');

    const detail = await api.queryBulletPallete({ editorId, strId: explicitStrId });
    ctx.ok('querying a single pallete by strId succeeds', detail);
    ctx.check('single-strId mode returns a "pallete" object rather than a page', !!detail.payload.pallete && detail.payload.palletes === undefined);
    ctx.hasKeys('pallete detail shape', detail.payload.pallete,
      ['strId', 'runtimeId', 'editorName', 'shooter', 'target', 'size', 'type', 'speed', 'placeOffset', 'randomOffsetRange', 'isEnableSoflan', 'referenceCount', 'referencingObjectIds']);
    ctx.equal('the detail reports the requested StrID', detail.payload.pallete.strId, explicitStrId);
    ctx.equal('the detail reports the requested shooter', detail.payload.pallete.shooter, 'Enemy');
    ctx.equal('the detail reports the requested target', detail.payload.pallete.target, 'Player');
    ctx.equal('the detail reports the requested size', detail.payload.pallete.size, 'Large');
    ctx.equal('the detail reports the requested type', detail.payload.pallete.type, 'Needle');
    ctx.equal('the detail reports the requested speed', detail.payload.pallete.speed, 2);
    ctx.equal('the detail reports the requested placeOffset', detail.payload.pallete.placeOffset, 8);
    ctx.equal('the detail reports the requested randomOffsetRange', detail.payload.pallete.randomOffsetRange, 4);
    ctx.check('referencingObjectIds is an array', Array.isArray(detail.payload.pallete.referencingObjectIds));
    ctx.equal('a brand-new pallete has no references yet', detail.payload.pallete.referenceCount, 0);

    ctx.fails('querying an unknown strId is rejected',
      await api.queryBulletPallete({ editorId, strId: 'no-such-pallete' }), 'PALLETE_NOT_FOUND');

    const page = await api.queryBulletPallete({ editorId, limit: 2 });
    ctx.ok('paging through palletes succeeds', page);
    ctx.hasKeys('list mode shape', page.payload, ['success', 'editorId', 'count', 'truncated', 'nextCursor', 'palletes']);
    ctx.equal('the page respects the limit', page.payload.count, 2);
    ctx.equal('the page is flagged truncated', page.payload.truncated, true);
    ctx.check('the page hands back a cursor', typeof page.payload.nextCursor === 'string', `nextCursor=${page.payload.nextCursor}`);

    const page2 = await api.queryBulletPallete({ editorId, limit: 2, cursor: page.payload.nextCursor });
    ctx.equal('the next page returns another two', page2.payload.count, 2);
    const overlapping = page2.payload.palletes.filter((p) => page.payload.palletes.some((q) => q.strId === p.strId));
    ctx.equal('paging does not repeat palletes', overlapping.length, 0);

    ctx.fails('a malformed cursor is rejected',
      await api.queryBulletPallete({ editorId, cursor: 'not-a-number' }), 'INVALID_CURSOR');

    const filtered = await api.queryBulletPallete({ editorId, editorNameContains: UNUSED_PREFIX, limit: 200 });
    ctx.check('editorNameContains filters the list', filtered.payload.palletes.length >= 2, `count=${filtered.payload.count} names=${filtered.payload.palletes.map((x) => x.editorName).join(' | ')}`);
    ctx.check('every filtered entry matches the filter',
      filtered.payload.palletes.every((p) => (p.editorName ?? '').toLowerCase().includes(UNUSED_PREFIX)),
      filtered.payload.palletes.map((p) => p.editorName).join(' | '));

    const insensitive = await api.queryBulletPallete({ editorId, editorNameContains: UNUSED_PREFIX.toUpperCase(), limit: 200 });
    ctx.equal('editorNameContains is case-insensitive', insensitive.payload.count, filtered.payload.count);

    // ---------------- modify ----------------
    ctx.section('editor.modify_bullet_pallete');

    const modified = await api.modifyBulletPallete({ editorId, strId: explicitStrId, propertyName: 'editorName', newValue: `${UNUSED_PREFIX} renamed` });
    ctx.ok('modify_bullet_pallete changes a property', modified);
    ctx.equal('the rename is visible',
      (await api.queryBulletPallete({ editorId, strId: explicitStrId })).payload.pallete.editorName, `${UNUSED_PREFIX} renamed`);

    ctx.fails('modify_bullet_pallete rejects an unknown property',
      await api.modifyBulletPallete({ editorId, strId: explicitStrId, propertyName: 'strId', newValue: '1' }), 'UNSUPPORTED_PROPERTY');
    ctx.fails('modify_bullet_pallete rejects an unknown strId',
      await api.modifyBulletPallete({ editorId, strId: 'no-such-pallete', propertyName: 'speed', newValue: '1' }), 'PALLETE_NOT_FOUND');
    ctx.fails('modify_bullet_pallete rejects an unparsable value',
      await api.modifyBulletPallete({ editorId, strId: explicitStrId, propertyName: 'speed', newValue: 'fast' }), 'INVALID_ARGUMENT');

    const everyProperty = { editorName: 'e2e all', shooter: 'TargetHead', target: 'FixField', size: 'Normal', type: 'Square', speed: '1.25', placeOffset: '3', randomOffsetRange: '7' };
    for (const [propertyName, newValue] of Object.entries(everyProperty)) {
      ctx.ok(`modify_bullet_pallete accepts property '${propertyName}'`,
        await api.modifyBulletPallete({ editorId, strId: explicitStrId, propertyName, newValue }));
    }

    await verifyUndoRedo(ctx, {
      label: 'modify_bullet_pallete(speed)',
      mutate: () => api.modifyBulletPallete({ editorId, strId: explicitStrId, propertyName: 'speed', newValue: '9.5' }),
      applied: async () => (await api.queryBulletPallete({ editorId, strId: explicitStrId })).payload.pallete.speed === 9.5,
      reverted: async () => (await api.queryBulletPallete({ editorId, strId: explicitStrId })).payload.pallete.speed !== 9.5,
    });

    // ---------------- in-use protection ----------------
    ctx.section('a pallete still referenced cannot be removed');

    const seedPalletes = await api.queryBulletPallete({ editorId, limit: 200 });
    const inUse = seedPalletes.payload.palletes.find((p) => p.referenceCount > 0);
    ctx.check('the seed chart has a pallete in use', !!inUse, `candidates=${seedPalletes.payload.palletes.filter((p) => p.referenceCount > 0).length}`);

    if (inUse) {
      ctx.fails('remove_bullet_pallete refuses a pallete that is still in use',
        await api.removeBulletPallete({ editorId, strId: inUse.strId }), 'PALLETE_IN_USE');
      ctx.check('the refusal names the referencing objects',
        ((await api.removeBulletPallete({ editorId, strId: inUse.strId })).payload?.errorMessage ?? '').length > 0);
    }

    // a freshly added bullet keeps its pallete alive until the bullet goes away
    const temporary = await api.createBulletPallete({
      editorId, editorName: `${UNUSED_PREFIX} in-use`, shooter: 'Center', target: 'FixField', size: 'Normal', type: 'Circle',
    });
    ctx.ok('a pallete for the in-use case is created', temporary);
    created.push(temporary.payload.strId);

    const taps = await api.family(editorId, 'tap');
    const at = taps[0].tGrid;
    const bullet = await api.addObject({
      editorId, objectType: 'bullet',
      tGridUnit: at.unit, tGridGrid: at.grid, xGridUnit: 0, xGridGrid: 0,
      bulletPalleteStrId: temporary.payload.strId,
    });
    ctx.ok('a bullet referencing the new pallete is created', bullet);

    const inUseDetail = await api.queryBulletPallete({ editorId, strId: temporary.payload.strId });
    ctx.equal('the pallete now reports one reference', inUseDetail.payload.pallete.referenceCount, 1);
    ctx.check('the referencing bullet id is reported',
      inUseDetail.payload.pallete.referencingObjectIds.includes(bullet.payload.objectId),
      `ids=${inUseDetail.payload.pallete.referencingObjectIds}`);

    ctx.fails('the pallete becomes unremovable while the bullet references it',
      await api.removeBulletPallete({ editorId, strId: temporary.payload.strId }), 'PALLETE_IN_USE');

    await api.removeObject({ editorId, objectId: bullet.payload.objectId });
    const afterBullet = await api.queryBulletPallete({ editorId, strId: temporary.payload.strId });
    ctx.equal('the reference count drops back to zero', afterBullet.payload.pallete.referenceCount, 0);

    // ---------------- remove ----------------
    ctx.section('editor.remove_bullet_pallete');

    ctx.fails('remove_bullet_pallete rejects an unknown strId',
      await api.removeBulletPallete({ editorId, strId: 'no-such-pallete' }), 'PALLETE_NOT_FOUND');

    const removed = await api.removeBulletPallete({ editorId, strId: temporary.payload.strId });
    ctx.ok('an unreferenced pallete can be removed', removed);
    ctx.fails('the removed pallete can no longer be queried',
      await api.queryBulletPallete({ editorId, strId: temporary.payload.strId }), 'PALLETE_NOT_FOUND');
    created.splice(created.indexOf(temporary.payload.strId), 1);

    await verifyUndoRedo(ctx, {
      label: 'remove_bullet_pallete',
      mutate: () => api.removeBulletPallete({ editorId, strId: explicitStrId }),
      applied: async () => (await api.queryBulletPallete({ editorId, strId: explicitStrId })).payload?.errorCode === 'PALLETE_NOT_FOUND',
      reverted: async () => (await api.queryBulletPallete({ editorId, strId: explicitStrId })).payload?.success === true,
    });

    // ---------------- cleanup ----------------
    for (const strId of created) {
      await api.removeBulletPallete({ editorId, strId });
    }
    const final = await api.queryBulletPallete({ editorId, editorNameContains: UNUSED_PREFIX, limit: 200 });
    ctx.equal('every pallete created by this suite was removed', final.payload.count, 0);
  },
};
