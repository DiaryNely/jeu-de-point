using jeuPoint.Models;
using DomainPoint = jeuPoint.Models.Point;

namespace jeuPoint.Services.Rules;

public sealed class GameActionResult
{
    public bool Success { get; }

    public string Message { get; }

    public int ScoreGained { get; }

    public bool Replay { get; }

    public Move? Move { get; }

    public IReadOnlyCollection<ValidatedLine> ValidatedLines { get; }

    public bool? Hit { get; }

    public DomainPoint? DestroyedPoint { get; }

    public IReadOnlyList<GridCell> Trajectory { get; }

    private GameActionResult(bool success, string message, int scoreGained, bool replay, Move? move, IReadOnlyCollection<ValidatedLine>? validatedLines, bool? hit, DomainPoint? destroyedPoint, IReadOnlyList<GridCell>? trajectory)
    {
        Success = success;
        Message = message;
        ScoreGained = scoreGained;
        Replay = replay;
        Move = move;
        ValidatedLines = validatedLines ?? Array.Empty<ValidatedLine>();
        Hit = hit;
        DestroyedPoint = destroyedPoint;
        Trajectory = trajectory ?? Array.Empty<GridCell>();
    }

    public static GameActionResult Fail(string message)
    {
        return new GameActionResult(false, message, 0, false, null, null, null, null, null);
    }

    public static GameActionResult Ok(string message, int scoreGained, bool replay, Move move, IReadOnlyCollection<ValidatedLine>? validatedLines = null)
    {
        return new GameActionResult(true, message, scoreGained, replay, move, validatedLines, null, null, null);
    }

    public static GameActionResult Shot(string message, int scoreGained, bool replay, Move move, bool hit, DomainPoint? destroyedPoint, IReadOnlyList<GridCell>? trajectory)
    {
        return new GameActionResult(true, message, scoreGained, replay, move, null, hit, destroyedPoint, trajectory);
    }

    public override string ToString()
    {
        return $"GameActionResult {{ Success = {Success}, Message = {Message}, ScoreGained = {ScoreGained}, Replay = {Replay}, Lines = {ValidatedLines.Count} }}";
    }
}
