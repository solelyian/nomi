/** @param {string} value */
export function normalize(value) {
  return value
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase()
    .trim();
}

/** @param {import("./types").Catalog} catalog */
export function commandsFor(catalog) {
  /** @type {import("./types").Command[]} */
  const commands = catalog.actions.map((action) => ({
    ...action,
    kind: "action",
    route: `action/${action.id}`,
  }));
  return commands.concat(
    catalog.contexts.map((context) => ({
      ...context,
      kind: "context",
      route: `action/${context.action}/${context.id}`,
    })),
  );
}

/**
 * @param {import("./types").Catalog} catalog
 * @param {string} query
 */
export function filterCommands(catalog, query) {
  const terms = normalize(query).split(/\s+/).filter(Boolean);
  return commandsFor(catalog).filter((command) => {
    const text = normalize(`${command.title} ${command.description}`);
    return terms.every((term) => text.includes(term));
  });
}

/**
 * @param {string} hash
 * @param {import("./types").Catalog} catalog
 */
export function resolveRoute(hash, catalog) {
  const [page, id, contextId] = hash.replace(/^#/, "").split("/");
  const action = catalog.actions.find((item) => item.id === id);
  if (page === "action" && action) {
    return {
      page,
      action,
      context: catalog.contexts.find(
        (item) => item.id === contextId && item.action === id,
      ),
    };
  }
  return {
    page: page === "spaces" || page === "preferences" ? page : "home",
    action: undefined,
    context: undefined,
  };
}

/**
 * @param {string} hash
 * @param {import("./types").Catalog} catalog
 * @param {string} intent
 */
export function workspaceSelection(hash, catalog, intent) {
  const route = resolveRoute(hash, catalog);
  return {
    action:
      route.action ??
      (route.page === "home"
        ? catalog.actions.find((item) => item.id === intent)
        : undefined),
    draftHash: route.page === "home" ? "#home" : hash,
  };
}

/**
 * @param {import("./types").Action} action
 * @param {string} format
 */
export function resultText(action, format) {
  return format === "summary" ? action.summary : action.steps.join("\n\n");
}
