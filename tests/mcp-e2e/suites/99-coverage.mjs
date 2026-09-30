// Suite 99 — the coverage matrix.
//
// This is the suite that answers "does every tool the host stores still have a test?".
// It does not call any tool: it inspects what the other suites reported through
// `ctx.cover(...)` / `ctx.coverUndoRedo(...)` and fails when
//   * a tool exists in lib/coverage.mjs but no suite exercises it, or
//   * a tool is exercised but has no undo/redo case even though it mutates the chart, or
//   * an undo/redo case is recorded for a tool that is not in the expected surface.

import { EXPECTED_TOOLS, MUTATING_TOOLS, READ_ONLY_TOOLS } from '../lib/coverage.mjs';

export default {
  name: '99-coverage',
  description: 'every tool has a suite, every mutating tool has an undo/redo case',

  async run(ctx) {
    const { coverage, undoRedo } = ctx;

    ctx.section('tool coverage');

    const uncovered = EXPECTED_TOOLS.filter((tool) => !coverage.has(tool));
    ctx.check('every stored tool is exercised by at least one suite', uncovered.length === 0,
      uncovered.length ? `no coverage: ${uncovered.join(', ')}` : `${coverage.size} tools covered`);

    const unknown = [...coverage.keys()].filter((tool) => !EXPECTED_TOOLS.includes(tool));
    ctx.check('no suite claims to cover a tool outside the expected surface', unknown.length === 0,
      unknown.length ? `unknown: ${unknown.join(', ')}` : '');

    ctx.section('undo / redo coverage');

    const missingUndoRedo = MUTATING_TOOLS.filter((tool) => !undoRedo.has(tool));
    ctx.check('every mutating tool has an undo/redo case', missingUndoRedo.length === 0,
      missingUndoRedo.length ? `no undo/redo case: ${missingUndoRedo.join(', ')}` : `${undoRedo.size} tools covered`);

    const contradiction = MUTATING_TOOLS.filter((tool) => READ_ONLY_TOOLS.includes(tool));
    ctx.check('no tool is classified as both read-only and mutating', contradiction.length === 0,
      contradiction.join(', '));

    ctx.section('matrix');

    const width = Math.max(...EXPECTED_TOOLS.map((t) => t.length));
    for (const tool of EXPECTED_TOOLS) {
      const suites = [...(coverage.get(tool) ?? [])].join(', ') || '(none)';
      const history = undoRedo.has(tool) ? 'undo/redo yes' : (MUTATING_TOOLS.includes(tool) ? 'undo/redo MISSING' : 'undo/redo n/a');
      console.log(`  ${tool.padEnd(width)}  ${history.padEnd(17)} ${suites}`);
    }
  },
};
