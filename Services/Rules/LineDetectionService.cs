using DomainPoint = jeuPoint.Models.Point;

namespace jeuPoint.Services.Rules;

public sealed class LineDetectionService : ILineDetectionService
{
    private static readonly DirectionStep[] DirectionSteps =
    [
        new(LineDirection.Horizontal, 1, 0),
        new(LineDirection.Vertical, 0, 1),
        new(LineDirection.DiagonalDown, 1, 1),
        new(LineDirection.DiagonalUp, 1, -1)
    ];

    public IReadOnlyList<ValidatedLine> DetectValidLines(
        long gameId,
        long playerId,
        GridCell anchor,
        IReadOnlyDictionary<GridCell, DomainPoint> activePointsByCell,
        IReadOnlyCollection<ValidatedLine> existingLines,
        long nextLineId)
    {
        var ownActiveCells = BuildOwnActiveCells(playerId, activePointsByCell);
        if (!ownActiveCells.Contains(anchor))
        {
            return Array.Empty<ValidatedLine>();
        }

        var existingCache = existingLines
            .Select(line => new ExistingLineCache(line, line.Cells as HashSet<GridCell> ?? line.Cells.ToHashSet()))
            .ToArray();

        var result = new List<ValidatedLine>(capacity: 2);
        var candidateSignatures = new HashSet<string>(StringComparer.Ordinal);

        foreach (var step in DirectionSteps)
        {
            var directionCells = BuildConsecutiveLine(anchor, ownActiveCells, step.StepX, step.StepY);
            if (directionCells.Count < 5)
            {
                continue;
            }

            var bestCandidateCells = SelectBestCandidateCells(
                playerId,
                step.Direction,
                anchor,
                directionCells,
                existingCache,
                result,
                candidateSignatures);

            if (bestCandidateCells is null)
            {
                continue;
            }

            var candidateLine = new ValidatedLine(nextLineId++, gameId, playerId, step.Direction, bestCandidateCells);
            if (!RespectsOpponentIntersectionRule(candidateLine, existingCache))
            {
                continue;
            }

            result.Add(candidateLine);
        }

        return result;
    }

    private static IReadOnlyCollection<GridCell>? SelectBestCandidateCells(
        long playerId,
        LineDirection direction,
        GridCell anchor,
        IReadOnlyList<GridCell> directionCells,
        IReadOnlyCollection<ExistingLineCache> existingCache,
        IReadOnlyCollection<ValidatedLine> newLines,
        HashSet<string> candidateSignatures)
    {
        var anchorIndex = -1;
        for (var i = 0; i < directionCells.Count; i++)
        {
            if (directionCells[i] == anchor)
            {
                anchorIndex = i;
                break;
            }
        }

        if (anchorIndex < 0)
        {
            return null;
        }

        var orderedCandidates = BuildCandidateSegments(directionCells, anchorIndex)
            .OrderBy(cells => cells[0].X)
            .ThenBy(cells => cells[0].Y);

        foreach (var candidateCells in orderedCandidates)
        {
            var signature = BuildSignature(playerId, direction, candidateCells);
            if (!candidateSignatures.Add(signature))
            {
                continue;
            }

            if (existingCache.Any(x => x.Line.PlayerId == playerId && x.Line.Direction == direction && BuildSignature(x.Line.PlayerId, x.Line.Direction, x.Line.Cells) == signature))
            {
                continue;
            }

            var candidateSet = candidateCells.ToHashSet();
            if (!RespectsSharedPointsRule(playerId, candidateSet, direction, existingCache, newLines))
            {
                continue;
            }

            return candidateCells;
        }

        return null;
    }

    private static IEnumerable<List<GridCell>> BuildCandidateSegments(IReadOnlyList<GridCell> cells, int anchorIndex)
    {
        const int lineLength = 5;

        for (var start = 0; start <= anchorIndex; start++)
        {
            for (var end = anchorIndex; end < cells.Count; end++)
            {
                var length = end - start + 1;
                if (length != lineLength)
                {
                    continue;
                }

                var segment = new List<GridCell>(length);
                for (var i = start; i <= end; i++)
                {
                    segment.Add(cells[i]);
                }

                yield return segment;
            }
        }
    }

    private static HashSet<GridCell> BuildOwnActiveCells(long playerId, IReadOnlyDictionary<GridCell, DomainPoint> activePointsByCell)
    {
        var cells = new HashSet<GridCell>();
        foreach (var (cell, point) in activePointsByCell)
        {
            if (!point.IsDestroyed && point.PlayerId == playerId)
            {
                cells.Add(cell);
            }
        }

        return cells;
    }

    private static List<GridCell> BuildConsecutiveLine(GridCell anchor, HashSet<GridCell> ownCells, int stepX, int stepY)
    {
        var start = anchor;
        while (ownCells.Contains(new GridCell(start.X - stepX, start.Y - stepY)))
        {
            start = new GridCell(start.X - stepX, start.Y - stepY);
        }

        var cells = new List<GridCell>(capacity: 8);
        var cursor = start;
        while (ownCells.Contains(cursor))
        {
            cells.Add(cursor);
            cursor = new GridCell(cursor.X + stepX, cursor.Y + stepY);
        }

        return cells;
    }

    private static bool RespectsSharedPointsRule(
        long candidatePlayerId,
        HashSet<GridCell> candidate,
        LineDirection candidateDirection,
        IReadOnlyCollection<ExistingLineCache> existingLines,
        IReadOnlyCollection<ValidatedLine> newLines)
    {
        foreach (var existing in existingLines)
        {
            var maxAllowedOverlap = GetMaxAllowedOverlap(candidatePlayerId, candidate, candidateDirection, existing.Line.PlayerId, existing.Line.Direction, existing.Cells);
            var overlap = 0;
            foreach (var cell in candidate)
            {
                if (!existing.Cells.Contains(cell))
                {
                    continue;
                }

                overlap++;
                if (overlap > maxAllowedOverlap)
                {
                    return false;
                }
            }
        }

        foreach (var line in newLines)
        {
            var lineCells = line.Cells as HashSet<GridCell> ?? line.Cells.ToHashSet();
            var maxAllowedOverlap = GetMaxAllowedOverlap(candidatePlayerId, candidate, candidateDirection, line.PlayerId, line.Direction, lineCells);
            var overlap = 0;
            foreach (var cell in candidate)
            {
                if (!line.Cells.Contains(cell))
                {
                    continue;
                }

                overlap++;
                if (overlap > maxAllowedOverlap)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int GetMaxAllowedOverlap(
        long candidatePlayerId,
        HashSet<GridCell> candidateCells,
        LineDirection candidateDirection,
        long existingPlayerId,
        LineDirection existingDirection,
        HashSet<GridCell> existingCells)
    {
        if (candidatePlayerId != existingPlayerId)
        {
            return 0;
        }

        if (candidateDirection != existingDirection)
        {
            return 1;
        }

        if (AreColinear(candidateDirection, candidateCells, existingCells))
        {
            return 0;
        }

        return 1;
    }

    private static bool AreColinear(LineDirection direction, HashSet<GridCell> first, HashSet<GridCell> second)
    {
        var a = first.First();
        var b = second.First();

        return direction switch
        {
            LineDirection.Horizontal => a.Y == b.Y,
            LineDirection.Vertical => a.X == b.X,
            LineDirection.DiagonalDown => (a.Y - a.X) == (b.Y - b.X),
            LineDirection.DiagonalUp => (a.Y + a.X) == (b.Y + b.X),
            _ => false
        };
    }

    private static bool RespectsOpponentIntersectionRule(ValidatedLine candidate, IReadOnlyCollection<ExistingLineCache> existingLines)
    {
        var (candidateStart, candidateEnd) = GetLineEndpoints(candidate);

        foreach (var existing in existingLines)
        {
            if (existing.Line.PlayerId == candidate.PlayerId)
            {
                continue;
            }

            var (existingStart, existingEnd) = GetLineEndpoints(existing.Line);
            if (SegmentsIntersect(candidateStart, candidateEnd, existingStart, existingEnd))
            {
                return false;
            }
        }

        return true;
    }

    private static (GridCell Start, GridCell End) GetLineEndpoints(ValidatedLine line)
    {
        var ordered = line.Direction switch
        {
            LineDirection.Horizontal => line.Cells.OrderBy(c => c.X).ThenBy(c => c.Y).ToArray(),
            LineDirection.Vertical => line.Cells.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray(),
            LineDirection.DiagonalDown => line.Cells.OrderBy(c => c.X).ThenBy(c => c.Y).ToArray(),
            LineDirection.DiagonalUp => line.Cells.OrderBy(c => c.X).ThenByDescending(c => c.Y).ToArray(),
            _ => line.Cells.OrderBy(c => c.X).ThenBy(c => c.Y).ToArray()
        };

        if (ordered.Length == 0)
        {
            return (new GridCell(0, 0), new GridCell(0, 0));
        }

        return (ordered[0], ordered[^1]);
    }

    private static bool SegmentsIntersect(GridCell p1, GridCell q1, GridCell p2, GridCell q2)
    {
        static int Orientation(GridCell a, GridCell b, GridCell c)
        {
            var value = (long)(b.Y - a.Y) * (c.X - b.X) - (long)(b.X - a.X) * (c.Y - b.Y);
            if (value == 0)
            {
                return 0;
            }

            return value > 0 ? 1 : 2;
        }

        static bool OnSegment(GridCell a, GridCell b, GridCell c)
        {
            return b.X <= Math.Max(a.X, c.X)
                && b.X >= Math.Min(a.X, c.X)
                && b.Y <= Math.Max(a.Y, c.Y)
                && b.Y >= Math.Min(a.Y, c.Y);
        }

        var o1 = Orientation(p1, q1, p2);
        var o2 = Orientation(p1, q1, q2);
        var o3 = Orientation(p2, q2, p1);
        var o4 = Orientation(p2, q2, q1);

        if (o1 != o2 && o3 != o4)
        {
            return true;
        }

        if (o1 == 0 && OnSegment(p1, p2, q1)) return true;
        if (o2 == 0 && OnSegment(p1, q2, q1)) return true;
        if (o3 == 0 && OnSegment(p2, p1, q2)) return true;
        if (o4 == 0 && OnSegment(p2, q1, q2)) return true;

        return false;
    }

    private static string BuildSignature(long playerId, LineDirection direction, IReadOnlyCollection<GridCell> cells)
    {
        var ordered = cells.OrderBy(c => c.X).ThenBy(c => c.Y);
        return $"{playerId}|{direction}|{string.Join(';', ordered.Select(c => $"{c.X}:{c.Y}"))}";
    }

    private sealed record DirectionStep(LineDirection Direction, int StepX, int StepY);

    private sealed record ExistingLineCache(ValidatedLine Line, HashSet<GridCell> Cells);
}
