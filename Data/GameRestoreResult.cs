using jeuPoint.Models;

namespace jeuPoint.Data;

public sealed class GameRestoreResult
{
    public Game Game { get; }

    public IReadOnlyList<Player> Players { get; }

    public IReadOnlyDictionary<(int X, int Y), long> ActiveGrid { get; }

    public IReadOnlyDictionary<long, int> ScoresByPlayer { get; }

    public IReadOnlyCollection<(long PlayerId, int X, int Y)> OwnershipClaims { get; }

    public GameRestoreResult(Game game, IReadOnlyList<Player> players, IReadOnlyCollection<(long PlayerId, int X, int Y)>? ownershipClaims = null)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        Players = players ?? throw new ArgumentNullException(nameof(players));

        ActiveGrid = game.Points
            .Where(p => !p.IsDestroyed)
            .ToDictionary(p => (p.X, p.Y), p => p.PlayerId);

        ScoresByPlayer = players.ToDictionary(p => p.Id, p => p.Score);
        OwnershipClaims = ownershipClaims ?? Array.Empty<(long PlayerId, int X, int Y)>();
    }

    public override string ToString()
    {
        return $"GameRestoreResult {{ GameId = {Game.Id}, Players = {Players.Count}, ActivePoints = {ActiveGrid.Count}, Moves = {Game.Moves.Count} }}";
    }
}
