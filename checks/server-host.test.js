import { test } from "node:test";
import assert from "node:assert/strict";
import { request } from "node:http";
import { createNomiServer } from "../server/app.js";

/** @param {number} port @param {string} host */
function status(port, host) {
  return new Promise((resolve, reject) => {
    const call = request(
      { port, host: "127.0.0.1", path: "/api/status", headers: { host } },
      (response) => {
        response.resume();
        resolve(response.statusCode);
      },
    );
    call.on("error", reject);
    call.end();
  });
}

test("the preview server only answers loopback host names", async () => {
  const server = createNomiServer().listen(0, "127.0.0.1");
  await new Promise((resolve) => server.once("listening", resolve));
  const address = server.address();
  const port = typeof address === "object" && address ? address.port : 0;
  try {
    assert.equal(await status(port, "rebind.example"), 403);
    assert.equal(await status(port, `rebind.example:${port}`), 403);
    assert.notEqual(await status(port, `127.0.0.1:${port}`), 403);
    assert.notEqual(await status(port, `localhost:${port}`), 403);
  } finally {
    server.close();
  }
});
