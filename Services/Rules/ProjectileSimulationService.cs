using DomainPoint = jeuPoint.Models.Point;

namespace jeuPoint.Services.Rules;

public sealed class ProjectileSimulationService : IProjectileSimulationService
{
    public ProjectileShotResult SimulateShot(
        long shooterPlayerId,
        GridCell origin,
        int power,
        int boardWidth,
        int boardHeight,
        IReadOnlyDictionary<GridCell, DomainPoint> activePointsByCell,
        IReadOnlySet<GridCell> protectedCells)
    {
        if (power <= 0)
        {
            return ProjectileShotResult.Miss(Array.Empty<GridCell>(), "Invalid power.");
        }

        var maxReach = Math.Max(1, boardWidth);
        var reach = ComputeReachFromPower(power, maxReach);
        if (reach <= 0)
        {
            return ProjectileShotResult.Miss(Array.Empty<GridCell>(), "Puissance insuffisante.");
        }

        var landingCell = ComputeLandingCell(origin, shooterPlayerId, reach, boardWidth);

        var trajectory = BuildParabolicTrajectory(origin, landingCell, boardWidth, boardHeight);
        if (trajectory.Count == 0)
        {
            return ProjectileShotResult.Miss(trajectory, "Puissance insuffisante.");
        }

        if (!activePointsByCell.TryGetValue(landingCell, out var landingPoint) || landingPoint.IsDestroyed)
        {
            return ProjectileShotResult.Miss(trajectory, "Aucun impact.");
        }

        if (protectedCells.Contains(landingCell))
        {
            return ProjectileShotResult.Miss(trajectory, "Point protégé par une ligne validée.");
        }

        if (landingPoint.PlayerId == shooterPlayerId)
        {
            return ProjectileShotResult.Miss(trajectory, "Point allié touché : aucun effet.");
        }

        return ProjectileShotResult.HitTarget(landingPoint, landingCell, trajectory);
    }

    private static int ComputeReachFromPower(int power, int maxReach)
    {
        if (power <= 0 || maxReach <= 0)
        {
            return 0;
        }

        var normalizedPower = Math.Clamp(power, 1, 9);

        if (maxReach == 1)
        {
            return 1;
        }

        var scaled = ((normalizedPower - 1) * (maxReach - 1)) / 8.0;
        return 1 + (int)scaled;
    }

    private static GridCell ComputeLandingCell(GridCell origin, long shooterPlayerId, int reach, int boardWidth)
    {
        var direction = shooterPlayerId == 1 ? 1 : -1;
        var landingX = origin.X + (direction * reach);
        landingX = Math.Clamp(landingX, 0, boardWidth - 1);
        return new GridCell(landingX, origin.Y);
    }

    private static List<GridCell> BuildParabolicTrajectory(GridCell origin, GridCell landingCell, int boardWidth, int boardHeight)
    {
        if (landingCell.X < 0 || landingCell.X >= boardWidth)
        {
            return new List<GridCell>();
        }

        if (origin.Y < 0 || origin.Y >= boardHeight)
        {
            return new List<GridCell>();
        }

        var landingX = landingCell.X;

        var dx = Math.Abs(landingX - origin.X);
        if (dx == 0)
        {
            return [landingCell];
        }

        var startX = origin.X;
        var startY = origin.Y;
        var endX = landingX;
        var endY = origin.Y;

        var controlX = (startX + endX) / 2.0;
        var arcAmplitude = Math.Max(1.0, dx * 0.35);

        var upRoom = origin.Y;
        var downRoom = (boardHeight - 1) - origin.Y;

        double controlY;
        if (upRoom >= downRoom && upRoom > 0)
        {
            controlY = origin.Y - Math.Min(arcAmplitude, upRoom);
        }
        else if (downRoom > 0)
        {
            controlY = origin.Y + Math.Min(arcAmplitude, downRoom);
        }
        else
        {
            controlY = origin.Y;
        }

        var sampleCount = Math.Max(12, dx * 6);
        var trajectory = new List<GridCell>(sampleCount);
        var visited = new HashSet<GridCell>();

        for (var i = 1; i <= sampleCount; i++)
        {
            var t = i / (double)sampleCount;
            var oneMinusT = 1.0 - t;

            var x = (oneMinusT * oneMinusT * startX)
                + (2.0 * oneMinusT * t * controlX)
                + (t * t * endX);
            var y = (oneMinusT * oneMinusT * startY)
                + (2.0 * oneMinusT * t * controlY)
                + (t * t * endY);

            var gridX = (int)Math.Round(x, MidpointRounding.AwayFromZero);
            var gridY = (int)Math.Round(y, MidpointRounding.AwayFromZero);

            if (gridX < 0 || gridX >= boardWidth || gridY < 0 || gridY >= boardHeight)
            {
                continue;
            }

            var cell = new GridCell(gridX, gridY);
            if (visited.Add(cell))
            {
                trajectory.Add(cell);
            }
        }

        if (trajectory.Count == 0 || trajectory[^1] != landingCell)
        {
            if (landingCell.X >= 0 && landingCell.X < boardWidth && landingCell.Y >= 0 && landingCell.Y < boardHeight)
            {
                trajectory.Add(landingCell);
            }
        }

        return trajectory;
    }

}
