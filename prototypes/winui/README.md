# Prototype Nomi — WinUI 3 (refonte v4)

Maquette interactive de la refonte « verre chaud » (design system v4, voir `docs/refonte/dossier.md`).
Application Windows **non empaquetée et autonome** : un `Nomi.Prototype.WinUI.exe` portable, comme l'app Nomi.

## Ce qui est réel / simulé / non implémenté

| Élément | Statut |
|---|---|
| Navigation en espaces (Journée / Studio / Études), thème clair/sombre, langue FR/EN (chrome) | **Réel** |
| Moteur de vérification de calculs (`CalcService`) | **Réel** — déterministe, aucune IA |
| Relecture de code (diagnostics + diff « appliquer / écarter ») | **Simulé** — `SimulatedInference` rejoue des données de démo |
| Transcription en direct + notes | **Simulé** — `SimulatedSpeech` rejoue un script (pas de micro, pas de Whisper) |
| Fiche de révision + badges de provenance | **Simulé** — `SimulatedInference` |
| Écrans Aujourd'hui, Tableau, Routines, Cours, Focus, Assistant | **Non implémenté** — dessinés dans la maquette HTML, placeholders ici |

L'IA est derrière `IInferenceService` / `ISpeechService` (`Services/Interfaces.cs`). Pour du réel :
implémenter ces interfaces avec LLamaSharp / Whisper.net comme dans `Nomi.WinUI`, et les permuter
dans `Services/Simulated.cs` → `PrototypeServices`. Aucune vue ne change.

## Build et exécution

Prérequis : Windows 10 1809+, .NET SDK 8 (`winget install Microsoft.DotNet.SDK.8`).

```powershell
cd prototypes\winui
dotnet restore Nomi.Prototype.WinUI.csproj
dotnet build Nomi.Prototype.WinUI.csproj -c Release -p:Platform=x64
dotnet publish Nomi.Prototype.WinUI.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained
```

L'exécutable portable est alors dans
`bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\Nomi.Prototype.WinUI.exe` —
double-clic pour lancer, aucun installateur, WindowsAppSDK embarqué.

## Permissions et limites

- Aucune permission système demandée (pas de micro simulé réel, pas de réseau).
- Aucune donnée ne quitte la machine ; les données affichées sont fictives.
- Aucun coût d'IA (aucune API). Le passage à l'IA réelle embarquée resterait 100 % local.
