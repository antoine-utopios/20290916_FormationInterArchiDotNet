namespace Textinord.Trame.Application.Messaging;

/// <summary>Message Translator : convertit un message d'un format vers un autre, sans effet de bord.</summary>
public interface IMessageTranslator<in TSource, out TCible>
{
    TCible Traduire(TSource source);
}
