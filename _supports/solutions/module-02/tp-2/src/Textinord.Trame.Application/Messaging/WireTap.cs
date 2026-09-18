namespace Textinord.Trame.Application.Messaging;

/// <summary>
/// Wire Tap : copie chaque message d'un canal vers un canal d'audit sans modifier le flux.
/// À abonner en premier sur le canal observé, pour capter aussi les messages qui échoueront ensuite.
/// </summary>
public static class WireTap
{
    public static MessageHandler Vers(string canalAudit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canalAudit);

        return (message, contexte, cancellationToken) =>
            contexte.PublierEnveloppeAsync(canalAudit, message, cancellationToken).AsTask();
    }
}
