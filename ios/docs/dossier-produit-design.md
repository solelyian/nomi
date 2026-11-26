# Nomi iOS — dossier produit et design

Version produit : 0.10.6, 25 novembre 2026. Produit de Nyne Technologies.

## 1. Pivot de périmètre

Le périmètre validé pour cette itération est la parité fonctionnelle avec la version Windows de Nomi, **sans la relecture de code**. Les anciens écrans de diagnostics de code ont été retirés du prototype. Les fonctions de transcription et de fiches restent présentes car elles appartiennent au parcours d'apprentissage de Nomi.

Nomi iOS devient un compagnon de journée centré sur :

1. **Aujourd'hui** — orientation, agenda, priorités, routine prête et focus ;
2. **Tableau** — tâches À faire / En cours / Terminé, création en langage naturel et édition ;
3. **Focus** — minuteur explicite avec pause, reprise, fin et notes ;
4. **Nomi** — conversation à fils multiples, avec vérification locale de calculs et réponses générées localement par Gemma 4 après installation du modèle ;
5. **Routines** — automatisations visibles, jamais silencieuses, avec heure, jours, document lié et déclencheurs simples ;
6. **Transcription** — capture de cours en direct avec mode démonstration et chemin Apple Speech ;
7. **Fiches** — transformation de contenu en fiche, flashcards et quiz.

## 2. Principes repris de Windows

- suggestions explicites, confirmées par l'utilisateur, jamais appliquées en silence ;
- calculs sensibles vérifiés par un moteur déterministe local ;
- état d'IA transparent : réel, semi-réel, simulé ou non implémenté ;
- confidentialité locale d'abord, aucun appel réseau dans les mocks ;
- automatisations lisibles, modifiables et désactivables ;
- interface chaleureuse, accessible et adaptée aux sessions courtes.

## 3. Adaptations iOS

| Windows | Adaptation iOS |
|---|---|
| observation du contexte de travail | impossible en continu sous iOS ; Focus conserve objectif, minuteur et notes explicites |
| modèles IA lourds locaux | protocoles remplaçables ; mocks locaux par défaut ; futur serveur intermédiaire ou modèles Apple |
| notifications/routines système profondes | rappels in-app dans le prototype ; notifications locales et App Intents à ajouter en production |
| documents liés au système de fichiers | prototype par titre de document ; intégration Fichiers/Document Picker à venir |
| navigation latérale | cinq onglets iPhone : Aujourd'hui, Tableau, Focus, Nomi, Plus ; NavigationSplitView sur iPad |
| persistance applicative desktop | JSON protégé localement dans le prototype ; SwiftData/iCloud restent à implémenter |

## 4. Structure de navigation

### iPhone

1. **Aujourd'hui** — progression, prochaine action, agenda, priorités, routine prête, focus ;
2. **Tableau** — composer une tâche, filtrer par colonne, éditer et faire progresser ;
3. **Focus** — démarrer, suspendre, reprendre et terminer une session ;
4. **Nomi** — conversation, calculs vérifiés, nouvelles conversations ;
5. **Plus** — Routines, Transcription, Fiches et révisions, Réglages.

### iPad

`NavigationSplitView` affiche la liste complète : Aujourd'hui, Tableau, Focus, Nomi, Routines, Transcription, Fiches, Réglages.

## 5. Spécifications des espaces

### Aujourd'hui

```text
Date et progression de journée
Prochaine action recommandée
Prochain événement
Priorités du jour
Routine prête
Focus en cours ou bouton de démarrage
```

Objectif : répondre en moins de cinq secondes à « que dois-je faire maintenant ? ». Les recommandations actuelles sont déterministes, pas générées par IA.

### Tableau

```text
Saisie langage naturel
[Ex. Envoyer le compte rendu demain 30 min important]
Picker À faire / En cours / Terminé
Carte tâche : priorité, durée, date, notes, actions
Menu : modifier, déplacer, diviser, focus, supprimer
```

Le parseur local comprend `aujourd'hui`, `demain`, `ce soir`, `urgent`, `important`, `30 min` et `2 heures`. Il ne prétend pas être une compréhension complète du langage naturel.

### Focus

```text
Objectif
Minuteur actif
Pause / Reprendre / Terminer
Notes de fin
Historique récent
```

iOS ne permet pas à une app tierce d'observer continuellement les autres applications comme sur Windows. Le minuteur, l'objectif et les notes sont réels ; l'analyse contextuelle est non implémentée.

### Nomi

```text
Fils de conversation
Messages utilisateur/assistant
Badge de vérification pour les calculs
Champ de réponse
Nouvelle conversation
```

Réel : structure conversationnelle, fils, persistance locale, vérification locale des calculs simples. Simulé : réponses génératives de planification et d'aide. Aucun appel réseau n'est effectué.

### Routines

```text
Création en langage naturel
Routine prête maintenant
Cartes : action, déclencheur, heure, activation, document lié
Modifier : nom, action, déclencheur, heure, jours, document
Exécuter / Plus tard / Ignorer / Supprimer
```

Le parseur local comprend matin, soir, chaque jour, week-end, un jour précis et une heure simple. Les routines liées à la fin d'une tâche ou d'un focus peuvent créer une tâche, mais l'exécution en arrière-plan n'est pas garantie dans ce prototype.

### Transcription

Le mode démonstration produit des segments horodatés sans permission et fonctionne sur simulateur. Le chemin microphone utilise `SFSpeechRecognizer` avec permissions explicites et reconnaissance sur appareil lorsque disponible. Il doit être validé sur iPhone physique. La diarisation fiable des locuteurs n'est pas implémentée.

### Fiches et révisions

Réel : bibliothèque, création depuis texte ou transcription, affichage des sections, flashcards, quiz interactif, export par feuille de partage, persistance locale. La génération utilise Gemma 4 localement quand le modèle est installé ; sinon un repli extractif hors-ligne est utilisé. Les sections affichent leur fidélité (`source`, `explication IA`, `hypothèse`) pour éviter de présenter une invention comme un fait du cours.

## 6. Design system — direction « Nomi Aura × Liquid Glass »

La refonte visuelle validée est **Nomi Aura**, unifiée dans cette itération avec **Liquid Glass** : une interface chaleureuse, vivante et tactile qui privilégie les composants natifs iOS 26 et dégrade proprement les effets sur les versions antérieures.

Tokens et composants actuels dans `NomiTheme` :

- fond porcelaine chaud (`#F4EEE7`) avec variations sombres dynamiques ;
- arrière-plan ambiant organique composé de halos corail, violet et turquoise floutés ;
- cartes en verre chaud : material natif ou `glassEffect` progressif, gradient translucide, bordure lumineuse, ombre douce et liseré contextuel ;
- hiérarchie typographique arrondie, grands titres explicites et métadonnées moins contrastées ;
- accent corail pour l'action principale, violet pour Focus/Nomi, turquoise pour transcription/apprentissage, vert pour routines et réussites ;
- boutons capsule primaires/secondaires avec ressort tactile et cibles d'au moins 44 pt ;
- barre d'onglets native Liquid Glass sur iPhone ; barre latérale native translucide sur iPad ;
- Focus comme objet central vivant : anneau animé en dégradé conique autour du minuteur ;
- iPad en workspace multi-colonnes au lieu d'une simple colonne iPhone élargie.

Règles : Dynamic Type, zones tactiles ≥ 44 pt, contrastes système, pas de signification portée uniquement par la couleur, effets visuels dégradables par les matériaux natifs. La préférence « Réduire les animations » est exposée dans Réglages, mais son application systématique à toutes les animations reste à auditer.

## 7. Données et confidentialité

Les tâches, routines, sessions Focus, conversations et fiches sont persistées dans un fichier JSON local du répertoire Application Support, écrit avec l'option de protection complète des fichiers iOS. SwiftData, migration de schéma, export/effacement utilisateur et chiffrement applicatif restent à concevoir avant diffusion App Store.

Aucune clé API n'est embarquée et aucun mock n'effectue d'appel réseau. Une future IA distante doit passer par un serveur intermédiaire, avec consentement explicite, journal minimal, quotas et suppression.

## 8. État réel / simulé / non implémenté

| Fonctionnalité | État actuel |
|---|---|
| Onboarding et navigation iPhone/iPad | réel |
| Tableau de tâches, édition et progression | réel |
| Ajout de tâche en langage naturel simple | réel mais limité |
| Persistance locale JSON protégée | réel, solution prototype |
| Focus minuteur/pause/reprise/fin/notes | réel |
| Calculs vérifiés dans Nomi | réel pour expressions simples |
| Fils de conversation et persistance | réel |
| Réponses IA conversationnelles | réelles avec Gemma 4 local après installation ; impossible sans modèle |
| Routines : parseur simple, heure, jours, activation | réel mais limité |
| Routines déclenchées par fin de tâche/focus | réel tant que l'app est ouverte |
| Documents liés | simulé par titre, sans Document Picker |
| Sauvetage de journée intelligent | sauvetage déterministe implémenté ; IA non implémentée |
| Notifications locales, App Intents, widgets, Live Activity | non implémentés |
| Transcription démonstration | réel localement |
| Apple Speech microphone | semi-réel : compilé, non validé sur appareil physique |
| Génération de fiches | réelle avec Gemma 4 local après installation ; repli extractif hors-ligne si indisponible/en échec |
| Flashcards/quiz/export | réel ; qualité dépendante de la source et de Gemma 4 |
| SwiftData, iCloud, extension de partage, Spotlight | non implémentés |
| Diarisation fiable et résumé live | non implémentés |

## 9. Tests et vérification

Vérifié dans cette itération :

- compilation iOS Debug réussie ;
- 30 tests unitaires NomiCore réussis ;
- réponse Gemma 4 E2B réelle en français, avec streaming, sur iPhone 17 Simulator ;
- génération de fiche Gemma avec sections, flashcards et quiz sur simulateur ;
- rendu Markdown inline sans astérisques bruts ;
- tests du parseur de tâches, du parseur de routines, de l'échéance des routines et du vérificateur de calculs.

Non vérifié ou insuffisant :

- permissions micro et reconnaissance sur iPhone physique ;
- VoiceOver/Dynamic Type en audit complet ;
- XCUITest automatisé ;
- endurance mémoire/batterie pendant transcription longue ;
- exactitude visuelle écran par écran sur toutes les tailles iPhone/iPad ; une passe manuelle iPhone/iPad a été faite sur simulateur, sans audit exhaustif des tailles et orientations.

## 10. Prochaines étapes recommandées

1. valider les parcours iPhone/iPad sur captures ou appareil ;
2. remplacer la persistance JSON par SwiftData avec migration ;
3. ajouter notifications locales et rappels de routines ;
4. brancher Document Picker et l'extension de partage ;
5. créer un vrai moteur conversationnel derrière protocole, local ou serveur intermédiaire ;
6. ajouter sauvetage de journée déterministe puis IA ;
7. exécuter audit VoiceOver/Dynamic Type et XCUITest ;
8. valider Apple Speech sur appareil physique avant toute promesse de transcription réelle.
