using jeuPoint.Models;
using jeuPoint.Services.Rules;

namespace jeuPoint.UI;

public sealed partial class MainForm
{
    private static long _lastGeneratedGameId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private void StartGame()
    {
        var gridWidth = (int)_gridWidthInput.Value;
        var gridHeight = (int)_gridHeightInput.Value;

        _playerOne.UpdateScore(0);
        _playerTwo.UpdateScore(0);

        _game = new Game(GenerateNextGameId(), gridWidth, gridHeight, GameStatus.Pending, DateTimeOffset.UtcNow);
        _gameEngine = new GameEngine(_game, _playerOne, _playerTwo);

        _boardPoints.Clear();
        _validatedLines.Clear();
        _boardControl.GridWidth = gridWidth;
        _boardControl.GridHeight = gridHeight;

        _isGameStarted = true;
        _mode = GameInteractionMode.Place;
        _lockedTarget = null;
        _boardControl.LockedTarget = null;
        _boardControl.PredictedImpactCell = null;
        _boardControl.ProjectileShooterPlayerId = null;
        _boardControl.ProjectileTargetCell = null;
        _boardControl.ProjectilePath = Array.Empty<GridCell>();
        _boardControl.ProjectileProgress = 0f;
        _boardControl.PlayerOneCannonRow = _gameEngine.GetCannonRow(_playerOne.Id);
        _boardControl.PlayerTwoCannonRow = _gameEngine.GetCannonRow(_playerTwo.Id);
        _boardControl.Mode = _mode;
        _modeButton.Text = "Mode : Pose";
        _modeButton.Enabled = true;
        _powerTrackBar.Enabled = true;
        _confirmShotButton.Enabled = false;
        _saveGameButton.Enabled = true;
        _startGameButton.Text = "Redémarrer partie";

        RenderState($"Partie démarrée ({gridWidth}x{gridHeight}).");
        _ = AutoSaveCurrentGameSilentlyAsync();
    }

    private static long GenerateNextGameId()
    {
        var candidate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (candidate <= _lastGeneratedGameId)
        {
            candidate = _lastGeneratedGameId + 1;
        }

        _lastGeneratedGameId = candidate;
        return candidate;
    }

    private void ToggleMode()
    {
        if (!_isGameStarted)
        {
            return;
        }

        _mode = _mode == GameInteractionMode.Place ? GameInteractionMode.Shoot : GameInteractionMode.Place;
        _modeButton.Text = _mode == GameInteractionMode.Place ? "Mode : Pose" : "Mode : Tir";
        _boardControl.Mode = _mode;
        if (_mode == GameInteractionMode.Place)
        {
            _lockedTarget = null;
            _boardControl.LockedTarget = null;
            _confirmShotButton.Enabled = false;
        }
        else
        {
            _lockedTarget = null;
            _boardControl.LockedTarget = null;
            _confirmShotButton.Enabled = !_isShotAnimating;
        }

        _boardControl.Invalidate();
        RenderState(_statusLabel.Text);
    }
}
