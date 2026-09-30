// Assertion + suite plumbing shared by every e2e suite.

const PASS = 'PASS';
const FAIL = 'FAIL';

/**
 * Poll `predicate` until it returns a truthy value, or fail after `timeoutMs`.
 *
 * Some editor operations are asynchronous by design (a project tab is created before its
 * chart and audio finish loading), so a suite has to wait for the observable state instead
 * of assuming it is ready the moment the tool returns.
 *
 * @returns {Promise<{ok: boolean, waitedMs: number, last: any}>}
 */
export async function waitFor(predicate, { timeoutMs = 20000, intervalMs = 250 } = {}) {
  const startedAt = Date.now();
  let last;
  while (Date.now() - startedAt < timeoutMs) {
    last = await predicate();
    if (last) return { ok: true, waitedMs: Date.now() - startedAt, last };
    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }
  return { ok: false, waitedMs: Date.now() - startedAt, last };
}

export class CheckLog {
  constructor(suiteName) {
    this.suiteName = suiteName;
    this.entries = [];
  }

  record(name, passed, detail) {
    this.entries.push({ name, passed, detail });
    const suffix = detail === undefined || detail === null || detail === '' ? '' : `  :: ${detail}`;
    console.log(`  ${passed ? PASS : FAIL}  ${name}${suffix}`);
    return passed;
  }

  get total() { return this.entries.length; }
  get failed() { return this.entries.filter((e) => !e.passed); }
  get passed() { return this.total - this.failed.length; }
}

/**
 * Thin wrapper handed to each suite: assertions plus the tool helpers that almost
 * every suite needs. Suites are async functions receiving this object.
 */
export class SuiteContext {
  constructor({ name, client, api, env, log, coverage, undoRedo }) {
    this.name = name;
    this.client = client;
    this.api = api;
    this.env = env;
    this.log = log;
    this.coverage = coverage;
    this.undoRedo = undoRedo;
  }

  /** Record that this suite exercises a tool (feeds the coverage matrix suite). */
  cover(...toolNames) {
    for (const toolName of toolNames) {
      if (!this.coverage.has(toolName)) this.coverage.set(toolName, new Set());
      this.coverage.get(toolName).add(this.name);
    }
  }

  /** Record that this suite has an undo/redo case for a tool. */
  coverUndoRedo(toolName) {
    if (!this.undoRedo.has(toolName)) this.undoRedo.set(toolName, new Set());
    this.undoRedo.get(toolName).add(this.name);
  }

  /** Plain boolean assertion. */
  check(name, condition, detail) {
    return this.log.record(name, !!condition, detail);
  }

  /** Assert an exact value with both sides printed on failure. */
  equal(name, actual, expected) {
    const passed = Object.is(actual, expected);
    return this.log.record(name, passed, passed ? `= ${JSON.stringify(actual)}` : `expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
  }

  /** Assert a tool call succeeded (`success === true`). */
  ok(name, response, detail) {
    const payload = response?.payload ?? response;
    const passed = payload?.success === true;
    const info = detail ?? (passed ? '' : `code=${payload?.errorCode} msg=${String(payload?.errorMessage ?? payload?.raw ?? '').slice(0, 160)}`);
    return this.log.record(name, passed, info);
  }

  /** Assert a tool call failed with a specific errorCode. */
  fails(name, response, errorCode) {
    const payload = response?.payload ?? response;
    const passed = payload?.success === false && (!errorCode || payload.errorCode === errorCode);
    const want = errorCode ? `expected ${errorCode}, ` : '';
    return this.log.record(name, passed, `${want}code=${payload?.errorCode} msg=${String(payload?.errorMessage ?? '').slice(0, 140)}`);
  }

  /** Assert every listed key is present on an object. */
  hasKeys(name, object, keys) {
    const missing = keys.filter((k) => object === null || object === undefined || !(k in object));
    return this.log.record(name, missing.length === 0, missing.length ? `missing: ${missing.join(', ')}` : `keys: ${keys.length}`);
  }

  /** Assert a value is an array with the given length (or at least `minLength`). */
  isArray(name, value, { length, minLength } = {}) {
    const isArray = Array.isArray(value);
    const okLength = length !== undefined ? value?.length === length : (minLength !== undefined ? value?.length >= minLength : true);
    const passed = isArray && okLength;
    const expectation = length !== undefined ? `length ${length}` : (minLength !== undefined ? `at least ${minLength}` : 'an array');
    return this.log.record(name, passed, passed ? `length=${value.length}` : `expected ${expectation}, got ${Array.isArray(value) ? `length=${value.length}` : typeof value}`);
  }

  /** Group separator in the console output. */
  section(title) {
    console.log(`\n--- ${title} ${'-'.repeat(Math.max(0, 60 - title.length))}`);
  }
}
