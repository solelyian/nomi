import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import {
  commandsFor,
  filterCommands,
  resolveRoute,
  resultText,
  workspaceSelection,
} from "../preview/model.js";

const fr = JSON.parse(
  await readFile(new URL("../content/fr.json", import.meta.url), "utf8"),
);
const en = JSON.parse(
  await readFile(new URL("../content/en.json", import.meta.url), "utf8"),
);

test("both clients receive complete catalogs with stable action and context identities", () => {
  assert.deepEqual(
    Object.keys(fr.labels).sort(),
    Object.keys(en.labels).sort(),
  );
  for (const catalog of [fr, en]) {
    assert.equal(catalog.actions.length, 6);
    assert.equal(catalog.contexts.length, 4);
    assert.equal(new Set(catalog.actions.map((action) => action.id)).size, 6);
    for (const value of Object.values(catalog.labels))
      assert.ok(value.trim().length > 0);
    for (const action of catalog.actions) {
      assert.ok(
        action.title && action.description && action.prompt && action.summary,
      );
      assert.equal(action.steps.length, 3);
      assert.ok(
        action.steps.every(
          (step) => typeof step === "string" && step.length > 0,
        ),
      );
    }
    for (const context of catalog.contexts) {
      assert.ok(catalog.actions.some((action) => action.id === context.action));
    }
  }
  assert.deepEqual(
    fr.actions.map((action) => action.id),
    en.actions.map((action) => action.id),
  );
  assert.deepEqual(
    fr.contexts.map((context) => [context.id, context.action]),
    en.contexts.map((context) => [context.id, context.action]),
  );
});

test("every literal UI translation reference exists in both catalogs", async () => {
  const files = [
    "preview/app.js",
    "preview/index.html",
    "Nomi.WinUI/MainWindow.xaml.cs",
  ];
  for (const path of files) {
    const source = await readFile(
      new URL(`../${path}`, import.meta.url),
      "utf8",
    );
    const patterns = [
      /\b(?:T|t|text)\("([a-zA-Z]+)"\)/g,
      /data-(?:i18n|label)="([^"]+)"/g,
    ];
    for (const pattern of patterns) {
      for (const [, key] of source.matchAll(pattern)) {
        assert.ok(fr.labels[key], `Missing French key ${key} in ${path}`);
        assert.ok(en.labels[key], `Missing English key ${key} in ${path}`);
      }
    }
  }
});

test("the palette supports accents, case, whitespace and multi-word queries", () => {
  assert.ok(
    filterCommands(fr, "  VERIFIER  ").some(
      (command) => command.id === "verify",
    ),
  );
  assert.ok(
    filterCommands(fr, "etudes").some((command) => command.id === "study"),
  );
  assert.ok(
    filterCommands(fr, "logique code").some(
      (command) => command.id === "software",
    ),
  );
  assert.ok(
    filterCommands(en, "higher education").some(
      (command) => command.id === "study",
    ),
  );
  assert.equal(filterCommands(fr, "does-not-exist").length, 0);
  assert.equal(filterCommands(fr, "").length, 10);
});

test("all palette destinations resolve and preserve identity across a language change", () => {
  for (const command of commandsFor(fr)) {
    const french = resolveRoute(`#${command.route}`, fr);
    const english = resolveRoute(`#${command.route}`, en);
    assert.equal(french.page, "action");
    assert.equal(french.action.id, english.action.id);
    assert.equal(french.context?.id, english.context?.id);
  }
});

test("unknown routes return home and unrelated contexts do not leak into another action", () => {
  assert.equal(resolveRoute("#action/missing", fr).page, "home");
  assert.equal(resolveRoute("#unknown", fr).page, "home");
  assert.equal(resolveRoute("#action/verify/study", fr).context, undefined);
  assert.equal(resolveRoute("#preferences", fr).page, "preferences");
});

test("copy uses the selected response format and language", () => {
  for (const catalog of [fr, en]) {
    const action = catalog.actions.find((item) => item.id === "verify");
    assert.equal(resultText(action, "summary"), action.summary);
    assert.equal(resultText(action, "steps"), action.steps.join("\n\n"));
    assert.match(action.summary, /68/);
  }
});

test("home submits the chosen intent while home aliases share the same draft", () => {
  for (const catalog of [fr, en]) {
    for (const action of catalog.actions) {
      for (const hash of ["", "#home", "#unknown"]) {
        const selection = workspaceSelection(hash, catalog, action.id);
        assert.equal(selection.action.id, action.id);
        assert.equal(selection.draftHash, "#home");
      }
    }
  }
});

test("workspaces keep their action and draft when the home intent changes", () => {
  for (const hash of ["#action/verify", "#action/verify/software"]) {
    const selection = workspaceSelection(hash, fr, "plan");
    assert.equal(selection.action.id, "verify");
    assert.equal(selection.draftHash, hash);
  }
  assert.equal(
    workspaceSelection("#preferences", fr, "plan").action,
    undefined,
  );
  assert.equal(workspaceSelection("#spaces", fr, "plan").action, undefined);
  assert.equal(workspaceSelection("#home", fr, "unknown").action, undefined);
});

test("UI foreground colours meet WCAG AA normal-text contrast on their surfaces", () => {
  function luminance(hex) {
    const values = hex
      .match(/\w\w/g)
      .map((channel) => parseInt(channel, 16) / 255);
    const linear = values.map((value) =>
      value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4,
    );
    return linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722;
  }
  for (const [foreground, background] of [
    ["282d28", "fcfcfa"],
    ["63695f", "ffffff"],
    ["63695f", "f3f5ef"],
    ["46563a", "fcfcfa"],
    ["35462c", "e8ede1"],
    ["ffffff", "34432c"],
  ]) {
    const values = [luminance(foreground), luminance(background)].sort(
      (a, b) => b - a,
    );
    assert.ok(
      (values[0] + 0.05) / (values[1] + 0.05) >= 4.5,
      `${foreground} on ${background}`,
    );
  }
});
