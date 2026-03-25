using jeuPoint.Services.Rules;

namespace jeuPoint.UI;

public sealed partial class GameBoardControl
{
    private static void DrawBoardBackground(Graphics graphics, RectangleF boardRect)
    {
        using var backgroundBrush = new SolidBrush(Color.FromArgb(255, 252, 254));
        graphics.FillRectangle(backgroundBrush, boardRect);

        using var borderPen = new Pen(Color.FromArgb(236, 194, 217), 1.5f);
        graphics.DrawRectangle(borderPen, boardRect.X, boardRect.Y, boardRect.Width, boardRect.Height);
    }

    private void DrawGrid(Graphics graphics, RectangleF boardRect)
    {
        if (GridWidth <= 1 || GridHeight <= 1)
        {
            return;
        }

        var stepX = boardRect.Width / (GridWidth - 1f);
        var stepY = boardRect.Height / (GridHeight - 1f);

        using var gridPen = new Pen(Color.FromArgb(245, 216, 231), 1f);

        for (var x = 0; x < GridWidth; x++)
        {
            var px = boardRect.Left + (x * stepX);
            graphics.DrawLine(gridPen, px, boardRect.Top, px, boardRect.Bottom);
        }

        for (var y = 0; y < GridHeight; y++)
        {
            var py = boardRect.Top + (y * stepY);
            graphics.DrawLine(gridPen, boardRect.Left, py, boardRect.Right, py);
        }
    }

    private void DrawPoints(Graphics graphics, RectangleF boardRect)
    {
        if (PointsByCell.Count == 0)
        {
            return;
        }

        if (GridWidth <= 1 || GridHeight <= 1)
        {
            return;
        }

        var stepX = boardRect.Width / (GridWidth - 1f);
        var stepY = boardRect.Height / (GridHeight - 1f);
        var diameter = Math.Max(8f, Math.Min(stepX, stepY) * 0.5f);

        foreach (var (cell, playerId) in PointsByCell)
        {
            var centerX = boardRect.Left + (cell.X * stepX);
            var centerY = boardRect.Top + (cell.Y * stepY);
            var rect = new RectangleF(centerX - (diameter / 2f), centerY - (diameter / 2f), diameter, diameter);

            var color = _playerColors.TryGetValue(playerId, out var playerColor) ? playerColor : Color.DimGray;
            using var brush = new SolidBrush(color);
            using var pen = new Pen(Color.White, 1.5f);

            graphics.FillEllipse(brush, rect);
            graphics.DrawEllipse(pen, rect);
        }
    }

    private void DrawValidatedLines(Graphics graphics, RectangleF boardRect)
    {
        if (ValidatedLines.Count == 0 || GridWidth <= 1 || GridHeight <= 1)
        {
            return;
        }

        var stepX = boardRect.Width / (GridWidth - 1f);
        var stepY = boardRect.Height / (GridHeight - 1f);
        var lineThickness = Math.Max(2f, Math.Min(stepX, stepY) * 0.16f);

        foreach (var line in ValidatedLines)
        {
            var orderedCells = OrderCells(line).ToArray();
            if (orderedCells.Length < 2)
            {
                continue;
            }

            var start = orderedCells.First();
            var end = orderedCells.Last();

            var startPoint = new PointF(boardRect.Left + (start.X * stepX), boardRect.Top + (start.Y * stepY));
            var endPoint = new PointF(boardRect.Left + (end.X * stepX), boardRect.Top + (end.Y * stepY));

            var color = _playerColors.TryGetValue(line.PlayerId, out var playerColor) ? playerColor : Color.DimGray;
            using var pen = new Pen(Color.FromArgb(180, color), lineThickness)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };

            graphics.DrawLine(pen, startPoint, endPoint);
        }
    }

    private static IEnumerable<GridCell> OrderCells(ValidatedLine line)
    {
        return line.Direction switch
        {
            LineDirection.Horizontal => line.Cells.OrderBy(c => c.X).ThenBy(c => c.Y),
            LineDirection.Vertical => line.Cells.OrderBy(c => c.Y).ThenBy(c => c.X),
            LineDirection.DiagonalDown => line.Cells.OrderBy(c => c.X).ThenBy(c => c.Y),
            LineDirection.DiagonalUp => line.Cells.OrderBy(c => c.X).ThenByDescending(c => c.Y),
            _ => line.Cells.OrderBy(c => c.X).ThenBy(c => c.Y)
        };
    }
}
