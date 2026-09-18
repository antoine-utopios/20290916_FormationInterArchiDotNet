namespace Textinord.Trame.Application.Tests.Outils;

/// <summary>TimeProvider pilotable : indispensable pour tester l'expiration sans attendre.</summary>
public sealed class HorlogeFixe(DateTimeOffset depart) : TimeProvider
{
    private DateTimeOffset _maintenant = depart;

    public override DateTimeOffset GetUtcNow() => _maintenant;

    public void Avancer(TimeSpan duree) => _maintenant += duree;
}
