// Reusable undo/redo verification.
//
// Every mutating tool must be able to (a) apply, (b) undo back to the exact previous
// state, and (c) redo forward again — repeatably, not just once. `verifyUndoRedo`
// drives that full cycle and asserts the history counters stay consistent, so a suite
// only has to describe the change and how to observe it.

/**
 * Derive the fully-qualified tool name from a human label such as
 * `add_object(tap)` or `set_metainfo(xResolution)`.
 */
function deriveToolName(label) {
  const head = String(label).split('(')[0].trim();
  if (head.includes('.')) return head;
  if (head.startsWith('script')) return `script.${head}`;
  return `editor.${head}`;
}

/**
 * @param {import('./harness.mjs').SuiteContext} ctx
 * @param {object} spec
 * @param {string} spec.label            human-readable change name, used in check names
 * @param {() => Promise<any>} spec.mutate      performs the change (one undoable action)
 * @param {() => Promise<boolean>} spec.applied  true when the change is observable
 * @param {() => Promise<boolean>} spec.reverted true when the change is gone
 * @param {string|RegExp} [spec.undoName]  expected `undone` name reported by the host
 * @param {string|RegExp} [spec.redoName]  expected `redone` name reported by the host
 * @param {boolean} [spec.leaveApplied]    when true the final state is "applied"; default "reverted"
 */
export async function verifyUndoRedo(ctx, spec) {
  const { label, mutate, applied, reverted, undoName, redoName, leaveApplied = false, tool } = spec;
  const api = ctx.api;
  const editorId = ctx.editorId;

  if (tool) ctx.coverUndoRedo(tool);
  else ctx.coverUndoRedo(deriveToolName(label));

  const matches = (pattern, value) => {
    if (pattern === undefined) return true;
    return pattern instanceof RegExp ? pattern.test(String(value)) : String(value).includes(pattern);
  };

  // ---- precondition: the observation helpers must actually distinguish the two states
  ctx.check(`[undo/redo] ${label}: precondition is "reverted"`, (await reverted()) === true);

  const mutation = await mutate();
  ctx.ok(`[undo/redo] ${label}: applied by one action`, mutation);
  ctx.check(`[undo/redo] ${label}: change is observable after apply`, (await applied()) === true);

  // ---- undo #1
  const u1 = await api.undo({ editorId });
  const u1p = u1.payload ?? {};
  ctx.ok(`[undo/redo] ${label}: undo succeeds`, u1);
  ctx.check(`[undo/redo] ${label}: undo reports the expected history entry`, matches(undoName, u1p.undone), `undone=${u1p.undone}`);
  ctx.check(`[undo/redo] ${label}: undo leaves exactly one redoable entry`, u1p.redoCount >= 1, `undoCount=${u1p.undoCount} redoCount=${u1p.redoCount}`);
  ctx.check(`[undo/redo] ${label}: change is gone after undo`, (await reverted()) === true);

  // ---- redo
  const r1 = await api.redo({ editorId });
  const r1p = r1.payload ?? {};
  ctx.ok(`[undo/redo] ${label}: redo succeeds`, r1);
  ctx.check(`[undo/redo] ${label}: redo reports the expected history entry`, matches(redoName, r1p.redone), `redone=${r1p.redone}`);
  ctx.check(`[undo/redo] ${label}: redo consumes the redo stack`, r1p.redoCount === 0, `redoCount=${r1p.redoCount}`);
  ctx.check(`[undo/redo] ${label}: redo restores the undo depth`, r1p.undoCount === u1p.undoCount + 1, `undoCount=${r1p.undoCount} expected=${u1p.undoCount + 1}`);
  ctx.check(`[undo/redo] ${label}: change is observable after redo`, (await applied()) === true);

  // ---- undo #2: proves the cycle is repeatable rather than one-shot
  const u2 = await api.undo({ editorId });
  const u2p = u2.payload ?? {};
  ctx.ok(`[undo/redo] ${label}: second undo succeeds`, u2);
  ctx.check(`[undo/redo] ${label}: second undo returns to the same depth`, u2p.undoCount === u1p.undoCount && u2p.redoCount === u1p.redoCount,
    `undoCount=${u2p.undoCount} redoCount=${u2p.redoCount} expected=${u1p.undoCount}/${u1p.redoCount}`);
  ctx.check(`[undo/redo] ${label}: change is gone after second undo`, (await reverted()) === true);

  if (leaveApplied) {
    const r2 = await api.redo({ editorId });
    ctx.ok(`[undo/redo] ${label}: final redo succeeds`, r2);
    ctx.check(`[undo/redo] ${label}: final state is applied`, (await applied()) === true);
  }
}

/**
 * Verify that a batch of mutations queued inside `begin_action`/`end_action`
 * collapses into exactly ONE history entry.
 *
 * @param {object} spec
 * @param {string} spec.label
 * @param {() => Promise<any[]>} spec.mutate  issues the queued mutations, returns their responses
 * @param {() => Promise<boolean>} spec.applied
 * @param {() => Promise<boolean>} spec.reverted
 */
export async function verifyActionScopeUndoRedo(ctx, spec) {
  const { label, mutate, applied, reverted } = spec;
  const api = ctx.api;
  const editorId = ctx.editorId;

  ctx.coverUndoRedo('editor.begin_action');
  ctx.coverUndoRedo('editor.end_action');

  ctx.check(`[scope] ${label}: precondition is "reverted"`, (await reverted()) === true);

  const begin = await api.beginAction({ editorId });
  const beginPayload = begin.payload ?? {};
  ctx.ok(`[scope] ${label}: begin_action opens a scope`, begin);
  ctx.check(`[scope] ${label}: scope state is "open"`, beginPayload.scope === 'open', `scope=${beginPayload.scope}`);

  const queued = await mutate();
  for (const [index, response] of queued.entries()) {
    const payload = response.payload ?? {};
    ctx.check(`[scope] ${label}: queued mutation #${index + 1} is accepted but not applied yet`,
      payload.success === true && payload.applied === false && payload.queued === true,
      `applied=${payload.applied} queued=${payload.queued} code=${payload.errorCode}`);
  }
  ctx.check(`[scope] ${label}: nothing is applied while the scope is open`, (await reverted()) === true);

  const end = await api.endAction({ editorId, name: `e2e ${label}` });
  const endPayload = end.payload ?? {};
  ctx.ok(`[scope] ${label}: end_action applies the batch`, end);
  ctx.check(`[scope] ${label}: batch applied as a single transaction`,
    endPayload.applied === true && endPayload.failedCount === 0 && endPayload.outcomeCount === queued.length,
    `applied=${endPayload.applied} outcomeCount=${endPayload.outcomeCount} failedCount=${endPayload.failedCount}`);
  ctx.check(`[scope] ${label}: change is observable after end_action`, (await applied()) === true);

  const u1 = await api.undo({ editorId });
  ctx.ok(`[scope] ${label}: one undo reverts the whole batch`, u1);
  ctx.check(`[scope] ${label}: undo reports the transaction name`, /e2e/.test(String((u1.payload ?? {}).undone)), `undone=${(u1.payload ?? {}).undone}`);
  ctx.check(`[scope] ${label}: change is gone after one undo`, (await reverted()) === true);

  const r1 = await api.redo({ editorId });
  ctx.ok(`[scope] ${label}: redo reapplies the whole batch`, r1);
  ctx.check(`[scope] ${label}: change is observable after redo`, (await applied()) === true);

  await api.undo({ editorId });
  ctx.check(`[scope] ${label}: final state is reverted`, (await reverted()) === true);
}
