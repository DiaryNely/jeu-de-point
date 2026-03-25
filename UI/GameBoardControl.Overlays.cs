using jeuPoint.Services.Rules;

namespace jeuPoint.UI;

public sealed partial class GameBoardControl
{
    private void DrawPredictedImpact(Graphics graphics, RectangleF boardRect)
    {
        if (PredictedImpactCell is null || GridWidth <= 1 || GridHeight <= 1)
        {
            return;
        }

        var stepX = boardRect.Width / (GridWidth - 1f);
        var stepY = boardRect.Height / (GridHeight - 1f);

        var center = new PointF(
            boardRect.Left + (PredictedImpactCell.Value.X * stepX),
            boardRect.Top + (PredictedImpactCell.Value.Y * stepY));

        var radius = Math.Max(8f, Math.Min(stepX, stepY) * 0.45f);
        using var ringPen = new Pen(Color.FromArgb(255, 193, 7), 2f);
        using var crossPen = new Pen(Color.FromArgb(255, 111, 0), 1.8f);

        graphics.DrawEllipse(ringPen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        graphics.DrawLine(crossPen, center.X - radius * 0.6f, center.Y, center.X + radius * 0.6f, center.Y);
        graphics.DrawLine(crossPen, center.X, center.Y - radius * 0.6f, center.X, center.Y + radius * 0.6f);
    }

    private void DrawLockedTarget(Graphics graphics, RectangleF boardRect)
    {
        if (LockedTarget is null || GridWidth <= 1 || GridHeight <= 1)
        {
            return;
        }

        var stepX = boardRect.Width / (GridWidth - 1f);
        var stepY = boardRect.Height / (GridHeight - 1f);
        var center = new PointF(boardRect.Left + (LockedTarget.Value.X * stepX), boardRect.Top + (LockedTarget.Value.Y * stepY));

        var outerRadius = Math.Max(8f, Math.Min(stepX, stepY) * 0.45f);
        var innerRadius = Math.Max(5f, outerRadius * 0.55f);

        using var outerPen = new Pen(Color.FromArgb(255, 193, 7), 2f);
        using var innerPen = new Pen(Color.FromArgb(255, 111, 0), 1.5f);

        graphics.DrawEllipse(outerPen, center.X - outerRadius, center.Y - outerRadius, outerRadius * 2, outerRadius * 2);
        graphics.DrawEllipse(innerPen, center.X - innerRadius, center.Y - innerRadius, innerRadius * 2, innerRadius * 2);
    }

    private void DrawProjectile(Graphics graphics, RectangleF boardRect)
    {
        if (ProjectileShooterPlayerId is null || GridWidth <= 1 || GridHeight <= 1)
        {
            return;
        }

        var stepX = boardRect.Width / (GridWidth - 1f);
        var stepY = boardRect.Height / (GridHeight - 1f);

        if (ProjectileTargetCell is null)
        {
            return;
        }

        var shooterRow = ProjectileShooterPlayerId.Value == 1 ? PlayerOneCannonRow : PlayerTwoCannonRow;
        var start = GetCannonMuzzle(boardRect, ProjectileShooterPlayerId.Value, shooterRow, GridHeight);
        var target = new PointF(
            boardRect.Left + (ProjectileTargetCell.Value.X * stepX),
            boardRect.Top + (ProjectileTargetCell.Value.Y * stepY));

        var t = Math.Clamp(ProjectileProgress, 0f, 1f);
        var center = GetParabolicProjectilePoint(boardRect, start, target, t);

        var radius = Math.Max(5f, Math.Min(stepX, stepY) * 0.3f);

        using var brush = new SolidBrush(Color.FromArgb(255, 87, 34));
        using var pen = new Pen(Color.FromArgb(183, 28, 28), 1.5f);

        graphics.FillEllipse(brush, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
    }

    private static PointF GetParabolicProjectilePoint(RectangleF boardRect, PointF start, PointF target, float progress)
    {
        var horizontalDistance = Math.Abs(target.X - start.X);
        var baseArc = Math.Max(24f, horizontalDistance * 0.22f);

        var upRoom = start.Y - boardRect.Top;
        var downRoom = boardRect.Bottom - start.Y;
        var arcUpward = upRoom >= downRoom;
        var availableRoom = arcUpward ? upRoom : downRoom;
        var arcHeight = Math.Min(baseArc, Math.Max(12f, availableRoom * 0.85f));

        var control = new PointF(
            (start.X + target.X) * 0.5f,
            arcUpward ? start.Y - arcHeight : start.Y + arcHeight);

        var oneMinusT = 1f - progress;
        var x = (oneMinusT * oneMinusT * start.X)
            + (2f * oneMinusT * progress * control.X)
            + (progress * progress * target.X);
        var y = (oneMinusT * oneMinusT * start.Y)
            + (2f * oneMinusT * progress * control.Y)
            + (progress * progress * target.Y);

        if (float.IsNaN(x) || float.IsNaN(y))
        {
            return target;
        }

        return new PointF(x, y);
    }

    private static PointF GetCannonMuzzle(RectangleF boardRect, long ownerPlayerId, int cannonRow, int gridHeight)
    {
        var ySegments = Math.Max(1, gridHeight - 1);
        var stepY = boardRect.Height / ySegments;
        var clampedRow = Math.Clamp(cannonRow, 0, Math.Max(0, gridHeight - 1));
        var centerY = boardRect.Top + (clampedRow * stepY);

        var center = ownerPlayerId == 1
            ? new PointF(boardRect.Left - 35f, centerY)
            : new PointF(boardRect.Right + 35f, centerY);

        var direction = ownerPlayerId == 1 ? 1f : -1f;
        var pivot = new PointF(center.X + (direction * 6f), center.Y - 2f);
        return new PointF(pivot.X + (direction * 30f), pivot.Y);
    }

    private void DrawCannons(Graphics graphics, RectangleF boardRect)
    {
        var ySegments = Math.Max(1, GridHeight - 1);
        var stepY = boardRect.Height / ySegments;

        var leftRow = Math.Clamp(PlayerOneCannonRow, 0, Math.Max(0, GridHeight - 1));
        var rightRow = Math.Clamp(PlayerTwoCannonRow, 0, Math.Max(0, GridHeight - 1));

        var leftCenter = new PointF(boardRect.Left - 35f, boardRect.Top + (leftRow * stepY));
        var rightCenter = new PointF(boardRect.Right + 35f, boardRect.Top + (rightRow * stepY));

        DrawCannon(graphics, leftCenter, isLeft: true, ownerPlayerId: 1);
        DrawCannon(graphics, rightCenter, isLeft: false, ownerPlayerId: 2);
    }

    private void DrawCannon(Graphics graphics, PointF center, bool isLeft, long ownerPlayerId)
    {
        var color = _playerColors.TryGetValue(ownerPlayerId, out var playerColor) ? playerColor : Color.DimGray;
        var isCurrent = ownerPlayerId == CurrentPlayerId;

        var direction = isLeft ? 1f : -1f;
        var mainColor = isCurrent ? color : Color.FromArgb(155, color);
        var darkMetal = Color.FromArgb(55, 60, 66);
        var woodColor = Color.FromArgb(125, 88, 55);

        var wheelRadius = 9f;
        var wheelCenter = new PointF(center.X - (direction * 10f), center.Y + 10f);

        var carriageWidth = 24f;
        var carriageRect = new RectangleF(
            isLeft ? center.X - 8f : center.X - carriageWidth + 8f,
            center.Y + 4f,
            carriageWidth,
            7f);

        var pivot = new PointF(center.X + (direction * 6f), center.Y - 2f);
        var muzzle = new PointF(pivot.X + (direction * 30f), pivot.Y);

        using var wheelBrush = new SolidBrush(Color.FromArgb(82, 68, 52));
        using var wheelHubBrush = new SolidBrush(Color.FromArgb(224, 179, 97));
        using var carriageBrush = new SolidBrush(woodColor);
        using var carriagePen = new Pen(Color.FromArgb(80, 58, 35), 1f);
        using var barrelShadowPen = new Pen(Color.FromArgb(35, 35, 35), 9f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        using var barrelPen = new Pen(mainColor, 7f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        using var ringPen = new Pen(darkMetal, 2f);
        using var pivotBrush = new SolidBrush(darkMetal);

        graphics.FillEllipse(wheelBrush, wheelCenter.X - wheelRadius, wheelCenter.Y - wheelRadius, wheelRadius * 2, wheelRadius * 2);
        graphics.FillEllipse(wheelHubBrush, wheelCenter.X - 3f, wheelCenter.Y - 3f, 6f, 6f);

        graphics.FillRectangle(carriageBrush, carriageRect);
        graphics.DrawRectangle(carriagePen, carriageRect.X, carriageRect.Y, carriageRect.Width, carriageRect.Height);

        var shadowOffset = 1.2f;
        graphics.DrawLine(barrelShadowPen, pivot.X, pivot.Y + shadowOffset, muzzle.X, muzzle.Y + shadowOffset);
        graphics.DrawLine(barrelPen, pivot, muzzle);

        graphics.FillEllipse(pivotBrush, pivot.X - 3.2f, pivot.Y - 3.2f, 6.4f, 6.4f);
        graphics.DrawEllipse(ringPen, muzzle.X - 4.2f, muzzle.Y - 4.2f, 8.4f, 8.4f);
    }

    private void DrawModeBadge(Graphics graphics)
    {
        var badgeText = Mode == GameInteractionMode.Place ? "MODE : POSE" : "MODE : TIR";
        var badgeRect = new RectangleF(12f, 12f, 140f, 30f);

        using var brush = new SolidBrush(Color.FromArgb(33, 37, 41));
        using var textBrush = new SolidBrush(Color.White);
        using var font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);

        graphics.FillRectangle(brush, badgeRect);
        graphics.DrawString(badgeText, font, textBrush, badgeRect, new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        });
    }
}
