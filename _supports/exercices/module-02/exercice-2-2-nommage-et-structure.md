# Exercice 2.2 — Nommage et structure d'une solution

**Module 2 — Intégration par messages et plateforme .NET · 20 min · individuel, correction collective**
Slide de renvoi : 34. Supports utiles : slides 30 à 33 (outillage, `Directory.Build.props`, structure, nommage).

## Objectifs

- Appliquer les conventions de nommage .NET (assemblies, namespaces, types, membres, méthodes asynchrones).
- Corriger la structure d'une solution pour qu'elle reflète les couches et les références autorisées.
- Écrire une règle `.editorconfig` qui transforme une convention en erreur de compilation.

## Prérequis

- Slides 30 à 33 du module.
- Aucun outil nécessaire : l'exercice se fait sur papier ou dans un éditeur de texte. Si vous avez le SDK .NET 10,
  la partie 3 peut être vérifiée en compilant.

## Contexte Textinord

Un prestataire a livré à Sofia Marques un « socle Trame 2 » avant le démarrage du programme. Sofia veut
l'utiliser comme base mais refuse de le laisser entrer dans le dépôt en l'état : les noms ne respectent aucune
convention, la structure mélange les couches, et rien ne garantit que le prochain développeur fera mieux.
Vous êtes chargé de la revue.

## Énoncé

### Partie 1 — La structure (7 min)

Voici l'arborescence livrée :

```text
TrameV2.sln
Core/                                   projet Trame.Core.csproj      AssemblyName = core       RootNamespace = Textinord
TextinordTrameBLL/                      projet TextinordTrameBLL.csproj                         (règles métier + accès EF Core + envoi des messages)
DAL/                                    projet Textinord.DAL.csproj    référence TextinordTrameBLL
WebApi/                                 projet TrameApi.csproj         namespace trameapi.controllers
Tests/                                  projet UnitTests.csproj        référence TrameApi uniquement
Utils/                                  projet Textinord.Utils.csproj  (extensions LINQ, helpers de dates, client HTTP du WMS)
```

Chaque `.csproj` porte ses propres versions de packages (`xunit` 2.4.2 dans un projet, 2.9.3 dans un autre ;
`Microsoft.Extensions.Logging` en 8.0.0 et 10.0.11). Il n'y a ni `Directory.Build.props`, ni `.editorconfig`,
ni `global.json`.

1. Proposez la nouvelle arborescence (`src/`, `tests/`, un projet par couche, noms d'assemblies et de namespaces
   racines) en respectant la convention `Textinord.Trame.<Couche>`.
2. Pour chaque projet livré, dites où va son contenu dans la nouvelle structure (un projet livré peut être
   éclaté sur plusieurs projets cibles).
3. Indiquez les références autorisées entre les nouveaux projets, et les deux références livrées qui sont
   dans le mauvais sens.
4. Quels fichiers ajoutez-vous à la racine, et pour quoi faire ?

### Partie 2 — Les noms (8 min)

Extrait du projet `TextinordTrameBLL`. Relevez **au moins douze** violations de convention, et proposez pour
chacune le nom corrigé. Ne cherchez pas les défauts de conception (ils existent, ce n'est pas le sujet).

```csharp
using System;
using System.Threading.Tasks;

namespace TextinordTrameBLL
{
    public interface commandeService
    {
        Task<Commande> Validate(string num);
        void send_to_entrepot(Commande c);
    }

    public class CommandeMgr : commandeService
    {
        private readonly ICommandeRepo m_repo;
        private readonly IEntrepotGW _EntrepotGw;
        private int iCount;
        public const int MAX_LIGNES = 200;
        public string strDernierNumero;

        public CommandeMgr(ICommandeRepo repo, IEntrepotGW gw)
        {
            m_repo = repo;
            _EntrepotGw = gw;
        }

        public async Task<Commande> Validate(string strNum)
        {
            var cmd = await m_repo.GetByNum(strNum);
            if (cmd.Lignes.Count > MAX_LIGNES) throw new Exception("trop de lignes");
            iCount++;
            strDernierNumero = strNum;
            return cmd;
        }

        public void send_to_entrepot(Commande c)
        {
            _EntrepotGw.SendAsync(c).Wait();
        }

        public string GetAdresseLivraisonAsync(Client c)
        {
            return c.Adresse;
        }

        public async Task<bool> CheckStock(Commande c)
        {
            return await m_repo.HasStock(c);
        }
    }

    public interface ICommandeRepo
    {
        Task<Commande> GetByNum(string num);
        Task<bool> HasStock(Commande c);
    }

    public interface IEntrepotGW
    {
        Task SendAsync(Commande c);
    }
}
```

Présentez vos réponses dans un tableau : `nom livré | règle violée | nom corrigé`.

### Partie 3 — La règle qui aurait tout empêché (5 min)

Parmi les violations relevées, laquelle est la plus fréquente dans l'extrait ? Écrivez la règle `.editorconfig`
(bloc `dotnet_naming_rule` / `dotnet_naming_symbols` / `dotnet_naming_style`) qui la signale en sévérité
`warning`, et indiquez quelle propriété MSBuild transforme cet avertissement en échec de compilation.

<details>
<summary>Indice — partie 1</summary>

Le projet `Core` est le domaine, mais son assembly s'appelle `core` et son namespace racine `Textinord` : rien ne
correspond. `TextinordTrameBLL` contient trois responsabilités qui vont dans trois couches différentes.
`Utils` est un projet « fourre-tout » : ses extensions LINQ n'ont rien à voir avec son client HTTP du WMS.

</details>

<details>
<summary>Indice — partie 2</summary>

Cherchez dans l'ordre : les interfaces sans `I`, les méthodes `async` sans `Async`, les méthodes non asynchrones
avec `Async`, les préfixes `m_` et hongrois (`i`, `str`), les `SCREAMING_CASE`, le `snake_case`, la casse des
champs privés, les abréviations (`Mgr`, `Repo`, `GW`, `cmd`), les paramètres d'une lettre. Une même ligne peut
cumuler deux violations.

</details>

<details>
<summary>Indice — partie 3</summary>

Une règle de nommage a trois blocs : `dotnet_naming_symbols.<x>.applicable_kinds`, `dotnet_naming_style.<y>.
required_suffix` ou `required_prefix`, et `dotnet_naming_rule.<z>` qui relie les deux avec une `severity`.
Pour les méthodes asynchrones, `required_modifiers = async` existe.

</details>

## Bonus

Dans la solution `solutions/module-02/tp-2/` (si vous y avez accès) ou dans un projet console vide, créez le
`.editorconfig` avec votre règle, ajoutez `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` et
`<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` dans le `.csproj`, collez une interface nommée
`commandeService` et vérifiez que `dotnet build` échoue avec le code IDE1006.
