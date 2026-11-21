# Nomi — dossier produit & design de la refonte

Mission : refonte graphique de Nomi et fondations de trois fonctionnalités
(relecture de code et vérification des calculs, transcription de cours en
direct, transformation d'un cours en fiche de révision), livrées sous forme
d'un dossier design puis de deux prototypes (WinUI 3 et React Native).

Statut du document : proposition complète, à valider avant implémentation.
Chaque décision nouvelle est marquée **[décision]** ; chaque point repris de
l'existant est marqué **[existant]** ; ce qui reste à trancher est marqué
**[à valider]**.

---

## 1. Vision et principes de conception

### 1.1 Ce qu'est Nomi

**[existant]** Nomi est un compagnon de productivité propulsé par l'IA, qui
tourne entièrement en local sur le PC Windows de l'utilisateur. Il aide à
travailler, apprendre, produire et vérifier, sans compte, sans abonnement et
sans envoyer de données.

Aujourd'hui, Nomi est centré sur la journée : un plan (« Aujourd'hui »), un
tableau de tâches, des routines déclenchées par le contexte, un assistant par
intentions (Comprendre, Vérifier, Convertir, Planifier, Communiquer,
Apprendre) et Focus, un appel temps réel sur une fenêtre partagée.

La refonte élargit Nomi d'un *compagnon de journée* à un *espace de travail
adaptatif* : le même compagnon, les mêmes garanties, mais organisé en espaces
métier qui accueillent les trois nouvelles fonctionnalités.

### 1.2 Principes (conservés, jamais renégociés)

1. **[existant] Local d'abord.** Inférence, transcription et données restent
   sur la machine. Zéro clé API, zéro service cloud obligatoire.
2. **[existant] L'IA propose, elle n'agit jamais seule.** Toute suggestion,
   correction, plan ou fiche attend un clic. Rien n'est appliqué silencieusement,
   aucun post-traitement ne choisit de chiffre à la place de l'utilisateur.
3. **[existant] Jamais clinique, jamais infantilisant, jamais de pitié.**
   Pas de diagnostic, pas de score, pas de streak, pas de classement.
   Vouvoiement partout.
4. **[existant] Vérifié plutôt que généré.** Un calcul affiché « vérifié » est
   recalculé par un moteur déterministe ; une affirmation douteuse est signalée
   comme telle, jamais maquillée.
5. **[décision] Provenance visible.** Tout ce que Nomi produit distingue ce
   qui vient de la source (cours, code, document) de ce qui est une explication
   ou une hypothèse de l'IA. Nouveau principe, exigé par la fiche de révision et
   généralisé à toute la refonte.
6. **[existant] Accessible et lisible.** Atkinson Hyperlegible, navigation
   clavier complète, contrastes suffisants, langue FR/EN réelle.
7. **[décision] Une seule densité de langage.** Les mêmes mots, les mêmes
   composants et les mêmes gestes sur Windows et sur mobile.

---

## 2. Parcours utilisateurs

Conformément aux choix passés, pas de personas génériques : des parcours
réels, chacun relié aux fonctions livrées ou prévues.

### Parcours existants (continuité)

- **Planifier sa journée.** Écrire une phrase → tâche datée et estimée →
  plan du jour → « Ma journée déraille » si ça dérape.
- **Suivre une tâche.** Carte en cours → temps écoulé, fin probable → bilan
  sans score.
- **Être relancé au bon moment.** Une routine se propose dans le compagnon,
  jamais plus d'une fois par jour.
- **Focus sur un écran.** Partage d'une fenêtre → Nomi voit, répond à voix
  haute ou par écrit, signale les écarts recalculés.
- **Demander à l'assistant.** Une intention + une demande libre (+ fichiers)
  → réponse passée par la politique de réponse.

### Nouveaux parcours (la mission)

- **P1 — Relecture de code.** L'utilisateur ouvre l'espace Code, choisit un
  fichier, une sélection ou un dossier → « Relire ». Nomi liste les
  diagnostics (problème, sévérité, explication, correction proposée). Chaque
  correction se prévisualise en diff et ne s'applique que sur clic. Un bouton
  « Expliquer » donne la version pédagogique ; une zone conversationnelle
  permet de poser une question sur une portion choisie.
- **P2 — Vérifier un calcul.** Dans l'espace Code ou depuis une demande,
  Nomi repère les expressions (formules, unités, conversions, arrondis) et les
  recalcule lui-même. Un panneau « Calculs » liste chaque expression, la valeur
  lue, la valeur recalculée et l'écart. Seul un écart confirmé par le moteur
  est signalé ; la formule choisie par le modèle n'est jamais présentée comme
  certaine.
- **P3 — Transcrire un cours.** « Démarrer » lance la capture micro ; la
  transcription défile avec horodatages et locuteurs estimés, pause/reprise
  possibles, reprise après coupure. Un panneau « En direct » cumule points
  clés, définitions et questions détectées. « Arrêter » fige la transcription
  et propose : corriger, exporter, ou « en faire une fiche ».
- **P4 — Produire une fiche de révision.** Depuis une transcription, un
  document importé ou un cours de la bibliothèque → choix du format (fiche,
  plan, flashcards, quiz, résumé) et du niveau (ultra-condensé, standard,
  détaillé) → fiche structurée. Chaque bloc porte un badge de provenance
  (« Cours » / « Explication Nomi » / « Hypothèse »). L'utilisateur corrige,
  réordonne, surligne, puis exporte (Markdown, PDF, impression).

---

## 3. Analyse des décisions passées

Sources relues : session « Nomi platform » (21 PR fusionnées, v0.9.0 →
v0.10.x), `ARCHITECTURE.md`, `README.md`, audits `audit-nomi-site-vs-app.md`
et `audit-v0.10.3.md`, documents de structure (IA-productivité, routines,
page Nomi), maquettes `design/nomi-v2-mockup.html` et
`design/nomi-v3-glass-mockup.html`.

### 3.1 Ce qui est conservé tel quel

| Décision | Statut | Pourquoi |
|---|---|---|
| Inférence locale LLamaSharp (Qwen3 8B + Gemma 4 E4B, Whisper small) | conservé | Pilier du produit ; aucune dépendance cloud |
| Moteur de calcul déterministe (`Calc.cs`) et double recalcul | conservé, réutilisé | Cœur de la fonctionnalité « vérification des calculs » |
| Politique de réponse déterministe (`response-policy.json`) | conservé | Garde-fou commun, partagé C#/JS |
| Données chiffrées DPAPI (`SecureFile`) + export + « Tout supprimer » | conservé | Exigence de confidentialité ; étendu aux transcriptions et fiches |
| « Une phrase devient une tâche », routines, « Ma journée déraille » | conservé | Différenciation produit |
| Focus en mode appel (aperçu live, Whisper, voix, résumé) | conservé | Modèle d'interaction repris pour la transcription |
| Ton : vouvoiement, sobre, jamais clinique | conservé | Identité de marque |
| FR/EN réel, Atkinson Hyperlegible | conservé | Exigence d'accessibilité |

### 3.2 Ce qui est repris et adapté

| Décision | Adaptation |
|---|---|
| Navigation plate (Aujourd'hui, Tableau, Assistant, Routines, Focus) | Regroupée en **trois espaces** : Journée, Studio, Études (§4.1) |
| Direction « verre chaud » de la maquette v3 | Formalisée en design system v4 (§8) : tokens, états, thème sombre |
| Panneau « Détails » des réponses | Deviennent les **panneaux de provenance** : règles, modèle, recalculs |
| Affichage adaptatif des nombres (mots / proportions / chiffres) | Appliqué aussi aux durées de transcription et aux tailles de fichiers |

### 3.3 Incohérences signalées (à traiter dans les prototypes ou plus tard)

- L'audit v0.10.3 liste des bugs ouverts (B1–B13) et optimisations (O1–O8)
  sur l'app existante. **[à valider]** Ils sont hors périmètre de la refonte
  (prototypes seulement) mais le dossier en tient compte : la nouvelle
  vérification de calculs réutilisera le moteur, pas l'ancienne chaîne.
- L'app actuelle stocke les modèles dans `%LOCALAPPDATA%\Nyne\Nomi\models` ;
  les prototypes réutilisent le même emplacement et le même catalogue
  `content/*.json`.

---

## 4. Architecture fonctionnelle

### 4.1 Les espaces [décision]

```text
Nomi
├── Accueil                  tableau de bord : journée, reprise, suggestions
├── Journée                  Aujourd'hui · Tableau · Routines  [existant]
├── Studio
│   ├── Code                 relecture de code + vérification des calculs
│   └── Focus                appel temps réel sur une fenêtre  [existant]
├── Études
│   ├── Cours                cours importés, liés à leurs fiches
│   ├── Transcriptions       sessions en direct et archives
│   └── Fiches               fiches de révision générées et éditées
├── Bibliothèque             recherche globale : cours, fiches, documents
├── Assistant                les six intentions  [existant]
└── Paramètres               lecture, nombres, présence, données, IA, compte
```

Règles : la navigation latérale est persistante et groupée ; chaque espace
garde son état pendant la session ; `Ctrl+K` ouvre la palette partout ;
`Ctrl+1…9` sautent d'espace ; la saisie d'une tâche (`Ctrl+N`,
`Ctrl+Alt+Espace`) reste globale.

### 4.2 Relecture de code et vérification des calculs

**Entrées.** Fichier(s), sélection, fonction, diff (presse-papiers ou patch),
ou dossier (périmètre borné). Langages v1 : C#, JS/TS, Python, XAML/XML,
Markdown. Limite d'entrée affichée (≈ 6 000 caractères par demande de
conversation ; la relecture de fichier passe par extrait structuré).

**Sorties.** Liste de diagnostics : emplacement, sévérité (bloquant /
important / à revoir / suggestion), problème, explication, correction
proposée en diff. Panneau « Calculs » : expression, valeur lue, valeur
recalculée, écart, statut (vérifié / écart / non vérifiable). Mode
« Expliquer » : pourquoi l'erreur est probable et comment la corriger, en
langage clair. Conversation contextuelle sur une portion choisie.

**Pipeline.**

```text
entrée → extraction structurée (langage, symboles, expressions numériques)
       → analyse LLM locale (relecture)         ─┐
       → moteur déterministe (calculs, unités)   ─┤ jamais confiée au modèle
       → politique de réponse                    ─┤
       → diagnostics + corrections en diff       ─┘ (application = clic)
```

**Cas d'erreur.** Fichier illisible ou binaire → refus explicite. Entrée trop
longue → découpage proposé, jamais silencieux. Modèle non chargé → état vide
avec action « Charger ». Réponse rejetée par la politique → message localisé
+ possibilité de relancer. Aucun fragment de code n'est affiché comme
« corrigé » sans diff visible.

**Garde-fous.** Une correction ne s'applique que sur clic, en diff. Le
diagnostic « calcul » n'est émis que si le moteur recalcule l'écart ; une
formule plausible mais non revérifiée reste « non vérifiable ». Aucune
exécution de code.

**Fallback.** Sans modèle : la vérification des calculs reste disponible en
pur déterministe (analyse des expressions), la relecture est désactivée et le
dit.

### 4.3 Transcription d'un cours en direct

**Entrées.** Micro système (Whisper small local, déjà utilisé par Focus) ;
langue principale FR/EN ; nom de session facultatif.

**Sorties.** Transcription horodatée, découpée en sections (titres détectés),
locuteurs estimés (Locuteur 1, 2… renommables). Panneau « En direct » :
points clés, définitions, questions importantes, actions à retenir — cumulés
pendant la session, marqués « provisoire » jusqu'à l'arrêt.

**États.** `Prêt` → `Écoute` → `Pause` ↔ `Écoute` → `Arrêtée` → `Résumée`.
Indicateurs permanents : état d'enregistrement, durée, qualité du signal,
dernière phrase reconnue, reprise après silence. Coupure (micro perdu,
mise en veille) : la session se met en pause d'elle-même et propose de
reprendre ; le texte déjà transcrit n'est jamais perdu (écriture incrémentale
chiffrée).

**Cas d'erreur.** Pas de micro ou permission refusée → état explicite +
basculer vers saisie manuelle/dictée courte. Silence long → la session reste
ouverte, marquée. Segment incompréhensible → `[inaudible]` horodaté plutôt
que texte inventé. **[à valider]** Diarisation réelle multi-locuteurs : le
matériel local la limite ; v1 = estimation simple, présentée comme telle.

**Garde-fous.** Aucun enregistrement ne démarre sans clic ; un indicateur
d'enregistrement reste visible (règle Focus reprise). Le résumé en direct est
régénéré en entier à intervalle, jamais patché : pas de dérive cumulée.

**Fallback.** Sans modèle : transcription brute seule, panneau « En direct »
désactivé et expliqué.

### 4.4 Cours → fiche de révision

**Entrées.** Transcription existante, document importé (PDF, Word, texte —
extraction locale comme l'assistant), ou cours de la bibliothèque. Choix :
format (fiche de révision, plan de cours, flashcards, quiz, résumé) et niveau
(ultra-condensé, standard, détaillé).

**Sorties.** Document structuré en sections : objectif du cours, idées
essentielles, définitions, formules, exemples, schémas/listes, pièges
fréquents, questions de révision, résumé. Chaque bloc porte sa provenance :
`Cours` (cité ou paraphrasé de la source), `Explication Nomi` (reformulation
pédagogique), `Hypothèse` (ajout non présent dans la source, signalé).
Édition : corriger le texte, réordonner les sections, surligner, retirer un
bloc. Export : Markdown, impression/PDF.

**Pipeline.**

```text
source → extraction texte locale → plan de fiche (LLM)
      → sections générées, chacune reliée à ses extraits source
      → politique de réponse → fiche éditable → export
```

**Cas d'erreur.** Source vide ou trop courte → refus explicite. Source très
longue → fiche par chapitre proposée. Bloc sans rattachement à la source →
badge `Hypothèse` obligatoire. Échec de génération → la fiche garde les
sections extraites et le dit.

**Garde-fous.** La fidélité est un affichage, pas une promesse : les badges
de provenance sont calculés par comparaison d'extraits, et tout bloc non
relié est marqué. Jamais de contenu « complété » présenté comme du cours.

**Fallback.** Sans modèle : fiche en mode « squelette » — sections vides
préremplies des extraits bruts, éditables.

---

## 5. Backlog priorisé

Échelle : **P0** cœur de mission, **P1** attendu pour un prototype crédible,
**P2** différable. Critères d'acceptation en langage de test.

### Relecture de code & calculs

| # | P | User story | Critères d'acceptation |
|---|---|---|---|
| US-C1 | P0 | Choisir un fichier / une sélection / un diff et lancer une relecture | La liste de diagnostics apparaît ; chaque diagnostic montre emplacement, sévérité, problème |
| US-C2 | P0 | Voir la correction proposée en diff | « Appliquer » remplace le texte en mémoire (jamais sur disque dans le prototype) ; « Ignorer » rejette ; rien ne s'applique seul |
| US-C3 | P0 | Panneau « Calculs » : expressions détectées et recalculées | Chaque expression liste valeur lue / recalculée / écart ; un écart n'apparaît que si le moteur le reproduit |
| US-C4 | P1 | Mode « Expliquer » | Un diagnostic sélectionné affiche cause probable + comment corriger, en français clair |
| US-C5 | P1 | Question sur une portion de code | Sélection + question → réponse ancrée sur la sélection, dans le panneau |
| US-C6 | P2 | Relecture de dossier bornée | Périmètre affiché, fichiers ignorés listés, résultats groupés par fichier |

### Transcription

| # | P | User story | Critères d'acceptation |
|---|---|---|---|
| US-T1 | P0 | Démarrer / pause / arrêter une session | États visibles en permanence ; reprise après pause ; indicateur d'enregistrement constant |
| US-T2 | P0 | Transcription horodatée défilant | Chaque segment porte son horodatage ; `[inaudible]` plutôt qu'invention |
| US-T3 | P0 | Panneau « En direct » | Points clés, définitions, questions s'accumulent, marqués provisoires jusqu'à l'arrêt |
| US-T4 | P1 | Sections et locuteurs | Titres de sections détectés ; locuteurs estimés renommables |
| US-T5 | P1 | Reprise après coupure | Texte déjà transcrit conservé ; reprise proposée, jamais forcée |
| US-T6 | P1 | Corriger et exporter | Édition segment par segment ; export Markdown horodaté |

### Fiche de révision

| # | P | User story | Critères d'acceptation |
|---|---|---|---|
| US-F1 | P0 | Générer une fiche depuis transcription / document / cours | Les 9 sections sont présentes ; chaque bloc porte un badge de provenance |
| US-F2 | P0 | Choisir format et niveau | 5 formats × 3 niveaux ; le changement régénère la structure, pas le contenu source |
| US-F3 | P0 | Éditer : corriger, réordonner, surligner, retirer | État « modifié » visible ; « Revenir à la version générée » possible |
| US-F4 | P1 | Exporter | Markdown et impression/PDF ; la provenance reste visible à l'export |
| US-F5 | P1 | Fidélité vérifiable | Cliquer un bloc « Cours » surligne l'extrait source correspondant |

### Refonte & socle

| # | P | User story | Critères d'acceptation |
|---|---|---|---|
| US-D1 | P0 | Design system v4 appliqué aux 7 écrans | Tokens partagés ; thème clair/sombre ; aucun style local hors tokens |
| US-D2 | P0 | Navigation en espaces | Sidebar groupée, `Ctrl+K`, raccourcis numériques, état conservé |
| US-D3 | P1 | Accessibilité | Clavier complet, focus visible, `prefers-reduced-motion`, contrastes AA |
| US-D4 | P1 | FR/EN sur les prototypes | Sélecteur de langue réel ; chiffres/dates localisés |
| US-D5 | P2 | Données de démo réalistes | Contenu français plausible, pas de lorem ipsum |

---

## 6. Architecture technique

### 6.1 Séparation des couches (les deux prototypes)

```text
UI (écrans, composants)
   ↓ interfaces typées
ViewModels / états (navigation, sessions, édition)
   ↓
Services métier   ── ReviewService, CalcService, TranscriptService, SheetService
   ↓ interfaces
Services IA       ── IInferenceService, ISpeechService  ← simulés en v1
   ↓
Données           ── stores chiffrés (JSON + DPAPI côté WinUI ; fichier local
                     côté RN en attendant l'équivalent sécurisé)
```

Règle absolue : les écrans ne connaissent que des interfaces ; les
implémentations simulées (données de démo, latence imitée, transcript
préenregistré) sont interchangeables avec les implémentations réelles
(LLamaSharp / Whisper) sans retoucher l'UI. **[existant : même principe que
l'app, qui sépare déjà `LocalInferenceClient` / `AdaptiveInferenceClient`.]**

### 6.2 Prototype A — WinUI 3

- Nouveau projet `prototypes/winui/` (solution autonome, .NET 8, Windows App
  SDK 1.8 épinglé comme l'app) pour ne pas risquer l'existant.
- NavigationView groupée par espaces ; thème clair/sombre via ressources ;
  Atkinson Hyperlegible embarquée.
- Services simulés : `SimulatedInference`, `SimulatedSpeech` (transcript
  échantillon), `CalcService` **réel** — la vérification des calculs est
  déterministe et peut être vraie dès le prototype.
- Données de démo chargées depuis `prototypes/winui/Demo/` (JSON lisible).

### 6.3 Prototype B — React Native

- `prototypes/mobile/`, React Native + Expo (lancement sans Android Studio),
  React Navigation, un seul dossier `src/` (screens / components / services /
  theme / demo).
- Même design system traduit en tokens JS (`theme/tokens.ts` généré depuis la
  même source que les tokens CSS/C# si faisable, sinon dupliqué documenté).
- Services simulés identiques en contrat ; démo orientée Études (transcription
  rejouée, fiche éditable) et Code (relecture sur extraits).
- Cible de démonstration : Expo Go sur Android/iOS ; pas de build natif requis.

### 6.4 Sécurité et données

- Aucune donnée sensible en clair : WinUI réutilise `SecureFile` (DPAPI) ;
  RN stocke dans le stockage privé de l'app et documente la limite (pas de
  DPAPI mobile ; chiffrement applicatif en P2).
- Transcriptions et fiches : fichiers JSON chiffrés, export explicite,
  « Tout supprimer » couvre tout.
- Pas de clé API : les services IA simulés n'ont aucun secret. Quand l'IA
  réelle branchera, ce sera le moteur local — aucun secret à gérer.
- Permissions : micro (transcription), capture de fenêtre (Focus, choix
  explicite), fichiers (import). Chacune demandée au moment d'usage, jamais au
  démarrage.
- Coûts IA : zéro coût API ; le coût réel est machine — RAM (16 Go
  conseillés), disque (~12 Go de modèles), temps d'inférence CPU (mesuré :
  analyse image 25–30 s, Whisper 3 s de parole ≈ 4 s). Le dossier le dit ; les
  prototypes simulent la latence de façon réaliste.

---

## 7. Design system v4 « surface chaude »

Formalisation de la direction v3 (maquette verre) validée dans `design/`.

### 7.1 Tokens

- **Fond :** canvas chaud `#ece6de` (clair) / `#0f0e0d` (sombre), avec nappes
  ambiantes floutées (terracotta, sable, sauge) en mouvement très lent —
  désactivées si `prefers-reduced-motion`.
- **Surfaces :** trois niveaux de verre (`glass`, `glass-2`, `glass-3`),
  flou 28 px + saturation 1,7, liseré interne en haut, ombre douce portée.
- **Encre :** `#1a1714` / secondaires `#4f4841`, `#877e74` (clair) ;
  `#f3efe9`, `#cbc4bb`, `#8f877d` (sombre).
- **Accent :** terracotta `#b4482b` (clair) / `#e8825f` (sombre), jamais de
  dégradé bleu-violet. États : `--ok`, `--warn` pour les diagnostics.
- **Typographie :** Atkinson Hyperlegible (interface), Atkinson Hyperlegible
  Mono (chiffres, horodatages, code). Échelle : 11 caption · 12,5 petit ·
  13,5 corps · 16 saisie · 19 sous-titre · 28 titre.
- **Espacements :** grille 4 px ; gouttières 12–14 px ; cartes 16–18 px.
- **Rayons :** 10 (chips), 12–14 (boutons, lignes), 18–22 (cartes, panneaux),
  999 (pilules via le helper Capsule — jamais `CornerRadius="999"` brut en
  WinUI : rendu ellipse).
- **Mouvement :** ressort `cubic-bezier(.2,.9,.25,1.15)`, ease
  `(.2,.8,.2,1)` ; survol = léger soulèvement, pression = scale .98 ;
  rien au-delà de 500 ms ; tout désactivable.
- **Focus :** anneau accent 2 px + offset 2 px, visible partout.

### 7.2 Composants nouveaux

| Composant | Usage |
|---|---|
| `DiagnosticChip` | sévérité + localisation (bloquant accent / important warn / info neutre) |
| `DiffCard` | correction proposée : avant/après, Appliquer · Ignorer · Expliquer |
| `CalcRow` | expression · lue · recalculée · écart · statut |
| `TranscriptRow` | horodatage mono + locuteur + texte éditable |
| `LiveNote` | point clé provisoire du panneau « En direct » |
| `ProvenanceBadge` | `Cours` (plein) / `Explication Nomi` (contour) / `Hypothèse` (pointillés) |
| `SheetSection` | section de fiche : titre, badge, poignée de réordonnancement |
| `SessionBadge` | état d'enregistrement : point animé + libellé + durée mono |
| `Companion` (existant) | colonne suggestions/reprise, reprise telle quelle |

### 7.3 États et règles transverses

- Chaque écran a ses états **vide / chargement / rempli / erreur**, jamais un
  écran blanc sans explication.
- « Simulé » : tout contenu produit par les services simulés porte la mention
  discrète `données de démonstration` dans les prototypes.
- Une action destructrice ou distante n'existe pas ; une action appliquant un
  changement exige toujours un clic explicite (diff, choix, confirmation).
- Les chiffres suivent la préférence mots/proportions/chiffres **[existant]**.

---

## 8. Spécifications des écrans

Maquettes interactives : `design/refonte/nomi-v4-mockup.html` (7 écrans,
clair/sombre, FR). Résumé de structure :

| # | Écran | Structure |
|---|---|---|
| E1 | Accueil | Salutation + date · reprise (« reprendre là où vous étiez ») · aperçu journée (tâche active, fin probable) · 3 raccourcis espaces · suggestion Nomi · activité récente |
| E2 | Code — relecture | Liste des fichiers ouverts · visionneuse avec diagnostics en marge · panneau diagnostic : explication, diff, Appliquer/Ignorer/Expliquer · onglet Calculs · conversation sur sélection |
| E3 | Calculs | Même écran que E2, onglet « Calculs » ouvert : tableau expression / lue / recalculée / écart / statut + filtre |
| E4 | Transcription | Badge session + durée + état · flux horodaté (locuteurs, sections) · panneau En direct (points clés, définitions, questions) · barre Micro/Pause/Arrêter · reprise après coupure |
| E5 | Fiche de révision | Sélecteurs format + niveau · sections ordonnables avec badges de provenance · édition en ligne · rail source (clic bloc → extrait) · export |
| E6 | Bibliothèque | Recherche globale + filtres (cours, fiches, transcriptions, documents) · cartes avec métadonnées · tuile « importer » |
| E7 | Paramètres | Modèles locaux (rôle, taille, état, charger) · langue · lecture (thème, espacement, contraste, animations) · nombres · présence · données (export, tout supprimer) |

Sur mobile (prototype B), la sidebar devient barre inférieure (Accueil,
Studio, Études, Bibliothèque) + « Plus » ; les panneaux latéraux deviennent
des feuilles modales.

---

## 9. Plan de tests et critères de qualité

### 9.1 Vérifications automatiques

- WinUI : compilation Release x64, checks XML (`xml_check.py`), tests du
  moteur de calcul réel (reprendre les cas de `checks/`), absence de NuGet
  vulnérable.
- RN : `tsc` strict, ESLint/Prettier, tests des services simulés et du
  moteur de calcul porté.
- Maquette HTML : chargement sans erreur console, `prefers-reduced-motion`,
  thème sombre, contraste (AA sur les textes).

### 9.2 Vérifications manuelles (à faire sous Windows / appareil)

- Clavier complet sur chaque écran ; Narrator sur les régions principales
  (rappel : un panneau WinUI sans pair UIA doit être enveloppé dans un
  `ContentControl` nommé pour être annoncé).
- Parcours P1–P4 de bout en bout avec les données de démo.
- États vides, erreurs (modèle absent, micro refusé, source illisible) et
  mentions « données de démonstration ».
- Comparatif écran par écran avec les maquettes validées **[existant : règle
  de conformité à la maquette]**.

### 9.3 Critères de qualité

- Zéro action silencieuse détectable dans le code des prototypes.
- Aucun fragment « corrigé » affiché sans diff.
- Aucun bloc de fiche sans badge de provenance.
- Latence simulée crédible (≈ chiffres mesurés) et états de chargement réels.

---

## 10. Risques, limites, questions ouvertes

**Risques.**
- *Diarisation* : la séparation des locuteurs est faible en local → affichée
  comme estimation, jamais comme fait.
- *Longueur des sources* : un cours entier peut dépasser le contexte → fiche
  par chapitre ; règle documentée, pas masquée.
- *Portage RN du moteur de calcul* : le moteur C# est déterministe et testé ;
  sa version TS devra rejouer les mêmes cas pour prétendre « vérifié ».
- *Perception* : trois espaces = app plus grande ; le risque est de diluer le
  compagnon de journée → l'Accueil reste orienté journée.

**Limites assumées (v1).**
- Pas d'exécution de code, pas de compilation dans la relecture.
- Pas d'OCR ni de lecture d'images pour les cours **[existant]**.
- Diarisation estimée ; export PDF via impression.
- Prototype RN : pas de micro réel (transcript rejoué), pas de Focus.

**Questions ouvertes [à valider].**
1. Diarisation : l'estimation suffit-elle en v1 ?
2. Faut-il un espace « Projets » séparé des espaces Journée/Studio/Études,
   ou le Tableau + les fichiers de Code suffisent-ils ? (La mission le nomme ;
   je l'ai plié dans les espaces pour éviter un doublon — à confirmer.)
3. Export des fiches : Markdown + impression suffisent-ils, ou viser DOCX ?
4. La transcription mobile réelle (micro) est-elle souhaitée hors prototype ?

**Prochaines étapes.**
1. Validation de ce dossier et des maquettes (`design/refonte/`).
2. Prototype WinUI 3 (phase 2), puis React Native (phase 3), en sessions
   parallèles si souhaité.
3. Synthèse finale : livré / simulé / décisions / hypothèses.

---

## 11. Synthèse

- **Livré ici :** vision, parcours, analyse de continuité, architecture des
  espaces, spécifications des trois fonctionnalités (entrées, sorties, erreurs,
  garde-fous, fallback), backlog priorisé, design system v4, plan de tests,
  sécurité/données, risques.
- **Livré avec :** maquettes HTML interactives des 7 écrans, clair + sombre.
- **Simulé dans les prototypes :** inférence (LLamaSharp), parole (Whisper),
  diarisation ; le moteur de calcul est réel.
- **Décisions clés :** 3 espaces de navigation ; principe de provenance
  visible ; « Projets » fondu dans les espaces ; mêmes tokens sur les deux
  plateformes.
- **Hypothèses à valider :** les quatre questions du §10 ; l'app existante
  n'est pas modifiée (périmètre validé).
