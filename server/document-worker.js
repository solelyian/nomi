import { parentPort, workerData } from "node:worker_threads";
import { extractDocument } from "./documents.js";

try {
  parentPort?.postMessage(
    await extractDocument(workerData.name, workerData.bytes),
  );
} catch (error) {
  parentPort?.postMessage({
    error: error instanceof Error ? error.message : "document-unreadable",
  });
}
