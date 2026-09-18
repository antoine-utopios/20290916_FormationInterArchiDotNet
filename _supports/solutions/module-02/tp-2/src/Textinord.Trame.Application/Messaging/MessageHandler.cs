namespace Textinord.Trame.Application.Messaging;

/// <summary>Un consommateur de messages : reçoit l'enveloppe et un contexte pour publier, rejeter, dater.</summary>
public delegate Task MessageHandler(MessageEnvelope message, IMessageContext contexte, CancellationToken cancellationToken);
