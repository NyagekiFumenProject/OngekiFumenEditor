// Suite 00 — the tool surface contract itself.
//
// Nothing here needs an editor: it asserts what the host advertises over MCP and what
// the bundled skill documentation claims. A failure means either a tool was added or
// removed without updating tests/mcp-e2e/lib/coverage.mjs, or the docs drifted.

import { EXPECTED_TOOLS, READ_ONLY_TOOLS } from '../lib/coverage.mjs';

export default {
  name: '00-tool-surface',
  description: 'tools/list + resources contract',

  async run(ctx) {
    const { tools } = await ctx.client.listTools();
    const names = tools.map((t) => t.name).sort();

    ctx.equal('tools/list returns exactly 28 tools', tools.length, 28);
    ctx.check('the advertised tool set matches the expected set',
      JSON.stringify(names) === JSON.stringify([...EXPECTED_TOOLS].sort()),
      `missing=[${EXPECTED_TOOLS.filter((n) => !names.includes(n))}] extra=[${names.filter((n) => !EXPECTED_TOOLS.includes(n))}]`);

    const byName = new Map(tools.map((t) => [t.name, t]));

    const undocumented = names.filter((n) => !byName.get(n)?.description || byName.get(n).description.length < 20);
    ctx.check('every tool ships a meaningful description', undocumented.length === 0, `weak: ${undocumented.join(', ')}`);

    const noSchema = names.filter((n) => !byName.get(n)?.inputSchema?.type);
    ctx.check('every tool ships an inputSchema', noSchema.length === 0, `missing: ${noSchema.join(', ')}`);

    const wronglyMarked = names.filter((n) => {
      const hint = byName.get(n)?.annotations?.readOnlyHint;
      return READ_ONLY_TOOLS.includes(n) ? hint !== true : hint === true;
    });
    ctx.check('readOnlyHint matches the read-only tool list', wronglyMarked.length === 0, `wrong: ${wronglyMarked.join(', ')}`);

    // ---- required-parameter contract for the tools that gate on input ----
    const required = (name) => (byName.get(name)?.inputSchema?.required ?? []).slice().sort();
    const expectsRequired = {
      'editor.add_object': ['objectType'],
      'editor.create_proj': ['audioPath'],
      'editor.create_hold_end': ['holdObjectId', 'tGridUnit'],
      'editor.modify_bullet_pallete': ['newValue', 'propertyName', 'strId'],
      'editor.modify_object': ['newValue', 'objectId', 'propertyName'],
      'editor.open_fast': ['fumenPath'],
      'editor.open_proj': ['projectPath'],
      'editor.query_object': ['objectType'],
      'editor.remove_bullet_pallete': ['strId'],
      'editor.remove_hold_end': ['holdObjectId'],
      'editor.remove_object': ['objectId'],
      'editor.set_metainfo': ['metainfoName', 'newValue'],
      'script.compile': ['scriptText'],
      'script.run_current_editor': ['scriptText'],
      'script.run_editor': ['editorId', 'scriptText'],
    };
    for (const [toolName, expected] of Object.entries(expectsRequired)) {
      ctx.check(`${toolName} still requires [${expected.join(', ')}]`,
        JSON.stringify(required(toolName)) === JSON.stringify(expected),
        `required=[${required(toolName)}]`);
    }

    // ---- add_object / modify_object parameter surface (the widest contract) ----
    const addProps = Object.keys(byName.get('editor.add_object')?.inputSchema?.properties ?? {});
    const addMustHave = [
      'objectType', 'tGridUnit', 'tGridGrid', 'xGridUnit', 'xGridGrid', 'laneType', 'soflanType',
      'endTGridUnit', 'endTGridGrid', 'bulletPalleteStrId', 'referenceLaneRecordId', 'snapXToLane',
      'parentRecordId', 'referenceObjectId', 'widthId', 'obliqueSourceXGridUnit', 'obliqueSourceXGridGrid',
      'colorId', 'brightness', 'endXGridUnit', 'endXGridGrid', 'blockDirection', 'editorId', 'expectedEditorId',
    ];
    ctx.check('add_object still exposes the full parameter surface',
      addMustHave.every((p) => addProps.includes(p)),
      `missing: ${addMustHave.filter((p) => !addProps.includes(p)).join(', ')}`);

    const modProps = Object.keys(byName.get('editor.modify_object')?.inputSchema?.properties ?? {});
    const modMustHave = ['objectId', 'propertyName', 'newValue', 'snapXToLane', 'editorId', 'expectedEditorId'];
    ctx.check('modify_object still exposes the full parameter surface',
      modMustHave.every((p) => modProps.includes(p)),
      `missing: ${modMustHave.filter((p) => !modProps.includes(p)).join(', ')}`);

    // ---- bundled skill docs are served from the deployed copy ----
    const { resources } = await ctx.client.listResources();
    const uris = resources.map((r) => r.uri);
    ctx.check('resources/list exposes skill://index', uris.includes('skill://index'), `count=${uris.length}`);
    ctx.check('resources/list exposes the MCP reference page',
      uris.includes('skill://ongeki-fumen-editor/references/05-runtime-automation-and-mcp.md'));

    const doc = await ctx.client.readResource('skill://ongeki-fumen-editor/references/05-runtime-automation-and-mcp.md');
    const text = doc.contents?.[0]?.text ?? '';
    ctx.check('the served MCP reference page is non-trivial', text.length > 5000, `length=${text.length}`);
    for (const probe of ['editor.check', 'editor.set_metainfo', 'lanenext', 'curvecontrol', 'isfarea', 'laneblock', 'colorId', 'snapXToLane']) {
      ctx.check(`served doc mentions '${probe}'`, text.includes(probe));
    }
  },
};
