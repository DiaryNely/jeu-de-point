using jeuPoint.Models;

namespace jeuPoint.Data;

public sealed class SavedGameInfo
{
    public long Id { get; }

    public int Width { get; }

    public int Height { get; }

    public GameStatus Status { get; }

    public DateTimeOffset CreatedAt { get; }

    public SavedGameInfo(long id, int width, int height, GameStatus status, DateTimeOffset createdAt)
    {
        Id = id;
        Width = width;
        Height = height;
        Status = status;
        CreatedAt = createdAt;
    }

    public override string ToString()
    {
        return $"Partie #{Id} — {Width}x{Height} — {Status} — {CreatedAt:yyyy-MM-dd HH:mm}";
    }
}