/** @param {unknown} value */
export function parseFrame(value) {
  if (typeof value !== "object" || value === null)
    throw new Error("invalid-response");
  if ("error" in value) {
    const known = [
      "timeout",
      "busy",
      "model-missing",
      "engine-unavailable",
      "invalid-request",
      "policy-blocked",
      "invalid-response",
      "incomplete-response",
      "document-limit",
      "document-context-limit",
      "document-unreadable",
    ];
    throw new Error(
      typeof value.error === "string" && known.includes(value.error)
        ? value.error
        : "engine-unavailable",
    );
  }
  let token = "";
  /** @type {import("./types").PolicyInfo | undefined} */
  let policy;
  if ("policy" in value) {
    const data = value.policy;
    if (
      typeof data !== "object" ||
      data === null ||
      !("version" in data) ||
      typeof data.version !== "string" ||
      !("changes" in data) ||
      !Array.isArray(data.changes) ||
      !data.changes.every((item) => typeof item === "string")
    )
      throw new Error("invalid-response");
    policy = { version: data.version, changes: data.changes };
  }
  if (
    "message" in value &&
    typeof value.message === "object" &&
    value.message !== null &&
    "content" in value.message &&
    typeof value.message.content === "string"
  )
    token = value.message.content;
  return {
    token,
    done: "done" in value && value.done === true,
    truncated: "done_reason" in value && value.done_reason === "length",
    policy,
  };
}

/**
 * @param {Response} response
 * @param {(token: string) => void} onToken
 */
export async function readResponse(response, onToken) {
  if (!response.ok) {
    const data = await response
      .json()
      .catch(() => ({ error: "engine-unavailable" }));
    parseFrame(data);
    throw new Error("engine-unavailable");
  }
  if (!response.body) throw new Error("invalid-response");
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  let complete = false;
  let truncated = false;
  let received = false;
  /** @type {import("./types").PolicyInfo | undefined} */
  let policy;
  /** @param {string} line */
  function consume(line) {
    if (!line.trim()) return;
    let parsed;
    try {
      parsed = JSON.parse(line);
    } catch {
      throw new Error("invalid-response");
    }
    const frame = parseFrame(parsed);
    if (frame.token) {
      onToken(frame.token);
      received = true;
    }
    complete ||= frame.done;
    truncated ||= frame.truncated;
    policy = frame.policy ?? policy;
  }
  try {
    while (true) {
      const { value, done } = await reader.read();
      buffer += decoder.decode(value, { stream: !done });
      const lines = buffer.split("\n");
      buffer = lines.pop() ?? "";
      for (const line of lines) consume(line);
      if (done) {
        consume(buffer);
        break;
      }
    }
  } finally {
    reader.releaseLock();
  }
  if (!complete || !received) throw new Error("incomplete-response");
  return { truncated, ...(policy ? { policy } : {}) };
}
