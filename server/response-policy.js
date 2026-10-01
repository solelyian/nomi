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
export const retriedReasons = ["conflicting-result", "wrong-arithmetic"];

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

/** @param {string} value @param {boolean} grouped */
function decimalValue(value, grouped) {
  return Number(
    grouped && /^[0-9]{1,3}[.,][0-9]{3}$/u.test(value)
      ? value.replace(/[.,]/u, "")
      : value.replace(",", "."),
  );
}

/** @param {string} value @param {boolean} grouped */
function decimalPlaces(value, grouped) {
  if (grouped && /^[0-9]{1,3}[.,][0-9]{3}$/u.test(value)) return 0;
  return /[.,]([0-9]+)$/u.exec(value)?.[1].length ?? 0;
}

/** @param {string} left @param {string} right @param {boolean} grouped */
function holds(left, right, grouped) {
  /** @type {{ value: number, percent: boolean }[]} */
  const terms = [];
  /** @type {string[]} */
  const operators = [];
  const token = new RegExp(policy.arithmetic.token, "gu");
  for (const match of left.matchAll(token)) {
    if (match.groups?.operator) operators.push(match.groups.operator);
    else if (match.groups?.number)
      terms.push({
        value: decimalValue(match.groups.number, grouped),
        percent: match.groups.unit.includes("%"),
      });
  }
  const result = [...right.matchAll(token)].find(
    (match) => match.groups?.number,
  );
  if (!result?.groups || terms.length !== operators.length + 1) return true;
  const negative = /^[ \t]*[-−]/u.test(right);
  const additive = operators.some((operator) => /[+\-−]/u.test(operator));
  const resultPercent = result.groups.unit.includes("%");
  const percents =
    terms.filter((term) => term.percent).length + (resultPercent ? 1 : 0);
  if (percents > 0 && additive && percents < terms.length + 1) return true;
  const scale = (/** @type {boolean} */ percent) => (percent ? 0.01 : 1);
  let sum = 0;
  let sign = 1;
  let product = terms[0].value * scale(terms[0].percent);
  for (let index = 0; index < operators.length; index++) {
    const operator = operators[index];
    const value = terms[index + 1].value * scale(terms[index + 1].percent);
    if (/[+\-−]/u.test(operator)) {
      sum += sign * product;
      sign = operator === "+" ? 1 : -1;
      product = value;
    } else if (operator === "/" || operator === "÷") {
      if (value === 0) return true;
      product /= value;
    } else product *= value;
  }
  const expected = sum + sign * product;
  const shown =
    (negative ? -1 : 1) *
    decimalValue(result.groups.number, grouped) *
    scale(resultPercent);
  const places =
    decimalPlaces(result.groups.number, grouped) + (resultPercent ? 2 : 0);
  return (
    Math.abs(expected - shown) <=
    0.5 * 10 ** -places + 1e-9 * Math.max(1, Math.abs(expected))
  );
}

/** @param {string} text */
function hasWrongArithmetic(text) {
  for (const match of text.matchAll(
    new RegExp(policy.arithmetic.expression, "gu"),
  )) {
    const { left, right } = /** @type {{ left: string, right: string }} */ (
      match.groups
    );
    if (!holds(left, right, false) && !holds(left, right, true)) return true;
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
  if (hasWrongArithmetic(text.normalize("NFC")))
    return blocked(["wrong-arithmetic"]);
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
