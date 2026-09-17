import { DOMParser } from "@xmldom/xmldom";
import { unzipSync } from "fflate";
import ExcelJS from "exceljs";
import { getDocument } from "pdfjs-dist/legacy/build/pdf.mjs";
import { documentKind, documentLimits } from "../preview/documents.js";

const officeLimit = 32_000_000;
const officeNamespaces = {
  word: "http://schemas.openxmlformats.org/wordprocessingml/2006/main",
  drawing: "http://schemas.openxmlformats.org/drawingml/2006/main",
  presentation: "http://schemas.openxmlformats.org/presentationml/2006/main",
  relationship:
    "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
};

class Extract {
  text = "";
  truncated = false;
  /** @param {string} text */
  add(text) {
    if (!text.trim()) return;
    const remaining = documentLimits.characters - this.text.length;
    if (text.length > remaining) this.truncated = true;
    this.text += text.slice(0, Math.max(0, remaining));
    if (this.text.endsWith("\r") || /[\uD800-\uDBFF]$/.test(this.text))
      this.text = this.text.slice(0, -1);
  }
}

/** @param {Uint8Array} bytes */
function officeEntries(bytes) {
  let size = 0;
  let count = 0;
  return unzipSync(bytes, {
    filter(entry) {
      size += entry.originalSize;
      if (++count > 2000 || size > officeLimit)
        throw new Error("document-complex");
      return entry.name.endsWith(".xml") || entry.name.endsWith(".rels");
    },
  });
}

/** @param {Uint8Array} bytes */
function xml(bytes) {
  const text = new TextDecoder("utf-8", { fatal: true }).decode(bytes);
  if (/<!DOCTYPE|<!ENTITY/i.test(text)) throw new Error("document-unreadable");
  return new DOMParser({
    onError: () => {
      throw new Error("document-unreadable");
    },
  }).parseFromString(text, "application/xml");
}

/** @param {import("@xmldom/xmldom").Element} element @param {string} ns */
function officeText(element, ns) {
  const text = [];
  for (const node of Array.from(element.getElementsByTagNameNS(ns, "*"))) {
    if (node.localName === "t") text.push(node.textContent ?? "");
    if (node.localName === "tab") text.push("\t");
    if (node.localName === "br") text.push("\n");
  }
  return text.join("");
}

/** @param {Record<string, Uint8Array>} entries @param {Extract} out */
function word(entries, out) {
  const part = entries["word/document.xml"];
  if (!part) throw new Error("document-unreadable");
  for (const paragraph of Array.from(
    xml(part).getElementsByTagNameNS(officeNamespaces.word, "p"),
  ))
    out.add(officeText(paragraph, officeNamespaces.word) + "\n");
}

/** @param {Record<string, Uint8Array>} entries @param {Extract} out */
function slides(entries, out) {
  const presentation = entries["ppt/presentation.xml"];
  const relationships = entries["ppt/_rels/presentation.xml.rels"];
  if (!presentation || !relationships) throw new Error("document-unreadable");
  const targets = new Map(
    Array.from(xml(relationships).getElementsByTagName("Relationship"))
      .filter((rel) => rel.getAttribute("TargetMode") !== "External")
      .map((rel) => [rel.getAttribute("Id"), rel.getAttribute("Target")]),
  );
  const ids = Array.from(
    xml(presentation).getElementsByTagNameNS(
      officeNamespaces.presentation,
      "sldId",
    ),
  );
  for (const [index, slide] of ids.entries()) {
    const target = targets.get(
      slide.getAttributeNS(officeNamespaces.relationship, "id"),
    );
    const path = target?.startsWith("/") ? target.slice(1) : `ppt/${target}`;
    if (!entries[path]) throw new Error("document-unreadable");
    const paragraphs = Array.from(
      xml(entries[path]).getElementsByTagNameNS(officeNamespaces.drawing, "p"),
    );
    const text = paragraphs
      .map((p) => officeText(p, officeNamespaces.drawing))
      .filter((p) => p.trim())
      .join("\n");
    if (text) out.add(`[Slide ${index + 1}]\n${text}\n\n`);
  }
}

/** @param {ExcelJS.CellValue} value @returns {string} */
function cellText(value) {
  if (value === null || value === undefined) return "";
  if (value instanceof Date)
    return value.toISOString().replace(/\.000Z$|Z$/, "");
  if (typeof value !== "object") return String(value);
  if ("formula" in value || "sharedFormula" in value)
    return value.result === undefined
      ? "[no cached result]"
      : cellText(value.result);
  if ("richText" in value)
    return value.richText.map((run) => run.text).join("");
  if ("error" in value) return value.error;
  return value.text;
}

/** @param {Uint8Array} bytes @param {Extract} out */
async function spreadsheet(bytes, out) {
  const book = new ExcelJS.Workbook();
  await book.xlsx.load(new Uint8Array(bytes).buffer);
  for (const sheet of book.worksheets) {
    const rows = new Extract();
    sheet.eachRow((row) => {
      if (row.number > 10_000) {
        rows.truncated = true;
        return;
      }
      /** @type {string[]} */
      const cells = [];
      row.eachCell((cell) => {
        if (Number(cell.col) > 256) {
          rows.truncated = true;
          return;
        }
        const value = cellText(
          cell.type === ExcelJS.ValueType.Formula
            ? (cell.result ?? "[no cached result]")
            : cell.value,
        );
        if (value)
          cells.push(
            `${cell.address}: ${value}${cell.numFmt && cell.numFmt !== "General" ? ` [format: ${cell.numFmt}]` : ""}`,
          );
      });
      rows.add(cells.join("\t") + "\n");
    });
    if (rows.text.trim()) out.add(`[Sheet: ${sheet.name}]\n${rows.text}\n`);
    out.truncated ||= rows.truncated;
  }
}

/** @param {Uint8Array} bytes @param {Extract} out */
async function pdf(bytes, out) {
  if (!new TextDecoder().decode(bytes.slice(0, 8)).startsWith("%PDF-"))
    throw new Error("document-unreadable");
  const task = getDocument({
    data: new Uint8Array(bytes),
    isEvalSupported: false,
    useSystemFonts: false,
    disableFontFace: true,
    verbosity: 0,
  });
  try {
    const document = await task.promise;
    out.truncated = document.numPages > 40;
    for (let page = 1; page <= Math.min(document.numPages, 40); page++) {
      const contents = await (await document.getPage(page)).getTextContent();
      const text = contents.items
        .map((item) =>
          "str" in item ? item.str + (item.hasEOL ? "\n" : " ") : "",
        )
        .join("")
        .trim();
      if (text) out.add(`[Page ${page}]\n${text}\n\n`);
      else out.truncated = true;
      if (out.text.length >= documentLimits.characters) {
        out.truncated ||= page < document.numPages;
        break;
      }
    }
  } finally {
    await task.destroy();
  }
}

/**
 * @param {string} name @param {Uint8Array} bytes
 * @returns {Promise<import("../preview/types").AttachedDocument>}
 */
export async function extractDocument(name, bytes) {
  const kind = documentKind(name);
  if (!kind) throw new Error("document-unsupported");
  if (!bytes.length) throw new Error("document-unreadable");
  if (bytes.length > documentLimits.bytes) throw new Error("document-size");
  const out = new Extract();
  try {
    if (kind === ".pdf") await pdf(bytes, out);
    else if ([".docx", ".xlsx", ".pptx"].includes(kind)) {
      const entries = officeEntries(bytes);
      if (!entries["[Content_Types].xml"])
        throw new Error("document-unreadable");
      for (const part of Object.values(entries)) {
        if (/<!DOCTYPE|<!ENTITY/i.test(new TextDecoder().decode(part)))
          throw new Error("document-unreadable");
      }
      if (kind === ".xlsx") await spreadsheet(bytes, out);
      else if (kind === ".docx") word(entries, out);
      else slides(entries, out);
    } else {
      const text = new TextDecoder("utf-8", { fatal: true }).decode(bytes);
      if (/\p{Cc}/u.test(text.replace(/[\t\r\n]/g, "")))
        throw new Error("document-unreadable");
      out.add(text);
    }
  } catch (error) {
    if (error instanceof Error && error.message === "document-complex")
      throw error;
    throw new Error("document-unreadable");
  }
  if (!out.text.trim()) throw new Error("document-no-text");
  return { name, text: out.text.trim(), truncated: out.truncated };
}
