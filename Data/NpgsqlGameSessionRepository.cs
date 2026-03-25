using jeuPoint.Models;
using Npgsql;

namespace jeuPoint.Data;

public sealed class NpgsqlGameSessionRepository : IGameSessionRepository
{
    private readonly string _connectionString;

    public NpgsqlGameSessionRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS game_sessions (
                id BIGSERIAL PRIMARY KEY,
                played_at_utc TIMESTAMPTZ NOT NULL,
                winner_name TEXT NOT NULL,
                player_one_score INTEGER NOT NULL,
                player_two_score INTEGER NOT NULL,
                target_score INTEGER NOT NULL
            );
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveSessionAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO game_sessions (played_at_utc, winner_name, player_one_score, player_two_score, target_score)
            VALUES (@playedAtUtc, @winnerName, @playerOneScore, @playerTwoScore, @targetScore);
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("playedAtUtc", session.PlayedAtUtc);
        command.Parameters.AddWithValue("winnerName", session.WinnerName);
        command.Parameters.AddWithValue("playerOneScore", session.PlayerOneScore);
        command.Parameters.AddWithValue("playerTwoScore", session.PlayerTwoScore);
        command.Parameters.AddWithValue("targetScore", session.TargetScore);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameSession>> GetRecentSessionsAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<GameSession>();
        }

        const string sql = """
            SELECT id, played_at_utc, winner_name, player_one_score, player_two_score, target_score
            FROM game_sessions
            ORDER BY played_at_utc DESC
            LIMIT @limit;
            """;

        var sessions = new List<GameSession>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sessions.Add(new GameSession
            {
                Id = reader.GetInt64(0),
                PlayedAtUtc = reader.GetDateTime(1),
                WinnerName = reader.GetString(2),
                PlayerOneScore = reader.GetInt32(3),
                PlayerTwoScore = reader.GetInt32(4),
                TargetScore = reader.GetInt32(5)
            });
        }

        return sessions;
    }
}
