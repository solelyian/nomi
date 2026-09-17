import js from "@eslint/js";

export default [
  js.configs.recommended,
  {
    files: ["preview/*.js"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      globals: Object.fromEntries(
        [
          "window",
          "document",
          "location",
          "navigator",
          "localStorage",
          "fetch",
          "HTMLElement",
          "HTMLDialogElement",
          "HTMLInputElement",
          "HTMLSelectElement",
          "HTMLFormElement",
          "HTMLFieldSetElement",
          "HTMLTextAreaElement",
          "AbortController",
          "AbortSignal",
          "TextDecoder",
          "Element",
          "clearTimeout",
        ].map((name) => [name, "readonly"]),
      ),
    },
  },
  {
    files: ["server/*.js", "checks/*.js"],
    languageOptions: {
      globals: Object.fromEntries(
        [
          "process",
          "console",
          "fetch",
          "Buffer",
          "URL",
          "AbortController",
          "AbortSignal",
          "setTimeout",
          "clearTimeout",
          "TextEncoder",
          "TextDecoder",
          "ReadableStream",
          "Response",
        ].map((name) => [name, "readonly"]),
      ),
    },
  },
];
