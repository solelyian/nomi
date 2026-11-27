# Système de calcul déterministe de Nomi

Version 0.1 — document de conception, avant implémentation.
Produit de Nyne Technologies. S'applique à Nomi Windows (WinUI 3, .NET) et Nomi iOS (Swift, NomiCore).

---

## 1. Objectif

Nomi répond à des demandes de calcul (remises, heures de travail, budgets, conversions simples, moyennes, pourcentages) dans un contexte où une erreur d'arithmétique détruit la confiance dans l'ensemble du produit. Les modèles de langage — y compris Gemma en local — ne sont pas des machines à calculer : ils prédisent des chiffres plausibles au lieu de les calculer, et rien dans leur architecture ne garantit l'exactitude d'un seul chiffre.

Ce document conçoit un **système de calcul déterministe complet**, commun dans ses principes aux versions Windows et iOS :

- un **noyau de calcul déterministe** qui évalue les expressions avec une arithmétique exacte, reproductible à l'identique sur les deux plateformes ;
- un **vérificateur de calcul déterministe**, implémentation indépendante qui recoupe chaque résultat et vérifie les calculs visibles ;
- une **frontière d'isolation** telle que l'IA ne peut ni calculer, ni altérer un résultat, ni désactiver la vérification, ni introduire des chiffres inventés dans une réponse ;
- une **chaîne de provenance** qui impose que chaque opérande provienne de la demande, des documents ou d'une étape précédente.

Principe fondateur : **l'IA propose, le déterminisme dispose**. Le modèle peut servir à comprendre la demande et à rédiger la phrase ; il ne produit jamais un chiffre qui parvienne à l'utilisateur sans être passé par le noyau ou sans être masqué.

---

## 2. État actuel et écarts

### 2.1 Windows — `Nomi.WinUI/Calc.cs` (existant, partiel)

Existe déjà :

- évaluateur exact en `decimal` (descente récursive) **et** réévaluation indépendante en `double` (shunting-yard), avec recoupement à 1e-9 relatif ;
- pipeline plan/réponse : le modèle émet un plan JSON (`steps` avec `id`, `expr`, `unit`) et une phrase patron avec `{id}` ; le moteur calcule, la composition insère les chiffres du moteur dans la phrase **après** génération ;
- ancrage (`Ground`) : tout littéral d'une étape doit provenir de la demande, des documents, des étapes précédentes ou d'une liste de constantes ;
- masquage (`Mask`) des chiffres inventés dans la réponse finale, avec escalade de niveau de modèle en cas d'échec ;
- grammaire : `+ - * / ^ %`, parenthèses, identificateurs, nombres avec séparateurs locaux.

Limites actuelles :

- le recoupement `decimal`/`double` est une comparaison à tolérance, pas une preuve d'exactitude : deux implémentations du **même** standard flottant/decimal peuvent converger vers le même arrondi sans qu'il soit le résultat exact ;
- pas de spécification écrite de l'arithmétique, donc pas de portage possible à l'identique ;
- l'unité est une simple étiquette texte : aucune analyse dimensionnelle (additionner des heures et des euros n'est pas rejeté) ;
- le corpus de tests n'est pas partagé avec iOS.

### 2.2 iOS — `NomiCore/CalculationVerifier.swift` (existant, partiel)

Existe déjà :

- vérificateur de calculs visibles `expression = résultat` en `Decimal`, avec statuts `verified / mismatch / invalid / unsupported` ;
- gestion des monnaies (une seule par calcul, placement du symbole validé), des milliers groupés, du pourcentage postfixe ;
- quand **l'utilisateur** tape un calcul, la réponse est produite directement par le vérificateur, sans IA.

Limites actuelles :

- les réponses **de Gemma** ne passent pas par le pipeline : le prompt demande au modèle de « ne pas remplacer le vérificateur », ce qui est une instruction sans force — exactement le type de biais que ce système doit rendre impossible ;
- pas de plan multi-étapes, pas d'ancrage de provenance, pas de masquage des chiffres inventés ;
- pas de réévaluation indépendante : une seule implémentation, donc aucune détection de ses propres bogues ;
- pas de vérificateur pour les calculs que le modèle afficherait dans du texte libre.

### 2.3 Écart cible

| Capacité | Windows | iOS | Cible |
|---|---|---|---|
| Noyau exact spécifié | partiel | partiel | **oui, spécifié et versionné** |
| Vérificateur indépendant | oui (à tolérance) | non | **oui, exact** |
| Pipeline plan → calcul → composition | oui | non | **oui, les deux** |
| Ancrage de provenance | oui | non | **oui, les deux** |
| Masquage des chiffres inventés | oui | non | **oui, les deux** |
| Analyse dimensionnelle | non | non | **oui** |
| Corpus de conformité partagé | non | non | **oui, CI les deux plateformes** |
| Journal/certificat de calcul | partiel (lignes posées) | non | **oui** |

---

## 3. Problématiques

### 3.1 Comment l'IA peut fausser un calcul

Par ordre de gravité :

1. **Chiffre inventé dans la narration** — le modèle écrit « il vous reste 68 € » sans aucun calcul. C'est le mode d'échec le plus fréquent et le plus dangereux, parce que le chiffre est présenté avec assurance.
2. **Erreur d'arithmétique sur un calcul correctement posé** — `80 × 15 %` évalué à 14 au lieu de 12. Le raisonnement est bon, le chiffre est faux.
3. **Plan mal posé** — le modèle choisit la mauvaise opération (diviser au lieu de multiplier un taux), le mauvais opérande (taux de remise pris sur le prix déjà remisé), ou invente un opérande absent de la demande. L'arithmétique sera exacte, la réponse sera fausse : c'est une erreur **sémantique**, hors de portée d'un évaluateur.
4. **Provenance fabriquée** — le plan cite un nombre qui n'existe ni dans la demande ni dans les documents (« TVA à 20 % » alors que l'utilisateur n'a rien dit). Cas particulier de 3, détectable.
5. **Contournement du cadre** — le modèle contourne la structure demandée : réponse hors patron, calcul « à voix haute » dans le texte libre, notation qui casse l'analyse (`12€50`), mélange de monnaies, ou reformulation d'un chiffre sous forme de texte (« soixante-huit »).
6. **Influence sur la vérification elle-même** — si le vérificateur était configurable par le modèle, invoqué par le modèle, ou si son résultat repassait par le modèle avant affichage, chaque garantie précédente s'effondre. C'est le point architectural central.

### 3.2 Problématiques de déterminisme cross-plateforme

1. **`decimal` .NET et `Decimal` Swift ne sont pas la même arithmétique.** .NET : décimal flottant 128 bits, 28–29 chiffres significatifs, arrondi interne spécifique. Swift (`NSDecimal`) : mantisse 38 chiffres, comportement de division différent. Une même division (`10 / 3`) ne produit pas nécessairement le même résultat final arrondi sur les deux plateformes. Spécifier « utilisez le type decimal natif » ne suffit donc **pas** à garantir la reproductibilité.
2. **Les types flottants binaires (`double`/`Double`) sont reproductibles** (IEEE 754) mais inexactes par construction pour le décimal usuel (0,1 n'y existe pas) : inacceptables comme chemin exact, utiles seulement comme contre-vérification bornée.
3. **Locale** : `1 200,5` vs `1,200.5` — le parsing dépend de conventions de l'utilisateur, pas du calcul. La grammaire interne doit être invariante ; la locale n'intervient qu'à la frontière (lecture et formatage d'affichage).
4. **Versionnement** : toute correction du noyau change potentiellement un résultat. Sans version d'architecte sémantique affichée et consignée, deux Nomi peuvent afficher des résultats différents pour la même entrée sans que personne ne sache lequel est à jour.

### 3.3 Problématiques de périmètre

1. Les conversions qui dépendent de données externes (taux de change, TVA en vigueur) ne sont **pas** déterministes par nature : elles doivent être traitées comme des opérandes fournis par l'utilisateur ou ses documents, jamais comme des constantes du moteur.
2. Les grandeurs physiques (heures + minutes) exigent une analyse dimensionnelle pour rester exactes ; sans elle, le système doit refuser plutôt que deviner.
3. « Infaillible » ne peut pas signifier « la bonne réponse à la mauvaise question » : la pertinence sémantique du calcul posé reste une responsabilité humaine, et le système doit la rendre visible au lieu de la masquer.

---

## 4. Architecture

### 4.1 Vue d'ensemble

```text
Demande utilisateur / documents
        │
        ▼
┌──────────────────┐   plan non fiable (JSON contraint)    ┌─────────────────────┐
│  IA (Gemma/LLM)  │ ───────────────────────────────────▶  │  Validation du plan │
│  zone non fiable │                                        │  (schéma, grammaire) │
└──────────────────┘                                        └─────────┬───────────┘
        │                                                             │
        │ phrase patron (texte + {id}, sans chiffres)                 ▼
        │                                                   ┌─────────────────────┐
        │                                                   │  NOYAU DÉTERMINISTE │
        │                                                   │  arithmétique exacte │
        │                                                   └─────────┬───────────┘
        │                                                             │ résultats exacts
        │                                                             ▼
        │                                                   ┌─────────────────────┐
        │                                                   │  VÉRIFICATEUR       │
        │                                                   │  réimplémentation   │
        │                                                   │  indépendante       │
        │                                                   └─────────┬───────────┘
        │                                                             │ accord exigé
        │                                                             ▼
        │                                                   ┌─────────────────────┐
        │                                                   │  Provenance         │
        │                                                   │  (ancrage opérandes)│
        │                                                   └─────────┬───────────┘
        ▼                                                             ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│  Composition : insertion des chiffres du moteur dans la phrase APRÈS          │
│  génération + masquage de tout chiffre non traçable + statut affiché          │
└──────────────────────────────────────────────────────────────────────────────┘
```

Deux chemins d'entrée :

- **Chemin direct** (sans IA du tout) : l'utilisateur tape un calcul explicite (`80 × 15 % = …`). Le vérificateur répond seul. Ce chemin existe déjà sur les deux plateformes et reste le socle de confiance.
- **Chemin assisté** : demande en langage naturel. L'IA propose un plan ; seul le pipeline ci-dessus produit les chiffres.

### 4.2 Règles d'isolation (non négociables)

1. **Flux de données à sens unique** : l'IA → le moteur, jamais l'inverse avant affichage. Le résultat du calcul ne repasse jamais dans le modèle pour être « reformulé » ; la composition est mécanique.
2. **Le noyau ne connaît pas l'IA** : bibliothèque pure, aucune dépendance vers les services d'inférence, aucune entrée/sortie réseau, horloge ou hasard. Dans les deux codebases, le noyau vit dans un module qui ne peut même pas importer le code du modèle (à vérifier en revue de dépendances / lint d'architecture).
3. **La vérification n'est pas désactivable** : ni par le modèle, ni par une préférence utilisateur, ni par un chemin de repli. Une réponse calculatoire qui échoue la vérification est masquée ou refusée, jamais affichée telle quelle.
4. **La sortie du modèle est une entrée non fiable** : traitée comme telle — validation de schéma, grammaire whitelist, limites de taille, aucune évaluation dynamique (pas d'`eval`, pas d'injection d'identificateurs arbitraires).
5. **Le statut est produit par le client, pas par le modèle** : les badges « calcul vérifié localement » sont émis par le pipeline déterministe ; aucun texte généré ne peut les imiter dans l'UI (le statut est une donnée structurée, pas une chaîne).
6. **Version épinglée et affichée** : chaque certificat de calcul porte la version du noyau ; le comportement ne dépend pas de la version du modèle.

### 4.3 Modules cibles

```text
nomi-calc-spec        corpus JSON partagé + grammaire + vecteurs de conformité (dépôt unique)
Nomi.WinUI/Calc/      noyau C# (réécriture conforme), vérificateur, composition, ancrage
NomiCore/Calculation/ noyau Swift (conforme), vérificateur, composition, ancrage
```

---

## 5. Le noyau de calcul déterministe

### 5.1 Arithmétique : rationnels exacts

Décision de conception centrale : le chemin exact n'utilise **ni** `decimal` **ni** `double`, mais une **arithmétique rationnelle exacte** (numérateur/dénominateur en entiers arbitrairement grands) :

- littéraux décimaux → fractions exactes (`0,1` = 1/10) ;
- `+ - * /` exacts ; division par zéro rejetée ;
- `%` postfixe = division par 100, exacte ;
- puissance limitée aux exposants entiers dans [-64, 64], exacte par multiplication répétée (bornes strictes pour la taille des entiers) ;
- l'arrondi n'existe **que** à la frontière d'affichage, avec une règle unique : arrondi « half-up » (away from zero) à 4 décimales par défaut, 2 pour les valeurs monétaires affichées — spécifié, jamais implicite.

Propriété clé : les entiers arbitrairement grands se comportent **à l'identique dans toutes les implémentations correctes** (ce sont de simples suites de chiffres). La reproductibilité cross-plateforme ne dépend plus d'aucun type natif flottant. .NET fournit `BigInteger` en standard ; Swift nécessite une petite implémentation interne (quelques centaines de lignes, entièrement testée) ou une dépendance épinglée auditée — à trancher à l'implémentation, préférence pour l'implémentation interne afin de garder zéro dépendance dans le noyau.

Garde-fous de ressources : longueur maximale des littéraux, borne de taille des numérateurs/dénominateurs intermédiaires (au-delà : `unsupported`, jamais d'approximation silencieuse), nombre maximal d'étapes (12, comme aujourd'hui).

### 5.2 Grammaire (identique sur les deux plateformes)

```ebnf
expression  = term { ("+" | "-") term }
term        = factor { ("*" | "/" | "×" | "÷") factor }
factor      = ["+" | "-"] postfix [ "^" factor ]
postfix     = atom { "%" }
atom        = number | identifier | "(" expression ")"
number      = chiffres [ séparateur-décimal chiffres ]   (* milliers groupés admis à la frontière *)
identifier  = lettre { lettre | chiffre | "_" }          (* étapes précédentes uniquement *)
```

Tout le reste est rejeté : pas de fonctions implicites, pas de constantes cachées (`π` etc.) dans une première version, pas d'unités collées interprétées hors du système dimensionnel. La multiplication implicite (`2(3+4)`) est **refusée** car ambiguë.

### 5.3 Unités et analyse dimensionnelle

Chaque valeur porte une dimension : `aucune`, `monnaie(<code>)`, `temps(<unité>)`, `pourcent` — pas de conversion implicite :

- addition/soustraction : dimensions identiques exigées ;
- multiplication/division : règles explicites (monnaie × taux → monnaie ; monnaie ÷ monnaie → ratio) ;
- mélange de monnaies : refusé (déjà le cas sur iOS) ;
- heures/minutes : la notation `2 h 30` est convertie à la frontière en une seule unité déclarée, jamais devinée — `2 heures + 30 minutes` reste `unsupported` tant que la conversion n'est pas explicitement demandée.

### 5.4 API du noyau (contrat identique, deux implémentations)

```text
parse(expression)        -> arbre | erreur-positionnée
evaluate(plan, entrées)  -> [étape: valeur rationnelle, dimension] | erreur typée
format(valeur, règle)    -> chaîne d'affichage (seule frontière dépendant de la locale)
```

Le noyau est total sur son domaine : toute entrée hors grammaire produit une erreur typée, jamais une approximation.

---

## 6. Le vérificateur de calcul déterministe

### 6.1 Deux rôles distincts

1. **Recoupement interne** : chaque évaluation du noyau est recalculée par une seconde implémentation **structurellement différente** — évaluation directe de l'arbre pour le noyau, évaluation par machine à pile (ou réduction de fractions différée) pour le vérificateur. L'accord exigé est l'**égalité exacte des fractions réduites** — pas une tolérance. Un désaccord est un défaut d'implémentation : la réponse est bloquée, l'incident consigné. Ce mécanisme ne protège pas contre une erreur de spécification partagée, mais contre les bogues de chaque implémentation.
2. **Vérification des calculs visibles** : toute affirmation `expression = résultat` — qu'elle vienne de l'utilisateur, d'un document cité, ou du texte du modèle — est revérifiée. Statuts : `verified`, `mismatch` (avec la valeur correcte calculée), `invalid`, `unsupported`. Un `mismatch` dans le texte du modèle invalide la réponse entière.

### 6.2 Indépendance

Le vérificateur partage la spécification mais pas le code du noyau : pas d'appel à l'évaluateur principal, pas de structure de données commune au-delà des types de la spécification. Sur chaque plateforme, les deux implémentations sont écrites dans des styles différents (récursif vs itératif) pour réduire le risque de bogue corrélé. La probabilité qu'un même bogue produise le même faux résultat dans deux implémentations indépendantes de la même spécification est faible — et c'est précisément ce que le corpus de conformité (§8) vient borner.

---

## 7. Provenance, composition et masquage

### 7.1 Ancrage (grounding)

Chaque littéral de chaque étape du plan doit être traçable vers :

- un nombre présent dans la demande ou les documents joints ;
- le résultat d'une étape précédente (par identificateur) ;
- une constante de la liste blanche (comptes usuels et unités de temps : 7, 12, 24, 30, 60, 100, 365, 1000…) — liste versionnée et visible dans le certificat ;
- les formes dérivées d'un taux (20 ↔ 0,2 / 1,2 / 0,8), déjà prévues sur Windows.

Tout littérale intraçable invalide l'étape. Un plan avec une seule étape non ancrée ne produit **aucune** réponse chiffrée — escalade ou déclinaison honnête (« je ne peux pas vérifier ce calcul »), jamais de chiffre partiel présenté comme fiable.

### 7.2 Composition mécanique

Le modèle écrit la phrase **sans chiffres**, avec des marqueurs `{id}`. La composition :

1. substitue les valeurs formatées du moteur — après la fin de la génération, dans le code client ;
2. échoue si un marqueur est manquant ou non vérifié ;
3. balaie ensuite le texte final : tout chiffre (numérique **ou** en toutes lettres, « soixante-huit ») non traçable vers les valeurs du moteur ou les entrées est masqué (`…`) ; au-delà d'un chiffre masqué, la réponse est remplacée par le refus honnête.

### 7.3 Certificat de calcul

Chaque réponse calculatoire embarque une structure (pas du texte libre) :

```json
{
  "engine": "nomi-calc/1.0",
  "steps": [{"id": "remise", "expr": "80 * 15%", "value": "12", "unit": "EUR"}],
  "grounding": "anchored",
  "verifier": "agree",
  "status": "verified"
}
```

Affiché dans l'UI sous forme de calcul posé (`remise = 80 × 15 % = 12 €`), c'est ce qui rend le calcul **auditable par l'utilisateur** : il voit exactement ce qui a été calculé, et peut déceler un plan mal posé — la partie que le système ne peut pas garantir à sa place.

---

## 8. Conformité et stratégie de test

Le dépôt porte un corpus unique `calc-spec/vectors.json` : grammaire, parsing, arrondis d'affichage, cas limites (division par zéro, milliers ambigus, monnaies mal placées, pourcentages chaînés, exposants bornes, entiers géants), cas d'ancrage et de masquage. Les deux plateformes exécutent **le même corpus** en CI ; un écart est un échec de build.

Au-delà du corpus figé :

- **tests de propriétés** : expressions générées aléatoirement (graine fixe par commit pour la reproductibilité), comparaison noyau/vérificateur sur les deux plateformes — égalité exacte exigée ;
- **test différentiel cross-plateforme** : un export JSON de résultats intermédiaires sur un corpus large, comparé en CI entre les artefacts Windows et iOS ;
- **fuzzing de parsing** : entrées arbitraires — le seul résultat acceptable est une erreur typée, jamais un crash ni une boucle ;
- **tests de non-biais** : prompts adversariaux (« ignore le vérificateur », réponses pré-remplies avec de faux badges, chiffres en toutes lettres) démontrant que le pipeline ne peut pas être contourné depuis le texte du modèle ;
- **mutants** : introduction volontaire de bogues dans le noyau pour prouver que le recoupement et le corpus les détectent.

---

## 9. Taxonomie des erreurs et expérience utilisateur

| Statut | Signification | Conduite |
|---|---|---|
| `verified` | calcul exact, ancré, recoupé | réponse affichée, calcul posé visible |
| `mismatch` | résultat affiché ≠ recalcul | valeur correcte affichée, écart signalé |
| `ungrounded` | opérande intraçable | pas de chiffre ; explication honnête |
| `invalid` | hors grammaire | reformulation demandée, aucune devinette |
| `unsupported` | unités/conversion hors périmètre | périmètre expliqué, pas de conversion inventée |
| `engine-disagreement` | noyau ≠ vérificateur | blocage + consignation — ne devrait jamais arriver en production |

Les messages restent en langage clair, sans jargon, et l'état `verified` n'est jamais imitable par du texte généré.

## 10. Limites honnêtes

1. **Exactitude ≠ pertinence** : le système garantit que le calcul posé est exact et ancré, pas que c'est le bon calcul pour la situation. Le calcul posé visible est la réponse à cette limite.
2. Les données externes (taux de change, fiscalité) sont hors périmètre déterministe : acceptées comme opérandes déclarées, jamais comme faits du moteur.
3. Le recoupement interne borne les bogues d'implémentation sans les exclure mathématiquement ; le corpus partagé et les tests de propriétés bornent les erreurs de spécification. « Infaillible » signifie ici : aucun chiffre ne peut être altéré, inventé ou contourné par l'IA, et toute erreur d'implémentation est bloquante plutôt que silencieuse.
4. La reconnaissance des chiffres en toutes lettres dans le masquage a une couverture bornée (français/anglais usuels) ; ce qui n'est pas reconnu comme nombre n'est pas masquable — la composition structurée rend ce chemin marginal.

## 11. Plan d'implémentation proposé

1. **Phase 1 — spécification et corpus** : figer grammaire, arithmétique rationnelle, règles d'arrondi et `calc-spec/vectors.json` ; faire passer le corpus sur les implémentations actuelles pour mesurer l'écart.
2. **Phase 2 — noyaux** : réécrire `Calc.cs` en rationnels exacts ; porter le même noyau dans `NomiCore` ; vérificateurs indépendants des deux côtés ; CI de conformité.
3. **Phase 3 — pipeline iOS** : plan JSON contraint pour Gemma, ancrage, composition mécanique, masquage — parité avec Windows.
4. **Phase 4 — dimensions et certificat** : analyse dimensionnelle, unités de temps, certificat affiché et consigné.
5. **Phase 5 — durcissement** : tests adversariaux, mutants, audit de dépendances du module noyau.

Chaque phase est vérifiable indépendamment ; les phases 1–3 suffisent à fermer le risque majeur actuel (les chiffres de Gemma sur iOS ne sont pas vérifiés du tout).
