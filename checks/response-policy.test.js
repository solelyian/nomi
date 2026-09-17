import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createServer } from "node:http";
import { once } from "node:events";
import { setTimeout as delay } from "node:timers/promises";
import {
  adaptResponse,
  regenerationInstruction,
  responseLimit,
} from "../server/response-policy.js";
import { createNomiServer } from "../server/app.js";
import { readResponse } from "../preview/inference.js";

const fixtures = JSON.parse(
  readFileSync(
    new URL("./response-policy-cases.json", import.meta.url),
    "utf8",
  ),
);
for (const fixture of fixtures) {
  test(`Nomi policy: ${fixture.name}`, () => {
    const result = adaptResponse(fixture.source, fixture.format);
    assert.equal(result.text, fixture.expected);
    assert.equal(result.accepted, fixture.reasons.length === 0);
    assert.deepEqual(result.changes, fixture.changes);
    assert.deepEqual(result.reasons, fixture.reasons);
    if (result.accepted) {
      const again = adaptResponse(result.text, fixture.format);
      assert.equal(again.text, result.text);
      assert.deepEqual(again.changes, []);
    }
  });
}

test("Nomi policy: reject oversized output without keeping raw text", () => {
  const result = adaptResponse("a".repeat(responseLimit + 1), "steps");
  assert.equal(result.text, "");
  assert.deepEqual(result.reasons, ["length"]);
});

test("all applied rule labels are available in both languages", () => {
  for (const language of ["fr", "en"]) {
    const catalog = JSON.parse(
      readFileSync(
        new URL(`../content/${language}.json`, import.meta.url),
        "utf8",
      ),
    );
    for (const rule of new Set(fixtures.flatMap((item) => item.changes))) {
      assert.ok(catalog.labels[`policy-${rule}`]);
    }
  }
});

async function listen(t, server) {
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  t.after(() => {
    server.closeAllConnections();
    server.close();
  });
  return `http://127.0.0.1:${server.address().port}`;
}

const frame = (content) => JSON.stringify({ message: { content } }) + "\n";
const post = (url, signal) =>
  fetch(`${url}/api/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      prompt: "80 € moins 15 %",
      action: "convert",
      language: "fr",
      format: "steps",
    }),
    signal,
  });

test(
  "HTTP exposes no bytes before completion, then returns only adapted text",
  { timeout: 5000 },
  async (t) => {
    let finish;
    let started;
    const ready = new Promise((resolve) => {
      started = resolve;
    });
    const upstream = await listen(
      t,
      createServer((_, response) => {
        response.write(frame("### **Résultat : 68 €**"));
        finish = () => response.end('\n{"done":true}\n');
        started();
      }),
    );
    const url = await listen(t, createNomiServer({ baseUrl: upstream }));
    const pending = post(url);
    await ready;
    const response = await pending;
    let visible = false;
    let output = "";
    const read = readResponse(response, (token) => {
      visible = true;
      output += token;
    });
    await delay(30);
    assert.equal(visible, false);
    finish();
    const result = await read;
    assert.equal(output, "Résultat : 68 €");
    assert.deepEqual(result.policy, { version: "1.0", changes: ["layout"] });
  },
);

test("HTTP withholds the whole answer if a violation arrives in the last token", async (t) => {
  const upstream = await listen(
    t,
    createServer((_, response) => {
      response.write(frame("Le total est 68 €. Quel est votre dia"));
      response.end(frame("gnostic ?") + '{"done":true}\n');
    }),
  );
  const url = await listen(t, createNomiServer({ baseUrl: upstream }));
  const wire = await (await post(url)).text();
  assert.equal(wire, '{"error":"policy-blocked"}\n');
});

test("HTTP withholds contradictory prices even when the conclusion arrives last", async (t) => {
  const upstream = await listen(
    t,
    createServer((_, response) => {
      response.write(frame("32,00 €\n"));
      response.end(
        frame("Le prix final est donc 68,00 €.") + '{"done":true}\n',
      );
    }),
  );
  const url = await listen(t, createNomiServer({ baseUrl: upstream }));
  assert.equal(await (await post(url)).text(), '{"error":"policy-blocked"}\n');
});

for (const second of [
  "68 €",
  "32 €\nLe prix final est 68 €.",
  "Quel est votre diagnostic ?",
]) {
  test(`regeneration is limited to one attempt and rechecks the new draft: ${second}`, async (t) => {
    let calls = 0;
    const upstream = await listen(
      t,
      createServer(async (request, response) => {
        const chunks = [];
        for await (const chunk of request) chunks.push(chunk);
        const input = JSON.parse(Buffer.concat(chunks).toString("utf8"));
        calls++;
        assert.equal(input.messages.length, 2);
        assert.equal(input.messages[1].content, "80 € moins 15 %");
        assert.equal(
          input.messages[0].content.includes(regenerationInstruction),
          calls === 2,
        );
        response.end(
          frame(calls === 1 ? "32 €\nLe prix final est 68 €." : second) +
            '{"done":true}\n',
        );
      }),
    );
    const url = await listen(t, createNomiServer({ baseUrl: upstream }));
    const wire = JSON.parse(await (await post(url)).text());
    assert.equal(calls, 2);
    if (second === "68 €") {
      assert.equal(wire.message.content, second);
      assert.deepEqual(wire.policy.changes, ["regenerated"]);
      for (const language of ["fr", "en"]) {
        const catalog = JSON.parse(
          readFileSync(
            new URL(`../content/${language}.json`, import.meta.url),
            "utf8",
          ),
        );
        assert.ok(catalog.labels["policy-regenerated"]);
      }
    } else assert.deepEqual(wire, { error: "policy-blocked" });
  });
}

for (const mode of ["cancel", "timeout", "engine-error"]) {
  test(
    `regeneration respects ${mode} without returning either draft`,
    { timeout: 5000 },
    async (t) => {
      let calls = 0;
      let started;
      const ready = new Promise((resolve) => {
        started = resolve;
      });
      const upstream = await listen(
        t,
        createServer((_, response) => {
          calls++;
          if (calls === 1)
            response.end(
              frame("32 €\nLe prix final est 68 €.") + '{"done":true}\n',
            );
          else {
            if (mode === "engine-error") response.writeHead(503).end();
            else response.write(frame("Private second draft"));
            started();
          }
        }),
      );
      const url = await listen(
        t,
        createNomiServer({
          baseUrl: upstream,
          timeout: mode === "timeout" ? 100 : 2000,
        }),
      );
      const controller = new AbortController();
      const pending = post(url, controller.signal).then((response) =>
        response.text(),
      );
      await ready;
      if (mode === "cancel") {
        controller.abort();
        await assert.rejects(pending, { name: "AbortError" });
      } else {
        assert.deepEqual(JSON.parse(await pending), {
          error: mode === "timeout" ? "timeout" : "engine-unavailable",
        });
      }
      assert.equal(calls, 2);
    },
  );
}

for (const [name, body, expected] of [
  [
    "truncated",
    frame("Partial") + '{"done":true,"done_reason":"length"}\n',
    "incomplete-response",
  ],
  ["interrupted", frame("Partial"), "incomplete-response"],
  ["invalid", frame("Partial") + "not-json\n", "invalid-response"],
  ["oversized", frame("x".repeat(responseLimit + 1)), "policy-blocked"],
]) {
  test(`HTTP withholds ${name} output`, async (t) => {
    const upstream = await listen(
      t,
      createServer((_, res) => res.end(body)),
    );
    const url = await listen(t, createNomiServer({ baseUrl: upstream }));
    assert.equal(
      await (await post(url)).text(),
      JSON.stringify({ error: expected }) + "\n",
    );
  });
}

test("timeout after raw tokens does not reveal them", async (t) => {
  const upstream = await listen(
    t,
    createServer((_, res) => res.write(frame("Private fragment"))),
  );
  const url = await listen(
    t,
    createNomiServer({ baseUrl: upstream, timeout: 100 }),
  );
  assert.equal(await (await post(url)).text(), '{"error":"timeout"}\n');
});
