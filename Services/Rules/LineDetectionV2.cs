using System.Drawing;

namespace jeuPoint.Services.Rules.LineDetectionV2;

public enum LineDirection
{
    Horizontal = 1,
    Vertical = 2,
    DiagonalDown = 3,
    DiagonalUp = 4
}

public sealed class Line
{
    private readonly HashSet<Point> _pointsSet;

    public LineDirection Direction { get; }

    public IReadOnlyList<Point> Points { get; }

    public int Length => Points.Count;

    public Line(LineDirection direction, IEnumerable<Point> points)
    {
        if (points is null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        var ordered = points
            .Distinct()
            .OrderBy(p => p.X)
            .ThenBy(p => p.Y)
            .ToArray();

        if (ordered.Length < 5)
        {
            throw new ArgumentException("Une ligne doit contenir au moins 5 points.", nameof(points));
        }

        Direction = direction;
        Points = ordered;
        _pointsSet = ordered.ToHashSet();
    }

    public int CountSharedPoints(Line other)
    {
        if (other is null)
        {
            return 0;
        }

        var count = 0;
        foreach (var point in other.Points)
        {
            if (_pointsSet.Contains(point))
            {
                count++;
            }
        }

        return count;
    }

    public string BuildSignature()
    {
        return $"{Direction}|{string.Join(';', Points.Select(p => $"{p.X}:{p.Y}"))}";
    }
}

public sealed class LineDetectionService
{
    private static readonly (LineDirection Direction, int StepX, int StepY)[] Directions =
    [
        (LineDirection.Horizontal, 1, 0),
        (LineDirection.Vertical, 0, 1),
        (LineDirection.DiagonalDown, 1, 1),
        (LineDirection.DiagonalUp, 1, -1)
    ];

    public List<Line> DetectNewLines(List<Point> allPoints, List<Line> existingLines, Point lastPlacedPoint)
    {
        if (allPoints is null)
        {
            throw new ArgumentNullException(nameof(allPoints));
        }

        if (existingLines is null)
        {
            throw new ArgumentNullException(nameof(existingLines));
        }

        var pointsSet = allPoints.ToHashSet();
        if (!pointsSet.Contains(lastPlacedPoint))
        {
            return new List<Line>();
        }

        var existingSignatures = existingLines
            .Select(line => line.BuildSignature())
            .ToHashSet(StringComparer.Ordinal);

        var newLines = new List<Line>(capacity: 4);
        var newLineSignatures = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (direction, stepX, stepY) in Directions)
        {
            var consecutive = BuildConsecutiveLine(lastPlacedPoint, pointsSet, stepX, stepY);
            if (consecutive.Count < 5)
            {
                continue;
            }

            var candidate = new Line(direction, consecutive);
            var signature = candidate.BuildSignature();

            if (existingSignatures.Contains(signature) || !newLineSignatures.Add(signature))
            {
                continue;
            }

            if (!IsIntersectionRuleRespected(candidate, existingLines, newLines))
            {
                continue;
            }

            newLines.Add(candidate);
        }

        return newLines;
    }

    private static List<Point> BuildConsecutiveLine(Point anchor, HashSet<Point> points, int stepX, int stepY)
    {
        var start = anchor;

        while (points.Contains(new Point(start.X - stepX, start.Y - stepY)))
        {
            start = new Point(start.X - stepX, start.Y - stepY);
        }

        var result = new List<Point>(capacity: 8);
        var cursor = start;

        while (points.Contains(cursor))
        {
            result.Add(cursor);
            cursor = new Point(cursor.X + stepX, cursor.Y + stepY);
        }

        return result;
    }

    private static bool IsIntersectionRuleRespected(Line candidate, IReadOnlyCollection<Line> existingLines, IReadOnlyCollection<Line> acceptedNewLines)
    {
        foreach (var existing in existingLines)
        {
            if (candidate.CountSharedPoints(existing) >= 2)
            {
                return false;
            }
        }

        foreach (var accepted in acceptedNewLines)
        {
            if (candidate.CountSharedPoints(accepted) >= 2)
            {
                return false;
            }
        }

        return true;
    }
}
