using Textinord.Trame.Application.Messaging;
using Textinord.Trame.Application.Tests.Outils;

namespace Textinord.Trame.Application.Tests;

public sealed class MessageEnvelopeTests
{
    [Fact]
    public void Un_message_derive_conserve_la_correlation_et_pointe_sur_sa_cause()
    {
        var origine = Fabrique.Enveloppe("origine", dureeDeVie: TimeSpan.FromHours(4));

        var derive = origine.Deriver(42, Fabrique.Depart.AddSeconds(1)).AvecSequence(1, 2);

        Assert.Equal(origine.CorrelationId, derive.CorrelationId);
        Assert.Equal(origine.MessageId, derive.CausationId);
        Assert.NotEqual(origine.MessageId, derive.MessageId);
        Assert.Equal(origine.ExpireLe, derive.ExpireLe);
        Assert.Equal("Int32.v1", derive.Type);
        Assert.Equal((1, 2), (derive.NumeroSequence, derive.TailleSequence));
    }

    [Fact]
    public void L_expiration_se_calcule_a_partir_de_la_duree_de_vie()
    {
        var message = Fabrique.Enveloppe("x", dureeDeVie: TimeSpan.FromMinutes(30));

        Assert.False(message.EstExpire(Fabrique.Depart.AddMinutes(30)));
        Assert.True(message.EstExpire(Fabrique.Depart.AddMinutes(31)));
        Assert.False(Fabrique.Enveloppe("sans limite").EstExpire(Fabrique.Depart.AddYears(10)));
    }
}
