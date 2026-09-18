using Textinord.Trame.Domain;
using Textinord.Trame.Infrastructure.Lecture;
using Xunit;

namespace Textinord.Trame.Infrastructure.Tests;

public sealed class CommandeLectureDapperTests : BaseSqliteEnMemoire
{
    [Fact]
    public async Task La_lecture_dapper_agrege_les_lignes_et_le_montant_par_commande_du_jour()
    {
        var (client, article1, article2) = JeuDeDonnees();
        var jour = new DateOnly(2026, 9, 8);

        await using (var contexte = CreerContexte())
        {
            contexte.AddRange(client, article1, article2);
            await contexte.SaveChangesAsync();

            var duJour = new Commande("CMD-2026-000300", client, jour.ToDateTime(new TimeOnly(10, 15)));
            duJour.AjouterLigne(article1, 100);   // 100 x 18,50 à 25 % = 1 387,50
            duJour.AjouterLigne(article2, 4);     // 4 x 42 à 25 % = 126,00
            duJour.Valider(_ => true);

            var veille = new Commande("CMD-2026-000299", client, jour.AddDays(-1).ToDateTime(new TimeOnly(17, 0)));
            veille.AjouterLigne(article1, 1);

            contexte.AddRange(duJour, veille);
            await contexte.SaveChangesAsync();
        }

        ICommandeLecture lecture = new CommandeLectureDapper(Connexion);
        var resumes = await lecture.ResumesDuJourAsync(jour);

        var resume = Assert.Single(resumes);
        Assert.Equal("CMD-2026-000300", resume.Numero);
        Assert.Equal("Hôtel Lux Lille", resume.Client);
        Assert.Equal("Validee", resume.Statut);
        Assert.Equal(2, resume.NombreLignes);
        Assert.Equal(1_513.50m, resume.TotalHt);
    }
}
