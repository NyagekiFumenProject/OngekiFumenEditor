// Suite 90 — document lifecycle.
//
// open_fast / create_proj / open_proj / close are the only tools that add or remove
// editors. They run last because `close` is allowed to empty the editor list, and this
// suite is responsible for handing exactly one seeded editor back to whatever runs after it.

import { waitFor } from '../lib/harness.mjs';

export default {
  name: '90-document-lifecycle',
  description: 'create_proj / open_fast / open_proj / close',

  async run(ctx) {
    const api = ctx.api;
    const env = ctx.env;
    ctx.cover('editor.create_proj', 'editor.open_fast', 'editor.open_proj', 'editor.close',
      'editor.list_opened', 'editor.get_current');

    const listIds = async () => (await api.listOpened()).payload.map((e) => e.editorId);

    // ---------------- open_fast ----------------
    ctx.section('editor.open_fast');

    ctx.fails('open_fast rejects a missing file',
      await api.openFast({ fumenPath: 'C:/definitely/not/here.ogkr' }), 'FILE_NOT_FOUND');
    ctx.fails('open_fast rejects a missing audio file',
      await api.openFast({ fumenPath: env.seededChart, audioPath: 'C:/definitely/not/here.wav' }), 'AUDIO_NOT_FOUND');
    ctx.fails('open_fast refuses a project file (use open_proj instead)',
      await api.openFast({ fumenPath: env.projectPath, audioPath: env.audioPath }), 'UNSUPPORTED_FILE_TYPE');

    const secondOpen = await api.openFast({ fumenPath: env.secondChart, audioPath: env.audioPath });
    ctx.ok('open_fast opens a second chart', secondOpen);
    ctx.hasKeys('open_fast response shape', secondOpen.payload, ['success', 'opened', 'fumenPath', 'audioPath', 'editorId']);
    ctx.equal('the response reports the chart that was opened', secondOpen.payload.fumenPath, env.secondChart);
    ctx.equal('the response reports the audio that was paired with it', secondOpen.payload.audioPath, env.audioPath);
    ctx.equal('the new editor is marked opened', secondOpen.payload.opened, true);

    const secondId = secondOpen.payload.editorId;
    ctx.check('the second editor is a different instance', secondId !== ctx.editorId, `first=${ctx.editorId} second=${secondId}`);
    const afterSecondOpen = await listIds();
    ctx.equal('two editors are now open', afterSecondOpen.length, 2);
    ctx.check('both editors are listed', afterSecondOpen.includes(ctx.editorId) && afterSecondOpen.includes(secondId), afterSecondOpen.join(', '));

    const secondSummary = await api.getCurrentSummary();
    ctx.check('the newly opened editor became the active one', secondSummary.payload?.editorId === secondId,
      `active=${secondSummary.payload?.editorId}`);

    // ---------------- create_proj ----------------
    ctx.section('editor.create_proj');

    ctx.fails('create_proj rejects a missing audio file',
      await api.createProject({ audioPath: 'C:/definitely/not/here.wav' }), 'AUDIO_NOT_FOUND');
    ctx.fails('create_proj rejects a missing seed chart',
      await api.createProject({ audioPath: env.audioPath, fumenPath: 'C:/definitely/not/here.ogkr' }), 'FILE_NOT_FOUND');

    const created = await api.createProject({ audioPath: env.audioPath, fumenPath: env.seededChart });
    ctx.ok('create_proj creates a project from audio + seeded chart', created);
    ctx.hasKeys('create_proj response shape', created.payload, ['success', 'opened', 'fumenPath', 'audioPath', 'editorId']);
    const createdId = created.payload.editorId;
    const afterCreate = await listIds();
    ctx.equal('a third editor is now open', afterCreate.length, 3);
    ctx.check('the created editor is listed', afterCreate.includes(createdId), afterCreate.join(', '));

    const createdSummary = await api.getCurrentSummary();
    ctx.check('the created project exposes counts (the seed chart was loaded)',
      (createdSummary.payload?.counts?.taps ?? 0) > 0,
      `taps=${createdSummary.payload?.counts?.taps}`);

    await api.close({ editorId: createdId, force: true });

    // ---------------- open_proj ----------------
    ctx.section('editor.open_proj');

    ctx.fails('open_proj rejects a missing file',
      await api.openProject({ projectPath: 'C:/definitely/not/here.nyagekiProj' }), 'FILE_NOT_FOUND');
    ctx.fails('open_proj refuses a plain chart (use open_fast instead)',
      await api.openProject({ projectPath: env.seededChart }), 'UNSUPPORTED_FILE_TYPE');

    const project = await api.openProject({ projectPath: env.projectPath });
    ctx.ok('open_proj opens a .nyagekiProj project', project);
    ctx.hasKeys('open_proj response shape', project.payload, ['success', 'opened', 'editorId']);
    const projectId = project.payload.editorId;
    ctx.check('the project editor is a new instance',
      projectId !== ctx.editorId && projectId !== secondId,
      `project=${projectId}`);
    ctx.equal('three editors are open again', (await listIds()).length, 3);

    // A project tab is created before its chart and audio finish loading (the editor shows a
    // loading dialog meanwhile), so wait for the observable state rather than sampling once.
    const loaded = await waitFor(async () => {
      const summary = (await api.getCurrentSummary()).payload;
      return summary?.counts?.taps > 0 ? summary : null;
    });
    ctx.check('the project loads its chart', loaded.ok,
      loaded.ok ? `taps=${loaded.last.counts.taps} after ${loaded.waitedMs}ms` : 'timed out waiting for the chart to load');

    const projectSummary = loaded.ok ? loaded.last : (await api.getCurrentSummary()).payload;
    ctx.equal('the loaded chart is the one the project referenced', projectSummary?.fumenPath, env.seededChart);
    ctx.equal('the summary reports the project file it came from', projectSummary?.projectPath, env.projectPath);

    // ---------------- close ----------------
    ctx.section('editor.close');

    ctx.fails('close rejects an unknown editorId',
      await api.close({ editorId: 'editor-does-not-exist' }), 'EDITOR_NOT_FOUND');

    // a clean editor closes without force
    const cleanClose = await api.close({ editorId: projectId });
    ctx.ok('close closes a clean editor', cleanClose);
    ctx.equal('the clean close reports nothing was discarded', cleanClose.payload?.wasDirty, false);
    ctx.check('the closed editor left the list', !(await listIds()).includes(projectId));

    // A dirty editor is refused until force is passed. Only a *property change* marks a chart
    // dirty (OngekiFumen.ObjectModifiedChanged), so add an object and then edit it — adding
    // alone does not flip the flag.
    const dirtyTarget = secondId;
    const dirtyTap = await api.addObject({
      editorId: dirtyTarget, objectType: 'tap', tGridUnit: 1000, tGridGrid: 0, xGridUnit: 0, xGridGrid: 0,
    });
    ctx.ok('a mutation lands on the second editor', dirtyTap);

    const dirtyEdit = await api.modifyObject({
      editorId: dirtyTarget, objectId: dirtyTap.payload?.objectId, propertyName: 'isCritical', newValue: 'true',
    });
    ctx.ok('editing the added object succeeds', dirtyEdit);

    const dirtyState = (await api.listOpened()).payload.find((e) => e.editorId === dirtyTarget);
    ctx.equal('editing the chart marks the editor dirty', dirtyState?.isDirty, true);

    ctx.fails('close refuses a dirty editor without force',
      await api.close({ editorId: dirtyTarget }), 'EDITOR_DIRTY');
    ctx.check('the refused editor is still open', (await listIds()).includes(dirtyTarget));

    const forcedClose = await api.close({ editorId: dirtyTarget, force: true });
    ctx.ok('close discards the unsaved changes when forced', forcedClose);
    ctx.equal('the forced close reports the discarded changes', forcedClose.payload?.discardedUnsavedChanges, true);
    ctx.equal('the forced close reports the editor was dirty', forcedClose.payload?.wasDirty, true);
    ctx.check('the forced close removed the editor', !(await listIds()).includes(dirtyTarget));

    // ---------------- no active editor ----------------
    ctx.section('empty editor list');

    const seedClose = await api.close({ editorId: ctx.editorId, force: true });
    ctx.ok('the last editor can be closed', seedClose);
    ctx.equal('no editors remain open', (await listIds()).length, 0);
    ctx.equal('get_current returns null with nothing open', (await api.getCurrent()).payload, null);
    ctx.fails('get_current_summary reports there is nothing to summarise',
      await api.getCurrentSummary(), 'NO_ACTIVE_EDITOR');
    ctx.fails('an editor-scoped tool without an editorId reports NO_ACTIVE_EDITOR',
      await api.check({}), 'NO_ACTIVE_EDITOR');
    ctx.fails('scrolling without an editorId reports NO_ACTIVE_EDITOR',
      await api.scrollTo({ tGridUnit: 1, tGridGrid: 0 }), 'NO_ACTIVE_EDITOR');

    // ---------------- restore a single seeded editor for the rest of the run ----------------
    ctx.section('restore');

    const restored = await api.openFast({ fumenPath: env.seededChart, audioPath: env.audioPath });
    ctx.ok('the seed editor is reopened', restored);
    ctx.editorId = restored.payload.editorId;
    ctx.equal('exactly one editor is open again', (await listIds()).length, 1);
    ctx.equal('the restored editor is the active one', (await api.getCurrent()).payload?.editorId, ctx.editorId);
    ctx.equal('the restored chart is clean', (await api.getCurrent()).payload?.isDirty, false);
  },
};
