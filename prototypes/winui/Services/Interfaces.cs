using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nomi.Prototype.WinUI.Services;

/// <summary>
/// Contrat d'inférence IA du prototype. L'implémentation actuelle est
/// <see cref="SimulatedInference"/> — la remplacer par une implémentation
/// réelle (LLamaSharp, comme dans Nomi.WinUI) suffit à rendre le prototype « vivant ».
/// </summary>
public interface IInferenceService
{
    /// <summary>Analyse un extrait de code et produit des diagnostics + un diff suggéré.</summary>
    Task<CodeReview> ReviewCodeAsync(string source, CancellationToken ct = default);

    /// <summary>Produit les notes « en direct » d'une transcription (points clés, définitions, questions).</summary>
    IAsyncEnumerable<string> StreamLiveNotesAsync(TranscriptChunk chunk, CancellationToken ct = default);

    /// <summary>Transforme une transcription en sections de fiche de révision.</summary>
    Task<RevisionSheet> BuildRevisionSheetAsync(string transcriptText, CancellationToken ct = default);
}

/// <summary>
/// Contrat de capture/transcription audio. Actuellement
/// <see cref="SimulatedSpeech"/> rejoue un script ; une implémentation réelle
/// brancherait le micro + Whisper (Whisper.net, cf. Nomi.WinUI).
/// </summary>
public interface ISpeechService
{
    /// <summary>Émet les segments de transcription au fil du temps (simulation temps réel).</summary>
    IAsyncEnumerable<TranscriptChunk> StreamTranscriptAsync(CancellationToken ct = default);
}

// ------------------- Modèles -------------------

public sealed record Diagnostic(string Severity, int Line, string Message, string Suggestion);

public sealed record CodeReview(
    string FileName,
    IReadOnlyList<Diagnostic> Diagnostics,
    string DiffBefore,
    string DiffAfter,
    string Explanation);

public sealed record TranscriptChunk(
    int StartSec, int EndSec, string Speaker, string Text, bool Uncertain);

public sealed record SheetSection(
    string Title,
    string Body,
    string Provenance /* "cours" | "nomi" | "hypothese" */);

public sealed record RevisionSheet(
    string Title,
    IReadOnlyList<SheetSection> Sections);

public sealed record CalcLine(
    string Label, string Expression, double Expected,
    double Computed, bool Match);
