namespace jeuPoint.Models;

public class Move
{
    public long Id { get; private set; }

    public long GameId { get; private set; }

    public Game? Game { get; private set; }

    public long PlayerId { get; private set; }

    public Player Player { get; private set; }

    public int X { get; private set; }

    public int Y { get; private set; }

    public MoveType Type { get; private set; }

    public int Power { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Move(long id, long gameId, Player player, int x, int y, MoveType type, int power = 0, DateTimeOffset? createdAt = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        if (gameId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gameId));
        }

        if (x < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if (y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        if (power < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(power));
        }

        Id = id;
        GameId = gameId;
        Player = player ?? throw new ArgumentNullException(nameof(player));
        PlayerId = player.Id;
        X = x;
        Y = y;
        Type = type;
        Power = power;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Move(long gameId, Player player, int x, int y, MoveType type, int power = 0)
        : this(1, gameId, player, x, y, type, power)
    {
    }

    public void UpdatePower(int power)
    {
        if (power < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(power));
        }

        Power = power;
    }

    internal void AttachGame(Game game)
    {
        Game = game;
        GameId = game.Id;
    }

    public override string ToString()
    {
        return $"Move {{ Id = {Id}, GameId = {GameId}, PlayerId = {PlayerId}, X = {X}, Y = {Y}, Type = {Type}, Power = {Power}, CreatedAt = {CreatedAt:O} }}";
    }
}
