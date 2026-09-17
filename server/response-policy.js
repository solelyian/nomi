import { readFileSync } from "node:fs";

/** @type {import("../preview/types").ResponsePolicyConfig} */
const policy = JSON.parse(
  readFileSync(
    new URL("../content/response-policy.json", import.meta.url),
    "utf8",
  ),
);
export const responseLimit = policy.maxCharacters;
export const regenerationInstruction = policy.regenerationInstruction;

/** @param {string} text */
function numbers(text) {
  return text.match(new RegExp(policy.numbersPattern, "gi")) ?? [];
}

/** @param {string} amount */
function currencyAmount(amount) {
  const [integer, fraction = ""] = amount
    .replace(/[ \u00a0\u202f]/gu, "")
    .replace("−", "-")
    .replace(",", ".")
    .split(".");
  return BigInt(integer + fraction.padEnd(2, "0"));
}

/** @param {string} text */
function hasConflictingResult(text) {
  const rules = policy.resultConsistency;
  const ending = "[ \\t]*[.!]?$";
  /** @type {{ unit: string, amount: bigint } | undefined} */
  let heading;
  for (const quantity of rules.quantities) {
    const match = new RegExp(
      `^${rules.headingPrefix}${quantity}${ending}`,
      "iu",
    ).exec(text.split("\n", 1)[0]);
    if (match?.groups) {
      heading = {
        unit: match.groups.unit,
        amount: currencyAmount(match.groups.amount),
      };
      break;
    }
  }
  if (!heading) return false;
  for (const quantity of rules.quantities) {
    const pattern = new RegExp(
      `^${rules.conclusionPrefix}${quantity}${ending}`,
      "gimu",
    );
    for (const match of text.matchAll(pattern)) {
      if (
        match.groups?.unit === heading.unit &&
        currencyAmount(match.groups.amount) !== heading.amount
      )
        return true;
    }
  }
  return false;
}

/** @param {string} source @param {string} format @returns {import("../preview/types").PolicyResult} */
export function adaptResponse(source, format) {
  /** @type {string[]} */
  const changes = [];
  /** @param {string[]} reasons */
  const blocked = (reasons) => ({
    text: "",
    version: policy.version,
    changes: [],
    reasons,
    accepted: false,
  });
  if (source.length > policy.maxCharacters) return blocked(["length"]);
  if (new RegExp(policy.forbiddenCharacters, "u").test(source))
    return blocked(["hidden-characters"]);
  let text = source;
  for (const rule of policy.normalizations) {
    const next = text.replace(
      new RegExp(rule.pattern, "gimu"),
      rule.replacement,
    );
    if (next !== text && !changes.includes(rule.id)) changes.push(rule.id);
    text = next;
  }
  const trimmed = text.trim();
  if (text !== trimmed && !changes.includes("layout")) changes.push("layout");
  text = trimmed;
  if (!text) return blocked(["empty-response"]);
  const reasons = policy.blockedPatterns
    .filter((rule) =>
      new RegExp(rule.pattern, "imu").test(text.normalize("NFC")),
    )
    .map((rule) => rule.id);
  if (reasons.length) return blocked(reasons);
  if (hasConflictingResult(text.normalize("NFC")))
    return blocked(["conflicting-result"]);
  if (format === "steps") {
    const spaced = text.split(/\n+/u).join("\n\n");
    if (spaced !== text) changes.push("reading-spacing");
    text = spaced;
  }
  if (JSON.stringify(numbers(source)) !== JSON.stringify(numbers(text)))
    return blocked(["numbers-changed"]);
  return {
    text,
    version: policy.version,
    changes,
    reasons: [],
    accepted: true,
  };
}
