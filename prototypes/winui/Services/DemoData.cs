using System.Collections.Generic;

namespace Nomi.Prototype.WinUI.Services;

/// <summary>Données de démonstration — aucune donnée réelle, aucune API.</summary>
public static class DemoData
{
    // ---------- Relecture de code (Studio > Code) ----------

    public const string InvoiceSource =
        "// facture.ts — module de facturation (extrait)\n" +
        "export function totaux(lignes: Ligne[]) {\n" +
        "  let ht = 0;\n" +
        "  for (const l of lignes) {\n" +
        "    ht += l.prix * l.qte;        // l.5 : pas d'arrondi par ligne\n" +
        "  }\n" +
        "  const tva = ht * 0.2;          // l.9 : taux codé en dur\n" +
        "  return { ht, tva, ttc: ht + tva };\n" +
        "}";

    public static readonly CodeReview InvoiceReview = new(
        FileName: "facture.ts",
        Diagnostics: new List<Diagnostic>
        {
            new("erreur", 9, "Taux de TVA codé en dur (0.2). Un changement de taux exige de modifier le code.", "Extraire en constante TAUX_TVA ou paramètre."),
            new("avertissement", 5, "Aucun arrondi par ligne : la somme des centimes peut diverger du total affiché.", "Arrondir chaque ligne à 2 décimales avant d'additionner."),
            new("info", 1, "La fonction accepte un tableau vide : totaux([]) renvoie 0/0/0 sans erreur — vérifier que c'est voulu.", "Documenter ou lever une erreur explicite."),
        },
        DiffBefore:
            "  for (const l of lignes) {\n" +
            "    ht += l.prix * l.qte;\n" +
            "  }\n" +
            "  const tva = ht * 0.2;",
        DiffAfter:
            "  const TAUX_TVA = 0.2;\n" +
            "  for (const l of lignes) {\n" +
            "    ht += Math.round(l.prix * l.qte * 100) / 100;\n" +
            "  }\n" +
            "  const tva = ht * TAUX_TVA;",
        Explanation: "Deux corrections proposées : taux extrait en constante nommée, arrondi bancaire par ligne. Nomi n'a rien modifié — à vous d'appliquer ou d'écarter.");

    // ---------- Vérification de calculs (panneau) ----------

    public static readonly IReadOnlyList<(string Label, string Expr, double Expected)> CalcRows =
        new List<(string, string, double)>
        {
            ("Sous-total HT", "8 900 + 4 200 + 1 532", 14900), // écart : 14 632 recalculé
            ("TVA 20 %", "14 900 * 20%", 2980),
            ("Total TTC", "14 900 + 2 980", 17880),
            ("Remise 5 %", "17 880 * 5%", 894),
            ("Net à payer", "17 880 - 894", 16986),
        };

    // ---------- Transcription (Études > Transcriptions) ----------

    public static readonly IReadOnlyList<TranscriptChunk> AnalyseS8 = new List<TranscriptChunk>
    {
        new(0, 14, "Prof", "Reprenons le théorème des accroissements finis. Qui se souvient des hypothèses ?", false),
        new(14, 31, "Locuteur 2", "f continue sur [a,b], dérivable sur ]a,b[.", false),
        new(31, 52, "Prof", "Exact. Il existe alors c dans ]a,b[ tel que f(b) − f(a) = f′(c)·(b − a). Retenez la forme : pente de la corde = pente de la tangente.", false),
        new(52, 78, "Prof", "Exemple : f(x) = x² sur [0, 2]. La corde a pour pente 2 ; on cherche c avec f′(c) = 2, donc 2c = 2, c = 1.", false),
        new(78, 101, "Locuteur 2", "Et si la fonction n'est pas dérivable en un point ?", false),
        new(101, 128, "Prof", "Le théorème ne s'applique plus — pensez à |x| en 0. C'est un contrexemple classique à l'examen.", false),
        new(128, 149, "Prof", "Pour jeudi : exercices 4 et 5 de la feuille 7, et relisez la preuve par le [inaudible] de Rolle.", true),
    };

    /// <summary>Notes « en direct » indexées par l'ordre des segments.</summary>
    public static IEnumerable<string> LiveNotesFor(TranscriptChunk chunk)
    {
        if (chunk.Text.Contains("accroissements")) yield return "📌 TAF : continuité [a,b] + dérivabilité ]a,b[.";
        if (chunk.Text.Contains("pente")) yield return "📐 Interprétation géométrique : tangente ∥ corde.";
        if (chunk.Text.Contains("x²")) yield return "✏️ Exemple corrigé : c = 1 pour x² sur [0,2].";
        if (chunk.Text.Contains("dérivable en un point")) yield return "❓ Bonne question : |x| contrexemple à retenir.";
        if (chunk.Text.Contains("contrexemple")) yield return "⚠️ Examen : |x| en 0 casse les hypothèses.";
        if (chunk.Text.Contains("exercices")) yield return "🗓️ À faire : ex. 4–5 feuille 7 + preuve de Rolle.";
    }

    // ---------- Fiche de révision (Études > Fiches) ----------

    public static readonly RevisionSheet IppSheet = new(
        Title: "IPP — Insuffisance pondérale à la naissance",
        Sections: new List<SheetSection>
        {
            new("Définition", "Poids de naissance inférieur au 10ᵉ percentile pour l'âge gestationnel. À distinguer du RCIU (retard de croissance in utero), qui est le processus, pas le constat.", "cours"),
            new("Causes maternelles", "HTA, tabac, dénutrition, infection (TORCH). Le tabac est le facteur évitable n°1 cité en cours.", "cours"),
            new("Causes fœtales et placentaires", "Anomalies chromosomiques, infections congénitales ; insuffisance placentaire = mécanisme le plus fréquent.", "cours"),
            new("Dépistage", "Mesure de la hauteur utérine à chaque consultation puis échographie de croissance (3ᵉ trimestre). Doppler ombilical si suspicion.", "cours"),
            new("Conduite", "Surveillance rapprochée ; le moment de l'accouchement arbitre entre mauvaise croissance in utero et prématurité induite.", "nomi"),
            new("Point de vigilance", "« IPP ≠ prématurité » — un IPP peut être à terme. La formulation exacte du seuil (10ᵉ vs 5ᵉ percentile selon les courbes) reste à confirmer avec votre enseignant.", "hypothese"),
        });
}
