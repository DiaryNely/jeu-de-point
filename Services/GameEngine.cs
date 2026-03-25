using jeuPoint.Data;
using jeuPoint.Models;

namespace jeuPoint.Services;

public sealed class GameEngine : IGameEngine
{
    private readonly IGameSessionRepository _gameSessionRepository;
    private GameState _currentState;

    public GameEngine(IGameSessionRepository gameSessionRepository)
    {
        _gameSessionRepository = gameSessionRepository;
        _currentState = new GameState(targetScore: 10);
    }

    public GameState CurrentState => _currentState;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gameSessionRepository.InitializeAsync(cancellationToken);
    }

    public GameState StartNewGame(int targetScore)
    {
        _currentState = new GameState(targetScore);
        return _currentState;
    }

    public GameState AddPointToPlayer(int playerIndex)
    {
        switch (playerIndex)
        {
            case 1:
                _currentState.AddPointToPlayerOne();
                break;
            case 2:
                _currentState.AddPointToPlayerTwo();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(playerIndex), "Player index must be 1 or 2.");
        }

        return _currentState;
    }

    public async Task PersistCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentState.IsFinished || string.IsNullOrWhiteSpace(_currentState.WinnerName))
        {
            return;
        }

        var session = new GameSession
        {
            PlayedAtUtc = DateTime.UtcNow,
            WinnerName = _currentState.WinnerName,
            PlayerOneScore = _currentState.PlayerOneScore,
            PlayerTwoScore = _currentState.PlayerTwoScore,
            TargetScore = _currentState.TargetScore
        };

        await _gameSessionRepository.SaveSessionAsync(session, cancellationToken);
    }

    public Task<IReadOnlyList<GameSession>> GetRecentSessionsAsync(int limit, CancellationToken cancellationToken = default)
    {
        return _gameSessionRepository.GetRecentSessionsAsync(limit, cancellationToken);
    }
}
