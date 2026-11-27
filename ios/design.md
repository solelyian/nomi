# Nomi Command — design language iOS / iPadOS

Version 0.1 — document de conception avant maquettes détaillées et implémentation.

## 1. Intention

Nomi n’est pas un dashboard de productivité ni une démo de chatbot. C’est un **outil personnel de commande** : il aide à décider quoi faire maintenant, à lancer l’action adaptée, puis à conserver une trace exploitable.

La direction **Nomi Command** combine quatre références sans les copier :

- **Raycast** : vitesse, palette de commande, actions explicites, indication des raccourcis.
- **Linear** : densité, listes nettes, métadonnées monospacées, absence de bruit visuel.
- **Claude** : chaleur éditoriale, conversation centrée sur l’entrée utilisateur, sentiment de calme.
- **Notion** : contenu documentaire, hiérarchie lisible, structure avant décoration.

Le design ne doit pas paraître « futuriste », « glassmorphism » ou « généré ». Il doit paraître **réglé**.

## 2. Principes directeurs

### 2.1 Une colonne vertébrale : la commande

La commande universelle est l’interface centrale. Elle permet de créer, chercher et demander sans naviguer :

- `focus démo 25` → crée ou lance un focus contextualisé ;
- `tâche relire dossier demain 30` → crée une tâche structurée ;
- `fiche probabilités` → ouvre ou génère une fiche ;
- `pourquoi …` / texte libre → question à Nomi.

Elle ne remplace pas la navigation, mais évite la navigation quand l’intention est connue.

### 2.2 Le contenu est document, pas décor

Les écrans principaux sont composés de listes, de sections et de documents. Les cartes ne sont utilisées que lorsqu’elles séparent réellement des objets distincts. Pas de grandes illustrations, gradients décoratifs ou ornements abstraits.

### 2.3 Typographie comme identité

L’identité vient de la relation entre trois voix typographiques :

1. **Éditoriale** : serif pour accueil, titres humains, réponses importantes et moments de respiration.
2. **Travail** : sans-serif compact pour listes, contrôles, labels et contenu.
3. **Instrument** : monospace pour durées, heures, compteurs, priorités et raccourcis.

### 2.4 Accent rare

Le corail Nomi signifie **action** ou **attention**. Il n’est jamais utilisé comme simple remplissage décoratif permanent. Plus l’accent est rare, plus l’interface semble conçue.

### 2.5 Matériau honnête

Le contenu reste mat, opaque et stable. Liquid Glass est réservé aux couches Apple prévues pour cela : tab bar, sidebar, palette flottante, composer, boutons flottants et contrôles contextuels. Le contenu n’est jamais derrière un effet de verre permanent.

### 2.6 Progressive disclosure

L’écran montre d’abord la décision. Les détails apparaissent au toucher. Une rangée ne doit pas afficher simultanément toutes les métadonnées possibles.

### 2.7 Local par défaut, explicite sinon

L’état Gemma/local doit être compréhensible sans documentation : `LOCAL`, `non installé`, `téléchargement`, `repli hors-ligne`. Aucun indicateur vague.

## 3. Modèle d’expérience

### 3.1 Boucle principale

1. **Comprendre** la journée : Aujourd’hui donne la prochaine action.
2. **Agir** : la commande lance une tâche, un focus, une fiche ou une réponse.
3. **Conserver** : le résultat devient trace, transcription, fiche ou conversation.
4. **Revenir** : Aujourd’hui se met à jour avec le prochain meilleur geste.

### 3.2 Rôle des écrans

- **Aujourd’hui** : décision et séquence. Ce n’est pas un résumé exhaustif.
- **Tableau** : gestion précise des tâches.
- **Focus** : instrument de temps et contexte d’exécution.
- **Nomi** : conversation et raisonnement local.
- **Plus** : Routines, Transcription, Fiches, Réglages.

Sur iPad, ces rôles peuvent coexister en colonnes. Sur iPhone, chaque écran reste mono-objectif.

## 4. Signature visuelle

### 4.1 Marque

Le logotype interne est `n.` : un `n` bas de casse et un point corail. Il remplace les mascottes et illustrations. Le point peut devenir indicateur d’état, mais il ne doit pas être animé en permanence.

### 4.2 Impression recherchée

- calme mais pas mou ;
- précis mais pas froid ;
- dense mais pas saturé ;
- chaleureux mais pas décoratif ;
- outil professionnel mais personnel.

## 5. Couleurs

### 5.1 Thème clair de référence

| Rôle | Valeur | Usage |
|---|---:|---|
| `background` | `#F6F1E9` | fond porcelaine chaud |
| `backgroundElevated` | `#FFFDFA` | surfaces documentaires |
| `backgroundGrouped` | `#EEE7DC` | zones groupées secondaires |
| `textPrimary` | `#191512` | texte principal |
| `textSecondary` | `#716961` | descriptions |
| `textTertiary` | `#9B938A` | métadonnées discrètes |
| `separator` | `rgba(36,27,20,0.11)` | hairlines |
| `accent` | `#D95F43` | action/attention |
| `accentText` | `#9E3F2A` | accent sur fond clair |
| `success` | `#557C68` | validation, disponible |
| `warning` | `#A66B2D` | attention non destructive |
| `danger` | `#A9463B` | suppression/erreur |

### 5.2 Thème sombre

| Rôle | Valeur | Usage |
|---|---:|---|
| `background` | `#11100F` | fond encre chaude |
| `backgroundElevated` | `#1C1A18` | surfaces |
| `backgroundGrouped` | `#252220` | zones groupées |
| `textPrimary` | `#F2EDE4` | texte principal |
| `textSecondary` | `#A99E90` | descriptions |
| `textTertiary` | `#777068` | métadonnées |
| `separator` | `rgba(255,255,255,0.11)` | hairlines |
| `accent` | `#E47B5D` | action/attention |
| `accentText` | `#F0A06F` | accent lisible |

### 5.3 Règles

- L’accent ne couvre pas plus de 8–10 % d’un écran.
- Les grandes surfaces restent neutres.
- Les erreurs ne sont pas représentées par le seul rouge : toujours texte ou icône associé.
- Les contrastes visent WCAG AA : 4,5:1 pour texte normal, 3:1 pour grand texte et contrôles.

## 6. Typographie

### 6.1 Familles iOS

- Sans : **Atkinson Hyperlegible** pour le texte de travail, les contrôles, les listes et les formulaires. Elle remplace SF Pro dans l’interface Nomi afin de renforcer la lisibilité, la distinction des caractères et l’identité produit. Les fichiers sont embarqués sous licence SIL Open Font License 1.1.
- Serif éditorial : New York / `.serif` si disponible, sinon serif système, réservé aux moments humains et documentaires.
- Mono : SF Mono / `.monospaced` pour les instruments, durées, compteurs, raccourcis et métadonnées tabulaires.
- Atkinson ne possède que les graisses Regular et Bold : les niveaux `medium` restent en Regular avec hiérarchie par couleur/taille ; `semibold` et au-delà utilisent Bold. Ne pas simuler de graisses intermédiaires.

### 6.2 Échelle iPhone

| Style | Taille | Graisse | Usage |
|---|---:|---|---|
| `displaySerif` | 34–38 | semibold | accueil, grands moments |
| `titleSerif` | 28–32 | semibold | Nomi, états vides importants |
| `title` | 22–26 | semibold | titres d’écrans fonctionnels |
| `headline` | 17 | semibold | tâche, titre de document |
| `body` | 15–16 | regular | contenu principal |
| `subhead` | 13–14 | regular | description |
| `caption` | 11–12 | medium | métadonnées |
| `microMono` | 9–11 | regular/medium | heures, compteurs, raccourcis |

### 6.3 Règles

- Un écran ne doit pas avoir plus de deux grands niveaux de titre.
- Les listes doivent rester compactes : 42–52 pt par rangée selon le contenu.
- Le serif est un accent typographique ; il n’est pas appliqué à tout.
- Dynamic Type est obligatoire, avec adaptation de densité aux tailles accessibilité.

## 7. Espacements, rayons, bordures

### 7.1 Espacement

Base 4 pt :

- 4 : micro-ajustement ;
- 8 : espacement interne compact ;
- 12 : séparation proche ;
- 16 : marge standard iPhone ;
- 20–24 : sections majeures ;
- 32 : respiration exceptionnelle.

Les écrans ne doivent pas être composés uniquement de grandes cartes à 24 pt de padding. La densité Linear/Raycast vient de paddings internes de 10–14 pt et de listes serrées.

### 7.2 Rayons

- 8–10 : petites puces, boutons, tags ;
- 12–14 : lignes importantes, petites surfaces ;
- 16–18 : cartes principales et composer ;
- 20–24 : palette et sheets ;
- continu uniquement pour les composants système/flottants.

### 7.3 Bordures et ombres

- Bordures principales : hairline 0,5–1 pt.
- Ombres très faibles dans le contenu.
- Ombres prononcées uniquement sur palette, composer flottant ou sheets.
- Éviter les doubles bordures et les halos colorés.

## 8. Matériaux et Liquid Glass

### 8.1 Hiérarchie

1. **Base** : fond opaque.
2. **Contenu** : surfaces opaques ou très légèrement teintées.
3. **Navigation/contrôles** : Liquid Glass ou matériau système.
4. **Palette/commande** : couche flottante avec blur + opacité contrôlée.

### 8.2 Interdits

- Pas de glassmorphism généralisé sur les cartes de contenu.
- Pas de texte important sur fond animé ou image décorative.
- Pas de blur permanent derrière une liste dense.

## 9. Composants

### 9.1 Commande universelle

État compact : champ de 44–48 pt avec marque `n`, placeholder « Créer, chercher ou demander… » et raccourci `⌘K` sur iPad/clavier.

État ouvert : palette flottante avec :

- input immédiat et curseur visible ;
- actions classées par pertinence ;
- icône ou initiale fonctionnelle ;
- titre + contexte en deux lignes maximum ;
- indicateur clavier (`↵`, `T`, `R`, `N`) ;
- footer avec nombre de résultats et navigation.

La palette doit être rapide : pas d’animation longue, pas de plein écran sauf petite taille/accessibilité.

### 9.2 Rangée de tâche

Structure :

```text
[checkbox] Titre compact                    P1 / heure
           métadonnées mono ou caption
```

Règles :

- hauteur normale 44–48 pt ;
- checkbox 16–18 pt, zone tactile totale ≥ 44 pt ;
- une seule priorité visuellement accentuée ;
- swipe actions : terminer, reporter, supprimer ;
- tap : détail ;
- long press : actions rapides.

### 9.3 Section

Titre compact à gauche, compteur ou métadonnée mono à droite. Pas de gros titre pour chaque groupe.

### 9.4 Carte « prochaine action »

Une seule carte importante par écran. Elle contient :

- label uppercase discret ;
- action principale ;
- justification en une phrase ;
- bouton primaire + une action secondaire.

Elle ne doit pas ressembler à une carte marketing.

### 9.5 Focus

Le Focus est un instrument :

- temps mono large mais sobre ;
- barre ou anneau minimal ;
- objectif explicite ;
- pause/reprise/fin ;
- état de notes/contexte.

Pas d’animation circulaire permanente tape-à-l’œil.

### 9.6 Conversation Nomi

État vide inspiré de Claude :

- marque ou symbole simple ;
- grande question serif ;
- indication honnête du modèle/local ;
- suggestions utiles et non enfantines ;
- composer immédiatement visible.

État actif :

- bulles sobres ou contenu en flux documentaire ;
- réponses structurées ;
- actions copier/régénérer ;
- indicateur local/modèle discret.

### 9.7 Composer

- hauteur 48–56 pt ;
- attachement, champ, envoi ;
- matériau système/Liquid Glass ;
- reste visible clavier ouvert ;
- ne recouvre pas le contenu sans compensation de scroll.

### 9.8 Tab bar iPhone

Cinq destinations maximum. Indicateur sobre, matériau système, aucune forme décorative supplémentaire. La commande peut exister dans le contenu, mais ne doit pas entrer en collision avec la tab bar.

### 9.9 Sidebar iPad

Sidebar claire et stable, entrées compactes, compteurs à droite. Le contenu est en deux colonnes si l’espace le permet. La commande reste accessible en haut ou via `⌘K`.

### 9.10 Sheets et alertes

Les sheets privilégient les formulaires courts. Une sheet ne doit pas devenir un second écran complet sauf création/édition complexe.

## 10. Navigation et interaction

### 10.1 Gestes

- tap : ouvrir/exécuter ;
- swipe horizontal tâche : actions rapides ;
- long press : menu contextuel ;
- pull down léger : ouvrir palette si validé en test ;
- swipe back iOS standard.

### 10.2 Clavier iPad

Raccourcis cibles :

- `⌘K` commande ;
- `⌘N` nouvelle tâche/note ;
- `Échap` fermer palette/sheet ;
- `↑↓` navigation palette ;
- `↵` exécuter ;
- `espace` pause/reprise focus quand le contexte est Focus.

### 10.3 États

Chaque composant doit être défini pour : vide, chargement, erreur, hors-ligne, modèle absent, permission refusée, succès.

## 11. Motion et haptique

### 11.1 Motion

- 120–180 ms : micro-interactions ;
- 220–320 ms : sheets/palette ;
- courbes naturelles, pas de rebond excessif ;
- mouvement fonctionnel : ouvrir, sélectionner, compléter.

### 11.2 Haptique

- succès léger : tâche terminée ;
- impact moyen : démarrage/arrêt focus ;
- notification : erreur ou permission requise.

Pas d’haptique décorative continue.

## 12. Iconographie

SF Symbols uniquement au lancement. Style :

- `regular` ou `medium` ;
- taille 15–18 pt dans les listes ;
- pas plus d’une icône par rangée ;
- pas d’emojis dans les contrôles ;
- les initiales peuvent remplacer une icône dans la palette si cela améliore la densité.

## 13. Accessibilité

- zones tactiles ≥ 44×44 pt ;
- Dynamic Type complet ;
- contraste AA ;
- Reduce Motion : suppression des animations non essentielles ;
- VoiceOver : chaque rangée annonce titre, état, priorité et action ;
- pas d’information uniquement par couleur ;
- palette entièrement utilisable au clavier ;
- les compteurs mono doivent avoir des labels accessibles en langage naturel.

## 14. Copywriting

Ton : direct, calme, humain, sans jargon marketing.

Exemples :

- « 25 minutes suffisent pour une version montrable. »
- « Gemma 4 fonctionne sur cet appareil. »
- « Aucun envoi réseau pour cette conversation. »
- « Cette fonction n’est pas encore disponible. »

Interdits :

- « Super ! » systématique ;
- emojis décoratifs ;
- promesses techniques non vérifiées ;
- messages vagues comme « quelque chose s’est mal passé » sans cause ou action.

## 15. Anatomie des écrans

### 15.1 Aujourd’hui

1. date/contexte discret ;
2. salutation ou titre éditorial ;
3. commande ;
4. prochaine action unique ;
5. priorités du jour ;
6. trajectoire horaire ;
7. focus actif s’il existe.

Ne pas afficher simultanément toutes les cartes possibles.

### 15.2 Tableau

1. titre + création rapide ;
2. filtres ;
3. liste dense ;
4. focus actif uniquement s’il est utile ;
5. pas de statistiques décoratives.

### 15.3 Focus

1. objectif ;
2. temps ;
3. progression ;
4. contrôles ;
5. notes/contexte.

### 15.4 Nomi

1. conversation ou état vide ;
2. composer ;
3. état modèle discret.

### 15.5 Transcription

1. consentement/permission ;
2. état micro ;
3. transcript documentaire ;
4. actions export/fiche.

### 15.6 Réglages Gemma

1. état installé/non installé ;
2. modèle actif ;
3. stockage ;
4. téléchargement/suppression ;
5. confidentialité.

## 16. Implémentation SwiftUI — contrat

### 16.1 Tokens attendus

Créer des tokens explicites :

- `NomiColor.background`, `.surface`, `.grouped`, `.text`, `.secondaryText`, `.separator`, `.accent` ;
- `NomiFont.displaySerif`, `.titleSerif`, `.headline`, `.body`, `.caption`, `.microMono` ;
- `NomiSpace.xs/sm/md/lg/xl` ;
- `NomiRadius.control/card/palette`.

### 16.2 Règles de code

- pas de couleur hex inline dispersée ;
- pas de rayon magique répété ;
- composants réutilisables pour rows, section headers, command input, badges ;
- séparation claire entre design system et features ;
- previews en clair/sombre, iPhone/iPad, Dynamic Type.

### 16.3 iOS 17 vs iOS 26

Le design reste fonctionnel sous iOS 17. Les effets Liquid Glass iOS 26 sont des améliorations progressives avec fallback matériau système.

## 17. Critères de qualité

Une maquette ou implémentation est rejetée si :

- elle ressemble à un template de dashboard ;
- chaque contenu est dans une carte arrondie ;
- l’accent est partout ;
- les paddings sont uniformément grands et vides ;
- les icônes/emoji remplacent la hiérarchie ;
- il n’y a pas de densité réelle ;
- l’état vide est décoratif plutôt qu’utile ;
- les métadonnées ne sont pas alignées ;
- l’écran ne dit pas quoi faire ensuite.

Une direction est acceptable seulement si :

- on peut imaginer Raycast/Linear la livrer sans rougir ;
- elle reste chaleureuse comme Claude sans devenir illustrative ;
- elle est plus précise que la version actuelle ;
- elle exploite les contraintes natives iOS au lieu de les imiter vaguement.

## 18. Décisions à valider

1. La commande universelle est-elle visible sur l’écran Aujourd’hui en permanence, ou seulement accessible par bouton/geste ?
2. Le serif éditorial doit-il être limité à Aujourd’hui/Nomi ou étendu aux titres principaux ?
3. La conversation Nomi doit-elle utiliser des bulles ou un flux documentaire plus sobre ?
4. Focus doit-il être un instrument circulaire, une barre fine ou un simple temps mono ?
5. Sur iPad, la sidebar doit-elle être persistante ou masquable comme Notion ?
6. Le logotype interne `n.` est-il acceptable ou faut-il conserver le symbole orbital actuel ?
7. Les identifiants internes type `NOM-124` apportent-ils une crédibilité Linear ou sont-ils trop « enterprise » pour un produit personnel ?

## 19. Prochaine étape

Après validation de ce langage, produire uniquement trois maquettes haute-fidélité :

1. iPhone — Aujourd’hui + palette ouverte ;
2. iPhone — Nomi conversation ;
3. iPad — workspace Aujourd’hui/Tableau/Focus.

Chaque maquette devra inclure états, interactions, mesures principales et variantes clair/sombre avant toute implémentation.
