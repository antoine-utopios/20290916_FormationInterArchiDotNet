namespace Textinord.Trame.Application.Messaging;

public sealed class CanalInconnuException : Exception
{
    public CanalInconnuException()
    {
    }

    public CanalInconnuException(string message)
        : base(message)
    {
    }

    public CanalInconnuException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static CanalInconnuException Pour(string canal) =>
        new($"Le canal « {canal} » n'est pas déclaré sur le bus. Déclarez-le avant de publier ou de vous abonner.");
}
