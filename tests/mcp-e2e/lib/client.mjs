// Minimal MCP client over Streamable HTTP (JSON + SSE), used by the e2e suites.
//
// The editor's MCP host speaks JSON-RPC 2.0 at POST /mcp. Responses come back as
// an SSE stream (`data: {...}` lines), so every request must be read incrementally
// until the message carrying the matching id shows up.

import { setTimeout as delay } from 'node:timers/promises';

export const DEFAULT_BASE_URL = 'http://127.0.0.1:39281/mcp';
const PROTOCOL_VERSION = '2025-06-18';

const JSON_HEADERS = {
  'content-type': 'application/json',
  accept: 'application/json, text/event-stream',
};

/** Read SSE frames from a fetch Response until the JSON-RPC message with `wantId` arrives. */
async function readMessage(response, wantId, deadlineMs) {
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  const deadline = Date.now() + deadlineMs;

  while (true) {
    const remaining = deadline - Date.now();
    if (remaining <= 0) {
      try { await reader.cancel(); } catch { /* ignore */ }
      throw new Error(`timed out waiting for JSON-RPC id=${wantId}`);
    }

    const { value, done } = await Promise.race([
      reader.read(),
      delay(remaining, null, { ref: false }).then(() => ({ __timeout: true })),
    ]);
    if (value === undefined && done) break;
    if (value && value.__timeout) continue;

    buffer += decoder.decode(value, { stream: true });
    for (const line of buffer.split('\n')) {
      const trimmed = line.trim();
      if (!trimmed.startsWith('data:')) continue;
      let message;
      try { message = JSON.parse(trimmed.slice(5).trim()); } catch { continue; }
      if (message.id !== wantId) continue;
      try { await reader.cancel(); } catch { /* ignore */ }
      return message;
    }
  }
  throw new Error(`SSE stream ended before JSON-RPC id=${wantId} arrived`);
}

export class McpClient {
  #baseUrl;
  #sessionId = null;
  #seq = 1;
  #timeoutMs;

  constructor({ baseUrl = DEFAULT_BASE_URL, timeoutMs = 180000 } = {}) {
    this.#baseUrl = baseUrl;
    this.#timeoutMs = timeoutMs;
  }

  get baseUrl() { return this.#baseUrl; }

  /** Wait until the host answers `initialize`, then complete the handshake. */
  async connect({ attempts = 60, intervalMs = 1000 } = {}) {
    let lastError = null;
    for (let i = 0; i < attempts; i++) {
      try {
        const init = await this.#post({
          jsonrpc: '2.0',
          id: 1,
          method: 'initialize',
          params: {
            protocolVersion: PROTOCOL_VERSION,
            capabilities: {},
            clientInfo: { name: 'ongeki-mcp-e2e', version: '1' },
          },
        });
        if (!init.ok) throw new Error(`initialize -> HTTP ${init.status}`);
        const sessionId = init.headers.get('mcp-session-id');
        await init.text();
        if (!sessionId) throw new Error('initialize response carried no mcp-session-id');
        this.#sessionId = sessionId;
        await this.#post({ jsonrpc: '2.0', method: 'notifications/initialized' });
        return this;
      } catch (error) {
        lastError = error;
        await delay(intervalMs);
      }
    }
    throw new Error(`MCP host at ${this.#baseUrl} never became ready: ${lastError?.message}`);
  }

  #post(body) {
    const headers = { ...JSON_HEADERS };
    if (this.#sessionId) headers['mcp-session-id'] = this.#sessionId;
    return fetch(this.#baseUrl, { method: 'POST', headers, body: JSON.stringify(body) });
  }

  async request(method, params = {}, { timeoutMs = this.#timeoutMs } = {}) {
    if (!this.#sessionId) throw new Error('connect() must be awaited before request()');
    const id = this.#seq++;
    const response = await this.#post({ jsonrpc: '2.0', id, method, params });
    if (!response.ok) throw new Error(`${method} -> HTTP ${response.status}`);
    const message = await readMessage(response, id, timeoutMs);
    if (message.error) {
      throw new Error(`${method} failed: ${JSON.stringify(message.error)}`);
    }
    return message.result;
  }

  listTools() {
    return this.request('tools/list', {});
  }

  listResources() {
    return this.request('resources/list', {});
  }

  readResource(uri) {
    return this.request('resources/read', { uri });
  }

  /**
   * Invoke one tool and return the decoded payload.
   *
   * The editor answers with a JSON document in `content[0].text`. Tools report
   * business-level failures as `{success:false, errorCode, errorMessage}` rather
   * than as JSON-RPC errors, so the payload is returned as-is and callers assert
   * on `success` / `errorCode`. `isError` is exposed when the host flags a
   * protocol-level failure.
   */
  async call(toolName, args = {}, { timeoutMs = this.#timeoutMs } = {}) {
    const result = await this.request(
      'tools/call',
      { name: toolName, arguments: { requestedBy: 'mcp-e2e', ...args } },
      { timeoutMs },
    );
    const text = result?.content?.find?.((c) => c.type === 'text')?.text ?? result?.content?.[0]?.text;
    let payload = null;
    if (typeof text === 'string' && text.length > 0) {
      try { payload = JSON.parse(text); } catch { payload = { raw: text }; }
    } else if (result?.structuredContent) {
      payload = result.structuredContent;
    }
    return { payload, isError: result?.isError === true, raw: result };
  }
}
