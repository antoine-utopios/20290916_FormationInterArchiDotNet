# Exercice 3.1 — Choisir la technologie front pour cinq besoins Textinord

> Module : 03 — Applications web et clients : ASP.NET Core, Blazor, SPA, MAUI (Jour 2, matin)
> Durée estimée : 25 min
> Difficulté : 2 / 5
> Type : Exercice de conception, en binôme, sans code

## Objectifs pédagogiques

À la fin de cet exercice, vous serez capable de :

- Décrire un besoin d'interface utilisateur avec une grille de critères objectifs
- Choisir entre ASP.NET Core MVC / Razor Pages, Blazor (SSR, Server, WebAssembly, Auto), SPA Angular, WinForms, WPF et .NET MAUI en justifiant par les critères, pas par la mode
- Associer à chaque choix le pattern de présentation attendu (MVC, MVP, MVVM, Dashboard)

## Prérequis

- Avoir suivi les sections 1 et 2 du module 3 (panorama 2026, patterns de présentation)
- Connaître le contexte Textinord décrit dans `FIL-ROUGE.md`
- Outils : une feuille ou un fichier Markdown, aucun environnement de développement

## Contexte

Julien Delcourt, architecte de Textinord, doit présenter à Nadia Benali (DSI)
la carte des futurs écrans de Trame 2. Il refuse de répondre « Blazor partout »
ou « on garde WinForms » sans critères : chaque besoin sera évalué avec la
même grille, et la décision sera consignée dans un ADR. Vous êtes son binôme
pour cette matinée.

Cinq besoins sont sur la table :

| Réf. | Besoin | Ce qu'on sait |
|---|---|---|
| B1 | **Extranet clients** | 1 200 clients professionnels, catalogue de 40 000 références, panier, suivi de commande ; navigateurs récents ; authentification Entra External ID ; l'équipe de Sofia Marques (6 développeurs C#, aucun développeur front dédié) |
| B2 | **Scanner entrepôt** | préparateurs de Roubaix et Lesquin, terminaux Android durcis Zebra avec lecteur de codes-barres intégré ; Wi-Fi intermittent entre les allées ; 15 000 mouvements de stock par jour ; Marc Vandewalle veut « zéro perte d'ordre de préparation » |
| B3 | **Back-office ADV et achats** | 25 utilisateurs internes sur postes Windows 11 ; écrans de saisie denses (commandes, tarifs, litiges) ; l'application WinForms actuelle reste en production 18 mois ; raccourcis clavier et grilles rapides indispensables |
| B4 | **Tableau de bord direction** | Nadia Benali et le comité de direction ; indicateurs de commandes, stock, retards, consultés depuis un navigateur ou une tablette ; rafraîchissement toutes les cinq minutes ; aucune saisie |
| B5 | **Borne showroom** | une borne tactile dans le showroom de Roubaix ; catalogue avec photos, sans compte client ; réseau filaire fiable ; doit fonctionner sans intervention pendant six mois ; évolutions rares |

## Énoncé

### Partie 1 — Remplir la grille de critères (10 min)

Pour chaque besoin, renseignez la grille ci-dessous. Une case = une valeur
courte et factuelle, pas un jugement.

| Critère | B1 Extranet | B2 Scanner | B3 Back-office | B4 Tableau de bord | B5 Borne |
|---|---|---|---|---|---|
| Public (interne / externe, nombre, formation) | | | | | |
| Réseau (fiable / intermittent / hors ligne obligatoire) | | | | | |
| Périphériques et matériel (caméra, scanner, écran, OS) | | | | | |
| Compétences disponibles (C#, XAML, TypeScript, mobile) | | | | | |
| Déploiement et mise à jour (qui installe, à quelle fréquence) | | | | | |
| Interactivité (lecture seule, formulaire, saisie intensive) | | | | | |
| Durée de vie et budget d'évolution | | | | | |

Résultat attendu : une grille complète ; si une information manque dans le
contexte, écrivez l'hypothèse que vous prenez (par exemple « les terminaux
Zebra sont gérés par Intune »).

### Partie 2 — Décider et justifier (10 min)

Pour chaque besoin, écrivez :

1. la **technologie retenue** (une seule ; pour Blazor, précisez le render mode) ;
2. la **technologie écartée de justesse** et pourquoi ;
3. la **justification en trois lignes**, chaque ligne citant un critère de la grille ;
4. le **pattern de présentation** associé (MVC, MVP, MVVM, Dashboard, ou composants) et où vivra la logique testable.

Format attendu :

```
B2 — Scanner entrepôt
Retenu : ...
Écarté : ... parce que ...
Justification :
  - réseau : ...
  - périphériques : ...
  - déploiement : ...
Pattern : ... ; logique testable dans ...
```

Résultat attendu : cinq décisions défendables devant un DSI, dont au moins une
où la technologie « évidente » n'est pas celle retenue.

### Partie 3 — Restitution (5 min)

Deux binômes présentent une décision contestée (le formateur choisit B3 ou B4).
Les autres binômes attaquent avec un critère de la grille, pas avec une
préférence.

## Indices (à consulter si bloqué)

<details>
<summary>Indice 1 — Le critère qui tranche en premier</summary>

Regardez d'abord le réseau et le matériel : un besoin qui exige le hors ligne ou
un périphérique natif (lecteur de codes-barres, caméra) élimine les technologies
purement serveur avant même de parler de compétences.

</details>

<details>
<summary>Indice 2 — Blazor n'est pas une réponse, c'est quatre réponses</summary>

SSR statique, InteractiveServer, InteractiveWebAssembly et InteractiveAuto ne
répondent pas aux mêmes contraintes : un réseau intermittent exclut
InteractiveServer, un très grand public pousse vers Auto, une page de contenu
se contente de SSR. Précisez toujours le mode.

</details>

<details>
<summary>Indice 3 — Le back-office n'est pas un choix technique seulement</summary>

Le WinForms actuel reste 18 mois. La question n'est pas « WinForms ou Blazor »
mais « comment les écrans refaits et les écrans conservés cohabitent, et où
vit la logique pour n'être écrite qu'une fois ». L'exercice 3.2 vous donne la
réponse pour la logique ; à vous de choisir la vue.

</details>

## Pour aller plus loin (bonus)

Rédigez l'ADR de l'extranet clients au format du module 1 (contexte, options
envisagées, décision, conséquences) en une page. Conséquences à ne pas
oublier : hébergement (un circuit SignalR par utilisateur connecté, donc
dimensionnement), réseau (WebSocket autorisé par les proxys des clients ?),
tests (bUnit), et ce qui se passerait si Textinord ouvrait un jour un site
grand public.
