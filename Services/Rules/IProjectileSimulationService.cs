using DomainPoint = jeuPoint.Models.Point;

namespace jeuPoint.Services.Rules;

public interface IProjectileSimulationService
{
    ProjectileShotResult SimulateShot(
        long shooterPlayerId,
        GridCell origin,
        int power,
        int boardWidth,
        int boardHeight,
        IReadOnlyDictionary<GridCell, DomainPoint> activePointsByCell,
        IReadOnlySet<GridCell> protectedCells);
}
