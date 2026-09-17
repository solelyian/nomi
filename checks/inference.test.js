import test from "node:test";
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { once } from "node:events";
import { config, buildPayload, validateRequest } from "../server/inference.js";
import { createNomiServer } from "../server/app.js";
import { readResponse } from "../preview/inference.js";

const request = {
  prompt: "  Une remise de 15 % sur 80 € ?  ",
  action: "convert",
  language: "fr",
  format: "steps",
};
const frames = `${JSON.stringify({ message: { content: "Résultat : 68 €" }, done: false })}\n${JSON.stringify({ done: true })}\n`;

async function listen(t, server) {
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  t.after(() => {
    server.closeAllConnections();
    server.close();
  });
  return `http://127.0.0.1:${server.address().port}`;
}

function post(url, body = request, signal) {
  return fetch(`${url}/api/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal,
  });
}

test("validates requests and rejects forged routing, oversized and empty prompts", () => {
  assert.equal(validateRequest(request).prompt, request.prompt.trim());
  for (const invalid of [
    null,
    [],
    { ...request, prompt: " " },
    { ...request, prompt: "a".repeat(6001) },
    { ...request, action: "__proto__" },
    { ...request, action: "constructor" },
    { ...request, language: "de" },
    { ...request, format: "html" },
    { ...request, prompt: 123 },
  ]) {
    assert.throws(() => validateRequest(invalid), /invalid-request/);
  }
  for (const action of Object.keys(config.actions)) {
    for (const language of ["fr", "en"]) {
      const input = validateRequest({ ...request, action, language });
      const payload = buildPayload(input, config.model);
      assert.equal(payload.messages.length, 2);
      assert.equal(payload.messages[0].role, "system");
      assert.ok(
        payload.messages[0].content.includes(config.languages[language]),
      );
      assert.ok(payload.messages[0].content.includes(config.actions[action]));
      assert.ok(
        payload.messages[0].content.includes(
          "never generate, rewrite or modify code",
        ),
      );
      assert.equal(payload.messages[1].content, request.prompt.trim());
    }
  }
});

test("reads split UTF-8 tokens, multiple frames, and a last frame without a newline", async () => {
  const bytes = new TextEncoder().encode(frames.trimEnd());
  const stream = new ReadableStream({
    start(controller) {
      for (const byte of bytes) controller.enqueue(Uint8Array.of(byte));
      controller.close();
    },
  });
  let output = "";
  assert.deepEqual(
    await readResponse(new Response(stream), (token) => (output += token)),
    { truncated: false },
  );
  assert.equal(output, "Résultat : 68 €");
});

test("reports truncation, interrupted streams and engine errors without inventing a result", async () => {
  const truncated = frames.replace(
    '"done":true',
    '"done":true,"done_reason":"length"',
  );
  assert.deepEqual(await readResponse(new Response(truncated), () => {}), {
    truncated: true,
  });
  await assert.rejects(
    readResponse(new Response('{"message":{"content":"Partial"}}\n'), () => {}),
    /incomplete-response/,
  );
  await assert.rejects(
    readResponse(new Response('{"done":true}\n'), () => {}),
    /incomplete-response/,
  );
  for (const error of [
    "busy",
    "model-missing",
    "engine-unavailable",
    "timeout",
    "invalid-request",
  ]) {
    await assert.rejects(
      readResponse(
        new Response(JSON.stringify({ error }), { status: 503 }),
        () => {},
      ),
      new RegExp(error),
    );
  }
});

test("serves the application and proxies only validated chat requests to local Ollama", async (t) => {
  let sent;
  const upstream = await listen(
    t,
    createServer(async (req, res) => {
      if (req.url === "/api/tags") {
        res.end(JSON.stringify({ models: [{ name: config.model }] }));
        return;
      }
      const chunks = [];
      for await (const chunk of req) chunks.push(chunk);
      sent = JSON.parse(Buffer.concat(chunks).toString("utf8"));
      res.end(frames);
    }),
  );
  const url = await listen(t, createNomiServer({ baseUrl: upstream }));
  assert.equal(
    (await fetch(`${url}/api/status`).then((r) => r.json())).ready,
    true,
  );
  for (const file of [
    "/preview/",
    "/preview/app.js",
    "/preview/inference.js",
    "/content/fr.json",
    "/content/en.json",
  ]) {
    assert.equal((await fetch(url + file)).status, 200, file);
  }
  assert.equal((await fetch(`${url}/server/inference.js`)).status, 404);
  assert.equal(
    (await post(url, { ...request, language: "invalid" })).status,
    400,
  );
  assert.equal(
    (await fetch(`${url}/api/chat`, { method: "POST", body: "x" })).status,
    415,
  );
  assert.equal(
    (
      await fetch(`${url}/api/chat`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Sec-Fetch-Site": "cross-site",
        },
        body: JSON.stringify(request),
      })
    ).status,
    415,
  );
  let output = "";
  await readResponse(await post(url), (token) => (output += token));
  assert.equal(output, "Résultat : 68 €");
  assert.equal(sent.model, config.model);
  assert.equal(sent.messages[1].content, request.prompt.trim());
  assert.equal(sent.think, false);
  assert.throws(
    () => createNomiServer({ baseUrl: "https://example.org" }),
    /loopback/,
  );
});

test("shows missing model and timeout errors and releases the busy state", async (t) => {
  const upstream = await listen(
    t,
    createServer((req, res) => {
      if (req.url === "/api/tags") res.end('{"models":[]}');
      else {
        res.writeHead(404);
        res.end();
      }
    }),
  );
  const url = await listen(t, createNomiServer({ baseUrl: upstream }));
  assert.equal(
    (await fetch(`${url}/api/status`).then((r) => r.json())).reason,
    "model-missing",
  );
  assert.equal((await post(url).then((r) => r.json())).error, "model-missing");
  const hanging = await listen(
    t,
    createServer(() => {}),
  );
  const timed = await listen(
    t,
    createNomiServer({ baseUrl: hanging, timeout: 50 }),
  );
  assert.equal((await post(timed).then((r) => r.json())).error, "timeout");
  assert.equal((await post(timed).then((r) => r.json())).error, "timeout");
});

test(
  "rejects concurrent work and propagates cancellation upstream",
  { timeout: 5000 },
  async (t) => {
    let started;
    let closed;
    const start = new Promise((resolve) => (started = resolve));
    const close = new Promise((resolve) => (closed = resolve));
    const upstream = await listen(
      t,
      createServer((req, res) => {
        res.writeHead(200, { "Content-Type": "application/x-ndjson" });
        res.write('{"message":{"content":"First"}}\n');
        res.on("close", closed);
        started();
      }),
    );
    const url = await listen(t, createNomiServer({ baseUrl: upstream }));
    const controller = new AbortController();
    const first = post(url, request, controller.signal);
    await start;
    const response = await first;
    const conflict = await post(url);
    assert.equal(conflict.status, 429);
    assert.equal((await conflict.json()).error, "busy");
    controller.abort();
    await close;
    await assert.rejects(response.text(), { name: "AbortError" });
  },
);
