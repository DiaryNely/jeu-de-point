using jeuPoint.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using DomainPoint = jeuPoint.Models.Point;

namespace jeuPoint.Data;

public sealed class NpgsqlGameRepository : IGameRepository
{
    private readonly string _connectionString;
    private readonly ILogger<NpgsqlGameRepository> _logger;

    public NpgsqlGameRepository(string connectionString, ILogger<NpgsqlGameRepository> logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));
        }

        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task SaveGameAsync(Game game, IReadOnlyCollection<Player> players, CancellationToken cancellationToken = default)
    {
        if (game is null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        if (players is null || players.Count == 0)
        {
            throw new ArgumentException("At least one player is required.", nameof(players));
        }

        _logger.LogInformation(
            "Repository SaveGameAsync started. GameId={GameId}, Players={PlayerCount}, Moves={MoveCount}, Points={PointCount}, Lines={LineCount}",
            game.Id,
            players.Count,
            game.Moves.Count,
            game.Points.Count,
            game.Lines.Count);

        await using var connection = await OpenConnectionWithSearchPathAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await UpsertPlayersAsync(connection, transaction, players, cancellationToken);
            await UpsertGameAsync(connection, transaction, game, cancellationToken);

            await DeleteGameChildrenAsync(connection, transaction, game.Id, cancellationToken);

            await InsertMovesAsync(connection, transaction, game.Moves, cancellationToken);
            await InsertPointsAsync(connection, transaction, game.Points, cancellationToken);
            await InsertLinesAsync(connection, transaction, game.Lines, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Repository SaveGameAsync committed successfully. GameId={GameId}", game.Id);
        }
        catch (PostgresException postgresException)
        {
            _logger.LogError(
                postgresException,
                "PostgreSQL error during SaveGameAsync. GameId={GameId}, SqlState={SqlState}, Constraint={Constraint}, Table={Table}, Detail={Detail}",
                game.Id,
                postgresException.SqlState,
                postgresException.ConstraintName,
                postgresException.TableName,
                postgresException.Detail);

            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during SaveGameAsync. GameId={GameId}", game.Id);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<GameRestoreResult?> LoadGameAsync(long gameId, CancellationToken cancellationToken = default)
    {
        if (gameId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gameId));
        }

        _logger.LogInformation("Repository LoadGameAsync started. GameId={GameId}", gameId);

        try
        {
            await using var connection = await OpenConnectionWithSearchPathAsync(cancellationToken);

            var gameRecord = await ReadGameAsync(connection, gameId, cancellationToken);
            if (gameRecord is null)
            {
                _logger.LogWarning("Repository LoadGameAsync: no game record found. GameId={GameId}", gameId);
                return null;
            }

            var players = await ReadPlayersForGameAsync(connection, gameId, cancellationToken);
            var playersById = players.ToDictionary(p => p.Id);

            var game = new Game(gameRecord.Id, gameRecord.Width, gameRecord.Height, gameRecord.Status, gameRecord.CreatedAt);

            if (gameRecord.CurrentPlayerId is long currentPlayerId && playersById.TryGetValue(currentPlayerId, out var currentPlayer))
            {
                game.SetCurrentPlayer(currentPlayer);
            }

            await ReadPointsAsync(connection, game, playersById, cancellationToken);
            await ReadLinesAsync(connection, game, playersById, cancellationToken);
            await ReadMovesAsync(connection, game, playersById, cancellationToken);

            _logger.LogInformation(
                "Repository LoadGameAsync completed. GameId={GameId}, Players={PlayerCount}, Moves={MoveCount}, Points={PointCount}, Lines={LineCount}",
                game.Id,
                players.Count,
                game.Moves.Count,
                game.Points.Count,
                game.Lines.Count);

            return new GameRestoreResult(game, players);
        }
        catch (PostgresException postgresException)
        {
            _logger.LogError(
                postgresException,
                "PostgreSQL error during LoadGameAsync. GameId={GameId}, SqlState={SqlState}, Constraint={Constraint}, Table={Table}, Detail={Detail}",
                gameId,
                postgresException.SqlState,
                postgresException.ConstraintName,
                postgresException.TableName,
                postgresException.Detail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during LoadGameAsync. GameId={GameId}", gameId);
            throw;
        }
    }

    public async Task<IReadOnlyList<SavedGameInfo>> ListGamesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Repository ListGamesAsync started.");

        try
        {
            await using var connection = await OpenConnectionWithSearchPathAsync(cancellationToken);

            const string sql = """
                SELECT id, width, height, status::text, created_at
                FROM game.games
                ORDER BY created_at DESC, id DESC;
                """;

            var results = new List<SavedGameInfo>();

            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SavedGameInfo(
                    id: reader.GetInt64(0),
                    width: reader.GetInt32(1),
                    height: reader.GetInt32(2),
                    status: FromDbGameStatus(reader.GetString(3)),
                    createdAt: new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc))));
            }

            _logger.LogInformation("Repository ListGamesAsync completed. Count={Count}", results.Count);
            return results;
        }
        catch (PostgresException postgresException)
        {
            _logger.LogError(
                postgresException,
                "PostgreSQL error during ListGamesAsync. SqlState={SqlState}, Constraint={Constraint}, Table={Table}, Detail={Detail}",
                postgresException.SqlState,
                postgresException.ConstraintName,
                postgresException.TableName,
                postgresException.Detail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during ListGamesAsync.");
            throw;
        }
    }

    private static async Task UpsertPlayersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyCollection<Player> players,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO game.players (id, name, score)
            VALUES (@id, @name, @score)
            ON CONFLICT (id)
            DO UPDATE SET
                name = EXCLUDED.name,
                score = EXCLUDED.score;
            """;

        foreach (var player in players)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("id", player.Id);
            command.Parameters.AddWithValue("name", player.Name);
            command.Parameters.AddWithValue("score", player.Score);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpsertGameAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Game game,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO game.games (id, width, height, current_player_id, status, created_at)
            VALUES (@id, @width, @height, @currentPlayerId, @status::game.game_status, @createdAt)
            ON CONFLICT (id)
            DO UPDATE SET
                width = EXCLUDED.width,
                height = EXCLUDED.height,
                current_player_id = EXCLUDED.current_player_id,
                status = EXCLUDED.status,
                created_at = EXCLUDED.created_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", game.Id);
        command.Parameters.AddWithValue("width", game.Width);
        command.Parameters.AddWithValue("height", game.Height);
        command.Parameters.AddWithValue("currentPlayerId", game.CurrentPlayerId is null ? DBNull.Value : game.CurrentPlayerId.Value);
        command.Parameters.AddWithValue("status", ToDbGameStatus(game.Status));
        command.Parameters.AddWithValue("createdAt", game.CreatedAt.UtcDateTime);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteGameChildrenAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long gameId,
        CancellationToken cancellationToken)
    {
        var sqlStatements = new[]
        {
            "DELETE FROM game.moves WHERE game_id = @gameId;",
            "DELETE FROM game.points WHERE game_id = @gameId;",
            "DELETE FROM game.lines WHERE game_id = @gameId;"
        };

        foreach (var sql in sqlStatements)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("gameId", gameId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertMovesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyCollection<Move> moves,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO game.moves (id, game_id, player_id, x, y, type, power, created_at)
            VALUES (@id, @gameId, @playerId, @x, @y, @type::game.move_type, @power, @createdAt);
            """;

        foreach (var move in moves)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("id", move.Id);
            command.Parameters.AddWithValue("gameId", move.GameId);
            command.Parameters.AddWithValue("playerId", move.PlayerId);
            command.Parameters.AddWithValue("x", move.X);
            command.Parameters.AddWithValue("y", move.Y);
            command.Parameters.AddWithValue("type", ToDbMoveType(move.Type));
            command.Parameters.AddWithValue("power", move.Power);
            command.Parameters.AddWithValue("createdAt", move.CreatedAt.UtcDateTime);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertPointsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyCollection<DomainPoint> points,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO game.points (id, game_id, player_id, x, y, is_destroyed)
            VALUES (@id, @gameId, @playerId, @x, @y, @isDestroyed);
            """;

        foreach (var point in points)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("id", point.Id);
            command.Parameters.AddWithValue("gameId", point.GameId);
            command.Parameters.AddWithValue("playerId", point.PlayerId);
            command.Parameters.AddWithValue("x", point.X);
            command.Parameters.AddWithValue("y", point.Y);
            command.Parameters.AddWithValue("isDestroyed", point.IsDestroyed);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertLinesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyCollection<Line> lines,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO game.lines (id, game_id, player_id, points_count, is_validated, validated_at)
            VALUES (@id, @gameId, @playerId, @pointsCount, @isValidated, @validatedAt);
            """;

        foreach (var line in lines)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("id", line.Id);
            command.Parameters.AddWithValue("gameId", line.GameId);
            command.Parameters.AddWithValue("playerId", line.PlayerId);
            command.Parameters.AddWithValue("pointsCount", line.PointsCount);
            command.Parameters.AddWithValue("isValidated", line.IsValidated);
            command.Parameters.AddWithValue("validatedAt", line.ValidatedAt?.UtcDateTime ?? (object)DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<GameRecord?> ReadGameAsync(NpgsqlConnection connection, long gameId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, width, height, current_player_id, status::text, created_at
            FROM game.games
            WHERE id = @gameId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("gameId", gameId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new GameRecord(
            Id: reader.GetInt64(0),
            Width: reader.GetInt32(1),
            Height: reader.GetInt32(2),
            CurrentPlayerId: reader.IsDBNull(3) ? null : reader.GetInt64(3),
            Status: FromDbGameStatus(reader.GetString(4)),
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc)));
    }

    private static async Task<List<Player>> ReadPlayersForGameAsync(NpgsqlConnection connection, long gameId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT p.id, p.name, p.score
            FROM game.players p
            INNER JOIN (
                SELECT current_player_id AS player_id FROM game.games WHERE id = @gameId
                UNION
                SELECT player_id FROM game.moves WHERE game_id = @gameId
                UNION
                SELECT player_id FROM game.points WHERE game_id = @gameId
                UNION
                SELECT player_id FROM game.lines WHERE game_id = @gameId
            ) gp ON gp.player_id = p.id
            ORDER BY p.id;
            """;

        var players = new List<Player>();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("gameId", gameId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            players.Add(new Player(
                id: reader.GetInt64(0),
                name: reader.GetString(1),
                score: reader.GetInt32(2)));
        }

        return players;
    }

    private static async Task ReadMovesAsync(
        NpgsqlConnection connection,
        Game game,
        IReadOnlyDictionary<long, Player> playersById,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, player_id, x, y, type::text, power, created_at
            FROM game.moves
            WHERE game_id = @gameId
            ORDER BY id;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("gameId", game.Id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var playerId = reader.GetInt64(1);
            if (!playersById.TryGetValue(playerId, out var player))
            {
                continue;
            }

            game.AddMove(
                moveId: reader.GetInt64(0),
                player: player,
                x: reader.GetInt32(2),
                y: reader.GetInt32(3),
                type: FromDbMoveType(reader.GetString(4)),
                power: reader.GetInt32(5),
                createdAt: new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc)));
        }
    }

    private static async Task ReadPointsAsync(
        NpgsqlConnection connection,
        Game game,
        IReadOnlyDictionary<long, Player> playersById,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, player_id, x, y, is_destroyed
            FROM game.points
            WHERE game_id = @gameId
            ORDER BY id;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("gameId", game.Id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var playerId = reader.GetInt64(1);
            if (!playersById.TryGetValue(playerId, out var player))
            {
                continue;
            }

            game.AddPoint(
                pointId: reader.GetInt64(0),
                player: player,
                x: reader.GetInt32(2),
                y: reader.GetInt32(3),
                isDestroyed: reader.GetBoolean(4));
        }
    }

    private static async Task ReadLinesAsync(
        NpgsqlConnection connection,
        Game game,
        IReadOnlyDictionary<long, Player> playersById,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, player_id, points_count, is_validated, validated_at
            FROM game.lines
            WHERE game_id = @gameId
            ORDER BY id;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("gameId", game.Id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var playerId = reader.GetInt64(1);
            if (!playersById.TryGetValue(playerId, out var player))
            {
                continue;
            }

            var isValidated = reader.GetBoolean(3);
            DateTimeOffset? validatedAt = null;

            if (!reader.IsDBNull(4))
            {
                validatedAt = new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc));
            }

            game.AddLine(
                lineId: reader.GetInt64(0),
                player: player,
                pointsCount: reader.GetInt32(2),
                isValidated: isValidated,
                validatedAt: validatedAt);
        }
    }

    private static string ToDbGameStatus(GameStatus status)
    {
        return status switch
        {
            GameStatus.Pending => "pending",
            GameStatus.InProgress => "in_progress",
            GameStatus.Finished => "finished",
            GameStatus.Cancelled => "cancelled",
            _ => "pending"
        };
    }

    private static GameStatus FromDbGameStatus(string status)
    {
        return status switch
        {
            "pending" => GameStatus.Pending,
            "in_progress" => GameStatus.InProgress,
            "finished" => GameStatus.Finished,
            "cancelled" => GameStatus.Cancelled,
            _ => GameStatus.Pending
        };
    }

    private static string ToDbMoveType(MoveType moveType)
    {
        return moveType switch
        {
            MoveType.Place => "place_point",
            MoveType.Shoot => "destroy_point",
            _ => "special"
        };
    }

    private static MoveType FromDbMoveType(string moveType)
    {
        return moveType switch
        {
            "place_point" => MoveType.Place,
            "destroy_point" => MoveType.Shoot,
            _ => MoveType.Place
        };
    }

    private sealed record GameRecord(
        long Id,
        int Width,
        int Height,
        long? CurrentPlayerId,
        GameStatus Status,
        DateTimeOffset CreatedAt);

    private async Task<NpgsqlConnection> OpenConnectionWithSearchPathAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SET search_path TO game, public;";
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}
