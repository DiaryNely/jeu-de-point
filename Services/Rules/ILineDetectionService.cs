using DomainPoint = jeuPoint.Models.Point;

namespace jeuPoint.Services.Rules;

public interface ILineDetectionService
{
    IReadOnlyList<ValidatedLine> DetectValidLines(
        long gameId,
        long playerId,
        GridCell anchor,
        IReadOnlyDictionary<GridCell, DomainPoint> activePointsByCell,
        IReadOnlyCollection<ValidatedLine> existingLines,
        long nextLineId);
}
