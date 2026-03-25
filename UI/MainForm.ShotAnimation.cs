using jeuPoint.Services.Rules;

namespace jeuPoint.UI;

public sealed partial class MainForm
{
    private void FireLockedShot()
    {
        if (_gameEngine is null)
        {
            return;
        }

        var shooterId = _gameEngine.CurrentPlayer.Id;
        var lineY = _gameEngine.GetCurrentCannonRow();

        var shotResult = _gameEngine.Shoot(_gameEngine.CurrentPlayer, lineY, _powerTrackBar.Value);
        if (!shotResult.Success)
        {
            RenderState(shotResult.Message);
            return;
        }

        if (shotResult.Trajectory.Count == 0)
        {
            RenderState(BuildTurnMessage(shotResult, GameInteractionMode.Shoot));
            return;
        }

        var animationTarget = shotResult.Trajectory.Count > 0
            ? shotResult.Trajectory[^1]
            : new GridCell(shooterId == _playerOne.Id ? 0 : _boardControl.GridWidth - 1, lineY);

        _pendingShotResult = shotResult;
        _shotAnimationShooterId = shooterId;
        _shotAnimationTarget = animationTarget;
        _shotAnimationStep = 0;
        _shotAnimationStepsTotal = 16;

        _boardControl.ProjectileShooterPlayerId = _shotAnimationShooterId;
        _boardControl.ProjectileTargetCell = _shotAnimationTarget;
        _boardControl.ProjectilePath = shotResult.Trajectory.ToArray();
        _boardControl.PredictedImpactCell = null;
        _boardControl.ProjectileProgress = 0f;

        _isShotAnimating = true;
        _modeButton.Enabled = false;
        _powerTrackBar.Enabled = false;
        _confirmShotButton.Enabled = false;
        _shotAnimationTimer.Start();
    }

    private void OnShotAnimationTick(object? sender, EventArgs e)
    {
        if (_shotAnimationStepsTotal <= 0)
        {
            CompleteShotAnimation();
            return;
        }

        _shotAnimationStep++;
        _boardControl.ProjectileProgress = Math.Clamp(_shotAnimationStep / (float)_shotAnimationStepsTotal, 0f, 1f);
        _boardControl.Invalidate();

        if (_shotAnimationStep >= _shotAnimationStepsTotal)
        {
            CompleteShotAnimation();
        }
    }

    private void CompleteShotAnimation()
    {
        _shotAnimationTimer.Stop();

        if (_pendingShotResult is not null && _pendingShotResult.Hit == true && _pendingShotResult.DestroyedPoint is not null)
        {
            _boardPoints.Remove(new GridCell(_pendingShotResult.DestroyedPoint.X, _pendingShotResult.DestroyedPoint.Y));
        }

        var message = _pendingShotResult is null
            ? "Tir terminé."
            : BuildTurnMessage(_pendingShotResult, GameInteractionMode.Shoot);

        _pendingShotResult = null;
        _shotAnimationStep = 0;
        _shotAnimationStepsTotal = 0;
        _shotAnimationTarget = null;
        _isShotAnimating = false;

        _lockedTarget = null;
        _boardControl.LockedTarget = null;
        _boardControl.ProjectileShooterPlayerId = null;
        _boardControl.ProjectileTargetCell = null;
        _boardControl.ProjectilePath = Array.Empty<GridCell>();
        _boardControl.PredictedImpactCell = null;
        _boardControl.ProjectileProgress = 0f;

        _modeButton.Enabled = true;
        _powerTrackBar.Enabled = true;
        _confirmShotButton.Enabled = _mode == GameInteractionMode.Shoot;

        RenderState(message);
    }
}
