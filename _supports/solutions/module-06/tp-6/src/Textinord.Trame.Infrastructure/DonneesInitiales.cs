using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;

namespace Textinord.Trame.Infrastructure;

/// <summary>Jeu de données minimal : trois clients, quatre articles, stocks par entrepôt.</summary>
public static class DonneesInitiales
{
    public static async Task InsererAsync(TrameDbContext db, CancellationToken ct = default)
    {
        if (await db.Clients.AnyAsync(ct))
        {
            return;
        }

        db.Clients.AddRange(
            new Client { Code = "C-0001", RaisonSociale = "Hôtel Le Beffroi, Lille", TauxRemise = 0.10m },
            new Client { Code = "C-0042", RaisonSociale = "Métropole Européenne de Lille", TauxRemise = 0.20m },
            new Client { Code = "C-0117", RaisonSociale = "Chantiers Vandamme SA", TauxRemise = 0.25m },
            new Client { Code = "C-0900", RaisonSociale = "Confection Dubrulle (radié)", TauxRemise = 0.05m, Actif = false });

        db.Articles.AddRange(
            new Article { Reference = "VT-1001", Libelle = "Veste de travail bleu marine, taille L", Famille = "Vêtement de travail", PrixBase = 24.90m },
            new Article { Reference = "VT-1050", Libelle = "Pantalon multipoches, taille 44", Famille = "Vêtement de travail", PrixBase = 21.50m },
            new Article { Reference = "EPI-2040", Libelle = "Gilet haute visibilité classe 2", Famille = "EPI", PrixBase = 8.50m },
            new Article { Reference = "LH-3300", Libelle = "Drap plat hôtelier 240 x 300", Famille = "Linge hôtelier", PrixBase = 19.00m });

        db.Stocks.AddRange(
            new StockArticle { Reference = "VT-1001", Entrepot = Entrepots.Roubaix, Quantite = 1200 },
            new StockArticle { Reference = "VT-1001", Entrepot = Entrepots.Lesquin, Quantite = 300 },
            new StockArticle { Reference = "VT-1050", Entrepot = Entrepots.Roubaix, Quantite = 0 },
            new StockArticle { Reference = "VT-1050", Entrepot = Entrepots.Lesquin, Quantite = 0 },
            new StockArticle { Reference = "EPI-2040", Entrepot = Entrepots.Lesquin, Quantite = 80 },
            new StockArticle { Reference = "LH-3300", Entrepot = Entrepots.Roubaix, Quantite = 2500 });

        await db.SaveChangesAsync(ct);
    }
}
