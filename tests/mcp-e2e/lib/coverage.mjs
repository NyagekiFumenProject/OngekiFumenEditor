// The full tool surface the suites must cover.
//
// Keep this list in sync with the `[McpServerTool(Name = ...)]` declarations under
// OngekiFumenEditor/Kernel/Mcp/Tools/. `suites/99-coverage.mjs` fails the run when a
// tool disappears from here without a deliberate edit, or when a tool is listed but no
// suite exercises it.

export const EXPECTED_TOOLS = [
  'editor.add_object',
  'editor.begin_action',
  'editor.check',
  'editor.close',
  'editor.create_bullet_pallete',
  'editor.create_hold_end',
  'editor.create_proj',
  'editor.end_action',
  'editor.get_current',
  'editor.get_current_summary',
  'editor.list_opened',
  'editor.modify_bullet_pallete',
  'editor.modify_object',
  'editor.open_fast',
  'editor.open_proj',
  'editor.query_bullet_pallete',
  'editor.query_object',
  'editor.redo',
  'editor.remove_bullet_pallete',
  'editor.remove_hold_end',
  'editor.remove_object',
  'editor.scroll_to',
  'editor.set_metainfo',
  'editor.undo',
  'script.compile',
  'script.get_last_result',
  'script.run_current_editor',
  'script.run_editor',
];

/** Tools that change chart state and therefore MUST have an undo/redo case. */
export const MUTATING_TOOLS = [
  'editor.add_object',
  'editor.modify_object',
  'editor.remove_object',
  'editor.create_hold_end',
  'editor.remove_hold_end',
  'editor.create_bullet_pallete',
  'editor.modify_bullet_pallete',
  'editor.remove_bullet_pallete',
  'editor.set_metainfo',
  'editor.begin_action',
  'editor.end_action',
  'editor.undo',
  'editor.redo',
  'script.run_editor',
  'script.run_current_editor',
];

/** Read-only tools: no history entry may be produced by calling them. */
export const READ_ONLY_TOOLS = [
  'editor.check',
  'editor.get_current',
  'editor.get_current_summary',
  'editor.list_opened',
  'editor.query_object',
  'editor.query_bullet_pallete',
  'script.compile',
  'script.get_last_result',
];
