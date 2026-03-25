using jeuPoint.Models;

namespace jeuPoint.Data;

public interface IGameRepository
{
    Task SaveGameAsync(Game game, IReadOnlyCollection<Player> players, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedGameInfo>> ListGamesAsync(CancellationToken cancellationToken = default);

    Task<GameRestoreResult?> LoadGameAsync(long gameId, CancellationToken cancellationToken = default);
}
