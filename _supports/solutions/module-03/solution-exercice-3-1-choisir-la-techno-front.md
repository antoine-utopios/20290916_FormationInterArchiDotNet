# Solution — Exercice 3.1 : choisir la technologie front pour cinq besoins Textinord

> Document formateur — Ne pas distribuer avant la restitution.

## Approche pédagogique

L'exercice n'a pas une seule bonne réponse par besoin : il a des réponses
**défendables par des critères** et des réponses qui ne le sont pas. Le
corrigé donne la décision attendue, la décision alternative acceptable, et les
réponses à refuser parce qu'elles contredisent un critère de la grille. Lors de
la restitution, faites attaquer chaque choix par un critère, jamais par une
préférence (« j'aime bien Angular » n'est pas un argument, « on n'a pas de
développeur TypeScript » en est un).

## Partie 1 — La grille remplie

| Critère | B1 Extranet | B2 Scanner | B3 Back-office | B4 Tableau de bord | B5 Borne |
|---|---|---|---|---|---|
| Public | externe, 1 200 clients, non formés | interne, ~40 préparateurs, formés sur le geste | interne, 25 experts, saisie intensive | interne, 8 dirigeants, lecture seule | public anonyme, showroom |
| Réseau | Internet, variable mais présent | Wi-Fi intermittent, hors ligne obligatoire entre deux allées | LAN fiable | LAN ou Internet, fiable | filaire fiable |
| Périphériques | navigateur, parfois tablette | terminal Android Zebra, lecteur intégré, gants | poste Windows 11, clavier, double écran | navigateur, tablette | écran tactile, un seul poste |
| Compétences | 6 dev C#, pas de front dédié | idem, pas de mobile natif (à acquérir) | idem, WinForms historique | idem | idem |
| Déploiement | serveur uniquement, releases fréquentes | MDM (Intune) sur 40 terminaux, releases mensuelles | 25 postes, tolérance à ClickOnce / MSIX ; coexistence 18 mois avec WinForms | serveur uniquement | un poste, mise à jour rare |
| Interactivité | catalogue, filtre, panier, formulaire | scan à la volée, validation ligne par ligne | grilles, raccourcis, saisie intensive | lecture seule, rafraîchissement 5 min | navigation tactile, lecture |
| Durée de vie et budget | cœur de Trame 2, 10 ans, évolutions continues | 5 ans, cycle matériel | 10 ans, migration progressive | 3 ans, évolutions fréquentes des indicateurs | 6 mois sans intervention, évolutions rares |

Hypothèses acceptées si elles sont écrites : terminaux Zebra gérés par Intune,
clients de l'extranet derrière des proxys d'entreprise (WebSocket à vérifier),
dirigeants équipés d'iPad.

## Partie 2 — Les décisions attendues

### B1 — Extranet clients

- **Retenu** : Blazor Web App, interactivité **InteractiveServer** (globale ou par page), pré-rendu SSR pour les fiches article.
- **Écarté de justesse** : SPA Angular + API. Pas de développeur front dédié, deux pipelines à maintenir, et l'API Trame 2 n'existe pas encore : Blazor Server accède directement aux services puis à l'API sans réécrire le client.
- **Justification** : compétences (100 % C#) ; interactivité (filtre, panier : InteractiveServer suffit, l'état vit par circuit) ; déploiement (serveur seul, releases continues sans installation).
- **Pattern** : composants Razor (vue + ViewModel), logique dans des services injectés (`ICatalogueService`, `PanierService`), testée avec bUnit.
- **Réponse à refuser** : Blazor WebAssembly seul « parce que c'est moderne » : premier chargement de 1 à 3 Mo pour des clients qui commandent une fois par semaine, et l'API n'est pas prête.
- **Conséquence à noter** : un circuit SignalR par utilisateur connecté ; vérifier que les proxys des grands comptes laissent passer WebSocket (sinon long polling, plus coûteux).

### B2 — Scanner entrepôt

- **Retenu** : **.NET MAUI** ciblant `net10.0-android`, MVVM avec CommunityToolkit.Mvvm, SQLite local, file de synchronisation vers l'API au retour du réseau.
- **Écarté de justesse** : Blazor WebAssembly en PWA installée (fonctionne hors ligne, accès caméra) — mais l'accès au lecteur matériel Zebra, au mode kiosque et à la gestion de flotte est natif en MAUI ; et une PWA sur un terminal durci reste dépendante du navigateur embarqué.
- **Justification** : réseau (hors ligne obligatoire entre deux allées, 15 000 mouvements par jour à ne pas perdre) ; périphériques (lecteur intégré, SDK Zebra, gants : gros boutons natifs) ; déploiement (Intune, APK signé, cycle matériel de 5 ans).
- **Pattern** : MVVM ; les ViewModels et la règle « un ordre de préparation par entrepôt » sont testés sans terminal ; `Shell` pour la navigation.
- **Réponse à refuser** : Blazor Server : une coupure Wi-Fi coupe le circuit, l'écran se fige, Marc Vandewalle perd des ordres.
- **Point d'animation** : c'est le besoin où le critère réseau tranche avant tout le reste.

### B3 — Back-office ADV et achats

- **Retenu** : en deux temps. **Maintenant** : migrer le WinForms de Trame vers .NET 10 (`net10.0-windows`), extraire les ViewModels (exercice 3.2) sans changer les écrans. **Nouveaux écrans** : Blazor Web App InteractiveServer dans l'intranet, mêmes services que l'extranet ; les deux coexistent 18 mois (Strangler Fig, module 6).
- **Écarté de justesse** : WPF. Il répond bien aux grilles denses et aux raccourcis, MVVM y est natif ; mais il ajoute une compétence XAML durable alors que la cible de Textinord est le web, et il ne partage rien avec l'extranet côté vue. Reste acceptable si le binôme argumente sur la densité de saisie et la durée de vie de 10 ans.
- **Justification** : durée de vie (la logique doit être écrite une fois pour WinForms, Blazor et MAUI : donc ViewModels et services partagés) ; déploiement (25 postes, MSIX ou ClickOnce acceptable pour l'existant, rien à installer pour le web) ; interactivité (saisie intensive : InteractiveServer sur LAN est aussi réactif qu'un client lourd ; raccourcis clavier gérés en Blazor avec `@onkeydown`).
- **Pattern** : MVP ou MVVM par binding sur l'existant WinForms ; composants + services pour les écrans Blazor ; un noyau de règles unique (`Textinord.Trame.Domain`).
- **Réponse à refuser** : « on réécrit tout en Blazor d'un coup » (180 000 lignes, aucun test : c'est le risque que Nadia Benali refuse) et « on garde WinForms tel quel » (.NET Framework ne recevra plus rien).

### B4 — Tableau de bord direction

- **Retenu** : Blazor Web App, page SSR avec **streaming rendering** pour l'affichage initial, et un composant **InteractiveServer par tuile** qui se rafraîchit toutes les cinq minutes (`PeriodicTimer`) ; pattern **Dashboard** : chaque tuile a sa source (`ICommandesDuJour`, `IStockCritique`, `IRetards`) et son test bUnit.
- **Écarté de justesse** : Power BI (ou Grafana) branché sur la base : parfaitement défendable si le binôme note qu'il ne s'agit alors plus d'un développement mais d'un outil, avec ses licences et sa gouvernance des données. C'est même souvent le bon choix pour un comité de direction ; l'exercice porte sur les technologies de développement, on accepte donc les deux réponses argumentées.
- **Justification** : interactivité (lecture seule, aucune saisie : le SSR suffit, l'interactivité ne sert qu'au rafraîchissement) ; périphériques (navigateur et tablette : responsive) ; durée de vie (les indicateurs changent souvent : une tuile = un composant, on en ajoute sans toucher aux autres).
- **Réponse à refuser** : WPF ou WinForms (la DSI regarde depuis un iPad), SPA Angular (équipe).

### B5 — Borne showroom

- **Retenu** : Blazor Web App (SSR + InteractiveServer) servie par le même serveur que l'extranet, affichée dans un navigateur en **mode kiosque** (Windows « accès assigné » ou Android kiosque) ; rien n'est installé sur la borne, les mises à jour sont côté serveur.
- **Écarté de justesse** : .NET MAUI pour Windows : natif, tactile, mais il faut déployer et mettre à jour sur la borne, pour un écran qui doit tourner six mois sans intervention ; et le catalogue avec photos existe déjà côté extranet.
- **Justification** : réseau (filaire fiable : InteractiveServer ne pose aucun problème) ; déploiement (zéro installation, une borne ne se met pas à jour facilement) ; durée de vie et budget (évolutions rares, on réutilise les composants de l'extranet : `ArticleCarte`, filtre par famille).
- **Pattern** : composants réutilisés ; aucun panier, donc aucun état scoped nécessaire.
- **Réponse à refuser** : WinForms « parce que la borne est sous Windows » (pas de réutilisation, déploiement manuel).

## Partie 3 — Restitution : ce qu'il faut faire ressortir

- Le critère **réseau** élimine avant tout le reste (B2).
- Le critère **compétences** n'est pas un aveu de faiblesse : six développeurs C# sans front dédié, c'est un fait qui vaut une décision (B1, B4).
- Le critère **durée de vie** impose de séparer logique et vue : c'est la raison d'être de MVVM et des services partagés (B3).
- **Blazor** est quatre réponses : SSR, Server, WebAssembly, Auto. Un binôme qui écrit « Blazor » sans préciser n'a pas fini.

## Variantes acceptables

1. B1 en **InteractiveAuto** si le binôme anticipe l'ouverture grand public : accepté à condition de noter le coût (deux projets, API obligatoire dès le départ).
2. B3 en **WPF** pour les nouveaux écrans : accepté avec l'argument densité + raccourcis + MVVM natif, et la conséquence « deux vues, un ViewModel ».
3. B4 en **Power BI** : accepté avec la remarque « ce n'est plus un développement ».
4. B5 en **MAUI Windows** : accepté seulement si le binôme prévoit le déploiement MDM et justifie un besoin natif (caméra, périphérique) — sinon, c'est de la complexité gratuite.

## Erreurs classiques à repérer

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| « Blazor » sans render mode | slide des render modes survolée | renvoyer à la slide « Choisir un render mode pour chaque page » et faire préciser |
| Blazor Server pour le scanner | le critère réseau n'a pas été rempli | faire rejouer la scène : la préparatrice est entre deux allées, le circuit tombe |
| Angular « pour être moderne » | technicité avant ingénierie (module 1) | demander qui code le TypeScript lundi matin |
| Réécriture complète du back-office | pas de notion de coexistence | rappeler les 18 mois du fil rouge et le Strangler Fig du module 6 |
| Aucune hypothèse écrite | grille remplie de « ? » | une hypothèse écrite est une décision réversible ; un « ? » n'est rien |

## Bonus — L'ADR de l'extranet (trame attendue)

- **Contexte** : site Web Forms non migrable, 1 200 clients pro, équipe C#, API en construction, Entra External ID prévu.
- **Options** : MVC / Razor Pages (pas assez interactif pour le panier sans JavaScript maison), Blazor Server (retenu), Blazor WebAssembly / Auto (premier chargement, API obligatoire), Angular (compétences).
- **Décision** : Blazor Web App .NET 10, InteractiveServer, pré-rendu, services derrière interfaces, bUnit.
- **Conséquences** : dimensionner les circuits (mémoire par utilisateur connecté), autoriser WebSocket, `ReconnectModal` et message de reconnexion, prévoir InteractiveAuto si un site grand public ouvre un jour (les composants restent, l'hébergement change).
