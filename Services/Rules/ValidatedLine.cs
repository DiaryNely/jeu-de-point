namespace jeuPoint.Services.Rules;

public sealed class ValidatedLine
{
    public long Id { get; }

    public long GameId { get; }

    public long PlayerId { get; }

    public LineDirection Direction { get; }

    public IReadOnlyCollection<GridCell> Cells { get; }

    public bool IsDiagonal => Direction is LineDirection.DiagonalDown or LineDirection.DiagonalUp;

    public int PointCount => Cells.Count;

    public ValidatedLine(long id, long gameId, long playerId, LineDirection direction, IReadOnlyCollection<GridCell> cells)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        if (gameId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gameId));
        }

        if (playerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerId));
        }

        if (cells is null || cells.Count < 5)
        {
            throw new ArgumentException("A validated line must contain at least 5 cells.", nameof(cells));
        }

        Id = id;
        GameId = gameId;
        PlayerId = playerId;
        Direction = direction;
        Cells = cells;
    }

    public override string ToString()
    {
        return $"ValidatedLine {{ Id = {Id}, GameId = {GameId}, PlayerId = {PlayerId}, Direction = {Direction}, PointCount = {PointCount} }}";
    }
}

public enum LineDirection
{
    Horizontal = 1,
    Vertical = 2,
    DiagonalDown = 3,
    DiagonalUp = 4
}

public readonly record struct GridCell(int X, int Y)
{
    public override string ToString()
    {
        return $"({X},{Y})";
    }
}
