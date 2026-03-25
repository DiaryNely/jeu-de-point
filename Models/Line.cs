namespace jeuPoint.Models;

public class Line
{
    public long Id { get; private set; }

    public long GameId { get; private set; }

    public Game? Game { get; private set; }

    public long PlayerId { get; private set; }

    public Player Player { get; private set; }

    public int PointsCount { get; private set; }

    public bool IsValidated { get; private set; }

    public DateTimeOffset? ValidatedAt { get; private set; }

    public Line(long id, long gameId, Player player, int pointsCount, bool isValidated = false, DateTimeOffset? validatedAt = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        if (gameId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gameId));
        }

        if (pointsCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(pointsCount), "A line must contain at least 2 points.");
        }

        if (isValidated && validatedAt is null)
        {
            throw new ArgumentException("validatedAt is required when line is validated.", nameof(validatedAt));
        }

        if (!isValidated && validatedAt is not null)
        {
            throw new ArgumentException("validatedAt must be null when line is not validated.", nameof(validatedAt));
        }

        Id = id;
        GameId = gameId;
        Player = player ?? throw new ArgumentNullException(nameof(player));
        PlayerId = player.Id;
        PointsCount = pointsCount;
        IsValidated = isValidated;
        ValidatedAt = validatedAt;
    }

    public Line(long gameId, Player player, int pointsCount)
        : this(1, gameId, player, pointsCount)
    {
    }

    public void Validate(DateTimeOffset? validatedAt = null)
    {
        IsValidated = true;
        ValidatedAt = validatedAt ?? DateTimeOffset.UtcNow;
    }

    internal void AttachGame(Game game)
    {
        Game = game;
        GameId = game.Id;
    }

    public override string ToString()
    {
        return $"Line {{ Id = {Id}, GameId = {GameId}, PlayerId = {PlayerId}, PointsCount = {PointsCount}, IsValidated = {IsValidated}, ValidatedAt = {ValidatedAt:O} }}";
    }
}
