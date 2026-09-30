#!/usr/bin/env node
// End-to-end test runner for the editor's MCP tool surface.
//
//   node tests/mcp-e2e/run.mjs                 # every suite
//   node tests/mcp-e2e/run.mjs 30 40           # only suites whose file name starts with 30 / 40
//   node tests/mcp-e2e/run.mjs --list          # list suites and exit
//   node tests/mcp-e2e/run.mjs --url http://127.0.0.1:39281/mcp
//
// The suites drive a *running* editor instance over HTTP, so start one first:
//
//   OngekiFumenEditor/bin/Debug/net10.0-windows/OngekiFumenEditor.exe
//
// Gate that must hold for a meaningful run:
//   * the MCP host is listening (Debug builds auto-start it only when the per-exe setting
//     `EnableMcpServerInGUIMode` is true)
//   * a chart sample and a generated silent WAV exist under tests/mcp-e2e/.tmp/ (auto-created)
//
// Exit code is 0 only when every check passed.

import { McpClient, DEFAULT_BASE_URL } from './lib/client.mjs';
import { McpApi } from './lib/api.mjs';
import { CheckLog, SuiteContext } from './lib/harness.mjs';
import { prepareFixtures } from './lib/env.mjs';

import suite00 from './suites/00-tool-surface.mjs';
import suite10 from './suites/10-discovery.mjs';
import suite20 from './suites/20-object-crud.mjs';
import suite30 from './suites/30-connectable.mjs';
import suite40 from './suites/40-hold-end.mjs';
import suite50 from './suites/50-action-scope.mjs';
import suite60 from './suites/60-bullet-pallete.mjs';
import suite70 from './suites/70-metainfo-check.mjs';
import suite80 from './suites/80-scroll-script.mjs';
import suite90 from './suites/90-document-lifecycle.mjs';
import suite99 from './suites/99-coverage.mjs';

const ALL_SUITES = [suite00, suite10, suite20, suite30, suite40, suite50, suite60, suite70, suite80, suite90, suite99];

function parseArgs(argv) {
  const options = { url: DEFAULT_BASE_URL, filters: [], list: false };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--list') options.list = true;
    else if (arg === '--url') options.url = argv[++i];
    else if (!arg.startsWith('--')) options.filters.push(arg);
  }
  return options;
}

const options = parseArgs(process.argv.slice(2));

if (options.list) {
  for (const suite of ALL_SUITES) console.log(`${suite.name}\t${suite.description ?? ''}`);
  process.exit(0);
}

const selected = options.filters.length
  ? ALL_SUITES.filter((s) => options.filters.some((f) => s.name.startsWith(f)))
  : ALL_SUITES;

if (selected.length === 0) {
  console.error(`no suite matched: ${options.filters.join(', ')}`);
  process.exit(2);
}

const coverage = new Map();
const undoRedo = new Map();
const totals = { passed: 0, failed: 0 };

console.log(`MCP e2e · ${options.url}`);
console.log(`suites: ${selected.map((s) => s.name).join(', ')}\n`);

const env = await prepareFixtures();
console.log(`fixtures: ${env.seededChart}`);
console.log(`          ${env.audioPath}\n`);

const client = new McpClient({ baseUrl: options.url });
try {
  await client.connect({ attempts: 20, intervalMs: 1000 });
} catch (error) {
  console.error(`\nCould not reach the MCP host: ${error.message}`);
  console.error('Start the editor (Debug build) and make sure the MCP server is enabled, then re-run.');
  process.exit(3);
}

const api = new McpApi(client);

// ---- deterministic starting point: no editors, then exactly one seeded editor ----
let guard = 0;
while (guard++ < 10) {
  const opened = (await api.listOpened()).payload;
  if (!Array.isArray(opened) || opened.length === 0) break;
  for (const editor of opened) {
    const response = await api.close({ editorId: editor.editorId, force: true });
    if (response.payload?.success !== true) {
      console.error(`could not close leftover editor ${editor.editorId}: ${JSON.stringify(response.payload)}`);
      process.exit(3);
    }
  }
}

const openResponse = await api.openFast({ fumenPath: env.seededChart, audioPath: env.audioPath });
const openPayload = openResponse.payload ?? {};
if (openPayload.success !== true || !openPayload.editorId) {
  console.error(`\nCould not open the seed chart: ${JSON.stringify(openPayload)}`);
  process.exit(3);
}
const editorId = openPayload.editorId;
console.log(`editor : ${editorId}  (${env.seededChart})\n`);

for (const suite of selected) {
  const log = new CheckLog(suite.name);
  console.log(`\n=== ${suite.name} — ${suite.description ?? ''} ===`);
  const ctx = new SuiteContext({ name: suite.name, client, api, env, log, coverage, undoRedo });
  ctx.editorId = editorId;
  try {
    await suite.run(ctx);
  } catch (error) {
    log.record(`suite crashed: ${error.message}`, false, error.stack?.split('\n')?.[1]?.trim());
  }
  totals.passed += log.passed;
  totals.failed += log.failed.length;
  console.log(`  -> ${suite.name}: ${log.passed}/${log.total} passed`);
}

console.log(`\n${'='.repeat(64)}`);
console.log(`TOTAL: ${totals.passed}/${totals.passed + totals.failed} checks passed`);
if (totals.failed > 0) {
  console.log(`FAILED: ${totals.failed} check(s)`);
}
console.log(`${'='.repeat(64)}`);

process.exit(totals.failed === 0 ? 0 : 1);
