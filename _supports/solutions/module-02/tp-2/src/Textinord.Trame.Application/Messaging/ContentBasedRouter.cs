namespace Textinord.Trame.Application.Messaging;

public sealed record DecisionRoutage(string? Canal, string Regle)
{
    public bool EstRejet => Canal is null;
}

/// <summary>
/// Content-Based Router : examine le contenu du message et choisit le canal de sortie.
/// Les règles sont évaluées dans l'ordre ; la première qui s'applique gagne.
/// Sans règle applicable ni canal par défaut, le message part en dead letter.
/// </summary>
public sealed class ContentBasedRouter
{
    private readonly List<RegleRoutage> _regles = [];
    private string? _canalParDefaut;

    public IReadOnlyList<string> Regles => _regles.Select(r => r.Nom).ToList();

    public ContentBasedRouter Quand(string nom, Func<MessageEnvelope, bool> condition, string canalCible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentException.ThrowIfNullOrWhiteSpace(canalCible);

        _regles.Add(new RegleRoutage(nom, condition, canalCible));
        return this;
    }

    public ContentBasedRouter Quand<T>(string nom, Func<T, bool> condition, string canalCible)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return Quand(nom, message => message.Corps is T corps && condition(corps), canalCible);
    }

    /// <summary>Règle de rejet explicite : le message part en dead letter avec le nom de la règle pour raison.</summary>
    public ContentBasedRouter Rejeter<T>(string nom, Func<T, bool> condition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);
        ArgumentNullException.ThrowIfNull(condition);

        _regles.Add(new RegleRoutage(nom, message => message.Corps is T corps && condition(corps), null));
        return this;
    }

    public ContentBasedRouter Sinon(string canalCible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canalCible);
        _canalParDefaut = canalCible;
        return this;
    }

    public DecisionRoutage Decider(MessageEnvelope message)
    {
        ArgumentNullException.ThrowIfNull(message);

        foreach (var regle in _regles)
        {
            if (regle.Condition(message))
            {
                return new DecisionRoutage(regle.CanalCible, regle.Nom);
            }
        }

        return _canalParDefaut is null
            ? new DecisionRoutage(null, "aucune règle applicable")
            : new DecisionRoutage(_canalParDefaut, "défaut");
    }

    /// <summary>Le routeur en tant que handler : à abonner sur le canal d'entrée.</summary>
    public async Task RouterAsync(MessageEnvelope message, IMessageContext contexte, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contexte);

        var decision = Decider(message);

        if (decision.EstRejet)
        {
            await contexte.RejeterAsync($"Routage refusé : {decision.Regle}", cancellationToken).ConfigureAwait(false);
            return;
        }

        // Le routeur ne modifie pas le message : même enveloppe, même identifiant.
        await contexte.PublierEnveloppeAsync(decision.Canal!, message, cancellationToken).ConfigureAwait(false);
    }

    private sealed record RegleRoutage(string Nom, Func<MessageEnvelope, bool> Condition, string? CanalCible);
}
