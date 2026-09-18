namespace Textinord.Trame.Infrastructure.Lecture;

/// <summary>Ligne d'écran « commandes du jour » : lecture directe, sans entité ni change tracker.</summary>
public sealed record CommandeResume(
    string Numero,
    string Client,
    string Statut,
    int NombreLignes,
    decimal TotalHt);
