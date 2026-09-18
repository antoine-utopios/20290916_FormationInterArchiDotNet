namespace Textinord.Trame.Domain;

/// <summary>Client professionnel de Textinord (collectivité, industriel, hôtel).</summary>
public sealed class Client
{
    public int Id { get; private set; }
    public string Code { get; private set; }
    public string RaisonSociale { get; private set; }
    public ConditionTarifaire ConditionTarifaire { get; private set; }

    // Constructeur réservé à EF Core (matérialisation).
    private Client()
    {
        Code = string.Empty;
        RaisonSociale = string.Empty;
    }

    public Client(string code, string raisonSociale, ConditionTarifaire conditionTarifaire)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(raisonSociale);
        Code = code.Trim().ToUpperInvariant();
        RaisonSociale = raisonSociale.Trim();
        ConditionTarifaire = conditionTarifaire;
    }

    public decimal RemisePourcent => ConditionTarifaire.RemisePourcent();

    public void ChangerConditionTarifaire(ConditionTarifaire nouvelleCondition) =>
        ConditionTarifaire = nouvelleCondition;
}
