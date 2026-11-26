# Nomi iOS 0.10.6

Application iPhone/iPad de productivité et d'apprentissage, refondue selon la direction **Nomi Aura** (fond porcelaine ambiant, cartes de verre chaud, navigation flottante iPhone et workspace iPad). L'IA conversationnelle et la génération de fiches utilisent **Gemma 4 en local** lorsque le modèle est installé. La relecture de code est hors périmètre. Le dossier produit/design complet est dans [`docs/dossier-produit-design.md`](docs/dossier-produit-design.md).

## Structure

- **iPhone** : cinq onglets — Aujourd'hui, Tableau, Focus, Nomi, Plus.
- **Plus** : Routines, Transcription, Fiches et révisions, Réglages.
- **iPad** : barre latérale complète avec toutes les sections.

## Gemma 4 local

Nomi intègre le package Swift officiel [`LiteRT-LM`](https://github.com/google-ai-edge/LiteRT-LM). Le modèle recommandé est **Gemma 4 E2B Instruct** (`gemma-4-E2B-it.litertlm`, 2,6 Go), téléchargé à la demande depuis Hugging Face dans Réglages. Le modèle **E4B** plus lourd est aussi proposé.

- le modèle n'est pas inclus dans le dépôt ;
- le téléchargement est le seul usage réseau prévu pour Gemma ;
- après installation, les conversations et fiches sont générées sur appareil ;
- le backend Metal/GPU est tenté d'abord, puis le CPU sert de repli ;
- le premier chargement peut prendre plusieurs secondes et consommer environ 2 Go de mémoire sur simulateur ;
- le moteur est déchargé lorsque l'app passe en arrière-plan ou quand le modèle est supprimé.

## État réel / simulé / non implémenté

| État | Fonctions |
|---|---|
| **Réel** | compilation SwiftUI, onboarding, navigation iPhone/iPad, tableau de tâches, édition, division et progression, minuteur Focus avec pause/reprise/fin, fils de conversation, vérification locale de calculs simples, réponses Gemma 4 locales après installation, création de fiches depuis texte ou transcription, flashcards/quiz/export, parseur local de tâches et routines, activation/édition des routines, persistance locale JSON protégée |
| **Semi-réel** | microphone + Apple Speech (`SFSpeechRecognizer`) : compile, mais doit être validé sur iPhone physique avec permissions réelles ; Gemma 4 validé sur simulateur, pas encore sur iPhone physique |
| **Repli local non génératif** | fiches extractives si Gemma n'est pas installé ou si la génération JSON échoue ; données d'agenda de démonstration |
| **Non implémenté** | observation des autres apps en Focus, notifications locales, App Intents, Document Picker, extension de partage, SwiftData, iCloud, Live Activity, widgets, Spotlight, diarisation fiable, interface multilingue complète |

## Prérequis

- macOS avec Xcode 26.6 ou version compatible iOS 17+ ;
- XcodeGen 2.48+ si vous régénérez le projet (`brew install xcodegen`) ;
- un simulateur iPhone iOS 17 ou supérieur. Le build vérifié utilisait Xcode 26.6 et iPhone 17 sous iOS 27.0/26.5 selon les runs.

Le projet `Nomi.xcodeproj` est commité pour ouverture directe. `project.yml` permet de le régénérer de façon reproductible. La version marketing est `0.10.6` et le build `106`.

## Ouvrir et exécuter

```bash
cd ios
open Nomi.xcodeproj
```

Dans Xcode : choisir le schéma **Nomi**, un simulateur iPhone ou iPad, puis Run. Sur appareil physique, configurer votre équipe de signature dans Signing & Capabilities.

En ligne de commande :

```bash
cd ios
xcodegen generate # facultatif : régénère Nomi.xcodeproj
xcodebuild -project Nomi.xcodeproj \
  -scheme Nomi \
  -destination 'platform=iOS Simulator,name=iPhone 17' \
  -configuration Debug \
  build CODE_SIGNING_ALLOWED=NO
```

## Tests

```bash
cd ios/Packages/NomiCore
xcodebuild test \
  -scheme NomiCore \
  -destination 'platform=iOS Simulator,name=iPhone 17' \
  CODE_SIGNING_ALLOWED=NO
```

Vérifications effectuées : build iOS réussi, tests unitaires NomiCore, réponse Gemma 4 française réelle en streaming sur simulateur, Markdown inline rendu sans astérisques bruts, génération de fiche Gemma avec sections/flashcards/quiz, validation visuelle iPhone/iPad. Les tests UI automatisés, VoiceOver complet, interruptions réelles, autonomie, permissions physiques et comportement mémoire sur iPhone physique restent à exécuter.

## Permissions

`Info.plist` contient :

- `NSMicrophoneUsageDescription` ;
- `NSSpeechRecognitionUsageDescription` ;
- `ITSAppUsesNonExemptEncryption=false` ;
- orientations iPhone/iPad.

L'app ne demande aucun mode arrière-plan. Les routines temporisées ne sont donc pas garanties quand l'app est fermée. Une version App Store doit utiliser notifications locales, App Intents et une conception validée App Review.

## Architecture

```text
Packages/NomiCore       domaine, protocoles, mocks, parseurs, vérificateur, tests
NomiIOS/App             composition, navigation adaptative et injection
NomiIOS/DesignSystem    jetons et composants visuels Nomi Aura
NomiIOS/Features        espaces produit par fonctionnalité
NomiIOS/Services        Apple Speech, téléchargement Gemma et runtime LiteRT-LM
```

La persistance actuelle est un JSON protégé dans Application Support. SwiftData, migration, export/effacement utilisateur, sauvegarde et chiffrement applicatif restent à concevoir pour une mise en production App Store.
