namespace Textinord.Trame.Worker.Outbox;

public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    /// <summary>Période de relecture de la table Outbox.</summary>
    public TimeSpan Intervalle { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Nombre de messages publiés par cycle.</summary>
    public int TailleLot { get; set; } = 50;

    /// <summary>Au-delà, le message n'est plus retenté : il sera traité à la main (supervision).</summary>
    public int TentativesMaximum { get; set; } = 10;
}
