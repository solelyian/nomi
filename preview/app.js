import { filterCommands, resolveRoute, workspaceSelection } from "./model.js";
import { readResponse } from "./inference.js";
import {
  documentExtensions,
  documentKind,
  documentLimits,
  validateDocuments,
} from "./documents.js";

/** @param {string} id */
function element(id) {
  const found = document.getElementById(id);
  if (!found) throw new Error(`Missing element: ${id}`);
  return found;
}

/** @param {string} key @param {string} fallback */
function readPreference(key, fallback) {
  try {
    return localStorage.getItem(`nomi.${key}`) || fallback;
  } catch {
    return fallback;
  }
}

/** @param {string} key @param {string} value */
function savePreference(key, value) {
  try {
    localStorage.setItem(`nomi.${key}`, value);
  } catch {
    /* Preferences still apply for the current session. */
  }
}

/** @param {string} value */
function escape(value) {
  return value.replace(
    /[&<>"']/g,
    (character) => `&#${character.charCodeAt(0)};`,
  );
}

/** @type {Record<string, string>} */
const paths = {
  home: '<path d="m3 10 9-7 9 7v10a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1Z"/>',
  spaces:
    '<rect x="3" y="4" width="7" height="7" rx="1.5"/><rect x="14" y="4" width="7" height="7" rx="1.5"/><rect x="3" y="15" width="7" height="6" rx="1.5"/><rect x="14" y="15" width="7" height="6" rx="1.5"/>',
  preferences:
    '<path d="M4 7h8m5 0h3M4 17h3m5 0h8"/><circle cx="14" cy="7" r="3"/><circle cx="10" cy="17" r="3"/>',
  search: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/>',
  globe:
    '<circle cx="12" cy="12" r="9"/><ellipse cx="12" cy="12" rx="4" ry="9"/><path d="M3 12h18"/>',
  close: '<path d="m6 6 12 12M6 18 18 6"/>',
  arrow: '<path d="M5 12h14m-5-5 5 5-5 5"/>',
  diagonal: '<path d="M6 18 18 6M7 6h11v11"/>',
  back: '<path d="M19 12H5m5-5-5 5 5 5"/>',
  command: '<path d="m13 3-8 11h6l-1 7 9-12h-7z"/>',
  understand:
    '<path d="M4 5h5a4 4 0 0 1 3 2 4 4 0 0 1 3-2h5v14h-5a4 4 0 0 0-3 2 4 4 0 0 0-3-2H4ZM12 7v14"/>',
  verify:
    '<rect x="4" y="4" width="16" height="16" rx="4"/><path d="m8 12 3 3 5-6"/>',
  convert: '<path d="M4 8h15m-4-4 4 4-4 4M20 16H5m4-4-4 4 4 4"/>',
  plan: '<circle cx="12" cy="12" r="9"/><path d="M12 6v6l4 2"/>',
  communicate:
    '<path d="M20 14a3 3 0 0 1-3 3H9l-5 4V6a3 3 0 0 1 3-3h10a3 3 0 0 1 3 3Z"/><path d="M8 8h8m-8 4h5"/>',
  learn: '<path d="m3 9 9-5 9 5-9 5ZM6 11v6l6 3 6-3v-6M21 9v8"/>',
  software: '<path d="m8 6-6 6 6 6m8-12 6 6-6 6m-3-14-2 16"/>',
  study: '<path d="m3 9 9-5 9 5-9 5ZM6 11v6l6 3 6-3v-6"/>',
  sales:
    '<path d="M6 7V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v2M3 7h18v13H3ZM3 12h18M10 12v3h4v-3"/>',
  knowledge:
    '<rect x="3" y="4" width="18" height="13" rx="2"/><path d="M8 21h8m-4-4v4M7 13v-2m5 2V8m5 5V6"/>',
  copy: '<rect x="8" y="8" width="12" height="13" rx="2"/><path d="M16 8V3H3v13h5"/>',
  attach:
    '<path d="m8 12 6-6a3 3 0 0 1 4 4l-8 8a5 5 0 0 1-7-7l8-8"/><path d="m6 14 8-8"/>',
};

/** @param {string} name */
function icon(name) {
  return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[name] || paths.command}</svg>`;
}

/** @type {Record<string, import("./types").Catalog>} */
const catalogs = Object.fromEntries(
  await Promise.all(
    ["fr", "en"].map(async (locale) => {
      const response = await fetch(`../content/${locale}.json`);
      if (!response.ok) throw new Error(`Cannot load ${locale} catalog`);
      return [locale, await response.json()];
    }),
  ),
);
let language = readPreference("language", "fr") === "en" ? "en" : "fr";
let density =
  readPreference("density", "comfortable") === "compact"
    ? "compact"
    : "comfortable";
let format =
  readPreference("format", "steps") === "summary" ? "summary" : "steps";
let homeIntent = "understand";
/** @type {Map<string, import("./types").InferenceState>} */
const drafts = new Map();
/** @type {AbortController | null} */
let generation = null;
/** @type {AbortController | null} */
let documentUpload = null;
let engine = { status: "engineChecking", model: "" };
let toastTimer = 0;
/** @type {HTMLElement | null} */
let paletteOpener = null;
const main = element("main");
const palette = /** @type {HTMLDialogElement} */ (element("palette"));
const search = /** @type {HTMLInputElement} */ (element("command-search"));
const languageSelect = /** @type {HTMLSelectElement} */ (element("language"));
const catalog = () => catalogs[language];
/** @param {string} key */
const t = (key) => catalog().labels[key];
/** @param {string} key */
const text = (key) => escape(t(key));

function currentDraft() {
  const hash = workspaceSelection(
    location.hash,
    catalog(),
    homeIntent,
  ).draftHash;
  const key = `${language}:${hash}`;
  if (!drafts.has(key))
    drafts.set(key, {
      draft: "",
      output: "",
      model: "",
      status: "idle",
      rules: [],
      documents: [],
      documentStatus: "",
      documentBusy: false,
    });
  return /** @type {import("./types").InferenceState} */ (drafts.get(key));
}

function selectedAction() {
  return workspaceSelection(location.hash, catalog(), homeIntent).action;
}

function cancelGeneration() {
  generation?.abort();
  documentUpload?.abort();
}

/** @param {import("./types").InferenceState} draft */
function renderDocuments(draft) {
  const area = document.getElementById("document-area");
  if (!area || currentDraft() !== draft) return;
  const busy = draft.documentBusy || draft.status === "generating";
  area.innerHTML = `<div class="document-toolbar"><button type="button" class="secondary-button" id="attach-files" ${busy ? "disabled" : ""}>${icon("attach")}${text("attachDocuments")}</button><span>${text("documentDrop")}</span></div>
    <input type="file" id="document-picker" accept="${documentExtensions.join(",")}" multiple hidden/>
    <p class="document-help">${text("documentHelp")}</p>
    <ul class="document-list" aria-label="${text("attachedDocuments")}">${draft.documents
      .map(
        (
          file,
          index,
        ) => `<li class="document-card"><div class="document-heading"><strong>${escape(file.name)}</strong><button type="button" class="text-link" data-remove-document="${index}" aria-label="${text("documentRemove")} ${escape(file.name)}" ${busy ? "disabled" : ""}>${text("documentRemove")}</button></div>
    ${file.truncated ? `<p class="document-warning">${text("documentPartial")}</p>` : ""}
    <details><summary>${text("documentPreview")}</summary><pre tabindex="0">${escape(file.text)}</pre></details></li>`,
      )
      .join("")}</ul>
    ${draft.documents.length ? `<p class="document-help">${text("documentPrivacy")}</p>` : ""}`;
  element("document-status").textContent = draft.documentStatus
    ? t(draft.documentStatus)
    : "";
  /** @type {HTMLTextAreaElement} */ (element("request-input")).required =
    !draft.documents.length;
  /** @type {HTMLButtonElement} */ (element("send")).disabled = busy;
}

/** @param {File[]} files */
async function attachFiles(files) {
  if (!selectedAction() || generation || documentUpload) return;
  const draft = currentDraft();
  if (draft.documents.length + files.length > documentLimits.files) {
    draft.documentStatus = "document-limit";
    renderDocuments(draft);
    return;
  }
  const controller = new AbortController();
  documentUpload = controller;
  draft.documentBusy = true;
  draft.documentStatus = "documentReading";
  renderDocuments(draft);
  try {
    for (const file of files) {
      controller.signal.throwIfAborted();
      if (!documentKind(file.name)) throw new Error("document-unsupported");
      if (file.size > documentLimits.bytes) throw new Error("document-size");
      const response = await fetch("/api/documents", {
        method: "POST",
        headers: {
          "Content-Type": "application/octet-stream",
          "X-File-Name": encodeURIComponent(file.name),
        },
        body: file,
        signal: controller.signal,
      });
      const data = await response.json();
      if (!response.ok) throw new Error(data.error);
      controller.signal.throwIfAborted();
      draft.documents = validateDocuments([...draft.documents, data]);
      renderDocuments(draft);
    }
    draft.documentStatus = "documentReady";
  } catch (error) {
    draft.documentStatus = controller.signal.aborted
      ? "documentStopped"
      : error instanceof Error && catalog().labels[error.message]
        ? error.message
        : "document-unreadable";
  } finally {
    draft.documentBusy = false;
    documentUpload = null;
    renderDocuments(draft);
  }
}

async function checkEngine() {
  try {
    const response = await fetch("/api/status", {
      signal: AbortSignal.timeout(5000),
    });
    /** @type {{ ready: boolean, model: string, reason: string }} */
    const data = await response.json();
    engine = {
      status: data.ready ? "engineReady" : data.reason,
      model: data.model,
    };
  } catch {
    engine.status = "engine-unavailable";
  }
  updateEngine();
}

function updateEngine() {
  const status = document.getElementById("engine-status");
  if (status)
    status.textContent = `${t(engine.status)}${engine.model ? ` · ${engine.model}` : ""}`;
}

/** @param {import("./types").Context} context */
function contextCard(context) {
  return `<button class="context-card" type="button" data-open="action/${escape(context.action)}/${escape(context.id)}">${icon(context.id)}<span class="card-title">${escape(context.title)}</span><span class="card-description">${escape(context.description)}</span></button>`;
}

function homePage() {
  const action = selectedAction();
  if (!action) return "";
  const starters = [
    ["verify", t("tryVerify")],
    ["plan", t("tryPlan")],
    ["learn", t("tryLearn")],
  ];
  const resumable = [...drafts].filter(
    ([key, draft]) =>
      key.startsWith(`${language}:#action/`) && draft.draft.trim(),
  );
  return `<div class="work-area home-workspace"><section class="hero"><p class="eyebrow">${text("greeting")}</p><h1 tabindex="-1">${text("headline")}</h1><p class="intro">${text("intro")}</p></section>
    ${workspacePanels(action, true)}
    <section class="starter-section" id="starter-section" aria-labelledby="starter-title" ${currentDraft().output ? "hidden" : ""}><div class="section-heading"><h2 id="starter-title">${text("starterTitle")}</h2><a class="text-link" href="#spaces">${text("allSpaces")}${icon("arrow")}</a></div><div class="starter-grid">${starters.map(([id, label]) => `<button type="button" class="starter-card" data-example="${id}">${icon(id)}<span>${escape(label)}</span>${icon("arrow")}</button>`).join("")}</div></section>
    ${
      resumable.length
        ? `<section class="resume-section" aria-labelledby="resume-title"><div class="section-heading"><h2 id="resume-title">${text("resumeTitle")}</h2><p>${text("sessionOnly")}</p></div>${resumable
            .slice(-3)
            .reverse()
            .map(([key, draft]) => {
              const route = key.slice(language.length + 2);
              const title =
                resolveRoute(`#${route}`, catalog()).action?.title ??
                t("action");
              return `<button type="button" class="resume-row" data-open="${escape(route)}"><strong>${escape(title)}</strong><span>${escape(draft.draft)}</span>${icon("arrow")}</button>`;
            })
            .join("")}</section>`
        : ""
    }</div>`;
}

function spacesPage() {
  return `<p class="eyebrow">${text("workspace")}</p><h1 tabindex="-1">${text("spaces")}</h1><p class="intro">${text("spacesIntro")}</p><div class="spaces-grid">${catalog().contexts.map(contextCard).join("")}</div><p class="demo-notice">${text("demoNotice")}</p>`;
}

/** @param {string} id @param {string} label @param {string} help @param {string[][]} options @param {string} value */
function setting(id, label, help, options, value) {
  return `<div class="setting-row"><div><label for="${id}">${text(label)}</label><p id="${id}-help">${text(help)}</p></div><select id="${id}" aria-describedby="${id}-help">${options.map(([key, name]) => `<option value="${key}"${key === value ? " selected" : ""}>${escape(name)}</option>`).join("")}</select></div>`;
}

function preferencesPage() {
  return `<p class="eyebrow">${text("workspace")}</p><h1 tabindex="-1">${text("preferences")}</h1><p class="intro">${text("preferencesIntro")}</p><section class="settings" aria-label="${text("preferences")}">
    ${setting(
      "settings-language",
      "language",
      "languageHelp",
      [
        ["fr", "Français"],
        ["en", "English"],
      ],
      language,
    )}
    ${setting(
      "settings-density",
      "density",
      "densityHelp",
      [
        ["comfortable", t("comfortable")],
        ["compact", t("compact")],
      ],
      density,
    )}
    ${setting(
      "settings-format",
      "response",
      "responseHelp",
      [
        ["steps", t("steps")],
        ["summary", t("summary")],
      ],
      format,
    )}
    </section>`;
}

/** @param {import("./types").Action} action @param {import("./types").Context | undefined} context */
function actionPage(action, context) {
  return `<div class="work-area"><a class="back-button" href="#home">${icon("back")}${text("back")}</a><header class="action-header"><p class="eyebrow">${context ? escape(context.title) : text("spaceTag")}</p><h1 tabindex="-1">${icon(action.id)}${escape(action.title)}</h1><p class="intro">${escape(action.description)}</p></header>${workspacePanels(action, false)}</div>`;
}

/** @param {import("./types").Action} action @param {boolean} home */
function workspacePanels(action, home) {
  const draft = currentDraft();
  const busy = draft.status === "generating";
  return `<div class="workspace-body ${draft.output ? "has-response" : ""}" id="work-content"><form class="panel request-form composer" id="request-form">
    ${
      home
        ? `<fieldset class="intent-picker" ${busy ? "disabled" : ""}><legend>${text("intent")}</legend><div class="intent-options">${catalog()
            .actions.map(
              (item) =>
                `<label class="intent-option"><input type="radio" name="intent" value="${item.id}" ${item.id === homeIntent ? "checked" : ""}/><span>${icon(item.id)}${escape(item.title)}</span></label>`,
            )
            .join(
              "",
            )}</div></fieldset><p class="intent-description" id="intent-description">${escape(action.description)}</p>`
        : ""
    }
    <label for="request-input" class="request-label">${text("requestLabel")}</label><textarea id="request-input" name="prompt" rows="4" maxlength="6000" ${draft.documents.length ? "" : "required"} ${busy ? "readonly" : ""} placeholder="${escape(action.prompt)}" aria-describedby="request-help">${escape(draft.draft)}</textarea>
    <section id="document-area" aria-label="${text("attachedDocuments")}"></section><p id="document-status" class="document-help" role="status" aria-live="polite"></p>
    <div class="composer-toolbar"><label class="format-control" for="next-format">${text("nextFormat")}<select id="next-format" ${busy ? "disabled" : ""}><option value="steps" ${format === "steps" ? "selected" : ""}>${text("steps")}</option><option value="summary" ${format === "summary" ? "selected" : ""}>${text("summary")}</option></select></label><div class="result-actions"><button type="button" class="secondary-button" id="load-example" ${busy ? "disabled" : ""}>${text("loadExample")}</button><button type="submit" class="primary-button" id="send" ${busy ? "disabled" : ""}>${text("send")}${icon("arrow")}</button><button type="button" class="secondary-button" id="stop" ${busy ? "" : "hidden"}>${text("stop")}</button></div></div>
    <p id="request-help" class="composer-help">${text("requestHelp")}</p><p id="generation-status" class="generation-status" role="status" aria-live="polite">${draft.status === "idle" ? "" : text(draft.status)}</p></form>
    <section id="result" class="panel result-panel" aria-labelledby="result-title" ${draft.output ? "" : "hidden"}><div class="result-heading"><h2 id="result-title" tabindex="-1">${text("resultTitle")}</h2><button type="button" class="secondary-button" id="copy">${icon("copy")}${text("copy")}</button></div><div class="result-content generated-output" id="generated-output">${escape(draft.output)}</div><p class="result-reminder">${text("resultReminder")}</p><details class="response-details"><summary>${text("responseDetails")}</summary><p id="policy-rules">${draft.rules.map((rule) => text(`policy-${rule}`)).join(" · ")}</p><p>${text("resultTag")} <span id="response-model">${escape(draft.model)}</span></p></details><button type="button" class="text-link" id="edit-request">${text("editRequest")}${icon("back")}</button></section></div>
    <details class="engine-details"><summary>${text("localProcessing")}</summary><div class="engine-row"><p id="engine-status" role="status"></p><button type="button" class="secondary-button" id="engine-refresh">${text("engineRefresh")}</button></div><p>${text("demoNotice")}</p></details>`;
}

async function generate() {
  if (generation || documentUpload) return;
  const action = selectedAction();
  if (!action) return;
  const draft = currentDraft();
  if (
    (!draft.draft.trim() && !draft.documents.length) ||
    draft.draft.length > 6000
  ) {
    notify(t("invalid-request"));
    return;
  }
  const controller = new AbortController();
  generation = controller;
  const requestLanguage = language;
  const requestHash = location.hash;
  const visible = () =>
    requestLanguage === language && requestHash === location.hash;
  draft.output = "";
  draft.rules = [];
  draft.status = "generating";
  draft.model = engine.model;
  const timer = window.setTimeout(() => controller.abort("timeout"), 185000);
  const update = () => {
    if (!visible()) return;
    element("generated-output").textContent = draft.output;
    element("policy-rules").textContent = draft.rules
      .map((rule) => t(`policy-${rule}`))
      .join(" · ");
    element("response-model").textContent = draft.model;
    element("result").hidden = !draft.output;
    element("work-content").classList.toggle("has-response", !!draft.output);
    const starters = document.getElementById("starter-section");
    if (starters)
      starters.hidden = !!draft.output || draft.status === "generating";
    const intentPicker = document.querySelector(".intent-picker");
    if (intentPicker instanceof HTMLFieldSetElement)
      intentPicker.disabled = draft.status === "generating";
    /** @type {HTMLSelectElement} */ (element("next-format")).disabled =
      draft.status === "generating";
    element("generation-status").textContent = t(draft.status);
    const active = draft.status === "generating";
    /** @type {HTMLButtonElement} */ (element("send")).disabled = active;
    /** @type {HTMLButtonElement} */ (element("load-example")).disabled =
      active;
    /** @type {HTMLTextAreaElement} */ (element("request-input")).readOnly =
      active;
    element("stop").hidden = !active;
    renderDocuments(draft);
  };
  update();
  try {
    const response = await fetch("/api/chat", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        prompt: draft.draft,
        action: action.id,
        language,
        format,
        documents: draft.documents,
      }),
      signal: controller.signal,
    });
    let output = "";
    const { truncated, policy } = await readResponse(response, (token) => {
      output += token;
    });
    controller.signal.throwIfAborted();
    if (truncated) throw new Error("incomplete-response");
    if (
      !policy ||
      policy.changes.some((rule) => !catalog().labels[`policy-${rule}`])
    )
      throw new Error("invalid-response");
    draft.output = output;
    draft.rules = policy.changes;
    draft.status = policy.changes.length ? "policy-adjusted" : "policy-applied";
  } catch (error) {
    draft.status = controller.signal.aborted
      ? controller.signal.reason === "timeout"
        ? "timeout"
        : "stopped"
      : error instanceof Error && catalog().labels[error.message]
        ? error.message
        : "engine-unavailable";
  } finally {
    clearTimeout(timer);
    generation = null;
    update();
    void checkEngine();
  }
}

/** @param {boolean} [focus] */
function render(focus = false) {
  const route = resolveRoute(location.hash, catalog());
  document.documentElement.lang = catalog().locale;
  document.title = `Nomi — ${route.action?.title || t(route.page)}`;
  document.body.dataset.density = density;
  document.querySelectorAll("[data-i18n]").forEach((node) => {
    if (node instanceof HTMLElement && node.dataset.i18n)
      node.textContent = t(node.dataset.i18n);
  });
  document.querySelectorAll("[data-label]").forEach((node) => {
    if (node instanceof HTMLElement && node.dataset.label)
      node.setAttribute("aria-label", t(node.dataset.label));
  });
  document.querySelectorAll("[data-icon]").forEach((node) => {
    if (node instanceof HTMLElement && node.dataset.icon)
      node.innerHTML = icon(node.dataset.icon);
  });
  document.querySelectorAll(".nav-link").forEach((node) => {
    if (node instanceof HTMLElement && node.dataset.page === route.page)
      node.setAttribute("aria-current", "page");
    else node.removeAttribute("aria-current");
  });
  languageSelect.value = language;
  search.placeholder = t("commandHint");
  element("current-page").textContent =
    route.action?.title || t(route.page === "home" ? "overview" : route.page);
  if (route.action) main.innerHTML = actionPage(route.action, route.context);
  else if (route.page === "spaces") main.innerHTML = spacesPage();
  else if (route.page === "preferences") main.innerHTML = preferencesPage();
  else main.innerHTML = homePage();
  updateEngine();
  if (selectedAction()) renderDocuments(currentDraft());
  if (focus) main.querySelector("h1")?.focus();
}

function renderCommands() {
  const commands = filterCommands(catalog(), search.value);
  element("command-results").innerHTML = commands
    .map(
      (command) =>
        `<button type="button" class="command-result" data-open="${escape(command.route)}">${icon(command.id)}<span>${escape(command.title)}</span><small>${text(command.kind)}</small>${icon("arrow")}</button>`,
    )
    .join("");
  element("no-results").hidden = commands.length > 0;
}

function openPalette() {
  if (palette.open) return;
  paletteOpener =
    document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
  search.value = "";
  renderCommands();
  palette.showModal();
  search.focus();
}

/** @param {string} message */
function notify(message) {
  clearTimeout(toastTimer);
  element("notification").textContent = message;
  toastTimer = window.setTimeout(() => {
    element("notification").textContent = "";
  }, 4500);
}

/** @param {string} route */
function navigate(route) {
  if (palette.open) {
    paletteOpener = null;
    palette.close();
  }
  cancelGeneration();
  if (location.hash === `#${route}`) render(true);
  else location.hash = route;
}

/** @param {string} value */
function changeLanguage(value) {
  cancelGeneration();
  language = value === "en" ? "en" : "fr";
  savePreference("language", language);
  render();
}

document.addEventListener("click", async (event) => {
  if (!(event.target instanceof Element)) return;
  const open = event.target.closest("[data-open]");
  if (open instanceof HTMLElement && open.dataset.open) {
    navigate(open.dataset.open);
    return;
  }
  if (event.target.closest("[data-command], #top-command")) {
    openPalette();
    return;
  }
  if (event.target.closest("#close-palette")) {
    palette.close();
    return;
  }
  if (event.target.closest("#stop")) cancelGeneration();
  if (event.target.closest("#attach-files")) element("document-picker").click();
  const remove = event.target.closest("[data-remove-document]");
  if (remove instanceof HTMLElement && !generation && !documentUpload) {
    const draft = currentDraft();
    draft.documents.splice(Number(remove.dataset.removeDocument), 1);
    draft.documentStatus = "";
    renderDocuments(draft);
    element("attach-files").focus();
  }
  if (event.target.closest("#engine-refresh")) void checkEngine();
  if (event.target.closest("#load-example")) {
    const action = selectedAction();
    if (action) {
      currentDraft().draft = action.prompt;
      /** @type {HTMLTextAreaElement} */ (element("request-input")).value =
        action.prompt;
      element("request-input").focus();
    }
  }
  const example = event.target.closest("[data-example]");
  if (
    example instanceof HTMLElement &&
    example.dataset.example &&
    !generation
  ) {
    const action = catalog().actions.find(
      (item) => item.id === example.dataset.example,
    );
    if (action) {
      homeIntent = action.id;
      currentDraft().draft = action.prompt;
      render();
      element("request-input").focus();
    }
  }
  if (event.target.closest("#edit-request")) {
    element("request-input").focus();
    element("request-input").scrollIntoView({ block: "center" });
  }
  if (event.target.closest("#copy")) {
    try {
      await navigator.clipboard.writeText(currentDraft().output);
      notify(t("copied"));
    } catch {
      notify(t("copyError"));
    }
  }
});

main.addEventListener("input", (event) => {
  if (
    event.target instanceof HTMLTextAreaElement &&
    event.target.id === "request-input"
  )
    currentDraft().draft = event.target.value;
});
main.addEventListener("submit", (event) => {
  if (
    event.target instanceof HTMLFormElement &&
    event.target.id === "request-form"
  ) {
    event.preventDefault();
    void generate();
  }
});
main.addEventListener("change", (event) => {
  if (
    event.target instanceof HTMLInputElement &&
    event.target.id === "document-picker"
  ) {
    void attachFiles(Array.from(event.target.files ?? []));
    return;
  }
  if (
    event.target instanceof HTMLInputElement &&
    event.target.name === "intent"
  ) {
    homeIntent = event.target.value;
    const action = selectedAction();
    if (action) {
      element("intent-description").textContent = action.description;
      /** @type {HTMLTextAreaElement} */ (
        element("request-input")
      ).placeholder = action.prompt;
    }
    return;
  }
  if (!(event.target instanceof HTMLSelectElement)) return;
  const { id, value } = event.target;
  if (id === "settings-language") {
    changeLanguage(value);
    element(id).focus();
  }
  if (id === "settings-density") {
    density = value;
    document.body.dataset.density = value;
    savePreference("density", value);
  }
  if (id === "settings-format" || id === "next-format") {
    format = value;
    savePreference("format", value);
  }
});
main.addEventListener("dragover", (event) => {
  if (!event.dataTransfer?.types.includes("Files")) return;
  event.preventDefault();
  if (event.target instanceof Element)
    event.target.closest(".composer")?.classList.add("document-dragover");
});
main.addEventListener("dragleave", (event) => {
  if (event.target instanceof Element)
    event.target.closest(".composer")?.classList.remove("document-dragover");
});
document.addEventListener("drop", (event) => {
  if (!event.dataTransfer?.types.includes("Files")) return;
  event.preventDefault();
  document
    .querySelector(".document-dragover")
    ?.classList.remove("document-dragover");
  if (event.target instanceof Element && event.target.closest(".composer"))
    void attachFiles(Array.from(event.dataTransfer.files));
});
languageSelect.addEventListener("change", () =>
  changeLanguage(languageSelect.value),
);
search.addEventListener("input", renderCommands);
window.addEventListener("hashchange", () => {
  cancelGeneration();
  render(true);
});
palette.addEventListener("close", () => {
  if (paletteOpener?.isConnected) paletteOpener.focus();
  paletteOpener = null;
});
document.addEventListener("keydown", (event) => {
  if (
    event.ctrlKey &&
    event.key === "Enter" &&
    document.activeElement?.id === "request-input"
  ) {
    event.preventDefault();
    void generate();
  }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
    event.preventDefault();
    if (palette.open) palette.close();
    else openPalette();
  }
  if (!palette.open) return;
  const buttons = Array.from(palette.querySelectorAll("button.command-result"));
  const current = buttons.findIndex(
    (button) => button === document.activeElement,
  );
  if (event.key === "ArrowDown" || event.key === "ArrowUp") {
    event.preventDefault();
    const index =
      event.key === "ArrowDown"
        ? (current + 1) % buttons.length
        : current <= 0
          ? buttons.length - 1
          : current - 1;
    const next = buttons[index];
    if (next instanceof HTMLElement) next.focus();
  }
  if (
    event.key === "Enter" &&
    document.activeElement === search &&
    buttons[0] instanceof HTMLElement
  ) {
    event.preventDefault();
    buttons[0].click();
  }
});
render();
void checkEngine();
