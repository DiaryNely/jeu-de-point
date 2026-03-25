namespace jeuPoint.Models;

public sealed class GameState
{
    public int PlayerOneScore { get; private set; }

    public int PlayerTwoScore { get; private set; }

    public int TargetScore { get; private set; }

    public bool IsFinished { get; private set; }

    public string? WinnerName { get; private set; }

    public GameState(int targetScore)
    {
        if (targetScore <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetScore), "Target score must be greater than zero.");
        }

        TargetScore = targetScore;
    }

    public void AddPointToPlayerOne()
    {
        if (IsFinished)
        {
            return;
        }

        PlayerOneScore++;
        EvaluateWinner();
    }

    public void AddPointToPlayerTwo()
    {
        if (IsFinished)
        {
            return;
        }

        PlayerTwoScore++;
        EvaluateWinner();
    }

    public void Reset(int targetScore)
    {
        if (targetScore <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetScore), "Target score must be greater than zero.");
        }

        PlayerOneScore = 0;
        PlayerTwoScore = 0;
        TargetScore = targetScore;
        IsFinished = false;
        WinnerName = null;
    }

    private void EvaluateWinner()
    {
        if (PlayerOneScore >= TargetScore)
        {
            IsFinished = true;
            WinnerName = "Joueur 1";
            return;
        }

        if (PlayerTwoScore >= TargetScore)
        {
            IsFinished = true;
            WinnerName = "Joueur 2";
        }
    }
}
