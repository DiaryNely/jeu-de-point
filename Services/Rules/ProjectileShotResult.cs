using DomainPoint = jeuPoint.Models.Point;

namespace jeuPoint.Services.Rules;

public sealed class ProjectileShotResult
{
    public bool Hit { get; }

    public DomainPoint? DestroyedPoint { get; }

    public GridCell? DestroyedCell { get; }

    public IReadOnlyList<GridCell> Trajectory { get; }

    public string Message { get; }

    private ProjectileShotResult(bool hit, DomainPoint? destroyedPoint, GridCell? destroyedCell, IReadOnlyList<GridCell> trajectory, string message)
    {
        Hit = hit;
        DestroyedPoint = destroyedPoint;
        DestroyedCell = destroyedCell;
        Trajectory = trajectory;
        Message = message;
    }

    public static ProjectileShotResult Miss(IReadOnlyList<GridCell> trajectory, string message = "Miss")
    {
        return new ProjectileShotResult(false, null, null, trajectory, message);
    }

    public static ProjectileShotResult HitTarget(DomainPoint destroyedPoint, GridCell destroyedCell, IReadOnlyList<GridCell> trajectory)
    {
        return new ProjectileShotResult(true, destroyedPoint, destroyedCell, trajectory, "Hit");
    }

    public override string ToString()
    {
        return $"ProjectileShotResult {{ Hit = {Hit}, DestroyedCell = {DestroyedCell}, TrajectoryCount = {Trajectory.Count}, Message = {Message} }}";
    }
}
