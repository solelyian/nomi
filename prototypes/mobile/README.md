# Prototype Nomi — React Native (refonte v4)

Maquette interactive mobile de la refonte « verre chaud » (design system v4,
voir `docs/refonte/dossier.md`). Projet **Expo** — tourne dans le navigateur,
sur téléphone via Expo Go, ou en build Android.

## Lancer

Prérequis : Node 20+ (`node -v`). Pas d'Android Studio requis pour le mode web/Expo Go.

```bash
cd prototypes/mobile
npm install
npx expo start
```

Puis :
- **`w`** → l'app s'ouvre dans le navigateur (le plus simple sur PC) ;
- scanner le QR code avec **Expo Go** (Android/iOS) pour un vrai rendu mobile ;
- `a` → émulateur Android (nécessite Android Studio).

## Ce qui est réel / simulé / non implémenté

| Élément | Statut |
|---|---|
| Navigation en espaces, thème clair/sombre | **Réel** |
| Vérification de calculs (`evalExpr` dans `src/services.ts`) | **Réel** — déterministe |
| Relecture de code (diagnostics + diff « appliquer/écarter ») | **Simulé** — `simulatedInference` |
| Transcription en direct + notes | **Simulé** — `simulatedSpeech` rejoue un script ; **pas de micro** (question ouverte §10 du dossier) |
| Fiche de révision + badges de provenance | **Simulé** |
| Aujourd'hui, Tableau, Routines, Cours, Focus, Assistant | **Non implémenté** — placeholders |
| Langue FR/EN | **Non implémenté** sur mobile (présent dans le prototype WinUI) |

L'IA est derrière `IInferenceService` / `ISpeechService` (`src/services.ts`) :
remplacer `simulatedInference` / `simulatedSpeech` par des implémentations
réelles sans toucher aux écrans.

## Limites

- Aucune permission demandée, aucun réseau, aucune donnée réelle, aucun coût d'API.
- Pas de build `.apk`/`.exe` fourni : il exige EAS Build (cloud Expo) ou Android Studio.
