using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nomi.Prototype.WinUI.Services;

/// <summary>
/// Chaînes FR/EN de l'interface (chrome uniquement — les données de démo
/// restent en français, signalé comme limite du prototype).
/// </summary>
public static class Loc
{
    public static bool English;

    private static readonly Dictionary<string, (string Fr, string En)> S = new()
    {
        ["accueil"] = ("Accueil", "Home"),
        ["journee"] = ("Journée", "Day"),
        ["aujourdhui"] = ("Aujourd'hui", "Today"),
        ["tableau"] = ("Tableau", "Board"),
        ["routines"] = ("Routines", "Routines"),
        ["studio"] = ("Studio", "Studio"),
        ["code"] = ("Code", "Code"),
        ["focus"] = ("Focus", "Focus"),
        ["etudes"] = ("Études", "Studies"),
        ["cours"] = ("Cours", "Courses"),
        ["transcriptions"] = ("Transcriptions", "Transcripts"),
        ["fiches"] = ("Fiches", "Sheets"),
        ["bibliotheque"] = ("Bibliothèque", "Library"),
        ["assistant"] = ("Assistant", "Assistant"),
        ["parametres"] = ("Paramètres", "Settings"),
        ["demo"] = ("maquette — données de démonstration", "mockup — demo data"),
        ["analyser"] = ("Analyser avec Nomi", "Analyze with Nomi"),
        ["verifier"] = ("Vérifier les calculs", "Check calculations"),
        ["demarrer"] = ("Démarrer la transcription", "Start transcription"),
        ["generer"] = ("Générer la fiche", "Generate sheet"),
        ["simulation"] = ("IA simulée", "Simulated AI"),
        ["reel"] = ("réel", "real"),
    };

    public static string T(string key) =>
        S.TryGetValue(key, out var v) ? (English ? v.En : v.Fr) : key;
}
