namespace Textinord.Trame.Domain.Clients;

/// <summary>
/// Un client professionnel de Textinord (collectivité, industriel, hôtel).
/// </summary>
public sealed class Client
{
    public Client(string code, string raisonSociale, ConditionTarifaire conditionTarifaire)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(raisonSociale);
        ArgumentNullException.ThrowIfNull(conditionTarifaire);

        Code = code;
        RaisonSociale = raisonSociale;
        ConditionTarifaire = conditionTarifaire;
    }

    public string Code { get; }

    public string RaisonSociale { get; }

    public ConditionTarifaire ConditionTarifaire { get; private set; }

    public void ChangerConditionTarifaire(ConditionTarifaire nouvelle)
    {
        ArgumentNullException.ThrowIfNull(nouvelle);
        ConditionTarifaire = nouvelle;
    }

    public override string ToString() => $"{Code} — {RaisonSociale} ({ConditionTarifaire.Code})";
}
