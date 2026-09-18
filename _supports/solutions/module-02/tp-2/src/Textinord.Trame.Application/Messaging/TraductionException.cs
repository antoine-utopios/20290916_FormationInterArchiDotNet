namespace Textinord.Trame.Application.Messaging;

/// <summary>Levée par un traducteur quand le message source ne peut pas être converti.</summary>
public sealed class TraductionException : Exception
{
    public TraductionException()
    {
    }

    public TraductionException(string message)
        : base(message)
    {
    }

    public TraductionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
