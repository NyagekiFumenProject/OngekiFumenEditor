// Thin, fully-typed wrappers for every tool the MCP host exposes.
//
// Suites must go through these instead of hand-rolling `client.call(...)` strings:
// the tool names and parameter names live here once, so a rename in the C# tool
// surface breaks the tests loudly in one place.

export class McpApi {
  #client;
  #defaultTimeoutMs;

  constructor(client, { timeoutMs = 180000 } = {}) {
    this.#client = client;
    this.#defaultTimeoutMs = timeoutMs;
  }

  get client() { return this.#client; }

  raw(toolName, args, options) {
    return this.#client.call(toolName, args, options);
  }

  // ---------------- discovery (read-only) ----------------

  getCurrent() { return this.#client.call('editor.get_current'); }

  getCurrentSummary() { return this.#client.call('editor.get_current_summary'); }

  listOpened() { return this.#client.call('editor.list_opened'); }

  queryObject({ editorId, objectType, minTotalGrid, maxTotalGrid, limit = 4000, cursor } = {}) {
    return this.#client.call('editor.query_object', { editorId, objectType, minTotalGrid, maxTotalGrid, limit, cursor });
  }

  // ---------------- document lifecycle ----------------

  createProject({ audioPath, fumenPath, baseBpm, audioDurationMs, requireConfirmation } = {}) {
    return this.#client.call('editor.create_proj', { audioPath, fumenPath, baseBpm, audioDurationMs, requireConfirmation });
  }

  openFast({ fumenPath, audioPath, requireConfirmation } = {}) {
    return this.#client.call('editor.open_fast', { fumenPath, audioPath, requireConfirmation });
  }

  openProject({ projectPath, requireConfirmation } = {}) {
    return this.#client.call('editor.open_proj', { projectPath, requireConfirmation });
  }

  close({ editorId, force, requireConfirmation } = {}) {
    return this.#client.call('editor.close', { editorId, force, requireConfirmation });
  }

  // ---------------- object mutation ----------------

  addObject({ editorId, expectedEditorId, objectType, ...rest } = {}) {
    return this.#client.call('editor.add_object', { editorId, expectedEditorId, objectType, ...rest });
  }

  modifyObject({ editorId, objectId, propertyName, newValue, snapXToLane } = {}) {
    return this.#client.call('editor.modify_object', { editorId, objectId, propertyName, newValue, snapXToLane });
  }

  removeObject({ editorId, objectId } = {}) {
    return this.#client.call('editor.remove_object', { editorId, objectId });
  }

  createHoldEnd({ holdObjectId, tGridUnit, tGridGrid, xGridUnit, xGridGrid, editorId } = {}) {
    return this.#client.call('editor.create_hold_end', { holdObjectId, tGridUnit, tGridGrid, xGridUnit, xGridGrid, editorId });
  }

  removeHoldEnd({ holdObjectId, editorId } = {}) {
    return this.#client.call('editor.remove_hold_end', { holdObjectId, editorId });
  }

  // ---------------- action scope + history ----------------

  beginAction({ editorId, expectedEditorId } = {}) {
    return this.#client.call('editor.begin_action', { editorId, expectedEditorId });
  }

  endAction({ editorId, name, discard } = {}) {
    return this.#client.call('editor.end_action', { editorId, name, discard });
  }

  undo({ editorId, expectedEditorId, requireConfirmation } = {}) {
    return this.#client.call('editor.undo', { editorId, expectedEditorId, requireConfirmation });
  }

  redo({ editorId, expectedEditorId, requireConfirmation } = {}) {
    return this.#client.call('editor.redo', { editorId, expectedEditorId, requireConfirmation });
  }

  // ---------------- bullet pallete ----------------

  createBulletPallete({ editorId, strId, editorName, shooter, target, size, type, speed, placeOffset, randomOffsetRange } = {}) {
    return this.#client.call('editor.create_bullet_pallete', { editorId, strId, editorName, shooter, target, size, type, speed, placeOffset, randomOffsetRange });
  }

  queryBulletPallete({ editorId, strId, editorNameContains, limit, cursor } = {}) {
    return this.#client.call('editor.query_bullet_pallete', { editorId, strId, editorNameContains, limit, cursor });
  }

  modifyBulletPallete({ editorId, strId, propertyName, newValue } = {}) {
    return this.#client.call('editor.modify_bullet_pallete', { editorId, strId, propertyName, newValue });
  }

  removeBulletPallete({ editorId, strId } = {}) {
    return this.#client.call('editor.remove_bullet_pallete', { editorId, strId });
  }

  // ---------------- chart meta info + checks ----------------

  setMetainfo({ editorId, metainfoName, newValue } = {}) {
    return this.#client.call('editor.set_metainfo', { editorId, metainfoName, newValue });
  }

  check({ editorId, minSeverity, limit } = {}) {
    return this.#client.call('editor.check', { editorId, minSeverity, limit });
  }

  // ---------------- viewport ----------------

  scrollTo({ editorId, expectedEditorId, tGridUnit, tGridGrid, requireConfirmation } = {}) {
    return this.#client.call('editor.scroll_to', { editorId, expectedEditorId, tGridUnit, tGridGrid, requireConfirmation });
  }

  // ---------------- runtime automation scripts ----------------

  compileScript({ scriptText, enableSecurityCheck } = {}) {
    return this.#client.call('script.compile', { scriptText, enableSecurityCheck });
  }

  /**
   * The script tools answer with `ScriptRunResult`, whose payload carries the script's
   * return value as a *JSON string* in `returnValueJson`. Decode it into `result` so
   * suites can assert on the returned object directly. A run without a return value, or a
   * run that failed before executing, has no `returnValueJson` at all — `result` is then
   * `null` rather than `undefined`, so "no value" is an explicit, assertable state.
   */
  #withScriptResult(response) {
    const payload = response?.payload;
    if (payload !== null && typeof payload === 'object') {
      const raw = payload.returnValueJson;
      if (raw === null || raw === undefined || raw === '') {
        payload.result = null;
      } else {
        try { payload.result = JSON.parse(raw); } catch { payload.result = raw; }
      }
    }
    return response;
  }

  async runScript({ editorId, scriptText, expectedEditorId, requireConfirmation, wrapUndoTransaction, transactionName } = {}) {
    return this.#withScriptResult(await this.#client.call('script.run_editor', {
      editorId, scriptText, expectedEditorId, requireConfirmation, wrapUndoTransaction, transactionName,
    }));
  }

  async runScriptOnCurrent({ scriptText, expectedEditorId, requireConfirmation, wrapUndoTransaction, transactionName } = {}) {
    return this.#withScriptResult(await this.#client.call('script.run_current_editor', {
      scriptText, expectedEditorId, requireConfirmation, wrapUndoTransaction, transactionName,
    }));
  }

  async getLastScriptResult() {
    return this.#withScriptResult(await this.#client.call('script.get_last_result'));
  }

  // ---------------- convenience ----------------

  /** Unwrap `{success, ...}` payloads and throw on failure (for fixture setup). */
  async must(response, what) {
    const payload = response?.payload ?? response;
    if (payload?.success !== true) {
      throw new Error(`fixture step failed: ${what} -> ${payload?.errorCode} ${payload?.errorMessage ?? ''}`);
    }
    return payload;
  }

  /** Query one family and return its `objects` array. */
  async family(editorId, objectType, extra = {}) {
    const payload = await this.must(await this.queryObject({ editorId, objectType, ...extra }), `query ${objectType}`);
    return payload.objects ?? [];
  }
}
