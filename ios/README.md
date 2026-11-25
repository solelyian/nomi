# Nomi iOS — prototype SwiftUI

Prototype iPhone/iPad de la refonte Nomi, recentré sur la parité avec les fonctions productivité de la version Windows. La relecture de code a été abandonnée dans cette itération. Le dossier produit/design complet est dans [`docs/dossier-produit-design.md`](docs/dossier-produit-design.md).

## Structure

- **iPhone** : cinq onglets — Aujourd'hui, Tableau, Focus, Nomi, Plus.
- **Plus** : Routines, Transcription, Fiches et révisions, Réglages.
- **iPad** : barre latérale complète avec toutes les sections.

## État réel / simulé / non implémenté

| État | Fonctions |
|---|---|
| **Réel** | compilation SwiftUI, onboarding de première ouverture, navigation iPhone/iPad, tableau de tâches, édition, division et progression des tâches, minuteur Focus avec pause/reprise/fin, fils de conversation, vérification locale de calculs simples, parseur local simple de tâches et routines, activation/édition des routines, persistance locale JSON protégée, flashcards/quiz, export de fiche, tests unitaires |
| **Semi-réel** | microphone + Apple Speech (`SFSpeechRecognizer`) : compile, mais doit être validé sur iPhone physique avec permissions réelles |
| **Simulé** | réponses IA génératives, génération de fiches par extraction/gabarits locaux, documents liés par simple titre, données d'agenda de démonstration |
| **Non implémenté** | observation des autres apps en Focus, notifications locales, App Intents, Document Picker, extension de partage, SwiftData, iCloud, Live Activity, widgets, Spotlight, diarisation fiable |

## Prérequis

- macOS avec Xcode 26.6 ou version compatible iOS 17+ ;
- XcodeGen 2.48+ si vous régénérez le projet (`brew install xcodegen`) ;
- un simulateur iPhone iOS 17 ou supérieur. Le build vérifié ici utilisait Xcode 26.6 et iPhone 17 sous iOS 26.5.

Le projet `Nomi.xcodeproj` est commité pour ouverture directe. `project.yml` permet de le régénérer de façon reproductible.

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

Vérification effectuée dans cette session : 30 tests unitaires, 18 réussites, couvrant le vérificateur de calculs, le parseur de tâches, le parseur de routines et les échéances de routines. Les tests UI, VoiceOver, interruptions réelles, autonomie et permissions physiques restent à exécuter.

## Permissions

`Info.plist` contient :

- `NSMicrophoneUsageDescription` ;
- `NSSpeechRecognitionUsageDescription` ;
- `ITSAppUsesNonExemptEncryption=false` ;
- orientations iPhone/iPad.

Le prototype ne demande aucun mode arrière-plan. Les routines temporisées ne peuvent donc pas être garanties quand l'app est fermée. Une production doit utiliser notifications locales, App Intents et une conception validée App Review.

## Architecture

```text
Packages/NomiCore       domaine, protocoles, mocks, parseurs, vérificateur, tests
NomiIOS/App             composition, navigation adaptative et injection
NomiIOS/DesignSystem    jetons et composants visuels
NomiIOS/Features        espaces produit par fonctionnalité
NomiIOS/Services        intégration Apple Speech
```

Remplacer un mock : créer une implémentation du protocole (`RevisionService`, `LiveTranscriptionService`), puis l'injecter dans `AppState`. Ne pas embarquer de clé API dans l'app ; préférer un serveur intermédiaire.

## Données et confidentialité

Les tâches, routines, sessions Focus, conversations et fiches sont persistées dans un fichier JSON local du répertoire Application Support, écrit avec l'option de protection complète des fichiers iOS. Cette persistance est une base de prototype, pas le modèle final : SwiftData, migration, export/effacement utilisateur et éventuel chiffrement restent à concevoir.

Aucun appel réseau n'est effectué par les mocks. La production doit utiliser Data Protection, Keychain pour les jetons et demander consentement avant tout envoi de document, audio ou conversation vers un service externe.
