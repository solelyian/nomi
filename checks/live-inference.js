import assert from "node:assert/strict";
import { once } from "node:events";
import { createNomiServer } from "../server/app.js";
import { readResponse } from "../preview/inference.js";

const examples = [
  {
    action: "convert",
    language: "fr",
    prompt:
      "Un article coûte 80 €. Une remise de 15 % est appliquée. Quel est le prix final ?",
    expected: /68/,
  },
  {
    action: "plan",
    language: "en",
    prompt:
      "I have 90 minutes, three tasks of 20 minutes each, and two breaks of 5 minutes. How much time remains?",
    expected: /20/,
  },
  {
    action: "verify",
    language: "fr",
    prompt:
      "Je vérifie le raisonnement d'un calcul dans mon code. Pour une remise de 15 % sur 80 €, j'ai fait 80 - 15 et obtenu 65 €. Explique l'erreur sans écrire ni modifier de code.",
    expected: /68/,
  },
  {
    action: "understand",
    language: "en",
    prompt:
      "My dashboard went from 40 to 50 orders. Explain the absolute increase and the percentage increase.",
    expected: /25/,
  },
  {
    action: "communicate",
    language: "fr",
    prompt:
      "Écris un message client court : prix initial 80 €, remise 15 %, prix final 68 €. N'ajoute aucune nouvelle offre.",
    expected: /68/,
  },
  {
    action: "learn",
    language: "en",
    prompt:
      "Help me learn the mean using 10, 20 and 30. Explain it briefly and ask one practice question.",
    expected: /20/,
  },
];

const server = createNomiServer();
server.listen(0, "127.0.0.1");
await once(server, "listening");
const base = `http://127.0.0.1:${server.address().port}`;
try {
  const status = await fetch(`${base}/api/status`).then((r) => r.json());
  assert.ok(status.ready, `Model not ready: ${status.reason}`);
  console.log(`Model: ${status.model}`);
  for (const { expected, ...input } of examples) {
    const start = Date.now();
    let output = "";
    const response = await fetch(`${base}/api/chat`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ...input, format: "summary" }),
      signal: AbortSignal.timeout(185000),
    });
    const { truncated } = await readResponse(
      response,
      (token) => (output += token),
    );
    assert.ok(!truncated, `${input.action}: truncated`);
    assert.match(output, expected);
    assert.ok(
      !output.includes("```"),
      `${input.action}: unexpected code block`,
    );
    console.log(
      `\n${input.action} (${input.language}, ${(Date.now() - start) / 1000}s)\n${output}`,
    );
  }
  console.log(
    "\nSix live inference smoke checks passed. Arithmetic patterns are sanity checks, not a general accuracy evaluation.",
  );
} finally {
  server.closeAllConnections();
  server.close();
}
