namespace jeuPoint.Models;

public class Player
{
    private readonly List<Game> _currentGames = new();
    private readonly List<Move> _moves = new();
    private readonly List<Point> _points = new();
    private readonly List<Line> _lines = new();

    public long Id { get; private set; }

    public string Name { get; private set; }

    public int Score { get; private set; }

    public IReadOnlyCollection<Game> CurrentGames => _currentGames;

    public IReadOnlyCollection<Move> Moves => _moves;

    public IReadOnlyCollection<Point> Points => _points;

    public IReadOnlyCollection<Line> Lines => _lines;

    public Player(long id, string name, int score = 0)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Player name is required.", nameof(name));
        }

        if (score < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(score));
        }

        Id = id;
        Name = name.Trim();
        Score = score;
    }

    public Player(string name) : this(1, name, 0)
    {
    }

    public void UpdateScore(int newScore)
    {
        if (newScore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newScore));
        }

        Score = newScore;
    }

    public void AddScore(int value)
    {
        var score = Score + value;
        if (score < 0)
        {
            throw new InvalidOperationException("Score cannot become negative.");
        }

        Score = score;
    }

    public void LinkCurrentGame(Game game)
    {
        if (!_currentGames.Contains(game))
        {
            _currentGames.Add(game);
        }
    }

    internal void LinkMove(Move move)
    {
        if (!_moves.Contains(move))
        {
            _moves.Add(move);
        }
    }

    internal void LinkPoint(Point point)
    {
        if (!_points.Contains(point))
        {
            _points.Add(point);
        }
    }

    internal void LinkLine(Line line)
    {
        if (!_lines.Contains(line))
        {
            _lines.Add(line);
        }
    }

    public override string ToString()
    {
        return $"Player {{ Id = {Id}, Name = {Name}, Score = {Score} }}";
    }
}
