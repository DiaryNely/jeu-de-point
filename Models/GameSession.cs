namespace jeuPoint.Models;

public sealed class GameSession
{
    public long Id { get; init; }

    public DateTime PlayedAtUtc { get; init; }

    public required string WinnerName { get; init; }

    public int PlayerOneScore { get; init; }

    public int PlayerTwoScore { get; init; }

    public int TargetScore { get; init; }
}
