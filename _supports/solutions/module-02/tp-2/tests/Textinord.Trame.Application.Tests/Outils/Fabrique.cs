using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Application.Messaging;

namespace Textinord.Trame.Application.Tests.Outils;

public static class Fabrique
{
    public static readonly DateTimeOffset Depart = new(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);

    public static CommandeValidee CommandeFrance(bool urgente = false) => new(
        "CMD-2026-001042",
        "CLI-000318",
        "FR",
        urgente,
        [new LigneValidee("vt-blouse-m", 120, "RBX"), new LigneValidee("EPI-GANT-L", 600, "LSQ")]);

    public static CommandeValidee CommandeExport() => new(
        "CMD-2026-001043",
        "CLI-000502",
        "BE",
        Urgente: true,
        [new LigneValidee("HOT-DRAP-240", 250, "LSQ")]);

    public static CommandeValidee CommandeSansPays() => new(
        "CMD-2026-001044",
        "CLI-000077",
        PaysLivraison: null,
        Urgente: false,
        [new LigneValidee("VT-PANTALON-44", 40, "RBX")]);

    public static CommandeValidee CommandeEntrepotInconnu() => new(
        "CMD-2026-001045",
        "CLI-000077",
        "FR",
        Urgente: false,
        [new LigneValidee("VT-PANTALON-44", 40, "PARIS")]);

    public static MessageEnvelope Enveloppe<T>(T corps, TimeSpan? dureeDeVie = null)
        where T : notnull =>
        MessageEnvelope.Creer(corps, Depart, dureeDeVie: dureeDeVie);
}
