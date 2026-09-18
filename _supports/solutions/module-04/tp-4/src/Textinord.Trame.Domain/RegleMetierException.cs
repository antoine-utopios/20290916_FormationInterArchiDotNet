namespace Textinord.Trame.Domain;

/// <summary>Violation d'une règle métier de Textinord (état interdit, remise hors plafond...).</summary>
public sealed class RegleMetierException(string message) : Exception(message);
