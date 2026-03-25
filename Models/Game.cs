namespace jeuPoint.Models;

public class Game
{
    private readonly List<Move> _moves = new();
    private readonly List<Point> _points = new();
    private readonly List<Line> _lines = new();

    public long Id { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public long? CurrentPlayerId { get; private set; }

    public Player? CurrentPlayer { get; private set; }

    public GameStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Move> Moves => _moves;

    public IReadOnlyCollection<Point> Points => _points;

    public IReadOnlyCollection<Line> Lines => _lines;

    public Game(long id, int width, int height, GameStatus status = GameStatus.Pending, DateTimeOffset? createdAt = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        ValidateDimensions(width, height);

        Id = id;
        Width = width;
        Height = height;
        Status = status;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Game(int width, int height) : this(1, width, height)
    {
    }

    public void SetCurrentPlayer(Player? player)
    {
        CurrentPlayer = player;
        CurrentPlayerId = player?.Id;

        if (player is not null)
        {
            player.LinkCurrentGame(this);
        }
    }

    public void SetStatus(GameStatus status)
    {
        Status = status;
    }

    public Move AddMove(long moveId, Player player, int x, int y, MoveType type, int power = 0, DateTimeOffset? createdAt = null)
    {
        var move = new Move(moveId, Id, player, x, y, type, power, createdAt);
        EnsureWithinBounds(move.X, move.Y);

        _moves.Add(move);
        player.LinkMove(move);

        return move;
    }

    public Point AddPoint(long pointId, Player player, int x, int y, bool isDestroyed = false)
    {
        EnsureWithinBounds(x, y);

        if (_points.Any(p => p.X == x && p.Y == y))
        {
            throw new InvalidOperationException($"A point already exists at ({x}, {y}) for game {Id}.");
        }

        var point = new Point(pointId, Id, player, x, y, isDestroyed);
        _points.Add(point);
        player.LinkPoint(point);

        return point;
    }

    public Line AddLine(long lineId, Player player, int pointsCount, bool isValidated = false, DateTimeOffset? validatedAt = null)
    {
        var line = new Line(lineId, Id, player, pointsCount, isValidated, validatedAt);
        _lines.Add(line);
        player.LinkLine(line);

        return line;
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }
    }

    private void EnsureWithinBounds(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Coordinates ({x}, {y}) are outside grid {Width}x{Height}.");
        }
    }

    public override string ToString()
    {
        return $"Game {{ Id = {Id}, Grid = {Width}x{Height}, CurrentPlayerId = {CurrentPlayerId}, Status = {Status}, CreatedAt = {CreatedAt:O} }}";
    }
}
