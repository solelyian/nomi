# Nomi iOS — dossier produit et design

Version : 0.1, 25 novembre 2026. Produit de Nyne Technologies.

## 1. Compréhension du produit et périmètre

Nomi est un espace de productivité assisté par IA qui aide à comprendre, vérifier, convertir, planifier, communiquer et apprendre. Les principes structurants repris de la version Windows sont :

- des réponses contrôlées avant affichage, avec rejet plutôt qu'affichage d'un fragment invalide ;
- les calculs sensibles vérifiés par un moteur déterministe, pas par la seule génération ;
- des suggestions toujours confirmées par l'utilisateur, jamais appliquées en silence ;
- une lecture accessible, multilingue et adaptable aux préférences de nombres, densité, contraste et animations ;
- des données personnelles protégées localement, avec transparence sur ce qui quitte l'appareil.

La version iOS n'est pas un portage complet du poste de travail Windows. Elle privilégie la consultation, la capture, la relecture et la révision en mobilité. Le développement complet, l'exécution de code, l'analyse de projet entier et les modèles lourds restent réservés au desktop ou à un service distant explicite.

Périmètre du prototype :

1. relecture de code et vérification déterministe de calculs ;
2. transcription d'un cours en direct avec mode réel Apple Speech et mode démonstration ;
3. transformation d'un cours en fiche de révision, avec formats mobile et fidélité au source ;
4. refonte visuelle native iOS en continuité des tokens « design vivant » de Nomi Windows.

Hors périmètre du prototype : compte multi-appareils, synchronisation iCloud, exécution de code, projet Xcode complet sur iPhone, diarisation fiable des locuteurs, widgets/Live Activities/App Intents, TestFlight.

## 2. Continuité utilisée et hypothèses

### Informations reprises

| Source                                         | Éléments repris                                                                                                                                                  |
| ---------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `README.md` et `ARCHITECTURE.md` du dépôt Nomi | six intentions, moteur de réponse contrôlé, vérification de calculs, confidentialité locale, accessibilité, préférences de lecture, absence de clé API embarquée |
| Résumé de la session « Nomi platform »         | IA locale Qwen/Gemma/Whisper, espaces Aujourd'hui/Tableau/Routines/Transcription, OOBE, multilingue, décisions visuelles récentes                                |
| PR #36                                         | correction des raccourcis, rampes typographiques entières, performance et contraste                                                                              |
| PR #37                                         | tokens de couleurs, tiers responsives, ressorts, couleurs par section, design « vivant » validé                                                                  |
| `nomi-page-nomi-structure.md`                  | conversation unique, détails de modèle, étapes Lecture/Calcul/Vérification, actions proposées et annulables                                                      |
| `nomi-structure-routines.md`                   | automatisations lisibles, jamais exécutées sans accord, mode calme                                                                                               |
| `structure-saisie-rappels.md`                  | langage naturel, compréhension affichée avant enregistrement, accessibilité des annonces                                                                         |
| audits v0.10.x et sécurité                     | stockage chiffré, journal sans contenu, risques de dépendances, besoin de transparence sur les capacités réelles                                                 |

### Écarts adaptés à iOS

| Windows / desktop                | Adaptation iOS proposée                                                                                    |
| -------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| Modèles lourds locaux Qwen/Gemma | IA distante derrière serveur intermédiaire ou modèles Apple/on-device quand disponibles ; mocks par défaut |
| Application de travail prolongée | sessions courtes, reprise, feuilles modales, actions tactiles, export et lecture                           |
| IDE / analyse de code            | collage, fichiers, partage, diff ou pull request ; pas d'exécution ni projet complet local                 |
| Transcription desktop            | microphone iOS, permissions, interruptions système, verrouillage et batterie à traiter                     |
| DPAPI Windows                    | Data Protection iOS, Keychain pour secrets, CryptoKit, fichiers protégés                                   |
| Navigation latérale persistante  | barre d'onglets iPhone à cinq entrées maximum, piles de navigation, adaptation iPad future                 |

### Hypothèses à valider

| Hypothèse             | Choix provisoire                                                     | À valider                                                             |
| --------------------- | -------------------------------------------------------------------- | --------------------------------------------------------------------- |
| Version minimale      | iOS 17                                                               | iOS 26 si `SpeechAnalyzer` et nouvelles API deviennent indispensables |
| Appareils             | iPhone prioritaire, iPad compatible universel                        | niveau de finition iPad et Stage Manager                              |
| Reconnaissance vocale | `SFSpeechRecognizer` pour iOS 17 ; `SpeechAnalyzer` réservé à iOS 26 | stratégie par version et disponibilité on-device                      |
| IA                    | protocoles + mocks, aucun appel réseau                               | fournisseur, coût, serveur intermédiaire, consentement                |
| Langues               | français d'abord, architecture multilingue                           | périmètre des 13 langues Windows sur mobile                           |
| Persistance           | mémoire dans le prototype                                            | SwiftData/fichiers protégés et export                                 |

## 3. Personas et parcours

### Étudiant en cours

Besoin : capter un cours sans être distrait, obtenir une transcription structurée, respecter les règles de consentement.

Parcours : Cours → confirmation d'autorisation → mode microphone ou démonstration → démarrer → pause sur interruption → arrêt → créer une fiche.

Critères clés : état visible en un coup d'œil, zones tactiles ≥ 44 pt, reprise explicite, aucun enregistrement caché.

### Étudiant en révision

Besoin : transformer le cours en fiche fidèle, réviser par courtes sessions.

Parcours : Bibliothèque ou Transcription → fiche → niveau de détail → format fiche/flashcards/quiz → éditer/mettre en évidence → exporter ou partager.

Critères clés : distinction source / explication IA / hypothèse, contenu exportable, corrections humaines faciles.

### Développeur en mobilité

Besoin : relire une fonction ou un diff, vérifier un calcul métier, comprendre un risque sans ouvrir un poste de travail.

Parcours : Code → importer/coller → relire → diagnostic → explication → correction proposée → question conversationnelle → copier ou appliquer après confirmation.

Critères clés : sévérité lisible, correction non appliquée sans confirmation, limites d'analyse explicites.

## 4. Backlog priorisé

| Priorité | User story                                                                       | Critères d'acceptation                                                                                                     |
| -------- | -------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| P0       | En tant qu'utilisateur, je comprends ce qui est réel ou simulé                   | Chaque écran IA indique son mode ; un tableau d'état existe ; aucune promesse implicite                                    |
| P0       | En tant qu'utilisateur, je colle du code et obtiens des diagnostics              | Chaque diagnostic contient ligne, problème, explication, sévérité, correction ; aucun correctif n'est appliqué sans action |
| P0       | En tant qu'utilisateur, je vérifie un calcul                                     | Le calcul est recalculé localement ; mismatch affiché ; notation ambiguë refusée sans correction inventée                  |
| P0       | En tant qu'étudiant, je démarre une transcription de démonstration               | Elle fonctionne sur simulateur sans permission, produit des segments horodatés                                             |
| P0       | En tant qu'étudiant, je comprends le consentement                                | L'écran exige une confirmation explicite avant démarrage et rappelle les règles                                            |
| P0       | En tant qu'étudiant, je génère une fiche                                         | Sections standard, niveau et format choisis, fidélité affichée, export partage                                             |
| P1       | En tant qu'utilisateur d'appareil physique, je peux transcrire avec Apple Speech | Permissions micro/reconnaissance, erreurs lisibles, arrêt propre ; échec retombe sur démonstration                         |
| P1       | En tant qu'utilisateur, je retrouve mes fiches                                   | Bibliothèque, suppression, persistance SwiftData dans la prochaine itération                                               |
| P1       | En tant qu'utilisateur VoiceOver, je parcours les écrans                         | Labels, éléments combinés, annonces d'état, Dynamic Type jusqu'aux grandes tailles                                         |
| P1       | En tant que développeur, je peux remplacer les mocks                             | Protocoles Swift, injection dans `AppState`, aucun type concret requis par les vues                                        |
| P2       | En tant qu'utilisateur, je démarre depuis un partage iOS                         | Extension de partage pour code, PDF et audio                                                                               |
| P2       | En tant qu'étudiant, je vois la transcription en Live Activity                   | État, pause, arrêt, durée ; batterie documentée                                                                            |
| P2       | En tant qu'utilisateur, je révise avec des rappels                               | notifications de révision espacées, désactivables                                                                          |
| P3       | En tant qu'utilisateur iPad, j'utilise une vue multi-colonnes                    | NavigationSplitView, clavier externe, tailles de classe                                                                    |

## 5. Architecture fonctionnelle et technique

### Version cible

iOS 17 minimum est retenu pour couvrir davantage d'appareils et utiliser SwiftUI, Swift Concurrency, `Speech` et les contrôles système récents. Limite importante : `SpeechAnalyzer` est documenté par Apple comme disponible à partir d'iOS 26. Le chemin semi-réel du prototype utilise donc `SFSpeechRecognizer`, compatible avec la cible. Une future version peut bifurquer : iOS 26+ avec `SpeechAnalyzer`/`SpeechTranscriber`, iOS 17–25 avec l'API historique ou un service tiers.

### Modules

```text
ios/
├── Nomi.xcodeproj                  # généré par XcodeGen, commité pour ouverture directe
├── project.yml                     # source reproductible du projet
├── Packages/NomiCore/              # domaine, protocoles, mocks, moteur de calcul, tests
└── NomiIOS/
    ├── App/                        # composition racine, injection de dépendances
    ├── DesignSystem/               # jetons, couleurs dynamiques, cartes, badges
    ├── Features/
    │   ├── Home/
    │   ├── CodeReview/
    │   ├── Courses/
    │   ├── Revision/
    │   ├── Library/
    │   └── Settings/
    ├── Services/                   # implémentations Apple spécifiques à l'appareil
    └── Resources/                  # Info.plist, icônes claires/sombre/teintée
```

Choix : MVVM léger avec `ObservableObject`, `@MainActor` pour les view models, injection par protocoles depuis `AppState`. Le domaine ne dépend pas de SwiftUI. Les vues ne connaissent pas les services concrets.

Raison : ce niveau de structure reste lisible pour un prototype tout en isolant les points remplaçables. Les modules SPM peuvent être multipliés plus tard (`NomiDesignSystem`, `NomiTranscription`, `NomiAI`) si l'équipe grandit.

### Concurrence et cycle de vie

- les view models sont `@MainActor` ;
- les appels IA sont `async throws`, annulables par `Task` ;
- les longues sources sont traitées en streams (`AsyncStream`) pour la transcription ;
- pause/arrêt annulent les tâches et libèrent la session audio ;
- interruption audio observe `AVAudioSession.interruptionNotification` ;
- aucun traitement ne continue en arrière-plan dans ce prototype.

### Persistance et sécurité

Prototype : fiches en mémoire uniquement. Production recommandée :

- SwiftData ou fichiers JSON dans le conteneur de l'app avec `NSFileProtectionComplete` ;
- Keychain pour jetons d'accès éventuels, jamais de clé API embarquée ;
- serveur intermédiaire pour toute IA payante ;
- ATS/TLS obligatoire ; pas de journal contenant transcription, code ou document ;
- export utilisateur explicite ; synchronisation iCloud chiffrée opt-in ;
- permission micro/reconnaissance demandée au moment d'usage avec écran d'explication préalable.

### Données qui quittent l'appareil

Dans ce prototype : aucune. Le mode réel de reconnaissance vocale utilise Apple Speech lorsque disponible ; selon l'appareil, la langue et les réglages système, le traitement peut être sur appareil ou géré par le service Apple Speech. L'app doit afficher l'état et demander consentement avant activation d'un quelconque service tiers.

Production possible :

| Donnée               | Destination envisageable       | Consentement                       |
| -------------------- | ------------------------------ | ---------------------------------- |
| extrait de code/diff | serveur Nyne ou fournisseur IA | explicite par action ou par espace |
| transcription        | Apple Speech ou service tiers  | explicite avant démarrage          |
| document cours       | fournisseur IA                 | explicite avant génération         |
| métriques techniques | observabilité sans contenu     | opt-in, minimisation               |

### Stratégie hors-ligne et fallback

| Fonction          | Principal                                   | Fallback                                             |
| ----------------- | ------------------------------------------- | ---------------------------------------------------- |
| Relecture de code | modèle distant ou Apple selon disponibilité | heuristiques locales + message de limite             |
| Calcul            | moteur local déterministe                   | toujours disponible                                  |
| Transcription     | Apple Speech sur appareil si disponible     | démonstration, fichier audio conservé pour plus tard |
| Fiche             | IA distante/on-device                       | résumé extractif local sans contenu inventé          |
| Bibliothèque      | stockage local                              | export manuel si sync indisponible                   |

## 6. Design system iOS

### Principes

Natif avant tout : contrôles SwiftUI système, navigation claire, SF Symbols, matériaux iOS, Dynamic Type. La personnalité Nomi vient des couleurs, de la hiérarchie et de la douceur des surfaces, sans réinventer boutons et champs.

### Jetons repris et adaptés

| Jeton Windows    | iOS clair | iOS sombre | Usage               |
| ---------------- | --------: | ---------: | ------------------- |
| Canvas           | `#ECE6DE` |  `#0F0E0D` | fond d'écran        |
| Surface          | `#FFFFFF` |  `#1F1E1C` | cartes              |
| Ink              | `#171512` |  `#F2EFEA` | texte principal     |
| Accent           | `#B4482B` |  `#E8825F` | actions et identité |
| Code             | `#3D6A98` |  `#7FA7D4` | espace code         |
| Transcript/cours | `#23787F` |  `#6CC3C9` | transcription       |
| Routines/succès  | `#2F7D4F` |  `#6FBF8A` | validation, fiches  |
| Ligne            | `#E2DDD6` |  `#2F2D2A` | séparateurs         |

Les ressorts Windows (press/morph) sont traduits en `Animation.spring` ; les animations doivent être désactivées ou réduites si Réduire les animations est actif.

### Typographie

SF Pro/Text styles système : `largeTitle` pour accueil, `title2` pour sections, `headline` pour lignes denses, `body` pour contenu, `footnote` monospace pour code et calculs. Ne pas fixer de tailles absolues sauf minuteur. Toutes les vues doivent rester utilisables en Dynamic Type élevé.

### Composants

| Composant            | Règle                                                         |
| -------------------- | ------------------------------------------------------------- |
| NomiCard             | surface arrondie 18 pt, filet 1 pt, accent discret en haut    |
| NomiBadge            | capsule sémantique, jamais comme unique porteur d'information |
| DiagnosticRow        | icône + titre + ligne + sévérité, hauteur ≥ 44 pt             |
| NomiSectionTitle     | titre + sous-titre explicatif pour réduire l'ambiguïté IA     |
| Permission expliquée | bénéfice, donnée utilisée, alternative si refus               |

### Icône et lancement

L'icône utilise un N géométrique sur dégradé chaud, en continuité de l'accent Nomi. Les variantes sombre et teintée sont fournies dans le catalogue d'assets. À valider visuellement avant TestFlight : reconnaissance à petite taille, respect des marges, absence de texte.

## 7. Navigation

Schéma retenu : `TabView` à cinq onglets sur iPhone.

1. Accueil — orientation et actions rapides ;
2. Code — relecture et calcul ;
3. Cours — transcription ;
4. Fiches — bibliothèque et révision ;
5. Réglages — IA, confidentialité, accessibilité.

Justification : cinq entrées restent dans la limite iOS. « Projets », « transcriptions » et « documents » sont regroupés sous Fiches/Bibliothèque plutôt que de multiplier les onglets. Chaque espace utilise `NavigationStack`. Sur iPad, la prochaine itération devra passer à `NavigationSplitView` dans les espaces contenu/détail, tout en conservant la même structure mentale.

## 8. Wireframes / spécifications des écrans clés

### Accueil

```text
[Badge prototype]
Bonjour.
Sous-titre d'orientation

[Carte action] Relire du code          >
[Carte action] Transcrire un cours     >
[Carte action] Créer une fiche         >

[Aujourd'hui]
 [2 actions] [0 données envoyées] [n fiches]

[IA simulée par défaut]
```

États : première ouverture, fiches existantes, hors-ligne (inchangé car mocks locaux), erreur IA (carte explicative non bloquante).

### Relecture de code

```text
Titre + limites mobiles
[Swift · exemple]        [Importer ⌄]
[Éditeur monospace]
[Relire] [Explication]

Diagnostics
(• sévérité) titre / ligne / problème
swipe: Corriger

Vérification de calcul
[expression] [Vérifier localement]
[résultat local]

Question sur le code
[champ] [Poser]
```

Détail diagnostic : feuille modale avec problème, explication pédagogique, correction proposée, bouton Appliquer. Toute application doit produire un diff dans la version production.

### Vérification des calculs

Entrées acceptées : décimaux, parenthèses, `+ - × * / ÷`, pourcent postfixé, résultat simple. Unités de temps et expressions ambiguës refusées. La sortie distingue `verified`, `mismatch`, `unsupported`, `invalid`.

### Transcription en direct

```text
[Mode démonstration | microphone réel]
[✓ autorisation confirmée]
Note diarisation/consentement

        03:42
      waveform

[Pause] [Arrêter] ou [Démarrer]

Transcription
00:00 Enseignant
Texte…
[Fiche]
```

États : permission non demandée, refusée, reconnaissance indisponible, interruption, arrière-plan non garanti, mode démonstration. Production : Live Activity et fichier audio segmenté.

### Éditeur de fiche

```text
Source / niveau / format / Générer
Titre fiche                    partage
[Section]
Titre                    badge fidélité
corps sélectionnable

Formats flashcards/quiz pour révision courte
[Enregistrer bibliothèque]
```

Badges : `source`, `explication IA`, `hypothèse`. Correction et réorganisation sont marquées simulées dans le prototype.

### Bibliothèque

Liste avec titre, format, niveau, date ; suppression par swipe ; détail en lecture. États : vide, import, erreur de persistance (production), synchronisation désactivée.

### Paramètres IA

- IA distante désactivée par défaut ;
- langue de transcription ;
- accessibilité ;
- tableau d'état réel/simulé/non implémenté ;
- explication des intégrations iOS priorisées.

## 9. Évaluation des options de transcription

| Option                                 | Précision/langues                                  | Coût                  | Confidentialité                                      | Hors-ligne                              | Limites                                            |
| -------------------------------------- | -------------------------------------------------- | --------------------- | ---------------------------------------------------- | --------------------------------------- | -------------------------------------------------- |
| `SFSpeechRecognizer`                   | bonne sur langues majeures, dépend appareil/locale | inclus système        | gérée par Apple, parfois serveur selon configuration | possible sur certains appareils/langues | API ancienne, pas de diarisation, limites de durée |
| `SpeechAnalyzer` / `SpeechTranscriber` | architecture moderne, assets installables          | inclus système        | meilleure maîtrise on-device selon assets            | possible selon module                   | iOS 26+ seulement ; incompatible avec cible iOS 17 |
| Service tiers cloud                    | parfois meilleure diarisation et vocabulaire       | par minute/appel      | audio envoyé hors appareil                           | non                                     | consentement fort, coût, latence, conservation     |
| Modèle Whisper on-device               | bon en vocabulaire général                         | modèle lourd/batterie | local                                                | oui                                     | poids, thermique, intégration non native           |

Décision prototype : `SFSpeechRecognizer` semi-réel + démonstration. Décision à valider : cible iOS 26 pour `SpeechAnalyzer` ou service tiers si la diarisation est indispensable. Apple Speech ne fournit pas nativement une attribution fiable des locuteurs ; toute étiquette « Enseignant » doit donc être présentée comme une hypothèse ou une entrée utilisateur, pas comme un fait.

## 10. Intégrations iOS proposées

| Priorité | Intégration                    | Valeur                               | Coût/risque            | Décision prototype |
| -------- | ------------------------------ | ------------------------------------ | ---------------------- | ------------------ |
| 1        | Extension de partage           | entrée naturelle pour code/PDF/cours | moyenne                | non implémentée    |
| 2        | Live Activity / Dynamic Island | état de transcription hors app       | permission, batterie   | non implémentée    |
| 3        | App Intents/Raccourcis/Siri    | démarrer cours/fiche                 | modération vocale      | non implémentée    |
| 4        | Notifications de révision      | rappel espacé                        | risque de bruit        | non implémentée    |
| 5        | Spotlight                      | retrouver fiches                     | confidentialité index  | non implémentée    |
| 6        | iCloud sync                    | continuité iPhone/iPad               | coût et conflits       | à décider          |
| 7        | Widgets                        | révision rapide                      | surface supplémentaire | à décider          |

## 11. Conformité App Store et confidentialité

Points à préparer avant distribution :

- `NSMicrophoneUsageDescription` et `NSSpeechRecognitionUsageDescription` rédigées en langage utilisateur ;
- privacy manifest listant API requises et domaines réseau ;
- Privacy Nutrition Labels : audio, contenu utilisateur, identifiants éventuels, diagnostics sans contenu ;
- pas d'enregistrement caché : indicateur système micro, bouton d'arrêt visible, consentement contextuel ;
- respect App Review Guidelines pour contenu généré par IA : filtrage, signalement, limitation, mention des réponses potentiellement erronées ;
- aucune clé API privée dans le binaire ;
- signalement clair des erreurs et moyen de contacter Nyne ;
- export/deletion des données locales ;
- accessibilité testée avant soumission.

## 12. Plan de tests et qualité

| Type                                |                       Écrit | Exécuté | Résultat                                 |
| ----------------------------------- | --------------------------: | ------: | ---------------------------------------- |
| Tests unitaires du moteur de calcul |                         oui |     oui | 4/4 réussis sur simulateur iOS 26.5      |
| Compilation iOS Debug               |                         oui |     oui | réussie, Xcode 26.6, iPhone 17 simulator |
| Tests view models IA                |         partiels/non écrits |     non | à faire avec horloges contrôlées         |
| XCUITest parcours principaux        |                         non |     non | prochaine itération                      |
| Audit VoiceOver/Dynamic Type        |                         non |     non | à exécuter sur appareil                  |
| Permission micro réelle             | implémentée semi-réellement |     non | simulateur insuffisant, appareil requis  |
| Interruptions/thermique/batterie    |        conception seulement |     non | appareil physique requis                 |
| CI macOS                            |                   spécifiée |     non | runner ou Xcode Cloud requis             |

Scénarios XCUITest à écrire : générer une relecture, ouvrir un diagnostic, vérifier un calcul correct et incorrect, démarrer la démonstration, produire une fiche, changer format flashcards, enregistrer dans bibliothèque.

CI/CD recommandée : GitHub Actions macOS ou Xcode Cloud, xcodegen, build sans signature, tests `NomiCore`, XCUITest sur iPhone récent, capture d'accessibilité, puis TestFlight avec signature gérée. Aucune donnée utilisateur dans les logs ou rapports de plantage.

## 13. État réel / simulé / non implémenté

| Fonctionnalité                                                   | État dans le prototype                             |
| ---------------------------------------------------------------- | -------------------------------------------------- |
| Compilation SwiftUI iOS                                          | réel                                               |
| Architecture protocoles + injection                              | réel                                               |
| Vérification déterministe de calculs simples                     | réel                                               |
| Diagnostics de code                                              | simulé par règles locales ; remplaçable par IA     |
| Application automatique d'un correctif                           | simulé par annotation ; pas de diff/AST            |
| Microphone + Apple Speech                                        | semi-réel ; compile, doit être validé sur appareil |
| Cours de démonstration                                           | réel en local, sans permission                     |
| Diarisation des locuteurs                                        | non implémentée ; limite native documentée         |
| Résumé en direct                                                 | non implémenté ; conception documentée             |
| Génération de fiche                                              | simulée par extraction et gabarits locaux          |
| Flashcards/quiz                                                  | simulés mais interactifs                           |
| Export par feuille de partage                                    | réel                                               |
| Bibliothèque                                                     | mémoire seulement, non persistante                 |
| Import Fichiers/Partage                                          | simulé/non implémenté                              |
| Persistance sécurisée, iCloud, widgets, Live Activity, Spotlight | non implémentés                                    |

## 14. Risques, limites et prochaines étapes

Risques majeurs :

1. permissions ou reconnaissance indisponibles selon langue/appareil ;
2. coût ou confidentialité de l'IA distante ;
3. hallucinations dans fiches et revue de code ;
4. batterie/thermique pendant transcription longue ;
5. diarisation impossible à garantir nativement ;
6. confusion utilisateur entre démonstration et service réel.

Prochaines étapes recommandées :

1. validation UX sur appareil iPhone physique ;
2. choix définitif iOS 17 vs iOS 26 pour la transcription ;
3. implémentation SwiftData + protections fichiers ;
4. service IA derrière serveur intermédiaire avec budget, quotas et consentement ;
5. XCUITest, audit VoiceOver/Dynamic Type et tests d'interruption ;
6. import Fichiers/Partage puis Live Activity ;
7. maquette validée écran par écran avant finition visuelle plus poussée.

## 15. Synthèse

Le prototype livré est une base native iOS compilable en Swift/SwiftUI, fidèle aux principes Nomi sans prétendre que toutes les fonctions IA sont réelles. La partie réellement vérifiable — calculs déterministes, architecture, transcription de démonstration, compilation et tests unitaires — est séparée des interfaces simulées. Les choix de continuité reprennent les tokens, la prudence sur les réponses IA et la logique de consentement des versions précédentes ; les adaptations iOS sont explicitement motivées par la mobilité, les permissions et les contraintes système.
