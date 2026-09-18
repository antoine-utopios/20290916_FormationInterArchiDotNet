namespace Textinord.Trame.Domain.Commandes;

/// <summary>
/// Implémentation en mémoire, séquentielle par année et sûre en multithread.
/// Suffisante pour les tests et la démo ; remplacée par une séquence SQL au module 4.
/// </summary>
public sealed class GenerateurNumeroCommandeEnMemoire : IGenerateurNumeroCommande
{
    private readonly Dictionary<int, int> _dernieresSequences = [];
    private readonly Lock _verrou = new();

    public NumeroCommande Suivant(int annee)
    {
        lock (_verrou)
        {
            var sequence = _dernieresSequences.GetValueOrDefault(annee) + 1;
            _dernieresSequences[annee] = sequence;
            return new NumeroCommande(annee, sequence);
        }
    }
}
