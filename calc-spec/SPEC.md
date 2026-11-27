# Spécification du noyau de calcul déterministe de Nomi

`nomi-calc-spec/1.0` — figée. Toute modification exige une incrémentation de version
et une entrée dans le journal des changements en fin de document.

Ce document est la source de vérité unique des deux implémentations
(`Nomi.WinUI/Calc/` en C#, `NomiCore/Calculation/` en Swift). Un écart entre une
implémentation et cette spécification est un bogue de l'implémentation ; un écart
entre deux implémentations conformes en apparence révèle une ambiguïté de cette
spécification et doit être résolu ici, jamais dans le code.

---

## 1. Représentation des valeurs

### 1.1 Arithmétique exacte

Toute valeur du chemin de calcul est un **rationnel exact** : un couple
numérateur/dénominateur d'entiers arbitrairement grands, réduit (pgcd = 1), avec
dénominateur strictement positif. Zéro est `0/1`.

- Les littéraux décimaux sont convertis en fractions exactes : `0,1` = `1/10`,
  `2,50` = `5/2`.
- `+ − × ÷` sont exacts. La division par zéro est une erreur `division-by-zero`,
  jamais une approximation.
- `%` postfixe divise par 100 — exactement.
- `^` n'accepte que des exposants **entiers** dans `[-64, 64]` ; calcul exact par
  multiplications répétées. Tout autre exposant est une erreur `exponent`.

### 1.2 Bornes de ressources

| Borne | Valeur | Dépassement |
|---|---|---|
| Longueur d'une expression (caractères) | 200 | erreur `too-long` |
| Nombre d'étapes d'un plan | 12 | erreur `too-many-steps` |
| Chiffres significatifs d'un numérateur ou dénominateur intermédiaire | 10 000 | erreur `overflow` |
| Longueur d'un identificateur | 32 | erreur de grammaire |
| Profondeur d'imbrication des parenthèses | 32 | erreur `too-deep` |

Aucune approximation silencieuse : au-delà des bornes, erreur typée.

### 1.3 Arrondi — uniquement à l'affichage

L'arrondi n'existe pas dans le calcul. Il intervient une seule fois, dans
`format`, selon la règle unique :

- arrondi **half-up** (away from zero : `0,5` → `1`, `−0,5` → `−1`) ;
- 4 décimales par défaut ; 2 décimales pour les valeurs dimensionnées monnaie ;
- les zéros terminaux sont supprimés (`2,50` → `2,5` ; `3,00` → `3`) ;
- si la valeur exacte est entière, aucune décimale n'est affichée.

Exemples normatifs : `1/3` → `0,3333` ; `2/3` → `0,6667` ; `10/4` → `2,5` ;
`-1/8` → `-0,125` ; `1/6` en monnaie → `0,17`.

### 1.4 Locale

La grammaire interne est **invariante** : le séparateur décimal interne est le
point, il n'y a pas de séparateur de milliers dans la forme canonique. La locale
n'intervient qu'à deux frontières, jamais entre les deux :

1. **lecture** — normalisation des entrées utilisateur (section 3.3) ;
2. **affichage** — `format` rend le séparateur décimal de la locale et groupe les
   milliers en français selon la règle exacte suivante : la partie entière est
   groupée par trois chiffres avec l'espace fine insécable U+202F dès qu'elle
   comporte **plus de 3 chiffres** (`1500` → `1 500`, `1000000` → `1 000 000`,
   `500` → `500` inchangé) ; en anglais, la virgule est le séparateur de milliers.
   La partie fractionnaire n'est jamais groupée.

---

## 2. Grammaire

```ebnf
plan        = { step }
step        = identifier ":" expression [ unit ]
expression  = term { ("+" | "-") term }
term        = factor { ("*" | "/" | "×" | "÷") factor }
factor      = ["+" | "-"] postfix [ "^" factor ]
postfix     = atom { "%" }
atom        = number | identifier | "(" expression ")"
number      = digits [ "." digits ] | digits
identifier  = letter { letter | digit | "_" }
```

Notes normatives :

- `×` et `÷` Unicode sont des alias exacts de `*` et `/` ; `−` (U+2212) est un
  alias exact de `-`. La normalisation a lieu avant toute tokenisation.
- La multiplication implicite (`2(3+4)`, `2x`) est **refusée** : erreur de
  grammaire. L'ambiguïté n'est jamais résolue par devinette.
- `^` est associatif à droite : `2^3^2` = `2^(3^2)` = 512.
- Les opérateurs `+`/`-` unaires se chaînent : `--3` = 3.
- `%` postfixe s'applique à l'atome ou au groupe qui précède immédiatement :
  `(80+20)%` = 1. `50% + 50%` = 1. Dans `a + b%`, le pourcentage porte sur `b`
  seul — jamais sur `a + b` ; les pourcentages de pourcentages ne sont pas
  interprétés comme relatifs à la base du membre gauche (contrairement à la
  convention des calculatrices grand public, jugée source d'erreurs).
- Les identificateurs ne peuvent référencer que des étapes **antérieures** du
  même plan. Un identificateur inconnu ou postérieur est une erreur
  `unknown-identifier`.

### 2.1 Erreurs de grammaire — taxonomie

| Code | Condition |
|---|---|
| `parse-error` | entrée hors grammaire, avec position du premier caractère fautif |
| `parenthesis` | parenthèse ouvrante/fermante non appariée |
| `trailing` | tokens résiduels après une expression complète |
| `division-by-zero` | diviseur exactement nul |
| `exponent` | exposant non entier ou hors `[-64, 64]` |
| `unknown-identifier` | référence inconnue ou postérieure |
| `too-long` / `too-many-steps` / `too-deep` / `overflow` | bornes de la section 1.2 |

Toute erreur est typée et positionnée ; aucune n'est rétrogradée en approximation.

---

## 3. Frontières

### 3.1 Dimensions

Chaque valeur porte une dimension : `none`, `currency(code)`, `duration(unit)`,
`percent`. Règles :

- addition/soustraction : dimensions identiques exigées, sinon `dimension-mismatch` ;
- `currency × percent → currency` ; `currency ÷ currency → none` (ratio) ;
- `none × currency → currency` ; multiplication/division de deux monnaies : `dimension-mismatch` ;
- deux codes monnaie différents dans un même plan : `currency-mixing` — aucune conversion n'existe dans le noyau ;
- `duration` : une seule unité par calcul en v1.0 ; la composition d'unités (`2 h 30`) est une conversion de frontière explicitement demandée, jamais devinée.

### 3.2 Monnaies

- Symboles reconnus : `€`, `$`, `£` — associés aux codes `EUR`, `USD`, `GBP`.
- Le symbole est accepté en préfixe (`$10`) ou suffixe (`10 €`, `10€`), avec ou
  sans espace ; il est ignoré par l'arithmétique et porté par la dimension.
- Un symbole **à l'intérieur** d'un nombre (`12€50`, `1 €200`) est `unsupported` :
  la notation « euros/cents » n'est pas interprétée — l'utilisateur doit écrire
  `12,50 €`.
- Le symbole sur le résultat affiché est celui de la dimension, positionné selon
  la locale d'affichage (suffixe en français, préfixe en anglais).

### 3.3 Lecture des nombres utilisateur

À la frontière de lecture seulement :

- espaces insécables fines/larges et espaces simples entre groupes de **exactement
  3 chiffres** sont des séparateurs de milliers, supprimés ;
- `1 200` est mille-deux-cents ; `12 00` est une erreur — le groupement invalide
  n'est jamais deviné ;
- virgule **ou** point décimal admis, jamais les deux dans le même nombre sauf si
  le groupement lève l'ambiguïté (`1 200,5` ; `1,200.5` en anglais) ;
- en cas d'ambiguïté irrésoluble (`1,200` seul), la lecture dépend de la locale
  explicite de la demande : virgule décimale en français, milliers en anglais —
  et le cas est consigné comme `ambiguous-input` dans le certificat.

### 3.4 Constantes de la liste blanche

Pour l'ancrage (provenance), les seuls littéraux admis sans occurrence dans la
demande ou les documents :

```text
0 1 2 3 4 5 6 7 8 9 10 12 24 30 31 52 60 100 365 1000
```

plus les formes dérivées d'un taux présent dans les entrées (`t` ↔ `t/100`,
`1 + t/100`, `1 − t/100`). Cette liste est versionnée avec le moteur et visible
dans le certificat de calcul.

---

## 4. Vérificateur indépendant

Le vérificateur partage cette spécification mais pas le code du noyau :

- le noyau évalue l'arbre syntaxique par récursion ;
- le vérificateur réduit les mêmes expressions par machine à pile à partir des
  tokens, en différant toutes les multiplications de dénominateurs ;
- l'accord exigé est l'**égalité exacte des fractions réduites** — pas de
  tolérance numérique ;
- un désaccord produit le statut `engine-disagreement` : blocage et consignation.

## 5. Vérification des calculs visibles

Pour une affirmation `expression = résultat` :

- `verified` — recalcul exact égal au résultat affiché, à l'arrondi d'affichage
  près quand le résultat affiché comporte moins de décimales que la valeur exacte
  (le résultat affiché doit être un arrondi half-up correct de la valeur exacte) ;
- `mismatch` — recalcul réussi mais différent ; la valeur correcte est fournie ;
- `invalid` — hors grammaire ou résultat non numérique simple ;
- `unsupported` — construction hors périmètre (unités composées, monnaie interne).

## 6. Format du corpus

`calc-spec/vectors.json` — un objet :

```json
{
  "spec": "nomi-calc-spec/1.0",
  "vectors": [
    {
      "id": "arith-001",
      "category": "arithmetic | precedence | percent | power | exactness |
                   boundary | error | display | dimension | grounding",
      "input": "80 * 15%",
      "locale": "fr",
      "expect": {
        "status": "ok | error",
        "rational": "12/1",
        "display_fr": "12",
        "error": null
      }
    }
  ]
}
```

`rational` est la forme réduite exacte ; `display_fr` applique les règles de la
section 1.3 en locale française. Les vecteurs de dimension et d'ancrage utilisent
des champs supplémentaires documentés dans le corpus.

## 7. Journal des changements

| Version | Date | Changement |
|---|---|---|
| 1.0 | 2026-11-27 | Spécification initiale figée |
| 1.0.1 | 2026-11-27 | Précision normative du groupement des milliers français (seuil, caractère U+202F, partie fractionnaire non groupée) — clarification sans changement de comportement attendu |
