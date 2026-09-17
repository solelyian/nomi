import { readFileSync } from "node:fs";
import {
  validateDocuments,
  buildDocumentContext,
} from "../preview/documents.js";

/** @type {import("../preview/types").InferenceConfig} */
export const config = JSON.parse(
  readFileSync(new URL("../content/inference.json", import.meta.url), "utf8"),
);

/** @param {unknown} input */
export function validateRequest(input) {
  if (typeof input !== "object" || input === null)
    throw new Error("invalid-request");
  const documents = validateDocuments(
    "documents" in input ? input.documents : undefined,
  );
  if (
    !("prompt" in input) ||
    typeof input.prompt !== "string" ||
    (!input.prompt.trim() && !documents.length) ||
    input.prompt.length > 6000
  )
    throw new Error("invalid-request");
  if (
    !("language" in input) ||
    (input.language !== "fr" && input.language !== "en")
  )
    throw new Error("invalid-request");
  if (
    !("format" in input) ||
    (input.format !== "steps" && input.format !== "summary")
  )
    throw new Error("invalid-request");
  if (
    !("action" in input) ||
    typeof input.action !== "string" ||
    !Object.hasOwn(config.actions, input.action)
  )
    throw new Error("invalid-request");
  if (
    documents.length &&
    Buffer.byteLength(input.prompt + buildDocumentContext(documents), "utf8") >
      12_000
  )
    throw new Error("document-context-limit");
  return {
    prompt: input.prompt.trim(),
    action: input.action,
    language: input.language,
    format: input.format,
    documents,
  };
}

/** @param {ReturnType<typeof validateRequest>} request @param {string} model @param {string} [instruction] */
export function buildPayload(request, model, instruction = "") {
  return {
    model,
    messages: [
      {
        role: "system",
        content: [
          config.system,
          config.actions[request.action],
          config.formats[request.format],
          config.languages[request.language],
          ...(request.documents.length ? [config.documents] : []),
          ...(instruction ? [instruction] : []),
        ].join("\n\n"),
      },
      {
        role: "user",
        content:
          (request.prompt || config.documentPrompts[request.language]) +
          (request.documents.length
            ? buildDocumentContext(request.documents)
            : ""),
      },
    ],
    stream: true,
    think: false,
    keep_alive: "15m",
    options: {
      temperature: 0.2,
      num_ctx: request.documents.length ? 16384 : 4096,
      num_predict: 768,
    },
  };
}
