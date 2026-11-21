// Services du prototype — séparation nette UI / métier / IA / données.
// IInferenceService et ISpeechService sont SIMULÉS : remplacer par des
// implémentations réelles (inférence locale ou API) sans toucher aux vues.
// CalcService est RÉEL : évaluateur arithmétique déterministe, aucune IA.

export interface Diagnostic { severity: 'erreur' | 'avertissement' | 'info'; line: number; message: string; suggestion: string; }
export interface CodeReview { fileName: string; diagnostics: Diagnostic[]; diffBefore: string; diffAfter: string; explanation: string; }
export interface TranscriptChunk { startSec: number; endSec: number; speaker: string; text: string; uncertain: boolean; }
export interface SheetSection { title: string; body: string; provenance: 'cours' | 'nomi' | 'hypothese'; }

export interface IInferenceService {
  reviewCode(source: string): Promise<CodeReview>;
  streamLiveNotes(chunk: TranscriptChunk): AsyncGenerator<string>;
  buildRevisionSheet(transcript: string): Promise<{ title: string; sections: SheetSection[] }>;
}
export interface ISpeechService {
  streamTranscript(): AsyncGenerator<TranscriptChunk>;
}

const wait = (ms: number) => new Promise(r => setTimeout(r, ms));

export const simulatedInference: IInferenceService = {
  async reviewCode() { await wait(900); return demoReview; },
  async *streamLiveNotes(chunk) {
    for (const n of liveNotesFor(chunk)) { await wait(300); yield n; }
  },
  async buildRevisionSheet() { await wait(1100); return demoSheet; },
};

export const simulatedSpeech: ISpeechService = {
  async *streamTranscript() {
    for (const c of demoTranscript) { await wait(1200); yield c; }
  },
};

// ------------------- Moteur de calcul RÉEL -------------------
// Évalue + - * / % (postfixe), parenthèses, espaces milliers, virgule décimale.

export function evalExpr(expr: string): number {
  const toks: string[] = [];
  let i = 0;
  while (i < expr.length) {
    const c = expr[i];
    if (/\s|€|\$/.test(c)) { i++; continue; }
    if (/[\d,.]/.test(c)) {
      let raw = '';
      while (i < expr.length && /[\d,.\s]/.test(expr[i])) raw += expr[i++];
      toks.push('n' + normalizeNumber(raw.trim())); continue;
    }
    if ('+-*/%()'.includes(c)) { toks.push(c); i++; continue; }
    throw new Error(`Caractère inattendu « ${c} »`);
  }
  let pos = 0;
  const peek = () => toks[pos];
  const factor = (): number => {
    let v: number;
    if (peek() === '-') { pos++; v = -factor(); }
    else if (peek() === '(') { pos++; v = expr_(); if (peek() !== ')') throw new Error('Parenthèse non fermée'); pos++; }
    else if (peek()?.startsWith('n')) v = parseFloat(toks[pos++].slice(1));
    else throw new Error('Expression incomplète');
    while (peek() === '%') { pos++; v /= 100; }
    return v;
  };
  const term = (): number => {
    let v = factor();
    while (peek() === '*' || peek() === '/') { const op = toks[pos++]; const r = factor(); v = op === '*' ? v * r : v / r; }
    return v;
  };
  const expr_ = (): number => {
    let v = term();
    while (peek() === '+' || peek() === '-') { const op = toks[pos++]; const r = term(); v = op === '+' ? v + r : v - r; }
    return v;
  };
  const v = expr_();
  if (pos !== toks.length) throw new Error(`Reste non évalué : ${toks[pos]}`);
  return v;
}

function normalizeNumber(raw: string): string {
  raw = raw.replace(/[\s ]/g, '');
  const commas = (raw.match(/,/g) || []).length;
  const dots = (raw.match(/\./g) || []).length;
  if (commas === 1 && dots === 0) {
    const dec = raw.length - raw.indexOf(',') - 1;
    const head = raw.slice(0, raw.indexOf(','));
    if (dec === 3 && head.length <= 3) return raw.replace(/,/g, '');
    return raw.replace(',', '.');
  }
  if (commas >= 1 && dots >= 1) return raw.replace(/,/g, '');
  return raw;
}

export const fmt = (v: number) =>
  Number.isNaN(v) ? '—' : v.toLocaleString('fr-FR', { maximumFractionDigits: 2 });

// ------------------- Données de démonstration -------------------

export const demoSource = `// facture.ts — module de facturation (extrait)
export function totaux(lignes: Ligne[]) {
  let ht = 0;
  for (const l of lignes) {
    ht += l.prix * l.qte;        // l.5 : pas d'arrondi par ligne
  }
  const tva = ht * 0.2;          // l.9 : taux codé en dur
  return { ht, tva, ttc: ht + tva };
}`;

export const demoReview: CodeReview = {
  fileName: 'facture.ts',
  diagnostics: [
    { severity: 'erreur', line: 9, message: 'Taux de TVA codé en dur (0.2). Un changement de taux exige de modifier le code.', suggestion: 'Extraire en constante TAUX_TVA ou paramètre.' },
    { severity: 'avertissement', line: 5, message: 'Aucun arrondi par ligne : la somme des centimes peut diverger du total affiché.', suggestion: 'Arrondir chaque ligne à 2 décimales avant d\'additionner.' },
    { severity: 'info', line: 1, message: 'totaux([]) renvoie 0/0/0 sans erreur — vérifier que c\'est voulu.', suggestion: 'Documenter ou lever une erreur explicite.' },
  ],
  diffBefore: `for (const l of lignes) {
  ht += l.prix * l.qte;
}
const tva = ht * 0.2;`,
  diffAfter: `const TAUX_TVA = 0.2;
for (const l of lignes) {
  ht += Math.round(l.prix * l.qte * 100) / 100;
}
const tva = ht * TAUX_TVA;`,
  explanation: 'Deux corrections proposées : taux extrait en constante nommée, arrondi par ligne. Nomi n\'a rien modifié — à vous d\'appliquer ou d\'écarter.',
};

export const calcRows: { label: string; expr: string; lu: number }[] = [
  { label: 'Sous-total HT', expr: '8 900 + 4 200 + 1 532', lu: 14900 }, // écart : 14 632 recalculé
  { label: 'TVA 20 %', expr: '14 900 * 20%', lu: 2980 },
  { label: 'Total TTC', expr: '14 900 + 2 980', lu: 17880 },
  { label: 'Remise 5 %', expr: '17 880 * 5%', lu: 894 },
  { label: 'Net à payer', expr: '17 880 - 894', lu: 16986 },
];

export const demoTranscript: TranscriptChunk[] = [
  { startSec: 0, endSec: 14, speaker: 'Prof', text: 'Reprenons le théorème des accroissements finis. Qui se souvient des hypothèses ?', uncertain: false },
  { startSec: 14, endSec: 31, speaker: 'Locuteur 2', text: 'f continue sur [a,b], dérivable sur ]a,b[.', uncertain: false },
  { startSec: 31, endSec: 52, speaker: 'Prof', text: 'Exact. Il existe alors c dans ]a,b[ tel que f(b) − f(a) = f′(c)·(b − a). Retenez la forme : pente de la corde = pente de la tangente.', uncertain: false },
  { startSec: 52, endSec: 78, speaker: 'Prof', text: 'Exemple : f(x) = x² sur [0, 2]. La corde a pour pente 2 ; on cherche c avec f′(c) = 2, donc 2c = 2, c = 1.', uncertain: false },
  { startSec: 78, endSec: 101, speaker: 'Locuteur 2', text: 'Et si la fonction n\'est pas dérivable en un point ?', uncertain: false },
  { startSec: 101, endSec: 128, speaker: 'Prof', text: 'Le théorème ne s\'applique plus — pensez à |x| en 0. C\'est un contrexemple classique à l\'examen.', uncertain: false },
  { startSec: 128, endSec: 149, speaker: 'Prof', text: 'Pour jeudi : exercices 4 et 5 de la feuille 7, et relisez la preuve par le [inaudible] de Rolle.', uncertain: true },
];

function liveNotesFor(c: TranscriptChunk): string[] {
  const out: string[] = [];
  if (c.text.includes('accroissements')) out.push('📌 TAF : continuité [a,b] + dérivabilité ]a,b[.');
  if (c.text.includes('pente')) out.push('📐 Interprétation géométrique : tangente ∥ corde.');
  if (c.text.includes('x²')) out.push('✏️ Exemple corrigé : c = 1 pour x² sur [0,2].');
  if (c.text.includes('dérivable en un point')) out.push('❓ Bonne question : |x| contrexemple à retenir.');
  if (c.text.includes('contrexemple')) out.push('⚠️ Examen : |x| en 0 casse les hypothèses.');
  if (c.text.includes('exercices')) out.push('🗓️ À faire : ex. 4–5 feuille 7 + preuve de Rolle.');
  return out;
}

export const demoSheet = {
  title: 'IPP — Insuffisance pondérale à la naissance',
  sections: [
    { title: 'Définition', body: 'Poids de naissance inférieur au 10ᵉ percentile pour l\'âge gestationnel. À distinguer du RCIU (retard de croissance in utero), qui est le processus, pas le constat.', provenance: 'cours' as const },
    { title: 'Causes maternelles', body: 'HTA, tabac, dénutrition, infection (TORCH). Le tabac est le facteur évitable n°1 cité en cours.', provenance: 'cours' as const },
    { title: 'Causes fœtales et placentaires', body: 'Anomalies chromosomiques, infections congénitales ; insuffisance placentaire = mécanisme le plus fréquent.', provenance: 'cours' as const },
    { title: 'Dépistage', body: 'Mesure de la hauteur utérine à chaque consultation puis échographie de croissance (3ᵉ trimestre). Doppler ombilical si suspicion.', provenance: 'cours' as const },
    { title: 'Conduite', body: 'Surveillance rapprochée ; le moment de l\'accouchement arbitre entre mauvaise croissance in utero et prématurité induite.', provenance: 'nomi' as const },
    { title: 'Point de vigilance', body: '« IPP ≠ prématurité » — un IPP peut être à terme. Le seuil exact (10ᵉ vs 5ᵉ percentile selon les courbes) reste à confirmer avec votre enseignant.', provenance: 'hypothese' as const },
  ],
};

export const library = [
  { kind: 'Transcription', title: 'Analyse — séance 8', meta: '42:17 · aujourd\'hui', nav: 'transcriptions' },
  { kind: 'Fiche', title: 'IPP — insuffisance pondérale', meta: '6 sections · hier', nav: 'fiches' },
  { kind: 'Cours', title: 'Anatomie — IPP', meta: 'transcription 38 min', nav: 'cours' },
  { kind: 'Transcription', title: 'TD Statistiques — tests', meta: '31:40 · mardi', nav: 'transcriptions' },
  { kind: 'Fiche', title: 'Théorèmes d\'analyse', meta: '8 sections · semaine dernière', nav: 'fiches' },
  { kind: 'Cours', title: 'Physiologie — néphron', meta: 'notes + audio', nav: 'cours' },
];
