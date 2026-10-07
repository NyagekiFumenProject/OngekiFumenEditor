// Suite 30 — the connectable-object families.
//
// lane / beam are *not* flat objects: a start owns an ordered chain of segments, and a
// lane segment can additionally carry Bezier control points. The tools expose those as
// six pseudo-families (lanenext / beamnext / beam / curvecontrol / isfarea / laneblock),
// linked by `parentRecordId` (the start) and `referenceObjectId` (the segment).
//
// This suite pins the whole structural contract, including the two traps that make the
// feature easy to get wrong:
//   * curve controls must be added/removed through the owning segment, and removal clears
//     the back-reference, so undo must re-attach to the captured owner
//   * moving a segment's TGrid must re-sort the start's child chain (segmentIndex follows)

import { verifyUndoRedo } from '../lib/history.mjs';
import { splitTotalGrid, totalGridResolution } from '../lib/env.mjs';
import { waitFor } from '../lib/harness.mjs';

export default {
  name: '30-connectable',
  description: 'lanenext / beam / beamnext / curvecontrol / isfarea / laneblock / colorful',

  async run(ctx) {
    const api = ctx.api;
    const editorId = ctx.editorId;
    ctx.cover('editor.add_object', 'editor.modify_object', 'editor.remove_object', 'editor.query_object');

    const add = (objectType, args = {}) => api.addObject({ editorId, objectType, ...args });
    const modify = (objectId, propertyName, newValue, extra = {}) => api.modifyObject({ editorId, objectId, propertyName, newValue, ...extra });
    const remove = (objectId) => api.removeObject({ editorId, objectId });
    const segments = async (recordId) => (await api.family(editorId, 'lanenext')).filter((s) => s.parentRecordId === recordId);
    const find = async (family, id) => (await api.family(editorId, family)).find((o) => o.id === id);

    const taps = await api.family(editorId, 'tap');
    const base = taps[0].tGrid.totalGrid;
    const T = (n) => splitTotalGrid(base + n * totalGridResolution);
    const totalOf = (t) => t.tGridUnit * totalGridResolution + t.tGridGrid;

    // ---------------- lane start + segments ----------------
    ctx.section('lane start and its segment chain');

    const laneResponse = await add('lane', { ...T(1), xGridUnit: 2, xGridGrid: 0, laneType: 'center' });
    ctx.ok('add_object(lane, center) succeeds', laneResponse);
    const laneId = laneResponse.payload.objectId;
    const laneDto = await find('lane', laneId);
    ctx.hasKeys('lane DTO exposes recordId and laneType', laneDto, ['recordId', 'laneType']);
    ctx.equal('lane DTO reports the requested laneType', laneDto.laneType, 'Center');
    ctx.check('lane recordId is an integer', Number.isInteger(laneDto.recordId), `recordId=${laneDto.recordId}`);
    const record = laneDto.recordId;

    ctx.equal('a freshly created lane carries no segments', (await segments(record)).length, 0);

    ctx.fails('lanenext without parentRecordId is rejected',
      await add('lanenext', { ...T(2), xGridUnit: 0, xGridGrid: 0 }), 'INVALID_ARGUMENT');
    ctx.fails('lanenext with an unknown parentRecordId is rejected',
      await add('lanenext', { ...T(2), parentRecordId: 999999 }), 'INVALID_ARGUMENT');
    ctx.fails('lanenext rejects endTGrid (a segment is a single point)',
      await add('lanenext', { ...T(2), parentRecordId: record, endTGridUnit: T(3).tGridUnit, endTGridGrid: T(3).tGridGrid }), 'INVALID_ARGUMENT');

    const snappingLane = await add('lane', { ...T(1), xGridUnit: 1, xGridGrid: 0, laneType: 'right' });
    ctx.ok('a second lane is created for the snapping case', snappingLane);
    const snappingRecord = (await find('lane', snappingLane.payload.objectId)).recordId;
    ctx.fails('snapping to a zero-length lane fails (no path to project onto)',
      await add('tap', { ...T(1), xGridUnit: 0, xGridGrid: 0, referenceLaneRecordId: snappingRecord, snapXToLane: true }), 'INVALID_ARGUMENT');

    const segment1 = await add('lanenext', { ...T(2), xGridUnit: 2, xGridGrid: 0, parentRecordId: record });
    ctx.ok('adding the first lanenext segment succeeds', segment1);
    const segment2 = await add('lanenext', { ...T(3), xGridUnit: 4, xGridGrid: 0, parentRecordId: record });
    ctx.ok('adding the second lanenext segment succeeds', segment2);

    const chain = await segments(record);
    ctx.equal('the lane now owns two segments', chain.length, 2);
    ctx.hasKeys('segment DTO exposes id / type / tGrid / xGrid / laneType / parentRecordId', chain[0],
      ['id', 'type', 'tGrid', 'xGrid', 'laneType', 'parentRecordId']);
    ctx.equal('segments report their parent record', chain[0].parentRecordId, record);
    ctx.equal('segments are typed as lanenext', chain[0].type, 'lanenext');
    ctx.equal('segments inherit the start lane type', chain[0].laneType, 'Center');
    ctx.check('segments are ordered by TGrid', chain[0].tGrid.totalGrid <= chain[1].tGrid.totalGrid,
      `${chain[0].tGrid.totalGrid} <= ${chain[1].tGrid.totalGrid}`);

    // a lane with a real path can now have objects docked onto it
    const docked = await add('tap', { ...T(2), xGridUnit: 0, xGridGrid: 0, referenceLaneRecordId: record, snapXToLane: true });
    ctx.ok('snapping a tap onto the extended lane succeeds', docked);
    const dockedDto = await find('tap', docked.payload.objectId);
    ctx.equal('the docked tap remembers its lane', dockedDto?.referenceLaneRecordId, record);
    ctx.check('the docked tap was moved onto the lane path at the requested TGrid',
      dockedDto?.xGrid?.unit === 2 && dockedDto?.xGrid?.grid === 0,
      `xGrid=${JSON.stringify(dockedDto?.xGrid)} (lane path at T[2,0] is XGrid 2:0)`);

    // moving a segment must re-sort the chain
    ctx.section('segment chain stays ordered when a segment moves');
    const movedTo = T(0);
    ctx.ok('moving the second segment before the first',
      await modify(segment2.payload.objectId, 'tGridUnit', String(movedTo.tGridUnit)));
    const reshuffled = await segments(record);
    ctx.equal('the chain still has two segments', reshuffled.length, 2);
    ctx.equal('the moved segment now sorts first',
      reshuffled[0].id, segment2.payload.objectId);
    ctx.check('the chain is re-sorted by TGrid after the move',
      reshuffled[0].tGrid.totalGrid <= reshuffled[1].tGrid.totalGrid,
      `${reshuffled[0].tGrid.totalGrid} <= ${reshuffled[1].tGrid.totalGrid}`);

    // ---------------- dockMode = nearest ----------------
    ctx.section('dockMode=nearest picks and snaps to the closest lane');

    // The seed chart already has lanes everywhere, which would make "nearest" ambiguous; the
    // second fixture has none, so only the lanes created below can be candidates.
    const dockOpen = await api.openFast({ fumenPath: ctx.env.secondChart, audioPath: ctx.env.audioPath });
    const dockEditorId = dockOpen.payload?.editorId;
    const dockReady = await waitFor(async () => {
      const probe = (await api.listOpened()).payload.find((e) => e.editorId === dockEditorId);
      const count = probe ? probe.laneCount + probe.tapCount + probe.bellCount + probe.bpmChangeCount + probe.soflanCount : 0;
      return count > 0 ? probe : null;
    });
    ctx.check('the lane-free dock probe finished loading', dockReady.ok,
      dockReady.ok ? `after ${dockReady.waitedMs}ms` : 'timed out waiting for the chart to load');

    const dAdd = (objectType, args = {}) => api.addObject({ editorId: dockEditorId, objectType, ...args });
    const dModify = (objectId, propertyName, newValue, extra = {}) => api.modifyObject({ editorId: dockEditorId, objectId, propertyName, newValue, ...extra });
    const dFind = async (family, id) => (await api.family(dockEditorId, family)).find((o) => o.id === id);
    const dT = (n) => splitTotalGrid(base + (100 + n) * totalGridResolution);

    // two lanes, 10 XGrid units apart, each with a constant path across [dT(0), dT(2)]
    const nearLeft = await dAdd('lane', { ...dT(0), xGridUnit: 10, xGridGrid: 0, laneType: 'center' });
    const nearLeftRecord = (await dFind('lane', nearLeft.payload?.objectId))?.recordId;
    await dAdd('lanenext', { ...dT(1), xGridUnit: 10, xGridGrid: 0, parentRecordId: nearLeftRecord });
    await dAdd('lanenext', { ...dT(2), xGridUnit: 10, xGridGrid: 0, parentRecordId: nearLeftRecord });

    const nearRight = await dAdd('lane', { ...dT(0), xGridUnit: 20, xGridGrid: 0, laneType: 'left' });
    const nearRightRecord = (await dFind('lane', nearRight.payload?.objectId))?.recordId;
    await dAdd('lanenext', { ...dT(1), xGridUnit: 20, xGridGrid: 0, parentRecordId: nearRightRecord });
    await dAdd('lanenext', { ...dT(2), xGridUnit: 20, xGridGrid: 0, parentRecordId: nearRightRecord });
    ctx.check('two lanes with a path across the dock range are ready',
      Number.isInteger(nearLeftRecord) && Number.isInteger(nearRightRecord),
      `left=${nearLeftRecord} right=${nearRightRecord}`);

    const snapAt = dT(1);
    const dockedLeft = await dAdd('tap', { ...snapAt, xGridUnit: 11, xGridGrid: 0, dockMode: 'nearest' });
    const dockedLeftDto = await dFind('tap', dockedLeft.payload?.objectId);
    ctx.equal('the nearest lane wins for a tap next to it', dockedLeftDto?.referenceLaneRecordId, nearLeftRecord);
    ctx.check('the nearest tap was snapped onto that lane',
      dockedLeftDto?.xGrid?.unit === 10 && dockedLeftDto?.xGrid?.grid === 0,
      `xGrid=${JSON.stringify(dockedLeftDto?.xGrid)}`);

    const dockedRight = await dAdd('tap', { ...snapAt, xGridUnit: 18, xGridGrid: 0, dockMode: 'nearest' });
    const dockedRightDto = await dFind('tap', dockedRight.payload?.objectId);
    ctx.equal('a tap closer to the other lane docks there', dockedRightDto?.referenceLaneRecordId, nearRightRecord);
    ctx.check('that tap was snapped onto the second lane',
      dockedRightDto?.xGrid?.unit === 20 && dockedRightDto?.xGrid?.grid === 0,
      `xGrid=${JSON.stringify(dockedRightDto?.xGrid)}`);

    const tied = await dAdd('tap', { ...snapAt, xGridUnit: 15, xGridGrid: 0, dockMode: 'nearest' });
    const tiedDto = await dFind('tap', tied.payload?.objectId);
    ctx.equal('an exact tie falls back to the lower RecordId', tiedDto?.referenceLaneRecordId, Math.min(nearLeftRecord, nearRightRecord));

    const explicit = await dAdd('tap', { ...snapAt, xGridUnit: 0, xGridGrid: 0, referenceLaneRecordId: nearRightRecord, snapXToLane: true, dockMode: 'explicit' });
    ctx.ok("dockMode='explicit' keeps the explicit binding path", explicit);
    ctx.equal('the explicit mode still honors referenceLaneRecordId',
      (await dFind('tap', explicit.payload?.objectId))?.referenceLaneRecordId, nearRightRecord);

    ctx.fails('an unknown dockMode value is rejected',
      await dAdd('tap', { ...snapAt, xGridUnit: 10, xGridGrid: 0, dockMode: 'closest' }), 'INVALID_ARGUMENT');
    ctx.fails('dockMode=nearest is refused on a family that cannot dock',
      await dAdd('bpm', { ...snapAt, bpm: 180, dockMode: 'nearest' }), 'INVALID_ARGUMENT');
    ctx.fails('dockMode=nearest cannot be combined with referenceLaneRecordId',
      await dAdd('tap', { ...snapAt, xGridUnit: 10, xGridGrid: 0, dockMode: 'nearest', referenceLaneRecordId: nearLeftRecord }), 'INVALID_ARGUMENT');
    ctx.fails('dockMode=nearest cannot be combined with snapXToLane',
      await dAdd('tap', { ...snapAt, xGridUnit: 10, xGridGrid: 0, dockMode: 'nearest', referenceLaneRecordId: nearLeftRecord, snapXToLane: true }), 'INVALID_ARGUMENT');
    ctx.fails('dockMode=nearest fails when no lane covers the position',
      await dAdd('tap', { ...dT(50), xGridUnit: 10, xGridGrid: 0, dockMode: 'nearest' }), 'INVALID_ARGUMENT');

    // modify re-docks after the write: the written position steers the pick, and undo restores binding + XGrid
    const redock = await dAdd('tap', { ...snapAt, xGridUnit: 10, xGridGrid: 0, referenceLaneRecordId: nearLeftRecord, snapXToLane: true });
    const redockId = redock.payload?.objectId;
    const redockResponse = await dModify(redockId, 'xGridUnit', '19', { dockMode: 'nearest' });
    ctx.equal('modify_object(dockMode=nearest) echoes the mode', redockResponse.payload?.dockMode, 'nearest');
    ctx.equal('modify_object(dockMode=nearest) reports the implicit snap', redockResponse.payload?.snapped, true);
    const redockedDto = await dFind('tap', redockId);
    ctx.equal('the re-dock picked the lane closest to the written position', redockedDto?.referenceLaneRecordId, nearRightRecord);
    ctx.check('the re-dock snapped the tap onto the new lane',
      redockedDto?.xGrid?.unit === 20 && redockedDto?.xGrid?.grid === 0,
      `xGrid=${JSON.stringify(redockedDto?.xGrid)}`);

    ctx.ok('the re-dock is undoable', await api.undo({ editorId: dockEditorId }));
    const restoredDto = await dFind('tap', redockId);
    ctx.equal('undo restores the previous lane binding', restoredDto?.referenceLaneRecordId, nearLeftRecord);
    ctx.check('undo restores the previous XGrid',
      restoredDto?.xGrid?.unit === 10 && restoredDto?.xGrid?.grid === 0,
      `xGrid=${JSON.stringify(restoredDto?.xGrid)}`);

    const floating = await dAdd('tap', { ...snapAt, xGridUnit: 12, xGridGrid: 0 });
    const floated = await dModify(floating.payload?.objectId, 'tag', 'floating', { dockMode: 'nearest' });
    ctx.ok('a floating tap can be docked with dockMode=nearest', floated);
    ctx.equal('the floating tap now carries a lane binding',
      (await dFind('tap', floating.payload?.objectId))?.referenceLaneRecordId, nearLeftRecord);
    ctx.ok('the automatic dock is undoable', await api.undo({ editorId: dockEditorId }));
    const undockedDto = await dFind('tap', floating.payload?.objectId);
    ctx.check('undo removes the automatic binding again',
      undockedDto !== undefined && undockedDto.referenceLaneRecordId === undefined,
      `dto=${JSON.stringify(undockedDto)}`);
    ctx.check('undo also restores the floating position',
      undockedDto?.xGrid?.unit === 12 && undockedDto?.xGrid?.grid === 0,
      `xGrid=${JSON.stringify(undockedDto?.xGrid)}`);

    ctx.fails('modify_object rejects an unknown dockMode',
      await dModify(redockId, 'tag', 'x', { dockMode: 'nearest-ish' }), 'INVALID_ARGUMENT');
    ctx.fails('modify_object(dockMode=nearest) cannot write referenceLaneRecordId',
      await dModify(redockId, 'referenceLaneRecordId', String(nearLeftRecord), { dockMode: 'nearest' }), 'INVALID_ARGUMENT');
    ctx.fails('modify_object(dockMode=nearest) cannot be combined with snapXToLane',
      await dModify(redockId, 'tGridUnit', String(snapAt.tGridUnit), { dockMode: 'nearest', snapXToLane: true }), 'INVALID_ARGUMENT');
    const notDockable = await dAdd('comment', { ...snapAt, content: 'not dockable' });
    ctx.fails('modify_object(dockMode=nearest) is refused on a non-dockable object',
      await dModify(notDockable.payload?.objectId, 'content', 'still floating', { dockMode: 'nearest' }), 'INVALID_ARGUMENT');
    ctx.fails('modify_object(dockMode=nearest) fails when no lane covers the position',
      await dModify(redockId, 'tGridUnit', String(dT(50).tGridUnit), { dockMode: 'nearest' }), 'INVALID_ARGUMENT');

    ctx.ok('the dock probe editor is closed again', await api.close({ editorId: dockEditorId, force: true }));

    // ---------------- curvecontrol ----------------
    ctx.section('curvecontrol');

    ctx.fails('curvecontrol without referenceObjectId is rejected',
      await add('curvecontrol', { ...T(2) }), 'INVALID_ARGUMENT');
    ctx.fails('curvecontrol with an unknown referenceObjectId is rejected',
      await add('curvecontrol', { ...T(2), referenceObjectId: 999999 }), 'INVALID_ARGUMENT');

    const segmentId = segment1.payload.objectId;
    const control = await add('curvecontrol', { ...T(2), referenceObjectId: segmentId });
    ctx.ok('adding a curve control point to a segment succeeds', control);
    const controlId = control.payload.objectId;
    const controlDto = await find('curvecontrol', controlId);
    ctx.hasKeys('curvecontrol DTO exposes the auxiliary owner fields', controlDto, ['isAuxiliary', 'ownerObjectId', 'ownerObjectType']);
    ctx.equal('the control point is marked auxiliary', controlDto.isAuxiliary, true);
    ctx.equal('the control point reports its owning segment', controlDto.ownerObjectId, segmentId);
    ctx.equal('the owner type is the lane segment family', controlDto.ownerObjectType, 'lanenext');

    const controls = (await api.family(editorId, 'curvecontrol')).filter((c) => c.ownerObjectId === segmentId);
    ctx.equal('the segment now carries one control point', controls.length, 1);

    // auxiliary objects can be hidden from queries, and ordinary objects never carry owner fields
    const withAuxiliary = await api.queryObject({ editorId, objectTypes: ['curvecontrol', 'tap'], limit: 2000 });
    ctx.ok('a multi-family query including auxiliaries runs', withAuxiliary);
    ctx.equal('includeAuxiliary defaults to true', withAuxiliary.payload.includeAuxiliary, true);
    const auxiliaryCount = withAuxiliary.payload.objects.filter((o) => o.isAuxiliary).length;
    ctx.check('the query sees the curve control auxiliaries', auxiliaryCount > 0, `count=${auxiliaryCount}`);

    const withoutAuxiliary = await api.queryObject({ editorId, objectTypes: ['curvecontrol', 'tap'], includeAuxiliary: false, limit: 2000 });
    ctx.ok('includeAuxiliary=false is accepted', withoutAuxiliary);
    ctx.equal('includeAuxiliary=false echoes the filter', withoutAuxiliary.payload.includeAuxiliary, false);
    ctx.equal('includeAuxiliary=false drops exactly the auxiliaries',
      withoutAuxiliary.payload.count, withAuxiliary.payload.count - auxiliaryCount);
    ctx.check('the filtered page contains no auxiliary items',
      withoutAuxiliary.payload.objects.every((o) => o.isAuxiliary === false));

    const hiddenControls = await api.queryObject({ editorId, objectType: 'curvecontrol', includeAuxiliary: false, limit: 2000 });
    ctx.equal('an explicit auxiliary family can be hidden too', hiddenControls.payload.count, 0);

    const tapSample = withAuxiliary.payload.objects.find((o) => o.type === 'tap');
    ctx.equal('ordinary objects are not auxiliary', tapSample?.isAuxiliary, false);
    ctx.equal('ordinary objects have no owner id', tapSample?.ownerObjectId, undefined);
    ctx.equal('ordinary objects have no owner type', tapSample?.ownerObjectType, undefined);

    const controlX = await modify(controlId, 'xGridGrid', '240');
    ctx.ok('a curve control point can be moved', controlX);
    ctx.equal('the move is visible', (await find('curvecontrol', controlId))?.xGrid.grid, 240);

    ctx.fails('curvecontrol does not accept endTGrid',
      await modify(controlId, 'endTGridUnit', '5'), 'UNSUPPORTED_PROPERTY');

    // removal clears the back-reference, so undo has to re-attach the captured owner
    await verifyUndoRedo(ctx, {
      label: 'remove_object(curvecontrol)',
      mutate: () => remove(controlId),
      applied: async () => (await find('curvecontrol', controlId)) === undefined,
      reverted: async () => !!(await find('curvecontrol', controlId)),
    });

    // a control point added via a lane whose segment was removed must not survive
    const orphanControl = await add('curvecontrol', { ...T(3), referenceObjectId: segmentId });
    ctx.ok('a second control point can be added', orphanControl);
    ctx.ok('removing the owning segment also removes its control point',
      await remove(segmentId));
    ctx.check('the orphaned control point is gone with its segment',
      (await find('curvecontrol', orphanControl.payload.objectId)) === undefined);
    await api.undo({ editorId });

    // ---------------- beams ----------------
    ctx.section('beam / beamnext');

    const beamResponse = await add('beam', { ...T(1), xGridUnit: 0, xGridGrid: 0, widthId: 3 });
    ctx.ok('add_object(beam, widthId=3) succeeds', beamResponse);
    const beamId = beamResponse.payload.objectId;
    const beamDto = await find('beam', beamId);
    ctx.hasKeys('beam DTO exposes recordId / widthId / isObliqueBeam', beamDto,
      ['recordId', 'widthId', 'isObliqueBeam']);
    ctx.equal('the beam reports the requested widthId', beamDto.widthId, 3);
    ctx.equal('a straight beam is not oblique', beamDto.isObliqueBeam, false);
    ctx.equal('a straight beam omits the oblique source', beamDto.obliqueSourceXGrid, undefined);
    const beamRecord = beamDto.recordId;

    const beamSegment = await add('beamnext', { ...T(2), xGridUnit: 0, xGridGrid: 0, parentRecordId: beamRecord });
    ctx.ok('adding a beamnext segment succeeds', beamSegment);
    ctx.equal('the beam owns one segment', (await api.family(editorId, 'beamnext')).filter((s) => s.parentRecordId === beamRecord).length, 1);

    const oblique = await modify(beamId, 'obliqueSourceXGridGrid', '120');
    ctx.ok('setting the oblique source grid succeeds', oblique);
    const obliqueDto = await find('beam', beamId);
    ctx.equal('the beam became oblique', obliqueDto.isObliqueBeam, true);
    ctx.equal('the oblique source grid component is stored', obliqueDto.obliqueSourceXGrid?.grid, 120);

    ctx.ok('clearing the oblique source with an empty value succeeds',
      await modify(beamId, 'obliqueSourceXGridGrid', ''));
    ctx.equal('clearing the oblique source makes the beam straight again', (await find('beam', beamId))?.isObliqueBeam, false);

    ctx.fails('widthId above the range is rejected', await modify(beamId, 'widthId', '9'), 'INVALID_ARGUMENT');
    ctx.fails('widthId below the range is rejected', await modify(beamId, 'widthId', '0'), 'INVALID_ARGUMENT');
    ctx.fails('a non-numeric widthId is rejected', await modify(beamId, 'widthId', 'wide'), 'INVALID_ARGUMENT');
    ctx.equal('a rejected widthId leaves the beam untouched', (await find('beam', beamId))?.widthId, 3);
    for (const id of [1, 2, 3, 4, 5]) {
      ctx.ok(`widthId ${id} is accepted`, await modify(beamId, 'widthId', String(id)));
    }
    await modify(beamId, 'widthId', '3');

    await verifyUndoRedo(ctx, {
      label: 'modify_object(beam.widthId)',
      mutate: () => modify(beamId, 'widthId', '5'),
      applied: async () => (await find('beam', beamId))?.widthId === 5,
      reverted: async () => (await find('beam', beamId))?.widthId === 3,
    });

    // ---------------- isfarea / laneblock ----------------
    ctx.section('isfarea / laneblock');

    ctx.fails('isfarea without endTGrid is rejected',
      await add('isfarea', { ...T(1), xGridUnit: 0, xGridGrid: 0 }), 'INVALID_ARGUMENT');
    ctx.fails('laneblock without endTGrid is rejected',
      await add('laneblock', { ...T(1), xGridUnit: 0, xGridGrid: 0, blockDirection: 'left' }), 'INVALID_ARGUMENT');

    const areaEnd = T(4);
    const area = await add('isfarea', {
      ...T(1), xGridUnit: 0, xGridGrid: 0,
      endTGridUnit: areaEnd.tGridUnit, endTGridGrid: areaEnd.tGridGrid,
    });
    ctx.ok('adding an IndividualSoflanArea succeeds', area);
    const areaDto = await find('isfarea', area.payload.objectId);
    ctx.hasKeys('isfarea DTO exposes endXGrid and areaWidth', areaDto, ['endXGrid', 'areaWidth']);
    ctx.equal('the area end lands on the requested TGrid', areaDto.tGrid && totalOf({ tGridUnit: areaEnd.tGridUnit, tGridGrid: areaEnd.tGridGrid }), totalOf(areaEnd));

    const areaX = await modify(area.payload.objectId, 'endXGridGrid', '300');
    ctx.ok('moving the area end on XGrid succeeds', areaX);
    ctx.equal('the area end XGrid is stored', (await find('isfarea', area.payload.objectId))?.endXGrid?.grid, 300);

    ctx.fails('isfarea rejects blockDirection', await modify(area.payload.objectId, 'blockDirection', 'left'), 'UNSUPPORTED_PROPERTY');

    const block = await add('laneblock', {
      ...T(1), xGridUnit: 0, xGridGrid: 0, blockDirection: 'right',
      endTGridUnit: areaEnd.tGridUnit, endTGridGrid: areaEnd.tGridGrid,
    });
    ctx.ok('adding a LaneBlockArea succeeds', block);
    const blockDto = await find('laneblock', block.payload.objectId);
    ctx.equal('the lane block reports its direction', blockDto.blockDirection, 'Right');

    ctx.ok('flipping the lane block direction succeeds', await modify(block.payload.objectId, 'blockDirection', 'left'));
    ctx.equal('the flipped direction is stored', (await find('laneblock', block.payload.objectId))?.blockDirection, 'Left');
    ctx.fails('an unknown blockDirection is rejected', await modify(block.payload.objectId, 'blockDirection', 'sideways'), 'INVALID_ARGUMENT');
    ctx.fails('laneblock rejects endXGrid', await modify(block.payload.objectId, 'endXGridGrid', '10'), 'UNSUPPORTED_PROPERTY');

    await verifyUndoRedo(ctx, {
      label: 'modify_object(laneblock.blockDirection)',
      mutate: () => modify(block.payload.objectId, 'blockDirection', 'right'),
      applied: async () => (await find('laneblock', block.payload.objectId))?.blockDirection === 'Right',
      reverted: async () => (await find('laneblock', block.payload.objectId))?.blockDirection === 'Left',
    });

    // ---------------- colorful lanes ----------------
    ctx.section('colorful lanes and autoplay fader');

    const plain = await add('lane', { ...T(5), xGridUnit: 0, xGridGrid: 0, laneType: 'center' });
    ctx.ok('a plain (non-colorful) lane is created', plain);
    ctx.fails('colorId is rejected on a non-colorful lane',
      await modify(plain.payload.objectId, 'colorId', 'Akari'), 'UNSUPPORTED_PROPERTY');

    const colorful = await add('lane', { ...T(6), xGridUnit: 0, xGridGrid: 0, laneType: 'colorful', colorId: 'LaneG', brightness: 2 });
    ctx.ok('adding a colorful lane with colorId + brightness succeeds', colorful);
    const colorfulId = colorful.payload.objectId;
    const colorfulDto = await find('lane', colorfulId);
    ctx.hasKeys('colorful lane DTO exposes colorId / colorName / brightness', colorfulDto, ['colorId', 'colorName', 'brightness']);
    ctx.equal('the colorful lane reports the requested colour', colorfulDto.colorName, 'LaneG');
    ctx.check('colorId is a numeric id', Number.isInteger(colorfulDto.colorId), `colorId=${colorfulDto.colorId}`);
    ctx.equal('the colorful lane reports the requested brightness', colorfulDto.brightness, 2);

    const numericId = String(colorfulDto.colorId);
    ctx.ok('colorId accepts a numeric id as well as a name', await modify(colorfulId, 'colorId', numericId));
    ctx.equal('the numeric form resolved to the same colour', (await find('lane', colorfulId))?.colorName, 'LaneG');
    ctx.fails('an unknown colour name is rejected', await modify(colorfulId, 'colorId', 'Ultraviolet'), 'INVALID_ARGUMENT');
    ctx.fails('an unknown numeric colour id is rejected', await modify(colorfulId, 'colorId', '9999'), 'INVALID_ARGUMENT');

    await verifyUndoRedo(ctx, {
      label: 'modify_object(lane.colorId)',
      mutate: () => modify(colorfulId, 'colorId', 'Akari'),
      applied: async () => (await find('lane', colorfulId))?.colorName === 'Akari',
      reverted: async () => (await find('lane', colorfulId))?.colorName === 'LaneG',
    });

    await verifyUndoRedo(ctx, {
      label: 'modify_object(lane.brightness)',
      mutate: () => modify(colorfulId, 'brightness', '5'),
      applied: async () => (await find('lane', colorfulId))?.brightness === 5,
      reverted: async () => (await find('lane', colorfulId))?.brightness === 2,
    });

    const fader = await add('lane', { ...T(7), xGridUnit: 0, xGridGrid: 0, laneType: 'autoplayfader' });
    ctx.ok('add_object(lane, autoplayfader) succeeds', fader);
    ctx.equal('the autoplay fader lane reports its laneType',
      (await find('lane', fader.payload.objectId))?.laneType, 'AutoPlayFader');
    const faderAlias = await add('lane', { ...T(8), xGridUnit: 0, xGridGrid: 0, laneType: 'autoplayfaderlane' });
    ctx.ok('the autoplayfaderlane alias is accepted too', faderAlias);

    // ---------------- lane transparency ----------------
    ctx.section('lane isTransparent');

    const faded = await add('lane', { ...T(10), xGridUnit: 0, xGridGrid: 0, laneType: 'center', isTransparent: true });
    ctx.ok('add_object(lane) accepts isTransparent', faded);
    const fadedId = faded.payload.objectId;

    const transparencyOff = await modify(fadedId, 'isTransparent', 'false');
    ctx.ok('modify_object writes isTransparent', transparencyOff);
    ctx.equal('the add-time isTransparent reads back through the modify echo', transparencyOff.payload?.oldValue, 'True');
    ctx.equal('the write reports the new value', transparencyOff.payload?.newValue, 'false');

    await modify(fadedId, 'isTransparent', 'true');
    await api.undo({ editorId });
    await api.redo({ editorId });
    const transparencyEcho2 = await modify(fadedId, 'isTransparent', 'false');
    ctx.equal('undo/redo round-trips isTransparent', transparencyEcho2.payload?.oldValue, 'True');
    await api.undo({ editorId });

    ctx.equal('a lane without the flag defaults to opaque',
      (await modify(plain.payload.objectId, 'isTransparent', 'false')).payload?.oldValue, 'False');

    ctx.fails('isTransparent is refused on a tap',
      await add('tap', { ...T(10), xGridUnit: 0, xGridGrid: 0, isTransparent: true }), 'INVALID_ARGUMENT');
    ctx.fails('isTransparent is refused on a beam',
      await add('beam', { ...T(10), xGridUnit: 0, xGridGrid: 0, isTransparent: true }), 'INVALID_ARGUMENT');
    ctx.fails('modify refuses isTransparent on a lanenext segment',
      await modify(segment1.payload.objectId, 'isTransparent', 'true'), 'UNSUPPORTED_PROPERTY');
    ctx.fails('an unparsable isTransparent is rejected',
      await modify(fadedId, 'isTransparent', 'maybe'), 'INVALID_ARGUMENT');

    // ---------------- cross-family misuse ----------------
    ctx.section('cross-family properties are refused, not ignored');

    ctx.fails('widthId is refused on a lanenext segment',
      await modify(segment1.payload.objectId, 'widthId', '2'), 'UNSUPPORTED_PROPERTY');
    ctx.fails('colorId is refused on a beam',
      await modify(beamId, 'colorId', 'Akari'), 'UNSUPPORTED_PROPERTY');
    ctx.fails('endXGridUnit is refused on a plain lane',
      await modify(plain.payload.objectId, 'endXGridUnit', '5'), 'UNSUPPORTED_PROPERTY');
    ctx.fails('blockDirection is refused on a plain lane',
      await modify(plain.payload.objectId, 'blockDirection', 'left'), 'UNSUPPORTED_PROPERTY');
    ctx.fails('obliqueSourceXGridUnit is refused on a plain lane',
      await modify(plain.payload.objectId, 'obliqueSourceXGridUnit', '1'), 'UNSUPPORTED_PROPERTY');
    ctx.fails('endTGrid is refused on a lane start',
      await modify(plain.payload.objectId, 'endTGridUnit', '9'), 'UNSUPPORTED_PROPERTY');

    ctx.fails('add_object(lane) refuses parentRecordId',
      await add('lane', { ...T(9), xGridUnit: 0, xGridGrid: 0, laneType: 'center', parentRecordId: 1 }), 'INVALID_ARGUMENT');
    ctx.fails('add_object(lane) refuses referenceObjectId',
      await add('lane', { ...T(9), xGridUnit: 0, xGridGrid: 0, laneType: 'center', referenceObjectId: 1 }), 'INVALID_ARGUMENT');
    ctx.fails('add_object(tap) refuses widthId',
      await add('tap', { ...T(9), xGridUnit: 0, xGridGrid: 0, widthId: 2 }), 'INVALID_ARGUMENT');
    ctx.fails('add_object(lane) refuses endXGrid',
      await add('lane', { ...T(9), xGridUnit: 0, xGridGrid: 0, laneType: 'center', endXGridUnit: 1 }), 'INVALID_ARGUMENT');

    // ---------------- undo / redo across the new families ----------------
    ctx.section('undo / redo across the connectable families');

    let segmentId3 = null;
    await verifyUndoRedo(ctx, {
      label: 'add_object(lanenext)',
      mutate: async () => {
        const response = await add('lanenext', { ...T(12), xGridUnit: 0, xGridGrid: 0, parentRecordId: record });
        segmentId3 = response.payload?.objectId;
        return response;
      },
      applied: async () => !!(await find('lanenext', segmentId3)),
      reverted: async () => (await find('lanenext', segmentId3)) === undefined,
    });

    let beam2 = null;
    await verifyUndoRedo(ctx, {
      label: 'add_object(beam)',
      mutate: async () => {
        const response = await add('beam', { ...T(13), xGridUnit: 0, xGridGrid: 0 });
        beam2 = response.payload?.objectId;
        return response;
      },
      applied: async () => !!(await find('beam', beam2)),
      reverted: async () => (await find('beam', beam2)) === undefined,
    });

    let area2 = null;
    await verifyUndoRedo(ctx, {
      label: 'add_object(isfarea)',
      mutate: async () => {
        const response = await add('isfarea', {
          ...T(14), xGridUnit: 0, xGridGrid: 0,
          endTGridUnit: T(16).tGridUnit, endTGridGrid: T(16).tGridGrid,
        });
        area2 = response.payload?.objectId;
        return response;
      },
      applied: async () => !!(await find('isfarea', area2)),
      reverted: async () => (await find('isfarea', area2)) === undefined,
    });

    let block2 = null;
    await verifyUndoRedo(ctx, {
      label: 'add_object(laneblock)',
      mutate: async () => {
        const response = await add('laneblock', {
          ...T(14), xGridUnit: 0, xGridGrid: 0, blockDirection: 'left',
          endTGridUnit: T(16).tGridUnit, endTGridGrid: T(16).tGridGrid,
        });
        block2 = response.payload?.objectId;
        return response;
      },
      applied: async () => !!(await find('laneblock', block2)),
      reverted: async () => (await find('laneblock', block2)) === undefined,
    });

    let control2 = null;
    await verifyUndoRedo(ctx, {
      label: 'add_object(curvecontrol)',
      mutate: async () => {
        const response = await add('curvecontrol', { ...T(2), referenceObjectId: segment1.payload.objectId });
        control2 = response.payload?.objectId;
        return response;
      },
      applied: async () => !!(await find('curvecontrol', control2)),
      reverted: async () => (await find('curvecontrol', control2)) === undefined,
    });

    ctx.check('the curve control undo/redo cycle left the owner intact',
      !!(await find('lanenext', segment1.payload.objectId)));
    ctx.check('the lane still owns both segments after the control-point cycles',
      (await segments(record)).length === 2, `count=${(await segments(record)).length}`);

    // ---------------- cleanup ----------------
    ctx.section('cleanup');
    for (const objectId of [laneId, snappingLane.payload.objectId, beamId, plain.payload.objectId,
      colorfulId, fader.payload.objectId, faderAlias.payload.objectId, docked.payload.objectId,
      area.payload.objectId, block.payload.objectId, fadedId,
      segment1.payload.objectId, segment2.payload.objectId]) {
      if (Number.isInteger(objectId)) await remove(objectId);
    }
    ctx.check('the lane created by this suite keeps no segments once it is gone',
      (await api.family(editorId, 'lanenext')).filter((s) => s.parentRecordId === record).length === 0);
  },
};
