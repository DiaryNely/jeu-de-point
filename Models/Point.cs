namespace jeuPoint.Models;

public class Point
{
    public long Id { get; private set; }

    public long GameId { get; private set; }

    public Game? Game { get; private set; }

    public long PlayerId { get; private set; }

    public Player Player { get; private set; }

    public int X { get; private set; }

    public int Y { get; private set; }

    public bool IsDestroyed { get; private set; }

    public Point(long id, long gameId, Player player, int x, int y, bool isDestroyed = false)
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

        Id = id;
        GameId = gameId;
        Player = player ?? throw new ArgumentNullException(nameof(player));
        PlayerId = player.Id;
        X = x;
        Y = y;
        IsDestroyed = isDestroyed;
    }

    public Point(long gameId, Player player, int x, int y) : this(1, gameId, player, x, y)
    {
    }

    public void Destroy()
    {
        IsDestroyed = true;
    }

    internal void AttachGame(Game game)
    {
        Game = game;
        GameId = game.Id;
    }

    public override string ToString()
    {
        return $"Point {{ Id = {Id}, GameId = {GameId}, PlayerId = {PlayerId}, X = {X}, Y = {Y}, IsDestroyed = {IsDestroyed} }}";
    }
}
