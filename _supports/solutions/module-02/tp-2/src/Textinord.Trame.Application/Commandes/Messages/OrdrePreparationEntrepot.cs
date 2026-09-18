namespace Textinord.Trame.Application.Commandes.Messages;

/// <summary>
/// Document Message au format attendu par le WMS des entrepôts (hérité de Trame) :
/// codes courts, libellés en majuscules, lignes numérotées. C'est le format « externe »,
/// produit par le traducteur ; il ne remonte jamais dans le domaine.
/// </summary>
public sealed record OrdrePreparationEntrepot(
    string NumeroOrdre,
    string Site,
    string NumeroCommande,
    string CodeClient,
    string TypeFlux,
    string Priorite,
    IReadOnlyList<LignePreparation> Lignes);

public sealed record LignePreparation(int Rang, string Reference, int Quantite);
