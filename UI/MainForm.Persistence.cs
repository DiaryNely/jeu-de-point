using jeuPoint.Models;
using jeuPoint.Data;
using jeuPoint.Services.Rules;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace jeuPoint.UI;

public sealed partial class MainForm
{
    private async Task SaveCurrentGameAsync()
    {
        if (!_isGameStarted || _gameEngine is null || _game is null)
        {
            _logger.LogWarning("Sauvegarde refusée: partie non démarrée ou moteur de jeu indisponible.");
            RenderState("Démarrez une partie avant de sauvegarder.");
            return;
        }

        try
        {
            await PersistCurrentSnapshotAsync(showUiFeedback: true);
        }
        catch (Exception ex)
        {
            var errorMessage = GetExplicitErrorMessage(ex);
            _logger.LogError(ex, "Erreur pendant la sauvegarde DB. GameId={GameId}", _game.Id);
            RenderState($"Erreur sauvegarde base: {errorMessage}");
            MessageBox.Show(
                this,
                errorMessage,
                "Erreur de sauvegarde base",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task AutoSaveCurrentGameSilentlyAsync()
    {
        if (!_isGameStarted || _gameEngine is null || _game is null || _isShotAnimating)
        {
            return;
        }

        try
        {
            await PersistCurrentSnapshotAsync(showUiFeedback: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Autosauvegarde ignorée suite à une erreur transitoire.");
        }
    }

    private async Task PersistCurrentSnapshotAsync(bool showUiFeedback)
    {
        if (_gameEngine is null)
        {
            throw new InvalidOperationException("Game engine is not initialized.");
        }

        var snapshot = BuildPersistableGame();
        _logger.LogInformation(
            "Sauvegarde DB. GameId={GameId}, Grid={Width}x{Height}, Moves={MoveCount}, Points={PointCount}, Lines={LineCount}, Mode={Mode}",
            snapshot.Id,
            snapshot.Width,
            snapshot.Height,
            snapshot.Moves.Count,
            snapshot.Points.Count,
            snapshot.Lines.Count,
            showUiFeedback ? "manuel" : "auto");

        await _gameRepository.SaveGameAsync(snapshot, [_playerOne, _playerTwo], _gameEngine.GetOwnershipHistoryClaims());

        if (showUiFeedback)
        {
            RenderState($"Partie sauvegardée en base (id={snapshot.Id}).");
        }
    }

    private async Task LoadSavedGameAsync()
    {
        if (_isShotAnimating)
        {
            return;
        }

        try
        {
            var games = await _gameRepository.ListGamesAsync();
            if (games.Count == 0)
            {
                _logger.LogWarning("Chargement DB: aucune partie disponible.");
                RenderState("Aucune partie sauvegardée disponible.");
                return;
            }

            var gameId = PromptGameIdSelection(games);
            if (gameId is null)
            {
                RenderState("Chargement annulé.");
                return;
            }

            _logger.LogInformation("Début chargement DB. GameId={GameId}", gameId);

            var restored = await _gameRepository.LoadGameAsync(gameId.Value);
            if (restored is null)
            {
                _logger.LogWarning("Chargement DB: aucune partie trouvée pour GameId={GameId}", gameId.Value);
                RenderState($"Aucune partie trouvée en base pour id={gameId.Value}.");
                return;
            }

            ApplyLoadedGame(restored);
            _logger.LogInformation(
                "Chargement DB terminé. GameId={GameId}, Moves={MoveCount}, Points={PointCount}, Lines={LineCount}",
                restored.Game.Id,
                restored.Game.Moves.Count,
                restored.Game.Points.Count,
                restored.Game.Lines.Count);

            RenderState($"Partie chargée depuis la base (id={restored.Game.Id}).");
        }
        catch (Exception ex)
        {
            var errorMessage = GetExplicitErrorMessage(ex);
            _logger.LogError(ex, "Erreur pendant le chargement DB.");
            RenderState($"Erreur chargement base: {errorMessage}");
            MessageBox.Show(
                this,
                errorMessage,
                "Erreur de chargement base",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string GetExplicitErrorMessage(Exception exception)
    {
        if (exception is PostgresException postgresException)
        {
            var primary = string.IsNullOrWhiteSpace(postgresException.MessageText)
                ? "Erreur PostgreSQL"
                : postgresException.MessageText;

            var sqlState = string.IsNullOrWhiteSpace(postgresException.SqlState)
                ? "N/A"
                : postgresException.SqlState;

            var constraint = string.IsNullOrWhiteSpace(postgresException.ConstraintName)
                ? "N/A"
                : postgresException.ConstraintName;

            var detail = string.IsNullOrWhiteSpace(postgresException.Detail)
                ? "Aucun détail"
                : postgresException.Detail;

            return $"{primary} (SQLSTATE={sqlState}, contrainte={constraint}, détail={detail})";
        }

        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message) && exception.InnerException is not null)
        {
            message = exception.InnerException.Message;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Erreur inconnue sans message.";
        }

        return $"{exception.GetType().Name}: {message}";
    }

    private long? PromptGameIdSelection(IReadOnlyList<SavedGameInfo> games)
    {
        using var picker = new Form
        {
            Text = "Choisir une partie",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(620, 420)
        };

        var instructions = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "Sélectionnez la partie à charger :",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };

        var listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            Font = new Font("Segoe UI", 9.5f),
            DataSource = games.ToList()
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };

        var cancelButton = new Button
        {
            Text = "Annuler",
            DialogResult = DialogResult.Cancel,
            Width = 110,
            Height = 32
        };

        var loadButton = new Button
        {
            Text = "Charger",
            DialogResult = DialogResult.OK,
            Width = 110,
            Height = 32
        };

        listBox.DoubleClick += (_, _) =>
        {
            if (listBox.SelectedItem is SavedGameInfo)
            {
                picker.DialogResult = DialogResult.OK;
                picker.Close();
            }
        };

        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(loadButton);

        picker.Controls.Add(listBox);
        picker.Controls.Add(buttons);
        picker.Controls.Add(instructions);
        picker.AcceptButton = loadButton;
        picker.CancelButton = cancelButton;

        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return null;
        }

        return (listBox.SelectedItem as SavedGameInfo)?.Id;
    }

    private Game BuildPersistableGame()
    {
        if (_gameEngine is null || _game is null)
        {
            throw new InvalidOperationException("Game is not initialized.");
        }

        var snapshot = new Game(_game.Id, _boardControl.GridWidth, _boardControl.GridHeight, GameStatus.InProgress, _game.CreatedAt);
        snapshot.SetCurrentPlayer(_gameEngine.CurrentPlayer);

        var moveSequence = 1;
        foreach (var move in _gameEngine.Moves.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id))
        {
            var player = move.PlayerId == _playerOne.Id ? _playerOne : _playerTwo;
            snapshot.AddMove(ComposePersistentEntityId(_game.Id, moveSequence++), player, move.X, move.Y, move.Type, move.Power, move.CreatedAt);
        }

        var pointSequence = 1;
        foreach (var point in _boardPoints.OrderBy(p => p.Key.X).ThenBy(p => p.Key.Y))
        {
            var player = point.Value == _playerOne.Id ? _playerOne : _playerTwo;
            snapshot.AddPoint(ComposePersistentEntityId(_game.Id, pointSequence++), player, point.Key.X, point.Key.Y, isDestroyed: false);
        }

        var lineSequence = 1;
        foreach (var line in _validatedLines.OrderBy(l => l.Id))
        {
            var player = line.PlayerId == _playerOne.Id ? _playerOne : _playerTwo;
            snapshot.AddLine(ComposePersistentEntityId(_game.Id, lineSequence++), player, line.PointCount, isValidated: true, validatedAt: DateTimeOffset.UtcNow);
        }

        return snapshot;
    }

    private static long ComposePersistentEntityId(long gameId, int sequence)
    {
        const long sequenceMultiplier = 1_000_000;

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        var maxSequence = sequenceMultiplier - 1;
        if (sequence > maxSequence)
        {
            throw new InvalidOperationException($"Sequence {sequence} exceeds maximum supported value {maxSequence}.");
        }

        checked
        {
            return (gameId * sequenceMultiplier) + sequence;
        }
    }

    private void ApplyLoadedGame(GameRestoreResult restored)
    {
        var game = restored.Game;

        _playerOne.UpdateScore(restored.ScoresByPlayer.TryGetValue(_playerOne.Id, out var p1) ? p1 : 0);
        _playerTwo.UpdateScore(restored.ScoresByPlayer.TryGetValue(_playerTwo.Id, out var p2) ? p2 : 0);

        _game = new Game(game.Id, game.Width, game.Height, game.Status, game.CreatedAt);
        _gameEngine = new GameEngine(_game, _playerOne, _playerTwo);

        _boardPoints.Clear();
        _validatedLines.Clear();

        _boardControl.GridWidth = _game.Width;
        _boardControl.GridHeight = _game.Height;
        _gridWidthInput.Value = Math.Clamp(_game.Width, (int)_gridWidthInput.Minimum, (int)_gridWidthInput.Maximum);
        _gridHeightInput.Value = Math.Clamp(_game.Height, (int)_gridHeightInput.Minimum, (int)_gridHeightInput.Maximum);

        foreach (var move in game.Moves.OrderBy(m => m.Id))
        {
            var player = move.PlayerId == _playerOne.Id ? _playerOne : _playerTwo;

            if (move.Type == MoveType.Place)
            {
                var result = _gameEngine.PlacePoint(player, move.X, move.Y);
                if (!result.Success)
                {
                    continue;
                }

                if (result.Move is not null)
                {
                    _boardPoints[new GridCell(result.Move.X, result.Move.Y)] = result.Move.PlayerId;
                }

                foreach (var line in result.ValidatedLines)
                {
                    if (_validatedLines.All(existing => existing.Id != line.Id))
                    {
                        _validatedLines.Add(line);
                    }
                }
            }
            else if (move.Type == MoveType.Shoot)
            {
                var result = _gameEngine.Shoot(player, move.Y, Math.Max(1, move.Power));
                if (result.Success && result.Hit == true && result.DestroyedPoint is not null)
                {
                    _boardPoints.Remove(new GridCell(result.DestroyedPoint.X, result.DestroyedPoint.Y));
                }
            }
        }

        _gameEngine.ImportOwnershipHistoryClaims(restored.OwnershipClaims);
        SyncBoardFromEngine();

        _isGameStarted = true;
        _mode = GameInteractionMode.Place;
        _lockedTarget = null;

        _boardControl.Mode = _mode;
        _boardControl.LockedTarget = null;
        _boardControl.PredictedImpactCell = null;
        _boardControl.ProjectileShooterPlayerId = null;
        _boardControl.ProjectileTargetCell = null;
        _boardControl.ProjectilePath = Array.Empty<GridCell>();
        _boardControl.ProjectileProgress = 0f;
        _boardControl.PlayerOneCannonRow = _gameEngine.GetCannonRow(_playerOne.Id);
        _boardControl.PlayerTwoCannonRow = _gameEngine.GetCannonRow(_playerTwo.Id);

        _modeButton.Text = "Mode : Pose";
        _modeButton.Enabled = true;
        _powerTrackBar.Enabled = true;
        _confirmShotButton.Enabled = false;
        _saveGameButton.Enabled = true;
        _startGameButton.Text = "Redémarrer partie";
    }
}
