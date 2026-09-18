using Textinord.Trame.Api.Domaine;

namespace Textinord.Trame.Api.Commandes;

// Les contrats de l'API sont des records immuables, distincts des entités du domaine :
// on peut faire évoluer la représentation (v1 → v2) sans toucher aux règles métier.

/// <summary>Corps de la requête POST /commandes.</summary>
public sealed record CreerCommandeRequete(string CodeClient, IReadOnlyList<LigneRequete> Lignes);

/// <summary>Une ligne demandée : la référence, la quantité, et un prix négocié facultatif (sinon le prix de base).</summary>
public sealed record LigneRequete(string Reference, int Quantite, decimal? PrixNegocie = null);

/// <summary>Représentation courte d'une commande dans une liste (v1 et v2).</summary>
public sealed record CommandeResume(string Numero, string CodeClient, DateOnly Date, StatutCommande Statut, int NombreLignes, int NombrePieces, decimal TotalHT);

/// <summary>Représentation détaillée v1 : la remise est un taux unique par ligne.</summary>
public sealed record CommandeDetailV1(string Numero, string CodeClient, DateOnly Date, StatutCommande Statut, IReadOnlyList<LigneDetailV1> Lignes, decimal TotalHT);

public sealed record LigneDetailV1(int Numero, string Reference, int Quantite, decimal PrixUnitaire, decimal TauxRemise, decimal MontantNet, string? Entrepot);

/// <summary>Représentation détaillée v2 : la remise est expliquée (client, volume, plafond) et les totaux sont décomposés.</summary>
public sealed record CommandeDetailV2(string Numero, string CodeClient, DateOnly Date, StatutCommande Statut, IReadOnlyList<LigneDetailV2> Lignes, TotauxCommande Totaux);

public sealed record LigneDetailV2(int Numero, string Reference, int Quantite, decimal PrixUnitaire, RemiseDetail Remise, decimal MontantBrut, decimal MontantNet, string? Entrepot);

public sealed record RemiseDetail(decimal Client, decimal Volume, decimal Appliquee, bool PlafondAtteint);

public sealed record TotauxCommande(decimal Brut, decimal Remises, decimal Net, int Pieces);

/// <summary>Page de résultats : les clients paginent toujours, 900 commandes par jour ne se listent pas d'un bloc.</summary>
public sealed record Page<T>(IReadOnlyList<T> Elements, int Numero, int Taille, int Total)
{
    public int NombrePages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)Taille);
}

/// <summary>Un statut et les transitions autorisées depuis ce statut (référentiel public, mis en cache).</summary>
public sealed record StatutDescription(StatutCommande Statut, string Libelle, IReadOnlyList<StatutCommande> TransitionsPossibles);

public static class Projections
{
    public static CommandeResume VersResume(this Commande c) =>
        new(c.Numero, c.CodeClient, c.Date, c.Statut, c.Lignes.Count, c.NombrePieces, c.TotalHT);

    public static CommandeDetailV1 VersDetailV1(this Commande c) =>
        new(
            c.Numero,
            c.CodeClient,
            c.Date,
            c.Statut,
            c.Lignes.Select(l => new LigneDetailV1(l.Numero, l.Reference, l.Quantite, l.PrixUnitaire, l.Remise.TauxApplique, l.MontantNet, l.EntrepotAffecte)).ToList(),
            c.TotalHT);

    public static CommandeDetailV2 VersDetailV2(this Commande c) =>
        new(
            c.Numero,
            c.CodeClient,
            c.Date,
            c.Statut,
            c.Lignes.Select(l => new LigneDetailV2(
                l.Numero,
                l.Reference,
                l.Quantite,
                l.PrixUnitaire,
                new RemiseDetail(l.Remise.TauxClient, l.Remise.TauxVolume, l.Remise.TauxApplique, l.Remise.PlafondAtteint),
                l.MontantBrut,
                l.MontantNet,
                l.EntrepotAffecte)).ToList(),
            new TotauxCommande(c.TotalBrut, c.TotalRemises, c.TotalHT, c.NombrePieces));
}
