# Solution — Exercice 6.2 : Tests unitaires avec NSubstitute

> Document formateur — Ne pas distribuer avant la fin de l'exercice.

Le code complet, compilable, se trouve dans `solutions/module-06/exercice-6-2/` :

```
exercice-6-2/
  Textinord.Trame.Validation.sln
  Directory.Build.props                          net10.0, nullable, warnings = erreurs
  src/Textinord.Trame.Validation/                Modele.cs, Ports.cs, RegleRemise.cs, ValidationCommandeService.cs
  tests/Textinord.Trame.Validation.Tests/        ValidationCommandeServiceTests.cs (13 cas de test)
```

`dotnet build` puis `dotnet test` : 13 tests réussis (2 fournis, 7 attendus dont une théorie à 3 cas, 1 bonus `Received.InOrder`).

## Approche pédagogique

L'exercice vise trois réflexes : substituer les **ports** (interfaces) plutôt que les implémentations ; vérifier les **interactions** (ce qui a été appelé, combien de fois, avec quoi) et pas seulement la valeur de retour ; rendre le **temps** injectable. Le quatrième réflexe, le plus important, est la consigne « commentez la règle, constatez le rouge » : un test qui reste vert quand la règle disparaît ne protège rien.

Circulez pendant la partie 2 : c'est là que les apprenants découvrent `Received` et `DidNotReceiveWithAnyArgs`, et qu'ils oublient d'attendre (`await`) la vérification d'une méthode asynchrone — le test passe alors sans rien vérifier.

## Solution détaillée

Le constructeur de la classe de tests est fourni : il prépare trois clients, les trois substituts, l'horloge fixée au 7 septembre 2026 à 9 h 30, et le service. Les aides `CommandeDe` et `StockPartout` évitent de répéter la construction des commandes.

### Partie 1 — Les refus

```csharp
[Fact]
public async Task Refuse_un_client_inconnu_sans_interroger_le_stock()
{
    _clients.TrouverAsync("C-9999", Arg.Any<CancellationToken>()).Returns((Client?)null);
    var commande = CommandeDe("C-9999", ("VT-1001", 10));

    var resultat = await _service.ValiderAsync(commande);

    Assert.Equal(StatutCommande.Refusee, resultat.Statut);
    Assert.Contains("Client inconnu", resultat.Motifs.Single());
    await _stock.DidNotReceiveWithAnyArgs().EntrepotsDisponiblesAsync(default!, default, default);
}
```

Commentaire : le transtypage `(Client?)null` lève l'ambiguïté entre `Returns(T)` et `Returns(Func<CallInfo, T>)`. La vérification `DidNotReceiveWithAnyArgs` est ce qui distingue ce test d'un simple contrôle de statut : il prouve que le service s'arrête avant d'interroger le stock. Pour le voir échouer : inverser dans le service l'ordre du contrôle client et de la boucle sur le stock.

```csharp
[Fact]
public async Task Refuse_une_ligne_a_quantite_nulle_ou_negative_sans_interroger_le_stock()
{
    var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 0), ("LH-3300", -3));

    var resultat = await _service.ValiderAsync(commande);

    Assert.Equal(StatutCommande.Refusee, resultat.Statut);
    Assert.Equal(2, resultat.Motifs.Count);
    Assert.All(resultat.Motifs, m => Assert.Contains("Quantité invalide", m));
    await _stock.DidNotReceiveWithAnyArgs().EntrepotsDisponiblesAsync(default!, default, default);
}

[Fact]
public async Task Refuse_une_commande_sans_ligne()
{
    var commande = CommandeDe(HotelLeBeffroi.Code);

    var resultat = await _service.ValiderAsync(commande);

    Assert.Equal(StatutCommande.Refusee, resultat.Statut);
    Assert.Contains("au moins une ligne", resultat.Motifs.Single());
}
```

Commentaire : deux motifs pour deux lignes invalides — le service collecte toutes les erreurs de quantité avant de refuser, plutôt que de s'arrêter à la première. `Assert.All` vérifie chaque motif ; `Assert.Equal(2, …)` vérifie qu'il n'y en a pas un troisième. `CommandeDe` sans tuple produit une commande sans ligne : le `params` accepte zéro élément.

### Partie 2 — Le stock

```csharp
[Fact]
public async Task Passe_en_attente_de_stock_et_alerte_l_adv_pour_chaque_rupture()
{
    _stock.EntrepotsDisponiblesAsync("VT-1001", 10, Arg.Any<CancellationToken>()).Returns(new[] { "RBX" });
    _stock.EntrepotsDisponiblesAsync("VT-1050", 5, Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
    _stock.EntrepotsDisponiblesAsync("EPI-2040", 200, Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
    var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10), ("VT-1050", 5), ("EPI-2040", 200));

    var resultat = await _service.ValiderAsync(commande);

    Assert.Equal(StatutCommande.EnAttenteStock, resultat.Statut);
    Assert.Equal(["Rupture de stock : VT-1050.", "Rupture de stock : EPI-2040."], resultat.Motifs);
    Assert.Equal(StatutCommande.EnAttenteStock, commande.Statut);
    Assert.Null(commande.ValideeLe);
    await _notificateur.Received(1).SignalerRuptureAsync("CMD-2026-000123", "VT-1050", Arg.Any<CancellationToken>());
    await _notificateur.Received(1).SignalerRuptureAsync("CMD-2026-000123", "EPI-2040", Arg.Any<CancellationToken>());
    await _notificateur.ReceivedWithAnyArgs(2).SignalerRuptureAsync(default!, default!, default);
}
```

Commentaire : trois vérifications d'interaction complémentaires. Les deux `Received(1)` avec arguments précis prouvent que **chaque** rupture a déclenché **sa** notification avec le bon numéro de commande ; le `ReceivedWithAnyArgs(2)` prouve qu'il n'y en a pas eu une troisième (par exemple pour la ligne disponible). L'ordre des motifs est celui des lignes : `Assert.Equal` sur deux collections compare élément par élément. Sans substitut configuré, `EntrepotsDisponiblesAsync` renvoie une liste vide (NSubstitute renvoie une valeur par défaut « raisonnable » pour `IReadOnlyList<T>`) : c'est pratique, mais dangereux — un apprenant qui oublie de configurer `VT-1001` obtient trois ruptures et un test qui échoue sur `ReceivedWithAnyArgs(2)`.

```csharp
[Fact]
public async Task Affecte_le_premier_entrepot_disponible_a_chaque_ligne()
{
    _stock.EntrepotsDisponiblesAsync("VT-1001", 10, Arg.Any<CancellationToken>()).Returns(new[] { "LSQ", "RBX" });
    _stock.EntrepotsDisponiblesAsync("LH-3300", 40, Arg.Any<CancellationToken>()).Returns(new[] { "RBX" });
    var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10), ("LH-3300", 40));

    await _service.ValiderAsync(commande);

    Assert.Equal(["LSQ", "RBX"], commande.Lignes.Select(l => l.Entrepot));
    await _stock.Received(1).EntrepotsDisponiblesAsync("VT-1001", 10, Arg.Any<CancellationToken>());
    await _stock.Received(1).EntrepotsDisponiblesAsync("LH-3300", 40, Arg.Any<CancellationToken>());
}
```

Commentaire : le port renvoie les entrepôts « par ordre de préférence » et le service prend le premier — c'est le contrat testé ici. Les `Received(1)` avec la quantité exacte vérifient que le service interroge le stock pour **la quantité de la ligne**, pas pour une valeur fixe : un bug classique dans Trame (`CommandeManager` demandait toujours 1).

### Partie 3 — Remise, horloge, exception

```csharp
[Theory]
[InlineData("C-0001", 100, 0.10)]   // 10 % client, pas de volume
[InlineData("C-0001", 600, 0.15)]   // 10 % + 5 % volume
[InlineData("C-0117", 600, 0.30)]   // 25 % + 5 % = 30 %, plafond atteint
public async Task Applique_la_remise_client_plus_volume_plafonnee_a_30_pour_cent(string codeClient, int quantite, double attendu)
{
    StockPartout();
    var commande = CommandeDe(codeClient, ("VT-1001", quantite));

    await _service.ValiderAsync(commande);

    Assert.Equal((decimal)attendu, commande.Lignes.Single().TauxRemise);
}
```

Commentaire : `InlineData` ne sait pas exprimer un `decimal` littéral (les attributs n'acceptent que des constantes primitives) ; on passe un `double` et on convertit. Le troisième cas est celui qui compte : 0,25 + 0,05 = 0,30, exactement le plafond ; ajoutez en séance un quatrième cas hors plafond si un apprenant doute que le plafond soit testé (un client à 25 % avec 1 000 pièces donne toujours 0,30).

```csharp
[Fact]
public async Task Date_la_validation_avec_l_horloge_injectee()
{
    StockPartout();
    var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10));

    await _service.ValiderAsync(commande);

    Assert.Equal(Instant, commande.ValideeLe);
    _horloge.Received(1).GetUtcNow();
}
```

Commentaire : `TimeProvider` est une classe abstraite ; NSubstitute la substitue comme une interface, et `GetUtcNow()` est virtuelle. Sans injection du temps, ce test devrait comparer à `DateTimeOffset.UtcNow` avec une tolérance — fragile et imprécis. Le `Received(1)` n'est pas indispensable, mais il attrape un service qui appellerait l'horloge à chaque ligne.

```csharp
[Fact]
public async Task Propage_l_exception_du_referentiel_et_laisse_la_commande_en_brouillon()
{
    _clients.TrouverAsync("C-0001", Arg.Any<CancellationToken>())
        .ThrowsAsync(new TimeoutException("SQL Server injoignable"));
    var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10));

    var exception = await Assert.ThrowsAsync<TimeoutException>(() => _service.ValiderAsync(commande));

    Assert.Contains("injoignable", exception.Message);
    Assert.Equal(StatutCommande.Brouillon, commande.Statut);
    await _notificateur.DidNotReceiveWithAnyArgs().SignalerRuptureAsync(default!, default!, default);
}
```

Commentaire : `ThrowsAsync` (namespace `NSubstitute.ExceptionExtensions`) renvoie une tâche en échec ; `Throws` lèverait au moment de l'appel. Pour un service qui fait `await` immédiatement, les deux passent ; `ThrowsAsync` est le comportement fidèle d'un appel réseau. L'assertion sur `Brouillon` vérifie que le service n'a pas modifié la commande avant de propager : c'est une règle de robustesse (pas d'état à moitié changé), et elle échouerait si le service positionnait `Refusee` dans un `catch`.

### Bonus — l'ordre des appels

```csharp
[Fact]
public async Task Consulte_le_client_avant_le_stock()
{
    StockPartout();
    var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10));

    await _service.ValiderAsync(commande);

    Received.InOrder(async () =>
    {
        await _clients.TrouverAsync(HotelLeBeffroi.Code, Arg.Any<CancellationToken>());
        await _stock.EntrepotsDisponiblesAsync("VT-1001", 10, Arg.Any<CancellationToken>());
    });
}
```

Commentaire : `Received.InOrder` vérifie l'ordre relatif des appels entre plusieurs substituts. À réserver aux cas où l'ordre est une règle métier (ici : ne pas interroger le stock d'un client radié) ; utilisé partout, il couple les tests à l'implémentation.

## Le rouge d'abord : quelle ligne commenter pour voir chaque test échouer

| Test | Ligne du service à commenter ou modifier | Effet attendu |
|---|---|---|
| Client inconnu | le `if (client is null)` | `NullReferenceException` sur `client.Actif` — le test échoue, mais pas proprement : bon moment pour parler de `ArgumentNullException.ThrowIfNull` et des messages d'erreur |
| Quantité nulle | le bloc `quantitesInvalides` | le service interroge le stock pour une quantité 0 : `DidNotReceiveWithAnyArgs` échoue |
| Sans ligne | le `if (commande.Lignes.Count == 0)` | la commande est validée avec zéro ligne |
| Rupture et alerte | `await notificateur.SignalerRuptureAsync(...)` | `Received(1)` échoue avec le message NSubstitute qui liste les appels reçus (aucun) |
| Affectation d'entrepôt | remplacer `entrepots[0]` par `entrepots[^1]` | `["LSQ","RBX"]` devient `["RBX","RBX"]` |
| Remise | remplacer `Math.Min(taux, 0.30m)` par `taux` | le troisième cas de la théorie échoue seul : 0,30 attendu, 0,30 obtenu… non : 0,25 + 0,05 = 0,30 ; il faut un client à 25 % et 1 000 pièces pour dépasser — d'où l'intérêt d'un quatrième cas |
| Horloge | remplacer `horloge.GetUtcNow()` par `DateTimeOffset.UtcNow` | `Assert.Equal(Instant, …)` échoue |
| Exception | entourer l'appel au référentiel d'un `try / catch` qui refuse | `ThrowsAsync` échoue : aucune exception |

La ligne « Remise » est volontairement piégeuse : faites-la jouer en séance, elle montre qu'un jeu de données peut ne pas exercer le plafond même quand on croit le tester.

## Variantes acceptables

1. `Throws` au lieu de `ThrowsAsync` : accepté, en faisant remarquer la différence de moment.
2. `Received()` sans nombre (équivaut à « au moins une fois ») : accepté pour la notification, mais le `ReceivedWithAnyArgs(2)` reste attendu pour prouver l'absence de troisième appel.
3. `Arg.Is<string>(r => r == "VT-1050")` au lieu du littéral : équivalent ; `Arg.Is` devient utile pour des objets sans égalité structurelle.
4. Une théorie avec `[MemberData]` et des `decimal` : plus propre que le `double` converti ; accepter et montrer.
5. Un `[Fact]` par cas de remise plutôt qu'une théorie : accepté, mais faire réécrire en théorie si le temps le permet, c'est l'objet de la partie 3.
6. Les substituts créés dans chaque test plutôt que dans le constructeur : accepté ; le constructeur est exécuté avant chaque test par xUnit (nouvelle instance de la classe), donc les deux formes isolent les tests.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| `_stock.Received(1).EntrepotsDisponiblesAsync(...)` sans `await` | la méthode renvoie une `Task` ; sans `await`, l'assertion NSubstitute s'exécute quand même, mais un avertissement CS4014 apparaît, transformé en erreur par `TreatWarningsAsErrors` | ajouter `await` ; expliquer que pour `Received` sur une méthode asynchrone, l'`await` sert surtout à faire taire l'avertissement, la vérification a lieu à l'appel |
| `Returns(null)` ambigu : erreur de compilation | surcharges de `Returns` | `Returns((Client?)null)` |
| Test « rupture » qui passe sans configurer `VT-1001` | valeur par défaut (liste vide) renvoyée par le substitut | montrer la sortie de `ReceivedWithAnyArgs(2)` : trois appels reçus ; configurer chaque référence |
| `Assert.Equal(0.30, ligne.TauxRemise)` : échec « double vs decimal » | types différents | convertir en `decimal`, ou typer le paramètre de théorie en `double` et convertir |
| Théorie avec un seul `InlineData` | l'apprenant n'a pas vu l'intérêt | trois cas minimum : sans volume, avec volume, au plafond |
| Test « exception » qui vérifie `Refusee` | intuition « une erreur = un refus » | relire la règle : une panne technique n'est pas une décision métier ; la commande reste `Brouillon`, l'appelant réessaiera |

## Points à insister en débriefing

- Un substitut remplace un **port**, jamais une classe concrète du domaine : `RegleRemise` n'est pas substituée, elle est exercée. C'est ce qui rend ces tests significatifs.
- Les vérifications d'interaction (`Received`, `DidNotReceive`) protègent des règles que la valeur de retour ne montre pas : « le stock n'est pas interrogé pour un client radié » n'est visible nulle part dans `ResultatValidation`.
- `TimeProvider` est dans la BCL depuis .NET 8 : plus aucune raison d'écrire `DateTime.Now` dans un service, ni d'inventer une interface `IHorloge`.
- Lien avec le TP 6 : `OutboxRelayTests` substitue `IPublishEndpoint` et `TimeProvider` exactement de cette façon ; `OrdrePreparationConsumerTests` substitue un `ConsumeContext<T>` de MassTransit — les mêmes gestes, sur les ports d'une bibliothèque tierce.

## Bonus

Réponse à la question de couverture : après les treize tests, la seule ligne non couverte du service est le `ArgumentNullException.ThrowIfNull(commande)` (aucun test ne passe `null`). Un test `Refuse_une_commande_nulle` avec `Assert.ThrowsAsync<ArgumentNullException>(() => _service.ValiderAsync(null!))` la couvre en trois lignes. Avec Bogus, un `Faker<Client>` typique :

```csharp
var clients = new Faker<Client>()
    .CustomInstantiator(f => new Client(
        $"C-{f.Random.Number(1, 9999):D4}",
        f.Company.CompanyName(),
        Math.Round(f.Random.Decimal(0m, 0.25m), 2),
        Actif: true))
    .UseSeed(42)
    .Generate(100);
```

et une théorie qui, pour chacun, valide une commande de 1 000 pièces et vérifie `TauxRemise <= 0.30m`.
