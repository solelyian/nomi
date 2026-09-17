import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname } from "node:path";
import { config, validateRequest, buildPayload } from "./inference.js";
import { readResponse } from "../preview/inference.js";
import { documentKind, documentLimits } from "../preview/documents.js";
import { readDocument } from "./document-upload.js";
import {
  adaptResponse,
  regenerationInstruction,
  responseLimit,
} from "./response-policy.js";

/** @param {import("node:http").ServerResponse} response @param {number} code @param {object} body */
function json(response, code, body) {
  response.writeHead(code, {
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store",
  });
  response.end(JSON.stringify(body));
}

/** @param {{ baseUrl?: string, model?: string, timeout?: number }} [options] */
export function createNomiServer(options = {}) {
  const baseUrl =
    options.baseUrl ?? process.env.NOMI_OLLAMA_URL ?? "http://127.0.0.1:11434";
  const model = options.model ?? process.env.NOMI_MODEL ?? config.model;
  const timeout = options.timeout ?? 180_000;
  if (
    new URL(baseUrl).protocol !== "http:" ||
    !["127.0.0.1", "localhost", "[::1]"].includes(new URL(baseUrl).hostname)
  )
    throw new Error("Ollama must use a loopback address.");
  let busy = false;
  let readingDocument = false;

  return createServer(async (request, response) => {
    response.setHeader("X-Content-Type-Options", "nosniff");
    response.setHeader("Referrer-Policy", "same-origin");
    const pathname = new URL(request.url ?? "/", "http://localhost").pathname;
    if (request.method === "POST" && pathname === "/api/documents") {
      if (
        request.headers["content-type"] !== "application/octet-stream" ||
        request.headers["sec-fetch-site"] === "cross-site"
      ) {
        json(response, 415, { error: "document-unreadable" });
        return;
      }
      if (readingDocument) {
        json(response, 429, { error: "document-busy" });
        return;
      }
      readingDocument = true;
      const controller = new AbortController();
      const timer = setTimeout(() => {
        controller.abort();
        request.destroy();
      }, 45_000);
      response.once("close", () => controller.abort());
      try {
        const name = decodeURIComponent(
          String(request.headers["x-file-name"] ?? ""),
        );
        if (!name.trim() || name.length > 180 || /[/\\\p{Cc}]/u.test(name))
          throw new Error("document-unreadable");
        if (!documentKind(name)) throw new Error("document-unsupported");
        if (Number(request.headers["content-length"]) > documentLimits.bytes)
          throw new Error("document-size");
        /** @type {Buffer[]} */
        const chunks = [];
        let size = 0;
        for await (const chunk of request) {
          size += chunk.length;
          if (size > documentLimits.bytes) throw new Error("document-size");
          chunks.push(chunk);
        }
        const document = await readDocument(
          name,
          Buffer.concat(chunks),
          controller.signal,
        );
        if (!response.destroyed) json(response, 200, document);
      } catch (error) {
        const code =
          error instanceof Error &&
          /^document-(size|unsupported|complex|timeout|no-text)$/.test(
            error.message,
          )
            ? error.message
            : "document-unreadable";
        if (!response.destroyed)
          json(response, code === "document-size" ? 413 : 400, { error: code });
      } finally {
        clearTimeout(timer);
        controller.abort();
        readingDocument = false;
      }
      return;
    }
    if (request.method === "GET" && pathname === "/api/status") {
      try {
        const upstream = await fetch(`${baseUrl}/api/tags`, {
          signal: AbortSignal.timeout(4000),
        });
        if (!upstream.ok) throw new Error("unavailable");
        /** @type {{ models?: { name: string }[] }} */
        const data = await upstream.json();
        const ready = data.models?.some((item) => item.name === model) ?? false;
        json(response, 200, {
          ready,
          model,
          busy,
          reason: ready ? null : "model-missing",
        });
      } catch {
        json(response, 200, {
          ready: false,
          model,
          busy,
          reason: "engine-unavailable",
        });
      }
      return;
    }
    if (request.method === "POST" && pathname === "/api/chat") {
      if (
        !request.headers["content-type"]?.startsWith("application/json") ||
        request.headers["sec-fetch-site"] === "cross-site"
      ) {
        json(response, 415, { error: "invalid-request" });
        return;
      }
      let input;
      try {
        /** @type {Buffer[]} */
        const chunks = [];
        let bytes = 0;
        for await (const chunk of request) {
          bytes += chunk.length;
          if (bytes > 160_000) throw new Error("invalid-request");
          chunks.push(chunk);
        }
        input = validateRequest(
          JSON.parse(Buffer.concat(chunks).toString("utf8")),
        );
      } catch (error) {
        const code =
          error instanceof Error &&
          [
            "document-limit",
            "document-context-limit",
            "document-unreadable",
          ].includes(error.message)
            ? error.message
            : "invalid-request";
        json(response, 400, { error: code });
        return;
      }
      if (busy) {
        json(response, 429, { error: "busy" });
        return;
      }
      busy = true;
      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), timeout);
      response.on("close", () => controller.abort());
      try {
        for (let attempt = 0; attempt < 2; attempt++) {
          controller.signal.throwIfAborted();
          const upstream = await fetch(`${baseUrl}/api/chat`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(
              buildPayload(
                input,
                model,
                attempt ? regenerationInstruction : "",
              ),
            ),
            signal: controller.signal,
          });
          if (!upstream.ok || !upstream.body)
            throw new Error(
              upstream.status === 404 ? "model-missing" : "engine-unavailable",
            );
          if (!response.headersSent) {
            response.writeHead(200, {
              "Content-Type": "application/x-ndjson; charset=utf-8",
              "Cache-Control": "no-store",
              "X-Accel-Buffering": "no",
            });
            response.flushHeaders();
          }
          let raw = "";
          const { truncated } = await readResponse(upstream, (token) => {
            raw += token;
            if (raw.length > responseLimit) throw new Error("policy-blocked");
          });
          controller.signal.throwIfAborted();
          if (truncated) throw new Error("incomplete-response");
          const adapted = adaptResponse(raw, input.format);
          if (attempt === 0 && adapted.reasons.includes("conflicting-result"))
            continue;
          if (!adapted.accepted) throw new Error("policy-blocked");
          response.end(
            `${JSON.stringify({
              message: { content: adapted.text },
              done: true,
              policy: {
                version: adapted.version,
                changes: attempt
                  ? ["regenerated", ...adapted.changes]
                  : adapted.changes,
              },
            })}\n`,
          );
          return;
        }
      } catch (failure) {
        if (response.destroyed) return;
        const known = [
          "model-missing",
          "policy-blocked",
          "incomplete-response",
          "invalid-response",
        ];
        const error = controller.signal.aborted
          ? "timeout"
          : failure instanceof Error && known.includes(failure.message)
            ? failure.message
            : "engine-unavailable";
        if (response.headersSent)
          response.end(`${JSON.stringify({ error })}\n`);
        else json(response, 503, { error });
      } finally {
        controller.abort();
        clearTimeout(timer);
        busy = false;
      }
      return;
    }
    if (request.method !== "GET") {
      json(response, 405, { error: "method-not-allowed" });
      return;
    }
    if (pathname === "/") {
      response.writeHead(302, { Location: "/preview/" });
      response.end();
      return;
    }
    const file = pathname === "/preview/" ? "/preview/index.html" : pathname;
    if (
      !/^\/preview\/[a-z-]+\.(html|css|js|svg)$/.test(file) &&
      !/^\/content\/(fr|en)\.json$/.test(file)
    ) {
      json(response, 404, { error: "not-found" });
      return;
    }
    try {
      const data = await readFile(new URL(`..${file}`, import.meta.url));
      /** @type {Record<string, string>} */
      const mime = {
        ".html": "text/html",
        ".css": "text/css",
        ".js": "text/javascript",
        ".svg": "image/svg+xml",
        ".json": "application/json",
      };
      response.writeHead(200, {
        "Content-Type": `${mime[extname(file)]}; charset=utf-8`,
        "Cache-Control": "no-cache",
      });
      response.end(data);
    } catch {
      json(response, 404, { error: "not-found" });
    }
  });
}
