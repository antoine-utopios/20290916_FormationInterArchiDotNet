using Textinord.Trame.Domain.Tarifs;

namespace Textinord.Trame.Domain.Clients;

public sealed record Client(string Code, string RaisonSociale, ConditionTarifaire Condition, string Pays);
