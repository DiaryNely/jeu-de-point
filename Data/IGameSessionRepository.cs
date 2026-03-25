using jeuPoint.Models;

namespace jeuPoint.Data;

public interface IGameSessionRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SaveSessionAsync(GameSession session, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameSession>> GetRecentSessionsAsync(int limit, CancellationToken cancellationToken = default);
}
