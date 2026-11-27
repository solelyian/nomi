# Mesure d'écart — phase 1

Date : 2026-11-27. Corpus : `calc-spec/vectors.json` (78 vecteurs, 61 testables,
17 erreurs non mesurables dynamiquement sur les implémentations actuelles).
Méthode : les implémentations **existantes et non modifiées** sont compilées dans
des harnais jetables et exécutées contre le corpus. Vecteurs `ok` seulement — les
implémentations actuelles ne produisent pas la taxonomie d'erreurs typée de la
spécification, les vecteurs `error` ne sont donc pas mesurables tel quel.

## Résultats

| Implémentation | Réussite | Échec | Non mesurable |
|---|---|---|---|
| Windows `Calc.Evaluate` (decimal .NET) | **50/61** | 10 + 1 artefact | 17 |
| iOS `CalculationVerifier` (Decimal Swift) | **39/61** | 22 | 17 |

## Écarts mesurés — Windows (`Nomi.WinUI/Calc.cs`)

1. **Affichage à 2 décimales au lieu de 4** (8 des 10 échecs) : `Calc.Format`
   utilise `#,0.##` — `22/7` affiche `3,14` au lieu de `3,1429`, `1/16` affiche
   `0,06` au lieu de `0,0625`. Écart de conformité à la spécification figée ;
   trancher si la spécification doit rester à 4 décimales ou si l'affichage
   produit reste à 2 — mais alors la règle doit être écrite, pas implicite.
2. **Inexactitude cachée par l'affichage** : `1/3 + 1/3 + 1/3` *passe* le test
   d'affichage alors que la valeur interne `decimal` est
   `0,9999999999999999999999999999` — la division en `decimal` arrondit à 28–29
   chiffres, et l'arrondi d'affichage masque l'erreur. C'est exactement le mode
   de défaillance que l'arithmétique rationnelle élimine : **la mesure au niveau
   affichage sous-estime l'écart d'exactitude interne**.
3. `ident-001` : artefact de mesure — l'API publique `Calc.Evaluate` ne prend pas
   de table d'identificateurs ; les plans multi-étapes les supportent via `Run`.
   Non compté comme écart.

## Écarts mesurés — iOS (`NomiCore/CalculationVerifier.swift`)

1. **Puissance absente de la grammaire** (7 échecs) : `^` est `unsupported` —
   `2^10`, `2^-2`, `0.1^10` tous refusés.
2. **Verdicts `mismatch` sur arrondis corrects** (9 échecs) : le vérificateur
   compare la valeur recalculée au résultat affiché avec une tolérance fixe de
   1e-6. `22/7 = 3,1429` — pourtant un arrondi half-up correct à 4 décimales —
   est déclaré `mismatch`. La spécification (section 5) exige qu'un résultat
   affiché arrondi correctement soit `verified`. Écart majeur : **le vérificateur
   actuel accuse à tort des calculs justes**.
3. **Pourcentages chaînés** : `20%%` refusé (le parseur n'accepte qu'un seul `%`).
4. **Pas d'identificateurs ni de plans multi-étapes** : `remise * 2` refusé.
5. Même inexactitude `Decimal` interne que Windows sur les divisions non
   terminales — masquée par l'affichage, mesurée par l'oracle.

## Écarts structurels communs (non mesurables par le corpus seul)

- aucune arithmétique rationnelle : les deux implémentations utilisent le type
  décimal natif de leur plateforme, dont les arrondis internes **diffèrent entre
  .NET et Swift** — la reproductibilité cross-plateforme n'est pas garantie ;
- aucun recoupement par implémentation indépendante côté iOS (Windows recoupe en
  `double`, à tolérance) ;
- aucune analyse dimensionnelle, aucun certificat, aucun statut `interpreted` ;
- iOS : les chiffres produits par Gemma ne passent par aucun pipeline de
  vérification — risque majeur confirmé, cf. section 2.2 du document de conception.

## Conclusion de phase 1

Le corpus est figé et **internement cohérent à 78/78** sous l'oracle de référence
(`oracle.py`, fractions exactes de la stdlib Python — indépendant des deux
implémentations). Les écarts mesurés confirment quantitativement la nécessité de
la phase 2 : réécriture en rationnels exacts, grammaire complète, arrondi
d'affichage spécifié, verdict de vérification conforme à la section 5.

Question ouverte remontée par la mesure : l'affichage produit doit-il rester à
2 décimales (habitude Windows actuelle) ou passer à 4 (spécification) ? La
spécification fige 4 ; si la décision produit diffère, modifier `SPEC.md` en
version 1.1 plutôt que d'accommoder le code.
