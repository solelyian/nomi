# Nomi iOS — prototype SwiftUI

Prototype iPhone/iPad de la refonte Nomi. Le dossier produit et design complet est dans [`docs/dossier-produit-design.md`](docs/dossier-produit-design.md).

## État réel

- **Réel** : compilation SwiftUI, navigation, injection par protocoles, vérification locale de calculs, cours de démonstration, export de fiche, tests unitaires du moteur de calcul.
- **Semi-réel** : microphone + Apple Speech (`SFSpeechRecognizer`) ; compile, mais doit être validé sur iPhone physique avec permissions réelles.
- **Simulé** : diagnostics IA de code, génération de fiche, réponses conversationnelles, correction automatique.
- **Non implémenté** : persistance SwiftData, extension de partage, iCloud, Live Activity, widgets, diarisation fiable, exécution de code.

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

Dans Xcode : choisir le schéma **Nomi**, un simulateur iPhone, puis Run. Sur appareil physique, configurer votre équipe de signature dans Signing & Capabilities.

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

Vérification effectuée dans cette session : 4 tests unitaires, 4 réussites. Les tests UI, VoiceOver, interruptions réelles, autonomie et permissions physiques restent à exécuter.

## Permissions

`Info.plist` contient :

- `NSMicrophoneUsageDescription` ;
- `NSSpeechRecognitionUsageDescription` ;
- `ITSAppUsesNonExemptEncryption=false` ;
- orientations iPhone/iPad.

Le prototype ne demande aucun mode arrière-plan. Une transcription longue en production doit être reconçue avec fichier audio segmenté, Live Activity et validation App Review.

## Architecture

```text
Packages/NomiCore       domaine, protocoles, mocks, vérificateur de calcul, tests
NomiIOS/App             composition et injection
NomiIOS/DesignSystem    jetons et composants visuels
NomiIOS/Features        écrans par fonctionnalité
NomiIOS/Services        intégration Apple Speech
```

Remplacer un mock : créer une implémentation du protocole (`CodeReviewService`, `RevisionService`, `LiveTranscriptionService`), puis l'injecter dans `AppState`. Ne pas embarquer de clé API dans l'app ; préférer un serveur intermédiaire.

## Données et confidentialité

Aucun appel réseau n'est effectué par les mocks. La bibliothèque est volatile. La production doit utiliser Data Protection, Keychain pour les jetons et demander consentement avant tout envoi de code, document ou audio vers un service externe.
