# Nomi — parcours de travail à valider

Produit de Nyne Technologies. Prototype d’espace de productivité, en français et en anglais.

## Architecture proposée

```text
Nomi
├── Votre espace — saisir et lancer sans changer de page
│   ├── Intention : Comprendre / Vérifier / Convertir / Planifier / Communiquer / Apprendre
│   ├── Demande libre — exemple visible dans la saisie vide
│   ├── Fichiers joints : PDF, Word, Excel, PowerPoint, texte — extrait local, retrait un par un
│   ├── Format : étapes / synthèse, puis Lancer / Arrêter
│   ├── Réponse adaptée dans le même espace, sélection et copie
│   ├── Exemples prêts à charger : remise / heure de travail / chapitre
│   └── Reprendre les espaces déjà utilisés pendant cette session
├── Espaces de travail
│   ├── Développement logiciel → Vérifier
│   ├── Études supérieures → Apprendre
│   ├── Vente & commerce → Convertir
│   └── Travail du savoir → Comprendre
└── Préférences
    ├── Langue — français / anglais
    ├── Densité de lecture — aérée / compacte
    └── Réponses — étapes / synthèse

Palette de commandes — Ctrl+K, accessible sur chaque écran

Chaque espace métier ou action ouverte depuis la palette
├── Retour à l’accueil
├── Intention et contexte
├── Demande libre, avec exemple facultatif
├── Fichiers joints, lus localement, extrait visible avant envoi
├── Format et Lancer / Arrêter au contact de la demande
├── Préparation et adaptation de la réponse en mémoire
├── Réponse adaptée, copie et retour à la saisie
└── Détails dépliables : règles appliquées, modèle et connexion
```

La palette de commandes est accessible depuis chaque écran. Elle propose les six
actions et les quatre contextes, avec filtrage textuel. L’accueil permet de
formuler une demande immédiatement. Comprendre est présélectionné ; choisir une
autre intention conserve le texte saisi. Charger un exemple ne lance pas
l’inférence : la demande reste modifiable avant envoi.

## Décisions à valider

- Un espace de travail applicatif, avec navigation persistante.
- La saisie est l’entrée principale ; les six verbes sont des choix compacts
  dans le même espace. Les métiers sont des points de départ facultatifs.
  Aucun questionnaire de profil n’est imposé.
- Une seule surface de travail commune aux six actions.
- Dans l’aperçu, demande et réponse apparaissent côte à côte sur écran large,
  puis l’une sous l’autre sur écran plus étroit. WinUI conserve un flux vertical
  natif. Copier affiche une confirmation discrète, sans fenêtre à fermer.
- Les préférences décrivent la manière de travailler : langue, densité, déroulé.

## Inférence locale

```text
WinUI → OllamaClient C# → Ollama local → Qwen3 4B Instruct
Aperçu web → /api/chat (Node) → Ollama local → Qwen3 4B Instruct
                             ↑
                content/inference.json
                consignes communes
```

Le modèle ouvert est `qwen3:4b-instruct` (Apache 2.0), installé via Ollama.
Pas de clé API, d’abonnement ou d’inférence cloud. Dans l’aperçu Devin, le modèle
tourne sur la machine de session ; l’installation Windows l’exécute sur votre PC.
Le catalogue d’interface reste éditorial ; les réponses utilisent les consignes
de l’action, du format et de la langue choisie.

Le serveur valide les entrées, borne leur taille et ne relaie que vers une adresse
locale. Une génération à la fois, avec annulation lors de la fermeture de la
connexion. Le client WinUI appelle Ollama directement.

Après génération, la réponse passe par une politique déterministe commune :

```text
Qwen → brouillon complet en mémoire → règles Nomi → texte adapté → affichage/copie
                                     ↑
                         content/response-policy.json
                         version et règles communes
```

Le serveur web utilise `server/response-policy.js` ; le client C# utilise
`ResponsePolicy.Apply` via `OllamaClient.GenerateNomiAsync`. Les règles et les
cas de test avant/après sont partagés. Aucune réponse brute n’est envoyée au
navigateur. WinUI attend aussi le résultat adapté avant de remplir la zone de texte.

La politique simplifie la mise en forme, retire certaines formules familières
et aère les étapes. Elle conserve la séquence des nombres et ne convertit pas
les unités. Des formulations ciblées de demande de diagnostic, de cadrage médical,
de pitié ou de code entraînent le rejet de la réponse entière. Le texte n’est
jamais tronqué silencieusement pour respecter une longueur. Erreurs, arrêt et
troncature du modèle n’exposent aucun fragment.
Une règle compare aussi un montant monétaire isolé en tête avec les conclusions
explicites de prix ou de total reconnues dans le catalogue. Un désaccord dans la
même monnaie bloque le texte entier ; les calculs ne sont pas réécrits.
Dans ce seul cas, une nouvelle génération est tentée une fois avec la demande
originale et une consigne de cohérence partagée. Le nouveau brouillon repasse par
la politique complète. Un nouvel échec est bloqué ; le délai global et l’annulation
restent applicables. Une réponse régénérée est signalée dans l’interface.

Le résultat porte une version de politique et la liste des transformations ;
l’interface affiche les transformations dans la langue choisie. Ces règles
textuelles ne constituent pas une garantie sémantique ni arithmétique.

Les changements de route ou de langue arrêtent la génération. Les brouillons et
réponses restent en mémoire par action, contexte et langue ; l’accueil conserve
sa propre demande par langue. Fermer ou recharger l’aperçu efface ces brouillons.
Ils ne constituent pas un historique de conversations. L’interface annonce
les états de génération sans lire chaque fragment de texte aux lecteurs d’écran.

## Limites de cette version

Chaque requête est indépendante. Les fichiers joints sont réduits à un extrait
texte local (pas d’OCR ni de lecture d’images) ; pas encore de transcription
audio ou de synchronisation. Le parcours logiciel n’édite ni n’exécute de code.
Le format par étapes demande de poser les calculs ou hypothèses avant une
conclusion unique, pour éviter une annonce prématurée. La LLM peut commettre
des erreurs : il n’y a pas encore de moteur de vérification mathématique déterministe.

L’aperçu navigateur est un support de validation de l’architecture. L’application
cible utilise WinUI 3 et des contrôles natifs, pas une WebView. Les deux partagent
le même catalogue de textes français–anglais.

## Présentation et accessibilité

Première direction sobre : fonds clairs, texte sombre, accent olive, typographie
système, respiration et séparation par filets. La finition visuelle attend la
validation de l’architecture.

Dans l’aperçu : régions sémantiques, lien d’évitement, focus visible, dialogue
natif pour la palette, restitution du focus, navigation clavier, langue du
document mise à jour, affichage compact et mode de contraste forcé.

Dans WinUI : NavigationView adaptative, boutons et sélecteurs natifs, libellés
UI Automation, titres structurés, retour du focus et ressources de thème système.
La validation réelle au clavier, avec Narrator et en contraste élevé doit être
effectuée sous Windows.

Les préférences de l’aperçu sont conservées localement. Celles du squelette
natif restent en mémoire pendant la session.

## État des vérifications

L’environnement de développement est Ubuntu. La compilation XAML, l’exécution
WinUI et les vérifications d’interface Windows restent à faire sur Windows.

Contrôles exécutés avec succès :

- ESLint, Prettier et TypeScript strict sur l’aperçu et le serveur.
- 62 tests Node : catalogues, navigation, intentions et brouillons de l’accueil,
  contraste, validation, relais HTTP,
  streaming UTF-8, erreurs, concurrence, annulation et politique de réponse.
- Trente et un cas avant/après partagés par les implémentations JavaScript et C#,
  dont une contradiction monétaire observée avec le vrai modèle.
- Contrôles .NET du transport et du parcours avec adaptation, compilés indépendamment de WinUI.
- Cas réel C# de remise : génération, politique et résultat de 68 € validés
  après avoir demandé les calculs avant la conclusion.
- Six demandes réelles via le serveur et Ollama, couvrant les six actions en
  français et en anglais. Le temps dépend du chargement du modèle et du CPU.
- XML des sources WinUI et présence des gestionnaires d’événements.
- Analyse syntaxique C# avec Tree-sitter, sans compilation.
- Ruff sur le script de contrôle XML.
- Réponses HTTP 200 de l’aperçu et des deux catalogues de langues.

L’affichage initial de l’accueil a été inspecté visuellement dans Chrome.
Les parcours interactifs, les lecteurs d’écran et l’application Windows n’ont
pas été testés. Ces contrôles ne remplacent pas une validation native.
