# Exercice 1.2 — Repérer et corriger cinq violations SOLID dans `CommandeManager`

> Module : 1 — Architecturer un SI .NET : styles, couches et design patterns
> Durée estimée : 30 min (20 min de travail, 10 min de comparaison)
> Difficulté : 3 / 5
> Type : lecture critique de code puis refactoring, en binôme

## Objectifs pédagogiques

À la fin de cet exercice, vous serez capable de :

- Reconnaître dans du code réel chacune des cinq violations SOLID et la nommer
- Proposer pour chacune la correction adaptée : extraction de classe, interface côté consommateur, injection, remplacement d'un héritage par une implémentation
- Réécrire un cas d'usage (la validation d'une commande) testable sans base de données, sans SMTP et sans système de fichiers

## Prérequis

- Avoir suivi la démo 1.1 (refactoring SOLID du calcul de tarif)
- SDK .NET 10 installé si vous voulez compiler votre version (facultatif : le refactoring peut se faire sur papier ou dans un fichier `.cs` non compilé)

## Contexte

Sofia Marques a extrait de Trame la classe `CommandeManager`, appelée depuis
l'écran WinForms « Validation ADV ». Elle veut la réécrire dans Trame 2 sans
en reproduire les défauts, et vous demande de les lister d'abord. Le code
compile et fonctionne en production depuis 2016.

Rappel des règles métier concernées : la remise client dépend de la condition
tarifaire (COL 10 %, HOT 12 %, IND 15 %, GC 25 %, sinon 0 %) ; une commande se
valide seulement si chaque ligne a du stock, sinon elle passe en attente de
stock ; le client est notifié à la validation.

## Le code fourni

```csharp
// Trame — Metier/CommandeManager.cs (extrait, 2016). Cinq violations SOLID à repérer.
using System.Net.Mail;
using Microsoft.Data.SqlClient;

namespace Trame.Metier;

public interface ICommandeManager
{
    void ValiderCommande(int idCommande);
    void AnnulerCommande(int idCommande);
    void ExporterEdi(int idCommande);
    void ImprimerBonPreparation(int idCommande);
    void EnvoyerEmailConfirmation(int idCommande, string email);
    void RecalculerTousLesTarifs();
}

public class CommandeManager : ICommandeManager
{
    private const string Cnx = "Server=SRV-TRAME01;Database=TRAME;Integrated Security=true;TrustServerCertificate=true";

    public virtual void ValiderCommande(int idCommande)
    {
        using var cnx = new SqlConnection(Cnx);
        cnx.Open();

        string condition;
        string emailClient;
        using (var cmd = new SqlCommand(
            "SELECT c.CONDITION_TARIF, c.EMAIL FROM COMMANDES co JOIN CLIENTS c ON c.CODE = co.CODE_CLIENT WHERE co.ID = @id", cnx))
        {
            cmd.Parameters.AddWithValue("@id", idCommande);
            using var lecteur = cmd.ExecuteReader();
            lecteur.Read();
            condition = lecteur.GetString(0);
            emailClient = lecteur.GetString(1);
        }

        decimal remise;
        switch (condition)
        {
            case "COL": remise = 0.10m; break;
            case "HOT": remise = 0.12m; break;
            case "IND": remise = 0.15m; break;
            case "GC": remise = 0.25m; break;
            default: remise = 0m; break;
        }

        bool stockOk = true;
        using (var cmd = new SqlCommand(
            "SELECT l.REF_ARTICLE, l.QTE, ISNULL(SUM(s.QTE_DISPO), 0) FROM LIGNES l LEFT JOIN STOCKS s ON s.REF_ARTICLE = l.REF_ARTICLE WHERE l.ID_COMMANDE = @id GROUP BY l.REF_ARTICLE, l.QTE", cnx))
        {
            cmd.Parameters.AddWithValue("@id", idCommande);
            using var lecteur = cmd.ExecuteReader();
            while (lecteur.Read())
            {
                if (lecteur.GetInt32(2) < lecteur.GetInt32(1)) stockOk = false;
            }
        }

        var statut = stockOk ? "VALIDEE" : "ATTENTE_STOCK";
        using (var cmd = new SqlCommand("UPDATE COMMANDES SET STATUT = @s, REMISE = @r WHERE ID = @id", cnx))
        {
            cmd.Parameters.AddWithValue("@s", statut);
            cmd.Parameters.AddWithValue("@r", remise);
            cmd.Parameters.AddWithValue("@id", idCommande);
            cmd.ExecuteNonQuery();
        }

        if (stockOk)
        {
            var smtp = new SmtpClient("smtp.textinord.local");
            smtp.Send("adv@textinord.fr", emailClient, "Commande validée", $"Votre commande n° {idCommande} est validée.");
        }

        File.AppendAllText(@"C:\Trame\Logs\commandes.log", $"{DateTime.Now:s} VALIDATION {idCommande} {statut}\n");
    }

    public void AnnulerCommande(int idCommande) => throw new NotImplementedException("Annulation gérée par l'écran WinForms.");

    public void ExporterEdi(int idCommande) => throw new NotImplementedException();

    public void ImprimerBonPreparation(int idCommande) => throw new NotImplementedException();

    public void EnvoyerEmailConfirmation(int idCommande, string email) => throw new NotImplementedException();

    public void RecalculerTousLesTarifs() => throw new NotImplementedException();
}

// Les commandes export se valident dans l'outil douane : la sous-classe désactive la validation.
public class CommandeExportManager : CommandeManager
{
    public override void ValiderCommande(int idCommande) =>
        throw new NotSupportedException("Les commandes export ne se valident pas dans Trame.");
}
```

## Énoncé

### Partie 1 — Le diagnostic (8 min)

Remplissez le tableau suivant, une ligne par violation. Il y en a exactement
cinq, une par lettre de SOLID. Indiquez la ligne ou le fragment de code
concerné, le principe violé, et en une phrase ce que cela coûte à Textinord
(un symptôme concret : test impossible, régression, duplication, panne).

| # | Fragment de code | Principe violé | Ce que cela coûte |
|---|---|---|---|
| 1 | | | |
| 2 | | | |
| 3 | | | |
| 4 | | | |
| 5 | | | |

### Partie 2 — Le plan de correction (5 min)

Pour chaque ligne du tableau, écrivez la correction en une phrase qui
commence par un verbe : « extraire… », « remplacer… par… », « injecter… »,
« découper… en… ». Nommez les interfaces et les classes que vous créez.

### Partie 3 — La réécriture (7 min)

Réécrivez la validation d'une commande sous la forme d'un service
`ValidationCommandeService` qui respecte votre plan : ses dépendances sont
injectées par le constructeur, aucune ne référence SQL Server, SMTP ni le
système de fichiers, et la politique de remise n'est plus un `switch`.
Réglez aussi le cas des commandes export sans lever `NotSupportedException`.

Vous pouvez vous appuyer sur la structure de la démo 1.1 (`IPolitiqueRemise`,
ports, `Result`) ; le code n'a pas besoin de compiler en séance, mais chaque
dépendance doit avoir un nom et une interface.

### Partie 4 — Comparaison (10 min)

Deux binômes comparent leurs découpages. Question à trancher : combien
d'interfaces avez-vous créées, et une seule aurait-elle suffi ? La bonne
réponse dépend du nombre de raisons de changer, pas du nombre de méthodes.

## Indices (à consulter si bloqué)

<details>
<summary>Indice 1 — Je ne trouve que trois violations</summary>

Regardez ailleurs que dans la méthode `ValiderCommande` : l'interface
`ICommandeManager` et la sous-classe `CommandeExportManager` portent
chacune une violation.

</details>

<details>
<summary>Indice 2 — Le S et le D se ressemblent</summary>

Le **S** parle du nombre de raisons de changer d'une classe (ici : le
schéma SQL, la grille de remise, le serveur SMTP, le format du journal, la
règle de stock). Le **D** parle du sens des dépendances : le métier ne doit
pas dépendre de `SqlConnection` ou `SmtpClient`, mais d'abstractions qu'il
possède. Une même ligne de code peut violer les deux ; comptez-la une fois
pour chacun avec un fragment différent.

</details>

<details>
<summary>Indice 3 — Que faire des commandes export</summary>

Une commande export se valide aussi, par un autre processus (dossier
douane). Plutôt qu'une sous-classe qui refuse, écrivez une seconde
implémentation de la même interface de validation, qui honore le contrat :
elle retourne un statut.

</details>

## Pour aller plus loin (bonus)

Écrivez deux tests xUnit avec NSubstitute pour votre
`ValidationCommandeService` : « stock suffisant → statut Validee et
notification envoyée », « stock insuffisant → statut EnAttenteStock et
aucune notification ». Si les tests s'écrivent sans effort, votre découpage
est bon ; s'il faut instancier quelque chose de lourd, une dépendance a
échappé à l'injection.
