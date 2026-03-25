using jeuPoint.Services.Rules;

namespace jeuPoint.UI;

public sealed partial class MainForm
{
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (TryHandleCannonArrowKey(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void HandleBoardClick(GridCell cell)
    {
        if (!_isGameStarted || _gameEngine is null)
        {
            RenderState("Veuillez démarrer une partie avant de jouer.");
            return;
        }

        if (_isShotAnimating)
        {
            return;
        }

        GameActionResult result;
        var actionMode = _mode;
        if (_mode == GameInteractionMode.Place)
        {
            result = _gameEngine.PlacePoint(_gameEngine.CurrentPlayer, cell.X, cell.Y);
            if (result.Success && result.Move is not null)
            {
                _boardPoints[cell] = result.Move.PlayerId;

                if (result.ValidatedLines.Count > 0)
                {
                    foreach (var line in result.ValidatedLines)
                    {
                        if (_validatedLines.Any(existing => existing.Id == line.Id))
                        {
                            continue;
                        }

                        _validatedLines.Add(line);
                    }
                }
            }

            _lockedTarget = null;
            _boardControl.LockedTarget = null;
            _boardControl.PredictedImpactCell = null;
            _boardControl.ProjectileShooterPlayerId = null;
            _boardControl.ProjectileTargetCell = null;
            _boardControl.ProjectilePath = Array.Empty<GridCell>();
            _boardControl.ProjectileProgress = 0f;
            _confirmShotButton.Enabled = false;

            RenderState(BuildTurnMessage(result, actionMode));
            return;
        }

        _gameEngine.SetCurrentCannonRow(cell.Y);
        _lockedTarget = null;
        _boardControl.LockedTarget = null;
        _confirmShotButton.Enabled = true;
        _boardControl.Invalidate();
        RenderState($"Ligne de tir sélectionnée : {cell.Y}. Choisissez la puissance (Ctrl+1..9) puis confirmez le tir.");
    }

    private void OnMainFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isGameStarted || _gameEngine is null || _mode != GameInteractionMode.Shoot)
        {
            return;
        }

        if (_isShotAnimating)
        {
            return;
        }

        if (TryHandleCannonArrowKey(e.KeyCode))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (!e.Control)
        {
            return;
        }

        var power = GetPowerFromShortcut(e.KeyCode);
        if (power is null)
        {
            return;
        }

        _powerTrackBar.Value = power.Value;
        RenderState($"Puissance sélectionnée : {power.Value}. Confirmez le tir.");
        e.Handled = true;
    }

    private bool TryHandleCannonArrowKey(Keys keyData)
    {
        if (!_isGameStarted || _gameEngine is null || _mode != GameInteractionMode.Shoot || _isShotAnimating)
        {
            return false;
        }

        var keyCode = keyData & Keys.KeyCode;
        if (keyCode != Keys.Up && keyCode != Keys.Down)
        {
            return false;
        }

        var delta = keyCode == Keys.Up ? -1 : 1;
        var newRow = _gameEngine.MoveCurrentCannon(delta);

        _lockedTarget = null;
        _boardControl.LockedTarget = null;
        _confirmShotButton.Enabled = true;

        RenderState($"Canon déplacé sur la ligne {newRow}. Ajustez la puissance puis confirmez le tir.");
        return true;
    }

    private void ConfirmShot()
    {
        if (!_isGameStarted || _gameEngine is null || _mode != GameInteractionMode.Shoot)
        {
            return;
        }

        if (_isShotAnimating)
        {
            return;
        }

        FireLockedShot();
    }

    private static int? GetPowerFromShortcut(Keys key)
    {
        return key switch
        {
            Keys.D1 or Keys.NumPad1 => 1,
            Keys.D2 or Keys.NumPad2 => 2,
            Keys.D3 or Keys.NumPad3 => 3,
            Keys.D4 or Keys.NumPad4 => 4,
            Keys.D5 or Keys.NumPad5 => 5,
            Keys.D6 or Keys.NumPad6 => 6,
            Keys.D7 or Keys.NumPad7 => 7,
            Keys.D8 or Keys.NumPad8 => 8,
            Keys.D9 or Keys.NumPad9 => 9,
            _ => null
        };
    }
}
