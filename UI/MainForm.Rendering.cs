using jeuPoint.Services.Rules;

namespace jeuPoint.UI;

public sealed partial class MainForm
{
    private void SyncBoardFromEngine()
    {
        _boardPoints.Clear();
        _validatedLines.Clear();

        if (_gameEngine is null)
        {
            return;
        }

        foreach (var (cell, playerId) in _gameEngine.GetActivePointsOwnership())
        {
            _boardPoints[cell] = playerId;
        }

        foreach (var line in _gameEngine.ValidatedLines)
        {
            _validatedLines.Add(line);
        }
    }

    private string BuildTurnIndicatorText()
    {
        if (!_isGameStarted || _gameEngine is null)
        {
            return "Tour : - | Action : démarrez une partie";
        }

        var playerName = _gameEngine.CurrentPlayer.Name;
        var actionText = _isShotAnimating
            ? "animation du tir en cours"
            : _mode switch
            {
                GameInteractionMode.Place => "poser un point",
                GameInteractionMode.Shoot => $"choisir ligne + puissance {_powerTrackBar.Value}, puis tirer",
                _ => "jouer"
            };

        return $"Tour : {playerName} | Action : {actionText}";
    }

    private string BuildTurnMessage(GameActionResult result, GameInteractionMode actionMode)
    {
        if (_gameEngine is null)
        {
            return result.Message;
        }

        if (!result.Success)
        {
            return result.Message;
        }

        if (actionMode == GameInteractionMode.Shoot)
        {
            return result.Hit == true
                ? $"{result.Message} — tour changé : {_gameEngine.CurrentPlayer.Name}."
                : $"{result.Message} — tour changé : {_gameEngine.CurrentPlayer.Name}.";
        }

        if (result.Replay)
        {
            return $"{result.Message} — ligne créée, {_gameEngine.CurrentPlayer.Name} rejoue.";
        }

        return $"{result.Message} — tour changé : {_gameEngine.CurrentPlayer.Name}.";
    }

    private void RenderState(string message)
    {
        _currentPlayerLabel.Text = BuildTurnIndicatorText();
        _currentPlayerLabel.ForeColor = _gameEngine?.CurrentPlayer.Id == _playerOne.Id
            ? Color.FromArgb(13, 110, 253)
            : Color.FromArgb(220, 53, 69);

        _scoreLabel.Text = $"Score  J1: {_playerOne.Score}   |   J2: {_playerTwo.Score}";
        _statusLabel.Text = message;

        _boardControl.PointsByCell = new Dictionary<GridCell, long>(_boardPoints);
        _boardControl.ValidatedLines = _validatedLines.ToArray();
        _boardControl.CurrentPlayerId = _gameEngine?.CurrentPlayer.Id ?? _playerOne.Id;
        _boardControl.PlayerOneCannonRow = _gameEngine?.GetCannonRow(_playerOne.Id) ?? 0;
        _boardControl.PlayerTwoCannonRow = _gameEngine?.GetCannonRow(_playerTwo.Id) ?? 0;
        _boardControl.PredictedImpactCell = _isGameStarted && _mode == GameInteractionMode.Shoot && !_isShotAnimating && _gameEngine is not null
            ? _gameEngine.PredictCurrentShotLandingCell(_powerTrackBar.Value)
            : null;
        _boardControl.LockedTarget = _lockedTarget;
        _boardControl.Invalidate();
    }
}
