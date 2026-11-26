# Nomi Command — design language

Version 1.0 · iOS / iPadOS · document de référence produit et design  
Statut : validé comme référence de conception avant maquettes détaillées et implémentation

## Sommaire

1. Intention et références
2. Principes directeurs
3. Modèle d’expérience et signatures produit
4. Ton de marque
5. Typographie
6. Couleurs
7. Grille, espacement, densité et layouts
8. Formes, rayons, bordures et mode sombre
9. Matériaux et Liquid Glass
10. Iconographie
11. Composants fondamentaux et spécifications
12. Commande universelle
13. Navigation et interactions
14. Motion, haptique et feedback
15. Accessibilité
16. Écriture, voix et formats de données
17. Directions par écran
18. États obligatoires
19. Critères de design review
20. Icône d’application
21. Processus de conception
22. Correspondance SwiftUI
23. Décisions ouvertes
24. Annexes opérationnelles
25. Exigence finale

---

## 1. Intention

Nomi n’est pas un dashboard de productivité décoratif. C’est un **outil de commande personnel** qui aide à décider, agir, se concentrer, transcrire et réviser sans disperser l’attention.

La direction **Nomi Command** combine quatre exigences inspirées des références choisies :

- **Raycast** : rapidité, commande, clavier, actions directes, densité maîtrisée ;
- **Linear** : précision, netteté, listes excellentes, métadonnées discrètes, absence de bruit ;
- **Claude** : chaleur calme, conversation invitante, intelligence présente sans théâtre ;
- **Notion** : structure documentaire, contenu lisible, hiérarchie flexible.

Nomi ne copie aucune de ces interfaces. Le langage retient leurs disciplines communes : une typographie forte, une action colorée rare, des surfaces sobres, une interaction rapide et une hiérarchie évidente.

### 1.1 Résultat recherché

L’utilisateur doit ressentir :

1. **Je comprends immédiatement quoi faire.**
2. **L’interface ne cherche pas à m’impressionner ; elle m’aide.**
3. **Chaque action est rapide, réversible ou clairement confirmée.**
4. **Nomi est intelligent mais reste à sa place.**
5. **L’application semble construite par une équipe qui a réglé chaque détail.**

### 1.2 Anti-objectifs

Nomi ne doit pas ressembler à :

- une collection de cartes arrondies interchangeable ;
- une démonstration de Liquid Glass ;
- un clone visuel d’une autre application ;
- un tableau de bord analytique grand public ;
- une interface futuriste artificielle ;
- un prototype dont chaque élément possède la même importance.


### 1.3 Références étudiées

Les références servent de calibration, pas de source de copie :

- [Apple Human Interface Guidelines — Materials](https://developer.apple.com/design/human-interface-guidelines/materials) : Liquid Glass forme une couche fonctionnelle pour les contrôles et la navigation ; le contenu doit utiliser des matériaux standard.
- [Apple Human Interface Guidelines — Layout](https://developer.apple.com/design/human-interface-guidelines/layout) : hiérarchie, alignement, groupement et progressive disclosure avant décor.
- Raycast : vitesse, palette, densité, clavier, résultats actionnables.
- Linear : netteté des listes, précision des états, création rapide, discipline visuelle.
- Claude : entrée directe dans la conversation, salutation centrée, chaleur retenue, composer excellent.
- Notion : structure documentaire, lisibilité, contenu modulaire, hiérarchie simple.
- Aside : collecte locale, objets personnels conservés, intelligence présentée comme facultative.

Toute ressemblance directe trop visible doit être corrigée : l’objectif est d’atteindre leur niveau de finition, pas de produire une copie.

---

## 2. Principes directeurs

### 2.1 Le contenu est l’interface

Le contenu — tâche, message, transcription, fiche, objectif — doit dominer. Les conteneurs servent la lecture et l’action, jamais l’inverse.

- Préférer une rangée bien alignée à une carte décorative.
- Utiliser une carte uniquement lorsqu’un objet possède une identité propre et plusieurs actions.
- Éviter les empilements de cartes visuellement identiques.
- Ne pas ajouter d’icône lorsque le texte suffit.

### 2.2 Une action principale à la fois

Chaque écran possède une intention dominante. Les actions secondaires existent, mais ne rivalisent pas visuellement.

- Une action primaire visible par zone de lecture.
- Les actions destructrices ne sont jamais les plus visibles.
- Les recommandations de Nomi sont proposées, pas imposées.
- L’utilisateur garde le contrôle final.

### 2.3 La vitesse est une qualité visuelle

Une interface rapide semble plus professionnelle. La vitesse se traduit par :

- peu de niveaux de navigation ;
- une commande universelle ;
- des formulaires courts ;
- des valeurs par défaut intelligentes ;
- des listes scannables ;
- des raccourcis clavier sur iPad et clavier externe ;
- des animations brèves qui ne retardent pas l’action.

### 2.4 L’intelligence est humble

Nomi propose, explique et vérifie. Il ne doit pas théâtraliser sa présence.

- Pas de mascotte permanente.
- Pas de grand halo animé sans fonction.
- Les réponses indiquent leur provenance lorsque cela compte.
- Les suggestions sont compactes et faciles à ignorer.
- Les états « local », « simulé », « indisponible » ou « non implémenté » sont honnêtes.

### 2.5 La rareté crée la qualité

La couleur, la profondeur, les ombres, les badges et les animations perdent leur valeur si tout les utilise.

- Un seul accent d’action.
- Peu de couleurs fonctionnelles.
- Peu d’ombres.
- Peu de surfaces translucides dans le contenu.
- Des badges seulement lorsque l’état change une décision.

### 2.6 Le mouvement explique

Le mouvement n’est pas un ornement. Il décrit ce qui vient de changer.

- Une tâche terminée se replie ou se déplace avec clarté.
- La palette apparaît comme une couche temporaire au-dessus du contexte.
- Un focus actif respire très légèrement, sans distraire.
- Le streaming de Nomi reste typographique et stable.

---

## 3. Modèle d’expérience

### 3.1 La boucle principale

Nomi organise une boucle simple :

1. **Comprendre** : voir la journée, les contraintes, les priorités.
2. **Choisir** : accepter, modifier ou reporter la prochaine action.
3. **Agir** : travailler, démarrer un focus, demander à Nomi, transcrire.
4. **Conserver** : transformer l’activité en tâche, note, transcription ou fiche.
5. **Revenir** : réviser, reprendre, ajuster.

L’interface doit rendre cette boucle visible sans l’expliquer constamment.

### 3.2 La commande universelle

La **commande universelle** est la colonne vertébrale de l’expérience. Elle permet de :

- créer une tâche ;
- démarrer un focus ;
- poser une question à Nomi ;
- chercher une fiche ;
- démarrer une transcription ;
- ouvrir un écran ;
- exécuter une action contextuelle.

Elle doit être accessible depuis :

- le champ de commande visible sur les écrans principaux ;
- le raccourci `⌘K` sur iPad ou clavier externe ;
- un bouton stable dans les outils quand le contexte est dense.

Elle ne remplace pas la navigation ; elle accélère les actions fréquentes.

### 3.3 Objets de premier rang

Le design traite ces objets comme des entités cohérentes dans toute l’application :

| Objet | Identité visuelle dominante | Action rapide attendue |
|---|---|---|
| Tâche | rangée compacte, case, titre, métadonnées | commencer, terminer, déplacer |
| Focus | temps mono, progression discrète, état clair | pause, reprise, terminer |
| Message | texte avant tout, provenance discrète | copier, régénérer, transformer |
| Transcription | chronologie, locuteur, état d’enregistrement | pause, arrêter, exporter |
| Fiche | structure documentaire, sections, quiz | réviser, exporter, régénérer |
| Routine | règle lisible en langage naturel | activer, modifier, reporter |

### 3.4 Signatures produit

Cinq signatures doivent rendre Nomi reconnaissable sans décor superficiel :

1. **La commande universelle** — un champ stable qui transforme l’intention en action.
2. **La prochaine action** — une recommandation unique, courte et contrôlable.
3. **La voix éditoriale** — serif chaleureux pour les moments humains, jamais pour tout l’écran.
4. **Le temps mono** — durées et horaires en SF Mono, précis comme un instrument.
5. **La preuve locale** — les états Gemma, transcription et données indiquent clairement ce qui reste sur l’appareil.

Si une maquette ne montre aucune de ces signatures, elle peut être n’importe quelle application de productivité et doit être retravaillée.

### 3.5 Couches de l’interface

L’interface utilise quatre couches conceptuelles :

| Couche | Contenu | Traitement |
|---|---|---|
| Canvas | fond et structure de page | mat, stable, sans effet |
| Contenu | listes, documents, messages | surfaces opaques, bordures fines |
| Action | boutons, inputs, commandes | contraste supérieur, parfois accent |
| Navigation | tab bar, sidebar, palette, composer | matériaux système, Liquid Glass autorisé |

Une couche ne doit jamais imiter la couche supérieure. En particulier, le contenu ne doit pas ressembler à une navigation flottante.

### 3.6 Parcours critiques

Les parcours suivants servent de tests de cohérence pour toutes les maquettes.

#### Capturer puis traiter une tâche

1. L’utilisateur ouvre la commande et saisit une intention.
2. La tâche est créée avec une interprétation visible.
3. Elle apparaît dans Aujourd’hui ou Tableau selon l’échéance.
4. L’utilisateur peut la corriger avant de la lancer.
5. Le focus ou la complétion transforme l’intention en résultat.

Règle : la création ne doit pas exiger plus d’un champ obligatoire.

#### Démarrer un focus utile

1. L’utilisateur voit la prochaine action ou choisit une tâche.
2. L’objectif est visible avant le démarrage.
3. Le temps démarre sans écran intermédiaire inutile.
4. Pause, reprise et fin restent accessibles en permanence.
5. À la fin, Nomi propose de noter, terminer ou prolonger.

Règle : démarrer un focus depuis une tâche ne devrait pas demander plus de deux interactions.

#### Transcrire puis réviser

1. L’utilisateur comprend que le consentement est requis pour cette session.
2. L’enregistrement montre clairement son état.
3. Les segments sont conservés si l’écran est quitté selon les limites réelles.
4. Après arrêt, la transcription peut être exportée ou transformée en fiche.
5. La fiche indique sa provenance et ses limites.

Règle : aucun enregistrement ne doit sembler continuer silencieusement.

#### Demander puis transformer une réponse

1. Le composer est visible immédiatement.
2. La réponse indique implicitement ou explicitement son mode de génération lorsque nécessaire.
3. Les actions utiles apparaissent après la réponse : copier, créer une tâche, créer une fiche, vérifier un calcul.
4. L’action transforme la réponse en objet persistant sans copier-coller manuel si possible.

Règle : la conversation n’est pas une impasse ; elle doit pouvoir devenir action ou document.

#### Installer Gemma local

1. L’utilisateur comprend la taille du modèle et l’espace requis.
2. Le téléchargement montre progression, reprise et annulation.
3. La vérification de l’intégrité est visible.
4. Le modèle prêt est distingué du modèle seulement téléchargé.
5. Les erreurs expliquent ce qui est conservé.

Règle : aucune barre de progression ne doit ressembler à une génération en cours si elle représente un téléchargement.

### 3.7 Barre de qualité mesurable

Les maquettes et l’implémentation doivent viser ces seuils :

| Critère | Seuil cible |
|---|---|
| créer une tâche simple | moins de 5 secondes après ouverture de la commande |
| démarrer un focus depuis la prochaine action | maximum 2 interactions |
| retrouver la transcription active | maximum 2 interactions |
| ouvrir la commande | 1 tap ou `⌘K` |
| comprendre l’action principale d’un écran | moins d’une seconde |
| fermer une couche temporaire | `Échap`, tap outside ou bouton visible |
| erreur récupérable | action de reprise visible sans chercher dans Réglages |

Ces seuils sont des intentions de design, pas encore des mesures validées. Ils devront être testés sur maquette puis sur prototype.

---

## 4. Ton de marque

### 4.1 Adjectifs

Nomi est :

- précis ;
- calme ;
- chaleureux sans être mignon ;
- dense sans être froid ;
- intelligent sans être arrogant ;
- local et digne de confiance.

Nomi n’est pas :

- playful au sens enfantin ;
- corporate au sens bureaucratique ;
- futuriste ;
- spectaculaire ;
- gadget.

### 4.2 Signe distinctif

Le signe de marque est le logotype abrégé **`n.`**.

- `n` bas de casse, simple et stable ;
- point final corail ;
- le point peut devenir un indicateur d’état ou de présence, mais avec extrême retenue ;
- ne pas transformer le point en personnage ni en orbite décorative permanente.

Le signe ne doit pas être plaqué partout. Il apparaît dans l’en-tête, l’icône, les états vides importants et l’onboarding.

---

## 5. Typographie

La typographie est le principal vecteur d’identité. Elle doit créer la personnalité sans illustration superflue.

### 5.1 Familles

| Rôle | Famille Apple | Usage |
|---|---|---|
| Éditorial | New York / `.serif` | accueils, titres humains, conversation à vide, moments de respiration |
| Interface | SF Pro | navigation, listes, formulaires, boutons, contenu fonctionnel |
| Technique | SF Mono | durées, horodatages, identifiants, états chiffrés, raccourcis |

Le serif n’est pas un effet décoratif. Il marque les moments où Nomi s’adresse à la personne. Les écrans de travail dense restent en sans-serif.

### 5.2 Échelle iPhone

| Style | Taille / interligne | Graisse | Usage |
|---|---:|---|---|
| Display serif | 34 / 38 | regular/medium | Bonsoir Ian, accueil conversation |
| Title 1 serif | 28 / 33 | regular/medium | titres de sections importantes |
| Title 2 | 22 / 27 | semibold | titre de panneau ou objectif principal |
| Headline | 17 / 22 | semibold | titre de tâche, objet important |
| Body | 15 / 21 | regular | contenu principal mobile |
| Callout | 13 / 18 | regular/medium | métadonnées, descriptions courtes |
| Caption | 11 / 14 | medium | labels, états, informations secondaires |
| Mono data | 11–17 / stable | regular | heure, durée, compteur, raccourci |

### 5.3 Règles

- Ne pas utiliser plus de trois niveaux de titre dans un même écran.
- Les titres serif n’excèdent pas deux lignes sur iPhone.
- Le corps de texte ne descend pas sous 13 pt hors métadonnées.
- Les métadonnées mono restent lisibles, jamais purement décoratives.
- Éviter le gras partout : le gras signale la structure, pas l’importance de chaque mot.

### 5.4 Hiérarchie typographique

| Niveau | Question à laquelle il répond | Style recommandé |
|---|---|---|
| Moment humain | « Où suis-je ? Que se passe-t-il ? » | serif display/title |
| Objet principal | « Sur quoi dois-je agir ? » | sans headline/title 2 |
| Contenu | « Que dois-je lire ? » | sans body |
| Métadonnée | « Quand, combien, quel état ? » | mono ou caption |
| Action | « Que puis-je faire ? » | sans medium/semibold |

Cette séparation évite l’uniformité visuelle : le titre éditorial ne doit pas concurrencer le titre de la tâche, et les métadonnées ne doivent pas ressembler à des actions.

### 5.5 Dynamic Type

- Tous les textes suivent Dynamic Type.
- Les tailles accessibilité peuvent passer en disposition verticale.
- Une rangée dense devient deux lignes plutôt qu’un texte tronqué.
- Les actions adjacentes passent en colonne si nécessaire.
- Les titres serif peuvent être remplacés par sans-serif en très grande taille si la lisibilité l’exige.

---

## 6. Couleurs

### 6.1 Philosophie

La palette est chaude, calme et restrictive. Le corail est réservé à l’action ou à l’attention réellement importante. Les surfaces ne doivent pas devenir orange, violettes ou vertes sans raison fonctionnelle.

### 6.2 Tokens de base

| Token | Clair | Sombre | Usage |
|---|---|---|---|
| `background` | `#F6F1E9` | `#11100F` | fond principal |
| `backgroundSecondary` | `#EEE7DC` | `#181614` | zones groupées, alternance douce |
| `surface` | `#FFFDFA` | `#1C1A18` | contenu, panneaux |
| `surfaceRaised` | `#FFFFFF` | `#26221F` | palette, sheets, éléments élevés |
| `textPrimary` | `#191512` | `#F2EDE4` | texte principal |
| `textSecondary` | `#716961` | `#A99E90` | descriptions, métadonnées |
| `textTertiary` | `#A0968C` | `#6F675E` | placeholders, indices |
| `separator` | `rgba(36,27,20,0.11)` | `rgba(243,237,227,0.12)` | lignes fines |
| `separatorStrong` | `rgba(36,27,20,0.18)` | `rgba(243,237,227,0.20)` | bordures de contrôle |

### 6.3 Accent et statuts

| Token | Clair | Sombre | Usage |
|---|---|---|---|
| `accent` | `#BF472C` | `#E47B5D` | action accentuée, sélection, indicateur actif |
| `accentText` | `#9E3F2A` | `#F0A06F` | texte ou petit label accentué |
| `accentSoft` | `#F2DED6` | `#3A241E` | fond très discret d’état actif |
| `success` | `#557C68` | `#85AE9A` | succès, vérifié |
| `warning` | `#A66B22` | `#D6A35A` | attention non destructive |
| `danger` | `#B33A31` | `#E06B60` | destruction, erreur importante |
| `focus` | `#5F707D` | `#9AAEBB` | concentration, état neutre actif |
| `intelligence` | `#6F6256` | `#C8B79F` | provenance IA, présence Nomi |

### 6.4 Règles d’usage

- Le bouton primaire ordinaire utilise `textPrimary` comme fond en clair et `textPrimary` adapté en sombre, pas systématiquement l’accent.
- L’accent est réservé aux recommandations importantes, états actifs, liens contextuels et actions accentuées.
- Les textes sur fond accent doivent respecter un contraste AA ; si nécessaire utiliser de l’encre très sombre plutôt que du blanc.
- Les couleurs de statut ne servent pas à colorer des sections entières.
- Les badges colorés sont petits, discrets et justifiés.

### 6.5 Contraste

- Texte normal : minimum 4,5:1.
- Texte large ou gras : minimum 3:1.
- Icônes et contrôles significatifs : minimum 3:1.
- Les placeholders ne sont jamais le seul vecteur d’information.

Contrôles indicatifs calculés sur les tokens proposés :

| Combinaison | Ratio approximatif | Usage autorisé |
|---|---:|---|
| `textPrimary` sur `background` clair | 16,1:1 | tout texte |
| `textSecondary` sur `background` clair | 4,8:1 | texte secondaire |
| `accentText` sur `background` clair | 5,8:1 | labels accentués |
| `accent` sur `surface` clair | 5,0:1 | accent, contrôles et texte suffisamment contrasté |
| `textSecondary` sur `background` sombre | 7,2:1 | texte secondaire |
| `accent` sombre sur `background` sombre | 6,6:1 | accent et contrôles |

`textTertiary` reste réservé aux placeholders, raccourcis et informations non essentielles. Si le texte devient nécessaire à la compréhension ou à l’action, il doit passer en `textSecondary`.

### 6.6 Attribution sémantique

La couleur ne doit pas être choisie écran par écran. Elle dépend du rôle.

| Besoin | Token à utiliser | Ne pas utiliser |
|---|---|---|
| action principale neutre | `textPrimary` ou surface inverse | `accent` systématique |
| recommandation importante | `accent` | `warning` |
| sélection/navigation active | `accent` ou `accentSoft` | couleurs multiples |
| calcul vérifié / réussite | `success` | `accent` |
| attention récupérable | `warning` | `danger` |
| destruction/perte | `danger` | `warning` |
| état focus neutre | `focus` | `accent` permanent |
| provenance IA/local | `intelligence` | couleur de marque partout |
| erreur de saisie | `danger` + texte explicite | contour rouge seul |
| information ordinaire | `textSecondary` | couleur décorative |

Une couleur sémantique ne doit pas changer de signification selon l’écran. Si `warning` signifie « attention récupérable » dans Transcription, il ne peut pas devenir « priorité haute » dans Tableau.

---

## 7. Grille, espacement et densité

### 7.1 Unité

L’unité de base est **4 pt**.

| Token | Valeur | Usage |
|---|---:|---|
| `space1` | 4 | micro-ajustements, séparation inline |
| `space2` | 8 | espaces internes compacts |
| `space3` | 12 | espaces de rangées et groupes proches |
| `space4` | 16 | padding standard |
| `space5` | 20 | marges compactes, grands groupes |
| `space6` | 24 | sections importantes |
| `space8` | 32 | respiration majeure |
| `space10` | 40 | transition de chapitre visuel |

### 7.2 Marges

- iPhone portrait : 18–20 pt.
- iPhone paysage : 20–24 pt selon hauteur disponible.
- iPad : 28–32 pt dans le canvas principal.
- Les contenus longs ne dépassent pas environ 680 pt de largeur de lecture sur iPad.
- Les panneaux de travail peuvent utiliser toute la largeur, mais les textes longs restent limités.

### 7.3 Densité

La densité par défaut est **professionnelle compacte**, pas grand public espacée.

- Rangée de tâche : 44–56 pt selon métadonnées.
- Rangée de réglage : 44 pt minimum.
- Message conversationnel : rythme de lecture naturel, sans bulle excessive.
- Formulaire : labels et champs regroupés logiquement.
- Pas plus d’un grand bloc de respiration avant le premier contenu utile.

### 7.4 Alignement

- Les rangées partagent une grille de départ constante.
- Les métadonnées sont alignées à droite quand elles sont comparables.
- Les icônes fonctionnelles occupent une colonne stable ou disparaissent.
- Les éléments décoratifs ne doivent jamais casser l’alignement du contenu.

### 7.5 Grammaire de layout

Trois compositions couvrent la majorité des écrans.

#### A. Composition « décision »

Pour Aujourd’hui et les écrans de recommandation :

```text
salutation/contexte
commande
prochaine action
priorités
trajectoire
activité en cours
```

La première action utile doit être visible sans scroll sur iPhone standard en taille de texte par défaut.

#### B. Composition « travail »

Pour Tableau, Fiches, Routines et Transcription :

```text
titre/contexte
filtres ou actions secondaires
liste/document dense
composer ou création rapide
```

Le contenu commence rapidement ; il n’y a pas de grand hero décoratif.

#### C. Composition « conversation »

Pour Nomi :

```text
fil de messages ou salutation centrée
suggestions compactes
composer stable
```

Le composer reste l’objet le plus important. La conversation ne doit pas être cachée derrière des cartes d’accueil répétées.

### 7.6 Adaptation par plateforme et orientation

| Contexte | Adaptation |
|---|---|
| iPhone portrait | une colonne, densité compacte, tab bar |
| iPhone paysage | réduire headers, passer certains panneaux côte à côte, préserver le composer |
| iPad portrait | sidebar réductible, contenu souvent en une colonne large mais mesurée |
| iPad paysage | workspace multi-colonnes, pas d’étirement des rangées à toute la largeur |
| très grande Dynamic Type | une colonne, actions empilées, métadonnées sous le titre |
| Split View/Stage Manager | largeur minimale fonctionnelle sans perte de contrôle principal |

### 7.7 Progressive disclosure

Les détails secondaires doivent apparaître à la demande :

- métadonnées avancées dans le détail ;
- actions rares dans un menu ;
- explications techniques derrière « En savoir plus » ;
- paramètres avancés regroupés et explicitement nommés.

La première vue ne doit pas expliquer toute la complexité du produit.

### 7.8 Dimensions de référence

Ces dimensions servent de points de départ ; elles ne remplacent pas les adaptations Dynamic Type.

| Élément | iPhone | iPad |
|---|---:|---:|
| marge horizontale standard | 20 pt | 28–32 pt |
| largeur maximale de lecture | pleine largeur | 620–680 pt |
| hauteur rangée compacte | 44–48 pt | 44–52 pt |
| hauteur rangée riche | 52–64 pt | 56–68 pt |
| hauteur champ standard | 44–52 pt | 44–56 pt |
| hauteur bouton standard | 36–44 pt | 36–44 pt |
| largeur palette | écran − 24/32 pt | 560–680 pt |
| largeur panneau secondaire | — | 280–360 pt |
| hauteur composer minimum | 48–56 pt | 52–60 pt |
| taille cible tactile minimale | 44 × 44 pt | 44 × 44 pt |

### 7.9 Seuils de rupture

- **< 390 pt** : densité iPhone compacte, une seule colonne.
- **390–600 pt** : iPhone large ou petite split view ; toujours une colonne dominante.
- **600–840 pt** : iPad portrait/split view ; deux colonnes possibles mais contenu prioritaire limité.
- **> 840 pt** : workspace iPad complet ; panneaux secondaires autorisés.

Si un layout ne fonctionne qu’à une largeur précise, il est incorrect. Chaque composition doit posséder un comportement avant et après son seuil.

---

## 8. Formes, rayons et bordures

### 8.1 Rayons

| Élément | Rayon |
|---|---:|
| petit contrôle | 8–10 pt |
| champ ou rangée encapsulée | 12 pt |
| panneau ou carte utile | 14–18 pt |
| sheet | 20–24 pt selon système |
| barre flottante | 18–22 pt |
| bouton capsule | capsule complète uniquement si naturel |

### 8.2 Bordures

- Épaisseur standard : 1 pt ou hairline selon le rendu système.
- Les bordures sont plus importantes que les ombres.
- Ne pas empiler bordure + fond translucide + liseré coloré + ombre forte.
- Une surface importante peut avoir une bordure légèrement plus forte, jamais un contour multicolore.

### 8.3 Profondeur

La profondeur reste discrète.

- Ombres faibles, flou court, opacité basse.
- Pas d’ombre portée colorée systématique.
- Les sheets et palettes peuvent avoir une ombre plus visible parce qu’ils sont temporaires.
- En sombre, utiliser des highlights internes plutôt que des ombres lourdes.

### 8.4 Mode sombre

Le sombre n’est pas une inversion mécanique.

- Les surfaces sombres restent chaudes, jamais bleu nuit générique.
- Les séparateurs doivent rester perceptibles sans devenir blancs.
- L’accent est éclairci pour conserver le contraste.
- Les ombres sont remplacées par bordures et highlights internes.
- Les images, graphiques et focus doivent être revus spécifiquement en sombre.

---

## 9. Matériaux et Liquid Glass

### 9.1 Politique stricte

Liquid Glass est un matériau de **contrôle et de navigation**, pas un matériau de contenu.

Utilisation autorisée :

- tab bar ;
- sidebar ;
- palette de commande ;
- composer conversationnel ;
- boutons flottants réellement interactifs ;
- contrôles temporairement activés ;
- barres système ou surfaces système adaptées.

Utilisation interdite :

- fond de toutes les cartes ;
- bulles de contenu systématiques ;
- rangées ordinaires ;
- panneaux documentaires ;
- empilement de plusieurs effets de verre dans une même zone de lecture.

### 9.2 Matériaux de contenu

Le contenu utilise des surfaces opaques ou quasi opaques :

- `surface` pour panneaux et objets importants ;
- `background` pour le canvas ;
- `backgroundSecondary` pour groupes discrets ;
- bordures fines pour séparer les couches.

### 9.3 Repli avant iOS 26

Le repli ne doit pas être une version dégradée illisible.

- Même géométrie.
- Même hiérarchie.
- Contraste explicitement vérifié.
- Les surfaces tintées appliquent réellement leur teinte lorsqu’elles portent du texte clair.

---

## 10. Iconographie

### 10.1 Style

- SF Symbols principalement.
- Graisse medium ou semibold selon taille.
- Taille visuelle cohérente : 15–18 pt dans les rangées, 17–20 pt dans les contrôles.
- Monochrome par défaut.
- Accent uniquement si l’icône indique une action ou un état actif.

### 10.2 Interdictions

- Pas d’icônes colorées dans des pastilles rondes partout.
- Pas de mélange emoji + SF Symbols.
- Pas d’icône purement décorative répétée sur chaque carte.
- Pas d’icônes métaphoriques obscures sans label.

### 10.3 Signification

Une icône doit répondre à au moins une question :

- aide-t-elle à scanner ?
- indique-t-elle un état ?
- rend-elle un contrôle plus évident ?
- compresse-t-elle une action connue sans ambiguïté ?

Si non, elle est supprimée.

---

## 11. Composants fondamentaux

### 11.1 Boutons

#### Primaire ordinaire

- Fond : `textPrimary` en clair, surface claire adaptée en sombre.
- Texte : inverse haut contraste.
- Hauteur : 36–44 pt selon densité.
- Rayon : 10–12 pt ou capsule si bouton flottant.
- Usage : action principale de la zone.

#### Accentué

- Fond : `accent` uniquement si l’action est recommandée ou critique positivement.
- Ne pas l’utiliser pour tous les boutons.
- État pressed légèrement assombri.

#### Secondaire

- Fond transparent ou `surface`.
- Bordure `separatorStrong`.
- Texte `textPrimary`.

#### Destructif

- Jamais en primaire par défaut.
- Couleur `danger`.
- Confirmation explicite si perte de données.

### 11.2 Rangées

La rangée est le composant central.

Structure type :

```text
[contrôle ou indicateur] [titre + métadonnées] [état/action compacte]
```

Règles :

- hauteur minimum 44 pt ;
- séparateur fin plutôt que carte systématique ;
- titre tronqué sur une ligne en densité normale, deux lignes en accessibilité ;
- métadonnées mono alignées ;
- action rapide visible ou disponible via swipe/menu selon contexte.

### 11.3 Cartes et panneaux

Une carte est autorisée si elle contient un objet autonome : prochaine action, focus actif, fiche, transcription en cours.

Elle doit :

- avoir une raison d’être regroupée ;
- utiliser `surface` ;
- éviter les multiples ornements ;
- proposer des actions clairement hiérarchisées.

### 11.4 Champs

- hauteur 44 pt minimum ;
- rayon 12–16 pt ;
- fond `surface` ;
- bordure visible mais discrète ;
- placeholder explicite ;
- label visible quand le champ n’est pas évident ;
- erreurs en texte, pas seulement en couleur.

### 11.5 Badges

Un badge sert à communiquer un état qui influence l’action : `P1`, `local`, `vérifié`, `en cours`.

- petit ;
- faible saturation ;
- pas de bordure + fond + icône + couleur forte simultanément ;
- pas de badge décoratif.

### 11.6 Menus et actions contextuelles

- Les menus regroupent les actions secondaires.
- Les actions destructrices sont en bas et marquées.
- Les menus ne cachent pas l’action principale.
- Sur iPad/clavier, les raccourcis sont indiqués dans les menus lorsque disponibles.

### 11.7 Sheets et dialogues

- Sheet pour création ou configuration temporaire.
- Dialogue pour confirmation courte et destructive.
- Ne pas empiler plusieurs sheets.
- Une sheet de création expose toujours un chemin d’annulation clair.

### 11.8 États vides

Un état vide doit aider à démarrer, pas décorer.

Structure :

1. titre humain en serif ou sans selon contexte ;
2. phrase concise ;
3. action primaire utile ;
4. exemple ou suggestion si pertinent.

Pas d’illustration générique.

Exemples de formulation :

| Écran | Titre | Action |
|---|---|---|
| Tableau | « Aucune tâche ouverte. » | « Créer une tâche » |
| Nomi | « Comment puis-je t’aider ? » | champ de saisie déjà prêt |
| Fiches | « Aucune fiche pour le moment. » | « Créer une fiche » |
| Transcription | « Prêt à transcrire. » | « Commencer » après consentement |
| Routines | « Aucune routine active. » | « Nouvelle routine » |

Un état vide ne doit pas répéter le nom de l’écran ni expliquer toute la fonctionnalité.

### 11.9 Chargement et génération

- Les chargements courts utilisent un libellé explicite plus qu’un spinner seul.
- La génération IA montre un état progressif si possible.
- Le streaming ne doit pas faire sauter la mise en page.
- Les opérations longues indiquent annulation ou reprise lorsque disponible.

### 11.10 États des composants

Chaque contrôle interactif doit définir visuellement :

| État | Règle |
|---|---|
| normal | contraste et bordure suffisants sans surbrillance permanente |
| hover/focus clavier | visible sur iPad et clavier externe |
| pressed | retour rapide, sans changement de layout |
| disabled | réduction d’opacité ou saturation, avec cause disponible si non évidente |
| loading | libellé d’action conservé ou remplacé par progression explicite |
| selected | accent rare et distinction nette |
| error | texte explicite, pas seulement contour rouge |

Un composant qui ne possède pas ces états n’est pas prêt à être implémenté.

### 11.11 Spécifications de composants clés

#### `NomiCommandBar`

- hauteur : 44–52 pt ;
- rayon : 14–18 pt ;
- fond : surface élevée ou matériau système selon position ;
- contenu : marque ou glyph discret, placeholder, raccourci éventuel ;
- comportement : tap ouvre la palette, aucun texte ne doit être effacé dans l’écran source ;
- ne doit pas ressembler à une publicité ou à un hero.

#### `NomiCommandPalette`

- largeur iPhone : marges 12–16 pt ;
- largeur iPad : 560–680 pt maximum ;
- position : haut de zone de travail, assez proche du champ invoqué ;
- rangées : 44–56 pt ;
- séparateurs internes faibles ;
- première action sélectionnée visible ;
- footer technique court avec nombre de résultats et navigation clavier.

#### `NomiTaskRow`

- hauteur : 48–56 pt ;
- colonne gauche : checkbox 18–22 pt ou indicateur ;
- titre : une ligne en densité normale ;
- métadonnées : date/durée/priorité, pas plus de trois informations ;
- action secondaire : menu ou trailing compact ;
- swipe : terminer à droite si cohérent, reporter/modifier à gauche selon plateforme ;
- pas de carte individuelle pour chaque tâche.

#### `NomiFocusCard`

- carte autonome autorisée parce que le focus est un objet actif ;
- temps mono dominant ;
- objectif visible sous le temps ;
- progression fine 2–4 pt ;
- pause/reprise/fin immédiates ;
- fond sombre possible même en thème clair si le contraste et la cohérence sont validés.

#### `NomiMessage`

Deux variantes sont à tester en maquette :

1. **structure éditoriale** sans bulles, proche d’un document ;
2. **bulles très discrètes** avec faible contraste et peu de rayon.

La variante retenue doit privilégier la lecture longue, la sélection de texte et l’absence de bruit. Les actions s’affichent après la réponse ou au menu contextuel, pas dans chaque ligne.

#### `NomiTranscriptRow`

- horodatage mono ;
- texte principal lisible ;
- locuteur uniquement si la diarisation est fiable ;
- état incertain marqué sans interrompre la lecture ;
- jamais de grosse carte par segment.

#### `NomiRevisionBlock`

- structure de document ;
- titre de section sans couleur décorative ;
- flashcards et quiz différenciés par forme et hiérarchie, pas par plusieurs couleurs ;
- provenance et génération indiquées une fois par fiche.

#### `NomiTabBar` et `NomiSidebar`

- composants natifs autant que possible ;
- aucun titre redondant ;
- badges de compteur seulement si l’utilisateur doit agir ;
- l’élément actif est net mais pas transformé en pilule colorée permanente.

### 11.12 Composer

Le composer de conversation est un composant de premier rang.

Exigences :

- visible dès l’état vide ;
- hauteur confortable ;
- bouton d’ajout clairement séparé du bouton envoyer ;
- envoi désactivé seulement si vide ou action impossible ;
- état « Nomi répond » visible sans bloquer la lecture ;
- annulation du streaming si supportée ;
- support Dictation, coller, multi-lignes et clavier externe.

### 11.13 Contrat minimal d’un composant

Chaque composant designé doit documenter ces huit propriétés avant implémentation :

| Propriété | Question |
|---|---|
| intention | à quoi sert-il en une phrase ? |
| contenu | quelles données sont obligatoires/facultatives ? |
| action principale | que fait l’utilisateur le plus souvent ? |
| états | vide, erreur, chargement, succès, désactivé, sélectionné ? |
| interactions | tap, clavier, menu, swipe, drag, VoiceOver ? |
| densité | comportement compact/étendu ? |
| accessibilité | label, valeur, hint, ordre de lecture ? |
| tokens | couleurs, espacements, rayons, typographies exacts ? |

Exemple condensé pour une tâche :

```text
NomiTaskRow
intention : montrer une unité de travail actionnable
contenu requis : titre, état
contenu facultatif : échéance, durée, priorité, identifiant, source
action principale : ouvrir/commencer selon contexte
états : à faire, en cours, terminé, reporté, erreur de synchronisation
interactions : tap, menu, swipe, clavier, VoiceOver
densité : compacte par défaut, deux lignes en accessibilité
tokens : body/headline, caption/mono, separator, accent rare
```

Un composant qui ne respecte pas ce contrat ne doit pas être dupliqué avec des variations locales.

---

## 12. Commande universelle

### 12.1 Comportement

La palette apparaît au-dessus du contexte sans le détruire.

- fond assombri très léger sur le contenu ;
- palette compacte en haut ou centrée selon la plateforme ;
- champ déjà focalisé ;
- premier résultat exécutable avec `↵` ;
- `Échap` ferme sans modifier le contexte.

### 12.2 Résultats

Chaque résultat contient :

- un sigle ou pictogramme discret ;
- un titre d’action ;
- une ligne de contexte ;
- un raccourci ou une indication d’exécution.

Exemples :

```text
Démarrer un focus
Préparer la démo produit · 25 min

Créer une tâche
« démo » sera interprété localement

Réviser la fiche
Probabilité conditionnelle · 5 cartes
```

### 12.3 Classement

Le classement privilégie :

1. action possible immédiatement dans le contexte courant ;
2. objet récent ou actif ;
3. intention explicite du texte saisi ;
4. navigation.

### 12.4 Sécurité

- Une action destructive ne peut pas être le premier résultat implicite.
- Les actions irréversibles demandent confirmation.
- Les actions simulées ou indisponibles sont marquées.
- La palette ne doit pas exécuter une action ambiguë sans prévisualisation.

### 12.5 Accessibilité

- Navigation complète au clavier.
- VoiceOver lit le titre, le contexte et le raccourci.
- Le focus reste dans la palette jusqu’à fermeture.
- Dynamic Type peut transformer les rangées en deux lignes.

### 12.6 Grammaire de commande

La palette comprend trois formes d’intention :

```text
[verbe] [objet] [modificateurs]
rechercher fiche probabilités
créer tâche envoyer compte rendu demain 30 min important
démarrer focus préparer démo 25 min
```

| Verbe | Objets possibles | Résultat |
|---|---|---|
| créer | tâche, routine, fiche, conversation | composer prérempli ou création directe |
| démarrer | focus, transcription, révision | action contextuelle confirmée |
| ouvrir | écran, fiche, tâche, routine | navigation |
| rechercher | tâche, fiche, transcription, message | résultats filtrés |
| demander | question libre | conversation Nomi |
| exporter | transcription, fiche | format ou destination à confirmer |
| modifier | tâche, routine, focus | détail prérempli |

Règles :

- ne jamais prétendre comprendre une intention ambiguë : proposer deux ou trois interprétations ;
- montrer la date/durée/priorité interprétée avant exécution si elle change les données ;
- garder le texte saisi visible après ouverture du détail ;
- permettre de corriger l’interprétation sans ressaisir ;
- distinguer recherche locale, action locale et demande IA.

### 12.7 États de la palette

La palette doit avoir les états suivants :

- vide avec suggestions contextuelles ;
- recherche sans résultat ;
- action unique trouvée ;
- ambiguïté avec choix ;
- exécution en cours ;
- erreur récupérable ;
- mode clavier externe ;
- mode VoiceOver/Dynamic Type élargi.

Un état sans résultat doit proposer une action utile : créer, demander à Nomi ou élargir la recherche.

---

## 13. Navigation

### 13.1 iPhone

Navigation principale recommandée :

- **Jour** ;
- **Tableau** ;
- **Focus** ;
- **Nomi** ;
- **Plus**.

`Plus` regroupe Routines, Transcription, Fiches et Réglages.

Règles :

- tab bar native Liquid Glass ;
- pas de duplication de titre système et de titre custom ;
- conserver l’état des onglets ;
- ne pas interrompre une activité sans raison ;
- signaler clairement si quitter un écran arrête un enregistrement.

### 13.2 iPad

- `NavigationSplitView` native.
- Sidebar stable avec compteurs uniquement s’ils ont une utilité.
- Detail large, canvas de travail.
- Commande visible en haut du workspace.
- Les écrans ne doivent pas être simplement un iPhone étiré.

### 13.3 Navigation profonde

- Préférer navigation typée.
- Les retours doivent être prévisibles.
- Ne pas détruire une pile de navigation lors d’un changement d’onglet actif.
- Préserver brouillons et états non destructifs.

### 13.4 Gestes

- Swipe sur rangée : uniquement actions fréquentes et réversibles.
- Long press/context menu : détails et actions secondaires.
- Pull to refresh : uniquement pour les contenus réellement synchronisés ; inutile dans les données purement locales.
- Drag & drop iPad : envisageable pour tâches et fichiers, mais jamais comme seul chemin.
- Les gestes ne doivent jamais remplacer un bouton accessible visible.

### 13.5 Clavier et raccourcis

Sur iPad et clavier externe, les raccourcis doivent être découvrables :

- afficher les raccourcis dans la palette et les menus ;
- ne pas capturer les touches système ;
- `Échap` ferme d’abord la couche la plus récente ;
- `⌘K` fonctionne depuis les écrans principaux ;
- navigation dans les listes par flèches lorsque pertinent.

### 13.6 État persistant

Les éléments suivants survivent à la navigation :

- brouillon de composer ;
- transcription en cours ou segments sauvegardés ;
- focus actif ;
- filtres essentiels ;
- position de scroll quand elle est importante.

Les éléments purement temporaires — palette, menus, sheets non sauvegardées — peuvent disparaître si leur coût de recréation est nul.

### 13.7 Pointeur, Pencil et multitâche iPad

- Les éléments interactifs réagissent au hover avec une discrète mise en évidence.
- Le pointeur ne doit pas révéler des actions essentielles impossibles à découvrir au tactile.
- Le Pencil peut servir à sélectionner du texte, annoter ou faire glisser des objets si ces fonctions existent réellement.
- En Split View étroit, Nomi repasse à la composition iPhone compacte.
- En Stage Manager ou fenêtre large, le contenu ne s’étire pas indéfiniment.
- Les actions de drag & drop doivent aussi être disponibles par menu.

---

## 14. Motion, haptique et feedback

### 14.1 Durées

| Type | Durée |
|---|---:|
| pressed/hover | 80–120 ms |
| micro-transition | 150–200 ms |
| changement d’état | 220–280 ms |
| apparition de palette | 180–240 ms |
| transition de sheet système | selon système |

### 14.2 Courbes

- utiliser les courbes système par défaut ;
- ressorts légers uniquement pour les petits déplacements ;
- pas de rebond exagéré ;
- pas d’animation permanente sauf état actif réel.

### 14.3 Feedback

- validation d’une tâche : haptique légère + changement visuel net ;
- démarrage focus : confirmation courte ;
- enregistrement : état visible en permanence ;
- erreur : message textuel, pas seulement vibration ou couleur.

### 14.4 Cartographie haptique

| Événement | Haptique recommandé |
|---|---|
| tâche terminée | impact léger ou succès selon contexte |
| démarrage/pause focus | impact léger |
| exécution d’une commande | sélection subtile |
| erreur irrécupérable | notification d’erreur, jamais répétée |
| streaming IA | aucune vibration |
| scroll/liste ordinaire | aucune vibration |

L’haptique confirme un événement ; elle ne doit pas devenir une notification permanente.

### 14.5 Reduced Motion

Si Reduce Motion est activé :

- les déplacements deviennent fondus ;
- les effets de respiration disparaissent ;
- les changements d’état restent visibles par contenu.

---

## 15. Accessibilité

### 15.1 Exigences

- zones tactiles ≥ 44 × 44 pt ;
- contraste AA ;
- Dynamic Type complet ;
- VoiceOver sur tous les contrôles ;
- labels explicites sans jargon interne ;
- ordre de lecture logique ;
- aucune information uniquement par la couleur.

### 15.2 VoiceOver

Les rangées complexes doivent être groupées lorsque pertinent :

```text
Préparer la démo produit, priorité haute, 25 minutes, à faire. Bouton.
```

Les actions multiples restent accessibles par actions personnalisées ou menu.

### 15.3 Reduce Transparency

- augmenter l’opacité des surfaces ;
- renforcer les bordures ;
- conserver la hiérarchie sans flou.

### 15.4 Clavier externe

Raccourcis initiaux proposés :

| Raccourci | Action |
|---|---|
| `⌘K` | ouvrir la commande |
| `↵` | exécuter le premier résultat |
| `Échap` | fermer palette/sheet |
| `⌘N` | nouvelle tâche ou nouvelle conversation selon contexte |
| `⌘F` | recherche dans la bibliothèque |
| `espace` | pause/reprise focus si le focus est visible |

### 15.5 Matrice minimale de validation accessibilité

Chaque écran majeur doit être vérifié dans les conditions suivantes avant validation :

| Condition | Résultat attendu |
|---|---|
| Dynamic Type standard | première action visible sans scroll excessif |
| Dynamic Type accessibility-medium | pas de troncature destructive ; actions empilées si nécessaire |
| VoiceOver | toutes les actions principales accessibles et correctement décrites |
| Reduce Motion | pas d’information transmise uniquement par animation |
| Reduce Transparency | lisibilité conservée sans flou |
| contraste augmenté | contrôles et textes distinguables |
| clavier externe iPad | navigation possible sans toucher l’écran pour les actions clés |
| orientation paysage | pas de composer, CTA ou timer perdu hors viewport |

### 15.6 Accessibilité par composant

| Composant | Exigence VoiceOver/clavier |
|---|---|
| commande | annoncée comme recherche/action, pas seulement champ texte |
| palette | focus piégé, résultats annoncés avec position et action |
| tâche | titre, état, échéance, priorité lus dans un ordre stable |
| focus | temps restant annoncé sans mise à jour excessive ; contrôles séparés |
| composer | champ, pièces jointes, dictée et envoi distincts |
| transcription | état d’enregistrement annoncé, segments groupés |
| fiche | structure de headings navigable |
| réglages modèle | progression et taille annoncées en texte |

Les notifications accessibles ne doivent pas interrompre la lecture toutes les secondes. Pour le temps qui passe, annoncer seulement les changements d’état significatifs.

---

## 16. Écriture et voix

### 16.1 Style

- français clair ;
- phrases courtes ;
- verbes d’action ;
- pas de jargon technique visible sauf dans Réglages avancés ;
- pas d’enthousiasme artificiel.

### 16.2 Exemples

| À éviter | Préférer |
|---|---|
| « Super ! Votre journée est optimisée » | « Votre journée est prête. » |
| « Erreur inattendue » | « La génération a échoué. Le texte source est conservé. » |
| « IA magique » | « Gemma 4 local » |
| « Cliquez ici » | « Démarrer » / « Créer » / « Réviser » |

### 16.3 Honnêteté

- « réel », « local », « simulé », « non implémenté » et « nécessite un appareil physique » doivent être formulés clairement.
- Ne jamais laisser croire qu’une intégration existe si elle est seulement prévue.

### 16.4 Localisation

- Les chaînes doivent être externalisées avant une version multilingue complète.
- Les labels courts en français peuvent devenir longs en anglais ou allemand : prévoir deux lignes ou compression progressive.
- Les formats de date, durée et nombre passent par les APIs Apple, jamais par concaténation manuelle.
- Les raccourcis visuels comme `⌘K` ne sont pas localisés, mais leur description l’est.

### 16.5 Formats de données

| Donnée | Format recommandé | Exemple |
|---|---|---|
| heure courte | `HH:mm` | `10:00` |
| durée courte | `25 min` | `25 min` |
| durée focus | `mm:ss` ou `h:mm:ss` | `18:42` |
| date du jour | jour + date localisés | `jeudi 26 novembre` |
| date compacte | `EEE d MMM` | `jeu. 26 nov.` |
| priorité | `P1`, `P2`, `P3` ou libellé accessible | `P1` |
| taille modèle | unité décimale localisée | `2,6 Go` |
| identifiant interne | uniquement si utile au suivi | `NOM-124` |

Les formats compacts ne remplacent pas une formulation accessible. VoiceOver doit lire « priorité 1 », pas seulement « P un » si cela est ambigu.

### 16.6 Messages d’erreur

Un bon message d’erreur contient :

1. ce qui s’est passé ;
2. ce qui est conservé ;
3. quoi faire ensuite.

Exemple : « Le téléchargement du modèle a été interrompu. Les fichiers incomplets seront repris au prochain essai. Réessayer ou choisir un modèle plus petit. »

---

## 17. Directions par écran

### 17.1 Aujourd’hui

Rôle : choisir la prochaine action.

Structure :

1. salutation éditoriale courte ;
2. commande universelle ;
3. prochaine action ;
4. priorités ;
5. trajectoire/agenda ;
6. état focus si actif.

Règles :

- pas de grand dashboard ;
- pas de carte pour chaque information ;
- une seule recommandation dominante ;
- les actions de secours sont secondaires.

### 17.2 Tableau

Rôle : capturer et faire progresser les tâches.

Structure :

- création rapide toujours accessible ;
- filtres compacts ;
- rangées denses ;
- identifiants internes seulement s’ils aident le suivi ;
- actions rapides par menu ou swipe.

Le Tableau doit être proche d’un outil de travail, pas d’un tableau Kanban miniature illisible sur iPhone.

### 17.3 Focus

Rôle : créer une bulle de travail mesurable.

- temps en mono, grande taille mais pas spectaculaire ;
- objectif toujours visible ;
- progression discrète ;
- pause/reprise/fin immédiates ;
- notes de contexte accessibles ;
- pas de visualisation circulaire dominante si elle ne donne pas d’information supplémentaire.

### 17.4 Nomi

Rôle : conversation locale et transformation en action.

État vide :

- marque `n.` ou signe discret ;
- salutation serif ;
- champ de saisie visible ;
- suggestions compactes.

Conversation active :

- texte avant tout ;
- bulles discrètes ou structure éditoriale ;
- provenance locale visible mais non répétée sur chaque ligne ;
- actions après réponse : copier, créer fiche, transformer en tâche, vérifier.

### 17.5 Routines

- règles lisibles en langage naturel ;
- heure/jours en mono ou métadonnées ;
- activation claire ;
- déclencheurs expliqués honnêtement ;
- aucune automatisation silencieuse présentée comme garantie si iOS ne la permet pas.

### 17.6 Transcription

- consentement toujours explicite pour chaque session ;
- état d’enregistrement visible en permanence ;
- chronologie claire ;
- pause/reprise/arrêt immédiats ;
- conservation honnête des segments ;
- export et création de fiche accessibles après arrêt.

### 17.7 Fiches et révisions

- structure documentaire ;
- sections, flashcards et quiz lisibles ;
- provenance source/IA distinguée ;
- pas de cartes multicolores pour chaque section ;
- actions de révision progressives.

### 17.8 Réglages

- natif, précis, groupé ;
- les fonctions non implémentées ne sont pas présentées comme disponibles ;
- les risques de confidentialité sont formulés simplement ;
- Gemma doit expliquer espace disque, téléchargement et localité.

### 17.9 Onboarding

- très court ;
- une seule promesse claire ;
- aucun carrousel marketing ;
- langue et permissions demandées au moment utile ;
- accès rapide au premier contenu.

### 17.10 Cycle de vie Gemma et confiance locale

L’expérience autour du modèle fait partie du design, pas seulement de l’ingénierie.

États à distinguer clairement :

| État | Message attendu | Action principale |
|---|---|---|
| non installé | modèle absent, espace requis indiqué | télécharger |
| téléchargement | progression, reprise possible | laisser continuer / annuler |
| vérification | empreinte et intégrité contrôlées | attendre |
| prêt | modèle local disponible | utiliser |
| chargement | préparation mémoire en cours | attendre / annuler si possible |
| erreur | cause simple et conservation des données | réessayer / choisir autre modèle |
| repli | service de secours ou extraction locale selon fonction | comprendre la différence |

Règles :

- indiquer la taille avant téléchargement ;
- ne pas prétendre que l’IA est locale si le modèle n’est pas chargé ;
- expliquer que les conversations restent sur l’appareil uniquement lorsque c’est réellement vrai ;
- ne pas afficher de statut « en ligne » pour Gemma local ;
- séparer clairement Gemma réel, services simulés et fonctionnalités non implémentées.

### 17.11 Matrice de contenu par écran

| Écran | Intention | Contenu obligatoire | Action principale | Action secondaire |
|---|---|---|---|---|
| Aujourd’hui | décider | prochaine action, priorités, trajectoire | démarrer | déplacer/modifier |
| Tableau | organiser | rangées de tâches, filtre, création rapide | créer/commencer | trier/modifier |
| Focus | se concentrer | objectif, temps, état | pause/terminer | noter/prolonger |
| Nomi | demander | composer, conversation, provenance | envoyer | transformer/copier |
| Routines | automatiser | règles lisibles, activation | activer/modifier | exécuter/reporter |
| Transcription | capturer | consentement, état, segments | démarrer/arrêter | exporter/créer fiche |
| Fiches | réviser | sections, flashcards, quiz | réviser | régénérer/exporter |
| Réglages | contrôler | préférences réelles, états modèle | modifier | diagnostiquer |

Si un écran ne possède pas d’action principale évidente dans cette matrice, sa maquette est probablement décorative et doit être simplifiée.

### 17.12 Anatomies détaillées

#### Aujourd’hui

1. **Contexte** — salutation ou date, une ligne maximum en densité normale.
2. **Commande** — champ visible sous le contexte.
3. **Prochaine action** — titre, raison de la recommandation, durée ou échéance, action primaire.
4. **Priorités** — trois rangées maximum avant « Tout voir ».
5. **Trajectoire** — événements ou blocs de temps à venir.
6. **Activité en cours** — focus/transcription uniquement si réellement actif.

Ne pas afficher simultanément : grande carte de progression, graphique, résumé IA, routine, statistiques et recommandations multiples.

#### Tableau

1. **En-tête de travail** — titre ou espace courant, compteur, bouton création.
2. **Création rapide** — champ langage naturel ou commande.
3. **Filtres** — trois à cinq options maximum, pas de filtre décoratif.
4. **Groupes** — À faire / En cours / Terminé ou tri contextuel.
5. **Rangées** — titre, durée, date, priorité, état.
6. **Détail** — description, dates, actions, division en sous-tâches si réelle.

Sur iPhone, éviter un Kanban horizontal illisible. La densité de liste prime sur la métaphore de colonnes.

#### Focus

1. **Objectif** — visible même si le timer domine.
2. **Temps** — mono, stable, sans reflow.
3. **Progression** — barre ou anneau fin, pas les deux.
4. **Contrôles** — pause/reprise et fin.
5. **Contexte** — notes ou intention courte.
6. **Fin** — résultat, prolonger, terminer, créer suite.

Le timer ne doit jamais masquer l’objectif. Si l’écran devient un écran de veille visuel, c’est une dérive décorative.

#### Nomi

1. **État vide** — salutation, composer, suggestions.
2. **Fil** — messages et réponses avec actions post-réponse.
3. **Composer** — stable au clavier, multi-lignes, pièces jointes si réelles.
4. **État génération** — streaming, annulation si disponible, provenance.
5. **Transformation** — créer tâche/fiche/calcul vérifié depuis une réponse.

Les réponses longues doivent être structurées en headings légers ou sections repliables si nécessaire, jamais en bulles décoratives multiples.

#### Routines

1. **Résumé des routines** — actives/inactives.
2. **Règle lisible** — « Tous les jours à 7:30, préparer… ».
3. **Déclencheurs** — heure, fin de tâche, fin de focus, ouverture manuelle selon ce qui est réellement implémenté.
4. **Actions** — modifier, activer, exécuter, reporter.
5. **Historique** — seulement s’il aide à comprendre ce qui s’est passé.

#### Transcription

1. **Consentement** — expliqué avant chaque session si requis.
2. **État** — prêt, enregistrement, pause, interruption.
3. **Contrôles** — démarrer, pause, arrêter.
4. **Segments** — chronologie lisible.
5. **Actions finales** — export, création de fiche, suppression.
6. **Limites** — diarisation et microphone réel clairement indiqués selon validation.

#### Fiches et révisions

1. **Bibliothèque** — liste de fiches avec source, date et état.
2. **Document** — titre, résumé, sections, points clés.
3. **Flashcards** — question/réponse compacte.
4. **Quiz** — question, options, validation explicite.
5. **Provenance** — transcription/texte/Gemma/extraction locale.
6. **Actions** — réviser, régénérer, exporter, supprimer.

#### Réglages et Gemma

1. **Groupes natifs** — profil, langue, confidentialité, modèle, accessibilité.
2. **État modèle** — installé/absent/téléchargement/vérification/prêt.
3. **Stockage** — taille, suppression, reprise.
4. **Confidentialité** — local/réseau exact.
5. **Avancé** — diagnostics uniquement si utiles.
6. **Non implémenté** — jamais présenté comme actif.

### 17.13 Onboarding détaillé

Le onboarding ne doit pas dépasser trois moments :

1. **Promesse** — « Organise ta journée, concentre-toi et transforme tes cours en révisions. »
2. **Choix essentiels** — langue et éventuellement préférence d’apparence.
3. **Premier geste** — créer une tâche, poser une question ou ouvrir Aujourd’hui.

Les permissions micro, notifications et stockage ne sont demandées qu’au moment où la fonction correspondante est utilisée. Ne pas montrer une série de captures marketing avant l’accès au produit.

---

## 18. États à spécifier avant implémentation

Chaque écran doit posséder une maquette ou une description pour :

- état vide ;
- état chargé ;
- erreur ;
- contenu long ;
- Dynamic Type accessibility-medium ;
- clair/sombre ;
- portrait/paysage ;
- iPad ;
- état sans modèle Gemma ;
- état téléchargement Gemma ;
- état génération ;
- état hors-ligne si pertinent.

Les états suivants sont obligatoires pour la transcription :

- avant consentement ;
- consentement donné ;
- enregistrement ;
- pause ;
- interruption ;
- erreur micro/permission ;
- arrêt avec segments conservés.

### 18.1 Matrice d’états minimale par écran

| Écran | Vide | Chargement/génération | Erreur critique | État particulier |
|---|---|---|---|---|
| Aujourd’hui | pas de tâche ni événement | recommandation en cours | données corrompues/indisponibles | focus actif |
| Tableau | aucune tâche | analyse du langage naturel | sauvegarde impossible | contenu très long |
| Focus | aucun focus | préparation | minuteur interrompu | pause longue |
| Nomi | nouvelle conversation | streaming Gemma | modèle absent/erreur | repli hors-ligne |
| Routines | aucune routine | suggestion | déclencheur impossible | routine inactive |
| Transcription | consentement non donné | préparation micro/transcription | permission refusée | interruption appel/arrière-plan |
| Fiches | aucune fiche | extraction/génération | JSON invalide/contenu vide | quiz long |
| Réglages Gemma | modèle absent | téléchargement/vérification | checksum/espace insuffisant | reprise possible |

### 18.2 Cas limites obligatoires

Les maquettes doivent aussi traiter :

- titre de tâche très long ;
- objectif de focus multi-lignes ;
- transcription sans segment ;
- conversation très longue ;
- modèle Gemma partiellement téléchargé ;
- espace disque insuffisant ;
- permission micro refusée ;
- app mise en arrière-plan pendant enregistrement ;
- rotation pendant composer ouvert ;
- clavier ouvert au dernier message ;
- contenu en anglais ou caractères accentués ;
- date passée et date sans heure.

### 18.3 États système et permissions

Les états système suivants doivent être dessinés ou décrits avant implémentation :

| Événement système | UX attendue |
|---|---|
| permission microphone demandée | expliquer la transcription avant la demande |
| permission refusée | bouton vers Réglages + alternative sans micro si possible |
| app en arrière-plan | état réel affiché honnêtement, pas de promesse d’enregistrement continu si incertain |
| interruption audio/appel | pause ou arrêt clairement indiqué |
| stockage presque plein | alerte avant téléchargement modèle |
| réseau absent | distinguer fonctions locales réelles et téléchargements impossibles |
| batterie faible | ne pas promettre de surveillance lourde continue |
| rotation appareil | conserver composer, scroll et état actif |
| clavier externe connecté | raccourcis visibles dans les surfaces pertinentes |
| Dynamic Type très grand | privilégier contenu et actions, réduire décor |

### 18.4 États de données locales

Nomi doit distinguer :

- donnée créée mais non sauvegardée ;
- brouillon sauvegardé localement ;
- objet confirmé ;
- objet en cours de génération ;
- objet partiellement généré ;
- objet corrompu ou invalide ;
- objet supprimé mais récemment récupérable si cette fonction existe.

Ne pas utiliser « synchronisation » pour une simple écriture locale.

---

## 19. Design review — critères de qualité

Un écran n’est pas acceptable si :

- tout est une carte ;
- tout est arrondi au même rayon ;
- l’accent apparaît partout ;
- le contenu utile commence après trop de décor ;
- plusieurs niveaux de gras concurrencent le titre ;
- les métadonnées ne sont pas alignées ;
- une icône n’a aucune fonction ;
- Liquid Glass est utilisé comme texture de contenu ;
- l’action principale n’est pas évidente ;
- le texte est tronqué sans alternative en Dynamic Type ;
- le sombre est seulement une inversion de couleurs.

Un écran est acceptable si :

- l’intention principale est visible en moins d’une seconde ;
- l’utilisateur sait quoi faire ensuite ;
- les actions secondaires ne perturbent pas la lecture ;
- les états honnêtes sont visibles ;
- chaque contrôle semble natif et précis ;
- l’écran reste solide sans animation ni effet de verre.

### 19.1 Notation design QA

Chaque écran peut être noté sur 100 points afin d’éviter les débats purement subjectifs :

| Critère | Points |
|---|---:|
| intention et action principale | 15 |
| hiérarchie typographique | 15 |
| densité et alignement | 15 |
| qualité des états | 15 |
| discipline couleur/accent | 10 |
| interaction et navigation | 10 |
| accessibilité et Dynamic Type | 10 |
| motion/haptique appropriés | 5 |
| honnêteté réel/simulé/local | 5 |

Seuils :

- **90–100** : prêt pour implémentation ;
- **75–89** : bon, corrections ciblées nécessaires ;
- **50–74** : structure à retravailler ;
- **< 50** : direction incorrecte, ne pas polir.

Une maquette ne peut pas dépasser 89 si ses états d’erreur et vide sont absents.

---

## 20. Icône d’application

L’icône doit prolonger le langage sans devenir un logo complexe.

- signe principal : `n.` ou monogramme équivalent ;
- fond porcelaine ou encre chaude selon variante ;
- point corail comme seul accent ;
- aucun texte secondaire ;
- aucun effet de verre exagéré ;
- versions claire, sombre et teintée conçues séparément ;
- le monogramme reste lisible à très petite taille.

L’icône ne doit pas tenter de représenter simultanément productivité, IA, verre, orbite, conversation et apprentissage. Un signe simple et reconnaissable vaut mieux qu’une illustration.

---

## 21. Processus de conception

### 21.1 Avant maquette

Pour chaque écran :

1. définir l’intention utilisateur ;
2. définir l’action principale ;
3. lister les données nécessaires ;
4. supprimer tout ce qui n’aide pas ;
5. définir les états.

### 21.2 Pendant la maquette

Vérifier :

- hiérarchie en noir et blanc ;
- densité ;
- contraste ;
- alignement ;
- comportement Dynamic Type ;
- pertinence des icônes ;
- texte réel, pas lorem ipsum.

### 21.3 Avant implémentation

La maquette doit couvrir :

- iPhone portrait ;
- iPhone paysage ;
- iPad portrait/paysage ;
- clair/sombre ;
- Dynamic Type ;
- interactions clés ;
- états vides et erreurs.

### 21.4 Avant validation finale

- comparer la maquette et l’implémentation pixel par pixel ;
- tester au moins un parcours complet ;
- vérifier VoiceOver sur les actions principales ;
- vérifier qu’aucun effet ne remplace la hiérarchie ;
- documenter ce qui est réel, simulé ou non implémenté.

---

## 22. Correspondance SwiftUI proposée

Ces noms sont des conventions d’implémentation, pas encore un engagement de code.

```text
NomiColor.background
NomiColor.backgroundSecondary
NomiColor.surface
NomiColor.surfaceRaised
NomiColor.textPrimary
NomiColor.textSecondary
NomiColor.textTertiary
NomiColor.separator
NomiColor.accent
NomiColor.accentText
NomiColor.success
NomiColor.warning
NomiColor.danger

NomiFont.displaySerif
NomiFont.titleSerif
NomiFont.headline
NomiFont.body
NomiFont.callout
NomiFont.caption
NomiFont.mono

NomiSpace.x1/x2/x3/x4/x5/x6/x8/x10
NomiRadius.control/field/panel/sheet/floating
```

Les composants candidats :

```text
NomiCommandBar
NomiCommandPalette
NomiListRow
NomiPanel
NomiPrimaryButton
NomiSecondaryButton
NomiStatusBadge
NomiEmptyState
NomiScreenHeader
```

Liquid Glass ne doit être appliqué que par des composants explicitement destinés à la navigation ou aux contrôles flottants.

### 22.1 Contrat d’implémentation des tokens

- Aucun hexadécimal ad hoc dans les écrans.
- Aucune valeur de spacing magique hors tokens ou exceptions documentées.
- Les rayons doivent provenir de `NomiRadius`.
- Les typographies doivent provenir de `NomiFont`.
- Les couleurs doivent provenir de `NomiColor`.
- Les variantes clair/sombre sont centralisées, pas dupliquées écran par écran.
- Une exception visuelle doit être nommée et justifiée dans le code ou la maquette.

### 22.2 Registre de tokens complémentaires

En plus des couleurs, typographies, espacements et rayons, l’implémentation doit définir :

```text
NomiBorder.subtle
NomiBorder.strong

NomiElevation.none
NomiElevation.control
NomiElevation.raised
NomiElevation.overlay

NomiOpacity.disabled
NomiOpacity.scrim
NomiOpacity.muted

NomiMotion.fast
NomiMotion.standard
NomiMotion.emphasis

NomiHitArea.minimum
NomiHitArea.comfortable
```

Chaque token doit avoir une valeur unique, une destination et un exemple d’usage. Un token qui n’est utilisé nulle part doit être supprimé.

### 22.3 Séparation design/produit

Le design system ne décide pas seul des capacités techniques. Il doit recevoir du produit :

- ce qui est réel ;
- ce qui est simulé ;
- ce qui est impossible sur iOS ;
- ce qui nécessite un modèle ou une permission ;
- ce qui est seulement prévu.

L’UI doit représenter ces états sans les embellir.

---

## 23. Décisions encore ouvertes

Ces décisions doivent être tranchées dans la maquette détaillée :

1. La barre de commande est-elle visible en permanence sur tous les écrans ou seulement sur les écrans principaux ?
2. Le serif reste-t-il limité aux salutations ou peut-il servir pour les titres de sections majeures ?
3. Les messages de Nomi utilisent-ils des bulles très discrètes ou une structure éditoriale sans bulles ?
4. Le Focus doit-il afficher un anneau de progression fin ou seulement une barre/temps mono ?
5. Quels raccourcis clavier deviennent publics sur iPad ?
6. L’identifiant type `NOM-124` est-il utile ou trop inspiré de Linear pour ce produit personnel ?

---

## 24. Annexes opérationnelles

### 24.1 Checklist de review rapide

Avant de montrer une maquette ou une implémentation, répondre oui/non :

- [ ] L’action principale est visible immédiatement.
- [ ] Le premier contenu utile arrive avant tout décor.
- [ ] La commande universelle est représentée ou son absence est justifiée.
- [ ] La typographie montre au moins deux registres correctement hiérarchisés.
- [ ] L’accent est rare et signifie quelque chose.
- [ ] Les métadonnées sont alignées et formatées.
- [ ] Liquid Glass est limité à la navigation ou aux contrôles flottants.
- [ ] Les états vide, erreur, chargement et contenu long sont prévus.
- [ ] Dynamic Type ne casse pas la hiérarchie.
- [ ] VoiceOver possède des labels exploitables.
- [ ] Les fonctions simulées ou non implémentées sont honnêtement marquées.
- [ ] L’écran reste solide en noir et blanc.

### 24.2 Glossaire

| Terme | Définition |
|---|---|
| Commande universelle | champ/palette permettant de créer, chercher ou exécuter une action |
| Prochaine action | recommandation unique calculée ou proposée pour le moment présent |
| Trajectoire | vue compacte du temps et des événements à venir |
| Focus | session de travail délimitée avec objectif, temps et état |
| Canvas | fond de l’écran, hors surfaces de contenu |
| Surface | zone opaque ou quasi opaque portant du contenu |
| Palette | interface temporaire de commande et recherche |
| Composer | champ d’entrée conversationnel ou de création |
| Accent rare | usage délibérément limité de la couleur d’action |
| Preuve locale | indication vérifiable que les données/modèle restent sur l’appareil |

### 24.3 Gouvernance du design language

- Toute modification d’un token, composant ou règle doit être faite dans ce fichier avant ou en même temps que l’implémentation.
- Une exception locale doit être documentée dans la section de l’écran concerné.
- Les décisions ouvertes sont fermées par une maquette validée ou une note de décision courte.
- La version du document augmente à chaque changement substantiel.
- En cas de conflit entre une maquette et ce fichier, le fichier validé fait foi jusqu’à révision explicite.

### 24.4 Bibliothèque de formulations système

| Situation | Formulation recommandée |
|---|---|
| modèle non installé | « Gemma 4 n’est pas encore installé. Le téléchargement nécessite environ 2,6 Go. » |
| vérification modèle | « Vérification de l’intégrité du modèle. » |
| reprise téléchargement | « Le téléchargement reprendra où il s’est arrêté. » |
| permission micro refusée | « Nomi ne peut pas accéder au microphone. Autorisez l’accès dans Réglages pour transcrire. » |
| transcription interrompue | « L’enregistrement a été interrompu. Les segments déjà transcrits sont conservés. » |
| génération impossible | « La génération a échoué. Le contenu source est conservé. » |
| ambiguïté commande | « Plusieurs actions correspondent. Choisissez celle à appliquer. » |
| calcul vérifié | « Vérifié localement. » |
| fonction non implémentée | « Cette fonction n’est pas encore disponible dans ce prototype. » |
| service simulé | « Réponse de démonstration générée localement sans modèle Gemma. » |

### 24.5 Registre de décision

Chaque décision de design importante doit être enregistrée sous cette forme :

```text
Date :
Question :
Options :
Décision :
Raison :
Conséquences :
Réversible : oui/non
```

Cela évite de rediscuter les mêmes choix à chaque itération et rend la maquette traçable.

### 24.6 Organisation des fichiers de design

Les fichiers produits pendant la conception doivent être séparés clairement :

```text
design.md                 référence validée
design-tokens.*           export futur des tokens si nécessaire
mockups/                  maquettes par direction ou version
mockups/screen-states/    états obligatoires
decisions/                registres de décision détaillés
qa/                       rapports de design QA
```

Une maquette exploratoire ne doit jamais être confondue avec une maquette validée. Nommer explicitement la version et la date.

### 24.7 Handoff design → implémentation

Pour chaque écran, le handoff doit contenir :

- intention utilisateur ;
- action principale ;
- composition et dimensions ;
- tokens utilisés ;
- contenus réels d’exemple ;
- états obligatoires ;
- interactions et raccourcis ;
- comportement Dynamic Type ;
- comportement iPad ;
- critères d’acceptation ;
- différences autorisées et interdites avec la maquette.

L’implémentation n’est pas une interprétation libre. Toute divergence doit être signalée avant review.

### 24.8 Critère de maturité

Le design language sera considéré comme suffisamment complet lorsque :

1. les décisions ouvertes de la section 23 sont tranchées ;
2. chaque écran possède ses états obligatoires ;
3. les tokens sont implémentés sans valeurs ad hoc ;
4. les composants clés sont réutilisés plutôt que recréés ;
5. les maquettes et l’implémentation peuvent être comparées sans ambiguïté ;
6. chaque parcours critique atteint la barre de qualité mesurable ;
7. la notation design QA dépasse 90 sur les écrans principaux.

---

## 25. Exigence finale

Le design réussi ne doit pas être remarqué comme « un design » au premier instant. Il doit donner l’impression que Nomi est un outil évident, rapide et fiable. La personnalité vient de la précision, du rythme, de la typographie, de la chaleur retenue et de la commande universelle — pas d’effets décoratifs.

Si un choix visuel ne rend pas l’action plus claire, plus rapide ou plus digne de confiance, il est supprimé.

---

