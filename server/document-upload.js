import { Worker } from "node:worker_threads";

/**
 * @param {string} name @param {Uint8Array} bytes @param {AbortSignal} signal
 * @returns {Promise<import("../preview/types").AttachedDocument>}
 */
export function readDocument(name, bytes, signal) {
  return new Promise((resolve, reject) => {
    signal.throwIfAborted();
    const worker = new Worker(
      new URL("./document-worker.js", import.meta.url),
      {
        workerData: { name, bytes },
        resourceLimits: { maxOldGenerationSizeMb: 256 },
      },
    );
    const cleanup = () => {
      clearTimeout(timer);
      signal.removeEventListener("abort", abort);
      void worker.terminate();
    };
    const abort = () => {
      cleanup();
      reject(new Error("document-timeout"));
    };
    const timer = setTimeout(abort, 30_000);
    signal.addEventListener("abort", abort, { once: true });
    worker.once("message", (result) => {
      cleanup();
      if ("error" in result) reject(new Error(result.error));
      else resolve(result);
    });
    worker.once("error", () => {
      cleanup();
      reject(new Error("document-complex"));
    });
    worker.once("exit", () => {
      cleanup();
      reject(new Error("document-unreadable"));
    });
  });
}
