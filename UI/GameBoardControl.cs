using jeuPoint.Services.Rules;
using System.ComponentModel;

namespace jeuPoint.UI;

public sealed partial class GameBoardControl : Control
{
    private const int SideHudWidth = 48;
    private const int BoardPadding = 10;

    private readonly Dictionary<long, Color> _playerColors = new()
    {
        [1] = Color.FromArgb(227, 73, 152),
        [2] = Color.FromArgb(173, 56, 130)
    };

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int GridWidth { get; set; } = 16;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int GridHeight { get; set; } = 12;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyDictionary<GridCell, long> PointsByCell { get; set; } = new Dictionary<GridCell, long>();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyCollection<ValidatedLine> ValidatedLines { get; set; } = Array.Empty<ValidatedLine>();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long CurrentPlayerId { get; set; } = 1;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PlayerOneCannonRow { get; set; } = 0;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PlayerTwoCannonRow { get; set; } = 0;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GridCell? LockedTarget { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GridCell? PredictedImpactCell { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long? ProjectileShooterPlayerId { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GridCell? ProjectileTargetCell { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<GridCell> ProjectilePath { get; set; } = Array.Empty<GridCell>();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float ProjectileProgress { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GameInteractionMode Mode { get; set; } = GameInteractionMode.Place;

    public event EventHandler<GridCell>? CellClicked;

    public GameBoardControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(255, 241, 247);
        Cursor = Cursors.Cross;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var boardRect = CalculateBoardRectangle();
        DrawBoardBackground(e.Graphics, boardRect);
        DrawGrid(e.Graphics, boardRect);
        DrawValidatedLines(e.Graphics, boardRect);
        DrawPredictedImpact(e.Graphics, boardRect);
        DrawLockedTarget(e.Graphics, boardRect);
        DrawProjectile(e.Graphics, boardRect);
        DrawCannons(e.Graphics, boardRect);
        DrawPoints(e.Graphics, boardRect);
        DrawModeBadge(e.Graphics);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        var boardRect = CalculateBoardRectangle();
        if (!boardRect.Contains(e.Location))
        {
            return;
        }

        if (GridWidth <= 1 || GridHeight <= 1)
        {
            return;
        }

        var stepX = boardRect.Width / (GridWidth - 1f);
        var stepY = boardRect.Height / (GridHeight - 1f);

        if (stepX <= 0 || stepY <= 0)
        {
            return;
        }

        var x = (int)Math.Round((e.X - boardRect.Left) / stepX, MidpointRounding.AwayFromZero);
        var y = (int)Math.Round((e.Y - boardRect.Top) / stepY, MidpointRounding.AwayFromZero);

        x = Math.Clamp(x, 0, GridWidth - 1);
        y = Math.Clamp(y, 0, GridHeight - 1);

        if (x < 0 || y < 0 || x >= GridWidth || y >= GridHeight)
        {
            return;
        }

        CellClicked?.Invoke(this, new GridCell(x, y));
    }

    private RectangleF CalculateBoardRectangle()
    {
        var availableWidth = Math.Max(100, Width - (SideHudWidth * 2) - (BoardPadding * 2));
        var availableHeight = Math.Max(100, Height - (BoardPadding * 2));

        var xSegments = Math.Max(1, GridWidth - 1);
        var ySegments = Math.Max(1, GridHeight - 1);

        var cellSize = Math.Min(availableWidth / xSegments, availableHeight / ySegments);
        var boardWidth = cellSize * xSegments;
        var boardHeight = cellSize * ySegments;

        var left = (Width - boardWidth) / 2f;
        var top = (Height - boardHeight) / 2f;

        return new RectangleF(left, top, boardWidth, boardHeight);
    }
}
