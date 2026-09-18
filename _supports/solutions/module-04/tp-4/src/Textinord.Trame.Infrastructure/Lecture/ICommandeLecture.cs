namespace Textinord.Trame.Infrastructure.Lecture;

/// <summary>Côté lecture (CQRS léger) : SQL explicite via Dapper pour les écrans et les exports.</summary>
public interface ICommandeLecture
{
    Task<IReadOnlyList<CommandeResume>> ResumesDuJourAsync(DateOnly jour, CancellationToken ct = default);
}
