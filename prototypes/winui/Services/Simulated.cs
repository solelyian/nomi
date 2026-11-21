using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nomi.Prototype.WinUI.Services;

/// <summary>
/// IA SIMULÉE — rejoue les données de démonstration de <see cref="DemoData"/>.
/// Ne contient aucun modèle, aucun appel réseau, aucune API key.
/// Pour du réel : implémenter <see cref="IInferenceService"/> avec LLamaSharp
/// (Qwen3 8B / Gemma 4, comme Nomi.WinUI) et permuter dans App.Services.
/// </summary>
public sealed class SimulatedInference : IInferenceService
{
    public async Task<CodeReview> ReviewCodeAsync(string source, CancellationToken ct = default)
    {
        await Task.Delay(900, ct); // latence simulée, affichée dans l'UI
        return DemoData.InvoiceReview;
    }

    public async IAsyncEnumerable<string> StreamLiveNotesAsync(
        TranscriptChunk chunk,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var note in DemoData.LiveNotesFor(chunk))
        {
            await Task.Delay(350, ct);
            yield return note;
        }
    }

    public async Task<RevisionSheet> BuildRevisionSheetAsync(string transcriptText, CancellationToken ct = default)
    {
        await Task.Delay(1200, ct);
        return DemoData.IppSheet;
    }
}

/// <summary>
/// Point d'injection des services du prototype.
/// Pour brancher l'IA réelle : remplacer <c>Inference</c>/<c>Speech</c> ici —
/// aucune vue n'a besoin de changer.
/// </summary>
public static class PrototypeServices
{
    public static IInferenceService Inference { get; set; } = new SimulatedInference();
    public static ISpeechService Speech { get; set; } = new SimulatedSpeech();
}

/// <summary>
/// Parole SIMULÉE — rejoue le cours « Analyse, séance 8 » à vitesse réduite.
/// Réel : micro + Whisper.net (modèle small ~488 Mo, voir README racine).
/// </summary>
public sealed class SimulatedSpeech : ISpeechService
{
    public async IAsyncEnumerable<TranscriptChunk> StreamTranscriptAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var chunk in DemoData.AnalyseS8)
        {
            await Task.Delay(1400, ct);
            yield return chunk;
        }
    }
}
