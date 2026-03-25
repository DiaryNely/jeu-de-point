using jeuPoint.Models;

namespace jeuPoint.Services;

public interface IGameEngine
{
    GameState CurrentState { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    GameState StartNewGame(int targetScore);

    GameState AddPointToPlayer(int playerIndex);

    Task PersistCurrentSessionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameSession>> GetRecentSessionsAsync(int limit, CancellationToken cancellationToken = default);
}
