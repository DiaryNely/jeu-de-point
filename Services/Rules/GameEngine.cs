using jeuPoint.Models;
using DomainPoint = jeuPoint.Models.Point;

namespace jeuPoint.Services.Rules;

public sealed class GameEngine
{
    private readonly Game _game;
    private readonly ILineDetectionService _lineDetectionService;
    private readonly IProjectileSimulationService _projectileSimulationService;
    private readonly Player[] _turnOrder;
    private int _turnIndex;
    private readonly Dictionary<long, int> _cannonRowsByPlayerId = new();

    private readonly Dictionary<GridCell, DomainPoint> _activePointsByCell = new();
    private readonly HashSet<(long PlayerId, GridCell Cell)> _ownershipHistory = new();
    private readonly List<Move> _moves = new();
    private readonly List<ValidatedLine> _validatedLines = new();

    private long _nextMoveId = 1;
    private long _nextPointId = 1;
    private long _nextLineId = 1;

    public GameEngine(
        Game game,
        Player playerOne,
        Player playerTwo,
        ILineDetectionService? lineDetectionService = null,
        IProjectileSimulationService? projectileSimulationService = null)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _lineDetectionService = lineDetectionService ?? new LineDetectionService();
        _projectileSimulationService = projectileSimulationService ?? new ProjectileSimulationService();

        if (playerOne is null)
        {
            throw new ArgumentNullException(nameof(playerOne));
        }

        if (playerTwo is null)
        {
            throw new ArgumentNullException(nameof(playerTwo));
        }

        if (playerOne.Id == playerTwo.Id)
        {
            throw new ArgumentException("Players must be distinct.");
        }

        _turnOrder = [playerOne, playerTwo];
        _turnIndex = 0;

        var defaultRow = Math.Clamp(_game.Height / 2, 0, _game.Height - 1);
        _cannonRowsByPlayerId[playerOne.Id] = defaultRow;
        _cannonRowsByPlayerId[playerTwo.Id] = defaultRow;

        _game.SetCurrentPlayer(CurrentPlayer);
        _game.SetStatus(GameStatus.InProgress);
    }

    public Player CurrentPlayer => _turnOrder[_turnIndex];

    public IReadOnlyCollection<Move> Moves => _moves;

    public IReadOnlyCollection<ValidatedLine> ValidatedLines => _validatedLines;

    public IReadOnlyDictionary<GridCell, long> GetActivePointsOwnership()
    {
        return _activePointsByCell.ToDictionary(entry => entry.Key, entry => entry.Value.PlayerId);
    }

    public IReadOnlyCollection<(long PlayerId, int X, int Y)> GetOwnershipHistoryClaims()
    {
        return _ownershipHistory
            .Select(entry => (entry.PlayerId, entry.Cell.X, entry.Cell.Y))
            .ToArray();
    }

    public void ImportOwnershipHistoryClaims(IEnumerable<(long PlayerId, int X, int Y)> claims)
    {
        foreach (var claim in claims)
        {
            _ownershipHistory.Add((claim.PlayerId, new GridCell(claim.X, claim.Y)));
        }
    }

    public int GetCannonRow(long playerId)
    {
        if (_cannonRowsByPlayerId.TryGetValue(playerId, out var row))
        {
            return row;
        }

        return Math.Clamp(_game.Height / 2, 0, _game.Height - 1);
    }

    public int GetCurrentCannonRow()
    {
        return GetCannonRow(CurrentPlayer.Id);
    }

    public GridCell? PredictCurrentShotLandingCell(int power)
    {
        if (_game.Status != GameStatus.InProgress)
        {
            return null;
        }

        if (power <= 0)
        {
            return null;
        }

        var origin = TryGetCannonOrigin(CurrentPlayer.Id);
        if (origin is null)
        {
            return null;
        }

        var maxReach = Math.Max(1, _game.Width);
        var normalizedPower = Math.Clamp(power, 1, 9);
        var reach = maxReach == 1
            ? 1
            : 1 + (int)(((normalizedPower - 1) * (maxReach - 1)) / 8.0);
        if (reach <= 0)
        {
            return null;
        }

        var direction = CurrentPlayer.Id == _turnOrder[0].Id ? 1 : -1;
        var landingX = origin.Value.X + (direction * reach);
        landingX = Math.Clamp(landingX, 0, _game.Width - 1);

        return new GridCell(landingX, GetCurrentCannonRow());
    }

    public int MoveCurrentCannon(int delta)
    {
        var current = GetCurrentCannonRow();
        var next = Math.Clamp(current + delta, 0, _game.Height - 1);
        _cannonRowsByPlayerId[CurrentPlayer.Id] = next;
        return next;
    }

    public int SetCurrentCannonRow(int row)
    {
        var clamped = Math.Clamp(row, 0, _game.Height - 1);
        _cannonRowsByPlayerId[CurrentPlayer.Id] = clamped;
        return clamped;
    }

    public GameActionResult PlacePoint(Player player, int x, int y)
    {
        var validation = ValidateMove(player, x, y, MoveType.Place, power: 0);
        if (!validation.IsValid)
        {
            return GameActionResult.Fail(validation.Message);
        }

        var move = new Move(_nextMoveId++, _game.Id, player, x, y, MoveType.Place, 0, DateTimeOffset.UtcNow);
        _moves.Add(move);

        var placedCell = new GridCell(x, y);
        var point = new DomainPoint(_nextPointId++, _game.Id, player, x, y, isDestroyed: false);
        _activePointsByCell[placedCell] = point;
        _ownershipHistory.Add((player.Id, placedCell));

        var newLines = DetectAndRegisterLines(player, new GridCell(x, y));
        var scoreGained = CalculateLineScore(newLines);
        if (scoreGained > 0)
        {
            player.AddScore(scoreGained);
        }

        var replay = scoreGained > 0;
        if (!replay)
        {
            SwitchTurn();
        }

        return GameActionResult.Ok("Point placed.", scoreGained, replay, move, newLines);
    }

    public GameActionResult Shoot(Player player, int targetX, int targetY, int power)
    {
        return Shoot(player, targetY, power);
    }

    public GameActionResult Shoot(Player player, int lineY, int power)
    {
        var validation = ValidateShot(player, lineY, power);
        if (!validation.IsValid)
        {
            return GameActionResult.Fail(validation.Message);
        }

        var selectedRow = SetCannonRow(player.Id, lineY);

        var origin = TryGetCannonOrigin(player.Id);
        if (origin is null)
        {
            var missMove = new Move(_nextMoveId++, _game.Id, player, 0, selectedRow, MoveType.Shoot, power, DateTimeOffset.UtcNow);
            _moves.Add(missMove);
            SwitchTurn();
            return GameActionResult.Shot("Aucun tir possible.", 0, replay: false, missMove, hit: false, destroyedPoint: null, trajectory: Array.Empty<GridCell>());
        }

        var protectedCells = BuildProtectedCells();
        var shot = _projectileSimulationService.SimulateShot(
            shooterPlayerId: player.Id,
            origin: origin.Value,
            power: power,
            boardWidth: _game.Width,
            boardHeight: _game.Height,
            activePointsByCell: _activePointsByCell,
            protectedCells: protectedCells);

        var landingCell = shot.Trajectory.Count > 0
            ? shot.Trajectory[^1]
            : new GridCell(player.Id == _turnOrder[0].Id ? 0 : _game.Width - 1, selectedRow);

        var move = new Move(_nextMoveId++, _game.Id, player, landingCell.X, landingCell.Y, MoveType.Shoot, power, DateTimeOffset.UtcNow);
        _moves.Add(move);

        var destroyedOpponentPoint = shot.Hit && shot.DestroyedPoint is not null && shot.DestroyedCell is not null;
        if (destroyedOpponentPoint)
        {
            shot.DestroyedPoint!.Destroy();
            _activePointsByCell.Remove(shot.DestroyedCell!.Value);
        }

        var restoredOwnPoint = TryRestoreOwnedPoint(player, landingCell);

        if (!destroyedOpponentPoint)
        {
            SwitchTurn();
            var missMessage = restoredOwnPoint ? "Point restauré." : shot.Message;
            return GameActionResult.Shot(missMessage, 0, replay: false, move, hit: false, destroyedPoint: null, trajectory: shot.Trajectory);
        }

        var scoreGained = 0;

        SwitchTurn();
        var hitMessage = restoredOwnPoint
            ? "Point adverse détruit et point restauré."
            : "Point adverse détruit.";
        return GameActionResult.Shot(hitMessage, scoreGained, replay: false, move, hit: true, destroyedPoint: shot.DestroyedPoint, trajectory: shot.Trajectory);
    }

    private bool TryRestoreOwnedPoint(Player player, GridCell landingCell)
    {
        if (!_ownershipHistory.Contains((player.Id, landingCell)))
        {
            return false;
        }

        if (_activePointsByCell.ContainsKey(landingCell))
        {
            return false;
        }

        var restored = new DomainPoint(_nextPointId++, _game.Id, player, landingCell.X, landingCell.Y, isDestroyed: false);
        _activePointsByCell[landingCell] = restored;
        _ownershipHistory.Add((player.Id, landingCell));
        return true;
    }

    private MoveValidationResult ValidateShot(Player player, int lineY, int power)
    {
        if (_game.Status != GameStatus.InProgress)
        {
            return MoveValidationResult.Invalid("Game is not in progress.");
        }

        if (player is null)
        {
            return MoveValidationResult.Invalid("Player is required.");
        }

        if (player.Id != CurrentPlayer.Id)
        {
            return MoveValidationResult.Invalid("It is not this player's turn.");
        }

        if (lineY < 0 || lineY >= _game.Height)
        {
            return MoveValidationResult.Invalid("Ligne de tir invalide.");
        }

        if (power <= 0)
        {
            return MoveValidationResult.Invalid("Power must be greater than zero.");
        }

        return MoveValidationResult.Valid();
    }

    private int SetCannonRow(long playerId, int row)
    {
        var clamped = Math.Clamp(row, 0, _game.Height - 1);
        _cannonRowsByPlayerId[playerId] = clamped;
        return clamped;
    }

    public MoveValidationResult ValidateShotTarget(Player player, int targetX, int targetY)
    {
        var baseValidation = ValidateMove(player, targetX, targetY, MoveType.Shoot, power: 1);
        if (!baseValidation.IsValid)
        {
            return baseValidation;
        }

        var cannonRow = GetCannonRow(player.Id);
        if (targetY != cannonRow)
        {
            return MoveValidationResult.Invalid("Le canon ne tire qu'en ligne droite: la cible doit être sur la même ligne que le canon.");
        }

        var targetCell = new GridCell(targetX, targetY);
        if (!_activePointsByCell.TryGetValue(targetCell, out var targetPoint) || targetPoint.IsDestroyed)
        {
            return MoveValidationResult.Invalid("La cible doit être un point existant.");
        }

        if (targetPoint.PlayerId == player.Id)
        {
            return MoveValidationResult.Invalid("La cible doit être un point adverse.");
        }

        var protectedCells = BuildProtectedCells();
        if (protectedCells.Contains(targetCell))
        {
            return MoveValidationResult.Invalid("Impossible de viser un point appartenant à une ligne validée.");
        }

        return MoveValidationResult.Valid();
    }

    public void SwitchTurn()
    {
        _turnIndex = (_turnIndex + 1) % _turnOrder.Length;
        _game.SetCurrentPlayer(CurrentPlayer);
    }

    public MoveValidationResult ValidateMove(Player player, int x, int y, MoveType moveType, int power = 0)
    {
        if (_game.Status != GameStatus.InProgress)
        {
            return MoveValidationResult.Invalid("Game is not in progress.");
        }

        if (player is null)
        {
            return MoveValidationResult.Invalid("Player is required.");
        }

        if (player.Id != CurrentPlayer.Id)
        {
            return MoveValidationResult.Invalid("It is not this player's turn.");
        }

        if (x < 0 || y < 0 || x >= _game.Width || y >= _game.Height)
        {
            return MoveValidationResult.Invalid("Coordinates are outside the board.");
        }

        var targetCell = new GridCell(x, y);

        if (moveType == MoveType.Place)
        {
            if (_activePointsByCell.ContainsKey(targetCell))
            {
                return MoveValidationResult.Invalid("Cannot place point on an occupied cell.");
            }

            return MoveValidationResult.Valid();
        }

        if (moveType == MoveType.Shoot)
        {
            if (power <= 0)
            {
                return MoveValidationResult.Invalid("Power must be greater than zero.");
            }

            return MoveValidationResult.Valid();
        }

        return MoveValidationResult.Invalid("Unknown move type.");
    }

    private static int CalculateLineScore(IReadOnlyCollection<ValidatedLine> lines)
    {
        return lines.Count;
    }

    private IReadOnlyCollection<ValidatedLine> DetectAndRegisterLines(Player player, GridCell anchor)
    {
        var newLines = _lineDetectionService.DetectValidLines(
            _game.Id,
            player.Id,
            anchor,
            _activePointsByCell,
            _validatedLines,
            _nextLineId);

        foreach (var line in newLines)
        {
            _validatedLines.Add(line);
            _game.AddLine(line.Id, player, line.PointCount, isValidated: true, validatedAt: DateTimeOffset.UtcNow);
        }

        if (newLines.Count > 0)
        {
            _nextLineId = newLines.Max(l => l.Id) + 1;
        }

        return newLines;
    }

    private GridCell? TryGetCannonOrigin(long playerId)
    {
        var row = GetCannonRow(playerId);
        if (playerId == _turnOrder[0].Id)
        {
            return new GridCell(-1, row);
        }

        if (playerId == _turnOrder[1].Id)
        {
            return new GridCell(_game.Width, row);
        }

        return null;
    }

    private HashSet<GridCell> BuildProtectedCells()
    {
        var protectedCells = new HashSet<GridCell>();
        foreach (var line in _validatedLines)
        {
            foreach (var cell in line.Cells)
            {
                protectedCells.Add(cell);
            }
        }

        return protectedCells;
    }
}
