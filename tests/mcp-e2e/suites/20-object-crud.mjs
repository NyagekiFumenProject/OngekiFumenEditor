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
import { waitFor } from '../lib/harness.mjs';

// Read-only probes for custom projectile parameters. The security policy requires every script
// to carry the mutation shape, so each probe registers a no-op action and only reads the chart —
// but that action still leaves the run's own "MCP Script" history entry behind. Probes therefore
// must not run between a mutation and its undo (the undo would consume the probe's entry).
const READ_IMPORTS = `using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System.Linq;`;

const readBellScript = (id) => `${READ_IMPORTS}

var editor = ScriptArgs.TargetEditor;
var bell = editor.Fumen.Bells.FirstOrDefault(x => x.Id == ${id});
editor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create("MCP e2e read-only probe", () => { }, () => { }));
if (bell is null)
    return null;
return new { speed = bell.Speed, placeOffset = bell.PlaceOffset, randomOffsetRange = bell.RandomOffsetRange, shooter = bell.ShooterValue.ToString(), target = bell.TargetValue.ToString(), hasPallete = bell.ReferenceBulletPallete != null };`;

const readBulletScript = (id) => `${READ_IMPORTS}

var editor = ScriptArgs.TargetEditor;
var bullet = editor.Fumen.Bullets.FirstOrDefault(x => x.Id == ${id});
editor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create("MCP e2e read-only probe", () => { }, () => { }));
if (bullet is null)
    return null;
return new { speed = bullet.Speed, placeOffset = bullet.PlaceOffset, randomOffsetRange = bullet.RandomOffsetRange, shooter = bullet.ShooterValue.ToString(), target = bullet.TargetValue.ToString(), size = bullet.SizeValue.ToString(), type = bullet.TypeValue.ToString(), damageType = bullet.BulletDamageTypeValue.ToString(), hasPallete = bullet.ReferenceBulletPallete != null };`;

// Selection probes for the delete / property-browser contract: select exactly one tap by id, or
// snapshot the current selection. The security policy requires the ExecuteAction shape, so each
// probe leaves one "MCP Script" entry — harmless as long as no assertion depends on the exact
// undo depth around it (the delete check below undoes before taking its snapshot).
const SELECT_IMPORTS = `using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System.Linq;`;

const selectTapScript = (id) => `${SELECT_IMPORTS}

var editor = ScriptArgs.TargetEditor;
var target = editor.Fumen.Taps.FirstOrDefault(x => x.Id == ${id});
editor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create("MCP e2e read-only probe", () => { }, () => { }));
editor.ClearSelection();
if (target is not null)
    target.IsSelected = true;
var selected = editor.SelectObjects.OfType<OngekiObjectBase>().Select(x => x.Id).ToArray();
return new { selected = selected.Length, ids = selected };`;

const selectionSnapshotScript = `${SELECT_IMPORTS}

var editor = ScriptArgs.TargetEditor;
editor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create("MCP e2e read-only probe", () => { }, () => { }));
var selected = editor.SelectObjects.OfType<OngekiObjectBase>().Select(x => x.Id).ToArray();
return new { selected = selected.Length, ids = selected };`;

export default {
  name: '20-object-crud',
  description: 'add_object / modify_object / remove_object + undo/redo',

  async run(ctx) {
    const api = ctx.api;
    const editorId = ctx.editorId;
    const env = ctx.env;
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

    // ---------------- custom projectile parameters ----------------
    ctx.section('editor.add_object — custom projectile parameters');

    const bellCustom = await api.addObject({
      editorId, objectType: 'bell', ...T(21), xGridUnit: 0, xGridGrid: 0,
      shooter: 'Enemy', target: 'Player', speed: 2.5, placeOffset: 8, randomOffsetRange: 4,
    });
    ctx.ok('add_object(bell) accepts custom projectile parameters', bellCustom);
    const bellCustomId = bellCustom.payload?.objectId;
    const bellRead = await api.runScript({ editorId, scriptText: readBellScript(bellCustomId) });
    ctx.ok('the custom bell can be read back through a script', bellRead);
    ctx.equal('the custom bell stores the requested shooter', bellRead.payload.result?.shooter, 'Enemy');
    ctx.equal('the custom bell stores the requested target', bellRead.payload.result?.target, 'Player');
    ctx.equal('the custom bell stores the requested speed', bellRead.payload.result?.speed, 2.5);
    ctx.equal('the custom bell stores the requested placeOffset', bellRead.payload.result?.placeOffset, 8);
    ctx.equal('the custom bell stores the requested randomOffsetRange', bellRead.payload.result?.randomOffsetRange, 4);
    ctx.equal('the custom bell has no pallete', bellRead.payload.result?.hasPallete, false);

    const bulletCustom = await api.addObject({
      editorId, objectType: 'bullet', ...T(22), xGridUnit: 0, xGridGrid: 0,
      shooter: 'Center', target: 'FixField', size: 'Large', type: 'Needle', bulletDamageType: 'Danger',
      speed: 1.5, placeOffset: 2, randomOffsetRange: 6,
    });
    ctx.ok('add_object(bullet) works without a pallete when custom parameters are explicit', bulletCustom);
    const bulletCustomId = bulletCustom.payload?.objectId;
    const bulletRead = await api.runScript({ editorId, scriptText: readBulletScript(bulletCustomId) });
    ctx.ok('the custom bullet can be read back through a script', bulletRead);
    ctx.equal('the custom bullet stores the requested shooter', bulletRead.payload.result?.shooter, 'Center');
    ctx.equal('the custom bullet stores the requested target', bulletRead.payload.result?.target, 'FixField');
    ctx.equal('the custom bullet stores the requested size', bulletRead.payload.result?.size, 'Large');
    ctx.equal('the custom bullet stores the requested type', bulletRead.payload.result?.type, 'Needle');
    ctx.equal('the custom bullet stores the requested damage type', bulletRead.payload.result?.damageType, 'Danger');
    ctx.equal('the custom bullet stores the requested speed', bulletRead.payload.result?.speed, 1.5);
    ctx.equal('the custom bullet has no pallete', bulletRead.payload.result?.hasPallete, false);

    ctx.fails('a pallete and custom parameters cannot be combined',
      await api.addObject({ editorId, objectType: 'bell', ...T(23), xGridUnit: 0, xGridGrid: 0, bulletPalleteStrId: palleteStrId, shooter: 'Enemy' }),
      'INVALID_ARGUMENT');
    ctx.fails('a bullet with a pallete rejects custom parameters too',
      await api.addObject({ editorId, objectType: 'bullet', ...T(23), xGridUnit: 0, xGridGrid: 0, bulletPalleteStrId: palleteStrId, speed: 2 }),
      'INVALID_ARGUMENT');
    ctx.fails('custom projectile parameters on a tap are rejected',
      await api.addObject({ editorId, objectType: 'tap', ...T(23), shooter: 'Enemy' }), 'INVALID_ARGUMENT');
    ctx.fails('a bell rejects size (it has no effect on bells)',
      await api.addObject({ editorId, objectType: 'bell', ...T(23), xGridUnit: 0, xGridGrid: 0, size: 'Large' }), 'INVALID_ARGUMENT');
    ctx.fails('a bell rejects type (a bell is always a Circle)',
      await api.addObject({ editorId, objectType: 'bell', ...T(23), xGridUnit: 0, xGridGrid: 0, type: 'Needle' }), 'INVALID_ARGUMENT');
    ctx.fails('a bell rejects bulletDamageType',
      await api.addObject({ editorId, objectType: 'bell', ...T(23), xGridUnit: 0, xGridGrid: 0, bulletDamageType: 'Danger' }), 'INVALID_ARGUMENT');
    ctx.fails('an unknown shooter enum value is rejected',
      await api.addObject({ editorId, objectType: 'bell', ...T(23), xGridUnit: 0, xGridGrid: 0, shooter: 'Nowhere' }), 'INVALID_ARGUMENT');
    ctx.fails("'speed' on a tap is rejected (it is a soflan / projectile parameter)",
      await api.addObject({ editorId, objectType: 'tap', ...T(23), speed: 2 }), 'INVALID_ARGUMENT');

    // custom parameters survive the undo/redo cycle like any other content. The cycle itself
    // observes the object through query_object (see the probe note at the top): after the
    // cycle's final undo, one redo must bring the whole object, fields included, back.
    let customBellCycleId = null;
    await verifyUndoRedo(ctx, {
      label: 'add_object(bell custom)',
      mutate: async () => {
        const response = await api.addObject({ editorId, objectType: 'bell', ...T(25), xGridUnit: 0, xGridGrid: 0, shooter: 'TargetHead', speed: 3 });
        customBellCycleId = response.payload?.objectId;
        return response;
      },
      applied: async () => !!(await findById('bell', customBellCycleId)),
      reverted: async () => (await findById('bell', customBellCycleId)) === undefined,
      undoName: /Add bell/i,
      redoName: /Add bell/i,
    });

    const revived = await api.redo({ editorId });
    ctx.ok('the custom bell is redoable after the cycle', revived);
    const revivedRead = await api.runScript({ editorId, scriptText: readBellScript(customBellCycleId) });
    ctx.equal('the redo restores the custom projectile fields', revivedRead.payload?.result?.speed, 3);

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

    // ---------------- modify: custom projectile parameters ----------------
    ctx.section('editor.modify_object — custom projectile parameters');

    const modBullet = await api.addObject({
      editorId, objectType: 'bullet', ...T(27), xGridUnit: 0, xGridGrid: 0,
      shooter: 'Center', target: 'FixField', size: 'Normal', type: 'Circle', bulletDamageType: 'Normal',
      speed: 1, placeOffset: 0, randomOffsetRange: 0,
    });
    ctx.ok('a custom bullet for the modify cases is created', modBullet);
    const modBulletId = modBullet.payload?.objectId;

    const palettedBullet = await api.addObject({ editorId, objectType: 'bullet', ...T(28), xGridUnit: 0, xGridGrid: 0, bulletPalleteStrId: palleteStrId });
    ctx.ok('a palleted bullet for the read-only case is created', palettedBullet);
    const palettedBulletId = palettedBullet.payload?.objectId;

    ctx.fails('custom projectile parameters are read-only while a pallete is set',
      await api.modifyObject({ editorId, objectId: palettedBulletId, propertyName: 'speed', newValue: '5' }), 'INVALID_ARGUMENT');

    const cleared = await api.modifyObject({ editorId, objectId: palettedBulletId, propertyName: 'bulletPallete', newValue: '' });
    ctx.ok('clearing a bullet pallete drops it to custom mode', cleared);
    ctx.equal('the clear echoes the pallete StrID it had', cleared.payload?.oldValue, palleteStrId);

    const afterClear = await api.modifyObject({ editorId, objectId: palettedBulletId, propertyName: 'speed', newValue: '5' });
    ctx.ok('custom parameters become writable once the pallete is cleared', afterClear);
    ctx.equal('the write echoes the default speed it replaced', afterClear.payload?.oldValue, '1');

    // every custom field is writable; each write echoes what the previous one left behind
    const writeChain = [
      ['shooter', 'Enemy', 'Center'],
      ['target', 'Player', 'FixField'],
      ['size', 'Large', 'Normal'],
      ['type', 'Needle', 'Circle'],
      ['bulletDamageType', 'Danger', 'Normal'],
      ['speed', '2.5', '1'],
      ['placeOffset', '7', '0'],
      ['randomOffsetRange', '3', '0'],
    ];
    for (const [property, value, expectedOld] of writeChain) {
      const response = await api.modifyObject({ editorId, objectId: modBulletId, propertyName: property, newValue: value });
      ctx.ok(`modify_object writes custom '${property}'`, response);
      ctx.equal(`custom '${property}' echoes the previous value`, response.payload?.oldValue, expectedOld);
    }

    const customRead = await api.runScript({ editorId, scriptText: readBulletScript(modBulletId) });
    ctx.equal('the custom reads back shooter', customRead.payload.result?.shooter, 'Enemy');
    ctx.equal('the custom reads back target', customRead.payload.result?.target, 'Player');
    ctx.equal('the custom reads back size', customRead.payload.result?.size, 'Large');
    ctx.equal('the custom reads back type', customRead.payload.result?.type, 'Needle');
    ctx.equal('the custom reads back damage type', customRead.payload.result?.damageType, 'Danger');
    ctx.equal('the custom reads back speed', customRead.payload.result?.speed, 2.5);
    ctx.equal('the custom reads back placeOffset', customRead.payload.result?.placeOffset, 7);
    ctx.equal('the custom reads back randomOffsetRange', customRead.payload.result?.randomOffsetRange, 3);

    // undo / redo of a custom write; the next write's echo proves what the history step restored
    await api.modifyObject({ editorId, objectId: modBulletId, propertyName: 'speed', newValue: '9' });
    const undoCustom = await api.undo({ editorId });
    ctx.check('the custom write is undoable', /speed/i.test(undoCustom.payload?.undone ?? ''), `undone=${undoCustom.payload?.undone}`);
    const redoCustom = await api.redo({ editorId });
    ctx.check('the custom write is redoable', /speed/i.test(redoCustom.payload?.redone ?? ''), `redone=${redoCustom.payload?.redone}`);
    const afterRedo = await api.modifyObject({ editorId, objectId: modBulletId, propertyName: 'speed', newValue: '8' });
    ctx.equal('the redo re-applied the custom value', afterRedo.payload?.oldValue, '9');
    await api.undo({ editorId });

    // family / value limits mirror add_object
    ctx.fails('a bell rejects size in modify too',
      await api.modifyObject({ editorId, objectId: bellCustomId, propertyName: 'size', newValue: 'Large' }), 'UNSUPPORTED_PROPERTY');
    ctx.fails('a bell rejects type in modify too',
      await api.modifyObject({ editorId, objectId: bellCustomId, propertyName: 'type', newValue: 'Needle' }), 'UNSUPPORTED_PROPERTY');
    ctx.fails('a bell rejects bulletDamageType in modify too',
      await api.modifyObject({ editorId, objectId: bellCustomId, propertyName: 'bulletDamageType', newValue: 'Danger' }), 'UNSUPPORTED_PROPERTY');
    ctx.fails('a tap rejects shooter',
      await api.modifyObject({ editorId, objectId: moveTarget, propertyName: 'shooter', newValue: 'Enemy' }), 'UNSUPPORTED_PROPERTY');
    ctx.fails('an unknown shooter value is rejected',
      await api.modifyObject({ editorId, objectId: modBulletId, propertyName: 'shooter', newValue: 'Nowhere' }), 'INVALID_ARGUMENT');
    ctx.fails('an unknown pallete StrID is rejected on modify',
      await api.modifyObject({ editorId, objectId: modBulletId, propertyName: 'bulletPallete', newValue: 'no-such-pallete' }), 'INVALID_ARGUMENT');

    // a custom bullet can be bound back to a pallete, which flips its custom parameters back to read-only
    const bound = await api.modifyObject({ editorId, objectId: modBulletId, propertyName: 'bulletPallete', newValue: palleteStrId });
    ctx.ok('a custom bullet can be bound back to a pallete', bound);
    ctx.equal('binding echoes the empty pallete it had', bound.payload?.oldValue, '');
    ctx.fails('custom parameters are read-only again after rebinding',
      await api.modifyObject({ editorId, objectId: modBulletId, propertyName: 'speed', newValue: '1' }), 'INVALID_ARGUMENT');
    const unbound = await api.modifyObject({ editorId, objectId: modBulletId, propertyName: 'bulletPallete', newValue: '' });
    ctx.ok('the pallete can be cleared again', unbound);
    ctx.equal('clearing echoes the pallete StrID it had', unbound.payload?.oldValue, palleteStrId);

    // ---------------- editor.get_object ----------------
    ctx.section('editor.get_object — full property read');
    ctx.cover('editor.get_object');

    const readBack = await api.addObject({ editorId, objectType: 'tap', ...T(34), xGridUnit: 1, xGridGrid: 2, tag: 'readback' });
    ctx.ok('a tap for the read-back is created', readBack);
    const readBackId = readBack.payload?.objectId;
    const propertyRow = (payload, name) => (payload?.properties ?? []).find((p) => p.name === name);

    const read = await api.getObject({ editorId, objectId: readBackId });
    ctx.hasKeys('get_object response shape', read.payload, ['success', 'editorId', 'objectType', 'objectId', 'object', 'properties']);
    ctx.equal('get_object echoes the family', read.payload.objectType, 'tap');
    ctx.equal('get_object echoes the object id', read.payload.objectId, readBackId);
    ctx.equal('the embedded DTO reports the object id', read.payload.object?.id, readBackId);
    ctx.equal('the property read reports the tag', propertyRow(read.payload, 'tag')?.value, 'readback');
    ctx.equal('the property read reports the TGrid unit', Number(propertyRow(read.payload, 'tGridUnit')?.value), T(34).tGridUnit);
    ctx.equal('the property read reports the XGrid unit', Number(propertyRow(read.payload, 'xGridUnit')?.value), 1);
    ctx.equal('the property read reports the XGrid grid', Number(propertyRow(read.payload, 'xGridGrid')?.value), 2);
    ctx.equal('bool properties use canonical formatting', propertyRow(read.payload, 'isCritical')?.value, 'False');
    ctx.equal('writable properties report writable=true', propertyRow(read.payload, 'tag')?.writable, true);
    ctx.check('every property name is unique',
      new Set(read.payload.properties.map((p) => p.name)).size === read.payload.properties.length);
    ctx.check('inapplicable properties are omitted',
      propertyRow(read.payload, 'bulletPallete') === undefined && propertyRow(read.payload, 'direction') === undefined);

    const viaQuery = await findById('tap', readBackId);
    ctx.check('the embedded DTO matches query_object',
      JSON.stringify(read.payload.object) === JSON.stringify(viaQuery),
      `get_object=${JSON.stringify(read.payload.object)} query_object=${JSON.stringify(viaQuery)}`);

    // get_object is the read half of modify_object: writes are observable and reads never disturb history
    await api.modifyObject({ editorId, objectId: readBackId, propertyName: 'tag', newValue: 'readback-2' });
    ctx.equal('get_object reflects the write',
      propertyRow((await api.getObject({ editorId, objectId: readBackId })).payload, 'tag')?.value, 'readback-2');
    const writeUndone = await api.undo({ editorId });
    ctx.check('the write is undoable after a read',
      typeof writeUndone.payload?.undone === 'string', `undone=${writeUndone.payload?.undone}`);
    ctx.equal('get_object reflects the undo',
      propertyRow((await api.getObject({ editorId, objectId: readBackId })).payload, 'tag')?.value, 'readback');

    // projectile eligibility: custom parameters are readable but only writable while no pallete is bound
    const readBell = await api.addObject({ editorId, objectType: 'bell', ...T(35), xGridUnit: 0, xGridGrid: 0, shooter: 'Enemy', target: 'Player', speed: 2 });
    const readBellId = readBell.payload?.objectId;
    const bellGet = await api.getObject({ editorId, objectId: readBellId });
    ctx.equal('the projectile read reports the custom shooter', propertyRow(bellGet.payload, 'shooter')?.value, 'Enemy');
    ctx.equal('custom parameters report writable=true while unbound', propertyRow(bellGet.payload, 'speed')?.writable, true);
    ctx.check('a bell omits properties it does not have',
      ['size', 'type', 'bulletDamageType'].every((name) => propertyRow(bellGet.payload, name) === undefined));

    const readPaletted = await api.addObject({ editorId, objectType: 'bullet', ...T(36), xGridUnit: 0, xGridGrid: 0, bulletPalleteStrId: palleteStrId });
    const readPalettedId = readPaletted.payload?.objectId;
    const palettedRead = await api.getObject({ editorId, objectId: readPalettedId });
    ctx.equal('the paletted bullet reports its pallete', propertyRow(palettedRead.payload, 'bulletPallete')?.value, palleteStrId);
    ctx.check('custom parameters stay readable while a pallete is set',
      propertyRow(palettedRead.payload, 'speed')?.value !== undefined);
    ctx.equal('custom parameters report writable=false while a pallete is set', propertyRow(palettedRead.payload, 'speed')?.writable, false);
    ctx.equal('the pallete reference itself stays writable', propertyRow(palettedRead.payload, 'bulletPallete')?.writable, true);

    ctx.fails('get_object rejects an unknown object id',
      await api.getObject({ editorId, objectId: 2147483000 }), 'OBJECT_NOT_FOUND');
    ctx.fails('get_object rejects a missing editor',
      await api.getObject({ editorId: 'editor-missing', objectId: readBackId }), 'EDITOR_NOT_FOUND');
    ctx.fails('get_object rejects a stale expectedEditorId',
      await api.getObject({ editorId, expectedEditorId: 'editor-somebody-else', objectId: readBackId }), 'EDITOR_CHANGED');

    // ---------------- tag ----------------
    ctx.section('editor.add_object / modify_object — tag');

    const taggedTap = await api.addObject({ editorId, objectType: 'tap', ...T(29), xGridUnit: 0, xGridGrid: 0, tag: 'mcp-tag' });
    ctx.ok('add_object accepts a tag', taggedTap);
    const taggedTapId = taggedTap.payload?.objectId;

    const tagEcho = await api.modifyObject({ editorId, objectId: taggedTapId, propertyName: 'tag', newValue: 'mcp-tag-2' });
    ctx.ok('modify_object writes tag', tagEcho);
    ctx.equal('the add-time tag reads back through the modify echo', tagEcho.payload?.oldValue, 'mcp-tag');
    ctx.equal('the tag write reports the new value', tagEcho.payload?.newValue, 'mcp-tag-2');

    // undo / redo round-trip, observed through the next write's echo
    await api.undo({ editorId });
    await api.redo({ editorId });
    const tagEcho2 = await api.modifyObject({ editorId, objectId: taggedTapId, propertyName: 'tag', newValue: 'mcp-tag-3' });
    ctx.equal('undo/redo round-trips the tag value', tagEcho2.payload?.oldValue, 'mcp-tag-2');
    await api.undo({ editorId });

    const tagCleared = await api.modifyObject({ editorId, objectId: taggedTapId, propertyName: 'tag', newValue: '' });
    ctx.equal('an empty tag clears it', tagCleared.payload?.newValue, '');
    ctx.equal('clearing echoes the value it replaced', tagCleared.payload?.oldValue, 'mcp-tag-2');

    // tag is universal: a comment carries one too
    const taggedComment = await api.addObject({ editorId, objectType: 'comment', ...T(30), content: 'tagged comment', tag: 'comment-tag' });
    ctx.ok('add_object(comment) accepts a tag', taggedComment);
    const taggedCommentId = taggedComment.payload?.objectId;
    const commentTagEcho = await api.modifyObject({ editorId, objectId: taggedCommentId, propertyName: 'tag', newValue: 'comment-tag-2' });
    ctx.equal('the comment tag reads back through the modify echo', commentTagEcho.payload?.oldValue, 'comment-tag');

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

    // ---------------- write tools mark the document dirty ----------------
    ctx.section('add_object / remove_object / modify_object mark the editor dirty');

    // Each probe opens a fresh editor so "dirty" can only come from the operation under test;
    // closing with force discards the probe chart and leaves the seed editor alone.
    const probeEditor = async (probeId) =>
      (await api.listOpened()).payload.find((e) => e.editorId === probeId);
    const probeDirty = async (probeId) => (await probeEditor(probeId))?.isDirty;
    const anyCount = (probe) =>
      probe.tapCount + probe.laneCount + probe.holdCount + probe.bellCount +
      probe.bulletCount + probe.bpmChangeCount + probe.soflanCount;

    // open_fast / open_proj create the tab before their chart finishes loading, so wait for the
    // counters instead of sampling once — querying or mutating too early would hit an empty chart.
    // `project: true` opens the project fixture (a copy of the seeded chart, which has taps to
    // remove and modify); otherwise second.ogkr is used and only its load completion is confirmed.
    const openProbeEditor = async (label, { project = false } = {}) => {
      const opened = project
        ? await api.openProject({ projectPath: env.projectPath })
        : await api.openFast({ fumenPath: env.secondChart, audioPath: env.audioPath });
      ctx.ok(`${label}: a fresh editor opens`, opened);
      const probeId = opened.payload?.editorId;
      const ready = await waitFor(async () => {
        const probe = await probeEditor(probeId);
        return probe && (project ? probe.tapCount > 0 : anyCount(probe) > 0) ? probe : null;
      });
      ctx.check(`${label}: the probe chart finished loading`, ready.ok,
        ready.ok ? `taps=${ready.last.tapCount} after ${ready.waitedMs}ms` : 'timed out waiting for the chart to load');
      ctx.equal(`${label}: the fresh editor starts clean`, await probeDirty(probeId), false);
      return probeId;
    };

    const addProbeId = await openProbeEditor('add probe');
    ctx.fails('add probe: a rejected add is refused',
      await api.addObject({ editorId: addProbeId, objectType: 'nope', ...T(31) }), 'UNSUPPORTED_OBJECT_TYPE');
    ctx.equal('add probe: a rejected write leaves the editor clean', await probeDirty(addProbeId), false);
    ctx.ok('add probe: a regular add succeeds',
      await api.addObject({ editorId: addProbeId, objectType: 'tap', ...T(31), xGridUnit: 0, xGridGrid: 0 }));
    ctx.equal('add_object marks the editor dirty', await probeDirty(addProbeId), true);
    ctx.ok('add probe is closed again', await api.close({ editorId: addProbeId, force: true }));

    const removeProbeId = await openProbeEditor('remove probe', { project: true });
    const removeVictim = (await api.family(removeProbeId, 'tap'))[0];
    if (removeVictim) {
      ctx.ok('remove probe: the victim tap is removed',
        await api.removeObject({ editorId: removeProbeId, objectId: removeVictim.id }));
      ctx.equal('remove_object marks the editor dirty', await probeDirty(removeProbeId), true);
    } else {
      ctx.check('remove probe: the fresh chart has a tap to remove', false, 'the project fixture should contain a tap');
    }
    ctx.ok('remove probe is closed again', await api.close({ editorId: removeProbeId, force: true }));

    const modifyProbeId = await openProbeEditor('modify probe', { project: true });
    const modifyVictim = (await api.family(modifyProbeId, 'tap'))[0];
    if (modifyVictim) {
      ctx.ok('modify probe: the tap is modified',
        await api.modifyObject({ editorId: modifyProbeId, objectId: modifyVictim.id, propertyName: 'tGridUnit', newValue: '1.5' }));
      ctx.equal('modify_object marks the editor dirty', await probeDirty(modifyProbeId), true);
    } else {
      ctx.check('modify probe: the fresh chart has a tap to modify', false, 'the project fixture should contain a tap');
    }
    ctx.ok('modify probe is closed again', await api.close({ editorId: modifyProbeId, force: true }));

    // a queued mutation dirties only when the action scope actually applies it
    const scopeProbeId = await openProbeEditor('scope probe');
    ctx.ok('scope probe: a scope opens', await api.beginAction({ editorId: scopeProbeId }));
    ctx.ok('scope probe: the add is queued',
      await api.addObject({ editorId: scopeProbeId, objectType: 'tap', ...T(32), xGridUnit: 0, xGridGrid: 0 }));
    ctx.equal('a queued add does not dirty the editor before it is applied', await probeDirty(scopeProbeId), false);
    ctx.ok('scope probe: the scope applies', await api.endAction({ editorId: scopeProbeId, name: 'e2e dirty scope' }));
    ctx.equal('applying the scope marks the editor dirty', await probeDirty(scopeProbeId), true);
    ctx.ok('scope probe is closed again', await api.close({ editorId: scopeProbeId, force: true }));

    // ---------------- selection stays consistent across delete / modify ----------------
    ctx.section('remove_object clears the selection; modify_object keeps it');

    const selectionVictim = await api.addObject({ editorId, objectType: 'tap', ...T(33), xGridUnit: 0, xGridGrid: 0 });
    ctx.ok('a tap for the selection contract is created', selectionVictim);
    const selectionVictimId = selectionVictim.payload?.objectId;

    const selectProbe = await api.runScript({ editorId, scriptText: selectTapScript(selectionVictimId) });
    ctx.equal('the probe selected exactly the victim', selectProbe.payload.result?.selected, 1);
    ctx.equal('the selection is visible through query_object',
      (await api.queryObject({ editorId, objectType: 'tap', selectedOnly: true, limit: 2000 })).payload.count, 1);

    // Delete it and undo: the flag must have been cleared at delete time, so the object comes back
    // unselected (the §27 contract; a surviving flag would make it reappear selected).
    ctx.ok('the selected tap is removed', await api.removeObject({ editorId, objectId: selectionVictimId }));
    const removalUndo = await api.undo({ editorId });
    ctx.check('the removal is undoable', /Remove tap/i.test(removalUndo.payload?.undone ?? ''), `undone=${removalUndo.payload?.undone}`);
    ctx.check('the tap is back after undo', !!(await findById('tap', selectionVictimId)));

    const afterUndoSelection = await api.runScript({ editorId, scriptText: selectionSnapshotScript });
    ctx.equal('undo brings the object back unselected', afterUndoSelection.payload.result?.selected, 0);

    // a modify on a selected object must keep it selected (the browser refresh must not disturb the selection)
    await api.runScript({ editorId, scriptText: selectTapScript(selectionVictimId) });
    ctx.ok('a selected object can still be modified',
      await api.modifyObject({ editorId, objectId: selectionVictimId, propertyName: 'tGridUnit', newValue: '7.5' }));
    const afterModifySelection = await api.runScript({ editorId, scriptText: selectionSnapshotScript });
    ctx.equal('modify_object keeps the selection', afterModifySelection.payload.result?.ids?.[0], selectionVictimId);

    // leave the chart as we found it: undo the leftover fixtures
    const leftovers = [addId, modifyId, victimId, bellCustomId, bulletCustomId, customBellCycleId, modBulletId, palettedBulletId, taggedTapId, taggedCommentId, selectionVictimId, readBackId, readBellId, readPalettedId, ...created.values()].filter((id) => Number.isInteger(id));
    for (const id of leftovers) {
      await api.removeObject({ editorId, objectId: id });
    }
  },
};
