# Nomi — prototype avec IA locale

Nomi, par Nyne Technologies : six outils de productivité pour comprendre,
vérifier, convertir, planifier, communiquer et apprendre. L’architecture reste
à valider avant la finition visuelle : [ARCHITECTURE.md](ARCHITECTURE.md).

## Deux supports

- **`Nomi.WinUI/`** : application Windows native en C# / XAML, WinUI 3, avec
  moteur llama.cpp intégré via LLamaSharp 0.26.0, sans Ollama ni serveur à installer.
- **`preview/`** : aperçu navigateur interactif, connecté au modèle local par
  le petit serveur Node de `server/`.
- **`content/`** : catalogue français–anglais partagé. Le sélecteur change les
  textes rédigés localement ; les nouvelles réponses sont générées dans la langue choisie.
- **`content/inference.json`** : modèle et consignes partagés par les deux clients.
- **`content/response-policy.json`** : règles déterministes appliquées à la réponse
  après génération, avant affichage, dans les deux clients.

L’accueil contient directement la saisie, les six intentions et le format de
réponse. Choisir une intention conserve la demande en cours ; « Lancer » démarre
l’inférence sans écran intermédiaire. Les raccourcis d’exemple remplissent la
saisie et sélectionnent l’intention correspondante, sans envoyer la demande.
Les espaces utilisés restent accessibles pendant la session ; ils ne sont pas
sauvegardés après fermeture ou rechargement. La génération peut être arrêtée.
Nomi attend la fin de la réponse, applique ses règles puis affiche le texte adapté,
sélectionnable et copiable. Les quatre contextes ouvrent ces mêmes outils.

## Adaptation des réponses aux directives Nomi

Le modèle produit un brouillon en mémoire. Un algorithme indépendant du modèle
applique ensuite les règles de `content/response-policy.json` :

1. Simplifier les titres Markdown et les emphases en début de ligne, nettoyer les
   espaces de fin de ligne et retirer quelques formules familières ciblées
   comme « Bravo champion ! ».
2. Écarter toute réponse contenant une formulation reconnue de demande de diagnostic,
   de présentation médicale de Nomi, de pitié, de code ou de raisonnement interne.
   La réponse entière est écartée, sans supprimer une phrase importante pour
   faire paraître le reste acceptable.
3. Aérer les lignes en mode « étapes », sans inventer de titres, réordonner des
   calculs ou couper le texte pour atteindre un nombre de mots.
4. Comparer la séquence des nombres, signes et notations décimales avant et après
   transformation. Si elle diffère, ne pas afficher la réponse.
5. Écarter un montant annoncé en première ligne qui contredit une conclusion
   explicite de prix final ou de total, dans la même monnaie. Cette règle couvre
   les symboles €, £ et $, placés avant ou après un montant à deux décimales maximum,
   et les formulations françaises–anglaises du catalogue. Les espaces de milliers
   et les variantes décimales sont comparés sans changer le texte affiché.
6. Si cette comparaison détecte une contradiction, demander au même modèle une
   seule nouvelle réponse à la demande originale. Le brouillon rejeté n’est pas
   renvoyé au modèle. La nouvelle réponse repasse par toutes les règles ; un
   deuxième échec est bloqué. Les autres motifs de rejet ne déclenchent pas de
   nouvelle tentative. L’interface indique lorsqu’une réponse a été régénérée.

Par exemple, `### **Résultat : 68 €**` devient `Résultat : 68 €`. Les calculs,
unités et hypothèses suivants sont conservés. Les transformations appliquées sont
indiquées dans « Traitement de la réponse » ; le bouton Copier utilise ce même texte.
Les chiffres ne sont ni arrondis ni convertis automatiquement.

La génération reste progressive en interne, mais **aucun fragment brut n’est
envoyé au navigateur ni affiché par WinUI**. Une interruption, une réponse
tronquée ou invalide, un dépassement de 24 000 caractères ou une règle bloquante
donne un message localisé, sans réponse partielle. Aucun deuxième modèle ni
service payant n’intervient.
La nouvelle tentative éventuelle utilise le même délai global (cinq minutes
dans WinUI, trois minutes dans l’aperçu web)
et peut aussi être annulée. Elle peut allonger l’attente.

Cette première politique utilise des règles textuelles ciblées, pas une analyse
sémantique exhaustive : elle peut manquer une paraphrase ou écarter une citation
légitime. Mentionner un terme de cours n’est pas automatiquement assimilé à une
demande de diagnostic. La politique ne prouve ni l’exactitude des calculs ni
la langue de la réponse et ne remplace pas une relecture.
La comparaison des montants reste ciblée : elle ignore notamment les conclusions
conditionnelles, les monnaies différentes et les formats qu’elle ne reconnaît pas.
Elle ne calcule pas un résultat de remplacement. Un essai réel annonçant 32,00 €
en tête puis un prix final de 68,00 € est conservé comme cas de régression et rejeté.
Le format par étapes demande désormais d’exposer les calculs ou hypothèses avant
une conclusion unique. Le même cas réel passe avec cette consigne ; cela ne
constitue pas une garantie d’exactitude sur de futures générations.

Pour faire évoluer les directives, modifier le catalogue de règles, ajouter les
libellés FR/EN si une nouvelle transformation est introduite, puis ajouter un cas
avant/après dans `checks/response-policy-cases.json`. Les mêmes cas sont exécutés
en JavaScript et en C#. Relancer les contrôles puis redémarrer Nomi.

## 1. Utiliser l’application Windows

Télécharger l’installateur depuis les [Releases](https://github.com/solelyian/nomi/releases),
puis l’ouvrir par double-clic. Lancer Nomi depuis le Bureau ou le Menu Démarrer.
Dans Nomi, choisir **Télécharger le modèle** : environ 2,5 Go sont téléchargés,
vérifiés puis chargés. La progression et le bouton **Arrêter** restent disponibles ;
un téléchargement interrompu reprend au prochain essai.

Le moteur est intégré dans l’installation. Le modèle gratuit Qwen3 4B Instruct 2507,
quantifié Q4_K_M, est téléchargé séparément depuis Hugging Face (Apache-2.0).
Le fichier, sa version, sa taille et son SHA-256 sont épinglés dans
`content/local-model.json`. Nomi n’utilise un modèle qu’après vérification complète.
À chaque ouverture suivante, choisir **Charger le modèle** : aucune connexion
Internet n’est nécessaire. Ni Ollama, ni PowerShell, ni clé API ne sont requis.

Prévoir au moins 8 Go de RAM (16 Go conseillés) et 4 Go d’espace libre.
L’inférence fonctionne sur CPU x64 ; la vitesse dépend de l’ordinateur.
Le modèle reste dans `%LOCALAPPDATA%\Nyne\Nomi\models` après une mise à jour
ou une désinstallation, pour éviter un nouveau téléchargement. Ce dossier peut
être supprimé manuellement si Nomi n’est plus utilisé.

## Modèle pour l’aperçu web uniquement

Installer [Ollama](https://ollama.com/download/windows) et ouvrir l’application.
Dans un terminal :

```sh
ollama pull qwen3:4b-instruct
ollama list
```

Utiliser exactement **`qwen3:4b-instruct`**. Ce modèle Qwen3 4B Instruct 2507
est disponible sous licence Apache 2.0, sans clé API ni abonnement.
Le téléchargement représente environ 2,5 Go. Prévoir au moins 8 Go de RAM
pour essayer ce petit modèle ; la vitesse dépend du matériel.
L’inférence a été vérifiée ici sur CPU, avec 31 Go de mémoire.
Les six demandes d’essai ont pris environ 6 à 12 secondes chacune après chargement ;
ce n’est pas une garantie de délai sur d’autres machines.

Si Ollama ne tourne pas déjà en arrière-plan, lancer `ollama serve` et laisser
ce terminal ouvert. Le service écoute par défaut sur `127.0.0.1:11434`.
Le téléchargement nécessite Internet ; les demandes suivantes utilisent le modèle local.

Le tag générique `qwen3:4b` n’est pas utilisé : lors de l’essai, il produisait
un raisonnement anglais au lieu de la réponse demandée malgré `think: false`.
La variante Instruct a été vérifiée en français et en anglais.

## 2. Ouvrir l’application WinUI sur Windows

### Installer sans compiler

Chaque tag `v*` publie sur la page **Releases** GitHub, via
`.github/workflows/windows.yml` (runner Windows) :

- `Nomi-Setup-<version>.exe` — installateur Inno Setup (`installer/nomi.iss`) :
  double-clic, raccourcis Bureau et Menu Démarrer, désinstallation depuis
  Windows ; aucune ligne de commande ni prérequis .NET ;
- `Nomi-win-x64.zip` — version portable, `Nomi.WinUI.exe` à lancer directement.

### Signature de code

Le workflow signe `Nomi.WinUI.exe` puis l’installateur avec Azure Trusted Signing
(action `azure/artifact-signing-action`, SHA-256, horodatage Microsoft) lorsque
ces secrets GitHub sont définis sur le dépôt (Settings → Secrets and variables →
Actions) :

- `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET` : app registration
  Entra ID disposant du rôle « Trusted Signing Certificate Profile Signer » sur
  le compte de signature ;
- `AZURE_TRUSTED_SIGNING_ENDPOINT` : point de terminaison régional du compte
  (ex. `https://weu.codesigning.azure.net`) ;
- `AZURE_TRUSTED_SIGNING_ACCOUNT` : nom du compte Trusted Signing ;
- `AZURE_TRUSTED_SIGNING_PROFILE` : nom du profil de certificat (Public Trust).

Chaque signature est vérifiée (`checks/windows-verify-signature.ps1`) avant la
publication. Sans ces secrets, la build reste non signée et Windows SmartScreen
peut afficher un avertissement (« Informations complémentaires → Exécuter quand
même »). Le désinstalleur généré par Inno Setup n’est pas signé.

### Compiler soi-même

Prérequis : Windows 10 version 1809 ou ultérieure, architecture x64, SDK .NET 8,
Visual Studio avec la charge de travail WinUI et Windows SDK.

Le package Windows App SDK est épinglé à 1.8.260209005 (il fixe lui-même la
version des Windows SDK Build Tools). Le projet est non empaqueté et autonome
pour simplifier l’essai local ; aucun certificat MSIX n’est requis.

Depuis le dossier racine :

```powershell
dotnet restore .\Nomi.WinUI\Nomi.WinUI.csproj
dotnet build .\Nomi.WinUI\Nomi.WinUI.csproj -c Debug -p:Platform=x64
dotnet run --project .\Nomi.WinUI\Nomi.WinUI.csproj -c Debug -p:Platform=x64
```

Le client WinUI exécute llama.cpp dans son propre processus. Node.js n’est pas requis.
Ouvrir le projet dans Visual Studio permet aussi de le lancer avec F5 en x64.
La publication autonome se fait avec :

```powershell
dotnet publish .\Nomi.WinUI\Nomi.WinUI.csproj -c Release -r win-x64 -p:Platform=x64
```

Le workflow Windows compile le XAML, installe le setup et vérifie l’activation
de la fenêtre depuis un autre répertoire de travail avant de publier les fichiers.
Les diagnostics de démarrage sont conservés en artefact du workflow.
La publication inclut explicitement l’index de ressources de l’application et
les XAML compilés : leur omission dans la publication unpackaged causait l’échec
de chargement de `MainWindow.xaml`. Le runtime Visual C++ est également livré
à côté de l’exécutable pour le moteur CPU, sans installation séparée.

Pour la validation Windows : envoyer depuis l’accueil sans navigation préalable ;
changer d’intention sans perdre la saisie ; charger un exemple ; choisir le format ;
reprendre un espace ; ouvrir chaque action et contexte ; changer de langue
dans une réponse ; essayer Ctrl+K, le filtrage, Entrée et Échap ; vérifier la
restitution du focus ; redimensionner la fenêtre ; contrôler Narrator, le contraste
élevé et l’agrandissement du texte ; copier une réponse. Confirmer aussi qu’aucun
dialogue de profil ou diagnostic n’apparaît.

## 3. Aperçu navigateur

Node.js 22 ou ultérieur. Depuis la racine du projet :

```sh
npm ci
npm run dev
```

Ouvrir `http://127.0.0.1:5173/preview/`. Le serveur utilise uniquement les modules
standard de Node. Un simple serveur statique ne suffit plus.
Ollama doit tourner sur **la même machine que le serveur Nomi**.
Dans l’aperçu Devin, il tourne donc sur la machine de cette session ;
après installation sur votre PC, il tourne sur votre PC.
L’aperçu de session peut s’arrêter quand la machine est mise en veille.

- Palette : bouton d’accueil, bouton dans l’en-tête ou Ctrl+K.
- Recherche : accents facultatifs, flèches pour parcourir, Entrée pour ouvrir.
- Échap ferme la palette et restitue le focus.
- La navigation et les exemples sont traduits en français et en anglais ;
  le modèle reçoit une consigne de langue pour chaque demande.
- Ctrl+Entrée envoie la demande ; « Arrêter » interrompt la génération.
- Le changement de page ou de langue annule une génération en cours.
- Les brouillons et réponses restent en mémoire, par action, contexte et langue,
  jusqu’à la fermeture de l’application ou au rechargement de la page.
- Préférences : langue, densité, réponses par étapes ou synthèse.
- Les préférences web sont enregistrées localement, sans compte. Le squelette
  natif les conserve uniquement pendant la session.
- La copie utilise le presse-papiers lorsque le navigateur l’autorise. Sinon,
  le texte de la réponse reste sélectionnable.

## Périmètre et limites

Chaque demande est indépendante : il n’y a pas encore d’historique de conversation
transmis au modèle. Compléter ou reformuler la demande pour préciser le contexte.
Le prototype accepte du texte collé, jusqu’à 6 000 caractères, et des fichiers joints
(voir ci-dessous) ; la transcription audio et la synchronisation restent à construire.

### Fichiers joints

Le bouton **Joindre des fichiers** (ou le dépôt dans la zone de demande, côté web)
accepte PDF, Word `.docx`, Excel `.xlsx`, PowerPoint `.pptx`, ainsi que TXT, CSV et
Markdown : jusqu’à 4 fichiers de 8 Mo. Le texte est extrait localement — dans un
`Worker` Node.js pour l’aperçu (`server/documents.js`), dans `DocumentText.cs` pour
WinUI avec PdfPig, ExcelDataReader et `System.IO.Compression` — et seul cet extrait,
limité à 8 000 caractères par fichier et 12 000 au total, est envoyé au modèle.
Aucun binaire ne quitte la machine ; les extraits sont visibles dans l’interface
avant le lancement et retirables un par un.

Chaque extrait est marqué comme matière première non fiable : les consignes du
modèle demandent d’ignorer toute instruction contenue dans un document ou son nom,
de citer page, diapositive ou cellule, et de signaler ce qui manque plutôt que
de l’inventer. Un extrait partiel (fichier long, plus de 40 pages, pages sans texte)
est signalé comme tel au modèle et à l’utilisateur.

Limites : pas de reconnaissance de caractères pour les PDF scannés ni de lecture
des images et graphiques ; les formules Excel donnent leur dernier résultat
enregistré (`[no cached result]` sinon) et leur format d’affichage entre crochets ;
les anciens formats `.doc`, `.xls`, `.ppt` et les fichiers protégés par mot de
passe sont refusés. Les archives Office sont limitées à 2 000 entrées et 32 Mo
décompressés, sans DTD ni entité XML ; macros, liens et contenus incorporés ne sont
jamais exécutés ni suivis.

Nomi ne demande aucun diagnostic. Ses consignes restent dans le registre de la
productivité et demandent d’expliquer hypothèses, unités et calculs.
Le parcours logiciel explique le raisonnement sans éditer ni exécuter de code.
Les sorties sont affichées comme texte, sans exécution de HTML ou de code.

Un modèle peut se tromper : les consignes ne constituent pas un vérificateur
mathématique déterministe. Relire les chiffres avant de les utiliser.
Les réponses sont limitées à 768 tokens et la génération à cinq minutes dans
WinUI (trois minutes dans l’aperçu web) :
une interruption ou une limite de longueur est signalée, sans afficher le brouillon.

Nomi ne journalise ni ne sauvegarde les demandes sur disque. Les préférences web
utilisent le stockage local du navigateur ; les textes restent en mémoire.
Aucun fournisseur d’inférence distant n’est appelé par le prototype.

## Dépannage et configuration

- **Démarrage Windows** : consulter `%LOCALAPPDATA%\Nyne\Nomi\logs\startup.log`.
  Les erreurs de démarrage affichent aussi une boîte de dialogue avec ce chemin.
- **Modèle absent dans WinUI** : choisir **Télécharger le modèle**, puis attendre
  la fin de la vérification et du chargement avant de lancer une demande.
- **Téléchargement interrompu** : vérifier la connexion et réessayer pour reprendre.
- **Modèle invalide** : retenter le téléchargement ; aucun fichier invalide n’est chargé.
- **Espace disque insuffisant** : libérer au moins 4 Go puis réessayer.
- **Ollama indisponible (web uniquement)** : ouvrir Ollama ou lancer `ollama serve`.
- **Modèle absent (web uniquement)** : lancer `ollama pull qwen3:4b-instruct`.
- **Modèle occupé** : attendre la fin de l’autre demande, puis réessayer.
- **Délai dépassé** : raccourcir la demande ; sur CPU, une réponse peut prendre plusieurs dizaines de secondes.
- **Réponse écartée par Nomi** : reformuler ou réessayer. Pour améliorer une règle
  trop stricte, ajouter un cas de régression avant de modifier la politique.

Pour l’aperçu web, `NOMI_MODEL` permet de choisir un autre modèle local déjà téléchargé.
`NOMI_OLLAMA_URL` permet de changer le port Ollama, avec une adresse HTTP loopback
uniquement. Relancer Nomi après modification. Les essais portent sur la variante
Qwen3 Instruct indiquée ci-dessus ; les modèles cloud et les sorties de raisonnement
ne sont pas pris en charge.

Le serveur web écoute localement par défaut (`HOST=127.0.0.1`, `PORT=5173`).
L’aperçu de session utilise `HOST=0.0.0.0` derrière le proxy privé de Devin.
Ce serveur de développement n’a pas d’authentification propre.

## Vérifications disponibles sous Linux

Node.js 22 ou ultérieur :

```sh
npm ci
npm run check
python3 checks/xml_check.py
dotnet run --project checks/Nomi.Inference.Checks
dotnet format checks/Nomi.Inference.Checks --verify-no-changes
```

`check` exécute ESLint, Prettier, TypeScript en mode strict sur le JavaScript,
puis les tests de catalogues, recherche, routage, contraste, validation de requêtes,
relais HTTP, streaming UTF-8, erreurs, concurrence et annulation.
Les tests documentaires lisent les fixtures de `checks/documents/` (générées par
`checks/make_documents.py` avec python-docx, XlsxWriter, python-pptx et reportlab),
couvrent les quatre formats, le PDF sans texte, les archives piégées (entités XML,
expansion excessive), la troncature et la route `/api/documents`.
Les tests de politique couvrent aussi les transformations FR/EN, les mathématiques
et les termes de cours à préserver, les violations en fin de flux, l’absence de
texte exposé avant la fin, les réponses tronquées et les délais dépassés.
Le projet .NET exécute les contrôles du vrai client C# avec un transport simulé,
les mêmes cas de politique, le parcours natif avec contrôle avant affichage et
la lecture C# des mêmes fixtures documentaires. Les contrôles du gestionnaire
de modèle couvrent la reprise, le hash, les plages HTTP, l’annulation et les
accès concurrents, sans téléchargement réel.
Le script Python vérifie les
documents XML natifs et les correspondances des gestionnaires XAML/C#.
Ces contrôles ne certifient pas l’accessibilité de l’interface rendue.

Pour refaire les essais avec **un vrai Ollama et le modèle installé** :

```sh
npm run test:live
dotnet run --project checks/Nomi.Inference.Checks -- --live
dotnet run --project checks/Nomi.Inference.Checks -- --policy-live
```

Le premier lance son propre serveur sur un port temporaire et exerce les six actions
en français et en anglais, après adaptation. `--live` exerce le transport Ollama C# ;
`--policy-live` exerce la politique commune via l’ancien transport Ollama.
Ces essais vérifient quelques demandes représentatives, sans mesurer la fiabilité
générale du modèle. Aucun test de l’interface dans le navigateur n’a encore été effectué.

Pour vérifier le moteur intégré, sans Ollama :

```sh
dotnet run --project checks/Nomi.Inference.Checks -- --embedded-live
```

Ce contrôle télécharge et vérifie le modèle si nécessaire, exerce les six actions
dans les deux langues, l’extraction d’un PDF et l’annulation suivie d’une nouvelle
demande. `NOMI_TEST_MODEL_DIR` permet d’utiliser un dossier de modèle dédié pour
ces contrôles uniquement.

Les contrôles du projet Windows, à exécuter sur Windows :

```powershell
dotnet format .\Nomi.WinUI\Nomi.WinUI.csproj --verify-no-changes
dotnet build .\Nomi.WinUI\Nomi.WinUI.csproj -c Release -p:Platform=x64 -warnaserror
```

## Références

- [Démarrage WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/create-your-first-winui3-app)
- [Windows App SDK 1.8.260209005](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/1.8.260209005)
- [Qwen3 4B Instruct dans Ollama](https://ollama.com/library/qwen3:4b-instruct)
- [Fiche et licence du modèle](https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507)
- [LLamaSharp](https://github.com/SciSharp/LLamaSharp)
